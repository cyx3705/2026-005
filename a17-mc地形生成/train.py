"""§3  训练

数据全部常驻显存（48 个 region 的三通道图才 ~50MB），patch 在 GPU 上现切，
没有 DataLoader、没有 host→device 拷贝，单步开销几乎全在 U-Net 上。

用法：
    venv\\Scripts\\python train.py --steps 4000            # 今晚的里程碑
    venv\\Scripts\\python train.py --steps 30000 --resume  # 挂机训满
"""

from __future__ import annotations

import argparse
import json
import random
import time
from pathlib import Path

import numpy as np
import torch

from model import Codec, Diffusion, TerrainUNet, make_cond_mask

ROOT = Path(__file__).resolve().parent
OUT = ROOT / "outputs"


class GPUPatches:
    """把整个数据集搬上显存，按预先枚举好的窗口左上角随机切 patch。"""

    def __init__(self, path: Path, device, patch: int):
        d = np.load(path, allow_pickle=True)
        self.norm = json.loads((ROOT / "norm.json").read_text("utf-8"))
        self.patch = patch
        self.W = d["H"].shape[-1]
        self.H = torch.from_numpy(d["H"].astype(np.float32)).to(device).reshape(-1)
        self.S = torch.from_numpy(d["S"].astype(np.int64)).to(device).reshape(-1)
        self.B = torch.from_numpy(d["B"].astype(np.int64)).to(device).reshape(-1)
        c = d["corners"].astype(np.int64)
        keep = (c[:, 1] + patch <= self.W) & (c[:, 2] + patch <= self.W)
        c = c[keep]
        self.base = torch.from_numpy(
            c[:, 0] * self.W * self.W + c[:, 1] * self.W + c[:, 2]).to(device)
        dz, dx = torch.meshgrid(torch.arange(patch, device=device),
                                torch.arange(patch, device=device), indexing="ij")
        self.off = (dz * self.W + dx).reshape(-1)
        self.h_mean, self.h_std = self.norm["h_mean"], self.norm["h_std"]
        self.n_block = len(self.norm["block_names"])
        self.n_biome = len(self.norm["biome_names"])
        print(f"数据集：{len(self.base)} 个 {patch}x{patch} 窗口，"
              f"方块词表 {self.n_block}，biome {self.n_biome}")

    def sample(self, n: int, aug: bool = True):
        i = torch.randint(0, len(self.base), (n,), device=self.base.device)
        idx = (self.base[i][:, None] + self.off[None, :])
        p = self.patch
        h = self.H[idx].view(n, 1, p, p)
        h = ((h - self.h_mean) / self.h_std).clamp(-5.0, 5.0)
        s = self.S[idx].view(n, p, p)
        b = self.B[idx].view(n, p, p)
        if aug:                                   # D4 对称：地形没有固定朝向
            k = random.randrange(4)               # 用 python random，避免 .item() 触发同步
            if k:
                h, s, b = torch.rot90(h, k, (2, 3)), torch.rot90(s, k, (1, 2)), torch.rot90(b, k, (1, 2))
            if random.random() < 0.5:
                h, s, b = h.flip(3), s.flip(2), b.flip(2)
        return h, s, b


class EMA:
    """权重滑动平均。用 _foreach_* 一次性更新整份参数——每步重建 state_dict()
    再逐个 tensor 更新，在这种小模型上比 U-Net 前向本身还贵。"""

    def __init__(self, model, decay=0.999):
        self.decay = decay
        self.step = 0
        self.params = list(model.parameters())
        self.shadow = [p.detach().clone().float() for p in self.params]
        self.buffers = [b.detach().clone() for b in model.buffers()]

    @torch.no_grad()
    def update(self):
        # warmup：decay=0.999 的有效窗口是 1000 步，前几千步影子权重里还混着
        # 大量随机初始化的权重（step 2000 时占 0.999^2000 = 13.5%），采样直接出噪声。
        self.step += 1
        d = min(self.decay, (1 + self.step) / (10 + self.step))
        torch._foreach_mul_(self.shadow, d)
        torch._foreach_add_(self.shadow, self.params, alpha=1 - d)

    @torch.no_grad()
    def copy_to(self, model):
        for p, s in zip(model.parameters(), self.shadow):
            p.copy_(s)
        for d, s in zip(model.buffers(), self.buffers):
            d.copy_(s)


