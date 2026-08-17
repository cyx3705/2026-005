"""matplotlib 公共设置。Windows 上 DejaVu Sans 没有中文字形，标题会退化成一堆方框，
统一切到系统里的中文字体。"""

from __future__ import annotations


def setup():
    import matplotlib
    matplotlib.use("Agg")
    matplotlib.rcParams["font.sans-serif"] = [
        "Microsoft YaHei", "SimHei", "Noto Sans CJK SC", "DejaVu Sans"]
    matplotlib.rcParams["axes.unicode_minus"] = False
    import matplotlib.pyplot as plt
    return plt
