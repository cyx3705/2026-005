# MC 2.5D 地形生成模型 —— 实现计划

> 目标：用**真实 Minecraft 存档（`.mca` region 文件）**训练一个 2.5D 地形生成模型，
> 输入条件 → 输出 `[高度场, 生物群系, 表层方块]` 三通道地表，并能无缝拼接成任意大的地形。
> 全流程在**本地 Windows 机器**执行（因为需要 Java worldgen / MC 生态库）。

---

## 0. 环境准备（一次性，~15 分钟）

```powershell
# 在 C:\Users\Administrator\Desktop\测试 下建工程
cd C:\Users\Administrator\Desktop\测试
python -m venv venv
.\venv\Scripts\activate

# 核心依赖
pip install anvil-parser nbtlib numpy torch torchvision matplotlib tqdm
# GPU 版 torch 按你的 CUDA 版本从 pytorch.org 装对应 wheel
```

**数据来源二选一：**
- **A（推荐，可控）**：装一个 headless 服务端（Paper/Fabric），用不同 `level-seed` 生成 N 个世界，
  只保留 `world/region/*.mca`。脚本见 §1.1。
- **B（最快，够用）**：直接拿你现有的几个单人存档 / 下载的大地图，
  `.mca` 在 `saves/<世界名>/region/` 下。几十个 region 文件就够训一个原型。

> 一个 region 文件 = 32×32 chunk = 512×512 方块的地表。哪怕只有 20 个 region，
> 也是 ~500 万个地表柱，样本量绰绰有余。

---

## 1. 数据管线：`.mca` → 训练张量

### 1.1（可选）批量生成世界

```python
# gen_worlds.py —— 用 headless server 批量出世界
import subprocess, os, shutil, random
SERVER_JAR = "paper.jar"   # 放同目录
OUT = "raw_worlds"
os.makedirs(OUT, exist_ok=True)
for i in range(20):
    seed = random.randint(0, 2**48)
    # 写 server.properties: level-seed=<seed>, 关掉刷怪/联机，生成后立即停
    # 用 --forceUpgrade 或挂个自动 op 走一圈 /forceload 加载区块，再 /stop
    # 生成的 world/region/*.mca 复制到 OUT/seed_<seed>/
    ...
```
> 若走 B 路线，跳过本步，直接指向已有 `region/` 目录。

### 1.2 解析 `.mca` → 地表三通道

核心：对每个 chunk（16×16），逐列从上往下扫，取**最高非空气方块**得到高度与表层方块，
biome 从 chunk 的 biome 数据取。

```python
# extract.py
import anvil, numpy as np, glob, os
from tqdm import tqdm

# —— 方块名 → 整数 token（地表只需二三十种）——
BLOCK_VOCAB = {
    "minecraft:air": 0, "minecraft:stone": 1, "minecraft:dirt": 2,
    "minecraft:grass_block": 3, "minecraft:sand": 4, "minecraft:water": 5,
    "minecraft:gravel": 6, "minecraft:snow_block": 7, "minecraft:snow": 7,
    "minecraft:sandstone": 8, "minecraft:clay": 9, "minecraft:ice": 10,
    "minecraft:packed_ice": 10, "minecraft:terracotta": 11,
    "minecraft:oak_log": 12, "minecraft:spruce_log": 12,
    "minecraft:podzol": 13, "minecraft:coarse_dirt": 2, "minecraft:mycelium": 14,
    # 兜底：未知方块归到最近类别或单独一个 "other" token
}
DEFAULT_TOKEN = 1  # 未知归为石头，避免污染词表

Y_MIN, Y_MAX = -64, 320   # 1.18+ 世界高度；老版本用 0..255

def block_token(name):
    return BLOCK_VOCAB.get(name, DEFAULT_TOKEN)

def chunk_to_surface(chunk):
    """返回 16x16 的 height / surface_block / biome 三层"""
    H = np.zeros((16,16), np.int16)
    S = np.zeros((16,16), np.uint8)
    B = np.zeros((16,16), np.uint8)   # biome id
    for x in range(16):
        for z in range(16):
            for y in range(Y_MAX, Y_MIN-1, -1):
                b = chunk.get_block(x, y, z)
                name = f"minecraft:{b.id}" if ":" not in b.id else b.id
                if name != "minecraft:air":
                    H[x,z] = y
                    S[x,z] = block_token(name)
                    break
            # biome：新版逐 3D，取地表那格；老版逐列
            B[x,z] = get_biome_id(chunk, x, H[x,z], z)
    return H, S, B

def process_region(path):
    region = anvil.Region.from_file(path)
    tiles = []
    for cx in range(32):
        for cz in range(32):
            try:
                ch = anvil.Chunk.from_region(region, cx, cz)
            except Exception:
                continue   # 未生成的 chunk
            H,S,B = chunk_to_surface(ch)
            tiles.append((H,S,B))
    return tiles

# 汇总所有 region → 大 numpy 存盘
all_H, all_S, all_B = [], [], []
for f in tqdm(glob.glob("raw_worlds/**/region/*.mca", recursive=True)):
    for H,S,B in process_region(f):
        all_H.append(H); all_S.append(S); all_B.append(B)
np.savez_compressed("dataset.npz",
    H=np.stack(all_H), S=np.stack(all_S), B=np.stack(all_B))
print("tiles:", len(all_H))
```

