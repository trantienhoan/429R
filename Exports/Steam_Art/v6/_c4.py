POSE_G1 = dict(turn=20, blade=(-0.5, 0.85, 0.15), r_hand=(-0.09, 0.13, 0.06), shield_face=(0.8, 0.0, 0.6), l_hand=(0.10, -0.02, 0.10))
for i, (loc, tgt, lens) in enumerate([((-0.20, 1.15, 0.07), (0.05, 0.0, 0.40), 22), ((-0.25, 1.30, 0.05), (0.10, 0.0, 0.48), 24)]):
    cam_look("CAM_V6_Main", loc, tgt, lens, 1232, 706)
    pose_g(cam_name="CAM_V6_Main", **POSE_G1)
    R("CAM_V6_Main", pct=50, spp=16, out="comp_w%d" % i, show_logo=True)
