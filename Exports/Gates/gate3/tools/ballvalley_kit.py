"""Ball Valley kit (gate 3), 2026-11-23. User: "For each gate create a kit for easy change and fix ... running and
snookering. Feel good and having fun is what I want for players."

Look: Exports/BallValley_Concept/concept_v1.png (approved): a giant haunted pool table in a moonlit pumpkin valley. The
game's real layout wins: a 50 x 25 m lawn, 6 holes (4 corners + the middle of each long side) cut into the rim like a
pool table's pockets, balls 1.6-4 m across, the giant pumpkin ball 5.2 m across (radius 2.6).

Pieces (one collection each, objects named BallValley_Kit_<Piece>, real metres, Blender Z up):
  Rim_Straight_4m  a 4 m run of rounded stones with a root; pivot = the lawn-side face's bottom-left corner: it runs along
                   +X, the lawn is on -Y, the outside on +Y (the game stretches rim pieces a little to fit each wall)
  Rim_End          the stone where a rim run meets a hole's collar; same pivot, its rounded end towards -X (the hole)
  Hole_Corner      a corner pocket: round pit (radius 2.55 m), purple rune ring, a collar of stones round its back with
                   jack-o'-lanterns; pivot = the lawn's corner, the lawn in the -X -Y quarter (turn it for the others)
  Hole_Side        a long side's middle pocket (pit radius 2.36 m); pivot = the middle of the lawn's edge, lawn on -Y
  Lawn_Tile_5m     5 x 5 m of moss lawn, two 2.5 m stripes (the game draws its own lawn: this is its colour)
  Cliff_Straight_4m  the earth edge of the table under the rim, 5 m down; same pivot as the rim
  Tree_Jelly       jelly bubbles on a twisted licorice trunk; pivot = the trunk's foot
  Boulder_Mossy, Gravestone, Lantern_Post, Gazebo_Ruined, Tree_Twisted, Pumpkin_Lantern: pivot = the foot
  Giant_Jack_O_Lantern  the gate's giant pumpkin look (radius 2.6 m); pivot = the ball's middle, face towards -Y
  Ball_Small / Ball_Medium / Ball_Big / Ball_Tail  googly-eyed balls (radius 0.8 / 1.2 / 2.0 / 0.5); body swatch "ball"
                   (white, for a colour tint), pivot = the middle, eyes towards -Y
The user's clean style: every part a closed mesh of quads (no triangles, no open edges), parts separate (no bevels).
One palette for the whole kit (the Exports/Remodel/tools/palette_kit.py method): Exports/Gates/gate3/Palette/
BallValley_Palette.png (each swatch a column, dark at the bottom, light at the top; alpha = smoothness, for Unity's
"Smoothness Source: Albedo Alpha") + BallValley_Palette_Emission.png (the glowing swatches). Faces get their swatch by
script: u = the swatch's column (a small tint shift per part), v = height inside the part (darker low, lighter high).

Collections: BallValley_Kit (the pieces, to export), Sample_Arena (instances laid out like the concept, not exported),
Kit_Sheet (instances in rows for the kit sheet render, not exported), Render_Stuff (lights, camera, moon, ground).

Run (headless):  blender -b --factory-startup --python Exports/Gates/gate3/tools/ballvalley_kit.py -- <out.blend>
                 [renders_dir] [--force]
An existing <out.blend> is never overwritten without --force (once the user edits it, it's theirs): the new file is then
saved as *_generated.blend. Renders: kit_sheet.png, arena_concept_angle.png (compare with the concept),
arena_above.png, arena_eye_level.png (what the player sees)."""
import bpy, bmesh, sys, os, math, random
from mathutils import Vector, Matrix, Euler, noise

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
POS = [a for a in ARGS if not a.startswith("--")]
OUT = POS[0] if POS else None
RENDERS = POS[1] if len(POS) > 1 else None
HERE = os.path.dirname(os.path.abspath(__file__))
GATE3 = os.path.abspath(os.path.join(HERE, ".."))
PAL_DIR = os.path.join(GATE3, "Palette")
KIT = "BallValley_Kit_"

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.unit_settings.system = "METRIC"

# --------------------------------------------------------------------------------------------------------------- palette
# name, dark hex, light hex, glows, smoothness (0 = matte, 1 = glossy)
SWATCHES = [
    ("moss",           "1d4636", "4f9a74", False, 0.10),
    ("moss_dark",      "173a2e", "3c7c5c", False, 0.10),
    ("stone",          "3c3544", "86798f", False, 0.15),
    ("stone_dark",     "28222e", "5a4e64", False, 0.10),
    ("root",           "2a1b14", "6e4a33", False, 0.15),
    ("bark",           "1e1816", "54443c", False, 0.10),
    ("earth",          "2a1d18", "5a4232", False, 0.05),
    ("licorice_red",   "5c0a14", "d02a40", False, 0.60),
    ("licorice_black", "120c10", "3c2a32", False, 0.60),
    ("jelly_pink",     "8c2a62", "ff92d0", False, 0.85),
    ("jelly_green",    "236a3c", "9cf2a8", False, 0.85),
    ("jelly_blue",     "23468c", "8cc8ff", False, 0.85),
    ("jelly_purple",   "4a2a80", "c494ff", False, 0.85),
    ("pumpkin",        "8a2c06", "ff9c34", False, 0.45),
    ("stem",           "26341a", "6e7c34", False, 0.20),
    ("grave",          "343a44", "8a96a6", False, 0.15),
    ("wood",           "3a2518", "8c5c38", False, 0.20),
    ("iron",           "1a1618", "4c444a", False, 0.50),
    ("pit",            "06040a", "2c1a44", False, 0.00),
    ("glow_purple",    "7a2ed0", "e4b4ff", True,  0.50),
    ("glow_lantern",   "ff7a14", "ffe08c", True,  0.50),
    ("glow_face",      "ff9a1a", "fff4a8", True,  0.50),
    ("glow_pit",       "1a0632", "5a2a9a", True,  0.00),
    ("white",          "c6c2cc", "ffffff", False, 0.90),
    ("black",          "08080a", "222226", False, 0.90),
    ("ball_red",       "b01822", "ff6a5c", False, 0.85),
    ("ball_blue",      "1838b0", "6aa4ff", False, 0.85),
    ("ball_yellow",    "c89a0c", "ffec70", False, 0.85),
    ("ball_green",     "18803a", "7ee48e", False, 0.85),
    ("ball_purple",    "5e24a4", "c690ff", False, 0.85),
    ("candy_pink",     "c03080", "ffa0d8", False, 0.80),
    ("ball",           "c8c4c0", "ffffff", False, 0.85),
]
SW, H = 32, 256
W = SW * len(SWATCHES)
INDEX = {s[0]: i for i, s in enumerate(SWATCHES)}


def make_palette(folder, name="BallValley_Palette"):
    import numpy as np
    os.makedirs(folder, exist_ok=True)
    col = np.zeros((H, W, 4)); col[..., 3] = 1
    emi = np.zeros((H, W, 4)); emi[..., 3] = 1
    v = np.linspace(0, 1, H)[:, None]
    v = v * v * (3 - 2 * v) * 0.85 + v * 0.15                       # soft S curve, as palette_kit.py
    hexc = lambda h: np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)])
    for i, (n, dk, lt, glow, smooth) in enumerate(SWATCHES):
        a, b = hexc(dk), hexc(lt)
        x0 = i * SW
        t = (np.arange(SW) + 0.5) / SW
        shift = (t - 0.5)[None, :, None]
        g = a[None, None, :] + (b - a)[None, None, :] * v[:, :, None]
        warm = np.array([1.06, 1.0, 0.92])
        g = g * (1 + 0.10 * shift) * (1 + (warm - 1) * shift * 2)
        col[:, x0:x0 + SW, :3] = np.clip(g, 0, 1)
        col[:, x0:x0 + SW, 3] = smooth
        if glow:
            emi[:, x0:x0 + SW, :3] = np.clip(g, 0, 1)
    paths = []
    for arr, suffix, alpha in ((col, "", True), (emi, "_Emission", False)):
        p = os.path.join(folder, name + suffix + ".png")
        img = bpy.data.images.new(name + suffix, W, H, alpha=alpha)
        img.pixels.foreach_set(arr.astype(np.float32).ravel())
        img.filepath_raw = p
        img.file_format = "PNG"
        img.save()
        bpy.data.images.remove(img)
        paths.append(p)
    return paths


