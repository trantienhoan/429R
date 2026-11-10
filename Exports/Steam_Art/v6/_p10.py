exec(open(os.path.join(HERE, "pose_g.py"), encoding="utf-8").read(), globals())
for i, kw in enumerate([dict(), dict(turn=20, blade=(-0.5, 0.85, 0.15), r_hand=(-0.09, 0.13, 0.06), shield_face=(0.8, 0.0, 0.6), l_hand=(0.10, -0.02, 0.10)),
                        dict(turn=8, legs="wide", l_hand=(0.11, 0.0, 0.10), shield_face=(0.7, 0.1, 0.7), blade=(-0.7, 0.68, 0.2))]):
    pose_g(**kw)
    k = head_w("Spine1")
    c = tmpcam("CAM_TMP_PE", k + (bpy.data.objects["CAM_V6_T2"].location - k).normalized() * 1.6, k + Vector((0, 0, 0.14)), lens=40, w=500, h=500)
    R("CAM_TMP_PE", pct=70, spp=12, out="pose_gd%d" % i, show_logo=False, hide=("ST_Pumpkin_Tree", "ST_Tree_Pumpkin", "ST_Hero", "Menace_Pumpkin"))
