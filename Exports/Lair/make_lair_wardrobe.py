# Headless: blender -b --factory-startup --python make_lair_wardrobe.py
# The storage-room wardrobes as lair furniture (user 2026-11-25: "Aged wood + lair remodel"), remodelled from the
# user's own Shelf_01 (Assets/Models/Livingroom_Stuffs/Shelf_01.fbx, the storage room's MiniBedroom_Shelf_01; its
# wood keeps its UVs):
# - Lair_Wardrobe: iron hinge plates with knuckles on both doors and iron corner straps with rivets (front + sides,
#   top and bottom);
# - Lair_Wardrobe_Ajar: the same, with one door swung 30 degrees open onto a dark inside where green eyes shine.
# Material slots: 0 the wood (Unity: Lair_Wood_Aged_Mat), 1 iron (Bedroom_Metal_Mat, planar UVs), 2 the dark inside
# (the furniture atlas's black spot), 3 the eyes (the glow jar's material, on its glowing spot).
# Adds both objects to Exports/Lair/Lair_Story_Props.blend (my own file) and writes Assets/Models/Lair/<name>.fbx.
# Then in Unity: Tools > 429 Game > Lair > Set Up Lair Story Props.
import bpy, bmesh, os, math
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", ".."))
SRC = os.path.join(REPO, "Assets", "Models", "Livingroom_Stuffs", "Shelf_01.fbx")
OUT = os.path.join(REPO, "Assets", "Models", "Lair")
BLEND = os.path.join(HERE, "Lair_Story_Props.blend")
BLACK_UV = (0.8164, 0.0742)      # Bedroom_Set_1_Mat's flat black spot
GLOW_UV = (0.6616, 0.8885)       # a glowing spot of Lair_Glow_Jar_Mat's own texture, through its tiling and offset
IRON_UV_PER_M = 2.0              # Bedroom_Metal_Mat: one texture per half metre
OPEN_DEGREES = 55.0   # 30 hid the gap from the front

if os.path.exists(BLEND):
    bpy.ops.wm.open_mainfile(filepath=BLEND)
for o in list(bpy.data.objects):
    if o.name.startswith("Lair_Wardrobe") or o.name == "Shelf_01":
        bpy.data.objects.remove(o)
before = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=SRC)
ob = next(o for o in bpy.data.objects if o not in before and o.type == 'MESH')
for o in bpy.context.view_layer.objects:
    o.select_set(o == ob)
bpy.context.view_layer.objects.active = ob
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
ob.name = "Lair_Wardrobe"
ob.data.name = "Lair_Wardrobe"


def material(name, colour):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color = colour
    return m


while len(ob.data.materials) < 4:
    ob.data.materials.append(None)
ob.data.materials[1] = material("Lair_Iron", (0.15, 0.15, 0.16, 1))
ob.data.materials[2] = material("Lair_Dark", (0.01, 0.01, 0.01, 1))
ob.data.materials[3] = material("Lair_Eyes", (0.3, 1.0, 0.3, 1))

# The parts (loose pieces, front = -X): the carcass, two door handles, the drawer handle, the crown, two doors, the
# drawer and the inside faces.
bm = bmesh.new()
bm.from_mesh(ob.data)
xs = [v.co.x for v in bm.verts]; ys = [v.co.y for v in bm.verts]; zs = [v.co.z for v in bm.verts]
FRONT = min(v.co.x for v in bm.verts if abs(v.co.y) > 0.55 and v.co.z < 2.15)   # carcass front face
SIDE = max(abs(v.co.y) for v in bm.verts if v.co.z < 2.15)                       # carcass side faces
print("WARDROBE front x %.3f side y %.3f height %.3f" % (FRONT, SIDE, max(zs)))
uvl = bm.loops.layers.uv.active


def add_box(lo, hi, mat):
    """An axis-aligned box from lo to hi (outward faces), iron UVs projected on each face."""
    lo, hi = Vector(lo), Vector(hi)
    c = [Vector((x, y, z)) for x in (lo.x, hi.x) for y in (lo.y, hi.y) for z in (lo.z, hi.z)]
    vs = [bm.verts.new(p) for p in c]
    quads = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
    made = []
    for q in quads:
        f = bm.faces.new([vs[i] for i in q])
        f.material_index = mat
        made.append(f)
    bmesh.ops.recalc_face_normals(bm, faces=made)
    for f in made:
        n = f.normal
        for l in f.loops:
            p = l.vert.co
            if abs(n.x) > 0.5:
                u, v = p.y, p.z
            elif abs(n.y) > 0.5:
                u, v = p.x, p.z
            else:
                u, v = p.x, p.y
            l[uvl].uv = (u * IRON_UV_PER_M, v * IRON_UV_PER_M) if mat == 1 else (BLACK_UV if mat == 2 else GLOW_UV)
    return made


