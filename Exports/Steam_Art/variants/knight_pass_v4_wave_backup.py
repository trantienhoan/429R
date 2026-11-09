"""Knight pass: the Pie Knight smiles (happy ^ ^ eyes, still glowing green) and waves with his RIGHT hand.

    "E:/Steam/steamapps/common/Blender/blender.exe" -b Exports/Steam_Art/Steam_Art.blend --python Exports/Steam_Art/knight_pass.py
    ... -- nosave        apply only (for test renders)

Pipeline place (v4): assemble_v2.py -> knight_pass.py -> lighting_pass.py -> render_all.py. lighting_pass does not undo
it and assemble_v2.py does not touch the knight, so re-running it alone later is fine too. The palm turns toward the
saved CAM_Main camera, so after build_steam_art.py (start over) run the pipeline twice or re-run knight_pass at the end. Safe to run again: everything it makes is named KP_... or
Steam_Wave_Pose (plus the helmet's Head vertex group) and is rebuilt from the original Pie_Knight_Pose on each run.

What it changes (only inside Steam_Art.blend, never in Assets/):
- Pose (fixed 2026-11-09, the v4 cartoon stretch made the arm look broken): a NEW action "Steam_Wave_Pose" (a copy of
  Pie_Knight_Pose) is put on Pie_Knight_Armature. Switches at the top: POSE = "wave" or "normal", LEAN_DEGREES,
  HEAD_TILT_DEGREES.
  - Lean (both poses): the upper body leans to HIS left (screen-right) over Spine/Spine1/Spine2 (LEAN_DEGREES) and the
    head tilts a bit more on Neck/Head (HEAD_TILT_DEGREES); legs and feet do not move. The helmet shell gets a
    "mixamorig:Head" vertex group (weight 1, it already had an Armature modifier) so it tilts with the face.
  - "wave": right clavicle lifts 8 deg, upper arm out to the side, elbow bent ~65 deg, forearm up and out, open glove
    beside the lower edge of the helmet, palm to the camera (forearm and wrist share the twist). Real bone lengths, no
    scaling (UPPER_STRETCH / FORE_STRETCH 0, HAND_SCALE 1). The head tilt lifts the helmet off his right shoulder,
    which is what lets the hand show (glove ~95 % visible from CAM_Main, ~80 % from CAM_Vertical).
  - "normal": the user's Pie_Knight_Pose arms (down), with the lean.
  Pie_Knight_Pose is kept (fake user) and not edited. The sword (parented to the right hand) and the shield stay
  hidden in renders, as before. Old stretched version: variants/knight_pass_v4_stretch_backup.py.
- Eyes: his eyes are two small dome meshes inside Pie_Knight (UV islands in the lime corner of the armour texture).
  Their faces get two new material slots, KP_Smile_Eye_A / _B: a procedural "^" arc that glows green, and the rest of
  the dome in the face's own brown (darkened), so the round eye is gone. No texture is edited. The arcs follow the
  head tilt.
- KP_Hand_Light: a small warm area light on the glove, light-linked to the knight only (KP_Knight_Receivers).
- Armature property kp_wave_hand_world = where the glove is (world), for placing things next to the knight.
Test helpers: assets/knight/fix_render.py (close-ups + Steam cameras, never saves), assets/knight/fix_vis_probe.py.
"""
import bpy, sys, math
import numpy as np
from mathutils import Vector, Matrix, Quaternion

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

SRC_ACTION = "Pie_Knight_Pose"
NEW_ACTION = "Steam_Wave_Pose"
ARM = "Pie_Knight_Armature"
BODY = "Pie_Knight"

# ---- THE SWITCHES
POSE = "wave"                       # "wave" = friendly right-hand wave, "normal" = the user's Pie_Knight_Pose (arms down)
LEAN_DEGREES = 12.0                 # the whole upper body leans to HIS left (screen-right), spread over the 3 spine bones;
                                    # the legs and feet do not move. 0 = upright.
HEAD_TILT_DEGREES = 15.0            # the head (and the helmet with it) tilts a bit more to his left, on the neck. This lifts
                                    # the helmet's right edge off his right shoulder so the raised hand can be seen.
