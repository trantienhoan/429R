"""SlimeBowl kit: gate 2 (Slime Bowling) of 429R, first made 2026-11-23.

User (2026-11-23): "For each gate create a kit for easy change and fix." The look is the approved concept
Exports/Bowling_Concept/concept_v1.png (the Slime Bowl: a haunted toy bowling alley in the night sky); the game's real
layout and sizes win (Assets/Scripts/Bowling/BowlingLayout.cs, BowlingArenaSetupMenu.cs).

Makes SlimeBowl_Kit.blend:
  - SlimeBowl_Kit: one collection per piece (SlimeBowl_Kit_<Piece>), each piece one object of the same name, its pivot
    on its snap point, in real metres, in a row at y = -45 (the catalogue);
  - one palette texture + material, SlimeBowl_Palette (textures/SlimeBowl_Palette.png: colour, alpha = gloss for the
    Unity material's smoothness; SlimeBowl_Palette_Emission.png: glow). Every face is UV'd onto its swatch by this
    script (u = the swatch's column + a small tint shift, v = height: darker low, lighter high), as in
    Exports/Remodel/tools/palette_kit.py, so nobody unwraps anything;
  - Sample_Arena (not exported): the pieces laid out like the concept at the game's real sizes, linked copies (edit a
    piece and the arena follows), plus stand-ins that aren't kit pieces (Sample_*: the balls, the giant slime, the moon);
  - Kit_Sheet (not exported): every piece in a grid, scaled to fit, labelled with its real size and triangles;
  - renders next to the .blend: kit_sheet.png, arena_concept_angle.png (the concept's camera), arena_eye_level.png (the
    player's eyes on the island, looking down lane 0).
The user's clean style: closed parts, all quads where it's cheap, separate trim pieces, no bevels; flat-shaded
woodwork and stone, smooth-shaded pumpkins, slime, rocks and clouds.

Frames (metres; Blender Z up). In the arena, lane k's pieces sit "along" metres out from the island's centre, "side"
metres to the lane's right (lane 0 = +Y in the X frame, lane 1 = -X, 2 = -Y, 3 = +X, as the game's lanes 0..3):
  Lane_End     2.4 (LaneStart) to 3.6; the foul line at 2.5          pivot: near end, centre line, lane top (z 0)
  Lane_4m      3.6, 7.6, 11.6, 15.6 (the wood ends at 19.6)          pivot: near end, centre line, lane top
  Lane_Arrows  7.0 (5 slim gold aim arrows, real-bowling style)       pivot: the back row's middle, on the lane top
  Rail_4m      side +-0.95 (RailSide), from 2.5 every 4 m            pivot: near end, bottom middle (0.1 x 0.2 m)
  Trestle      under the lane, top at z -0.3 (every 6 m)              pivot: top middle
  Deck         21.3 (DeckAt): round stone stage r 2.0, cut straight   pivot: centre, top (z 0 = where the pins stand)
               at 19.6 where the wood ends
  Rune_Ring_*  21.3, r 0.86-1.0 (the game's ring: radius 1)           pivot: centre, bottom (on the deck top)
  Brazier      21.3, side +-1.6 (FireSide); Flame on its coals        pivot: foot middle / flame base
  Pumpkin_Arch 23.1, behind the deck (never over the lane)            pivot: base middle, opening towards -Y
  Island       the centre: 5 x 5 m rounded square (DirtHalf 2.5, corner 0.6), top at z 0.02 (DirtTop)
  Vine         a ball sprout: 1.1 m out (SproutRadius) at the game's sprout yaws; its cup holds a ball at hand height
  Slime_Pin    a rack slot (0.66 m tall: the game's young pin), face towards -Y (the island)
  Approach_Stone 1.2 m out on each lane's line: where to stand to bowl
  Chain_Rock, Floating_Rock, Lantern, Lantern_Post, Pumpkin_Small, Cloud_Puff: dressing.

Run (Blender 5.x):
  blender -b --factory-startup --python Exports/Gates/gate2/tools/slimebowl_kit.py -- [out.blend] [--force] [--no-render]
out.blend defaults to Exports/Gates/gate2/SlimeBowl_Kit.blend. An existing .blend (the user may have edited it) is never
overwritten without --force: the new one is saved as *_generated.blend (renders *_generated.png).
"""
import bpy, bmesh, sys, os, math, random, zlib
from mathutils import Vector, Matrix

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
POS = [a for a in ARGS if not a.startswith("--")]
HERE = os.path.dirname(os.path.abspath(__file__))
GATE2 = os.path.dirname(HERE)
OUT = os.path.abspath(POS[0]) if POS else os.path.join(GATE2, "SlimeBowl_Kit.blend")
FORCE = "--force" in ARGS
RENDER = "--no-render" not in ARGS
SUFFIX = ""
if os.path.exists(OUT) and not FORCE:
    OUT = OUT[:-6] + "_generated.blend"
    SUFFIX = "_generated"
    print("kept the existing file; this one is", OUT)
OUTDIR = os.path.dirname(OUT)
TEXDIR = os.path.join(OUTDIR, "textures")

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene

# ------------------------------------------------------------------------------------------------- the game's numbers
DIRT_HALF, DIRT_CORNER, DIRT_TOP = 2.5, 0.6, 0.02
LANE_START, LANE_END, LANE_W, RAIL_SIDE, FOUL_LINE = 2.4, 22.6, 2.0, 0.95, 2.5
HEAD_PIN, DECK_AT, RING_SIZE = 20.8, 21.3, 2.0
FIRE_SIDE, SPROUT_R = 1.6, 1.1
RACK = [(0.0, 20.8), (-0.26, 21.25), (0.26, 21.25), (-0.52, 21.7), (0.0, 21.7), (0.52, 21.7)]
SPROUT_YAWS = [22.5, 112.5, 202.5, 292.5, 67.5, 247.5, 157.5, 337.5]   # Unity yaw: from lane 0 towards lane 3
LANE_T, RAIL_W, RAIL_H = 0.3, 0.1, 0.2

# ------------------------------------------------------------------------------------------------------ kit numbers
END_LEN, SEG_LEN, WOOD_END = 1.2, 4.0, 19.6
DECK_R = 2.0
BOARDS, BOARD_GAP, BOARD_T = 6, 0.025, 0.1
CUP_Z = 0.80           # the vine's cup; a ball rests in it at about 0.9 m (hand height, not the floor)
ARCH_AT = 23.1
PIN_H = 0.66

# ---------------------------------------------------------------------------------------------------------- palette
# name, dark (v 0), light (v 1), glow (emission = colour x glow), gloss (alpha: the Unity material's smoothness)
SWATCHES = [
    ("wood_honey",   "8a5426", "eab26a", 0.0, 0.25),
    ("wood_dark",    "38200f", "7c4e2e", 0.0, 0.15),
    ("groove",       "1a0f08", "38241a", 0.0, 0.05),
    ("candy_red",    "8a0e16", "f24a4a", 0.0, 0.45),
    ("candy_white",  "aaa2b0", "fff8f2", 0.0, 0.45),
    ("stone",        "4a4458", "aca4be", 0.0, 0.10),
    ("stone_dark",   "26222e", "645c72", 0.0, 0.08),
    ("moss",         "2c4818", "88ba46", 0.0, 0.10),
    ("grass_dark",   "182c0e", "4a7a28", 0.0, 0.08),
    ("dirt",         "2e1a0e", "7a4a28", 0.0, 0.05),
    ("rock",         "322e3a", "827a92", 0.0, 0.10),
    ("iron",         "181416", "56494c", 0.0, 0.35),
    ("pumpkin",      "963208", "ff9e3a", 0.0, 0.30),
    ("stem",         "243212", "6e7e2c", 0.0, 0.10),
    ("leaf",         "22501a", "6eb43e", 0.0, 0.20),
    ("vine",         "183e10", "4e8c28", 0.0, 0.20),
    ("slime",        "1c7812", "90f250", 0.0, 0.80),
    ("slime_dark",   "081c06", "1a3810", 0.0, 0.60),
    ("gold",         "8a5a10", "ffd060", 0.0, 0.60),
    ("glow_gold",    "c88418", "fff0a8", 1.0, 0.60),
    ("glow_mist",    "2a0c58", "aa6aff", 1.0, 0.20),
    ("glow_flame",   "ff4808", "ffe680", 1.0, 0.00),
    ("glow_coal",    "b81c00", "ff9a20", 1.0, 0.00),
    ("glow_lantern", "ff8a20", "ffe8a8", 1.0, 0.30),
    ("glow_green",   "18b030", "b0ffa8", 1.0, 0.30),
    ("glow_red",     "d01828", "ffb0b0", 1.0, 0.30),
    ("glow_blue",    "1c48f0", "a8c4ff", 1.0, 0.30),
    ("glow_yellow",  "e0a000", "fff2a0", 1.0, 0.30),
    ("cloud",        "2c2450", "a494d4", 0.42, 0.05),
    ("white",        "c4bcc8", "fafaff", 0.0, 0.30),
    ("black",        "060408", "1a161e", 0.0, 0.30),
]
COL_W, TEX_H, TEX_W = 32, 256, 1024
BASE_GLOW = 0.16
INDEX = {s[0]: i for i, s in enumerate(SWATCHES)}
assert len(SWATCHES) <= TEX_W // COL_W


def hexrgb(h):
    return [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]


