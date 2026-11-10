t = bpy.data.objects["ST_Pumpkin_Tree"]
print("tree mats", [(s.material.name if s.material else None) for s in t.material_slots], "dims", tuple(round(v,2) for v in t.dimensions), "loc", tuple(round(v,2) for v in t.matrix_world.translation))
for s in t.material_slots:
    m = s.material
    if not m: continue
    for nd in m.node_tree.nodes:
        if nd.type == "TEX_IMAGE": print("  ", m.name, "img", nd.image.name if nd.image else None)
        if nd.type == "BSDF_PRINCIPLED":
            print("  ", m.name, "emis", tuple(round(x,2) for x in nd.inputs["Emission Color"].default_value), nd.inputs["Emission Strength"].default_value, "linked", nd.inputs["Emission Color"].is_linked)
mn, mx = wbbox(["ST_Pumpkin_Tree"])
print("tree bbox", tuple(round(v,2) for v in mn), tuple(round(v,2) for v in mx))