def add_cylinder(centre, radius, height, mat, segments=8):
    """A vertical cylinder (a hinge knuckle)."""
    made = []
    ring_lo, ring_hi = [], []
    for i in range(segments):
        a = i * 2 * math.pi / segments
        d = Vector((math.cos(a) * radius, math.sin(a) * radius, 0))
        ring_lo.append(bm.verts.new(Vector(centre) + d - Vector((0, 0, height / 2))))
        ring_hi.append(bm.verts.new(Vector(centre) + d + Vector((0, 0, height / 2))))
    for i in range(segments):
        j = (i + 1) % segments
        made.append(bm.faces.new((ring_lo[i], ring_lo[j], ring_hi[j], ring_hi[i])))
    made.append(bm.faces.new(list(reversed(ring_lo))))
    made.append(bm.faces.new(ring_hi))
    for f in made:
        f.material_index = mat
    bmesh.ops.recalc_face_normals(bm, faces=made)
    for f in made:
        for l in f.loops:
            p = l.vert.co
            l[uvl].uv = (math.atan2(p.y - centre[1], p.x - centre[0]) * 0.05, p.z * IRON_UV_PER_M)
    return made


def add_dome(centre, radius, depth, mat):
    """A flattened half ball facing -X (an eye)."""
    made = []
    rings, segs = 3, 10
    tip = bm.verts.new(Vector(centre) + Vector((-depth, 0, 0)))
    prev = None
    rows = []
    for r in range(1, rings + 1):
        t = r / rings
        ang = t * math.pi / 2
        row = []
        for s in range(segs):
            a = s * 2 * math.pi / segs
            row.append(bm.verts.new(Vector(centre) + Vector((-depth * math.cos(ang), math.sin(ang) * radius * math.cos(a), math.sin(ang) * radius * math.sin(a)))))
        rows.append(row)
    for s in range(segs):
        made.append(bm.faces.new((tip, rows[0][s], rows[0][(s + 1) % segs])))
    for r in range(rings - 1):
        for s in range(segs):
            made.append(bm.faces.new((rows[r][s], rows[r + 1][s], rows[r + 1][(s + 1) % segs], rows[r][(s + 1) % segs])))
    for f in made:
        f.material_index = mat
        for l in f.loops:
            l[uvl].uv = GLOW_UV
    bmesh.ops.recalc_face_normals(bm, faces=made)
    for f in made:   # all must face the room (-X)
        if f.normal.x > 0:
            f.normal_flip()
    return made


T = 0.006           # iron thickness
W = 0.034           # strap width
ARM = 0.13          # strap arm length
x_out = FRONT - T   # in front of the carcass face
# Iron corner straps: an L on the front at each corner, and an arm round onto the side.
for sy in (-1, 1):
    for (z_edge, up) in ((0.0, 1), (2.159, -1)):
        y_edge = sy * SIDE
        # along the front's top/bottom edge, inward from the side
        y0, y1 = sorted((y_edge, y_edge - sy * ARM))
        z0, z1 = sorted((z_edge, z_edge + up * W))
        add_box((x_out, y0, z0), (FRONT + 0.001, y1, z1), 1)
        # up/down the front's side edge
        y0, y1 = sorted((y_edge, y_edge - sy * W))
        z0, z1 = sorted((z_edge, z_edge + up * ARM))
        add_box((x_out, y0, z0), (FRONT + 0.001, y1, z1), 1)
        # round the corner onto the side
        y0, y1 = sorted((y_edge - sy * 0.001, y_edge + sy * T))
        z0, z1 = sorted((z_edge, z_edge + up * W))
        add_box((FRONT - T, y0, z0), (FRONT + ARM, y1, z1), 1)
        # rivets at the arms' ends and the corner
        for (ry, rz) in ((y_edge - sy * (ARM - 0.015), z_edge + up * W / 2), (y_edge - sy * W / 2, z_edge + up * (ARM - 0.015)),
                         (y_edge - sy * W / 2, z_edge + up * W / 2)):
            add_box((x_out - 0.006, ry - 0.007, rz - 0.007), (x_out, ry + 0.007, rz + 0.007), 1)
