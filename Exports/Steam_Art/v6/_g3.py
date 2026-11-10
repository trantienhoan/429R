exec(open(os.path.join(HERE, "fx_v6.py"), encoding="utf-8").read(), globals())
# A: knight key neutral-cool, a touch more lime in the armour
knight_lights(key=48, rim_l=60, rim_r=50, fill=8, top=14)
bpy.data.objects["V6_K_Key"].data.color = (0.92, 0.95, 1.0)
km = bpy.data.materials["Knight_Armour_Sword_Shield"]
mixn = next(n for n in km.node_tree.nodes if n.bl_idname == "ShaderNodeMix" and n.name == "Mix")
mixn.inputs[0].default_value = 0.28
# B: boss body darker, face keeps its glow
bpy.data.objects["V6_Boss_Under"].data.energy = 45
bm_ = bpy.data.materials["ST_Boss_Mat"]
for n in bm_.node_tree.nodes:
    if n.bl_idname == "ShaderNodeMix" and n.blend_type == "MULTIPLY":
        n.inputs[7].default_value = (0.22, 0.22, 0.22, 1)
# D: embers around and above the knight / between knight and boss; green sparkles round the blade
K = head_w("Spine1")
sw = bpy.data.objects["Pie_Knight_Sword"]
blade_mid = sw.matrix_world @ Vector((0, 0, -0.05))
cam = bpy.data.objects["CAM_V6_Main"]
ne = sparks("V6_Ember", 70, K + Vector((0.2, -1.2, 0.7)), (1.8, 1.4, 0.7), rad=(0.004, 0.011), color=(1.0, 0.42, 0.07), strength=40, seed=3,
            avoid=[(cam.location, 0.45), (K, 0.32)])
ng = sparks("V6_Spark", 26, blade_mid, (0.16, 0.10, 0.16), rad=(0.006, 0.014), color=(0.45, 1.0, 0.25), strength=25, seed=7, shape="star",
            avoid=[(K + Vector((0, 0, 0.15)), 0.17)])
print("embers", ne, "sparks", ng)
R("CAM_V6_Main", pct=70, spp=64, out="look_i", show_logo=False, hide=("ST_Hero", "Menace_Pumpkin_Body"))
