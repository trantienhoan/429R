"""PUMPKIN GLOW HERO lighting variant of the Steam art.
Opens Exports/Steam_Art/Steam_Art.blend, relights it so the big jack-o'-lantern is the hero light source
(warm light pouring out of its face onto the floor, the knight and the logo; small pumpkins make warm pools;
everything else falls into deep shadow; low orange-lit ground fog; a gentle cool moon rim only for separation;
strong vignette), then SAVES AS variants/Steam_Art_pumpkinglow.blend (Steam_Art.blend is never overwritten).
Cameras, logo shape and poses are not touched.
  blender -b Exports/Steam_Art/Steam_Art.blend --python Exports/Steam_Art/variants/light_pumpkinglow.py
(it also opens the source itself, so any blend / factory startup works as the -b file)."""
import bpy, math, os
from mathutils import Vector, Euler

SRC = r"C:/Github_Repository/429R/Exports/Steam_Art/Steam_Art.blend"
OUT = r"C:/Github_Repository/429R/Exports/Steam_Art/variants/Steam_Art_pumpkinglow.blend"
if os.path.normcase(os.path.abspath(bpy.data.filepath or "")) != os.path.normcase(os.path.abspath(SRC)):
    bpy.ops.wm.open_mainfile(filepath=SRC)
sc = bpy.context.scene
OB = bpy.data.objects
C_LIGHT = bpy.data.collections["Lights"]
C_SET = bpy.data.collections["Set Dressing"]

def principled(m):
    return next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")

def new_light(name, kind, loc, color, energy, size=0.05, rot=None, target=None):
    old = OB.get(name)
    if old is not None:
        bpy.data.objects.remove(old)
    ld = bpy.data.lights.new(name, kind)
    ld.color, ld.energy = color, energy
    if kind == "AREA":
        ld.shape, ld.size = "DISK", size
    elif kind in ("POINT", "SPOT"):
        ld.shadow_soft_size = size
    o = bpy.data.objects.new(name, ld)
    C_LIGHT.objects.link(o)
    o.location = loc
    if target is not None:
        d = Vector(target) - Vector(loc)
        o.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    elif rot is not None:
        o.rotation_euler = Euler(rot)
    return o

def aim(o, target):
    d = Vector(target) - o.matrix_world.translation
    o.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()

# ------------------------------------------------------------------ render / colour
sc.cycles.max_bounces = 6
sc.cycles.volume_bounces = 0          # fog: single scatter only (cheap)
sc.cycles.volume_step_rate = 2.0
sc.view_settings.view_transform = "AgX"
for lk in ("AgX - Punchy", "AgX - Medium High Contrast"):
    try:
        sc.view_settings.look = lk
        break
    except TypeError:
        pass
sc.view_settings.exposure = 0.0
sc.view_settings.gamma = 1.0

# ------------------------------------------------------------------ world: deep night, darker ambient
w = sc.world
nt = w.node_tree
grad = next(n for n in nt.nodes if n.bl_idname == "ShaderNodeValToRGB")
grad.color_ramp.elements[0].color = (0.016, 0.014, 0.04, 1)     # horizon: dark indigo
grad.color_ramp.elements[1].position = 0.4
grad.color_ramp.elements[1].color = (0.0015, 0.0025, 0.009, 1)  # zenith: near black navy
star = next(n for n in nt.nodes if n.bl_idname == "ShaderNodeMapRange")
star.inputs["To Max"].default_value = 2.2
# Stars count as sky only: the floor and walls get almost no ambient (camera rays see stars; diffuse rays see dark).
bg = next(n for n in nt.nodes if n.bl_idname == "ShaderNodeBackground")
out_w = next(n for n in nt.nodes if n.bl_idname == "ShaderNodeOutputWorld")
lp = nt.nodes.new("ShaderNodeLightPath")
amb = nt.nodes.new("ShaderNodeBackground")
amb.inputs["Color"].default_value = (0.005, 0.007, 0.016, 1)   # darker than the visible sky: no grey sheen on the far floor
amb.inputs["Strength"].default_value = 1.0
mxw = nt.nodes.new("ShaderNodeMixShader")
nt.links.new(lp.outputs["Is Camera Ray"], mxw.inputs["Fac"])
nt.links.new(amb.outputs["Background"], mxw.inputs[1])
nt.links.new(bg.outputs["Background"], mxw.inputs[2])
nt.links.new(mxw.outputs["Shader"], out_w.inputs["Surface"])