def palette_material(paths, name="BallValley_Palette", glow_strength=1.6):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    tc = nt.nodes.new("ShaderNodeTexImage")
    te = nt.nodes.new("ShaderNodeTexImage")
    inv = nt.nodes.new("ShaderNodeMath")
    inv.operation = "SUBTRACT"
    inv.inputs[0].default_value = 1.0
    tc.image = bpy.data.images.load(paths[0], check_existing=True)
    te.image = bpy.data.images.load(paths[1], check_existing=True)
    tc.image.alpha_mode = "CHANNEL_PACKED"
    tc.interpolation = te.interpolation = "Linear"
    tc.location, te.location, inv.location = (-600, 250), (-600, -150), (-300, 60)
    bsdf.location, out.location = (-50, 100), (300, 100)
    nt.links.new(tc.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(tc.outputs["Alpha"], inv.inputs[1])                  # roughness = 1 - smoothness
    nt.links.new(inv.outputs[0], bsdf.inputs["Roughness"])
    nt.links.new(te.outputs["Color"], bsdf.inputs["Emission Color"])
    bsdf.inputs["Emission Strength"].default_value = glow_strength
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    nt.nodes.active = tc
    return m


PAL = make_palette(PAL_DIR)
MAT = palette_material(PAL)


# --------------------------------------------------------------------------------------------------------------- building
def h01(*a):
    h = 0
    for x in a:
        h = (h * 1103515245 + int(x) * 2654435761 + 12345) & 0x7fffffff
    return (h % 1000) / 1000.0


class Piece:
    """One kit piece being built: closed quad parts in one bmesh, each face tagged with its swatch and part."""

    def __init__(self, name, seed=0):
        self.name = name
        self.bm = bmesh.new()
        self.sw = self.bm.faces.layers.int.new("swatch")
        self.pt = self.bm.faces.layers.int.new("part")
        self.fv = self.bm.faces.layers.float.new("fixed_v")
        # (bmesh hands out a new Python object for an element on every access, so sets of verts can't tell old from
        # new: these stamps do; they're dropped before the mesh is written)
        self.old_v = self.bm.verts.layers.int.new("old")
        self.old_f = self.bm.faces.layers.int.new("old")
        self.parts = 0
        self.rnd = random.Random(seed)

    def snapshot(self):
        for v in self.bm.verts:
            v[self.old_v] = 1
        for f in self.bm.faces:
            f[self.old_f] = 1

    def fresh(self):
        return [v for v in self.bm.verts if not v[self.old_v]], [f for f in self.bm.faces if not f[self.old_f]]

    def new_part(self):
        self.parts += 1
        return self.parts - 1

    def tag(self, faces, swatch, part, smooth=True, fixed_v=-1.0):
        for f in faces:
            f[self.sw] = INDEX[swatch]
            f[self.pt] = part
            f[self.fv] = fixed_v
            f.smooth = smooth

    def quad(self, vs):
        return self.bm.faces.new(vs)


def cube_grid(pc, cuts):
    """A cube from -1 to 1, its faces cut into (cuts + 1)^2 quads: returns its verts and faces."""
    bm = pc.bm
    pc.snapshot()
    bmesh.ops.create_cube(bm, size=2.0)
    new_v, _ = pc.fresh()
    if cuts > 0:
        bm.edges.index_update()
        edges = {e.index: e for v in new_v for e in v.link_edges}
        bmesh.ops.subdivide_edges(bm, edges=list(edges.values()), cuts=cuts, use_grid_fill=True)
    return pc.fresh()


def blob(pc, size, at, cuts=1, round_=0.55, jitter=0.0, seed=0, rot=(0, 0, 0), swatch="stone", top=None,
         top_above=0.6, smooth=True, sink=0.0, base="bottom", shape=None, fixed_v=-1.0):
    """A rounded box / ball: a cut cube pulled towards a sphere (round_ 0 = box, 1 = ball), size = full x y z, jittered
    by smooth noise; at = its bottom middle (base "bottom", sunk by sink) or its middle (base "centre"). top: the swatch
    of its up-facing faces (moss on a stone). shape(p) -> p reshapes the unit ball first (ribs on a pumpkin)."""
    part = pc.new_part()
    verts, faces = cube_grid(pc, cuts)
    off = Vector((seed * 1.37, seed * 0.71, seed * 2.13))
    for v in verts:
        p = v.co.copy()
        q = p.lerp(p.normalized(), round_)
        if shape:
            q = shape(q)
        if jitter:
            q *= 1.0 + jitter * noise.noise(p * 1.1 + off)
        v.co = Vector((q.x * size[0] * 0.5, q.y * size[1] * 0.5, q.z * size[2] * 0.5))
    m = Euler([math.radians(a) for a in rot]).to_matrix()
    for v in verts:
        v.co = m @ v.co
    if base == "bottom":
        lift = -min(v.co.z for v in verts) - sink
    else:
        lift = 0.0
    for v in verts:
        v.co += Vector(at) + Vector((0, 0, lift))
    bmesh.ops.recalc_face_normals(pc.bm, faces=faces)
    pc.tag(faces, swatch, part, smooth, fixed_v)
    if top:
        for f in faces:
            f.normal_update()
            if f.normal.z > top_above:
                f[pc.sw] = INDEX[top]
    return part, faces


def frames(pts):
    """Tangent / normal / binormal along a polyline (parallel transport)."""
    n = len(pts)
    ts = []
    for i in range(n):
        a, b = pts[max(0, i - 1)], pts[min(n - 1, i + 1)]
        ts.append((b - a).normalized())
    ref = Vector((0, 0, 1)) if abs(ts[0].z) < 0.9 else Vector((1, 0, 0))
    nrm = (ref - ts[0] * ref.dot(ts[0])).normalized()
    out = []
    for t in ts:
        nrm = (nrm - t * nrm.dot(t)).normalized()
        out.append((t, nrm, t.cross(nrm)))
    return out


def cap_quads(ring):
    """An even ring closed with quads (a fan of quads from vertex 0)."""
    n = len(ring)
    return [(ring[0], ring[2 * k + 1], ring[2 * k + 2], ring[2 * k + 3]) for k in range((n - 2) // 2)]


def tube(pc, pts, radii, sides=6, swatch="root", twist=0.0, checker=None, smooth=True, caps=True, fixed_v=-1.0):
    """A closed tube along pts (radius per point); checker = a second swatch on every other face, a spiral when twist =
    one side per ring (a licorice stripe)."""
    part = pc.new_part()
    pts = [Vector(p) for p in pts]
    fr = frames(pts)
    rings = []
    for i, (p, (t, nrm, bi)) in enumerate(zip(pts, fr)):
        r = radii[i] if isinstance(radii, (list, tuple)) else radii
        ring = []
        for k in range(sides):
            a = 2 * math.pi * k / sides + twist * i
            ring.append(pc.bm.verts.new(p + (nrm * math.cos(a) + bi * math.sin(a)) * r))
        rings.append(ring)
    faces = []
    for i in range(len(rings) - 1):
        for k in range(sides):
            f = pc.quad((rings[i][k], rings[i][(k + 1) % sides], rings[i + 1][(k + 1) % sides], rings[i + 1][k]))
            faces.append(f)
            f[pc.sw] = INDEX[checker if (checker and (i + k) % 2) else swatch]
            f[pc.pt] = part
            f[pc.fv] = fixed_v
            f.smooth = smooth
    if caps:
        for ring in (rings[0], rings[-1][::-1]):
            for q in cap_quads(ring):
                f = pc.quad(q)
                faces.append(f)
                f[pc.sw] = INDEX[swatch]
                f[pc.pt] = part
                f[pc.fv] = fixed_v
                f.smooth = False
    bmesh.ops.recalc_face_normals(pc.bm, faces=faces)
    return part, faces


def ring_prism(pc, centre, r_in, r_out, z0, z1, segs, swatch, top_swatch=None, a0=0.0, a1=2 * math.pi,
               fixed_top=-1.0, smooth=False):
    """A closed flat ring (an annulus with thickness), or an arc of one (a0..a1)."""
    part = pc.new_part()
    full = abs(a1 - a0 - 2 * math.pi) < 1e-6
    n = segs if full else segs + 1
    c = Vector(centre)
    grid = []
    for k in range(n):
        a = a0 + (a1 - a0) * k / segs
        d = Vector((math.cos(a), math.sin(a), 0))
        grid.append([pc.bm.verts.new(c + d * r + Vector((0, 0, z))) for r, z in ((r_in, z0), (r_out, z0), (r_out, z1), (r_in, z1))])
    faces = []
    steps = segs if full else segs
    for k in range(steps):
        a, b = grid[k], grid[(k + 1) % n]
        for j in range(4):
            f = pc.quad((a[j], b[j], b[(j + 1) % 4], a[(j + 1) % 4]))
            faces.append(f)
            is_top = j == 2
            f[pc.sw] = INDEX[top_swatch if (is_top and top_swatch) else swatch]
            f[pc.pt] = part
            f[pc.fv] = fixed_top if is_top else -1.0
            f.smooth = smooth
    if not full:
        for g in (grid[0], grid[-1][::-1]):
            f = pc.quad(g)
            faces.append(f)
            f[pc.sw] = INDEX[swatch]
            f[pc.pt] = part
            f[pc.fv] = -1.0
    bmesh.ops.recalc_face_normals(pc.bm, faces=faces)
    return part, faces


def prism(pc, outline, z0, z1, swatch, side_swatch=None, smooth=False, xform=None, top_swatch=None, fixed_v=-1.0):
    """A closed prism from an even convex outline (x, y) between heights z0 and z1; xform(Vector) moves it."""
    part = pc.new_part()
    xform = xform or (lambda p: p)
    bot = [pc.bm.verts.new(xform(Vector((x, y, z0)))) for x, y in outline]
    topv = [pc.bm.verts.new(xform(Vector((x, y, z1)))) for x, y in outline]
    faces = []
    for q in cap_quads(bot[::-1]):
        faces.append((pc.quad(q), swatch))
    for q in cap_quads(topv):
        faces.append((pc.quad(q), top_swatch or swatch))
    n = len(outline)
    for k in range(n):
        faces.append((pc.quad((bot[k], bot[(k + 1) % n], topv[(k + 1) % n], topv[k])), side_swatch or swatch))
    for f, s in faces:
        f[pc.sw] = INDEX[s]
        f[pc.pt] = part
        f[pc.fv] = fixed_v
        f.smooth = smooth
    bmesh.ops.recalc_face_normals(pc.bm, faces=[f for f, _ in faces])
    return part


def box(pc, lo, hi, swatch, rot=(0, 0, 0), about=None, **kw):
    """A closed box from lo to hi, turned by rot (degrees) about 'about' (default its middle)."""
    lo, hi = Vector(lo), Vector(hi)
    mid = (lo + hi) * 0.5 if about is None else Vector(about)
    m = Euler([math.radians(a) for a in rot]).to_matrix()
    outline = [(lo.x, lo.y), (hi.x, lo.y), (hi.x, hi.y), (lo.x, hi.y)]
    return prism(pc, outline, lo.z, hi.z, swatch, xform=lambda p: m @ (p - mid) + mid, **kw)


def ladder(pc, cols, depth, swatch, place, side_swatch=None, fixed_v=-1.0, smooth=False):
    """A closed slab from a 'ladder' outline: cols = [(bottom (u, v), top (u, v)), ...] left to right in a flat (u, v)
    face; place(u, v, w) -> Vector puts a point of it in the world (w = 0 front face .. 1 back face)."""
    part = pc.new_part()
    fr = [(pc.bm.verts.new(place(*b, 0.0)), pc.bm.verts.new(place(*t, 0.0))) for b, t in cols]
    bk = [(pc.bm.verts.new(place(*b, 1.0)), pc.bm.verts.new(place(*t, 1.0))) for b, t in cols]
    faces = []
    for i in range(len(cols) - 1):
        faces.append((pc.quad((fr[i][0], fr[i + 1][0], fr[i + 1][1], fr[i][1])), swatch, fixed_v))
        faces.append((pc.quad((bk[i][1], bk[i + 1][1], bk[i + 1][0], bk[i][0])), side_swatch or swatch, -1.0))
        faces.append((pc.quad((fr[i][1], fr[i + 1][1], bk[i + 1][1], bk[i][1])), side_swatch or swatch, -1.0))  # top
        faces.append((pc.quad((bk[i][0], bk[i + 1][0], fr[i + 1][0], fr[i][0])), side_swatch or swatch, -1.0))  # bottom
    for i, s in ((0, -1), (len(cols) - 1, 1)):
        q = (fr[i][0], fr[i][1], bk[i][1], bk[i][0]) if s < 0 else (bk[i][0], bk[i][1], fr[i][1], fr[i][0])
        faces.append((pc.quad(q), side_swatch or swatch, -1.0))
    for f, sw_, fv in faces:
        f[pc.sw] = INDEX[sw_]
        f[pc.pt] = part
        f[pc.fv] = fv
        f.smooth = smooth
    bmesh.ops.recalc_face_normals(pc.bm, faces=[f for f, _, _ in faces])
    return part


def finish(pc, coll):
    """The piece's object: UVs on the palette, the material, checks (all quads, closed)."""
    me = bpy.data.meshes.new(pc.name)
    bm = pc.bm
    bm.normal_update()
    uv = bm.loops.layers.uv.verify()
    rng = {}
    for f in bm.faces:
        p = f[pc.pt]
        for v in f.verts:
            lo, hi = rng.get(p, (1e9, -1e9))
            rng[p] = (min(lo, v.co.z), max(hi, v.co.z))
    for f in bm.faces:
        col, p, fv = f[pc.sw], f[pc.pt], f[pc.fv]
        tint = h01(p, len(pc.name))
        u = (col * SW + 4 + tint * (SW - 8)) / W
        lo, hi = rng[p]
        span = max(hi - lo, 0.05)
        bonus = 0.06 if f.normal.z > 0.7 else 0.0
        for l in f.loops:
            v = fv if fv >= 0 else 0.10 + 0.80 * (l.vert.co.z - lo) / span + bonus
            l[uv].uv = (u, min(0.97, max(0.03, v)))
    tris = sum(len(f.verts) - 2 for f in bm.faces)
    non_quads = sum(1 for f in bm.faces if len(f.verts) != 4)
    open_edges = sum(1 for e in bm.edges if len(e.link_faces) != 2)
    bm.verts.layers.int.remove(pc.old_v)
    bm.faces.layers.int.remove(pc.old_f)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(MAT)
    ob = bpy.data.objects.new(pc.name, me)
    coll.objects.link(ob)
    print("piece %-38s tris %5d  non-quads %d  open edges %d  parts %d" % (pc.name, tris, non_quads, open_edges, pc.parts))
    return ob


def piece_coll(short):
    c = bpy.data.collections.new(KIT + short)
    KIT_COLL.children.link(c)
    return c


KIT_COLL = bpy.data.collections.new("BallValley_Kit")
sc.collection.children.link(KIT_COLL)
PIECES = {}


def make(short, builder, seed=0):
    pc = Piece(KIT + short, seed)
    builder(pc)
    PIECES[short] = finish(pc, piece_coll(short))
    return PIECES[short]


# --------------------------------------------------------------------------------------------------------------- pieces
RIM_H, RIM_D = 1.2, 1.45           # the rim's height over the lawn and its depth (outwards)
PIT_CORNER, PIT_SIDE = 2.55, 2.36  # the game's pit radii (Tuning: caveCornerWidth 5 / caveMiddleWidth 4.5)
PIT_OUT = 0.75                     # the pit's middle sits this far out from the lawn's corner / edge
PIT_DEPTH = 6.0
BAND = 0.32                        # the rune ring's width round a pit


def rim_root(pc, x0, x1, seed):
    """A root along the rim's outer side that climbs over the top once."""
    r = random.Random(seed)
    pts = []
    n = 9
    xm = x0 + (x1 - x0) * r.uniform(0.35, 0.65)
    for i in range(n):
        t = i / (n - 1)
        x = x0 + (x1 - x0) * t
        over = math.exp(-((x - xm) / 0.55) ** 2)                      # 1 where it crosses the top
        y = RIM_D + 0.06 - over * (RIM_D * 0.55)
        z = 0.25 + 0.35 * math.sin(t * math.pi * 1.3 + seed) * (1 - over) + over * (RIM_H + 0.05)
        pts.append((x, y, z))
    rad = [0.11 - 0.05 * abs(i / (n - 1) - 0.5) for i in range(n)]
    tube(pc, pts, rad, sides=6, swatch="root", twist=0.35)


def build_rim(pc):
    lengths, heights = (1.30, 1.28, 1.30), (1.12, 1.24, 1.08)
    x = 0.02
    for i, (L, h) in enumerate(zip(lengths, heights)):
        blob(pc, (L, RIM_D, h), (x + L / 2, RIM_D / 2, 0), cuts=1, round_=0.6, jitter=0.06, seed=11 + i,
             rot=(pc.rnd.uniform(-2, 2), pc.rnd.uniform(-2, 2), pc.rnd.uniform(-3, 3)), swatch="stone",
             top="moss_dark" if i == 1 else None, top_above=0.8, sink=0.08)
        x += L + 0.02
    rim_root(pc, 0.1, 3.9, 3)


def build_rim_end(pc):
    blob(pc, (1.25, RIM_D, 1.16), (0.68, RIM_D / 2, 0), cuts=1, round_=0.75, jitter=0.05, seed=21, swatch="stone",
         sink=0.08)
    blob(pc, (0.7, 0.8, 0.55), (0.15, RIM_D * 0.75, 0), cuts=1, round_=0.8, jitter=0.08, seed=22, swatch="stone_dark",
         sink=0.05)
    tube(pc, [(1.3, RIM_D + 0.05, 0.3), (1.0, RIM_D * 0.7, RIM_H + 0.06), (0.5, RIM_D * 0.4, RIM_H + 0.02),
              (0.05, RIM_D * 0.45, 0.75), (-0.25, RIM_D * 0.5, 0.1)], [0.1, 0.1, 0.09, 0.07, 0.05], sides=6,
         swatch="root", twist=0.3)


def pumpkin_shape(ribs, depth=0.10, squash=0.9, dent=0.18):
    def shape(q):
        d = q.normalized()
        az = math.atan2(d.y, d.x)
        flat = 1.0 - d.z * d.z
        r = 1.0 - depth * (0.5 - 0.5 * math.cos(ribs * az)) ** 1.4 * flat
        p = d * r
        p.z *= squash
        p.z -= dent * math.exp(-((d.x * d.x + d.y * d.y) / 0.06)) * (1 if d.z > 0 else -0.6)
        return p
    return shape


def face_on(r, squash, shape):
    """place(u, v, w) on a pumpkin's front (towards -Y): w 0 = just outside the skin, 1 = 12% inside."""
    def place(u, v, w):
        d = Vector((u / r, -1.0, v / (r * squash))).normalized()
        q = shape(d)
        p = Vector((q.x * r, q.y * r, q.z * r))
        return p * (1.012 - 0.13 * w)
    return place


def jack_o_lantern(pc, r, ribs=12, face=True, stem=True, squash=0.9, cuts=6, seed=0):
    """A ribbed pumpkin (radius r, its middle at the origin) with a glowing carved face towards -Y and a stem."""
    shape = pumpkin_shape(ribs, squash=squash)
    blob(pc, (2 * r, 2 * r, 2 * r), (0, 0, 0), cuts=cuts, round_=1.0, swatch="pumpkin", base="centre", shape=shape,
         seed=seed)
    if face:
        place = face_on(r, squash, shape)
        k = r / 2.6                                                         # the face was drawn on the giant (r 2.6)
        P = lambda pts: [((a[0] * k, a[1] * k), (b[0] * k, b[1] * k)) for a, b in pts]
        eye = [((-1.30, 0.30), (-0.86, 1.12)), ((-0.40, 0.30), (-0.80, 1.12))]
        ladder(pc, P(eye), 1, "glow_face", place, side_swatch="glow_face", fixed_v=0.85)
        ladder(pc, P([((0.40, 0.30), (0.80, 1.12)), ((1.30, 0.30), (0.86, 1.12))]), 1, "glow_face", place,
               side_swatch="glow_face", fixed_v=0.85)
        ladder(pc, P([((-0.24, -0.08), (-0.05, 0.42)), ((0.24, -0.08), (0.05, 0.42))]), 1, "glow_face", place,
               side_swatch="glow_face", fixed_v=0.85)
        cols = []
        n = 9 if r > 1.0 else 5                                             # small lanterns: a simpler grin
        for i in range(n):
            x = -1.65 + 3.3 * i / (n - 1)
            t = x / 1.65
            top = -0.50 + 0.38 * t * t
            bot = -1.18 + 0.62 * t * t
            if (n == 9 and i in (2, 6)) or (n == 5 and i in (1, 3)):
                top -= 0.26                                                  # teeth from the top
            if i == n // 2:
                bot += 0.24                                                  # a tooth from below
            cols.append(((x, bot), (x, top)))
        ladder(pc, P(cols), 1, "glow_face", place, side_swatch="glow_face", fixed_v=0.85)
    if stem:
        top_z = r * squash * (1 - 0.18) + 0.02
        s = r / 2.6
        if r > 1.0:
            tube(pc, [(0, 0, top_z - 0.25 * s), (0.05 * s, 0.02 * s, top_z + 0.35 * s), (0.22 * s, 0.08 * s, top_z + 0.75 * s),
                      (0.5 * s, 0.1 * s, top_z + 0.95 * s)], [0.36 * s, 0.32 * s, 0.26 * s, 0.2 * s], sides=6,
                 swatch="stem", twist=0.4)
        else:
            tube(pc, [(0, 0, top_z - 0.25 * s), (0.1 * s, 0.03 * s, top_z + 0.45 * s), (0.4 * s, 0.08 * s, top_z + 0.85 * s)],
                 [0.38 * s, 0.3 * s, 0.22 * s], sides=4, swatch="stem", twist=0.4)


def place_verts(verts, at, yaw=0.0, scale=1.0):
    rot = Matrix.Rotation(math.radians(yaw), 3, "Z")
    for v in verts:
        v.co = rot @ (v.co * scale) + Vector(at)


def build_pumpkin_lantern(pc, at=(0, 0, 0), r=0.27, yaw=0.0, seed=0):
    """A small jack-o'-lantern sitting on 'at' (its foot), face towards -Y turned by yaw."""
    pc.snapshot()
    jack_o_lantern(pc, r, ribs=6, cuts=2, squash=0.82, seed=seed)
    new, _ = pc.fresh()
    low = min(v.co.z for v in new)
    for v in new:
        v.co.z -= low
    place_verts(new, at, yaw)


def pit(pc, r, centre):
    c = Vector(centre)
    # the pit's wall: a closed tube ring from the lawn down (inside faces the pit), its floor glowing a little
    ring_prism(pc, c, r, r + 0.3, -PIT_DEPTH, -0.02, 24, "pit")
    part = pc.new_part()
    bot = [pc.bm.verts.new(c + Vector((math.cos(a) * r, math.sin(a) * r, -PIT_DEPTH - 0.3)))
           for a in (2 * math.pi * k / 24 for k in range(24))]
    top = [pc.bm.verts.new(c + Vector((math.cos(a) * r, math.sin(a) * r, -PIT_DEPTH + 0.25)))
           for a in (2 * math.pi * k / 24 for k in range(24))]
    faces = [pc.quad(q) for q in cap_quads(bot[::-1])] + [pc.quad(q) for q in cap_quads(top)]
    faces += [pc.quad((bot[k], bot[(k + 1) % 24], top[(k + 1) % 24], top[k])) for k in range(24)]
    pc.tag(faces, "glow_pit", part, smooth=False, fixed_v=0.3)
    bmesh.ops.recalc_face_normals(pc.bm, faces=faces)
    # the rune ring round the rim, on the lawn (no collider in the game: looks only)
    ring_prism(pc, c, r, r + BAND, -0.02, 0.025, 32, "stone_dark", top_swatch="glow_purple", fixed_top=0.45)


def collar(pc, centre, r, a0, a1, lanterns_every=3, seed=0):
    """Stones round the back of a pit from angle a0 to a1 (degrees, counter-clockwise), with jack-o'-lanterns on top."""
    c = Vector(centre)
    rc = r + BAND + 0.08 + RIM_D / 2
    arc = math.radians(a1 - a0) * rc
    n = max(2, round(arc / 1.32))
    step = (a1 - a0) / n
    rnd = random.Random(seed)
    for i in range(n):
        a = math.radians(a0 + step * (i + 0.5))
        d = Vector((math.cos(a), math.sin(a), 0))
        length = 2 * math.sin(math.radians(step) / 2) * rc - 0.04
        h = RIM_H + rnd.uniform(-0.1, 0.1)
        at = c + d * rc
        yaw = math.degrees(a) + 90
        blob(pc, (length, RIM_D, h), (at.x, at.y, 0), cuts=1, round_=0.6, jitter=0.06, seed=seed * 31 + i,
             rot=(rnd.uniform(-2, 2), rnd.uniform(-2, 2), yaw), swatch="stone",
             top="moss_dark" if i % 3 == 1 else None, top_above=0.8, sink=0.08)
        if lanterns_every and i % lanterns_every == 1:                       # facing the pit (and the lawn past it)
            build_pumpkin_lantern(pc, (at.x - d.x * 0.15, at.y - d.y * 0.15, h - 0.12), r=0.26,
                                  yaw=math.degrees(a) - 90, seed=i)
    # one root wound round the collar's back
    pts = []
    for k in range(10):
        t = k / 9
        a = math.radians(a0 + (a1 - a0) * (0.1 + 0.8 * t))
        d = Vector((math.cos(a), math.sin(a), 0))
        rr = rc + RIM_D / 2 + 0.04 - 0.5 * math.exp(-((t - 0.5) / 0.12) ** 2)
        z = 0.3 + 0.3 * math.sin(t * 7 + seed) + (RIM_H - 0.2) * math.exp(-((t - 0.5) / 0.12) ** 2)
        p = c + d * rr
        pts.append((p.x, p.y, z))
    tube(pc, pts, [0.11 - 0.04 * abs(k / 9 - 0.5) for k in range(10)], sides=6, swatch="root", twist=0.3)


def build_hole_corner(pc):
    centre = (PIT_OUT, PIT_OUT, 0)
    pit(pc, PIT_CORNER, centre)
    collar(pc, centre, PIT_CORNER, -88, 178, seed=5)


def build_hole_side(pc):
    centre = (0, PIT_OUT, 0)
    pit(pc, PIT_SIDE, centre)
    collar(pc, centre, PIT_SIDE, 2, 178, seed=9)


def build_lawn_tile(pc):
    part = pc.new_part()
    box(pc, (0, 0, -0.2), (2.5, 5, 0), "moss", fixed_v=0.62)
    box(pc, (2.5, 0, -0.2), (5, 5, 0), "moss_dark", fixed_v=0.62)


def build_cliff(pc):
    """The earth edge under the rim: a slab 4 m long from the lawn's height 5 m down, rough rock bands, roots."""
    part = pc.new_part()
    nx, nz = 6, 5
    off = Vector((7.3, 2.1, 0.4))
    def out(x, z):
        edge = x in (0.0, 4.0)                                              # the ends stay flat so pieces meet
        bulge = 0.0 if edge else 0.28 * noise.noise(Vector((x * 0.9, z * 0.7, 0)) + off)
        return RIM_D * 0.9 + bulge - 0.12 * (z / -5.0)
    front, back = [], []
    for j in range(nz + 1):
        z = -5.0 * j / nz
        zj = z + (0.0 if j in (0, nz) else 0.18 * noise.noise(Vector((j * 1.3, 0.5, 0.2)) + off))
        # rock bands: every other row sticks out a little more (strata, not planks)
        band = 0.16 * (j % 2) if 0 < j < nz else 0.0
        front.append([pc.bm.verts.new((4.0 * i / nx, out(4.0 * i / nx, z) + band * (0 < i < nx), zj))
                      for i in range(nx + 1)])
        back.append([pc.bm.verts.new((4.0 * i / nx, 0.0, zj)) for i in range(nx + 1)])
    faces = []
    for j in range(nz):
        for i in range(nx):
            f = pc.quad((front[j][i], front[j + 1][i], front[j + 1][i + 1], front[j][i + 1]))
            faces.append(f)
            faces.append(pc.quad((back[j][i], back[j][i + 1], back[j + 1][i + 1], back[j + 1][i])))
    for i in range(nx):
        faces.append(pc.quad((back[0][i], front[0][i], front[0][i + 1], back[0][i + 1])))           # top
        faces.append(pc.quad((back[nz][i], back[nz][i + 1], front[nz][i + 1], front[nz][i])))     # bottom
    for j in range(nz):
        faces.append(pc.quad((back[j][0], back[j + 1][0], front[j + 1][0], front[j][0])))
        faces.append(pc.quad((back[j][nx], front[j][nx], front[j + 1][nx], back[j + 1][nx])))
    pc.tag(faces, "earth", part, smooth=False)
    for f in faces:                                                          # every other metre: a darker rock row
        f.normal_update()
        zc = sum(v.co.z for v in f.verts) / 4
        if abs(f.normal.y) > 0.5 and int(-zc) % 2 == 1:
            f[pc.sw] = INDEX["stone_dark"]
    bmesh.ops.recalc_face_normals(pc.bm, faces=faces)
    tube(pc, [(1.2, RIM_D * 0.95, -0.05), (1.35, RIM_D + 0.15, -1.2), (1.1, RIM_D + 0.1, -2.4), (1.4, RIM_D + 0.25, -3.3)],
         [0.1, 0.09, 0.07, 0.04], sides=6, swatch="root", twist=0.3)
    tube(pc, [(3.0, RIM_D * 0.95, -0.05), (2.9, RIM_D + 0.12, -0.9), (3.15, RIM_D + 0.2, -1.8)], [0.08, 0.07, 0.04],
         sides=6, swatch="root", twist=0.3)


def build_tree_jelly(pc):
    pts, rad = [], []
    n = 9
    for i in range(n):
        t = i / (n - 1)
        pts.append((0.18 * math.sin(t * 3.0), 0.12 * math.sin(t * 2.2 + 1), 3.5 * t))
        rad.append(0.34 - 0.1 * t)
    tube(pc, pts, rad, sides=6, swatch="licorice_red", checker="licorice_black", twist=2 * math.pi / 6)
    for k in range(3):
        a = k * 2.1 + 0.4
        d = Vector((math.cos(a), math.sin(a), 0))
        tube(pc, [d * 0.15 + Vector((0, 0, 0.45)), d * 0.55 + Vector((0, 0, 0.12)), d * 0.85 + Vector((0, 0, -0.12))],
             [0.16, 0.11, 0.06], sides=6, swatch="licorice_black", twist=0.3)
    top = Vector(pts[-1])
    bubbles = [((0.0, 0.0, 1.05), 1.18, "jelly_pink"), ((0.95, 0.25, 0.35), 0.86, "jelly_green"),
               ((-0.8, 0.55, 0.45), 0.9, "jelly_blue"), ((-0.2, -0.95, 0.3), 0.82, "jelly_purple"),
               ((0.35, 0.75, 1.75), 0.62, "jelly_green")]
    for i, (o, r, s) in enumerate(bubbles):
        c = top + Vector(o)
        blob(pc, (2 * r, 2 * r, 2 * r), (c.x, c.y, c.z), cuts=3, round_=1.0, swatch=s, base="centre", seed=40 + i,
             jitter=0.03)


def build_boulder(pc):
    blob(pc, (3.0, 2.3, 1.8), (0, 0, 0), cuts=2, round_=0.85, jitter=0.14, seed=7, rot=(0, 0, 12), swatch="stone",
         top="moss", top_above=0.55, sink=0.15)
    blob(pc, (1.0, 0.85, 0.62), (1.65, -0.75, 0), cuts=1, round_=0.75, jitter=0.1, seed=8, swatch="stone_dark",
         top="moss_dark", top_above=0.7, sink=0.06)


def build_gravestone(pc):
    blob(pc, (1.15, 0.7, 0.3), (0, 0.05, 0), cuts=1, round_=0.8, swatch="earth", top="moss_dark", top_above=0.5,
         sink=0.12)
    w, h = 0.4, 1.1
    cols = []
    arch = 5
    for i in range(arch + 1):
        a = math.pi - math.pi * i / arch                                     # left to right over the top
        x = math.cos(a) * w
        top = h - 0.42 + math.sin(a) * 0.42
        cols.append(((x, -0.1), (x, top)))
    tilt = Matrix.Rotation(math.radians(-9), 3, "X") @ Matrix.Rotation(math.radians(7), 3, "Y")
    turn = Matrix.Rotation(math.radians(8), 3, "Z")
    ladder(pc, cols, 1, "grave", lambda u, v, wd: turn @ (tilt @ Vector((u, -0.1 + 0.2 * wd, v))) + Vector((0, 0.05, 0)))


def build_lantern_post(pc):
    blob(pc, (0.62, 0.62, 0.32), (0, 0, 0), cuts=1, round_=0.7, swatch="stone_dark", sink=0.08)
    post = [(0.0, 0.0, 0.1), (0.05, 0.02, 0.9), (-0.04, 0.03, 1.8), (0.03, 0.0, 2.55)]
    tube(pc, post, [0.11, 0.1, 0.09, 0.08], sides=6, swatch="wood", twist=0.2, smooth=False)
    tube(pc, [(0.03, 0.0, 2.5), (0.3, 0.0, 2.68), (0.62, 0.0, 2.62), (0.7, 0.0, 2.48)], [0.06, 0.055, 0.05, 0.045],
         sides=6, swatch="iron", smooth=False)
    box(pc, (0.68, -0.025, 2.2), (0.72, 0.025, 2.48), "iron")                                   # the hanger
    box(pc, (0.55, -0.15, 1.78), (0.85, 0.15, 2.2), "glow_lantern", fixed_v=0.8)                 # the glass
    box(pc, (0.52, -0.18, 1.72), (0.88, 0.18, 1.8), "iron")                                      # bottom
    box(pc, (0.52, -0.18, 2.18), (0.88, 0.18, 2.26), "iron")                                     # top
    for sx in (-1, 1):
        for sy in (-1, 1):
            box(pc, (0.7 + sx * 0.165 - 0.025, sy * 0.165 - 0.025, 1.8), (0.7 + sx * 0.165 + 0.025, sy * 0.165 + 0.025, 2.18),
                "iron")


def hexagon(r, rot=0.0):
    return [(math.cos(math.radians(rot + 60 * k)) * r, math.sin(math.radians(rot + 60 * k)) * r) for k in range(6)]


def build_gazebo(pc):
    prism(pc, hexagon(2.45, 30), -0.1, 0.22, "stone_dark", top_swatch="stone")
    prism(pc, hexagon(2.15, 30), 0.22, 0.42, "stone", top_swatch="stone")
    tops = []
    for k in range(6):
        a = math.radians(30 + 60 * k)
        c = Vector((math.cos(a) * 1.8, math.sin(a) * 1.8, 0))
        if k == 4:                                                           # fallen: a drum lying on the floor
            box(pc, (c.x - 0.9, c.y - 0.15, 0.42), (c.x + 0.4, c.y + 0.15, 0.72), "stone", rot=(0, 0, 25))
            continue
        h = 1.35 if k == 2 else 2.65                                         # one broken short
        box(pc, (c.x - 0.15, c.y - 0.15, 0.42), (c.x + 0.15, c.y + 0.15, 0.42 + h), "stone",
            rot=(0, 6 if k == 2 else 0, 0))
        box(pc, (c.x - 0.22, c.y - 0.22, 0.42 + h), (c.x + 0.22, c.y + 0.22, 0.42 + h + 0.16), "stone_dark",
            rot=(0, 6 if k == 2 else 0, 0))
        tops.append(k)
    # the ring beam between whole columns, and the roof (tilted: it slipped when a column fell)
    z = 0.42 + 2.65 + 0.16
    for k in (5, 0, 1):
        a0, a1 = math.radians(30 + 60 * k), math.radians(30 + 60 * (k + 1))
        p0 = Vector((math.cos(a0), math.sin(a0), 0)) * 1.8
        p1 = Vector((math.cos(a1), math.sin(a1), 0)) * 1.8
        mid, d = (p0 + p1) / 2, (p1 - p0)
        L = d.length
        yaw = math.degrees(math.atan2(d.y, d.x))
        box(pc, (mid.x - L / 2, mid.y - 0.16, z), (mid.x + L / 2, mid.y + 0.16, z + 0.3), "stone", rot=(0, 0, yaw))
    roof = Matrix.Rotation(math.radians(-7), 3, "X") @ Matrix.Rotation(math.radians(5), 3, "Y")
    rz = z + 0.3
    part = pc.new_part()
    outer = [pc.bm.verts.new(roof @ Vector((x, y, 0)) + Vector((0.15, 0.25, rz))) for x, y in hexagon(2.35)]
    inner = [pc.bm.verts.new(roof @ Vector((x, y, 0.2)) + Vector((0.15, 0.25, rz))) for x, y in hexagon(1.9)]
    peak = [pc.bm.verts.new(roof @ Vector((x, y, 1.45)) + Vector((0.15, 0.25, rz))) for x, y in hexagon(0.35)]
    under = [pc.bm.verts.new(roof @ Vector((x, y, -0.18)) + Vector((0.15, 0.25, rz))) for x, y in hexagon(2.35)]
    under_in = [pc.bm.verts.new(roof @ Vector((x, y, -0.18)) + Vector((0.15, 0.25, rz))) for x, y in hexagon(0.9)]
    faces = []
    for k in range(6):
        k1 = (k + 1) % 6
        faces.append(pc.quad((outer[k], outer[k1], inner[k1], inner[k])))
        faces.append(pc.quad((inner[k], inner[k1], peak[k1], peak[k])))
        faces.append(pc.quad((under[k], under[k1], outer[k1], outer[k])))
        faces.append(pc.quad((under_in[k1], under_in[k], under[k], under[k1])))
    faces += [pc.quad(q) for q in cap_quads(peak)] + [pc.quad(q) for q in cap_quads(under_in[::-1])]
    pc.tag(faces, "stone_dark", part, smooth=False)
    bmesh.ops.recalc_face_normals(pc.bm, faces=faces)
    tip = roof @ Vector((0, 0, 1.45)) + Vector((0.15, 0.25, rz))
    blob(pc, (0.5, 0.5, 0.6), (tip.x, tip.y, tip.z - 0.02), cuts=1, round_=0.8, swatch="stone")
    tube(pc, [(-2.2, -1.0, 0.1), (-1.6, -1.25, 0.5), (-1.55, -1.3, 1.6), (-1.45, -1.25, 2.4)], [0.1, 0.08, 0.07, 0.04],
         sides=6, swatch="root", twist=0.3)


def build_tree_twisted(pc):
    pts, rad = [], []
    n = 9
    for i in range(n):
        t = i / (n - 1)
        pts.append((0.6 * math.sin(t * 2.6) * t, 0.35 * math.sin(t * 4 + 1) * t, 4.2 * t))
        rad.append(0.42 * (1 - t) + 0.1)
    tube(pc, pts, rad, sides=6, swatch="bark", twist=0.25, smooth=False)
    for k, (h, a, L) in enumerate(((2.4, 0.3, 1.6), (3.1, 2.5, 1.4), (3.7, 4.3, 1.1), (4.15, 1.4, 0.9))):
        base = Vector(pts[min(n - 1, int(h / 4.2 * (n - 1)))])
        d = Vector((math.cos(a), math.sin(a), 0))
        br = [base, base + d * L * 0.45 + Vector((0, 0, 0.35)), base + d * L * 0.85 + Vector((0, 0, 0.85)),
              base + d * L * 0.95 + d.cross(Vector((0, 0, 1))) * 0.3 + Vector((0, 0, 1.25))]
        tube(pc, br, [0.13, 0.09, 0.06, 0.03], sides=6, swatch="bark", twist=0.3, smooth=False)
    for k in range(3):
        a = k * 2.1 + 1.0
        d = Vector((math.cos(a), math.sin(a), 0))
        tube(pc, [d * 0.2 + Vector((0, 0, 0.5)), d * 0.75 + Vector((0, 0, 0.15)), d * 1.15 + Vector((0, 0, -0.15))],
             [0.2, 0.13, 0.06], sides=6, swatch="bark", twist=0.3, smooth=False)


def build_giant(pc):
    jack_o_lantern(pc, 2.6, ribs=12, cuts=7, squash=0.92)


def build_ball(r, eyes=True):
    def b(pc):
        blob(pc, (2 * r, 2 * r, 2 * r), (0, 0, 0), cuts=4 if eyes else 3, round_=1.0, swatch="ball", base="centre")
        for sx in ((-1, 1) if eyes else ()):
            e = Vector((sx * 0.34 * r, -0.86 * r, 0.32 * r))
            er = 0.3 * r
            blob(pc, (2 * er, 2 * er, 2 * er), (e.x, e.y, e.z), cuts=2, round_=1.0, swatch="white", base="centre")
            p = e + Vector((sx * 0.05 * r, -0.86 * er, -0.04 * r))
            blob(pc, (1.1 * er, 0.45 * er, 1.1 * er), (p.x, p.y, p.z), cuts=1, round_=1.0, swatch="black",
                 base="centre")
    return b


def build_candy(pc):
    blob(pc, (0.3, 0.18, 0.18), (0, 0, 0.09), cuts=1, round_=0.85, swatch="candy_pink", base="centre")
    for sx in (-1, 1):
        tube(pc, [(sx * 0.13, 0, 0.09), (sx * 0.26, 0, 0.09)], [0.04, 0.1], sides=4, swatch="candy_pink", smooth=False)


make("Rim_Straight_4m", build_rim, 1)
make("Rim_End", build_rim_end, 2)
make("Hole_Corner", build_hole_corner, 3)
make("Hole_Side", build_hole_side, 4)
make("Lawn_Tile_5m", build_lawn_tile, 5)
make("Cliff_Straight_4m", build_cliff, 6)
make("Tree_Jelly", build_tree_jelly, 7)
make("Boulder_Mossy", build_boulder, 8)
make("Gravestone", build_gravestone, 9)
make("Lantern_Post", build_lantern_post, 10)
make("Gazebo_Ruined", build_gazebo, 11)
make("Tree_Twisted", build_tree_twisted, 12)
make("Pumpkin_Lantern", lambda pc: build_pumpkin_lantern(pc), 13)
make("Giant_Jack_O_Lantern", build_giant, 14)
make("Ball_Small", build_ball(0.8), 15)
make("Ball_Medium", build_ball(1.2), 16)
make("Ball_Big", build_ball(2.0), 17)
make("Ball_Tail", build_ball(0.5, eyes=False), 18)
make("Candy", build_candy, 19)                     # sample arena only (the game has its own candies)
PIECES["Candy"].users_collection[0]["export"] = False

# --------------------------------------------------------------------------------------------------------------- sample arena
ARENA = bpy.data.collections.new("Sample_Arena")
sc.collection.children.link(ARENA)
HX, HZ = 25.0, 12.5                                # the game's lawn half sizes (Blender X = the long side, Y = the short)


def inst(short, loc, yaw=0.0, coll=None, scale=(1, 1, 1), name=None, data=None):
    src = PIECES[short]
    ob = bpy.data.objects.new(name or (short + "_inst"), data or src.data)
    (coll or ARENA).objects.link(ob)
    ob.location = loc
    ob.rotation_euler = (0, 0, math.radians(yaw))
    ob.scale = scale
    return ob


def ball_copy(short, swatch):
    """A copy of a ball piece with its body on a colour swatch (the game tints the white body per ball)."""
    me = PIECES[short].data.copy()
    me.name = short + "_" + swatch
    uv = me.uv_layers.active.data
    attr = me.attributes["swatch"].data
    src = INDEX["ball"]
    for poly in me.polygons:
        if attr[poly.index].value != src:
            continue
        for li in poly.loop_indices:
            u, v = uv[li].uv
            col = int(u * W // SW)
            uv[li].uv = ((INDEX[swatch] * SW + (u * W - col * SW)) / W, v)
    return me


PITS = [(Vector((sx * (HX + PIT_OUT), sy * (HZ + PIT_OUT), 0)), PIT_CORNER) for sx in (1, -1) for sy in (1, -1)] + \
       [(Vector((0, sy * (HZ + PIT_OUT), 0)), PIT_SIDE) for sy in (1, -1)]


def sample_lawn():
    """The lawn as the game draws it (2.5 m stripes, the pits cut out): sample only, the game makes its own."""
    pc = Piece("Sample_Lawn")
    part = pc.new_part()
    cell = 0.25
    nx, ny = int(2 * HX / cell), int(2 * HZ / cell)
    grid = [[pc.bm.verts.new((-HX + i * cell, -HZ + j * cell, 0)) for j in range(ny + 1)] for i in range(nx + 1)]
    for i in range(nx):
        for j in range(ny):
            mid = Vector((-HX + (i + 0.5) * cell, -HZ + (j + 0.5) * cell, 0))
            if any((mid - c).length < r for c, r in PITS):
                continue
            f = pc.quad((grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]))
            stripe = int((mid.x + HX) // 2.5) % 2
            pc.tag([f], "moss" if stripe else "moss_dark", part, smooth=False, fixed_v=0.62)
    loose = [v for v in pc.bm.verts if not v.link_faces]
    bmesh.ops.delete(pc.bm, geom=loose, context="VERTS")
    ob = finish(pc, ARENA)
    return ob


sample_lawn()

# holes: corners (the piece's lawn is its -X -Y quarter) and the long sides' middles (the piece's lawn is its -Y)
corner_yaw = {(1, 1): 0, (-1, 1): 90, (-1, -1): 180, (1, -1): 270}
for (sx, sy), yaw in corner_yaw.items():
    inst("Hole_Corner", (sx * HX, sy * HZ, 0), yaw)
for sy, yaw in ((1, 0), (-1, 180)):
    inst("Hole_Side", (0, sy * HZ, 0), yaw)


def run(a, b, outward):
    """Rim along the lawn edge from a to b (outward = which side is outside): an end stone at each hole, 4 m pieces
    stretched to fill the rest; the cliff under it."""
    a, b = Vector((a[0], a[1], 0)), Vector((b[0], b[1], 0))
    d = (b - a)
    L = d.length
    d.normalize()
    yaw = math.degrees(math.atan2(d.y, d.x))
    out = Vector((outward[0], outward[1], 0))
    # the piece's +Y must point outward: if d x Z points inward, the piece runs the other way round
    if d.cross(Vector((0, 0, 1))).dot(out) > 0:
        a, b, d = b, a, -d
        yaw = math.degrees(math.atan2(d.y, d.x))
    end = 1.3
    inst("Rim_End", a, yaw)
    inst("Rim_End", b, yaw + 180).location = b + out * RIM_D
    mid = L - 2 * end
    n = max(1, round(mid / 4.0))
    s = mid / (4.0 * n)
    for k in range(n):
        inst("Rim_Straight_4m", a + d * (end + 4.0 * s * k), yaw, scale=(s, 1, 1))
    m = max(1, round(L / 4.0))
    for k in range(m):
        inst("Cliff_Straight_4m", a + d * (L / m * k), yaw, scale=(L / m / 4.0, 1, 1))


def collar_cliffs(centre, r, a0, a1):
    """Cliff pieces round the back of a hole's collar, so the table reads as one block from outside."""
    c = Vector(centre)
    rc = r + BAND + 0.08 + 0.1
    n = max(2, round(math.radians(a1 - a0) * (rc + RIM_D) / 2.0))
    step = (a1 - a0) / n
    for i in range(n):
        a = math.radians(a0 + step * i)
        b = math.radians(a0 + step * (i + 1))
        p0 = c + Vector((math.cos(a), math.sin(a), 0)) * rc
        p1 = c + Vector((math.cos(b), math.sin(b), 0)) * rc
        d = p0 - p1                                                      # clockwise, so the piece's +Y points out
        yaw = math.degrees(math.atan2(d.y, d.x))
        inst("Cliff_Straight_4m", p1, yaw, scale=(d.length / 4.0 + 0.05, 1, 1))


reach_c = PIT_CORNER + BAND + 0.08 + RIM_D + PIT_OUT - 0.25          # where a corner collar ends along an edge
reach_s = PIT_SIDE + BAND + 0.08 + RIM_D - 0.25
for sy in (1, -1):
    run((-HX + reach_c, sy * HZ), (-reach_s, sy * HZ), (0, sy))
    run((reach_s, sy * HZ), (HX - reach_c, sy * HZ), (0, sy))
for sx in (1, -1):
    run((sx * HX, -HZ + reach_c), (sx * HX, HZ - reach_c), (sx, 0))
for (sx, sy), yaw in corner_yaw.items():
    collar_cliffs((sx * (HX + PIT_OUT), sy * (HZ + PIT_OUT), 0), PIT_CORNER, -88 + yaw, 178 + yaw)
for sy, yaw in ((1, 0), (-1, 180)):
    collar_cliffs((0, sy * (HZ + PIT_OUT), 0), PIT_SIDE, 2 + yaw, 178 + yaw)
CAM = Vector((-19, -42, 21))                                          # the concept's angle (the render below)
CAM_AT = Vector((4.5, 8.0, -3.0))

# inside the lawn: trees, boulders, graves (open lawn between them), balls, candies
for i, (x, y, yaw) in enumerate(((-15.5, 6.5, 20), (11.0, 7.5, 140), (17.5, -3.0, 70), (-8.0, -6.5, 200), (2.0, 2.5, 300))):
    inst("Tree_Jelly", (x, y, 0), yaw)
for i, (x, y, yaw) in enumerate(((-18.0, -4.0, 10), (6.5, -7.5, 75), (-3.5, 8.0, 160), (14.0, 3.0, 250),
                                  (-11.5, 2.0, 300), (20.0, 7.0, 40))):
    inst("Boulder_Mossy", (x, y, 0), yaw, scale=(0.8 + 0.1 * (i % 3),) * 3)
for x, y, yaw in ((-13.0, 8.8, 190), (8.0, 9.0, 160), (-20.0, 1.0, 100)):
    inst("Gravestone", (x, y, 0), yaw)
balls = [("Ball_Medium", "ball_red", (-19.0, -8.0)), ("Ball_Small", "ball_blue", (-6.5, 1.0)),
         ("Ball_Small", "ball_yellow", (4.5, -1.5)), ("Ball_Medium", "ball_green", (-10.0, -2.0)),
         ("Ball_Small", "ball_purple", (12.0, -5.5)), ("Ball_Small", "ball", (7.5, 3.5)),
         ("Ball_Big", "ball_blue", (-1.0, -6.0)), ("Ball_Small", "ball_red", (16.0, 5.0)),
         ("Ball_Medium", "ball_purple", (-14.5, -6.5)), ("Ball_Small", "ball_green", (19.5, -8.5)),
         ("Ball_Small", "ball_yellow", (-21.0, 7.0)), ("Ball_Medium", "ball", (-2.5, 4.5)),
         ("Ball_Small", "ball_red", (10.5, -9.5)), ("Ball_Small", "ball_blue", (-16.5, 3.5)),
         ("Ball_Medium", "ball_yellow", (1.5, -10.0))]
meshes = {}
for i, (piece, colour, (x, y)) in enumerate(balls):
    key = (piece, colour)
    if key not in meshes:
        meshes[key] = ball_copy(piece, colour)
    r = {"Ball_Small": 0.8, "Ball_Medium": 1.2, "Ball_Big": 2.0}[piece]
    to_cam = CAM - Vector((x, y, 0))
    face = math.degrees(math.atan2(to_cam.x, -to_cam.y)) + 35 * math.sin(i * 1.7)   # eyes (-Y) roughly at the camera
    inst(piece, (x, y, r), face, data=meshes[key], name="Ball_%02d" % i)
    if i in (0, 6, 9):                                                  # tails, like the concept's snakes
        tail_mesh = meshes.setdefault(("Ball_Tail", colour), ball_copy("Ball_Tail", colour))
        d = Vector((math.cos(math.radians(face + 90)), math.sin(math.radians(face + 90)), 0))   # behind the ball
        for k in range(4):
            p = Vector((x, y, 0)) + d * (r + 0.55 + 1.05 * k) + Vector((0, 0.35 * math.sin(k * 1.3), 0))
            inst("Ball_Tail", (p.x, p.y, 0.5), face, data=tail_mesh, name="Tail_%02d_%d" % (i, k))
rnd = random.Random(4)
candy_cols = ["candy_pink", "ball_yellow", "ball_green", "ball_blue", "ball_red", "ball_purple"]
cmeshes = {}
for k in range(30):
    x, y = rnd.uniform(-22, 22), rnd.uniform(-10.5, 10.5)
    col = candy_cols[k % len(candy_cols)]
    if col not in cmeshes:
        me = PIECES["Candy"].data.copy()
        uvd = me.uv_layers.active.data
        for li in range(len(uvd)):
            u, v = uvd[li].uv
            uvd[li].uv = ((INDEX[col] * SW + (u * W - int(u * W // SW) * SW)) / W, v)
        cmeshes[col] = me
    inst("Candy", (x, y, 0.15), rnd.uniform(0, 360), data=cmeshes[col], name="Candy_%02d" % k)
inst("Giant_Jack_O_Lantern", (19.0, 4.5, 2.6), -35)

# outside the table
for x, y, yaw in ((-HX - 3.5, -HZ + 3, 0), (HX + 3.5, -HZ - 3.0, 0), (-8.0, HZ + 4.0, 0), (12.0, -HZ - 4.0, 0)):
    inst("Lantern_Post", (x, y, -0.05), yaw)
inst("Gazebo_Ruined", (HX - 3.0, -HZ - 9.0, -0.3), 15)
for x, y in ((-HX - 4.0, HZ + 3.0), (HX + 4.5, HZ + 2.0), (-HX - 5.0, -HZ - 3.0)):
    inst("Tree_Twisted", (x, y, -0.2), rnd.uniform(0, 360))
for k in range(22):
    a = rnd.uniform(0, 2 * math.pi)
    p = Vector((math.cos(a) * rnd.uniform(34, 48), math.sin(a) * rnd.uniform(22, 30), -5.0))
    inst("Pumpkin_Lantern", p, rnd.uniform(0, 360), scale=(1.6,) * 3)

# --------------------------------------------------------------------------------------------------------------- kit sheet
# The pieces themselves stand in rows away from the arena (the sheet render shows them). Their own pivot is what the
# export uses (each piece is exported at the origin), so moving them here changes nothing in the game.
SHEET = bpy.data.collections.new("Kit_Sheet")
sc.collection.children.link(SHEET)
SX, SY = 0.0, -70.0
rows = [["Rim_Straight_4m", "Rim_End", "Hole_Corner", "Hole_Side", "Lawn_Tile_5m", "Cliff_Straight_4m"],
        ["Tree_Jelly", "Boulder_Mossy", "Gravestone", "Lantern_Post", "Gazebo_Ruined", "Tree_Twisted"],
        ["Pumpkin_Lantern", "Giant_Jack_O_Lantern", "Ball_Small", "Ball_Medium", "Ball_Big", "Ball_Tail", "Candy"]]
gaps = {"Hole_Corner": 11, "Hole_Side": 10, "Gazebo_Ruined": 7, "Giant_Jack_O_Lantern": 8, "Lawn_Tile_5m": 7.5,
        "Cliff_Straight_4m": 6, "Rim_Straight_4m": 6.5, "Tree_Twisted": 6, "Rim_End": 4, "Ball_Big": 6}
y = SY
for row in rows:
    x = SX
    for nm in row:
        z = {"Ball_Small": 0.8, "Ball_Medium": 1.2, "Ball_Big": 2.0, "Ball_Tail": 0.5, "Giant_Jack_O_Lantern": 2.6}.get(nm, 0.0)
        PIECES[nm].location = (x, y, z)
        x += gaps.get(nm, 4.5)
    y += 14.0
ground = bpy.data.meshes.new("Sheet_Ground")
gb = bmesh.new()
bmesh.ops.create_grid(gb, x_segments=1, y_segments=1, size=60)
gb.to_mesh(ground)
gb.free()
g = bpy.data.objects.new("Sheet_Ground", ground)
SHEET.objects.link(g)
g.location = (SX + 27, SY + 12, -0.21)
gm = bpy.data.materials.new("Sheet_Grey")
gm.use_nodes = True
next(n for n in gm.node_tree.nodes if n.type == "BSDF_PRINCIPLED").inputs["Base Color"].default_value = (0.08, 0.08, 0.09, 1)
ground.materials.append(gm)

# --------------------------------------------------------------------------------------------------------------- render stuff
STUFF = bpy.data.collections.new("Render_Stuff")
sc.collection.children.link(STUFF)


def light(name, kind, energy, colour, loc=(0, 0, 0), rot=(0, 0, 0), size=0.5):
    ld = bpy.data.lights.new(name, kind)
    ld.energy = energy
    ld.color = colour
    if kind == "POINT":
        ld.shadow_soft_size = size
    ob = bpy.data.objects.new(name, ld)
    STUFF.objects.link(ob)
    ob.location = loc
    ob.rotation_euler = tuple(math.radians(a) for a in rot)
    return ob


moon_key = light("Moonlight", "SUN", 1.25, (0.55, 0.66, 1.0), rot=(42, 0, 200))
moon_rim = light("Moon_Glow", "SUN", 0.9, (1.0, 0.5, 0.22), rot=(62, 0, 15))
for k, (c, r) in enumerate(PITS):
    light("Hole_Light_%d" % k, "POINT", 2500, (1.0, 0.5, 0.18), (c.x, c.y, 2.4), size=1.5)
    light("Pit_Glow_%d" % k, "POINT", 250, (0.6, 0.25, 1.0), (c.x, c.y, -2.5), size=1.0)
# the valley floor below the table, hills, the moon
floor = bpy.data.meshes.new("Valley_Floor")
fb = bmesh.new()
bmesh.ops.create_grid(fb, x_segments=1, y_segments=1, size=220)
fb.to_mesh(floor)
fb.free()
fo = bpy.data.objects.new("Valley_Floor", floor)
STUFF.objects.link(fo)
fo.location = (0, 0, -5.0)
fm = bpy.data.materials.new("Valley_Floor_Mat")
fm.use_nodes = True
fbsdf = next(n for n in fm.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
fbsdf.inputs["Base Color"].default_value = (0.008, 0.014, 0.018, 1)
fbsdf.inputs["Roughness"].default_value = 1.0
floor.materials.append(fm)
hill_pc = Piece("Hills")
for k in range(11):
    a = math.radians(-25 + k * 23)
    c = (math.cos(a) * 150 + 10, math.sin(a) * 120 + 60, -6)
    blob(hill_pc, (90, 60, 22 + 7 * (k % 3)), c, cuts=3, round_=1.0, jitter=0.08, seed=k, swatch="stone_dark", sink=10,
         fixed_v=0.25)
hills = finish(hill_pc, STUFF)
moon_me = bpy.data.meshes.new("Moon")
mb = bmesh.new()
bmesh.ops.create_uvsphere(mb, u_segments=24, v_segments=12, radius=13)
mb.to_mesh(moon_me)
mb.free()
moon = bpy.data.objects.new("Moon", moon_me)
STUFF.objects.link(moon)
moon.visible_shadow = False
_d = (CAM_AT - CAM).normalized()
_flat = Vector((_d.x, _d.y, 0)).normalized()
moon.location = CAM + (_flat * math.cos(math.radians(3.5)) + Vector((0, 0, math.sin(math.radians(3.5))))) * 260
moon.scale = (1.5, 1.5, 1.5)
mm = bpy.data.materials.new("Moon_Mat")
mm.use_nodes = True
mn = next(n for n in mm.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
mn.inputs["Base Color"].default_value = (1.0, 0.45, 0.12, 1)
mn.inputs["Emission Color"].default_value = (1.0, 0.38, 0.08, 1)
mn.inputs["Emission Strength"].default_value = 1.4
moon_me.materials.append(mm)
world = bpy.data.worlds.new("Night")
world.use_nodes = True
bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
bg.inputs[0].default_value = (0.03, 0.02, 0.08, 1.0)
bg.inputs[1].default_value = 1.0
sc.world = world
for c in (SHEET, ARENA, STUFF):
    c["export"] = False

cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
STUFF.objects.link(cam)
sc.camera = cam

if OUT:
    if os.path.exists(OUT) and "--force" not in ARGS:
        OUT = OUT[:-6] + "_generated.blend"
        print("kept the existing file; this one is", OUT)
    os.makedirs(os.path.dirname(os.path.abspath(OUT)), exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0                    # no .blend1 copy next to it
    bpy.ops.wm.save_as_mainfile(filepath=OUT)
    try:
        bpy.ops.file.make_paths_relative()
        bpy.ops.wm.save_mainfile()
    except RuntimeError as e:
        print("paths kept absolute:", e)
    print("saved", OUT)

if RENDERS:
    os.makedirs(RENDERS, exist_ok=True)
    sc.render.engine = "BLENDER_EEVEE"
    try:
        sc.view_settings.view_transform = "Standard"
    except TypeError:
        pass
    sc.render.resolution_x, sc.render.resolution_y = 1600, 900
    try:
        sc.eevee.taa_render_samples = 32
    except AttributeError:
        pass

    def show(*colls):
        for c in (SHEET, ARENA, STUFF, KIT_COLL):
            c.hide_render = c not in colls

    def shot(name, loc, target, lens):
        cam.data.lens = lens
        cam.data.clip_end = 1000
        cam.location = loc
        cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = os.path.join(RENDERS, name)
        bpy.ops.render.render(write_still=True)
        print("render", name)

    # the kit sheet: neutral light, grey ground
    show(SHEET, STUFF, KIT_COLL)
    for o in STUFF.objects:
        o.hide_render = o.name not in ("Cam",)
    sheet_sun = light("Sheet_Sun", "SUN", 3.2, (1, 0.97, 0.92), rot=(50, 0, 30))
    sheet_fill = light("Sheet_Fill", "SUN", 1.2, (0.75, 0.8, 1.0), rot=(60, 0, 210))
    bg.inputs[0].default_value = (0.16, 0.16, 0.18, 1.0)
    shot("kit_sheet.png", (SX + 26, SY - 36, 30), (SX + 26, SY + 14, 0.5), 32)
    sheet_sun.hide_render = sheet_fill.hide_render = True
    for o in STUFF.objects:
        o.hide_render = o.name.startswith("Sheet_")
    bg.inputs[0].default_value = (0.03, 0.02, 0.08, 1.0)
    # the arena from the concept's angle, and from the player's eyes
    show(ARENA, STUFF)
    shot("arena_concept_angle.png", tuple(CAM), tuple(CAM_AT), 19)
    shot("arena_above.png", (-21, -52, 34), (2.5, 1.5, -1.0), 26)
    shot("arena_eye_level.png", (10.0, -2.0, 1.6), (25.75, -12.75, 0.4), 18)
