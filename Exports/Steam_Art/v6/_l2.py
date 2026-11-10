exec(open(os.path.join(HERE, "light_v6.py"), encoding="utf-8").read(), globals())
boss_v6(face=(0.85, -3.35, 2.0))
cam_look("CAM_V6_Main", (-0.20, 1.15, 0.07), (0.05, 0.0, 0.40), 22, 1232, 706)
POSE_G1 = dict(turn=20, blade=(-0.62, 0.75, 0.15), r_hand=(-0.09, 0.13, 0.06), shield_face=(0.8, 0.0, 0.6), l_hand=(0.10, -0.02, 0.10))
pose_g(cam_name="CAM_V6_Main", **POSE_G1)
light_v6()
SC.view_settings.look = 'AgX - Punchy'
R("CAM_V6_Main", pct=50, spp=24, out="light_a", show_logo=False)
