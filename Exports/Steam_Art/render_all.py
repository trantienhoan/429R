"""Renders every CAM_* camera of Steam_Art.blend at its Steam size into Exports/Steam_Art/renders/.
v5 camera properties: exposure (added for that picture), format ("JPG" for the app icon), trim (library logo: the
transparent margins are cut off and the logo scaled to just fit its size, as Steam asks).
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
exposure = sc.view_settings.exposure


def trim_to_logo(path, box_w, box_h, pad=0.012):
    """v5: Steam's library logo must BE the logo (1280 wide and/or 720 tall), not a logo floating in a 1280x720
    canvas: crop the transparent margins (keeping a small pad) and scale it so it just fits box_w x box_h."""
    import numpy as np
    img = bpy.data.images.load(path, check_existing=False)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    ys, xs = np.where(px[:, :, 3] > 0.02)
    if not len(xs):
        bpy.data.images.remove(img)
        return
    p = int(round(pad * max(w, h)))
    x0, x1 = max(0, xs.min() - p), min(w, xs.max() + 1 + p)
    y0, y1 = max(0, ys.min() - p), min(h, ys.max() + 1 + p)
    crop = np.ascontiguousarray(px[y0:y1, x0:x1])
    ch, cw = crop.shape[:2]
    k = min(box_w / cw, box_h / ch)
    nw, nh = max(1, int(round(cw * k))), max(1, int(round(ch * k)))
    out = bpy.data.images.new("trimmed_logo", cw, ch, alpha=True)
    out.colorspace_settings.name = img.colorspace_settings.name
    out.alpha_mode = "STRAIGHT"
    out.pixels.foreach_set(crop.ravel())
    out.scale(nw, nh)
    out.filepath_raw = path
    out.file_format = "PNG"
    out.save()
    print("TRIMMED", path, "%dx%d -> crop %dx%d -> %dx%d" % (w, h, cw, ch, nw, nh))
    bpy.data.images.remove(img)
    bpy.data.images.remove(out)


def clean_backdrop(path, out_path, jpg):
    """v5 icons (user picked the "clean" look, 2026-11-09): the knight (rendered on transparent) over a dark radial
    backdrop with a soft green glow round him, so he reads even at Steam's 32 px list size."""
    import numpy as np
    img = bpy.data.images.load(path, check_existing=False)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bpy.data.images.remove(img)
    yy, xx = np.mgrid[0:h, 0:w]
    # Blender rows go bottom-up: the glow centre sits a little above the middle (y 0.54 from the bottom)
    r = np.hypot(xx - w * 0.5, yy - h * 0.54) / (max(w, h) * 0.72)
    t = np.clip(r, 0.0, 1.0)[..., None] ** 1.0
    inner = np.array([22, 52, 28], np.float32) / 255.0
    outer = np.array([6, 9, 11], np.float32) / 255.0
    k = (1.0 - t) ** 1.4
    bg = inner * k + outer * (1.0 - k)
    a = px[:, :, 3]
    # soft glow: the knight's alpha blurred (separable gaussian), green, 38 %
    sig = 18.0 * w / 512.0
    rad = int(sig * 3)
    g = np.exp(-0.5 * (np.arange(-rad, rad + 1) / sig) ** 2)
    g /= g.sum()
    pad = np.pad(a, rad, mode="constant")
    b1 = np.apply_along_axis(lambda m: np.convolve(m, g, mode="valid"), 1, pad)
    b2 = np.apply_along_axis(lambda m: np.convolve(m, g, mode="valid"), 0, b1)
    ga = np.clip(b2 * 0.38, 0.0, 1.0)[..., None]
    col = bg * (1.0 - ga) + np.array([90, 255, 110], np.float32) / 255.0 * ga
    fa = a[..., None]
    col = col * (1.0 - fa) + px[:, :, :3] * fa
    out = bpy.data.images.new("icon_out", w, h, alpha=False)
    out.pixels.foreach_set(np.dstack([col, np.ones((h, w, 1), np.float32)]).ravel().astype(np.float32))
    out.filepath_raw = out_path
    out.file_format = "JPEG" if jpg else "PNG"
    try:
        out.save(quality=95)
    except TypeError:
        out.save()
    bpy.data.images.remove(out)
    print("BACKDROP", out_path)


