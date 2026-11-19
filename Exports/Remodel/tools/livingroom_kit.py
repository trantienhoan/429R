"""Living room + playroom kit (2026-11-19). User: "let's continue with the kit of the livingroom and the playroom ... create
a kit of livingroom and playroom to replace the current livingroom_Base".

The same rules as the bedroom kit (tools/bedroom_kit.py, the user's clean style, see EXPORTS/Remodel/BEDROOM_KIT_PLAN.md):
3 m grid with 1.5 m halves, 3 m high, pivot at the inner face's bottom-left (a piece runs along +X, its inside is -Y, its
outside +Y); each wall ONE closed slab, cut with the fewest quads round an opening; the user's own frames, doors and roof
parts taken as they are from Livingroom_Base_Z in Assets/Blender/Bedroom_Walls.blend (rest pose = the end of COME).
Those parts are found by place (their islands' bounds), so a changed model is noted and left out, never mangled.
  - Wallpaper: Livingroom_Wallpaper_Spooky (wood panelling painted at the bottom, a dark band at the top), once up each
    wall (v 0 at the floor, 1 at the top) and repeating every 3 m (4 panels), so pieces meet without a seam.
  - No baseboards or corner posts: the old living room had none (the user painted the panelling into the paper).
  - Slabs 0.31 m thick, like the user's living room walls.
Pieces: walls 3 m / 1.5 m; LR_Kit_Door (the user's 1.40 x 2.08 m doorway frame, the same as the bedroom's Wall05);
LR_Kit_Door_Big (the 2.47 x 2.08 m doorway of the big double door); leaves that stand in them (LR_Kit_Leaf_X: the
X-braced chained door of the old north wall; LR_Kit_Leaf_Double: the big double door; LR_Kit_Leaf_Horseshoe: the
breakable shortcut door, the bedroom's horseshoe door with its chain); floor tile; the old living room's roof slab and
lamp frame. The playroom's breakable ceiling and brick walls are game objects with their own break logic: the room
build reuses them as they are (see LIVINGROOM_KIT_PLAN.md).
Sample rooms: the living room (9 x 9 m) and the playroom (the old cross: a 7.5 m square with four 1.5 m wide, 3 m long
arms; the game scales it 1.5x as now).

Run (makes the file, renders): blender -b --factory-startup --python Exports/Remodel/tools/livingroom_kit.py --
  <out.blend> [renders_dir] [--force]     (an existing <out.blend> is kept: the new one is saved as *_generated.blend)
Live (in the user's open kit file: adds the LR_Kit_* pieces it lacks, never touches the ones there unless named in
LIVINGROOM_KIT_REBUILD, saves nothing):
  exec(open(this).read(), {"LIVINGROOM_KIT_LIVE": True, "__file__": this, "LIVINGROOM_KIT_REBUILD": []})"""
import bpy, bmesh, sys, os, math
from mathutils import Vector, Matrix

LIVE = bool(globals().get("LIVINGROOM_KIT_LIVE", False))
REBUILD = set(globals().get("LIVINGROOM_KIT_REBUILD", ()))
ARGS = sys.argv[sys.argv.index("--") + 1:] if ("--" in sys.argv and not LIVE) else []
POS = [a for a in ARGS if not a.startswith("--")]
OUT = POS[0] if POS else None
RENDERS = POS[1] if len(POS) > 1 else None
REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
TEX = os.path.join(REPO, "Assets", "M_Textures")
BEDROOM_WALLS = os.path.join(REPO, "Assets", "Blender", "Bedroom_Walls.blend")
SOURCE = "Livingroom_Base_Z"

if not LIVE:
    bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene

H = 3.0               # wall height (11-16 decision)
T = 0.31              # the user's living room slabs
FULL, HALF = 3.0, 1.5
FLOOR_T = 0.12
PAPER_PER_M = 1.0 / 3.0
WOOD_PER_M = 1.0 / 3.0
WALL_I, WOOD_I, METAL_I = 0, 1, 2
PART_SLAB, PART_FLOOR, PART_USER = 1, 3, 9
TOL = 0.025

