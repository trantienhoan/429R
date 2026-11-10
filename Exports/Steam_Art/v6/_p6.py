exec(open(os.path.join(HERE, "pose_d.py"), encoding="utf-8").read(), globals())
ARM.location.z = 0
cam_look("CAM_V6_T2", (-0.15, 1.35, 0.06), (0.0, 0.0, 0.42), 22, 1232, 706)
for i, kw in enumerate([dict(), dict(sword="up"), dict(turn=-25, lean=20)]):
    pose_d(**kw)
    R("CAM_V6_T2", pct=34, spp=12, out="pose_d%d" % i, show_logo=False)
