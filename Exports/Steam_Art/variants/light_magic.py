"""MAGIC GREEN lighting variant of the Steam art.
Opens Exports/Steam_Art/Steam_Art.blend, relights it (green magic rims, glowing logo + halo, fireflies,
purple-to-teal sky, low green mist, warm pumpkins for contrast, bloom + vignette + grade) and SAVES AS
Exports/Steam_Art/variants/Steam_Art_magic.blend (Steam_Art.blend is never overwritten).
Cameras, logo shape and character poses are not touched.
  blender -b --python variants/light_magic.py
"""
import bpy, math, os, random
from mathutils import Vector, Euler

SRC = r"C:/Github_Repository/429R/Exports/Steam_Art/Steam_Art.blend"
OUT = r"C:/Github_Repository/429R/Exports/Steam_Art/variants/Steam_Art_magic.blend"
bpy.ops.wm.open_mainfile(filepath=SRC)
sc = bpy.context.scene
random.seed(4290)

GREEN = (0.22, 1.0, 0.3)
TEAL = (0.25, 0.9, 1.0)
WARM = (1.0, 0.55, 0.18)

def coll(name):
    c = bpy.data.collections.get(name)
    if c is None:
        c = bpy.data.collections.new(name)
        sc.collection.children.link(c)
    return c
C_LIGHT, C_MAGIC = coll("Lights"), coll("Magic")

def aim(o, target):
    d = Vector(target) - o.location
    o.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()

def light(name, kind, loc, color, energy, target=None, size=0.1, spot=None, blend=0.5, spread=None, parent=None, c=None):
    ob = bpy.data.objects.get(name)
    if ob is None:
        ld = bpy.data.lights.new(name, kind)
        ob = bpy.data.objects.new(name, ld)
        (c or C_LIGHT).objects.link(ob)
    ob.data.type = kind
    ld = ob.data   # re-fetch: the Python class changes with the type
    ld.color, ld.energy = color, energy
    if kind == "AREA":
        ld.size = size
        if spread is not None: ld.spread = math.radians(spread)
    else:
        ld.shadow_soft_size = size
    if kind == "SPOT":
        ld.spot_size, ld.spot_blend = math.radians(spot or 45), blend
    ob.parent = parent
    ob.location = loc
    if target is not None: aim(ob, target)
    return ob

def principled(m):
    return next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")

def emission_mat(name, color, strength, sample=False):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    o = nt.nodes.new("ShaderNodeOutputMaterial")
    e = nt.nodes.new("ShaderNodeEmission")
    e.inputs["Color"].default_value = (*color, 1)
    e.inputs["Strength"].default_value = strength
    nt.links.new(e.outputs[0], o.inputs["Surface"])
    try: m.emission_sampling = "AUTO" if sample else "NONE"
    except Exception: pass
    return m

# ------------------------------------------------------------------ render / colour management
sc.cycles.use_light_tree = True
sc.cycles.volume_bounces = 0
sc.cycles.volume_step_rate = 4.0
sc.view_settings.view_transform = "AgX"
try: sc.view_settings.look = "AgX - Punchy"
except Exception as e: print("look", e)
sc.view_settings.exposure = 0.0

# ------------------------------------------------------------------ world: teal horizon -> purple -> near-black zenith
w = sc.world
nt = w.node_tree
grad = next(n for n in nt.nodes if n.bl_idname == "ShaderNodeValToRGB")
els = grad.color_ramp.elements
while len(els) > 2: els.remove(els[-1])
els[0].position, els[0].color = 0.0, (0.016, 0.12, 0.11, 1)         # teal glow behind the mountains
els[1].position, els[1].color = 0.4, (0.012, 0.004, 0.03, 1)        # zenith
mid = els.new(0.12); mid.color = (0.11, 0.022, 0.19, 1)               # violet band up high
mid2 = els.new(0.04); mid2.color = (0.02, 0.1, 0.095, 1)            # teal just above the mountain line
star = next(n for n in nt.nodes if n.bl_idname == "ShaderNodeMapRange")
star.inputs["To Max"].default_value = 2.2
bg = next(n for n in nt.nodes if n.bl_idname == "ShaderNodeBackground")
mixsky = next(n for n in nt.nodes if n.bl_idname == "ShaderNodeMix")
# The camera sees the full sky; everything else is lit by a much dimmer cool ambient (keeps the floor dark).
lp = nt.nodes.new("ShaderNodeLightPath")
amb = nt.nodes.new("ShaderNodeBackground")
amb.inputs["Color"].default_value = (0.006, 0.012, 0.02, 1)
amb.inputs["Strength"].default_value = 1.0
ms = nt.nodes.new("ShaderNodeMixShader")
out = next(n for n in nt.nodes if n.bl_idname == "ShaderNodeOutputWorld")
nt.links.new(lp.outputs["Is Camera Ray"], ms.inputs["Fac"])
nt.links.new(amb.outputs[0], ms.inputs[1])
nt.links.new(bg.outputs[0], ms.inputs[2])
nt.links.new(ms.outputs[0], out.inputs["Surface"])

