"""§2  混合类型的 2.5D 地形 Diffusion

三个通道类型不同（高度连续，方块/biome 离散），不能一股脑当 RGB。这里走 PLAN 的
「简单版」，但把离散通道的表示定死为 **±1 的 one-hot**，而不是可学习 embedding：

  可学习 embedding + 连续扩散有个隐患——扩散的目标分布随着 embedding 一起动，
  训练早期很容易塌成一团、采样后最近邻解码全落到同一个 token。
  ±1 one-hot 是固定的、互相等距的，解码就是 argmax，最稳。代价只是通道数变多
  （1 + 18 + ~25 ≈ 44），对 64x64 的轻量 U-Net 完全不构成压力。

损失按「组」加权：高度只有 1 个通道，若与 44 个通道平摊 MSE，高度几乎不被优化。

条件通道：为了 §4 的无缝拼接，模型从一开始就按 outpainting 训练——
输入额外拼上 [已知区域的干净值, 已知掩码]，训练时随机生成边带/矩形掩码。
所以同一个模型既能无条件生成，也能按邻块边缘续画。
"""

from __future__ import annotations

import math

import torch
import torch.nn as nn
import torch.nn.functional as F


# ---------------------------------------------------------------- 表示

class Codec:
    """[高度, 方块token, biome token] ←→ 连续张量 (C,H,W)。

    **每个通道都标准化到零均值单位方差**——这不是锦上添花，是必需的。
    裸的 ±1 one-hot 数据全通道均值是 -0.94（K 类里只有 1 个 +1），而扩散先验是
    N(0,1)。模型要把轨迹从 0 拉到 -0.94，靠的是 ε 上一个 √ᾱ·μ 的偏置；在 t→T、
    ᾱ→1e-8 时这个偏置量级是 1e-4，**bf16 的相对精度 0.4% 根本表示不了**，
    第一步就被舍掉，DDIM 轨迹锁死在零均值，自洽地收敛到完全错误的分布。
    （实测：真值 mean=-0.937，采样收敛到 -0.073。）

    one-hot 通道按 p_c=类频率 标准化：μ=2p-1，σ=2√(p(1-p))，
    并对 σ 设下限使「present」值不超过 amp_cap 个 σ——否则占比 0.5% 的稀有类
    会给出 14σ 的尖峰，对高斯扩散同样是灾难。
    """

    def __init__(self, block_freq, biome_freq, device="cpu", amp_cap: float = 4.0):
        self.block_freq = list(block_freq)
        self.biome_freq = list(biome_freq)
        self.n_block = len(self.block_freq)
        self.n_biome = len(self.biome_freq)
        self.C = 1 + self.n_block + self.n_biome
        self.sl_h = slice(0, 1)
        self.sl_b = slice(1, 1 + self.n_block)
        self.sl_m = slice(1 + self.n_block, self.C)

        mu, sd = [0.0], [1.0]                      # 高度在数据侧已经标准化
        for p in self.block_freq + self.biome_freq:
            p = min(max(float(p), 1e-6), 1 - 1e-6)
            m = 2 * p - 1
            s = max(2 * math.sqrt(p * (1 - p)), (1.0 - m) / amp_cap)
            mu.append(m); sd.append(s)
        self.mu = torch.tensor(mu, device=device).view(1, -1, 1, 1)
        self.sd = torch.tensor(sd, device=device).view(1, -1, 1, 1)

    def encode(self, h: torch.Tensor, s: torch.Tensor, b: torch.Tensor) -> torch.Tensor:
        """h:(N,1,H,W) 已标准化高度; s,b:(N,H,W) long → (N,C,H,W)"""
        oh_s = F.one_hot(s.long(), self.n_block).permute(0, 3, 1, 2).float() * 2 - 1
        oh_b = F.one_hot(b.long(), self.n_biome).permute(0, 3, 1, 2).float() * 2 - 1
        return (torch.cat([h, oh_s, oh_b], 1) - self.mu) / self.sd

    def decode(self, x: torch.Tensor):
        """(N,C,H,W) → (标准化高度, block long, biome long)"""
        raw = x * self.sd + self.mu               # 回到 ±1 one-hot 尺度再 argmax
        return (raw[:, self.sl_h].clamp(-5, 5),
                raw[:, self.sl_b].argmax(1),
                raw[:, self.sl_m].argmax(1))

    def bounds(self):
        """每个通道在编码空间里的合法取值范围，采样时用它裁 x0。

        不能用一个全局的 ±1.5：标准化之后稀有类的「present」值高到 +4σ、
        高度尾部到 ±5σ，一刀切会把稀有类和山峰全压掉，表现为离散通道塌到主类、
        高度方差只剩真值的 1/4。
        """
        lo = (-1.0 - self.mu) / self.sd
        hi = (1.0 - self.mu) / self.sd
        lo = lo.clone(); hi = hi.clone()
        lo[:, 0] = -5.0                            # 高度：数据侧就是裁到 ±5σ
        hi[:, 0] = 5.0
        return lo, hi

    def group_weights(self, device) -> torch.Tensor:
        """每组总权重相等 → 高度不会被 43 个离散通道淹掉。"""
        w = torch.empty(self.C, device=device)
        w[self.sl_h] = 1.0 / 1
        w[self.sl_b] = 1.0 / self.n_block
        w[self.sl_m] = 1.0 / self.n_biome
        return (w * self.C / w.sum()).view(1, -1, 1, 1)


