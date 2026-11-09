"""New clovers for the knight's armour colour map (user 2026-11-09: "the clover is being stretched, improve it, make it
look better, like in the 3rd pic": a bold four-leaf clover, bright lime gradient, dark outline, veins, a highlight).

The old clovers were drawn flat in the texture, so the model's UVs stretched them. Each one is erased (the plate behind
it is filled in from around it) and a new clover is painted STRAIGHT ONTO THE SURFACE: every texel near the old
clover's 3D spot is placed on a flat decal facing out of the armour there (knight_surface_2048.npz, made by
knight_bake_surface.py), so the clover is round on the model whatever the UVs do.

    from paint_clovers import paint_clovers;  D2 = paint_clovers(D)        (D: float RGB 0..1, H x W x 3)
    python paint_clovers.py        -> writes clover_canvas.png (the design) and clovers_only.png (preview)
"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
SURFACE = os.path.join(HERE, "knight_surface_2048.npz")

# the old clovers: UV box (x0, y0, x1, y1 in the 2048 texture), size of the new one (x the old one's size)
CLOVERS = {          # only the two the user showed stretched (2026-11-09); the shield's is the source, the sword gems
    "helmet": ((1500, 545, 1670, 672), 0.735),         # are left as they are; user: "scale it down to 70%"
    "chest": ((872, 1852, 1068, 2048), 0.70),
}
# the clover design = the user's OWN shamrock, cut from the shield (not stretched there): same outline, folded leaves and
# colours as the rest of the knight (a brighter drawn clover was blown out to pale mint by the knight material's lime ramp)
SOURCE_BOX = (500, 1072, 782, 1368)
MATCH_TONE = True                    # each new clover takes the colour of the old one at that spot
LEAVES = 3                           # the user's reference (2026-11-09): a three-leaf shamrock
# the user's reference: bright saturated lime, each heart leaf folded along its middle (one half lighter), crisp dark
# outline, a soft shadow, a short thick stem. (The Steam art's lime-glow is masked off the clovers: clover_mask.png.)
C_HI = (178, 238, 40)                # lit half / highlight
C_MID = (98, 200, 12)               # base lime
C_LO = (40, 132, 8)                 # shaded half / deep
C_OUTLINE = (6, 34, 8)
C_STEM = (70, 160, 12)
C_SHINE = (240, 255, 200)
OUTLINE = 0.055                      # outline thickness (share of the clover's half size)
LEAF_ANGLES = (90, 196, 344)         # top, left, right (degrees; 0 = right), like the reference
LEAF_LEN, LEAF_WIDE = 0.60, 1.28     # leaf length (from the centre) and width factor: plump


def _heart_poly(n=120):
    t = np.linspace(0, 2 * np.pi, n, endpoint=False)
    x = 16 * np.sin(t) ** 3
    y = 13 * np.cos(t) - 5 * np.cos(2 * t) - 2 * np.cos(3 * t) - np.cos(4 * t)
    return x, y - y.min()            # the heart's point at y = 0, lobes up (+y), height ~29


def clover_canvas(size=1024, ss=2):
    """The clover design on a transparent square, decal coordinates u, v in [-1, 1] (v up)."""
    S = size * ss
    def P(u, v):
        return ((u + 1) / 2 * S, (1 - (v + 1) / 2) * S)
    yy, xx = np.mgrid[0:S, 0:S].astype(np.float32)
    U = xx / S * 2 - 1; V = 1 - yy / S * 2
    hx, hy = _heart_poly()
    k = LEAF_LEN / hy.max()
    leaves = []
    for a in LEAF_ANGLES:
        r = np.radians(a)
        d = np.array([np.cos(r), np.sin(r)]); q = np.array([-d[1], d[0]])
        pts = [P(*(d * (0.02 + y * k) + q * (x * k * LEAF_WIDE))) for x, y in zip(hx, hy)]
        leaves.append((pts, d, q))
    ts = np.linspace(0, 1, 40)
    stem = [(0.012 * t + 0.05 * t ** 2, -0.02 - 0.68 * t) for t in ts]
    stem_w = 0.155
    # masks
    fill = Image.new("L", (S, S), 0); df = ImageDraw.Draw(fill)
    for pts, d, q in leaves:
        df.polygon(pts, fill=255)
    stem_m = Image.new("L", (S, S), 0); dsm = ImageDraw.Draw(stem_m)
    # a smooth tapering band (one polygon: no seams), round end
    sp = np.array(stem)
    tg = np.gradient(sp, axis=0); tg /= np.linalg.norm(tg, axis=1, keepdims=True)
    nm = np.stack([-tg[:, 1], tg[:, 0]], 1)
    hw = (stem_w / 2) * (1.0 - 0.25 * ts)[:, None]
    left = sp + nm * hw; right = sp - nm * hw
    dsm.polygon([P(*p) for p in np.concatenate([left, right[::-1]])], fill=255)
    e, er = sp[-1], hw[-1, 0]
    dsm.ellipse([P(e[0] - er, e[1] + er), P(e[0] + er, e[1] - er)], fill=255)
    allm = Image.fromarray(np.maximum(np.asarray(fill), np.asarray(stem_m)))
    o = OUTLINE * S / 2
    base = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    # soft shadow, down-right
    sh = allm.filter(ImageFilter.GaussianBlur(o * 1.4))
    sh = sh.transform(sh.size, Image.AFFINE, (1, 0, -0.035 * S / 2, 0, 1, 0.045 * S / 2))
    sh_img = Image.new("RGBA", (S, S), (0, 12, 0, 0)); sh_img.putalpha(sh.point(lambda v: int(v * 0.7)))
    base.alpha_composite(sh_img)
    # each shape gets its OWN crisp dark outline (like the reference: the leaves are separated by dark lines);
    # drawn bottom-up: stem, then the side leaves, then the top leaf
    def outline_of(mask_img):
        g = np.asarray(mask_img.filter(ImageFilter.GaussianBlur(o * 0.75)), np.float32)
        a = Image.new("RGBA", (S, S), C_OUTLINE + (0,))
        a.putalpha(Image.fromarray((np.clip((g - 40.0) / 12.0, 0, 1) * 255).astype(np.uint8)))
        return a
    # stem: darker lime, lighter on its left edge
    base.alpha_composite(outline_of(stem_m))
    sc = np.zeros((S, S, 3), np.float32) + np.array(C_STEM, np.float32)
    sc *= (0.86 + 0.26 * np.clip(0.5 - (U - 0.03) * 8.0, 0, 1))[..., None]
    st = Image.fromarray(np.clip(sc, 0, 255).astype(np.uint8)).convert("RGBA"); st.putalpha(stem_m)
    base.alpha_composite(st)
    # leaves: each folded along its middle (the half towards the top-left light lighter), lighter at the lobes
    light = np.array([-0.6, 0.8]); light /= np.linalg.norm(light)
    order = sorted(range(len(leaves)), key=lambda i: leaves[i][1][1])      # lowest first, the top leaf last
    for i in order:
        pts, d, q = leaves[i]
        lm = Image.new("L", (S, S), 0); ImageDraw.Draw(lm).polygon(pts, fill=255)
        base.alpha_composite(outline_of(lm))
        m = np.asarray(lm, np.float32) / 255.0
        side = U * q[0] + V * q[1]                       # + on one half of the leaf, - on the other
        along = np.clip((U * d[0] + V * d[1]) / LEAF_LEN, 0, 1)
        lit_half = 1.0 if q @ light > 0 else -1.0
        half = np.clip(0.5 + 6.0 * side * lit_half, 0, 1)   # 1 on the lit half, 0 on the shaded half (soft crease)
        c = (np.array(C_LO, np.float32) * (1 - half[..., None]) + np.array(C_MID, np.float32) * half[..., None])
        hl = np.clip((along - 0.35) / 0.65, 0, 1)[..., None] * half[..., None] * 0.55
        c = c * (1 - hl) + np.array(C_HI, np.float32) * hl
        c *= (0.84 + 0.16 * along[..., None] ** 0.5)       # deeper where the leaves meet
        inner = np.asarray(lm.filter(ImageFilter.GaussianBlur(0.03 * S / 2)), np.float32)[..., None] / 255.0
        c *= 0.8 + 0.2 * np.clip(inner * 1.6 - 0.3, 0, 1)  # a touch darker at its rim: puffy
        lv = Image.fromarray(np.clip(c, 0, 255).astype(np.uint8)).convert("RGBA"); lv.putalpha(lm)
        base.alpha_composite(lv)
    # a thin dark line where the leaves meet the centre
    dc = ImageDraw.Draw(base)
    dc.ellipse([P(-0.05, 0.05), P(0.05, -0.05)], fill=C_LO + (255,))
    # glossy highlights near the tips of the lit halves
    shine = Image.new("L", (S, S), 0); dsh = ImageDraw.Draw(shine)
    for pts, d, q in leaves:
        lit = q if q @ light > 0 else -q
        cpt = d * LEAF_LEN * 0.66 + lit * 0.12
        dsh.ellipse([P(cpt[0] - 0.07, cpt[1] + 0.045), P(cpt[0] + 0.07, cpt[1] - 0.045)], fill=255)
    shine = shine.filter(ImageFilter.GaussianBlur(0.025 * S / 2))
    sa = (np.asarray(shine, np.float32) * (np.asarray(fill, np.float32) / 255.0) * 0.4).astype(np.uint8)
    shi = Image.new("RGBA", (S, S), C_SHINE + (0,)); shi.putalpha(Image.fromarray(sa))
    base.alpha_composite(shi)
    return base.resize((size, size), Image.LANCZOS)


def canvas_from_texture(D, size=1024):
    """The user's shamrock on the shield, cut out with its dark outline, on a transparent square (decal coordinates)."""
    x0, y0, x1, y1 = SOURCE_BOX
    c = D[y0:y1, x0:x1]
    h, s_, v = _hsv(c)
    gold = (h > 25) & (h < 65) & (s_ > 0.35) & (v > 0.18)
    green = (h > 70) & (h < 170) & ~gold
    border = np.zeros(green.shape, bool); border[:3] = border[-3:] = True; border[:, :3] = border[:, -3:] = True
    pv = np.median(v[border & green])
    leaf = green & (v > pv * 1.22)
    # keep only the big blob (the shamrock), then its dark outline round it
    ys, xs = np.nonzero(leaf)
    cy, cx = np.median(ys), np.median(xs)
    keep = np.zeros_like(leaf)
    from collections import deque
    seed = (int(ys[np.argmin((ys - cy) ** 2 + (xs - cx) ** 2)]), int(xs[np.argmin((ys - cy) ** 2 + (xs - cx) ** 2)]))
    q = deque([seed]); keep[seed] = True
    while q:
        y, x = q.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            yy, xx = y + dy, x + dx
            if 0 <= yy < leaf.shape[0] and 0 <= xx < leaf.shape[1] and leaf[yy, xx] and not keep[yy, xx]:
                keep[yy, xx] = True; q.append((yy, xx))
    shape = _dilate(keep, 2)
    outline = _dilate(keep, 7) & green & (v < pv * 1.05)        # the dark rim the user painted round it
    alpha = (shape | outline).astype(np.float32)
    al = Image.fromarray((alpha * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.0))
    ys, xs = np.nonzero(shape | outline)
    by0, by1, bx0, bx1 = ys.min(), ys.max(), xs.min(), xs.max()
    cyc, cxc = (by0 + by1) / 2, (bx0 + bx1) / 2
    half = 0.5 * max(by1 - by0, bx1 - bx0) * 1.06
    rgba = np.dstack([c * 255, np.asarray(al, np.float32)]).astype(np.uint8)
    img = Image.fromarray(rgba, "RGBA")
    box = (int(cxc - half), int(cyc - half), int(cxc + half), int(cyc + half))
    sq = Image.new("RGBA", (box[2] - box[0], box[3] - box[1]), (0, 0, 0, 0))
    sq.paste(img, (-box[0], -box[1]))
    return sq.resize((size, size), Image.LANCZOS)


