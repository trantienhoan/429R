# Models the 15 bedroom furniture pieces from the user's Gemini concept (Exports/Remodel/Furniture_Fix/concept_v1.png,
# 2026-11-20: "help me model these") into Bedroom_Furniture_Fix.blend, in the user's clean style (closed boxes, all
# quads except the clock's round caps, no bevels; separate parts may touch or sink into each other, nothing doubled).
# Each piece is built in a canonical frame (front -Y, width X, depth Y, floor z 0), turned to the old piece's facing
# and fitted to its footprint and height, then written INTO the old object (same name, same object transform, so the
# FBX export keeps every prefab link). The old meshes are kept as copies in the hidden collection "Old_Reference".
# Colours: one palette texture (Furniture_Palette.png, swatch columns dark->light) UV'd by script from the face's part
# colour and its height. Broken pieces are NOT remade here (next step).
#   blender -b Bedroom_Furniture_Fix.blend --python model_furniture.py -- <repo root> [--save]
import bpy, bmesh, sys, os, math
from mathutils import Vector, Matrix

a = sys.argv[sys.argv.index("--") + 1:]
ROOT = a[0].rstrip("/\\") + "/"
SAVE = "--save" in a
exec(open(ROOT + "Exports/Remodel/tools/palette_kit.py", encoding="utf-8").read())

# The furniture palette (overrides the rooftop one in palette_kit): name, dark hex, light hex, glows.
SWATCHES = [
    ("wood",          "2a170b", "6e3f20", False),
    ("wood_dark",     "190d06", "45260f", False),
    ("wood_top",      "3c2210", "8a5630", False),
    ("iron",          "121012", "3a3638", False),
    ("quilt_purple",  "3a1660", "7e3fc0", False),
    ("quilt_green",   "1e4a1a", "4e9a34", False),
    ("quilt_orange",  "7a2c06", "e8761e", False),
    ("quilt_blue",    "182e5e", "3e6cc0", False),
    ("quilt_pink",    "5a1636", "c04a84", False),
    ("cream",         "9a8a74", "f2e8d4", False),
    ("copper",        "5e3014", "c87e3a", False),
    ("clock_face",    "b0a080", "f4ead0", False),
    ("black",         "121012", "2c2628", False),
    ("pillow",        "a08c9a", "f0e2ea", False),
]
COLS = len(SWATCHES)
W = 32 * COLS
INDEX = {s[0]: i for i, s in enumerate(SWATCHES)}

# ---------------------------------------------------------------------------------------------------------------
# Shapes. Each returns (verts, faces) in world-ish canonical coordinates; faces carry a swatch name.

