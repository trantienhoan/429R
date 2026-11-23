"""Gate 3's jello volcano (user 2026-11-23: "jello volcano spit out candy occasionally"), a kit piece of its own file.

Makes Exports/Gates/gate3/BallValley_Kit_Volcano.blend (never overwrites it without --force: once the user edits it, it's
theirs) with the usual kit layout: collection BallValley_Kit > BallValley_Kit_Jello_Volcano > object
BallValley_Kit_Jello_Volcano (pivot at the middle of its foot, real metres: 4 m across, 1.85 m tall), on the kit palette
(BallValley_Palette: glossy jelly, a glowing candy-lava crater with drips down its sides). Then export it like the rest:
  blender -b Exports/Gates/gate3/BallValley_Kit_Volcano.blend --python Exports/Gates/gate3/tools/export_ballvalley_kit.py
(the export adds it to kit_materials.json); Build Ball Valley Kit makes its prefab and gives it to the valley.

Run: blender -b --factory-startup --python Exports/Gates/gate3/tools/volcano_piece.py -- [--force] [<renders folder>]"""
import bpy, bmesh, math, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
GATE3 = os.path.abspath(os.path.join(HERE, ".."))
OUT = os.path.join(GATE3, "BallValley_Kit_Volcano.blend")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
FORCE = "--force" in ARGS
RENDERS = next((a for a in ARGS if not a.startswith("--")), None)
SWATCHES = 32
COL = {"jelly_pink": 9, "jelly_purple": 12, "glow_lantern": 20, "glow_face": 21, "pit": 18}
NAME = "BallValley_Kit_Jello_Volcano"

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.unit_settings.system = "METRIC"

# The kit palette (the same PNGs as BallValley_Kit.blend).
pal = bpy.data.images.load(os.path.join(GATE3, "Palette", "BallValley_Palette.png"))
glow = bpy.data.images.load(os.path.join(GATE3, "Palette", "BallValley_Palette_Emission.png"))
mat = bpy.data.materials.new("BallValley_Palette")
mat.use_nodes = True
nt = mat.node_tree
bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
t1 = nt.nodes.new("ShaderNodeTexImage"); t1.image = pal
t2 = nt.nodes.new("ShaderNodeTexImage"); t2.image = glow
nt.links.new(t1.outputs["Color"], bsdf.inputs["Base Color"])
nt.links.new(t2.outputs["Color"], bsdf.inputs["Emission Color"])
bsdf.inputs["Emission Strength"].default_value = 1.6
bsdf.inputs["Roughness"].default_value = 0.2


def uv_at(col, v, tint=0.5):
    """A palette spot: column col, height v (0 dark .. 1 light), a little across the column (kept 4 px from its sides)."""
    u = (col + 0.15 + 0.7 * min(1.0, max(0.0, tint))) / SWATCHES
    return (u, min(0.97, max(0.03, v)))


bm = bmesh.new()
uvl = bm.loops.layers.uv.new("UVMap")
SEG = 16
# Rings up the cone: (height, radius); a jelly bulge round each ring.
RINGS = [(-0.12, 2.05), (0.35, 1.95), (0.85, 1.6), (1.3, 1.15), (1.68, 0.86), (1.85, 0.78)]
rings = []
for i, (z, r) in enumerate(RINGS):
    ring = []
    for s in range(SEG):
        a = 2 * math.pi * s / SEG
        wob = 1.0 + 0.07 * math.sin(a * 3 + i * 1.3) + 0.04 * math.sin(a * 5 - i)
        ring.append(bm.verts.new((math.cos(a) * r * wob, math.sin(a) * r * wob, z)))
    rings.append(ring)
# The crater: in from the rim and down to the lava.
crater = []
for z, r in ((1.72, 0.58), (1.58, 0.4)):
    crater.append([bm.verts.new((math.cos(2 * math.pi * s / SEG) * r, math.sin(2 * math.pi * s / SEG) * r, z)) for s in range(SEG)])
