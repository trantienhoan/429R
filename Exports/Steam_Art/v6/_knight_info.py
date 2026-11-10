import bpy, mathutils
arm = bpy.data.objects["Pie_Knight_Armature"]
print("ARM", arm.location[:], arm.rotation_euler[:], arm.scale[:], "action", arm.animation_data.action.name if arm.animation_data and arm.animation_data.action else None)
for ob in arm.children:
    print("CHILD", ob.name, ob.type, "dims", tuple(round(v,3) for v in ob.dimensions), "parent_type", ob.parent_type, ob.parent_bone)
deps = bpy.context.evaluated_depsgraph_get()
import numpy as np
mn = np.array([9e9]*3); mx = -mn
for ob in arm.children:
    if ob.type != 'MESH': continue
    e = ob.evaluated_get(deps)
    for c in e.bound_box:
        w = ob.matrix_world @ mathutils.Vector(c)
        mn = np.minimum(mn, w); mx = np.maximum(mx, w)
print("KNIGHT WORLD BBOX", mn.round(3), mx.round(3))
for b in arm.pose.bones:
    h = arm.matrix_world @ b.head
    print("BONE", b.name, "head", tuple(round(v,3) for v in h), "rotmode", b.rotation_mode, "q", tuple(round(v,3) for v in b.rotation_quaternion), "e", tuple(round(v,3) for v in b.rotation_euler), "s", tuple(round(v,2) for v in b.scale), "cons", [c.type for c in b.constraints])