class Builder:
    def __init__(self):
        self.verts, self.faces, self.sw = [], [], []

    def _add(self, vs, fs, sws):
        o = len(self.verts)
        self.verts += vs
        self.faces += [[i + o for i in f] for f in fs]
        self.sw += sws

    def box(self, c, s, sw, rot=None, top=None):
        """Closed box, centre c, size s; rot = Matrix (3x3) about its centre; top = swatch of its top face."""
        cx, cy, cz = c; sx, sy, sz = s[0] / 2, s[1] / 2, s[2] / 2
        p = [(-sx, -sy, -sz), (sx, -sy, -sz), (sx, sy, -sz), (-sx, sy, -sz),
             (-sx, -sy, sz), (sx, -sy, sz), (sx, sy, sz), (-sx, sy, sz)]
        R = rot if rot is not None else Matrix.Identity(3)
        vs = [tuple(Vector((cx, cy, cz)) + R @ Vector(q)) for q in p]
        fs = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
        sws = [sw, top or sw, sw, sw, sw, sw]
        self._add(vs, fs, sws)

    def span(self, x0, x1, y0, y1, z0, z1, sw, top=None):
        self.box(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), (x1 - x0, y1 - y0, z1 - z0), sw, top=top)

    def planks(self, x0, x1, y0, y1, z0, z1, n, axis, sw, top=None, gap=0.006):
        """A slab split into n planks along axis ('x', 'y' or 'z') with thin gaps: each plank a closed box."""
        lo = {"x": x0, "y": y0, "z": z0}[axis]; hi = {"x": x1, "y": y1, "z": z1}[axis]
        step = (hi - lo + gap) / n
        for k in range(n):
            a = lo + k * step; b = a + step - gap
            if axis == "x": self.span(a, b, y0, y1, z0, z1, sw, top=top)
            elif axis == "y": self.span(x0, x1, a, b, z0, z1, sw, top=top)
            else: self.span(x0, x1, y0, y1, a, b, sw, top=top)

    def beam(self, p0, p1, w, d, sw):
        """A box from point p0 to p1 (centre line), w across (along the side axis), d thick."""
        p0, p1 = Vector(p0), Vector(p1)
        ax = (p1 - p0)
        L = ax.length
        z = ax.normalized()
        side = Vector((1, 0, 0)) if abs(z.x) < 0.9 else Vector((0, 1, 0))
        x = side - z * side.dot(z); x.normalize()
        y = z.cross(x)
        R = Matrix((x, y, z)).transposed()
        self.box(tuple((p0 + p1) / 2), (w, d, L), sw, rot=R)

    def grid_slab(self, x0, x1, y0, y1, z0, z1, nx, ny, top_sw, side_sw):
        """Closed slab whose top is an nx x ny grid of quads (top_sw(i, j) gives each cell's swatch); sides split to
        match; bottom one grid too. All quads."""
        xs = [x0 + (x1 - x0) * i / nx for i in range(nx + 1)]
        ys = [y0 + (y1 - y0) * j / ny for j in range(ny + 1)]
        vs, fs, sws = [], [], []
        idx = {}
        def v(i, j, k):
            key = (i, j, k)
            if key not in idx:
                idx[key] = len(vs); vs.append((xs[i], ys[j], (z0, z1)[k]))
            return idx[key]
        for i in range(nx):
            for j in range(ny):
                fs.append((v(i, j, 1), v(i + 1, j, 1), v(i + 1, j + 1, 1), v(i, j + 1, 1))); sws.append(top_sw(i, j))
                fs.append((v(i, j, 0), v(i, j + 1, 0), v(i + 1, j + 1, 0), v(i + 1, j, 0))); sws.append(side_sw)
        for i in range(nx):
            fs.append((v(i, 0, 0), v(i + 1, 0, 0), v(i + 1, 0, 1), v(i, 0, 1))); sws.append(side_sw)
            fs.append((v(i + 1, ny, 0), v(i, ny, 0), v(i, ny, 1), v(i + 1, ny, 1))); sws.append(side_sw)
        for j in range(ny):
            fs.append((v(nx, j, 0), v(nx, j + 1, 0), v(nx, j + 1, 1), v(nx, j, 1))); sws.append(side_sw)
            fs.append((v(0, j + 1, 0), v(0, j, 0), v(0, j, 1), v(0, j + 1, 1))); sws.append(side_sw)
        self._add(vs, fs, sws)

    def disc(self, c, r, y0, y1, n, sw_cap, sw_side):
        """A round plate facing -Y (front cap at y0), n sides; caps are n-gons."""
        vs = []
        for k in range(n):
            t = 2 * math.pi * k / n
            x, z = c[0] + r * math.cos(t), c[2] + r * math.sin(t)
            vs += [(x, y0, z), (x, y1, z)]
        fs, sws = [], []
        for k in range(n):
            a, b = 2 * k, 2 * ((k + 1) % n)
            fs.append((a, b, b + 1, a + 1)); sws.append(sw_side)
        fs.append(tuple(2 * k for k in range(n))); sws.append(sw_cap)
        fs.append(tuple(2 * k + 1 for k in reversed(range(n)))); sws.append(sw_side)
        self._add(vs, fs, sws)

# ---------------------------------------------------------------------------------------------------------------
# The pieces, canonical: front -Y, x across, y depth, z up, footprint centred on 0, floor at z 0.

