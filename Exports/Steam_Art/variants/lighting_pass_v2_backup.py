"""Final lighting + framing pass for the Steam art (verdict recipe: moonlit base + pumpkinglow lanterns + magic logo).
Opens Exports/Steam_Art/Steam_Art.blend, relights it, re-frames the cameras and SAVES OVER it. Run assemble_v2.py first.
Safe to run again: everything it adds is named LP_* (removed and rebuilt each run); every value it sets is absolute.
The first run backs the unlit file up to variants/Steam_Art_before_lighting.blend.
  "E:/Steam/steamapps/common/Blender/blender.exe" -b Exports/Steam_Art/Steam_Art.blend --python Exports/Steam_Art/lighting_pass.py
"""
import bpy, bmesh, math, os, shutil, random
from mathutils import Vector, Matrix, Euler

SRC = r"C:/Github_Repository/429R/Exports/Steam_Art/Steam_Art.blend"
BACKUP = r"C:/Github_Repository/429R/Exports/Steam_Art/variants/Steam_Art_before_lighting.blend"
if os.path.normcase(os.path.abspath(bpy.data.filepath or "")) != os.path.normcase(os.path.abspath(SRC)):
    bpy.ops.wm.open_mainfile(filepath=SRC)
sc = bpy.context.scene
OB = bpy.data.objects
C_LIGHT = bpy.data.collections["Lights"]
C_LOGO = bpy.data.collections["Logos"]
C_SKY = bpy.data.collections["Sky"]

if not sc.get("lighting_pass"):            # the file on disk is still the unlit build: keep a copy
    os.makedirs(os.path.dirname(BACKUP), exist_ok=True)
    shutil.copy2(SRC, BACKUP)
    print("BACKUP", BACKUP)

# ---------------------------------------------------------------- tuning
MOON_COLOR = (0.78, 0.88, 1.0)
MOON_STRENGTH = 1.4             # verdict said 10; lower keeps the moon texture and stops it pulling focus
SUN_ENERGY = 3.0               # lights the characters and props only (not the floor or the far background)
SUN_COLOR = (0.55, 0.7, 1.0)
HAZE_DENSITY = 0.0016
HAZE_ANISO = 0.72
SET_HAZE_DENSITY = 0.008
RIM = (0.62, 0.78, 1.0)
WARM = (1.0, 0.48, 0.13)
GREEN = (0.22, 1.0, 0.3)
KEY_ENERGY = 0.45
FILL_ENERGY = 1.1
RIM_GAIN = 1.0
COOL_FILL = 4.0
PUMPKIN_GLOW = 0.5
GOBO_SCALE = 3.0
GOBO_HOLE = 1.4
BEAM_STRENGTH = 0.18
HERO_INNER_W = 50.0             # hero pumpkin's inner light (it only gets out through the carved face)
SMALL_INNER_W = 3.0             # small pumpkins' inner lights, times their size squared
GLOW_INNER = 0.55               # PV2_Pumpkin_Inner "Glow" (keep near 1: AgX washes higher values to peach)
GLOW_CUT = 1.0                  # PV2_Pumpkin_Cut_Flesh "Glow"
PILLAR_GLOW_W = 1.0             # 3.5 tinted the back floor green
AMBIENT = (0.005, 0.007, 0.016)
HALO_STRENGTH = 1.5             # logo halo shell glow
HALO_GROW = 1.012               # halo shell scale over the logo
HALO_PUFF = 0.03                # halo shell push-out along the normals (logo mesh units)
LOGO_KEY_GAIN = 1.0             # logo key light
HAZE_NEAR_GAIN = 0.5            # far background: haze emission on the near ground (1 = as built)
GROUND_ALBEDO_GAIN = 0.55        # far background: BG2_Ground / BG2_Path colour (1 = as built)
HAZE_NEAR_MATS = ("BG2_Ground", "BG2_Path", "BG2_Hill_Rock", "BG2_DeadGrass", "BG2_Tombstone", "BG2_Iron", "BG2_DeadTree")
LOGO_REFLECTORS = ("Logo_429_Reflector_Low", "Logo_429_Reflector_Top")   # the asset's Front card mirrors in the flat back plate (olive)

# ---------------------------------------------------------------- clean out the previous run
for o in [o for o in OB if o.name.startswith("LP_")]:
    bpy.data.objects.remove(o, do_unlink=True)
for c in [c for c in bpy.data.collections if c.name.startswith("LP_")]:
    bpy.data.collections.remove(c)
for blocks in (bpy.data.lights, bpy.data.meshes):
    for b in [b for b in blocks if b.name.startswith("LP_") and b.users == 0]:
        blocks.remove(b)


def principled(m):
    return next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")


def drop_lp_nodes(nt):
    for n in [n for n in nt.nodes if n.name.startswith("LP_")]:
        nt.nodes.remove(n)


def new_node(nt, kind, name, **inputs):
    n = nt.nodes.new(kind)
    n.name = n.label = name
    for k, v in inputs.items():
        n.inputs[k].default_value = v
    return n


def track(d):
    return Vector(d).to_track_quat("-Z", "Y").to_euler()


def aim(o, target):
    o.rotation_euler = track(Vector(target) - o.location)