# ------------------------------------------------------------------ moon: cooler, calmer (it is not the hero)
moon_m = bpy.data.materials["Steam_Moon"]
for n in moon_m.node_tree.nodes:
    if n.bl_idname == "ShaderNodeValToRGB":
        n.color_ramp.elements[0].color = (0.42, 0.46, 0.56, 1)
        n.color_ramp.elements[1].color = (0.88, 0.92, 1.0, 1)
    if n.bl_idname == "ShaderNodeEmission":
        n.inputs["Strength"].default_value = 1.4
win = bpy.data.materials["Steam_Castle_Window"]
principled(win).inputs["Emission Strength"].default_value = 30.0
principled(win).inputs["Emission Color"].default_value = (1.0, 0.42, 0.08, 1)

# Far haze: cool, a bit denser so the castle and mountains step back in layers.
haze_pv = next(n for n in bpy.data.materials["Steam_Haze"].node_tree.nodes if n.bl_idname == "ShaderNodeVolumePrincipled")
haze_pv.inputs["Color"].default_value = (0.06, 0.08, 0.16, 1)
haze_pv.inputs["Density"].default_value = 0.0045

# ------------------------------------------------------------------ jack-o'-lantern faces: the cut walls glow too
pmat = bpy.data.materials["Steam_Jack_O_Lantern"]
pn, pl = pmat.node_tree.nodes, pmat.node_tree.links
prin = principled(pmat)
albedo = next(l.from_node for l in pl if l.to_node == prin and l.to_socket.name == "Base Color")
for l in list(pl):
    if l.to_node == prin and l.to_socket.name in ("Emission Color", "Emission Strength"):
        pl.remove(l)
hsv = pn.new("ShaderNodeSeparateColor")
hsv.mode = "HSV"
pl.new(albedo.outputs[0], hsv.inputs[0])
rgb = pn.new("ShaderNodeSeparateColor")
pl.new(albedo.outputs[0], rgb.inputs[0])
# cut walls = very dark red-brown (linear value < 0.09, hue < 0.12; the stem is darker but yellow-green); inner flesh = pale yellow
walls_v = pn.new("ShaderNodeMapRange")
walls_v.inputs["From Min"].default_value = 0.1
walls_v.inputs["From Max"].default_value = 0.065
walls_v.inputs["To Min"].default_value = 0.0
walls_v.inputs["To Max"].default_value = 1.0
pl.new(hsv.outputs[2], walls_v.inputs["Value"])
walls_h = pn.new("ShaderNodeMath"); walls_h.operation = "LESS_THAN"; walls_h.inputs[1].default_value = 0.12
walls_k = pn.new("ShaderNodeMath"); walls_k.operation = "MULTIPLY"; walls_k.inputs[1].default_value = 0.4   # walls glow softer than the flesh
pl.new(hsv.outputs[0], walls_h.inputs[0]); pl.new(walls_h.outputs[0], walls_k.inputs[0])
walls = pn.new("ShaderNodeMath"); walls.operation = "MULTIPLY"; walls.name = "PG_Walls"
walls_h.use_clamp = False
pl.new(walls_v.outputs["Result"], walls.inputs[0]); pl.new(walls_k.outputs[0], walls.inputs[1])
flesh = pn.new("ShaderNodeMapRange")
flesh.inputs["From Min"].default_value = 0.55
flesh.inputs["From Max"].default_value = 0.78
flesh.inputs["To Min"].default_value = 0.0
flesh.inputs["To Max"].default_value = 1.0
pl.new(rgb.outputs[1], flesh.inputs["Value"])
emit_mask = pn.new("ShaderNodeMath")
emit_mask.operation = "MAXIMUM"
emit_mask.use_clamp = True
pl.new(walls.outputs[0], emit_mask.inputs[0])
pl.new(flesh.outputs["Result"], emit_mask.inputs[1])
estr = pn.new("ShaderNodeMath")
estr.operation = "MULTIPLY"
estr.inputs[1].default_value = 12.0
estr.name = "PG_Face_Strength"
pl.new(emit_mask.outputs[0], estr.inputs[0])
pl.new(estr.outputs[0], prin.inputs["Emission Strength"])
ecol = pn.new("ShaderNodeMix")          # deep orange at the walls, hot yellow at the core
ecol.data_type = "RGBA"
ecol.inputs["A"].default_value = (1.0, 0.3, 0.03, 1)
ecol.inputs["B"].default_value = (1.0, 0.62, 0.16, 1)
pl.new(flesh.outputs["Result"], ecol.inputs["Factor"])
pl.new(ecol.outputs["Result"], prin.inputs["Emission Color"])
# skin: a touch of waxy sheen to catch the moon rim
if not prin.inputs["Roughness"].is_linked:
    prin.inputs["Roughness"].default_value = 0.42