# The user's parts in Livingroom_Base_Z (rest pose, Blender coordinates). "islands": the exact bounds of the parts' islands
# (connected faces); "within": every island inside this box but the frame's. "turn": from the old wall's frame onto a
# kit piece's (x along, -y inside, z up).
# North wall (Wall_08): inner face y = -4.28, inside +y; its doorway x -3.35..-1.95 (1.40 m), to 2.08 m.
FRAME = dict(islands=[((-3.35, -4.33, 0.0), (-1.95, -4.18, 2.08))],
             turn=lambda c: (-(c.x + 2.65) + FULL / 2.0, -(c.y + 4.28), c.z))
DOOR_OPENING = (1.40, 2.08)
LEAF_X = dict(within=((-3.40, -4.50, -0.01), (-1.90, -4.00, 2.10)), skip=FRAME, turn=FRAME["turn"])
# East wall (Wall_05): inner face x = -4.27, inside +x; the big doorway y -3.08..-0.61 (2.47 m), to 2.08 m.
BIG_FRAME = dict(islands=[((-4.32, -3.08, 0.0), (-4.18, -0.61, 2.08))],
                 turn=lambda c: (c.y + 1.845 + FULL / 2.0, -(c.x + 4.27), c.z))
BIG_OPENING = (2.47, 2.08)
LEAF_DOUBLE = dict(within=((-4.45, -3.10, -0.01), (-4.00, -0.60, 2.00)), skip=BIG_FRAME, turn=BIG_FRAME["turn"])
# The roof (Rooftop_LivingRoom): the wood slab and the lamp frame, centred, moved down so the slab's underside is the
# ceiling (the walls' tops, z = H). Unity scales the slab to each room (the old roof covered the old 8.55 m room).
ROOF_DROP = 3.58 - H
ROOF = dict(islands=[((-4.63, -4.63, 3.58), (4.63, 4.63, 3.90))], turn=lambda c: (c.x, c.y, c.z - ROOF_DROP))
LAMP = dict(islands=[((-0.63, -0.63, 3.50), (0.63, 0.63, 3.58))], turn=lambda c: (c.x, c.y, c.z - ROOF_DROP))
# The breakable shortcut door of the old south doorway: the bedroom's horseshoe door model (whole, chain and all), hinged
# at x = 0, boards on +y; turned to face the room, centred in the doorway, its planks just behind the frame.
LEAF_HORSESHOE = "Bedroom_Base_Z_Door"
FRAME_BACK = 0.047


# ----------------------------------------------------------------------------------------------------------------- materials
def image(rel, colour=True):
    img = bpy.data.images.load(os.path.join(TEX, rel), check_existing=True)
    if not colour:
        img.colorspace_settings.name = "Non-Color"
    return img


