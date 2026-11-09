

tex_nodes = {n.name: n for n in src_mat.node_tree.nodes if n.type == "TEX_IMAGE"}
alb_img = tex_nodes["Image Texture"].image
met_img = tex_nodes["Image Texture.001"].image
nrm_img = tex_nodes["Image Texture.002"].image
W_, H_ = alb_img.size
mask = raster_mask(fill_uv_tris, W_, H_, grow=5)
new_alb = repaint(alb_img, mask, "Menace_Pumpkin_Albedo", "albedo")
new_met = repaint(met_img, mask, "Menace_Pumpkin_MetallicSmoothness", "met")
new_nrm = repaint(nrm_img, mask, "Menace_Pumpkin_Normal", "normal")


def principled(nt):
    return next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")


skin = src_mat.copy()
skin.name = "MP_Pumpkin_Skin"
for n in skin.node_tree.nodes:
    if n.type == "TEX_IMAGE":
        n.image = {"Image Texture": new_alb, "Image Texture.001": new_met, "Image Texture.002": new_nrm}[n.name]
pb = principled(skin.node_tree)
pb.inputs["Roughness"].default_value = 0.55
pb.inputs["Subsurface Weight"].default_value = 0.12           # a little inner warmth in the skin
pb.inputs["Subsurface Radius"].default_value = (1.0, 0.35, 0.12)
pb.inputs["Subsurface Scale"].default_value = 0.012