def bed(W, D, H):
    frame, sheet, pillow = Builder(), Builder(), Builder()
    p = 0.09
    hx = W / 2 - p / 2
    for sx in (-1, 1):
        frame.span(sx * hx - p / 2, sx * hx + p / 2, D / 2 - p, D / 2, 0, H, "wood_dark", top="wood_top")          # head posts
        frame.span(sx * hx - p / 2, sx * hx + p / 2, -D / 2, -D / 2 + p, 0, 0.78, "wood_dark", top="wood_top")    # foot posts
        frame.span(sx * hx - 0.03, sx * hx + 0.03, -D / 2 + p, D / 2 - p, 0.24, 0.42, "wood")                     # side rails
    frame.planks(-W / 2 + p, W / 2 - p, D / 2 - p / 2 - 0.03, D / 2 - p / 2 + 0.03, 0.32, H - 0.16, 5, "x", "wood")         # headboard
    frame.span(-W / 2 + p - 0.01, W / 2 - p + 0.01, D / 2 - p / 2 - 0.045, D / 2 - p / 2 + 0.045, H - 0.18, H - 0.1, "wood_dark", top="wood_top")  # its top rail
    frame.planks(-W / 2 + p, W / 2 - p, -D / 2 + p / 2 - 0.03, -D / 2 + p / 2 + 0.03, 0.32, 0.64, 5, "x", "wood")           # footboard
    frame.span(-W / 2 + p, W / 2 - p, -D / 2 + p, D / 2 - p, 0.28, 0.32, "wood_dark")                           # slats board
    # mattress + patchwork quilt (the "sheet" model)
    sheet.span(-W / 2 + p - 0.005, W / 2 - p + 0.005, -D / 2 + p, D / 2 - p, 0.32, 0.48, "cream")
    colours = ["quilt_purple", "quilt_green", "quilt_orange", "quilt_blue", "quilt_pink"]
    sheet.grid_slab(-W / 2 + p - 0.03, W / 2 - p + 0.03, -D / 2 + p - 0.02, D / 2 - p - 0.42, 0.47, 0.55, 4, 5,
                    lambda i, j: colours[(i * 2 + j * 3) % len(colours)], "quilt_purple")
    sheet.span(-W / 2 + p - 0.03, W / 2 - p + 0.03, D / 2 - p - 0.44, D / 2 - p - 0.36, 0.48, 0.57, "cream")     # folded band
    rot = Matrix.Rotation(math.radians(-14), 3, "X")
    pillow.box((0, D / 2 - p - 0.19, 0.6), (0.62, 0.3, 0.13), "pillow", rot=rot)
    return {"Bedroom_Bed": frame, "Bedroom_Bed_Sheet": sheet, "Bedroom_Bed_Pillow": pillow}

def stool(W, D, H):
    b = Builder(); t = 0.07; l = 0.075; i = 0.025
    b.planks(-W / 2, W / 2, -D / 2, D / 2, H - t, H, 3, "x", "wood", top="wood_top")
    for sx in (-1, 1):
        for sy in (-1, 1):
            cx, cy = sx * (W / 2 - i - l / 2), sy * (D / 2 - i - l / 2)
            b.span(cx - l / 2, cx + l / 2, cy - l / 2, cy + l / 2, 0, H - t, "wood_dark")
    ax, ay = W / 2 - i - l, D / 2 - i - l
    for sy in (-1, 1):
        b.span(-ax, ax, sy * (D / 2 - i - l / 2) - 0.015, sy * (D / 2 - i - l / 2) + 0.015, H - t - 0.08, H - t, "wood")
        b.span(-ax, ax, sy * (D / 2 - i - l / 2) - 0.012, sy * (D / 2 - i - l / 2) + 0.012, 0.08, 0.12, "wood_dark")
    for sx in (-1, 1):
        b.span(sx * (W / 2 - i - l / 2) - 0.015, sx * (W / 2 - i - l / 2) + 0.015, -ay, ay, H - t - 0.08, H - t, "wood")
    return b

