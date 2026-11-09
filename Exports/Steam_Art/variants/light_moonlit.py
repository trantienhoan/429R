"""MOONLIT CINEMATIC lighting variant of the Steam art.
Opens Exports/Steam_Art/Steam_Art.blend, relights it (moon backlight + god rays through haze, cool rims, cool fill,
warm pumpkin/candle accents, teal-orange grade, vignette) and saves AS variants/Steam_Art_moonlit.blend.
Cameras, logo shape and poses are untouched.
  blender -b Exports/Steam_Art/Steam_Art.blend --python variants/light_moonlit.py
"""
import bpy, math, os
from mathutils import Vector, Matrix

SRC = r"C:/Github_Repository/429R/Exports/Steam_Art/Steam_Art.blend"
OUT = r"C:/Github_Repository/429R/Exports/Steam_Art/variants/Steam_Art_moonlit.blend"
if os.path.normcase(os.path.abspath(bpy.data.filepath or "")) != os.path.normcase(os.path.abspath(SRC)):
    bpy.ops.wm.open_mainfile(filepath=SRC)

sc = bpy.context.scene
OBJ = bpy.data.objects
C_LIGHT = bpy.data.collections["Lights"]
C_LOGO = bpy.data.collections["Logos"]
C_SKY = bpy.data.collections["Sky"]

# ---------------------------------------------------------------- tuning (all the numbers in one place)
MOON_COLOR = (0.78, 0.88, 1.0)
MOON_STRENGTH = 14.0
SUN_ENERGY = 3.0                 # moonlight sun (aimed from the moon: rim + god rays)
SUN_COLOR = (0.55, 0.7, 1.0)
HAZE_DENSITY = 0.0022            # far haze (god rays from the castle / bats)
HAZE_ANISO = 0.72
SET_HAZE_DENSITY = 0.008         # thin haze over the set (backlit glow behind the characters)
RIM = (0.62, 0.78, 1.0)          # blue-white rim colour
WARM = (1.0, 0.48, 0.13)         # pumpkin / candle orange
KEY_ENERGY = 0.8                 # warm key on the pumpkin face
FILL_ENERGY = 1.1                # knight face fill
RIM_GAIN = 1.0                   # all character rims
COOL_FILL = 4.0                  # big dim blue fill from above
PUMPKIN_GLOW = 0.5
GOBO_SCALE = 3.0                 # cloud-hole size of the god-ray gobo
GOBO_HOLE = 1.4                  # metres of clear moonlight around the set
BEAM_STRENGTH = 0.4              # moon shaft cards


def find_principled(m):
    return next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")


def aim(o, target):
    """Point a light's -Z at target (world)."""
    loc = o.matrix_world.translation.copy() if o.parent else Vector(o.location)
    d = (Vector(target) - loc).normalized()
    q = d.to_track_quat("-Z", "Y")
    o.matrix_world = Matrix.Translation(loc) @ q.to_matrix().to_4x4()


def area(name, loc, target, size, color, energy, spread=None, coll=C_LIGHT, parent=None):
    ld = bpy.data.lights.get(name) or bpy.data.lights.new(name, "AREA")
    ld.type = "AREA"
    ld.size, ld.color, ld.energy = size, color, energy
    if spread is not None:
        ld.spread = math.radians(spread)
    o = OBJ.get(name) or bpy.data.objects.new(name, ld)
    if o.name not in coll.objects:
        coll.objects.link(o)
    o.parent = parent
    o.visible_camera = False
    bpy.context.view_layer.update()
    if parent is not None:
        o.matrix_world = Matrix.Translation(Vector(loc))
    else:
        o.location = loc
    bpy.context.view_layer.update()
    aim(o, target)
    return o


def point(name, loc, color, energy, radius=0.03):
    ld = bpy.data.lights.get(name) or bpy.data.lights.new(name, "POINT")
    ld.color, ld.energy, ld.shadow_soft_size = color, energy, radius
    o = OBJ.get(name) or bpy.data.objects.new(name, ld)
    if o.name not in C_LIGHT.objects:
        C_LIGHT.objects.link(o)
    o.location = loc
    o.visible_camera = False
    return o


