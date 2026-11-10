"""v6 one-time build + per-shot setup + the SHOTS table (exec'd by render_v6.py)."""
HIDE_MAIN = ("ST_Hero", "Menace_Pumpkin_Body", "ST_Spider_", "V6_TMP_", "pumpkin_boss")
POSE_G1 = dict(turn=20, blade=(-0.78, 0.58, 0.18), r_hand=(-0.09, 0.13, 0.06), shield_face=(0.45, 0.0, 0.89),
               l_hand=(0.10, -0.02, 0.10))
BOSS_DEF = dict(clip="pumpkin_boss_jump_slam", frame=65, yaw=200, face=(2.1, -3.35, 1.85))


def load_boss_clips():
    if bpy.data.collections.get("V6_TMP_BossBrowse"):
        return
    path = os.path.abspath(os.path.join(HERE, "..", "..", "..", "Assets", "Blender", "Pumpkin_Boss_Z.blend"))
    col = bpy.data.collections.new("V6_TMP_BossBrowse")
    SC.collection.children.link(col)
    with bpy.data.libraries.load(path, link=False) as (src, dst):
        dst.objects = ["pumpkin_boss_armature", "pumpkin_boss"]
        dst.actions = ["pumpkin_boss_jump_slam", "pumpkin_boss_attack"]
    for o in dst.objects:
        col.objects.link(o)
        o.hide_render = True
    arm = next(o for o in dst.objects if o.type == "ARMATURE")
    arm.location = (40, 0, 0)


def floor_look(alb=0.65, rough=0.38, mats=("ST_Roof_Rooftop_Cursed", "ST_Roof_Rooftop_Base_Lid")):
    for mn in mats:
        m = bpy.data.materials[mn]
        nt = m.node_tree
        b = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
        lk = b.inputs["Base Color"].links
        mul = nt.nodes.get("V6_Darken")
        if mul is None and lk:
            src = lk[0].from_socket
            mul = nt.nodes.new("ShaderNodeMix")
            mul.name = "V6_Darken"
            mul.data_type = "RGBA"
            mul.blend_type = "MULTIPLY"
            mul.inputs[0].default_value = 1.0
            nt.links.new(src, mul.inputs[6])
            nt.links.new(mul.outputs[2], b.inputs["Base Color"])
        if mul:
            mul.inputs[7].default_value = (alb, alb, alb, 1)
        rl = b.inputs["Roughness"].links
        if rl:
            nt.links.remove(rl[0])
        b.inputs["Roughness"].default_value = rough


def build_v6():
    prefs = bpy.context.preferences.addons['cycles'].preferences
    for t in ('OPTIX', 'CUDA'):
        try:
            prefs.compute_device_type = t
            prefs.get_devices()
            if any(d.type == t for d in prefs.devices):
                for d in prefs.devices:
                    d.use = (d.type == t)
                break
        except Exception:
            pass
    SC.cycles.device = 'GPU'
    SC.cycles.use_denoising = True
    globals()["ORIG"] = {o.name: o.matrix_world.copy() for o in bpy.data.objects}
    load_boss_clips()
    move_group("tree", (-0.75, 0.15, 0), 0)          # the Tree of Light to the knight's right (screen right)
    sky_colours()
    km = bpy.data.materials["Knight_Armour_Sword_Shield"]
    next(n for n in km.node_tree.nodes if n.bl_idname == "ShaderNodeMix" and n.name == "Mix").inputs[0].default_value = 0.28
    for n in bpy.data.materials["ST_Boss_Mat"].node_tree.nodes:
        if n.bl_idname == "ShaderNodeMix" and n.blend_type == "MULTIPLY":
            n.inputs[7].default_value = (0.22, 0.22, 0.22, 1)
    floor_look(alb=0.65, rough=0.38)
    SC.view_settings.view_transform = 'AgX'
    SC.view_settings.look = 'AgX - Punchy'
    SC.view_settings.exposure = 0.0
    grade_v6()
    for o in bpy.data.collections["Logos"].all_objects:   # the old 3D logos: the logo is laid on in 2D now
        o.hide_render = True
    print("BUILD v6 done")