def coffer(W, D, H):
    b = Builder(); f = 0.05; lid = 0.14
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.span(sx * (W / 2 - 0.06) - 0.045, sx * (W / 2 - 0.06) + 0.045, sy * (D / 2 - 0.06) - 0.045, sy * (D / 2 - 0.06) + 0.045, 0, f + 0.01, "iron")
    b.span(-W / 2 + 0.02, W / 2 - 0.02, -D / 2 + 0.02, D / 2 - 0.02, f, H - lid, "wood_dark")
    # front planks, a little proud, with gaps
    n = 3; z0, z1 = f + 0.01, H - lid - 0.01
    hgt = (z1 - z0 - (n - 1) * 0.012) / n
    for k in range(n):
        za = z0 + k * (hgt + 0.012)
        b.span(-W / 2 + 0.03, W / 2 - 0.03, -D / 2 + 0.008, -D / 2 + 0.024, za, za + hgt, "wood")
    b.planks(-W / 2, W / 2, -D / 2, D / 2, H - lid, H, 3, "y", "wood", top="wood_top")                                   # lid
    for sx in (-1, 1):                                                                                         # straps
        x = sx * (W / 2 - 0.26)
        b.span(x - 0.035, x + 0.035, -D / 2 - 0.008, -D / 2 + 0.004, f, H + 0.004, "iron")
        b.span(x - 0.035, x + 0.035, D / 2 - 0.004, D / 2 + 0.008, f, H + 0.004, "iron")
        b.span(x - 0.035, x + 0.035, -D / 2 - 0.008, D / 2 + 0.008, H - 0.004, H + 0.008, "iron")
    for sx in (-1, 1):                                                                                         # corners
        for sy in (-1, 1):
            cx, cy = sx * (W / 2 - 0.02), sy * (D / 2 - 0.02)
            b.span(cx - 0.032, cx + 0.032, cy - 0.032, cy + 0.032, f, H - lid + 0.03, "iron")
    b.span(-0.05, 0.05, -D / 2 - 0.022, -D / 2 + 0.004, H - lid - 0.07, H - lid + 0.06, "iron")                 # lock
    return b

def wardrobe(W, D, H):
    b = Builder(); plinth = 0.1; cap = 0.1; sd = 0.05
    b.span(-W / 2 + 0.04, W / 2 - 0.04, -D / 2 + 0.04, D / 2 - 0.04, 0, plinth, "wood_dark")
    for sx in (-1, 1):
        b.span(sx * (W / 2 - 0.03) - sd / 2, sx * (W / 2 - 0.03) + sd / 2, -D / 2 + 0.02, D / 2 - 0.02, plinth, H - cap, "wood")
    b.span(-W / 2 + 0.055, W / 2 - 0.055, D / 2 - 0.05, D / 2 - 0.02, plinth, H - cap, "wood_dark")           # back
    b.span(-W / 2, W / 2, -D / 2, D / 2, H - cap, H, "wood", top="wood_top")                                 # top
    inner = W / 2 - 0.055
    yd0, yd1 = -D / 2 + 0.02, -D / 2 + 0.055
    for sx in (-1, 1):                                                                                     # doors
        xa, xb = (0.004, inner) if sx > 0 else (-inner, -0.004)
        b.planks(xa, xb, yd0, yd1, plinth + 0.03, H - cap - 0.03, 3, "x", "wood")
        for z in (plinth + 0.3, H - cap - 0.35):                                                          # hinges
            xh = sx * (inner - 0.03)
            b.span(xh - 0.05, xh + 0.05, yd0 - 0.012, yd0 + 0.004, z, z + 0.07, "iron")
        xk = sx * 0.07                                                                                    # knobs
        b.span(xk - 0.022, xk + 0.022, yd0 - 0.04, yd0 + 0.004, H * 0.5 - 0.022, H * 0.5 + 0.022, "iron")
        for zz in (H * 0.32, H * 0.68):                                                                    # door planks
            pass
    return b