def _dilate(m, r):
    out = m.copy()
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            if dx * dx + dy * dy <= r * r:
                out |= np.roll(np.roll(m, dy, 0), dx, 1)
    return out


def _hsv(c):
    mx = c.max(-1); mn = c.min(-1); d = mx - mn
    r, g, b = c[..., 0], c[..., 1], c[..., 2]
    h = np.zeros_like(mx); m = d > 1e-6
    rm = m & (mx == r); gm = m & (mx == g) & ~rm; bm = m & ~rm & ~gm
    h[rm] = ((g - b)[rm] / d[rm]) % 6; h[gm] = (b - r)[gm] / d[gm] + 2; h[bm] = (r - g)[bm] / d[bm] + 4
    return h * 60, np.where(mx > 1e-6, d / np.maximum(mx, 1e-6), 0), mx


def _erase(D, box):
    """Old clover out: its bright leaves (+ their dark outline) are refilled from the plate around them."""
    x0, y0, x1, y1 = box
    c = D[y0:y1, x0:x1].copy()
    h, s, v = _hsv(c)
    gold = (h > 25) & (h < 65) & (s > 0.35) & (v > 0.18)
    green = (h > 70) & (h < 170) & ~gold
    # the plate's brightness: median of the green border pixels
    border = np.zeros(green.shape, bool); border[:3] = border[-3:] = True; border[:, :3] = border[:, -3:] = True
    pv = np.median(v[border & green]) if (border & green).any() else np.median(v[green])
    leaf = green & (v > pv * 1.22)
    near = _dilate(leaf, 16)
    dark = green & (v < pv * 0.86) & near                 # the old clover's dark outline / drop shadow
    hole = _dilate(leaf | dark, 5) & green
    known = green & ~hole
    est = c.copy()
    est[hole] = np.median(c[known], 0) if known.any() else c[hole].mean(0)
    w = (green).astype(np.float32)
    for _ in range(500):            # fill the hole smoothly from the plate around it (gold does not bleed in)
        acc = np.zeros_like(est); ws = np.zeros(w.shape, np.float32)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ww = np.roll(np.roll(w, dy, 0), dx, 1)
            acc += np.roll(np.roll(est, dy, 0), dx, 1) * ww[..., None]
            ws += ww
        new = acc / np.maximum(ws, 1e-6)[..., None]
        est[hole] = new[hole]
    D = D.copy()
    D[y0:y1, x0:x1] = est
    full = np.zeros(D.shape[:2], bool); full[y0:y1, x0:x1] = leaf
    return D, full


