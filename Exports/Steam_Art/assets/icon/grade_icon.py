"""Colour-grades the 3D icon render to the palette of the user's painted badge (user 2026-11-09: "the color is so off").
Measured on the badge (assets/icon/old_steam_icon_badge.webp): greens hue ~117 (emerald), saturation ~0.97; gold hue
~34 (orange-gold), saturation ~0.96, value ~0.88; overall value ~0.52. The scene's own paints are yellow-green (~94)
and yellow gold (~44-54), so a hue-band grade moves each band onto the badge's: hue shift per band (blended smoothly
between bands), saturation and value pulled toward the badge's medians. Works on straight-alpha RGBA PNGs.

    python grade_icon.py in.png out.png [strength=1.0] [green_hue=117] [gold_hue=34] [lift=1.0] [green_gamma=1.0]
"""
import sys
import numpy as np
from PIL import Image

A = dict(a.split("=", 1) for a in sys.argv[3:] if "=" in a)
STRENGTH = float(A.get("strength", 1.0))
GREEN_HUE = float(A.get("green_hue", 117.0))
GOLD_HUE = float(A.get("gold_hue", 34.0))
GREEN_SAT = float(A.get("green_sat", 0.92))
GOLD_SAT = float(A.get("gold_sat", 0.93))
GOLD_VAL = float(A.get("gold_val", 0.88))
LIFT = float(A.get("lift", 1.0))           # overall value gain (1 = only what the band targets do)
GREEN_DARK_HUE = float(A.get("green_dark_hue", 135.0))   # dark greens: emerald (the badge's digit faces ~135)
GREEN_LIGHT_HUE = float(A.get("green_light_hue", 80.0))   # bright greens: lime (the badge's wisps ~88)
GOLD_DARK_HUE = float(A.get("gold_dark_hue", 26.0))       # gold shadows: amber-orange
GOLD_LIGHT_HUE = float(A.get("gold_light_hue", 34.0))     # gold highlights: warm gold (not lemon)
GOLD_REL = float(A.get("gold_rel", 0.35))                 # share of each gold pixel's own hue variation kept
GREEN_REL = float(A.get("green_rel", 0.6))
CORE_HUE = float(A.get("core_hue", 80.0))                 # the hottest glow (eye and flame cores): saturated lime-yellow
CORE_SAT = float(A.get("core_sat", 0.86))
CORE_PALE_HUE = float(A.get("core_pale_hue", 62.0))       # the very middle of each glow: pale yellow (#ECFB55)
CORE_PALE_SAT = float(A.get("core_pale_sat", 0.55))
HALO = float(A.get("halo", 0.4))                         # soft lime halo round the glows                 # (the judges: pale cream cores looked washed out)
BROWN_HUE = float(A.get("brown_hue", 13.0))               # face and paws: chestnut red-brown
BROWN_SAT = float(A.get("brown_sat", 0.86))
GOLD_LIP_HUE = float(A.get("gold_lip_hue", 46.0))
GOLD_SHADOW_HUE = float(A.get("gold_shadow_hue", 24.0))   # gold in shadow: burnt orange-brown, never khaki
GUTTER_PX = float(A.get("gutter_px", 6.0))                # the badge's near-black line between the disc and the ring
RING_FROM = float(A.get("ring_from", 0.95))      # the gold ring = gold pixels beyond this share of the disc radius
RING_FALL = float(A.get("ring_fall", 0.36))      # its value at the very bottom x (1 - this): amber-brown, like the badge
RING_HUE_DROP = float(A.get("ring_hue_drop", 3.0))
# the eyes, painted from the middle out (the badge's: a small pale-yellow core, saturated lime body, deeper lime rim)
LEAF_EYE = A.get("leaf_eye", "knight_left")     # the weed leaf: in the knight's LEFT eye (= the screen-right one); "none" = off
LEAF_ALPHA = float(A.get("leaf_alpha", 0.7))      # how much it darkens the glow (subtle)
LEAF_TINT = (0.55, 0.72, 0.40)                    # the glow x this inside the leaf: a deeper green leaf in the lime
EYE_STOPS = [(0.0, (0.99, 0.99, 0.93)), (0.18, (0.96, 0.98, 0.58)), (0.40, (0.84, 0.98, 0.18)),
             (0.80, (0.68, 0.96, 0.04)), (1.0, (0.50, 0.90, 0.02))]   # the badge's: white-hot middle, yellow-lime, lime rim