def new_light(name, kind, loc, color, energy, size=0.05, target=None, spread=None, coll=C_LIGHT, parent=None):
    """New LP_ light (world loc, or camera-local loc when parent is given). Hidden from the camera."""
    ld = bpy.data.lights.new(name, kind)
    ld.color, ld.energy = color, energy
    if kind == "AREA":
        ld.size = size
        if spread is not None:
            ld.spread = math.radians(spread)
    else:
        ld.shadow_soft_size = size
    o = bpy.data.objects.new(name, ld)
    coll.objects.link(o)
    o.parent = parent
    o.location = loc
    if target is not None:
        aim(o, target)
    o.visible_camera = False
    return o


def set_area(o, loc, target, size, color, energy, spread=None):
    """Re-set one of the build's area lights (absolute values)."""
    ld = o.data
    ld.type = "AREA"
    ld = o.data
    ld.size, ld.color, ld.energy = size, color, energy
    if spread is not None:
        ld.spread = math.radians(spread)
    o.parent = None
    o.location = loc
    aim(o, target)
    o.visible_camera = False
    o.hide_render = False


def coll_of(name, objs, state=None):
    c = bpy.data.collections.new(name)          # not in the scene: only light linking uses it
    for o in objs:
        c.objects.link(o)
    if state:
        for co in c.collection_objects:
            co.light_linking.link_state = state
    return c


def distance_cutoff(o, near, far):
    """Light node tree: smooth fade to 0 between near and far metres (hard falloff)."""
    ld = o.data
    try:
        ld.use_nodes = True
    except Exception as e:
        print("light nodes", e)
    t = ld.node_tree
    if t is None:
        print("NO LIGHT NODE TREE", o.name)
        return
    for n in [n for n in t.nodes if n.name.startswith(("PG_", "LP_"))]:
        t.nodes.remove(n)
    em = next(n for n in t.nodes if n.type == "EMISSION")
    lf = new_node(t, "ShaderNodeLightFalloff", "LP_Falloff", Strength=1.0)
    mr = t.nodes.new("ShaderNodeMapRange"); mr.name = "LP_Cutoff"
    mr.interpolation_type = "SMOOTHSTEP"
    for k, v in (("From Min", near), ("From Max", far), ("To Min", 1.0), ("To Max", 0.0)):
        mr.inputs[k].default_value = v
    t.links.new(lf.outputs["Linear"], mr.inputs["Value"])
    t.links.new(mr.outputs["Result"], em.inputs["Strength"])


def mesh_obj(name, verts, faces, mat, coll, uvs=None):
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    if uvs:
        uv = me.uv_layers.new()
        for li, uvv in enumerate(uvs):
            uv.data[li].uv = uvv
    me.materials.append(mat)
    o = bpy.data.objects.new(name, me)
    coll.objects.link(o)
    return o


def sock(socks, name):
    """The colour socket called name (ShaderNodeMix has one A/B/Result per data type)."""
    return next(s for s in socks if s.name == name and s.type == "RGBA")


def mesh_width(me):
    xs = [v.co.x for v in me.vertices]
    return max(xs) - min(xs)


def emission_mat(name, color, strength):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    e = nt.nodes.new("ShaderNodeEmission")
    e.inputs["Color"].default_value = (*color, 1)
    e.inputs["Strength"].default_value = strength
    nt.links.new(e.outputs[0], out.inputs["Surface"])
    try:
        m.emission_sampling = "NONE"
    except Exception:
        pass
    return m


# ================================================================ CAMERAS (absolute; framing pass)
def cam_euler(name, loc, rot_deg, lens):
    c = OB[name]
    c.location = loc
    c.rotation_euler = Euler([math.radians(a) for a in rot_deg])
    c.data.lens = lens


def cam_look(name, loc, target, lens):
    c = OB[name]
    c.location = loc
    c.rotation_euler = track(Vector(target) - Vector(loc))
    c.data.lens = lens


YAW = 168.45
# wide shots: the build's view, tilted up a little so the moon sits fully in frame
cam_euler("CAM_Header_Capsule_920x430", (0.567, 2.109, 0.384), (86.85, 0, YAW), 50.0)
cam_euler("CAM_Library_Header_920x430", (0.567, 2.109, 0.384), (86.85, 0, YAW), 50.0)
cam_euler("CAM_Main_Capsule_1232x706", (0.861, 2.586, 0.43), (85.85, 0, YAW), 50.0)
cam_euler("CAM_Small_Capsule_462x174", (0.597, 2.109, 0.384), (84.65, 0, YAW), 50.0)   # tilted down: pumpkin not cut, moon mostly out
cam_euler("CAM_Page_BG_1438x810", (1.119, 3.255, 0.494), (85.85, 0, YAW), 50.0)
cam_euler("CAM_Library_Hero_3840x1240", (0.861, 2.586, 0.43), (87.85, 0, YAW), 35.0)
cam_euler("CAM_Library_Logo_1280x720", (0.677, 2.109, 0.384), (85.85, 0, YAW), 50.0)
# tall shots: closer, nearly level, characters low in the frame, logo high on dark sky
VIEW = Vector((0.2003, 0.9797, 0.0))           # horizontal direction from the set toward the cameras (yaw 168.45)
T_TALL = Vector((-0.002, 0.21, 0.0))
cam_look("CAM_Library_Capsule_600x900", T_TALL + VIEW * 2.05 + Vector((0, 0, 0.62)), T_TALL + Vector((0, 0, 0.57)), 50.0)
cam_look("CAM_Vertical_Capsule_748x896", T_TALL + VIEW * 1.9 + Vector((0, 0, 0.60)), T_TALL + Vector((0, 0, 0.55)), 50.0)

