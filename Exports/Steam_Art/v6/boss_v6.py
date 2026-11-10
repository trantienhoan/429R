"""v6: re-pose the Pumpkin Boss (bake a clip frame into ST_Pumpkin_Boss), scale it up and place its face."""
BOSS_CLIP, BOSS_F = "pumpkin_boss_attack", 430
BOSS_SCALE_V6 = 2.4
BOSS_FACE_V6 = Vector((-0.30, -3.35, 2.10))
BOSS_YAW_V6 = 180.0
def boss_v6(clip=None, frame=None, scale=None, face=None, yaw=None):
    clip = clip or BOSS_CLIP; frame = BOSS_F if frame is None else frame
    scale = scale or BOSS_SCALE_V6; face = Vector(face) if face is not None else BOSS_FACE_V6
    yaw = BOSS_YAW_V6 if yaw is None else yaw
    col = bpy.data.collections["V6_TMP_BossBrowse"]
    arm = next(o for o in col.objects if o.type == "ARMATURE")
    body = next(o for o in col.objects if o.type == "MESH")
    a = bpy.data.actions[clip]
    ad = arm.animation_data or arm.animation_data_create(); ad.action = a
    if a.slots: ad.action_slot = a.slots[0]
    arm.location = (0, 0, 0); arm.rotation_euler = (0, 0, math.radians(yaw)); arm.scale = (scale,) * 3
    keep = SC.frame_current
    SC.frame_set(frame); bpy.context.view_layer.update()
    fb = arm.pose.bones.get("Face") or arm.pose.bones.get("Head")
    face_now = arm.matrix_world @ ((fb.head + fb.tail) * 0.5)
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(body.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    st = bpy.data.objects["ST_Pumpkin_Boss"]
    old = st.data
    mat = st.material_slots[0].material
    me.name = "ST_Boss_Mesh_v6"
    st.data = me
    for i in range(len(st.material_slots)):
        st.material_slots[i].material = mat
    st.matrix_world = body.matrix_world.copy()
    st.location = st.location + (face - face_now)
    SC.frame_set(keep)
    arm.location = (40, 0, 0)
    if old.users == 0: bpy.data.meshes.remove(old)
    bpy.context.view_layer.update()
    # its two lights follow the face
    fl = bpy.data.objects.get("ST_Boss_Face_Light")
    if fl: fl.location = face + Vector((0, 0.45 * scale, 0))
    rim = bpy.data.objects.get("ST_Boss_Rim")
    if rim:
        rim.location = face + Vector((0, -2.5, 1.7)) * scale
        rim.rotation_euler = (rim.location - face).to_track_quat("Z", "Y").to_euler()
    globals()["BOSS_FACE_NOW"] = face.copy()
    lo, hi = wbbox(["ST_Pumpkin_Boss"])
    print("BOSS v6", clip, frame, "scale", scale, "bbox", tuple(round(v, 2) for v in lo), tuple(round(v, 2) for v in hi))
