exec(open(os.path.join(HERE, "posekit.py"), encoding="utf-8").read(), globals())
boss_v6(face=(0.45, -3.35, 2.05))
def pose_c(turn=-30, fist_k=2.1, arm_k=1.55, fist_off=(0.0, 0.0)):
    reset_pose()
    for bn in ARM.data.bones: bn.inherit_scale = 'FULL'
    ARM.rotation_euler.z = math.radians(142.0); upd()
    cam = bpy.data.objects["CAM_V6_Main"]
    F, Rt, U = frame()
    to_cam = (cam.location - ARM.location); to_cam.z = 0; to_cam.normalize()
    ARM.rotation_euler.z += (math.atan2(to_cam.y, to_cam.x) - math.atan2(F.y, F.x)) + math.radians(turn); upd()
    F, Rt, U = frame()
    pb("Hips").location = (0, -4.0, 0); upd()
    rotw("LeftUpLeg", Rt, -30); rotw("LeftLeg", Rt, 34)
    rotw("RightUpLeg", Rt, 26); rotw("RightLeg", Rt, 18)
    for n in ("Spine", "Spine1", "Spine2"):
        rotw(n, Rt, 5); rotw(n, U, 9)
    rotw("Head", U, -20); rotw("Head", F, 10)
    # punch: fist toward the camera at helmet-cheek height, out to his right
    fist_target = cam.location.copy() + U * (0.30 + fist_off[1]) + Rt * (0.42 + fist_off[0])
    stretch("RightArm", arm_k); stretch("RightForeArm", arm_k)
    for n in ("RightArm", "RightForeArm", "RightHand"):
        aim(n, fist_target)
    pb("RightHand").scale = (fist_k,) * 3
    curl(["RightHandIndex1", "RightHandIndex2", "RightHandIndex3"], 75, 'X')
    curl(["Thumb_01.R", "Thumb_02.R", "Thumb_03.R"], 25, 'X')
    la = head_w("LeftArm")
    aim("LeftArm", la + U * 0.06 - Rt * 0.05 - F * 0.02)
    aim("LeftForeArm", head_w("LeftForeArm") + U * 0.07 - F * 0.04 - Rt * 0.01)
    aim("LeftHand", head_w("LeftHand") + U * 0.05 - F * 0.03)
    curl(["LeftHandIndex1", "LeftHandIndex2", "LeftHandIndex3"], 75, 'X')
    upd()
    F, Rt, U = frame()
    hand = wmat("LeftHand"); grip = hand.translation + hand.col[1].xyz.normalized() * 0.018
    blade = (U * 1.0 - F * 0.5 - Rt * 0.45).normalized()
    place_obj("Pie_Knight_Sword", "LeftHand", (0, 0, -0.25), grip, blade, F)
    fa = wmat("LeftForeArm"); mid = fa.translation + fa.col[1].xyz.normalized() * 0.035
    place_obj("Pie_Knight_Shield", "LeftForeArm", (0, 0, 0.02), mid - Rt * 0.03, U, -Rt * 0.7 + F * 0.7)
pose_c()
k = head_w("Spine1")
for i, (dist, lens, z, sy, sx, dx) in enumerate([(0.80, 20, 0.08, 0.20, -0.17, 0.10), (0.95, 22, 0.09, 0.21, -0.17, 0.12)]):
    loc = k + Vector((dx, dist, 0)); loc.z = z
    tgt = k + Vector((dx, 0, 0)); tgt.z = z + 0.03
    cam_look("CAM_V6_T%d" % i, loc, tgt, lens, 1232, 706, shift_x=sx, shift_y=sy)
    R("CAM_V6_T%d" % i, pct=40, spp=16, out="comp_v%d" % i, show_logo=False)
