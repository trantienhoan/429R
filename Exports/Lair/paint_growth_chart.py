"""The bedroom lair's growth chart (user 2026-11-26, bedroom lair plan item 8: "a growth chart on the door frame,
pencil lines with little pumpkin heads getting bigger and bigger: the room itself tells that the pumpkin grows").

A strip of old paper pinned to the wall by the chained north door, like a child's height chart: a pencil ruler down
one side and, at rising heights, a pencil line with a little pumpkin head drawn next to it, each one bigger and
angrier than the last (the top one scribbled over in a hurry). No words or numbers (the game's rule: no text on props).
Style: the game's flat colours with soft painted gradients (Exports/Rooftop_Concept/TEXTURE_STYLE.md).

The strip is 0.2 m x 1.6 m in the game (bottom 0.15 m above the floor); the picture is 160 x 1280 px (800 px/m).
Run: python -I paint_growth_chart.py  -> Assets/M_Textures/Lair/Lair_Growth_Chart.png
"""
import math
import os
import random
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "M_Textures", "Lair", "Lair_Growth_Chart.png"))
W, H = 160, 1280
PX_PER_M = 800
BOTTOM = 0.15                     # the strip's bottom above the floor (m)
PAPER = (222, 205, 168)
PAPER_DARK = (176, 150, 108)
PENCIL = (70, 62, 58)
ORANGE = (226, 120, 34)
ORANGE_DARK = (170, 78, 20)
STEM = (96, 112, 46)
FACE = (52, 34, 22)

random.seed(429)


def y_of(height_m):
    """Picture row for a height above the floor (row 0 = the top of the strip)."""
    return int(H - (height_m - BOTTOM) * PX_PER_M)


def paper():
    im = Image.new("RGB", (W, H), PAPER)
    px = im.load()
    for y in range(H):
        for x in range(W):
            # soft darker edges and a few warm stains, flat colours with gentle gradients
            edge = min(x, W - 1 - x, y, H - 1 - y)
            e = max(0.0, 1.0 - edge / 22.0)
            n = 0.5 + 0.5 * math.sin(x * 0.11 + y * 0.023) * math.sin(y * 0.0071 + x * 0.05)
            t = min(1.0, 0.55 * e + 0.08 * n)
            px[x, y] = tuple(int(PAPER[i] * (1 - t) + PAPER_DARK[i] * t) for i in range(3))
    return im


def ruler(d):
    x = 18
    d.line([(x, 6), (x, H - 6)], fill=PENCIL, width=2)
    h = BOTTOM
    i = 0
    while h <= BOTTOM + H / PX_PER_M:
        y = y_of(h)
        long_tick = i % 5 == 0
        d.line([(x, y), (x + (16 if long_tick else 8), y)], fill=PENCIL, width=2 if long_tick else 1)
        h += 0.02 * 5 / 5 * 5      # a tick every 10 cm
        i += 1


def pumpkin(d, cx, cy, r, anger):
    """A small pumpkin head: orange ribs, a stem, a jack-o face that gets meaner with anger 0..1."""
    rx, ry = r * 1.15, r
    for k, dx in enumerate((-0.55, 0.55, 0.0)):
        col = ORANGE_DARK if k < 2 else ORANGE
        d.ellipse([cx + dx * r - rx * 0.62, cy - ry, cx + dx * r + rx * 0.62, cy + ry], fill=col)
    d.ellipse([cx - rx * 0.6, cy - ry * 0.98, cx + rx * 0.6, cy + ry * 0.98], fill=ORANGE)
    d.ellipse([cx - rx * 0.28, cy - ry * 0.85, cx + rx * 0.12, cy - ry * 0.25], fill=(242, 160, 70))  # soft highlight
    d.polygon([(cx - r * 0.12, cy - ry * 0.92), (cx + r * 0.12, cy - ry * 0.92), (cx + r * 0.22, cy - ry * 1.45),
               (cx + r * 0.02, cy - ry * 1.4)], fill=STEM)
    # eyes: from round to slanted angry triangles
    slant = anger * r * 0.35
    for side in (-1, 1):
        ex = cx + side * r * 0.42
        d.polygon([(ex - r * 0.2, cy - r * 0.15 - (slant if side < 0 else 0)),
                   (ex + r * 0.2, cy - r * 0.15 - (slant if side > 0 else 0)),
                   (ex, cy + r * 0.12)], fill=FACE)
    # grin: wider and more teeth as it grows
    teeth = 2 + int(anger * 4)
    pts = []
    for i in range(teeth * 2 + 1):
        t = i / (teeth * 2)
        x = cx - r * 0.62 + t * r * 1.24
        y = cy + r * 0.3 + (r * 0.16 if i % 2 else 0) + math.sin(t * math.pi) * r * 0.22
        pts.append((x, y))
    pts += [(cx + r * 0.5, cy + r * 0.58), (cx - r * 0.5, cy + r * 0.58)]
    d.polygon(pts, fill=FACE)


def mark(d, height, r, anger, scribble=False):
    y = y_of(height)
    d.line([(16, y), (W - 52 - r * 0.2, y + random.randint(-1, 1))], fill=PENCIL, width=2)
    cx = W - 30 - r * 0.5
    pumpkin(d, cx, y - r * 1.05, r, anger)
    if scribble:
        for _ in range(9):
            x0 = random.uniform(cx - r * 1.2, cx + r * 0.6)
            y0 = random.uniform(y - r * 2.1, y - r * 0.2)
            d.line([(x0, y0), (x0 + random.uniform(10, 26), y0 + random.uniform(-14, 14))], fill=PENCIL, width=1)


def main():
    im = paper()
    d = ImageDraw.Draw(im)
    ruler(d)
    # the pumpkin's heights through the nights: small and cheeky at first, huge and furious at the top
    marks = [(0.42, 9, 0.0), (0.66, 12, 0.2), (0.9, 15, 0.4), (1.13, 19, 0.6), (1.36, 23, 0.8), (1.6, 28, 1.0)]
    for i, (h, r, anger) in enumerate(marks):
        mark(d, h, r, anger, scribble=(i == len(marks) - 1))
    # two pins at the top
    for x in (30, W - 30):
        d.ellipse([x - 6, 10, x + 6, 22], fill=(150, 40, 40))
        d.ellipse([x - 3, 12, x + 1, 16], fill=(220, 120, 110))
    im = im.filter(ImageFilter.SMOOTH)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    im.save(OUT)
    print("wrote", OUT, im.size)


if __name__ == "__main__":
    main()
