outs = []
for i, (turn, side, up, fk, ak) in enumerate([(-30, 0.75, 0.16, 1.9, 1.6), (-38, 0.85, 0.10, 1.9, 1.6), (-22, 0.70, 0.24, 2.0, 1.5)]):
    import types
    # re-define fist target via fist_off relative to the base (0.42 side, 0.30 up)
    pose_c(turn=turn, fist_k=fk, arm_k=ak, fist_off=(side - 0.42, up - 0.30))
    R("CAM_V6_T1", pct=34, spp=12, out="pose_c%d" % i, show_logo=False)
