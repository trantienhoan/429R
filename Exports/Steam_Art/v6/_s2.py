exec(open(os.path.join(HERE, "pose_g.py"), encoding="utf-8").read(), globals())
cam = bpy.data.objects["CAM_V6_Main_Capsule_1232x706"]
outs = []
for i, sfv in enumerate([(0.35, 0.0, 0.94), (0.6, 0.05, 0.8)]):
    pose_g(cam_name=cam.name, **dict(POSE_G1, shield_face=sfv))
    sh = bpy.data.objects["Pie_Knight_Shield"]
    c = sh.matrix_world.translation.copy()
    tc = tmpcam("CAM_TMP_SH", c + (cam.location - c).normalized() * 0.5, c, lens=50, w=400, h=400)
    R("CAM_TMP_SH", pct=60, spp=16, out="shield_c%d" % i, show_logo=False, hide=HIDE_MAIN)
