"""PlayGround kit (gate 1, "cook and defend"), 2026-11-23.

The user: "For each gate create a kit for easy change and fix." The look is the approved concept
(Exports/PlayGround_Concept/concept_v1.png); the pieces are Prompt 3 of Exports/PlayGround_Concept/PROMPTS.md plus
BUILD_LIST.md. Every piece is chunky low poly in the user's clean style: closed parts, all quads, no bevels, real
metres from the game (today's PlayGround field inside its walls is about 15.2 x 16.6 m, the old trees 3.7-5.7 m tall,
the cauldron about 1.9 m across with its stones).

What it makes
- One collection per piece under "PlayGround_Kit": the piece is PlayGround_Kit_<Piece>, its pivot (object origin) on
  its snap point: the middle of its foot on the floor (a fence piece: the foot of its left post, so pieces chain along
  +X). Its front faces -Y. Breakables also get PlayGround_Kit_<Piece>_Broken: 3-5 chunks, each its own object.
- One palette texture for the whole kit and one material, PlayGround_Kit_Palette:
  textures/PlayGround_Palette.png (colour; alpha = gloss) and PlayGround_Palette_Emission.png (only the glowing
  colours). Every face sits on its colour's column, dark low and light high, so nothing needs unwrapping (the
  Exports/Remodel/tools/palette_kit.py method). To recolour a part: move its faces' UVs to another column, or paint the
  column.
- "Sample_Arena" (never exported): the pieces placed like the concept around a stand-in cauldron, the field of today
  (the walls' line) marked by fence pieces at the front, the sky, the moon and the lights, to compare with the concept.
- Renders in the renders folder: kit_sheet.png (every piece with its name), kit_sheet_small.png (the small breakables
  and toys), arena_concept_angle.png (the concept's high three-quarter view), arena_eye_level.png (the player's eyes).
- kit_report.txt next to the renders: each piece's size, faces, triangles and checks (all quads, closed).

Run (headless, from the repo):
  blender -b --factory-startup --python Exports/Gates/gate1/tools/playground_kit.py --
      Exports/Gates/gate1/PlayGround_Kit.blend Exports/Gates/gate1/kit_renders [--force] [--no-render]
An existing PlayGround_Kit.blend is never overwritten without --force (once the user edits it, it's theirs): the new
one is saved next to it as PlayGround_Kit_generated.blend.
"""
import bpy, bmesh, sys, os, math, random
import numpy as np
from mathutils import Vector, Matrix, Euler

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
POS = [a for a in ARGS if not a.startswith("--")]
OUT = os.path.abspath(POS[0]) if POS else None
RENDERS = os.path.abspath(POS[1]) if len(POS) > 1 else None
FORCE = "--force" in ARGS
NO_RENDER = "--no-render" in ARGS
HERE = os.path.dirname(os.path.abspath(__file__))
GATE_DIR = os.path.dirname(HERE)
TEX_DIR = os.path.join(GATE_DIR, "textures")
KIT = "PlayGround"
PREFIX = KIT + "_Kit_"

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene

# ----------------------------------------------------------------------------------------------------------- palette
# name, dark (bottom of the column), light (top), glows, gloss (0 matte .. 1 shiny; stored in the colour's alpha)
SWATCHES = [
    ("licorice",        "1c1024", "5a2c64", False, 0.35),   # plum trunks
    ("licorice_stripe", "46287a", "a272dc", False, 0.35),   # the trunks' spiral stripes
    ("jelly_pink",      "a24c6c", "ffc2dc", False, 0.60),
    ("jelly_aqua",      "2c8a8a", "aaf6ec", False, 0.60),
    ("jelly_lilac",     "66529e", "dccaff", False, 0.60),
    ("jelly_teal",      "12565c", "4ec6b8", False, 0.60),
    ("moss",            "142c2a", "3c6c50", False, 0.08),   # the teal moss ground
    ("stone",           "3a3446", "9e96ae", False, 0.12),   # plum-grey stone
    ("stone_dark",      "221e2a", "66607a", False, 0.12),
    ("wood",            "3c2214", "925e38", False, 0.18),
    ("wood_light",      "684024", "d29c66", False, 0.22),
    ("bone",            "867e6c", "f2ead8", False, 0.30),
    ("pumpkin",         "963406", "ff9c36", False, 0.35),
    ("stem",            "283e16", "6a8c34", False, 0.20),
    ("slime",           "1c7810", "aaff5c", True,  0.80),   # glows
    ("lantern",         "ff861c", "ffeaa2", True,  0.40),   # glows: jack-o'-lantern faces, flames
    ("mist",            "36126a", "b272ff", True,  0.40),   # glows: the monster gates' mist
    ("crystal_pink",    "b83a7c", "ff88cc", True,  0.70),   # glows
    ("crystal_aqua",    "1a8ca0", "70f2ff", True,  0.70),   # glows
    ("crystal_lilac",   "6a3cc8", "c69cff", True,  0.70),   # glows
    ("candy_pink",      "ae285a", "ff8cba", False, 0.55),
    ("candy_white",     "c6bed0", "fff8ff", False, 0.55),
    ("iron",            "1c181e", "56484a", False, 0.40),
    ("tin",             "586878", "cadaea", False, 0.55),
    ("sand",            "98784e", "f2dca2", False, 0.08),
    ("clay",            "682c18", "cc7a4a", False, 0.20),
    ("groove",          "0c0910", "221c2a", False, 0.08),   # dark carvings, slits, holes
    ("toy_red",         "781214", "ea4c42", False, 0.45),
    ("toy_blue",        "182c78", "5c8cec", False, 0.45),
    ("toy_yellow",      "986808", "ffda4c", False, 0.45),
    ("toy_green",       "1c5824", "6cca62", False, 0.45),
    ("eye_glow",        "d6ae5e", "fff6ca", True,  0.30),   # glows: robot and skull eyes
]
SW = {s[0]: i for i, s in enumerate(SWATCHES)}
CW = 32                                  # pixels a column
PW, PH = CW * len(SWATCHES), 256          # 1024 x 256


def hexrgb(h):
    return [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]


def make_palette():
    """Writes textures/PlayGround_Palette.png (RGB colour, A gloss) and _Emission.png; returns both paths."""
    os.makedirs(TEX_DIR, exist_ok=True)
    col = np.zeros((PH, PW, 4), np.float32)
    emi = np.zeros((PH, PW, 4), np.float32)
    emi[..., 3] = 1.0
    v = (np.arange(PH) + 0.5) / PH
    v = (v * v * (3 - 2 * v) * 0.85 + v * 0.15)[:, None, None]          # a soft S from dark to light
    t = ((np.arange(CW) + 0.5) / CW - 0.5)[None, :, None]                # across the column: -0.5 .. 0.5
    warm = np.array([1.12, 1.0, 0.84])[None, None, :]                    # right side warmer and lighter
    for i, (name, dk, lt, glow, gloss) in enumerate(SWATCHES):
        a = np.array(hexrgb(dk))[None, None, :]
        b = np.array(hexrgb(lt))[None, None, :]
        g = np.clip((a + (b - a) * v) * (1 + 0.10 * t) * (1 + (warm - 1) * t), 0, 1)
        col[:, i * CW:(i + 1) * CW, :3] = g
        col[:, i * CW:(i + 1) * CW, 3] = gloss
        if glow:
            emi[:, i * CW:(i + 1) * CW, :3] = g
    paths = []
    for arr, suffix in ((col, ""), (emi, "_Emission")):
        path = os.path.join(TEX_DIR, KIT + "_Palette" + suffix + ".png")
        img = bpy.data.images.new(KIT + "_Palette" + suffix, PW, PH, alpha=True)
        img.pixels.foreach_set(arr.ravel())
        img.filepath_raw = path
        img.file_format = 'PNG'
        img.save()
        bpy.data.images.remove(img)
        paths.append(path)
    return paths


