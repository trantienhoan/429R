outs = []
for i, (clip, fr, yaw, face) in enumerate([("pumpkin_boss_jump_slam", 65, 200, (2.1, -3.35, 1.85)), ("pumpkin_boss_jump_slam", 60, 205, (2.1, -3.35, 1.85)), ("pumpkin_boss_attack", 456, 200, (2.2, -3.35, 2.0))]):
    boss_v6(clip=clip, frame=fr, yaw=yaw, face=face)
    light_v6()
    bpy.data.objects["V6_Fire_Key"].data.energy = 70
    bpy.data.objects["V6_Fire_Key"].data.size = 1.2
    K = head_w("Spine1")
    bpy.data.objects["V6_Fire_Key"].location = K + Vector((0.8, 0.9, 0.55))
    bpy.data.objects["V6_Fire_Key"].rotation_euler = (K + Vector((0, 0, 0.15)) - bpy.data.objects["V6_Fire_Key"].location).to_track_quat('-Z', 'Y').to_euler()
    bpy.data.objects["V6_Fill"].data.energy = 16
    R("CAM_V6_Main", pct=50, spp=24, out="light_c%d" % i, show_logo=False)