# ---------------------------------------------------------------- render / colour management
sc.cycles.volume_step_rate = 4.0
sc.cycles.volume_bounces = 0
sc.view_settings.view_transform = "AgX"
for lk in ("AgX - Punchy", "Punchy", "AgX - High Contrast"):
    try:
        sc.view_settings.look = lk
        print("LOOK", lk)
        break
    except Exception:
        pass
sc.view_settings.exposure = 0.0

# ---------------------------------------------------------------- world: deep blue-teal night, fainter cool stars
w = sc.world
nt = w.node_tree
grad = next(n for n in nt.nodes if n.type == "VALTORGB")
grad.color_ramp.elements[0].position = 0.0
grad.color_ramp.elements[0].color = (0.012, 0.03, 0.05, 1)     # horizon: teal-blue glow
grad.color_ramp.elements[1].position = 0.42
grad.color_ramp.elements[1].color = (0.001, 0.003, 0.009, 1)   # zenith: near black navy
star = next(n for n in nt.nodes if n.type == "MAP_RANGE")
star.inputs["To Max"].default_value = 2.2

# ---------------------------------------------------------------- moon: cool blue-white and bright (bloom halo)
moon_m = bpy.data.materials["Steam_Moon"]
cr = next(n for n in moon_m.node_tree.nodes if n.type == "VALTORGB")
cr.color_ramp.elements[0].color = (0.45, 0.55, 0.72, 1)
cr.color_ramp.elements[1].color = (*MOON_COLOR, 1)
next(n for n in moon_m.node_tree.nodes if n.type == "EMISSION").inputs["Strength"].default_value = MOON_STRENGTH
moon = OBJ["Moon"]
moon_loc = moon.matrix_world.translation.copy()

# ---------------------------------------------------------------- moonlight sun aimed FROM the moon (rays converge on it)
sun_o = OBJ["Moonlight"]
sun = sun_o.data
sun.color, sun.energy, sun.angle = SUN_COLOR, SUN_ENERGY, math.radians(1.2)
set_center = Vector((0.0, 0.2, 0.2))
d = (set_center - moon_loc).normalized()
sun_o.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
print("SUN dir", tuple(round(c, 3) for c in d))

# ---------------------------------------------------------------- haze: far (god rays) + thin set haze (backlit glow)
haze_m = bpy.data.materials["Steam_Haze"]
pv = next(n for n in haze_m.node_tree.nodes if n.type == "PRINCIPLED_VOLUME")
pv.inputs["Color"].default_value = (0.5, 0.65, 1.0, 1)
pv.inputs["Density"].default_value = HAZE_DENSITY
pv.inputs["Anisotropy"].default_value = HAZE_ANISO
haze = OBJ["Haze"]
haze.location = (0, -31.5, 6)
haze.scale = (140, 57, 30)              # y from -3 to -60: right behind the stone ring

sh_m = bpy.data.materials.get("Steam_Set_Haze") or bpy.data.materials.new("Steam_Set_Haze")
sh_m.use_nodes = True
sn = sh_m.node_tree.nodes
sn.clear()
so = sn.new("ShaderNodeOutputMaterial")
sv = sn.new("ShaderNodeVolumePrincipled")
sv.inputs["Color"].default_value = (0.55, 0.7, 1.0, 1)
sv.inputs["Density"].default_value = SET_HAZE_DENSITY
sv.inputs["Anisotropy"].default_value = 0.65
sh_m.node_tree.links.new(sv.outputs["Volume"], so.inputs["Volume"])
set_haze = OBJ.get("Set_Haze")
if set_haze is None:
    me = bpy.data.meshes.new("Set_Haze")
    import bmesh
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bm.to_mesh(me)
    bm.free()
    set_haze = bpy.data.objects.new("Set_Haze", me)
    C_SKY.objects.link(set_haze)
    me.materials.append(sh_m)
set_haze.location = (0.0, -1.65, 0.75)
set_haze.scale = (6.0, 3.1, 1.5)        # x -3..3, y -3.2..-0.1 (only behind the characters), z 0..1.5
set_haze.visible_shadow = False
set_haze.visible_glossy = False

# ---------------------------------------------------------------- character lights
KNIGHT_HEAD = Vector((-0.27, 0.13, 0.31))
PUMPKIN_C = Vector((0.14, 0.27, 0.2))

