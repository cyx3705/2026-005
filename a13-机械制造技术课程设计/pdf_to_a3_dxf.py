from __future__ import annotations

from pathlib import Path
import math

import pdfplumber


ROOT = Path(__file__).resolve().parent
PDF = ROOT / "CA6140车床拨叉加工零件图-模型(2).pdf"
OUT_DIR = ROOT / "A1"
OUT_DIR.mkdir(parents=True, exist_ok=True)
PART_OUT = OUT_DIR / "A1_CA6140拨叉_零件图_A3.dxf"
BLANK_OUT = OUT_DIR / "A1_CA6140拨叉_毛坯图_A3.dxf"

PT_PER_MM = 72.0 / 25.4


def mm(x):
    return x / PT_PER_MM


def xy(pt):
    return mm(pt[0]), mm(pt[1])


def header():
    return [
        "0", "SECTION", "2", "HEADER",
        "9", "$ACADVER", "1", "AC1009",
        "9", "$INSUNITS", "70", "4",
        "9", "$MEASUREMENT", "70", "1",
        "9", "$EXTMIN", "10", "0.0", "20", "0.0",
        "9", "$EXTMAX", "10", "420.28", "20", "297.04",
        "0", "ENDSEC",
        "0", "SECTION", "2", "TABLES",
        "0", "TABLE", "2", "LTYPE", "70", "1",
        "0", "LTYPE", "2", "CONTINUOUS", "70", "0", "3", "Solid line", "72", "65", "73", "0", "40", "0.0",
        "0", "ENDTAB",
        "0", "TABLE", "2", "LAYER", "70", "4",
        "0", "LAYER", "2", "PDF_GEOMETRY", "70", "0", "62", "7", "6", "CONTINUOUS",
        "0", "LAYER", "2", "FINISHED_REFERENCE", "70", "0", "62", "8", "6", "CONTINUOUS",
        "0", "LAYER", "2", "BLANK_OUTLINE", "70", "0", "62", "1", "6", "CONTINUOUS",
        "0", "LAYER", "2", "NOTES", "70", "0", "62", "3", "6", "CONTINUOUS",
        "0", "ENDTAB",
        "0", "TABLE", "2", "STYLE", "70", "1",
        "0", "STYLE", "2", "STANDARD", "70", "0", "40", "0.0", "41", "1.0", "50", "0.0", "71", "0", "42", "2.5", "3", "txt.shx", "4", "",
        "0", "ENDTAB",
        "0", "ENDSEC",
        "0", "SECTION", "2", "ENTITIES",
    ]


def line_entity(p1, p2, layer="PDF_GEOMETRY"):
    x1, y1 = xy(p1)
    x2, y2 = xy(p2)
    return ["0", "LINE", "8", layer, "10", f"{x1:.6f}", "20", f"{y1:.6f}", "30", "0.0", "11", f"{x2:.6f}", "21", f"{y2:.6f}", "31", "0.0"]


def polyline_entity(points, layer="PDF_GEOMETRY", closed=False, scale=1.0, center=(430.0, 450.0)):
    if len(points) < 2:
        return []
    out = ["0", "POLYLINE", "8", layer, "66", "1", "70", "1" if closed else "0", "10", "0.0", "20", "0.0", "30", "0.0"]
    cx, cy = center
    for x, y in points:
        if scale != 1.0:
            x = cx + (x - cx) * scale
            y = cy + (y - cy) * scale
        xx, yy = xy((x, y))
        out += ["0", "VERTEX", "8", layer, "10", f"{xx:.6f}", "20", f"{yy:.6f}", "30", "0.0"]
    out += ["0", "SEQEND", "8", layer]
    return out


def text_entity(text, x, y, height=3.2, layer="NOTES", rotation=0.0):
    xx, yy = xy((x, y))
    return [
        "0", "TEXT", "8", layer, "10", f"{xx:.6f}", "20", f"{yy:.6f}", "30", "0.0",
        "40", f"{height / PT_PER_MM:.6f}", "1", text, "50", f"{rotation:.6f}",
    ]


