exec(open(os.path.join(HERE, "cam_v6.py"), encoding="utf-8").read(), globals())
exec(open(os.path.join(HERE, "shots_v6.py"), encoding="utf-8").read(), globals())
SHOTS["Vertical_Capsule_748x896"] = dict(size=(748, 896), fit="VERTICAL", cam=((-0.20, 1.05, 0.12), (0.05, 0.0, 0.40), 30), hide=HIDE_MAIN,
                                         boss=dict(face=(1.0, -3.5, 2.35)), moon=(0.10, 0.08))
SHOTS["Library_Capsule_600x900"] = dict(size=(600, 900), fit="VERTICAL", cam=((-0.20, 1.10, 0.12), (0.03, 0.0, 0.42), 30), hide=HIDE_MAIN,
                                        boss=dict(face=(0.9, -3.5, 2.45)), moon=(0.10, 0.08))
for nm in ("Vertical_Capsule_748x896", "Library_Capsule_600x900"):
    setup_shot(nm, SHOTS[nm])
    R("CAM_V6_" + nm, pct=60, spp=32, out="cam_" + nm[:8].strip("_"), show_logo=False, hide=HIDE_MAIN)