# Warm key: a narrow pool on the pumpkin's face (floor falls off quickly).
area("Key_Warm", (-0.35, 1.35, 0.85), PUMPKIN_C + Vector((0, 0.1, 0.0)), 0.5, (1.0, 0.66, 0.4), KEY_ENERGY, spread=40)
# Knight face fill: soft cool-neutral, just enough for the visor and eyes.
area("Knight_Fill", (-0.5, 0.95, 0.5), KNIGHT_HEAD, 0.3, (0.72, 0.84, 1.0), FILL_ENERGY, spread=50)
# Moon-side rims (behind the characters, blue-white). Light-linked to the characters only, so the floor
# does not catch them (no white floor pool, no specular glare toward the camera).
rims = [
    area("Rim_Cool", (0.8, -0.9, 0.75), PUMPKIN_C + Vector((0, 0, 0.08)), 0.45, RIM, 24.0 * RIM_GAIN, spread=70),
    area("Rim_Knight", (-0.95, -0.55, 0.7), KNIGHT_HEAD + Vector((0, 0, -0.05)), 0.35, RIM, 18.0 * RIM_GAIN, spread=70),
    area("Rim_Top", (-0.1, -0.75, 1.35), Vector((-0.05, 0.2, 0.25)), 0.5, RIM, 16.0 * RIM_GAIN, spread=60),
    area("Rim_Small_L", (1.4, -0.2, 0.6), Vector((0.95, 0.75, 0.1)), 0.4, RIM, 14.0 * RIM_GAIN, spread=80),
    area("Rim_Small_R", (-1.5, -0.3, 0.6), Vector((-0.9, 0.7, 0.1)), 0.4, RIM, 14.0 * RIM_GAIN, spread=80),
]
recv = bpy.data.collections.get("Moonlit_Rim_Receivers") or bpy.data.collections.new("Moonlit_Rim_Receivers")
for o in OBJ:
    if o.type == "MESH" and (o.name.startswith(("Pie_Knight", "Small_Pumpkin_", "Candles_")) or o.name == "Hero_Pumpkin"):
        if o.name not in recv.objects:
            recv.objects.link(o)
for r in rims:
    r.light_linking.receiver_collection = recv
# Cool moon fill from above-front: blue shadows, very dim.
area("Cool_Fill", (0.3, 1.0, 2.4), Vector((0.0, 0.1, 0.0)), 1.5, (0.35, 0.5, 0.95), COOL_FILL, spread=60)
# Pumpkin spill onto the knight's near side (orange accent on the green armour).
point("Pumpkin_Spill", (-0.06, 0.36, 0.2), WARM, 0.55, 0.04)
OBJ["Pumpkin_Glow"].data.color = WARM
OBJ["Pumpkin_Glow"].data.energy = PUMPKIN_GLOW