HELMET_FOLLOWS_HEAD = True          # the helmet shell is bound to the Head bone (vertex group, weight 1) so it leans/tilts
                                    # with the face; without it the helmet stays put and the face pokes out when leaning.

# ---- wave targets (POSE = "wave"), in the knight's CHEST frame after the lean: (his right, up, forward).
# His right = screen-left for the cameras. The helmet is a big shell hanging down to his shoulders and his arms are
# short, so the wave is a low one: upper arm out to the side, elbow bent, forearm up, open glove beside the
# lower edge of the helmet, palm to the camera. No stretching, no scaling: real joint angles.
CLAVICLE_RAISE = 8.0                # degrees the right shoulder (clavicle) lifts, as anyone does raising an arm
ARM_DIR = (1.0, -0.30, 0.30)        # upper arm: out to his side, a little down and toward the camera
FORE_DIR = (0.80, 1.0, 0.30)        # forearm: up and out (about 50 deg from vertical), a little forward
HAND_DIR = (0.25, 1.0, 0.15)        # hand: fingers up (small wrist bend)
PALM_CAM = "CAM_Main_Capsule_1232x706"   # the palm turns to face this camera (falls back to straight ahead)
PALM_FWD_MIX = 0.5                  # 0 = palm straight at the camera, 1 = palm straight ahead of the knight
FORE_TWIST_SHARE = 0.5              # share of the palm twist taken by the forearm (the rest by the wrist): no candy-wrap
HAND_TWIST = 25.0                   # extra twist of the hand about its own axis (degrees)
THUMB_SPREAD = 30.0                 # degrees the thumb opens away from the fingers
FINGER_OPEN = True                  # straighten the index chain + thumb (open hand instead of a sword grip)
# The old v4 cartoon stretch is OFF: it made the arm look broken. Keep these at 0 / 0 / 1.
UPPER_STRETCH = 0.0                 # upper arm longer by this much (0 = real length)
FORE_STRETCH = 0.0                  # forearm longer by this much (0 = real length)
HAND_SCALE = 1.0                    # glove size (1 = real size)

# ---- eyes
FACE_DARK = 0.15                    # the dome around the arc = face colour x this (the domes catch a lot of light)
LIME_SAT = (0.30, 1.0, 0.12)        # arc colour (more saturated than the texture's lime so it stays green when it glows)
EYE_GLOW = 2.6                      # emission strength of the arcs (the old eyes got ~3 from lighting_pass)
ARC_R = 0.62                        # arc radius, in eye units (the eye dome is about -1..1 each way)
ARC_CY = -0.28                      # arc centre height (lower = arc sits higher in the eye)
ARC_APERTURE = 82.0                 # half-angle of the arc from its top, degrees
ARC_W = 0.16                       # half thickness of the arc stroke
ARC_AA = 0.06                       # soft edge


def log(*a):
    print("[knight_pass]", *a)


def knight_frame(arm):
    M = arm.matrix_world.to_3x3()
    F = (M @ Vector((0, 0, 1))).normalized()
    U = (M @ Vector((0, 1, 0))).normalized()
    R = F.cross(U).normalized()
    return R, U, F


def kdir(frame, v):
    R, U, F = frame
    return (R * v[0] + U * v[1] + F * v[2]).normalized()


# ------------------------------------------------------------------------------------------------ pose
def aim_bone(arm, pb, world_dir, twist_axis_local=None, twist_to_world=None, extra_twist_deg=0.0):
    """Turn pb (about its head) so its Y axis points along world_dir; optionally twist about Y so the local vector
    twist_axis_local points as close as possible to twist_to_world."""
    aw = arm.matrix_world
    a3 = aw.to_3x3()
    inv3 = a3.inverted()
    M = pb.matrix.copy()                          # armature space
    y = M.to_3x3() @ Vector((0, 1, 0))
    d = (inv3 @ world_dir).normalized()
    q = y.normalized().rotation_difference(d)
    R3 = q.to_matrix() @ M.to_3x3()
    if twist_axis_local is not None:
        cur = (R3 @ Vector(twist_axis_local))
        want = (inv3 @ twist_to_world)
        cur_p = (cur - d * cur.dot(d)).normalized()
        want_p = (want - d * want.dot(d)).normalized()
        ang = cur_p.angle(want_p)
        if cur_p.cross(want_p).dot(d) < 0:
            ang = -ang
        R3 = Matrix.Rotation(ang, 3, d) @ R3
    if extra_twist_deg:
        R3 = Matrix.Rotation(math.radians(extra_twist_deg), 3, d) @ R3
    N = R3.to_4x4()
    N.translation = M.translation
    pb.matrix = N
    bpy.context.view_layer.update()


