def pose_f(turn=18, legs="lunge_r", spine_twist=-6, spine_lean=5, r_reach=0.17, r_up=0.0, r_stretch=1.45, sword_bias=(0, 0, 0),
           l_target=(0.16, -0.05, 0.02), l_hint=(0, -1, -0.4), l_stretch=1.5, l_fist=1.6, head=(10, -4, -8), lift=0.0,
           shield_face=(0.8, -0.6, 0.0)):
    reset_pose()
    for bn in ARM.data.bones: bn.inherit_scale = 'FULL'
    ARM.rotation_euler.z = math.radians(142.0); ARM.location.z = 0; upd()
    cam = bpy.data.objects["CAM_V6_Main"]
    F, Rt, U = frame()
    to_cam = (cam.location - ARM.location); to_cam.z = 0; to_cam.normalize()
    ARM.rotation_euler.z += (math.atan2(to_cam.y, to_cam.x) - math.atan2(F.y, F.x)) + math.radians(turn)
    ARM.location.z = lift; upd()
    F, Rt, U = frame()
    if legs == "lunge_r":      # right leg steps forward (toward the boss side), left leg back
        pb("Hips").location = (0, -4.0, 0); upd()
        rotw("RightUpLeg", Rt, -26); rotw("RightLeg", Rt, 30); rotw("LeftUpLeg", Rt, 24); rotw("LeftLeg", Rt, 16)
        rotw("RightUpLeg", F, -10); rotw("LeftUpLeg", F, 8)
    for n in ("Spine", "Spine1", "Spine2"):
        rotw(n, U, spine_twist); rotw(n, Rt, spine_lean)
    yaw, pitch, roll = head
    F, Rt, U = frame()
    rotw("Head", U, yaw); rotw("Head", Rt, pitch); rotw("Head", F, roll)
    boss = Vector(BOSS_FACE_V6)
    # right arm points the sword at the boss face
    if r_stretch != 1.0:
        stretch("RightArm", r_stretch); stretch("RightForeArm", r_stretch)
    S = head_w("RightArm")
    to_boss = (boss - S).normalized()
    T = S + to_boss * r_reach + U * r_up
    ik2("RightArm", "RightForeArm", "RightHand", T, -U * 0.6 + Rt * 0.4, None)
    curl(["RightHandIndex1", "RightHandIndex2", "RightHandIndex3"], 80, 'X')
    curl(["Thumb_01.R", "Thumb_02.R", "Thumb_03.R"], 30, 'X')
    upd()
    hand = wmat("RightHand"); grip = hand.translation + hand.col[1].xyz.normalized() * 0.018
    blade = ((boss - grip).normalized() + Vector(sword_bias)).normalized()
    flat = (cam.location - grip).normalized()
    place_obj("Pie_Knight_Sword", "RightHand", (0, 0, -0.25), grip, blade, flat)
    # left arm: shield-fist punch toward the camera
    if l_stretch != 1.0:
        stretch("LeftArm", l_stretch); stretch("LeftForeArm", l_stretch)
    S = head_w("LeftArm")
    to_cam3 = (cam.location - S).normalized()
    F, Rt, U = frame()
    T = S + to_cam3 * l_target[0] + Rt * l_target[1] + U * l_target[2]
    ik2("LeftArm", "LeftForeArm", "LeftHand", T, kf(*l_hint), None)
    pb("LeftHand").scale = (l_fist,) * 3
    curl(["LeftHandIndex1", "LeftHandIndex2", "LeftHandIndex3"], 80, 'X')
    curl(["Thumb_01.L", "Thumb_02.L", "Thumb_03.L"], 30, 'X')
    upd()
    fa = wmat("LeftForeArm"); mid = fa.translation + fa.col[1].xyz.normalized() * 0.03
    F, Rt, U = frame()
    face = kf(*shield_face)
    place_obj("Pie_Knight_Shield", "LeftForeArm", (0, 0, 0.02), mid - Rt * 0.035, fa.col[1].xyz.normalized() * 0.3 + U, face)
    bpy.data.objects["Pie_Knight_Shield"].hide_render = False
