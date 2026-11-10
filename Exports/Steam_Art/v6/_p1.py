exec(open(os.path.join(HERE, "posekit.py"), encoding="utf-8").read(), globals())
cam = bpy.data.objects["CAM_V6_Main"]
reset_pose()
# face the camera, turned 22 deg to his left (his right shoulder comes toward the camera)
F, Rt, U = frame()
kpos = ARM.location.copy()
to_cam = (cam.location - kpos); to_cam.z = 0; to_cam.normalize()
cur = math.atan2(F.y, F.x); want = math.atan2(to_cam.y, to_cam.x)
ARM.rotation_euler.z += (want - cur) + math.radians(-22)
upd()
F, Rt, U = frame()
print("F", tuple(round(v,2) for v in F), "Rt", tuple(round(v,2) for v in Rt))
# lunge: hips down a little, legs apart
pb("Hips").location = (0, -4.0, 0)    # local units (armature scale 0.0047 -> ~2 cm)
upd()
rotw("LeftUpLeg", Rt, -28); rotw("LeftLeg", Rt, 30)          # left leg steps forward
rotw("RightUpLeg", Rt, 24); rotw("RightLeg", Rt, 20)          # right leg back
# torso leans into the punch and twists the right shoulder forward
for n in ("Spine", "Spine1", "Spine2"):
    rotw(n, Rt, 6); rotw(n, U, 7)
# head looks at the camera with a small tilt
hp = head_w("Head")
rotw("Head", U, -14); rotw("Head", F, 8)
# right arm punches at the camera, a little up and to his right
fist_target = cam.location.copy() + Vector((0, 0, 0.18)) + Rt * 0.25
aim("RightArm", fist_target)
aim("RightForeArm", fist_target)
aim("RightHand", fist_target)
pb("RightHand").scale = (1.7, 1.7, 1.7)
# left arm chambered back at his side
aim("LeftArm", head_w("LeftArm") - U * 0.08 - F * 0.05 + (-Rt) * 0.03)
aim("LeftForeArm", head_w("LeftForeArm") + F * 0.07 + U * 0.03)
upd()
print("fist at", tuple(round(v,3) for v in head_w("RightHand")))
def kcam(name, off, lens=50):
    k = head_w("Spine1")
    return tmpcam(name, k + off, k + Vector((0, 0, 0.03)), lens=lens, w=800, h=800)
c1 = kcam("CAM_TMP_P1", (cam.location - head_w("Spine1")).normalized() * 1.3 + Vector((0, 0, 0.1)))
c2 = kcam("CAM_TMP_P2", Rt * 1.2 + Vector((0, 0, 0.15)))
R("CAM_TMP_P1", pct=50, spp=16, out="pose_a1", show_logo=False, hide=("ST_Hero", "Menace_Pumpkin"))
R("CAM_TMP_P2", pct=50, spp=16, out="pose_a2", show_logo=False, hide=("ST_Hero", "Menace_Pumpkin"))
R("CAM_V6_Main", pct=50, spp=16, out="pose_a3", show_logo=False)
