cam = bpy.data.objects["CAM_V6_Main_Capsule_1232x706"]
sh = bpy.data.objects["Pie_Knight_Shield"]
c = sh.matrix_world.translation.copy()
tc = tmpcam("CAM_TMP_SH", c + (cam.location - c).normalized() * 0.5, c, lens=50, w=400, h=400)
R("CAM_TMP_SH", pct=60, spp=16, out="shield_a", show_logo=False, hide=HIDE_MAIN)
# local axes of the shield in world
M = sh.matrix_world.to_3x3().normalized()
print("shield local X world", tuple(round(v,2) for v in M.col[0]), "to cam", tuple(round(v,2) for v in (cam.location - c).normalized()))
# flip about its local Z (turn 180) and render again
sh.matrix_world = sh.matrix_world @ Matrix.Rotation(math.pi, 4, 'Z')
R("CAM_TMP_SH", pct=60, spp=16, out="shield_b", show_logo=False, hide=HIDE_MAIN)
sh.matrix_world = sh.matrix_world @ Matrix.Rotation(math.pi, 4, 'Z')