def make_palette(folder):
    """Writes SlimeBowl_Palette.png (RGB + gloss in alpha) and SlimeBowl_Palette_Emission.png; returns both paths."""
    import numpy as np
    os.makedirs(folder, exist_ok=True)
    col = np.zeros((TEX_H, TEX_W, 4), dtype=np.float32)
    col[..., :3] = 0.5
    emi = np.zeros((TEX_H, TEX_W, 4), dtype=np.float32)
    emi[..., 3] = 1.0
    v = np.linspace(0.0, 1.0, TEX_H)[:, None]
    v = v * v * (3 - 2 * v) * 0.85 + v * 0.15                     # a soft S curve (palette_kit.py)
    t = (np.arange(COL_W) + 0.5) / COL_W
    shift = (t - 0.5)[None, :, None]
    warm = np.array([1.06, 1.0, 0.92])
    for i, (name, dk, lt, glow, gloss) in enumerate(SWATCHES):
        a, b = np.array(hexrgb(dk)), np.array(hexrgb(lt))
        g = a[None, None, :] + (b - a)[None, None, :] * v[:, :, None]
        g = g * (1 + 0.10 * shift) * (1 + (warm - 1) * shift * 2)   # left cooler/darker, right warmer/lighter
        x0 = i * COL_W
        col[:, x0:x0 + COL_W, :3] = np.clip(g, 0, 1)
        col[:, x0:x0 + COL_W, 3] = gloss
        # Every colour glows a little (BASE_GLOW: a toy that stays readable in the night arena, Unity test 2026-11-23);
        # the glow swatches by their own Glow.
        emi[:, x0:x0 + COL_W, :3] = np.clip(g * max(glow, BASE_GLOW), 0, 1)
    paths = []
    for arr, suffix, alpha in ((col, "", True), (emi, "_Emission", False)):
        p = os.path.join(folder, "SlimeBowl_Palette" + suffix + ".png")
        img = bpy.data.images.new("SlimeBowl_Palette" + suffix, TEX_W, TEX_H, alpha=alpha)
        img.alpha_mode = 'CHANNEL_PACKED'
        img.pixels.foreach_set(arr.ravel())
        img.filepath_raw = p
        img.file_format = 'PNG'
        img.save()
        bpy.data.images.remove(img)
        paths.append(p)
    return paths


def palette_material(paths):
    m = bpy.data.materials.new("SlimeBowl_Palette")
    m.use_nodes = True
    nt = m.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    out = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL")
    tc = nt.nodes.new("ShaderNodeTexImage")
    te = nt.nodes.new("ShaderNodeTexImage")
    tc.image = bpy.data.images.load(paths[0], check_existing=True)
    tc.image.alpha_mode = 'CHANNEL_PACKED'
    te.image = bpy.data.images.load(paths[1], check_existing=True)
    tc.interpolation = te.interpolation = 'Linear'
    rough = nt.nodes.new("ShaderNodeMath")
    rough.operation = 'SUBTRACT'
    rough.inputs[0].default_value = 1.0
    tc.location, te.location, rough.location = (-650, 250), (-650, -150), (-350, 80)
    bsdf.location, out.location = (-100, 100), (250, 100)
    nt.links.new(tc.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(tc.outputs["Alpha"], rough.inputs[1])
    nt.links.new(rough.outputs[0], bsdf.inputs["Roughness"])
    nt.links.new(te.outputs["Color"], bsdf.inputs["Emission Color"])
    bsdf.inputs["Emission Strength"].default_value = 1.6
    nt.nodes.active = tc
    return m


# ------------------------------------------------------------------------------------------------------ mesh builder
def R(sw, lo=0.0, hi=1.0, tint=None, top=0.0, smooth=False, up=None, keep=False, v=None):
    """A face's paint: swatch, the height range for its dark-to-light v (piece frame), a fixed tint (else per part),
    a lift for faces looking up, smooth shading, another swatch for faces looking up, keep the winding (open faces),
    a fixed v."""
    return dict(sw=sw, lo=lo, hi=hi, tint=tint, top=top, smooth=smooth, up=up, keep=keep, v=v)


class Part:
    """One piece's faces. Vertices are shared only inside a group (one closed loose part)."""

    def __init__(self):
        self.co, self.pool, self.faces = [], {}, []

    def vid(self, p, group):
        key = (group, round(p[0], 5), round(p[1], 5), round(p[2], 5))
        i = self.pool.get(key)
        if i is None:
            i = len(self.co)
            self.co.append(Vector(p))
            self.pool[key] = i
        return i

    def face(self, pts, rule, group):
        idx = []
        for p in pts:
            i = self.vid(p, group)
            if not idx or idx[-1] != i:
                idx.append(i)
        if len(idx) > 1 and idx[0] == idx[-1]:
            idx.pop()
        if len(set(idx)) >= 3:
            self.faces.append((idx, rule, group))


def hash01(*a):
    return (zlib.crc32(repr(a).encode()) % 1000) / 1000.0


def T(M, p):
    return tuple(M @ Vector(p)) if M is not None else tuple(p)


def box(P, lo, hi, rule, group, M=None):
    x0, y0, z0 = lo
    x1, y1, z1 = hi
    c = [T(M, p) for p in ((x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0),
                           (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1))]
    for q in ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)):
        P.face([c[i] for i in q], rule, group)


def loft(P, rings, rule, group, cap0=None, cap1=None, closed=True):
    """Quads between consecutive rings (lists of points, same count, or a single point = a pole). rule: a rule or
    rule(k, j). cap0/cap1: the rule for an n-gon over the first/last ring (None = open)."""
    rk = rule if callable(rule) else (lambda k, j: rule)
    for k in range(len(rings) - 1):
        a, b = rings[k], rings[k + 1]
        if len(a) == 1 and len(b) == 1:
            continue
        n = max(len(a), len(b))
        for j in range(n if closed else n - 1):
            jj = (j + 1) % n
            if len(a) == 1:
                P.face([a[0], b[jj], b[j]], rk(k, j), group)
            elif len(b) == 1:
                P.face([a[j], a[jj], b[0]], rk(k, j), group)
            else:
                P.face([a[j], a[jj], b[jj], b[j]], rk(k, j), group)
    if cap0 is not None and len(rings[0]) > 2:
        P.face(list(reversed(rings[0])), cap0, group)
    if cap1 is not None and len(rings[-1]) > 2:
        P.face(rings[-1], cap1, group)


def lathe(P, profile, segs, rule, group, M=None, radial=None, phase=0.0, twist=0.0, cap0=None, cap1=None):
    """profile: [(r, z)] bottom to top; r == 0 at an end = a pole. radial(k, j) -> radius factor."""
    rings = []
    for k, (r, z) in enumerate(profile):
        if r <= 1e-6:
            rings.append([T(M, (0.0, 0.0, z))])
            continue
        ring = []
        for j in range(segs):
            a = phase + twist * k + 2 * math.pi * j / segs
            f = radial(k, j) if radial else 1.0
            ring.append(T(M, (r * f * math.cos(a), r * f * math.sin(a), z)))
        rings.append(ring)
    loft(P, rings, rule, group, cap0, cap1)


def lathe_loop(P, loop, segs, rule, group, M=None, phase=0.0):
    """A closed (r, z) profile turned round Z: a ring with that cross-section."""
    rings = []
    for r, z in loop + [loop[0]]:
        rings.append([T(M, (r * math.cos(phase + 2 * math.pi * j / segs), r * math.sin(phase + 2 * math.pi * j / segs), z))
                      for j in range(segs)])
    loft(P, rings, rule, group)


def prism(P, outline, z0, z1, top, side, bottom, group, M=None):
    rings = [[T(M, (x, y, z0)) for x, y in outline], [T(M, (x, y, z1)) for x, y in outline]]
    loft(P, rings, side, group, bottom, top)


def tube(P, path, radii, sides, rule, group, cap0=None, cap1=None, phase=0.0):
    """A tube along path (points); radii per point (0 = a pointed end)."""
    path = [Vector(p) for p in path]
    n = len(path)
    tans = []
    for i in range(n):
        t = path[min(i + 1, n - 1)] - path[max(i - 1, 0)]
        tans.append(t.normalized())
    ref = Vector((0, 0, 1)) if abs(tans[0].z) < 0.9 else Vector((1, 0, 0))
    nrm = tans[0].cross(ref).normalized()
    rings = []
    for i in range(n):
        t = tans[i]
        nrm = (nrm - t * nrm.dot(t)).normalized()
        bi = t.cross(nrm)
        r = radii[i] if isinstance(radii, (list, tuple)) else radii
        if r <= 1e-6:
            rings.append([tuple(path[i])])
            continue
        rings.append([tuple(path[i] + (nrm * math.cos(phase + 2 * math.pi * j / sides) +
                                       bi * math.sin(phase + 2 * math.pi * j / sides)) * r) for j in range(sides)])
    loft(P, rings, rule, group, cap0, cap1)


def rounded_square(half, r, per_corner=4):
    """Anticlockwise outline of a rounded square (seen from above)."""
    pts = []
    for cx, cy, a0 in ((half - r, half - r, 0), (-half + r, half - r, 90), (-half + r, -half + r, 180), (half - r, -half + r, 270)):
        for i in range(per_corner + 1):
            a = math.radians(a0 + 90.0 * i / per_corner)
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def ngon(r, n, phase=0.0):
    return [(r * math.cos(phase + 2 * math.pi * i / n), r * math.sin(phase + 2 * math.pi * i / n)) for i in range(n)]


def rock(P, centre, size, rng, rule, group, segs=7, flat_top=False, M=None):
    sx, sy, sz = size
    prof = [(0.0, -1.0), (0.55, -0.82), (0.95, -0.32), (1.0, 0.12), (0.72, 0.55), (0.0, 0.62 if flat_top else 0.78)]
    jit = {(k, j): rng.uniform(0.8, 1.16) for k in range(len(prof)) for j in range(segs)}
    Mr = Matrix.Translation(centre) @ Matrix.Rotation(rng.uniform(0, 6.283), 4, 'Z') @ Matrix.Diagonal((sx, sy, sz, 1.0))
    if M is not None:
        Mr = M @ Mr
    lathe(P, prof, segs, rule, group, M=Mr, radial=lambda k, j: jit[(k, j)])


