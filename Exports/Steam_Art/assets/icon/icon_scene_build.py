"""The 3D version of the user's badge icon (user 2026-11-09: "re-create the icon scene in blender and render a new
icon with our current style and setup"): the knight (improved texture, the user's pose for the head and body) peeks
over the gold-and-green 3D "429", his fists resting on the digits, in front of a gold ring holding a dark green disc
with green fire wisps and sparkles. Our lighting style: warm key, cool moon rim, green under-glow, bloom.

    blender -b Exports/Steam_Art/Steam_Art.blend --python Exports/Steam_Art/assets/icon/icon_scene_build.py
        -> saves a COPY: Exports/Steam_Art/assets/icon/Icon_Scene.blend (Steam_Art.blend is not changed)
    then: blender -b Exports/Steam_Art/assets/icon/Icon_Scene.blend --python Exports/Steam_Art/assets/icon/icon_render.py

Everything is placed in the knight's own frame (F = where he faces, U = up, R = his right), so nothing in the Steam
scene has to move. All numbers are at the top.
"""
import bpy, bmesh, math, os
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "Icon_Scene.blend")

# ---- layout, laid out like the user's painted badge (2026-11-09 feedback: "the 429 in the old icon is so easy to read
# and fits well in the circle, the knight interacts with it, both hands on it; raise the 429, fit it inside the circle,
# make the ring smaller and less glossy, remove the smile eyes"; then "make the knight hug 429 tight together": the
# digits snug under his visor, his arms spread over their tops, fists on the 4 and the 9). Badge units: R = the ring
# radius, C = its centre.
BADGE_R = 0.34                       # ring radius (m)
EARS_AT = 0.95                       # the helmet's top (ear tips) sits at C + EARS_AT * R, just touching the ring
KNIGHT_X = 0.02                      # the knight sits this far right of the badge centre (like the badge)
RING_W = 0.011                       # ring thickness: a slim gold rim
RING_ROUGH = 0.58                    # ring gold roughness (less glossy than the logo's polished gold)
DISC_BACK = -0.24                    # the disc's distance along F (behind the helmet)
LOGO_W_R = 1.60                       # the 429's width, in R (its 4 and 9 reach the ring, like the badge)
LOGO_TOP_R = 0.27                    # the digits' top at C + this * R (just under his visor: eyes and visor show)
LOGO_DEPTH = 0.7                     # the digits' thickness (x the logo's own): less side wall, reads flatter
LOGO_BACK = 0.125                    # the back of the digits along F (just in front of his belly)
LOGO_FIRE = False                    # the logo's own green flame card (it would cover his chin)
SIDE_GOLD = ((0.86, 0.47, 0.07), (1.0, 0.64, 0.16))   # the bevels' gold (was copper-brown 0.55..0.72)
CRACK_GLOW = 0.25                    # the digits' glowing crack network x this (neon and busy at icon size)
GROOVE_GLOW = 0.3                    # the lime line inside each gold outline x this
GLOW_LIME = (0.45, 1.0, 0.08)        # glow colour of both: lime (the badge's highlights lean yellow-green)
FACE_R = 0.085                       # his face = his body within this distance of its middle (the fists are ~0.2 m away)
FACE_MATTE = 0.85                    # the face's shine x (1 - this)
ARM_COLOUR = (0.011, 0.004, 0.002)  # linear: his arms, a dark matte shadow-brown (only the paws read)
FACE_COLOUR = (0.030, 0.0047, 0.0025)  # linear: the badge's smooth dark maroon (#44170C)
FACE_TINT = (0.2, 0.11, 0.08)       # the face x this on top (dark chocolate-maroon, the eyes pop)
BROWN_TINT = (0.24, 0.092, 0.05)      # his face and gloves x this: deep chestnut red-brown (the badge's)
HELMET_ROUGH_ADD = 0.3
HELMET_MASK_LO = -0.03               # (G-R) where the helmet tint starts; gold trims are far below
HELMET_MASK_W = 0.02                # (G-R) over which the helmet tint fades in (was 0.05: only ~40% of the helmet took it)
CLOVER_MASK = os.path.normpath(os.path.join(HERE, "..", "knight", "Knight_Armour_Set_CloverMask.png"))   # the 70 % clovers (clover_mask.png = old 100 %)
HELMET_SPEC_TINT = (0.35, 0.8, 0.08)  # the helmet green's sheen colour
HELMET_SPEC = 0.55                   # and its strength x this
HELMET_TINT = (0.50, 0.32, 0.045)        # the helmet green warmer (leaf / olive, like the badge) and lighter
VIEW = "Standard"                    # the judges' pick (c6): Standard view, no Steam warm grade, then grade_icon.py
ENAMEL_DIM = 0.42                     # the digits' green enamel darker x this
FIST_X_R = (-0.60, 0.56)             # where the fists sit, in R from the badge centre (his right on the 4, his left on the 2)
FIST_RIGHT_RANGE = (-0.66, -0.26)    # his right fist: the top of the 4's stem within this range (R units)
HAND_SCALE = 1.85                     # cartoon fists, like the big paws in the user's badge
FIST_LIFT = 0.016                    # fist centre this far above the digit's top (resting on it)
PLATE_BELOW = 0.045                  # the front piece of the disc starts this far under the digits' top
FIST_IN = 0.022                       # fist centre this far behind the digits' front face (on the top edge)
CAM_LENS = 85.0
CAM_MARGIN = 1.16                    # frame half-size = R x this (+ the digits poking out)
# ---- look
BLOOM_STRENGTH, BLOOM_SIZE = 0.22, 0.5
ICON_SATURATION = 1.0               # an icon is small: a bit more colour than the Steam scene's 1.12
CARD_W = 0.0                        # warm cards the gold reflects (only seen in reflections)
FIST_W = 5.5                         # a small warm light on his fists (they sit under the helmet's rim)
LOOK = "AgX - Punchy"                # a little more colour and contrast than the Steam scene's plain AgX
FIRE_W = 2.5                         # green fire wisps in the disc
SPARK_W = 9.0
KEY_W, RIM_W, GREEN_W, FILL_W = 30.0, 18.0, 0.6, 10.0
RIM_COLOUR = (0.95, 1.0, 0.8)        # warm greenish white (was a cool moon blue)
RING_LIGHT_W = 10.0                  # a warm light on the gold ring only (light linking): bright gold, no haze elsewhere
EXPOSURE = -0.4                     # the icon is a small bright object: a touch darker than the Steam scene