# ---------------------------------------------------------------- god-ray gobo: a cloud-holed sheet in front of the moon
# Invisible to the camera, it only casts the sun's shadow, so the haze shows light shafts streaming from the moon.
gobo_m = bpy.data.materials.get("Steam_Moon_Gobo") or bpy.data.materials.new("Steam_Moon_Gobo")
gobo_m.use_nodes = True
gn_ = gobo_m.node_tree.nodes
gn_.clear()
g_out = gn_.new("ShaderNodeOutputMaterial")
g_tc = gn_.new("ShaderNodeTexCoord")
g_mp = gn_.new("ShaderNodeMapping")
g_mp.inputs["Scale"].default_value = (1.0, 2.2, 1.0)
g_noise = gn_.new("ShaderNodeTexNoise")
g_noise.inputs["Scale"].default_value = GOBO_SCALE
g_noise.inputs["Detail"].default_value = 4.0
g_noise.inputs["Roughness"].default_value = 0.6
g_ramp = gn_.new("ShaderNodeValToRGB")
g_ramp.color_ramp.elements[0].position = 0.46
g_ramp.color_ramp.elements[0].color = (0, 0, 0, 1)
g_ramp.color_ramp.elements[1].position = 0.54
g_ramp.color_ramp.elements[1].color = (1, 1, 1, 1)
g_tr = gn_.new("ShaderNodeBsdfTransparent")
g_df = gn_.new("ShaderNodeBsdfDiffuse")
g_df.inputs["Color"].default_value = (0, 0, 0, 1)
g_mx = gn_.new("ShaderNodeMixShader")
L2 = gobo_m.node_tree.links.new
L2(g_tc.outputs["Generated"], g_mp.inputs["Vector"])
L2(g_mp.outputs["Vector"], g_noise.inputs["Vector"])
L2(g_noise.outputs["Fac"], g_ramp.inputs["Factor"])
# a clear hole where the moonlight reaches the knight and pumpkin (object coords * size in metres)
g_oc = gn_.new("ShaderNodeVectorMath")
g_oc.operation = "MULTIPLY"
g_oc.inputs[1].default_value = (14.0, 9.0, 0.0)
L2(g_tc.outputs["Object"], g_oc.inputs[0])
g_len = gn_.new("ShaderNodeVectorMath")
g_len.operation = "LENGTH"
L2(g_oc.outputs["Vector"], g_len.inputs[0])
g_hole = gn_.new("ShaderNodeMapRange")
g_hole.inputs["From Min"].default_value = GOBO_HOLE
g_hole.inputs["From Max"].default_value = GOBO_HOLE * 0.6
L2(g_len.outputs["Value"], g_hole.inputs["Value"])
g_max = gn_.new("ShaderNodeMath")
g_max.operation = "MAXIMUM"
L2(g_ramp.outputs["Color"], g_max.inputs[0])
L2(g_hole.outputs["Result"], g_max.inputs[1])
L2(g_max.outputs[0], g_mx.inputs["Fac"])      # 1 = hole (transparent)
L2(g_df.outputs[0], g_mx.inputs[1])
L2(g_tr.outputs[0], g_mx.inputs[2])
L2(g_mx.outputs[0], g_out.inputs["Surface"])
gobo = OBJ.get("Moon_Gobo")
if gobo is None:
    gme = bpy.data.meshes.new("Moon_Gobo")
    gme.from_pydata([(-1, -1, 0), (1, -1, 0), (1, 1, 0), (-1, 1, 0)], [], [(0, 1, 2, 3)])
    gme.materials.append(gobo_m)
    gobo = bpy.data.objects.new("Moon_Gobo", gme)
    C_SKY.objects.link(gobo)
# Halfway between the moon and the set, facing along the moonlight, big enough to cover the set's light.
gpos = moon_loc + (set_center - moon_loc) * 0.12
gobo.location = gpos
gobo.rotation_euler = (-d).to_track_quat("Z", "Y").to_euler()
gobo.scale = (14, 9, 1)
gobo.visible_camera = False
gobo.visible_diffuse = False
gobo.visible_glossy = False
gobo.visible_transmission = False
gobo.visible_volume_scatter = False
gobo.visible_shadow = True

for o in OBJ:
    if o.type == "LIGHT" and o.name.startswith("Small_Pumpkin_Glow_"):
        o.data.color = WARM
        o.data.energy *= 1.3
    if o.type == "LIGHT" and o.name.startswith(("Candles_L_Light", "Candles_R_Light")):
        o.data.color = (1.0, 0.55, 0.2)
        o.data.energy = 0.55

