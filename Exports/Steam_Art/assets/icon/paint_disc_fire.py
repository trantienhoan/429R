"""Green fire for the icon's disc, like the user's painted badge: flame tongues rising up both sides (the knight covers
the middle) from behind the digits, wide at the base and licking upward, flickering, bright cores, coloured by a fire
ramp (dark green -> green -> lime -> pale yellow-green), soft glow and a few sparkles.
Writes disc_fire.png (RGB glow colour; black = none) next to this script; the disc maps it across its face.

    python paint_disc_fire.py        (system Python with Pillow + numpy)
"""
import os, math, random
import numpy as np
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
S = 1024
rnd = random.Random(429)
yy, xx = np.mgrid[0:S, 0:S].astype(np.float32)
U = xx / S * 2 - 1
V = 1 - yy / S * 2


def value_noise(scale, seed):
    r = np.random.default_rng(seed)
    g = r.random((scale + 3, scale + 3)).astype(np.float32)
    x = (U + 1) / 2 * scale; y = (V + 1) / 2 * scale
    x0 = np.floor(x).astype(int); y0 = np.floor(y).astype(int)
    fx = x - x0; fy = y - y0
    fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy)
    a = g[y0, x0]; b = g[y0, x0 + 1]; c = g[y0 + 1, x0]; d = g[y0 + 1, x0 + 1]
    return a * (1 - fx) * (1 - fy) + b * fx * (1 - fy) + c * (1 - fx) * fy + d * fx * fy


def fbm(seed, octaves=4, base=4):
    out = np.zeros_like(U); amp = 1.0; tot = 0
    for o in range(octaves):
        out += value_noise(base * 2 ** o, seed + o) * amp
        tot += amp; amp *= 0.5
    return out / tot


field = np.zeros_like(U)
warp = (fbm(7, 3, 3) - 0.5) * 0.12                     # sideways wobble of every tongue (flicker)
for side in (-1, 1):
    for i in range(11 if side < 0 else 9):
        # like the badge: the wisps rise beside his head (above the digits), not behind the 429; the right ones sit
        # further out (his helmet and left fist cover the inner part there)
        bx = side * (rnd.uniform(0.45, 0.86) if side < 0 else rnd.uniform(0.66, 0.9))
        by = rnd.uniform(0.2, 0.34)                         # their bases just above the digits' top
        h = rnd.uniform(0.45, 0.75) * (1.1 - 0.3 * abs(abs(bx) - 0.64) / 0.22)   # tall, up beside his head
        w = rnd.uniform(0.03, 0.05)                       # flame tongues, max-combined so dark gaps stay between
        amp = rnd.uniform(0.05, 0.13); f = rnd.uniform(0.7, 1.4); ph = rnd.uniform(0, 6.28)
        lean = side * rnd.uniform(-0.05, 0.12)               # a little outward, as if round the knight
        t = (V - by) / h
        ok = (t > -0.15) & (t < 1.0)
        tc = np.clip(t, 0, 1)
        xc = bx + lean * tc + amp * np.sin(2 * math.pi * f * tc + ph) * tc + warp
        hw = w * np.clip(1 - tc, 0, 1) ** 0.8 * (0.75 + 0.25 * np.sin(math.pi * np.clip(tc + 0.2, 0, 1)))
        hw = np.maximum(hw, 1e-3)
        base_fade = np.clip((t + 0.15) / 0.2, 0, 1)
        k = np.exp(-((U - xc) / hw) ** 2) * (1 - tc) ** 0.45 * base_fade
        field = np.maximum(field, np.where(ok, k, 0.0) * rnd.uniform(0.75, 1.0))   # max: overlaps don't fuse into a blob
# low flames along the bottom of the disc, under the digits (the badge's fire swirls round the bottom too)
for i in range(9):
    bx = -0.6 + 1.2 * i / 8 + rnd.uniform(-0.04, 0.04)
    by = -0.97 + 0.12 * (abs(bx) / 0.62) ** 2
    h = rnd.uniform(0.2, 0.32); w = rnd.uniform(0.05, 0.085)   # a soft glow under the digits (v6 neon, v8 dead: between)
    t = (V - by) / h
    ok = (t > -0.15) & (t < 1.0)
    tc = np.clip(t, 0, 1)
    xc = bx + 0.05 * np.sin(6.0 * tc + i) * tc + warp
    hw = np.maximum(w * np.clip(1 - tc, 0, 1) ** 0.8, 1e-3)
    k = np.exp(-((U - xc) / hw) ** 2) * (1 - tc) ** 0.45 * np.clip((t + 0.15) / 0.2, 0, 1)
    field = np.maximum(field, np.where(ok, k, 0.0) * rnd.uniform(0.5, 0.7))