def rotate_bone(pb, world_axis, deg, arm):
    """Turn pb about its head around a world axis."""
    if not deg:
        return
    a3i = arm.matrix_world.to_3x3().inverted()
    ax = (a3i @ world_axis).normalized()
    M = pb.matrix.copy()
    N = (Matrix.Rotation(math.radians(deg), 3, ax) @ M.to_3x3()).to_4x4()
    N.translation = M.translation
    pb.matrix = N
    bpy.context.view_layer.update()


LEAN_BONES = (("mixamorig:Spine", 0.30), ("mixamorig:Spine1", 0.35), ("mixamorig:Spine2", 0.35))
TILT_BONES = (("mixamorig:Neck", 0.5), ("mixamorig:Head", 0.5))


def pose_lean(arm):
    """Upper body leans to his left (- about his forward axis = top goes to his left, as R = F x U); legs untouched."""
    R, U, F = knight_frame(arm)
    P = arm.pose.bones
    for n, w in LEAN_BONES:
        rotate_bone(P[n], F, -LEAN_DEGREES * w, arm)
    for n, w in TILT_BONES:
        rotate_bone(P[n], F, -HEAD_TILT_DEGREES * w, arm)


def chest_frame(arm):
    """Knight frame turned with the lean (the arm targets are relative to the chest)."""
    R, U, F = knight_frame(arm)
    rot = Matrix.Rotation(math.radians(-LEAN_DEGREES), 3, F)
    return (rot @ R).normalized(), (rot @ U).normalized(), F


def pose_wave(arm):
    fr = chest_frame(arm)
    R, U, F = fr
    P = arm.pose.bones
    # the clavicle lifts a little (rotation only); - about forward = its outer end goes up on his right
    rotate_bone(P["mixamorig:RightShoulder"], F, -CLAVICLE_RAISE, arm)
    ua = P["mixamorig:RightArm"]
    aim_bone(arm, ua, kdir(fr, ARM_DIR))
    if UPPER_STRETCH:                    # cartoon reach (off): the elbow slides out along the upper arm
        fa = P["mixamorig:RightForeArm"]
        M = fa.matrix.copy()
        M.translation = M.translation + (ua.matrix.to_3x3() @ Vector((0, 1, 0))).normalized() * ua.length * UPPER_STRETCH
        fa.matrix = M
        bpy.context.view_layer.update()
    # The palm: a right hand with fingers up and the palm to the camera has its thumb pointing at -(palm x fingers).
    hand = P["mixamorig:RightHand"]
    th = P["Thumb_01.R"]
    hd = kdir(fr, HAND_DIR)
    cam = bpy.data.objects.get(PALM_CAM)
    fa = P["mixamorig:RightForeArm"]
    aim_bone(arm, fa, kdir(fr, FORE_DIR))            # aim first (no twist) to know where the hand will be
    hpos = arm.matrix_world @ hand.head
    palm = ((cam.matrix_world.translation - hpos).normalized() if cam else F)
    palm = (palm.lerp(F, PALM_FWD_MIX)).normalized()
    thumb_to = -(palm.cross(hd)).normalized()
    # the forearm takes part of the twist (so the wrist does not candy-wrap), then the hand finishes it
    if FORE_TWIST_SHARE:
        thumb_local = (hand.matrix.inverted() @ th.matrix.translation).normalized()
        before = fa.matrix.copy()
        aim_bone(arm, fa, kdir(fr, FORE_DIR), thumb_local, thumb_to)
        q = before.to_quaternion().slerp(fa.matrix.to_quaternion(), FORE_TWIST_SHARE)
        N = q.to_matrix().to_4x4()
        N.translation = before.translation
        fa.matrix = N
        bpy.context.view_layer.update()
    arm.data.bones["mixamorig:RightHand"].inherit_scale = "NONE" if FORE_STRETCH else "FULL"
    fa.scale = (1.0, 1.0 + FORE_STRETCH, 1.0)
    bpy.context.view_layer.update()
    if FINGER_OPEN:
        for n in ("mixamorig:RightHandIndex1", "mixamorig:RightHandIndex2", "mixamorig:RightHandIndex3",
                  "Thumb_01.R", "Thumb_02.R", "Thumb_03.R"):
            P[n].rotation_quaternion = (1, 0, 0, 0)
        bpy.context.view_layer.update()
    thumb_local = (hand.matrix.inverted() @ th.matrix.translation).normalized()
    aim_bone(arm, hand, hd, thumb_local, thumb_to, HAND_TWIST)
    if THUMB_SPREAD:                     # thumb out, away from the fingers, so the glove reads as an open hand
        t1 = P["Thumb_01.R"]
        tip = t1.matrix.to_3x3() @ Vector((0, 1, 0))
        fing = hand.matrix.to_3x3() @ Vector((0, 1, 0))
        ax = tip.cross(fing).normalized()
        M = t1.matrix.copy()
        R3 = Matrix.Rotation(-math.radians(THUMB_SPREAD), 3, ax) @ M.to_3x3()
        N = R3.to_4x4()
        N.translation = M.translation
        t1.matrix = N
        bpy.context.view_layer.update()
    hand.scale = (HAND_SCALE,) * 3
    bpy.context.view_layer.update()


