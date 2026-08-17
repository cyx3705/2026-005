"""方块名 → (token, kind) 分类。

kind：0=空气  1=植被/覆盖物(扫描时跳过)  2=固体  3=流体
把植被单独归一类是对 PLAN §1.2 的一点修正——直接取「最高非空气方块」会让
树冠、雪层、草丛在高度场上打出一堆 +1..+8 的毛刺，中尺度地形结构反而被噪声盖住。
我们要的是「地表」，所以扫描时跳过植被，取它下面第一个固体/流体。
"""

from __future__ import annotations

AIR = {"minecraft:air", "minecraft:cave_air", "minecraft:void_air"}

# 需要跳过的覆盖物：树、草、花、雪层、菌类、水生植物 …
VEG_EXACT = {
    "minecraft:snow", "minecraft:vine", "minecraft:glow_lichen", "minecraft:cactus",
    "minecraft:bamboo", "minecraft:sugar_cane", "minecraft:dead_bush", "minecraft:cobweb",
    "minecraft:lily_pad", "minecraft:seagrass", "minecraft:tall_seagrass", "minecraft:kelp",
    "minecraft:kelp_plant", "minecraft:sea_pickle", "minecraft:moss_carpet",
    "minecraft:pale_moss_carpet", "minecraft:hanging_roots", "minecraft:big_dripleaf",
    "minecraft:big_dripleaf_stem", "minecraft:small_dripleaf", "minecraft:spore_blossom",
    "minecraft:sculk_vein", "minecraft:pointed_dripstone", "minecraft:brown_mushroom",
    "minecraft:red_mushroom", "minecraft:sweet_berry_bush", "minecraft:pumpkin",
    "minecraft:melon", "minecraft:turtle_egg", "minecraft:bubble_column",
    "minecraft:snow_layer", "minecraft:torchflower", "minecraft:pitcher_plant",
    "minecraft:firefly_bush", "minecraft:leaf_litter", "minecraft:short_dry_grass",
    "minecraft:tall_dry_grass", "minecraft:bush", "minecraft:cactus_flower",
}
VEG_SUFFIX = (
    "_leaves", "_log", "_wood", "_stem", "_hyphae", "_sapling", "_flower", "_grass",
    "_fern", "_bush", "_roots", "_tulip", "_orchid", "_mushroom_block", "_coral",
    "_coral_fan", "_coral_block", "_wall_fan", "_azalea", "_petals", "_sprouts",
    "_propagule", "_stripped", "_carpet", "_button", "_torch", "_sign", "_plant",
)
VEG_CONTAINS = ("_coral", "azalea", "mushroom")

FLUID = {"minecraft:water", "minecraft:lava", "minecraft:flowing_water", "minecraft:flowing_lava"}

# --- token 词表（地表二三十种足够）---
_GROUPS: list[tuple[str, tuple[str, ...]]] = [
    ("air", ()),                                                     # 0
    ("stone", ("stone", "deepslate", "andesite", "diorite", "granite", "tuff",
               "calcite", "dripstone_block", "cobblestone", "smooth_basalt",
               "basalt", "blackstone", "amethyst_block", "budding_amethyst")),   # 1
    ("dirt", ("dirt", "coarse_dirt", "rooted_dirt", "mud", "dirt_path", "farmland")),  # 2
    ("grass_block", ("grass_block",)),                               # 3
    ("sand", ("sand", "red_sand", "suspicious_sand")),               # 4
    ("water", ("water", "flowing_water")),                           # 5
    ("gravel", ("gravel", "suspicious_gravel")),                     # 6
    ("snow_block", ("snow_block", "powder_snow")),                   # 7
    ("sandstone", ("sandstone", "red_sandstone", "smooth_sandstone",
                   "smooth_red_sandstone", "cut_sandstone", "cut_red_sandstone",
                   "chiseled_sandstone", "chiseled_red_sandstone")),  # 8
    ("clay", ("clay",)),                                             # 9
    ("ice", ("ice", "packed_ice", "blue_ice", "frosted_ice")),       # 10
    ("terracotta", ("terracotta",)),                                 # 11
    ("moss", ("moss_block", "pale_moss_block")),                     # 12
    ("podzol", ("podzol",)),                                         # 13
    ("mycelium", ("mycelium",)),                                     # 14
    ("lava", ("lava", "flowing_lava")),                              # 15
    ("bedrock", ("bedrock",)),                                       # 16
    ("other", ()),                                                   # 17
]
BLOCK_NAMES = [g[0] for g in _GROUPS]
BLOCK_VOCAB_SIZE = len(_GROUPS)
OTHER_TOKEN = BLOCK_VOCAB_SIZE - 1

