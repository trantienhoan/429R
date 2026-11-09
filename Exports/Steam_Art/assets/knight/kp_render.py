"""Test renders for knight_pass: blender -b variants/knight_dev.blend --python assets/knight/kp_render.py -- tag=x [pass] [views=close,side,main,vert] [samples=N]
pass = run knight_pass.apply() first (no save)."""
import bpy, sys, os, math, importlib.util
from mathutils import Vector
args = sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
kw = dict(a.split("=",1) for a in args if "=" in a)
tag = kw.get("tag","t"); spp=int(kw.get("samples","64"))
views = kw.get("views","close,main,vert").split(",")
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE,"tests")
if "pass" in args:
    spec = importlib.util.spec_from_file_location("knight_pass", os.path.join(HERE,"..","..","knight_pass.py"))
    kp = importlib.util.module_from_spec(spec); spec.loader.exec_module(kp)
    for k,v in kw.items():
        if k.isupper(): setattr(kp, k, eval(v))
    kp.apply(save=False)
sc = bpy.context.scene
sc.cycles.samples = spp
try: sc.cycles.use_denoising = True
except: pass
HEAD = Vector((-0.27, 0.13, 0.25))
def look_cam(name, pos, target, lens):
    cd = bpy.data.cameras.new(name); cd.lens = lens; cd.clip_start=0.01
    c = bpy.data.objects.new(name, cd); sc.collection.objects.link(c)
    c.location = pos
    c.rotation_euler = (target-pos).to_track_quat('-Z','Y').to_euler()
    return c
BH = None
def render(cam, w, h, name, pct=100):
    global base_hidden
    try: base_hidden
    except NameError: base_hidden = {o.name: o.hide_render for o in bpy.data.objects}
    for o in bpy.data.objects: o.hide_render = base_hidden.get(o.name, False)
    for o in (bpy.data.collections["Logos"].all_objects if "Logos" in bpy.data.collections else []):
        o.hide_render = base_hidden.get(o.name, False) or not (o.parent == cam and cam.get("logo", False))
    for pre in [x.strip() for x in str(cam.get("hide_objs","")).split(",") if x.strip()]:
        for o in bpy.data.objects:
            if o.name.startswith(pre): o.hide_render=True
    sc.camera = cam; sc.render.resolution_x=w; sc.render.resolution_y=h; sc.render.resolution_percentage=pct
    sc.render.film_transparent=False
    sc.render.image_settings.file_format="PNG"; sc.render.image_settings.color_mode="RGB"
    sc.render.filepath = os.path.join(OUT, f"{tag}_{name}.png")
    bpy.ops.render.render(write_still=True); print("RENDERED", sc.render.filepath)
main = bpy.data.objects["CAM_Main_Capsule_1232x706"]
d = (main.location - HEAD).normalized()
if "hidehelm" in args:
    for n in ("Pie_Knight_Helmet",): bpy.data.objects[n].hide_render = True
    base_hidden = {o.name: o.hide_render for o in bpy.data.objects}
if "close" in views:
    render(look_cam("KPT_Close", HEAD + d*0.95, HEAD, 50), 700, 700, "close")
if "side" in views:
    from mathutils import Matrix
    d2 = Matrix.Rotation(math.radians(55),3,'Z') @ d
    render(look_cam("KPT_Side", HEAD + d2*0.95, HEAD, 50), 700, 700, "side")
if "face" in views:
    H2 = Vector((-0.25,0.17,0.31))
    render(look_cam("KPT_Face", H2 + d*0.45, H2, 50), 600, 600, "face")
if "main" in views:
    render(main, int(main["steam_w"]), int(main["steam_h"]), "main")
if "vert" in views:
    v = bpy.data.objects["CAM_Vertical_Capsule_748x896"]
    render(v, int(v["steam_w"]), int(v["steam_h"]), "vert")
if "hidehelm" in args:
    pass