def joint_report(arm):
    """Right-arm joint angles vs the rest pose: bend (swing) and twist about the bone, degrees; plus scale."""
    P = arm.pose.bones
    for n in ("mixamorig:RightShoulder", "mixamorig:RightArm", "mixamorig:RightForeArm", "mixamorig:RightHand"):
        q = P[n].rotation_quaternion.normalized()
        sw, tw = q.to_swing_twist("Y")
        log("joint", n, "bend %.0f twist %.0f scale %s" % (math.degrees(sw.angle), math.degrees(tw),
                                                          tuple(round(x, 2) for x in P[n].scale)))
    pa = P["mixamorig:RightArm"].matrix.to_3x3() @ Vector((0, 1, 0))
    pf = P["mixamorig:RightForeArm"].matrix.to_3x3() @ Vector((0, 1, 0))
    ph = P["mixamorig:RightHand"].matrix.to_3x3() @ Vector((0, 1, 0))
    log("elbow bend %.0f deg, wrist bend %.0f deg" % (math.degrees(pa.angle(pf)), math.degrees(pf.angle(ph))))


def helmet_follow(arm):
    """Bind the helmet shell to the Head bone (it has an Armature modifier but no groups), or unbind it."""
    h = bpy.data.objects.get("Pie_Knight_Helmet")
    if not h:
        return
    vg = h.vertex_groups.get("mixamorig:Head")
    if HELMET_FOLLOWS_HEAD:
        if not vg:
            vg = h.vertex_groups.new(name="mixamorig:Head")
        vg.add(list(range(len(h.data.vertices))), 1.0, "REPLACE")
        mod = next((m for m in h.modifiers if m.type == "ARMATURE"), None)
        if mod is None:
            mod = h.modifiers.new("Armature", "ARMATURE")
            mod.object = arm
        mod.use_vertex_groups = True
    elif vg:
        h.vertex_groups.remove(vg)


