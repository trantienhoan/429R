"""v6 icon (2026-11-10): the user's posed knight in front of a big moon on a night-sky disc, inside a gold ring,
like the user's painted badge (helmet on top, "429" across the bottom) but in the v6 key-art style. The "429" logo is
laid on afterwards in 2D (icon_compose.py). Exec'd in the helper Blender on Icon_v6_work.blend (a COPY of the user's
Steam_Art_v6_pose.blend). Safe to run again: everything it makes is ICON_*.
Layout numbers are FRACTIONS OF THE ICON (x from left, y from top), converted with the ortho frame width."""
import bmesh

P = dict(
    ortho=0.66,                     # frame width in metres (ortho camera)
    helmet_at=(0.50, 0.34),         # where the helmet centre sits in the icon
    moon_at=(0.53, 0.33), moon_r=0.265,
    moon_col=(1.0, 0.90, 0.70), moon_str=3.6, crater=0.45,
    halo_col=(0.45, 0.6, 1.0), halo_str=0.7, halo_k=0.06,
    sky_in=(0.018, 0.026, 0.13), sky_out=(0.004, 0.004, 0.03), sky_r=0.50,
    star_str=2.5,
    ring_r=0.455, ring_w=0.034, ring_d=0.018,      # ring centre-line radius, radial half-width, depth half-width
    ring_gold=(1.0, 0.62, 0.20), ring_rough=0.25, ring_light=6.0,
    rim_moon=18.0, k_top=0.35,
    sword_tilt=16.0, sword_scale=0.70,          # 2026-11-10: keeps the tip inside a 4% margin + 16% rounded corners
    bloom=0.12, fire_glow=3.0, embers=9,
)


def icon_col():
    c = bpy.data.collections.get("ICON")
    if not c:
        c = bpy.data.collections.new("ICON")
        SC.collection.children.link(c)
    return c


def obj_replace(name, mesh):
    old = bpy.data.objects.get(name)
    if old:
        bpy.data.objects.remove(old, do_unlink=True)
    ob = bpy.data.objects.new(name, mesh)
    icon_col().objects.link(ob)
    return ob


def basis():
    """the icon camera's axes: right, up, toward camera (horizontal view from the main camera's side)"""
    mc = bpy.data.objects["CAM_V6_Main_Capsule_1232x706"]
    hd = head_w("Head")
    d = Vector((mc.location.x - hd.x, mc.location.y - hd.y, 0.0)).normalized()
    up = Vector((0, 0, 1))
    right = up.cross(d).normalized()
    return right, up, d


def helmet_centre():
    return head_w("Head") + Vector((0, 0, 0.125))


def frame_centre():
    """world point at the icon's centre, in the helmet's depth plane"""
    right, up, toward = basis()
    W = P["ortho"]
    hx, hy = P["helmet_at"]
    return helmet_centre() - right * (hx - 0.5) * W + up * (hy - 0.5) * W


def frac_to_local(fx, fy):
    """icon fraction -> metres in the camera plane, from the frame centre (x right, y up)"""
    W = P["ortho"]
    return (fx - 0.5) * W, (0.5 - fy) * W


def node(nt, kind, **kw):
    n = nt.nodes.new(kind)
    for k, v in kw.items():
        setattr(n, k, v)
    return n