# ------------------------------------------------------------------ moon: pale cyan, a bit less blown
moon_m = bpy.data.materials["Steam_Moon"]
cr = next(n for n in moon_m.node_tree.nodes if n.bl_idname == "ShaderNodeValToRGB")
cr.color_ramp.elements[0].color = (0.32, 0.45, 0.52, 1)
cr.color_ramp.elements[1].color = (0.85, 1.0, 0.95, 1)
next(n for n in moon_m.node_tree.nodes if n.bl_idname == "ShaderNodeEmission").inputs["Strength"].default_value = 2.2

# Far haze: violet-teal, slightly denser = mountains/castle fall off into the sky
hz = next(n for n in bpy.data.materials["Steam_Haze"].node_tree.nodes if n.bl_idname == "ShaderNodeVolumePrincipled")
hz.inputs["Color"].default_value = (0.5, 0.35, 0.7, 1)
hz.inputs["Density"].default_value = 0.007

# Castle windows: a few turn magic green, the rest stay warm
win_g = bpy.data.materials["Steam_Castle_Window"].copy(); win_g.name = "Steam_Castle_Window_Green"
pg = principled(win_g)
pg.inputs["Emission Color"].default_value = (*GREEN, 1); pg.inputs["Emission Strength"].default_value = 30
for i, o in enumerate(sorted([o for o in bpy.data.objects if o.name.startswith("Castle_Window_")], key=lambda o: o.name)):
    if i % 3 == 0:
        o.data = o.data.copy(); o.data.materials.clear(); o.data.materials.append(win_g)

# ------------------------------------------------------------------ the character lights
# Moonlight: cool teal back light (rims the knight + pumpkin tops from behind).
moon_l = bpy.data.objects["Moonlight"]
moon_l.data.color, moon_l.data.energy = (0.45, 0.8, 1.0), 0.15

# Warm key becomes a spot that pools on the pumpkin (floor beyond the pool falls into darkness).
light("Key_Warm", "SPOT", (-0.55, 1.5, 1.15), (1.0, 0.68, 0.42), 26.0, target=(0.1, 0.25, 0.12), size=0.18, spot=34, blend=0.75)
# Knight face fill: soft, slightly cool so the green eyes read against the visor.
light("Knight_Fill", "SPOT", (-0.2, 1.0, 0.62), (0.85, 0.95, 1.0), 18.0, target=(-0.27, 0.13, 0.27), size=0.12, spot=24, blend=0.8)
# Teal rim from behind-left (pumpkin top + knight's left edge).
light("Rim_Cool", "AREA", (0.75, -1.2, 0.9), TEAL, 32.0, target=(-0.05, 0.2, 0.25), size=0.6, spread=70)
# Green magic rims on the knight, from behind on both sides.
light("Magic_Rim_Knight_R", "AREA", (-0.85, -0.45, 0.55), GREEN, 18.0, target=(-0.27, 0.13, 0.28), size=0.3, spread=60)
light("Magic_Rim_Knight_L", "AREA", (0.15, -0.55, 0.65), GREEN, 12.0, target=(-0.27, 0.13, 0.3), size=0.25, spread=60)
# Green kick on the pumpkin's back-left edge (complementary edge on the warm subject).
light("Magic_Kick_Pumpkin", "AREA", (0.75, -0.35, 0.35), GREEN, 6.0, target=(0.14, 0.27, 0.2), size=0.25, spread=60)
# Light linking: the rims/fill touch only the characters, never the floor (floor stays dark, lit only by pools).
ll = bpy.data.collections.get("LL_Characters") or bpy.data.collections.new("LL_Characters")
for o in bpy.data.objects:
    if o.type == "MESH" and (o.name.startswith("Pie_Knight") or o.name == "Hero_Pumpkin") and o.name not in ll.objects:
        ll.objects.link(o)