# ---------------------------------------------------------------- U-Net

def timestep_embedding(t: torch.Tensor, dim: int) -> torch.Tensor:
    half = dim // 2
    freqs = torch.exp(-math.log(10000) * torch.arange(half, device=t.device) / half)
    a = t.float()[:, None] * freqs[None]
    return torch.cat([a.cos(), a.sin()], -1)


class ResBlock(nn.Module):
    def __init__(self, cin: int, cout: int, tdim: int):
        super().__init__()
        self.n1 = nn.GroupNorm(32, cin)
        self.c1 = nn.Conv2d(cin, cout, 3, padding=1)
        self.emb = nn.Linear(tdim, cout * 2)
        self.n2 = nn.GroupNorm(32, cout)
        self.c2 = nn.Conv2d(cout, cout, 3, padding=1)
        self.skip = nn.Conv2d(cin, cout, 1) if cin != cout else nn.Identity()
        nn.init.zeros_(self.c2.weight); nn.init.zeros_(self.c2.bias)

    def forward(self, x, t):
        h = self.c1(F.silu(self.n1(x)))
        scale, shift = self.emb(F.silu(t))[:, :, None, None].chunk(2, 1)
        h = F.silu(self.n2(h) * (1 + scale) + shift)
        return self.skip(x) + self.c2(h)


