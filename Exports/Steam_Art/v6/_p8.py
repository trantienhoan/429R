exec(open(os.path.join(HERE, "pose_f.py"), encoding="utf-8").read(), globals())
outs = []
for i, kw in enumerate([dict(), dict(turn=28, r_up=0.04), dict(turn=10, l_target=(0.18, -0.10, 0.08))]):
    pose_f(**kw)
    R("CAM_V6_T2", pct=50, spp=12, out="pose_f%d" % i, show_logo=False)
    F, Rt, U = frame()
    k = head_w("Spine1")
    c = tmpcam("CAM_TMP_PE", k + (bpy.data.objects["CAM_V6_T2"].location - k).normalized() * 1.1, k + Vector((0, 0, 0.07)), lens=40, w=500, h=500)
    R("CAM_TMP_PE", pct=70, spp=12, out="pose_fc%d" % i, show_logo=False, hide=("ST_Pumpkin_Tree", "ST_Tree_Pumpkin", "ST_Hero", "Menace_Pumpkin"))
