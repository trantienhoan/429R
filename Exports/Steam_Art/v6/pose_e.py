def pose_e(turn=-15, lift=0.0, hips_roll=0.0, hips_pitch=0.0, legs="wide", spine_twist=6, spine_lean=4,
           r_target=(0.10, 0.12, 0.12), r_hint=(0, 0.3, -1), r_stretch=1.5, r_fist=1.7, r_hand_dir=None,
           l_target=(0.02, -0.10, 0.10), l_hint=(0, -0.3, -1), l_stretch=1.0, sword_dir=(-0.3, -0.4, 1.0), sword_flat=(1, 0, 0),
           head=(-10, 0, 6), shield="arm"):
    reset_pose()
    for bn in ARM.data.bones: bn.inherit_scale = 'FULL'
    ARM.rotation_euler.z = math.radians(142.0); ARM.location.z = 0; upd()
    cam = bpy.data.objects["CAM_V6_Main"]
    F, Rt, U = frame()
    to_cam = (cam.location - ARM.location); to_cam.z = 0; to_cam.normalize()
    ARM.rotation_euler.z += (math.atan2(to_cam.y, to_cam.x) - math.atan2(F.y, F.x)) + math.radians(turn)
    ARM.location.z = lift; upd()
    F, Rt, U = frame()
    if hips_roll: rotw("Hips", F, hips_roll)
    if hips_pitch: rotw("Hips", Rt, hips_pitch)
    if legs == "wide":
        pb("Hips").location = (0, -3.0, 0); upd()
        rotw("LeftUpLeg", F, 14); rotw("RightUpLeg", F, -14)
        rotw("LeftUpLeg", Rt, -12); rotw("LeftLeg", Rt, 14); rotw("RightUpLeg", Rt, 10); rotw("RightLeg", Rt, 10)
    elif legs == "lunge":
        pb("Hips").location = (0, -4.0, 0); upd()
        rotw("LeftUpLeg", Rt, -30); rotw("LeftLeg", Rt, 34); rotw("RightUpLeg", Rt, 26); rotw("RightLeg", Rt, 18)
        rotw("LeftUpLeg", F, 8); rotw("RightUpLeg", F, -8)
    elif legs == "jump":
        rotw("LeftUpLeg", Rt, -55); rotw("LeftLeg", Rt, 70); rotw("RightUpLeg", Rt, 15); rotw("RightLeg", Rt, 40)
    for n in ("Spine", "Spine1", "Spine2"):
        rotw(n, U, spine_twist); rotw(n, Rt, spine_lean)
    yaw, pitch, roll = head
    F, Rt, U = frame()
    rotw("Head", U, yaw); rotw("Head", Rt, pitch); rotw("Head", F, roll)
    # right arm
    if r_stretch != 1.0:
        stretch("RightArm", r_stretch); stretch("RightForeArm", r_stretch)
    S = head_w("RightArm")
    ik2("RightArm", "RightForeArm", "RightHand", S + kf(*r_target), kf(*r_hint), kf(*r_hand_dir) if r_hand_dir else None)
    pb("RightHand").scale = (r_fist,) * 3
    curl(["RightHandIndex1", "RightHandIndex2", "RightHandIndex3"], 80, 'X')
    curl(["Thumb_01.R", "Thumb_02.R", "Thumb_03.R"], 30, 'X')
    # left arm + sword
    if l_stretch != 1.0:
        stretch("LeftArm", l_stretch); stretch("LeftForeArm", l_stretch)
    S = head_w("LeftArm")
    ik2("LeftArm", "LeftForeArm", "LeftHand", S + kf(*l_target), kf(*l_hint), None)
    curl(["LeftHandIndex1", "LeftHandIndex2", "LeftHandIndex3"], 80, 'X')
    upd()
    hand = wmat("LeftHand"); grip = hand.translation + hand.col[1].xyz.normalized() * 0.018
    place_obj("Pie_Knight_Sword", "LeftHand", (0, 0, -0.25), grip, kf(*sword_dir), kf(*sword_flat))
    if shield == "arm":
        fa = wmat("LeftForeArm"); mid = fa.translation + fa.col[1].xyz.normalized() * 0.035
        F, Rt, U = frame()
        place_obj("Pie_Knight_Shield", "LeftForeArm", (0, 0, 0.02), mid - Rt * 0.03, U, -Rt * 0.7 + F * 0.7)
        bpy.data.objects["Pie_Knight_Shield"].hide_render = False
    elif shield == "back":
        F, Rt, U = frame()
        sp = wmat("Spine2").translation
        place_obj("Pie_Knight_Shield", "Spine2", (0, 0, 0.0), sp - F * 0.07 + U * 0.01, U, -F)
        bpy.data.objects["Pie_Knight_Shield"].hide_render = False
    else:
        bpy.data.objects["Pie_Knight_Shield"].hide_render = True