def pose_knight(arm):
    helmet_follow(arm)
    pose_lean(arm)
    if POSE == "wave":
        pose_wave(arm)
    else:                                # normal: the user's pose; only undo what the old stretch did to the rig
        arm.data.bones["mixamorig:RightHand"].inherit_scale = "FULL"
    bpy.context.view_layer.update()
    joint_report(arm)
    R, U, F = knight_frame(arm)
    P = arm.pose.bones
    hand = P["mixamorig:RightHand"]
    hw = arm.matrix_world @ ((hand.head + hand.tail) * 0.5)
    arm["kp_wave_hand_world"] = tuple(round(c, 4) for c in hw)
    base = arm.matrix_world @ P["mixamorig:Spine2"].head
    for n in ("mixamorig:RightShoulder", "mixamorig:RightArm", "mixamorig:RightForeArm", "mixamorig:RightHand",
              "mixamorig:RightHandIndex3"):
        p = arm.matrix_world @ P[n].tail - base
        log("pos", n, "right %.3f up %.3f fwd %.3f" % (p.dot(R), p.dot(U), p.dot(F)))


POSED = ["mixamorig:Spine", "mixamorig:Spine1", "mixamorig:Spine2", "mixamorig:Neck", "mixamorig:Head",
         "mixamorig:RightShoulder", "mixamorig:RightArm", "mixamorig:RightForeArm", "mixamorig:RightHand",
         "mixamorig:RightHandIndex1", "mixamorig:RightHandIndex2", "mixamorig:RightHandIndex3",
         "Thumb_01.R", "Thumb_02.R", "Thumb_03.R"]


def action_fcurves(act):
    out = []
    for L in act.layers:
        for st in L.strips:
            for cb in st.channelbags:
                out.extend(cb.fcurves)
    return out


def build_action(arm):
    src = bpy.data.actions[SRC_ACTION]
    src.use_fake_user = True             # keep the original pose in the file once nothing uses it
    ad = arm.animation_data or arm.animation_data_create()
    # always start from the original pose
    ad.action = src
    if src.slots:
        ad.action_slot = src.slots[0]
    old = bpy.data.actions.get(NEW_ACTION)
    if old:
        bpy.data.actions.remove(old)
    sc = bpy.context.scene
    sc.frame_set(1)
    bpy.context.view_layer.update()
    ad.action = None                     # pose freely; values stay as Pie_Knight_Pose left them
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
    pose_knight(arm)
    vals = {}
    for n in POSED:
        pb = arm.pose.bones[n]
        vals[(n, "rotation_quaternion")] = tuple(pb.rotation_quaternion)
        vals[(n, "scale")] = tuple(pb.scale)
        vals[(n, "location")] = tuple(pb.location)
    new = src.copy()
    new.name = NEW_ACTION
    new.use_fake_user = False
    done = set()
    for fc in action_fcurves(new):
        for (n, prop), q in vals.items():
            if fc.data_path == f'pose.bones["{n}"].{prop}':
                for k in fc.keyframe_points:
                    k.co[1] = q[fc.array_index]
                    k.handle_left[1] = k.handle_right[1] = q[fc.array_index]
                fc.update()
                done.add((n, prop))
    missing = [key for key in vals if key not in done]
    ad.action = new
    if new.slots:
        ad.action_slot = new.slots[0]
    for n, prop in missing:              # channel not keyed in the source: key it now (frame 1)
        pb = arm.pose.bones[n]
        setattr(pb, prop, vals[(n, prop)])
        pb.keyframe_insert(prop, frame=1)
    sc.frame_set(1)
    bpy.context.view_layer.update()
    log("action", new.name, "re-keyed", len(POSED), "bones, newly keyed:", missing)


# ------------------------------------------------------------------------------------------------ eyes
def eye_islands(me):
    """The two eye domes: faces whose UV centre is in the lime corner of the armour texture."""
    uv = me.uv_layers.active.data
    eyes = {"A": [], "B": []}
    for p in me.polygons:
        us = [uv[li].uv for li in p.loop_indices]
        cu = sum(u.x for u in us) / len(us)
        cv = sum(u.y for u in us) / len(us)
        if 0.95 < cu < 0.99:
            if 0.53 < cv < 0.58:
                eyes["A"].append(p.index)
            elif 0.49 < cv < 0.535:
                eyes["B"].append(p.index)
    return eyes