for cam in cams:
    if cam.get("skip", False) and not only:       # v5: e.g. the 3D icon cameras (the icons are the user's badge now)
        print("SKIPPED", cam.name)
        continue
    if only and not any(x.lower() in cam.name.lower() for x in only.split(",")):
        continue
    tag = cam.name[4:]
    for o in bpy.data.objects:
        o.hide_render = base_hidden[o.name]
    # Only this camera's logo (its children) shows; anything hidden in the file (templates, retired pieces) stays hidden.
    for o in (list(logos.all_objects) if logos else []):
        o.hide_render = base_hidden[o.name] or not (o.parent == cam and cam.get("logo", False))
    # v3: per-picture hides (camera property hide_objs = comma list of name prefixes), e.g. a pumpkin cut by the frame
    for pre in [x.strip() for x in str(cam.get("hide_objs", "")).split(",") if x.strip()]:
        for o in bpy.data.objects:
            if o.name.startswith(pre):
                o.hide_render = True
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
    rs = float(cam.get("render_scale", 1.0))      # v5: the library logo renders bigger, then trim_to_logo scales it DOWN
    sc.render.resolution_x, sc.render.resolution_y = int(round(cam["steam_w"] * rs)), int(round(cam["steam_h"] * rs))
    sc.render.resolution_percentage = pct
    sc.cycles.samples = spp or (48 if preview else samples)
    backdrop = bool(cam.get("clean_backdrop", False))
    sc.render.film_transparent = bool(cam.get("transparent", False)) or backdrop
    hidden_cam = []
    if backdrop:            # only the knight is SEEN; the rest of the set still lights and reflects on him
        for o in bpy.data.objects:
            if o.type in ("MESH", "CURVE", "FONT", "VOLUME", "CURVES") and not o.name.startswith("Pie_Knight") \
                    and o.visible_camera:
                o.visible_camera = False
                hidden_cam.append(o)
    # v3: on the transparent library logo, skip the grade (CDL offset, curves, vignette): on premultiplied pixels with
    # a soft fire alpha they turned the glow into a pale haze. Only the bloom stays.
    tree = sc.compositing_node_group
    sa = next((n for n in tree.nodes if n.bl_idname == "CompositorNodeSetAlpha"), None) if tree else None
    bl = next((n for n in tree.nodes if n.bl_idname == "CompositorNodeGlare"), None) if tree else None
    graded = sa.inputs["Image"].links[0].from_socket if (sa and sa.inputs["Image"].is_linked) else None
    if sc.render.film_transparent and sa and bl and graded:
        tree.links.new(bl.outputs["Image"], sa.inputs["Image"])
    sc.view_settings.exposure = exposure + float(cam.get("exposure", 0.0))
    jpg = str(cam.get("format", "PNG")).upper() in ("JPG", "JPEG")
    final_path = os.path.join(out_dir, tag + (".jpg" if jpg else ".png"))
    if backdrop:            # rendered with alpha first, the backdrop is put behind it afterwards
        sc.render.image_settings.file_format = "PNG"
        sc.render.image_settings.color_mode = "RGBA"
        sc.render.filepath = os.path.join(out_dir, "_" + tag + "_alpha.png")
    else:
        sc.render.image_settings.file_format = "JPEG" if jpg else "PNG"
        sc.render.image_settings.color_mode = "RGBA" if (sc.render.film_transparent and not jpg) else "RGB"
        if jpg:
            sc.render.image_settings.quality = 95
        sc.render.filepath = final_path
    # v5: a render can fail to save now and then on Windows ("Render error ... cannot save", the file held a moment by
    # another program): try again, and never let one picture stop the others
    ok = False
    for attempt in range(3):
        try:
            bpy.ops.render.render(write_still=True)
            ok = True
            break
        except RuntimeError as e:
            print("RETRY", tag, attempt + 1, e)
            import time
            time.sleep(2.0)
    sc.view_settings.exposure = exposure
    if not ok:
        for o in hidden_cam:
            o.visible_camera = True
        print("FAILED", sc.render.filepath)
        continue
    print("RENDERED", sc.render.filepath)
    if cam.get("trim", False) and sc.render.film_transparent:
        trim_to_logo(sc.render.filepath, int(cam["steam_w"] * pct / 100), int(cam["steam_h"] * pct / 100))
    if backdrop:
        for o in hidden_cam:
            o.visible_camera = True
        clean_backdrop(sc.render.filepath, final_path, jpg)
        try:
            os.remove(sc.render.filepath)
        except OSError:
            pass
    if sc.render.film_transparent and sa and bl and graded:
        tree.links.new(graded, sa.inputs["Image"])
for o in bpy.data.objects:
    o.hide_render = base_hidden[o.name]
