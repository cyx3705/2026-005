"""§1.2  .mca → 地表三通道 [高度 / 表层方块 / 生物群系]

一个 region = 32x32 chunk = 512x512 方块，直接按 region 存成 512x512 的二维图，
这样 §1.3 切 patch 时空间邻接是天然保留的（不用再按坐标重排）。

用法：
    venv\\Scripts\\python extract.py                 # 全量
    venv\\Scripts\\python extract.py --limit 1 --png # 先跑 1 个 region 并出 PNG 肉眼检查
"""

from __future__ import annotations

import argparse
import json
import time
from collections import Counter
from pathlib import Path

import numpy as np

from blockvocab import BLOCK_NAMES, BLOCK_VOCAB_SIZE, classify
from mcio import iter_region, section_biomes, section_blocks

ROOT = Path(__file__).resolve().parent
MIN_Y, MAX_Y = -64, 320          # 1.18+ 世界高度
NY = MAX_Y - MIN_Y               # 384
NSEC = NY // 16                  # 24 个 section
NB4 = NY // 4                    # 96 层 biome（4x4x4 一格）

_unknown: Counter[str] = Counter()
_biome_ids: dict[str, int] = {}


def biome_id(name: str) -> int:
    i = _biome_ids.get(name)
    if i is None:
        i = len(_biome_ids)
        _biome_ids[name] = i
    return i


