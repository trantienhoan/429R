"""Palette colour test on the user's rooftop kit (2026-11-17).
Opens Rooftop_Cubes_Blockout.blend (never saves over it), colours the user's assembled plaza + west arm with the
palette (auto UVs), adds a few kit copies (stage, stepping stones, loose cubes, lamp towers), the old pumpkins for
colour reference and runes from Rune_Library.blend, renders a flat-colour pass and a night pass, and saves
Exports/Remodel/Rooftop_Colour_Test.blend.
  blender -b Exports/Remodel/Rooftop_Cubes_Blockout.blend --python Exports/Remodel/tools/colour_test.py -- <render_dir>"""
import bpy, bmesh, os, sys, math
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT = sys.argv[sys.argv.index("--") + 1]
os.makedirs(OUT, exist_ok=True)
exec(open(os.path.join(HERE, "palette_kit.py"), encoding="utf-8").read())
sc = bpy.context.scene
PAL = os.path.join(REPO, "Exports", "Remodel", "Palette")
mat = palette_material([os.path.join(PAL, "Rooftop_Palette.png"), os.path.join(PAL, "Rooftop_Palette_Emission.png")])

here = bpy.data.collections["Here"]
for c in bpy.data.collections:
    c.hide_render = c is not here
for o in here.objects:
    o.hide_render = o.location.x < 15          # the user's real layout only (the stacked preview stays hidden)
test = bpy.data.collections.new("Colour test extras")
sc.collection.children.link(test)


def copy_of(name, loc, rot_z=0.0):
    src = bpy.data.objects[name]
    o = src.copy()
    o.data = src.data.copy()
    o.location = loc
    o.rotation_euler = (0, 0, rot_z)
    o.hide_render = False
    test.objects.link(o)
    return o


def kind(o):
    return o.name.split(".")[0]


# ---- extras placed on the user's layout (plaza centre 41,0; west arm block 29,0; gate 25.4,0)
copy_of("Center", (41, 0, 0))
for i, (x, y) in enumerate(((27.6, 0.3), (30.0, -0.4), (32.4, 0.2), (34.6, -0.3))):
    copy_of("Glowing_Rock_%d" % (i % 3 + 1), (x, y, 0), i * 1.3)
copy_of("Cube_1", (27.0, -2.4, 0), 0.2)
copy_of("Cube_2", (27.3, -2.2, 2.5), 0.6)
copy_of("Cube_3", (28.4, -1.7, 0.25), 0.4)
copy_of("Cube_2", (27.4, 2.5, 0.5), -0.3)
copy_of("Light_Pole", (33.9, -3.4, 0), math.pi / 2)
copy_of("Light_Pole", (33.9, 3.4, 0), math.pi / 2)

# ---- old models for reference (pumpkins keep their textures; the stone pumpkin gets the palette)
lib = os.path.join(REPO, "Exports", "Remodel", "Rooftop_Remodel.blend")
with bpy.data.libraries.load(lib, link=False) as (src, dst):
    dst.collections = [n for n in src.collections if n in ("pumpkin_1", "pumpkin_evil", "pumpkin_0", "Stone_Pumpkin_2")]
gate = bpy.data.objects["Gate.001"]
gtop = max((gate.matrix_world @ Vector(v)).z for v in gate.bound_box)
place = {"pumpkin_1": (38.2, 2.6, 0, 0.4), "pumpkin_evil": (37.6, -2.9, 0, -0.6), "pumpkin_0": (44.0, -3.0, 0, 2.6),
         "Stone_Pumpkin_2": (gate.location.x, gate.location.y, gtop - 0.05, -math.pi / 2)}
stone_pumpkin = []
for c in dst.collections:
    sc.collection.children.link(c)
    for cc in c.children:
        cc.hide_render = True
    root = next(o for o in c.objects if o.name.startswith("MODEL"))
    meshes = [o for o in c.objects if o.type == 'MESH']
    bpy.context.view_layer.update()
    zmin = min((o.matrix_world @ Vector(v)).z for o in meshes for v in o.bound_box)
    x, y, z, r = place[c.name]
    root.location = (x, y, z + root.location.z - zmin)
    root.rotation_euler = (0, 0, r)
    if c.name == "Stone_Pumpkin_2":
        stone_pumpkin = meshes
bpy.context.view_layer.update()

# ---- palette UVs
def local_zrange(o):
    zs = [v.co.z for v in o.data.vertices]
    return min(zs), max(zs)