def eye_fit(o, me, faces, fr):
    """Least-squares map UV -> (right, up) in metres for one eye dome. Returns uv centre, 2x2 matrix, half sizes."""
    R, U, F = fr
    uv = me.uv_layers.active.data
    dg = bpy.context.evaluated_depsgraph_get()
    ev = o.evaluated_get(dg)
    em = ev.to_mesh()
    rows, tg = [], []
    for f in faces:
        for li in me.polygons[f].loop_indices:
            vi = me.loops[li].vertex_index
            w = o.matrix_world @ em.vertices[vi].co
            rows.append([uv[li].uv.x, uv[li].uv.y, 1.0])
            tg.append([w.dot(R), w.dot(U)])
    ev.to_mesh_clear()
    A = np.array(rows); T = np.array(tg)
    sol = np.linalg.lstsq(A, T, rcond=None)[0]
    c_uv = A[:, :2].mean(0)
    Tc = T - T.mean(0)
    half = np.abs(Tc).max(0)
    return c_uv, sol[:2, :], half          # (du, dv) @ sol[:2] = (dr, du_world)


def sample_face_colour(o, me, eye_faces, img):
    """Average texture colour of the face skin right around the eyes (not the eye domes)."""
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    uv = me.uv_layers.active.data
    eyeset = set(f for fs in eye_faces.values() for f in fs)
    eye_vs = [me.vertices[v].co for f in eyeset for v in me.polygons[f].vertices]
    c = sum(eye_vs, Vector()) / len(eye_vs)
    rad = max((v - c).length for v in eye_vs) * 1.15
    cols = []
    for p in me.polygons:
        if p.index in eyeset:
            continue
        if (p.center - c).length > rad:
            continue
        for li in p.loop_indices:
            u, v = uv[li].uv
            x = int(min(max(u, 0), 0.9999) * w); y = int(min(max(v, 0), 0.9999) * h)
            col = px[y, x, :3]
            if col[1] - col[0] < 0.15:   # skip anything lime
                cols.append(col)
    eye_cols = []
    for f in eye_faces["A"]:
        for li in me.polygons[f].loop_indices:
            u, v = uv[li].uv
            eye_cols.append(px[int(v * h), int(u * w), :3])
    face = np.median(np.array(cols), 0) if cols else np.array([0.12, 0.07, 0.05])
    lime = np.array(eye_cols).max(0) if eye_cols else np.array([0.4, 1.0, 0.2])
    return tuple(face), tuple(lime), len(cols)


def node(nt, kind, x, y, **props):
    n = nt.nodes.new(kind)
    n.location = (x, y)
    for k, v in props.items():
        setattr(n, k, v)
    return n


def math_n(nt, op, x, y, a=None, b=None, clamp=False):
    n = node(nt, "ShaderNodeMath", x, y, operation=op, use_clamp=clamp)
    for i, v in enumerate((a, b)):
        if v is None:
            continue
        if isinstance(v, (int, float)):
            n.inputs[i].default_value = v
        else:
            nt.links.new(v, n.inputs[i])
    return n.outputs[0]