> ⚠️ 两个易踩坑：
> 1. **版本差异**：1.18+ 用分段 `sections`、biome 变 3D、Y 范围 -64..320；
>    老版本 0..255、biome 是 2D。`anvil-parser` 对新版支持有限，
>    **若你的存档是 1.18+，改用 `amulet-core`**（API 更全，`level.get_chunk`），
>    解析逻辑一样，只是取方块/biome 的调用不同。先确认你存档的版本再选库。
> 2. **未生成 chunk**：region 里很多 chunk 是空的，`try/except` 跳过，别让它污染数据。

### 1.3 编码成训练张量

```python
# encode.py
import numpy as np
d = np.load("dataset.npz")
H, S, B = d["H"].astype(np.float32), d["S"], d["B"]

# 高度：归一化到 [-1,1]（连续通道，diffusion 友好）
H_norm = (H - H.mean()) / (H.std() + 1e-6)
np.save("H_mean_std.npy", np.array([H.mean(), H.std()]))

# 表层方块 / biome：离散，保持整数（后面在模型里做 embedding 或 one-hot）
# 训练 tile 尺寸：16x16 太小，拼 chunk 成 64x64 的 patch 更利于学到中尺度结构
def make_patches(arr, k=4):
    # 把相邻 chunk 拼成 (k*16)x(k*16) 的 patch —— 需要按 region 内坐标重排
    ...  # 见仓库实现；核心是保留空间邻接
```

> **关键设计**：不要把 16×16 单 chunk 当训练单位——中尺度地形（山脊、河谷、biome 过渡）
> 跨 chunk，patch 至少 64×64（4×4 chunk）才学得到。这直接决定生成质量。

---

## 2. 模型：混合类型的 2.5D Diffusion

**核心难点**：一个通道是连续的（高度），两个是离散的（方块 token / biome id）。
不能一股脑当 RGB 图。方案：

- **高度**：标准连续 diffusion（在归一化高度上加高斯噪声，预测噪声）。
- **方块 / biome**：两条路，先用简单的——
  - **简单版**：把离散通道 embedding 成低维连续向量，和高度拼在一起做**连续 latent diffusion**，
    采样后对离散通道取最近 embedding / argmax 解码。够跑通、够看。
  - **进阶版**：离散扩散 **D3PM / multinomial diffusion**，对 token 直接做类别转移。
    质量更好但实现复杂，**留给第二版**。

```python
# model.py —— 简单版：连续 latent diffusion + 条件
import torch, torch.nn as nn

class TerrainUNet(nn.Module):
    """
    输入通道 = 1(高度) + E_block + E_biome  (embedding 展平后的连续表示)
    结构 = 轻量 2D U-Net（下采样3次，attention 在最低分辨率一层）
    条件 = biome 主类别 / 相邻 patch 边缘（做拼接用，见 §4）
    """
    def __init__(self, block_vocab=16, biome_vocab=64, emb=8, base=64):
        super().__init__()
        self.block_emb = nn.Embedding(block_vocab, emb)
        self.biome_emb = nn.Embedding(biome_vocab, emb)
        in_ch = 1 + emb + emb
        # ... 标准 U-Net down/up + 时间步 embedding（正弦）
    def forward(self, x, t, cond=None):
        ...
```

- 训练目标：DDPM 的 ε-prediction，MSE loss（高度部分）+ 离散通道的重建约束。
- 参数量：base=64 的 U-Net，**~10-30M 参数**，单卡 8-12GB 显存足够，
  这就是你要的"图像生成级别"。
- 调度器：DDPM 1000 步训练，采样用 DDIM 50 步加速。

---

## 3. 训练

```python
# train.py
import torch, numpy as np
from torch.utils.data import DataLoader, TensorDataset
from model import TerrainUNet

# 加载 patch 数据集 → DataLoader
# 标准 DDPM 训练循环：sample t → 加噪 → 预测 → loss.backward()
# 每 N step：
#   - 记录 loss
#   - 采样几个 patch，解码回 [高度/方块/biome]，存 PNG 可视化
#   - 存 checkpoint

CFG = dict(patch=64, batch=32, lr=2e-4, steps=30_000,
           timesteps=1000, sample_every=1000, ckpt_every=5000)
```