def dresser(W, D, H):
    b = Builder(); low = 0.84; ctop = 0.06; du = 0.34; cap = 0.08
    b.span(-W / 2 + 0.03, W / 2 - 0.03, -D / 2 + 0.03, D / 2 - 0.03, 0, 0.08, "wood_dark")
    b.span(-W / 2 + 0.02, W / 2 - 0.02, -D / 2 + 0.04, D / 2 - 0.02, 0.08, low, "wood")
    for sx in (-1, 1):                                                                                     # doors + knobs
        xa, xb = (0.006, W / 2 - 0.05) if sx > 0 else (-W / 2 + 0.05, -0.006)
        b.planks(xa, xb, -D / 2 + 0.012, -D / 2 + 0.042, 0.12, low - 0.04, 2, "x", "wood")
        b.span(sx * 0.06 - 0.02, sx * 0.06 + 0.02, -D / 2 - 0.02, -D / 2 + 0.014, low * 0.55 - 0.02, low * 0.55 + 0.02, "iron")
    b.span(-W / 2, W / 2, -D / 2, D / 2, low, low + ctop, "wood", top="wood_top")                           # counter top
    y0 = D / 2 - du
    for sx in (-1, 1):
        b.span(sx * (W / 2 - 0.04) - 0.025, sx * (W / 2 - 0.04) + 0.025, y0, D / 2 - 0.01, low + ctop, H - cap, "wood")
    b.span(-W / 2 + 0.065, W / 2 - 0.065, D / 2 - 0.04, D / 2 - 0.01, low + ctop, H - cap, "wood_dark")      # back
    for z in (low + ctop + 0.36, low + ctop + 0.72):                                                       # shelves
        b.span(-W / 2 + 0.065, W / 2 - 0.065, y0 + 0.01, D / 2 - 0.04, z, z + 0.035, "wood", top="wood_top")
    b.span(-W / 2 + 0.01, W / 2 - 0.01, y0 - 0.03, D / 2, H - cap, H, "wood", top="wood_top")
    return b

def rack(W, D, H):
    b = Builder(); p = 0.055
    for sx in (-1, 1):
        for sy in (-1, 1):
            cx, cy = sx * (W / 2 - p / 2), sy * (D / 2 - p / 2)
            b.span(cx - p / 2, cx + p / 2, cy - p / 2, cy + p / 2, 0, H, "wood_dark", top="wood_top")
    for z in (0.06, 0.42, 0.78, H - 0.035):
        b.span(-W / 2 + p, W / 2 - p, -D / 2 + 0.005, D / 2 - 0.005, z, z + 0.035, "wood", top="wood_top")
    return b

def desk(W, D, H):
    b = Builder(); t = 0.07; ped = 0.44
    b.planks(-W / 2, W / 2, -D / 2, D / 2, H - t, H, 4, "y", "wood", top="wood_top")
    b.span(-W / 2 + 0.04, -W / 2 + 0.1, -D / 2 + 0.05, D / 2 - 0.05, 0, H - t, "wood")                         # left side
    px0, px1 = W / 2 - 0.05 - ped, W / 2 - 0.05
    b.span(px0 + 0.01, px1 - 0.01, -D / 2 + 0.06, D / 2 - 0.06, 0, 0.05, "wood_dark")                         # plinth
    b.span(px0, px1, -D / 2 + 0.05, D / 2 - 0.05, 0.05, H - t, "wood")                                         # pedestal
    for za, zb in ((0.09, 0.34), (0.37, H - t - 0.03)):                                                        # drawers
        b.span(px0 + 0.03, px1 - 0.03, -D / 2 + 0.025, -D / 2 + 0.05, za, zb, "wood_dark")
        zm = (za + zb) / 2
        xm = (px0 + px1) / 2
        b.span(xm - 0.07, xm + 0.07, -D / 2 + 0.005, -D / 2 + 0.026, zm - 0.014, zm + 0.014, "iron")
    b.span(-W / 2 + 0.1, px0, D / 2 - 0.08, D / 2 - 0.05, H * 0.42, H - t, "wood_dark")                        # back panel
    return b