_LUT: dict[str, int] = {}
for _tok, (_n, _members) in enumerate(_GROUPS):
    for _m in _members:
        _LUT[f"minecraft:{_m}"] = _tok

# 各色陶瓦全归 terracotta（恶地地表的主料）
_COLORS = ("white", "orange", "magenta", "light_blue", "yellow", "lime", "pink", "gray",
           "light_gray", "cyan", "purple", "blue", "brown", "green", "red", "black")
for _c in _COLORS:
    _LUT[f"minecraft:{_c}_terracotta"] = BLOCK_NAMES.index("terracotta")


# --- biome 归族 ---
# 原始 53 个 biome 里长尾极重（一半占比 <1%）。one-hot 之后这些通道的「present」值
# 会飙到 14 个 sigma，对高斯扩散是灾难。归成 15 个族之后每族占比都 >2%，
# 通道尖峰被压到 4 sigma 以内，通道数也从 50 降到 15。
BIOME_FAMILIES: list[tuple[str, tuple[str, ...]]] = [
    ("ocean", ("ocean",)),
    ("river", ("river",)),
    ("beach", ("beach", "stony_shore")),
    ("plains", ("plains", "meadow")),          # 含 sunflower_plains；snowy_plains 见下
    ("snowy", ("snowy", "ice_spikes", "frozen_peaks", "jagged_peaks",
               "stony_peaks", "grove", "snowy_slopes")),
    ("forest", ("forest", "cherry_grove", "pale_garden")),
    ("taiga", ("taiga",)),
    ("jungle", ("jungle",)),
    ("savanna", ("savanna",)),
    ("desert", ("desert",)),
    ("badlands", ("badlands",)),
    ("swamp", ("swamp",)),
    ("windswept", ("windswept",)),
    ("mushroom", ("mushroom",)),
    ("other", ()),
]
BIOME_FAMILY_NAMES = [f[0] for f in BIOME_FAMILIES]


def biome_family(name: str) -> int:
    """按子串归族。顺序有讲究：snowy_* 必须在 plains/taiga/beach 之前命中。"""
    s = name.split(":", 1)[-1]
    if "snowy" in s or s in ("ice_spikes", "frozen_peaks", "jagged_peaks",
                             "stony_peaks", "grove", "snowy_slopes"):
        return BIOME_FAMILY_NAMES.index("snowy")
    if "frozen_river" == s:
        return BIOME_FAMILY_NAMES.index("river")
    for i, (_fam, keys) in enumerate(BIOME_FAMILIES):
        if any(k in s for k in keys):
            return i
    return len(BIOME_FAMILIES) - 1                    # other


def classify(name: str) -> tuple[int, int]:
    """返回 (token, kind)。未知固体归到 'other' 而不是石头，免得污染石头的分布统计。"""
    if name in AIR:
        return 0, 0
    if name in VEG_EXACT:
        return 0, 1
    short = name.split(":", 1)[-1]
    if name.endswith(VEG_SUFFIX) or any(c in short for c in VEG_CONTAINS):
        # *_stripped / *_button 之类不是植被但同样不该算地表，一并跳过
        return 0, 1
    tok = _LUT.get(name)
    if tok is None:
        return OTHER_TOKEN, 2
    return tok, 3 if name in FLUID else 2
