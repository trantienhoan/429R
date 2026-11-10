exec(open(os.path.join(HERE, "pose_g.py"), encoding="utf-8").read(), globals())
for i, kw in enumerate([dict(), dict(turn=20, blade=(-0.5, 0.85, 0.15), r_hand=(-0.09, 0.13, 0.06)), dict(turn=5, legs="wide", l_hand=(0.08, 0.0, 0.12))]):
    pose_g(**kw)
    k = head_w("Spine1")
    c = tmpcam("CAM_TMP_PE", k + (bpy.data.objects["CAM_V6_T2"].location - k).normalized() * 1.35, k + Vector((0, 0, 0.1)), lens=40, w=500, h=500)
    R("CAM_TMP_PE", pct=70, spp=12, out="pose_gc%d" % i, show_logo=False, hide=("ST_Pumpkin_Tree", "ST_Tree_Pumpkin", "ST_Hero", "Menace_Pumpkin"))