def backdrop_material():
    W = P["ortho"]
    mx0, my0 = frac_to_local(*P["moon_at"])
    moon_r = P["moon_r"] * W
    m = bpy.data.materials.get("ICON_Sky") or bpy.data.materials.new("ICON_Sky")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    L = nt.links.new
    out = node(nt, "ShaderNodeOutputMaterial")
    tc = node(nt, "ShaderNodeTexCoord")
    sep = node(nt, "ShaderNodeSeparateXYZ"); L(tc.outputs["Object"], sep.inputs[0])
    mx = node(nt, "ShaderNodeMath", operation="SUBTRACT"); L(sep.outputs[0], mx.inputs[0]); mx.inputs[1].default_value = mx0
    my = node(nt, "ShaderNodeMath", operation="SUBTRACT"); L(sep.outputs[1], my.inputs[0]); my.inputs[1].default_value = my0
    cmb = node(nt, "ShaderNodeCombineXYZ"); L(mx.outputs[0], cmb.inputs[0]); L(my.outputs[0], cmb.inputs[1])
    dist = node(nt, "ShaderNodeVectorMath", operation="LENGTH"); L(cmb.outputs[0], dist.inputs[0])
    rad = node(nt, "ShaderNodeVectorMath", operation="LENGTH"); L(tc.outputs["Object"], rad.inputs[0])
    skyf = node(nt, "ShaderNodeMapRange"); L(rad.outputs["Value"], skyf.inputs["Value"])
    skyf.inputs["From Min"].default_value = 0.0; skyf.inputs["From Max"].default_value = P["sky_r"] * W
    skym = node(nt, "ShaderNodeMix", data_type="RGBA"); L(skyf.outputs[0], skym.inputs[0])
    skym.inputs[6].default_value = (*P["sky_in"], 1); skym.inputs[7].default_value = (*P["sky_out"], 1)
    vor = node(nt, "ShaderNodeTexVoronoi"); vor.inputs["Scale"].default_value = 120.0; L(tc.outputs["Object"], vor.inputs["Vector"])
    st = node(nt, "ShaderNodeMapRange"); L(vor.outputs["Distance"], st.inputs["Value"])
    st.inputs["From Min"].default_value = 0.035; st.inputs["From Max"].default_value = 0.0
    st.inputs["To Min"].default_value = 0.0; st.inputs["To Max"].default_value = P["star_str"]
    nz = node(nt, "ShaderNodeTexNoise"); nz.inputs["Scale"].default_value = 40.0; L(tc.outputs["Object"], nz.inputs["Vector"])
    sth = node(nt, "ShaderNodeMath", operation="GREATER_THAN"); L(nz.outputs["Fac"], sth.inputs[0]); sth.inputs[1].default_value = 0.62
    stm = node(nt, "ShaderNodeMath", operation="MULTIPLY"); L(st.outputs[0], stm.inputs[0]); L(sth.outputs[0], stm.inputs[1])
    sky_plus = node(nt, "ShaderNodeMix", data_type="RGBA", blend_type="ADD"); sky_plus.inputs[0].default_value = 1.0
    L(skym.outputs[2], sky_plus.inputs[6])
    stc = node(nt, "ShaderNodeCombineColor"); L(stm.outputs[0], stc.inputs[0]); L(stm.outputs[0], stc.inputs[1]); L(stm.outputs[0], stc.inputs[2])
    L(stc.outputs[0], sky_plus.inputs[7])
    hs = node(nt, "ShaderNodeMath", operation="SUBTRACT"); L(dist.outputs["Value"], hs.inputs[0]); hs.inputs[1].default_value = moon_r
    hc = node(nt, "ShaderNodeMath", operation="MAXIMUM"); L(hs.outputs[0], hc.inputs[0]); hc.inputs[1].default_value = 0.0
    hd_ = node(nt, "ShaderNodeMath", operation="DIVIDE"); L(hc.outputs[0], hd_.inputs[0]); hd_.inputs[1].default_value = -P["halo_k"] * W
    he = node(nt, "ShaderNodeMath", operation="EXPONENT"); L(hd_.outputs[0], he.inputs[0])
    hm = node(nt, "ShaderNodeMath", operation="MULTIPLY"); L(he.outputs[0], hm.inputs[0]); hm.inputs[1].default_value = P["halo_str"]
    halo = node(nt, "ShaderNodeMix", data_type="RGBA", blend_type="ADD"); L(hm.outputs[0], halo.inputs[0])
    L(sky_plus.outputs[2], halo.inputs[6]); halo.inputs[7].default_value = (*P["halo_col"], 1)
    me_ = node(nt, "ShaderNodeMapRange"); L(dist.outputs["Value"], me_.inputs["Value"])
    me_.inputs["From Min"].default_value = moon_r; me_.inputs["From Max"].default_value = moon_r - 0.003
    cz = node(nt, "ShaderNodeTexNoise"); cz.inputs["Scale"].default_value = 9.0; cz.inputs["Detail"].default_value = 6.0
    L(tc.outputs["Object"], cz.inputs["Vector"])
    cr = node(nt, "ShaderNodeMapRange"); L(cz.outputs["Fac"], cr.inputs["Value"])
    cr.inputs["From Min"].default_value = 0.45; cr.inputs["From Max"].default_value = 0.62
    cr.inputs["To Min"].default_value = 1.0; cr.inputs["To Max"].default_value = 1.0 - P["crater"]
    cv = node(nt, "ShaderNodeTexVoronoi"); cv.inputs["Scale"].default_value = 22.0; L(tc.outputs["Object"], cv.inputs["Vector"])
    cvr = node(nt, "ShaderNodeMapRange"); L(cv.outputs["Distance"], cvr.inputs["Value"])
    cvr.inputs["From Min"].default_value = 0.10; cvr.inputs["From Max"].default_value = 0.28
    cvr.inputs["To Min"].default_value = 0.82; cvr.inputs["To Max"].default_value = 1.0
    crm = node(nt, "ShaderNodeMath", operation="MULTIPLY"); L(cr.outputs[0], crm.inputs[0]); L(cvr.outputs[0], crm.inputs[1])
    mcol = node(nt, "ShaderNodeMix", data_type="RGBA", blend_type="MULTIPLY"); mcol.inputs[0].default_value = 1.0
    s = P["moon_str"]
    mcol.inputs[6].default_value = (P["moon_col"][0] * s, P["moon_col"][1] * s, P["moon_col"][2] * s, 1)
    crc = node(nt, "ShaderNodeCombineColor"); L(crm.outputs[0], crc.inputs[0]); L(crm.outputs[0], crc.inputs[1]); L(crm.outputs[0], crc.inputs[2])
    L(crc.outputs[0], mcol.inputs[7])
    fin = node(nt, "ShaderNodeMix", data_type="RGBA"); L(me_.outputs[0], fin.inputs[0])
    L(halo.outputs[2], fin.inputs[6]); L(mcol.outputs[2], fin.inputs[7])
    em = node(nt, "ShaderNodeEmission"); L(fin.outputs[2], em.inputs["Color"]); em.inputs["Strength"].default_value = 1.0
    L(em.outputs[0], out.inputs["Surface"])
    return m


