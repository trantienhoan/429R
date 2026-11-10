SHOTS["Vertical_Capsule_748x896"] = dict(size=(748, 896), fit="VERTICAL", cam=((-0.27, 1.33, 0.12), (-0.27, 0.13, 0.36), 35), hide=HIDE_MAIN,
                                         boss=dict(face=(0.15, -3.5, 2.45)), moon=(0.12, 0.10), pose=dict(blade=(-0.45, 0.88, 0.15), turn=12))
SHOTS["Library_Capsule_600x900"] = dict(size=(600, 900), fit="VERTICAL", cam=((-0.27, 1.38, 0.12), (-0.27, 0.13, 0.37), 33), hide=HIDE_MAIN,
                                        boss=dict(face=(0.10, -3.5, 2.55)), moon=(0.12, 0.10), pose=dict(blade=(-0.42, 0.90, 0.15), turn=12))
for nm in ("Vertical_Capsule_748x896", "Library_Capsule_600x900"):
    setup_shot(nm, SHOTS[nm])
    R("CAM_V6_" + nm, pct=60, spp=32, out="cam_" + nm[:8].strip("_"), show_logo=False, hide=HIDE_MAIN)
