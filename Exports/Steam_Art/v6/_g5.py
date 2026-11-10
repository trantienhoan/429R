POSE_G1 = dict(turn=20, blade=(-0.78, 0.58, 0.18), r_hand=(-0.09, 0.13, 0.06), shield_face=(0.8, 0.0, 0.6), l_hand=(0.10, -0.02, 0.10))
pose_g(cam_name="CAM_V6_Main", **POSE_G1)
knight_lights(key=48, rim_l=60, rim_r=50, fill=8, top=14); bpy.data.objects["V6_K_Key"].data.color = (0.92, 0.95, 1.0)
sw = bpy.data.objects["Pie_Knight_Sword"]
blade_mid = sw.matrix_world @ Vector((0, 0, -0.05))
K = head_w("Spine1")
sparks("V6_Spark", 22, blade_mid, (0.15, 0.10, 0.15), rad=(0.006, 0.013), color=(0.45, 1.0, 0.25), strength=25, seed=7, shape="star", avoid=[(K + Vector((0, 0, 0.15)), 0.17)])
bpy.data.objects["V6_Sword_Glow"].location = blade_mid
# spiders onto the rooftop, creeping toward the knight (keep their poses, turn to face him)
SP = {"ST_Spider_0": (1.05, -1.25), "ST_Spider_1": (0.55, -1.75), "ST_Spider_3": (-0.85, -0.55), "ST_Spider_2": (1.45, -0.55)}
for n, (x, y) in SP.items():
    o = bpy.data.objects[n]
    if "ORIG" in globals() and n in ORIG:
        o.matrix_world = ORIG[n].copy()
    z0 = o.matrix_world.translation.z
    o.location.x, o.location.y = x, y
    d = (ARM.location - o.location); d.z = 0
    # spider meshes face +Y? turn them by the angle difference to face the knight
    o.rotation_euler.z = math.atan2(d.y, d.x) - math.pi / 2
upd()
# hero burning pumpkin at the right foreground
move_group("hero", (-1.55, 0.25, 0), rot_z=-70)
R("CAM_V6_Main", pct=70, spp=64, out="look_k", show_logo=False)