coloured = 0
for o in list(here.objects) + list(test.objects) + stone_pumpkin:
    if o.type != 'MESH' or (o in list(here.objects) and o.hide_render):
        continue
    k = kind(o)
    mw = o.matrix_world.copy()
    wz = lambda co, mw=mw: (mw @ co).z                     # world height: blocks may be flipped for variety
    zlo = min(wz(v.co) for v in o.data.vertices)
    seed = sum(map(ord, o.name))
    if k == "Block_0":
        sw, hfn = (lambda f, p: "stone"), wz
    elif k.startswith("Battlement"):
        sw, hfn = (lambda f, p: "stone_light"), wz
    elif k == "Gate":
        sw, hfn = (lambda f, p: ("stone_light" if p <= 1 else "stone" if p <= 3 else "stone_dark")), wz
    elif k == "Light_Pole":
        sw, hfn = (lambda f, p: ("stone" if p == 0 else "glow_lantern" if p == 1 else "iron")), wz
    elif k == "Center":
        bm = bmesh.new(); bm.from_mesh(o.data); bm.faces.ensure_lookup_table()
        parts = loose_parts(bm)
        low = {pid for pid in set(parts.values())
               if max(v.co.z for f in bm.faces if parts[f.index] == pid for v in f.verts) < 0.3}
        bm.free()
        sw, hfn = (lambda f, p, low=low: "stone_dark" if p in low else "stone"), (lambda co, wz=wz: wz(co) + 0.6)
    elif k.startswith("Cube_"):
        sw, hfn = (lambda f, p: "stone_light"), wz
    elif k.startswith("Glowing_Rock"):
        palette_uv(o, lambda f, p: "glow_cream", lambda co: 0, v_override=lambda f, p: 0.55, tint_seed=seed)
        o.data.materials.clear(); o.data.materials.append(mat); coloured += 1
        continue
    elif o in stone_pumpkin:
        sw, hfn = (lambda f, p: ("stone_light", 0.3)), (lambda co, wz=wz, zlo=zlo: (wz(co) - zlo) * 3 + 3)
    else:
        continue
    palette_uv(o, sw, hfn, tint_seed=seed, mw=mw)
    o.data.materials.clear()
    o.data.materials.append(mat)
    for p in o.data.polygons:
        p.material_index = 0
    coloured += 1
print("COLOURED", coloured)

# ---- runes from the library (placed by ray casts on the surfaces, before any rune exists)
rlib = os.path.join(REPO, "Exports", "Remodel", "Rune_Library.blend")
with bpy.data.libraries.load(rlib, link=False) as (src, dst):
    dst.meshes = [n for n in src.meshes if n.startswith("Rune_")]
RUNE = {m.name.split(".")[0]: m for m in dst.meshes}
for m in RUNE.values():
    m.materials.clear(); m.materials.append(mat)
dg = bpy.context.evaluated_depsgraph_get()
placements = []


def visible(ob):
    return not ob.hide_render and all(not c.hide_render for c in ob.users_collection)


def cast(origin, direction):
    o, d = Vector(origin), Vector(direction).normalized()
    for _ in range(20):                                          # skip hidden objects (old placeholders)
        hit, loc, nrm, _, ob, _ = sc.ray_cast(dg, o, d, distance=30)
        if not hit:
            return None
        if visible(ob) or (ob.parent and visible(ob.parent) and visible(ob)):
            return loc, nrm, ob
        o = loc + d * 0.001
    return None


def plan(mesh, origin, direction, size, up=None, recolour=None):
    h = cast(origin, direction)
    if not h:
        print("MISS", mesh, origin)
        return
    loc, nrm, ob = h
    if up is None:
        up = Vector((0, 0, 1))
    up = Vector(up)
    up = (up - nrm * up.dot(nrm)).normalized()
    x = up.cross(nrm)
    m = Matrix((x, up, nrm)).transposed().to_4x4()
    m.translation = loc
    placements.append((mesh, m, size, recolour, ob.name))


ALC = ["Fire", "Water", "Air", "Earth", "Salt", "Sun", "Moon", "Sulfur"]
for i, n in enumerate(ALC):                                     # stage ring, inlay gold, reads from outside
    a = 2 * math.pi * i / len(ALC)
    p = Vector((41 + 2.45 * math.cos(a), 2.45 * math.sin(a), 6))
    plan("Rune_%s_Inlay" % n, p, (0, 0, -1), 0.5, up=(math.cos(a), math.sin(a), 0))
mw = gate.matrix_world
plaza = Vector((41, 0, 0))
for side in (-1, 1):                                             # gate towers: carved purple, facing the plaza
    for j, (zz, n) in enumerate(((1.5, "Algiz"), (2.9, "Tiwaz"))):
        c = mw @ Vector((side * 3.19, -0.23, zz))
        d = (plaza - c); d.z = 0; d.normalize()
        plan("Rune_%s_Carved" % (n if side < 0 else ("Othala" if j else "Ehwaz")), c + d * 4, -d, 0.75, recolour="glow_purple")