def frame_ring(P, w, h, bar, thick, rule, group, M):
    """A rectangular ring (a chain link): w x h outside, bar wide, thick deep; in the XZ plane, centred."""
    ox, oz, ix, iz, t = w / 2, h / 2, w / 2 - bar, h / 2 - bar, thick / 2
    outer = [(ox, oz), (-ox, oz), (-ox, -oz), (ox, -oz)]
    inner = [(ix, iz), (-ix, iz), (-ix, -iz), (ix, -iz)]
    P3 = lambda xz, y: T(M, (xz[0], y, xz[1]))
    for i in range(4):
        j = (i + 1) % 4
        P.face([P3(outer[i], -t), P3(outer[j], -t), P3(inner[j], -t), P3(inner[i], -t)], rule, group)
        P.face([P3(outer[i], t), P3(inner[i], t), P3(inner[j], t), P3(outer[j], t)], rule, group)
        P.face([P3(outer[i], -t), P3(outer[i], t), P3(outer[j], t), P3(outer[j], -t)], rule, group)
        P.face([P3(inner[i], -t), P3(inner[j], -t), P3(inner[j], t), P3(inner[i], t)], rule, group)


def pumpkin(P, centre, r, group, squash=0.78, ribs=8, face=False, stem=True, rot=0.0, rng=None, face_dir=-1.0):
    """A ribbed pumpkin, its own dark-bottom/light-top shade; face: a carved glowing face towards face_dir * Y."""
    c = Vector(centre)
    prof = []
    for phi in (-90, -50, -10, 30, 64, 90):
        a = math.radians(phi)
        if phi == -90:
            prof.append((0.0, -r * squash * 0.82))
        elif phi == 90:
            prof.append((0.0, r * squash * 0.78))
        else:
            prof.append((r * math.cos(a), r * squash * math.sin(a)))
    lo, hi = c.z - r * squash, c.z + r * squash
    M = Matrix.Translation(c) @ Matrix.Rotation(rot, 4, 'Z')
    lathe(P, prof, ribs * 2, R("pumpkin", lo=lo, hi=hi, smooth=True), group, M=M,
          radial=lambda k, j: 1.0 if j % 2 == 0 else 0.86)
    if stem:
        rng = rng or random.Random(hash01(group) * 1e6)
        lean = Vector((rng.uniform(-0.25, 0.25), rng.uniform(-0.25, 0.25), 1.0)).normalized()
        base = c + Vector((0, 0, r * squash * 0.7))
        path = [base, base + lean * r * 0.22, base + lean * r * 0.38 + Vector((r * 0.08, 0, 0))]
        tube(P, path, [r * 0.13, r * 0.10, r * 0.07], 4, R("stem", lo=base.z, hi=base.z + r * 0.4, smooth=False),
             group + "_stem", cap0=R("stem", v=0.3), cap1=R("stem", v=0.8))
    if face:
        g = R("glow_lantern", lo=c.z - r * 0.5, hi=c.z + r * 0.5)
        fy = c.y + face_dir * r * 0.86
        depth = face_dir * r * 0.35
        for sx in (-1, 1):   # the eyes: triangles pointing up
            ex, ez = c.x + sx * r * 0.34, c.z + r * squash * 0.22
            tri = [(ex - r * 0.15, ez - r * 0.1), (ex + r * 0.15, ez - r * 0.1), (ex, ez + r * 0.16)]
            rings = [[(x, fy + face_dir * r * 0.06, z) for x, z in tri], [(x, fy - depth, z) for x, z in tri]]
            loft(P, rings, g, group + "_eye%d" % sx, g, g)
        # the grin: a wide band with two teeth notches (three boxes)
        mz = c.z - r * squash * 0.22
        for x0, x1, dz in ((-0.42, -0.16, 0.04), (-0.12, 0.12, -0.02), (0.16, 0.42, 0.04)):
            ys = sorted((fy + face_dir * r * 0.05, fy - depth))
            box(P, (c.x + x0 * r, ys[0], mz - r * 0.08 + dz * r), (c.x + x1 * r, ys[1], mz + r * 0.07 + dz * r), g,
                group + "_grin%.2f" % x0)


# -------------------------------------------------------------------------------------------------------- objects
MAT = None
PIECES = {}


def build(P, name, coll, sharp_angle=70.0):
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    verts = [bm.verts.new(c) for c in P.co]
    made = []
    for idx, rule, group in P.faces:
        try:
            f = bm.faces.new([verts[i] for i in idx])
        except ValueError:
            continue
        made.append((f, rule, group))
    bm.faces.ensure_lookup_table()
    recalc = [f for f, r, g in made if not r["keep"]]
    if recalc:
        bmesh.ops.recalc_face_normals(bm, faces=recalc)
    bm.normal_update()
    uv = bm.loops.layers.uv.new("UVMap")
    for f, rule, group in made:
        sw = rule["sw"]
        if rule["up"] and f.normal.z > 0.55:
            sw = rule["up"]
        col = INDEX[sw]
        tint = rule["tint"] if rule["tint"] is not None else hash01(name, group)
        u = (col * COL_W + 4 + tint * (COL_W - 8)) / TEX_W
        bonus = rule["top"] if f.normal.z > 0.7 else 0.0
        for l in f.loops:
            if rule["v"] is not None:
                vv = rule["v"]
            else:
                vv = (l.vert.co.z - rule["lo"]) / max(1e-6, rule["hi"] - rule["lo"]) + bonus
            l[uv].uv = (u, min(0.97, max(0.03, vv)))
        f.smooth = rule["smooth"]
    for e in bm.edges:
        fs = e.link_faces
        if len(fs) == 2 and (fs[0].smooth != fs[1].smooth or
                             (fs[0].smooth and fs[0].normal.angle(fs[1].normal, 0.0) > math.radians(sharp_angle))):
            e.smooth = False
    bm.to_mesh(me)
    bm.free()
    me.materials.append(MAT)
    ob = bpy.data.objects.new(name, me)
    coll.objects.link(ob)
    return ob


# ---------------------------------------------------------------------------------------------------------- pieces
BOARD_TINTS = (0.15, 0.7, 0.4, 0.92, 0.25, 0.6)


def lane_section(P, length, gname="lane"):
    """The lane's wood from y 0 to length: a dark base slab and 6 honey boards on it, 2.5 cm apart (the grooves)."""
    box(P, (-LANE_W / 2, 0.0, -LANE_T), (LANE_W / 2, length, -BOARD_T), R("wood_dark", lo=-0.5, hi=0.1), gname + "_base")
    w = (LANE_W - (BOARDS - 1) * BOARD_GAP) / BOARDS
    x = -LANE_W / 2
    for b in range(BOARDS):
        box(P, (x, 0.0, -BOARD_T), (x + w, length, 0.0),
            R("wood_honey", lo=-0.55, hi=0.25, tint=BOARD_TINTS[b], top=0.08), gname + "_board%d" % b)
        x += w + BOARD_GAP


def piece_lane_4m():
    P = Part()
    lane_section(P, SEG_LEN)
    return P


def piece_lane_end():
    P = Part()
    lane_section(P, END_LEN)
    # The foul line (the game's FoulLine, 2.5 m out = 0.1 m into this piece): a gold strip across, 1 cm proud.
    f = FOUL_LINE - LANE_START
    box(P, (-LANE_W / 2, f + 0.03, 0.0), (LANE_W / 2, f + 0.11, 0.012), R("glow_gold", v=0.6), "foul")
    return P


def piece_lane_arrows():
    """5 slim gold triangles pointing down the lane in a V (the middle one furthest), like a real lane's aim arrows."""
    P = Part()
    g = R("glow_gold", v=0.55)
    for i, (x, y) in enumerate(((-0.62, 0.0), (-0.31, 0.3), (0.0, 0.6), (0.31, 0.3), (0.62, 0.0))):
        tri = [(x - 0.07, y), (x + 0.07, y), (x, y + 0.34)]
        prism(P, tri, 0.0, 0.012, g, g, g, "arrow%d" % i)
    return P


def piece_rail_4m():
    """The candy-striped rail (the game's 0.1 x 0.2 m rail box), red and white bands 0.25 m long."""
    P = Part()
    n = int(round(SEG_LEN / 0.25))
    x0, x1, z0, z1 = -RAIL_W / 2, RAIL_W / 2, 0.0, RAIL_H
    for i in range(n):
        y0, y1 = SEG_LEN * i / n, SEG_LEN * (i + 1) / n
        rl = R("candy_red" if i % 2 == 0 else "candy_white", lo=-0.15, hi=0.3, tint=0.5)
        P.face([(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)], rl, "rail")
        P.face([(x0, y0, z0), (x0, y1, z0), (x1, y1, z0), (x1, y0, z0)], rl, "rail")
        P.face([(x0, y0, z0), (x0, y0, z1), (x0, y1, z1), (x0, y1, z0)], rl, "rail")
        P.face([(x1, y0, z0), (x1, y1, z0), (x1, y1, z1), (x1, y0, z1)], rl, "rail")
    end = R("candy_white", lo=-0.15, hi=0.3, tint=0.5)
    P.face([(x0, 0, z0), (x1, 0, z0), (x1, 0, z1), (x0, 0, z1)], end, "rail")
    P.face([(x0, SEG_LEN, z0), (x0, SEG_LEN, z1), (x1, SEG_LEN, z1), (x1, SEG_LEN, z0)], end, "rail")
    return P