def eye_material(name, c_uv, M2, half, face_rgb, lime_rgb, base_mat):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    L = nt.links
    out = node(nt, "ShaderNodeOutputMaterial", 1400, 0)
    bsdf = node(nt, "ShaderNodeBsdfPrincipled", 1100, 0)
    L.new(bsdf.outputs[0], out.inputs[0])
    tc = node(nt, "ShaderNodeTexCoord", -900, 0)
    sep = node(nt, "ShaderNodeSeparateXYZ", -700, 0)
    L.new(tc.outputs["UV"], sep.inputs[0])
    du = math_n(nt, "SUBTRACT", -500, 100, sep.outputs[0], float(c_uv[0]))
    dv = math_n(nt, "SUBTRACT", -500, -100, sep.outputs[1], float(c_uv[1]))
    # eye units: x = his right / half width, y = up / half height
    def lin(cu, cv, s, y):
        a = math_n(nt, "MULTIPLY", -300, y, du, float(cu / s))
        b = math_n(nt, "MULTIPLY", -300, y - 60, dv, float(cv / s))
        return math_n(nt, "ADD", -150, y, a, b)
    ex = lin(M2[0][0], M2[1][0], half[0], 200)
    ey = lin(M2[0][1], M2[1][1], half[1], -200)
    ax = math_n(nt, "ABSOLUTE", 0, 200, ex)                   # symmetric arc
    yr = math_n(nt, "SUBTRACT", 0, -200, ey, ARC_CY)          # relative to the arc centre
    comb = node(nt, "ShaderNodeCombineXYZ", 150, 0)
    L.new(ax, comb.inputs[0]); L.new(yr, comb.inputs[1])
    ln = node(nt, "ShaderNodeVectorMath", 300, 0, operation="LENGTH")
    L.new(comb.outputs[0], ln.inputs[0])
    lenv = ln.outputs["Value"]
    ring = math_n(nt, "ABSOLUTE", 450, 150, math_n(nt, "SUBTRACT", 450, 250, lenv, ARC_R))
    th = math.radians(ARC_APERTURE)
    # inside the aperture when yr >= len*cos(th)
    lim = math_n(nt, "MULTIPLY", 450, 0, lenv, math.cos(th))
    inside = math_n(nt, "GREATER_THAN", 600, 0, math_n(nt, "SUBTRACT", 525, 0, yr, lim), 0.0)
    # distance to the arc's end cap at (r sin th, cy + r cos th)
    ex2 = math_n(nt, "SUBTRACT", 450, -150, ax, ARC_R * math.sin(th))
    ey2 = math_n(nt, "SUBTRACT", 450, -250, yr, ARC_R * math.cos(th))
    comb2 = node(nt, "ShaderNodeCombineXYZ", 600, -200)
    L.new(ex2, comb2.inputs[0]); L.new(ey2, comb2.inputs[1])
    ln2 = node(nt, "ShaderNodeVectorMath", 750, -200, operation="LENGTH")
    L.new(comb2.outputs[0], ln2.inputs[0])
    mix = node(nt, "ShaderNodeMix", 800, 0, data_type="FLOAT")
    L.new(inside, mix.inputs["Factor"])
    L.new(ln2.outputs["Value"], mix.inputs["A"])
    L.new(ring, mix.inputs["B"])
    d = mix.outputs["Result"]
    mr = node(nt, "ShaderNodeMapRange", 950, 0, interpolation_type="SMOOTHSTEP")
    L.new(d, mr.inputs["Value"])
    mr.inputs["From Min"].default_value = ARC_W - ARC_AA
    mr.inputs["From Max"].default_value = ARC_W + ARC_AA
    mr.inputs["To Min"].default_value = 1.0
    mr.inputs["To Max"].default_value = 0.0
    mask = mr.outputs["Result"]
    # soft inner halo so the arc reads at thumbnail size
    halo = node(nt, "ShaderNodeMapRange", 950, -250, interpolation_type="SMOOTHSTEP")
    L.new(d, halo.inputs["Value"])
    halo.inputs["From Min"].default_value = ARC_W
    halo.inputs["From Max"].default_value = ARC_W + 0.35
    halo.inputs["To Min"].default_value = 0.18
    halo.inputs["To Max"].default_value = 0.0
    col = node(nt, "ShaderNodeMix", 1100, 300, data_type="RGBA")
    L.new(mask, col.inputs["Factor"])
    col.inputs["A"].default_value = (*face_rgb, 1)
    col.inputs["B"].default_value = (*lime_rgb, 1)
    L.new(col.outputs["Result"], bsdf.inputs["Base Color"])
    bsdf.inputs["Emission Color"].default_value = (*lime_rgb, 1)
    es = math_n(nt, "MULTIPLY", 1100, -300, math_n(nt, "MAXIMUM", 1050, -300, mask, halo.outputs["Result"]), EYE_GLOW)
    L.new(es, bsdf.inputs["Emission Strength"])
    rough = node(nt, "ShaderNodeMix", 950, 300, data_type="FLOAT")
    L.new(mask, rough.inputs["Factor"])
    rough.inputs["A"].default_value = 0.75
    rough.inputs["B"].default_value = 0.35
    bsdf.inputs["Specular IOR Level"].default_value = 0.15
    L.new(rough.outputs["Result"], bsdf.inputs["Roughness"])
    return m


