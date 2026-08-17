"""最小依赖的 Minecraft region(.mca) + NBT 读取器。

只依赖 numpy + 标准库，不用 anvil-parser / amulet-core：
  - anvil-parser 对 1.18+ 的 sections / 3D biome 支持不全
  - amulet-core 在 Python 3.12 上装起来很痛苦

支持 1.18+ (DataVersion >= 2860) 的区块格式：
  sections[].block_states.{palette,data}   4096 格/section，非跨 long 打包
  sections[].biomes.{palette,data}         4x4x4=64 格/section
  Heightmaps.*                             256 个 9-bit 值
"""

from __future__ import annotations

import gzip
import struct
import zlib
from pathlib import Path

import numpy as np

# ---------------------------------------------------------------- NBT

TAG_END, TAG_BYTE, TAG_SHORT, TAG_INT, TAG_LONG = 0, 1, 2, 3, 4
TAG_FLOAT, TAG_DOUBLE, TAG_BYTE_ARRAY, TAG_STRING = 5, 6, 7, 8
TAG_LIST, TAG_COMPOUND, TAG_INT_ARRAY, TAG_LONG_ARRAY = 9, 10, 11, 12

_FIXED = {TAG_BYTE: 1, TAG_SHORT: 2, TAG_INT: 4, TAG_LONG: 8, TAG_FLOAT: 4, TAG_DOUBLE: 8}
_FMT = {
    TAG_BYTE: ">b", TAG_SHORT: ">h", TAG_INT: ">i",
    TAG_LONG: ">q", TAG_FLOAT: ">f", TAG_DOUBLE: ">d",
}


class NBTReader:
    """按字节游标解析 NBT。root_keep 用于在根 compound 里跳过不需要的大子树。"""

    __slots__ = ("buf", "pos")

    def __init__(self, buf: bytes):
        self.buf = buf
        self.pos = 0

    # --- 基础读取 -------------------------------------------------
    def _u1(self) -> int:
        v = self.buf[self.pos]
        self.pos += 1
        return v

    def _i2(self) -> int:
        v = struct.unpack_from(">h", self.buf, self.pos)[0]
        self.pos += 2
        return v

    def _u2(self) -> int:
        v = struct.unpack_from(">H", self.buf, self.pos)[0]
        self.pos += 2
        return v

    def _i4(self) -> int:
        v = struct.unpack_from(">i", self.buf, self.pos)[0]
        self.pos += 4
        return v

    def _string(self) -> str:
        n = self._u2()
        s = self.buf[self.pos:self.pos + n].decode("utf-8", "replace")
        self.pos += n
        return s

    # --- payload --------------------------------------------------
    def payload(self, tag: int, keep: set[str] | None = None):
        if tag in _FIXED:
            v = struct.unpack_from(_FMT[tag], self.buf, self.pos)[0]
            self.pos += _FIXED[tag]
            return v
        if tag == TAG_STRING:
            return self._string()
        if tag == TAG_BYTE_ARRAY:
            n = self._i4()
            a = np.frombuffer(self.buf, ">i1", n, self.pos)
            self.pos += n
            return a
        if tag == TAG_INT_ARRAY:
            n = self._i4()
            a = np.frombuffer(self.buf, ">i4", n, self.pos)
            self.pos += 4 * n
            return a
        if tag == TAG_LONG_ARRAY:
            n = self._i4()
            a = np.frombuffer(self.buf, ">i8", n, self.pos)
            self.pos += 8 * n
            return a
        if tag == TAG_LIST:
            item = self._u1()
            n = self._i4()
            if n <= 0:
                return []
            # 定长标量列表可以整块 frombuffer
            if item in _FIXED:
                dt = {1: ">i1", 2: ">i2", 3: ">i4", 4: ">i8", 5: ">f4", 6: ">f8"}[item]
                a = np.frombuffer(self.buf, dt, n, self.pos)
                self.pos += _FIXED[item] * n
                return a
            return [self.payload(item) for _ in range(n)]
        if tag == TAG_COMPOUND:
            out = {}
            while True:
                t = self._u1()
                if t == TAG_END:
                    return out
                name = self._string()
                if keep is not None and name not in keep:
                    self.skip(t)
                else:
                    out[name] = self.payload(t)
        raise ValueError(f"未知 NBT tag: {tag}")

    def skip(self, tag: int) -> None:
        """不构造对象，只推进游标——跳过 entities/structures 这类大子树用。

        注意：这里所有长度都必须先读进局部变量。`self.pos += self._u2()` 会先取旧的
        self.pos 再算 RHS，把 _u2 自己推进的 2 字节吞掉，游标当场跑飞。
        """
        if tag in _FIXED:
            self.pos += _FIXED[tag]
        elif tag == TAG_STRING:
            n = self._u2()
            self.pos += n
        elif tag == TAG_BYTE_ARRAY:
            n = self._i4()
            self.pos += n
        elif tag == TAG_INT_ARRAY:
            n = self._i4()
            self.pos += 4 * n
        elif tag == TAG_LONG_ARRAY:
            n = self._i4()
            self.pos += 8 * n
        elif tag == TAG_LIST:
            item = self._u1()
            n = self._i4()
            if n <= 0:
                return
            if item in _FIXED:
                self.pos += _FIXED[item] * n
            else:
                for _ in range(n):
                    self.skip(item)
        elif tag == TAG_COMPOUND:
            while True:
                t = self._u1()
                if t == TAG_END:
                    return
                n = self._u2()          # 名字长度
                self.pos += n
                self.skip(t)
        else:
            raise ValueError(f"未知 NBT tag: {tag}")