def piece_trestle():
    """Under the lane (top at its underside): a cap beam, two splayed legs, an X brace, a little rock under each leg."""
    P = Part()
    rng = random.Random(11)
    wd = R("wood_dark", lo=-6.5, hi=0.6)
    box(P, (-1.15, -0.15, -0.26), (1.15, 0.15, 0.0), wd, "cap")

    def beam(a, b, w, d, group):
        a, b = Vector(a), Vector(b)
        t = (b - a).normalized()
        side = Vector((0, 1, 0)).cross(t).normalized()
        up = t.cross(side).normalized()
        ring = lambda p: [tuple(p + side * sx * w / 2 + up * sy * d / 2) for sx, sy in ((1, 1), (-1, 1), (-1, -1), (1, -1))]
        loft(P, [ring(a), ring(b)], wd, group, wd, wd)

    for sx in (-1, 1):
        beam((sx * 0.82, 0.0, -0.24), (sx * 1.28, 0.0, -3.55), 0.24, 0.24, "leg%d" % sx)
        rock(P, (sx * 1.3, 0.0, -3.72), (0.34, 0.3, 0.24), rng, R("rock", lo=-4.0, hi=-3.4, smooth=True, up="moss"), "foot%d" % sx)
    beam((-0.92, 0.0, -0.55), (1.12, 0.0, -2.9), 0.13, 0.09, "brace_a")
    beam((0.92, 0.0, -0.55), (-1.12, 0.0, -2.9), 0.13, 0.09, "brace_b")
    return P


def piece_chain_rock():
    """A chain hanging from its top (7 links) with a small floating rock on the end."""
    P = Part()
    rng = random.Random(5)
    ir = R("iron", lo=-2.0, hi=0.0)
    pitch = 0.22
    for i in range(7):
        z = -0.15 - i * pitch
        M = Matrix.Translation((0, 0, z)) @ Matrix.Rotation(math.radians(90 * (i % 2)), 4, 'Z')
        frame_ring(P, 0.16, 0.3, 0.045, 0.045, ir, "link%d" % i, M)
    bottom = -0.15 - 7 * pitch
    rock(P, (0.0, 0.0, bottom - 0.3), (0.5, 0.45, 0.4), rng, R("rock", lo=bottom - 0.8, hi=bottom, smooth=True, up="moss"), "rock")
    return P


def island_rings(rng):
    out = rounded_square(DIRT_HALF, DIRT_CORNER, 4)

    def ring(scale, z, jit=0.0, zjit=0.0):
        pts = []
        for x, y in out:
            s = scale * (1 + rng.uniform(-jit, jit))
            pts.append((x * s, y * s, z + rng.uniform(-zjit, zjit)))
        return pts

    return out, [
        [(x, y, DIRT_TOP) for x, y in out],
        ring(1.016, -0.10),
        ring(0.99, -0.27, 0.01),
        ring(0.94, -0.72, 0.05, 0.05),
        ring(0.80, -1.42, 0.08, 0.12),
        ring(0.58, -2.22, 0.10, 0.14),
        ring(0.32, -2.95, 0.12, 0.12),
        [(0.12, -0.08, -3.5)],
    ]


def piece_island():
    """The middle: the game's 5 x 5 m rounded-square dirt (the only teleport area), a moss top with a dirt bed round
    the middle (the ball sprouts' ring), a mossy lip, an earth-and-rock underside and hanging roots."""
    P = Part()
    rng = random.Random(3)
    out, rings = island_rings(rng)
    ang = [math.atan2(y, x) for x, y in out]
    zt = DIRT_TOP
    c1 = [(0.62 * math.cos(a), 0.62 * math.sin(a), zt) for a in ang]
    c2 = [(1.55 * math.cos(a), 1.55 * math.sin(a), zt) for a in ang]
    moss = R("moss", lo=-0.6, hi=0.12, tint=0.55)
    bed = R("dirt", lo=-0.5, hi=0.1, tint=0.6)
    n = len(out)
    P.face(c1, moss, "island")
    for i in range(n):
        j = (i + 1) % n
        P.face([c1[i], c1[j], c2[j], c2[i]], bed, "island")
        P.face([c2[i], c2[j], rings[0][j], rings[0][i]], moss, "island")

    def body(k, j):
        if k <= 1:
            return R("grass_dark", lo=-0.35, hi=0.05, tint=0.5)
        r = hash01("isl", k, j)
        if k <= 3:
            return R("rock" if r < 0.28 else "dirt", lo=-3.6, hi=0.0, smooth=True)
        return R("dirt" if r < 0.3 else "rock", lo=-3.6, hi=0.0, smooth=True)

    loft(P, rings, body, "island")
    # roots hanging from the underside
    for i, (k, j) in enumerate(((4, 2), (4, 9), (5, 14), (4, 17), (5, 6))):
        a = Vector(rings[k][j]) * 0.97
        out_dir = Vector((a.x, a.y, 0)).normalized()
        L = rng.uniform(0.7, 1.2)
        path = [a, a + out_dir * 0.12 + Vector((0, 0, -L * 0.35)), a + out_dir * 0.05 + Vector((0.08, 0.05, -L * 0.7)),
                a + out_dir * 0.18 + Vector((0.0, -0.06, -L))]
        tube(P, path, [0.08, 0.06, 0.04, 0.0], 5, R("wood_dark", lo=-4.5, hi=-1.0, smooth=True), "root%d" % i,
             cap0=R("wood_dark", v=0.4))
    return P


def piece_approach_stone():
    """Where to stand to bowl: a flat stepping stone on the island, a gold arrow on it pointing down the lane."""
    P = Part()
    st = R("stone", lo=-0.1, hi=0.06, tint=0.8)
    prism(P, ngon(0.3, 8, math.pi / 8), 0.0, 0.035, st, R("stone_dark", lo=-0.05, hi=0.05), st, "stone")
    g = R("glow_gold", v=0.6)
    prism(P, [(-0.1, -0.1), (0.1, -0.1), (0.0, 0.16)], 0.035, 0.045, g, g, g, "arrow")
    return P


def deck_outline():
    cut = WOOD_END - DECK_AT
    a0 = math.asin(cut / DECK_R)
    a1 = math.pi - a0
    steps = 22
    return [(DECK_R * math.cos(a0 + (a1 - a0) * i / steps), DECK_R * math.sin(a0 + (a1 - a0) * i / steps)) for i in range(steps + 1)]


def piece_deck():
    """The pin deck: a round stone stage (r 2 m), its top level with the lane (the pins stand on it), cut straight where
    the lane's wood ends, a lighter rim band, a little lip, a rocky underside."""
    P = Part()
    rng = random.Random(21)
    out = deck_outline()
    n = len(out)
    inner = [(x * 0.84, y * 0.84 + 0.0, 0.0) for x, y in out]
    top = R("stone", lo=-0.9, hi=0.1, tint=0.35)
    rim = R("stone", lo=-0.9, hi=0.1, tint=0.85, top=0.06)
    P.face(inner, top, "deck")
    ring0 = [(x, y, 0.0) for x, y in out]
    for i in range(n):
        j = (i + 1) % n
        P.face([inner[i], inner[j], ring0[j], ring0[i]], rim, "deck")

    def ring(s, z, jit=0.0):
        return [(x * s * (1 + rng.uniform(-jit, jit)), y * s * (1 + rng.uniform(-jit, jit)), z) for x, y in out]

    rings = [ring0, ring(1.03, -0.07), ring(1.0, -0.42), ring(0.82, -0.85, 0.06), ring(0.5, -1.35, 0.1), [(0.05, 0.0, -1.75)]]

    def body(k, j):
        if k <= 1:
            return R("stone", lo=-0.9, hi=0.1, tint=0.7)
        if k == 2:
            return R("stone_dark", lo=-1.0, hi=0.1)
        return R("rock" if hash01("deck", k, j) < 0.75 else "dirt", lo=-1.9, hi=-0.2, smooth=True)

    loft(P, rings, body, "deck")
    return P


def piece_rune_ring(colour):
    """The glowing ring round the rack (the game's Deck Ring: radius 1), 8 rune teeth on its outside."""
    P = Part()
    g = R("glow_" + colour, lo=-0.02, hi=0.03)
    lathe_loop(P, [(0.86, 0.0), (1.0, 0.0), (1.0, 0.012), (0.86, 0.012)], 24, g, "ring")
    for i in range(8):
        M = Matrix.Rotation(math.radians(22.5 + 45 * i), 4, 'Z')
        box(P, (-0.03, 1.0, 0.0), (0.03, 1.12, 0.012), g, "tooth%d" % i, M)
    return P


def piece_brazier():
    """An iron fire bowl on a column (1.18 m to the rim), glowing coals in it; the Flame sits on the coals."""
    P = Part()
    ir = R("iron", lo=-0.1, hi=1.3)
    prism(P, ngon(0.27, 8, math.pi / 8), 0.0, 0.08, ir, ir, ir, "foot")
    prism(P, ngon(0.085, 8, math.pi / 8), 0.08, 0.8, ir, ir, ir, "column")
    prism(P, ngon(0.15, 8, math.pi / 8), 0.72, 0.8, ir, ir, ir, "collar")
    lathe(P, [(0.0, 0.8), (0.14, 0.8), (0.36, 0.95), (0.43, 1.12), (0.43, 1.18), (0.36, 1.18), (0.33, 1.07), (0.0, 1.07)],
          8, ir, "bowl", phase=math.pi / 8)
    lathe(P, [(0.0, 1.072), (0.31, 1.072), (0.24, 1.13), (0.0, 1.16)], 8, R("glow_coal", lo=1.06, hi=1.17), "coals",
          phase=math.pi / 8)
    return P