for nm in ("Rim_Cool", "Magic_Rim_Knight_R", "Magic_Rim_Knight_L", "Magic_Kick_Pumpkin", "Knight_Fill"):
    bpy.data.objects[nm].light_linking.receiver_collection = ll
# Pillars + floor near the back: green magic up-glow.
light("Magic_Pillar_Glow_L", "POINT", (0.9, -1.85, 0.15), GREEN, 8.0, size=0.08)
light("Magic_Pillar_Glow_R", "POINT", (-0.9, -1.85, 0.15), TEAL, 7.0, size=0.08)
light("Magic_Pillar_Glow_L2", "POINT", (1.9, -0.75, 0.15), GREEN, 5.0, size=0.08)
light("Magic_Pillar_Glow_R2", "POINT", (-1.9, -0.75, 0.15), TEAL, 4.0, size=0.08)
light("Magic_Floor_Glow_Back", "POINT", (0.0, -1.1, 0.08), GREEN, 3.0, size=0.1)

# Warm practicals: brighter flames + pumpkin glow
for o in bpy.data.objects:
    if o.type == "LIGHT" and o.name.startswith("Candles_"):
        o.data.energy = 0.55
    if o.type == "LIGHT" and o.name.startswith("Small_Pumpkin_Glow_"):
        o.data.energy *= 1.4; o.data.color = WARM
pgl = bpy.data.objects["Pumpkin_Glow"]
pgl.data.energy, pgl.data.color = 3.0, (1.0, 0.5, 0.12)
principled(bpy.data.materials["Steam_Candle_Flame"]).inputs["Emission Strength"].default_value = 70

jack = bpy.data.materials["Steam_Jack_O_Lantern"]
rng = next(n for n in jack.node_tree.nodes if n.bl_idname == "ShaderNodeMapRange")
rng.inputs["To Max"].default_value = 11.0

# ------------------------------------------------------------------ knight: glowing eyes + clover (bright green texels emit)
km = bpy.data.materials["Knight_Armour_Sword_Shield"]
kn, kl = km.node_tree.nodes, km.node_tree.links
kp = principled(km)
tex = next(n for n in kn if n.bl_idname == "ShaderNodeTexImage" and n.name == "Image Texture")
if not any(n.name == "Magic_Eye_Mask" for n in kn):
    sp = kn.new("ShaderNodeSeparateColor")
    mx = kn.new("ShaderNodeMath"); mx.operation = "MAXIMUM"
    sub = kn.new("ShaderNodeMath"); sub.operation = "SUBTRACT"
    mr = kn.new("ShaderNodeMapRange"); mr.name = "Magic_Eye_Mask"
    mr.inputs["From Min"].default_value, mr.inputs["From Max"].default_value = 0.14, 0.45
    mr.inputs["To Min"].default_value, mr.inputs["To Max"].default_value = 0.0, 3.0
    kl.new(tex.outputs["Color"], sp.inputs[0])
    kl.new(sp.outputs[0], mx.inputs[0]); kl.new(sp.outputs[2], mx.inputs[1])
    kl.new(sp.outputs[1], sub.inputs[0]); kl.new(mx.outputs[0], sub.inputs[1])
    kl.new(sub.outputs[0], mr.inputs["Value"])
    kl.new(mr.outputs["Result"], kp.inputs["Emission Strength"])
    kp.inputs["Emission Color"].default_value = (0.1, 1.0, 0.15, 1)