def gold_material():
    m = bpy.data.materials.get("ICON_Gold") or bpy.data.materials.new("ICON_Gold")
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = (*P["ring_gold"], 1)
    b.inputs["Metallic"].default_value = 1.0
    b.inputs["Roughness"].default_value = P["ring_rough"]
    return m


def build_icon():
    right, up, toward = basis()
    W = P["ortho"]
    C = frame_centre()
    R3 = Matrix((right, up, toward)).transposed()
    # camera (ortho), looking straight at the frame centre
    cd = bpy.data.cameras.get("CAM_ICON") or bpy.data.cameras.new("CAM_ICON")
    cd.type = 'ORTHO'; cd.ortho_scale = W; cd.clip_start = 0.01; cd.clip_end = 50
    cd.shift_x = cd.shift_y = 0.0
    cam = bpy.data.objects.get("CAM_ICON") or bpy.data.objects.new("CAM_ICON", cd)
    if not cam.users_collection:
        icon_col().objects.link(cam)
    cam.location = C + toward * 3.0
    cam.rotation_euler = (-toward).to_track_quat('-Z', 'Y').to_euler()
    cam["steam_w"], cam["steam_h"] = 1024, 1024
    # backdrop disc 0.9 m behind, ending under the ring (outside the ring = transparent)
    me = bpy.data.meshes.new("ICON_Backdrop")
    bm = bmesh.new()
    bmesh.ops.create_circle(bm, cap_ends=True, radius=(P["ring_r"] + 0.25 * P["ring_w"]) * W, segments=192)
    bm.to_mesh(me); bm.free()
    bd = obj_replace("ICON_Backdrop", me)
    M = R3.to_4x4(); M.translation = C - toward * 0.9
    bd.matrix_world = M
    me.materials.append(backdrop_material())
    bd.visible_shadow = False
    bd.visible_glossy = False          # the helmet must not mirror the moon (it went milky)
    bd.visible_diffuse = False
    # gold ring: a torus with a flat, wide cross-section (reads as a solid gold band)
    rm = bpy.data.meshes.new("ICON_Ring")
    bm = bmesh.new()
    seg, tseg = 192, 28
    Rr, a_, b_ = P["ring_r"] * W, P["ring_w"] * W, P["ring_d"] * W
    rows = []
    for i in range(seg):
        t = 2 * math.pi * i / seg
        row = []
        for j in range(tseg):
            p = 2 * math.pi * j / tseg
            cx, cz = math.cos(p), math.sin(p)
            # superellipse cross-section: flatter faces, rounded edges
            ex = math.copysign(abs(cx) ** 0.6, cx) * a_
            ez = math.copysign(abs(cz) ** 0.6, cz) * b_
            row.append(bm.verts.new(((Rr + ex) * math.cos(t), (Rr + ex) * math.sin(t), ez)))
        rows.append(row)
    for i in range(seg):
        for j in range(tseg):
            bm.faces.new((rows[i][j], rows[(i + 1) % seg][j], rows[(i + 1) % seg][(j + 1) % tseg], rows[i][(j + 1) % tseg]))
    bm.to_mesh(rm); bm.free()
    for p in rm.polygons:
        p.use_smooth = True
    ring = obj_replace("ICON_Ring", rm)
    M = R3.to_4x4(); M.translation = C - toward * 0.30
    ring.matrix_world = M
    rm.materials.append(gold_material())
    # lights
    hc = helmet_centre()
    krc = bpy.data.collections.get("V6_Knight_Recv")
    rim = v6light("ICON_MoonRim", "AREA", hc - toward * 0.9 + up * 0.25 + right * 0.05, P["rim_moon"], (0.80, 0.86, 1.0), size=0.35, aim_at=hc)
    if krc:
        rim.light_linking.receiver_collection = krc
    rc = bpy.data.collections.get("ICON_Ring_Recv") or bpy.data.collections.new("ICON_Ring_Recv")
    if ring.name not in rc.objects:
        rc.objects.link(ring)
    for nm, off, col, k in (("ICON_RingKey", -right * 0.45 + up * 0.55 + toward * 0.9, (1.0, 0.86, 0.62), 1.0),
                            ("ICON_RingFill", right * 0.55 - up * 0.45 + toward * 0.7, (0.75, 0.70, 1.0), 0.45)):
        lt = v6light(nm, "AREA", C + off, P["ring_light"] * k, col, size=0.6, aim_at=C)
        lt.light_linking.receiver_collection = rc
    # the 429's green fire lights him from below (ties the 2D logo to the 3D knight)
    fg = v6light("ICON_FireGlow", "POINT", hc - up * 0.17 + toward * 0.12, P["fire_glow"], (0.30, 1.0, 0.30), radius=0.08)
    if krc:
        fg.light_linking.receiver_collection = krc
    # a few warm embers in the sky (as in the key art), kept off the helmet and the moon's middle
    exec(open(os.path.join(HERE, "fx_v6.py"), encoding="utf-8").read(), globals())
    mx, my = frac_to_local(*P["moon_at"])
    moon_c = C + right * mx + up * my - toward * 0.5
    sparks("ICON_Ember", P["embers"], C - toward * 0.5, (0.27 * W, 0.27 * W, 0.02), rad=(0.0035, 0.007),
           color=(1.0, 0.45, 0.08), strength=60, seed=11,
           avoid=[(moon_c, P["moon_r"] * W * 0.75), (hc - toward * 0.5, 0.17)])
    for o in bpy.data.objects:
        if o.name.startswith("ICON_Ember_"):
            # keep them inside the ring
            d = o.location - (C - toward * 0.5)
            if Vector((d.dot(right), d.dot(up))).length > (P["ring_r"] - 0.03) * W:
                bpy.data.objects.remove(o, do_unlink=True)
    return cam


