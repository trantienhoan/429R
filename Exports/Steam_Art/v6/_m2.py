m = bpy.data.materials["Knight_Armour_Sword_Shield"]
nt = m.node_tree
for n in nt.nodes:
    ins = []
    for i in n.inputs:
        if i.is_linked: ins.append(i.name + "<-" + i.links[0].from_node.name)
        elif hasattr(i, "default_value") and i.enabled:
            try: v = tuple(round(x, 3) for x in i.default_value)
            except TypeError: v = round(i.default_value, 3) if isinstance(i.default_value, float) else i.default_value
            if i.name in ("Base Color", "Roughness", "Metallic", "Specular IOR Level", "Coat Weight", "Emission Strength", "Strength", "Fac", "Factor", "Color1", "Color2", "Scale"):
                ins.append("%s=%s" % (i.name, v))
    extra = ""
    if n.type == "TEX_IMAGE" and n.image: extra = n.image.name + " " + n.image.filepath
    if n.type == "VALTORGB": extra = str([(round(e.position,2), tuple(round(c,2) for c in e.color)) for e in n.color_ramp.elements])
    print(n.bl_idname, n.name, ins, extra)
