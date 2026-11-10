"""v6 helpers, exec'd into the helper Blender's globals."""
import bpy, os, time, math
from mathutils import Vector, Matrix, Quaternion, Euler
SC = bpy.context.scene
PREV = os.path.join(HERE, "prev")
os.makedirs(PREV, exist_ok=True)

def base_hidden_snapshot():
    return {o.name: o.hide_render for o in bpy.data.objects}

def R(cam_name, pct=50, spp=48, out=None, hide=(), show_logo=True, exposure=None, transparent=None, res=None):
    """Render one CAM_ like render_all.py does (its logo only, its hide_objs) into prev/<out>.png; restores hides."""
    cam = bpy.data.objects[cam_name]
    base = base_hidden_snapshot()
    logos = bpy.data.collections.get("Logos")
    try:
        for o in (list(logos.all_objects) if logos else []):
            o.hide_render = base[o.name] or not (o.parent == cam and cam.get("logo", False) and show_logo)
        for pre in [x.strip() for x in str(cam.get("hide_objs", "")).split(",") if x.strip()] + list(hide):
            for o in bpy.data.objects:
                if o.name.startswith(pre):
                    o.hide_render = True
        SC.camera = cam
        rs = float(cam.get("render_scale", 1.0))
        if res:
            SC.render.resolution_x, SC.render.resolution_y = res
        else:
            SC.render.resolution_x, SC.render.resolution_y = int(round(cam["steam_w"] * rs)), int(round(cam["steam_h"] * rs))
        SC.render.resolution_percentage = pct
        SC.cycles.samples = spp
        SC.render.film_transparent = bool(cam.get("transparent", False)) if transparent is None else transparent
        ex0 = SC.view_settings.exposure
        SC.view_settings.exposure = ex0 + (float(cam.get("exposure", 0.0)) if exposure is None else exposure)
        SC.render.image_settings.file_format = "PNG"
        SC.render.image_settings.color_mode = "RGBA" if SC.render.film_transparent else "RGB"
        path = os.path.join(PREV, (out or cam_name[4:]) + ".png")
        SC.render.filepath = path
        t0 = time.time()
        bpy.ops.render.render(write_still=True)
        SC.view_settings.exposure = ex0
        print("RENDERED", path, "%.1fs" % (time.time() - t0))
        return path
    finally:
        for o in bpy.data.objects:
            if o.name in base:
                o.hide_render = base[o.name]

def tmpcam(name, loc, look_at, lens=50, w=900, h=900):
    if name in bpy.data.objects:
        bpy.data.objects.remove(bpy.data.objects[name], do_unlink=True)
    cd = bpy.data.cameras.new(name); cd.lens = lens; cd.clip_start = 0.01; cd.clip_end = 300
    ob = bpy.data.objects.new(name, cd); SC.collection.objects.link(ob)
    ob.location = loc
    d = Vector(look_at) - Vector(loc)
    ob.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    ob["steam_w"], ob["steam_h"] = w, h
    return ob

def wbbox(names):
    deps = bpy.context.evaluated_depsgraph_get()
    mn = Vector((1e9,)*3); mx = Vector((-1e9,)*3)
    for n in names:
        ob = bpy.data.objects[n]
        e = ob.evaluated_get(deps)
        for c in e.bound_box:
            w = ob.matrix_world @ Vector(c)
            mn = Vector(map(min, mn, w)); mx = Vector(map(max, mx, w))
    return mn, mx