def parse_nbt(raw: bytes, root_keep: set[str] | None = None) -> dict:
    """解析一个完整 NBT 文档，返回根 compound。root_keep 只在根层生效。"""
    r = NBTReader(raw)
    tag = r._u1()
    if tag != TAG_COMPOUND:
        raise ValueError(f"根标签不是 compound，而是 {tag}")
    r._string()  # 根名字，通常是空串
    return r.payload(TAG_COMPOUND, keep=root_keep)


# ---------------------------------------------------------------- region

# 只保留地形需要的键，其余（entities / block_entities / structures）直接跳字节
CHUNK_KEEP = {"DataVersion", "sections", "Heightmaps", "Status", "xPos", "zPos", "yPos"}


def iter_region(path: str | Path, root_keep: set[str] | None = CHUNK_KEEP):
    """逐个产出 (cx_local, cz_local, chunk_nbt)。cx/cz 是 region 内 0..31 的局部坐标。"""
    path = Path(path)
    data = path.read_bytes()
    if len(data) < 8192:
        return
    header = np.frombuffer(data, ">u4", 1024, 0)
    offsets = (header >> 8).astype(np.int64)          # 以 4KiB 扇区计
    sectors = (header & 0xFF).astype(np.int64)

    for idx in range(1024):
        if sectors[idx] == 0 or offsets[idx] == 0:
            continue                                   # 未生成的区块
        start = int(offsets[idx]) * 4096
        if start + 5 > len(data):
            continue
        length = struct.unpack_from(">i", data, start)[0]
        comp = data[start + 4]
        blob = data[start + 5:start + 4 + length]
        try:
            if comp == 1:
                raw = gzip.decompress(blob)
            elif comp == 2:
                raw = zlib.decompress(blob)
            elif comp == 3:
                raw = blob
            else:
                continue                               # LZ4/自定义压缩，跳过
            nbt = parse_nbt(raw, root_keep)
        except Exception:
            continue                                   # 损坏区块，跳过而不是污染数据
        yield idx % 32, idx // 32, nbt


# ---------------------------------------------------------------- 打包位域

def unpack_bits(longs, nbits: int, count: int) -> np.ndarray:
    """1.16+ 的非跨 long 打包：每个 long 塞 floor(64/nbits) 个值，高位补零丢弃。"""
    per = 64 // nbits
    # 先 astype 成本机序 int64（这一步才真的字节翻转），再 view 成 uint64 做逻辑右移。
    # 直接对 '>i8' 数组 .view(uint64) 是按本机序重新解释字节，值会全错。
    arr = np.asarray(longs).astype(np.int64).view(np.uint64)
    shifts = (np.arange(per, dtype=np.uint64) * np.uint64(nbits))
    vals = (arr[:, None] >> shifts[None, :]) & np.uint64((1 << nbits) - 1)
    return vals.reshape(-1)[:count].astype(np.int32)


def _bits_for(n: int, floor: int) -> int:
    b = max(1, (n - 1).bit_length())
    return max(b, floor)


def section_blocks(sec: dict) -> tuple[list[str], np.ndarray] | None:
    """返回 (palette_names, idx[4096])，索引顺序 (y*16+z)*16+x。"""
    bs = sec.get("block_states")
    if not bs:
        return None
    pal = [e.get("Name", "minecraft:air") for e in bs.get("palette", [])]
    if not pal:
        return None
    data = bs.get("data")
    if data is None or len(pal) == 1:
        return pal, np.zeros(4096, np.int32)
    idx = unpack_bits(data, _bits_for(len(pal), 4), 4096)
    return pal, np.clip(idx, 0, len(pal) - 1)


def section_biomes(sec: dict) -> tuple[list[str], np.ndarray] | None:
    """返回 (palette_names, idx[64])，索引顺序 (y4*4+z4)*4+x4，每格覆盖 4x4x4。"""
    bi = sec.get("biomes")
    if not bi:
        return None
    pal = list(bi.get("palette", []))
    if not pal:
        return None
    data = bi.get("data")
    if data is None or len(pal) == 1:
        return pal, np.zeros(64, np.int32)
    idx = unpack_bits(data, _bits_for(len(pal), 1), 64)
    return pal, np.clip(idx, 0, len(pal) - 1)


def heightmap(chunk: dict, kind: str = "MOTION_BLOCKING_NO_LEAVES") -> np.ndarray | None:
    """解出 16x16 的高度图，值是「相对最低建筑高度的格数」，还没减 min_y。"""
    hm = chunk.get("Heightmaps")
    if not hm or kind not in hm:
        return None
    longs = hm[kind]
    if longs is None or len(longs) == 0:
        return None
    v = unpack_bits(longs, 9, 256)
    return v.reshape(16, 16)  # [z][x]