GOLD_KNEE = float(A.get("gold_knee", 0.76))               # gold highlights roll off above this (no flat clipped neon)
BLACK = float(A.get("black", 0.07))
TOE = float(A.get("toe", 0.3))
SHADOW_HUE = float(A.get("shadow_hue", 115.0))            # near-black shadows tinted green-black
BROWN_MAX = float(A.get("brown_max", 0.45))     # gold-hued pixels darker than this are browns (kept)
HELMET_MASK = A.get("helmet")                    # helmet coverage PNG (alpha) from icon_render.py
HELMET_HUE = float(A.get("helmet_hue", 96.0))    # the badge's helmet: olive / grass green (#2C6E03..#3B7C02)
HELMET_VAL = float(A.get("helmet_val", 1.25))
HELMET_GOLD = float(A.get("helmet_gold", 1.3))   # the visor frame's gold x this (it read dim copper)
FIELD_MASK = A.get("field")                      # the disc's coverage PNG (alpha) from icon_render.py
FIELD_HUE = float(A.get("field_hue", 117.0))     # the badge's field: saturated emerald (#034304..#064C02, measured h 117-119)
FIELD_LIGHT_HUE = float(A.get("field_light_hue", 86.0))   # its swirls/flames: green (#189103 h 109) -> lime only at the hot cores
FIELD_VAL = float(A.get("field_val", 1.3))       # its darks x this (it read too dark and empty: v 0.18 vs the badge's 0.26-0.30)     # its greens x this
GREEN_GAMMA = float(A.get("green_gamma", 1.0))   # >1 darkens the mid greens (enamel, disc) and keeps the bright ones
                                                  # (fire, eyes): the badge's dark-green fills inside bright gold


def rgb2hsv(c):
    mx = c.max(-1); mn = c.min(-1); d = mx - mn
    r, g, b = c[..., 0], c[..., 1], c[..., 2]
    h = np.zeros_like(mx); m = d > 1e-6
    rm = m & (mx == r); gm = m & (mx == g) & ~rm; bm = m & ~rm & ~gm
    h[rm] = ((g - b)[rm] / d[rm]) % 6; h[gm] = (b - r)[gm] / d[gm] + 2; h[bm] = (r - g)[bm] / d[bm] + 4
    return h * 60.0, np.where(mx > 1e-6, d / np.maximum(mx, 1e-6), 0.0), mx


def hsv2rgb(h, s, v):
    h = (h % 360.0) / 60.0
    i = np.floor(h).astype(int) % 6
    f = h - np.floor(h)
    p = v * (1 - s); q = v * (1 - s * f); t = v * (1 - s * (1 - f))
    out = np.zeros(h.shape + (3,), np.float32)
    for k, (a, b, c) in enumerate(((v, t, p), (q, v, p), (p, v, t), (p, q, v), (t, p, v), (v, p, q))):
        sel = i == k
        out[sel, 0], out[sel, 1], out[sel, 2] = a[sel], b[sel], c[sel]
    return out


def band(h, lo, hi, soft):
    """1 inside [lo, hi] degrees, fading to 0 over `soft` degrees outside."""
    a = np.clip((h - (lo - soft)) / soft, 0, 1)
    b = np.clip(((hi + soft) - h) / soft, 0, 1)
    return np.minimum(a, b)


def _mask(path, shape):
    """A mask PNG's alpha (0..1) at this image's size; zeros if there is none."""
    if not path:
        return np.zeros(shape, np.float32)
    try:
        mi = Image.open(path).convert("RGBA")
    except OSError:
        print("WARNING: no mask", path)
        return np.zeros(shape, np.float32)
    if mi.size != (shape[1], shape[0]):
        mi = mi.resize((shape[1], shape[0]), Image.LANCZOS)
    return np.asarray(mi, np.float32)[..., 3] / 255.0


