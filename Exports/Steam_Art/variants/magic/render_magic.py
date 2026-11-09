"""render_all.py, but writes into variants/magic/<preview|full>/. Same Steam rules (hero/page bg no logo, logo-only transparent).
  blender -b variants/Steam_Art_magic.blend --python variants/magic/render_magic.py -- [preview] [only=Header,Main]"""
import bpy, os, sys
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
preview = "preview" in args
only = next((a.split("=", 1)[1] for a in args if a.startswith("only=")), None)
onlys = [s.strip().lower() for s in only.split(",")] if only else None
sc = bpy.context.scene
out_dir = os.path.join(r"C:/Github_Repository/429R/Exports/Steam_Art/variants/magic", "preview" if preview else "full")
os.makedirs(out_dir, exist_ok=True)
logos = bpy.data.collections.get("Logos")
cams = sorted([o for o in bpy.data.objects if o.type == "CAMERA" and o.name.startswith("CAM_")], key=lambda o: o.name)
base_hidden = {o.name: o.hide_render for o in bpy.data.objects}
samples = sc.cycles.samples
for cam in cams:
    if onlys and not any(s in cam.name.lower() for s in onlys):
        continue
    tag = cam.name[4:]
    for o in bpy.data.objects:
        o.hide_render = base_hidden[o.name]
    for o in (logos.all_objects if logos else []):
        if o.name == "Logo_429_Mesh":
            continue
        o.hide_render = not (o.parent == cam and cam.get("logo", False))
    if cam.get("hide_chars", False):
        keep = set(logos.all_objects) if logos else set()
        for o in bpy.data.objects:
            if o not in keep and o.type not in ("CAMERA",):
                o.hide_render = True
        for o in keep:
            if o.parent == cam:
                o.hide_render = False
        for o in keep:
            if o.name.endswith("_Glow"):
                o.hide_render = True
    sc.camera = cam
    sc.render.resolution_x, sc.render.resolution_y = int(cam["steam_w"]), int(cam["steam_h"])
    pct = next((int(a.split("=", 1)[1]) for a in args if a.startswith("pct=")), 40)
    sc.render.resolution_percentage = pct if preview else 100
    sc.cycles.samples = 64 if preview else samples
    sc.render.film_transparent = bool(cam.get("transparent", False))
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA" if sc.render.film_transparent else "RGB"
    sc.render.filepath = os.path.join(out_dir, tag + ".png")
    bpy.ops.render.render(write_still=True)
    print("RENDERED", sc.render.filepath)
for o in bpy.data.objects:
    o.hide_render = base_hidden[o.name]
