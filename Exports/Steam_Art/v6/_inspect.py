import bpy
sc = bpy.context.scene
print("FRAME", sc.frame_current, "range", sc.frame_start, sc.frame_end, "CAMERA", sc.camera.name if sc.camera else None)
arm = bpy.data.objects["Pie_Knight_Armature"]
ad = arm.animation_data
print("ARM pose_position", arm.data.pose_position, "action", ad.action.name if ad and ad.action else None,
      "nla", [(t.name, t.mute, [(s.name, s.action.name if s.action else None, s.frame_start, s.frame_end) for s in t.strips]) for t in ad.nla_tracks] if ad else None,
      "drivers", len(ad.drivers) if ad else 0)
for b in arm.pose.bones:
    if b.constraints:
        print("BONE CONS", b.name, [(c.type, c.name, c.mute, round(c.influence, 2), getattr(c, 'subtarget', '')) for c in c_list] if False else [(c.type, c.name, c.mute, round(c.influence, 2)) for c in b.constraints])
for o in bpy.data.objects:
    if o.name.startswith("Pie_Knight") or o.type == "ARMATURE":
        mods = [(m.type, m.name, "vp" if m.show_viewport else "-", "rd" if m.show_render else "-", (m.object.name if getattr(m, 'object', None) else ""), (m.node_group.name if getattr(m, 'node_group', None) else "")) for m in o.modifiers]
        oad = o.animation_data
        print("OBJ", o.name, o.type, "hide_vp", o.hide_viewport, "hide_get", o.hide_get(), "hide_r", o.hide_render, "parent", o.parent.name if o.parent else None,
              "act", (oad.action.name if oad and oad.action else None), "mods", mods)
# the cameras the user could have looked through
for o in bpy.data.objects:
    if o.type == "CAMERA" and o.name.startswith("CAM_V6"):
        print("CAM", o.name, tuple(round(v, 3) for v in o.location), "lens", round(o.data.lens, 1), "fit", o.data.sensor_fit)
# viewport vs render evaluation of the knight body: compare vertex positions
import numpy as np
def verts(dg, ob):
    e = ob.evaluated_get(dg)
    me = e.to_mesh()
    co = np.array([ (e.matrix_world @ v.co)[:] for v in me.vertices ])
    e.to_mesh_clear()
    return co
dgv = bpy.context.evaluated_depsgraph_get()
for n in ("Pie_Knight", "Pie_Knight_Chest_Piece", "Pie_Knight_Helmet"):
    ob = bpy.data.objects[n]
    cv = verts(dgv, ob)
    print("EVAL", n, "nverts", len(cv), "bbox", cv.min(0).round(3), cv.max(0).round(3))
for ng in bpy.data.node_groups:
    if ng.bl_idname == "GeometryNodeTree":
        print("GN", ng.name, "users", ng.users, "nodes", [n.bl_idname.replace('GeometryNode', '') for n in ng.nodes][:14])