def leaf_mask(H_, W_, cx, cy, ay):
    """A seven-leaflet cannabis leaf, upright, centred in an eye (cx, cy, half-height ay), anti-aliased: 0..1."""
    import math
    from PIL import ImageDraw
    unit = 0.88 * ay                                  # the whole leaf is about 0.55 of the eye's height (subtle)
    bx, by = cx, cy + 0.42 * unit                     # where the leaflets meet
    SS = 4
    x0, y0 = int(cx - 1.6 * ay), int(cy - 1.6 * ay)
    bw = bh = int(3.2 * ay) + 2
    im = Image.new("L", (bw * SS, bh * SS), 0)
    dr = ImageDraw.Draw(im)
    def P(x, y):
        return ((x - x0) * SS, (y - y0) * SS)
    for ang, L, Wd in ((0, 1.0, 0.13), (27, 0.86, 0.12), (-27, 0.86, 0.12), (55, 0.62, 0.10), (-55, 0.62, 0.10),
                       (85, 0.36, 0.07), (-85, 0.36, 0.07)):
        a_ = math.radians(ang)
        dx, dy = math.sin(a_), -math.cos(a_)          # up = -y in the image
        px_, py_ = -dy, dx
        left, right = [], []
        for k in range(41):
            t = k / 40.0
            hw = Wd * L * math.sin(math.pi * t ** 0.75) ** 0.9 * (0.86 + 0.14 * abs(math.sin(t * 9 * math.pi)))
            ax_, ay_ = bx + dx * t * L * unit, by + dy * t * L * unit
            left.append(P(ax_ + px_ * hw * unit, ay_ + py_ * hw * unit))
            right.append(P(ax_ - px_ * hw * unit, ay_ - py_ * hw * unit))
        dr.polygon(left + right[::-1], fill=255)
    sw_ = 0.025 * unit                                 # the stem
    dr.polygon([P(bx - sw_, by), P(bx + sw_, by), P(bx + sw_ * 0.7, by + 0.2 * unit), P(bx - sw_ * 0.7, by + 0.2 * unit)], fill=255)
    small = np.asarray(im.resize((bw, bh), Image.LANCZOS), np.float32) / 255.0
    m = np.zeros((H_, W_), np.float32)
    ya, xa = max(0, y0), max(0, x0)
    yb, xb = min(H_, y0 + bh), min(W_, x0 + bw)
    m[ya:yb, xa:xb] = small[ya - y0:yb - y0, xa - x0:xb - x0]
    return np.clip(m, 0, 1)


def paint_eyes(out, h, s, v, vis):
    """Find his two round glowing eyes (lime or blown-white discs in the top half) and paint them from the middle out
    with EYE_STOPS, anti-aliased. Returns (out, eye weight) or (out, None) if two eyes aren't found."""
    from scipy import ndimage
    H_ = out.shape[0]; sc = H_ / 1024.0
    m = vis & ((((h > 55) & (h < 115) & (s > 0.4)) | (s < 0.3)) & (v > 0.72))
    m = ndimage.binary_closing(m, iterations=max(1, int(round(2 * sc))))
    m = ndimage.binary_fill_holes(m)
    lab, n = ndimage.label(m)
    found = []
    for i, sl in enumerate(ndimage.find_objects(lab), 1):
        comp = lab[sl] == i
        area = int(comp.sum()); hh = sl[0].stop - sl[0].start; ww = sl[1].stop - sl[1].start
        fill = area / float(hh * ww); asp = ww / float(hh)
        cyc = (sl[0].start + sl[0].stop) / 2.0
        if 1500 * sc * sc < area < 16000 * sc * sc and fill > 0.66 and 0.5 < asp < 1.7 and cyc < 0.45 * H_:
            found.append((i, sl, area, fill, asp))
    print("eyes found:", [(f[2], round(f[3], 2), round(f[4], 2), (f[1][1].start, f[1][0].start)) for f in found])
    if len(found) != 2:
        print("WARNING: expected 2 eyes; eyes left as rendered")
        return out, None
    W_ = out.shape[1]
    yy, xx = np.mgrid[0:H_, 0:W_].astype(np.float32)
    wsum = np.zeros((H_, W_), np.float32)
    for i, sl, area, fill, asp in found:
        comp = (lab == i)
        ys, xs = np.nonzero(comp)
        cy, cx = ys.mean(), xs.mean()
        ay = (ys.max() - ys.min() + 1) / 2.0; ax = (xs.max() - xs.min() + 1) / 2.0
        # the hot core sits a little up and in (toward the light), like a painted eye's
        r = np.hypot((xx - (cx - 0.1 * ax)) / ax, (yy - (cy - 0.16 * ay)) / ay)
        r = np.clip(r / 1.12, 0, 1)
        col = np.zeros((H_, W_, 3), np.float32)
        for (a0, c0), (a1, c1) in zip(EYE_STOPS[:-1], EYE_STOPS[1:]):
            t = np.clip((r - a0) / (a1 - a0), 0, 1)[..., None]
            sel = ((r >= a0) & (r <= a1))[..., None]
            col = np.where(sel, np.array(c0, np.float32) * (1 - t) + np.array(c1, np.float32) * t, col)
        # the knight faces us: his left eye is the one on the screen's right
        right_i = max(found, key=lambda f_: f_[1][1].start + f_[1][1].stop)[0]
        if LEAF_EYE == "knight_left" and i == right_i:
            lm = leaf_mask(H_, W_, cx, cy, ay)
            col = col * (1 - (LEAF_ALPHA * lm)[..., None] * (1 - np.array(LEAF_TINT, np.float32)))
        # a glint on the eye's glossy front (on top of the leaf too), up and in like a painted highlight
        gr = np.hypot((xx - (cx - 0.36 * ax)) / (0.28 * ax), (yy - (cy - 0.42 * ay)) / (0.22 * ay))
        gl_ = np.clip((1.0 - gr) / 0.35, 0, 1) ** 0.8      # a crisp white dot, soft only at its edge
        col = col * (1 - gl_[..., None]) + np.array([0.99, 0.99, 0.96], np.float32) * gl_[..., None]
        w = np.clip(ndimage.gaussian_filter(comp.astype(np.float32), 0.9 * sc) * 1.15, 0, 1)
        out = out * (1 - w[..., None]) + col * w[..., None]
        wsum = np.maximum(wsum, w)
    return out, wsum


