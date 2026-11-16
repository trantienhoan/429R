"""Grey-box of the cube rooftop (USER design 2026-11-16): 8 x 8 m stone cubes (4 m deep), a 2 x 2 plaza with the
rune dais and the tree where the four corners meet, paths of 1 cube (north, east, west) and 2 cubes (south = start),
a gate at each path's end, battlements on the outer edges only. Current props imported for scale. Renders a top view
next to today's rooftop at the same scale, a bird's-eye view and an eye-level view from the start.
  blender -b --factory-startup --python Exports/Remodel/tools/rooftop_cubes_blockout.py -- <out_dir>"""
import bpy, bmesh, sys, os, math
from mathutils import Vector

OUT = sys.argv[sys.argv.index("--") + 1]
REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
os.makedirs(OUT, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
C = 8.0          # cube size (top)
D = 4.0          # cube depth
MER = 1.1        # battlement height (merlons), waist-to-chest in VR
WT = 0.6         # battlement thickness


def mat(name, rgb):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (rgb[0], rgb[1], rgb[2], 1)
    return m


STONE = mat("stone", (0.46, 0.42, 0.50))
STONE2 = mat("stone2", (0.40, 0.36, 0.45))
DARK = mat("dark", (0.20, 0.18, 0.24))
GOLD = mat("gold", (0.95, 0.72, 0.25))
IRON = mat("iron", (0.12, 0.11, 0.13))
MIST = mat("mist", (0.55, 0.25, 0.75))
TRUNK = mat("trunk", (0.30, 0.18, 0.10))
LEAF = mat("leaf", (0.95, 0.55, 0.12))
PERSON = mat("person", (0.15, 0.6, 0.3))
CREAM = mat("cream", (0.95, 0.88, 0.62))


def box(name, size, loc, m, coll, bev=0.12, segs=2):
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    o.location = loc
    me.materials.append(m)
    coll.objects.link(o)
    if bev:
        b = o.modifiers.new("bevel", 'BEVEL')
        b.width = bev
        b.segments = segs
    return o


def cyl(name, r, h, loc, m, coll, verts=48):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=h, location=loc)
    o = bpy.context.object
    o.name = name
    o.data.materials.append(m)
    for c in list(o.users_collection):
        c.objects.unlink(o)
    coll.objects.link(o)
    return o