# ------------------------------------------------------------------ knight: glowing green eyes (bright lime only)
kmat = bpy.data.materials["Knight_Armour_Sword_Shield"]
kn, kl = kmat.node_tree.nodes, kmat.node_tree.links
kp = principled(kmat)
ktex = next(n for n in kn if n.bl_idname == "ShaderNodeTexImage" and "Diffuse" in n.image.filepath)
khsv = kn.new("ShaderNodeSeparateColor")
khsv.mode = "HSV"
kl.new(ktex.outputs["Color"], khsv.inputs[0])
hue_lo = kn.new("ShaderNodeMath"); hue_lo.operation = "GREATER_THAN"; hue_lo.inputs[1].default_value = 0.2
hue_hi = kn.new("ShaderNodeMath"); hue_hi.operation = "LESS_THAN"; hue_hi.inputs[1].default_value = 0.42
kl.new(khsv.outputs[0], hue_lo.inputs[0]); kl.new(khsv.outputs[0], hue_hi.inputs[0])
val = kn.new("ShaderNodeMapRange")
val.inputs["From Min"].default_value = 0.78
val.inputs["From Max"].default_value = 0.92
kl.new(khsv.outputs[2], val.inputs["Value"])
m1 = kn.new("ShaderNodeMath"); m1.operation = "MULTIPLY"
m2 = kn.new("ShaderNodeMath"); m2.operation = "MULTIPLY"
m3 = kn.new("ShaderNodeMath"); m3.operation = "MULTIPLY"; m3.inputs[1].default_value = 2.5; m3.name = "PG_Eye_Strength"
kl.new(hue_lo.outputs[0], m1.inputs[0]); kl.new(hue_hi.outputs[0], m1.inputs[1])
kl.new(m1.outputs[0], m2.inputs[0]); kl.new(val.outputs["Result"], m2.inputs[1])
kl.new(m2.outputs[0], m3.inputs[0])
kl.new(ktex.outputs["Color"], kp.inputs["Emission Color"])
kl.new(m3.outputs[0], kp.inputs["Emission Strength"])

# ------------------------------------------------------------------ logo: richer gold, glowing green face
gold = principled(bpy.data.materials["Steam_Logo_Gold"])
gold.inputs["Base Color"].default_value = (1.0, 0.6, 0.16, 1)
gold.inputs["Roughness"].default_value = 0.24
gold.inputs["Emission Color"].default_value = (1.0, 0.55, 0.12, 1)
gold.inputs["Emission Strength"].default_value = 0.45
green = principled(bpy.data.materials["Steam_Logo_Green"])
green.inputs["Base Color"].default_value = (0.002, 0.035, 0.007, 1)
green.inputs["Emission Color"].default_value = (0.12, 1.0, 0.18, 1)
green.inputs["Emission Strength"].default_value = 0.22

# ------------------------------------------------------------------ floor: a little sheen so the glow pools shine
for mname, r in (("Rooftop_Base_Lid", 0.5), ("Soil_Surface", 0.6)):
    m = bpy.data.materials.get(mname)
    if m:
        p = principled(m)
        if not p.inputs["Roughness"].is_linked:
            p.inputs["Roughness"].default_value = r

# ------------------------------------------------------------------ old broad lights: down / off
OB["Key_Warm"].data.energy = 0.0
OB["Key_Warm"].hide_render = True
OB["Knight_Fill"].data.energy = 0.6
OB["Knight_Fill"].data.color = (1.0, 0.7, 0.45)
moon_l = OB["Moonlight"].data
moon_l.energy, moon_l.color = 0.12, (0.55, 0.68, 1.0)
rim = OB["Rim_Cool"]
rim.data.energy, rim.data.color, rim.data.size = 22.0, (0.5, 0.64, 1.0), 0.8
aim(rim, (0.15, 0.27, 0.25))
OB["Pumpkin_Glow"].data.energy = 0.0
OB["Pumpkin_Glow"].hide_render = True

# ------------------------------------------------------------------ light linking: pumpkin lights skip the pumpkins
ll = bpy.data.collections.get("PG_No_Pumpkins") or bpy.data.collections.new("PG_No_Pumpkins")
for o in [OB["Hero_Pumpkin"]] + [o for o in OB if o.name.startswith("Small_Pumpkin_") and o.type == "MESH"]:
    if o.name not in ll.objects:
        ll.objects.link(o)