# ---------------------------------------------------------------- moon shafts: soft additive beam cards fanning from the moon
# (camera-only, behind the castle so its towers cut them; cheap and the same in every camera)
beam_m = bpy.data.materials.get("Steam_Moon_Beam") or bpy.data.materials.new("Steam_Moon_Beam")
beam_m.use_nodes = True
bn = beam_m.node_tree.nodes
bn.clear()
b_out = bn.new("ShaderNodeOutputMaterial")
b_uv = bn.new("ShaderNodeUVMap")
b_sep = bn.new("ShaderNodeSeparateXYZ")
beam_m.node_tree.links.new(b_uv.outputs["UV"], b_sep.inputs[0])
b_along = bn.new("ShaderNodeMath")          # (1-u)^1.6 : fades with distance from the moon
b_along.operation = "SUBTRACT"
b_along.inputs[0].default_value = 1.0
beam_m.node_tree.links.new(b_sep.outputs["X"], b_along.inputs[1])
b_ap = bn.new("ShaderNodeMath")
b_ap.operation = "POWER"
b_ap.inputs[1].default_value = 1.6
beam_m.node_tree.links.new(b_along.outputs[0], b_ap.inputs[0])
b_c = bn.new("ShaderNodeMath")              # across: 1-|2v-1|, squared -> soft edges
b_c.operation = "MULTIPLY_ADD"
b_c.inputs[1].default_value = 2.0
b_c.inputs[2].default_value = -1.0
beam_m.node_tree.links.new(b_sep.outputs["Y"], b_c.inputs[0])
b_abs = bn.new("ShaderNodeMath")
b_abs.operation = "ABSOLUTE"
beam_m.node_tree.links.new(b_c.outputs[0], b_abs.inputs[0])
b_inv = bn.new("ShaderNodeMath")
b_inv.operation = "SUBTRACT"
b_inv.inputs[0].default_value = 1.0
beam_m.node_tree.links.new(b_abs.outputs[0], b_inv.inputs[1])
b_sq = bn.new("ShaderNodeMath")
b_sq.operation = "POWER"
b_sq.inputs[1].default_value = 2.2
beam_m.node_tree.links.new(b_inv.outputs[0], b_sq.inputs[0])
b_m = bn.new("ShaderNodeMath")
b_m.operation = "MULTIPLY"
beam_m.node_tree.links.new(b_ap.outputs[0], b_m.inputs[0])
beam_m.node_tree.links.new(b_sq.outputs[0], b_m.inputs[1])
b_attr = bn.new("ShaderNodeAttribute")      # per-beam strength (object property "beam")
b_attr.attribute_type = "OBJECT"
b_attr.attribute_name = "beam"
b_s = bn.new("ShaderNodeMath")
b_s.operation = "MULTIPLY"
beam_m.node_tree.links.new(b_m.outputs[0], b_s.inputs[0])
beam_m.node_tree.links.new(b_attr.outputs["Fac"], b_s.inputs[1])
b_em = bn.new("ShaderNodeEmission")
b_em.inputs["Color"].default_value = (0.62, 0.78, 1.0, 1)
beam_m.node_tree.links.new(b_s.outputs[0], b_em.inputs["Strength"])
b_tr = bn.new("ShaderNodeBsdfTransparent")
b_add = bn.new("ShaderNodeAddShader")
beam_m.node_tree.links.new(b_em.outputs[0], b_add.inputs[0])
beam_m.node_tree.links.new(b_tr.outputs[0], b_add.inputs[1])
beam_m.node_tree.links.new(b_add.outputs[0], b_out.inputs["Surface"])

for o in [o for o in OBJ if o.name.startswith("Moon_Beam_")]:
    bpy.data.objects.remove(o)
cam_avg = Vector((0.75, 2.6, 0.45))
face = (cam_avg - moon_loc).normalized()
q = face.to_track_quat("Z", "Y")             # card plane faces the cameras; local +Y = world up-ish
import random
rnd = random.Random(4290)
# angle (deg, 0 = right, -90 = straight down), length m, end width m, strength
BEAMS = [(-8, 22, 3.0, 0.35), (-28, 26, 4.0, 0.7), (-152, 26, 4.0, 0.7), (-172, 22, 3.0, 0.35), (-62, 30, 4.5, 1.0), (-78, 34, 3.0, 0.8), (-95, 32, 5.5, 1.0), (-112, 30, 3.5, 0.75), (-128, 28, 5.0, 0.9),
         (-146, 24, 3.0, 0.6), (-40, 26, 3.5, 0.7), (-20, 22, 2.5, 0.5), (-165, 20, 2.5, 0.5), (10, 14, 2.0, 0.35),
         (170, 14, 2.0, 0.35), (60, 12, 2.0, 0.3), (115, 12, 2.0, 0.3)]
for i, (ang, length, wid, st) in enumerate(BEAMS):
    a = math.radians(ang + rnd.uniform(-3, 3))
    r0 = 2.0
    p = lambda r, s: (r * math.cos(a) - s * math.sin(a), r * math.sin(a) + s * math.cos(a), 0.0)
    verts = [p(r0, -0.5), p(length, -wid / 2), p(length, wid / 2), p(r0, 0.5)]
    me = bpy.data.meshes.new(f"Moon_Beam_{i}")
    me.from_pydata(verts, [], [(0, 1, 2, 3)])
    uv = me.uv_layers.new()
    for li, uvv in zip(range(4), [(0, 0), (1, 0), (1, 1), (0, 1)]):
        uv.data[li].uv = uvv
    me.materials.append(beam_m)
    b = bpy.data.objects.new(f"Moon_Beam_{i}", me)
    C_SKY.objects.link(b)
    b.matrix_world = Matrix.Translation(moon_loc + face * (0.4 + 0.01 * i)) @ q.to_matrix().to_4x4()
    b["beam"] = st * BEAM_STRENGTH
    b.visible_shadow = False
    b.visible_diffuse = False
    b.visible_glossy = False
    b.visible_transmission = False
    b.visible_volume_scatter = False

