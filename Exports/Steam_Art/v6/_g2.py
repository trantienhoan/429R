exec(open(os.path.join(HERE, "grade_v6.py"), encoding="utf-8").read(), globals())
grade_v6()
haze_v6(density=0.010, color=(0.40, 0.42, 1.0))
# only the moon-side lights scatter in the haze
for o in bpy.data.objects:
    if o.type == "LIGHT":
        o.visible_volume_scatter = o.name in ("V6_Moon_Rim", "V6_Moon_Rim2", "V6_Boss_Rim", "V6_Tree_Glow")
bpy.data.objects["V6_Boss_Rim"].data.energy = 450
bpy.data.objects["V6_Boss_Under"].data.energy = 110
knight_lights(key=45, rim_l=60, rim_r=50, fill=8, top=14)
SC.view_settings.exposure = 0.0
R("CAM_V6_Main", pct=70, spp=64, out="look_h", show_logo=False, hide=("ST_Hero", "Menace_Pumpkin_Body"))
