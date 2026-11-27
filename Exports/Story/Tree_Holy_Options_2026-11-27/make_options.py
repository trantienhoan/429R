# Textures for a holier Pumpkin Tree (2026-11-27). Reads the ORIGINAL textures, writes only into this folder:
# opt_* mock-ups, and green/*.png = look B (the user's pick), which Tools > 429 Game > Story > Pumpkin Tree: The Seed's
# Green copies into the texture copies. Run: python make_options.py (numpy + Pillow).
import numpy as np
from PIL import Image

import os, shutil
HERE = os.path.dirname(os.path.abspath(__file__)) + "/"
SRC = HERE + "../../../Assets/"   # the repo's Assets (this file is in Exports/Story/Tree_Holy_Options_2026-11-27)
OUT = HERE

A = np.asarray(Image.open(SRC + "M_Textures/Pumpkin_Tree/Pumpkin_Tree_Pumpkin_Tree_AlbedoTransparency.png").convert("RGB")).astype(np.float32) / 255
E = np.asarray(Image.open(SRC + "M_Textures/Pumpkin_Tree/Pumpkin_Tree_Pumpkin_Tree_Emission.png").convert("RGB")).astype(np.float32) / 255
L = np.asarray(Image.open(SRC + "Images/pumpkin_leaves.png").convert("RGBA")).astype(np.float32) / 255
H, W, _ = A.shape


def shift(img, dy, dx):
    return np.roll(np.roll(img, dy, axis=0), dx, axis=1)


def dilate(mask, n):
    m = mask.copy()
    for _ in range(n):
        m = m | shift(m, 1, 0) | shift(m, -1, 0) | shift(m, 0, 1) | shift(m, 0, -1)
    return m


def blur(img, n):
    out = img.copy()
    for _ in range(n):
        out = (out + shift(out, 1, 0) + shift(out, -1, 0) + shift(out, 0, 1) + shift(out, 0, -1)) / 5
    return out


# The carvings on the trunk (claw slashes, the angry face and its drips): where the glow map is bright, left of the
# separate orange patches (x < 1640), grown a bit to take their dark outlines too.
glow = E.max(axis=2)
xs = np.arange(W)[None, :].repeat(H, axis=0)
near = dilate((glow > 0.18) & (xs < 1640), 30)
orange = near & (A[..., 0] > 0.62) & (A[..., 0] - A[..., 1] > 0.28) & (A[..., 2] < 0.28)
carve = dilate(((glow > 0.18) | orange) & (xs < 1640), 9)
# The separate orange carved-face patches on the right.
patches = dilate((xs >= 1640) & ((glow > 0.45) | ((A[..., 0] > 0.6) & (A[..., 0] - A[..., 1] > 0.25) & (A[..., 2] < 0.35))), 4) & (xs >= 1600)

# Paint the carvings out: fill from the bark around them, then put back bark grain taken from just above.
fill = A.copy()
known = ~carve
for _ in range(80):
    acc = np.zeros_like(A)
    cnt = np.zeros((H, W, 1), np.float32)
    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1), (2, 2), (-2, -2), (2, -2), (-2, 2)):
        k = shift(known, dy, dx)[..., None].astype(np.float32)
        acc += shift(fill, dy, dx) * k
        cnt += k
    new = (~known) & (cnt[..., 0] > 0)
    fill[new] = acc[new] / cnt[new]
    known |= new
    if known.all():
        break
# Real bark copied over each carving from a spot nearby (above first: the trunk's grain runs up and down; then below,
# then the sides), never from another carving, toned to the bark round it, with soft edges.
src = A.copy()
taken = np.zeros((H, W), bool)
for dy, dx in ((160, 0), (-160, 0), (0, 170), (0, -170), (240, 0), (-240, 0), (110, 120), (110, -120)):
    cand = shift(A, dy, dx)
    ok = carve & ~taken & ~shift(carve, dy, dx)
    src[ok] = cand[ok]
    taken |= ok