def log(*a):
    print("[icon]", *a)


bpy.ops.wm.save_as_mainfile(filepath=OUT)            # a copy: from here on only Icon_Scene.blend is written
sc = bpy.context.scene
arm = bpy.data.objects["Pie_Knight_Armature"]
logo_src = bpy.data.objects["LOGO_Main_Capsule_1232x706"]
keep = {arm, logo_src} | set(arm.children)
for o in list(bpy.data.objects):
    if o not in keep:
        bpy.data.objects.remove(o, do_unlink=True)
for n in ("Pie_Knight_Sword", "Pie_Knight_Shield", "Pie_Knight_Bracer"):    # bracers: they stretch with the arms
    o = bpy.data.objects.get(n)
    if o:
        o.hide_render = True                         # the badge: fists on the digits, no sword or shield
col = bpy.data.collections.new("IC_Icon")
sc.collection.children.link(col)

M = arm.matrix_world.to_3x3()
F = (M @ Vector((0, 0, 1))).normalized()
U = Vector((0, 0, 1))
R = F.cross(U).normalized()                           # his right (camera-left)
A = arm.matrix_world.translation.copy()
A.z = 0.0


def K(r, f, z):                                       # knight frame -> world
    return A + R * r + F * f + U * z


def KC(x, f, z):                                      # camera-x (right on screen), along F, up -> world
    return A - R * x + F * f + U * z


dg0 = bpy.context.evaluated_depsgraph_get()
_h = bpy.data.objects["Pie_Knight_Helmet"].evaluated_get(dg0)
_hm = _h.to_mesh()
helmet_top = max((_h.matrix_world @ v.co).z for v in _hm.vertices)
_h.to_mesh_clear()
BADGE_Z = helmet_top - EARS_AT * BADGE_R
CX = -KNIGHT_X                                        # badge centre, camera-x
C = KC(CX, 0, BADGE_Z)
LOGO_W = LOGO_W_R * BADGE_R
LOGO_TOP = BADGE_Z + LOGO_TOP_R * BADGE_R
# ---------------------------------------------------------------- the logo
logo = logo_src
logo.parent = None
logo.matrix_parent_inverse = Matrix.Identity(4)
s = LOGO_W / 2.0                                      # the lettering is 2.0 wide at scale 1
rot = Matrix((-R, -F, U)).transposed()                # local X -> camera right (-R), local -Y -> F (to camera), Z up
logo.matrix_world = rot.to_4x4() @ Matrix.Diagonal((s, s * LOGO_DEPTH, s, 1.0))
bpy.context.view_layer.update()
mats = [sl.material.name if sl.material else "" for sl in logo.material_slots]
fire_i = mats.index("Logo_Fire_v3") if "Logo_Fire_v3" in mats else -1


def logo_pts():
    return [logo.matrix_world @ logo.data.vertices[i].co for p in logo.data.polygons if p.material_index != fire_i
            for i in p.vertices]


pts = logo_pts()
zt = max(p.z for p in pts)
fb = min((p - A).dot(F) for p in pts)
cxl = (max((p - A).dot(-R) for p in pts) + min((p - A).dot(-R) for p in pts)) / 2
logo.location += U * (LOGO_TOP - zt) + F * (LOGO_BACK - fb) - R * (CX - cxl)
if not LOGO_FIRE and fire_i >= 0:
    clear = bpy.data.materials.new("IC_Clear")
    clear.node_tree.nodes.clear()
    o_ = clear.node_tree.nodes.new("ShaderNodeOutputMaterial")
    t_ = clear.node_tree.nodes.new("ShaderNodeBsdfTransparent")
    clear.node_tree.links.new(t_.outputs[0], o_.inputs["Surface"])
    logo.material_slots[fire_i].link = "OBJECT"
    logo.material_slots[fire_i].material = clear
logo.name = "IC_Logo"
bpy.context.view_layer.update()
pts = logo_pts()
logo_front = max((p - A).dot(F) for p in pts)
log("logo: width %.3f, top %.3f, bottom %.3f, front %.3f (badge centre z %.3f, R %.3f)" % (
    LOGO_W, max(p.z for p in pts), min(p.z for p in pts), logo_front, BADGE_Z, BADGE_R))


def top_at(x0, x1):
    """The highest point of the digits between camera-x x0 and x1: (x, z)."""
    best = None
    for p in pts:
        x = (p - A).dot(-R)
        if x0 <= x <= x1 and (best is None or p.z > best[1]):
            best = (x, p.z)
    return best


