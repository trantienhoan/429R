exec(open(os.path.join(HERE, "stage_v6.py"), encoding="utf-8").read(), globals())
stage(tree_delta=(-0.75, 0.15, 0), tree_rot=0, hero_delta=(0.95, -0.55, 0), hero_rot=-25)
pose_b()
k = head_w("Spine1")
F, Rt, U = frame()
for i, (dist, lens, z, sy, sx) in enumerate([(1.0, 22, 0.09, 0.17, -0.12), (1.25, 26, 0.10, 0.19, -0.12)]):
    loc = k + Vector((0.05, dist, 0)); loc.z = z
    tgt = k.copy(); tgt.z = z + 0.03
    cam_look("CAM_V6_T%d" % i, loc, tgt, lens, 1232, 706, shift_x=sx, shift_y=sy)
    R("CAM_V6_T%d" % i, pct=40, spp=16, out="comp_u%d" % i, show_logo=False)