# ------------------------------------------------------------------ logo: glowing green face, brighter gold, green halo shell
gold = principled(bpy.data.materials["Steam_Logo_Gold"])
gold.inputs["Emission Strength"].default_value = 0.35
gold.inputs["Emission Color"].default_value = (1.0, 0.78, 0.22, 1)   # yellow emission pulls copper toward gold
gold.inputs["Roughness"].default_value = 0.3
lg = principled(bpy.data.materials["Steam_Logo_Green"])
lg.inputs["Emission Color"].default_value = (0.05, 1.0, 0.12, 1)
lg.inputs["Emission Strength"].default_value = 0.45
halo_m = emission_mat("Steam_Logo_Halo", (0.04, 1.0, 0.1), 4.0)
# fade the shell out toward its silhouette: a soft glow instead of a hard outline
_hn = halo_m.node_tree
_em = next(n for n in _hn.nodes if n.bl_idname == "ShaderNodeEmission")
_ou = next(n for n in _hn.nodes if n.bl_idname == "ShaderNodeOutputMaterial")
_lw = _hn.nodes.new("ShaderNodeLayerWeight"); _lw.inputs["Blend"].default_value = 0.5
_iv = _hn.nodes.new("ShaderNodeMath"); _iv.operation = "SUBTRACT"; _iv.inputs[0].default_value = 1.0
_pw = _hn.nodes.new("ShaderNodeMath"); _pw.operation = "POWER"; _pw.inputs[1].default_value = 1.5
_tr = _hn.nodes.new("ShaderNodeBsdfTransparent")
_mx = _hn.nodes.new("ShaderNodeMixShader")
_hn.links.new(_lw.outputs["Facing"], _iv.inputs[1]); _hn.links.new(_iv.outputs[0], _pw.inputs[0])
_hn.links.new(_pw.outputs[0], _mx.inputs["Fac"]); _hn.links.new(_tr.outputs[0], _mx.inputs[1]); _hn.links.new(_em.outputs[0], _mx.inputs[2])
_hn.links.new(_mx.outputs[0], _ou.inputs["Surface"])
logo_mesh = bpy.data.meshes.get("Logo_429_Mesh") or bpy.data.objects["Logo_429_Mesh"].data
for lo in [o for o in bpy.data.objects if o.name.startswith("LOGO_") and o.type == "MESH"]:
    cam = lo.parent
    s = lo.scale.x
    hn = lo.name + "_Halo"
    h = bpy.data.objects.get(hn) or bpy.data.objects.new(hn, lo.data)
    if h.name not in bpy.data.collections["Logos"].objects:
        bpy.data.collections["Logos"].objects.link(h)
    h.parent = cam
    h.location = lo.location + Vector((0, 0, -0.012))
    h.rotation_euler = lo.rotation_euler.copy()
    h.scale = (s * 1.012,) * 3
    if not h.modifiers.get("Halo"):
        d = h.modifiers.new("Halo", "DISPLACE")
        d.mid_level, d.strength = 0.0, 0.03
    for slot in h.material_slots:
        slot.link = "OBJECT"; slot.material = halo_m
    h.visible_shadow = False
    h.visible_glossy = h.visible_diffuse = False   # gold must not reflect green (green x gold = mud)
    # logo's own green light: drop it toward the floor so the logo throws a green pool on the stone
    gl = bpy.data.objects.get(lo.name + "_Light")
    if gl:
        gl.data.color, gl.data.energy, gl.data.shadow_soft_size = GREEN, 10.0 * (s / 0.15) ** 2, 0.15
        gl.location = lo.location + Vector((0, -0.16, -0.3))
    # Gold needs something bright to reflect: warm cards only glossy rays see (camera never does).
    for tag, loc, rx, col, stg in (("_Reflector_Low", (0.0, -0.22, 0.08), -60, (1.0, 0.82, 0.35), 1.5),
                                   ("_Reflector_High", (0.0, 0.24, 0.1), -120, (1.0, 0.85, 0.6), 0.6)):
        rn = lo.name + tag
        rf = bpy.data.objects.get(rn)
        if rf is None:
            rme = bpy.data.meshes.new(rn)
            rme.from_pydata([(-1, -1, 0), (1, -1, 0), (1, 1, 0), (-1, 1, 0)], [], [(0, 1, 2, 3)])
            rf = bpy.data.objects.new(rn, rme)
            bpy.data.collections["Logos"].objects.link(rf)
            rme.materials.append(emission_mat("Steam_Logo_Reflector" + tag, col, stg, sample=False))
        rf.parent = cam
        k = s / 0.15
        rf.location = lo.location + Vector(loc) * k
        rf.rotation_euler = Euler((math.radians(rx), 0, 0))
        rf.scale = (0.2 * k, 0.12 * k, 1)
        rf.visible_camera = rf.visible_diffuse = rf.visible_shadow = rf.visible_transmission = rf.visible_volume_scatter = False
        rf.visible_glossy = True
    ky = bpy.data.objects.get(lo.name + "_Key")
    if ky:
        ky.data.color = (1.0, 0.92, 0.62)
        ky.data.energy *= 1.6
        ky.data.size *= 2.0