def palette_material(paths):
    m = bpy.data.materials.new(PREFIX + "Palette")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    tc = nt.nodes.new("ShaderNodeTexImage")
    te = nt.nodes.new("ShaderNodeTexImage")
    inv = nt.nodes.new("ShaderNodeMath")
    tc.image = bpy.data.images.load(paths[0], check_existing=True)
    tc.image.alpha_mode = 'CHANNEL_PACKED'
    te.image = bpy.data.images.load(paths[1], check_existing=True)
    tc.interpolation = te.interpolation = 'Linear'
    inv.operation = 'SUBTRACT'
    inv.inputs[0].default_value = 1.0
    tc.location, te.location, inv.location, bsdf.location, out.location = (-700, 250), (-700, -150), (-350, 50), (-100, 100), (300, 100)
    nt.links.new(tc.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(tc.outputs["Alpha"], inv.inputs[1])
    nt.links.new(inv.outputs[0], bsdf.inputs["Roughness"])
    nt.links.new(te.outputs["Color"], bsdf.inputs["Emission Color"])
    bsdf.inputs["Emission Strength"].default_value = 1.6
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    nt.nodes.active = tc
    return m


PATHS = make_palette()
MAT = palette_material(PATHS)

# ---------------------------------------------------------------------------------------------- shapes (all quads)
def V(x, y=0.0, z=0.0):
    return Vector((x, y, z))


def ang(a):
    r = math.radians(a)
    return Vector((math.cos(r), math.sin(r), 0.0))


def T(x=0.0, y=0.0, z=0.0):
    if isinstance(x, Vector):
        return Matrix.Translation(x)
    return Matrix.Translation((x, y, z))


def R(x=0.0, y=0.0, z=0.0):
    return Euler((math.radians(x), math.radians(y), math.radians(z)), 'XYZ').to_matrix().to_4x4()


def S(x=1.0, y=None, z=None):
    y = x if y is None else y
    z = x if z is None else z
    return Matrix.Diagonal((x, y, z, 1.0))


def loft(rings, caps=True, closed=False):
    """Quads between rings of the same even size (ring k corner j to ring k+1 corner j). Caps close both ends with the
    fewest quads (strips across, the user's way round an opening); closed joins the last ring to the first instead.
    Returns (verts, quads, sides): the first 'sides' quads are the side faces, ring by ring."""
    n = len(rings[0])
    verts = [Vector(p) for r in rings for p in r]
    quads = []
    count = len(rings) if closed else len(rings) - 1
    for k in range(count):
        a, b = k * n, ((k + 1) % len(rings)) * n
        for j in range(n):
            j1 = (j + 1) % n
            quads.append((a + j, a + j1, b + j1, b + j))
    sides = len(quads)
    if caps and not closed:
        for base in (0, (len(rings) - 1) * n):
            for i in range(n // 2 - 1):
                quads.append((base + i, base + i + 1, base + n - 2 - i, base + n - 1 - i))
    return verts, quads, sides


def ring(n, r, z=0.0, rot=0.0, ry=None, rib=0.0, wobble=0.0, seed=0):
    """n corners round the z axis at height z (radius r, or an oval r x ry). Rib sinks every other corner (lobes);
    wobble varies each corner's radius (the same seed gives the same wobble)."""
    ry = r if ry is None else ry
    rr = random.Random(seed)
    pts = []
    for j in range(n):
        a = rot + 2 * math.pi * j / n
        k = (1.0 - rib) if (rib and j % 2) else 1.0
        if wobble:
            k *= 1.0 + rr.uniform(-wobble, wobble)
        pts.append(Vector((r * k * math.cos(a), ry * k * math.sin(a), z)))
    return pts


def rect(w, d, z, cx=0.0, cy=0.0):
    x, y = w / 2, d / 2
    return [V(cx - x, cy - y, z), V(cx + x, cy - y, z), V(cx + x, cy + y, z), V(cx - x, cy + y, z)]


def lathe(profile, n=12, rib=0.0, rot=0.0, wobble=0.0, seed=0):
    """A turned shape: profile = [(radius, height)] from bottom to top, n sides (even). Rib sinks every other side
    (pumpkin lobes); the end rings stay round so the caps stay flat."""
    rings = []
    last = len(profile) - 1
    for k, (r, z) in enumerate(profile):
        rings.append(ring(n, r, z, rot, rib=0.0 if k in (0, last) else rib, wobble=wobble, seed=seed * 31 + 1))
    return loft(rings)


def cuboid(sx, sy, sz, bottom=False, top=(1.0, 1.0)):
    """A box (its foot on z = 0 with bottom, else centred); top scales the top face (a tapered block)."""
    x, y = sx / 2, sy / 2
    z0 = 0.0 if bottom else -sz / 2
    return loft([rect(sx, sy, z0), rect(sx * top[0], sy * top[1], z0 + sz)])


def frames(points):
    """(point, tangent, side, up) along a path, carried along without spinning."""
    pts = [Vector(p) for p in points]
    out, side = [], None
    for i, p in enumerate(pts):
        t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        if side is None:
            ref = Vector((0, 0, 1)) if abs(t.z) < 0.95 else Vector((1, 0, 0))
            side = t.cross(ref).normalized()
        else:
            side = (side - t * side.dot(t)).normalized()
        out.append((p, t, side, side.cross(t).normalized()))
    return out


def tube(points, radii, n=6, twist=0.0, rot=0.0):
    """A tube along a path (radius per point, or one radius), n sides; twist turns each ring a bit more (radians)."""
    rings = []
    for i, (p, t, side, up) in enumerate(frames(points)):
        r = radii[i] if isinstance(radii, (list, tuple)) else radii
        a0 = rot + twist * i
        rings.append([p + (side * math.cos(a0 + 2 * math.pi * j / n) + up * math.sin(a0 + 2 * math.pi * j / n)) * r
                      for j in range(n)])
    return loft(rings)


def beam(a, b, w):
    """A square bar w wide from a to b."""
    return tube([a, b], w * 0.7071, n=4, rot=math.pi / 4)


def sweep(path, profile):
    """A closed profile [(across, up)] carried along a path, kept level (no roll): a slide's chute."""
    pts = [Vector(p) for p in path]
    rings = []
    for i, p in enumerate(pts):
        t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        side = t.cross(Vector((0, 0, 1))).normalized()
        up = side.cross(t).normalized()
        rings.append([p + side * a + up * b for a, b in profile])
    return loft(rings)


def torus(rad, r, n_major=12, n_minor=4, rot=45.0):
    """A closed ring (a barrel hoop, a dome ring) round the z axis."""
    rings = []
    for i in range(n_major):
        a = 2 * math.pi * i / n_major
        radial = Vector((math.cos(a), math.sin(a), 0))
        c = radial * rad
        rings.append([c + (radial * math.cos(math.radians(rot) + 2 * math.pi * j / n_minor) +
                           Vector((0, 0, 1)) * math.sin(math.radians(rot) + 2 * math.pi * j / n_minor)) * r
                      for j in range(n_minor)])
    return loft(rings, caps=False, closed=True)


def plate(outline, thick):
    """A flat piece: outline [(x, z)] (an even number of corners, in order round it), thick along y, centred on y = 0."""
    return loft([[V(x, -thick / 2, z) for x, z in outline], [V(x, thick / 2, z) for x, z in outline]])


def quadsphere(n=3, jitter=0.0, seed=0):
    """A ball of quads only: a cube cut n times a side, pushed out round (6 n^2 quads)."""
    index, verts, quads = {}, [], []
    rr = random.Random(seed)

    def vid(i, j, k):
        key = (i, j, k)
        if key not in index:
            x, y, z = i / n * 2 - 1, j / n * 2 - 1, k / n * 2 - 1
            p = Vector((x * math.sqrt(max(0.0, 1 - y * y / 2 - z * z / 2 + y * y * z * z / 3)),
                        y * math.sqrt(max(0.0, 1 - z * z / 2 - x * x / 2 + z * z * x * x / 3)),
                        z * math.sqrt(max(0.0, 1 - x * x / 2 - y * y / 2 + x * x * y * y / 3))))
            if jitter:
                p *= 1.0 + rr.uniform(-jitter, jitter)
            index[key] = len(verts)
            verts.append(p)
        return index[key]

    for axis in range(3):
        for side in (0, n):
            for a in range(n):
                for b in range(n):
                    corners = []
                    for u, w in ((a, b), (a + 1, b), (a + 1, b + 1), (a, b + 1)):
                        c = [0, 0, 0]
                        c[axis], c[(axis + 1) % 3], c[(axis + 2) % 3] = side, u, w
                        corners.append(vid(*c))
                    quads.append(tuple(corners))
    return verts, quads, len(quads)


# ----------------------------------------------------------------------------------------------------- the pieces
class Piece:
    """One mesh being built: each add() is one closed part with its colour (a swatch name, or a function of
    (face index, side count, face centre, part centre) giving one)."""

    def __init__(self, name, seed=7):
        self.name = name
        self.bm = bmesh.new()
        lay = self.bm.faces.layers
        self.l_sw, self.l_tint = lay.int.new("sw"), lay.float.new("tint")
        self.l_lo, self.l_hi = lay.float.new("vlo"), lay.float.new("vhi")
        self.rng = random.Random(seed)
        self.parts = 0

    def add(self, geo, swatch, M=None, vrange=None, tint=None):
        verts, quads, sides = geo
        M = M if M is not None else Matrix.Identity(4)
        vs = [self.bm.verts.new(M @ v) for v in verts]
        zs = [v.co.z for v in vs]
        lo, hi = vrange if vrange else (min(zs), max(zs))
        if hi - lo < 0.02:
            lo, hi = lo - 0.1, hi + 0.1
        t = self.rng.random() if tint is None else tint
        centre = sum((v.co for v in vs), Vector()) / len(vs)
        for i, q in enumerate(quads):
            f = self.bm.faces.new([vs[k] for k in q])
            if callable(swatch):
                name = swatch(i, sides, sum((vs[k].co for k in q), Vector()) / len(q), centre)
            else:
                name = swatch
            f[self.l_sw] = SW[name]
            f[self.l_tint] = t
            f[self.l_lo] = lo
            f[self.l_hi] = hi
        self.parts += 1
        return vs


def finish(p, coll):
    """The piece as an object in its collection: normals out, palette UVs, the kit material, flat shading."""
    bm = p.bm
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    uv = bm.loops.layers.uv.new("UVMap")
    for f in bm.faces:
        col, t, lo, hi = f[p.l_sw], f[p.l_tint], f[p.l_lo], f[p.l_hi]
        u = (col * CW + 5 + t * (CW - 10)) / PW
        bonus = 0.07 if f.normal.z > 0.7 else 0.0
        for l in f.loops:
            v = (l.vert.co.z - lo) / (hi - lo)
            l[uv].uv = (u, min(0.96, max(0.04, 0.06 + 0.86 * v + bonus)))
    stats = dict(faces=len(bm.faces), tris=sum(len(f.verts) - 2 for f in bm.faces),
                 non_quads=sum(1 for f in bm.faces if len(f.verts) != 4),
                 open_edges=sum(1 for e in bm.edges if not e.is_manifold), parts=p.parts)
    bm.faces.layers.int.remove(p.l_sw)
    for layer in (p.l_tint, p.l_lo, p.l_hi):
        bm.faces.layers.float.remove(layer)
    me = bpy.data.meshes.new(p.name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(MAT)
    for poly in me.polygons:
        poly.use_smooth = False
    ob = bpy.data.objects.new(p.name, me)
    coll.objects.link(ob)
    return ob, stats


def carve(p, M, r, mood=0, glow="lantern", eyes=None):
    """A jack-o'-lantern face on the -Y side of a pumpkin (matrix M, radius r): two eyes and a grin, glowing."""
    eye = [(-0.16 * r, -0.08 * r), (0.16 * r, -0.08 * r), (0.04 * r, 0.12 * r), (-0.04 * r, 0.12 * r)]
    if mood == 1:   # angry: the eyes lean in
        eye = [(-0.15 * r, -0.06 * r), (0.15 * r, -0.1 * r), (0.06 * r, 0.1 * r), (-0.06 * r, 0.12 * r)]
    for sx in (-1, 1):
        p.add(plate(eye, 0.16 * r), eyes or glow, M @ R(0, 0, sx * 27) @ R(-15, 0, 0) @ T(0, -0.97 * r, 0) @ S(-sx if mood == 1 else 1, 1, 1))
    wide = 0.34 if mood != 2 else 0.24
    grin = [(-0.13 * r, -0.07 * r), (0.13 * r, -0.07 * r), (wide * r, 0.08 * r), (-wide * r, 0.08 * r)]
    p.add(plate(grin, 0.16 * r), glow, M @ R(18, 0, 0) @ T(0, -0.97 * r, 0))


def pumpkin(p, M, r, face=False, stem=True, sw="pumpkin", mood=0):
    prof = [(0.30, -0.80), (0.80, -0.62), (1.00, -0.26), (1.00, 0.16), (0.82, 0.54), (0.32, 0.74)]
    p.add(lathe([(a * r, b * r) for a, b in prof], n=16, rib=0.13), sw, M)
    if stem:
        p.add(tube([V(0, 0, 0.62 * r), V(0.04 * r, 0.02 * r, 0.9 * r), V(0.12 * r, 0.04 * r, 1.05 * r)],
                   [0.13 * r, 0.1 * r, 0.08 * r], n=6), "stem", M)
    if face:
        carve(p, M, r, mood)


# ---- row 1: trees
def tree_jelly(p, crowns):
    path = [V(0, 0, 0), V(0.05, 0.02, 0.6), V(0.14, -0.04, 1.3), V(0.08, 0.06, 2.0), V(-0.05, 0.08, 2.6), V(0.0, 0.02, 3.3)]
    p.add(tube(path, [0.42, 0.32, 0.27, 0.24, 0.21, 0.18], n=8, twist=math.radians(30)),
          lambda i, s, c, m: "licorice_stripe" if i < s and (i % 8) % 2 == 0 else "licorice", vrange=(0, 4.5))
    for a in (20, 140, 260):
        d = ang(a)
        p.add(tube([d * 0.12 + V(0, 0, 0.4), d * 0.45 + V(0, 0, 0.14), d * 0.72 + V(0, 0, -0.03)], [0.16, 0.11, 0.06], n=6),
              "licorice", vrange=(0, 4.5))
    for (x, y, z, r, sw) in crowns:
        p.add(tube([V(0.0, 0.02, 3.0), V(x * 0.5, y * 0.5, (3.0 + z) / 2 - 0.1), V(x * 0.85, y * 0.85, z - r * 0.55)],
                   [0.15, 0.12, 0.09], n=6), "licorice", vrange=(0, 4.5))
        p.add(quadsphere(3, jitter=0.07, seed=int(abs(x) * 100 + z * 10)), sw, T(x, y, z) @ S(r, r, r * 0.88))


def tree_jelly_a(p):
    tree_jelly(p, [(0, 0, 4.7, 1.45, "jelly_pink"), (-1.25, 0.35, 4.05, 1.05, "jelly_pink"),
                   (1.15, -0.25, 4.15, 1.1, "jelly_lilac"), (0.35, 0.7, 5.45, 0.92, "jelly_pink"),
                   (0.55, -0.95, 3.75, 0.75, "jelly_lilac")])


def tree_jelly_b(p):
    tree_jelly(p, [(0, 0, 4.5, 1.4, "jelly_aqua"), (1.2, 0.4, 3.95, 1.0, "jelly_teal"),
                   (-1.15, -0.3, 4.05, 1.05, "jelly_aqua"), (-0.2, 0.75, 5.2, 0.9, "jelly_teal"),
                   (0.6, -0.8, 5.0, 0.7, "jelly_aqua")])


def roots(p, sw, vr, size=1.0):
    for a in (15, 135, 255):
        d = ang(a)
        p.add(tube([d * 0.1 * size + V(0, 0, 0.35 * size), d * 0.4 * size + V(0, 0, 0.12 * size), d * 0.62 * size + V(0, 0, -0.03)],
                   [0.14 * size, 0.1 * size, 0.05 * size], n=6), sw, vrange=vr)


def tree_lantern(p):
    path = [V(0, 0, 0), V(0.1, 0, 0.8), V(-0.05, 0.05, 1.6), V(0.08, -0.02, 2.4), V(0, 0.05, 3.1), V(-0.1, 0, 3.7)]
    p.add(tube(path, [0.36, 0.27, 0.23, 0.2, 0.16, 0.08], n=6, twist=math.radians(20)), "licorice", vrange=(0, 4.2))
    roots(p, "licorice", (0, 4.2))
    for h, a, L in ((2.2, 30, 1.6), (2.75, 160, 1.45), (3.25, 285, 1.25)):
        d = ang(a)
        pts = [V(0, 0, h) + d * 0.1, V(0, 0, h + 0.35) + d * (L * 0.45), V(0, 0, h + 0.45) + d * (L * 0.8), V(0, 0, h + 0.25) + d * L]
        p.add(tube(pts, [0.11, 0.08, 0.06, 0.04], n=6), "licorice", vrange=(0, 4.2))
        tip = pts[-1]
        p.add(beam(tip + V(0, 0, 0.03), tip - V(0, 0, 0.42), 0.035), "iron")
        pumpkin(p, T(tip - V(0, 0, 0.66)) @ R(0, 0, a + 90), 0.25, face=True, stem=False, mood=(a // 100) % 3)


def tree_mushroom(p):
    p.add(lathe([(0.4, 0.0), (0.3, 0.4), (0.24, 1.3), (0.27, 2.05)], n=10, wobble=0.04, seed=3), "bone", vrange=(-0.4, 2.4))
    cap = lathe([(0.32, 1.95), (1.05, 1.9), (1.5, 2.05), (1.4, 2.4), (1.05, 2.8), (0.55, 3.05), (0.14, 3.12)], n=12)
    p.add(cap, lambda i, s, c, m: "licorice" if i < 12 else ("licorice" if i >= s and c.z < 2.0 else "candy_pink"),
          vrange=(1.6, 3.2))
    for k, (a, rad, z) in enumerate(((20, 0.92, 2.86), (110, 1.12, 2.66), (200, 0.85, 2.9), (290, 1.15, 2.64), (160, 0.4, 3.07), (330, 0.55, 3.02))):
        p.add(quadsphere(2, jitter=0.08, seed=k), "candy_white",
              T(ang(a) * rad + V(0, 0, z)) @ R(0, 0, a) @ R(0, 18 + 40 * (rad - 0.4), 0) @ S(0.17, 0.17, 0.05))


def tree_dead(p):
    path = [V(0, 0, 0), V(0.1, 0.05, 0.9), V(-0.12, 0, 1.8), V(0.05, -0.08, 2.7), V(0.15, 0, 3.5), V(0.05, 0.05, 4.4)]
    p.add(tube(path, [0.36, 0.25, 0.19, 0.14, 0.09, 0.04], n=6, twist=math.radians(25)), "stone_dark", vrange=(0, 5.5))
    roots(p, "stone_dark", (0, 5.5))
    for k in range(6):
        h = 1.6 + k * 0.45
        d = ang(k * 137 + 20)
        n = Vector((-d.y, d.x, 0))
        L = 1.55 - k * 0.13
        pts = [V(0, 0, h) + d * 0.05, V(0, 0, h + 0.4) + d * (L * 0.35), V(0, 0, h + 0.55) + d * (L * 0.7) + n * 0.15,
               V(0, 0, h + 0.95) + d * L]
        p.add(tube(pts, [0.09, 0.07, 0.05, 0.022], n=4, rot=math.pi / 4), "stone_dark", vrange=(0, 5.5))
        m = pts[2]
        p.add(tube([m, m + V(0, 0, 0.36) + n * 0.32], [0.035, 0.014], n=4, rot=math.pi / 4), "stone_dark", vrange=(0, 5.5))


def bush_jelly(p):
    for (x, y, z, r, sw) in ((0, 0, 0.42, 0.6, "jelly_teal"), (0.55, 0.2, 0.32, 0.45, "jelly_aqua"),
                             (-0.5, 0.15, 0.3, 0.48, "jelly_teal"), (0.05, -0.45, 0.28, 0.4, "jelly_aqua")):
        p.add(quadsphere(3, jitter=0.06, seed=int(r * 100)), sw, T(x, y, z) @ S(r, r, r * 0.85))


def stump_slime(p):
    p.add(lathe([(0.55, 0.0), (0.44, 0.12), (0.4, 0.45), (0.42, 0.6)], n=10, wobble=0.05, seed=5),
          lambda i, s, c, m: "wood_light" if i >= s + 4 else "wood", vrange=(-0.2, 0.75))
    p.add(quadsphere(3, jitter=0.05, seed=9), "slime", T(0.05, 0, 0.61) @ S(0.37, 0.37, 0.09))
    for a in (30, 150, 260):
        d = ang(a)
        top = d * 0.38 + V(0, 0, 0.6)
        p.add(tube([top, top + d * 0.06 - V(0, 0, 0.15), top + d * 0.08 - V(0, 0, 0.3)], [0.07, 0.055, 0.04], n=6), "slime")


# ---- row 2: rocks and statues
def boulder_slime(p):
    p.add(quadsphere(4, jitter=0.1, seed=11), lambda i, s, c, m: "moss" if (c - m).normalized().z > 0.55 else "stone",
          T(0, 0, 0.45) @ S(1.0, 0.82, 0.78))
    p.add(quadsphere(3, jitter=0.06, seed=12), "slime", T(0.05, -0.05, 1.12) @ S(0.62, 0.55, 0.2))
    for a in (0, 70, 160, 230, 300):
        d = ang(a)
        rx = 0.62 * abs(d.x) + 0.55 * abs(d.y)
        p.add(tube([d * rx * 0.95 + V(0, 0, 1.08), d * (rx + 0.25) + V(0, 0, 0.82), d * (rx + 0.33) + V(0, 0, 0.58)],
                   [0.09, 0.07, 0.05], n=6), "slime")


def geode_candy(p):
    p.add(quadsphere(3, jitter=0.12, seed=21), "stone_dark", T(0, 0, 0.2) @ S(0.85, 0.7, 0.55))
    for (x, y, z, tx, ty, r, h, sw) in ((0.0, 0.0, 0.45, 0, 0, 0.2, 0.75, "crystal_pink"),
                                        (0.28, 0.12, 0.4, -10, 28, 0.15, 0.55, "crystal_aqua"),
                                        (-0.3, 0.05, 0.4, 5, -30, 0.16, 0.6, "crystal_lilac"),
                                        (0.1, -0.28, 0.38, 30, 8, 0.13, 0.45, "crystal_aqua"),
                                        (-0.12, 0.3, 0.38, -32, -6, 0.12, 0.42, "crystal_pink"),
                                        (0.38, -0.2, 0.3, 22, 40, 0.1, 0.35, "crystal_lilac")):
        rot = random.Random(int(h * 100)).random()
        p.add(loft([ring(6, r, -0.15, rot), ring(6, r, h, rot), ring(6, r * 0.14, h * 1.28, rot)]), sw, T(x, y, z) @ R(tx, ty, 0))


def gravestone(p):
    p.add(cuboid(0.95, 0.5, 0.16, bottom=True), lambda i, s, c, m: "moss" if c.z > 0.15 else "stone_dark", vrange=(0, 1.2))
    lean = R(-5, 0, 0)
    p.add(plate([(-0.36, 0.14), (0.36, 0.14), (0.36, 0.56), (-0.36, 0.56)], 0.17), "stone", lean, vrange=(0, 1.2))
    arch = ([(-0.36, 0.6), (-0.36, 0.78)] +
            [(0.36 * math.cos(math.radians(a)), 0.78 + 0.36 * math.sin(math.radians(a))) for a in (150, 110, 70, 30)] +
            [(0.36, 0.78), (0.36, 0.6)])
    crack = lean @ T(0.02, 0, 0.58) @ R(0, 6, 0) @ T(0, 0, -0.58)
    p.add(plate(arch, 0.17), "stone", crack, vrange=(0, 1.2))
    p.add(plate([(-0.025, 0.72), (0.025, 0.72), (0.025, 0.98), (-0.025, 0.98)], 0.02), "groove", crack @ T(0, -0.09, 0))
    p.add(plate([(-0.09, 0.86), (0.09, 0.86), (0.09, 0.91), (-0.09, 0.91)], 0.02), "groove", crack @ T(0, -0.092, 0))


def stepping_stones(p):
    for k in range(5):
        x, y = -1.3 + k * 0.65, 0.22 * math.sin(k * 1.3)
        r = p.rng.uniform(0.26, 0.36)
        rot = p.rng.random()
        p.add(loft([ring(8, r, -0.03, rot, wobble=0.12, seed=k), ring(8, r * 0.92, 0.06, rot, wobble=0.12, seed=k)]),
              "stone", T(x, y, 0))


def statue_knight(p):
    vr = (0, 2.6)
    p.add(cuboid(1.0, 1.0, 0.45, bottom=True), "stone_dark", vrange=vr)
    p.add(cuboid(1.1, 1.1, 0.08, bottom=True), "stone_dark", T(0, 0, 0.45), vrange=vr)
    for sx in (-1, 1):
        p.add(cuboid(0.2, 0.26, 0.6, bottom=True), "stone", T(sx * 0.13, 0, 0.53), vrange=vr)
        p.add(cuboid(0.24, 0.36, 0.1, bottom=True), "stone", T(sx * 0.13, -0.04, 0.53), vrange=vr)
        p.add(quadsphere(2, seed=sx), "stone", T(sx * 0.36, 0, 1.66) @ S(0.18, 0.2, 0.15), vrange=vr)
        p.add(beam(V(sx * 0.37, 0, 1.6), V(sx * 0.31, -0.1, 1.3), 0.13), "stone", vrange=vr)
        p.add(beam(V(sx * 0.31, -0.1, 1.3), V(sx * 0.17, -0.33, 1.38), 0.12), "stone", vrange=vr)
    p.add(loft([rect(0.62, 0.36, 1.08), rect(0.5, 0.3, 1.22)]), "stone", vrange=vr)
    p.add(loft([rect(0.5, 0.3, 1.2), rect(0.68, 0.38, 1.74)]), "stone", vrange=vr)
    p.add(cuboid(0.16, 0.16, 0.08, bottom=True), "stone", T(0, 0, 1.74), vrange=vr)
    p.add(loft([rect(0.3, 0.32, 1.8), rect(0.33, 0.35, 2.02), rect(0.24, 0.27, 2.14)]), "stone", vrange=vr)
    p.add(plate([(-0.11, -0.02), (0.11, -0.02), (0.11, 0.02), (-0.11, 0.02)], 0.03), "groove", T(0, -0.17, 1.94))
    p.add(cuboid(0.05, 0.22, 0.26, bottom=True, top=(1.0, 0.5)), "stone", T(0, 0.02, 2.12) @ R(-12, 0, 0), vrange=vr)
    pumpkin(p, T(0, -0.36, 1.42), 0.24, face=True)


def totem_pumpkin(p):
    p.add(lathe([(0.5, 0.0), (0.5, 0.18), (0.42, 0.28)], n=8), "stone_dark")
    z = 0.28
    zc = z
    for k, r in enumerate((0.48, 0.43, 0.39, 0.35)):
        zc = z + 0.8 * r - 0.04
        pumpkin(p, T(0, 0, zc) @ R(0, 0, (k % 2) * 14 - 7), r, face=True, stem=(k == 3), mood=k % 3)
        z = zc + 0.74 * r
    wing = [(0.0, -0.1), (0.35, -0.05), (0.62, 0.12), (0.55, 0.38), (0.3, 0.22), (0.08, 0.18)]
    for sx in (-1, 1):
        p.add(plate(wing, 0.04), "candy_pink", T(sx * 0.26, 0.06, zc + 0.05) @ R(0, 0, sx * 18) @ S(sx * 1.35, 1, 1.35))


# ---- row 3: playground and toys
def swing_set(p):
    vr = (0, 2.6)
    for sx in (-1, 1):
        x = sx * 1.6
        for sy in (-1, 1):
            p.add(beam(V(x, sy * 0.8, 0), V(x, sy * 0.06, 2.45), 0.11), "wood", vrange=vr)
        p.add(beam(V(x, -0.42, 1.05), V(x, 0.42, 1.05), 0.08), "wood", vrange=vr)
    p.add(beam(V(-1.8, 0, 2.47), V(1.8, 0, 2.47), 0.15), "wood", vrange=vr)
    for k, (x, swing) in enumerate(((-0.7, 0), (0.7, 24))):
        M = T(x, 0, 2.42) @ R(swing, 0, 0)
        for sx in (-1, 1):
            p.add(beam(V(sx * 0.24, 0, 0), V(sx * 0.24, 0, -1.85), 0.035), "iron", M)
        p.add(cuboid(0.58, 0.24, 0.05), "toy_red" if k == 0 else "toy_blue", M @ T(0, 0, -1.87))


def seesaw_bone(p):
    p.add(loft([rect(0.55, 0.42, 0.0), rect(0.22, 0.42, 0.44)]), "stone", vrange=(0, 0.8))
    M = T(0, 0, 0.52) @ R(0, -11, 0)
    p.add(tube([V(-1.3, 0, 0), V(1.3, 0, 0)], 0.11, n=8), "bone", M, vrange=(0, 1.0))
    for ex in (-1, 1):
        for ey in (-1, 1):
            p.add(quadsphere(2, jitter=0.05, seed=ex * 3 + ey), "bone", M @ T(ex * 1.36, ey * 0.1, 0) @ S(0.15), vrange=(0, 1.0))
        p.add(beam(V(ex * 0.95, 0, 0.08), V(ex * 0.95, 0, 0.42), 0.05), "iron", M)
        p.add(beam(V(ex * 0.95, -0.16, 0.42), V(ex * 0.95, 0.16, 0.42), 0.05), "toy_red", M)


def rocking_horse(p):
    vr = (0, 2.0)
    for sy in (-1, 1):
        pts = [V(x, sy * 0.33, 0.06 + 0.16 * x * x) for x in (-1.05, -0.7, -0.35, 0.0, 0.35, 0.7, 1.05)]
        p.add(tube(pts, 0.055, n=4, rot=math.pi / 4), "wood", vrange=vr)
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.add(beam(V(sx * 0.6, sy * 0.33, 0.1), V(sx * 0.46, sy * 0.19, 0.8), 0.1), "wood", vrange=vr)
    p.add(loft([rect(1.15, 0.4, 0.72), rect(1.25, 0.46, 0.95), rect(1.1, 0.4, 1.12)]), "wood_light", vrange=vr)
    p.add(cuboid(0.42, 0.5, 0.07, bottom=True), "toy_red", T(-0.05, 0, 1.12))
    p.add(beam(V(0.45, 0, 1.0), V(0.8, 0, 1.55), 0.24), "wood_light", vrange=vr)
    for k in range(3):
        p.add(cuboid(0.1, 0.06, 0.16, bottom=True), "wood", T(0.5 + k * 0.11, 0, 1.18 + k * 0.17) @ R(0, -32, 0), vrange=vr)
    H = T(0.98, 0, 1.6) @ R(0, 28, 0)
    p.add(quadsphere(3, jitter=0.03, seed=31), "bone", H @ S(0.34, 0.19, 0.2), vrange=(1.3, 1.9))
    p.add(cuboid(0.3, 0.16, 0.08), "bone", H @ T(0.12, 0, -0.17), vrange=(1.3, 1.9))
    p.add(plate([(-0.06, -0.012), (0.3, -0.012), (0.3, 0.012), (-0.06, 0.012)], 0.17), "groove", H @ T(0.0, 0, -0.12))
    for sy in (-1, 1):
        p.add(plate([(-0.06, -0.05), (0.06, -0.05), (0.06, 0.05), (-0.06, 0.05)], 0.05), "groove", H @ T(-0.06, sy * 0.17, 0.05))
    p.add(tube([V(-0.6, 0, 1.0), V(-0.78, 0, 0.82), V(-0.86, 0, 0.55)], [0.07, 0.06, 0.04], n=6), "wood", vrange=vr)


def jack_in_the_box(p):
    def sides(i, s, c, m):
        d = c - m
        if abs(d.z) > max(abs(d.x), abs(d.y)):
            return "toy_yellow"
        return "toy_red" if abs(d.x) > abs(d.y) else "toy_blue"
    p.add(cuboid(0.8, 0.8, 0.78, bottom=True), sides, vrange=(0, 1.2))
    for z in (0.0, 0.73):
        p.add(cuboid(0.86, 0.86, 0.07, bottom=True), "toy_yellow", T(0, 0, z), vrange=(0, 1.2))
    p.add(cuboid(0.8, 0.8, 0.05, bottom=True), "toy_yellow", T(0, 0.42, 0.8) @ R(-115, 0, 0) @ T(0, -0.42, 0), vrange=(0, 1.6))
    pts = [V(0.11 * math.cos(k * math.pi / 4), 0.11 * math.sin(k * math.pi / 4), 0.78 + 0.6 * k / 24) for k in range(25)]
    p.add(tube(pts, 0.032, n=4), "tin")
    pumpkin(p, T(0, 0, 1.58), 0.3, face=True, mood=2)
    p.add(beam(V(0.4, 0, 0.42), V(0.56, 0, 0.42), 0.05), "tin")
    p.add(beam(V(0.56, 0, 0.42), V(0.56, 0, 0.66), 0.05), "tin")
    p.add(quadsphere(2), "toy_red", T(0.56, 0, 0.7) @ S(0.06))


def toy_block(p):
    def sides(i, s, c, m):
        d = c - m
        if abs(d.z) > max(abs(d.x), abs(d.y)):
            return "toy_red"
        return "toy_green" if abs(d.x) > abs(d.y) else "toy_yellow"
    p.add(cuboid(0.8, 0.8, 0.8, bottom=True), sides, vrange=(-0.8, 0.9))
    eye = [(-0.1, -0.06), (0.1, -0.06), (0.03, 0.08), (-0.03, 0.08)]
    for sx in (-1, 1):
        p.add(plate(eye, 0.03), "groove", T(sx * 0.17, -0.402, 0.53))
    p.add(plate([(-0.12, -0.05), (0.12, -0.05), (0.24, 0.05), (-0.24, 0.05)], 0.03), "groove", T(0, -0.402, 0.27))


def tin_robot(p):
    vr = (0, 1.5)
    for sx in (-1, 1):
        p.add(cuboid(0.2, 0.3, 0.1, bottom=True), "iron", T(sx * 0.13, -0.03, 0))
        p.add(cuboid(0.12, 0.12, 0.36, bottom=True), "tin", T(sx * 0.13, 0, 0.1), vrange=vr)
        p.add(beam(V(sx * 0.3, 0, 0.9), V(sx * 0.37, -0.12, 0.56), 0.1), "tin", vrange=vr)
        p.add(cuboid(0.12, 0.12, 0.1), "iron", T(sx * 0.38, -0.14, 0.5))
        p.add(cuboid(0.08, 0.04, 0.06), "eye_glow", T(sx * 0.09, -0.165, 1.18))
        p.add(cuboid(0.14, 0.03, 0.1), "toy_yellow", T(sx * 0.08, 0.33, 0.7))
    p.add(cuboid(0.52, 0.38, 0.52, bottom=True), "tin", T(0, 0, 0.44), vrange=vr)
    p.add(cuboid(0.3, 0.03, 0.22), "groove", T(0, -0.19, 0.72))
    for k, sw in enumerate(("eye_glow", "toy_red", "toy_yellow")):
        p.add(cuboid(0.05, 0.03, 0.05), sw, T(-0.08 + k * 0.08, -0.205, 0.72))
    p.add(cuboid(0.14, 0.14, 0.06, bottom=True), "iron", T(0, 0, 0.96))
    p.add(cuboid(0.36, 0.32, 0.3, bottom=True), "tin", T(0, 0, 1.0), vrange=vr)
    p.add(cuboid(0.2, 0.03, 0.05), "groove", T(0, -0.165, 1.07))
    p.add(beam(V(0, 0, 1.3), V(0, 0, 1.45), 0.03), "iron")
    p.add(quadsphere(2), "toy_red", T(0, 0, 1.48) @ S(0.05))
    p.add(beam(V(0, 0.19, 0.7), V(0, 0.33, 0.7), 0.04), "toy_yellow")


def slide(p):
    vr = (0, 2.2)
    p.add(cuboid(0.9, 0.9, 0.07, bottom=True), "wood", T(0, 0, 1.58), vrange=vr)
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.add(beam(V(sx * 0.42, sy * 0.42, 0), V(sx * 0.42, sy * 0.42, 2.0), 0.07), "wood", vrange=vr)
        p.add(beam(V(sx * 0.42, -0.42, 1.95), V(sx * 0.42, 0.42, 1.95), 0.06), "wood", vrange=vr)
        p.add(beam(V(sx * 0.3, 1.25, 0), V(sx * 0.3, 0.42, 1.62), 0.07), "wood", vrange=vr)
    for k in range(1, 6):
        f = k / 6.0
        p.add(beam(V(-0.3, 1.25 - 0.83 * f, 1.62 * f), V(0.3, 1.25 - 0.83 * f, 1.62 * f), 0.05), "wood_light", vrange=vr)
    path = []
    for k in range(13):
        s = k / 12.0
        path.append(V(0.25 * math.sin(math.pi * s), -0.45 - 3.2 * s, 0.24 + 1.38 * (1 - s) ** 2))
    path.append(V(0.0, -4.0, 0.24))
    u = [(-0.34, 0.16), (-0.34, -0.03), (0.34, -0.03), (0.34, 0.16), (0.28, 0.16), (0.28, 0.03), (-0.28, 0.03), (-0.28, 0.16)]
    p.add(sweep(path, u), "tin", vrange=(0, 2.0))
    for k in (3, 8):
        q = path[k]
        p.add(beam(V(q.x, q.y, 0), q - V(0, 0, 0.02), 0.06), "wood", vrange=vr)


def sandbox_candy(p):
    for sy in (-1, 1):
        p.add(cuboid(2.4, 0.14, 0.3, bottom=True), "wood_light", T(0, sy * 0.83, 0), vrange=(-0.2, 0.5))
    for sx in (-1, 1):
        p.add(cuboid(0.14, 1.52, 0.3, bottom=True), "wood_light", T(sx * 1.13, 0, 0), vrange=(-0.2, 0.5))
    p.add(cuboid(2.12, 1.52, 0.2, bottom=True), "sand", vrange=(-0.4, 0.3))
    cols = ("candy_pink", "jelly_aqua", "toy_yellow", "jelly_lilac", "toy_green", "candy_pink", "jelly_aqua", "toy_yellow")
    for k, sw in enumerate(cols):
        x, y = p.rng.uniform(-0.85, 0.85), p.rng.uniform(-0.55, 0.55)
        p.add(quadsphere(2, seed=k), sw, T(x, y, 0.22) @ R(0, 0, p.rng.uniform(0, 180)) @ S(0.09, 0.055, 0.05))
    for (x, y, a) in ((-0.55, 0.2, 15), (0.6, -0.25, -20)):
        p.add(beam(V(x, y, 0.15), V(x, y, 0.62), 0.03), "candy_white")
        disc = lathe([(0.15, -0.025), (0.15, 0.025)], n=12)
        p.add(disc, lambda i, s, c, m: "candy_pink" if i < s or (i - s) % 2 == 0 else "candy_white",
              T(x, y, 0.72) @ R(0, 0, a) @ R(90, 0, 0))
    p.add(lathe([(0.1, 0.18), (0.15, 0.44)], n=10), "toy_red", T(0.78, 0.45, 0))


def dome_candycane(p):
    r0 = 1.75

    def stripes(i, s, c, m):
        return "candy_pink" if (i // 6) % 2 == 0 else "candy_white"
    for k in range(4):
        d = ang(k * 45)
        pts = [d * (r0 * math.cos(math.pi * j / 14)) + V(0, 0, r0 * math.sin(math.pi * j / 14)) for j in range(15)]
        p.add(tube(pts, 0.05, n=6), stripes, vrange=(0, 2.0))
        for e in (d, -d):
            p.add(cuboid(0.16, 0.16, 0.08, bottom=True), "stone_dark", T(e * r0))
    hz, hr = r0 * math.sin(math.radians(40)), r0 * math.cos(math.radians(40))
    p.add(torus(hr, 0.045, 24, 6), lambda i, s, c, m: "candy_pink" if (i // 6) % 2 == 0 else "candy_white", T(0, 0, hz), vrange=(0, 2.0))


def merry_go_round(p):
    p.add(lathe([(1.5, 0.0), (1.5, 0.16)], n=16),
          lambda i, s, c, m: "toy_red" if i < s else ("wood_light" if (i - s) % 2 == 0 else "wood"), vrange=(-0.6, 0.4))
    p.add(lathe([(0.13, 0.16), (0.1, 1.15), (0.2, 1.2), (0.2, 1.3), (0.06, 1.36)], n=8), "tin", vrange=(0, 1.5))
    for k in range(4):
        d = ang(45 + 90 * k)
        pts = [V(0, 0, 1.18) + d * 0.12, V(0, 0, 1.12) + d * 0.7, V(0, 0, 0.78) + d * 1.25, V(0, 0, 0.17) + d * 1.35]
        p.add(tube(pts, 0.035, n=6), "tin", vrange=(0, 1.5))
        a = 90 * k
        pumpkin(p, T(ang(a) * 0.95 + V(0, 0, 0.16 + 0.8 * 0.3)) @ R(0, 0, a + 90), 0.3, face=(k % 2 == 0), mood=k % 3)


def monster_gate(p):
    vr = (0, 3.6)
    for sx in (-1, 1):
        pts = [V(sx * 1.25, 0, 0), V(sx * 1.32, 0.05, 0.9), V(sx * 1.2, -0.05, 1.8), V(sx * 1.18, 0, 2.6)]
        p.add(tube(pts, [0.26, 0.2, 0.19, 0.17], n=6, twist=math.radians(35)), "wood", vrange=vr)
        for (dx, dy, r) in ((0.22, -0.25, 0.36), (0.1, 0.28, 0.3)):
            p.add(quadsphere(2, jitter=0.15, seed=int(r * 50) + sx), "stone", T(sx * (1.25 + dx), dy, 0.1) @ S(r, r * 0.9, r * 0.75))
    arch = [V(1.18 * math.cos(math.pi * j / 10), 0, 2.55 + 0.85 * math.sin(math.pi * j / 10)) for j in range(11)]
    p.add(tube(arch, 0.17, n=6), "wood", vrange=vr)
    for (a, L) in ((35, 0.5), (140, 0.42)):
        b = V(1.18 * math.cos(math.radians(a)), 0, 2.55 + 0.85 * math.sin(math.radians(a)))
        out = b + Vector((math.cos(math.radians(a)), 0, math.sin(math.radians(a)))) * L + V(0, 0, 0.15)
        p.add(tube([b, out], [0.06, 0.02], n=4, rot=math.pi / 4), "wood", vrange=vr)
    mist = ([(-1.05, 0.0), (-1.05, 2.5)] +
            [(1.05 * math.cos(math.radians(a)), 2.5 + 0.78 * math.sin(math.radians(a))) for a in (150, 115, 65, 30)] +
            [(1.05, 2.5), (1.05, 0.0)])
    p.add(plate(mist, 0.05), "mist")
    p.add(quadsphere(3, jitter=0.03, seed=77), "bone", T(0, -0.06, 3.43) @ S(0.24, 0.22, 0.22))
    p.add(cuboid(0.2, 0.14, 0.08), "bone", T(0, -0.12, 3.24))
    for sx in (-1, 1):
        p.add(plate([(-0.05, -0.04), (0.05, -0.04), (0.05, 0.05), (-0.05, 0.05)], 0.04), "eye_glow", T(sx * 0.08, -0.26, 3.45))


def fence_picket(p):
    for x in (0.0, 2.0):
        p.add(cuboid(0.11, 0.11, 1.05, bottom=True), "wood", T(x, 0, 0), vrange=(0, 1.2))
        p.add(loft([rect(0.13, 0.13, 1.05), rect(0.03, 0.03, 1.15)]), "wood", T(x, 0, 0), vrange=(0, 1.2))
    for z in (0.3, 0.75):
        p.add(cuboid(1.9, 0.05, 0.08), "wood", T(1.0, 0.07, z), vrange=(0, 1.2))
    for k in range(8):
        if k == 5:
            continue      # one picket missing
        h = p.rng.uniform(0.88, 1.04)
        pk = [(-0.05, 0.0), (-0.05, h), (-0.008, h + 0.09), (0.008, h + 0.09), (0.05, h), (0.05, 0.0)]
        p.add(plate(pk, 0.03), "bone", T(0.2 + k * 0.23, 0, 0.02) @ R(0, p.rng.uniform(-7, 7), 0), vrange=(-0.3, 1.2))


def hill_slime(p):
    p.add(quadsphere(5, jitter=0.06, seed=41), lambda i, s, c, m: "moss" if (c - m).normalized().z > 0.5 else "stone",
          T(0, 0, -0.9) @ S(5.0, 3.2, 3.4))
    p.add(quadsphere(3, jitter=0.08, seed=42), "slime", T(-1.0, -0.9, 2.28) @ S(1.4, 1.0, 0.22))

    def on_hill(x, z, out=1.03):
        # the point on the hill's front (-y) at this x and height, a little proud of it
        k = 1.0 - (x / 5.0) ** 2 - ((z + 0.9) / 3.4) ** 2
        return V(x * out, -3.2 * math.sqrt(max(0.0, k)) * out, z)
    for x, low in ((-1.7, 1.2), (-0.4, 1.45)):
        p.add(tube([on_hill(x, z) for z in (2.2, (2.2 + low) / 2, low)], [0.2, 0.13, 0.06], n=6), "slime")


def puddle_slime(p):
    p.add(loft([ring(12, 0.8, -0.01, ry=0.55, wobble=0.15, seed=3), ring(12, 0.76, 0.025, ry=0.52, wobble=0.15, seed=3)]), "slime")


def beach_ball(p):
    cols = ["toy_red", "candy_white", "toy_blue", "toy_yellow", "candy_white", "toy_green"]

    def gores(i, s, c, m):
        d = c - m
        if abs(d.normalized().z) > 0.86:
            return "candy_white"
        return cols[int((math.atan2(d.y, d.x) + math.pi) / (2 * math.pi) * 6) % 6]
    p.add(quadsphere(4), gores, T(0, 0, 0.36) @ S(0.42, 0.42, 0.36), vrange=(-0.2, 0.9))


# ---- row 4: small breakables, each with its broken chunks
def crate(p, broken=False):
    vr = (0, 0.62)
    p.add(cuboid(0.56, 0.56, 0.56, bottom=True), "wood", T(0, 0, 0.02), vrange=vr)
    for z in (0.0, 0.55):
        p.add(cuboid(0.62, 0.62, 0.07, bottom=True), "wood_light", T(0, 0, z), vrange=vr)
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.add(cuboid(0.075, 0.075, 0.48, bottom=True), "wood_light", T(sx * 0.28, sy * 0.28, 0.07), vrange=vr)
    for sy in (-1, 1):
        p.add(cuboid(0.07, 0.025, 0.6), "wood_light", T(0, sy * 0.29, 0.31) @ R(0, 45, 0), vrange=vr)


def crate_broken():
    a = Piece("a")
    a.add(cuboid(0.56, 0.56, 0.2, bottom=True), "wood", T(0, 0, 0.02), vrange=(0, 0.62))
    a.add(cuboid(0.62, 0.62, 0.07, bottom=True), "wood_light", vrange=(0, 0.62))
    b = Piece("b")
    b.add(cuboid(0.56, 0.05, 0.38), "wood", R(0, 0, 0), vrange=(-0.3, 0.3))
    b.add(cuboid(0.07, 0.025, 0.46), "wood_light", T(0, -0.035, 0) @ R(0, 45, 0), vrange=(-0.3, 0.3))
    c = Piece("c")
    c.add(cuboid(0.62, 0.07, 0.07), "wood_light")
    c.add(cuboid(0.075, 0.075, 0.42), "wood_light", T(0.27, 0, 0.17), vrange=(-0.1, 0.4))
    d = Piece("d")
    d.add(cuboid(0.5, 0.05, 0.3), "wood", vrange=(-0.3, 0.3))
    return [(a, (0, 0, 0), 12), (b, (0.62, 0.15, 0.06), 80), (c, (-0.2, 0.62, 0.04), 30), (d, (-0.62, -0.1, 0.03), -50)]


def barrel(p):
    p.add(lathe([(0.27, 0.0), (0.33, 0.18), (0.355, 0.45), (0.33, 0.72), (0.27, 0.9)], n=12),
          lambda i, s, c, m: "wood_light" if i >= s + 5 else "wood", vrange=(0, 0.9))
    for z in (0.17, 0.73):
        p.add(torus(0.335, 0.028, 12, 4), "iron", T(0, 0, z))


def barrel_broken():
    out = []
    for k in range(4):
        st = Piece("s%d" % k)
        st.add(cuboid(0.13, 0.04, 0.86), "wood", vrange=(-0.5, 0.5))
        out.append((st, (-0.45 + k * 0.28, 0.1 * math.sin(k * 2.1), 0.02), 70 + k * 25))
    hoop = Piece("hoop")
    hoop.add(torus(0.335, 0.028, 12, 4), "iron")
    out.append((hoop, (0.35, 0.55, 0.03), 0))
    lid = Piece("lid")
    lid.add(lathe([(0.27, -0.025), (0.27, 0.025)], n=12), "wood_light", vrange=(-0.3, 0.3))
    out.append((lid, (-0.4, 0.6, 0.03), 0))
    return [(pc, off, a) for pc, off, a in out]


def candy_basket(p):
    p.add(lathe([(0.15, 0.0), (0.19, 0.04), (0.25, 0.24), (0.27, 0.3)], n=10, rib=0.05), "wood_light", vrange=(-0.1, 0.4))
    p.add(torus(0.27, 0.025, 10, 4), "wood", T(0, 0, 0.3))
    arc = [V(0.25 * math.cos(math.pi * j / 8), 0, 0.3 + 0.3 * math.sin(math.pi * j / 8)) for j in range(9)]
    p.add(tube(arc, 0.022, n=4, rot=math.pi / 4), "wood")
    for k, sw in enumerate(("candy_pink", "jelly_aqua", "toy_yellow", "jelly_lilac", "toy_green", "candy_pink", "jelly_aqua")):
        a = k * 51
        rad = 0.12 if k else 0.0
        p.add(quadsphere(2, seed=k), sw, T(ang(a) * rad + V(0, 0, 0.32 + 0.02 * (k % 2))) @ S(0.06))


def candy_basket_broken():
    body = Piece("body")
    body.add(lathe([(0.15, 0.0), (0.19, 0.04), (0.25, 0.24), (0.27, 0.3)], n=10, rib=0.05), "wood_light", vrange=(-0.1, 0.4))
    body.add(torus(0.27, 0.025, 10, 4), "wood", T(0, 0, 0.3))
    handle = Piece("handle")
    arc = [V(0.25 * math.cos(math.pi * j / 8), 0.3 * math.sin(math.pi * j / 8), 0) for j in range(9)]
    handle.add(tube(arc, 0.022, n=4, rot=math.pi / 4), "wood")
    sweets = Piece("sweets")
    for k, sw in enumerate(("candy_pink", "jelly_aqua", "toy_yellow")):
        sweets.add(quadsphere(2, seed=k), sw, T(k * 0.13 - 0.13, 0.04 * k, 0.06) @ S(0.06))
    return [(body, (0, 0, 0.27), 0), (handle, (0.45, 0.2, 0.02), 25), (sweets, (-0.35, 0.3, 0.0), 0)]


def clay_pot(p):
    p.add(lathe([(0.14, 0.0), (0.22, 0.08), (0.28, 0.26), (0.25, 0.42), (0.16, 0.52), (0.15, 0.56), (0.2, 0.62)], n=10),
          "clay", vrange=(0, 0.62))
    p.add(torus(0.283, 0.02, 10, 4), "pumpkin", T(0, 0, 0.27))


def clay_pot_broken():
    base = Piece("base")
    base.add(lathe([(0.14, 0.0), (0.22, 0.08), (0.26, 0.18)], n=10), "clay", vrange=(0, 0.62))
    out = [(base, (0, 0, 0), 0)]
    for k in range(3):
        sh = Piece("shard%d" % k)
        a0 = math.radians(k * 50)
        arc = [(0.27 * math.cos(a0 + t), 0.27 * math.sin(a0 + t)) for t in (0.0, 0.35, 0.7)]
        inner = [(0.24 * math.cos(a0 + t), 0.24 * math.sin(a0 + t)) for t in (0.7, 0.35, 0.0)]
        ring0 = [V(x, y, 0.0) for x, y in arc + inner]
        ring1 = [V(x, y, 0.18 + 0.03 * k) for x, y in arc + inner]
        sh.add(loft([ring0, ring1]), "clay", T(-0.25, 0, 0), vrange=(-0.1, 0.3))
        out.append((sh, (0.45 * math.cos(k * 2.2), 0.45 * math.sin(k * 2.2), 0.03), k * 70))
    return out


def pumpkin_stack(p):
    pumpkin(p, T(-0.24, 0.02, 0.8 * 0.3) @ R(0, 0, -15), 0.3)
    pumpkin(p, T(0.27, -0.05, 0.8 * 0.27) @ R(0, 0, 20), 0.27, face=True)
    pumpkin(p, T(0.02, 0.0, 0.58) @ R(0, 0, 5), 0.22, face=True, mood=1)


def pumpkin_stack_broken():
    out = []
    for k in range(4):
        ch = Piece("chunk%d" % k)
        ch.add(quadsphere(2, jitter=0.25, seed=k), "pumpkin", T(0, 0, 0.1) @ S(0.17, 0.14, 0.11), vrange=(-0.1, 0.3))
        out.append((ch, (0.38 * math.cos(k * 1.7), 0.38 * math.sin(k * 1.7), 0), k * 40))
    stem = Piece("stem")
    stem.add(tube([V(0, 0, 0.03), V(0.03, 0, 0.12), V(0.08, 0, 0.17)], [0.035, 0.028, 0.022], n=6), "stem")
    out.append((stem, (0.05, 0.05, 0), 0))
    return out


def lollipop_sign(p):
    p.add(tube([V(0, 0, 0), V(0, 0, 1.32)], 0.04, n=6), "candy_white", vrange=(0, 1.7))
    p.add(lathe([(0.36, -0.045), (0.36, 0.045)], n=16),
          lambda i, s, c, m: "candy_pink" if i < s or (i - s) % 2 == 0 else "candy_white", T(0, 0, 1.6) @ R(90, 0, 0))
    p.add(cuboid(0.62, 0.05, 0.26), "wood_light", T(0, -0.05, 0.95), vrange=(0.5, 1.3))


def lollipop_sign_broken():
    a = Piece("stick_low")
    a.add(tube([V(0, 0, 0), V(0, 0, 0.7)], 0.04, n=6), "candy_white", vrange=(0, 1.7))
    b = Piece("stick_high")
    b.add(tube([V(0, 0, 0), V(0, 0, 0.6)], 0.04, n=6), "candy_white", vrange=(0, 1.7))
    c = Piece("disc")
    c.add(lathe([(0.36, -0.045), (0.36, 0.045)], n=16),
          lambda i, s, c_, m: "candy_pink" if i < s or (i - s) % 2 == 0 else "candy_white")
    d = Piece("board")
    d.add(cuboid(0.62, 0.05, 0.26), "wood_light", vrange=(-0.4, 0.4))
    return [(a, (0, 0, 0.04), 0), (b, (0.3, 0.35, 0.04), 0), (c, (-0.5, 0.2, 0.05), 0), (d, (0.5, -0.2, 0.03), 20)]


# ---- gate 1's giant pumpkin (the finale boss's look): a stew pumpkin, its slime boiling over
STEW_R = 1.5     # gate 1's giant pumpkin radius (CauldronDefence Finale Radius); the boss grows the model to its own


def giant_stew_pumpkin(p):
    """Pivot = the ball's middle (the boss model's centre), face to -Y. Body, a glowing slime lid boiling over the top,
    drips down the sides, bubbles, a stem through the slime, an angry face glowing slime green."""
    r = STEW_R
    prof = [(0.30, -0.80), (0.80, -0.62), (1.00, -0.26), (1.00, 0.16), (0.82, 0.54), (0.32, 0.74)]
    p.add(lathe([(a * r, b * r) for a, b in prof], n=20, rib=0.11), "pumpkin", vrange=(-2.6 * r, 0.6 * r))

    def body_radius(z):
        # the body's radius at height z (between its profile rings, ribs ignored)
        zz = z / r
        for (a0, b0), (a1, b1) in zip(prof, prof[1:]):
            if b0 <= zz <= b1:
                return r * (a0 + (a1 - a0) * (zz - b0) / (b1 - b0))
        return r * 0.3
    lid = [(0.87, 0.48), (0.8, 0.6), (0.58, 0.76), (0.3, 0.84), (0.08, 0.87)]
    p.add(lathe([(a * r, b * r) for a, b in lid], n=20, wobble=0.07, seed=12), "slime", vrange=(0.2 * r, 1.0 * r))
    rng = random.Random(5)
    for k in range(9):
        a = k * 40 + rng.uniform(-12, 12)
        if abs(((a - 270) + 180) % 360 - 180) < 55:
            continue      # the face stays clear
        low = rng.uniform(-0.45, 0.15) * r
        pts = []
        for i in range(6):
            z = 0.5 * r + (low - 0.5 * r) * i / 5
            pts.append(ang(a) * (body_radius(z) * (0.96 if i % 2 else 1.0) + 0.05 * r) + V(0, 0, z))
        p.add(tube(pts, [0.1 * r, 0.08 * r, 0.065 * r, 0.055 * r, 0.05 * r, 0.045 * r], n=6), "slime", vrange=(-0.6 * r, 1.0 * r))
        p.add(quadsphere(2, jitter=0.1, seed=k), "slime", T(pts[-1] + ang(a) * 0.01 * r - V(0, 0, 0.03 * r)) @ S(0.075 * r), vrange=(-0.6 * r, 1.0 * r))
    for k, (a, d, sz) in enumerate(((30, 0.35, 0.09), (150, 0.5, 0.07), (250, 0.25, 0.11), (330, 0.6, 0.06))):
        p.add(quadsphere(2, seed=20 + k), "slime", T(ang(a) * (d * r) + V(0, 0, (0.9 - 0.15 * d) * r)) @ S(sz * r))
    p.add(tube([V(0, 0, 0.78 * r), V(0.04 * r, 0.02 * r, 1.0 * r), V(0.14 * r, 0.05 * r, 1.12 * r)], [0.1 * r, 0.08 * r, 0.06 * r], n=6), "stem")
    # eyes on their own glowing column (eye_glow): the Unity kit menu finds the face by it and turns it to the front
    carve(p, Matrix.Identity(4), r, mood=1, glow="slime", eyes="eye_glow")


# ---- the stand-in cauldron (Sample_Arena only: the game keeps its own Fire_Woods_n_Cauldron)
def cauldron_standin(p):
    for k in range(11):
        d = ang(k * 360 / 11 + 8)
        r = p.rng.uniform(0.24, 0.32)
        p.add(quadsphere(2, jitter=0.15, seed=k), "stone", T(d * 1.02 + V(0, 0, 0.1)) @ R(0, 0, k * 33) @ S(r, r * 0.85, r * 0.6))
    for k in range(3):
        d = ang(k * 60)
        p.add(beam(-d * 0.62 + V(0, 0, 0.1), d * 0.62 + V(0, 0, 0.14), 0.14), "wood")
    for k in range(5):
        d = ang(k * 72 + 20)
        p.add(quadsphere(2, seed=k), "lantern", T(d * 0.22 + V(0, 0, 0.32)) @ S(0.14, 0.14, 0.3))
    for k in range(3):
        d = ang(k * 120 + 30)
        p.add(beam(d * 0.5 + V(0, 0, 0.0), d * 0.42 + V(0, 0, 0.5), 0.07), "iron")
    p.add(lathe([(0.25, 0.3), (0.56, 0.42), (0.7, 0.72), (0.64, 1.06), (0.56, 1.16), (0.62, 1.22)], n=14), "iron", vrange=(0.2, 1.6))
    p.add(lathe([(0.55, 1.06), (0.55, 1.13)], n=14), "slime")
    for k in range(4):
        d = ang(k * 90 + 40)
        p.add(quadsphere(2, seed=k), "slime", T(d * (0.15 + 0.08 * k) + V(0, 0, 1.2 + 0.05 * k)) @ S(0.07 + 0.015 * k))


def ground(p):
    p.add(lathe([(80.0, -0.08), (80.0, 0.0)], n=32), "moss", vrange=(-1.5, 1.0), tint=0.35)


# ------------------------------------------------------------------------------------------------------------ build
PIECES = [   # rows of the kit sheet, front to back: (name, builder, broken builder or None)
    ("Breakables", [("Crate", crate, crate_broken), ("Barrel", barrel, barrel_broken),
                    ("Candy_Basket", candy_basket, candy_basket_broken), ("Clay_Pot", clay_pot, clay_pot_broken),
                    ("Pumpkin_Stack", pumpkin_stack, pumpkin_stack_broken), ("Lollipop_Sign", lollipop_sign, lollipop_sign_broken)]),
    ("Toys", [("Toy_Block", toy_block, None), ("Tin_Robot", tin_robot, None), ("Jack_In_The_Box", jack_in_the_box, None),
              ("Rocking_Horse", rocking_horse, None), ("Beach_Ball", beach_ball, None), ("Puddle_Slime", puddle_slime, None),
              ("Stepping_Stones", stepping_stones, None)]),
    ("Playground", [("Seesaw_Bone", seesaw_bone, None), ("Swing_Set", swing_set, None), ("Slide", slide, None),
                    ("Dome_CandyCane", dome_candycane, None), ("Merry_Go_Round", merry_go_round, None),
                    ("Sandbox_Candy", sandbox_candy, None)]),
    ("Rocks and statues", [("Gravestone", gravestone, None), ("Geode_Candy", geode_candy, None),
                           ("Boulder_Slime", boulder_slime, None), ("Stump_Slime", stump_slime, None),
                           ("Statue_Knight", statue_knight, None), ("Totem_Pumpkin", totem_pumpkin, None),
                           ("Fence_Picket", fence_picket, None)]),
    ("Trees", [("Bush_Jelly", bush_jelly, None), ("Tree_Mushroom", tree_mushroom, None), ("Tree_Lantern", tree_lantern, None),
               ("Tree_Jelly_A", tree_jelly_a, None), ("Tree_Jelly_B", tree_jelly_b, None), ("Tree_Dead", tree_dead, None)]),
    ("Edge", [("Monster_Gate", monster_gate, None), ("Hill_Slime", hill_slime, None),
              ("Giant_Stew_Pumpkin", giant_stew_pumpkin, None)]),
]

KIT_COLL = bpy.data.collections.new(KIT + "_Kit")
sc.collection.children.link(KIT_COLL)
ARENA = bpy.data.collections.new("Sample_Arena")
sc.collection.children.link(ARENA)
SHEET = bpy.data.collections.new("Kit_Sheet_Labels")
sc.collection.children.link(SHEET)

groups = {}      # piece name -> dict(coll, objs, lo, hi, row)
report = []


def local_bounds(objs_offsets):
    lo, hi = Vector((1e9, 1e9, 1e9)), Vector((-1e9, -1e9, -1e9))
    for ob, off in objs_offsets:
        for v in ob.data.vertices:
            w = ob.matrix_basis @ v.co
            lo = Vector(map(min, lo, w))
            hi = Vector(map(max, hi, w))
    return lo, hi


seed = 100
for row, items in PIECES:
    for name, build, build_broken in items:
        seed += 1
        coll = bpy.data.collections.new(PREFIX + name)
        KIT_COLL.children.link(coll)
        p = Piece(PREFIX + name, seed)
        build(p)
        ob, st = finish(p, coll)
        if name == "Giant_Stew_Pumpkin":
            ob.location.z = STEW_R      # its pivot is the ball's middle; on the sheet it stands on the floor
        lo, hi = local_bounds([(ob, None)])
        groups[name] = dict(coll=coll, objs=[ob], lo=lo, hi=hi, row=row)
        report.append((name, hi - lo, st))
        if build_broken:
            bcoll = bpy.data.collections.new(PREFIX + name + "_Broken")
            KIT_COLL.children.link(bcoll)
            objs = []
            tot = dict(faces=0, tris=0, non_quads=0, open_edges=0, parts=0)
            for k, (chunk, off, yaw) in enumerate(build_broken()):
                chunk.name = "%s%s_Broken_%d" % (PREFIX, name, k + 1)
                cob, cst = finish(chunk, bcoll)
                # the chunk's origin in its middle (a loose physics piece), sitting on the floor
                me = cob.data
                clo, chi = local_bounds([(cob, None)])
                mid = Vector(((clo.x + chi.x) / 2, (clo.y + chi.y) / 2, (clo.z + chi.z) / 2))
                me.transform(Matrix.Translation(-mid))
                cob.location = Vector(off) + Vector((0, 0, mid.z - clo.z))
                cob.rotation_euler = (0, 0, math.radians(yaw))
                objs.append(cob)
                for key in tot:
                    tot[key] += cst[key]
            lo, hi = local_bounds([(o, None) for o in objs])
            groups[name + "_Broken"] = dict(coll=bcoll, objs=objs, lo=lo, hi=hi, row=row)
            report.append((name + "_Broken", hi - lo, tot))

# The kit sheet: rows from front to back on the floor, far from the arena (which sits at the world's middle).
SHEET_X, SHEET_Y = 200.0, 0.0
ELEV = math.radians(32)                 # the sheet camera looks down this much
LABEL = 0.75                            # room for the name in front of each piece
row_y = SHEET_Y
prev = None
sheet_box = [1e9, -1e9, 1e9, -1e9]      # x min/max, screen-up min/max
for row, items in PIECES:
    names = []
    for name, _, b in items:
        names.append(name)
        if b:
            names.append(name + "_Broken")
    widths = [max(groups[n]["hi"].x - groups[n]["lo"].x, 1.55) for n in names]
    gap = 0.9
    total = sum(widths) + gap * (len(names) - 1)
    row_lo = min(groups[n]["lo"].y for n in names)
    row_hi = max(groups[n]["hi"].y for n in names)
    row_h = max(groups[n]["hi"].z for n in names)
    if prev is not None:
        p_hi, p_h = prev
        row_y = row_y + p_hi - row_lo + LABEL + (p_h * math.cos(ELEV) + 0.4) / math.sin(ELEV)
    x = SHEET_X - total / 2
    for n, w in zip(names, widths):
        g = groups[n]
        gx = x + w / 2 - (g["lo"].x + g["hi"].x) / 2
        for ob in g["objs"]:
            ob.location = ob.location + Vector((gx, row_y, 0.0))
        g["at"] = Vector((gx, row_y, 0.0))
        g["coll"].instance_offset = g["at"]
        # its name in front of it, turned to the sheet camera
        cu = bpy.data.curves.new("Label_" + n, type="FONT")
        cu.body = n.replace("_", " ")
        cu.size = 0.24 if w < 1.6 else 0.3 if w < 2.4 else 0.42
        cu.align_x = "CENTER"
        lab = bpy.data.objects.new("Label_" + n, cu)
        lab.location = (x + w / 2, row_y + g["lo"].y - 0.45, 0.02)
        lab.rotation_euler = (math.pi / 2 - ELEV, 0, 0)
        SHEET.objects.link(lab)
        sheet_box[0] = min(sheet_box[0], x - 0.3)
        sheet_box[1] = max(sheet_box[1], x + w + 0.3)
        sheet_box[2] = min(sheet_box[2], (row_y + g["lo"].y - 0.8) * math.sin(ELEV))
        sheet_box[3] = max(sheet_box[3], (row_y + g["hi"].y) * math.sin(ELEV) + g["hi"].z * math.cos(ELEV))
        x += w + gap
    prev = (row_hi, row_h)

label_mat = bpy.data.materials.new("Kit_Label")
label_mat.use_nodes = True
lnt = label_mat.node_tree
lnt.nodes.clear()
lem = lnt.nodes.new("ShaderNodeEmission")
lem.inputs["Color"].default_value = (0.92, 0.88, 1.0, 1.0)
lem.inputs["Strength"].default_value = 1.2
lout = lnt.nodes.new("ShaderNodeOutputMaterial")
lnt.links.new(lem.outputs[0], lout.inputs["Surface"])
for lab in SHEET.objects:
    lab.data.materials.append(label_mat)
# a plain floor under the sheet (hides what's sunk into the ground, gives shadows)
floor_mat = bpy.data.materials.new("Kit_Sheet_Floor")
floor_mat.use_nodes = True
fb = next(n for n in floor_mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
fb.inputs["Base Color"].default_value = (0.11, 0.1, 0.14, 1.0)
fb.inputs["Roughness"].default_value = 0.9
fw, fd = sheet_box[1] - sheet_box[0] + 20.0, row_y - SHEET_Y + 40.0
bpy.ops.mesh.primitive_plane_add(size=1.0, location=((sheet_box[0] + sheet_box[1]) / 2, SHEET_Y + (row_y - SHEET_Y) / 2, 0.0))
floor = bpy.context.active_object
floor.name = "Kit_Sheet_Floor"
floor.scale = (fw, fd, 1.0)
floor.data.materials.append(floor_mat)
for c in floor.users_collection:
    c.objects.unlink(floor)
SHEET.objects.link(floor)

# ------------------------------------------------------------------------------------------------- sample arena
def place(name, x, y, rot=0.0, scale=1.0):
    g = groups[name]
    e = bpy.data.objects.new("Arena_" + name, None)
    e.instance_type = 'COLLECTION'
    e.instance_collection = g["coll"]
    e.location = (x, y, 0.0)
    e.rotation_euler = (0, 0, math.radians(rot))
    e.scale = (scale, scale, scale)
    ARENA.objects.link(e)
    return e


def arena_piece(name, build, seed=5):
    p = Piece(name, seed)
    build(p)
    ob, _ = finish(p, ARENA)
    return ob


arena_piece("Arena_Ground", ground)
arena_piece("Arena_Cauldron_Standin", cauldron_standin)
# Inside today's field (about 15.2 x 16.6 m inside the walls): an open middle for the fight.
for (n, x, y, r) in (("Seesaw_Bone", -3.8, -2.6, 25), ("Seesaw_Bone", 3.0, 3.6, -15), ("Boulder_Slime", 4.8, -1.2, -20),
                     ("Puddle_Slime", -2.6, -5.0, 0), ("Puddle_Slime", 3.2, -4.4, 40), ("Puddle_Slime", 5.6, 2.6, 80),
                     ("Puddle_Slime", -5.8, 1.0, 10), ("Stepping_Stones", -4.8, -6.6, 30), ("Stepping_Stones", 1.2, -7.2, -10),
                     ("Monster_Gate", -9.2, -9.4, 135), ("Monster_Gate", 9.2, -9.4, 225), ("Monster_Gate", -9.6, 9.0, 45),
                     ("Monster_Gate", 9.6, 9.0, 315)):
    place(n, x, y, r)
# The play things round it, just past the field's line.
for (n, x, y, r) in (("Swing_Set", -4.6, 8.4, 8), ("Slide", -0.6, 9.6, 200), ("Dome_CandyCane", 5.6, 8.6, 10),
                     ("Merry_Go_Round", 9.0, 1.8, 0), ("Sandbox_Candy", 9.2, -4.2, 15), ("Rocking_Horse", -9.0, 3.0, -25),
                     ("Jack_In_The_Box", -10.0, -1.8, 15), ("Tin_Robot", -10.4, -4.4, 30), ("Toy_Block", 2.6, 11.0, 20),
                     ("Toy_Block", 3.5, 11.6, -10), ("Toy_Block", -6.6, 11.0, 35), ("Beach_Ball", 3.2, 8.0, 0),
                     ("Statue_Knight", -3.2, -11.6, 0), ("Totem_Pumpkin", 4.4, -11.2, 0), ("Gravestone", 9.8, 6.8, -20),
                     ("Gravestone", 11.4, 4.0, -35), ("Gravestone", -12.0, 8.0, 15), ("Geode_Candy", 11.6, -0.8, 0),
                     ("Geode_Candy", -9.6, -10.2, 40), ("Geode_Candy", 6.8, -12.0, 0), ("Crate", -7.4, -9.6, 15),
                     ("Candy_Basket", -6.4, -10.2, 0), ("Pumpkin_Stack", -11.0, 5.2, 30), ("Pumpkin_Stack", 7.6, -9.2, -20),
                     ("Barrel", 10.8, -6.8, 0), ("Barrel", 12.2, 9.6, 0), ("Clay_Pot", -12.6, 1.4, 0),
                     ("Lollipop_Sign", -1.4, 12.0, 10), ("Lollipop_Sign", 8.2, 11.4, -20), ("Tree_Mushroom", -12.8, -6.4, 0)):
    place(n, x, y, r)
# Today's field line at the front (the walls drop when the cook starts; these show where they stand).
for k, x in enumerate((-7.4, -5.4, -3.4, 1.4, 3.4, 5.4)):
    place("Fence_Picket", x, -8.6 - 0.15 * (k % 2), (k % 3 - 1) * 6)
# The edge: trees, hills.
for (n, x, y, r) in (("Tree_Jelly_A", -14.0, 9.5, 0), ("Tree_Jelly_A", -8.0, 16.0, 40), ("Tree_Jelly_A", 13.5, 13.5, 80),
                     ("Tree_Jelly_B", -16.0, -2.0, 20), ("Tree_Jelly_B", 2.5, 17.0, 0), ("Tree_Jelly_B", 16.5, 4.0, 60),
                     ("Tree_Lantern", -11.5, 13.8, 0), ("Tree_Lantern", 9.0, 15.2, 40), ("Tree_Lantern", 16.8, -7.4, 0),
                     ("Tree_Dead", -19.0, 12.0, 0), ("Tree_Dead", 19.5, 13.0, 90), ("Tree_Dead", -20.0, -9.0, 30),
                     ("Tree_Dead", 21.0, -2.0, 0), ("Bush_Jelly", -13.0, -10.8, 0), ("Bush_Jelly", 12.4, -11.8, 0),
                     ("Bush_Jelly", -3.6, 14.4, 0), ("Stump_Slime", 14.0, -3.6, 0), ("Stump_Slime", -15.6, 4.6, 0),
                     ("Hill_Slime", 0.0, 24.0, 0), ("Hill_Slime", -17.0, 21.0, 35), ("Hill_Slime", 17.0, 21.0, -35),
                     ("Hill_Slime", -25.0, 3.0, 80), ("Hill_Slime", 25.0, 3.0, -80), ("Hill_Slime", -22.0, -14.0, 120),
                     ("Hill_Slime", 22.0, -14.0, -120)):
    place(n, x, y, r)

# Sky, moon, lights.
sky = bpy.data.materials.new("Kit_Sky")
sky.use_nodes = True
nt = sky.node_tree
nt.nodes.clear()
geo = nt.nodes.new("ShaderNodeNewGeometry")
sep = nt.nodes.new("ShaderNodeSeparateXYZ")
mr = nt.nodes.new("ShaderNodeMapRange")
mr.inputs["From Min"].default_value = -6.0
mr.inputs["From Max"].default_value = 95.0
ramp = nt.nodes.new("ShaderNodeValToRGB")
cr = ramp.color_ramp
cr.elements[0].position, cr.elements[0].color = 0.0, (0.95, 0.42, 0.14, 1.0)
cr.elements[1].position, cr.elements[1].color = 1.0, (0.025, 0.02, 0.08, 1.0)
for pos, colr in ((0.05, (0.55, 0.17, 0.22, 1.0)), (0.12, (0.2, 0.08, 0.27, 1.0)), (0.3, (0.07, 0.045, 0.17, 1.0))):
    el = cr.elements.new(pos)
    el.color = colr
emis = nt.nodes.new("ShaderNodeEmission")
emis.inputs["Strength"].default_value = 1.0
sout = nt.nodes.new("ShaderNodeOutputMaterial")
nt.links.new(geo.outputs["Position"], sep.inputs[0])
nt.links.new(sep.outputs["Z"], mr.inputs["Value"])
nt.links.new(mr.outputs[0], ramp.inputs["Fac"])
nt.links.new(ramp.outputs["Color"], emis.inputs["Color"])
nt.links.new(emis.outputs[0], sout.inputs["Surface"])
bpy.ops.mesh.primitive_uv_sphere_add(segments=48, ring_count=24, radius=150.0, location=(0, 0, 0))
dome = bpy.context.active_object
dome.name = "Arena_Sky"
dome.data.materials.append(sky)
dome.visible_shadow = False
for c in dome.users_collection:
    c.objects.unlink(dome)
ARENA.objects.link(dome)

moon_mat = bpy.data.materials.new("Kit_Moon")
moon_mat.use_nodes = True
mn = moon_mat.node_tree
mn.nodes.clear()
me_ = mn.nodes.new("ShaderNodeEmission")
me_.inputs["Color"].default_value = (1.0, 0.6, 0.26, 1.0)
me_.inputs["Strength"].default_value = 1.8
mo = mn.nodes.new("ShaderNodeOutputMaterial")
mn.links.new(me_.outputs[0], mo.inputs["Surface"])
CAM_HIGH = (Vector((0.0, -24.0, 10.5)), Vector((0.0, 5.0, 0.0)))
cam_dir = (CAM_HIGH[1] - CAM_HIGH[0]).normalized()
yaw = math.atan2(cam_dir.x, cam_dir.y) + math.radians(19)
el_ = math.radians(2.6)
moon_at = CAM_HIGH[0] + Vector((math.sin(yaw) * math.cos(el_), math.cos(yaw) * math.cos(el_), math.sin(el_))) * 135.0
bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16, radius=5.5, location=moon_at)
moon = bpy.context.active_object
moon.name = "Arena_Moon"
moon.data.materials.append(moon_mat)
moon.visible_shadow = False
for c in moon.users_collection:
    c.objects.unlink(moon)
ARENA.objects.link(moon)


def sun(name, energy, colour, elev, azim):
    li = bpy.data.objects.new(name, bpy.data.lights.new(name, "SUN"))
    li.data.energy = energy
    li.data.color = colour
    li.data.angle = math.radians(6)
    d = Vector((math.sin(math.radians(azim)) * math.cos(math.radians(elev)), math.cos(math.radians(azim)) * math.cos(math.radians(elev)),
                math.sin(math.radians(elev))))
    li.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
    sc.collection.objects.link(li)
    return li


KEY = sun("Moonlight", 2.4, (1.0, 0.8, 0.6), 48, 30)        # from the back right, warm (the moon's side)
FILL = sun("Night_Fill", 0.9, (0.55, 0.5, 1.0), 35, 200)     # from the front left, cool


def point(name, at, energy, colour, radius=0.3):
    li = bpy.data.objects.new(name, bpy.data.lights.new(name, "POINT"))
    li.data.energy = energy
    li.data.color = colour
    li.data.shadow_soft_size = radius
    li.location = at
    ARENA.objects.link(li)


point("Arena_Cauldron_Light", (0, 0, 1.6), 600.0, (0.45, 1.0, 0.3), 0.4)
for k, (x, y) in enumerate(((-9.2, -9.4), (9.2, -9.4), (-9.6, 9.0), (9.6, 9.0))):
    point("Arena_Gate_Light_%d" % (k + 1), (x * 0.94, y * 0.94, 1.6), 260.0, (0.62, 0.3, 1.0), 0.5)

world = bpy.data.worlds.new("World")
world.use_nodes = True
bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
bg.inputs[0].default_value = (0.05, 0.045, 0.1, 1.0)
bg.inputs[1].default_value = 1.0
sc.world = world

# -------------------------------------------------------------------------------------------------------- report
lines = ["PlayGround kit report (playground_kit.py). Sizes in metres (x wide, y deep, z tall).", ""]
lines.append("%-24s %18s %6s %6s %5s %5s %5s" % ("piece", "size", "faces", "tris", "!quad", "open", "parts"))
for name, size, st in report:
    lines.append("%-24s %5.2f x %4.2f x %4.2f %6d %6d %5d %5d %5d" % (
        name, size.x, size.y, size.z, st["faces"], st["tris"], st["non_quads"], st["open_edges"], st["parts"]))
bad = [n for n, _, st in report if st["non_quads"] or st["open_edges"]]
lines += ["", "All quads and closed: " + ("yes" if not bad else "NO: " + ", ".join(bad))]
print("\n".join(lines))
if RENDERS:
    os.makedirs(RENDERS, exist_ok=True)
    with open(os.path.join(RENDERS, "kit_report.txt"), "w", encoding="utf-8") as fh:
        fh.write("\n".join(lines) + "\n")

# ---------------------------------------------------------------------------------------------------------- save
if OUT:
    bpy.context.preferences.filepaths.save_version = 0      # no .blend1 next to it
    if os.path.exists(OUT) and not FORCE:
        OUT = OUT[:-6] + "_generated.blend"
        print("kept the existing file; this one is", OUT)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=OUT, relative_remap=True)
    bpy.ops.file.make_paths_relative()
    bpy.ops.wm.save_mainfile()
    print("saved", OUT)

# -------------------------------------------------------------------------------------------------------- renders
if RENDERS and not NO_RENDER:
    sc.render.engine = 'BLENDER_EEVEE'
    for key, value in (("taa_render_samples", 48), ("shadow_pool_size", "1024")):
        try:
            setattr(sc.eevee, key, value)
        except Exception as e:
            print("eevee", key, e)
    try:
        sc.view_settings.view_transform = 'Standard'
    except Exception as e:
        print("view transform:", e)
    try:
        g = bpy.data.node_groups.new("Kit_Comp", "CompositorNodeTree")
        g.interface.new_socket("Image", in_out="OUTPUT", socket_type="NodeSocketColor")
        rl = g.nodes.new("CompositorNodeRLayers")
        gl = g.nodes.new("CompositorNodeGlare")
        go = g.nodes.new("NodeGroupOutput")
        for value in ("Bloom", "BLOOM"):
            try:
                gl.inputs["Type"].default_value = value
                break
            except Exception:
                continue
        gl.inputs["Threshold"].default_value = 1.0
        gl.inputs["Strength"].default_value = 0.7
        g.links.new(rl.outputs["Image"], gl.inputs["Image"])
        g.links.new(gl.outputs["Image"], go.inputs[0])
        sc.compositing_node_group = g
        sc.render.use_compositing = True
    except Exception as e:
        print("no bloom:", e)
    cam = bpy.data.objects.new("Kit_Camera", bpy.data.cameras.new("Kit_Camera"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.data.clip_end = 1000.0

    def light(key, fill):
        KEY.data.energy, FILL.data.energy = key, fill

    def shot(name, w, h):
        sc.render.resolution_x, sc.render.resolution_y = w, h
        sc.render.filepath = os.path.join(RENDERS, name)
        bpy.ops.render.render(write_still=True)
        print("render", name)

    def ortho(box, w, h):
        x0, x1, s0, s1 = box
        cam.data.type = 'ORTHO'
        view = Vector((0.0, math.cos(ELEV), -math.sin(ELEV)))
        xc, sm = (x0 + x1) / 2, (s0 + s1) / 2
        target = Vector((xc, sm / math.sin(ELEV), 0.0))
        cam.location = target - view * 150.0
        cam.rotation_euler = (math.pi / 2 - ELEV, 0, 0)
        cam.data.ortho_scale = max(x1 - x0, (s1 - s0) * w / h) * 1.04

    light(3.6, 1.8)
    shot_w, shot_h = 2560, 1600
    ortho(sheet_box, shot_w, shot_h)
    shot("kit_sheet.png", shot_w, shot_h)
    # the two front rows (breakables and toys) close up
    small = [1e9, -1e9, 1e9, -1e9]
    for n, g in groups.items():
        if g["row"] not in ("Breakables", "Toys"):
            continue
        at, lo, hi = g["at"], g["lo"], g["hi"]
        small[0] = min(small[0], at.x + lo.x - 0.4)
        small[1] = max(small[1], at.x + hi.x + 0.4)
        small[2] = min(small[2], (at.y + lo.y - 0.8) * math.sin(ELEV))
        small[3] = max(small[3], (at.y + hi.y) * math.sin(ELEV) + hi.z * math.cos(ELEV))
    ortho(small, 2800, 1000)
    shot("kit_sheet_small.png", 2800, 1000)

    light(2.4, 0.9)
    cam.data.type = 'PERSP'
    cam.data.lens = 22
    cam.location = CAM_HIGH[0]
    cam.rotation_euler = (CAM_HIGH[1] - CAM_HIGH[0]).to_track_quat("-Z", "Y").to_euler()
    shot("arena_concept_angle.png", 1920, 1080)
    eye, look = Vector((-2.0, -8.6, 1.6)), Vector((3.0, 12.0, 2.6))
    cam.data.lens = 16
    cam.location = eye
    cam.rotation_euler = (look - eye).to_track_quat("-Z", "Y").to_euler()
    shot("arena_eye_level.png", 1920, 1080)
print("done")