# logo per camera: camera-local (x, y, -1) and its WIDTH in camera units at distance 1 (scale = width / mesh width)
LOGOS = {
    "Header_Capsule_920x430": ((-0.2, 0.0, -1.0), 0.227),
    "Library_Header_920x430": ((-0.2, 0.0, -1.0), 0.227),
    "Main_Capsule_1232x706": ((-0.2, 0.0, -1.0), 0.212),
    "Small_Capsule_462x174": ((-0.165, -0.005, -1.0), 0.258),
    "Vertical_Capsule_748x896": ((0.0, 0.222, -1.0), 0.33),
    "Library_Capsule_600x900": ((0.0, 0.222, -1.0), 0.305),
    "Library_Logo_1280x720": ((0.0, 0.0, -1.0), 0.364),
}
REF_LOGO_W = 0.227              # the width the per-logo light numbers below were tuned at
bpy.context.view_layer.update()

# ================================================================ render / colour management
sc.cycles.volume_step_rate = 4.0
sc.cycles.volume_bounces = 0
sc.cycles.use_light_tree = True
sc.view_settings.view_transform = "AgX"
for lk in ("AgX - Punchy", "Punchy", "AgX - High Contrast"):
    try:
        sc.view_settings.look = lk
        break
    except Exception:
        pass
sc.view_settings.exposure = 0.0

# ================================================================ world: deep blue-teal night; dark bounce light
w = sc.world
nt = w.node_tree
drop_lp_nodes(nt)
grad = next(n for n in nt.nodes if n.type == "VALTORGB")
grad.color_ramp.elements[0].position = 0.0
grad.color_ramp.elements[0].color = (0.012, 0.03, 0.05, 1)
grad.color_ramp.elements[1].position = 0.42
grad.color_ramp.elements[1].color = (0.001, 0.003, 0.009, 1)
next(n for n in nt.nodes if n.type == "MAP_RANGE").inputs["To Max"].default_value = 2.2
bg = next(n for n in nt.nodes if n.type == "BACKGROUND")
out_w = next(n for n in nt.nodes if n.type == "OUTPUT_WORLD")
lp = new_node(nt, "ShaderNodeLightPath", "LP_LightPath")
amb = new_node(nt, "ShaderNodeBackground", "LP_Ambient", Color=(*AMBIENT, 1), Strength=1.0)
mxw = new_node(nt, "ShaderNodeMixShader", "LP_SkyOnlyForCamera")
nt.links.new(lp.outputs["Is Camera Ray"], mxw.inputs["Fac"])
nt.links.new(amb.outputs["Background"], mxw.inputs[1])
nt.links.new(bg.outputs["Background"], mxw.inputs[2])
nt.links.new(mxw.outputs["Shader"], out_w.inputs["Surface"])

# ================================================================ moon + moonlight
moon_m = bpy.data.materials["Steam_Moon"]
cr = next(n for n in moon_m.node_tree.nodes if n.type == "VALTORGB")
cr.color_ramp.elements[0].color = (0.45, 0.55, 0.72, 1)
cr.color_ramp.elements[1].color = (*MOON_COLOR, 1)
next(n for n in moon_m.node_tree.nodes if n.type == "EMISSION").inputs["Strength"].default_value = MOON_STRENGTH
moon_loc = OB["Moon"].matrix_world.translation.copy()

sun_o = OB["Moonlight"]
sun_o.data.color, sun_o.data.energy, sun_o.data.angle = SUN_COLOR, SUN_ENERGY, math.radians(1.2)
set_center = Vector((0.0, 0.2, 0.2))
d_moon = (set_center - moon_loc).normalized()
sun_o.rotation_euler = d_moon.to_track_quat("-Z", "Y").to_euler()
# The far background (Background_v2) brings its own moon lights (BG2_Rim_Moon, BG2_Key_Side, ...): the set's moonlight
# skips it, or the far ground in the wall gap washes out pale. (Its castle and crag still cast shadows.)
bg_coll = bpy.data.collections.get("Background_v2")
# Nor the floor: the old castle hill used to shadow the whole set, and a grazing moon on the glossy floor is a pale wash.
FLOOR = [OB[n] for n in ("Rooftop_Base_Z", "Rooftop_Base_Z_Lid") if n in OB]
if bg_coll:
    sun_o.light_linking.receiver_collection = coll_of("LP_Not_Background", list(bg_coll.all_objects) + FLOOR, "EXCLUDE")

# ================================================================ haze: far (god rays) + thin set haze
pv = next(n for n in bpy.data.materials["Steam_Haze"].node_tree.nodes if n.type == "PRINCIPLED_VOLUME")
pv.inputs["Color"].default_value = (0.5, 0.65, 1.0, 1)
pv.inputs["Density"].default_value = HAZE_DENSITY
pv.inputs["Anisotropy"].default_value = HAZE_ANISO
OB["Haze"].location = (0, -31.5, 6)
OB["Haze"].scale = (140, 57, 30)

