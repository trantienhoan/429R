"""Renders every CAM_* camera of Steam_Art.blend at its Steam size into Exports/Steam_Art/renders/.
  blender -b Exports/Steam_Art/Steam_Art.blend --python render_all.py -- [preview] [only=Header]
preview = 40 % size, 48 samples (fast check) into renders_preview/. pct=N and samples=N override the size and samples."""
import bpy, os, sys
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
preview = "preview" in args
pct = int(next((a.split("=", 1)[1] for a in args if a.startswith("pct=")), 40 if preview else 100))
spp = next((int(a.split("=", 1)[1]) for a in args if a.startswith("samples=")), None)
only = next((a.split("=", 1)[1] for a in args if a.startswith("only=")), None)
sc = bpy.context.scene
out_dir = os.path.join(os.path.dirname(bpy.data.filepath), "renders" + ("_preview" if preview else ""))
os.makedirs(out_dir, exist_ok=True)
logos = bpy.data.collections.get("Logos")
lights = bpy.data.collections.get("Lights")
cams = sorted([o for o in bpy.data.objects if o.type == "CAMERA" and o.name.startswith("CAM_")], key=lambda o: o.name)
base_hidden = {o.name: o.hide_render for o in bpy.data.objects}
samples = sc.cycles.samples
for cam in cams:
    if only and not any(x.lower() in cam.name.lower() for x in only.split(",")):
        continue
    tag = cam.name[4:]
    for o in bpy.data.objects:
        o.hide_render = base_hidden[o.name]
    # Only this camera's logo (its children) shows; anything hidden in the file (templates, retired pieces) stays hidden.
    for o in (list(logos.all_objects) if logos else []):
        o.hide_render = base_hidden[o.name] or not (o.parent == cam and cam.get("logo", False))
    if cam.get("hide_chars", False):
        keep = set(logos.all_objects) if logos else set()
        for o in bpy.data.objects:
            if o not in keep and o.type not in ("CAMERA",):
                o.hide_render = True
        for o in keep:
            if o.parent == cam:
                o.hide_render = base_hidden[o.name]
        # The logo's glow card / halo shell would show as a green box or outline on a transparent image: hide it, keep the light.
        for o in keep:
            if o.name.endswith(("_Glow", "_Halo")):
                o.hide_render = True
    sc.camera = cam
    sc.render.resolution_x, sc.render.resolution_y = int(cam["steam_w"]), int(cam["steam_h"])
    sc.render.resolution_percentage = pct
    sc.cycles.samples = spp or (48 if preview else samples)
    sc.render.film_transparent = bool(cam.get("transparent", False))
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA" if sc.render.film_transparent else "RGB"
    sc.render.filepath = os.path.join(out_dir, tag + ".png")
    bpy.ops.render.render(write_still=True)
    print("RENDERED", sc.render.filepath)
for o in bpy.data.objects:
    o.hide_render = base_hidden[o.name]
