for n in ("ST_Roof_Rooftop_Base_Z.001", "ST_Roof_Lid", "ST_Roof_Rooftop_Base_Z_Battlements"):
    o = bpy.data.objects[n]
    for s in o.material_slots:
        m = s.material
        if not m: continue
        b = next((nd for nd in m.node_tree.nodes if nd.type == "BSDF_PRINCIPLED"), None)
        imgs = [nd.image.name for nd in m.node_tree.nodes if nd.type == "TEX_IMAGE" and nd.image]
        print(n, m.name, "emis_str", round(b.inputs["Emission Strength"].default_value, 2) if b else None, "emis_linked", b.inputs["Emission Color"].is_linked if b else None, "rough", (round(b.inputs["Roughness"].default_value,2) if b and not b.inputs["Roughness"].is_linked else "tex"), imgs)
