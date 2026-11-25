"""The storage room's specimen board card (2026-11-25, the lair concept: "4 spiders turning into a pumpkin spider").

Painted from top-down renders of our own Spider_Normal (Spider_Smol mesh) in its old look (Spider_Smol_Mat) and its
pumpkin look (Spider_Smol_Pumpkin_Mat), each rendered on black and on white (the pair gives the alpha):
Exports/Lair/specimen/<material>_black.png / _white.png (512 x 512, from the Unity render script in NOTES).
Four pinned spiders on old paper: I the old spider, II and III the pumpkin spreading out from the belly, IV the
pumpkin spider with a faint green glow (the Green Power). Flat colours and soft gradients.
Writes Assets/M_Textures/Lair/Lair_Specimen_Card.png. Run: python -I paint_specimen_card.py
"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "specimen")
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "M_Textures", "Lair", "Lair_Specimen_Card.png"))
SIZE = 512


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def noise(shape, cells, seed):
    rng = np.random.default_rng(seed)
    small = rng.random((cells, cells)).astype(np.float32)
    return np.asarray(Image.fromarray((small * 255).astype(np.uint8)).resize(shape[::-1], Image.BICUBIC), float) / 255.0


def cutout(name):
    """RGBA of a render from its black- and white-background pictures."""
    b = np.asarray(Image.open(os.path.join(SRC, name + "_black.png")).convert("RGB"), float)
    w = np.asarray(Image.open(os.path.join(SRC, name + "_white.png")).convert("RGB"), float)
    a = np.clip(1.0 - (w - b).mean(axis=2) / 255.0, 0.0, 1.0)
    rgb = np.where(a[..., None] > 0.01, b / np.maximum(a[..., None], 0.01), 0.0)
    return np.clip(rgb, 0, 255), a


def paper():
    yy, xx = np.mgrid[0:SIZE, 0:SIZE] / SIZE
    rgb = np.zeros((SIZE, SIZE, 3)) + np.array([214, 196, 150], float)
    rgb *= (0.92 + 0.12 * noise((SIZE, SIZE), 6, 3))[..., None]
    edge = np.minimum(np.minimum(xx, 1 - xx), np.minimum(yy, 1 - yy))
    rgb *= (0.72 + 0.28 * smoothstep(0.0, 0.16, edge))[..., None]
    # A few tea-coloured stains.
    stains = smoothstep(0.62, 0.8, noise((SIZE, SIZE), 9, 17))
    rgb = rgb * (1 - 0.18 * stains[..., None]) + np.array([150, 112, 62]) * 0.18 * stains[..., None]
    # A thin dark ink border.
    border = (edge > 0.035) & (edge < 0.043)
    rgb[border] = rgb[border] * 0.45
    return rgb


def place(card, rgb, a, centre, size, glow=None):
    """Scale an RGBA spider to size px and lay it on the card at centre, with a soft shadow (and an optional glow)."""
    im = Image.fromarray(np.dstack([rgb, a * 255]).astype(np.uint8), "RGBA").resize((size, size), Image.LANCZOS)
    x0, y0 = centre[0] - size // 2, centre[1] - size // 2
    alpha = np.asarray(im, float)[..., 3] / 255.0
    if glow is not None:
        halo = np.asarray(Image.fromarray((alpha * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(size * 0.06)), float) / 255.0
        halo = np.clip(halo * 1.6, 0, 1) * glow
        sub = card[y0:y0 + size, x0:x0 + size]
        sub[:] = sub * (1 - halo[..., None] * 0.55) + np.array([120, 255, 90]) * halo[..., None] * 0.55
    shadow = np.asarray(Image.fromarray((alpha * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(size * 0.025)), float) / 255.0
    sh = np.zeros_like(shadow)
    d = max(2, size // 40)
    sh[d:, d:] = shadow[:-d, :-d]
    sub = card[y0:y0 + size, x0:x0 + size]
    sub *= (1 - 0.35 * sh)[..., None]
    col = np.asarray(im, float)[..., :3]
    sub[:] = sub * (1 - alpha[..., None]) + col * alpha[..., None]


def blend_stage(old, new, amount, seed):
    """The pumpkin look spreading out from the belly: amount 0 = old, 1 = new."""
    (orgb, oa), (nrgb, na) = old, new
    yy, xx = np.mgrid[0:SIZE, 0:SIZE]
    belly = np.array([256, 318])            # the pumpkin belly's stem in the 512 render
    dist = np.hypot(xx - belly[0], yy - belly[1]) + (noise((SIZE, SIZE), 14, seed) - 0.5) * 70
    reach = 40 + amount * 260
    m = 1.0 - smoothstep(reach - 18, reach + 18, dist)
    rgb = orgb * (1 - m[..., None]) + nrgb * m[..., None]
    return rgb, np.maximum(oa * (1 - m), na * m)


def numeral(draw, text, cx, cy, h, colour):
    """Roman numerals from strokes (I and V only), centred at cx, cy."""
    w_i, w_v, gap = h * 0.12, h * 0.55, h * 0.18
    widths = [w_i if c == "I" else w_v for c in text]
    x = cx - (sum(widths) + gap * (len(text) - 1)) / 2
    top, bot = cy - h / 2, cy + h / 2
    lw = max(2, int(h * 0.13))
    for c, wd in zip(text, widths):
        if c == "I":
            draw.line([(x + wd / 2, top), (x + wd / 2, bot)], fill=colour, width=lw)
        else:
            draw.line([(x, top), (x + wd / 2, bot), (x + wd, top)], fill=colour, width=lw, joint="curve")
        x += wd + gap


def main():
    old = cutout("Spider_Smol_Mat")
    new = cutout("Spider_Smol_Pumpkin_Mat")
    card = paper()
    stages = [old, blend_stage(old, new, 0.16, 5), blend_stage(old, new, 0.6, 9), new]
    centres = [(134, 130), (378, 130), (134, 356), (378, 356)]
    for i, ((rgb, a), c) in enumerate(zip(stages, centres)):
        place(card, rgb, a, c, 196, glow=1.0 if i == 3 else None)
    img = Image.fromarray(np.clip(card, 0, 255).astype(np.uint8))
    d = ImageDraw.Draw(img)
    ink = (52, 36, 26)
    for i, c in enumerate(centres):
        # The pin through the back, a dark head with a light spot.
        px, py = c[0], c[1] - 34
        d.ellipse([px - 7, py - 7, px + 7, py + 7], fill=(40, 30, 30))
        d.ellipse([px - 3, py - 4, px + 1, py], fill=(200, 190, 180))
        # A small paper tag with the stage number.
        tx, ty = c[0], c[1] + 98
        d.rectangle([tx - 26, ty - 11, tx + 26, ty + 11], fill=(232, 220, 186), outline=ink, width=2)
        numeral(d, ["I", "II", "III", "IV"][i], tx, ty, 14, ink)
    # Arrows: I -> II, II -> III (down-left), III -> IV.
    for (x0, y0, x1, y1) in ((222, 130, 290, 130), (318, 214, 196, 270), (222, 356, 290, 356)):
        d.line([(x0, y0), (x1, y1)], fill=ink, width=3)
        v = np.array([x1 - x0, y1 - y0], float); v /= np.linalg.norm(v)
        n = np.array([-v[1], v[0]])
        tip = np.array([x1, y1])
        d.polygon([tuple(tip), tuple(tip - v * 12 + n * 6), tuple(tip - v * 12 - n * 6)], fill=ink)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    img.save(OUT)
    print("wrote", OUT)


if __name__ == "__main__":
    main()