def piece_flame():
    """A chunky toy flame (no particles): one tall twisted tongue and two small ones. Glows orange to yellow."""
    P = Part()
    rule = R("glow_flame", lo=0.0, hi=0.78, smooth=True)
    prof = [(0.0, 0.0), (0.13, 0.07), (0.17, 0.2), (0.13, 0.38), (0.07, 0.56), (0.0, 0.78)]
    lathe(P, prof, 6, rule, "main", twist=0.35)
    for sx in (-1, 1):
        M = Matrix.Translation((sx * 0.1, 0.03 * sx, 0.0)) @ Matrix.Rotation(math.radians(22 * sx), 4, 'Y') @ \
            Matrix.Diagonal((0.62, 0.62, 0.62, 1.0))
        lathe(P, prof, 6, rule, "side%d" % sx, M=M, twist=-0.35 * sx)
    return P


def piece_pumpkin_arch():
    """Behind a deck (never over the lane): two pillars of stacked pumpkins on stone plinths, an arch of pumpkins with a
    carved glowing keystone, a curtain of purple mist in the opening facing the island (-Y)."""
    P = Part()
    rng = random.Random(31)
    px = 1.25
    st = R("stone", lo=-0.5, hi=0.1, tint=0.6)
    for sx in (-1, 1):
        prism(P, ngon(0.44, 8, math.pi / 8), -0.45, 0.02, st, R("stone_dark", lo=-0.5, hi=0.1), st, "plinth%d" % sx,
              M=Matrix.Translation((sx * px, 0, 0)))
        z = 0.02
        for i, r in enumerate((0.44, 0.4, 0.36)):
            sq = 0.76
            z += r * sq * 0.92
            pumpkin(P, (sx * px + rng.uniform(-0.05, 0.05), rng.uniform(-0.04, 0.04), z), r, "p%d_%d" % (sx, i),
                    squash=sq, rot=rng.uniform(0, 6.28), rng=rng)
            z += r * sq * 0.92
    top = z
    arc_r = px
    for i, deg in enumerate((158, 128, 90, 52, 22)):
        a = math.radians(deg)
        r = 0.46 if deg == 90 else 0.36
        pumpkin(P, (arc_r * math.cos(a), rng.uniform(-0.03, 0.03), top - 0.18 + arc_r * math.sin(a) * 0.95), r, "a%d" % i,
                squash=0.8, rot=rng.uniform(0, 6.28), face=(deg == 90), rng=rng)
    # the mist curtain: the opening between the pillars, up into the arch (one face, facing the island)
    inner = px - 0.4
    pts = [(-inner, 0.02), (inner, 0.02)]
    for i in range(0, 9):
        a = math.radians(180.0 * i / 8)
        pts.append((inner * math.cos(a), top - 0.18 + (arc_r - 0.42) * math.sin(a)))
    pts.append((-inner, 0.02))
    curtain = [(x, 0.08, z) for x, z in pts[:-1]]
    curtain = [curtain[0], curtain[1]] + curtain[2:]
    fn = (Vector(curtain[1]) - Vector(curtain[0])).cross(Vector(curtain[2]) - Vector(curtain[0]))
    if fn.y > 0:
        curtain.reverse()
    P.face(curtain, R("glow_mist", lo=0.0, hi=top + 0.8, keep=True), "curtain")
    return P


def piece_slime_pin():
    """A green slime bowling pin (0.66 m: the game's young pin), a red neck stripe, a goofy face towards -Y."""
    P = Part()
    prof = [(0.0, 0.0), (0.085, 0.0), (0.108, 0.05), (0.122, 0.17), (0.112, 0.29), (0.078, 0.40), (0.058, 0.46),
            (0.06, 0.52), (0.074, 0.58), (0.068, 0.625), (0.04, 0.652), (0.0, 0.66)]

    def rule(k, j):
        if k == 6:
            return R("candy_red", lo=0.3, hi=0.6, smooth=True, tint=0.5)
        return R("slime", lo=-0.1, hi=0.7, smooth=True, tint=0.5)

    lathe(P, prof, 12, rule, "pin", cap0=R("slime", v=0.2))
    for sx in (-1, 1):
        ex = sx * 0.03
        M = Matrix.Translation((ex, -0.062, 0.582)) @ Matrix.Rotation(math.radians(90), 4, 'X') @ Matrix.Diagonal((0.022, 0.026, 0.012, 1))
        lathe(P, [(0.0, -1.0), (0.9, -0.6), (1.0, 0.0), (0.8, 0.6), (0.0, 1.0)], 8, R("white", v=0.85, smooth=True), "eye%d" % sx, M=M)
        Mp = Matrix.Translation((ex + sx * 0.004, -0.074, 0.578)) @ Matrix.Rotation(math.radians(90), 4, 'X') @ Matrix.Diagonal((0.009, 0.011, 0.006, 1))
        lathe(P, [(0.0, -1.0), (1.0, 0.0), (0.0, 1.0)], 6, R("black", v=0.5, smooth=True), "pupil%d" % sx, M=Mp)
    for i, (x0, x1, z) in enumerate(((-0.028, -0.008, 0.538), (-0.01, 0.01, 0.532), (0.008, 0.028, 0.538))):
        box(P, (x0, -0.074, z - 0.005), (x1, -0.058, z + 0.005), R("slime_dark", v=0.4), "mouth%d" % i)
    return P


def piece_vine():
    """A ball sprout: a curling vine from the dirt up to a leafy cup at hand height (the ball waits in it)."""
    P = Part()
    path = [(0.0, 0.0, -0.06), (0.07, 0.03, 0.16), (0.03, 0.07, 0.36), (-0.07, 0.03, 0.52), (-0.05, -0.04, 0.66),
            (0.0, 0.0, CUP_Z)]
    vr = R("vine", lo=-0.1, hi=1.0, smooth=True, tint=0.5)
    tube(P, path, [0.05, 0.046, 0.042, 0.038, 0.035, 0.03], 6, vr, "stem", cap0=vr, cap1=vr)
    lf = R("leaf", lo=CUP_Z - 0.1, hi=CUP_Z + 0.2, smooth=True)
    for i in range(3):   # the cup: three curled leaves round the vine's top
        a = math.radians(90 + 120 * i)
        d = Vector((math.cos(a), math.sin(a), 0.0))
        side = Vector((-d.y, d.x, 0.0))
        base = Vector((0, 0, CUP_Z - 0.02))
        mids = [base, base + d * 0.08 + Vector((0, 0, 0.03)), base + d * 0.15 + Vector((0, 0, 0.1)), base + d * 0.16 + Vector((0, 0, 0.18))]
        widths = [0.02, 0.07, 0.06, 0.0]
        rings = []
        for m, w in zip(mids, widths):
            if w <= 0:
                rings.append([tuple(m)])
                continue
            t = 0.008
            rings.append([tuple(m + side * w), tuple(m + d * t), tuple(m - side * w), tuple(m - d * t)])
        loft(P, rings, lf, "cup%d" % i, cap0=lf)
    for i, (k, a) in enumerate(((2, 30), (4, 200))):   # two leaves on the stem
        p = Vector(path[k])
        d = Vector((math.cos(math.radians(a)), math.sin(math.radians(a)), 0.25)).normalized()
        side = Vector((-d.y, d.x, 0.0)).normalized()
        mids = [p, p + d * 0.08, p + d * 0.16 + Vector((0, 0, 0.02)), p + d * 0.22 + Vector((0, 0, -0.01))]
        rings = []
        for m, w in zip(mids, [0.015, 0.05, 0.04, 0.0]):
            if w <= 0:
                rings.append([tuple(m)])
                continue
            rings.append([tuple(m + side * w), tuple(m + Vector((0, 0, 0.007))), tuple(m - side * w), tuple(m - Vector((0, 0, 0.007)))])
        loft(P, rings, R("leaf", lo=0.0, hi=0.9, smooth=True), "leaf%d" % i, cap0=R("leaf", v=0.5))
    curl = []   # a little tendril
    for i in range(10):
        a = i * 0.75
        r = 0.06 * (1 - i / 12)
        curl.append((0.05 + r * math.cos(a), 0.06 + r * math.sin(a), 0.44 + i * 0.012))
    tube(P, curl, [0.008] * 9 + [0.0], 4, R("vine", v=0.7, smooth=True), "tendril", cap0=R("vine", v=0.7))
    return P


def piece_lantern():
    """A hanging lantern (pivot at its hook): iron cap and frame, a glowing body."""
    P = Part()
    ir = R("iron", lo=-0.5, hi=0.0)
    frame_ring(P, 0.07, 0.09, 0.018, 0.018, ir, "hook", Matrix.Translation((0, 0, -0.045)))
    lathe(P, [(0.025, -0.09), (0.15, -0.17), (0.15, -0.19), (0.0, -0.19)], 4, ir, "cap", phase=math.pi / 4, cap0=ir)
    lathe(P, [(0.0, -0.19), (0.105, -0.19), (0.105, -0.42), (0.0, -0.42)], 4, R("glow_lantern", lo=-0.45, hi=-0.15),
          "glass", phase=math.pi / 4)
    for i in range(4):
        a = math.pi / 4 + i * math.pi / 2
        x, y = 0.105 * math.sqrt(2) * math.cos(a) * 0.72, 0.105 * math.sqrt(2) * math.sin(a) * 0.72
        box(P, (x - 0.015, y - 0.015, -0.42), (x + 0.015, y + 0.015, -0.19), ir, "bar%d" % i)
    prism(P, ngon(0.14, 4, math.pi / 4), -0.46, -0.42, ir, ir, ir, "base")
    return P


