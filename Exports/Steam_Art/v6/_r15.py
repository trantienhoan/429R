exec(open(os.path.join(HERE, "shots_v6.py"), encoding="utf-8").read(), globals())
setup_shot("Main_Capsule_1232x706", SHOTS["Main_Capsule_1232x706"])
R("CAM_V6_Main_Capsule_1232x706", pct=100, spp=256, out="full_main", show_logo=False, hide=HIDE_MAIN)