def save_grid(h, s, b, path: Path, norm, title=""):
    from viz import setup
    plt = setup()

    n = h.shape[0]
    hd = h[:, 0] * norm["h_std"] + norm["h_mean"]
    # 色阶按真实地形的实际范围收紧：绝大多数地表在海平面 62 到 110 之间
    vmin, vmax = 56, 112
    fig, ax = plt.subplots(4, n, figsize=(1.9 * n, 8.2))
    for i in range(n):
        ax[0, i].imshow(hd[i], cmap="terrain", vmin=vmin, vmax=vmax)
        gz, gx = np.gradient(hd[i].astype(np.float32))
        ax[1, i].imshow(np.clip(0.5 + 0.5 * (gx + gz) / 3.0, 0, 1), cmap="gray")
        ax[2, i].imshow(s[i], cmap="tab20", vmin=0, vmax=19)
        ax[3, i].imshow(b[i], cmap="tab20b", vmin=0, vmax=len(norm["biome_names"]) - 1)
        for r in range(4):
            ax[r, i].set_xticks([]); ax[r, i].set_yticks([])
    for r, nm in enumerate(["height", "hillshade", "block", "biome"]):
        ax[r, 0].set_ylabel(nm, fontsize=9)
    fig.suptitle(title)
    fig.tight_layout()
    fig.savefig(path, dpi=100)
    plt.close(fig)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--data", default="encoded.npz")
    ap.add_argument("--patch", type=int, default=64)
    ap.add_argument("--batch", type=int, default=32)
    ap.add_argument("--lr", type=float, default=2e-4)
    ap.add_argument("--steps", type=int, default=30000)
    ap.add_argument("--timesteps", type=int, default=1000)
    ap.add_argument("--base", type=int, default=64)
    ap.add_argument("--sample-every", type=int, default=1000)
    ap.add_argument("--ckpt-every", type=int, default=2000)
    ap.add_argument("--resume", action="store_true")
    ap.add_argument("--pred", default="v", choices=["v", "eps"],
                    help="参数化：v 在低终端 SNR 下能正确定住样本的 DC 分量")
    ap.add_argument("--eta", type=float, default=0.0,
                    help="采样随机性：0=确定性 DDIM，1=祖先采样(DDPM)，多样性更高")
    ap.add_argument("--sample-only", type=int, default=0,
                    help="不训练，只从 checkpoint 采 N 张图（用来重出交付图）")
    a = ap.parse_args()

    if a.sample_only:
        from model import load_ckpt
        dev = "cuda" if torch.cuda.is_available() else "cpu"
        net, codec, cfg, step = load_ckpt(OUT / "ckpt_last.pt", dev)  # noqa: F811
        diff = Diffusion(cfg["timesteps"], dev, cfg["pred"])
        norm = json.loads((ROOT / "norm.json").read_text("utf-8"))
        with torch.autocast("cuda", torch.bfloat16, enabled=(dev == "cuda")):
            x = diff.ddim(net, (a.sample_only, codec.C, cfg["patch"], cfg["patch"]),
                          dev, steps=50, eta=a.eta, bounds=codec.bounds())
        h, s, b = codec.decode(x.float())
        save_grid(h.cpu().numpy(), s.cpu().numpy(), b.cpu().numpy(),
                  OUT / f"sample_final_{step:06d}.png", norm,
                  f"DDIM-50 无条件采样 @ step {step}")
        print(f"→ outputs/sample_final_{step:06d}.png")
        return 0

    OUT.mkdir(exist_ok=True)
    dev = "cuda" if torch.cuda.is_available() else "cpu"
    torch.backends.cudnn.benchmark = True
    torch.backends.cuda.matmul.allow_tf32 = True

    ds = GPUPatches(ROOT / a.data, dev, a.patch)
    codec = Codec(ds.norm["block_freq"], ds.norm["biome_freq"], dev)
    net = TerrainUNet(codec.C, base=a.base).to(dev)
    diff = Diffusion(a.timesteps, dev, a.pred)
    w = codec.group_weights(dev)
    nparam = sum(p.numel() for p in net.parameters())
    print(f"通道 {codec.C}，参数量 {nparam/1e6:.1f}M，设备 {dev}")

    opt = torch.optim.AdamW(net.parameters(), lr=a.lr, weight_decay=0.0, betas=(0.9, 0.99))
    ema = EMA(net)
    step0 = 0
    ck = OUT / "ckpt_last.pt"
    if a.resume and ck.exists():
        st = torch.load(ck, map_location=dev, weights_only=False)
        net.load_state_dict(st["net"]); opt.load_state_dict(st["opt"])
        ema.shadow = [v.to(dev) for v in st["ema"]]
        step0 = st["step"]
        print(f"从 step {step0} 续训")

    # 先存一张真实 patch 做对照
    if not (OUT / "real_patches.png").exists():
        h, s, b = ds.sample(8, aug=False)
        save_grid(h.cpu().numpy(), s.cpu().numpy(), b.cpu().numpy(),
                  OUT / "real_patches.png", ds.norm, "真实 MC patch（对照）")

    losses, t0 = [], time.time()
    acc = torch.zeros((), device=dev)
    for step in range(step0 + 1, a.steps + 1):
        h, s, b = ds.sample(a.batch)
        x0 = codec.encode(h, s, b)
        t = torch.randint(0, diff.T, (a.batch,), device=dev)
        noise = torch.randn_like(x0)
        xt = diff.q_sample(x0, t, noise)
        tgt = diff.target(x0, noise, t)
        mask = make_cond_mask(a.batch, a.patch, dev)

        with torch.autocast("cuda", torch.bfloat16, enabled=(dev == "cuda")):
            pred = net(xt, t, x0, mask)
            unk = 1.0 - mask
            num = (w * (pred.float() - tgt) ** 2 * unk).sum()
            den = (w.expand_as(pred) * unk).sum().clamp(min=1.0)
            loss = num / den

        opt.zero_grad(set_to_none=True)
        loss.backward()
        torch.nn.utils.clip_grad_norm_(net.parameters(), 1.0)
        opt.step()
        ema.update()
        acc += loss.detach()                       # 累在显存里，每 100 步才同步一次

        if step % 100 == 0:
            el = time.time() - t0
            cur = (acc / 100).item()
            acc.zero_()
            losses.append(cur)
            print(f"step {step:6d}  loss {cur:.4f}  "
                  f"{(step-step0)/el:.1f} it/s  已用 {el/60:.1f} min", flush=True)
        if step % a.sample_every == 0 or step == a.steps:
            snap = TerrainUNet(codec.C, base=a.base).to(dev)
            ema.copy_to(snap); snap.eval()
            with torch.autocast("cuda", torch.bfloat16, enabled=(dev == "cuda")):
                x = diff.ddim(snap, (8, codec.C, a.patch, a.patch), dev, steps=50,
                              eta=a.eta, bounds=codec.bounds())
            hh, ss, bb = codec.decode(x.float())
            save_grid(hh.cpu().numpy(), ss.cpu().numpy(), bb.cpu().numpy(),
                      OUT / f"sample_{step:06d}.png", ds.norm, f"DDIM-50 采样 @ step {step}")
            print(f"  → outputs/sample_{step:06d}.png", flush=True)
            del snap
        if step % a.ckpt_every == 0 or step == a.steps:
            torch.save({"net": net.state_dict(), "opt": opt.state_dict(),
                        "ema": ema.shadow, "step": step,
                        "cfg": {"base": a.base, "patch": a.patch, "timesteps": a.timesteps,
                                "block_freq": codec.block_freq,
                                "biome_freq": codec.biome_freq, "pred": a.pred}},
                       ck)

    np.save(OUT / "loss.npy", np.array(losses))
    print(f"训练完成 {a.steps} step，用时 {(time.time()-t0)/60:.1f} 分钟")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