# Hinges on the stiles beside each door: a plate and a knuckle at the door's outer edge.
DOOR_EDGE = 0.505
for sy in (-1, 1):
    for zc in (0.82, 1.93):
        y0, y1 = sorted((sy * (DOOR_EDGE + 0.004), sy * (DOOR_EDGE + 0.07)))
        add_box((x_out, y0, zc - 0.045), (FRONT + 0.001, y1, zc + 0.045), 1)
        add_cylinder((FRONT - 0.008, sy * DOOR_EDGE, zc), 0.009, 0.1, 1)

bm.to_mesh(ob.data)
ob.data.update()


def export(o, name):
    for x in bpy.context.view_layer.objects:
        x.select_set(x == o)
    bpy.context.view_layer.objects.active = o
    keep = o.location.copy()
    o.location = (0, 0, 0)
    path = os.path.join(OUT, name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'},
                             apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
                             axis_forward='-Z', axis_up='Y', mesh_smooth_type='OFF', add_leaf_bones=False,
                             bake_anim=False, path_mode='AUTO', use_mesh_modifiers=True)
    o.location = keep
    print("EXPORTED", path, "faces", len(o.data.polygons))


os.makedirs(OUT, exist_ok=True)
export(ob, "Lair_Wardrobe")

# The open one: a copy with the +Y door (and its handle) swung out on its outer edge, a dark inside, eyes.
ajar = ob.copy()
ajar.data = ob.data.copy()
ajar.name = ajar.data.name = "Lair_Wardrobe_Ajar"
bpy.context.scene.collection.objects.link(ajar)
bm = bmesh.new()
bm.from_mesh(ajar.data)
uvl = bm.loops.layers.uv.active
# The loose pieces that lie wholly inside the +Y door's box or its handle's box (the door and its handle).
seen = set()
pieces = set()
for start in bm.verts:
    if start in seen:
        continue
    piece, stack = [], [start]
    while stack:
        v = stack.pop()
        if v in seen:
            continue
        seen.add(v)
        piece.append(v)
        for e in v.link_edges:
            stack.append(e.other_vert(v))
    lo = Vector((min(v.co.x for v in piece), min(v.co.y for v in piece), min(v.co.z for v in piece)))
    hi = Vector((max(v.co.x for v in piece), max(v.co.y for v in piece), max(v.co.z for v in piece)))
    is_door = lo.x > FRONT and hi.x < -0.2 and lo.y > -0.001 and hi.y < DOOR_EDGE + 0.002 and lo.z > 0.64 and hi.z < 2.11
    is_handle = lo.x > FRONT - 0.08 and hi.x < -0.25 and lo.y > -0.001 and hi.y < 0.1 and lo.z > 1.25 and hi.z < 1.42
    if is_door or is_handle:
        print("AJAR piece", "door" if is_door else "handle", len(piece), "verts", tuple(round(c, 3) for c in lo), tuple(round(c, 3) for c in hi))
        pieces.update(piece)
pivot = Vector((FRONT + 0.011, DOOR_EDGE, 0))
turn = Matrix.Rotation(math.radians(-OPEN_DEGREES), 4, 'Z')
for v in pieces:
    p = v.co - pivot
    p = turn @ Vector((p.x, p.y, 0)) + Vector((0, 0, p.z))
    v.co = pivot + Vector((p.x, p.y, 0)) + Vector((0, 0, p.z - pivot.z))
print("AJAR door verts moved", len(pieces))
add_box((-0.233, -0.5, 0.656), (0.262, 0.5, 2.095), 2)
# Eyes in the dark: a spider's (two big, two small above, two tiny below) and a smaller pair lower down.
xe = -0.234
for (y, z, r) in ((0.1, 1.52, 0.034), (0.19, 1.52, 0.034), (0.06, 1.585, 0.02), (0.23, 1.585, 0.02),
                  (0.12, 1.455, 0.012), (0.17, 1.455, 0.012), (0.07, 0.98, 0.02), (0.13, 0.99, 0.02)):
    add_dome((xe, y, z), r, r * 0.45, 3)
bm.to_mesh(ajar.data)
ajar.data.update()
ajar.location = (1.0, 0, 0)
export(ajar, "Lair_Wardrobe_Ajar")
ob.location = (3.0, 0, 0)
ajar.location = (4.0, 0, 0)
bpy.ops.wm.save_as_mainfile(filepath=BLEND)
print("SAVED", BLEND)