src[carve & ~taken] = fill[carve & ~taken]
tone = blur(fill, 24) - blur(np.where(carve[..., None], src, fill), 24)
src = np.clip(src + tone * 0.8, 0, 1)
alpha = np.clip(blur(carve.astype(np.float32)[..., None], 5) * 1.6, 0, 1)
clean = A * (1 - alpha) + src * alpha
print("carving px from bark nearby", int(taken.sum()), "of", int(carve.sum()))

lum = (clean * np.array([0.3, 0.55, 0.15], np.float32)).sum(axis=2)
lo, hi = np.percentile(lum, 3), np.percentile(lum, 97)
t = np.clip((lum - lo) / (hi - lo), 0, 1)[..., None]


def bark(dark, light, keep=0.25):
    dark = np.array(dark, np.float32)
    light = np.array(light, np.float32)
    out = dark * (1 - t) + light * t
    # keep a little of the painted colour (moss, warm streaks)
    out = out * (1 - keep) + clean * keep * (light.mean() / max(clean.mean(), 1e-3))
    return np.clip(out, 0, 1)


def save(arr, name, alpha=None):
    a = (np.clip(arr, 0, 1) * 255 + 0.5).astype(np.uint8)
    if alpha is not None:
        a = np.dstack([a, (alpha * 255 + 0.5).astype(np.uint8)])
    Image.fromarray(a).save(OUT + name)


def leaves(tint_light, tint_dark):
    rgb, al = L[..., :3], L[..., 3]
    ll = (rgb * np.array([0.3, 0.55, 0.15], np.float32)).sum(axis=2)
    lt = np.clip((ll - 0.25) / 0.6, 0, 1)[..., None]
    out = np.array(tint_dark, np.float32) * (1 - lt) + np.array(tint_light, np.float32) * lt
    # the veins and edges stay darker, as painted
    out = out * (0.55 + 0.45 * np.clip(ll / 0.7, 0, 1))[..., None]
    return out, al


# 1. GOLD: the Tree of Light. Pale warm ash bark, a soft golden glow along its grain, golden leaves.
b1 = bark((0.62, 0.52, 0.38), (1.0, 0.96, 0.84), keep=0.12)
b1[patches] = np.array([1.0, 0.86, 0.5])
save(b1, "opt_gold_bark.png")
e1 = np.array([1.0, 0.8, 0.42], np.float32) * (0.08 + 0.42 * t ** 1.8)
e1[patches] = np.array([1.0, 0.85, 0.5]) * 0.8
save(e1, "opt_gold_emis.png")
l1, a1 = leaves((1.0, 0.97, 0.72), (1.0, 0.78, 0.3))
save(l1, "opt_gold_leaves.png", a1)

# 2. GREEN: the Seed's green (the girl, the Green Protector). Lighter warm bark, green light along its grain,
# fresh spring leaves with a light glow.
b2 = bark((0.48, 0.38, 0.26), (0.92, 0.84, 0.66), keep=0.15)
# The carved face's eye and mouth hollows (model geometry) use these patches: dark bark, no glow, so they read as
# knot holes, not glowing eyes (2026-11-27: a green glow there still made a face once the stone pumpkin broke).
b2[patches] = shift(b2, 0, -420)[patches] * 0.45
save(b2, "opt_green_bark.png")
e2 = np.array([0.55, 1.0, 0.45], np.float32) * (0.04 + 0.4 * t ** 2.2)
e2[patches] = 0
save(e2, "opt_green_emis.png")
l2, a2 = leaves((0.85, 1.0, 0.6), (0.35, 0.75, 0.25))
save(l2, "opt_green_leaves.png", a2)

# 3. The carvings painted out only, colours as they are (to see the face's part alone).
save(clean, "opt_clean_bark.png")
e3 = E.copy()
e3[carve] = 0
save(e3, "opt_clean_emis.png")
print("carve px", int(carve.sum()), "patch px", int(patches.sum()))

# Look B, the user's pick: the names the Unity menu reads.
os.makedirs(OUT + "green", exist_ok=True)
for a, b in (("opt_green_bark.png", "Pumpkin_Tree_Green_Albedo.png"), ("opt_green_emis.png", "Pumpkin_Tree_Green_Emission.png"),
             ("opt_green_leaves.png", "pumpkin_leaves_green.png")):
    shutil.copyfile(OUT + a, OUT + "green/" + b)
print("green/ written")