def chair(W, D, H, rails):
    b = Builder(); l = 0.045; seat = round(H * 0.52, 3); st = 0.045
    for sx in (-1, 1):
        x = sx * (W / 2 - l / 2)
        b.span(x - l / 2, x + l / 2, -D / 2, -D / 2 + l, 0, seat - st, "wood_dark")                              # front legs
        b.span(x - l / 2, x + l / 2, D / 2 - l, D / 2, 0, H, "wood_dark", top="wood_top")                         # back posts
        b.span(x - 0.012, x + 0.012, -D / 2 + l, D / 2 - l, 0.12, 0.155, "wood_dark")                           # side stretchers
    b.planks(-W / 2, W / 2, -D / 2, D / 2, seat - st, seat, 3, "x", "wood", top="wood_top")                              # seat
    b.span(-W / 2 + l, W / 2 - l, -D / 2 + l / 2 - 0.012, -D / 2 + l / 2 + 0.012, 0.12, 0.155, "wood_dark")
    top = H - 0.04
    for k in range(rails):                                                                                     # back rails
        z = seat + 0.1 + (top - seat - 0.1) * (k + 1) / rails - 0.03
        b.span(-W / 2 + l, W / 2 - l, D / 2 - l / 2 - 0.013, D / 2 - l / 2 + 0.013, z - 0.035, z + 0.035, "wood")
    return b

def table2(W, D, H):
    b = Builder(); t = 0.06; l = 0.065
    b.planks(-W / 2, W / 2, -D / 2, D / 2, H - t, H, 3, "y", "wood", top="wood_top")
    for sx in (-1, 1):
        for sy in (-1, 1):
            cx, cy = sx * (W / 2 - 0.04 - l / 2), sy * (D / 2 - 0.04 - l / 2)
            b.span(cx - l / 2, cx + l / 2, cy - l / 2, cy + l / 2, 0, H - t, "wood_dark")
    ax, ay = W / 2 - 0.04 - l, D / 2 - 0.04 - l
    for sy in (-1, 1):
        y = sy * (D / 2 - 0.04 - l / 2)
        b.span(-ax, ax, y - 0.014, y + 0.014, H - t - 0.08, H - t, "wood")
    for sx in (-1, 1):
        x = sx * (W / 2 - 0.04 - l / 2)
        b.span(x - 0.014, x + 0.014, -ay, ay, H - t - 0.08, H - t, "wood")
    b.span(-ax - 0.01, ax + 0.01, -ay - 0.01, ay + 0.01, 0.1, 0.135, "wood", top="wood_top")                   # lower shelf
    return b

def basket(W, D, H):
    b = Builder(); t = 0.035
    b.span(-W / 2 + t, W / 2 - t, -D / 2 + t, D / 2 - t, 0, 0.04, "wood_dark")
    for sy in (-1, 1):
        b.planks(-W / 2, W / 2, sy * (D / 2 - t / 2) - t / 2, sy * (D / 2 - t / 2) + t / 2, 0, H, 3, "z", "wood", top="wood_top")
    for sx in (-1, 1):
        b.planks(sx * (W / 2 - t / 2) - t / 2, sx * (W / 2 - t / 2) + t / 2, -D / 2 + t, D / 2 - t, 0, H, 3, "z", "wood", top="wood_top")
    for sx in (-1, 1):
        for sy in (-1, 1):
            cx, cy = sx * (W / 2 - 0.02), sy * (D / 2 - 0.02)
            b.span(cx - 0.035, cx + 0.035, cy - 0.035, cy + 0.035, 0, 0.09, "iron")
            b.span(cx - 0.035, cx + 0.035, cy - 0.035, cy + 0.035, H - 0.09, H + 0.005, "iron")
    return b

