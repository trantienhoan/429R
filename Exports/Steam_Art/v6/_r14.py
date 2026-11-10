SHOTS["Vertical_Capsule_748x896"] = dict(size=(748, 896), fit="VERTICAL", cam=((-0.27, 1.50, 0.13), (-0.27, 0.13, 0.30), 34), hide=HIDE_MAIN,
                                         boss=dict(face=(0.15, -3.5, 2.45)), moon=(0.12, 0.10), pose=dict(blade=(-0.45, 0.88, 0.15), turn=12, shield_face=(0.95, 0.0, 0.3)))
SHOTS["Library_Capsule_600x900"] = dict(size=(600, 900), fit="VERTICAL", cam=((-0.27, 1.55, 0.13), (-0.27, 0.13, 0.31), 32), hide=HIDE_MAIN,
                                        boss=dict(face=(0.10, -3.5, 2.55)), moon=(0.12, 0.10), pose=dict(blade=(-0.42, 0.90, 0.15), turn=12, shield_face=(0.95, 0.0, 0.3)))
for nm in ("Vertical_Capsule_748x896", "Library_Capsule_600x900"):
    setup_shot(nm, SHOTS[nm])
    R("CAM_V6_" + nm, pct=60, spp=32, out="cam_" + nm[:8].strip("_"), show_logo=False, hide=HIDE_MAIN)