def make_mat(name, base, rough, emit_lo, emit_hi, glow, t_lo=1.0, t_hi=1.0, sss=0.0, streaks=False):
    """Principled material; emission colour/strength ramp along the point attribute carve_t (skin edge 0 .. deep 1):
    strength = Glow * lerp(t_lo, t_hi, carve_t) * fibre noise.  A Value node named "Glow" scales it."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    L = nt.links.new
    p = principled(nt)
    p.inputs["Roughness"].default_value = rough
    if sss:
        p.inputs["Subsurface Weight"].default_value = sss
        p.inputs["Subsurface Radius"].default_value = (1.0, 0.4, 0.15)
        p.inputs["Subsurface Scale"].default_value = 0.01
    g = nt.nodes.new("ShaderNodeValue")
    g.name = g.label = "Glow"
    g.outputs[0].default_value = glow
    at = nt.nodes.new("ShaderNodeAttribute")
    at.attribute_type = "GEOMETRY"
    at.attribute_name = "carve_t"
    tc = nt.nodes.new("ShaderNodeTexCoord")
    mp = nt.nodes.new("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = (1.0, 1.0, 1.0) if not streaks else (1.0, 1.0, 6.0)
    L(tc.outputs["Object"], mp.inputs["Vector"])
    nz = nt.nodes.new("ShaderNodeTexNoise")
    nz.inputs["Scale"].default_value = 70.0
    nz.inputs["Detail"].default_value = 6.0
    L(mp.outputs["Vector"], nz.inputs["Vector"])
    # base colour: base darkened/lightened by the noise
    bmix = nt.nodes.new("ShaderNodeMix")
    bmix.data_type = "RGBA"
    bmix.inputs[6].default_value = (*[c * 0.55 for c in base], 1)
    bmix.inputs[7].default_value = (*[min(1.0, c * 1.35) for c in base], 1)
    L(nz.outputs["Fac"], bmix.inputs[0])
    L(bmix.outputs[2], p.inputs["Base Color"])
    ramp = nt.nodes.new("ShaderNodeMix")
    ramp.data_type = "RGBA"
    ramp.inputs[6].default_value = (*emit_lo, 1)
    ramp.inputs[7].default_value = (*emit_hi, 1)
    L(at.outputs["Fac"], ramp.inputs[0])
    mr = nt.nodes.new("ShaderNodeMapRange")
    mr.inputs["To Min"].default_value = t_lo
    mr.inputs["To Max"].default_value = t_hi
    L(at.outputs["Fac"], mr.inputs["Value"])
    nm = nt.nodes.new("ShaderNodeMapRange")
    nm.inputs["To Min"].default_value = 0.8
    nm.inputs["To Max"].default_value = 1.15
    L(nz.outputs["Fac"], nm.inputs["Value"])
    m1 = nt.nodes.new("ShaderNodeMath")
    m1.operation = "MULTIPLY"
    L(g.outputs[0], m1.inputs[0])
    L(nm.outputs["Result"], m1.inputs[1])
    m2 = nt.nodes.new("ShaderNodeMath")
    m2.operation = "MULTIPLY"
    L(m1.outputs[0], m2.inputs[0])
    L(mr.outputs["Result"], m2.inputs[1])
    L(ramp.outputs[2], p.inputs["Emission Color"])
    L(m2.outputs[0], p.inputs["Emission Strength"])
    return m


cutters = []
if STYLE == "lantern":
    cut_mat = make_mat("MP_Pumpkin_Cut_Flesh", (0.9, 0.55, 0.2), 0.6, (1.0, 0.62, 0.16), (1.0, 0.40, 0.04),
                       GLOW_CUT, 0.15, 1.0, sss=0.25)
    inner_mat = make_mat("MP_Pumpkin_Inner", (0.8, 0.4, 0.1), 0.7, (1.0, 0.36, 0.03), (1.0, 0.36, 0.03),
                         GLOW_INNER, sss=0.2)
    # the scooped inside: an icosphere pushed WALL inside the skin, smoothed
    cbm = bmesh.new()
    bmesh.ops.create_icosphere(cbm, subdivisions=4, radius=1.0)
    cbm.verts.ensure_lookup_table()
    dirs = [v.co.normalized() for v in cbm.verts]
    Rin = np.array([((outer_bvh.ray_cast(C, d)[0] or (C + d * 0.18)) - C).length for d in dirs])
    cnb = [[e.other_vert(v).index for e in v.link_edges] for v in cbm.verts]
    Rs = Rin - WALL
    for _ in range(6):
        Rs = np.array([0.5 * Rs[i] + 0.5 * Rs[cnb[i]].mean() for i in range(len(Rs))])
    Rs = np.minimum(Rs, Rin - WALL * 0.85)
    for v, d, r in zip(cbm.verts, dirs, Rs):
        v.co = C + d * r
    cav_me = bpy.data.meshes.new("MP_Cut_Cavity")
    cbm.to_mesh(cav_me)
    cav_me.uv_layers.new(name="UVMap")
    cav_me.materials.append(inner_mat)
    cav_ob = bpy.data.objects.new("MP_Cut_Cavity", cav_me)
    cut_coll.objects.link(cav_ob)
    cav_bvh = BVHTree.FromBMesh(cbm)
    cutters.append(cav_ob)
    D = WALL + 0.035
    cutters += [cutter("MP_Cut_Eye_R", EYE_R, D, cut_mat, cut_mat),
                cutter("MP_Cut_Eye_L", [(-x, z) for (x, z) in reversed(EYE_R)], D, cut_mat, cut_mat),
                cutter("MP_Cut_Nose", NOSE, D, cut_mat, cut_mat),
                cutter("MP_Cut_Mouth", mouth_outline(), D, cut_mat, cut_mat)]
    light_at = C.copy()
else:   # "game": dark sockets like the game pumpkin, glowing yellow pupils, a glowing mouth back
    sock_mat = make_mat("MP_Pumpkin_Socket", (0.075, 0.045, 0.03), 0.75, (1.0, 0.5, 0.12), (1.0, 0.45, 0.08),
                        GLOW_SOCKET, 0.0, 1.0, streaks=True)
    pupil_mat = make_mat("MP_Pumpkin_Pupil", (0.9, 0.75, 0.2), 0.5, (1.0, 0.86, 0.22), (1.0, 0.7, 0.12),
                         GLOW_PUPIL, 0.8, 1.0)
    mouth_mat = make_mat("MP_Pumpkin_Mouth_Glow", (0.9, 0.55, 0.15), 0.6, (1.0, 0.70, 0.18), (1.0, 0.55, 0.10),
                         GLOW_MOUTH, 0.8, 1.0)
    pr = PUPIL_R
    cutters += [cutter("MP_Cut_Eye_R", EYE_R, EYE_DEPTH, sock_mat, sock_mat),
                cutter("MP_Cut_Eye_L", [(-x, z) for (x, z) in reversed(EYE_R)], EYE_DEPTH, sock_mat, sock_mat),
                cutter("MP_Cut_Pupil_R", almond(*pr), PUPIL_DEPTH, pupil_mat, pupil_mat),
                cutter("MP_Cut_Pupil_L", [(-x, z) for (x, z) in reversed(almond(*pr))], PUPIL_DEPTH, pupil_mat, pupil_mat),
                cutter("MP_Cut_Nose", NOSE, NOSE_DEPTH, sock_mat, sock_mat),
                cutter("MP_Cut_Mouth", mouth_outline(), MOUTH_DEPTH, sock_mat, mouth_mat)]
    S, d = project(Vector((0.0, 0.5 * (UPPER_MID_Z + LOWER_MID_Z) + 0.01)))
    light_at = S - d * (MOUTH_DEPTH * 0.45)

# ------------------------------------------------------------------ 5. the boolean
filled_me.materials.clear()
filled_me.materials.append(skin)
work = bpy.data.objects.new("MP_Work", filled_me)
bpy.context.scene.collection.objects.link(work)
mod = work.modifiers.new("Carve", "BOOLEAN")
mod.operation = "DIFFERENCE"
mod.operand_type = "COLLECTION"
mod.collection = cut_coll
mod.solver = "EXACT"
mod.material_mode = "TRANSFER"
mod.use_hole_tolerant = True
bpy.context.view_layer.update()
dg = bpy.context.evaluated_depsgraph_get()
me = bpy.data.meshes.new_from_object(work.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
me.name = "Menace_Pumpkin_Mesh"
log("result", len(me.vertices), "verts", len(me.polygons), "faces", "mats", [m.name for m in me.materials])
rbm = bmesh.new()
rbm.from_mesh(me)
log("non-manifold edges after carve", len([e for e in rbm.edges if not e.is_manifold]))
rbm.free()

# carve_t: 0 at the skin .. 1 deep (lantern: at the inner wall; game: at T_REF below the skin)
vals = []
for v in me.vertices:
    do = outer_bvh.find_nearest(v.co)[3]
    if STYLE == "lantern":
        di = cav_bvh.find_nearest(v.co)[3]
        vals.append(do / max(do + di, 1e-6))
    else:
        vals.append(min(1.0, do / T_REF))
a = me.attributes.new("carve_t", "FLOAT", "POINT")
a.data.foreach_set("value", vals)
me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
me.set_sharp_from_angle(angle=math.radians(40.0))

# ------------------------------------------------------------------ 6. objects + collections
scn = bpy.context.scene
for c in list(scn.collection.children):
    scn.collection.children.unlink(c)
for o in list(scn.collection.objects):
    scn.collection.objects.unlink(o)


def new_coll(name):
    c = bpy.data.collections.new(name)
    scn.collection.children.link(c)
    return c


def build_set(prefix, coll, light_w, light_r, at):
    root = bpy.data.objects.new(prefix + "_Root", None)
    root.empty_display_size = 0.15
    root.empty_display_type = "PLAIN_AXES"
    coll.objects.link(root)
    root.location = at
    body = bpy.data.objects.new(prefix + "_Body", me)
    coll.objects.link(body)
    body.parent = root
    body["source"] = "pumpkin_0 (Assets/Blender/Knight_429.blend), face recarved, style " + STYLE
    L = bpy.data.lights.new(prefix + "_Inner_Light", "POINT")
    L.energy = light_w
    L.color = (1.0, 0.5, 0.12)
    L.shadow_soft_size = light_r
    lo = bpy.data.objects.new(prefix + "_Inner_Light", L)
    coll.objects.link(lo)
    lo.parent = root
    lo.location = light_at
    lo.visible_camera = False
    return root, body, lo


C_HERO = new_coll("Menace_Pumpkin")
C_SMALL = new_coll("Menace_Pumpkin_Small")
build_set("Menace_Pumpkin", C_HERO, LIGHT_W, 0.02, (0, 0, 0))
build_set("Menace_Pumpkin_Small", C_SMALL, SMALL_LIGHT_W, 0.008, (0.9, 0, 0))
scn["menace_style"] = STYLE

# test rig (excluded from the view layer; menace_pumpkin_test_render.py switches it on)
C_RIG = new_coll("Menace_Pumpkin_Test_Rig")
fl = bpy.data.meshes.new("MP_Test_Floor")
fl.from_pydata([(-4, -4, 0), (4, -4, 0), (4, 4, 0), (-4, 4, 0)], [], [[0, 1, 2, 3]])
flo = bpy.data.objects.new("MP_Test_Floor", fl)
fm = bpy.data.materials.new("MP_Test_Floor")
fm.use_nodes = True
principled(fm.node_tree).inputs["Base Color"].default_value = (0.05, 0.05, 0.06, 1)
principled(fm.node_tree).inputs["Roughness"].default_value = 0.8
fl.materials.append(fm)
C_RIG.objects.link(flo)
orig = bpy.data.objects.new("MP_Test_Original_pumpkin_0", src_ob.data)   # the untouched game pumpkin, for comparison
orig.location = (-0.9, 0, 0)
C_RIG.objects.link(orig)
for lc in scn.view_layers[0].layer_collection.children:
    if lc.name == "Menace_Pumpkin_Test_Rig":
        lc.exclude = True

# ------------------------------------------------------------------ 7. clean + save
for o in [work] + cutters:
    bpy.data.objects.remove(o)
bpy.data.collections.remove(cut_coll)
bpy.data.objects.remove(src_ob)
bpy.data.meshes.remove(filled_me)
bpy.data.orphans_purge(do_recursive=True)
scn.render.engine = "CYCLES"
bpy.ops.wm.save_as_mainfile(filepath=OUT, compress=True)
log("SAVED", OUT, "style", STYLE)
