outs = []
for i, (bx, k_fire, k_fill, blade) in enumerate([(2.3, 45, 14, (-0.70, 0.66, 0.15)), (1.9, 60, 18, (-0.78, 0.58, 0.18))]):
    boss_v6(face=(bx, -3.35, 2.0))
    pose_g(cam_name="CAM_V6_Main", **dict(POSE_G1, blade=blade))
    light_v6()
    bpy.data.objects["V6_Fire_Key"].data.energy = k_fire
    bpy.data.objects["V6_Fill"].data.energy = k_fill
    R("CAM_V6_Main", pct=50, spp=24, out="light_b%d" % i, show_logo=False)