lava_mid = bm.verts.new((0.0, 0.0, 1.6))
bottom_mid = bm.verts.new((0.0, 0.0, -0.12))


def face(vs, col, vs_v, tint=0.5):
    f = bm.faces.new(vs)
    for loop, v in zip(f.loops, vs_v):
        loop[uvl].uv = uv_at(col, v, tint)
    return f


# Drips: which segments carry a lava drip, and how far down (ring index it reaches).
DRIPS = {1: 2, 4: 3, 7: 2, 10: 3, 13: 2}
for i in range(len(rings) - 1):
    lo, hi = rings[i], rings[i + 1]
    for s in range(SEG):
        n = (s + 1) % SEG
        drip = s in DRIPS and i >= DRIPS[s]
        if drip:
            col, v0, v1 = COL["glow_lantern"], 0.55 + 0.1 * i, 0.65 + 0.1 * i
        else:
            col = COL["jelly_purple"] if i < 2 else COL["jelly_pink"]
            v0, v1 = 0.25 + 0.14 * i, 0.25 + 0.14 * (i + 1)
        face([lo[s], lo[n], hi[n], hi[s]], col, [v0, v0, v1, v1], tint=(s % 4) / 3)
# The rim into the crater, the crater wall (glowing), the lava top.
for s in range(SEG):
    n = (s + 1) % SEG
    face([rings[-1][s], rings[-1][n], crater[0][n], crater[0][s]], COL["jelly_pink"], [0.95, 0.95, 0.8, 0.8])
    face([crater[0][s], crater[0][n], crater[1][n], crater[1][s]], COL["glow_face"], [0.55, 0.55, 0.75, 0.75])
    face([crater[1][s], crater[1][n], lava_mid], COL["glow_lantern"], [0.8, 0.8, 0.97])
    face([rings[0][n], rings[0][s], bottom_mid], COL["jelly_purple"], [0.1, 0.1, 0.1])
bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])

me = bpy.data.meshes.new(NAME)
bm.to_mesh(me)
bm.free()
for p in me.polygons:
    p.use_smooth = True
me.materials.append(mat)
ob = bpy.data.objects.new(NAME, me)
kit = bpy.data.collections.new("BallValley_Kit")
sc.collection.children.link(kit)
piece = bpy.data.collections.new(NAME)
kit.children.link(piece)
piece.objects.link(ob)
tris = sum(len(p.vertices) - 2 for p in me.polygons)
print("volcano: %d tris, %d faces" % (tris, len(me.polygons)))

if os.path.exists(OUT) and not FORCE:
    OUT = OUT[:-6] + "_generated.blend"
    print("kept the existing file; this one is", OUT)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=OUT)
try:
    bpy.ops.file.make_paths_relative()
    bpy.ops.wm.save_mainfile()
except RuntimeError as e:
    print("paths kept absolute:", e)
print("saved", OUT)

if RENDERS:
    os.makedirs(RENDERS, exist_ok=True)
    try:
        sc.render.engine = "BLENDER_EEVEE"
    except TypeError:
        pass
    try:
        sc.view_settings.view_transform = "Standard"
    except TypeError:
        pass
    sc.render.resolution_x, sc.render.resolution_y = 900, 700
    world = bpy.data.worlds.new("W"); sc.world = world; world.use_nodes = True
    next(n for n in world.node_tree.nodes if n.type == "BACKGROUND").inputs[0].default_value = (0.05, 0.04, 0.09, 1)
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN")); sun.data.energy = 3
    sun.rotation_euler = (math.radians(50), 0, math.radians(30)); sc.collection.objects.link(sun)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.lens = 40
    cam.location = (5.5, -6.5, 4.2)
    from mathutils import Vector
    cam.rotation_euler = (Vector((0, 0, 0.8)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = os.path.join(RENDERS, "volcano_piece.png")
    bpy.ops.render.render(write_still=True)
    print("render", sc.render.filepath)