def chunk_surface(chunk: dict):
    """返回 (H[16,16] int16, S[16,16] uint8, B[16,16] uint8)，索引 [z][x]；失败返回 None。"""
    secs = chunk.get("sections")
    if not secs:
        return None

    tok = np.zeros((NY, 16, 16), np.uint8)
    kind = np.zeros((NY, 16, 16), np.uint8)
    bio = np.full((NB4, 4, 4), -1, np.int16)
    any_block = False

    for sec in secs:
        sy = int(sec.get("Y", 0))
        y0 = sy * 16 - MIN_Y
        if 0 <= y0 <= NY - 16:
            sb = section_blocks(sec)
            if sb is not None:
                pal, idx = sb
                pairs = [classify(n) for n in pal]
                for _n, (_t, _k) in zip(pal, pairs):
                    if _t == BLOCK_VOCAB_SIZE - 1 and _k == 2:
                        _unknown[_n] += 1
                lut_t = np.array([p[0] for p in pairs], np.uint8)
                lut_k = np.array([p[1] for p in pairs], np.uint8)
                tok[y0:y0 + 16] = lut_t[idx].reshape(16, 16, 16)
                kind[y0:y0 + 16] = lut_k[idx].reshape(16, 16, 16)
                any_block = True
        b0 = sy * 4 - MIN_Y // 4
        if 0 <= b0 <= NB4 - 4:
            sbi = section_biomes(sec)
            if sbi is not None:
                bpal, bidx = sbi
                blut = np.array([biome_id(n) for n in bpal], np.int16)
                bio[b0:b0 + 4] = blut[bidx].reshape(4, 4, 4)

    if not any_block:
        return None

    # 最高的「固体或流体」格 —— 植被(kind==1)被跳过，不会在高度场上打毛刺
    solid = kind >= 2
    rev = solid[::-1]
    has = rev.any(axis=0)
    if not has.any():
        return None
    top = (NY - 1) - rev.argmax(axis=0)          # [z][x] 在 0..NY-1 的索引空间

    zz, xx = np.meshgrid(np.arange(16), np.arange(16), indexing="ij")
    H = (top + MIN_Y).astype(np.int16)
    S = tok[top, zz, xx].astype(np.uint8)
    B = bio[np.clip(top // 4, 0, NB4 - 1), zz // 4, xx // 4]
    B = np.where(B < 0, 0, B).astype(np.uint8)

    H[~has] = MIN_Y
    S[~has] = 0
    return H, S, B


def process_region(path: Path):
    """返回 (H, S, B, M) 四张 512x512；M 是「该柱有效」的掩码。"""
    H = np.zeros((512, 512), np.int16)
    S = np.zeros((512, 512), np.uint8)
    B = np.zeros((512, 512), np.uint8)
    M = np.zeros((512, 512), bool)
    n = 0
    for cx, cz, chunk in iter_region(path):
        status = chunk.get("Status", "")
        if status and not str(status).endswith("full"):
            continue                                  # 只生成到一半的区块，跳过
        out = chunk_surface(chunk)
        if out is None:
            continue                                  # 未生成 / 损坏的区块，跳过
        h, s, b = out
        z0, x0 = cz * 16, cx * 16
        H[z0:z0 + 16, x0:x0 + 16] = h
        S[z0:z0 + 16, x0:x0 + 16] = s
        B[z0:z0 + 16, x0:x0 + 16] = b
        M[z0:z0 + 16, x0:x0 + 16] = True
        n += 1
    return H, S, B, M, n


def save_png(H, S, B, M, out: Path) -> None:
    from viz import setup
    plt = setup()

    fig, ax = plt.subplots(1, 3, figsize=(16, 5.6))
    hv = np.where(M, H, np.nan)
    im0 = ax[0].imshow(hv, cmap="terrain")
    ax[0].set_title(f"height  [{np.nanmin(hv):.0f}, {np.nanmax(hv):.0f}]")
    fig.colorbar(im0, ax=ax[0], fraction=0.046)
    ax[1].imshow(np.where(M, S, np.nan), cmap="tab20", vmin=0, vmax=19)
    ax[1].set_title("surface block")
    ax[2].imshow(np.where(M, B, np.nan), cmap="tab20b")
    ax[2].set_title("biome")
    for a in ax:
        a.set_xticks([]); a.set_yticks([])
    fig.suptitle(out.stem)
    fig.tight_layout()
    fig.savefig(out, dpi=110)
    plt.close(fig)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", default="raw_worlds")
    ap.add_argument("--out", default="dataset.npz")
    ap.add_argument("--limit", type=int, default=0, help="只处理前 N 个 region（先验证用）")
    ap.add_argument("--png", action="store_true", help="每个 region 出一张三通道预览图")
    ap.add_argument("--min-chunks", type=int, default=64, help="有效区块太少的 region 丢掉")
    a = ap.parse_args()

    files = sorted((ROOT / a.src).glob("**/region/*.mca"))
    if a.limit:
        files = files[:a.limit]
    if not files:
        print(f"没找到 .mca：{ROOT / a.src}/**/region/*.mca")
        return 1
    print(f"{len(files)} 个 region 文件")

    Hs, Ss, Bs, Ms, names = [], [], [], [], []
    t0 = time.time()
    for i, f in enumerate(files, 1):
        H, S, B, M, n = process_region(f)
        tag = f"{f.parent.parent.name}/{f.stem}"
        if n < a.min_chunks:
            print(f"[{i}/{len(files)}] {tag}: 仅 {n} 区块，丢弃")
            continue
        Hs.append(H); Ss.append(S); Bs.append(B); Ms.append(M); names.append(tag)
        print(f"[{i}/{len(files)}] {tag}: {n} 区块  "
              f"h={H[M].min()}..{H[M].max()}  biome种类={len(np.unique(B[M]))}", flush=True)
        if a.png:
            (ROOT / "outputs").mkdir(exist_ok=True)
            save_png(H, S, B, M, ROOT / "outputs" / f"region_{tag.replace('/', '_')}.png")

    if not Hs:
        print("没有任何合格 region")
        return 1

    np.savez_compressed(ROOT / a.out, H=np.stack(Hs), S=np.stack(Ss),
                        B=np.stack(Bs), M=np.stack(Ms), names=np.array(names))
    meta = {
        "block_names": BLOCK_NAMES,
        "biomes": [k for k, _ in sorted(_biome_ids.items(), key=lambda kv: kv[1])],
        "min_y": MIN_Y, "max_y": MAX_Y,
        "unknown_blocks": _unknown.most_common(30),
    }
    (ROOT / "meta.json").write_text(json.dumps(meta, ensure_ascii=False, indent=2), "utf-8")

    Ma = np.stack(Ms)
    print(f"\n存了 {len(Hs)} 个 region → {a.out}  "
          f"有效柱 {Ma.sum()/1e6:.2f}M  用时 {time.time()-t0:.0f}s")
    print(f"biome 种类 {len(_biome_ids)}，方块词表 {BLOCK_VOCAB_SIZE}")
    if _unknown:
        print("未识别方块 top10:", _unknown.most_common(10))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
