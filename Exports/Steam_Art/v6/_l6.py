exec(open(os.path.join(HERE, "fx_v6.py"), encoding="utf-8").read(), globals())
cam = bpy.data.objects["CAM_V6_Main"]
B = Vector(BOSS_FACE_V6)
cR = cam.matrix_world.col[0].xyz.normalized(); cU = cam.matrix_world.col[1].xyz.normalized()
d = (B - cam.location).normalized()
moon_to(d + cU * 0.11 + cR * 0.05, scale=1.7, origin=cam.location)
haze_v6()
tree_glow()
for n in ("Menace_Pumpkin_Root",):
    pass
R("CAM_V6_Main", pct=50, spp=48, out="light_e", show_logo=False, hide=("ST_Hero", "Menace_Pumpkin_Body"))
