"""Knight pass: the Pie Knight smiles (happy ^ ^ eyes, still glowing green) and waves with his RIGHT hand.

    "E:/Steam/steamapps/common/Blender/blender.exe" -b Exports/Steam_Art/Steam_Art.blend --python Exports/Steam_Art/knight_pass.py
    ... -- nosave        apply only (for test renders)

Pipeline place (v4): assemble_v2.py -> knight_pass.py -> lighting_pass.py -> render_all.py. lighting_pass does not undo
it and assemble_v2.py does not touch the knight, so re-running it alone later is fine too. The palm turns toward the
saved CAM_Main camera, so after build_steam_art.py (start over) run the pipeline twice or re-run knight_pass at the end. Safe to run again: everything it makes is named KP_... or
Steam_Wave_Pose and is rebuilt from the original Pie_Knight_Pose on each run.

What it changes (only inside Steam_Art.blend, never in Assets/):
- Pose: a NEW action "Steam_Wave_Pose" (a copy of Pie_Knight_Pose) is put on Pie_Knight_Armature. Only the right arm
  chain (RightArm, RightForeArm, RightHand, RightHandIndex1-3, Thumb_01-03.R) is re-keyed: v4 = forearm up beside his
  helmet, open glove raised at visor height, palm to the camera, thumb in. Cartoon help so the wave clears his big
  helmet: shoulder joint up/out 5 cm (under the helmet), upper arm and forearm 60 % longer (the forearm is stretched
  along its length; RightHand's inherit_scale is set to NONE in this copy of the rig so the glove keeps its shape),
  glove 1.5x. Pie_Knight_Pose is kept (fake user) and not edited.
  The sword (parented to the right hand) and the shield stay hidden in renders, as before.
- Eyes: his eyes are two small dome meshes inside Pie_Knight (UV islands in the lime corner of the armour texture).
  Their faces get two new material slots, KP_Smile_Eye_A / _B: a procedural "^" arc that glows green, and the rest of
  the dome in the face's own brown (darkened), so the round eye is gone. No texture is edited.
- KP_Hand_Light: a small warm area light on the glove, light-linked to the knight only (KP_Knight_Receivers).
- Armature property kp_wave_hand_world = where the glove is (world), for placing things next to the knight.
"""
import bpy, sys, math
import numpy as np
from mathutils import Vector, Matrix, Quaternion

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

SRC_ACTION = "Pie_Knight_Pose"
NEW_ACTION = "Steam_Wave_Pose"
ARM = "Pie_Knight_Armature"
BODY = "Pie_Knight"

# ---- pose targets, in the knight's own frame: (right, up, forward). His right = screen-left for the cameras.
# The helmet is a big shell (not skinned, it never moves) that hangs down to his shoulders, and his arms are short:
# a hand raised higher than this disappears behind the helmet. So the wave is out to the side, at the helmet's
# lower edge, palm to the camera (checked with a ray test from the Main and Vertical cameras).
ARM_DIR = (1.0, 0.30, 0.80)         # upper arm: out to his side, a little up and toward the camera
FORE_DIR = (0.45, 1.0, 0.50)        # forearm: out and up
HAND_DIR = (0.35, 1.0, 0.20)        # hand: fingers up and out
PALM_CAM = "CAM_Main_Capsule_1232x706"   # the palm turns to face this camera (falls back to straight ahead)
PALM_FWD_MIX = 0.5                  # 0 = palm straight at the camera, 1 = palm straight ahead of the knight
HAND_TWIST = 0.0                      # extra twist of the hand about its own axis (degrees)
HAND_SCALE = 1.5                    # >1 = cartoon-bigger waving hand (reads better at thumbnail size)
THUMB_SPREAD = 35.0                 # degrees the thumb opens away from the fingers
SHOULDER_SHIFT = (0.02, 0.05, 0.01)   # metres, knight frame: moves the right shoulder joint (hidden under the helmet)
UPPER_STRETCH = 0.6                 # 0.3 = upper arm 30 % longer (cartoon reach)
FORE_STRETCH = 0.6                  # forearm longer by this much (bone stretched lengthwise; the glove keeps its size)
FINGER_OPEN = True                  # straighten the index chain + thumb (open hand instead of a sword grip)
LEAN_DEG = 0.0                      # spine lean; keep 0: the helmet does not follow the body, the head would poke out

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


def pose_wave(arm):
    fr = knight_frame(arm)
    R, U, F = fr
    P = arm.pose.bones
    # small lean of the upper body away from the waving arm (feels livelier; the helmet is not skinned, so tiny)
    if LEAN_DEG:
        sp = P["mixamorig:Spine1"]
        a3 = arm.matrix_world.to_3x3()
        axis = (a3.inverted() @ F).normalized()
        M = pb_m = sp.matrix.copy()
        rot = Matrix.Rotation(math.radians(LEAN_DEG), 3, axis)       # + about forward = top goes to his left
        N = (rot @ M.to_3x3()).to_4x4(); N.translation = M.translation
        sp.matrix = N
        bpy.context.view_layer.update()
    a3i = arm.matrix_world.to_3x3().inverted()
    ua = P["mixamorig:RightArm"]
    if any(SHOULDER_SHIFT):              # shoulder joint a touch up and out (under the helmet, so no seam shows)
        M = ua.matrix.copy()
        M.translation = M.translation + a3i @ (kdir(fr, SHOULDER_SHIFT) * Vector(SHOULDER_SHIFT).length)
        ua.matrix = M
        bpy.context.view_layer.update()
    aim_bone(arm, ua, kdir(fr, ARM_DIR))
    if UPPER_STRETCH:                    # cartoon reach: the elbow slides out along the upper arm
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
    hpos = arm.matrix_world @ hand.head
    palm = ((cam.matrix_world.translation - hpos).normalized() if cam else F)
    palm = (palm.lerp(F, PALM_FWD_MIX)).normalized()
    thumb_to = -(palm.cross(hd)).normalized()
    # the forearm takes the twist first (so the wrist does not candy-wrap), then the hand finishes it
    thumb_local = (hand.matrix.inverted() @ th.matrix.translation).normalized()
    fa = P["mixamorig:RightForeArm"]
    aim_bone(arm, fa, kdir(fr, FORE_DIR), thumb_local, thumb_to)
    # The hand is connected to the forearm (its location is ignored), so the forearm is stretched along its length
    # instead; the hand stops inheriting that scale (bone setting in this copy of the rig) so the glove keeps its shape.
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
        N = R3.to_4x4(); N.translation = M.translation
        t1.matrix = N
        bpy.context.view_layer.update()
    hand.scale = (HAND_SCALE,) * 3
    bpy.context.view_layer.update()
    # where the waving hand ended up (world), for whoever places things next to the knight
    hw = arm.matrix_world @ ((hand.head + hand.tail) * 0.5)
    arm["kp_wave_hand_world"] = tuple(round(c, 4) for c in hw)
    base = arm.matrix_world @ P["mixamorig:Spine2"].head
    for n in ("mixamorig:RightArm", "mixamorig:RightForeArm", "mixamorig:RightHand", "mixamorig:RightHandIndex3"):
        p = arm.matrix_world @ P[n].tail - base
        log("pos", n, "right %.3f up %.3f fwd %.3f" % (p.dot(R), p.dot(U), p.dot(F)))


POSED = ["mixamorig:Spine1", "mixamorig:RightArm", "mixamorig:RightForeArm", "mixamorig:RightHand",
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
    pose_wave(arm)
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
    fr = knight_frame(arm)
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