class Attn(nn.Module):
    def __init__(self, c: int, heads: int = 4):
        super().__init__()
        self.h = heads
        self.n = nn.GroupNorm(32, c)
        self.qkv = nn.Conv2d(c, c * 3, 1)
        self.out = nn.Conv2d(c, c, 1)
        nn.init.zeros_(self.out.weight); nn.init.zeros_(self.out.bias)

    def forward(self, x):
        n, c, hh, ww = x.shape
        q, k, v = self.qkv(self.n(x)).reshape(n, 3, self.h, c // self.h, hh * ww).unbind(1)
        a = F.scaled_dot_product_attention(q.transpose(-1, -2), k.transpose(-1, -2),
                                           v.transpose(-1, -2))
        return x + self.out(a.transpose(-1, -2).reshape(n, c, hh, ww))


class TerrainUNet(nn.Module):
    """3 层下采样的轻量 U-Net，最低分辨率带一层 self-attention。"""

    def __init__(self, cdata: int, base: int = 64, mults=(1, 2, 4), nres: int = 2,
                 attn_at: int = 2, cond: bool = True):
        super().__init__()
        self.cdata = cdata
        self.cond = cond
        cin = cdata * 2 + 1 if cond else cdata      # [x_t, 已知值, 已知掩码]
        tdim = base * 4
        self.tmlp = nn.Sequential(nn.Linear(base, tdim), nn.SiLU(), nn.Linear(tdim, tdim))
        self.tdim_in = base

        self.stem = nn.Conv2d(cin, base, 3, padding=1)
        chs = [base]
        self.downs = nn.ModuleList()
        c = base
        for i, m in enumerate(mults):
            for _ in range(nres):
                blk = nn.ModuleList([ResBlock(c, base * m, tdim),
                                     Attn(base * m) if i == attn_at else nn.Identity()])
                self.downs.append(blk)
                c = base * m
                chs.append(c)
            if i < len(mults) - 1:
                self.downs.append(nn.ModuleList([nn.Conv2d(c, c, 3, 2, 1), nn.Identity()]))
                chs.append(c)

        self.mid1 = ResBlock(c, c, tdim)
        self.mida = Attn(c)
        self.mid2 = ResBlock(c, c, tdim)

        self.ups = nn.ModuleList()
        for i, m in reversed(list(enumerate(mults))):
            for j in range(nres + 1):
                blk = nn.ModuleList([ResBlock(c + chs.pop(), base * m, tdim),
                                     Attn(base * m) if i == attn_at else nn.Identity()])
                self.ups.append(blk)
                c = base * m
            if i > 0:
                self.ups.append(nn.ModuleList([nn.Upsample(scale_factor=2, mode="nearest"),
                                               nn.Conv2d(c, c, 3, padding=1)]))
        self.outn = nn.GroupNorm(32, c)
        self.outc = nn.Conv2d(c, cdata, 3, padding=1)
        nn.init.zeros_(self.outc.weight); nn.init.zeros_(self.outc.bias)

    def forward(self, x, t, known=None, mask=None):
        if self.cond:
            if known is None:
                known = torch.zeros_like(x)
                mask = torch.zeros_like(x[:, :1])
            x = torch.cat([x, known * mask, mask], 1)
        temb = self.tmlp(timestep_embedding(t, self.tdim_in))

        h = self.stem(x)
        hs = [h]
        for blk in self.downs:
            if isinstance(blk[0], ResBlock):
                h = blk[1](blk[0](h, temb)) if not isinstance(blk[1], nn.Identity) else blk[0](h, temb)
            else:
                h = blk[0](h)
            hs.append(h)
        h = self.mid2(self.mida(self.mid1(h, temb)), temb)
        for blk in self.ups:
            if isinstance(blk[0], ResBlock):
                h = torch.cat([h, hs.pop()], 1)
                h = blk[0](h, temb)
                if not isinstance(blk[1], nn.Identity):
                    h = blk[1](h)
            else:
                h = blk[1](blk[0](h))
        return self.outc(F.silu(self.outn(h)))


# ---------------------------------------------------------------- 扩散

class Diffusion:
    """DDPM 训练 + DDIM 采样，cosine schedule，默认 **v-prediction**。

    为什么不用 ε-prediction：cosine schedule 下 ᾱ_T ≈ 1e-8，而样本的低频/DC 分量
    恰恰是在 t→T 这一端定下来的。ε 参数化下
        x0 = (x_t − √(1−ᾱ)·ε̂) / √ᾱ
    的 1/√ᾱ = 1e4 会把 ε̂ 的任何误差放大成 x0 的巨大偏移，DC 完全失控。
    实测：生成 patch 之间的高度均值 std 是真值的 2.3 倍，
    且把采样步数从 50 加到 250 毫无改善（说明不是离散化误差）。

    v-prediction 下
        v = √ᾱ·ε − √(1−ᾱ)·x0
        x0 = √ᾱ·x_t − √(1−ᾱ)·v̂
    在 t=T（ᾱ=0）时退化成 x0 = −v̂，模型直接输出信号本身，没有任何放大。
    """

    def __init__(self, timesteps: int = 1000, device="cuda", pred: str = "v"):
        self.T = timesteps
        self.pred = pred
        s = 0.008
        t = torch.linspace(0, timesteps, timesteps + 1, dtype=torch.float64) / timesteps
        f = torch.cos((t + s) / (1 + s) * math.pi / 2) ** 2
        ab = (f / f[0]).clamp(1e-8, 1.0)
        betas = (1 - ab[1:] / ab[:-1]).clamp(0, 0.999)
        self.betas = betas.float().to(device)
        self.ab = torch.cumprod(1 - self.betas, 0)
        self.sab = self.ab.sqrt()
        self.somab = (1 - self.ab).sqrt()

    def q_sample(self, x0, t, noise):
        return self.sab[t][:, None, None, None] * x0 + self.somab[t][:, None, None, None] * noise

    def target(self, x0, noise, t):
        """训练目标：v-pred 下是 v = √ᾱ·ε − √(1−ᾱ)·x0，ε-pred 下就是 ε。"""
        if self.pred != "v":
            return noise
        sa = self.sab[t][:, None, None, None]
        so = self.somab[t][:, None, None, None]
        return sa * noise - so * x0

    def to_x0_eps(self, out, x, a):
        """模型输出 → (x0, ε)。"""
        sa, so = a.sqrt(), (1 - a).sqrt()
        if self.pred == "v":
            return sa * x - so * out, so * x + sa * out
        return (x - so * out) / sa, out

    @torch.no_grad()
    def ddim(self, model, shape, device, steps: int = 50, eta: float = 0.0,
             known=None, mask=None, guidance_repeat: int = 1, progress=None,
             bounds=None):
        """DDIM 采样。known/mask 非空时做 outpainting，并在每步把已知区域硬贴回去。

        bounds=(lo,hi) 是每通道的合法取值范围（见 Codec.bounds），用来裁 x0。
        高噪声端 x0 = (x - √(1-ᾱ)ε)/√ᾱ 会被 1/√ᾱ 放大到几千，不裁会炸；
        但裁得比数据范围还紧会直接抹掉稀有类和地形尾部。
        """
        x = torch.randn(shape, device=device)
        seq = torch.linspace(self.T - 1, 0, steps).long().to(device)
        blo, bhi = bounds if bounds is not None else (-8.0, 8.0)
        for i, tc in enumerate(seq):
            tb = tc.repeat(shape[0])
            for _ in range(guidance_repeat):
                out = model(x, tb, known, mask)
                a = self.ab[tc]
                x0, eps = self.to_x0_eps(out, x, a)
                x0 = x0.clamp(blo, bhi)
                # 用裁过的 x0 反算 ε，两者保持一致；否则裁剪只作用在一半的更新项上
                so = (1 - a).sqrt()
                if so > 1e-3:
                    eps = (x - a.sqrt() * x0) / so
                if i + 1 < len(seq):
                    ap = self.ab[seq[i + 1]]
                    sigma = eta * ((1 - ap) / (1 - a)).sqrt() * (1 - a / ap).sqrt()
                    x = ap.sqrt() * x0 + (1 - ap - sigma ** 2).clamp(min=0).sqrt() * eps
                    if sigma > 0:
                        x = x + sigma * torch.randn_like(x)
                else:
                    x = x0
                if mask is not None:
                    # 已知区域直接用真值加噪覆盖，杜绝接缝处漂移
                    if i + 1 < len(seq):
                        tn = seq[i + 1].repeat(shape[0])
                        x = mask * self.q_sample(known, tn, torch.randn_like(x)) + (1 - mask) * x
                    else:
                        x = mask * known + (1 - mask) * x
            if progress is not None:
                progress(i + 1, len(seq))
        return x


def load_ckpt(path, device, use_ema: bool = True):
    """读 train.py 存的 checkpoint，返回 (net, codec, cfg, step)。"""
    st = torch.load(path, map_location=device, weights_only=False)
    cfg = st["cfg"]
    codec = Codec(cfg["block_freq"], cfg["biome_freq"], device)
    cfg.setdefault("pred", "eps")                   # 老 checkpoint 默认是 ε-pred
    net = TerrainUNet(codec.C, base=cfg["base"]).to(device)
    net.load_state_dict(st["net"])
    if use_ema:
        with torch.no_grad():
            for p, s in zip(net.parameters(), st["ema"]):
                p.copy_(s.to(device))
    net.eval()
    return net, codec, cfg, st["step"]


def make_cond_mask(n: int, size: int, device, p_uncond: float = 0.5) -> torch.Tensor:
    """随机 outpainting 掩码：边带为主（拼接时就是这种形状），偶尔来个矩形缺口。

    全向量化——按 batch 逐个 python 循环建掩码会打出上百个小 CUDA kernel，
    在这种单步只有几十毫秒的训练里能吃掉一半以上的时间。
    """
    rr = torch.arange(size, device=device, dtype=torch.float32).view(1, 1, size, 1)
    cc = rr.view(1, 1, 1, size)
    u = torch.rand(n, 1, 1, 1, device=device)

    # 边带：四条边各以 50% 出现，宽度 size/8 ~ size/2
    use = (torch.rand(n, 4, 1, 1, device=device) < 0.5).float()
    wid = torch.randint(size // 8, size // 2, (n, 4, 1, 1), device=device).float() * use
    band = ((rr < wid[:, 0:1]) | (rr >= size - wid[:, 1:2])
            | (cc < wid[:, 2:3]) | (cc >= size - wid[:, 3:4])).float()

    # 矩形缺口：矩形内未知、外部已知（残缺补全）
    z0 = torch.randint(0, size // 2, (n, 1, 1, 1), device=device).float()
    x0 = torch.randint(0, size // 2, (n, 1, 1, 1), device=device).float()
    hh = torch.randint(size // 4, size, (n, 1, 1, 1), device=device).float()
    ww = torch.randint(size // 4, size, (n, 1, 1, 1), device=device).float()
    inside = (rr >= z0) & (rr < z0 + hh) & (cc >= x0) & (cc < x0 + ww)
    rect = (~inside).float()

    # p_uncond 从 0.25 提到 0.5：条件样本的损失只算未知区，那是个「照着已知区补全」
    # 的低方差回归任务；无条件样本才是真正的生成任务。25% 的份额喂不出生成能力，
    # 表现为采样出来的高度方差只有真值的 1/4（几乎是一张平均地形）。
    zero = torch.zeros_like(band)
    p_band = p_uncond + (1 - p_uncond) * 0.73
    return torch.where(u < p_uncond, zero, torch.where(u < p_band, band, rect))
