def cam_basis(cam):
    M = cam.matrix_world
    return M.col[0].xyz.normalized(), M.col[1].xyz.normalized(), M.col[2].xyz.normalized()   # right, up, back(toward camera)
def pose_g(cam_name="CAM_V6_T2", turn=12, legs="lunge_r", spine_twist=-6, spine_lean=5,
           r_hand=(-0.11, 0.10, 0.07), r_stretch=1.45, blade=(-0.62, 0.75, 0.22),
           l_hand=(0.05, -0.03, 0.15), l_stretch=1.5, l_fist=1.6, l_hint=(0.3, -1.0, 0.0), shield_face=(0.35, 0.0, 1.0),
           head=(4, -4, -8), lift=0.0, hips_roll=0.0):
    """hand offsets and directions are in CAMERA space: (right, up, toward camera)"""
    reset_pose()
    for bn in ARM.data.bones: bn.inherit_scale = 'FULL'
    ARM.rotation_euler.z = math.radians(142.0); ARM.location.z = 0; upd()
    cam = bpy.data.objects[cam_name]
    cR, cU, cB = cam_basis(cam)
    def cs(v): return cR * v[0] + cU * v[1] + cB * v[2]
    F, Rt, U = frame()
    to_cam = (cam.location - ARM.location); to_cam.z = 0; to_cam.normalize()
    ARM.rotation_euler.z += (math.atan2(to_cam.y, to_cam.x) - math.atan2(F.y, F.x)) + math.radians(turn)
    ARM.location.z = lift; upd()
    F, Rt, U = frame()
    if hips_roll: rotw("Hips", F, hips_roll)
    if legs == "lunge_r":
        pb("Hips").location = (0, -4.0, 0); upd()
        rotw("RightUpLeg", Rt, -26); rotw("RightLeg", Rt, 30); rotw("LeftUpLeg", Rt, 24); rotw("LeftLeg", Rt, 16)
        rotw("RightUpLeg", F, -12); rotw("LeftUpLeg", F, 10)
    elif legs == "wide":
        pb("Hips").location = (0, -3.0, 0); upd()
        rotw("LeftUpLeg", F, 14); rotw("RightUpLeg", F, -14)
        rotw("LeftUpLeg", Rt, -12); rotw("LeftLeg", Rt, 14); rotw("RightUpLeg", Rt, 10); rotw("RightLeg", Rt, 10)
    for n in ("Spine", "Spine1", "Spine2"):
        rotw(n, U, spine_twist); rotw(n, Rt, spine_lean)
    yaw, pitch, roll = head
    F, Rt, U = frame()
    rotw("Head", U, yaw); rotw("Head", Rt, pitch); rotw("Head", F, roll)
    # right arm raises the sword up and screen-left
    if r_stretch != 1.0:
        stretch("RightArm", r_stretch); stretch("RightForeArm", r_stretch)
    S = head_w("RightArm")
    ik2("RightArm", "RightForeArm", "RightHand", S + cs(r_hand), cs((-0.2, -1.0, 0.3)), None)
    curl(["RightHandIndex1", "RightHandIndex2", "RightHandIndex3"], 80, 'X')
    curl(["Thumb_01.R", "Thumb_02.R", "Thumb_03.R"], 30, 'X')
    upd()
    hand = wmat("RightHand"); grip = hand.translation + hand.col[1].xyz.normalized() * 0.018
    bd = cs(blade).normalized()
    place_obj("Pie_Knight_Sword", "RightHand", (0, 0, 0.40), grip, -bd, cB)
    # left: fist + shield toward the camera, screen-right
    if l_stretch != 1.0:
        stretch("LeftArm", l_stretch); stretch("LeftForeArm", l_stretch)
    S = head_w("LeftArm")
    ik2("LeftArm", "LeftForeArm", "LeftHand", S + cs(l_hand), cs(l_hint), None)
    pb("LeftHand").scale = (l_fist,) * 3
    curl(["LeftHandIndex1", "LeftHandIndex2", "LeftHandIndex3"], 80, 'X')
    curl(["Thumb_01.L", "Thumb_02.L", "Thumb_03.L"], 30, 'X')
    upd()
    fa = wmat("LeftForeArm"); ax = fa.col[1].xyz.normalized(); mid = fa.translation + ax * 0.03
    sf = cs(shield_face).normalized()
    up_s = (cU - sf * cU.dot(sf)).normalized()
    place_obj("Pie_Knight_Shield", "LeftForeArm", (0, 0, 0.02), mid - ax * 0.012 + sf * 0.05, up_s, sf)
    bpy.data.objects["Pie_Knight_Shield"].hide_render = False