def approximate_path(path, steps=8):
    points = []
    current = None
    start = None
    closed = False
    for item in path:
        op = item[0]
        vals = item[1:] if op == "c" else (item[1] if len(item) > 1 else ())
        if op == "m":
            current = vals
            start = vals
            points.append(vals)
        elif op == "l":
            current = vals
            points.append(vals)
        elif op == "c" and current is not None:
            p0 = current
            if len(vals) == 3 and all(isinstance(v, (tuple, list)) for v in vals):
                p1, p2, p3 = vals
            else:
                p1, p2, p3 = vals[0:2], vals[2:4], vals[4:6]
            for i in range(1, steps + 1):
                t = i / steps
                u = 1.0 - t
                x = u**3 * p0[0] + 3 * u**2 * t * p1[0] + 3 * u * t**2 * p2[0] + t**3 * p3[0]
                y = u**3 * p0[1] + 3 * u**2 * t * p1[1] + 3 * u * t**2 * p2[1] + t**3 * p3[1]
                points.append((x, y))
            current = p3
        elif op == "h" and start is not None:
            points.append(start)
            closed = True
            current = start
    return points, closed


def source_entities(page):
    entities = []
    for item in page.lines:
        pts = item.get("pts") or []
        if len(pts) >= 2:
            entities.append(line_entity(pts[0], pts[1]))
    for item in page.rects:
        pts = item.get("pts") or []
        if len(pts) >= 4:
            entities.append(polyline_entity(pts, closed=True))
    for item in page.curves:
        path = item.get("path") or []
        points, closed = approximate_path(path)
        if len(points) >= 2:
            entities.append(polyline_entity(points, closed=closed))
    return entities


def thick_blank_entities(page):
    entities = []
    # Scale only the thick part geometry in the central drawing zone. Thin
    # dimensions, leaders, hatch marks and the title block remain reference data.
    for item in list(page.lines) + list(page.rects) + list(page.curves):
        lw = float(item.get("linewidth") or 0.0)
        if lw < 1.5:
            continue
        pts = item.get("pts") or []
        if not pts:
            path = item.get("path") or []
            pts, closed = approximate_path(path)
        else:
            closed = item.get("object_type") == "rect"
        if not pts:
            continue
        xs = [p[0] for p in pts]
        ys = [p[1] for p in pts]
        if max(xs) < 190 or min(xs) > 650 or max(ys) < 260 or min(ys) > 590:
            continue
        entities.append(polyline_entity(pts, layer="BLANK_OUTLINE", closed=closed, scale=1.04))
    return entities


def write_dxf(path, entities):
    data = header() + [item for entity in entities for item in entity] + ["0", "ENDSEC", "0", "EOF", ""]
    path.write_text("\n".join(data), encoding="ascii", errors="strict")


with pdfplumber.open(PDF) as pdf:
    page = pdf.pages[0]
    base = source_entities(page)
    part_entities = list(base)
    part_entities.append(text_entity("A1 CA6140 FORK PART DRAWING - A3", 18, 286, height=4.0))
    part_entities.append(text_entity("MATERIAL: HT200", 18, 280, height=3.0))
    part_entities.append(text_entity("SOURCE: CA6140 PDF / VERIFY ALL DIMENSIONS", 18, 274, height=2.8))
    part_entities.append(text_entity("A1 - PART DRAWING", 330, 286, height=3.0))
    write_dxf(PART_OUT, part_entities)

    blank_entities = list(base)
    blank_entities += thick_blank_entities(page)
    blank_entities.append(text_entity("A1 CA6140 FORK CASTING BLANK - A3", 18, 286, height=4.0))
    blank_entities.append(text_entity("MATERIAL: HT200 / SAND CASTING", 18, 280, height=3.0))
    blank_entities.append(text_entity("BLANK OUTLINE: FINISHED OUTLINE + 2.5 mm SINGLE-SIDE ALLOWANCE (SCHEMATIC)", 18, 274, height=2.8))
    blank_entities.append(text_entity("RED OUTLINE: BLANK   GRAY/BLACK: FINISHED REFERENCE", 18, 268, height=2.8))
    blank_entities.append(text_entity("A1 - CASTING BLANK DRAWING", 320, 286, height=3.0))
    write_dxf(BLANK_OUT, blank_entities)

print(PART_OUT)
print(BLANK_OUT)