sh_m = bpy.data.materials.get("Steam_Set_Haze") or bpy.data.materials.new("Steam_Set_Haze")
sh_m.use_nodes = True
sh_m.node_tree.nodes.clear()
so = sh_m.node_tree.nodes.new("ShaderNodeOutputMaterial")
sv = sh_m.node_tree.nodes.new("ShaderNodeVolumePrincipled")
sv.inputs["Color"].default_value = (0.55, 0.7, 1.0, 1)
sv.inputs["Density"].default_value = SET_HAZE_DENSITY
sv.inputs["Anisotropy"].default_value = 0.65
sh_m.node_tree.links.new(sv.outputs["Volume"], so.inputs["Volume"])
me = bpy.data.meshes.new("LP_Set_Haze")
bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0); bm.to_mesh(me); bm.free()
me.materials.append(sh_m)
set_haze = bpy.data.objects.new("LP_Set_Haze", me)
C_SKY.objects.link(set_haze)
set_haze.location = (0.0, -1.65, 0.75)
set_haze.scale = (6.0, 3.1, 1.5)          # only behind the characters
set_haze.visible_shadow = False
set_haze.visible_glossy = False

# ================================================================ character lights (moonlit)
KNIGHT_HEAD = Vector((-0.27, 0.13, 0.31))
PUMPKIN_C = Vector((0.14, 0.27, 0.2))
set_area(OB["Key_Warm"], (-0.35, 1.35, 0.85), PUMPKIN_C + Vector((0, 0.1, 0)), 0.5, (1.0, 0.66, 0.4), KEY_ENERGY, spread=40)
set_area(OB["Knight_Fill"], (-0.5, 0.95, 0.5), KNIGHT_HEAD, 0.3, (0.72, 0.84, 1.0), FILL_ENERGY, spread=50)
set_area(OB["Rim_Cool"], (0.8, -0.9, 0.75), PUMPKIN_C + Vector((0, 0, 0.08)), 0.45, RIM, 24.0 * RIM_GAIN, spread=70)
rims = [OB["Rim_Cool"],
        new_light("LP_Rim_Knight", "AREA", (-0.95, -0.55, 0.7), RIM, 18.0 * RIM_GAIN, 0.35, KNIGHT_HEAD + Vector((0, 0, -0.05)), 70),
        new_light("LP_Rim_Top", "AREA", (-0.1, -0.75, 1.35), RIM, 16.0 * RIM_GAIN, 0.5, (-0.05, 0.2, 0.25), 60),
        new_light("LP_Rim_Small_L", "AREA", (1.4, -0.2, 0.6), RIM, 14.0 * RIM_GAIN, 0.4, (0.95, 0.75, 0.1), 80),
        new_light("LP_Rim_Small_R", "AREA", (-1.5, -0.3, 0.6), RIM, 14.0 * RIM_GAIN, 0.4, (-0.9, 0.7, 0.1), 80)]
PUMPKIN_MESHES = [o for o in OB if o.type == "MESH" and o.name.startswith(("Hero_Pumpkin_", "Small_Pumpkin_v2_"))
                  and any(c.name in ("Hero_Pumpkin_v2", "V2_Small_Pumpkins") for c in o.users_collection)]
recv = coll_of("LP_Rim_Receivers", [o for o in OB if o.type == "MESH" and o.name.startswith(("Pie_Knight", "Candles_"))]
               + PUMPKIN_MESHES)
for r in rims:
    r.light_linking.receiver_collection = recv
new_light("LP_Cool_Fill", "AREA", (0.3, 1.0, 2.4), (0.35, 0.5, 0.95), COOL_FILL, 1.5, (0.0, 0.1, 0.0), 60)
new_light("LP_Pumpkin_Spill", "POINT", (-0.06, 0.36, 0.2), WARM, 0.55, 0.04)
pg = OB["Pumpkin_Glow"]
pg.data.color, pg.data.energy = WARM, PUMPKIN_GLOW
pg.hide_render = False

# ================================================================ lantern pumpkins (v2: real light from inside, out through the carved holes)
# The pumpkins' own inner lights replace the old fake face lights (LP_Face_Light, LP_Face_Spill, Small_Pumpkin_Glow_*).
# They skip the pumpkin shells as receivers (the shells still block them), so only the carved holes let light out and
# the face is projected onto the floor. The glow you SEE in the holes is the PV2_* materials' own "Glow" value.
no_pumpkins = coll_of("LP_No_Pumpkins", PUMPKIN_MESHES, "EXCLUDE")
for nm in ("Pumpkin_Glow", "LP_Pumpkin_Spill"):      # warm fills light the knight and floor, not the shells
    OB[nm].light_linking.receiver_collection = no_pumpkins
hero = OB["Hero_Pumpkin_Body"]
hc = sum((hero.matrix_world @ Vector(c) for c in hero.bound_box), Vector()) / 8
inner = OB["Hero_Pumpkin_Inner_Light"]
inner.data.energy, inner.data.color, inner.data.shadow_soft_size = HERO_INNER_W, (1.0, 0.45, 0.1), 0.03
inner.light_linking.receiver_collection = no_pumpkins
inner.visible_camera = False
for o in [o for o in OB if o.type == "LIGHT" and o.name.startswith("Small_Pumpkin_v2_") and o.parent is not None
          and "size_k" in o.parent]:
    k_ = o.parent["size_k"]
    o.data.energy, o.data.color, o.data.shadow_soft_size = SMALL_INNER_W * k_ * k_, (1.0, 0.45, 0.1), 0.012
    o.light_linking.receiver_collection = no_pumpkins
    o.visible_camera = False
for m_name, v in (("PV2_Pumpkin_Inner", GLOW_INNER), ("PV2_Pumpkin_Cut_Flesh", GLOW_CUT)):
    m_ = bpy.data.materials.get(m_name)
    g = m_.node_tree.nodes.get("Glow") if m_ else None
    if g:
        g.outputs[0].default_value = v