def battlement(coll, a, b):
    """Merlons along the top edge from a to b (2D points, on the cube's top at z=0), on the inside of the edge."""
    a, b = Vector((a[0], a[1], 0)), Vector((b[0], b[1], 0))
    d = (b - a)
    L = d.length
    d.normalize()
    n = Vector((-d.y, d.x, 0))
    # a low wall (0.5 m) all along, merlons on top
    mid = (a + b) / 2 + n * (WT / 2)
    w = box("wall", (L, WT, 0.5), mid + Vector((0, 0, 0.25)), STONE2, coll, 0.06)
    w.rotation_euler = (0, 0, math.atan2(d.y, d.x))
    k = int(L // 1.6)
    for i in range(k):
        p = a + d * (0.8 + i * (L - 1.6) / max(1, k - 1)) + n * (WT / 2)
        m = box("merlon", (0.8, WT, MER - 0.5), p + Vector((0, 0, 0.5 + (MER - 0.5) / 2)), STONE2, coll, 0.06)
        m.rotation_euler = (0, 0, math.atan2(d.y, d.x))


def cube(coll, cx, cy, outer_edges, shade=0):
    """One 8 m floor cube centred at (cx, cy), top at z=0. Outer Edges: 'N','S','E','W' sides that get battlements."""
    box("cube", (C - 0.06, C - 0.06, D), (cx, cy, -D / 2), STONE if shade == 0 else STONE2, coll, 0.25, 3)
    h = C / 2
    corners = {"N": ((cx - h, cy + h), (cx + h, cy + h)), "S": ((cx + h, cy - h), (cx - h, cy - h)),
               "E": ((cx + h, cy + h), (cx + h, cy - h)), "W": ((cx - h, cy - h), (cx - h, cy + h))}
    for e in outer_edges:
        a, b = corners[e]
        battlement(coll, a, b)


def gate(coll, cx, cy, rot):
    """A gate at (cx, cy) facing the plaza: two towers, an arch, bars, a pumpkin keystone, mist behind."""
    g = bpy.data.objects.new("GATE", None)
    coll.objects.link(g)
    parts = []
    parts.append(box("tower_l", (1.4, 1.4, 4.2), (-2.4, 0, 2.1), STONE2, coll, 0.1))
    parts.append(box("tower_r", (1.4, 1.4, 4.2), (2.4, 0, 2.1), STONE2, coll, 0.1))
    parts.append(box("arch", (6.2, 1.2, 0.9), (0, 0, 3.9), STONE, coll, 0.1))
    for i in range(7):
        parts.append(box("bar", (0.08, 0.08, 3.4), (-1.5 + i * 0.5, 0, 1.7), IRON, coll, 0))
    pk = cyl("pumpkin_key", 0.55, 0.9, (0, -0.1, 4.75), LEAF, coll, 16)
    parts.append(pk)
    parts.append(box("mist", (3.4, 0.2, 3.4), (0, 0.5, 1.7), MIST, coll, 0))
    for p in parts:
        p.parent = g
    g.location = (cx, cy, 0)
    g.rotation_euler = (0, 0, rot)
    return g


def import_prop(rel, at, rot, coll):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(REPO, rel))
    new = [o for o in bpy.data.objects if o not in before]
    root = bpy.data.objects.new("PROP_" + os.path.basename(rel), None)
    coll.objects.link(root)
    for o in new:
        for c in list(o.users_collection):
            c.objects.unlink(o)
        coll.objects.link(o)
        if o.parent is None:
            o.parent = root
    root.location = at
    root.rotation_euler = (0, 0, rot)
    return root


new = bpy.data.collections.new("Cube rooftop")
sc.collection.children.link(new)

# plaza: 4 cubes at (+-4, +-4)
plaza = {(-4, 4): "", (4, 4): "", (-4, -4): "", (4, -4): ""}
for (x, y) in plaza:
    cube(new, x, y, [], shade=(x + y) // 8 % 2)
# paths: north (0, 12), east (12, 0), west (-12, 0) one cube; south (start) (0, -12), (0, -20)
# the paths line up with the plaza seams, so each path cube is centred on a seam: shift by half a cube
cube(new, 0, 12, ["E", "W"])
cube(new, 12, 0, ["N", "S"])
cube(new, -12, 0, ["N", "S"])
cube(new, 0, -12, ["E", "W"])
cube(new, 0, -20, ["E", "W", "S"], shade=1)
# plaza outer edges where no path joins: battlements on the plaza's outside, leaving the path openings
for (a, b) in (((-8, 8), (-4, 8)), ((4, 8), (8, 8)), ((8, 8), (8, 4)), ((8, -4), (8, -8)), ((8, -8), (4, -8)),
               ((-4, -8), (-8, -8)), ((-8, -8), (-8, -4)), ((-8, 4), (-8, 8))):
    battlement(new, a, b)
# gates at the path ends (north, east, west); the start end (south) has none
gate(new, 0, 15.6, 0)
gate(new, 15.6, 0, math.pi / 2)
gate(new, -15.6, 0, -math.pi / 2)
# the dais + tree where the 4 plaza corners meet
cyl("dais_1", 3.2, 0.2, (0, 0, 0.1), STONE2, new)
cyl("dais_2", 2.5, 0.2, (0, 0, 0.3), STONE, new)
cyl("rune_ring", 2.2, 0.03, (0, 0, 0.41), GOLD, new)
cyl("trunk", 0.7, 4.0, (0, 0, 2.4), TRUNK, new, 12)
bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=3.2, location=(0, 0, 6.0))
crown = bpy.context.object
crown.data.materials.append(LEAF)
for c in list(crown.users_collection):
    c.objects.unlink(crown)