def crate(W, D, H):
    b = Builder(); e = 0.06; s = 0.018
    b.span(-W / 2 + 0.01, W / 2 - 0.01, -D / 2 + 0.01, D / 2 - 0.01, 0.0, H - 0.005, "wood_dark", top="wood_top")
    for face in range(4):                                                                                       # 4 sides
        ang = face * math.pi / 2
        R = Matrix.Rotation(ang, 3, "Z")
        L = W if face % 2 == 0 else D
        off = (D if face % 2 == 0 else W) / 2
        def P(x, z, d=0.0):
            return tuple(R @ Vector((x, -off - d, z)))
        for z0, z1 in ((0.0, e), (H - e, H)):
            c = Vector(P(0, (z0 + z1) / 2, s / 2))
            b.box(tuple(c), (L - 0.02, s, z1 - z0) if face % 2 == 0 else (s, L - 0.02, z1 - z0), "wood", top="wood_top")
        for x in (-L / 2 + e / 2 + 0.01, L / 2 - e / 2 - 0.01):
            c = Vector(P(x, H / 2, s / 2))
            b.box(tuple(c), (e, s, H - 2 * e) if face % 2 == 0 else (s, e, H - 2 * e), "wood")
        p0, p1 = Vector(P(-L / 2 + e + 0.01, e, s * 1.5)), Vector(P(L / 2 - e - 0.01, H - e, s * 1.5))
        b.beam(tuple(p0), tuple(p1), 0.055, s, "wood")
    for sx in (-1, 1):                                                                                         # iron corners
        for sy in (-1, 1):
            cx, cy = sx * (W / 2 + 0.004), sy * (D / 2 + 0.004)
            for z0, z1 in ((0, 0.08), (H - 0.08, H + 0.006)):
                b.span(cx - 0.04, cx + 0.04, cy - 0.04, cy + 0.04, z0, z1, "iron")
    return b

def wall_shelf(W, D, H):
    """Wall at +Y (canonical), sticking out to -Y."""
    b = Builder(); t = 0.05
    b.planks(-W / 2, W / 2, -D / 2, D / 2 - 0.03, H - t, H, 2, "y", "wood", top="wood_top")
    for sx in (-1, 1):
        x = sx * (W / 2 - min(0.16, W * 0.18))
        b.span(x - 0.025, x + 0.025, D / 2 - 0.03, D / 2, 0, H + 0.02, "wood_dark", top="wood_top")            # post
        b.beam((x, D / 2 - 0.035, 0.03), (x, -D / 2 + 0.07, H - t - 0.005), 0.04, 0.04, "wood_dark")           # brace
    return b

def clock(W, D, H):
    b = Builder(); r = min(W, H) / 2; c = (0, 0, H / 2)
    b.disc(c, r, -D / 2 + 0.012, D / 2, 24, "copper", "copper")
    b.disc(c, r * 0.84, -D / 2, -D / 2 + 0.014, 24, "clock_face", "clock_face")
    for k in range(12):                                                                                        # hour ticks
        t = 2 * math.pi * k / 12
        R = Matrix.Rotation(-t, 3, "Y")
        p = Vector((r * 0.72 * math.sin(t), -D / 2 - 0.003, H / 2 + r * 0.72 * math.cos(t)))
        b.box(tuple(p), (0.014, 0.006, 0.04 if k % 3 == 0 else 0.026), "black", rot=R)
    for ang, length, wdt in ((-50, 0.62, 0.02), (165, 0.4, 0.026)):                                             # hands
        R = Matrix.Rotation(math.radians(ang), 3, "Y")
        p = Vector((0, -D / 2 - 0.008, H / 2)) + R @ Vector((0, 0, r * length / 2))
        b.box(tuple(p), (wdt, 0.006, r * length), "black", rot=R)
    b.box((0, -D / 2 - 0.01, H / 2), (0.03, 0.01, 0.03), "black")
    return b

# piece name in the .blend -> (builder, facing turn in degrees: canonical front -Y turned to the old piece's front)
PIECES = {
    "Bed": (bed, 0),                       # foot at -Y (head +Y)
    "Bed_Stool": (stool, 0),
    "Coffer": (coffer, -90),               # front -X
    "Shelf_01": (wardrobe, -90),           # doors -X
    "Shelf_03": (dresser, -90),            # open front -X
    "Stuffs_Shelf": (rack, -90),
    "Work_Table": (desk, 0),               # drawers face -Y, pedestal at +X
    "Work_Table_Chair": (lambda W, D, H: chair(W, D, H, 2), 180),   # back at -Y
    "LivingRoom_Chair": (lambda W, D, H: chair(W, D, H, 3), 180),
    "Livingroom_Table_2_small": (table2, -90),
    "Candy_Basket": (basket, 0),
    "Wood_Box": (crate, 0),
    "Shelf_Hang": (wall_shelf, 180),       # wall at -Y
    "Shelf_Hang_2": (wall_shelf, 180),
    "Clock": (clock, 0),                   # face -Y
}

