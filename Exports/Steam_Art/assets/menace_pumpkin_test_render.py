"""Close-up test renders of Menace_Pumpkin.blend (never saves the file).

    "E:/Steam/steamapps/common/Blender/blender.exe" -b Exports/Steam_Art/assets/Menace_Pumpkin.blend --python Exports/Steam_Art/assets/menace_pumpkin_test_render.py -- tag=r1 cams=Front,Close,ThreeQ,Scene,Thumb,Small,Compare samples=64 inner=20

Output: assets/menace_pumpkin/tests/<tag>_<cam>.png
"""
import bpy, os, sys, math
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "menace_pumpkin", "tests")
os.makedirs(OUT, exist_ok=True)
args = dict(a.split("=", 1) for a in (sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []) if "=" in a)
TAG = args.get("tag", "test")
CAMS = args.get("cams", "Front,Close,ThreeQ,Scene,Thumb,Small,Compare").split(",")
SAMPLES = int(args.get("samples", "64"))
INNER = float(args.get("inner", "20"))

sc = bpy.context.scene
for lc in sc.view_layers[0].layer_collection.children:
    lc.exclude = False
OB = bpy.data.objects
sc.render.engine = "CYCLES"
sc.cycles.samples = SAMPLES
sc.cycles.use_denoising = True
try:
    sc.cycles.device = "GPU"
except Exception:
    pass
sc.view_settings.view_transform = "AgX"
sc.view_settings.look = "None" if "None" in [
    i.identifier for i in bpy.types.ColorManagedViewSettings.bl_rna.properties["look"].enum_items] else "None"

w = bpy.data.worlds.new("MP_Test_World")
w.use_nodes = True
bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
bg.inputs["Color"].default_value = (0.012, 0.016, 0.035, 1)
sc.world = w


def area(name, loc, target, size, color, energy):
    L = bpy.data.lights.new(name, "AREA")
    L.size, L.color, L.energy = size, color, energy
    o = bpy.data.objects.new(name, L)
    sc.collection.objects.link(o)
    o.location = loc
    o.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    return o


# moon from behind-left-above (cool rim), a dim cool fill from the front, a warm key low front-left (like the scene)
area("MP_T_Moon", (-1.2, 1.6, 1.8), (0, 0, 0.2), 1.0, (0.6, 0.72, 1.0), 120)
area("MP_T_Fill", (0.4, -2.0, 1.0), (0, 0, 0.2), 1.5, (0.55, 0.65, 1.0), 12)
area("MP_T_Warm", (-0.9, -1.0, 0.6), (0, 0, 0.2), 0.5, (1.0, 0.66, 0.4), 10)
# like lighting_pass.py: the inner lights skip the pumpkin bodies (the glow you see is the materials' own)
no_pk = bpy.data.collections.new("MP_T_No_Pumpkins")
for o in OB:
    if o.type == "MESH" and (o.name.endswith("_Body") or o.name.startswith("MP_Test_Original")):
        no_pk.objects.link(o)
for co in no_pk.collection_objects:
    co.light_linking.link_state = "EXCLUDE"
for o in OB:
    if o.type == "LIGHT" and o.name.endswith("_Inner_Light"):
        o.data.energy = INNER if "Small" not in o.name else INNER * 0.25 * 0.25 * 2
        o.light_linking.receiver_collection = no_pk

cam = bpy.data.objects.new("MP_T_Cam", bpy.data.cameras.new("MP_T_Cam"))
sc.collection.objects.link(cam)
sc.camera = cam

hero = OB["Menace_Pumpkin_Root"]
small = OB["Menace_Pumpkin_Small_Root"]
orig = OB["MP_Test_Original_pumpkin_0"]


def shoot(name, loc, look, lens, res, show):
    for o, on in ((hero, "hero"), (small, "small"), (orig, "orig")):
        vis = on in show
        for ob in [o] + list(o.children_recursive):
            ob.hide_render = not vis
    cam.location = loc
    cam.rotation_euler = (Vector(look) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = lens
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.render.filepath = os.path.join(OUT, f"{TAG}_{name}.png")
    bpy.ops.render.render(write_still=True)
    print("[test] wrote", sc.render.filepath)


small.location = (0.55, -0.35, 0)
small.scale = (0.25, 0.25, 0.25)
small.rotation_euler = (0, 0, 0.35)
orig.location = (-0.55, 0, 0)
for c in CAMS:
    if c == "Front":
        shoot(c, (0.0, -1.7, 0.42), (0, 0, 0.2), 50, (900, 800), ("hero",))
    elif c == "Close":
        shoot(c, (0.0, -0.95, 0.30), (0, 0, 0.19), 50, (900, 700), ("hero",))
    elif c == "ThreeQ":
        shoot(c, (-1.0, -1.35, 0.55), (0, 0, 0.2), 50, (900, 800), ("hero",))
    elif c == "Scene":   # about how the Main capsule camera sees it (2.4 m away, 0.43 high, 50 mm)
        shoot(c, (0.25, -2.4, 0.43), (0.0, 0, 0.2), 50, (1232, 706), ("hero", "small"))
    elif c == "Thumb":   # small-capsule scale: is it still readable?
        shoot(c, (0.25, -2.4, 0.43), (0.0, 0, 0.2), 50, (300, 172), ("hero", "small"))
    elif c == "Small":
        shoot(c, (0.62, -0.95, 0.22), (0.55, -0.35, 0.05), 50, (700, 600), ("small",))
    elif c == "Compare":
        shoot(c, (-0.27, -2.2, 0.5), (-0.27, 0, 0.2), 50, (1200, 600), ("hero", "orig"))