def piece_lantern_post():
    """A crooked wooden post (1.85 m) with an arm; a Lantern hangs from the arm's end (0.42, 0, 1.8)."""
    P = Part()
    wd = R("wood_dark", lo=-0.1, hi=2.0)
    tube(P, [(0, 0, -0.05), (0.03, 0.02, 0.7), (-0.03, 0.03, 1.35), (0.0, 0.0, 1.9)], [0.07, 0.065, 0.06, 0.055], 4, wd,
         "post", cap0=wd, cap1=wd, phase=math.pi / 4)
    tube(P, [(-0.02, 0.0, 1.78), (0.2, 0.0, 1.86), (0.46, 0.0, 1.86)], [0.04, 0.035, 0.03], 4, wd, "arm", cap0=wd, cap1=wd,
         phase=math.pi / 4)
    rock(P, (0.0, 0.0, 0.0), (0.22, 0.2, 0.08), random.Random(8), R("rock", lo=-0.1, hi=0.1, smooth=True), "foot")
    return P


def piece_floating_rock():
    """A floating rock (about 1.6 x 1.4 m), moss on top."""
    P = Part()
    rock(P, (0, 0, 0), (0.8, 0.7, 0.62), random.Random(41), R("rock", lo=-0.7, hi=0.4, smooth=True, up="moss"), "rock",
         segs=8, flat_top=True)
    return P


def piece_pumpkin_small():
    """A loose decoration pumpkin (0.5 m), for the island and the decks."""
    P = Part()
    pumpkin(P, (0, 0, 0.19), 0.25, "pumpkin", squash=0.76, rng=random.Random(2))
    return P


def piece_boss_pumpkin():
    """Gate 2's giant pumpkin (the Pumpkin Ball Boss variant's model): a bowling-ball pumpkin, radius 1 m here (the
    game grows it to its radius), ribbed, three finger holes glowing inside at the top front, a carved grin under them,
    its stem on top. Its front (face) is -Y (Unity +Z after the export, where the boss looks)."""
    P = Part()
    ribs = 12
    prof = []
    for phi in (-90, -70, -48, -24, 0, 24, 48, 70, 90):
        a = math.radians(phi)
        if abs(phi) == 90:
            prof.append((0.0, 0.94 * math.sin(a)))
        else:
            prof.append((math.cos(a), 0.96 * math.sin(a)))
    lathe(P, prof, ribs * 2, R("pumpkin", lo=-1.1, hi=1.0, smooth=True), "ball",
          radial=lambda k, j: 1.0 if j % 2 == 0 else 0.9)
    # Finger holes: two side by side and the thumb hole below, on the upper front: they are its eyes and nose (a dark
    # disc, glowing inside).
    for i, (x, z, r) in enumerate(((-0.24, 0.5, 0.12), (0.24, 0.5, 0.12), (0.0, 0.22, 0.11))):
        y = -math.sqrt(max(0.0, 0.94 - x * x - z * z))
        n = Vector((x, y, z)).normalized()
        M = Matrix.Translation(n * 0.96) @ n.to_track_quat("Z", "Y").to_matrix().to_4x4()
        lathe(P, [(0.0, -0.06), (r, -0.06), (r, 0.03), (0.0, 0.03)], 10, R("black", v=0.5), "hole%d" % i, M=M)
        lathe(P, [(0.0, 0.0), (r * 0.6, 0.0), (r * 0.6, 0.042), (0.0, 0.042)], 10, R("glow_coal", v=0.75), "glow%d" % i, M=M)
    # The grin: three glowing pieces in an arc under the holes.
    g = R("glow_lantern", lo=-0.6, hi=0.2)
    for i, (x0, x1, dz) in enumerate(((-0.46, -0.18, 0.06), (-0.15, 0.15, -0.02), (0.18, 0.46, 0.06))):
        zc = -0.22 + dz
        y = -math.sqrt(max(0.0, 0.9 - ((x0 + x1) / 2) ** 2 - zc * zc))
        box(P, (x0, y - 0.06, zc - 0.07), (x1, y + 0.14, zc + 0.07), g, "grin%d" % i)
    tube(P, [(0, 0, 0.85), (0.04, 0.02, 1.1), (0.16, 0.0, 1.24)], [0.12, 0.09, 0.06], 5, R("stem", lo=0.8, hi=1.3), "stem",
         cap0=R("stem", v=0.3), cap1=R("stem", v=0.8))
    return P


def piece_cloud_puff():
    """A soft purple cloud (about 6 x 3 m) to fill the void under the arena; a faint glow so it reads at night."""
    P = Part()
    blobs = [((0, 0, 0), 1.4), ((1.3, 0.2, -0.2), 1.1), ((-1.3, -0.1, -0.25), 1.0), ((0.5, 0.9, -0.1), 0.9),
             ((-0.6, -0.85, -0.2), 0.95), ((2.3, 0.0, -0.45), 0.7), ((-2.25, 0.3, -0.5), 0.65)]
    for i, (c, r) in enumerate(blobs):
        M = Matrix.Translation(c) @ Matrix.Diagonal((r, r, r * 0.62, 1.0))
        lathe(P, [(0.0, -0.8), (0.7, -0.6), (1.0, -0.1), (0.85, 0.45), (0.45, 0.85), (0.0, 1.0)], 9,
              R("cloud", lo=-1.2, hi=1.0, smooth=True, tint=0.5), "blob%d" % i, M=M)
    return P


# --------------------------------------------------------------------------------------------------- sample-only
def sample_ball():
    P = Part()
    pumpkin(P, (0, 0, 0), 0.16, "ball", squash=0.92, ribs=8, face=True, stem=True, rng=random.Random(4))
    return P


def sample_slime():
    """A stand-in for the game's Giant Flying Slime (Flying_Slime.fbx, not a kit piece): a fat green blob, bat wings."""
    P = Part()
    lathe(P, [(0.0, -0.9), (0.9, -0.75), (1.35, -0.2), (1.2, 0.45), (0.7, 0.95), (0.0, 1.1)], 14,
          R("slime", lo=-1.0, hi=1.2, smooth=True, tint=0.5), "body")
    for sx in (-1, 1):
        M = Matrix.Translation((sx * 0.42, -1.12, 0.35)) @ Matrix.Rotation(math.radians(90), 4, 'X') @ Matrix.Diagonal((0.2, 0.24, 0.1, 1))
        lathe(P, [(0.0, -1.0), (0.9, -0.6), (1.0, 0.0), (0.8, 0.6), (0.0, 1.0)], 8, R("white", v=0.85, smooth=True), "eye%d" % sx, M=M)
        Mp = Matrix.Translation((sx * 0.4, -1.2, 0.33)) @ Matrix.Rotation(math.radians(90), 4, 'X') @ Matrix.Diagonal((0.09, 0.1, 0.05, 1))
        lathe(P, [(0.0, -1.0), (1.0, 0.0), (0.0, 1.0)], 6, R("black", v=0.5, smooth=True), "pupil%d" % sx, M=Mp)
        wing = [(0.0, 0.0), (1.1, 1.1), (1.9, 0.45), (1.6, -0.3), (1.3, 0.1), (1.0, -0.65), (0.7, -0.1), (0.35, -0.6)]
        Mw = Matrix.Translation((sx * 1.05, 0.2, 0.55)) @ Matrix.Rotation(math.radians(-35 * sx), 4, 'Y') @ Matrix.Diagonal((sx, 1, 1, 1))
        rings = [[T(Mw, (x, y, -0.03)) for x, y in wing], [T(Mw, (x, y, 0.03)) for x, y in wing]]
        loft(P, rings, R("black", v=0.6), "wing%d" % sx, R("black", v=0.6), R("black", v=0.6))
    tube(P, [(0, 0, 1.05), (0.05, 0.02, 1.3), (0.18, 0.0, 1.42)], [0.12, 0.09, 0.06], 5, R("stem", lo=1.0, hi=1.5), "stem",
         cap0=R("stem", v=0.3), cap1=R("stem", v=0.8))
    return P


def sample_moon():
    P = Part()
    prof = [(math.cos(math.radians(a)) if abs(a) < 90 else 0.0, math.sin(math.radians(a))) for a in range(-90, 91, 15)]
    lathe(P, prof, 32, R("glow_lantern", v=0.42, smooth=True), "moon")
    return P


# ---------------------------------------------------------------------------------------------------------- build it
paths = make_palette(TEXDIR)
MAT = palette_material(paths)

kit = bpy.data.collections.new("SlimeBowl_Kit")
sc.collection.children.link(kit)
PIECE_MAKERS = [
    ("Island", piece_island), ("Approach_Stone", piece_approach_stone), ("Vine", piece_vine),
    ("Lantern_Post", piece_lantern_post), ("Pumpkin_Small", piece_pumpkin_small),
    ("Lane_End", piece_lane_end), ("Lane_4m", piece_lane_4m), ("Lane_Arrows", piece_lane_arrows),
    ("Rail_4m", piece_rail_4m), ("Trestle", piece_trestle),
    ("Deck", piece_deck), ("Rune_Ring_Green", lambda: piece_rune_ring("green")), ("Rune_Ring_Red", lambda: piece_rune_ring("red")),
    ("Rune_Ring_Blue", lambda: piece_rune_ring("blue")), ("Rune_Ring_Yellow", lambda: piece_rune_ring("yellow")),
    ("Brazier", piece_brazier), ("Flame", piece_flame), ("Pumpkin_Arch", piece_pumpkin_arch), ("Slime_Pin", piece_slime_pin),
    ("Lantern", piece_lantern), ("Chain_Rock", piece_chain_rock), ("Floating_Rock", piece_floating_rock),
    ("Cloud_Puff", piece_cloud_puff), ("Boss_Pumpkin", piece_boss_pumpkin),
]
for short, maker in PIECE_MAKERS:
    name = "SlimeBowl_Kit_" + short
    coll = bpy.data.collections.new(name)
    kit.children.link(coll)
    PIECES[short] = build(maker(), name, coll)

