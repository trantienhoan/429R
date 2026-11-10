SHOTS["Vertical_Capsule_748x896"] = dict(size=(748, 896), cam=((-0.22, 1.30, 0.11), (0.0, 0.0, 0.55), 18), hide=HIDE_MAIN,
                                         boss=dict(face=(1.05, -3.5, 2.45)), moon=(0.10, 0.10))
SHOTS["Library_Capsule_600x900"] = dict(size=(600, 900), cam=((-0.22, 1.36, 0.11), (0.0, 0.0, 0.57), 17), hide=HIDE_MAIN,
                                        boss=dict(face=(1.0, -3.5, 2.55)), moon=(0.10, 0.10))
for nm in ("Vertical_Capsule_748x896", "Library_Capsule_600x900"):
    setup_shot(nm, SHOTS[nm])
    R("CAM_V6_" + nm, pct=60, spp=32, out="cam_" + nm[:8], show_logo=False, hide=HIDE_MAIN)