# ---------------------------------------------------------------- materials (emission / roughness only)
pm = bpy.data.materials["Steam_Jack_O_Lantern"]
mr = next(n for n in pm.node_tree.nodes if n.type == "MAP_RANGE")
mr.inputs["From Min"].default_value = 0.66
mr.inputs["From Max"].default_value = 0.86
mr.inputs["To Max"].default_value = 14.0
pp = find_principled(pm)
# Tint the face glow orange-yellow (multiply the albedo by a warm colour).
if not any(n.name == "Moonlit_Tint" for n in pm.node_tree.nodes):
    alb = next(n for n in pm.node_tree.nodes if n.type == "TEX_IMAGE" and "Albedo" in (n.image.name if n.image else ""))
    mul = pm.node_tree.nodes.new("ShaderNodeMix")
    mul.name = "Moonlit_Tint"
    mul.data_type = "RGBA"
    mul.blend_type = "MULTIPLY"
    mul.inputs["Factor"].default_value = 1.0
    mul.inputs[7].default_value = (1.0, 0.62, 0.22, 1)
    pm.node_tree.links.new(alb.outputs["Color"], mul.inputs[6])
    pm.node_tree.links.new(mul.outputs[2], pp.inputs["Emission Color"])

# Knight's eyes: the bright lime parts of his texture glow softly.
km = bpy.data.materials["Knight_Armour_Sword_Shield"]
kp = find_principled(km)
if not any(n.name == "Moonlit_Eyes" for n in km.node_tree.nodes):
    kmix = next(n for n in km.node_tree.nodes if n.type == "MIX")
    sep = km.node_tree.nodes.new("ShaderNodeSeparateColor")
    sep.name = "Moonlit_Eyes"
    km.node_tree.links.new(kmix.outputs[2], sep.inputs[0])
    sub = km.node_tree.nodes.new("ShaderNodeMath")
    sub.operation = "SUBTRACT"
    km.node_tree.links.new(sep.outputs["Green"], sub.inputs[0])
    km.node_tree.links.new(sep.outputs["Red"], sub.inputs[1])
    rng = km.node_tree.nodes.new("ShaderNodeMapRange")
    rng.name = "Moonlit_Eyes_Range"
    rng.inputs["From Min"].default_value = 0.35
    rng.inputs["From Max"].default_value = 0.6
    rng.inputs["To Max"].default_value = 3.0
    km.node_tree.links.new(sub.outputs[0], rng.inputs["Value"])
    km.node_tree.links.new(kmix.outputs[2], kp.inputs["Emission Color"])
    km.node_tree.links.new(rng.outputs["Result"], kp.inputs["Emission Strength"])

bpy.data.materials["Steam_Candle_Flame"].node_tree.nodes  # (kept)
find_principled(bpy.data.materials["Steam_Candle_Flame"]).inputs["Emission Strength"].default_value = 70
find_principled(bpy.data.materials["Steam_Candle_Flame"]).inputs["Emission Color"].default_value = (1.0, 0.55, 0.15, 1)
find_principled(bpy.data.materials["Steam_Castle_Window"]).inputs["Emission Strength"].default_value = 45
# Floor: a little sheen to catch the moon.
lid = bpy.data.materials["Rooftop_Base_Lid"]
find_principled(lid).inputs["Roughness"].default_value = 0.5

# Logo: deeper gold metal, glowing green face.
gold = bpy.data.materials["Steam_Logo_Gold"]
gp = find_principled(gold)
gp.inputs["Roughness"].default_value = 0.22
gp.inputs["Emission Strength"].default_value = 0.15
green = bpy.data.materials["Steam_Logo_Green"]
gr = find_principled(green)
gr.inputs["Base Color"].default_value = (0.0, 0.06, 0.012, 1)
gr.inputs["Emission Color"].default_value = (0.12, 1.0, 0.2, 1)
gr.inputs["Emission Strength"].default_value = 0.1