for co in ll.collection_objects:
    co.light_linking.link_state = "EXCLUDE"

hero = OB["Hero_Pumpkin"]
bb = [hero.matrix_world @ Vector(c) for c in hero.bound_box]
hc = sum(bb, Vector()) / 8
cam = OB["CAM_Header_Capsule_920x430"]
to_cam = (cam.location - hc)
to_cam.z = 0
to_cam.normalize()
FACE = hc + to_cam * 0.36
FACE.z = 0.16

# Hero light 1: the face itself (warm point just outside the cut face, sees floor, knight, logo).
face = new_light("PG_Face_Light", "POINT", FACE, (1.0, 0.46, 0.12), 16.0, size=0.09)
face.light_linking.receiver_collection = ll
# Hero light 2: low wide spill onto the floor in front (hot pool under the face).
spill = new_light("PG_Face_Spill", "SPOT", FACE + Vector((0, 0, 0.06)) - to_cam * 0.05, (1.0, 0.5, 0.14), 12.0, size=0.12,
                  target=hc + to_cam * 0.55 + Vector((0, 0, -0.2)))
spill.data.spot_size, spill.data.spot_blend = math.radians(110), 1.0
spill.light_linking.receiver_collection = ll
# Hero light 3: sideways out of the pumpkin toward the knight (warm side key on the knight, from the pumpkin side).
knight_c = Vector((-0.27, 0.14, 0.24))
side = new_light("PG_Knight_Warm", "AREA", hc + Vector((-0.2, 0.28, -0.02)), (1.0, 0.5, 0.16), 5.0, size=0.18,
                 target=knight_c)
side.light_linking.receiver_collection = ll
# Hero light 4: dim glow on top of the shell so the hero pumpkin's skin still reads orange (not black).
shell = new_light("PG_Shell_Fill", "AREA", hc + to_cam * 0.9 + Vector((0.25, 0, 0.55)), (1.0, 0.62, 0.36), 2.2, size=0.6,
                  target=hc)

# Cool moon rim on the knight (from behind, camera-right) for separation.
krim = new_light("PG_Knight_Rim", "AREA", (-0.85, -0.45, 0.62), (0.48, 0.62, 1.0), 10.0, size=0.45, target=knight_c)

# Small pumpkins: low warm pools on the floor.
for o in [o for o in OB if o.name.startswith("Small_Pumpkin_Glow_")]:
    i = int(o.name.rsplit("_", 1)[1])
    p = OB[f"Small_Pumpkin_{i}"]
    pc = sum((p.matrix_world @ Vector(c) for c in p.bound_box), Vector()) / 8
    s = p.dimensions.x / 0.15
    o.location = Vector((pc.x, pc.y, 0.035)) + (cam.location - pc).normalized() * Vector((1, 1, 0)) * (p.dimensions.x * 0.6)
    o.data.energy = 3.0 * s
    o.data.color = (1.0, 0.42, 0.09)
    o.data.shadow_soft_size = 0.03
    o.light_linking.receiver_collection = ll

# Candles: warmer, a bit brighter (tiny pools on the stone).
flame = principled(bpy.data.materials["Steam_Candle_Flame"])
flame.inputs["Emission Strength"].default_value = 70.0
for o in OB:
    if o.type == "LIGHT" and o.name.startswith("Candles_"):
        o.data.energy, o.data.color = 0.7, (1.0, 0.55, 0.2)

# Logo lights: warm key (like the pumpkin glow bouncing up), green halo behind.
for o in OB:
    if o.type == "LIGHT" and o.name.startswith("LOGO_"):
        if o.name.endswith("_Key"):
            o.data.color = (1.0, 0.78, 0.52)
            o.data.energy *= 2.2
        elif o.name.endswith("_Light"):
            o.data.energy *= 1.4