def grade(rgba):
    rgb = rgba[..., :3]
    al = rgba[..., 3]
    h, s, v = rgb2hsv(rgb)
    vis = al > 0.5
    gb = band(h, 70, 165, 12) * np.clip((s - 0.12) / 0.15, 0, 1)        # greens (not the near-greys)
    gd = band(h, 15, 60, 8) * np.clip((s - 0.2) / 0.15, 0, 1)           # golds / oranges / browns
    gd = gd * (1 - gb)
    # the browns (his face inside the visor, the gloves) share the gold's hue but are dark: they keep their own colour;
    # only the bright golds (trims, outlines, ring) take the badge's gold
    gd_bright = gd * np.clip((v - BROWN_MAX) / 0.15, 0, 1)
    # measured medians of this render's bands
    gsel = vis & (gb > 0.9); dsel = vis & (gd > 0.9) & (v > 0.35)
    gh = np.median(h[gsel]) if gsel.any() else GREEN_HUE
    dh = np.median(h[dsel]) if dsel.any() else GOLD_HUE
    gs_ = np.median(s[gsel]) if gsel.any() else GREEN_SAT
    ds_ = np.median(s[dsel]) if dsel.any() else GOLD_SAT
    dv_ = np.median(v[dsel]) if dsel.any() else GOLD_VAL
    # hue: shift each band by (target - its median), blended by band weight
    # target hue ramps with brightness, like the badge: emerald shadows -> lime highlights, orange shadows -> yellow gold
    t_green = GREEN_DARK_HUE + (GREEN_LIGHT_HUE - GREEN_DARK_HUE) * np.clip((v - 0.28) / 0.45, 0, 1)
    t_gold = GOLD_DARK_HUE + (GOLD_LIGHT_HUE - GOLD_DARK_HUE) * np.clip((v - 0.45) / 0.45, 0, 1) \
        + (GOLD_LIP_HUE - GOLD_LIGHT_HUE) * np.clip((v - 0.96) / 0.04, 0, 1) * np.clip((0.55 - s) / 0.2, 0, 1)
    # (the badge: orange shadows -> amber-gold body -> a pale-yellow lip only on the glints, which render desaturated)
    hm = _mask(HELMET_MASK, v.shape)
    t_green = t_green * (1 - hm) + HELMET_HUE * hm
    fm = _mask(FIELD_MASK, v.shape) * (1 - hm)
    t_field = FIELD_HUE + (FIELD_LIGHT_HUE - FIELD_HUE) * np.clip((v - 0.25) / 0.4, 0, 1)   # the fire/smoke: toxic lime (badge H87-93)
    t_green = t_green * (1 - fm) + t_field * fm
    rel_g = h - gh                                   # keep part of each pixel's own variation round the band's median
    rel_d = h - dh
    h2 = h + STRENGTH * (gb * (t_green + GREEN_REL * rel_g - h) + gd_bright * (t_gold + GOLD_REL * rel_d - h))
    # browns (gold hue but dark): chestnut red-brown
    brown = gd * (1 - np.clip((v - BROWN_MAX) / 0.15, 0, 1)) * np.clip((v - 0.1) / 0.06, 0, 1) * np.clip((s - 0.3) / 0.15, 0, 1)
    brown = brown * (1 - hm)                                   # (not the helmet's gold trims: in shadow they turned copper)
    h2 = h2 + STRENGTH * brown * (BROWN_HUE - h2)
    gsh = gd * np.clip((0.8 - v) / 0.3, 0, 1) * np.clip((s - 0.68) / 0.1, 0, 1) * (1 - brown) * (h > 33)
    h2 = h2 + gsh * (GOLD_SHADOW_HUE - h2)
    # saturation: a power curve that moves the band's median onto the target (keeps the spread)
    def sat_to(sv, med, tgt, w):
        if med <= 1e-3 or med >= 0.999:
            return sv
        k = np.log(max(min(tgt, 0.995), 1e-3)) / np.log(med)
        return sv ** (1 + (k - 1) * w * STRENGTH)
    s2 = sat_to(s, gs_, GREEN_SAT, gb)
    s2 = sat_to(s2, ds_, GOLD_SAT, gd_bright)
    s2 = s2 + brown * STRENGTH * np.clip(BROWN_SAT - s2, 0, 1)
    s2 = s2 + fm * gb * 0.85 * np.clip(0.96 - s2, 0, 1)        # the field stays a rich green (never khaki)
    # dark field pixels that aren't green at all (the gold ring's warm grey bounce on the disc edge) become field green too
    fd = fm * np.clip((0.5 - v) / 0.15, 0, 1) * (1 - gb)
    h2 = h2 + fd * (FIELD_HUE - h2)
    s2 = s2 + fd * np.clip(0.92 - s2, 0, 1)
    brown = brown * (1 - fd)
    # value: the gold band toward the badge's bright gold; a gentle overall lift
    gain = 1 + gd_bright * STRENGTH * (GOLD_VAL / max(dv_, 1e-3) - 1)
    v2 = v * gain * LIFT
    # golds: highlights roll off softly above the knee instead of clipping flat
    k_ = GOLD_KNEE
    v2 = np.where((gd > 0.3) & (v2 > k_), k_ + (1 - k_) * np.tanh((v2 - k_) / (1 - k_)), v2)
    v2 = np.clip(v2, 0, 1)
    v2 = v2 * (1 - gb) + (v2 ** GREEN_GAMMA) * gb
    v2 = v2 * (1 - hm * gb * (1 - HELMET_VAL) * np.clip((0.65 - v2) / 0.2, 0, 1))   # (not the bright clover)
    hg = hm * gd
    v2 = np.minimum(v2 * (1 + hg * (HELMET_GOLD - 1)), np.maximum(v2, 0.97))
    h2 = h2 + hg * 4.0
    v2 = v2 * (1 + fm * (FIELD_VAL - 1) * np.clip((0.55 - v2) / 0.2, 0, 1))
    v2 = np.clip((v2 - BLACK) / (1 - BLACK), 0, 1)         # deeper blacks (the render's were lifted)
    v2 = v2 * (TOE + (1 - TOE) * np.clip(v2 / 0.2, 0, 1))  # a toe: the shadows reach near-black, the mids stay
    # near-black pixels are green-black, like the badge's (no brown or grey in the shadows)
    nb = np.clip((0.14 - v2) / 0.1, 0, 1) * vis * (1 - np.clip((s - 0.0) * 0, 0, 1))
    shadow_ok = (gb < 0.5) & (brown < 0.2)
    h2 = np.where(shadow_ok, h2 + nb * (SHADOW_HUE * (1 - fm) + FIELD_HUE * fm - h2), h2)
    s2 = np.where(shadow_ok, s2 + nb * np.clip(0.85 - s2, 0, 1), s2)
    # near-white glow (fire behind the 9, eye cores) keeps a lime tint instead of clipping to white
    s2 = np.clip(s2, 0, 1)
    # the hottest glows (eye and flame cores) turn pale yellow-lime, like the badge's, instead of neon green / white
    pale = (v2 > 0.86) & (s2 < 0.32) & (h > 25) & (h < 70) & vis
    s2 = np.where(pale, np.maximum(s2, 0.42), s2)
    h2 = np.where(pale, 44.0, h2)
    hot = np.clip((v2 - 0.9) / 0.1, 0, 1) * (gb > 0.2)
    hot = hot * (1 - hm)                                        # (not the helmet: the clover clipped to a neon plateau)
    hf = hot * fm * np.clip((0.6 - s) / 0.25, 0, 1)            # only the field's truly white-hot cores go pale yellow
    hs = (hot - hf) * fm                                        # the field's saturated hot fire: lime-green, not chartreuse
    ho = hot - hf - hs
    s2 = s2 + ho * (CORE_SAT - s2) + hf * (0.5 - s2) + hs * (0.9 - s2)
    h2 = h2 + ho * (CORE_HUE - h2) + hf * (66.0 - h2) + hs * (90.0 - h2)
    # the gold ring darkens toward the bottom (light from above), amber-brown at the very bottom
    H_, W_ = al.shape
    ys_, xs_ = np.nonzero(vis)
    if len(ys_):
        low = vis[int(H_ * 0.58):, :]
        cols = np.nonzero(low.any(0))[0]
        R_ = (cols.max() - cols.min()) / 2.0
        cx_ = (cols.max() + cols.min()) / 2.0
        cy_ = ys_.max() - R_
        yy_, xx_ = np.mgrid[0:H_, 0:W_]
        rr_ = np.hypot(xx_ - cx_, yy_ - cy_)
        ring = vis & (rr_ > RING_FROM * R_) & (gd > 0.3)
        kk = np.clip((yy_ - cy_) / R_, 0, 1) ** 1.3 * ring
        v2 = v2 * (1 - RING_FALL * kk)
        h2 = h2 - RING_HUE_DROP * kk
        # a near-black gutter just inside the ring, like the badge's (the ring pops; no lime touching the gold)
        # (measured in the bottom sector only: the digits break past the ring at the sides)
        goldring = vis & (rr_ > 0.85 * R_) & (gd > 0.5) & (v > 0.2) & ((yy_ - cy_) > 0.8 * R_)
        if goldring.any():
            R_in = np.percentile(rr_[goldring], 3)
            gut = vis & (gd < 0.5) & (rr_ > R_in - GUTTER_PX * H_ / 1024.0) & (rr_ < R_in + 10 * H_ / 1024.0)   # hugs the gold
            gw = np.clip((rr_ - (R_in - GUTTER_PX * H_ / 1024.0)) / 2.0, 0, 1) * gut
            v2 = v2 * (1 - 0.85 * gw)
    out = hsv2rgb(h2, np.clip(s2, 0, 1), v2)
    out, eyes = paint_eyes(out, h, s, v, vis)
    if eyes is not None and HALO > 0:
        from scipy import ndimage
        sc = H_ / 1024.0
        halo = ndimage.gaussian_filter(eyes, 14 * sc)
        out = out + (np.clip(halo * HALO * 2.2, 0, 1) * (1 - eyes))[..., None] * np.array([0.30, 0.85, 0.05], np.float32)
    return np.dstack([out, al]), dict(green=(round(float(gh), 1), round(float(gs_), 2)),
                                       gold=(round(float(dh), 1), round(float(ds_), 2), round(float(dv_), 2)))


if __name__ == "__main__":
    im = np.asarray(Image.open(sys.argv[1]).convert("RGBA"), np.float32) / 255.0
    out, info = grade(im)
    Image.fromarray((np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA").save(sys.argv[2])
    print("graded", sys.argv[1].split("/")[-1], "->", sys.argv[2].split("/")[-1], "measured before:", info)
