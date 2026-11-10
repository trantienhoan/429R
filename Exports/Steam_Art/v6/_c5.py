HIDE_MAIN = ("ST_Hero", "Menace_Pumpkin_Body", "ST_Spider_")
cm = bpy.data.objects["CAM_V6_Main"]
L0, T0 = cm.location.copy(), Vector((0.05, 0.0, 0.40))
# header 920x430: same spot, a little wider lens so the boss's fists still fit
cam_look("CAM_V6_Header", L0, T0 + Vector((0, 0, 0.03)), 19, 920, 430)
# library hero 3840x1240: wide panorama, knight in the middle third
cam_look("CAM_V6_Hero", L0 + Vector((-0.05, 0.35, 0.02)), T0 + Vector((0.25, 0, 0.06)), 17, 3840, 1240)
R("CAM_V6_Header", pct=60, spp=32, out="cam_header", show_logo=False, hide=HIDE_MAIN)
R("CAM_V6_Hero", pct=20, spp=32, out="cam_hero", show_logo=False, hide=HIDE_MAIN)
