from pathlib import Path

import pdfplumber
from reportlab.pdfgen import canvas
from reportlab.lib.colors import black, Color


ROOT = Path(__file__).resolve().parent
SOURCE = ROOT / "CA6140车床拨叉加工零件图-模型(2).pdf"
OUT_DIR = ROOT / "A1"
OUT_DIR.mkdir(parents=True, exist_ok=True)
PART = OUT_DIR / "A1_CA6140拨叉_零件图_A3.pdf"
BLANK = OUT_DIR / "A1_CA6140拨叉_毛坯图_A3.pdf"
PAGE_HEIGHT = 842.0


def add_path(c, path, scale=1.0, center=(430.0, 450.0)):
    if not path:
        return
    p = c.beginPath()
    cx, cy = center
    current = None
    for item in path:
        op = item[0]
        vals = item[1:] if op == "c" else (item[1] if len(item) > 1 else ())
        if op == "m":
            x, y = vals
            if scale != 1.0:
                x, y = cx + (x - cx) * scale, cy + (y - cy) * scale
            p.moveTo(x, PAGE_HEIGHT - y)
            current = (x, y)
        elif op == "l":
            x, y = vals
            if scale != 1.0:
                x, y = cx + (x - cx) * scale, cy + (y - cy) * scale
            p.lineTo(x, PAGE_HEIGHT - y)
            current = (x, y)
        elif op == "c":
            p1, p2, p3 = vals
            if scale != 1.0:
                p1 = (cx + (p1[0] - cx) * scale, cy + (p1[1] - cy) * scale)
                p2 = (cx + (p2[0] - cx) * scale, cy + (p2[1] - cy) * scale)
                p3 = (cx + (p3[0] - cx) * scale, cy + (p3[1] - cy) * scale)
            p.curveTo(p1[0], PAGE_HEIGHT - p1[1], p2[0], PAGE_HEIGHT - p2[1], p3[0], PAGE_HEIGHT - p3[1])
            current = p3
        elif op == "h":
            p.close()
    c.drawPath(p, stroke=1, fill=0)


def draw_source(c, page):
    c.setStrokeColor(black)
    c.setLineWidth(0.72)
    for item in page.lines:
        pts = item.get("pts") or []
        if len(pts) >= 2:
            c.setLineWidth(float(item.get("linewidth") or 0.72))
            c.line(pts[0][0], PAGE_HEIGHT - pts[0][1], pts[1][0], PAGE_HEIGHT - pts[1][1])
    for item in page.rects:
        c.setLineWidth(float(item.get("linewidth") or 0.72))
        add_path(c, item.get("path") or [])
    for item in page.curves:
        c.setLineWidth(float(item.get("linewidth") or 0.72))
        add_path(c, item.get("path") or [])


def draw_blank_overlay(c, page):
    c.setStrokeColor(Color(0.8, 0.05, 0.05))
    c.setLineWidth(1.8)
    for item in list(page.lines) + list(page.rects) + list(page.curves):
        if float(item.get("linewidth") or 0.0) < 1.5:
            continue
        pts = item.get("pts") or []
        if not pts:
            path = item.get("path") or []
            for node in path:
                if len(node) > 1:
                    if node[0] == "c":
                        pts.extend(list(node[1:]))
                    else:
                        pts.append(node[1])
        if not pts:
            continue
        xs = [p[0] for p in pts]
        ys = [p[1] for p in pts]
        if max(xs) < 190 or min(xs) > 650 or max(ys) < 260 or min(ys) > 590:
            continue
        if max(xs) - min(xs) > 700 or max(ys) - min(ys) > 500:
            continue
        add_path(c, item.get("path") or [], scale=1.04)


def label(c, text, x, y, size=10, color=Color(0.0, 0.2, 0.6)):
    c.setFillColor(color)
    c.setFont("Helvetica", size)
    c.drawString(x, y, text)


with pdfplumber.open(SOURCE) as pdf:
    page = pdf.pages[0]
    size = (page.width, page.height)

    c = canvas.Canvas(str(PART), pagesize=size)
    draw_source(c, page)
    label(c, "A1 CA6140 FORK PART DRAWING - A3", 18, 815, 10)
    label(c, "MATERIAL: HT200   SOURCE: ORIGINAL VECTOR PDF", 18, 802, 8)
    c.showPage()
    c.save()

    c = canvas.Canvas(str(BLANK), pagesize=size)
    draw_source(c, page)
    draw_blank_overlay(c, page)
    label(c, "A1 CA6140 FORK CASTING BLANK - A3", 18, 815, 10, Color(0.6, 0.0, 0.0))
    label(c, "MATERIAL: HT200 / SAND CASTING", 18, 802, 8, Color(0.6, 0.0, 0.0))
    label(c, "RED OVERLAY: BLANK OUTLINE + 2.5 mm SINGLE-SIDE ALLOWANCE (SCHEMATIC)", 18, 789, 8, Color(0.6, 0.0, 0.0))
    c.showPage()
    c.save()

print(PART)
print(BLANK)
