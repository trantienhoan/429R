for o in bpy.data.objects:
    if "oon" in o.name or "Sky" in o.name or "Cloud" in o.name or "Star" in o.name:
        print(o.name, o.type, "vis" if not o.hide_render else "HID", tuple(round(v,2) for v in o.matrix_world.translation), "dims", tuple(round(v,2) for v in o.dimensions), [c.name for c in o.users_collection][:2])