# ------------------------------------------------------------------ fireflies / magic sparks
for o in [o for o in C_MAGIC.objects if o.name.startswith("Firefly_")]:
    bpy.data.objects.remove(o)
ff_a = emission_mat("Steam_Firefly_Lime", (0.65, 1.0, 0.2), 40.0)
ff_b = emission_mat("Steam_Firefly_Teal", (0.2, 1.0, 0.65), 35.0)
bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=1.0)
tmp = bpy.context.active_object
ff_mesh = tmp.data; ff_mesh.name = "Firefly_Mesh"
bpy.data.objects.remove(tmp)
def keep_out(p):
    if -0.5 < p.x < 0.45 and -0.05 < p.y < 0.65 and p.z < 0.55: return True   # pumpkin + knight
    return False
n = 0
tries = 0
while n < 110 and tries < 5000:
    tries += 1
    p = Vector((random.uniform(-1.9, 1.9), random.uniform(-1.5, 1.35), random.uniform(0.04, 0.95) ** 1.4))
    if keep_out(p): continue
    o = bpy.data.objects.new(f"Firefly_{n:03d}", ff_mesh)
    C_MAGIC.objects.link(o)
    o.location = p
    r = random.uniform(0.0025, 0.0065)
    o.scale = (r, r, r)
    n += 1
ff_mesh.materials.clear()
ff_mesh.materials.append(ff_a)
for i, o in enumerate(o for o in C_MAGIC.objects if o.name.startswith("Firefly_")):
    o.material_slots[0].link = "OBJECT"
    o.material_slots[0].material = ff_a if i % 3 else ff_b
    o.visible_shadow = False

# ------------------------------------------------------------------ low magic mist hugging the floor (cheap volume)
mist = bpy.data.objects.get("Magic_Mist")
if mist is None:
    bpy.ops.mesh.primitive_cube_add(size=1.0)
    mist = bpy.context.active_object
    mist.name = "Magic_Mist"
    for c in list(mist.users_collection): c.objects.unlink(mist)
    C_MAGIC.objects.link(mist)
mist.location, mist.scale = (0.0, -1.1, 0.17), (6.0, 4.0, 0.34)
mist.visible_shadow = False
mm = bpy.data.materials.get("Steam_Magic_Mist") or bpy.data.materials.new("Steam_Magic_Mist")
mm.use_nodes = True
mn = mm.node_tree; mn.nodes.clear()
mo = mn.nodes.new("ShaderNodeOutputMaterial")
pv = mn.nodes.new("ShaderNodeVolumePrincipled")
pv.inputs["Color"].default_value = (0.55, 1.0, 0.8, 1)
tc = mn.nodes.new("ShaderNodeTexCoord")
sx = mn.nodes.new("ShaderNodeSeparateXYZ")
fall = mn.nodes.new("ShaderNodeMapRange")
fall.inputs["From Min"].default_value, fall.inputs["From Max"].default_value = 0.0, 0.85
fall.inputs["To Min"].default_value, fall.inputs["To Max"].default_value = 1.0, 0.0
pw = mn.nodes.new("ShaderNodeMath"); pw.operation = "POWER"; pw.inputs[1].default_value = 2.0
noi = mn.nodes.new("ShaderNodeTexNoise"); noi.inputs["Scale"].default_value = 3.0; noi.inputs["Detail"].default_value = 3
nm = mn.nodes.new("ShaderNodeMapRange")
nm.inputs["From Min"].default_value, nm.inputs["From Max"].default_value = 0.35, 0.7
mul = mn.nodes.new("ShaderNodeMath"); mul.operation = "MULTIPLY"
dens = mn.nodes.new("ShaderNodeMath"); dens.operation = "MULTIPLY"; dens.inputs[1].default_value = 0.08
mn.links.new(tc.outputs["Generated"], sx.inputs[0])
mn.links.new(sx.outputs["Z"], fall.inputs["Value"])
mn.links.new(fall.outputs["Result"], pw.inputs[0])
mn.links.new(tc.outputs["Object"], noi.inputs["Vector"])
mn.links.new(noi.outputs["Fac"], nm.inputs["Value"])
mn.links.new(pw.outputs[0], mul.inputs[0])
mn.links.new(nm.outputs["Result"], mul.inputs[1])
mn.links.new(mul.outputs[0], dens.inputs[0])
mn.links.new(dens.outputs[0], pv.inputs["Density"])
mn.links.new(pv.outputs["Volume"], mo.inputs["Volume"])
mist.data.materials.clear(); mist.data.materials.append(mm)