def smile_eyes(arm):
    o = bpy.data.objects[BODY]
    me = o.data
    R, U, F = knight_frame(arm)
    tilt = Matrix.Rotation(-math.radians(LEAN_DEGREES + HEAD_TILT_DEGREES), 3, F)   # the arcs stay upright on the face
    fr = ((tilt @ R).normalized(), (tilt @ U).normalized(), F)
    eyes = eye_islands(me)
    if not eyes["A"] or not eyes["B"]:
        raise RuntimeError("knight_pass: eye faces not found")
    base = me.materials[0]
    tex = next(n for n in base.node_tree.nodes if n.type == "TEX_IMAGE" and n.image and "Diffuse" in n.image.name)
    face_rgb, lime_rgb, nface = sample_face_colour(o, me, eyes, tex.image)
    face_rgb = tuple(float(c) * FACE_DARK for c in face_rgb)
    lime_rgb = LIME_SAT
    log("face colour", np.round(face_rgb, 3), "from", nface, "samples; lime", np.round(lime_rgb, 3))
    for key in ("A", "B"):
        c_uv, M2, half = eye_fit(o, me, eyes[key], fr)
        mat = eye_material("KP_Smile_Eye_" + key, c_uv, M2, half, face_rgb, lime_rgb, base)
        idx = next((i for i, m in enumerate(me.materials) if m and m.name == mat.name), None)
        if idx is None:
            me.materials.append(mat)
            idx = len(me.materials) - 1
        for f in eyes[key]:
            me.polygons[f].material_index = idx
        log("eye", key, "faces", len(eyes[key]), "slot", idx, "half size m", np.round(half, 4))
    me.update()


# ------------------------------------------------------------------------------------------------ hand light
HAND_LIGHT_W = 7.0                  # small warm light on the waving hand (it sits in the helmet's shadow); 0 = off
HAND_LIGHT_COL = (1.0, 0.62, 0.32)  # pumpkin-warm (the hero pumpkin is on this side)
HAND_LIGHT_OFS = (0.10, -0.04, 0.12)  # from the hand, in the knight frame (right, up, forward)


def hand_light(arm):
    old = bpy.data.objects.get("KP_Hand_Light")
    if old:
        bpy.data.objects.remove(old)
    for ld in [l for l in bpy.data.lights if l.name.startswith("KP_Hand_Light") and l.users == 0]:
        bpy.data.lights.remove(ld)
    if HAND_LIGHT_W <= 0:
        return
    fr = knight_frame(arm)
    P = arm.pose.bones
    target = arm.matrix_world @ ((P["mixamorig:RightHand"].head + P["mixamorig:RightHand"].tail) * 0.5)
    pos = target + kdir(fr, HAND_LIGHT_OFS) * Vector(HAND_LIGHT_OFS).length
    ld = bpy.data.lights.new("KP_Hand_Light", "AREA")
    ld.energy, ld.color, ld.size = HAND_LIGHT_W, HAND_LIGHT_COL, 0.08
    lo = bpy.data.objects.new("KP_Hand_Light", ld)
    coll = bpy.data.collections.get("Lights") or bpy.context.scene.collection
    coll.objects.link(lo)
    lo.location = pos
    lo.rotation_euler = (target - pos).to_track_quat("-Z", "Y").to_euler()
    lo.visible_camera = False
    lo.visible_glossy = False
    rc = bpy.data.collections.get("KP_Knight_Receivers") or bpy.data.collections.new("KP_Knight_Receivers")
    for o in list(rc.objects):
        rc.objects.unlink(o)
    for o in bpy.data.objects:
        if o.type == "MESH" and o.name.startswith("Pie_Knight"):
            rc.objects.link(o)
    lo.light_linking.receiver_collection = rc
    log("hand light at", tuple(round(c, 3) for c in pos))


def apply(save=True):
    arm = bpy.data.objects[ARM]
    build_action(arm)
    smile_eyes(arm)
    hand_light(arm)
    if save:
        bpy.ops.wm.save_mainfile()
        log("saved", bpy.data.filepath)


if __name__ == "__main__":
    apply(save="nosave" not in ARGS)
