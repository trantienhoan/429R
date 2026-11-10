def pose_d(turn=-12, lift=0.10, fist_k=1.75, arm_k=1.5, lean=14, look_up=10, sword="trail", knee=55):
    reset_pose()
    for bn in ARM.data.bones: bn.inherit_scale = 'FULL'
    ARM.rotation_euler.z = math.radians(142.0); upd()
    cam = bpy.data.objects["CAM_V6_Main"]
    F, Rt, U = frame()
    to_cam = (cam.location - ARM.location); to_cam.z = 0; to_cam.normalize()
    ARM.rotation_euler.z += (math.atan2(to_cam.y, to_cam.x) - math.atan2(F.y, F.x)) + math.radians(turn); upd()
    F, Rt, U = frame()
    boss = Vector(BOSS_FACE_V6)
    # airborne
    ARM.location.z = lift; upd()
    # whole body leans toward the boss (his right side up): roll about F, and back a little
    rotw("Hips", F, -lean); rotw("Hips", Rt, -6)
    # legs: left knee up, right leg hangs down/back
    rotw("LeftUpLeg", Rt, -knee); rotw("LeftLeg", Rt, knee * 1.1)
    rotw("RightUpLeg", Rt, 18); rotw("RightLeg", Rt, 26)
    for n in ("Spine", "Spine1", "Spine2"):
        rotw(n, F, -4); rotw(n, U, 6)
    # right arm: uppercut straight at the boss face
    stretch("RightArm", arm_k); stretch("RightForeArm", arm_k)
    for n in ("RightShoulder",):
        rotw(n, F, -12)
    for n in ("RightArm", "RightForeArm", "RightHand"):
        aim(n, boss)
    pb("RightHand").scale = (fist_k,) * 3
    curl(["RightHandIndex1", "RightHandIndex2", "RightHandIndex3"], 80, 'X')
    curl(["Thumb_01.R", "Thumb_02.R", "Thumb_03.R"], 30, 'X')
    # head: faces the camera, tipped up toward the boss a bit
    rotw("Head", U, -8); rotw("Head", Rt, -look_up); rotw("Head", F, -6)
    # left arm: sword
    F, Rt, U = frame()
    sh = head_w("LeftArm")
    if sword == "trail":      # swung down and back, blade trailing behind
        aim("LeftArm", sh - U * 0.05 - Rt * 0.06 - F * 0.03)
        aim("LeftForeArm", head_w("LeftForeArm") - U * 0.03 - Rt * 0.05 - F * 0.05)
        aim("LeftHand", head_w("LeftHand") - Rt * 0.04 - F * 0.03)
        curl(["LeftHandIndex1", "LeftHandIndex2", "LeftHandIndex3"], 75, 'X')
        upd(); F, Rt, U = frame()
        hand = wmat("LeftHand"); grip = hand.translation + hand.col[1].xyz.normalized() * 0.018
        blade = (-U * 0.35 - Rt * 1.0 - F * 0.55).normalized()
    else:                     # held out to the side, blade up
        aim("LeftArm", sh + U * 0.02 - Rt * 0.08)
        aim("LeftForeArm", head_w("LeftForeArm") + U * 0.06 - Rt * 0.03)
        aim("LeftHand", head_w("LeftHand") + U * 0.05)
        curl(["LeftHandIndex1", "LeftHandIndex2", "LeftHandIndex3"], 75, 'X')
        upd(); F, Rt, U = frame()
        hand = wmat("LeftHand"); grip = hand.translation + hand.col[1].xyz.normalized() * 0.018
        blade = (U * 1.0 - Rt * 0.55 - F * 0.2).normalized()
    place_obj("Pie_Knight_Sword", "LeftHand", (0, 0, -0.25), grip, blade, F)
    fa = wmat("LeftForeArm"); mid = fa.translation + fa.col[1].xyz.normalized() * 0.035
    place_obj("Pie_Knight_Shield", "LeftForeArm", (0, 0, 0.02), mid - Rt * 0.03, U, -Rt * 0.7 + F * 0.7)
