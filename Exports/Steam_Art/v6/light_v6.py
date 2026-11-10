"""v6 lighting rig (all V6_*), safe to run again. Old character lights are switched off (hide_render), practicals kept."""
OLD_OFF = ["Key_Warm", "Knight_Fill", "LP_Cool_Fill", "LP_Knight_Green_Rim", "LP_Knight_Warm", "LP_Rim_Knight",
           "LP_Rim_Small_L", "LP_Rim_Small_R", "LP_Rim_Top", "Rim_Cool", "KP_Hand_Light", "Moonlight", "ST_Spider_Key",
           "ST_Spider_Rim", "ST_Boss_Rim", "ST_Boss_Face_Light", "ST_Tree_Light", "ST_Tree_Face_Light", "Pumpkin_Glow",
           "LP_Pumpkin_Spill", "ST_Hero_Face_Light", "LP_Pillar_Glow_L", "LP_Pillar_Glow_R"]
def v6col():
    c = bpy.data.collections.get("V6_Lights")
    if not c:
        c = bpy.data.collections.new("V6_Lights"); SC.collection.children.link(c)
    return c
def v6light(name, kind, loc, energy, color, size=0.5, aim_at=None, spot=None, radius=None):
    ob = bpy.data.objects.get(name)
    if ob is None:
        ob = bpy.data.objects.new(name, bpy.data.lights.new(name, kind)); v6col().objects.link(ob)
    d = ob.data
    d.energy = energy; d.color = color
    if kind == "AREA":
        d.shape = 'DISK'; d.size = size
    elif kind in ("POINT", "SPOT"):
        d.shadow_soft_size = radius if radius is not None else size
    if kind == "SPOT" and spot:
        d.spot_size = math.radians(spot); d.spot_blend = 0.6
    ob.location = Vector(loc)
    if aim_at is not None:
        ob.rotation_euler = (Vector(aim_at) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    ob.hide_render = False
    return ob
def light_v6(knight=None, boss_face=None, tree=None, k=1.0):
    for n in OLD_OFF:
        o = bpy.data.objects.get(n)
        if o: o.hide_render = True
    K = Vector(knight) if knight else head_w("Spine1")
    B = Vector(boss_face) if boss_face else Vector(BOSS_FACE_V6)
    T = Vector(tree) if tree else bpy.data.objects["ST_Pumpkin_Tree"].matrix_world.translation + Vector((0, 0, 0.75))
    # moon rim: big soft cool light behind, high, a little to screen-right (-x)
    v6light("V6_Moon_Rim", "AREA", K + Vector((-1.4, -3.2, 2.6)), 220 * k, (0.55, 0.68, 1.0), size=2.2, aim_at=K)
    v6light("V6_Moon_Rim2", "AREA", K + Vector((1.6, -2.6, 1.6)), 90 * k, (0.62, 0.62, 1.0), size=1.6, aim_at=K)
    # warm firelight key from the front-left, low (screen-left = +x)
    v6light("V6_Fire_Key", "AREA", K + Vector((0.75, 0.75, 0.10)), 9.0 * k, (1.0, 0.52, 0.18), size=0.55, aim_at=K + Vector((0, 0, 0.12)))
    # violet fill from the camera side, high
    v6light("V6_Fill", "AREA", K + Vector((-0.4, 1.4, 0.9)), 6.0 * k, (0.55, 0.45, 1.0), size=1.5, aim_at=K)
    # the Tree of Light: golden glow in the canopy
    v6light("V6_Tree_Glow", "POINT", T, 45 * k, (1.0, 0.72, 0.35), radius=0.35)
    # green magic from the blade
    sw = bpy.data.objects["Pie_Knight_Sword"]
    tip = sw.matrix_world @ Vector((0, 0, -0.05))
    v6light("V6_Sword_Glow", "POINT", tip, 3.0 * k, (0.35, 1.0, 0.25), radius=0.08)
    # green bounce from below at the knight's feet (magic floor)
    v6light("V6_Green_Under", "POINT", K + Vector((0, 0.18, -0.12)), 1.2 * k, (0.3, 1.0, 0.35), radius=0.1)
    # boss: orange underlight below its face and a cool rim from behind
    v6light("V6_Boss_Under", "AREA", B + Vector((0, 1.6, -1.4)), 160 * k, (1.0, 0.42, 0.10), size=1.2, aim_at=B)
    v6light("V6_Boss_Rim", "AREA", B + Vector((-1.5, -3.0, 2.5)), 900 * k, (0.5, 0.62, 1.0), size=3.0, aim_at=B)
    print("lights v6 ok")