# ---------------------------------------------------------------------------------------------------------------

def world_bounds(objs):
    lo = Vector((1e9,) * 3); hi = Vector((-1e9,) * 3)
    for o in objs:
        for v in o.bound_box:
            w = o.matrix_world @ Vector(v); lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
    return lo, hi

pal_dir = ROOT + "Exports/Remodel/Furniture_Fix/Palette"
paths = make_palette_images(pal_dir, name="Furniture_Palette")
pal = palette_material(paths, name="Furniture_Palette", glow_strength=0.0)

old_ref = bpy.data.collections.get("Old_Reference") or bpy.data.collections.new("Old_Reference")
if old_ref.name not in bpy.context.scene.collection.children:
    bpy.context.scene.collection.children.link(old_ref)

top = bpy.data.collections["Furniture_Fix"]
report = []
for gname, (fn, turn) in PIECES.items():
    g = top.children[gname]
    g["front_turn"] = turn
    whole = [o for c in g.children if c.name.endswith(" - whole") for o in c.all_objects if o.type == "MESH"]
    bpy.context.view_layer.update()
    lo, hi = world_bounds(whole)
    size = hi - lo
    # canonical size: the old footprint turned back
    if turn in (90, -90):
        Wc, Dc = size.y, size.x
    else:
        Wc, Dc = size.x, size.y
    built = fn(Wc, Dc, size.z)
    if isinstance(built, Builder):
        built = {whole[0].name: built}
    R = Matrix.Rotation(math.radians(turn), 4, "Z")
    # all new parts of the piece, turned, then fitted to the old footprint centre + floor
    allv = [R @ Vector(v) for b in built.values() for v in b.verts]
    nlo = Vector(map(min, *allv)) if len(allv) > 1 else allv[0]
    nlo = Vector((min(v.x for v in allv), min(v.y for v in allv), min(v.z for v in allv)))
    nhi = Vector((max(v.x for v in allv), max(v.y for v in allv), max(v.z for v in allv)))
    T = Matrix.Translation(Vector(((lo.x + hi.x) / 2 - (nlo.x + nhi.x) / 2, (lo.y + hi.y) / 2 - (nlo.y + nhi.y) / 2, lo.z - nlo.z)))
    M = T @ R
    for oname, b in built.items():
        ob = bpy.data.objects[oname]
        # keep the old mesh for comparison
        keep = ob.copy(); keep.data = ob.data.copy(); keep.name = oname + "_OLD"
        old_ref.objects.link(keep)
        inv = ob.matrix_world.inverted()
        me = bpy.data.meshes.new(oname)
        me.from_pydata([tuple(inv @ (M @ Vector(v))) for v in b.verts], [], b.faces)
        me.update()
        # UVs onto the palette (height in world metres from the piece's floor)
        tmp = bpy.data.objects.new("_tmp", me)
        bpy.context.scene.collection.objects.link(tmp)
        tmp.matrix_world = ob.matrix_world
        mw = ob.matrix_world.copy()
        h0, h1 = lo.z, lo.z + max(size.z, 0.3)
        sws = b.sw
        palette_uv(tmp, lambda f, pid: (sws[f.index], 0.35 + 0.3 * ((pid * 37) % 10) / 10.0),
                   lambda co: (mw @ co).z, h_range=(h0 - 0.1 * (h1 - h0), h1 + 0.05 * (h1 - h0)), mw=mw)
        bpy.data.objects.remove(tmp)
        old_me = ob.data
        ob.data = me
        ob.data.materials.clear()
        ob.data.materials.append(pal)
        me.name = old_me.name
        if old_me.users == 0:
            bpy.data.meshes.remove(old_me)
        tris = sum(len(f) - 2 for f in b.faces)
        report.append(f"{gname}: {oname} {len(b.faces)} faces (~{tris} tris)")
old_ref.hide_render = True
bpy.context.view_layer.layer_collection.children[old_ref.name].exclude = True
print("REPORT"); print("\n".join(report))
if SAVE:
    bpy.ops.wm.save_mainfile()
    print("SAVED", bpy.data.filepath)
