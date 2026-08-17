"""§5  验证：学得像不像

不看「好不好看」，只看统计量对不对得上：
  1) 高度直方图        地形整体海拔分布
  2) 高度功率谱（径向平均） 地形粗糙度 / 各尺度能量
  3) 半方差函数        空间结构的特征尺度
  4) biome 占比
  5) 表层方块占比
  6) 高度-方块联合关系  （水在低处、雪在高处这种物理约束有没有学到）

用法：
    venv\\Scripts\\python eval.py --n 512
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


def radial_spectrum(x: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    """一批 patch 的径向平均功率谱。"""
    n, h, w = x.shape
    f = np.fft.fftshift(np.fft.fft2(x - x.mean((1, 2), keepdims=True)), axes=(1, 2))
    p = (np.abs(f) ** 2).mean(0)
    cy, cx = h // 2, w // 2
    yy, xx = np.mgrid[:h, :w]
    r = np.hypot(yy - cy, xx - cx).astype(int)
    nb = min(cy, cx)
    prof = np.array([p[r == k].mean() for k in range(1, nb)])
    return np.arange(1, nb), prof


def semivariogram(x: np.ndarray, max_lag: int = 24) -> np.ndarray:
    """γ(h) = 0.5 * E[(Z(x+h) - Z(x))^2]，两个方向平均。"""
    g = []
    for lag in range(1, max_lag + 1):
        d1 = x[:, lag:, :] - x[:, :-lag, :]
        d2 = x[:, :, lag:] - x[:, :, :-lag]
        g.append(0.5 * (d1 ** 2).mean() * 0.5 + 0.5 * (d2 ** 2).mean() * 0.5)
    return np.array(g)


@torch.no_grad()
def sample_many(net, codec, diff, n, patch, device, steps=50, batch=32, eta=0.0):
    hs, ss, bs = [], [], []
    done = 0
    bd = codec.bounds()
    while done < n:
        k = min(batch, n - done)
        with torch.autocast("cuda", torch.bfloat16, enabled=(device == "cuda")):
            x = diff.ddim(net, (k, codec.C, patch, patch), device, steps=steps,
                          eta=eta, bounds=bd)
        h, s, b = codec.decode(x.float())
        hs.append(h[:, 0].cpu().numpy()); ss.append(s.cpu().numpy()); bs.append(b.cpu().numpy())
        done += k
        print(f"  采样 {done}/{n}", end="\r", flush=True)
    print()
    return np.concatenate(hs), np.concatenate(ss), np.concatenate(bs)


def real_patches(enc: Path, n: int, patch: int, norm: dict, rng):
    d = np.load(enc, allow_pickle=True)
    H, S, B = d["H"], d["S"], d["B"]
    c = d["corners"]
    c = c[(c[:, 1] + patch <= H.shape[1]) & (c[:, 2] + patch <= H.shape[2])]
    idx = rng.choice(len(c), size=min(n, len(c)), replace=False)
    hh, ss, bb = [], [], []
    for r, z0, x0 in c[idx]:
        hh.append(H[r, z0:z0 + patch, x0:x0 + patch])
        ss.append(S[r, z0:z0 + patch, x0:x0 + patch])
        bb.append(B[r, z0:z0 + patch, x0:x0 + patch])
    h = np.stack(hh).astype(np.float32)
    h = np.clip((h - norm["h_mean"]) / norm["h_std"], -5, 5)
    return h, np.stack(ss), np.stack(bb)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--ckpt", default="outputs/ckpt_last.pt")
    ap.add_argument("--data", default="encoded.npz")
    ap.add_argument("--n", type=int, default=512)
    ap.add_argument("--steps", type=int, default=50)
    ap.add_argument("--eta", type=float, default=0.0)
    ap.add_argument("--out", default="outputs/eval.png")
    a = ap.parse_args()

    from viz import setup
    plt = setup()

    dev = "cuda" if torch.cuda.is_available() else "cpu"
    net, codec, cfg, step = load_ckpt(ROOT / a.ckpt, dev)
    diff = Diffusion(cfg["timesteps"], dev, cfg["pred"])
    norm = json.loads((ROOT / "norm.json").read_text("utf-8"))
    patch = cfg["patch"]
    hm, hs = norm["h_mean"], norm["h_std"]

    rng = np.random.default_rng(0)
    print(f"checkpoint step {step}；采样 {a.n} 个 {patch}x{patch} patch …")
    gh, gs, gb = sample_many(net, codec, diff, a.n, patch, dev, a.steps, eta=a.eta)
    rh, rs, rb = real_patches(ROOT / a.data, a.n, patch, norm, rng)

    ghd, rhd = gh * hs + hm, rh * hs + hm
    bn = [x.split(":")[-1] for x in norm["biome_names"]]
    kn = norm["block_names"]

    fig, ax = plt.subplots(2, 3, figsize=(18, 10))

    ax[0, 0].hist(rhd.ravel(), bins=80, alpha=.55, density=True, label="real")
    ax[0, 0].hist(ghd.ravel(), bins=80, alpha=.55, density=True, label="generated")
    ax[0, 0].set_title("1) height histogram"); ax[0, 0].set_xlabel("y"); ax[0, 0].legend()

    k, pr = radial_spectrum(rhd); _, pg = radial_spectrum(ghd)
    ax[0, 1].loglog(k, pr, label="real"); ax[0, 1].loglog(k, pg, label="generated")
    ax[0, 1].set_title("2) radial power spectrum (roughness)")
    ax[0, 1].set_xlabel("wavenumber"); ax[0, 1].legend()

    lags = np.arange(1, 25)
    ax[0, 2].plot(lags, semivariogram(rhd), label="real")
    ax[0, 2].plot(lags, semivariogram(ghd), label="generated")
    ax[0, 2].set_title("3) semivariogram (spatial scale)")
    ax[0, 2].set_xlabel("lag (blocks)"); ax[0, 2].legend()

    nbi = len(bn)
    fr = np.bincount(rb.ravel(), minlength=nbi) / rb.size
    fg = np.bincount(gb.ravel(), minlength=nbi) / gb.size
    o = np.argsort(-fr)[:14]
    xx = np.arange(len(o))
    ax[1, 0].bar(xx - .2, fr[o], .4, label="real"); ax[1, 0].bar(xx + .2, fg[o], .4, label="generated")
    ax[1, 0].set_xticks(xx); ax[1, 0].set_xticklabels([bn[i] for i in o], rotation=70, fontsize=7)
    ax[1, 0].set_title("4) biome proportion"); ax[1, 0].legend()

    nbl = len(kn)
    fr = np.bincount(rs.ravel(), minlength=nbl) / rs.size
    fg = np.bincount(gs.ravel(), minlength=nbl) / gs.size
    o = np.argsort(-fr)[:12]
    xx = np.arange(len(o))
    ax[1, 1].bar(xx - .2, fr[o], .4, label="real"); ax[1, 1].bar(xx + .2, fg[o], .4, label="generated")
    ax[1, 1].set_xticks(xx); ax[1, 1].set_xticklabels([kn[i] for i in o], rotation=70, fontsize=7)
    ax[1, 1].set_title("5) surface block frequency"); ax[1, 1].legend()

    # 每种表层方块的平均高度：水必须在低处、雪必须在高处
    o = np.argsort(-np.bincount(rs.ravel(), minlength=nbl))[:10]
    mr = [rhd[rs == i].mean() if (rs == i).any() else np.nan for i in o]
    mg = [ghd[gs == i].mean() if (gs == i).any() else np.nan for i in o]
    xx = np.arange(len(o))
    ax[1, 2].bar(xx - .2, mr, .4, label="real"); ax[1, 2].bar(xx + .2, mg, .4, label="generated")
    ax[1, 2].set_xticks(xx); ax[1, 2].set_xticklabels([kn[i] for i in o], rotation=70, fontsize=7)
    ax[1, 2].set_title("6) mean height per surface block"); ax[1, 2].legend()

    fig.suptitle(f"real vs generated  (n={a.n}, DDIM-{a.steps}, step {step})")
    fig.tight_layout()
    fig.savefig(ROOT / a.out, dpi=110)
    plt.close(fig)

    # 几个能一眼看的标量
    def kl(p, q):
        p = p + 1e-9; q = q + 1e-9
        p, q = p / p.sum(), q / q.sum()
        return float((p * np.log(p / q)).sum())

    rep = {
        "step": int(step),
        "height_mean": [float(rhd.mean()), float(ghd.mean())],
        "height_std": [float(rhd.std()), float(ghd.std())],
        "biome_KL": kl(np.bincount(rb.ravel(), minlength=nbi),
                       np.bincount(gb.ravel(), minlength=nbi)),
        "block_KL": kl(np.bincount(rs.ravel(), minlength=nbl),
                       np.bincount(gs.ravel(), minlength=nbl)),
        "semivar_ratio_lag8": float(semivariogram(ghd)[7] / semivariogram(rhd)[7]),
    }
    (OUT / "eval.json").write_text(json.dumps(rep, indent=2), "utf-8")
    print(json.dumps(rep, indent=2))
    print(f"→ {a.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