def material(name, albedo, normal=None, rough=None, rough_value=0.8):
    if name in bpy.data.materials:
        return bpy.data.materials[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = image(albedo)
    tex.location = (-500, 250)
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = rough_value
    if rough:
        rt = nt.nodes.new("ShaderNodeTexImage")
        rt.image = image(rough, colour=False)
        rt.location = (-500, -50)
        nt.links.new(rt.outputs["Color"], bsdf.inputs["Roughness"])
    if normal:
        nt_ = nt.nodes.new("ShaderNodeTexImage")
        nt_.image = image(normal, colour=False)
        nt_.location = (-800, -300)
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nm.location = (-300, -300)
        nt.links.new(nt_.outputs["Color"], nm.inputs["Color"])
        nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    return m


WALL = material("Livingroom_Wall", "Combos/Wall_Stylize_Set_1/Livingroom_Wallpaper_Spooky.png",
                normal="Combos/Wall_Stylize_Set_1/Livingroom_Wallpaper_Spooky_Normal.png",
                rough="Combos/Wall_Stylize_Set_1/wall_not_clean_roughness.png")
WOOD = material("Bedroom_Wood_Floor", "Combos/Wood_Floor_Stylize_Set_2/wood_floor3_albedo.png",
                normal="Combos/Wood_Floor_Stylize_Set_2/wood_floor3_normal.png", rough_value=0.9)
METAL = material("Bedroom_Metal", "Combos/Metal_Stylize_Set_2/medieval_metal_albedo_set2.png",
                 normal="Combos/Metal_Stylize_Set_2/medieval_metal_normal_set2.png",
                 rough="Combos/Metal_Stylize_Set_2/medieval_metal_roughness_set2.png", rough_value=0.5)
MATS = [WALL, WOOD, METAL]


def slot_for(material_name):
    """The kit's slot for one of the old room's materials: its wallpaper is called Brick_White in Bedroom_Walls.blend."""
    if "Metal" in material_name:
        return METAL_I
    if "Brick" in material_name or "Wall" in material_name:
        return WALL_I
    return WOOD_I


# ----------------------------------------------------------------------------------------------------------------- the user's parts
def tidy(before, me=None, ob=None):
    """Removes only what a load brought in (never the user's own unused data)."""
    if ob is not None:
        bpy.data.objects.remove(ob)
    if me is not None:
        bpy.data.meshes.remove(me)
    for m in [m for m in bpy.data.materials if m not in before[1] and m.users == 0]:
        bpy.data.materials.remove(m)
    for im in [im for im in bpy.data.images if im not in before[2] and im.users == 0]:
        bpy.data.images.remove(im)


_source = {}


def source_islands():
    """Livingroom_Base_Z's islands, read once: [(faces [(corners [(co, uv, vertex index)], slot)], lo, hi)]."""
    if "islands" in _source:
        return _source["islands"]
    _source["islands"] = None
    if not os.path.exists(BEDROOM_WALLS):
        print("NOTE: no", BEDROOM_WALLS)
        return None
    before = (set(bpy.data.meshes), set(bpy.data.materials), set(bpy.data.images))
    with bpy.data.libraries.load(BEDROOM_WALLS, link=False) as (src, dst):
        dst.meshes = [SOURCE] if SOURCE in src.meshes else []
    me = next((m for m in bpy.data.meshes if m not in before[0]), None)
    try:
        if me is None or not me.uv_layers:
            print("NOTE: no", SOURCE, "mesh in", BEDROOM_WALLS)
            return None
        uvl = me.uv_layers.active.data
        parent = list(range(len(me.vertices)))

        def find(i):
            while parent[i] != i:
                parent[i] = parent[parent[i]]
                i = parent[i]
            return i
        for e in me.edges:
            a, b = find(e.vertices[0]), find(e.vertices[1])
            if a != b:
                parent[a] = b
        groups = {}
        for p in me.polygons:
            groups.setdefault(find(p.vertices[0]), []).append(p)
        out = []
        for polys in groups.values():
            faces = []
            for p in polys:
                corners = [(me.vertices[me.loops[li].vertex_index].co.copy(), tuple(uvl[li].uv), me.loops[li].vertex_index)
                           for li in p.loop_indices]
                faces.append((corners, slot_for(me.materials[p.material_index].name if me.materials else "")))
            cos = [c[0] for f in faces for c in f[0]]
            lo = tuple(min(c[k] for c in cos) for k in range(3))
            hi = tuple(max(c[k] for c in cos) for k in range(3))
            out.append((faces, lo, hi))
        _source["islands"] = out
        return out
    finally:
        tidy(before, me)


def same_box(lo, hi, box):
    return all(abs(a - b) <= TOL for a, b in zip(lo + hi, box[0] + box[1]))


def inside_box(lo, hi, box):
    return all(box[0][k] - TOL <= lo[k] and hi[k] <= box[1][k] + TOL for k in range(3))


def user_part(spec, name):
    """The user's faces for a part, turned onto the kit piece: [(corners [(co, uv, vertex index)], slot)]."""
    isl = source_islands()
    if not isl:
        return None
    if "islands" in spec:
        chosen = [i for i in isl if any(same_box(i[1], i[2], b) for b in spec["islands"])]
        if len(chosen) < len(spec["islands"]):
            print("NOTE: %s isn't in %s as it was (found %d of %d parts): left out" % (name, SOURCE, len(chosen), len(spec["islands"])))
            return None
    else:
        skip = spec.get("skip", {}).get("islands", [])
        chosen = [i for i in isl if inside_box(i[1], i[2], spec["within"]) and not any(same_box(i[1], i[2], b) for b in skip)]
        if not chosen:
            print("NOTE: nothing of %s in %s: left out" % (name, SOURCE))
            return None
    turn = spec["turn"]
    return [([(Vector(turn(co)), uv, vi) for co, uv, vi in corners], slot) for faces, _, _ in chosen for corners, slot in faces]


def leaf_faces(object_name, opening_width):
    """A whole door model from Bedroom_Walls.blend, turned into a kit doorway as the bedroom kit does it."""
    if not os.path.exists(BEDROOM_WALLS):
        return None
    before = (set(bpy.data.meshes), set(bpy.data.materials), set(bpy.data.images))
    before_obs = set(bpy.data.objects)
    with bpy.data.libraries.load(BEDROOM_WALLS, link=False) as (src, dst):
        dst.objects = [object_name] if object_name in src.objects else []
    ob = next((o for o in bpy.data.objects if o not in before_obs), None)
    if ob is None or ob.type != "MESH" or not ob.data.uv_layers:
        print("NOTE: no door model", object_name)
        if ob is not None:
            tidy(before, ob.data if ob.type == "MESH" else None, ob)
        return None
    me = ob.data
    uvl = me.uv_layers.active.data
    cos = [v.co for v in me.vertices]
    x0, x1 = min(c.x for c in cos), max(c.x for c in cos)
    z0 = min(c.z for c in cos)
    parent = list(range(len(me.vertices)))

    def find(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i
    for e in me.edges:
        a, b = find(e.vertices[0]), find(e.vertices[1])
        if a != b:
            parent[a] = b
    groups = {}
    for p in me.polygons:
        if "Metal" in me.materials[p.material_index].name:
            continue
        groups.setdefault(find(p.vertices[0]), set()).update(p.vertices)
    planks = max(groups.values(), key=len)
    py1 = max(me.vertices[v].co.y for v in planks)
    turn = lambda c: Vector((-c.x + FULL / 2.0 + (x0 + x1) / 2.0, -c.y + FRAME_BACK + py1, c.z - z0))
    faces = []
    for p in me.polygons:
        mat = slot_for(me.materials[p.material_index].name)
        faces.append(([(turn(me.vertices[me.loops[li].vertex_index].co), tuple(uvl[li].uv), me.loops[li].vertex_index) for li in p.loop_indices], mat))
    tidy(before, me, ob)
    return faces


# ----------------------------------------------------------------------------------------------------------------- geometry
def new_bm():
    bm = bmesh.new()
    bm.faces.layers.int.new("part")
    bm.loops.layers.uv.new("UVMap")
    return bm


def faces_from(bm, quads, mat, tag):
    part = bm.faces.layers.int["part"]
    made = []
    pool = {}
    for q in quads:
        vs = []
        for co in q:
            key = tuple(round(c, 6) for c in co)
            if key not in pool:
                pool[key] = bm.verts.new(co)
            vs.append(pool[key])
        f = bm.faces.new(vs)
        f.material_index = mat
        f[part] = tag
        made.append(f)
    bmesh.ops.recalc_face_normals(bm, faces=made)
    return made


def box(bm, lo, hi, mat, tag):
    x0, y0, z0 = lo
    x1, y1, z1 = hi
    quads = [
        [(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)],
        [(x0, y1, z0), (x0, y1, z1), (x1, y1, z1), (x1, y1, z0)],
        [(x0, y0, z0), (x0, y0, z1), (x0, y1, z1), (x0, y1, z0)],
        [(x1, y0, z0), (x1, y1, z0), (x1, y1, z1), (x1, y0, z1)],
        [(x0, y0, z0), (x0, y1, z0), (x1, y1, z0), (x1, y0, z0)],
        [(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)],
    ]
    return faces_from(bm, quads, mat, tag)


def slab(bm, x0, x1, hole=None):
    """One closed wallpaper slab from x0 to x1 (inner face y = 0, outer y = T). Hole (a, b, top): a doorway from the
    floor; the faces round it run straight to the slab's corners (the user's way: 14 quads)."""
    if hole is None:
        return box(bm, (x0, 0.0, 0.0), (x1, T, H), WALL_I, PART_SLAB)
    a, b, z1 = hole
    quads = []
    for y in (0.0, T):
        p = lambda x, z: (x, y, z)
        quads += [[p(x0, 0), p(a, 0), p(a, z1), p(x0, H)],
                  [p(x0, H), p(a, z1), p(b, z1), p(x1, H)],
                  [p(b, 0), p(x1, 0), p(x1, H), p(b, z1)]]
    f, k = (lambda x, z: (x, 0.0, z)), (lambda x, z: (x, T, z))
    quads += [[f(a, 0), k(a, 0), k(a, z1), f(a, z1)],
              [f(b, 0), f(b, z1), k(b, z1), k(b, 0)],
              [f(a, z1), k(a, z1), k(b, z1), f(b, z1)],
              [f(x0, 0), k(x0, 0), k(a, 0), f(a, 0)],
              [f(b, 0), k(b, 0), k(x1, 0), f(x1, 0)],
              [f(x0, H), f(x1, H), k(x1, H), k(x0, H)],
              [f(x0, 0), f(x0, H), k(x0, H), k(x0, 0)],
              [f(x1, 0), k(x1, 0), k(x1, H), f(x1, H)]]
    return faces_from(bm, quads, WALL_I, PART_SLAB)


def add_user(bm, faces):
    """The user's faces as they are; vertices shared exactly where the user's mesh shares them."""
    part = bm.faces.layers.int["part"]
    uv = bm.loops.layers.uv.active
    pool = {}
    for corners, mat in faces:
        vs = []
        for co, _, vi in corners:
            if vi not in pool:
                pool[vi] = bm.verts.new(co)
            vs.append(pool[vi])
        if len(set(vs)) < len(vs):
            continue
        try:
            f = bm.faces.new(vs)
        except ValueError:
            continue
        f.material_index = mat
        f[part] = PART_USER
        for loop, (_, u, _) in zip(f.loops, corners):
            loop[uv].uv = u


def project_uvs(bm):
    """The wallpaper once up the wall, repeating every 3 m along it; wood with the grain along. The user's own faces
    keep theirs."""
    part = bm.faces.layers.int["part"]
    uv = bm.loops.layers.uv.active
    for f in bm.faces:
        if f[part] == PART_USER:
            continue
        n = f.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        for l in f.loops:
            co = l.vert.co
            if f.material_index == WALL_I:
                if ax == 2:
                    l[uv].uv = (co.x * PAPER_PER_M, co.y * PAPER_PER_M)
                else:
                    along = co.x if ax == 1 else co.y
                    l[uv].uv = (along * PAPER_PER_M, co.z / H)
            elif f[part] == PART_FLOOR or ax == 2:
                l[uv].uv = (co.x * WOOD_PER_M, co.y * WOOD_PER_M)
            else:
                along = co.x if ax == 1 else co.y
                l[uv].uv = (along * WOOD_PER_M, co.z * WOOD_PER_M)


def finish(bm, name, coll):
    """Write the piece: a new object; in a live file, over an existing piece's mesh only when asked (REBUILD)."""
    ob = bpy.data.objects.get(name)
    if LIVE and ob is not None and name not in REBUILD:
        bm.free()
        print("kept as it is in the file:", name)
        return ob
    project_uvs(bm)
    slots = [f.material_index for f in bm.faces]
    if ob is not None and ob.type == "MESH":
        me = ob.data
        bm.to_mesh(me)
        me.polygons.foreach_set("material_index", slots)
        if [m.name for m in me.materials] != [m.name for m in MATS]:
            me.materials.clear()
            for m in MATS:
                me.materials.append(m)
    else:
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)
        for m in MATS:
            me.materials.append(m)
        me.polygons.foreach_set("material_index", slots)
        ob = bpy.data.objects.new(name, me)
        coll.objects.link(ob)
    bm.free()
    for p in me.polygons:
        p.use_smooth = False
    me.update()
    return ob


# ----------------------------------------------------------------------------------------------------------------- pieces
def piece_wall(name, w, coll):
    bm = new_bm()
    slab(bm, 0.0, w)
    return finish(bm, name, coll)


def piece_door(name, coll, frame, opening):
    w, top = opening
    a, b = FULL / 2 - w / 2, FULL / 2 + w / 2
    bm = new_bm()
    slab(bm, 0.0, FULL, (a, b, top))
    faces = user_part(frame, name + " frame")
    if faces:
        add_user(bm, faces)
    return finish(bm, name, coll)


def piece_user(name, coll, spec):
    faces = user_part(spec, name)
    if not faces:
        return None
    bm = new_bm()
    add_user(bm, faces)
    return finish(bm, name, coll)


def piece_leaf_model(name, coll, model):
    faces = leaf_faces(model, DOOR_OPENING[0])
    if not faces:
        return None
    bm = new_bm()
    add_user(bm, faces)
    return finish(bm, name, coll)


def piece_floor(name, coll):
    bm = new_bm()
    box(bm, (0.0, 0.0, -FLOOR_T), (FULL, FULL, 0.0), WOOD_I, PART_FLOOR)
    return finish(bm, name, coll)


kit = bpy.data.collections.get("Livingroom_Kit")
if kit is None:
    kit = bpy.data.collections.new("Livingroom_Kit")
    sc.collection.children.link(kit)
existed = {o.name for o in bpy.data.objects}
pieces = {
    "LR_Kit_Wall_3m": piece_wall("LR_Kit_Wall_3m", FULL, kit),
    "LR_Kit_Wall_1p5m": piece_wall("LR_Kit_Wall_1p5m", HALF, kit),
    "LR_Kit_Door": piece_door("LR_Kit_Door", kit, FRAME, DOOR_OPENING),
    "LR_Kit_Door_Big": piece_door("LR_Kit_Door_Big", kit, BIG_FRAME, BIG_OPENING),
    "LR_Kit_Leaf_X": piece_user("LR_Kit_Leaf_X", kit, LEAF_X),
    "LR_Kit_Leaf_Double": piece_user("LR_Kit_Leaf_Double", kit, LEAF_DOUBLE),
    "LR_Kit_Leaf_Horseshoe": piece_leaf_model("LR_Kit_Leaf_Horseshoe", kit, LEAF_HORSESHOE),
    "LR_Kit_Floor_3m": piece_floor("LR_Kit_Floor_3m", kit),
    "LR_Kit_Roof": piece_user("LR_Kit_Roof", kit, ROOF),
    "LR_Kit_Lamp": piece_user("LR_Kit_Lamp", kit, LAMP),
}
pieces = {k: v for k, v in pieces.items() if v is not None}
for name, ob in pieces.items():
    me = ob.data
    print("%-22s faces %4d (quads %4d) tris %5d" % (name, len(me.polygons), sum(1 for p in me.polygons if len(p.vertices) == 4),
                                                   sum(len(p.vertices) - 2 for p in me.polygons)))
# The catalogue row: pieces side by side, 4 m apart (live: new ones at the end of the row); leaves in their doorways.
if LIVE:
    kept = [ob for name, ob in pieces.items() if name in existed]
    x = max((ob.location.x for ob in kept), default=-20.0) + 4.0
    for name, ob in pieces.items():
        if name not in existed:
            ob.location = (x, -16.0, 0.0)
            x += 4.0
else:
    for i, ob in enumerate(pieces.values()):
        ob.location = (i * 4.0 - 18.0, -16.0, 0.0)
    for leaf, doorway in (("LR_Kit_Leaf_X", "LR_Kit_Door"), ("LR_Kit_Leaf_Double", "LR_Kit_Door_Big")):
        if leaf in pieces and doorway in pieces:
            pieces[leaf].location = pieces[doorway].location.copy()

if not LIVE:
    rooms = bpy.data.collections.new("Sample_Rooms")
    sc.collection.children.link(rooms)

    def place(name, coll, a, b):
        """A piece with its inner face from a to b (Blender XY; the room on the right of a -> b), pivot at a."""
        ob = bpy.data.objects.new(name + "_inst", pieces[name].data)
        coll.objects.link(ob)
        d = (Vector(b) - Vector(a)).normalized()
        ob.matrix_world = Matrix(((d.x, -d.y, 0, a[0]), (d.y, d.x, 0, a[1]), (0, 0, 1, 0), (0, 0, 0, 1)))
        return ob

    def floor(coll, x0, y0, x1, y1):
        x = x0
        while x < x1 - 0.01:
            y = y0
            tw = min(FULL, x1 - x)
            while y < y1 - 0.01:
                td = min(FULL, y1 - y)
                fo = bpy.data.objects.new("LR_Kit_Floor_3m_inst", pieces["LR_Kit_Floor_3m"].data)
                coll.objects.link(fo)
                fo.location = (x, y, 0.0)
                fo.scale = (tw / FULL, td / FULL, 1.0)
                y += td
            x += tw

    def walk(coll, points, pieces_per_side):
        """Walls round an outline, clockwise seen from above (the room on the right). Each side's pieces in order; an
        entry "Doorway+Leaf" puts the leaf in the doorway; no pieces: the side is the end of the wall before it."""
        for (a, b), names in zip(zip(points, points[1:] + points[:1]), pieces_per_side):
            a, b = Vector(a), Vector(b)
            d = (b - a).normalized()
            run = 0.0
            for entry in names:
                parts = entry.split("+")
                length = HALF if parts[0].endswith("1p5m") else FULL
                for nm in parts:
                    place(nm, coll, a + d * run, a + d * (run + length))
                run += length

    # The living room, 9 x 9 m, centred at the origin. Clockwise from the north-west inner corner (Unity north = Blender
    # -Y): the X door near the north wall's east end, the big door in the east wall at Unity z 0..3, the horseshoe
    # shortcut door near the south wall's west end (as in the old room).
    q = 4.5
    lr = [(q, -q), (-q, -q), (-q, q), (q, q)]
    walk(rooms, lr, [
        ["LR_Kit_Wall_3m", "LR_Kit_Wall_3m", "LR_Kit_Door+LR_Kit_Leaf_X"],                       # north (Unity +Z)
        ["LR_Kit_Wall_1p5m", "LR_Kit_Door_Big+LR_Kit_Leaf_Double", "LR_Kit_Wall_3m", "LR_Kit_Wall_1p5m"],  # east (Unity +X)
        ["LR_Kit_Wall_3m", "LR_Kit_Wall_3m", "LR_Kit_Door+LR_Kit_Leaf_Horseshoe"],               # south
        ["LR_Kit_Wall_3m", "LR_Kit_Wall_3m", "LR_Kit_Wall_3m"],                                  # west
    ])
    floor(rooms, -q, -q, q, q)
    roof_scale = (q + T) / 4.63
    for nm in ("LR_Kit_Roof", "LR_Kit_Lamp"):
        if nm in pieces:
            ob = bpy.data.objects.new(nm + "_inst", pieces[nm].data)
            rooms.objects.link(ob)
            if nm == "LR_Kit_Roof":
                ob.scale = (roof_scale, roof_scale, 1.0)
            ob.hide_render = False

    # The playroom (built at 1x; the game scales it 1.5x), centred 22 m to the east: a 7.5 m square, an arm 1.5 m wide
    # in the middle of each side (the old cross: the arms end in the breakable brick walls). Where an arm leaves the
    # square the corner sticks into the room: the arm's walls start behind the square's wall (its end is that corner's
    # face), so no two faces lie on each other. The arms are 3 m of wall + that, 3.31 m.
    px = 22.0
    c, w = 3.75, 0.75
    s = c + T
    e = s + FULL
    outline = [(-w, e), (w, e), (w, s), (w, c), (c, c), (c, w), (s, w), (e, w), (e, -w), (s, -w), (c, -w), (c, -c),
               (w, -c), (w, -s), (w, -e), (-w, -e), (-w, -s), (-w, -c), (-c, -c), (-c, -w), (-s, -w), (-e, -w),
               (-e, w), (-s, w), (-c, w), (-c, c), (-w, c), (-w, s)]
    outline = [(px + x, y) for x, y in outline]
    sides = []
    for (a, b) in zip(outline, outline[1:] + outline[:1]):
        length = (Vector(b) - Vector(a)).length
        sides.append([] if length < 1.0 else ["LR_Kit_Wall_1p5m"] if length < 2.0 else ["LR_Kit_Wall_3m"])
    walk(rooms, outline, sides)
    floor(rooms, px - c, -c, px + c, c)
    floor(rooms, px - w, c, px + w, e)
    floor(rooms, px - w, -e, px + w, -c)
    floor(rooms, px + c, -w, px + e, w)
    floor(rooms, px - e, -w, px - c, w)

    for nm, energy, rot in (("Sun", 3.0, (50, 10, 30)), ("Fill", 1.4, (60, -10, 215))):
        light = bpy.data.objects.new(nm, bpy.data.lights.new(nm, "SUN"))
        light.data.energy = energy
        light.rotation_euler = tuple(math.radians(a) for a in rot)
        sc.collection.objects.link(light)
    for nm, loc in (("Lamp_LR", (0.0, 0.0, 2.6)), ("Lamp_Play", (px, 0.0, 2.6))):
        light = bpy.data.objects.new(nm, bpy.data.lights.new(nm, "POINT"))
        light.data.energy = 900.0
        light.location = loc
        sc.collection.objects.link(light)
    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs[0].default_value = (0.05, 0.06, 0.12, 1.0)
    bg.inputs[1].default_value = 1.0
    sc.world = world

    if OUT:
        if os.path.exists(OUT) and "--force" not in ARGS:
            OUT = OUT[:-6] + "_generated.blend"
            print("kept the existing file; this one is", OUT)
        os.makedirs(os.path.dirname(OUT), exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=OUT, relative_remap=True)
        bpy.ops.file.make_paths_relative()
        bpy.ops.wm.save_mainfile()
        print("saved", OUT)

    if RENDERS:
        os.makedirs(RENDERS, exist_ok=True)
        for engine in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE", "BLENDER_WORKBENCH"):
            try:
                sc.render.engine = engine
                break
            except TypeError:
                continue
        sc.render.resolution_x, sc.render.resolution_y = 1280, 720
        cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
        sc.collection.objects.link(cam)
        sc.camera = cam
        cam.data.lens = 20
        roofs = [o for o in rooms.objects if o.name.startswith(("LR_Kit_Roof", "LR_Kit_Lamp"))]

        def shot(name, loc, target, lens=20, roof=True):
            for o in roofs:
                o.hide_render = not roof
            cam.data.lens = lens
            cam.location = loc
            cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
            sc.render.filepath = os.path.join(RENDERS, name)
            bpy.ops.render.render(write_still=True)
            print("render", name)

        shot("1_pieces.png", (0.0, -33.0, 6.5), (0.0, -16.0, 1.0), lens=16)
        shot("2_living_inside_north_east.png", (-2.6, 2.6, 1.6), (2.2, -3.0, 1.2))
        shot("3_living_inside_south.png", (1.5, -2.5, 1.6), (-2.0, 4.5, 1.0))
        shot("4_living_above.png", (0.0, 0.01, 22.0), (0.0, 0.0, 0.0), lens=35, roof=False)
        shot("5_playroom_above.png", (px, 0.01, 30.0), (px, 0.0, 0.0), lens=35, roof=False)
        shot("6_playroom_inside.png", (px - 2.5, -2.5, 1.6), (px + 6.0, 0.0, 1.2))
        shot("7_rooms_outside.png", (11.0, -26.0, 16.0), (11.0, 0.0, 0.0), lens=24, roof=True)