def paint_clovers(D, canvas=None, report=print, with_mask=False):
    S = np.load(SURFACE)
    pos, nrm, obj = S["pos"], S["nrm"], S["obj"]
    H, W = D.shape[:2]
    if pos.shape[:2] != (H, W):
        raise SystemExit("surface bake is %s, texture %s: re-bake with size=%d" % (pos.shape[:2], (H, W), W))
    valid = pos[..., 0] > -999
    canvas = canvas or canvas_from_texture(D)
    hh, ss_, vv = _hsv(D)
    gold_all = (hh > 25) & (hh < 65) & (ss_ > 0.35) & (vv > 0.18)
    out = D.copy()
    cmask = np.zeros((H, W), np.float32)
    yy, xx = np.mgrid[0:H, 0:W]
    for name, (box, scale) in CLOVERS.items():
        before = out
        out, leaf = _erase(out, box)
        sel = leaf & valid
        if sel.sum() < 30:
            report("clover %s: nothing found, left as it was" % name)
            out = before
            continue
        oid = np.bincount(np.clip(obj[sel], 0, None)).argmax()
        P = pos[sel].astype(np.float64)
        n = nrm[sel].mean(0); n /= np.linalg.norm(n)
        # the texture's "up" and "right" at the clover, in 3D (least squares over the old clover's texels)
        A = np.stack([xx[sel] - xx[sel].mean(), yy[sel] - yy[sel].mean(), np.ones(sel.sum())], 1)
        J = np.linalg.lstsq(A, P, rcond=None)[0]
        up = -J[1]; up -= n * up.dot(n); up /= np.linalg.norm(up)
        right = np.cross(up, n)                                    # as seen from outside: never mirrored
        c0 = P.mean(0)
        s_ = (P - c0) @ right; t_ = (P - c0) @ up
        c = c0 + right * (s_.max() + s_.min()) / 2 + up * (t_.max() + t_.min()) / 2
        r = 0.5 * np.sqrt((s_.max() - s_.min()) * (t_.max() - t_.min())) * 1.08 * scale
        # every texel of that part, near that spot and facing the same way, gets the decal
        near = valid & (obj == oid) & ((nrm.reshape(-1, 3) @ n).reshape(H, W) > 0.25)
        Q = pos - c
        su = (Q @ right) / r; tv = (Q @ up) / r; depth = np.abs(Q @ n) / r
        dec = near & (np.abs(su) <= 1.0) & (np.abs(tv) <= 1.0) & (depth < 0.6)
        if not dec.any():
            report("clover %s: no surface for the decal, left as it was" % name)
            out = before
            continue
        # sample the design at a resolution close to the texel density (no aliasing)
        dtex = max(16, int(np.sqrt(dec.sum()) * 1.6))
        cv = np.asarray(canvas.resize((dtex, dtex), Image.LANCZOS), np.float32) / 255.0
        fx = (su[dec] + 1) / 2 * (dtex - 1); fy = (1 - (tv[dec] + 1) / 2) * (dtex - 1)
        x0i = np.clip(np.floor(fx).astype(int), 0, dtex - 2); y0i = np.clip(np.floor(fy).astype(int), 0, dtex - 2)
        ax = (fx - x0i)[:, None]; ay = (fy - y0i)[:, None]
        smp = (cv[y0i, x0i] * (1 - ax) * (1 - ay) + cv[y0i, x0i + 1] * ax * (1 - ay) +
               cv[y0i + 1, x0i] * (1 - ax) * ay + cv[y0i + 1, x0i + 1] * ax * ay)
        # same tone as the clover that was painted HERE (the helmet's is darker than the shield's): per-channel gain
        cvl = np.asarray(canvas, np.float32) / 255.0
        cm_ = cvl[..., 3] > 0.9
        lum = cvl[..., :3] @ np.array([0.3, 0.59, 0.11], np.float32)
        bright = cm_ & (lum > np.median(lum[cm_]))
        old_mean = D[sel].mean(0)
        new_mean = cvl[..., :3][bright].mean(0)
        gain = np.clip(old_mean / np.maximum(new_mean, 1e-3), 0.5, 1.3) if MATCH_TONE else np.ones(3)
        smp = np.concatenate([np.clip(smp[:, :3] * gain, 0, 1), smp[:, 3:4]], 1)
        a = smp[:, 3:4] * (~gold_all[dec])[:, None]               # never over the gold trims
        base = out[dec]
        out[dec] = base * (1 - a) + smp[:, :3] * a
        cmask[dec] = np.maximum(cmask[dec], a[:, 0])
        report("clover %-14s on %s: %d texels, size %.3f m, tone gain %s" % (name, S["names"][oid - 1], int(dec.sum()), 2 * r, np.round(gain, 2)))
    return (out, cmask) if with_mask else out


if __name__ == "__main__":
    TEX = os.path.join(HERE, "..", "..", "..", "..", "Assets", "M_Textures", "Knight", "Knight_Sword_Shield_Armor",
                       "Knight_Armour_Set_Diffuse.png")
    D = np.asarray(Image.open(TEX).convert("RGB"), np.float32) / 255.0
    cnv = canvas_from_texture(D)
    cnv.save(os.path.join(HERE, "clover_canvas.png"))
    D2, M = paint_clovers(D, cnv, with_mask=True)
    Image.fromarray((np.clip(D2, 0, 1) * 255 + 0.5).astype(np.uint8)).save(os.path.join(HERE, "clovers_only.png"))
    Image.fromarray((np.clip(M, 0, 1) * 255 + 0.5).astype(np.uint8)).save(os.path.join(HERE, "clover_mask.png"))
    print("wrote clover_canvas.png, clovers_only.png")
