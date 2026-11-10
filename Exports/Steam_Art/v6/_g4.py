exec(open(os.path.join(HERE, "pose_g.py"), encoding="utf-8").read(), globals())
pose_g(cam_name="CAM_V6_Main", **POSE_G1)
knight_lights(key=48, rim_l=60, rim_r=50, fill=8, top=14); bpy.data.objects["V6_K_Key"].data.color = (0.92, 0.95, 1.0)
# floor: darker, a little wet
def floor_look(alb=0.65, rough=0.38, mats=("ST_Roof_Rooftop_Cursed", "ST_Roof_Rooftop_Base_Lid")):
    for mn in mats:
        m = bpy.data.materials[mn]; nt = m.node_tree
        b = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
        lk = b.inputs["Base Color"].links
        mul = nt.nodes.get("V6_Darken")
        if mul is None and lk:
            src = lk[0].from_socket
            mul = nt.nodes.new("ShaderNodeMix"); mul.name = "V6_Darken"; mul.data_type = "RGBA"; mul.blend_type = "MULTIPLY"
            mul.inputs[0].default_value = 1.0
            nt.links.new(src, mul.inputs[6]); nt.links.new(mul.outputs[2], b.inputs["Base Color"])
        if mul: mul.inputs[7].default_value = (alb, alb, alb, 1)
        rl = b.inputs["Roughness"].links
        if rl:
            nt.links.remove(rl[0])
        b.inputs["Roughness"].default_value = rough
floor_look()
R("CAM_V6_Main", pct=70, spp=64, out="look_j", show_logo=False, hide=("ST_Hero", "Menace_Pumpkin_Body"))
