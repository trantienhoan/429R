exec(open(os.path.join(HERE, "cam_v6.py"), encoding="utf-8").read(), globals())
k = head_w("Spine1")
for i, (dist, lens, z, sy, side) in enumerate([(1.25, 24, 0.10, 0.16, 0.0), (1.6, 28, 0.09, 0.20, 0.25), (1.05, 21, 0.11, 0.14, -0.15)]):
    F, Rt, U = frame()
    loc = k + Vector((0, 1, 0)) * dist + Vector((side, 0, 0)); loc.z = z
    tgt = k.copy(); tgt.z = z + 0.02; tgt.x += 0.0
    cam_look("CAM_V6_T%d" % i, loc, tgt, lens, 1232, 706, shift_x=-0.10, shift_y=sy)
    print(i, "knight head", project("CAM_V6_T%d" % i, head_w("Head") + Vector((0, 0, 0.15))), "feet", project("CAM_V6_T%d" % i, k - Vector((0, 0, 0.15))), "boss face", project("CAM_V6_T%d" % i, BOSS_FACE_V6) if 'BOSS_FACE_V6' in globals() else None)
    R("CAM_V6_T%d" % i, pct=40, spp=16, out="comp_t%d" % i, show_logo=False)