for name in ("Battlement_block_1.001", "Battlement_block_1.002", "Battlement_block_0.001"):
    b = bpy.data.objects.get(name)
    if not b:
        continue
    bc = sum((b.matrix_world @ Vector(v) for v in b.bound_box), Vector()) / 8
    origin = Vector((29.0, 0.0, 0.9)) if name != "Battlement_block_0.001" else Vector((41.0, -19.0, 0.9))
    target = Vector((bc.x, bc.y, 0.9))
    if (target - origin).length < 0.5:
        continue
    plan("Rune_Sowilo_Carved", origin, target - origin, 0.5)

GOLD, PURPLE = INDEX["glow_gold"], INDEX["glow_purple"]
rc = bpy.data.collections.new("Runes (test)")
sc.collection.children.link(rc)
for mesh, m, size, recolour, on in placements:
    me = RUNE[mesh]
    if recolour:
        me = me.copy()
        uv = me.uv_layers.active.data
        for l in uv:
            if int(l.uv.x * W) // 32 == GOLD:
                l.uv.x += (PURPLE - GOLD) * 32 / W
    o = bpy.data.objects.new(mesh, me)
    o.matrix_world = m @ Matrix.Scale(size / 0.6, 4)
    rc.objects.link(o)
print("RUNES", len(placements))

# ---- renders
sc.render.resolution_x, sc.render.resolution_y = 1600, 1000


def cam(name, loc, tgt, lens):
    c = bpy.data.objects.new(name, bpy.data.cameras.new(name)); sc.collection.objects.link(c)
    c.location = loc; c.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    c.data.lens = lens; c.data.clip_end = 500
    return c


views = {"bird": cam("bird", (52, -20, 15), (35, 0, 1), 24),
         "eye_gate": cam("eye_gate", (39.5, -1.8, 1.6), (26, 0.5, 2.6), 20),
         "eye_stage": cam("eye_stage", (35.5, 4.5, 1.6), (41, 0, 0.6), 22)}
# flat colours (what the palette looks like, no lighting mood)
sc.render.engine = 'BLENDER_WORKBENCH'
sh = sc.display.shading
sh.light = 'FLAT'; sh.color_type = 'TEXTURE'; sh.show_cavity = False; sh.show_shadows = False
sc.view_settings.view_transform = 'Standard'
sc.world = bpy.data.worlds.new("w")
for n in ("bird", "eye_gate"):
    sc.camera = views[n]; sc.render.filepath = os.path.join(OUT, "flat_" + n + ".png")
    bpy.ops.render.render(write_still=True)
# night mood (moonlight + warm lamps)
try:
    sc.render.engine = 'BLENDER_EEVEE'
except TypeError as e:
    print(e)
w = sc.world; w.use_nodes = True
bg = next(n for n in w.node_tree.nodes if n.type == 'BACKGROUND')
bg.inputs[0].default_value = (0.035, 0.03, 0.08, 1); bg.inputs[1].default_value = 1.0
moon = bpy.data.objects.new("moon", bpy.data.lights.new("moon", 'SUN')); sc.collection.objects.link(moon)
moon.data.energy = 1.6; moon.data.color = (0.62, 0.66, 1.0); moon.rotation_euler = (math.radians(55), 0, math.radians(-40))
for o in list(test.objects):
    if kind(o) == "Light_Pole":
        lp = o.matrix_world @ Vector((0, 1.68, 5.2))
        L = bpy.data.objects.new("lamp", bpy.data.lights.new("lamp", 'POINT')); sc.collection.objects.link(L)
        L.location = lp; L.data.energy = 450; L.data.color = (1.0, 0.62, 0.3); L.data.shadow_soft_size = 0.3
T = bpy.data.objects.new("tree_glow", bpy.data.lights.new("tree_glow", 'POINT')); sc.collection.objects.link(T)
T.location = (41, 0, 4); T.data.energy = 700; T.data.color = (1.0, 0.55, 0.2)
for o in rc.objects:
    pass
for m in bpy.data.materials:
    if m.name.startswith("Rooftop_Palette"):
        nb = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        nb.inputs["Emission Strength"].default_value = 1.5
for n, c in views.items():
    sc.camera = c; sc.render.filepath = os.path.join(OUT, "night_" + n + ".png")
    bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(REPO, "Exports", "Remodel", "Rooftop_Colour_Test.blend"), relative_remap=True)
print("DONE")