def icon_sword():
    """for the icon only: turn the user's sword about its grip toward upright (in the camera plane)"""
    sw = bpy.data.objects["Pie_Knight_Sword"]
    if "ICON_SWORD_MW" not in globals():
        globals()["ICON_SWORD_MW"] = sw.matrix_world.copy()
    M0 = ICON_SWORD_MW
    right, up, toward = basis()
    grip = M0 @ Vector((0, 0, 0.40))
    Rm = Matrix.Translation(grip) @ Matrix.Rotation(math.radians(-P["sword_tilt"]), 4, toward) @ Matrix.Translation(-grip)
    k = P["sword_scale"]
    S = Matrix.Translation(grip) @ Matrix.Diagonal((k, k, k, 1.0)) @ Matrix.Translation(-grip)
    sw.matrix_world = S @ Rm @ M0
    bpy.context.view_layer.update()
    mid = sw.matrix_world @ Vector((0, 0, -0.05))
    exec(open(os.path.join(HERE, "fx_v6.py"), encoding="utf-8").read(), globals())
    sparks("V6_Spark", 18, mid, (0.14, 0.10, 0.14), rad=(0.005, 0.011), color=(0.45, 1.0, 0.25), strength=25, seed=7,
           shape="star", avoid=[(helmet_centre(), 0.19)])
    if "V6_Sword_Glow" in bpy.data.objects:
        bpy.data.objects["V6_Sword_Glow"].location = mid
    # sparkles only inside the ring (outside it is see-through)
    C = frame_centre()
    for o in [o for o in bpy.data.objects if o.name.startswith("V6_Spark_")]:
        d = o.location - C
        if Vector((d.dot(right), d.dot(up))).length > (P["ring_r"] - 0.035) * P["ortho"]:
            bpy.data.objects.remove(o, do_unlink=True)


