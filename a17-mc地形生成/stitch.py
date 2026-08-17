"""§4  无缝拼接 —— 无限地形的关键

逐块生成时，把画布上已经填好的**左边带 + 上边带**当作已知区域喂给模型
（模型在 §3 里就是按这种边带掩码训的），让它「续画」。
重叠区再做一次线性 blend，把采样残留的细微不连续抹平。

用法：
    venv\\Scripts\\python stitch.py --grid 6 6 --overlap 16 --steps 60
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np
import torch

from model import Diffusion, load_ckpt

ROOT = Path(__file__).resolve().parent
OUT = ROOT / "outputs"


def ramp(n: int, device) -> torch.Tensor:
    """线性权重 0→1，两端取不到 0/1，避免边界一格突变。"""
    return (torch.arange(n, device=device, dtype=torch.float32) + 0.5) / n


@torch.no_grad()
def generate_tiled(net, codec, diff, grid=(4, 4), patch=64, overlap=16,
                   steps=60, device="cuda", eta=0.0):
    gh, gw = grid
    stride = patch - overlap
    Hc = gh * stride + overlap
    Wc = gw * stride + overlap
    canvas = torch.zeros(1, codec.C, Hc, Wc, device=device)
    filled = torch.zeros(1, 1, Hc, Wc, device=device)

    for i in range(gh):
        for j in range(gw):
            z0, x0 = i * stride, j * stride
            sl = (slice(None), slice(None), slice(z0, z0 + patch), slice(x0, x0 + patch))
            known = canvas[sl].clone()
            mask = filled[sl].clone()

            x = diff.ddim(net, (1, codec.C, patch, patch), device, steps=steps,
                          known=known, mask=mask, eta=eta, bounds=codec.bounds())

            # 重叠区线性混合：上边带按行渐变，左边带按列渐变
            w = torch.ones(1, 1, patch, patch, device=device)
            if i > 0:
                w[:, :, :overlap, :] *= ramp(overlap, device).view(1, 1, -1, 1)
            if j > 0:
                w[:, :, :, :overlap] *= ramp(overlap, device).view(1, 1, 1, -1)
            old = canvas[sl]
            m = filled[sl]
            canvas[sl] = torch.where(m > 0, old * (1 - w) + x * w, x)
            filled[sl] = 1.0
            print(f"  tile {i*gw+j+1}/{gh*gw}", end="\r", flush=True)
    print()
    return canvas


def save_big(h, s, b, path: Path, norm, title=""):
    from viz import setup
    plt = setup()

    hd = h * norm["h_std"] + norm["h_mean"]
    # 山体阴影：只看高度色图看不出接缝，加了光照一眼就能看出有没有台阶
    gz, gx = np.gradient(hd.astype(np.float32))
    shade = np.clip(0.5 + 0.45 * (gx * 0.7 + gz * 0.7) / (1 + np.abs(gx) + np.abs(gz)), 0, 1)

    fig, ax = plt.subplots(2, 2, figsize=(15, 14))
    im = ax[0, 0].imshow(hd, cmap="terrain", vmin=56, vmax=112)
    ax[0, 0].set_title("height"); fig.colorbar(im, ax=ax[0, 0], fraction=0.046)
    ax[0, 1].imshow(shade, cmap="gray")
    ax[0, 1].set_title("hillshade (check seams)")
    ax[1, 0].imshow(s, cmap="tab20", vmin=0, vmax=19)
    ax[1, 0].set_title("surface block")
    ax[1, 1].imshow(b, cmap="tab20b", vmin=0, vmax=len(norm["biome_names"]) - 1)
    ax[1, 1].set_title("biome")
    for a in ax.ravel():
        a.set_xticks([]); a.set_yticks([])
    fig.suptitle(title)
    fig.tight_layout()
    fig.savefig(path, dpi=110)
    plt.close(fig)


def seam_score(hd: np.ndarray, patch: int, overlap: int) -> tuple[float, float]:
    """接缝处的一阶差分 vs 全图平均一阶差分。比值接近 1 就是看不出缝。"""
    stride = patch - overlap
    d = np.abs(np.diff(hd, axis=1))
    all_mean = float(d.mean())
    cols = [c for c in range(stride, hd.shape[1] - 1, stride)]
    seam = float(d[:, cols].mean()) if cols else all_mean
    return seam, all_mean


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--ckpt", default="outputs/ckpt_last.pt")
    ap.add_argument("--grid", type=int, nargs=2, default=[6, 6])
    ap.add_argument("--overlap", type=int, default=16)
    ap.add_argument("--steps", type=int, default=60)
    ap.add_argument("--eta", type=float, default=0.0)
    ap.add_argument("--out", default="outputs/stitched.png")
    ap.add_argument("--seed", type=int, default=0)
    a = ap.parse_args()

    dev = "cuda" if torch.cuda.is_available() else "cpu"
    torch.manual_seed(a.seed)
    net, codec, cfg, step = load_ckpt(ROOT / a.ckpt, dev)
    diff = Diffusion(cfg["timesteps"], dev, cfg["pred"])
    norm = json.loads((ROOT / "norm.json").read_text("utf-8"))
    patch = cfg["patch"]
    print(f"checkpoint step {step}，patch={patch}，grid={a.grid}，overlap={a.overlap}")

    with torch.autocast("cuda", torch.bfloat16, enabled=(dev == "cuda")):
        canvas = generate_tiled(net, codec, diff, tuple(a.grid), patch,
                                a.overlap, a.steps, dev, a.eta)
    h, s, b = codec.decode(canvas.float())
    hn = h[0, 0].cpu().numpy()
    sn = s[0].cpu().numpy()
    bn = b[0].cpu().numpy()

    hd = hn * norm["h_std"] + norm["h_mean"]
    seam, avg = seam_score(hd, patch, a.overlap)
    print(f"接缝检查：缝上平均高差 {seam:.3f}，全图平均 {avg:.3f}，比值 {seam/max(avg,1e-6):.2f}"
          f"（≈1 即无可见接缝）")

    save_big(hn, sn, bn, ROOT / a.out, norm,
             f"无缝拼接 {a.grid[0]}x{a.grid[1]} tiles = {hn.shape[0]}x{hn.shape[1]} blocks "
             f"| seam ratio {seam/max(avg,1e-6):.2f} | step {step}")
    np.savez_compressed(OUT / "stitched.npz", H=hd.astype(np.float32), S=sn, B=bn)
    print(f"→ {a.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
