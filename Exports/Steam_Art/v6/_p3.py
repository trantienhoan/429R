exec(open(os.path.join(HERE, "posekit.py"), encoding="utf-8").read(), globals())
cam = bpy.data.objects["CAM_V6_Main"]
def pose_b(turn=-22, punch_up=0.18, punch_side=0.25):
    reset_pose()
    ARM.rotation_euler.z = math.radians(142.0)   # original yaw, then face the camera
    upd()
    F, Rt, U = frame()
    kpos = ARM.location.copy()
    to_cam = (cam.location - kpos); to_cam.z = 0; to_cam.normalize()
    ARM.rotation_euler.z += (math.atan2(to_cam.y, to_cam.x) - math.atan2(F.y, F.x)) + math.radians(turn)
    upd()
    F, Rt, U = frame()
    pb("Hips").location = (0, -4.0, 0); upd()
    rotw("LeftUpLeg", Rt, -28); rotw("LeftLeg", Rt, 30)
    rotw("RightUpLeg", Rt, 24); rotw("RightLeg", Rt, 20)
    for n in ("Spine", "Spine1", "Spine2"):
        rotw(n, Rt, 6); rotw(n, U, 7)
    rotw("Head", U, -14); rotw("Head", F, 8)
    fist_target = cam.location.copy() + Vector((0, 0, punch_up)) + Rt * punch_side
    for n in ("RightArm", "RightForeArm", "RightHand"):
        aim(n, fist_target)
    pb("RightHand").scale = (1.7, 1.7, 1.7)
    # right fist closed
    curl(["RightHandIndex1", "RightHandIndex2", "RightHandIndex3"], 70, 'X')
    # left arm raises the sword up and back over his shoulder
    la = head_w("LeftArm")
    aim("LeftArm", la + U * 0.06 - Rt * 0.05 - F * 0.02)
    aim("LeftForeArm", head_w("LeftForeArm") + U * 0.07 - F * 0.04 - Rt * 0.01)
    aim("LeftHand", head_w("LeftHand") + U * 0.05 - F * 0.03)
    curl(["LeftHandIndex1", "LeftHandIndex2", "LeftHandIndex3"], 70, 'X')
    upd()
    F, Rt, U = frame()
    hand = wmat("LeftHand"); grip = hand.translation + hand.col[1].xyz.normalized() * 0.018
    blade = (U * 1.0 - F * 0.55 - Rt * 0.35).normalized()
    place_obj("Pie_Knight_Sword", "LeftHand", (0, 0, -0.25), grip, blade, F)
    # shield strapped on the left forearm, facing out (his left) and a bit forward
    fa = wmat("LeftForeArm"); mid = fa.translation + fa.col[1].xyz.normalized() * 0.035
    place_obj("Pie_Knight_Shield", "LeftForeArm", (0, 0, 0.02), mid - Rt * 0.03, U, -Rt * 0.7 + F * 0.7)
    print("fist", tuple(round(v, 3) for v in head_w("RightHand")))
pose_b()
def kcam(name, dirv, dist=1.3, lens=50, up=0.1):
    k = head_w("Spine1")
    return tmpcam(name, k + Vector(dirv).normalized() * dist + Vector((0, 0, up)), k + Vector((0, 0, 0.05)), lens=lens, w=800, h=800)
F, Rt, U = frame()
kcam("CAM_TMP_P1", (cam.location - head_w("Spine1")))
kcam("CAM_TMP_P2", Rt * 0.6 + F * 0.8)
kcam("CAM_TMP_P3", -Rt * 0.8 + F * 0.6)
for i in (1, 2, 3):
    R("CAM_TMP_P%d" % i, pct=40, spp=16, out="pose_b%d" % i, show_logo=False, hide=("ST_Hero", "Menace_Pumpkin", "ST_Pumpkin_Tree", "ST_Tree_Pumpkin"))