for o in OB:
    if o.type == "LIGHT" and o.name.startswith(("Candles_L_Light", "Candles_R_Light")):
        o.data.color, o.data.energy = (1.0, 0.55, 0.2), 0.55

# Knight's eyes: the bright lime parts of his texture glow softly (moonlit)
km = bpy.data.materials["Knight_Armour_Sword_Shield"]
drop_lp_nodes(km.node_tree)
kp = principled(km)
kmix = next(n for n in km.node_tree.nodes if n.type == "MIX")
sep = new_node(km.node_tree, "ShaderNodeSeparateColor", "LP_Eyes")
km.node_tree.links.new(kmix.outputs[2], sep.inputs[0])
sub = new_node(km.node_tree, "ShaderNodeMath", "LP_Eyes_Green"); sub.operation = "SUBTRACT"
km.node_tree.links.new(sep.outputs["Green"], sub.inputs[0]); km.node_tree.links.new(sep.outputs["Red"], sub.inputs[1])
rng = new_node(km.node_tree, "ShaderNodeMapRange", "LP_Eyes_Range", **{"From Min": 0.35, "From Max": 0.6, "To Max": 3.0})
km.node_tree.links.new(sub.outputs[0], rng.inputs["Value"])
km.node_tree.links.new(kmix.outputs[2], kp.inputs["Emission Color"])
km.node_tree.links.new(rng.outputs["Result"], kp.inputs["Emission Strength"])

flame = principled(bpy.data.materials["Steam_Candle_Flame"])
flame.inputs["Emission Strength"].default_value = 70
flame.inputs["Emission Color"].default_value = (1.0, 0.55, 0.15, 1)
principled(bpy.data.materials["Steam_Castle_Window"]).inputs["Emission Strength"].default_value = 45
principled(bpy.data.materials["Rooftop_Base_Lid"]).inputs["Roughness"].default_value = 0.5

# ================================================================ god-ray gobo + moon shafts (moonlit, trimmed)
gobo_m = bpy.data.materials.get("Steam_Moon_Gobo") or bpy.data.materials.new("Steam_Moon_Gobo")
gobo_m.use_nodes = True
gt = gobo_m.node_tree
gt.nodes.clear()
L2 = gt.links.new
g_out = gt.nodes.new("ShaderNodeOutputMaterial")
g_tc = gt.nodes.new("ShaderNodeTexCoord")
g_mp = gt.nodes.new("ShaderNodeMapping"); g_mp.inputs["Scale"].default_value = (1.0, 2.2, 1.0)
g_noise = gt.nodes.new("ShaderNodeTexNoise")
g_noise.inputs["Scale"].default_value, g_noise.inputs["Detail"].default_value, g_noise.inputs["Roughness"].default_value = GOBO_SCALE, 4.0, 0.6
g_ramp = gt.nodes.new("ShaderNodeValToRGB")
g_ramp.color_ramp.elements[0].position, g_ramp.color_ramp.elements[0].color = 0.46, (0, 0, 0, 1)
g_ramp.color_ramp.elements[1].position, g_ramp.color_ramp.elements[1].color = 0.54, (1, 1, 1, 1)
g_tr = gt.nodes.new("ShaderNodeBsdfTransparent")
g_df = gt.nodes.new("ShaderNodeBsdfDiffuse"); g_df.inputs["Color"].default_value = (0, 0, 0, 1)
g_mx = gt.nodes.new("ShaderNodeMixShader")
L2(g_tc.outputs["Generated"], g_mp.inputs["Vector"]); L2(g_mp.outputs["Vector"], g_noise.inputs["Vector"])
L2(g_noise.outputs["Fac"], g_ramp.inputs["Factor"])
g_oc = gt.nodes.new("ShaderNodeVectorMath"); g_oc.operation = "MULTIPLY"; g_oc.inputs[1].default_value = (14.0, 9.0, 0.0)
L2(g_tc.outputs["Object"], g_oc.inputs[0])
g_len = gt.nodes.new("ShaderNodeVectorMath"); g_len.operation = "LENGTH"; L2(g_oc.outputs["Vector"], g_len.inputs[0])
g_hole = gt.nodes.new("ShaderNodeMapRange")
g_hole.inputs["From Min"].default_value, g_hole.inputs["From Max"].default_value = GOBO_HOLE, GOBO_HOLE * 0.6
L2(g_len.outputs["Value"], g_hole.inputs["Value"])
g_max = gt.nodes.new("ShaderNodeMath"); g_max.operation = "MAXIMUM"
L2(g_ramp.outputs["Color"], g_max.inputs[0]); L2(g_hole.outputs["Result"], g_max.inputs[1])
L2(g_max.outputs[0], g_mx.inputs["Fac"]); L2(g_df.outputs[0], g_mx.inputs[1]); L2(g_tr.outputs[0], g_mx.inputs[2])
L2(g_mx.outputs[0], g_out.inputs["Surface"])
gobo = mesh_obj("LP_Moon_Gobo", [(-1, -1, 0), (1, -1, 0), (1, 1, 0), (-1, 1, 0)], [(0, 1, 2, 3)], gobo_m, C_SKY)
gobo.location = moon_loc + (set_center - moon_loc) * 0.12
gobo.rotation_euler = (-d_moon).to_track_quat("Z", "Y").to_euler()
gobo.scale = (14, 9, 1)
for attr in ("visible_camera", "visible_diffuse", "visible_glossy", "visible_transmission", "visible_volume_scatter"):
    setattr(gobo, attr, False)