# ------------------------------------------------------------------ hard falloff: pumpkin light dies out after a set distance
def distance_cutoff(o, near, far):
    """Light node tree: Light Falloff 'Linear' (= distance when strength is 1) drives a smooth fade to 0 between near and far."""
    ld = o.data
    try:
        ld.use_nodes = True
    except Exception as e:
        print("light nodes", e)
    t = ld.node_tree
    if t is None:
        print("NO LIGHT NODE TREE", o.name)
        return
    em = next(n for n in t.nodes if n.type == "EMISSION")
    for n in [n for n in t.nodes if n.name.startswith("PG_")]:
        t.nodes.remove(n)
    lf = t.nodes.new("ShaderNodeLightFalloff"); lf.name = "PG_Falloff"
    lf.inputs["Strength"].default_value = 1.0
    mr = t.nodes.new("ShaderNodeMapRange"); mr.name = "PG_Cutoff"
    mr.interpolation_type = "SMOOTHSTEP"
    mr.inputs["From Min"].default_value = near
    mr.inputs["From Max"].default_value = far
    mr.inputs["To Min"].default_value = 1.0
    mr.inputs["To Max"].default_value = 0.0
    t.links.new(lf.outputs["Linear"], mr.inputs["Value"])
    t.links.new(mr.outputs["Result"], em.inputs["Strength"])
distance_cutoff(OB["PG_Face_Light"], 0.6, 1.6)
distance_cutoff(OB["PG_Face_Spill"], 0.5, 1.3)
for o in OB:
    if o.type == "LIGHT" and o.name.startswith("Small_Pumpkin_Glow_"):
        distance_cutoff(o, 0.18, 0.6)
# ------------------------------------------------------------------ low ground fog lit by the pumpkins
fog_m = bpy.data.materials.get("PG_Ground_Fog") or bpy.data.materials.new("PG_Ground_Fog")
fog_m.use_nodes = True
fn, fl = fog_m.node_tree.nodes, fog_m.node_tree.links
fn.clear()
fo = fn.new("ShaderNodeOutputMaterial")
fv = fn.new("ShaderNodeVolumePrincipled")
fv.inputs["Color"].default_value = (1.0, 0.8, 0.6, 1)
fv.inputs["Anisotropy"].default_value = 0.35
ftc = fn.new("ShaderNodeTexCoord")
fsep = fn.new("ShaderNodeSeparateXYZ")
fl.new(ftc.outputs["Generated"], fsep.inputs[0])
fall = fn.new("ShaderNodeMapRange")          # thick at the floor, gone at the top of the box
fall.inputs["From Min"].default_value = 0.0
fall.inputs["From Max"].default_value = 1.0
fall.inputs["To Min"].default_value = 1.0
fall.inputs["To Max"].default_value = 0.0
fall.interpolation_type = "SMOOTHERSTEP"
fl.new(fsep.outputs["Z"], fall.inputs["Value"])
fnoise = fn.new("ShaderNodeTexNoise")
fnoise.inputs["Scale"].default_value = 3.0
fnoise.inputs["Detail"].default_value = 3.0
fl.new(ftc.outputs["Object"], fnoise.inputs["Vector"])
nmap = fn.new("ShaderNodeMapRange")
nmap.inputs["From Min"].default_value = 0.35
nmap.inputs["From Max"].default_value = 0.7
nmap.inputs["To Min"].default_value = 0.25
nmap.inputs["To Max"].default_value = 1.0
fl.new(fnoise.outputs["Fac"], nmap.inputs["Value"])
fmul = fn.new("ShaderNodeMath"); fmul.operation = "MULTIPLY"
fl.new(fall.outputs["Result"], fmul.inputs[0]); fl.new(nmap.outputs["Result"], fmul.inputs[1])
fden = fn.new("ShaderNodeMath"); fden.operation = "MULTIPLY"; fden.name = "PG_Fog_Density"
fden.inputs[1].default_value = 1.5
fl.new(fmul.outputs[0], fden.inputs[0])
fl.new(fden.outputs[0], fv.inputs["Density"])
fl.new(fv.outputs["Volume"], fo.inputs["Volume"])
fog = OB.get("PG_Ground_Fog")
if fog is None:
    me = bpy.data.meshes.new("PG_Ground_Fog")
    import bmesh
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bm.to_mesh(me)
    bm.free()
    fog = bpy.data.objects.new("PG_Ground_Fog", me)
    C_SET.objects.link(fog)
    me.materials.append(fog_m)
fog.location = (0.0, 0.6, 0.055)
fog.scale = (6.0, 6.0, 0.15)            # z -0.02 .. 0.13 m (knight is 0.45 m tall)
fog.visible_shadow = True

# ------------------------------------------------------------------ light linking: only warm pumpkin/candle light touches the fog
def only(name, objs, state):
    c = bpy.data.collections.get(name) or bpy.data.collections.new(name)
    for o in objs:
        if o.name not in c.objects:
            c.objects.link(o)
    for co in c.collection_objects:
        co.light_linking.link_state = state
    return c
