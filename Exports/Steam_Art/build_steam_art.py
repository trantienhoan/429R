"""Builds Exports/Steam_Art/Steam_Art.blend from the user's Knight_429.blend (opened read-only, saved under a NEW name):
night sky (stars, moon, bats, castle, mountains, haze), jack-o'-lanterns, candles, a 3D "429" logo, lights, and one
camera per Steam image (each with its own logo, parented to it). Run headless:
  blender -b Assets/Blender/Knight_429.blend --python build_steam_art.py
"""
import bpy, bmesh, math, os, random
from mathutils import Vector, Euler, Matrix

OUT_DIR = r"C:/Github_Repository/429R/Exports/Steam_Art"
OUT_BLEND = os.path.join(OUT_DIR, "Steam_Art.blend")
os.makedirs(OUT_DIR, exist_ok=True)
random.seed(429)

sc = bpy.context.scene
bpy.ops.file.make_paths_absolute()          # textures still found after saving elsewhere

def coll(name):
    c = bpy.data.collections.get(name)
    if c is None:
        c = bpy.data.collections.new(name)
        sc.collection.children.link(c)
    return c

C_SKY, C_SET, C_LIGHT, C_LOGO, C_CAM = coll("Sky"), coll("Set Dressing"), coll("Lights"), coll("Logos"), coll("Cameras")

def link(obj, c):
    for old in list(obj.users_collection):
        old.objects.unlink(obj)
    c.objects.link(obj)
    return obj

