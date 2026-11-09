"""Renders chosen CAM_* shots of the open blend into variants/pumpkinglow/ (same per-shot rules as render_all.py).
  blender -b variants/Steam_Art_pumpkinglow.blend --python variants/render_pumpkinglow.py -- [preview] [only=Header,Main]"""
import bpy, os, sys
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
preview = "preview" in args
only = next((a.split("=", 1)[1].split(",") for a in args if a.startswith("only=")), None)
sc = bpy.context.scene
out_dir = r"C:/Github_Repository/429R/Exports/Steam_Art/variants/pumpkinglow" + ("" if preview else "/full")
os.makedirs(out_dir, exist_ok=True)
logos = bpy.data.collections.get("Logos")
cams = sorted([o for o in bpy.data.objects if o.type == "CAMERA" and o.name.startswith("CAM_")], key=lambda o: o.name)
base_hidden = {o.name: o.hide_render for o in bpy.data.objects}
samples = sc.cycles.samples
for cam in cams:
    if only and not any(s.lower() in cam.name.lower() for s in only):
        continue
    tag = cam.name[4:]
    for o in bpy.data.objects:
        o.hide_render = base_hidden[o.name]
    for o in (list(logos.all_objects) if logos else []):
        if o.name == "Logo_429_Mesh":
            continue
        o.hide_render = not (o.parent == cam and cam.get("logo", False))
    if cam.get("hide_chars", False):
        keep = set(logos.all_objects) if logos else set()
        for o in bpy.data.objects:
            if o not in keep and o.type != "CAMERA":
                o.hide_render = True
        for o in keep:
            if o.parent == cam:
                o.hide_render = False
    sc.camera = cam
    sc.render.resolution_x, sc.render.resolution_y = int(cam["steam_w"]), int(cam["steam_h"])
    sc.render.resolution_percentage = 40 if preview else 100
    if tag.startswith("Small") and preview:
        sc.render.resolution_percentage = 100      # judge the small capsule at its real size
    sc.cycles.samples = 48 if preview else samples
    sc.render.film_transparent = bool(cam.get("transparent", False))
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA" if sc.render.film_transparent else "RGB"
    sc.render.filepath = os.path.join(out_dir, tag + ".png")
    bpy.ops.render.render(write_still=True)
    print("RENDERED", sc.render.filepath)
for o in bpy.data.objects:
    o.hide_render = base_hidden[o.name]