# ------------------------------------------------------------------ compositor: bloom -> grade -> vignette
tree = sc.compositing_node_group
for nd in [nd for nd in tree.nodes if nd.bl_idname not in ("CompositorNodeRLayers", "CompositorNodeGlare", "NodeGroupOutput")]:
    tree.nodes.remove(nd)
rl = next(nd for nd in tree.nodes if nd.bl_idname == "CompositorNodeRLayers")
glare = next(nd for nd in tree.nodes if nd.bl_idname == "CompositorNodeGlare")
gout = next(nd for nd in tree.nodes if nd.bl_idname == "NodeGroupOutput")
for k, v in (("Type", "Bloom"), ("Quality", "High"), ("Threshold", 0.6), ("Smoothness", 0.3), ("Strength", 0.75),
             ("Size", 0.8), ("Saturation", 1.0)):
    try: glare.inputs[k].default_value = v
    except Exception as e: print("glare", k, e)
last = glare.outputs["Image"]
try:
    cb = tree.nodes.new("CompositorNodeColorBalance")
    cb.inputs["Type"].default_value = "Lift/Gamma/Gain"
    lifts = [s for s in cb.inputs if s.name == "Lift" and s.bl_idname == "NodeSocketColor"]
    gains = [s for s in cb.inputs if s.name == "Gain" and s.bl_idname == "NodeSocketColor"]
    if lifts: lifts[0].default_value = (0.97, 1.0, 1.03, 1)    # shadows lean teal
    if gains: gains[0].default_value = (1.04, 1.01, 0.97, 1)   # highlights lean warm
    tree.links.new(last, cb.inputs["Image"]); last = cb.outputs["Image"]
except Exception as e:
    print("colour balance", e)
try:
    ic = tree.nodes.new("CompositorNodeImageCoordinates")
    vm = tree.nodes.new("ShaderNodeVectorMath"); vm.operation = "SUBTRACT"; vm.inputs[1].default_value = (0.5, 0.56, 0.0)
    vs = tree.nodes.new("ShaderNodeVectorMath"); vs.operation = "MULTIPLY"; vs.inputs[1].default_value = (1.0, 0.8, 0.0)
    vl = tree.nodes.new("ShaderNodeVectorMath"); vl.operation = "LENGTH"
    vr = tree.nodes.new("ShaderNodeMapRange"); vr.interpolation_type = "SMOOTHSTEP"
    vr.inputs["From Min"].default_value, vr.inputs["From Max"].default_value = 0.28, 0.75
    vr.inputs["To Min"].default_value, vr.inputs["To Max"].default_value = 1.0, 0.38
    mixv = tree.nodes.new("ShaderNodeMix"); mixv.data_type = "RGBA"; mixv.blend_type = "MULTIPLY"
    mixv.inputs["Factor"].default_value = 1.0
    norm = ic.outputs.get("Normalized") or ic.outputs[1]
    tree.links.new(norm, vm.inputs[0])
    tree.links.new(vm.outputs[0], vs.inputs[0])
    tree.links.new(vs.outputs[0], vl.inputs[0])
    tree.links.new(vl.outputs["Value"], vr.inputs["Value"])
    a = next(s for s in mixv.inputs if s.name == "A" and s.bl_idname == "NodeSocketColor")
    b = next(s for s in mixv.inputs if s.name == "B" and s.bl_idname == "NodeSocketColor")
    tree.links.new(last, a)
    tree.links.new(vr.outputs["Result"], b)
    last = next(s for s in mixv.outputs if s.name == "Result" and s.bl_idname == "NodeSocketColor")
    print("VIGNETTE ok", [o.name for o in ic.outputs])
except Exception as e:
    print("vignette", e)
for l in list(gout.inputs[0].links): tree.links.remove(l)
tree.links.new(last, gout.inputs[0])

sc.camera = bpy.data.objects["CAM_Header_Capsule_920x430"]
os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=OUT, copy=False)
print("SAVED", OUT)