stats = {}
for short, ob in PIECES.items():
    me = ob.data
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    xs = [v.co.x for v in me.vertices]
    ys = [v.co.y for v in me.vertices]
    zs = [v.co.z for v in me.vertices]
    stats[short] = dict(tris=tris, quads=sum(1 for p in me.polygons if len(p.vertices) == 4), faces=len(me.polygons),
                        size=(max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs)), zmin=min(zs), zmax=max(zs),
                        cx=(max(xs) + min(xs)) / 2, cy=(max(ys) + min(ys)) / 2)
    s = stats[short]
    print("%-18s tris %5d  faces %4d (quads %4d)  size %.2f x %.2f x %.2f m" % (short, tris, s["faces"], s["quads"], *s["size"]))

# The catalogue: every piece at its pivot, in a row along X at y = -45.
x = 0.0
for short, ob in PIECES.items():
    s = stats[short]
    x += s["size"][0] / 2 + 1.5
    ob.location = (x - s["cx"], -45.0, 0.0)
    x += s["size"][0] / 2 + 1.5

# ------------------------------------------------------------------------------------------------- the sample arena
arena = bpy.data.collections.new("Sample_Arena")
sc.collection.children.link(arena)
samples = bpy.data.collections.new("Sample_Standins")
arena.children.link(samples)
SAMPLE_PARTS = {"Sample_Ball": build(sample_ball(), "Sample_Ball", samples),
                "Sample_Slime": build(sample_slime(), "Sample_Slime", samples),
                "Sample_Moon": build(sample_moon(), "Sample_Moon", samples)}
for ob in SAMPLE_PARTS.values():
    ob.hide_set(True)
    ob.hide_render = True
A = Matrix.Rotation(math.radians(45), 4, 'Z')   # the game's X: lanes on the diagonals


def lane_frame(k):
    return A @ Matrix.Rotation(math.radians(90 * k), 4, 'Z')


def at(k, along, side=0.0, z=0.0, rot=0.0):
    return lane_frame(k) @ Matrix.Translation((side, along, z)) @ Matrix.Rotation(rot, 4, 'Z')


def place(short, M, name=None, scale=1.0):
    src = PIECES[short] if short in PIECES else SAMPLE_PARTS[short]
    ob = bpy.data.objects.new(name or ("Arena_" + short), src.data)
    arena.objects.link(ob)
    ob.matrix_world = M @ Matrix.Diagonal((scale, scale, scale, 1.0))
    ob.hide_render = False
    return ob


rng = random.Random(99)
RING_OF = ["Green", "Red", "Blue", "Yellow"]
fire_points = []
lantern_points = []
for k in range(4):
    place("Lane_End", at(k, LANE_START))
    for i in range(4):
        place("Lane_4m", at(k, LANE_START + END_LEN + SEG_LEN * i))
    place("Lane_Arrows", at(k, 7.0))
    for i in range(5):
        for side in (-RAIL_SIDE, RAIL_SIDE):
            place("Rail_4m", at(k, FOUL_LINE + SEG_LEN * i, side))
    for along in (5.6, 11.6, 17.6):
        place("Trestle", at(k, along, 0.0, -LANE_T))
    place("Deck", at(k, DECK_AT))
    place("Rune_Ring_" + RING_OF[k], at(k, DECK_AT))
    for side in (-FIRE_SIDE, FIRE_SIDE):
        place("Brazier", at(k, DECK_AT, side))
        place("Flame", at(k, DECK_AT, side, 1.1, rot=rng.uniform(0, 6.28)))
        fire_points.append(at(k, DECK_AT, side, 1.5).to_translation())
    place("Pumpkin_Arch", at(k, ARCH_AT))
    for sx, sy in RACK:
        place("Slime_Pin", at(k, sy, sx, 0.0, rot=rng.uniform(-0.3, 0.3)))
    place("Approach_Stone", at(k, 1.25, 0.0, DIRT_TOP))
    for along, side, drop in ((9.6, 1.0, 0.0), (13.6, -1.0, 0.0)):
        side = side if k % 2 == 0 else -side
        place("Chain_Rock", at(k, along, side, -0.28))
    place("Pumpkin_Small", at(k, DECK_AT + 1.25, -1.0 if k % 2 else 1.0, 0.0, rot=rng.uniform(0, 6.28)))

place("Island", A)
for i, yaw in enumerate(SPROUT_YAWS):
    t = math.radians(yaw)
    p = Vector((SPROUT_R * math.sin(t), SPROUT_R * math.cos(t), DIRT_TOP))
    M = A @ Matrix.Translation(p) @ Matrix.Rotation(-t, 4, 'Z')
    place("Vine", M)
    if i < 6:
        place("Sample_Ball", M @ Matrix.Translation((0, 0, CUP_Z + 0.13)) @ Matrix.Rotation(math.radians(180), 4, 'Z'))
for sx, sy in ((2.02, 2.02), (-2.02, -2.02)):
    place("Lantern_Post", A @ Matrix.Translation((sx, sy, DIRT_TOP)) @ Matrix.Rotation(math.atan2(sy, sx) + math.pi, 4, 'Z'))
    lantern_points.append(A @ Matrix.Translation((sx, sy, DIRT_TOP)) @ Matrix.Rotation(math.atan2(sy, sx) + math.pi, 4, 'Z'))
for M in list(lantern_points):
    place("Lantern", M @ Matrix.Translation((0.44, 0.0, 1.84)))
lantern_points = [(M @ Matrix.Translation((0.44, 0.0, 1.5))).to_translation() for M in lantern_points]
for p, r in (((1.6, -1.7, 0.02), 0.0), ((-1.75, 1.5, 0.02), 1.0), ((-1.9, -1.2, 0.02), 2.0)):
    place("Pumpkin_Small", A @ Matrix.Translation(p) @ Matrix.Rotation(r, 4, 'Z'))
for p in ((1.1, -0.9, -1.2), (-1.2, 1.1, -1.4), (0.3, 1.3, -1.6)):
    place("Chain_Rock", A @ Matrix.Translation(p))
# floating rocks in the sky between the lanes, some with a lantern under them
for i, (q, r, z, s) in enumerate(((0, 9.0, 3.5, 1.0), (1, 13.0, 1.5, 1.3), (2, 8.0, -1.0, 0.9), (3, 15.0, 4.5, 1.1),
                                  (0, 17.0, -2.5, 1.4), (2, 18.0, 5.0, 0.8))):
    a = math.radians(45 + 90 * q + rng.uniform(-12, 12))
    pos = Vector((r * math.cos(a), r * math.sin(a), z))
    place("Floating_Rock", Matrix.Translation(pos) @ Matrix.Rotation(rng.uniform(0, 6.28), 4, 'Z'), scale=s)
    if i % 2 == 0:
        place("Lantern", Matrix.Translation(pos + Vector((0.2, 0.0, -0.35 * s))))
        lantern_points.append(pos + Vector((0.2, 0.0, -0.35 * s - 0.3)))
# clouds below
for i in range(18):
    a = rng.uniform(0, 6.283)
    r = rng.uniform(9, 42)
    place("Cloud_Puff", Matrix.Translation((r * math.cos(a), r * math.sin(a), rng.uniform(-24, -14))) @
          Matrix.Rotation(rng.uniform(0, 6.28), 4, 'Z'), scale=rng.uniform(2.0, 3.6))
# the giant slime over lane 3, the moon behind
place("Sample_Slime", at(3, 13.0, 0.0, 6.5, rot=math.radians(180)) @ Matrix.Rotation(math.radians(10), 4, 'X'), scale=1.6)
MOON = place("Sample_Moon", Matrix.Translation((0.0, 95.0, 20.0)), scale=12.0)

# lights: moonlight, a warm light over the island (the game's Dirt Light), a point at every fire and lantern
def light(name, kind, energy, colour, M=None, loc=None, radius=0.1, coll=arena):
    ld = bpy.data.lights.new(name, kind)
    ld.energy = energy
    ld.color = colour
    if kind in ('POINT', 'SPOT'):
        ld.shadow_soft_size = radius
    ob = bpy.data.objects.new(name, ld)
    coll.objects.link(ob)
    if M is not None:
        ob.matrix_world = M
    if loc is not None:
        ob.location = loc
    return ob


moon = light("Moonlight", 'SUN', 2.8, (0.62, 0.68, 1.0))
fill = light("Warm_Fill", 'SUN', 0.9, (1.0, 0.62, 0.4))
fill.rotation_euler = (math.radians(62), 0.0, math.radians(-25))
moon.rotation_euler = (math.radians(-52), 0.0, math.radians(8))
moon.data.angle = math.radians(3)
spot = light("Island_Light", 'SPOT', 1300, (1.0, 0.78, 0.5), loc=(0, 0, 7.0), radius=0.4)
spot.data.spot_size = math.radians(70)
spot.data.spot_blend = 0.6
for i, p in enumerate(fire_points):
    light("Fire_%d" % i, 'POINT', 420, (1.0, 0.55, 0.22), loc=p, radius=0.25)
for i, p in enumerate(lantern_points):
    light("Lantern_%d" % i, 'POINT', 45, (1.0, 0.7, 0.35), loc=p, radius=0.1)
for k in range(4):   # the game's deck spot over each deck
    s = light("Deck_Light_%d" % k, 'SPOT', 700, (1.0, 0.68, 0.4), M=at(k, DECK_AT, 0.0, 6.5) @ Matrix.Rotation(0, 4, 'X'), radius=0.3)
    s.data.spot_size = math.radians(50)
    s.data.spot_blend = 0.5