# Per-logo: cool rim from behind-above (blue edges on the gold bevel) + soft green halo card behind.
halo = bpy.data.materials.get("Steam_Logo_Halo") or bpy.data.materials.new("Steam_Logo_Halo")
halo.use_nodes = True
hn = halo.node_tree.nodes
hn.clear()
h_out = hn.new("ShaderNodeOutputMaterial")
h_em = hn.new("ShaderNodeEmission")
h_em.inputs["Color"].default_value = (0.15, 1.0, 0.35, 1)
h_tr = hn.new("ShaderNodeBsdfTransparent")
h_add = hn.new("ShaderNodeAddShader")
h_tc = hn.new("ShaderNodeTexCoord")
h_mp = hn.new("ShaderNodeMapping")
h_mp.inputs["Location"].default_value = (-1.0, -1.0, 0)
h_mp.inputs["Scale"].default_value = (2.0, 2.0, 1.0)
h_gd = hn.new("ShaderNodeTexGradient")
h_gd.gradient_type = "SPHERICAL"
h_pw = hn.new("ShaderNodeMath")
h_pw.operation = "POWER"
h_pw.inputs[1].default_value = 2.5
h_st = hn.new("ShaderNodeMath")
h_st.operation = "MULTIPLY"
h_st.inputs[1].default_value = 0.5
halo.node_tree.links.new(h_tc.outputs["Generated"], h_mp.inputs["Vector"])
halo.node_tree.links.new(h_mp.outputs["Vector"], h_gd.inputs["Vector"])
halo.node_tree.links.new(h_gd.outputs["Fac"], h_pw.inputs[0])
halo.node_tree.links.new(h_pw.outputs[0], h_st.inputs[0])
halo.node_tree.links.new(h_st.outputs[0], h_em.inputs["Strength"])
halo.node_tree.links.new(h_em.outputs[0], h_add.inputs[0])
halo.node_tree.links.new(h_tr.outputs[0], h_add.inputs[1])
halo.node_tree.links.new(h_add.outputs[0], h_out.inputs["Surface"])
halo.surface_render_method = "BLENDED" if hasattr(halo, "surface_render_method") else None

bpy.context.view_layer.update()
for lg in [o for o in C_LOGO.objects if o.name.startswith("LOGO_") and o.type == "MESH"]:
    cam = lg.parent
    s = lg.scale.x
    k = s / 0.15
    L = lg.matrix_world.translation.copy()
    cm = cam.matrix_world
    up = (cm.to_3x3() @ Vector((0, 1, 0))).normalized()
    fwd = (cm.to_3x3() @ Vector((0, 0, -1))).normalized()     # camera looking direction
    right = (cm.to_3x3() @ Vector((1, 0, 0))).normalized()
    rim_loc = L + fwd * 0.35 * k + up * 0.22 * k - right * 0.05 * k
    area(lg.name + "_Rim", rim_loc, L + up * 0.02 * k, 0.3 * k, RIM, 9.0 * k * k, spread=80, coll=C_LOGO, parent=cam)
    # the warm key a touch warmer and stronger (gold must sparkle against the dark sky)
    key = OBJ.get(lg.name + "_Key")
    if key:
        key.data.color = (1.0, 0.82, 0.58)
        key.data.energy *= 1.15
        key.visible_camera = False
    gl = OBJ.get(lg.name + "_Light")
    if gl:
        gl.data.color = (0.2, 1.0, 0.35)
    # The logo's own lights light ONLY the logo (they used to wash the near floor brown/green).
    rc = bpy.data.collections.get("Moonlit_Recv_" + lg.name) or bpy.data.collections.new("Moonlit_Recv_" + lg.name)
    if lg.name not in rc.objects:
        rc.objects.link(lg)
    for ln in ("_Key", "_Light", "_Rim"):
        lo = OBJ.get(lg.name + ln)
        if lo:
            lo.light_linking.receiver_collection = rc
    # halo card just behind the logo (render_all hides *_Glow on the transparent library logo)
    hname = lg.name + "_Glow"
    card = OBJ.get(hname)
    if card is None:
        cme = bpy.data.meshes.new(hname)
        cme.from_pydata([(-1, -1, 0), (1, -1, 0), (1, 1, 0), (-1, 1, 0)], [], [(0, 1, 2, 3)])
        cme.uv_layers.new()
        cme.materials.append(halo)
        card = bpy.data.objects.new(hname, cme)
        C_LOGO.objects.link(card)
    card.parent = cam
    card.location = Vector(lg.location) + Vector((0, 0, -0.06))
    card.rotation_euler = (0, 0, 0)
    card.scale = (0.24 * k, 0.11 * k, 1)
    card.visible_shadow = False
    card.visible_diffuse = False
    card.visible_glossy = False