def mat(name, color=(0.5, 0.5, 0.5), rough=0.6, metal=0.0, emit=None, strength=0.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    p = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    p.inputs["Base Color"].default_value = (*color, 1)
    p.inputs["Roughness"].default_value = rough
    p.inputs["Metallic"].default_value = metal
    if emit is not None:
        p.inputs["Emission Color"].default_value = (*emit, 1)
        p.inputs["Emission Strength"].default_value = strength
    return m

def obj_from_mesh(name, me, c, material=None):
    o = bpy.data.objects.new(name, me)
    c.objects.link(o)
    if material:
        o.data.materials.append(material)
    return o

def prim(kind, name, c, material=None, **kw):
    getattr(bpy.ops.mesh, "primitive_" + kind + "_add")(**kw)
    o = bpy.context.active_object
    o.name = name
    link(o, c)
    if material:
        o.data.materials.clear()
        o.data.materials.append(material)
    return o

def point(name, loc, color, power, radius=0.02):
    ld = bpy.data.lights.new(name, "POINT")
    ld.color, ld.energy, ld.shadow_soft_size = color, power, radius
    o = bpy.data.objects.new(name, ld)
    C_LIGHT.objects.link(o)
    o.location = loc
    return o

# ------------------------------------------------------------------ render settings
sc.render.engine = "CYCLES"
try:
    prefs = bpy.context.preferences.addons["cycles"].preferences
    for t in ("OPTIX", "CUDA", "HIP", "ONEAPI"):
        try:
            prefs.compute_device_type = t
            prefs.get_devices()
            if any(d.type == t for d in prefs.devices):
                for d in prefs.devices: d.use = True
                sc.cycles.device = "GPU"
                break
        except Exception:
            pass
except Exception as e:
    print("GPU setup:", e)
sc.cycles.samples = 160
sc.cycles.use_denoising = True
sc.cycles.max_bounces = 6
sc.cycles.volume_step_rate = 4.0
sc.view_settings.view_transform = "AgX"
sc.view_settings.look = "AgX - Punchy" if "AgX - Punchy" in [i.identifier for i in bpy.types.ColorManagedViewSettings.bl_rna.properties["look"].enum_items] else sc.view_settings.look
sc.render.film_transparent = False

# ------------------------------------------------------------------ world: night sky with stars
w = sc.world
w.use_nodes = True
nt = w.node_tree
nt.nodes.clear()
out = nt.nodes.new("ShaderNodeOutputWorld")
bg = nt.nodes.new("ShaderNodeBackground")
tc = nt.nodes.new("ShaderNodeTexCoord")
sep = nt.nodes.new("ShaderNodeSeparateXYZ")
nt.links.new(tc.outputs["Generated"], sep.inputs[0])
grad = nt.nodes.new("ShaderNodeValToRGB")             # horizon purple -> zenith navy
grad.color_ramp.elements[0].position = 0.0
grad.color_ramp.elements[0].color = (0.055, 0.03, 0.07, 1)
grad.color_ramp.elements[1].position = 0.45
grad.color_ramp.elements[1].color = (0.004, 0.006, 0.02, 1)
nt.links.new(sep.outputs["Z"], grad.inputs["Fac"])
vor = nt.nodes.new("ShaderNodeTexVoronoi")             # stars
vor.inputs["Scale"].default_value = 260
nt.links.new(tc.outputs["Generated"], vor.inputs["Vector"])
star = nt.nodes.new("ShaderNodeMapRange")
star.inputs["From Min"].default_value = 0.035
star.inputs["From Max"].default_value = 0.0
star.inputs["To Min"].default_value = 0.0
star.inputs["To Max"].default_value = 3.0
nt.links.new(vor.outputs["Distance"], star.inputs["Value"])
mix = nt.nodes.new("ShaderNodeMix")
mix.data_type = "RGBA"
mix.blend_type = "ADD"
mix.inputs["Factor"].default_value = 1.0
nt.links.new(grad.outputs["Color"], mix.inputs["A"])
nt.links.new(star.outputs["Result"], mix.inputs["B"])
nt.links.new(mix.outputs["Result"], bg.inputs["Color"])
bg.inputs["Strength"].default_value = 1.0
nt.links.new(bg.outputs["Background"], out.inputs["Surface"])

# ------------------------------------------------------------------ the scene's own pieces
knight_arm = bpy.data.objects["Pie_Knight_Armature"]
pumpkin = bpy.data.objects["pumpkin_0"]

# Jack-o'-lantern glow: a copy of the pumpkin material that lights its bright yellow face parts.
src = pumpkin.data.materials[0]
pmat = src.copy()
pmat.name = "Steam_Jack_O_Lantern"
pn = pmat.node_tree.nodes
prin = next(n for n in pn if n.type == "BSDF_PRINCIPLED")
albedo = None
for l in pmat.node_tree.links:
    if l.to_node == prin and l.to_socket.name == "Base Color":
        albedo = l.from_node
if albedo is not None:
    sp = pn.new("ShaderNodeSeparateColor")
    pmat.node_tree.links.new(albedo.outputs[0], sp.inputs[0])
    ramp = pn.new("ShaderNodeMapRange")
    ramp.inputs["From Min"].default_value = 0.55
    ramp.inputs["From Max"].default_value = 0.8
    ramp.inputs["To Min"].default_value = 0.0
    ramp.inputs["To Max"].default_value = 6.0
    pmat.node_tree.links.new(sp.outputs[1], ramp.inputs["Value"])
    pmat.node_tree.links.new(albedo.outputs[0], prin.inputs["Emission Color"])
    pmat.node_tree.links.new(ramp.outputs["Result"], prin.inputs["Emission Strength"])
pumpkin.data = pumpkin.data.copy()
pumpkin.data.materials.clear()
pumpkin.data.materials.append(pmat)
pumpkin.name = "Hero_Pumpkin"
point("Pumpkin_Glow", pumpkin.location + Vector((0.3, 0.3, 0.12)), (1.0, 0.55, 0.15), 2.2, 0.06)

# Small jack-o'-lanterns around the ring (left and right foreground and the back).
small_spots = [(0.95, 0.75, 0.32, 0.6), (1.2, 0.35, 0.28, -0.4), (0.8, 1.05, 0.22, 0.9), (-0.85, 0.7, 0.3, -0.7),
               (-1.15, 0.25, 0.27, 0.5), (-0.7, 1.0, 0.2, -1.1), (0.55, -0.9, 0.3, 0.2), (-0.5, -1.0, 0.28, -0.3)]
for i, (x, y, s, yaw) in enumerate(small_spots):
    p = pumpkin.copy()
    p.name = f"Small_Pumpkin_{i}"
    C_SET.objects.link(p)
    p.location = Vector((x, y, 0.0))
    p.scale = Vector((s, s, s)) * 0.828
    p.rotation_euler = Euler((0, 0, -3.55 + yaw))
    point(f"Small_Pumpkin_Glow_{i}", (x, y + 0.12, 0.12 * s / 0.3), (1.0, 0.5, 0.12), 0.8 * s / 0.3, 0.03)

# ------------------------------------------------------------------ candles on iron stands by the pillars
wax = mat("Steam_Candle_Wax", (0.85, 0.78, 0.62), rough=0.45)
flame = mat("Steam_Candle_Flame", (1, 0.6, 0.2), emit=(1.0, 0.62, 0.18), strength=40)
iron = mat("Steam_Iron", (0.03, 0.03, 0.035), rough=0.35, metal=0.9)
def candelabra(name, base):
    base = Vector(base)
    stem = prim("cylinder", name + "_Stem", C_SET, iron, vertices=12, radius=0.008, depth=0.32, location=base + Vector((0, 0, 0.16)))
    foot = prim("cylinder", name + "_Foot", C_SET, iron, vertices=16, radius=0.05, depth=0.015, location=base + Vector((0, 0, 0.008)))
    bar = prim("cylinder", name + "_Bar", C_SET, iron, vertices=10, radius=0.005, depth=0.16, location=base + Vector((0, 0, 0.3)),
               rotation=(0, math.radians(90), 0))
    for k, dx in enumerate((-0.08, 0.0, 0.08)):
        h = 0.06 + 0.02 * (k == 1)
        z0 = 0.3 + (0.03 if k == 1 else 0.0)
        cup = prim("cylinder", f"{name}_Cup{k}", C_SET, iron, vertices=12, radius=0.016, depth=0.01, location=base + Vector((dx, 0, z0)))
        c = prim("cylinder", f"{name}_Candle{k}", C_SET, wax, vertices=12, radius=0.011, depth=h, location=base + Vector((dx, 0, z0 + h / 2)))
        f = prim("uv_sphere", f"{name}_Flame{k}", C_SET, flame, segments=10, ring_count=6, radius=0.007,
                 location=base + Vector((dx, 0, z0 + h + 0.009)))
        f.scale = (1, 1, 2.0)
        point(f"{name}_Light{k}", base + Vector((dx, 0, z0 + h + 0.02)), (1.0, 0.62, 0.25), 0.25, 0.005)
candelabra("Candles_L", (1.35, -0.55, 0.0))
candelabra("Candles_R", (-1.4, -0.5, 0.0))

# ------------------------------------------------------------------ moon, bats, castle, mountains
moon_m = bpy.data.materials.new("Steam_Moon")
moon_m.use_nodes = True
mn = moon_m.node_tree.nodes
mn.clear()
mo = mn.new("ShaderNodeOutputMaterial")
me_ = mn.new("ShaderNodeEmission")
noi = mn.new("ShaderNodeTexNoise")
noi.inputs["Scale"].default_value = 3.5
noi.inputs["Detail"].default_value = 8
cr = mn.new("ShaderNodeValToRGB")
cr.color_ramp.elements[0].position = 0.35
cr.color_ramp.elements[0].color = (0.55, 0.42, 0.28, 1)
cr.color_ramp.elements[1].position = 0.7
cr.color_ramp.elements[1].color = (1.0, 0.86, 0.62, 1)
moon_m.node_tree.links.new(noi.outputs["Fac"], cr.inputs["Fac"])
moon_m.node_tree.links.new(cr.outputs["Color"], me_.inputs["Color"])
me_.inputs["Strength"].default_value = 4.0
moon_m.node_tree.links.new(me_.outputs["Emission"], mo.inputs["Surface"])
moon = prim("uv_sphere", "Moon", C_SKY, moon_m, segments=48, ring_count=24, radius=2.2, location=(10.5, -40.0, 4.3))
sun = bpy.data.lights.new("Moonlight", "SUN")
sun.color, sun.energy, sun.angle = (0.62, 0.72, 1.0), 0.45, math.radians(2)
sun_o = bpy.data.objects.new("Moonlight", sun)
C_LIGHT.objects.link(sun_o)
sun_o.rotation_euler = Euler((math.radians(-62), math.radians(18), math.radians(-160)))   # from behind, upper left

bat_m = mat("Steam_Bat", (0.005, 0.005, 0.008), rough=0.9)
def bat(name, loc, size, yaw, flap):
    bm = bmesh.new()
    pts = [(0, 0.15), (0.12, 0.35), (0.35, 0.42), (0.6, 0.28), (0.95, 0.4), (0.82, 0.05), (0.62, -0.05), (0.45, 0.08),
           (0.3, -0.02), (0.15, 0.06), (0, -0.12)]
    left = [(-x, y) for (x, y) in reversed(pts[1:-1])]
    poly = pts + left
    vs = [bm.verts.new((x, 0, y)) for (x, y) in poly]
    bm.faces.new(vs)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = obj_from_mesh(name, me, C_SKY, bat_m)
    o.location = loc
    o.scale = (size, size, size * flap)
    o.rotation_euler = Euler((math.radians(random.uniform(-15, 15)), 0, yaw))
    return o
for i in range(14):
    a = random.uniform(0, math.tau)
    r = random.uniform(1.5, 7.0)
    bat(f"Bat_{i}", (10.5 + math.cos(a) * r * 0.9, -34.0 + random.uniform(-4, 4), 4.6 + math.sin(a) * r * 0.45),
        random.uniform(0.35, 0.8), math.radians(random.uniform(150, 210)), random.uniform(0.6, 1.0))

stone = mat("Steam_Castle_Stone", (0.018, 0.018, 0.025), rough=0.85)
roof = mat("Steam_Castle_Roof", (0.012, 0.01, 0.015), rough=0.7)
window = mat("Steam_Castle_Window", (1, 0.6, 0.2), emit=(1.0, 0.55, 0.15), strength=25)
def castle(origin):
    o = Vector(origin)
    towers = [(0, 0, 2.2, 9.0), (-2.6, 0.8, 1.2, 6.5), (2.4, 0.6, 1.3, 7.0), (-4.6, 1.8, 0.9, 5.0), (4.3, 1.5, 0.95, 5.5),
              (-1.2, -1.6, 0.8, 7.8), (1.3, -1.5, 0.75, 8.6), (0, 2.6, 1.0, 4.5)]
    for i, (x, y, r, h) in enumerate(towers):
        t = prim("cylinder", f"Castle_Tower_{i}", C_SKY, stone, vertices=16, radius=r * 0.5, depth=h, location=o + Vector((x, y, h / 2)))
        cone = prim("cone", f"Castle_Roof_{i}", C_SKY, roof, vertices=16, radius1=r * 0.62, depth=r * 2.4,
                    location=o + Vector((x, y, h + r * 1.2)))
        for k in range(3):
            if random.random() < 0.75:
                ang = math.radians(random.uniform(150, 210))
                z = h * random.uniform(0.35, 0.85)
                wpos = o + Vector((x + math.sin(ang) * r * 0.51, y + math.cos(ang) * r * 0.51, z))
                win = prim("plane", f"Castle_Window_{i}_{k}", C_SKY, window, size=r * 0.22, location=wpos)
                win.rotation_euler = Euler((math.radians(90), 0, -ang + math.pi))
                win.scale = (0.6, 1.4, 1)
    keep = prim("cube", "Castle_Keep", C_SKY, stone, size=1, location=o + Vector((0, 0.8, 2.5)))
    keep.scale = (7.5, 3.5, 5.0)
    hill = prim("uv_sphere", "Castle_Hill", C_SKY, stone, segments=32, ring_count=16, radius=1, location=o + Vector((0, 1, -5.5)))
    hill.scale = (12, 8, 7)
castle((0.0, 0.0, 0.0))
castle_root = bpy.data.objects.new("Castle", None)
C_SKY.objects.link(castle_root)
for o in list(C_SKY.objects):
    if o.name.startswith("Castle_"):
        o.parent = castle_root
castle_root.location = (-8.5, -36.0, -0.6)
castle_root.scale = (0.62, 0.62, 0.62)

mount_m = mat("Steam_Mountains", (0.004, 0.005, 0.01), rough=0.95)
for i in range(11):
    x = -40 + i * 8 + random.uniform(-3, 3)
    h = random.uniform(7, 14)
    m_ = prim("cone", f"Mountain_{i}", C_SKY, mount_m, vertices=6, radius1=random.uniform(7, 11), depth=h,
              location=(x, -55 + random.uniform(-5, 5), h / 2 - 1.5))
    m_.rotation_euler = Euler((0, 0, random.uniform(0, 1)))

ground_m = mat("Steam_Far_Ground", (0.02, 0.018, 0.016), rough=0.95)
ground = prim("plane", "Far_Ground", C_SET, ground_m, size=160, location=(0, -30, -0.06))

# Haze between the set and the sky (gives the far things depth).
haze_m = bpy.data.materials.new("Steam_Haze")
haze_m.use_nodes = True
hn = haze_m.node_tree.nodes
hn.clear()
ho = hn.new("ShaderNodeOutputMaterial")
pv = hn.new("ShaderNodeVolumePrincipled")
pv.inputs["Color"].default_value = (0.18, 0.2, 0.32, 1)
pv.inputs["Density"].default_value = 0.005
haze_m.node_tree.links.new(pv.outputs["Volume"], ho.inputs["Volume"])
haze = prim("cube", "Haze", C_SKY, haze_m, size=1, location=(0, -32, 6))
haze.scale = (140, 56, 30)
haze.visible_shadow = False

# Lights for the characters: a warm key from the front right, a cool rim from the moon side.
def area(name, loc, rot, size, color, power):
    ld = bpy.data.lights.new(name, "AREA")
    ld.size, ld.color, ld.energy = size, color, power
    o = bpy.data.objects.new(name, ld)
    C_LIGHT.objects.link(o)
    o.location, o.rotation_euler = loc, Euler(rot)
    return o
area("Key_Warm", (-0.6, 1.6, 1.1), (math.radians(55), 0, math.radians(200)), 1.2, (1.0, 0.72, 0.5), 9.0)
area("Knight_Fill", (-0.05, 1.05, 0.55), (math.radians(70), 0, math.radians(195)), 0.5, (1.0, 0.92, 0.8), 3.5)
area("Rim_Cool", (0.9, -1.4, 1.0), (math.radians(-60), 0, math.radians(20)), 1.0, (0.55, 0.65, 1.0), 25.0)

# ------------------------------------------------------------------ the 3D "429" logo
font_path = r"C:/Windows/Fonts/ariblk.ttf"
font = bpy.data.fonts.load(font_path, check_existing=True)
cu = bpy.data.curves.new("Logo_429", "FONT")
cu.body = "429"
cu.font = font
cu.align_x = "CENTER"
cu.align_y = "CENTER"
cu.size = 1.0
cu.extrude = 0.16
cu.bevel_depth = 0.075
cu.bevel_resolution = 2
cu.space_character = 0.97
tmp = bpy.data.objects.new("Logo_Tmp", cu)
sc.collection.objects.link(tmp)
bpy.context.view_layer.objects.active = tmp
tmp.select_set(True)
bpy.ops.object.convert(target="MESH")
logo_src = bpy.context.active_object
logo_src.name = "Logo_429_Mesh"
gold = mat("Steam_Logo_Gold", (0.95, 0.52, 0.1), rough=0.28, metal=1.0, emit=(1.0, 0.5, 0.08), strength=0.12)
green = bpy.data.materials.new("Steam_Logo_Green")
green.use_nodes = True
gn = green.node_tree.nodes
gp = next(n for n in gn if n.type == "BSDF_PRINCIPLED")
gp.inputs["Base Color"].default_value = (0.0, 0.045, 0.01, 1)
gp.inputs["Roughness"].default_value = 0.12
gp.inputs["Coat Weight"].default_value = 0.35
gp.inputs["Emission Color"].default_value = (0.15, 1.0, 0.2, 1)
gp.inputs["Emission Strength"].default_value = 0.08
me = logo_src.data
me.materials.clear()
me.materials.append(gold)
me.materials.append(green)
# Text faces +Z after conversion: the flat front (normal +Z, at the top of the extrude) is green, all else gold.
top = max(v.co.z for v in me.vertices)
for poly in me.polygons:
    poly.material_index = 1 if (poly.normal.z > 0.95 and poly.center.z > top - 0.001) else 0
logo_src.rotation_euler = Euler((math.radians(90), 0, 0))      # stand it up, front toward -Y... turned per camera
bpy.context.view_layer.objects.active = logo_src
bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
link(logo_src, C_LOGO)
logo_src.hide_render = True
logo_src.hide_viewport = True

# A green glow card behind each logo (soft emissive halo).
glow_m = bpy.data.materials.new("Steam_Logo_Glow")
glow_m.use_nodes = True
gn = glow_m.node_tree.nodes
gn.clear()
go_ = gn.new("ShaderNodeOutputMaterial")
em = gn.new("ShaderNodeEmission")
em.inputs["Color"].default_value = (0.2, 1.0, 0.25, 1)
tr = gn.new("ShaderNodeBsdfTransparent")
mx = gn.new("ShaderNodeMixShader")
grd = gn.new("ShaderNodeTexGradient")
grd.gradient_type = "SPHERICAL"
tco = gn.new("ShaderNodeTexCoord")
mp = gn.new("ShaderNodeMapping")
mp.inputs["Location"].default_value = (-1.0, -1.0, 0)
mp.inputs["Scale"].default_value = (2.0, 2.0, 1.0)
glow_m.node_tree.links.new(tco.outputs["Generated"], mp.inputs["Vector"])
glow_m.node_tree.links.new(mp.outputs["Vector"], grd.inputs["Vector"])
pw = gn.new("ShaderNodeMath")
pw.operation = "POWER"
pw.inputs[1].default_value = 2.0
glow_m.node_tree.links.new(grd.outputs["Fac"], pw.inputs[0])
glow_m.node_tree.links.new(pw.outputs[0], mx.inputs["Fac"])
glow_m.node_tree.links.new(tr.outputs[0], mx.inputs[1])
glow_m.node_tree.links.new(em.outputs[0], mx.inputs[2])
em.inputs["Strength"].default_value = 2.0
glow_m.node_tree.links.new(mx.outputs[0], go_.inputs["Surface"])

def logo_for(cam, name, local_loc, scale):
    """A logo copy parented to the camera: it stays where it is in that camera's picture."""
    o = bpy.data.objects.new(name, logo_src.data)
    C_LOGO.objects.link(o)
    o.parent = cam
    o.location = local_loc
    o.rotation_euler = Euler((0, 0, 0))
    o.scale = (scale, scale, scale)
    # The logo mesh faces -Y after the stand-up; in camera space the camera looks down -Z, so turn it to face +Z.
    o.rotation_euler = Euler((math.radians(-90), 0, 0))
    # Its own key light, above and in front of it (gold needs something bright to shine).
    kl = bpy.data.lights.new(name + "_Key", "AREA")
    kl.size, kl.color, kl.energy = 0.25 * scale / 0.15, (1.0, 0.88, 0.7), 2.2 * (scale / 0.15) ** 2
    ko = bpy.data.objects.new(name + "_Key", kl)
    C_LOGO.objects.link(ko)
    ko.parent = cam
    ko.location = Vector(local_loc) + Vector((-0.12 * scale / 0.15, 0.22 * scale / 0.15, 0.32))
    ko.rotation_euler = Euler((math.radians(-35), math.radians(-12), 0))
    gl = bpy.data.lights.new(name + "_Light", "POINT")
    gl.color, gl.energy, gl.shadow_soft_size = (0.25, 1.0, 0.3), 1.5 * scale * 10, 0.1
    lo = bpy.data.objects.new(name + "_Light", gl)
    C_LOGO.objects.link(lo)
    lo.parent = cam
    lo.location = Vector(local_loc) + Vector((0, 0.0, -0.08 * scale * 10))
    return o

# ------------------------------------------------------------------ cameras, one per Steam image
base_cam = sc.camera
base_cam.name = "CAM_Header_Capsule_920x430"
link(base_cam, C_CAM)
BASE_LOC = base_cam.location.copy()
BASE_ROT = base_cam.rotation_euler.copy()

def make_cam(name, w, h, loc_off=(0, 0, 0), dist_scale=1.0, lens=50.0, rot_off=(0, 0, 0), logo=None, logo_scale=0.12,
             hide_chars=False, transparent=False, aim=Vector((-0.06, 0.2, 0.2))):
    if name == base_cam.name:
        cam = base_cam
    else:
        cd = bpy.data.cameras.new(name)
        cam = bpy.data.objects.new(name, cd)
        C_CAM.objects.link(cam)
    cam.data.lens = lens
    cam.data.sensor_fit = "AUTO"
    cam.data.clip_end = 400
    # Pull back from the aim point along the base view, then offset.
    d = (BASE_LOC - aim) * dist_scale
    cam.location = aim + d + Vector(loc_off)
    cam.rotation_euler = Euler((BASE_ROT.x + rot_off[0], BASE_ROT.y + rot_off[1], BASE_ROT.z + rot_off[2]))
    cam["steam_w"], cam["steam_h"] = w, h
    cam["logo"] = bool(logo)
    cam["hide_chars"] = hide_chars
    cam["transparent"] = transparent
    if logo:
        logo_for(cam, "LOGO_" + name[4:], logo, logo_scale)
    return cam

SHOTS = [
    # name, w, h, kwargs
    ("CAM_Header_Capsule_920x430", 920, 430, dict(loc_off=(-0.11, 0, 0), logo=(-0.2, 0.0, -1.0), logo_scale=0.15)),
    ("CAM_Library_Header_920x430", 920, 430, dict(loc_off=(-0.11, 0, 0), logo=(-0.2, 0.0, -1.0), logo_scale=0.15)),
    ("CAM_Main_Capsule_1232x706", 1232, 706, dict(dist_scale=1.25, logo=(-0.2, 0.03, -1.0), logo_scale=0.14)),
    ("CAM_Small_Capsule_462x174", 462, 174, dict(dist_scale=1.0, loc_off=(-0.08, 0, 0), logo=(-0.17, -0.005, -1.0), logo_scale=0.16)),
    ("CAM_Vertical_Capsule_748x896", 748, 896, dict(dist_scale=1.45, rot_off=(math.radians(-3), 0, 0), loc_off=(-0.12, 0, 0.12),
                                                    logo=(0.0, 0.17, -1.0), logo_scale=0.12)),
    ("CAM_Library_Capsule_600x900", 600, 900, dict(dist_scale=1.55, rot_off=(math.radians(-3), 0, 0), loc_off=(-0.12, 0, 0.12),
                                                   logo=(0.0, 0.18, -1.0), logo_scale=0.11)),
    ("CAM_Page_BG_1438x810", 1438, 810, dict(dist_scale=1.6)),
    ("CAM_Library_Hero_3840x1240", 3840, 1240, dict(dist_scale=1.25, lens=40.0)),
    ("CAM_Library_Logo_1280x720", 1280, 720, dict(logo=(0.0, 0.0, -1.0), logo_scale=0.17, hide_chars=True, transparent=True)),
]
for name, w, h, kw in SHOTS:
    make_cam(name, w, h, **kw)
sc.camera = base_cam
def place_from(cam, u, v, dist, w, h):
    fw = 36.0 / cam.data.lens
    fh = fw * h / w
    local = Vector(((u - 0.5) * fw, (v - 0.5) * fh, -1.0)).normalized() * dist
    return cam.matrix_world @ local
bpy.context.view_layer.update()
moon_at = place_from(base_cam, 0.36, 0.9, 46.0, 920, 430)
dm = moon_at - moon.location
moon.location = moon_at
for o in C_SKY.objects:
    if o.name.startswith("Bat_"):
        o.location += dm
print("MOON at", tuple(round(c, 1) for c in moon_at))
sc.render.resolution_x, sc.render.resolution_y, sc.render.resolution_percentage = 920, 430, 100

bpy.data.objects.remove(bpy.data.objects["Logo_Tmp"]) if "Logo_Tmp" in bpy.data.objects else None
try:
    tree = bpy.data.node_groups.new("Steam Glow", "CompositorNodeTree")
    sc.compositing_node_group = tree
    rl = tree.nodes.new("CompositorNodeRLayers")
    gl = tree.nodes.new("CompositorNodeGlare")
    for prop, val in (("glare_type", "BLOOM"), ("quality", "HIGH")):
        try: setattr(gl, prop, val)
        except Exception as e: print("glare prop", prop, e)
    for sock_name, val in (("Type", "Bloom"), ("Threshold", 1.0), ("Strength", 0.6), ("Size", 0.6), ("Quality", "High")):
        sck = gl.inputs.get(sock_name)
        if sck is not None:
            try: sck.default_value = val
            except Exception as e: print("glare input", sock_name, e)
    tree.interface.new_socket("Image", in_out="OUTPUT", socket_type="NodeSocketColor")
    go = tree.nodes.new("NodeGroupOutput")
    tree.links.new(rl.outputs["Image"], gl.inputs["Image"])
    tree.links.new(gl.outputs["Image"], go.inputs[0])
    print("COMPOSITOR ok, glare inputs", [i.name for i in gl.inputs])
except Exception as e:
    print("COMPOSITOR failed", e)
bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND, copy=False)
print("SAVED", OUT_BLEND)