# the digits' green enamel: matte and darker in the icon (the shine blotched it at icon size; the gold outline reads)
for sl in logo.material_slots:
    if sl.material and sl.material.name == "Logo_Green_v2":
        gm = sl.material.copy()
        gm.name = "IC_Logo_Green"
        b_ = next(n for n in gm.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        for k_, v_ in (("Coat Weight", 0.0), ("Roughness", 0.55), ("Specular IOR Level", 0.25)):
            if k_ in b_.inputs and not b_.inputs[k_].is_linked:
                b_.inputs[k_].default_value = v_
        if b_.inputs["Base Color"].is_linked:
            src_ = b_.inputs["Base Color"].links[0].from_socket
            mul = gm.node_tree.nodes.new("ShaderNodeMix"); mul.data_type = "RGBA"; mul.blend_type = "MULTIPLY"
            mul.inputs[0].default_value = 1.0; mul.inputs[7].default_value = (ENAMEL_DIM,) * 3 + (1,)
            gm.node_tree.links.new(src_, mul.inputs[6]); gm.node_tree.links.new(mul.outputs[2], b_.inputs["Base Color"])
        else:
            c_ = b_.inputs["Base Color"].default_value
            b_.inputs["Base Color"].default_value = (c_[0] * ENAMEL_DIM, c_[1] * ENAMEL_DIM, c_[2] * ENAMEL_DIM, 1)
        sl.link = "OBJECT"
        sl.material = gm

# colour fixes from the judged comparison with the badge (2026-11-09): icon-only copies of the materials
def _scaled_input(mat, node, sock, k):
    """Multiplies whatever feeds node.inputs[sock] by k (or its value, if nothing is linked)."""
    nt = mat.node_tree
    i_ = node.inputs[sock]
    if i_.is_linked:
        src_ = i_.links[0].from_socket
        m_ = nt.nodes.new("ShaderNodeMath"); m_.operation = "MULTIPLY"; m_.inputs[1].default_value = k
        nt.links.new(src_, m_.inputs[0]); nt.links.new(m_.outputs[0], i_)
    else:
        i_.default_value = i_.default_value * k


for sl in logo.material_slots:
    m0 = sl.material
    if m0 is None or m0.name not in ("IC_Logo_Green", "Logo_Glow_v2"):
        continue
    m1 = m0.copy() if m0.name == "Logo_Glow_v2" else m0
    if m0.name == "Logo_Glow_v2":
        m1.name = "IC_Logo_Glow"
    b_ = next(n for n in m1.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    _scaled_input(m1, b_, "Emission Strength", CRACK_GLOW if m0.name == "IC_Logo_Green" else GROOVE_GLOW)
    b_.inputs["Emission Color"].default_value = GLOW_LIME + (1,)
    sl.link = "OBJECT"
    sl.material = m1


for sl in logo.material_slots:
    if sl.material and sl.material.name == "Logo_Gold_Side_v3":
        gs = sl.material.copy()
        gs.name = "IC_Logo_Gold_Side"
        for n in gs.node_tree.nodes:
            if n.type == "VALTORGB" and len(n.color_ramp.elements) == 2 and n.color_ramp.elements[0].color[0] > 0.5:
                n.color_ramp.elements[0].color = SIDE_GOLD[0] + (1,)
                n.color_ramp.elements[1].color = SIDE_GOLD[1] + (1,)
        sl.link = "OBJECT"
        sl.material = gs


def knight_copy(obj_name, fn):
    o = bpy.data.objects.get(obj_name)
    if o is None:
        return
    for sl in o.material_slots:
        if sl.material and sl.material.name == "Knight_Armour_Sword_Shield":
            m1 = sl.material.copy()
            m1.name = "IC_Knight_" + obj_name
            fn(m1)
            sl.link = "OBJECT"
            sl.material = m1


def _base_socket(m1):
    b_ = next(n for n in m1.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    return b_, b_.inputs["Base Color"].links[0].from_socket


def _masked_multiply(m1, src_, b_, chan_a, chan_b, width, factor_rgb):
    """Base colour x factor_rgb where channel a exceeds channel b (soft over `width`); feeds Base + Emission Color."""
    nt = m1.node_tree
    sep = nt.nodes.new("ShaderNodeSeparateColor"); nt.links.new(src_, sep.inputs[0])
    d_ = nt.nodes.new("ShaderNodeMath"); d_.operation = "SUBTRACT"
    nt.links.new(sep.outputs[chan_a], d_.inputs[0]); nt.links.new(sep.outputs[chan_b], d_.inputs[1])
    mr = nt.nodes.new("ShaderNodeMapRange"); mr.inputs["From Min"].default_value = 0.0; mr.inputs["From Max"].default_value = width
    nt.links.new(d_.outputs[0], mr.inputs["Value"])
    mx = nt.nodes.new("ShaderNodeMix"); mx.data_type = "RGBA"; mx.blend_type = "MULTIPLY"
    nt.links.new(mr.outputs["Result"], mx.inputs[0]); nt.links.new(src_, mx.inputs[6])
    mx.inputs[7].default_value = tuple(factor_rgb) + (1,)
    nt.links.new(mx.outputs[2], b_.inputs["Base Color"])
    if b_.inputs["Emission Color"].is_linked:
        nt.links.new(mx.outputs[2], b_.inputs["Emission Color"])
    return mx


def darker_browns(m1):
    """His face (inside the visor) and gloves: the badge keeps them a deep warm brown, the eyes glow out of the dark.
    The face (everything above his fists) is darkened further: the fists' lights light it too, and the badge's face
    is near-black chocolate while its paws are a lit chestnut."""
    nt = m1.node_tree
    b_, src_ = _base_socket(m1)
    mx = _masked_multiply(m1, src_, b_, "Red", "Green", 0.04, BROWN_TINT)
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    dist = nt.nodes.new("ShaderNodeVectorMath"); dist.operation = "DISTANCE"
    dist.inputs[1].default_value = tuple(FACE_C)
    nt.links.new(geo.outputs["Position"], dist.inputs[0])
    up = nt.nodes.new("ShaderNodeMapRange"); up.inputs["From Min"].default_value = FACE_R
    up.inputs["From Max"].default_value = FACE_R - 0.015          # 1 inside the face, 0 out at the fists
    nt.links.new(dist.outputs["Value"], up.inputs["Value"])
    sep = nt.nodes.new("ShaderNodeSeparateColor"); nt.links.new(src_, sep.inputs[0])
    d_ = nt.nodes.new("ShaderNodeMath"); d_.operation = "SUBTRACT"
    nt.links.new(sep.outputs["Red"], d_.inputs[0]); nt.links.new(sep.outputs["Green"], d_.inputs[1])
    br = nt.nodes.new("ShaderNodeMapRange"); br.inputs["From Min"].default_value = -0.06; br.inputs["From Max"].default_value = -0.02
    # (every near-neutral face texel counts fully, so the texture's grain can't show through; only the lime eyes,
    #  with G far above R, stay out)
    # (his face texture is a near-neutral dark brown, R just above G; the lime eyes have G above R and stay)
    nt.links.new(d_.outputs[0], br.inputs["Value"])
    k_ = nt.nodes.new("ShaderNodeMath"); k_.operation = "MULTIPLY"
    nt.links.new(up.outputs["Result"], k_.inputs[0]); nt.links.new(br.outputs["Result"], k_.inputs[1])
    mf = nt.nodes.new("ShaderNodeMix"); mf.data_type = "RGBA"; mf.blend_type = "MIX"
    nt.links.new(k_.outputs[0], mf.inputs[0]); nt.links.new(mx.outputs[2], mf.inputs[6])
    mf.inputs[7].default_value = FACE_COLOUR + (1,)
    # his arms (shoulders to wrists, the "ic_arm" attribute): one smooth dark matte shadow, so only the paws show
    # (user: "be careful with the shoulder and elbow, they are really distracting"; the badge shows no arms at all)
    at = nt.nodes.new("ShaderNodeAttribute"); at.attribute_type = "GEOMETRY"; at.attribute_name = "ic_arm"
    ka = nt.nodes.new("ShaderNodeMapRange"); ka.inputs["From Min"].default_value = 0.3; ka.inputs["From Max"].default_value = 0.7
    nt.links.new(at.outputs["Fac"], ka.inputs["Value"])
    ma = nt.nodes.new("ShaderNodeMix"); ma.data_type = "RGBA"; ma.blend_type = "MIX"
    nt.links.new(ka.outputs["Result"], ma.inputs[0]); nt.links.new(mf.outputs[2], ma.inputs[6])
    ma.inputs[7].default_value = ARM_COLOUR + (1,)
    nt.links.new(ma.outputs[2], b_.inputs["Base Color"])
    kmax = nt.nodes.new("ShaderNodeMath"); kmax.operation = "MAXIMUM"     # face OR arm: smooth and matte
    nt.links.new(k_.outputs[0], kmax.inputs[0]); nt.links.new(ka.outputs["Result"], kmax.inputs[1])
    # ...and its bumps: the armour normal map's grain read as dirt on the flat dark face (the badge's face is smooth)
    nm_ = next((n for n in nt.nodes if n.type == "NORMAL_MAP"), None)
    if nm_ is not None and not nm_.inputs["Strength"].is_linked:
        fl = nt.nodes.new("ShaderNodeMath"); fl.operation = "MULTIPLY_ADD"
        nt.links.new(kmax.outputs[0], fl.inputs[0]); fl.inputs[1].default_value = -nm_.inputs["Strength"].default_value
        fl.inputs[2].default_value = nm_.inputs["Strength"].default_value
        nt.links.new(fl.outputs[0], nm_.inputs["Strength"])
    # the face also loses its shine (it mirrored the green field and the gold as a khaki sheen)
    inv = nt.nodes.new("ShaderNodeMath"); inv.operation = "MULTIPLY_ADD"
    nt.links.new(kmax.outputs[0], inv.inputs[0]); inv.inputs[1].default_value = -FACE_MATTE; inv.inputs[2].default_value = 1.0
    for sock in ("Specular IOR Level", "Coat Weight"):
        if sock in b_.inputs:
            if b_.inputs[sock].is_linked:
                src2 = b_.inputs[sock].links[0].from_socket
                mm = nt.nodes.new("ShaderNodeMath"); mm.operation = "MULTIPLY"
                nt.links.new(src2, mm.inputs[0]); nt.links.new(inv.outputs[0], mm.inputs[1]); nt.links.new(mm.outputs[0], b_.inputs[sock])
            else:
                mm = nt.nodes.new("ShaderNodeMath"); mm.operation = "MULTIPLY"; mm.inputs[0].default_value = b_.inputs[sock].default_value
                nt.links.new(inv.outputs[0], mm.inputs[1]); nt.links.new(mm.outputs[0], b_.inputs[sock])
    if b_.inputs["Roughness"].is_linked:
        r2 = b_.inputs["Roughness"].links[0].from_socket
        ra = nt.nodes.new("ShaderNodeMath"); ra.operation = "MULTIPLY_ADD"; ra.use_clamp = True
        nt.links.new(kmax.outputs[0], ra.inputs[0]); ra.inputs[1].default_value = 0.5; nt.links.new(r2, ra.inputs[2])
        nt.links.new(ra.outputs[0], b_.inputs["Roughness"])
    if b_.inputs["Emission Color"].is_linked:
        nt.links.new(ma.outputs[2], b_.inputs["Emission Color"])


def satin_helmet(m1):
    """No white streak across the clover: rougher, and its green a little lighter (the badge's lime-olive helmet)."""
    nt = m1.node_tree
    b_, src_ = _base_socket(m1)
    if b_.inputs["Roughness"].is_linked:
        r_ = b_.inputs["Roughness"].links[0].from_socket
        ad = nt.nodes.new("ShaderNodeMath"); ad.operation = "ADD"; ad.use_clamp = True
        ad.inputs[1].default_value = HELMET_ROUGH_ADD
        nt.links.new(r_, ad.inputs[0]); nt.links.new(ad.outputs[0], b_.inputs["Roughness"])
    mx = _masked_multiply(m1, src_, b_, "Green", "Red", HELMET_MASK_W, HELMET_TINT)
    # the clovers keep their own bright green (they are painted into the same texture)
    tex = next((n for n in nt.nodes if n.type == "TEX_IMAGE" and n.image and "Diffuse" in n.image.name), None)
    if tex is not None and os.path.exists(CLOVER_MASK):
        cm = nt.nodes.new("ShaderNodeTexImage")
        cm.image = bpy.data.images.load(CLOVER_MASK, check_existing=True)
        cm.image.colorspace_settings.name = "Non-Color"
        if tex.inputs["Vector"].is_linked:
            nt.links.new(tex.inputs["Vector"].links[0].from_socket, cm.inputs["Vector"])
        fac = mx.inputs[0].links[0].from_socket
        # every non-gold texel of the helmet takes the tint (its painted clover shadow is near-neutral: G-R ~ 0)
        fac.node.inputs["From Min"].default_value = HELMET_MASK_LO
        fac.node.inputs["From Max"].default_value = HELMET_MASK_LO + HELMET_MASK_W
        inv = nt.nodes.new("ShaderNodeMath"); inv.operation = "MULTIPLY_ADD"; inv.use_clamp = True
        crisp = nt.nodes.new("ShaderNodeMapRange")              # only the clover itself (the mask's soft skirt is wide)
        crisp.inputs["From Min"].default_value = 0.35; crisp.inputs["From Max"].default_value = 0.65
        nt.links.new(cm.outputs["Color"], crisp.inputs["Value"])
        nt.links.new(crisp.outputs["Result"], inv.inputs[0]); inv.inputs[1].default_value = -1.0; inv.inputs[2].default_value = 1.0
        mul = nt.nodes.new("ShaderNodeMath"); mul.operation = "MULTIPLY"
        nt.links.new(fac, mul.inputs[0]); nt.links.new(inv.outputs[0], mul.inputs[1])
        nt.links.new(mul.outputs[0], mx.inputs[0])
        # its shine: green and lower on the green parts (a white sheen read as a grey haze on the darker olive);
        # the gold trims keep a white specular
        if "Specular Tint" in b_.inputs and not b_.inputs["Specular Tint"].is_linked:
            st = nt.nodes.new("ShaderNodeMix"); st.data_type = "RGBA"; st.blend_type = "MIX"
            nt.links.new(mul.outputs[0], st.inputs[0])
            st.inputs[6].default_value = (1, 1, 1, 1); st.inputs[7].default_value = HELMET_SPEC_TINT + (1,)
            nt.links.new(st.outputs[2], b_.inputs["Specular Tint"])
        if b_.inputs["Specular IOR Level"].is_linked:
            sp_ = b_.inputs["Specular IOR Level"].links[0].from_socket
            k2 = nt.nodes.new("ShaderNodeMath"); k2.operation = "MULTIPLY_ADD"     # 1 - mask * (1 - HELMET_SPEC)
            nt.links.new(mul.outputs[0], k2.inputs[0]); k2.inputs[1].default_value = -(1 - HELMET_SPEC); k2.inputs[2].default_value = 1.0
            m2 = nt.nodes.new("ShaderNodeMath"); m2.operation = "MULTIPLY"
            nt.links.new(sp_, m2.inputs[0]); nt.links.new(k2.outputs[0], m2.inputs[1])
            nt.links.new(m2.outputs[0], b_.inputs["Specular IOR Level"])
    else:
        print("WARNING: no clover mask", CLOVER_MASK)


_eye_z = 0.294                                        # (the eyes' height, measured)
FACE_C = A + F * 0.08 + U * _eye_z                    # the middle of his face, between the eyes
# "ic_arm": 1 on his shoulders, upper arms and forearms (their skin weights summed), 0 on the hands and the rest
_body = bpy.data.objects["Pie_Knight"]
_arm_vg = {_body.vertex_groups[n].index for n in ("mixamorig:%s%s" % (sd, pt) for sd in ("Left", "Right")
           for pt in ("Shoulder", "Arm", "ForeArm")) if n in _body.vertex_groups}
_w = [min(1.0, sum(g.weight for g in v.groups if g.group in _arm_vg)) for v in _body.data.vertices]
if "ic_arm" in _body.data.attributes:
    _body.data.attributes.remove(_body.data.attributes["ic_arm"])
_body.data.attributes.new("ic_arm", "FLOAT", "POINT").data.foreach_set("value", _w)
log("arm shadow: %d of %d vertices on the arms" % (sum(1 for x in _w if x > 0.5), len(_w)))
knight_copy("Pie_Knight", darker_browns)
# the eye domes' slots (swapped from the smile eyes to the base material) take the same darkened copy
_kb = bpy.data.objects["Pie_Knight"]
_kc = next((sl.material for sl in _kb.material_slots if sl.material and sl.material.name == "IC_Knight_Pie_Knight"), None)
for sl in _kb.material_slots:
    if sl.material and sl.material.name == "Knight_Armour_Sword_Shield" and _kc is not None:
        sl.link = "OBJECT"
        sl.material = _kc
knight_copy("Pie_Knight_Helmet", satin_helmet)

# ---------------------------------------------------------------- his eyes: the round glowing ones (no smile)
body = bpy.data.objects["Pie_Knight"]
base_mat = bpy.data.materials.get("Knight_Armour_Sword_Shield")
for sl in body.material_slots:
    if sl.material and sl.material.name.startswith("KP_Smile_Eye"):
        sl.link = "OBJECT"
        sl.material = base_mat

# ---------------------------------------------------------------- his fists on the digits (IK, stretching: the arms
# are hidden behind the helmet and the digits; only the fists show, resting on the top edge like the badge's paws)
P = arm.pose.bones
def spot(xr):
    x = CX + xr * BADGE_R
    return top_at(x - 0.02, x + 0.02)


def spot_top(xr0, xr1):
    """The middle of the highest flat top between xr0 and xr1 (the 4's stem: its top is the 4's highest point)."""
    x0, x1 = CX + xr0 * BADGE_R, CX + xr1 * BADGE_R
    tops = [((p - A).dot(-R), p.z) for p in pts if x0 <= (p - A).dot(-R) <= x1]
    zmax = max(z for _, z in tops)
    xs = [x for x, z in tops if z > zmax - 0.004]
    return (min(xs) + max(xs)) / 2.0, zmax


# (it used to sit at -0.60 R, on the 4's sloping diagonal, and hung down its side: "sliding off", reviewers)
four = spot_top(*FIST_RIGHT_RANGE)                     # on top of the 4's stem, like the badge's paw
two = spot(FIST_X_R[1])                                # on the 2
targets = {}
for side, spot in (("Right", four), ("Left", two)):    # his right fist on the 4 (screen-left), his left on the 2
    x, z = spot
    t = bpy.data.objects.new("IC_Hand_" + side, None)
    col.objects.link(t)
    t.location = KC(x, logo_front - FIST_IN, z + FIST_LIFT)
    aim = bpy.data.objects.new("IC_Aim_" + side, None)
    col.objects.link(aim)
    aim.location = t.location + F * 0.07 - U * 0.03 + R * (0.05 if side == "Right" else -0.05)   # knuckles to the viewer,
    #                                                       turned in a little toward the middle: hugging the number
    pole = bpy.data.objects.new("IC_Pole_" + side, None)
    col.objects.link(pole)
    sgn = 1.0 if side == "Right" else -1.0
    pole.location = KC(-sgn * 0.3, -0.1, 0.1)
    for bn in ("mixamorig:%sArm" % side, "mixamorig:%sForeArm" % side):
        P[bn].ik_stretch = 1.0
    fa = P["mixamorig:%sForeArm" % side]
    for c_ in list(fa.constraints):
        fa.constraints.remove(c_)
    ik = fa.constraints.new("IK")
    ik.target = t
    ik.pole_target = pole
    ik.chain_count = 2
    ik.use_tail = True
    ik.use_stretch = True
    hb = P["mixamorig:%sHand" % side]
    for c_ in list(hb.constraints):
        hb.constraints.remove(c_)
    dt = hb.constraints.new("DAMPED_TRACK")
    dt.target = aim
    dt.track_axis = "TRACK_Y"
    arm.data.bones["mixamorig:%sHand" % side].inherit_scale = "NONE"   # the stretched arm must not grow the fist
    hb.scale = (HAND_SCALE,) * 3
    targets[side] = (t, pole, ik)
# his right hand is the MIRROR of his left fist. (In the Steam art his right hand is posed round the sword hilt: fingers
# nearly straight, thumb up. With the sword hidden and the hand aimed on its own it read as a flat, twisted mitten:
# user 2026-11-09 "the right arm of the knight is being twisted".) Fingers: the left ones' local rotations, mirrored
# (w, x, -y, -z, Blender's pose-flip rule for a symmetric rig); the wrist: the left hand's armature-space rotation,
# mirrored across the armature's X (left-right) plane, i.e. its Y and Z euler parts inverted.
_lh = arm.data.bones["mixamorig:LeftHand"]
for lb in arm.data.bones:
    if _lh not in lb.parent_recursive:
        continue
    rn = lb.name.replace("Left", "Right").replace(".L", ".R")
    if rn == lb.name or rn not in P:
        continue
    pl, pr = P[lb.name], P[rn]
    q = pl.rotation_quaternion.copy() if pl.rotation_mode == "QUATERNION" else pl.rotation_euler.to_quaternion()
    pr.rotation_mode = "QUATERNION"
    pr.rotation_quaternion = (q[0], q[1], -q[2], -q[3])
    pr.scale = pl.scale.copy()
_hr = P["mixamorig:RightHand"]
for c_ in list(_hr.constraints):
    _hr.constraints.remove(c_)
_cr = _hr.constraints.new("COPY_ROTATION")
_cr.target = arm
_cr.subtarget = "mixamorig:LeftHand"
_cr.target_space = "POSE"
_cr.owner_space = "POSE"
_cr.invert_y = True
_cr.invert_z = True
bpy.context.view_layer.update()
_ml = (arm.matrix_world @ P["mixamorig:LeftHand"].matrix).to_3x3().normalized()
_mr = (arm.matrix_world @ P["mixamorig:RightHand"].matrix).to_3x3().normalized()
log("fists: left knuckles %s, right knuckles %s (camera-facing parts should match, sideways parts opposite)"
    % (tuple(round(x, 2) for x in _ml.col[1]), tuple(round(x, 2) for x in _mr.col[1])))
for side in ("Right", "Left"):
    t = targets[side][0]
    hand = arm.matrix_world @ P["mixamorig:%sHand" % side].head
    log("%s fist on the digits: wrist %.3f m from its target" % (side, (hand - t.location).length))

# ---------------------------------------------------------------- the badge: disc + gold ring
def disc_mesh(name, radius, segs=128):
    bm = bmesh.new()
    bmesh.ops.create_circle(bm, cap_ends=True, cap_tris=False, segments=segs, radius=radius)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    return me


def torus_mesh(name, R_, r_, seg=160, ring=24, flat=0.7):
    bm = bmesh.new()
    rows = []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        row = []
        for j in range(ring):
            b = 2 * math.pi * j / ring
            x = (R_ + r_ * math.cos(b)) * math.cos(a)
            y = (R_ + r_ * math.cos(b)) * math.sin(a)
            z = r_ * math.sin(b) * flat
            row.append(bm.verts.new((x, y, z)))
        rows.append(row)
    for i in range(seg):
        for j in range(ring):
            bm.faces.new((rows[i][j], rows[(i + 1) % seg][j], rows[(i + 1) % seg][(j + 1) % ring], rows[i][(j + 1) % ring]))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    return me


face_cam = Matrix((-R, U, F)).transposed().to_4x4()      # local X -> camera right, Y -> up, Z -> toward the camera


def disc_material():
    """Deep green disc, darker at the rim, with the painted green fire (disc_fire.png, paint_disc_fire.py) glowing on it."""
    m = bpy.data.materials.new("IC_Disc")
    nt = m.node_tree
    nt.nodes.clear()
    N, L = nt.nodes.new, nt.links.new
    out = N("ShaderNodeOutputMaterial")
    tc = N("ShaderNodeTexCoord")
    ln = N("ShaderNodeVectorMath"); ln.operation = "LENGTH"; L(tc.outputs["Object"], ln.inputs[0])
    rr = N("ShaderNodeMath"); rr.operation = "DIVIDE"; rr.inputs[1].default_value = BADGE_R; L(ln.outputs["Value"], rr.inputs[0])
    grad = N("ShaderNodeValToRGB")
    e = grad.color_ramp.elements
    e[0].position, e[0].color = 0.0, (0.005, 0.044, 0.008, 1)       # deep forest green (not black-brown), like the badge
    e[1].position, e[1].color = 1.0, (0.0008, 0.008, 0.0015, 1)     # darker toward the ring (a vignette)
    L(rr.outputs[0], grad.inputs["Fac"])
    # the fire picture across the disc: object x, y (-R..R) -> UV 0..1
    mp = N("ShaderNodeMapping")
    mp.inputs["Location"].default_value = (0.5, 0.5, 0.0)
    mp.inputs["Scale"].default_value = (0.5 / BADGE_R, 0.5 / BADGE_R, 1.0)
    L(tc.outputs["Object"], mp.inputs["Vector"])
    tx = N("ShaderNodeTexImage")
    tx.image = bpy.data.images.load(os.path.join(HERE, "disc_fire.png"), check_existing=True)
    tx.extension = "CLIP"
    L(mp.outputs["Vector"], tx.inputs["Vector"])
    em = N("ShaderNodeEmission"); em.inputs["Strength"].default_value = FIRE_W
    L(tx.outputs["Color"], em.inputs["Color"])
    bs = N("ShaderNodeBsdfPrincipled"); L(grad.outputs["Color"], bs.inputs["Base Color"]); bs.inputs["Roughness"].default_value = 0.8
    ad = N("ShaderNodeAddShader"); L(bs.outputs[0], ad.inputs[0]); L(em.outputs[0], ad.inputs[1])
    L(ad.outputs[0], out.inputs["Surface"])
    return m


disc = bpy.data.objects.new("IC_Disc", disc_mesh("IC_Disc_Mesh", BADGE_R))
col.objects.link(disc)
disc.matrix_world = Matrix.Translation(C + F * DISC_BACK) @ face_cam
disc.data.materials.append(disc_material())
# the badge shows the knight as a bust (helmet + fists over the digits): a second piece of the same disc just behind
# the digits, below the fists, hides his body and legs that would show through the digits' gaps
_D = BADGE_R * CAM_MARGIN * CAM_LENS / 18.0             # the camera's distance from the badge centre
_k = (_D - (LOGO_BACK - 0.006)) / (_D - DISC_BACK)      # closer to the camera: drawn smaller so it lines up with the disc
pm = disc_mesh("IC_Disc_Front_Mesh", BADGE_R * 0.985)
bmf = bmesh.new()
bmf.from_mesh(pm)
cut_y = ((LOGO_TOP - PLATE_BELOW) - BADGE_Z) / _k
bmesh.ops.bisect_plane(bmf, geom=bmf.verts[:] + bmf.edges[:] + bmf.faces[:], plane_co=(0, cut_y, 0), plane_no=(0, 1, 0),
                       clear_outer=True)
bmf.to_mesh(pm)
bmf.free()
plate = bpy.data.objects.new("IC_Disc_Front", pm)
col.objects.link(plate)
plate.matrix_world = Matrix.Translation(C + F * (LOGO_BACK - 0.006)) @ face_cam @ Matrix.Scale(_k, 4)
pmat = disc.data.materials[0].copy()
pmat.name = "IC_Disc_Front"
nt = pmat.node_tree
outn = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL")
surf = outn.inputs["Surface"].links[0].from_socket
tcn = next(n for n in nt.nodes if n.type == "TEX_COORD")
sx = nt.nodes.new("ShaderNodeSeparateXYZ"); nt.links.new(tcn.outputs["Object"], sx.inputs[0])
fade = nt.nodes.new("ShaderNodeMapRange")
fade.inputs["From Min"].default_value = cut_y
fade.inputs["From Max"].default_value = cut_y - 0.05 / _k            # fades in over 5 cm below its top edge
nt.links.new(sx.outputs["Y"], fade.inputs["Value"])
tr = nt.nodes.new("ShaderNodeBsdfTransparent")
mx = nt.nodes.new("ShaderNodeMixShader")
nt.links.new(fade.outputs["Result"], mx.inputs["Fac"]); nt.links.new(tr.outputs[0], mx.inputs[1]); nt.links.new(surf, mx.inputs[2])
nt.links.new(mx.outputs[0], outn.inputs["Surface"])
plate.data.materials.append(pmat)
ring = bpy.data.objects.new("IC_Ring", torus_mesh("IC_Ring_Mesh", BADGE_R, RING_W))
col.objects.link(ring)
ring.matrix_world = Matrix.Translation(C + F * (DISC_BACK + 0.004)) @ face_cam
rg = bpy.data.materials.new("IC_Ring_Gold")             # satin gold: less glossy than the logo's
rgb_ = next(n for n in rg.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
rgb_.inputs["Base Color"].default_value = (0.86, 0.52, 0.12, 1)
rgb_.inputs["Metallic"].default_value = 1.0
rgb_.inputs["Roughness"].default_value = RING_ROUGH
ring.data.materials.append(rg)

# ---------------------------------------------------------------- camera + lights
cam = bpy.data.objects.new("IC_Camera", bpy.data.cameras.new("IC_Camera"))
col.objects.link(cam)
cam.data.lens = CAM_LENS
cam.data.clip_start = 0.05
_half = BADGE_R * CAM_MARGIN
cam.location = C + F * (_half * CAM_LENS / 18.0)      # straight on (the 429 reads flat); frame = +-R x margin at C
cam.rotation_euler = (C - cam.location).to_track_quat("-Z", "Y").to_euler()
sc.camera = cam
cr_ = -R                                                  # camera right


def area(name, loc, look, w, color, size, glossy_only=False):
    L_ = bpy.data.objects.new(name, bpy.data.lights.new(name, "AREA"))
    col.objects.link(L_)
    L_.data.energy, L_.data.color, L_.data.size = w, color, size
    L_.location = loc
    L_.rotation_euler = (look - loc).to_track_quat("-Z", "Y").to_euler()
    return L_


head = arm.matrix_world @ P["mixamorig:Head"].head
area("IC_Key", C + F * 1.1 - cr_ * 0.7 + U * 0.15, head, KEY_W, (1.0, 0.86, 0.66), 1.2)    # big and low: no blown highlight
area("IC_Rim", C - F * 0.5 + cr_ * 0.6 + U * 0.2, head, RIM_W, RIM_COLOUR, 0.35)    # low: no streak on the helmet top
area("IC_Rim2", C - F * 0.5 - cr_ * 0.5 + U * 0.3, head, RIM_W * 0.5, RIM_COLOUR, 0.35)
area("IC_Green", C + F * 0.6 + U * -0.45, C, GREEN_W, (0.3, 1.0, 0.35), 0.6)
if FILL_W > 0:
    area("IC_Fill", C + F * 1.1 + cr_ * 0.5 + U * 0.1, head, FILL_W, (0.85, 0.9, 1.0), 0.8)
rl = area("IC_Ring_Light", C + F * 0.9 - cr_ * 0.3 + U * 0.7, C, RING_LIGHT_W, (1.0, 0.78, 0.45), 1.8)   # big: no glare band
rc = bpy.data.collections.new("IC_Ring_Only")       # the ring and the logo's gold get this warm light
rc.objects.link(ring)
rc.objects.link(logo)
rl.light_linking.receiver_collection = rc
for side in ("Right", "Left"):                        # one warm light on each fist
    fp = targets[side][0].location
    area("IC_Fist_Light_" + side, fp + F * 0.4 + U * 0.1, fp, FIST_W, (1.0, 0.8, 0.55), 0.25)
# warm cards the gold can reflect (seen only in reflections)
for i, (dx, dz) in enumerate(((-0.5, 0.45), (0.55, 0.3), (0.0, 0.75), (0.0, -0.5)) if CARD_W > 0 else ()):
    cd = bpy.data.meshes.new("IC_Card_%d" % i)
    cd.from_pydata([(-0.25, -0.15, 0), (0.25, -0.15, 0), (0.25, 0.15, 0), (-0.25, 0.15, 0)], [], [(0, 1, 2, 3)])
    mt = bpy.data.materials.new("IC_Card_Mat_%d" % i)
    mt.node_tree.nodes.clear()
    o_ = mt.node_tree.nodes.new("ShaderNodeOutputMaterial"); em = mt.node_tree.nodes.new("ShaderNodeEmission")
    em.inputs["Color"].default_value = (1.0, 0.72, 0.35, 1); em.inputs["Strength"].default_value = CARD_W if i < 3 else CARD_W * 0.3
    mt.node_tree.links.new(em.outputs[0], o_.inputs["Surface"])
    cd.materials.append(mt)
    card = bpy.data.objects.new("IC_Card_%d" % i, cd)
    col.objects.link(card)
    card.location = C + F * 0.45 + cr_ * dx * 1.3 + U * (dz * 1.3 + 0.15)
    card.rotation_euler = (C - card.location).to_track_quat("Z", "Y").to_euler()
    for a_ in ("visible_camera", "visible_diffuse", "visible_shadow", "visible_transmission", "visible_volume_scatter"):
        setattr(card, a_, False)
    card.scale = (0.6, 0.6, 0.6)
sc.render.resolution_x = sc.render.resolution_y = 1024
sc.render.resolution_percentage = 100
sc.view_settings.exposure = EXPOSURE
sc.view_settings.view_transform = VIEW
try:
    sc.view_settings.look = "None"
except TypeError:
    pass
sc.view_settings.exposure = EXPOSURE
_cb = next((n for n in sc.compositing_node_group.nodes if n.bl_idname == "CompositorNodeColorBalance"), None)
if _cb is not None:
    _cb.inputs["Factor"].default_value = 0.0         # the Steam scene's warm teal/orange grade: off for the icon
# the Steam scene's bloom is big and soft (a night scene): tighter and weaker for a small, bright icon
gl = next((n for n in sc.compositing_node_group.nodes if n.bl_idname == "CompositorNodeGlare"), None)
if gl is not None:
    gl.inputs["Strength"].default_value = BLOOM_STRENGTH
    gl.inputs["Size"].default_value = BLOOM_SIZE
hs = next((n for n in sc.compositing_node_group.nodes if n.bl_idname == "CompositorNodeHueSat"), None)
if hs is not None:
    hs.inputs["Saturation"].default_value = ICON_SATURATION
bpy.ops.wm.save_mainfile()
log("saved", OUT)