# ------------------------------------------------------------------------------------------------------ the kit sheet
sheet = bpy.data.collections.new("Kit_Sheet")
sc.collection.children.link(sheet)
label_mat = bpy.data.materials.new("Sheet_Label")
label_mat.use_nodes = True
lb = next(n for n in label_mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
lb.inputs["Base Color"].default_value = (0.9, 0.88, 0.8, 1)
lb.inputs["Emission Color"].default_value = (0.9, 0.88, 0.8, 1)
lb.inputs["Emission Strength"].default_value = 1.0
ground_mat = bpy.data.materials.new("Sheet_Ground")
ground_mat.use_nodes = True
gb = next(n for n in ground_mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
gb.inputs["Base Color"].default_value = (0.045, 0.045, 0.05, 1)
gb.inputs["Roughness"].default_value = 0.9

COLS, CELL_X, CELL_Y = 5, 9.5, 12.0
SHEET_ORIGIN = Vector((200.0, 0.0, 0.0))
SHEET_PITCH = math.radians(52)          # the sheet camera's tilt from straight down
cam_rot = (SHEET_PITCH, 0.0, 0.0)
rows = (len(PIECE_MAKERS) + COLS - 1) // COLS
for i, (short, _) in enumerate(PIECE_MAKERS):
    s = stats[short]
    col, row = i % COLS, i // COLS
    cx = SHEET_ORIGIN.x + (col - (COLS - 1) / 2) * CELL_X
    cy = SHEET_ORIGIN.y + ((rows - 1) / 2 - row) * CELL_Y
    biggest = max(s["size"])
    k = min(5.0, max(0.75, 4.2 / max(0.01, biggest)))
    ob = bpy.data.objects.new("Sheet_" + short, PIECES[short].data)
    sheet.objects.link(ob)
    yaw = math.radians(-28)
    ob.matrix_world = Matrix.Translation((cx, cy, -s["zmin"] * k)) @ Matrix.Rotation(yaw, 4, 'Z') @ \
        Matrix.Diagonal((k, k, k, 1.0)) @ Matrix.Translation((-s["cx"], -s["cy"], 0.0))
    w, d, h = s["size"]
    txt = bpy.data.curves.new("Label_" + short, 'FONT')
    txt.body = "%s\n%.2f x %.2f x %.2f m\n%d tris%s" % (short, w, d, h, s["tris"], "" if abs(k - 1) < 0.05 else ", shown x%.1f" % k)
    txt.align_x = 'CENTER'
    txt.size = 0.5
    to = bpy.data.objects.new("Label_" + short, txt)
    sheet.objects.link(to)
    to.data.materials.append(label_mat)
    to.location = (cx, cy - 4.1, 1.35)
    to.rotation_euler = cam_rot
    to.visible_shadow = False
gp = bpy.data.meshes.new("Sheet_Ground")
gbm = bmesh.new()
half = 60.0
vs = [gbm.verts.new((SHEET_ORIGIN.x + x * half, SHEET_ORIGIN.y + y * half, 0.0)) for x, y in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
gbm.faces.new(vs)
gbm.to_mesh(gp)
gbm.free()
gp.materials.append(ground_mat)
gob = bpy.data.objects.new("Sheet_Ground", gp)
sheet.objects.link(gob)
sheet_sun = light("Sheet_Sun", 'SUN', 3.2, (1.0, 0.96, 0.9), coll=sheet)
sheet_sun.rotation_euler = (math.radians(22), math.radians(-12), math.radians(-35))
sheet_fill = light("Sheet_Fill", 'SUN', 1.0, (0.7, 0.75, 1.0), coll=sheet)
sheet_fill.rotation_euler = (math.radians(65), math.radians(20), math.radians(150))

# --------------------------------------------------------------------------------------------------------- world
world = bpy.data.worlds.new("SlimeBowl_Night")
world.use_nodes = True
wt = world.node_tree
bg = next(n for n in wt.nodes if n.type == "BACKGROUND")
tco = wt.nodes.new("ShaderNodeTexCoord")
sep = wt.nodes.new("ShaderNodeSeparateXYZ")
half = wt.nodes.new("ShaderNodeMath")          # the view direction's z (-1 down .. 1 up) -> 0 .. 1
half.operation = 'MULTIPLY_ADD'
half.inputs[1].default_value = 0.5
half.inputs[2].default_value = 0.5
ramp = wt.nodes.new("ShaderNodeValToRGB")
wt.links.new(tco.outputs["Generated"], sep.inputs[0])
wt.links.new(sep.outputs["Z"], half.inputs[0])
wt.links.new(half.outputs[0], ramp.inputs["Fac"])
wt.links.new(ramp.outputs["Color"], bg.inputs["Color"])
cr = ramp.color_ramp
cr.elements[0].position = 0.0
cr.elements[0].color = (0.03, 0.015, 0.06, 1)
cr.elements[1].position = 1.0
cr.elements[1].color = (0.025, 0.03, 0.11, 1)
e = cr.elements.new(0.47)
e.color = (0.12, 0.05, 0.14, 1)
e = cr.elements.new(0.53)
e.color = (0.42, 0.17, 0.12, 1)
e = cr.elements.new(0.62)
e.color = (0.09, 0.06, 0.2, 1)
bg.inputs["Strength"].default_value = 1.0
sc.world = world

# ----------------------------------------------------------------------------------------------------- cameras
def camera(name, loc, target, lens=35.0, ortho=None):
    cd = bpy.data.cameras.new(name)
    cd.lens = lens
    cd.clip_end = 600.0
    if ortho:
        cd.type = 'ORTHO'
        cd.ortho_scale = ortho
    ob = bpy.data.objects.new(name, cd)
    sc.collection.objects.link(ob)
    ob.location = loc
    ob.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    return ob


cam_concept = camera("Cam_Concept_Angle", (0.0, -58.0, 47.0), (0.0, 2.5, -2.5), lens=44.0)
eye = lane_frame(0) @ Matrix.Translation((0.0, -0.6, 1.6))
look = lane_frame(0) @ Matrix.Translation((0.0, DECK_AT, 0.9))
cam_eye = camera("Cam_Eye_Level", eye.to_translation(), look.to_translation(), lens=15.0)
sheet_h = rows * CELL_Y
cam_sheet_loc = SHEET_ORIGIN + Vector((0.0, -math.sin(SHEET_PITCH) * 80.0 - 2.0, math.cos(SHEET_PITCH) * 80.0))
cam_sheet = camera("Cam_Kit_Sheet", cam_sheet_loc, SHEET_ORIGIN + Vector((0, -2.0, 0)), ortho=COLS * CELL_X + 3.0)
cam_sheet.rotation_euler = cam_rot
# Fit the sheet camera to what's on the sheet (pieces and labels).
bpy.context.view_layer.update()
right = Vector((1.0, 0.0, 0.0))
up = Vector((0.0, math.cos(SHEET_PITCH), math.sin(SHEET_PITCH)))
fwd = Vector((0.0, math.sin(SHEET_PITCH), -math.cos(SHEET_PITCH)))
us, vs = [], []
for ob in sheet.objects:
    if ob.type not in ('MESH', 'FONT') or ob.name == "Sheet_Ground":
        continue
    for c in ob.bound_box:
        wpt = ob.matrix_world @ Vector(c)
        us.append(wpt.dot(right))
        vs.append(wpt.dot(up))
SHEET_W, SHEET_H = 2200, 1800
cam_sheet.location = right * ((min(us) + max(us)) / 2) + up * ((min(vs) + max(vs)) / 2) - fwd * 150.0
cam_sheet.data.ortho_scale = max(max(us) - min(us), (max(vs) - min(vs)) * SHEET_W / SHEET_H) * 1.04
sc.camera = cam_concept

# ------------------------------------------------------------------------------------------------------- save
os.makedirs(OUTDIR, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=OUT, relative_remap=True)
bpy.ops.file.make_paths_relative()
bpy.ops.wm.save_mainfile()
print("saved", OUT)

# ------------------------------------------------------------------------------------------------------ renders
if RENDER:
    for engine in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT", "BLENDER_WORKBENCH"):
        try:
            sc.render.engine = engine
            break
        except TypeError:
            continue
    print("engine", sc.render.engine)
    try:
        sc.eevee.taa_render_samples = 48
    except Exception:
        pass
    try:
        sc.view_settings.view_transform = 'AgX'
        sc.view_settings.look = 'AgX - Punchy'
    except Exception:
        pass

    def show(only):
        for c in (kit, arena, sheet):
            c.hide_render = c not in only
        for ob in SAMPLE_PARTS.values():
            ob.hide_render = True

    def shot(cam, name, w, h, only, world_strength=1.0, flat_world=None):
        show(only)
        sc.camera = cam
        sc.render.resolution_x, sc.render.resolution_y = w, h
        sc.render.resolution_percentage = 100
        if flat_world is not None:
            for l in list(wt.links):
                if l.to_node == bg and l.to_socket.name == "Color":
                    wt.links.remove(l)
            bg.inputs["Color"].default_value = flat_world
        else:
            wt.links.new(ramp.outputs["Color"], bg.inputs["Color"])
        bg.inputs["Strength"].default_value = world_strength
        sc.render.filepath = os.path.join(OUTDIR, name + SUFFIX + ".png")
        bpy.ops.render.render(write_still=True)
        print("render", sc.render.filepath)

    shot(cam_concept, "arena_concept_angle", 1920, 1080, (arena,))
    MOON.hide_render = True
    shot(cam_eye, "arena_eye_level", 1920, 1080, (arena,))
    MOON.hide_render = False
    shot(cam_sheet, "kit_sheet", SHEET_W, SHEET_H, (sheet,), world_strength=1.0, flat_world=(0.05, 0.05, 0.056, 1))
    show((kit, arena, sheet))
    sc.camera = cam_concept
print("done")