gobo.visible_shadow = True

beam_m = bpy.data.materials.get("Steam_Moon_Beam") or bpy.data.materials.new("Steam_Moon_Beam")
beam_m.use_nodes = True
bt = beam_m.node_tree
bt.nodes.clear()
BL = bt.links.new
b_out = bt.nodes.new("ShaderNodeOutputMaterial")
b_uv = bt.nodes.new("ShaderNodeUVMap")
b_sep = bt.nodes.new("ShaderNodeSeparateXYZ"); BL(b_uv.outputs["UV"], b_sep.inputs[0])
def math_node(op, a=None, b=None):
    n = bt.nodes.new("ShaderNodeMath"); n.operation = op
    for i, v in enumerate((a, b)):
        if isinstance(v, (int, float)):
            n.inputs[i].default_value = v
        elif v is not None:
            BL(v, n.inputs[i])
    return n
b_along = math_node("SUBTRACT", 1.0, b_sep.outputs["X"])
b_ap = math_node("POWER", b_along.outputs[0], 1.6)
b_c = bt.nodes.new("ShaderNodeMath"); b_c.operation = "MULTIPLY_ADD"
b_c.inputs[1].default_value, b_c.inputs[2].default_value = 2.0, -1.0
BL(b_sep.outputs["Y"], b_c.inputs[0])
b_abs = math_node("ABSOLUTE", b_c.outputs[0])
b_inv = math_node("SUBTRACT", 1.0, b_abs.outputs[0])
b_sq = math_node("POWER", b_inv.outputs[0], 2.2)
b_m = math_node("MULTIPLY", b_ap.outputs[0], b_sq.outputs[0])
b_attr = bt.nodes.new("ShaderNodeAttribute"); b_attr.attribute_type = "OBJECT"; b_attr.attribute_name = "beam"
b_s = math_node("MULTIPLY", b_m.outputs[0], b_attr.outputs["Fac"])
b_em = bt.nodes.new("ShaderNodeEmission"); b_em.inputs["Color"].default_value = (0.62, 0.78, 1.0, 1)
BL(b_s.outputs[0], b_em.inputs["Strength"])
b_tr = bt.nodes.new("ShaderNodeBsdfTransparent")
b_add = bt.nodes.new("ShaderNodeAddShader")
BL(b_em.outputs[0], b_add.inputs[0]); BL(b_tr.outputs[0], b_add.inputs[1]); BL(b_add.outputs[0], b_out.inputs["Surface"])
face_dir = (Vector((0.75, 2.6, 0.45)) - moon_loc).normalized()
qb = face_dir.to_track_quat("Z", "Y")
rnd = random.Random(4290)
BEAMS = [(-8, 22, 3.0, 0.35), (-28, 26, 4.0, 0.7), (-152, 26, 4.0, 0.7), (-172, 22, 3.0, 0.35), (-62, 30, 4.5, 1.0),
         (-78, 34, 3.0, 0.8), (-95, 32, 5.5, 1.0), (-112, 30, 3.5, 0.75), (-128, 28, 5.0, 0.9), (-146, 24, 3.0, 0.6),
         (-40, 26, 3.5, 0.7), (-20, 22, 2.5, 0.5), (-165, 20, 2.5, 0.5), (10, 14, 2.0, 0.35), (170, 14, 2.0, 0.35),
         (60, 12, 2.0, 0.3), (115, 12, 2.0, 0.3)]
for i, (ang, length, wid, st) in enumerate(BEAMS):
    a = math.radians(ang + rnd.uniform(-3, 3))
    p = lambda r, s_: (r * math.cos(a) - s_ * math.sin(a), r * math.sin(a) + s_ * math.cos(a), 0.0)
    b = mesh_obj(f"LP_Moon_Beam_{i}", [p(2.0, -0.5), p(length, -wid / 2), p(length, wid / 2), p(2.0, 0.5)], [(0, 1, 2, 3)],
                 beam_m, C_SKY, uvs=[(0, 0), (1, 0), (1, 1), (0, 1)])
    b.matrix_world = Matrix.Translation(moon_loc + face_dir * (0.4 + 0.01 * i)) @ qb.to_matrix().to_4x4()
    b["beam"] = st * BEAM_STRENGTH
    for attr in ("visible_shadow", "visible_diffuse", "visible_glossy", "visible_transmission", "visible_volume_scatter"):
        setattr(b, attr, False)

# ================================================================ far background: thinner shader haze on the near ground
# Background_v2 fakes aerial haze in its shaders (blend toward a haze emission by distance, brighter toward the moon).
# Right under the moon that turns the ground seen through the wall gap into a pale flat veil; scale that emission down
# on the near-ground materials only (the mountains and castle keep their full depth haze).
def haze_gain(mat_name, gain):
    m_ = bpy.data.materials.get(mat_name)
    if m_ is None:
        return
    nt_ = m_.node_tree
    g_ = nt_.nodes.get("LP_Haze_Gain")
    if g_ is None:
        for mx_ in [n for n in nt_.nodes if n.type == "MIX_SHADER"]:
            src = [l.from_node for l in nt_.links if l.to_socket == mx_.inputs[2]]
            if (src and src[0].type == "EMISSION" and src[0].inputs["Strength"].is_linked and not src[0].inputs["Color"].is_linked
                    and max(src[0].inputs["Color"].default_value[:3]) < 0.08):          # the dark-blue haze colour
                em_ = src[0]
                lk = em_.inputs["Strength"].links[0]
                g_ = nt_.nodes.new("ShaderNodeMath"); g_.name = g_.label = "LP_Haze_Gain"; g_.operation = "MULTIPLY"
                nt_.links.new(lk.from_socket, g_.inputs[0])
                nt_.links.new(g_.outputs[0], em_.inputs["Strength"])
                print("HAZE GAIN on", mat_name)
                break
    if g_ is not None:
        g_.inputs[1].default_value = gain