new.objects.link(crown)
# props for scale: barrels on rune rings, lamp posts, the start path
for (x, y) in ((5, 5), (-5, 5), (5, -5), (-5, -5)):
    cyl("barrel_ring", 0.75, 0.02, (x, y, 0.01), GOLD, new)
    import_prop("Assets/Models/Bedroom_Stuffs/Wood_Barrel_Jack.fbx", (x, y, 0), 0, new)
for (x, y) in ((7.2, 7.2), (-7.2, 7.2), (7.2, -7.2), (-7.2, -7.2), (3.2, -17), (-3.2, -17)):
    import_prop("Assets/Models/Bedroom_Stuffs/Rooftop_Pole_Light.fbx", (x, y, 0), 0, new)
for i in range(7):
    box("step", (1.0, 0.8, 0.06), (0.6 * (1 if i % 2 else -1) * 0.4, -23 + i * 1.6, 0.03), CREAM, new, 0.03)
import_prop("Assets/Models/Bedroom_Stuffs/Armour_Stand_Sign1.fbx", (2.0, -21, 0), 0.3, new)
import_prop("Assets/Models/Bedroom_Stuffs/Armour_Stand_Sign1.fbx", (-2.0, -18, 0), -0.3, new)
for i, x in enumerate((-2.2, 0, 2.2)):
    import_prop("Assets/Models/Bedroom_Stuffs/Stone_Cube2.fbx", (x * 1.4, -14.5, 0), 0, new)
box("person_1.75m", (0.45, 0.3, 1.75), (1.6, -19, 0.875), PERSON, new, 0.05)
box("person_plaza", (0.45, 0.3, 1.75), (4.5, -2.5, 0.875), PERSON, new, 0.05)

# today's rooftop, for the size comparison only
old = bpy.data.collections.new("Today's rooftop (for size)")
sc.collection.children.link(old)
import_prop("Assets/Models/Walls/Rooftop_Base_Z.fbx", (0, 0, 0), 0, old)

sc.render.engine = 'BLENDER_WORKBENCH'
sh = sc.display.shading
sh.light = 'STUDIO'
sh.color_type = 'MATERIAL'
sh.show_cavity = True
sh.show_shadows = True
sh.shadow_intensity = 0.4
sc.world = bpy.data.worlds.new("w")
sc.render.resolution_x, sc.render.resolution_y = 1600, 1000


def camera(name, loc, target, lens=24, ortho=None):
    c = bpy.data.objects.new(name, bpy.data.cameras.new(name))
    sc.collection.objects.link(c)
    c.location = loc
    c.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    c.data.lens = lens
    c.data.clip_end = 500
    if ortho:
        c.data.type = 'ORTHO'
        c.data.ortho_scale = ortho
    return c


def shoot(c, name, show_old=False, show_new=True):
    old.hide_render = not show_old
    new.hide_render = not show_new
    sc.camera = c
    sc.render.filepath = os.path.join(OUT, name)
    bpy.ops.render.render(write_still=True)


top = camera("top", (0, -4, 80), (0, -4, 0), ortho=60)
shoot(top, "1_top_new.png")
shoot(top, "1_top_today.png", show_old=True, show_new=False)
shoot(camera("bird", (30, -42, 34), (0, -2, 0), 26), "2_birds_eye.png")
shoot(camera("eye", (0, -23, 1.6), (0, 0, 2.4), 16), "3_eye_level_start.png")
shoot(camera("eye2", (-5.5, -5.5, 1.6), (6, 8, 2.0), 16), "4_eye_level_plaza.png")
old.hide_render = True
new.hide_render = False
_blend = os.path.join(REPO, "Exports", "Remodel", "Rooftop_Cubes_Blockout.blend")
if os.path.exists(_blend):   # the user models in this file now: never overwrite it
    _blend = os.path.join(REPO, "Exports", "Remodel", "Rooftop_Cubes_Blockout_generated.blend")
bpy.ops.wm.save_as_mainfile(filepath=_blend)
print("DONE")