not_fog = only("PG_Not_Fog", [fog], "EXCLUDE")
pump_only = only("PG_Pumpkin_Only", [hero], "INCLUDE")
logo_only = only("PG_Logo_Only", [o for o in OB if o.name.startswith("LOGO_") and o.type == "MESH"], "INCLUDE")
knight_only = only("PG_Knight_Only", [o for o in OB if o.name.startswith("Pie_Knight") and o.type == "MESH"], "INCLUDE")
for n in ("Moonlight", "Rim_Cool", "PG_Knight_Rim"):
    OB[n].light_linking.receiver_collection = not_fog
OB["PG_Shell_Fill"].light_linking.receiver_collection = pump_only
chars_only = only("PG_Characters_Only", [hero] + [o for o in OB if o.type == "MESH" and (o.name.startswith("Pie_Knight") or
                  (o.name.startswith("Small_Pumpkin_") and not o.name.startswith("Small_Pumpkin_Glow")))], "INCLUDE")
for n in ("Rim_Cool", "PG_Knight_Rim"):
    OB[n].light_linking.receiver_collection = chars_only
OB["Knight_Fill"].light_linking.receiver_collection = knight_only
for o in OB:
    if o.type == "LIGHT" and o.name.startswith("LOGO_"):
        o.light_linking.receiver_collection = logo_only

# ------------------------------------------------------------------ compositor: bloom + vignette
tree = sc.compositing_node_group
gl = next(n for n in tree.nodes if n.bl_idname == "CompositorNodeGlare")
rl = next(n for n in tree.nodes if n.bl_idname == "CompositorNodeRLayers")
go = next(n for n in tree.nodes if n.bl_idname == "NodeGroupOutput")
for k, v in (("Threshold", 0.8), ("Strength", 0.8), ("Size", 0.75), ("Saturation", 1.1)):
    s_ = gl.inputs.get(k)
    if s_ is not None:
        s_.default_value = v
for n in [n for n in tree.nodes if n.name.startswith("PG_")]:
    tree.nodes.remove(n)
ic = tree.nodes.new("CompositorNodeImageCoordinates"); ic.name = "PG_Coords"
tree.links.new(rl.outputs["Image"], ic.inputs["Image"])
sub = tree.nodes.new("ShaderNodeVectorMath"); sub.name = "PG_Center"; sub.operation = "SUBTRACT"
sub.inputs[1].default_value = (0.5, 0.5, 0.0)
tree.links.new(ic.outputs["Normalized"], sub.inputs[0])
ln = tree.nodes.new("ShaderNodeVectorMath"); ln.name = "PG_Len"; ln.operation = "LENGTH"
tree.links.new(sub.outputs[0], ln.inputs[0])
vg = tree.nodes.new("ShaderNodeMapRange"); vg.name = "PG_Vignette"
vg.interpolation_type = "SMOOTHSTEP"
vg.inputs["From Min"].default_value = 0.3
vg.inputs["From Max"].default_value = 0.74
vg.inputs["To Min"].default_value = 1.0
vg.inputs["To Max"].default_value = 0.18
tree.links.new(ln.outputs["Value"], vg.inputs["Value"])
mul = tree.nodes.new("ShaderNodeMix"); mul.name = "PG_Vignette_Mul"
mul.data_type = "RGBA"; mul.blend_type = "MULTIPLY"
mul.inputs["Factor"].default_value = 1.0
tree.links.new(gl.outputs["Image"], mul.inputs["A"])
tree.links.new(vg.outputs["Result"], mul.inputs["B"])
# keep alpha (the transparent Library Logo shot): multiply colour only via Set Alpha from the glare image
sa = tree.nodes.new("CompositorNodeSetAlpha"); sa.name = "PG_Keep_Alpha"
print("SETALPHA inputs", [(i.name, getattr(i, "default_value", None)) for i in sa.inputs if i.name not in ("Image", "Alpha")])
for i in sa.inputs:
    if i.name in ("Type", "Mode"):
        try: i.default_value = "Replace Alpha"
        except Exception as e: print("setalpha type", e)
tree.links.new(mul.outputs["Result"], sa.inputs["Image"])
sepa = tree.nodes.new("CompositorNodeSeparateColor"); sepa.name = "PG_Alpha"
tree.links.new(rl.outputs["Image"], sepa.inputs["Image"])
tree.links.new(sepa.outputs["Alpha"], sa.inputs["Alpha"])
tree.links.new(sa.outputs["Image"], go.inputs[0])

os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=OUT, copy=False)
print("SAVED", OUT)
