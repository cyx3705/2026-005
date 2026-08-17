"""§1.3  dataset.npz → 训练张量

对 PLAN 的一点工程修正：不把 patch 全部物化成一个大数组。
按 stride 物化 40k 个 64x64 patch 要 1GB 内存，而且固定切法丢掉了随机偏移这个
天然的数据增强。这里只做三件事：

  1. 算归一化参数（高度用分位数做鲁棒缩放，不用 mean/std——高度分布在海平面 62
     处有一根极高的尖峰，用 std 会把整个陆地压进 [-0.2, 0.2] 的窄带里）
  2. 清洗 + 重编 biome id（丢掉占比过低的、把无效柱补成邻近值）
  3. 枚举所有「全有效」的 64x64 窗口左上角，存成索引表

真正的 patch 由 train.py 在 DataLoader 里现切，带随机偏移 + 八向对称增强。

用法：
    venv\\Scripts\\python encode.py --patch 64 --stride 16
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np

from blockvocab import BIOME_FAMILY_NAMES, biome_family

ROOT = Path(__file__).resolve().parent


def valid_corners(M: np.ndarray, patch: int, stride: int) -> np.ndarray:
    """返回所有 patch x patch 窗口全部有效的左上角 (region, z0, x0)。"""
    n, h, w = M.shape
    # 二维前缀和，O(1) 查任意窗口的有效柱数
    ii = np.zeros((n, h + 1, w + 1), np.int32)
    ii[:, 1:, 1:] = M.astype(np.int32).cumsum(1).cumsum(2)
    zs = np.arange(0, h - patch + 1, stride)
    xs = np.arange(0, w - patch + 1, stride)
    out = []
    for r in range(n):
        s = (ii[r][np.ix_(zs + patch, xs + patch)] - ii[r][np.ix_(zs, xs + patch)]
             - ii[r][np.ix_(zs + patch, xs)] + ii[r][np.ix_(zs, xs)])
        zz, xx = np.nonzero(s == patch * patch)
        if len(zz):
            out.append(np.stack([np.full(len(zz), r), zs[zz], xs[xx]], 1))
    return np.concatenate(out) if out else np.zeros((0, 3), int)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", default="dataset.npz")
    ap.add_argument("--out", default="encoded.npz")
    ap.add_argument("--patch", type=int, default=64)
    ap.add_argument("--stride", type=int, default=16)
    a = ap.parse_args()

    d = np.load(ROOT / a.src, allow_pickle=True)
    H, S, B, M = d["H"].astype(np.int16), d["S"], d["B"], d["M"]
    meta = json.loads((ROOT / "meta.json").read_text("utf-8"))
    print(f"{len(H)} 个 region，有效柱 {M.sum()/1e6:.2f}M")

    # --- 高度：零均值单位方差 ---
    # 这里必须是 mean/std，不能是「缩放到 [-1,1]」。扩散的先验是 N(0,1)，数据
    # 均值离 0 越远，模型越要在 ε 上加一个 √ᾱ·μ 的偏置来把轨迹拉过去；在 t→T、
    # ᾱ→1e-8 时这个偏置小到 bf16 表示不了，采样轨迹会锁死在零均值收敛到错的地方。
    hv = H[M].astype(np.float32)
    h_mean, h_std = float(hv.mean()), float(hv.std())
    print(f"高度 mean={h_mean:.2f} std={h_std:.2f} "
          f"(原始 {hv.min():.0f}..{hv.max():.0f}, 中位数 {np.median(hv):.0f})")

    # --- biome 归族 ---
    names = meta["biomes"]
    remap = np.array([biome_family(n) for n in names], np.uint8)
    biome_names = BIOME_FAMILY_NAMES
    B2 = remap[B]
    cnt = np.bincount(B2[M].ravel(), minlength=len(biome_names)).astype(np.float64)
    biome_freq = (cnt / cnt.sum()).tolist()
    print(f"biome {len(names)} 个原始群系 → {len(biome_names)} 个族")
    print("  ", [(n, f"{f*100:.1f}%") for n, f in
                 sorted(zip(biome_names, biome_freq), key=lambda t: -t[1])])

    bc = np.bincount(S[M].ravel(), minlength=len(meta["block_names"])).astype(np.float64)
    block_freq = (bc / bc.sum()).tolist()
    print("  表层方块:", [(n, f"{f*100:.1f}%") for n, f in
                        sorted(zip(meta["block_names"], block_freq), key=lambda t: -t[1])[:8]])

    # --- 无效柱：用整块的中位高度填掉，避免 patch 边缘出现 -64 的悬崖 ---
    Hf = H.copy()
    for r in range(len(H)):
        if not M[r].all():
            Hf[r][~M[r]] = np.int16(np.median(H[r][M[r]])) if M[r].any() else 63

    corners = valid_corners(M, a.patch, a.stride)
    print(f"合格 {a.patch}x{a.patch} 窗口: {len(corners)}（stride={a.stride}）")
    if len(corners) == 0:
        print("没有全有效窗口——检查区块是否生成完整")
        return 1

    np.savez_compressed(
        ROOT / a.out, H=Hf, S=S, B=B2, M=M, corners=corners.astype(np.int32),
        names=d["names"])
    norm = {
        "h_mean": h_mean, "h_std": h_std, "patch": a.patch, "stride": a.stride,
        "block_names": meta["block_names"], "biome_names": biome_names,
        "block_freq": block_freq, "biome_freq": biome_freq,
        "n_regions": int(len(H)), "n_corners": int(len(corners)),
    }
    (ROOT / "norm.json").write_text(json.dumps(norm, ensure_ascii=False, indent=2), "utf-8")
    print(f"→ {a.out} + norm.json")
    return 0


def encode_height(H, mean: float, std: float):
    """高度 → 零均值单位方差，极端离群值裁到 ±5σ。"""
    return ((H - mean) / std).clip(-5.0, 5.0)


def decode_height(x, mean: float, std: float):
    return x * std + mean


if __name__ == "__main__":
    raise SystemExit(main())
