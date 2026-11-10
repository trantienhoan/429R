for mn in ("KP_Smile_Eye_A", "KP_Smile_Eye_B"):
    m = bpy.data.materials.get(mn)
    if not m: print("no", mn); continue
    for n in m.node_tree.nodes:
        vals = []
        for i in n.inputs:
            if i.is_linked: vals.append(i.name + "<-" + i.links[0].from_node.name)
            elif hasattr(i, "default_value") and i.name in ("Color", "Strength", "Emission Strength", "Emission Color", "Base Color"):
                dv = i.default_value
                vals.append("%s=%s" % (i.name, tuple(round(x, 3) for x in dv) if hasattr(dv, "__len__") else round(dv, 3)))
        print(mn, n.bl_idname, n.name, vals)