def setup_shot(name, shot):
    cam_name = "CAM_V6_" + name
    loc, tgt, lens = shot["cam"]
    w, h = shot["size"]
    sx, sy = shot.get("shift", (0, 0))
    cam_look(cam_name, loc, tgt, lens, w, h, shift_x=sx, shift_y=sy, fit=shot.get('fit', 'HORIZONTAL'))
    cam = bpy.data.objects[cam_name]
    bd = dict(BOSS_DEF)
    bd.update(shot.get("boss", {}))
    boss_v6(**bd)
    pose_cam = shot.get("pose_cam") or cam_name
    if pose_cam not in bpy.data.objects:          # e.g. the main camera, when only another shot is rendered
        mn = pose_cam[len("CAM_V6_"):]
        ml, mt, mlens = SHOTS[mn]["cam"]
        cam_look(pose_cam, ml, mt, mlens, *SHOTS[mn]["size"])
    pose_g(cam_name=pose_cam, **dict(POSE_G1, **shot.get("pose", {})))
    cR = cam.matrix_world.col[0].xyz.normalized()
    cU = cam.matrix_world.col[1].xyz.normalized()
    d = (BOSS_FACE_NOW - cam.location).normalized()
    mu, mr = shot.get("moon", (0.12, 0.06))
    moon_to(d + cU * mu + cR * mr, scale=shot.get("moon_scale", 1.9), origin=cam.location)
    light_v6()
    bpy.data.objects["V6_Fire_Key"].data.energy = 30
    bpy.data.objects["V6_Fill"].data.energy = 8
    bpy.data.objects["V6_Boss_Rim"].data.energy = 450
    bpy.data.objects["V6_Boss_Under"].data.energy = 45
    tree_glow(1.2, 45)
    knight_lights(cam_name=pose_cam, key=48, rim_l=60, rim_r=50, fill=8, top=14)
    bpy.data.objects["V6_K_Key"].data.color = (0.92, 0.95, 1.0)
    haze_v6(density=shot.get("haze", 0.010), color=(0.40, 0.42, 1.0))
    for o in bpy.data.objects:
        if o.type == "LIGHT":
            o.visible_volume_scatter = o.name in ("V6_Moon_Rim", "V6_Moon_Rim2", "V6_Boss_Rim", "V6_Tree_Glow")
    K = head_w("Spine1")
    sparks("V6_Ember", 70, K + Vector((0.2, -1.2, 0.7)), (1.8, 1.4, 0.7), rad=(0.004, 0.011), color=(1.0, 0.42, 0.07),
           strength=40, seed=3, avoid=[(cam.location, 0.45), (K, 0.32)])
    sw = bpy.data.objects["Pie_Knight_Sword"]
    blade_mid = sw.matrix_world @ Vector((0, 0, -0.05))
    sparks("V6_Spark", 22, blade_mid, (0.15, 0.10, 0.15), rad=(0.006, 0.013), color=(0.45, 1.0, 0.25), strength=25,
           seed=7, shape="star", avoid=[(K + Vector((0, 0, 0.15)), 0.17)])
    bpy.data.objects["V6_Sword_Glow"].location = blade_mid
    for fn in shot.get("extra", []):
        fn()


L0 = (-0.20, 1.15, 0.07)
T0 = (0.05, 0.0, 0.40)
HIDE_NOCHARS = HIDE_MAIN + ("Pie_Knight", "ST_Pumpkin_Boss", "V6_Spark", "V6_Sword_Glow")
PORTRAIT_POSE = dict(blade=(-0.45, 0.88, 0.15), turn=12, shield_face=(0.5, 0.0, 0.87))
SHOTS = {
    "Main_Capsule_1232x706": dict(size=(1232, 706), cam=(L0, T0, 22), hide=HIDE_MAIN, logo=(0.02, 0.47, 0.44)),
    "Header_Capsule_920x430": dict(size=(920, 430), cam=(L0, (0.05, 0.0, 0.43), 19), hide=HIDE_MAIN,
                                   boss=dict(face=(2.75, -3.6, 2.05)), logo=(0.03, 0.50, 0.36)),
    "Small_Capsule_462x174": dict(size=(462, 174), cam=((-0.30, 0.95, 0.20), (0.02, 0.0, 0.30), 26), hide=HIDE_MAIN,
                                  boss=dict(face=(2.75, -3.6, 2.05)), logo=(0.02, 0.06, 0.56)),
    "Vertical_Capsule_748x896": dict(size=(748, 896), fit="VERTICAL", cam=((-0.27, 1.50, 0.13), (-0.27, 0.13, 0.30), 34),
                                     hide=HIDE_MAIN, boss=dict(face=(0.15, -3.5, 2.45)), moon=(0.12, 0.10),
                                     pose=PORTRAIT_POSE, logo=(0.13, 0.66, 0.74)),
    "Library_Capsule_600x900": dict(size=(600, 900), fit="VERTICAL", cam=((-0.27, 1.55, 0.13), (-0.27, 0.13, 0.31), 32),
                                    hide=HIDE_MAIN, boss=dict(face=(0.10, -3.5, 2.55)), moon=(0.12, 0.10),
                                    pose=dict(PORTRAIT_POSE, blade=(-0.42, 0.90, 0.15)), logo=(0.09, 0.70, 0.82)),
    "Library_Header_920x430": dict(same="Header_Capsule_920x430"),
    "Library_Hero_3840x1240": dict(size=(3840, 1240), cam=((-0.32, 1.50, 0.09), (-0.10, 0.0, 0.46), 17), hide=HIDE_MAIN,
                                   pose_cam="CAM_V6_Main_Capsule_1232x706"),
    "Page_BG_1438x810": dict(size=(1438, 810), cam=((-0.32, 1.50, 0.09), (-0.10, 0.0, 0.46), 22), hide=HIDE_NOCHARS,
                             pose_cam="CAM_V6_Main_Capsule_1232x706", exposure=-1.1),
    "Promo_KeyArt_1920x1080": dict(size=(1920, 1080), cam=(L0, T0, 21), hide=HIDE_MAIN, logo=(0.02, 0.50, 0.42)),
    "Promo_Portrait_1080x1350": dict(size=(1080, 1350), fit="VERTICAL", cam=((-0.27, 1.50, 0.13), (-0.27, 0.13, 0.30), 32),
                                     hide=HIDE_MAIN, boss=dict(face=(0.15, -3.5, 2.45)), moon=(0.12, 0.10),
                                     pose=PORTRAIT_POSE, logo=(0.12, 0.68, 0.76)),
}