# ---------------------------------------------------------------- compositor: bloom -> teal/orange CDL -> vignette
tree = sc.compositing_node_group
tree.nodes.clear()
if not any(s.in_out == "OUTPUT" for s in tree.interface.items_tree):
    tree.interface.new_socket("Image", in_out="OUTPUT", socket_type="NodeSocketColor")
N = tree.nodes.new
Lk = tree.links.new
rl = N("CompositorNodeRLayers")
bloom = N("CompositorNodeGlare")
for nm, v in (("Type", "Bloom"), ("Quality", "High"), ("Threshold", 0.85), ("Strength", 0.55), ("Size", 0.72)):
    bloom.inputs[nm].default_value = v
Lk(rl.outputs["Image"], bloom.inputs["Image"])
cb = N("CompositorNodeColorBalance")
cb.inputs["Type"].default_value = "Offset/Power/Slope (ASC-CDL)"
cols = [i for i in cb.inputs if i.bl_idname == "NodeSocketColor" and i.name in ("Offset", "Power", "Slope")]
for i in cols:
    if i.name == "Offset":
        i.default_value = (-0.0015, 0.0012, 0.0045, 1)    # teal blacks
    elif i.name == "Power":
        i.default_value = (1.02, 1.0, 0.95, 1)
    elif i.name == "Slope":
        i.default_value = (1.06, 1.0, 0.93, 1)           # warm highlights
Lk(bloom.outputs["Image"], cb.inputs["Image"])
hs = N("CompositorNodeHueSat")
hs.inputs["Saturation"].default_value = 1.12
Lk(cb.outputs["Image"], hs.inputs["Image"])
# vignette from normalized image coordinates (resolution independent)
ic = N("CompositorNodeImageCoordinates")
Lk(rl.outputs["Image"], ic.inputs["Image"])
vm = N("ShaderNodeVectorMath")
vm.operation = "SUBTRACT"
vm.inputs[1].default_value = (0.5, 0.56, 0.0)   # centre a bit high: darker floor edge
Lk(ic.outputs["Normalized"], vm.inputs[0])
vl = N("ShaderNodeVectorMath")
vl.operation = "LENGTH"
Lk(vm.outputs["Vector"], vl.inputs[0])
vr = N("ShaderNodeMapRange")
vr.interpolation_type = "SMOOTHSTEP"
vr.inputs["From Min"].default_value = 0.32
vr.inputs["From Max"].default_value = 0.78
vr.inputs["To Min"].default_value = 0.0
vr.inputs["To Max"].default_value = 0.72
Lk(vl.outputs["Value"], vr.inputs["Value"])
vig = N("ShaderNodeMix")
vig.data_type = "RGBA"
vig.blend_type = "MIX"
vig.inputs[7].default_value = (0.0, 0.004, 0.008, 1)
Lk(vr.outputs["Result"], vig.inputs["Factor"])
Lk(hs.outputs["Image"], vig.inputs[6])
go = N("NodeGroupOutput")
try:   # keep the render's own alpha (the library logo is transparent)
    sa = N("CompositorNodeSetAlpha")
    if "Type" in sa.inputs:
        sa.inputs["Type"].default_value = "Replace Alpha"
    Lk(vig.outputs[2], sa.inputs["Image"])
    Lk(rl.outputs["Alpha"], sa.inputs["Alpha"])
    Lk(sa.outputs["Image"], go.inputs[0])
except Exception as e:
    print("SETALPHA", e)
    Lk(vig.outputs[2], go.inputs[0])

os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=OUT, copy=False)
print("SAVED", OUT)