for mn_ in HAZE_NEAR_MATS:
    haze_gain(mn_, HAZE_NEAR_GAIN)
# ...and a darker ground colour there, so the moonlit field behind the wall reads as dark ground, not pale snow
def albedo_gain(mat_name, gain):
    m_ = bpy.data.materials.get(mat_name)
    if m_ is None:
        return
    nt_ = m_.node_tree
    p_ = next((n for n in nt_.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if p_ is None:
        return
    g_ = nt_.nodes.get("LP_Albedo_Gain")
    if g_ is None:
        g_ = nt_.nodes.new("ShaderNodeMix"); g_.name = g_.label = "LP_Albedo_Gain"
        g_.data_type, g_.blend_type = "RGBA", "MULTIPLY"
        g_.inputs["Factor"].default_value = 1.0
        bc = p_.inputs["Base Color"]
        if bc.is_linked:
            nt_.links.new(bc.links[0].from_socket, sock(g_.inputs, "A"))
        else:
            sock(g_.inputs, "A").default_value = bc.default_value[:]
        nt_.links.new(sock(g_.outputs, "Result"), bc)
    sock(g_.inputs, "B").default_value = (gain, gain, gain, 1.0)
for mn_ in ("BG2_Ground", "BG2_Path"):
    albedo_gain(mn_, GROUND_ALBEDO_GAIN)


# ================================================================ back pillars: faint green echo (magic, trimmed)
for nm, x in (("LP_Pillar_Glow_L", 0.9), ("LP_Pillar_Glow_R", -0.9)):
    new_light(nm, "POINT", (x, -1.85, 0.15), GREEN, PILLAR_GLOW_W, 0.08)

# ================================================================ logo (v2): green halo shell around its silhouette, reflector cards
# Materials are the asset's own (Logo_Gold_v2, Logo_Green_v2, Logo_Glow_v2); assemble_v2.py swaps the mesh in.
halo_m = emission_mat("Steam_Logo_Halo_Shell", (0.04, 1.0, 0.1), HALO_STRENGTH)
hn = halo_m.node_tree
h_em = next(n for n in hn.nodes if n.type == "EMISSION")
h_out = next(n for n in hn.nodes if n.type == "OUTPUT_MATERIAL")
h_lw = hn.nodes.new("ShaderNodeLayerWeight"); h_lw.inputs["Blend"].default_value = 0.5
h_iv = hn.nodes.new("ShaderNodeMath"); h_iv.operation = "SUBTRACT"; h_iv.inputs[0].default_value = 1.0
h_pw = hn.nodes.new("ShaderNodeMath"); h_pw.operation = "POWER"; h_pw.inputs[1].default_value = 1.5
h_tr = hn.nodes.new("ShaderNodeBsdfTransparent")
h_mx = hn.nodes.new("ShaderNodeMixShader")
hn.links.new(h_lw.outputs["Facing"], h_iv.inputs[1]); hn.links.new(h_iv.outputs[0], h_pw.inputs[0])
hn.links.new(h_pw.outputs[0], h_mx.inputs["Fac"]); hn.links.new(h_tr.outputs[0], h_mx.inputs[1])
hn.links.new(h_em.outputs[0], h_mx.inputs[2]); hn.links.new(h_mx.outputs[0], h_out.inputs["Surface"])
halo_me = bpy.data.meshes.get("Logo_429_v2_Halo_Mesh")          # the back plate alone = the logo's outer silhouette
refl_tpl = [OB[n] for n in LOGO_REFLECTORS if n in OB]
R90 = Euler((math.radians(-90), 0, 0)).to_matrix().to_4x4()

for shot, (loc, width) in LOGOS.items():
    cam = OB["CAM_" + shot]
    lg = OB["LOGO_" + shot]
    s = width / mesh_width(lg.data)
    k = width / REF_LOGO_W
    L = Vector(loc)
    lg.parent = cam
    lg.location, lg.rotation_euler, lg.scale = L, Euler((math.radians(-90), 0, 0)), (s, s, s)
    key = OB[lg.name + "_Key"]
    key.data.size, key.data.color, key.data.energy = 0.25 * k, (1.0, 0.82, 0.58), 2.2 * k * k * 1.15 * LOGO_KEY_GAIN
    key.location = L + Vector((-0.12 * k, 0.22 * k, 0.32))
    key.rotation_euler = Euler((math.radians(-35), math.radians(-12), 0))
    key.visible_camera = False
    gl = OB[lg.name + "_Light"]
    gl.data.color, gl.data.energy, gl.data.shadow_soft_size = (0.2, 1.0, 0.35), 2.25 * k, 0.1
    gl.location = L + Vector((0, 0, -0.12 * k))
    gl.visible_camera = False
    rim_loc = L + Vector((-0.05 * k, 0.22 * k, -0.35 * k))
    rim = new_light("LP_" + lg.name + "_Rim", "AREA", rim_loc, RIM, 9.0 * k * k, 0.3 * k, None, 80, coll=C_LOGO, parent=cam)
    rim.rotation_euler = track(L + Vector((0, 0.02 * k, 0)) - rim_loc)
    rc = coll_of("LP_Recv_" + lg.name, [lg])
    for lo in (key, gl, rim):
        lo.light_linking.receiver_collection = rc
    # green halo: a slightly fattened copy of the silhouette just behind it (render_all hides *_Halo on the transparent logo)
    h = bpy.data.objects.new("LP_" + lg.name + "_Halo", halo_me or lg.data)
    C_LOGO.objects.link(h)
    h.parent = cam
    h.location, h.rotation_euler, h.scale = L + Vector((0, 0, -0.012)), lg.rotation_euler.copy(), (s * HALO_GROW,) * 3
    dm = h.modifiers.new("Halo", "DISPLACE")
    dm.mid_level, dm.strength = 0.0, HALO_PUFF
    for slot in h.material_slots:
        slot.link = "OBJECT"
        slot.material = halo_m
    h.visible_shadow = h.visible_glossy = h.visible_diffuse = False
    # gold needs something bright to reflect: the asset's own cards (only glossy rays see them), carried with each logo
    for tpl in refl_tpl:
        rf = tpl.copy()
        rf.name = "LP_" + lg.name + "_" + tpl.name.replace("Logo_429_", "")
        C_LOGO.objects.link(rf)
        rf.parent = cam
        rf.matrix_parent_inverse = Matrix.Identity(4)
        rf.matrix_basis = Matrix.Translation(L) @ R90 @ Matrix.Scale(s, 4) @ tpl.matrix_world
        for attr in ("visible_camera", "visible_diffuse", "visible_shadow", "visible_transmission", "visible_volume_scatter"):
            setattr(rf, attr, False)
        rf.visible_glossy = True

# ================================================================ compositor: bloom -> teal/orange CDL -> vignette (keeps alpha)
tree = sc.compositing_node_group
tree.nodes.clear()
if not any(getattr(s_, "in_out", None) == "OUTPUT" for s_ in tree.interface.items_tree):
    tree.interface.new_socket("Image", in_out="OUTPUT", socket_type="NodeSocketColor")
N, Lk = tree.nodes.new, tree.links.new
rl = N("CompositorNodeRLayers")
bloom = N("CompositorNodeGlare")
for nm, v in (("Type", "Bloom"), ("Quality", "High"), ("Threshold", 0.85), ("Strength", 0.55), ("Size", 0.72)):
    bloom.inputs[nm].default_value = v
Lk(rl.outputs["Image"], bloom.inputs["Image"])
cb = N("CompositorNodeColorBalance")
cb.inputs["Type"].default_value = "Offset/Power/Slope (ASC-CDL)"
for i in cb.inputs:
    if i.bl_idname == "NodeSocketColor":
        if i.name == "Offset":
            i.default_value = (-0.0015, 0.0012, 0.0045, 1)
        elif i.name == "Power":
            i.default_value = (1.02, 1.0, 0.95, 1)
        elif i.name == "Slope":
            i.default_value = (1.06, 1.0, 0.93, 1)
Lk(bloom.outputs["Image"], cb.inputs["Image"])
hs = N("CompositorNodeHueSat")
hs.inputs["Saturation"].default_value = 1.12
Lk(cb.outputs["Image"], hs.inputs["Image"])
ic = N("CompositorNodeImageCoordinates")
Lk(rl.outputs["Image"], ic.inputs["Image"])
vm = N("ShaderNodeVectorMath"); vm.operation = "SUBTRACT"; vm.inputs[1].default_value = (0.5, 0.56, 0.0)
Lk(ic.outputs["Normalized"], vm.inputs[0])
vl = N("ShaderNodeVectorMath"); vl.operation = "LENGTH"
Lk(vm.outputs["Vector"], vl.inputs[0])
vr = N("ShaderNodeMapRange"); vr.interpolation_type = "SMOOTHSTEP"
for nm, v in (("From Min", 0.32), ("From Max", 0.78), ("To Min", 0.0), ("To Max", 0.72)):
    vr.inputs[nm].default_value = v
Lk(vl.outputs["Value"], vr.inputs["Value"])
vig = N("ShaderNodeMix"); vig.data_type = "RGBA"; vig.blend_type = "MIX"
vig.inputs[7].default_value = (0.0, 0.004, 0.008, 1)
Lk(vr.outputs["Result"], vig.inputs["Factor"])
Lk(hs.outputs["Image"], vig.inputs[6])
go = N("NodeGroupOutput")
sa = N("CompositorNodeSetAlpha")
if "Type" in sa.inputs:
    sa.inputs["Type"].default_value = "Replace Alpha"
Lk(vig.outputs[2], sa.inputs["Image"])
Lk(rl.outputs["Alpha"], sa.inputs["Alpha"])
Lk(sa.outputs["Image"], go.inputs[0])

sc.camera = OB["CAM_Header_Capsule_920x430"]
sc["lighting_pass"] = 1
bpy.ops.wm.save_as_mainfile(filepath=SRC, copy=False)
print("SAVED", SRC)
