"""§1.1 用 headless Paper 服务端批量生成真实 MC 世界。

每个 seed 起一次服务端，用 /forceload 分批把指定范围的区块生成出来，
边生边 save-all，最后 /stop，把 world/region/*.mca 收到 raw_worlds/seed_<seed>/region/。

用法：
    venv\\Scripts\\python gen_worlds.py --worlds 12 --radius 32
        radius=32 → 64x64 chunk = 1024x1024 方块 = 2x2 个 region
"""

from __future__ import annotations

import argparse
import json
import random
import shutil
import subprocess
import sys
import time
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parent
JAVA = ROOT / "tools" / "jdk" / "jdk-21.0.12+8" / "bin" / "java.exe"
PAPER = ROOT / "tools" / "paper-1.21.8-60.jar"
RUN = ROOT / "tools" / "server_run"
OUT = ROOT / "raw_worlds"

# 关掉一切与地形无关的东西：要的是干净的原生地形分布
PROPS = """\
level-seed={seed}
level-type=minecraft:normal
level-name=world
online-mode=false
generate-structures=false
spawn-protection=0
spawn-monsters=false
spawn-animals=false
spawn-npcs=false
allow-nether=false
enable-command-block=false
max-players=1
view-distance=6
simulation-distance=4
sync-chunk-writes=false
network-compression-threshold=-1
enable-status=false
"""

BATCH = 256  # 单条 /forceload 的上限就是 256 个区块


class Server:
    def __init__(self, heap: str = "4G"):
        self.p = subprocess.Popen(
            [str(JAVA), f"-Xms{heap}", f"-Xmx{heap}", "-jar", str(PAPER), "--nogui"],
            cwd=RUN, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT, text=True, encoding="utf-8", errors="replace",
            bufsize=1,
        )

    def wait_for(self, marker: str, timeout: float) -> bool:
        end = time.time() + timeout
        while time.time() < end:
            line = self.p.stdout.readline()
            if not line:
                return False
            if marker in line:
                return True
        return False

    def cmd(self, c: str) -> None:
        self.p.stdin.write(c + "\n")
        self.p.stdin.flush()

    def drain(self) -> None:
        """把积压的 stdout 吃掉，避免管道写满把服务端卡死。"""
        import threading
        threading.Thread(target=lambda: [None for _ in self.p.stdout], daemon=True).start()


def region_bytes(world: Path) -> int:
    d = world / "region"
    return sum(f.stat().st_size for f in d.glob("*.mca")) if d.is_dir() else 0


def count_chunks(world: Path, radius: int) -> int:
    """只读 region 头部的扇区表，数「目标方阵内已落盘的区块」。

    不能靠「文件大小不再增长」判停：区块早就生成完了，save-all 还会持续微调几十 KB，
    大小永远差一点点相等，判停条件永远不成立。数区块才是确定性的。
    """
    d = world / "region"
    if not d.is_dir():
        return 0
    total = 0
    for f in d.glob("*.mca"):
        try:
            parts = f.stem.split(".")
            rx, rz = int(parts[1]), int(parts[2])
            with open(f, "rb") as fh:
                head = fh.read(4096)
            if len(head) < 4096:
                continue
        except Exception:
            continue
        sectors = np.frombuffer(head, ">u4", 1024) & 0xFF
        nz = np.nonzero(sectors)[0]
        cx = rx * 32 + (nz % 32)
        cz = rz * 32 + (nz // 32)
        total += int(((cx >= -radius) & (cx < radius) & (cz >= -radius) & (cz < radius)).sum())
    return total


def gen_one(seed: int, radius: int, heap: str, quiet_polls: int = 4) -> bool:
    if RUN.exists():
        shutil.rmtree(RUN, ignore_errors=True)
    RUN.mkdir(parents=True)
    (RUN / "eula.txt").write_text("eula=true\n", encoding="utf-8")
    (RUN / "server.properties").write_text(PROPS.format(seed=seed), encoding="utf-8")

    print(f"  [seed {seed}] 启动服务端 …", flush=True)
    srv = Server(heap)
    if not srv.wait_for('For help, type "help"', timeout=300):
        print("  !! 服务端启动超时/失败", flush=True)
        srv.p.kill()
        return False
    srv.drain()

    # 把 [-radius, radius) 的区块方阵切成 <=256 的批次逐条 forceload
    side = int(BATCH ** 0.5)  # 16x16=256
    coords = []
    for x0 in range(-radius, radius, side):
        for z0 in range(-radius, radius, side):
            coords.append((x0, z0, min(x0 + side, radius) - 1, min(z0 + side, radius) - 1))
    print(f"  [seed {seed}] forceload {len(coords)} 批 / {(2*radius)**2} 区块", flush=True)
    for x0, z0, x1, z1 in coords:
        srv.cmd(f"forceload add {x0*16} {z0*16} {x1*16+15} {z1*16+15}")
        time.sleep(0.15)

    # 轮询「目标方阵内已落盘的区块数」，够了就收工
    world = RUN / "world"
    target = (2 * radius) ** 2
    last, stall, waited = -1, 0, 0.0
    while waited < 900:
        srv.cmd("save-all")
        time.sleep(10)
        waited += 10
        cur = count_chunks(world, radius)
        print(f"    … {waited:.0f}s  {cur}/{target} 区块 ({region_bytes(world)/1e6:.1f}MB)", flush=True)
        if cur >= target:
            break
        stall = stall + 1 if cur <= last else 0
        last = cur
        if stall >= quiet_polls:                     # 卡住不涨了，拿到多少算多少
            print("    (区块数不再增长，提前收工)", flush=True)
            break

    srv.cmd("save-all flush")
    time.sleep(5)
    srv.cmd("stop")
    try:
        srv.p.wait(timeout=180)
    except subprocess.TimeoutExpired:
        srv.p.kill()

    dst = OUT / f"seed_{seed}" / "region"
    dst.parent.mkdir(parents=True, exist_ok=True)
    if dst.exists():
        shutil.rmtree(dst)
    shutil.copytree(world / "region", dst)
    n = len(list(dst.glob("*.mca")))
    print(f"  [seed {seed}] 收下 {n} 个 region ({region_bytes(world)/1e6:.1f}MB)", flush=True)
    shutil.rmtree(RUN, ignore_errors=True)
    return n > 0


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--worlds", type=int, default=12)
    ap.add_argument("--radius", type=int, default=32, help="区块半径，32 → 64x64 chunk")
    ap.add_argument("--heap", default="4G")
    ap.add_argument("--seed0", type=int, default=20260731)
    a = ap.parse_args()

    if not JAVA.exists() or not PAPER.exists():
        print(f"缺 java 或 paper.jar：{JAVA} / {PAPER}", file=sys.stderr)
        return 1
    OUT.mkdir(parents=True, exist_ok=True)

    rng = random.Random(a.seed0)
    seeds = [rng.randint(0, 2**48 - 1) for _ in range(a.worlds)]
    (OUT / "seeds.json").write_text(json.dumps(seeds), encoding="utf-8")

    t0 = time.time()
    ok = 0
    for i, s in enumerate(seeds, 1):
        print(f"[{i}/{len(seeds)}] ---------------------------------", flush=True)
        if (OUT / f"seed_{s}" / "region").is_dir():
            print(f"  [seed {s}] 已存在，跳过", flush=True)
            ok += 1
            continue
        ok += bool(gen_one(s, a.radius, a.heap))
    print(f"\n完成 {ok}/{len(seeds)} 个世界，用时 {(time.time()-t0)/60:.1f} 分钟")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
