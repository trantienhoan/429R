"""v6 look: moon placement, sky colours, knight-only lights (light linking)."""
MOON_GROUP = ["BG2_Moon", "BG2_Moon_Halo", "LP_Moon_Gobo", "Moon_Anchor"] + [o.name for o in bpy.data.objects if o.name.startswith("LP_Moon_Beam_")]
if "ORIG" not in globals():
    ORIG = {o.name: o.matrix_world.copy() for o in bpy.data.objects}
MOON_DIR0 = Vector((-0.105, -0.993, 0.06)).normalized()
def moon_to(dirv, scale=1.0, origin=(0, 0, 0.3)):
    dirv = Vector(dirv).normalized()
    q = MOON_DIR0.rotation_difference(dirv)
    O = Vector(origin)
    Rm = Matrix.Translation(O) @ q.to_matrix().to_4x4() @ Matrix.Translation(-O)
    anchor0 = ORIG["BG2_Moon"].translation
    for n in MOON_GROUP:
        o = bpy.data.objects.get(n)
        if not o: continue
        M = Rm @ ORIG[n]
        if scale != 1.0 and n in ("BG2_Moon", "BG2_Moon_Halo"):
            c = M.translation.copy()
            M = Matrix.Translation(c) @ M.to_3x3().to_4x4() @ Matrix.Diagonal((scale, scale, scale, 1.0))
        o.matrix_world = M
    # the sky's moon glow vectors
    m = bpy.data.materials["BG2_Sky"]
    for nd in m.node_tree.nodes:
        if nd.bl_idname == "ShaderNodeVectorMath":
            for i in nd.inputs:
                if not i.is_linked and hasattr(i, "default_value") and hasattr(i.default_value, "__len__") and len(i.default_value) == 3:
                    v = Vector(i.default_value)
                    if v.length > 0.9 and (v.normalized() - MOON_DIR0).length < 0.05 or ("SKY_MOON_SET" in globals() and (v - SKY_MOON_SET).length < 1e-4):
                        i.default_value = dirv
    globals()["SKY_MOON_SET"] = dirv.copy()
    upd()
def sky_colours(h=(0.05, 0.045, 0.15), mid=(0.018, 0.016, 0.06), hi=(0.006, 0.005, 0.022), top=(0.001, 0.001, 0.006), cloud=(0.42, 0.45, 0.78), gain=1.0):
    m = bpy.data.materials["BG2_Sky"]
    cr = next(n for n in m.node_tree.nodes if n.bl_idname == "ShaderNodeValToRGB")
    cols = [h, mid, hi, top]
    for e, c in zip(cr.color_ramp.elements, cols):
        e.color = (c[0] * gain, c[1] * gain, c[2] * gain, 1)
    mx = m.node_tree.nodes.get("Mix")
    if mx: mx.inputs["B"].default_value = (*cloud, 1)
def knight_recv():
    c = bpy.data.collections.get("V6_Knight_Recv")
    if not c:
        c = bpy.data.collections.new("V6_Knight_Recv")
    for o in bpy.data.objects:
        if o.name.startswith("Pie_Knight") and o.type == "MESH" and o.name not in c.objects:
            c.objects.link(o)
    return c
def knight_lights(cam_name="CAM_V6_Main", key=26.0, rim_l=40.0, rim_r=34.0, fill=5.0, top=10.0):
    rc = knight_recv()
    cam = bpy.data.objects[cam_name]
    K = head_w("Spine1") + Vector((0, 0, 0.08))
    cR, cU, cB = cam.matrix_world.col[0].xyz.normalized(), cam.matrix_world.col[1].xyz.normalized(), cam.matrix_world.col[2].xyz.normalized()
    up = Vector((0, 0, 1))
    def place(name, offset, energy, color, size):
        ob = v6light(name, "AREA", K + offset, energy, color, size=size, aim_at=K)
        ob.light_linking.receiver_collection = rc
        return ob
    # key: front-left, above (screen-left = -cR)
    place("V6_K_Key", (-cR * 0.55 + cB * 0.65 + up * 0.55), key, (1.0, 0.86, 0.70), 0.7)
    # rims: behind-left cool violet, behind-right warm gold
    place("V6_K_RimL", (-cR * 0.65 - cB * 0.55 + up * 0.35), rim_l, (0.55, 0.62, 1.0), 0.35)
    place("V6_K_RimR", (cR * 0.65 - cB * 0.5 + up * 0.25), rim_r, (1.0, 0.7, 0.35), 0.35)
    # fill from the camera, violet
    place("V6_K_Fill", (cB * 1.0 + cR * 0.2 + up * 0.1), fill, (0.6, 0.5, 1.0), 1.0)
    # top light for the helmet dome
    place("V6_K_Top", (up * 0.9 - cB * 0.15), top, (0.8, 0.85, 1.0), 0.6)
