for n in ("BG2_Sky_Backdrop", "BG2_Moon", "BG2_Moon_Halo", "LP_Set_Haze", "Haze"):
    o = bpy.data.objects.get(n)
    if not o: print("no", n); continue
    m = o.active_material
    print(n, "mat", m.name if m else None, "dims", tuple(round(v,2) for v in o.dimensions), "loc", tuple(round(v,2) for v in o.matrix_world.translation), "hide_r", o.hide_render, "vis_cam", o.visible_camera)
    if m and m.node_tree:
        for nd in m.node_tree.nodes:
            vals = []
            for i in nd.inputs:
                if i.is_linked or not hasattr(i, 'default_value') or not i.enabled: continue
                try: v = tuple(round(x, 3) for x in i.default_value)
                except TypeError: v = round(i.default_value, 3) if isinstance(i.default_value, float) else i.default_value
                vals.append("%s=%s" % (i.name, v))
            ex = ""
            if nd.type == "VALTORGB": ex = str([(round(e.position,2), tuple(round(c,3) for c in e.color[:3])) for e in nd.color_ramp.elements])
            print("   ", nd.bl_idname, nd.name, vals[:6], ex)