- **今晚目标**：先跑 3k-5k step 出一个"能看"的版本（山是山、水是水、biome 有块状结构），
  确认管线通、loss 降、采样不是噪声。完整 30k step 可以挂着过夜。
- 边训边看采样图，比只看 loss 靠谱得多。

---

## 4. 无缝拼接（无限地形的关键）

单独生成的两块地在交界处对不上。用 **outpainting / inpainting 条件**解决：
生成新 patch 时，把已生成的邻块边缘作为已知区域，让模型"续画"。

```python
# stitch.py
def generate_tiled(model, grid=(4,4), patch=64, overlap=16):
    canvas = init_empty(grid, patch, overlap)
    for i in range(grid[0]):
        for j in range(grid[1]):
            known_mask, known_vals = get_neighbor_edges(canvas, i, j, overlap)
            tile = ddim_sample(model, cond_edges=(known_mask, known_vals))
            paste(canvas, tile, i, j, overlap, blend="linear")  # 重叠区线性混合
    return canvas
```

- 重叠区（16 格）做线性 blend，消除接缝残留。
- 这是整个项目里唯一需要点技巧的地方，也是最出效果的 demo：一张无缝大地形图。

---

## 5. 验证：学得像不像

不能只看"好看"，要跟真实 MC 分布对齐：

```python
# eval.py
# 1) 高度谱：真实 vs 生成的高度直方图 / 功率谱，看地形粗糙度是否一致
# 2) biome 比例：各生物群系占比对比
# 3) 方块频率：表层方块 token 分布对比
# 4) 空间自相关：变差函数 / 半方差，看空间结构尺度是否匹配
# 全部并排画图，一眼判断
```

---

## 6. 目录结构

```
测试/
├── PLAN.md              ← 本文件
├── venv/
├── gen_worlds.py        §1.1 (可选)
├── extract.py           §1.2  .mca → 三通道
├── encode.py            §1.3  → patch 张量
├── model.py             §2
├── train.py             §3
├── stitch.py            §4    无缝拼接
├── eval.py              §5    分布验证
├── raw_worlds/          真实存档 region 文件
├── dataset.npz          解析后的数据
└── outputs/             采样图 / checkpoint / 拼接大图
```

---

## 7. 执行顺序（勾选推进）

- [x] §0 建 venv、装依赖、**确认存档 MC 版本**（本机无存档 → 走 A 路线，Paper 1.21.8 现生成；解析器自研，见 README）
- [x] §1.2 跑通 `extract.py`，先只处理 1 个 region，肉眼检查三通道 PNG 对不对（并与 MC 自带 Heightmaps 逐格交叉验证）
- [x] §1.2 全量解析 → `dataset.npz`（144 region / 1418 万地表柱）
- [x] §1.3 编码 + 切 64×64 patch（40400 个窗口）
- [x] §2/§3 训练，3k step 出第一张能看的采样图 ← **今晚的里程碑**
- [x] §4 拼接 demo，出无缝大地形（8×8 tile = 400×400 方块，接缝比值 0.91）
- [x] §5 分布验证（`outputs/eval.png`）
- [x] 挂机训满 30k（99.5 分钟）；第二版考虑 D3PM 离散扩散 / 加树木矿脉层

> 实际踩到的坑与修法见 [README.md](README.md)。最关键的一条：
> **ε-prediction 在 ᾱ_T→0 时定不住样本的 DC 分量，必须换 v-prediction。**

---

## 8. 风险与对策（诚实版）

| 风险 | 说明 | 对策 |
|---|---|---|
| 版本/库不兼容 | 1.18+ 存档 anvil-parser 可能读不全 | 优先 amulet-core；先拿 1 个 region 验证再全量 |
| "学个不如原生 worldgen" | 神经网络模仿一个又快又准的算法，没意义 | 立足点必须是**可控生成**：草图→地形 / biome 融合 / 残缺补全。原型跑通后立刻加一个条件维度证明价值 |
| 离散通道糊 | 简单版 embedding+argmax 会让 biome 边界毛糙 | 第二版上 D3PM 离散扩散 |
| 接缝 | 拼接处对不上 | §4 的 outpainting 条件 + 重叠 blend |

---

## 9. 一句话给未来的自己

这个静态地形模型本身的意义不在"复刻 MC 地形"（原生算法又快又免费），
而在于 **(a) 提供原生 worldgen 给不了的可控/可编辑生成**，
以及 **(b) 作为通往 Level 2/3（结构生成、可交互世界模型）的表示与训练台**。
第一版跑通后，尽快加一个条件维度（哪怕只是"文字/草图→地形"里最简单的一种），
否则它只是个昂贵的噪声函数。
