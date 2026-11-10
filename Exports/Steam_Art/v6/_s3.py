exec(open(os.path.join(HERE, "pose_g.py"), encoding="utf-8").read(), globals())
exec(open(os.path.join(HERE, "shots_v6.py"), encoding="utf-8").read(), globals())
setup_shot("Main_Capsule_1232x706", SHOTS["Main_Capsule_1232x706"])
R("CAM_V6_Main_Capsule_1232x706", pct=70, spp=64, out="main_m", show_logo=False, hide=HIDE_MAIN)
