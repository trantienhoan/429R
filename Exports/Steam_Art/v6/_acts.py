import bpy
def fcurves_of(act):
    out = []
    try:
        for c in act.fcurves:
            out.append(c)
    except Exception:
        pass
    try:
        for layer in act.layers:
            for strip in layer.strips:
                for slot in act.slots:
                    cb = strip.channelbag(slot)
                    if cb:
                        out += list(cb.fcurves)
    except Exception as e:
        pass
    return out
for o in bpy.data.objects:
    ad = o.animation_data
    if not ad or not ad.action or not (o.name.startswith("Pie_Knight") or o.name.startswith("CAM_V6")):
        continue
    fcs = fcurves_of(ad.action)
    print("OBJ", o.name, "action", ad.action.name, "fcurves", [(f.data_path, f.array_index, [tuple(round(x, 3) for x in k.co) for k in f.keyframe_points][:3]) for f in fcs][:12])
    print("    now loc", tuple(round(v, 4) for v in o.location), "rot", tuple(round(v, 4) for v in o.rotation_euler), "scale", tuple(round(v, 4) for v in o.scale), "parent_bone", o.parent_bone)
