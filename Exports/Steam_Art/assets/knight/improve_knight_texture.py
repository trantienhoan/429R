"""Improved copy of the knight's armour colour map (user 2026-11-09: "can you re-paint the texture of the knight better?").
The user's design, colours and UVs stay exactly where they are; it only adds depth:
  - soft shading in the creases / under overlaps (ambient occlusion baked from the knight in rest pose on his own UVs:
    knight_ao.png, made by knight_bake_ao.py in Blender)
  - edge highlights on the bevels and trims (from the user's own normal map)
  - a warmer, richer gold (less olive / mustard) and slightly deeper greens; glowing lime parts untouched
  - new clovers (paint_clovers.py): bold four-leaf clovers painted straight onto the surface, not stretched
  - soft "lit from above" volume (tops brighter, undersides darker; no cast shadows), from the baked surface
    directions (knight_bake_normals.py), not on the sword / shield
Writes Knight_Armour_Set_Diffuse_v2.png next to this script. The game texture (Assets/M_Textures/Knight/...) is only read.

    python improve_knight_texture.py [ao=path] [strength=0.55]
"""
import os, sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
TEX = os.path.join(REPO, "Assets", "M_Textures", "Knight", "Knight_Sword_Shield_Armor")
A = dict(a.split("=", 1) for a in sys.argv[1:] if "=" in a)
AO_PATH = A.get("ao", os.path.join(HERE, "knight_ao.png"))
AO_STRENGTH = float(A.get("strength", 0.7))      # 0 = no crease shading, 1 = full
EDGE_GOLD = float(A.get("edge_gold", 0.45))       # edge highlight on gold
EDGE_GREEN = float(A.get("edge_green", 0.22))     # edge highlight on green
TOP_LOW, TOP_HIGH = float(A.get("top_low", 0.78)), float(A.get("top_high", 1.16))   # "lit from above": undersides x
                                                  # TOP_LOW .. tops x TOP_HIGH (no cast shadows; off on sword / shield)
NORMALS = os.path.join(HERE, "knight_normal_os.png")       # knight_bake_normals.py
FRAME = os.path.join(HERE, "knight_frame.txt")
SWORD_SHIELD = os.path.join(HERE, "knight_sword_shield_uv.json")   # knight_dump_uv_mask.py
OUT = os.path.join(HERE, A.get("out", "Knight_Armour_Set_Diffuse_v2.png"))
CLOVERS = A.get("clovers", "1") == "1"            # repaint the clovers (user: they were stretched)


def rgb_to_hsv(c):
    r, g, b = c[..., 0], c[..., 1], c[..., 2]
    mx = c.max(-1); mn = c.min(-1); d = mx - mn
    h = np.zeros_like(mx)
    m = d > 1e-6
    rm = m & (mx == r); gm = m & (mx == g) & ~rm; bm = m & ~rm & ~gm
    h[rm] = ((g - b)[rm] / d[rm]) % 6
    h[gm] = (b - r)[gm] / d[gm] + 2
    h[bm] = (r - g)[bm] / d[bm] + 4
    h = h * 60.0
    s = np.where(mx > 1e-6, d / np.maximum(mx, 1e-6), 0)
    return h, s, mx


def hsv_to_rgb(h, s, v):
    h = (h % 360) / 60.0
    i = np.floor(h).astype(int) % 6
    f = h - np.floor(h)
    p = v * (1 - s); q = v * (1 - s * f); t = v * (1 - s * (1 - f))
    out = np.zeros(h.shape + (3,), np.float32)
    for k, (a, b, c) in enumerate(((v, t, p), (q, v, p), (p, v, t), (p, q, v), (t, p, v), (v, p, q))):
        sel = i == k
        out[sel, 0], out[sel, 1], out[sel, 2] = a[sel], b[sel], c[sel]
    return out


