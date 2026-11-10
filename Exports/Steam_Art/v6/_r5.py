exec(open(os.path.join(HERE, "lib.py"), encoding="utf-8").read(), globals())
b = bpy.data.objects["ST_Pumpkin_Boss"]
mn, mx = wbbox(["ST_Pumpkin_Boss"])
print("BOSS bbox", tuple(round(v,2) for v in mn), tuple(round(v,2) for v in mx), "type", b.type, "mods", [m.type for m in b.modifiers], "mats", [m.name for m in b.data.materials][:8])
c = (mn + mx) / 2
cam = tmpcam("CAM_TMP_B", c + Vector((0, 6.5, 0.6)), c, lens=35, w=800, h=800)
R("CAM_TMP_B", pct=60, spp=24, out="boss_front", show_logo=False, hide=("ST_Pumpkin_Tree", "ST_Tree_Pumpkin_", "Pie_Knight", "ST_Hero", "Menace_Pumpkin", "ST_Roof_Battle", "LP_Set_Haze"))
bpy.data.objects.remove(cam, do_unlink=True)
