exec(open(os.path.join(HERE, "boss_v6.py"), encoding="utf-8").read(), globals())
boss_v6(clip="pumpkin_boss_jump_slam", frame=65, yaw=200, face=(2.1, -3.35, 1.85))
cam = bpy.data.objects["CAM_V6_Main"]
B = BOSS_FACE_NOW
cR = cam.matrix_world.col[0].xyz.normalized(); cU = cam.matrix_world.col[1].xyz.normalized()
for i, (du, dr) in enumerate([(0.07, 0.0), (0.12, 0.06)]):
    d = (B - cam.location).normalized()
    moon_to(d + cU * du + cR * dr, scale=1.9, origin=cam.location)
    haze_v6(density=0.018)
    light_v6(); knight_lights(); tree_glow()
    bpy.data.objects["V6_Fire_Key"].data.energy = 30; bpy.data.objects["V6_Fill"].data.energy = 8
    R("CAM_V6_Main", pct=50, spp=48, out="light_f%d" % i, show_logo=False, hide=("ST_Hero", "Menace_Pumpkin_Body"))
