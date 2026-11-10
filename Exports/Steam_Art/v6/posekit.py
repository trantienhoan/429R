"""v6 posing kit for the Pie Knight (mixamo rig): world-space bone rotations and aims, from the rest pose."""
ARM = bpy.data.objects["Pie_Knight_Armature"]
def pb(n):
    return ARM.pose.bones.get("mixamorig:" + n) or ARM.pose.bones[n]
def upd():
    bpy.context.view_layer.update()
def reset_pose():
    if ARM.animation_data:
        ARM.animation_data.action = None
    for b in ARM.pose.bones:
        b.rotation_mode = 'QUATERNION'; b.rotation_quaternion = (1, 0, 0, 0); b.location = (0, 0, 0); b.scale = (1, 1, 1)
    upd()
def wmat(name):
    return ARM.matrix_world @ pb(name).matrix
def set_wmat(name, M):
    pb(name).matrix = ARM.matrix_world.inverted() @ M
    upd()
def rotw(name, axis, deg, pivot=None):
    M = wmat(name); p = M.translation.copy() if pivot is None else Vector(pivot)
    Rm = Matrix.Translation(p) @ Matrix.Rotation(math.radians(deg), 4, Vector(axis).normalized()) @ Matrix.Translation(-p)
    set_wmat(name, Rm @ M)
def aim(name, target, amount=1.0):
    M = wmat(name); head = M.translation.copy(); ydir = M.col[1].xyz.normalized()
    want = (Vector(target) - head).normalized()
    q = ydir.rotation_difference(want)
    if amount != 1.0:
        q = Quaternion((1, 0, 0, 0)).slerp(q, amount)
    Rm = Matrix.Translation(head) @ q.to_matrix().to_4x4() @ Matrix.Translation(-head)
    set_wmat(name, Rm @ M)
def head_w(name):
    return wmat(name).translation.copy()
def tail_w(name):
    b = pb(name); return ARM.matrix_world @ b.tail
def frame():
    """the knight's own axes in world: F forward, Rt his right, U up (from the hips)"""
    M = wmat("Hips")
    F = M.col[2].xyz.normalized(); Lf = M.col[0].xyz.normalized(); U = M.col[1].xyz.normalized()
    return F, -Lf, U
def curl(names, deg, axis_local='X'):
    """curl finger bones about their own local axis"""
    for n in names:
        b = pb(n)
        ax = {'X': Vector((1, 0, 0)), 'Y': Vector((0, 1, 0)), 'Z': Vector((0, 0, 1))}[axis_local]
        b.rotation_quaternion = b.rotation_quaternion @ Quaternion(ax, math.radians(deg))
    upd()
def save_pose(path):
    import json
    d = {b.name: {"q": list(b.rotation_quaternion), "l": list(b.location), "s": list(b.scale)} for b in ARM.pose.bones}
    d["__object__"] = {"loc": list(ARM.location), "rot": list(ARM.rotation_euler), "scale": list(ARM.scale)}
    for n in ("Pie_Knight_Sword", "Pie_Knight_Shield"):
        o = bpy.data.objects[n]
        d["__" + n] = {"parent_bone": o.parent_bone, "mw": [list(r) for r in o.matrix_world], "hide_render": o.hide_render}
    json.dump(d, open(path, "w"), indent=1)

def world_scale(o):
    return o.matrix_world.to_scale()
def place_obj(name, bone, origin_local, at_world, z_dir, x_dir):
    """parent object to bone, local Z -> z_dir, local X -> x_dir (orthogonalised), local point origin_local at at_world"""
    o = bpy.data.objects[name]
    s = world_scale(o)
    mw0 = o.matrix_world.copy()
    o.parent = ARM; o.parent_type = 'BONE'; o.parent_bone = "mixamorig:" + bone if ("mixamorig:" + bone) in ARM.pose.bones else bone
    upd()
    z = Vector(z_dir).normalized(); x = Vector(x_dir); x = (x - z * x.dot(z)).normalized(); y = z.cross(x)
    Rm = Matrix((x, y, z)).transposed()
    S = Matrix.Diagonal((s.x, s.y, s.z))
    M3 = Rm @ S
    t = Vector(at_world) - M3 @ Vector(origin_local)
    M = M3.to_4x4(); M.translation = t
    o.matrix_world = M
    upd()

def stretch(name, k):
    """cartoon stretch along the bone (children don't inherit the stretch)"""
    b = pb(name)
    b.scale = (1.0, k, 1.0)
    for c in b.children:
        ARM.data.bones[c.name].inherit_scale = 'NONE'
    upd()

def blen(name):
    return (tail_w(name) - head_w(name)).length
def ik2(upper, lower, hand, target, hint, hand_dir=None):
    """2-bone IK in world space: elbow bends toward hint; the hand points along hand_dir (default: forearm line)"""
    S = head_w(upper); a = blen(upper); b = blen(lower)
    T = Vector(target); d_vec = T - S; d = min(d_vec.length, (a + b) * 0.999); dirv = d_vec.normalized()
    hint = Vector(hint); perp = (hint - dirv * hint.dot(dirv))
    perp = perp.normalized() if perp.length > 1e-6 else dirv.orthogonal().normalized()
    cosA = max(-1.0, min(1.0, (a * a + d * d - b * b) / (2 * a * d)))
    A = math.acos(cosA)
    E = S + a * (math.cos(A) * dirv + math.sin(A) * perp)
    aim(upper, E)
    aim(lower, S + dirv * d)
    if hand:
        hd = Vector(hand_dir) if hand_dir is not None else ((S + dirv * d) - E)
        aim(hand, head_w(hand) + hd.normalized())
def kf(f=0.0, r=0.0, u=0.0):
    F, Rt, U = frame()
    return F * f + Rt * r + U * u
