exec(open(os.path.join(HERE, "look_v6.py"), encoding="utf-8").read(), globals())
boss_v6(clip="pumpkin_boss_jump_slam", frame=65, yaw=200, face=(2.1, -3.35, 1.85))
cam = bpy.data.objects["CAM_V6_Main"]
B = Vector(BOSS_FACE_V6)
d = (B - cam.location).normalized()
# a little up and to the screen-right of the boss head
cR = cam.matrix_world.col[0].xyz.normalized(); cU = cam.matrix_world.col[1].xyz.normalized()
moon_to(d + cU * 0.10 - cR * 0.02, scale=1.7)
sky_colours()
light_v6()
bpy.data.objects["V6_Fire_Key"].data.energy = 30
bpy.data.objects["V6_Fill"].data.energy = 8
knight_lights()
R("CAM_V6_Main", pct=50, spp=32, out="light_d", show_logo=False)