def smooth(x, a, b):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def main():
    D = np.asarray(Image.open(os.path.join(TEX, "Knight_Armour_Set_Diffuse.png")).convert("RGB"), np.float32) / 255.0
    if CLOVERS:                                       # new clovers, painted straight onto the surface (paint_clovers.py)
        sys.path.insert(0, HERE)
        from paint_clovers import paint_clovers
        D, CLOVER_MASK = paint_clovers(D, with_mask=True)
        # where the clovers are: the Steam art keeps its lime glow (the sword blade, eyes) OFF them with this mask
        Image.fromarray((np.clip(CLOVER_MASK, 0, 1) * 255 + 0.5).astype(np.uint8)).save(
            os.path.join(HERE, "Knight_Armour_Set_CloverMask.png"))
    else:
        CLOVER_MASK = None
    N = np.asarray(Image.open(os.path.join(TEX, "Knight_Armour_Set_Normal.png")).convert("RGB"), np.float32) / 255.0
    AO = np.asarray(Image.open(AO_PATH).convert("L"), np.float32) / 255.0
    H, W = D.shape[:2]
    if AO.shape != (H, W):
        AO = np.asarray(Image.fromarray((AO * 255).astype(np.uint8)).resize((W, H), Image.BILINEAR), np.float32) / 255.0
    used = AO > 0.004                                  # texels some face uses (the bake leaves the rest black)
    ao = np.where(used, AO, 1.0)

    # edges: how fast the normal map's direction changes (bevels, trim rims)
    n = N * 2 - 1
    gx = np.abs(np.gradient(n[..., 0], axis=1)) + np.abs(np.gradient(n[..., 1], axis=1))
    gy = np.abs(np.gradient(n[..., 0], axis=0)) + np.abs(np.gradient(n[..., 1], axis=0))
    edge = smooth(gx + gy, 0.04, 0.22)
    # soften the edge mask a little (3x3 box) so highlights are painted strokes, not pixel lines
    e = edge.copy()
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            e += np.roll(np.roll(edge, dy, 0), dx, 1)
    edge = np.clip(e / 6.0, 0, 1)

    h, s, v = rgb_to_hsv(D)
    gold = smooth(s, 0.35, 0.55) * smooth(v, 0.22, 0.4) * (np.abs(((h - 50 + 180) % 360) - 180) < 18)
    green = smooth(s, 0.3, 0.5) * ((h > 85) & (h < 165))
    lime = smooth(v, 0.62, 0.8) * smooth(s, 0.55, 0.75) * ((h > 75) & (h < 130))    # glowing blade / runes: keep
    green = green * (1 - lime)

    # gold: warmer hue, a bit more saturation, more contrast (deep shadows, bright tops)
    hg = h - 6.0 * gold
    sg = np.clip(s * (1 + 0.12 * gold), 0, 1)
    vg = v + gold * (np.clip(v * 1.18 - 0.06, 0, 1) - v)
    # green: slightly richer, slightly deeper
    sg = np.clip(sg * (1 + 0.08 * green), 0, 1)
    vg = vg + green * (np.clip(v * 1.06 - 0.03, 0, 1) - v)
    out = hsv_to_rgb(hg, sg, vg)
    keep = lime[..., None]
    out = out * (1 - keep) + D * keep
    if CLOVER_MASK is not None:                       # the clovers keep their own colours (no gold / green shift)
        cm = CLOVER_MASK[..., None]
        out = out * (1 - cm) + D * cm

    # "lit from above" (painted volume): from the baked surface directions, a wrap light from above and a little in
    # front; not on the sword and shield (another frame in the rest pose) nor on the glowing lime
    if os.path.exists(NORMALS) and os.path.exists(FRAME):
        import json
        from PIL import ImageDraw
        no = np.asarray(Image.open(NORMALS).convert("RGB"), np.float32) / 255.0 * 2 - 1
        up, front = [np.array([float(x) for x in line.split()], np.float32) for line in open(FRAME).read().split("\n")[:2]]
        Ld = up * 1.0 + front * 0.5
        Ld /= np.linalg.norm(Ld)
        lam = (no @ Ld) * 0.5 + 0.5
        top = TOP_LOW + (TOP_HIGH - TOP_LOW) * np.clip(lam, 0, 1) ** 1.2
        skip = Image.new("L", (W, H), 0)
        if os.path.exists(SWORD_SHIELD):
            dr = ImageDraw.Draw(skip)
            for t in json.load(open(SWORD_SHIELD)):
                dr.polygon([(u * W, (1 - v) * H) for u, v in t], fill=255)
        sk = np.asarray(skip, np.float32) / 255.0
        f = np.where(used, top, 1.0)
        f = f * (1 - sk) + sk
        f = f * (1 - lime) + lime
        out = out * f[..., None]
    # crease shading (stronger on gold / green armour, gentler on the face)
    k = AO_STRENGTH * (0.6 + 0.4 * np.maximum(gold, green))
    shade = 1 - k * (1 - np.clip(ao, 0, 1) ** 0.85)
    out = out * shade[..., None] * (1 - keep) + out * keep
    # edge highlights: pale warm gold on gold trims, a lighter green on green
    hi_gold = np.array([1.0, 0.93, 0.62], np.float32)
    hi_green = np.array([0.55, 0.95, 0.45], np.float32)
    eg = (edge * gold * EDGE_GOLD)[..., None]
    en = (edge * green * EDGE_GREEN)[..., None]
    out = out * (1 - eg) + hi_gold * eg
    out = out * (1 - en) + hi_green * en
    # untouched outside the used texels (keeps the smeared padding exactly as it was)
    out = np.where(used[..., None], out, D)
    Image.fromarray((np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8)).save(OUT)
    print("wrote", OUT, "gold %.1f%% green %.1f%% lime %.1f%% of used texels" % (
        100 * gold[used].mean(), 100 * green[used].mean(), 100 * lime[used].mean()))


if __name__ == "__main__":
    main()