# soft swirling wisps all round the disc, like the badge's smoky green swirls (mid green, no hot cores): each an S-curve
# drawn as soft dots along it
sw = np.zeros_like(U)
for i in range(10):
    # round the two sides only (at the bottom they read as a neon zigzag under the digits)
    side_ = -1 if i % 2 == 0 else 1
    ang = (math.pi if side_ < 0 else 0.0) + side_ * (-0.55 + 1.25 * (i // 2) / 4.0) + rnd.uniform(-0.1, 0.1)
    r0 = rnd.uniform(0.62, 0.86)
    cx0, cy0 = r0 * math.cos(ang), r0 * math.sin(ang)
    tx, ty = -math.sin(ang), math.cos(ang)                             # along the ring
    L = rnd.uniform(0.35, 0.6); A_ = rnd.uniform(0.05, 0.1); fq = rnd.uniform(1.2, 2.2); ph = rnd.uniform(0, 6.28)
    wd = rnd.uniform(0.02, 0.032); inten = rnd.uniform(0.45, 0.62)     # smoky lime swirls (the badge's fill the disc)
    for k in range(160):
        t = k / 159.0 - 0.5
        px = cx0 + tx * L * t + math.cos(ang) * A_ * math.sin(fq * 6.28 * t + ph)
        py = cy0 + ty * L * t + math.sin(ang) * A_ * math.sin(fq * 6.28 * t + ph)
        if math.hypot(px, py) > 0.93:
            continue
        cxp, cyp = int((px + 1) / 2 * S), int((1 - py) / 2 * S)
        rw = int(wd * S * 1.6) + 2
        ya, yb, xa, xb = max(0, cyp - rw), min(S, cyp + rw), max(0, cxp - rw), min(S, cxp + rw)
        d2 = ((U[ya:yb, xa:xb] - px) ** 2 + (V[ya:yb, xa:xb] - py) ** 2) / (wd * wd)
        taper = math.sin(math.pi * (t + 0.5)) ** 0.7
        sw[ya:yb, xa:xb] = np.maximum(sw[ya:yb, xa:xb], np.exp(-d2) * inten * taper)
field = np.maximum(field, sw)
# behind the 429 band the field stays calm dark green (the digits must read on it)
behind = np.clip((V + 0.62) / 0.08, 0, 1) * np.clip((0.3 - V) / 0.1, 0, 1)
field = field * (1 - 0.45 * behind)
# flicker: break the tongues up a little, more towards their tips
n = fbm(11, 4, 5)
field = field * (0.55 + 0.75 * n)
field = np.clip(field * 0.9, 0, 1.0)                      # never white-hot: the brightest is lime-yellow
glow = np.asarray(Image.fromarray(np.clip(field / 1.6 * 255, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(22)),
                  np.float32) / 255.0
# fire colour ramp
stops = [(0.0, (0, 0, 0)), (0.18, (0.01, 0.16, 0.02)), (0.42, (0.05, 0.58, 0.04)), (0.7, (0.3, 0.95, 0.08)),
         (0.95, (0.6, 1.0, 0.2)), (1.3, (0.75, 1.0, 0.35))]
img = np.zeros(U.shape + (3,), np.float32)
for (a, ca), (b, cb) in zip(stops[:-1], stops[1:]):
    m = (field >= a) & (field < b)
    t = ((field - a) / (b - a))[..., None]
    img = np.where(m[..., None], np.array(ca) * (1 - t) + np.array(cb) * t, img)
img = np.where((field >= stops[-1][0])[..., None], np.array(stops[-1][1]), img)
img += np.clip(glow * 1.4, 0, 1)[..., None] * np.array([0.015, 0.26, 0.03])   # a soft glow; the gaps stay darker
# sparkles (embers) mostly above the flames
for _ in range(60):
    x, y = rnd.uniform(-0.92, 0.92), rnd.uniform(-0.5, 0.92)
    if math.hypot(x, y) > 0.92 or abs(x) < 0.28:
        continue
    px, py = (x + 1) / 2 * S, (1 - (y + 1) / 2) * S
    r = rnd.uniform(1.4, 3.6)
    d = np.hypot(xx - px, yy - py)
    img += (np.exp(-(d / r) ** 2) * rnd.uniform(0.5, 1.0))[..., None] * np.array([0.75, 1.0, 0.55])
rr = np.hypot(U, V)
img *= np.clip((1.0 - rr) / 0.05, 0, 1)[..., None]
Image.fromarray((np.clip(img, 0, 1) * 255 + 0.5).astype(np.uint8)).save(os.path.join(HERE, "disc_fire.png"))
print("wrote disc_fire.png")
