import bpy
sc = bpy.context.scene
print("ENGINE", sc.render.engine, "samples", sc.cycles.samples, "device", sc.cycles.device, "denoise", sc.cycles.use_denoising)
print("VIEW", sc.view_settings.view_transform, sc.view_settings.look, "exp", sc.view_settings.exposure, "gamma", sc.view_settings.gamma, "curves", sc.view_settings.use_curve_mapping)
print("WORLD", sc.world.name if sc.world else None)
print("FRAME", sc.frame_current, "RES", sc.render.resolution_x, sc.render.resolution_y)
for c in bpy.data.collections:
    print("COLL", c.name, "objs", len(c.objects), "hide_render", c.hide_render, "children", [k.name for k in c.children])
for o in sorted(bpy.data.objects, key=lambda o: o.name):
    if o.type in ("CAMERA",):
        print("CAM", o.name, tuple(round(v,3) for v in o.location), "lens", round(o.data.lens,1), "props", {k: o[k] for k in o.keys() if not k.startswith('_')})
for o in sorted(bpy.data.objects, key=lambda o: o.name):
    if o.type == "LIGHT":
        print("LIGHT", o.name, o.data.type, round(o.data.energy,2), tuple(round(v,2) for v in o.data.color), "hide_r", o.hide_render, tuple(round(v,2) for v in o.location))
for o in sorted(bpy.data.objects, key=lambda o: o.name):
    if o.type not in ("LIGHT","CAMERA"):
        print("OBJ", o.type, o.name, "vis" if not o.hide_render else "HID", "parent", o.parent.name if o.parent else "-", tuple(round(v,2) for v in o.matrix_world.translation), [c.name for c in o.users_collection][:2])