ICON_KEEP = ("Pie_Knight", "V6_K_", "V6_Spark", "ICON_", "V6_Sword_Glow", "V6_TMP_L")


def icon_render(out="icon_test", pct=50, spp=96, mask=False):
    """mask=True: only the knight (+ sword, sparkles), for the 2D compose (where the logo's fire goes behind him)"""
    cam = bpy.data.objects["CAM_ICON"]
    keep = {}
    top = bpy.data.objects.get("V6_K_Top")
    top_e = top.data.energy if top else None
    for o in bpy.data.objects:
        keep[o.name] = o.hide_render
        if not o.name.startswith(ICON_KEEP):
            o.hide_render = True
        if mask and o.name in ("ICON_Backdrop", "ICON_Ring"):
            o.hide_render = True
    try:
        if top:
            top.data.energy = top_e * P["k_top"]
        glare = SC.compositing_node_group.nodes.get("Glare") if SC.compositing_node_group else None
        g_in = next((i for i in glare.inputs if i.name == "Strength"), None) if glare else None
        g_old = g_in.default_value if g_in else None
        if g_in:
            g_in.default_value = P["bloom"]
        SC.camera = cam
        SC.render.resolution_x = SC.render.resolution_y = 1024
        SC.render.resolution_percentage = pct
        SC.cycles.samples = spp
        SC.render.film_transparent = True
        SC.render.image_settings.file_format = "PNG"
        SC.render.image_settings.color_mode = "RGBA"
        SC.render.image_settings.color_depth = "8"
        path = os.path.join(HERE, "icon", out + ".png")
        SC.render.filepath = path
        bpy.ops.render.render(write_still=True)
        print("ICON RENDERED", path)
        return path
    finally:
        if top:
            top.data.energy = top_e
        try:
            if g_in is not None:
                g_in.default_value = g_old
        except NameError:
            pass
        for n, h in keep.items():
            if n in bpy.data.objects:
                bpy.data.objects[n].hide_render = h


def eyes_boost(k):
    """icon only: brighter smile eyes (they must read at Steam's 32 px list size)"""
    for mn in ("KP_Smile_Eye_A", "KP_Smile_Eye_B"):
        m = bpy.data.materials.get(mn)
        if not m:
            continue
        n = m.node_tree.nodes.get("Math.018")
        sock = next((i for i in n.inputs if not i.is_linked), None) if n else None
        if sock is None:
            continue
        key = "_icon_eye0_" + mn
        if key not in globals():
            globals()[key] = sock.default_value
        sock.default_value = globals()[key] * k
        print("eye", mn, n.operation, "const", round(globals()[key], 3), "->", round(sock.default_value, 3))
