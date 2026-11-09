"""Test renders for the knight arm fix (2026-11-09). Never saves the .blend.
  blender -b variants/knight_fix_dev.blend --python assets/knight/fix_render.py -- tag=fixA [kp=path/to/knight_pass.py]
        [views=close,front,side,nohelm,main,vert,header,hero] [samples=N] [pct=N] [POSE='"wave"' LEAN_DEGREES=12 ...]
Runs knight_pass.apply(save=False) (default: Exports/Steam_Art/knight_pass.py) with any UPPER-CASE overrides, then
renders into assets/knight/tests/<tag>_<view>.png."""
import bpy, sys, os, math, importlib.util
from mathutils import Vector, Matrix
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
kw = dict(a.split("=", 1) for a in args if "=" in a)
tag = kw.get("tag", "fix")
spp = int(kw.get("samples", "64"))
pct = int(kw.get("pct", "100"))
views = kw.get("views", "close,front,side,main").split(",")
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "tests")
kp_path = kw.get("kp", os.path.join(HERE, "..", "..", "knight_pass.py"))
spec = importlib.util.spec_from_file_location("knight_pass", kp_path)
kp = importlib.util.module_from_spec(spec)
spec.loader.exec_module(kp)
for k, v in kw.items():
    if k.isupper():
        setattr(kp, k, eval(v))
kp.apply(save=False)
sc = bpy.context.scene
sc.cycles.samples = spp
try:
    sc.cycles.use_denoising = True
except Exception:
    pass
arm = bpy.data.objects["Pie_Knight_Armature"]
R, U, F = kp.knight_frame(arm)
HEAD = arm.matrix_world @ arm.pose.bones["mixamorig:Spine2"].head + U * 0.06
base_hidden = {o.name: o.hide_render for o in bpy.data.objects}


def look_cam(name, pos, target, lens):
    cd = bpy.data.cameras.new(name)
    cd.lens = lens
    cd.clip_start = 0.01
    c = bpy.data.objects.new(name, cd)
    sc.collection.objects.link(c)
    c.location = pos
    c.rotation_euler = (target - pos).to_track_quat("-Z", "Y").to_euler()
    return c


def render(cam, w, h, name, extra_hide=()):
    for o in bpy.data.objects:
        o.hide_render = base_hidden.get(o.name, False)
    for o in (bpy.data.collections["Logos"].all_objects if "Logos" in bpy.data.collections else []):
        o.hide_render = base_hidden.get(o.name, False) or not (o.parent == cam and cam.get("logo", False))
    for pre in [x.strip() for x in str(cam.get("hide_objs", "")).split(",") if x.strip()]:
        for o in bpy.data.objects:
            if o.name.startswith(pre):
                o.hide_render = True
    for o in bpy.data.objects:
        if any(o.name.startswith(n) for n in extra_hide):
            o.hide_render = True
    sc.camera = cam
    sc.render.resolution_x, sc.render.resolution_y = w, h
    sc.render.resolution_percentage = pct
    sc.render.film_transparent = False
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGB"
    sc.render.filepath = os.path.join(OUT, f"{tag}_{name}.png")
    bpy.ops.render.render(write_still=True)
    print("RENDERED", sc.render.filepath)


main = bpy.data.objects["CAM_Main_Capsule_1232x706"]
d = (main.location - HEAD).normalized()
if "close" in views:        # from the Main camera's direction, close up
    render(look_cam("KPT_Close", HEAD + d * 0.95, HEAD, 50), 700, 700, "close")
if "front" in views:        # straight from his front
    render(look_cam("KPT_Front", HEAD + F * 0.95, HEAD, 50), 700, 700, "front")
if "side" in views:         # from his right side, a little in front: shows the elbow / shoulder
    d2 = (R * 0.8 + F * 0.6).normalized()
    render(look_cam("KPT_Side", HEAD + d2 * 0.95, HEAD, 50), 700, 700, "side", extra_hide=("Menace_Pumpkin",))
if "nohelm" in views:       # helmet hidden: shoulder deformation under the helmet
    render(look_cam("KPT_NoHelm", HEAD + d * 0.75, HEAD - U * 0.03, 50), 700, 700, "nohelm",
           extra_hide=("Pie_Knight_Helmet",))
for key, cname in (("main", "CAM_Main_Capsule_1232x706"), ("vert", "CAM_Vertical_Capsule_748x896"),
                   ("header", "CAM_Header_Capsule_920x430"), ("hero", "CAM_Library_Hero_3840x1240")):
    if key in views:
        c = bpy.data.objects[cname]
        render(c, int(c["steam_w"]), int(c["steam_h"]), key)
