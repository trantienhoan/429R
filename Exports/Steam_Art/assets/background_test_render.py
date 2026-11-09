"""Test renders of the new background.
Standalone (Background.blend alone, reference cameras):
  blender -b assets/Background.blend --python assets/background_test_render.py -- tag=r1 cams=Main,Hero pct=50 samples=48
Composite (opens Steam_Art.blend read-only, appends Background_v2, hides the old far set; NEVER saves):
  blender -b --python assets/background_test_render.py -- composite tag=r1 cams=Main,Vertical pct=50
"""
import bpy, os, sys
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
opt = dict(a.split("=", 1) for a in args if "=" in a)
composite = "composite" in args
HERE = r"C:/Github_Repository/429R/Exports/Steam_Art/assets"
OUTD = os.path.join(HERE, "background_tests")
os.makedirs(OUTD, exist_ok=True)
tag = opt.get("tag", "test")
want = [c.lower() for c in opt.get("cams", "Main").split(",")]
pct = int(opt.get("pct", "50"))
samples = int(opt.get("samples", "48"))

if composite:
    bpy.ops.wm.open_mainfile(filepath=r"C:/Github_Repository/429R/Exports/Steam_Art/Steam_Art.blend")
    sc = bpy.context.scene
    OLD = ("Castle", "Mountain_", "Bat_", "Far_Ground")
    for o in bpy.data.objects:
        if o.name.startswith(OLD) or o.name == "Moon":
            o.hide_render = True
        if o.name == "Haze" and "nohaze" in args:
            o.hide_render = True
    with bpy.data.libraries.load(os.path.join(HERE, "Background.blend"), link=False) as (src, dst):
        dst.collections = ["Background_v2"]
    for c in dst.collections:
        sc.collection.children.link(c)
    cams = [o for o in bpy.data.objects if o.type == "CAMERA" and o.name.startswith("CAM_")]
    logos = [o for o in bpy.data.objects if any(c.name == "Logos" for c in o.users_collection)]
    if "nologo" in args:
        for o in logos:
            o.hide_render = True
else:
    sc = bpy.context.scene
    cams = [o for o in bpy.data.objects if o.type == "CAMERA" and o.name.startswith("REF_CAM_")]
    logos = []
sc.cycles.samples = samples
sc.cycles.use_denoising = True
sc.render.resolution_percentage = pct
sc.render.film_transparent = False
sc.render.image_settings.file_format = "PNG"
sc.render.image_settings.color_mode = "RGB"
base_hidden = {o.name: o.hide_render for o in logos}
for cam in sorted(cams, key=lambda o: o.name):
    short = cam.name.replace("REF_", "").replace("CAM_", "")
    if not any(w in short.lower() for w in want) and "all" not in want:
        continue
    if cam.get("transparent", False):
        continue
    if composite and "nologo" not in args:
        for o in logos:
            o.hide_render = base_hidden[o.name] or not (o.parent == cam and cam.get("logo", False))
            if o.name == "Logo_429_Mesh":
                o.hide_render = True
    sc.camera = cam
    sc.render.resolution_x, sc.render.resolution_y = int(cam["steam_w"]), int(cam["steam_h"])
    sc.render.filepath = os.path.join(OUTD, f"{tag}_{'comp' if composite else 'bg'}_{short}.png")
    bpy.ops.render.render(write_still=True)
    print("RENDERED", sc.render.filepath)
