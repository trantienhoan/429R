"""Bedroom kit (2026-11-19). User: "create a kit with the same texture that we are using in a new blend file", then "that's
the window I want", then "check how I model the wall also, I want a clean mesh, not a messy kit".

Pieces on the 3 m grid of the 11-16 kit decision (3 m and 1.5 m walls, 3 m high, pivot at the inner face's bottom-left:
a piece runs along +X, its inside is -Y, its outside +Y), modelled the way the user modelled the old bedroom's walls
(Bedroom_Base_Z in Assets/Blender/Bedroom_Walls.blend):
  - a wall is ONE closed slab, 0.345 m thick, all wallpaper, cut with the fewest quads: a plain box (6), a doorway from
    the floor (14: the faces round the opening run straight to the slab's corners), a window hole (16, the same way);
  - the trim is separate closed boxes: baseboards 12 cm tall, 5 cm deep, a hair below the floor, split at a door;
  - the user's own door frame (Wall01, 14 quads) and window (Wall02, 86 faces) are taken as they are, UVs and all;
  - corner posts are thin boxes in the room's inside corners; floor tiles and beams are boxes;
  - no bevels, no crown, all quads, no faces inside faces.
Materials and textures as the old room (same names as Bedroom_Walls.blend, so Unity's import finds them):
  Bedroom_Wall        Bedroom_Wallpaper_Spooky.png (+ wall_not_clean normal/roughness): the user's layout, v 0.444 at the
                      floor to 0.966 at the top (dirty band at the floor, drips at the top); 3 m = u 0.5 (8 damask
                      repeats, so slabs meet without a seam).
  Bedroom_Wood_Floor  wood_floor3: floor tiles (one texture per 3 m), baseboards, posts, beams (grain along).

Run (makes the file, two sample rooms at today's size, renders):
  blender -b --factory-startup --python Exports/Remodel/tools/bedroom_kit.py -- <out.blend> [renders_dir] [--force]
An existing <out.blend> (the user may have edited it) is never overwritten without --force: the new one is saved next to
it as *_generated.blend.
Live (in the user's open kit file; adds the BR_Kit_* pieces it doesn't have yet, never touches the ones there (the
user edits them) unless named in BEDROOM_KIT_REBUILD, leaves everything else alone, saves nothing):
  exec(open(this).read(), {"BEDROOM_KIT_LIVE": True, "__file__": this, "BEDROOM_KIT_REBUILD": ["BR_Kit_Window"]})"""
import bpy, bmesh, sys, os, math
from mathutils import Vector, Matrix

LIVE = bool(globals().get("BEDROOM_KIT_LIVE", False))
# Live: pieces already in the file are the user's (they edit them: e.g. the chain taken off the secret door,
# 2026-11-19) and are never rebuilt, only missing ones are added; name a piece here to rebuild it on purpose.
REBUILD = set(globals().get("BEDROOM_KIT_REBUILD", ()))
ARGS = sys.argv[sys.argv.index("--") + 1:] if ("--" in sys.argv and not LIVE) else []
POS = [a for a in ARGS if not a.startswith("--")]
OUT = POS[0] if POS else None
RENDERS = POS[1] if len(POS) > 1 else None
REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
TEX = os.path.join(REPO, "Assets", "M_Textures")
BEDROOM_WALLS = os.path.join(REPO, "Assets", "Blender", "Bedroom_Walls.blend")

if not LIVE:
    bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene

H = 3.0            # wall height (11-16 decision)
T = 0.345          # wall thickness: the user's slabs
FULL, HALF = 3.0, 1.5
BASE_H, BASE_D, BASE_SINK = 0.122, 0.052, 0.001   # the user's baseboards
POST = 0.14        # the user's corner posts (0.138 x 0.14)
FLOOR_T = 0.12
U_PER_M = 0.5 / 3.0
V_FLOOR, V_TOP = 0.444, 0.966
WOOD_PER_M = 1.0 / 3.0
WALL_I, WOOD_I = 0, 1
PART_SLAB, PART_TRIM, PART_FLOOR, PART_BEAM, PART_USER = 1, 2, 3, 4, 9   # PART_USER: the user's own faces and UVs

# The user's own pieces in Bedroom_Base_Z (rest pose), taken by face index; Bounds checks they're still those pieces.
# Turn: how the old wall's frame maps onto the kit's (x along, -y inside, z up).
WINDOW = dict(  # Wall02: inner face x = -2.775 (inside +x); opening y 0.44..1.165, z 1.042..1.985
    faces=[87, 88, 89, 90, 91, 92, 93, 94, 95, 96, 97, 98, 99, 100, 101, 102, 103, 104, 105, 106, 143, 144, 145, 146,
           147, 148, 149, 150, 151, 152, 153, 154, 155, 156, 157, 158, 159, 160, 161, 162, 163, 164, 165, 166, 167, 168,
           169, 170, 171, 172, 173, 174, 175, 176, 259, 260, 261, 262, 263, 264, 265, 266, 267, 268, 269, 270, 271, 272,
           273, 274, 275, 276, 277, 278, 279, 280, 324, 325, 326, 327, 328, 329, 330, 331, 332, 467],
    bounds=((-3.138, 0.314, 0.949), (-2.705, 1.291, 2.074)),
    turn=lambda c: (c.y - (0.44 + 1.165) / 2.0 + FULL / 2.0, -(c.x + 2.775), c.z),
    opening=(1.165 - 0.44, 1.042, 1.985))           # width, bottom, top
DOOR = dict(    # Wall01: inner face y = -2.775 (inside +y); opening x 0.464..1.577, from the floor to 2.085 (an open passage)
    faces=[0, 1, 2, 3, 4, 5, 6, 7, 8, 53, 54, 63, 64, 65],
    bounds=((0.464, -2.822, 0.0), (1.577, -2.677, 2.085)),
    turn=lambda c: (-(c.x - (0.464 + 1.577) / 2.0) + FULL / 2.0, -(c.y + 2.775), c.z),
    opening=(1.577 - 0.464, 0.0, 2.085))
WIDE_DOOR = dict(  # Wall05, the small room's secret door: inner face y = -1.775 (inside +y); opening x 2.273..3.68
    faces=[336, 337, 338, 339, 340, 341, 342, 343, 344, 348, 349, 350, 351, 352],
    bounds=((2.273, -1.822, 0.0), (3.68, -1.677, 2.085)),
    turn=lambda c: (-(c.x - (2.273 + 3.68) / 2.0) + FULL / 2.0, -(c.y + 1.775), c.z),
    opening=(3.68 - 2.273, 0.0, 2.085))
FRAME_BACK = 0.047   # the frames sit this far into the slab: a door leaf's planks go just behind them
# The old roof (Rooftop bone), its three parts as they are: the wood slab, the trim ring along the walls under it, the
# square lamp panel. Centred on the room, moved down so the slab's underside is the ceiling (the walls' tops, z = H);
# Unity scales the slab and ring to each room's size (the old roof was scaled 1.36-1.48 the same way).
ROOF_DROP = 3.082 - H
ROOF = dict(faces=[48, 177, 178, 179, 180, 181], bounds=((-3.025, -3.025, 3.082), (3.025, 3.025, 3.418)),
            turn=lambda c: (c.x, c.y, c.z - ROOF_DROP))
ROOF_RIM = dict(faces=list(range(533, 549)), bounds=((-2.775, -2.775, 2.956), (2.775, 2.775, 3.082)),
                turn=lambda c: (c.x, c.y, c.z - ROOF_DROP))
LAMP = dict(faces=list(range(524, 533)), bounds=((-0.254, -0.254, 3.052), (0.254, 0.254, 3.082)),
            turn=lambda c: (c.x, c.y, c.z - ROOF_DROP))
# The door leaves (user, 2026-11-19: "2 kinds of door, 1 secret door as a short cut, 1 normal locked door"): the old
# room's own door models, whole, from Bedroom_Walls.blend. Bedroom_Base_Z_Door (the horseshoe sign) is the small
# bedroom's breakable shortcut door; Bedroom_Base_Z_Door_2 (W/C) the big bedroom's locked door. Each is hinged at x = 0,
# its boards, chain and padlock on +y: turned so they face the room, centred in the wide doorway, standing on the floor,
# the planks just behind the frame (so its jambs hold the leaf's edges, as in the old room).
# BR_Kit_Door_Chained (user, 2026-11-19: "Yes, add it"): the same horseshoe door WITH its chain and padlock, the old big
# room's second locked door (its north wall). The user took the chain off BR_Kit_Door_Secret so the shortcut door looks
# breakable; this one stays chained, so it looks locked.
LEAVES = {"BR_Kit_Door_Secret": "Bedroom_Base_Z_Door", "BR_Kit_Door_Locked": "Bedroom_Base_Z_Door_2",
          "BR_Kit_Door_Chained": "Bedroom_Base_Z_Door"}


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


WALL = material("Bedroom_Wall", "Combos/Wall_Stylize_Set_1/Bedroom_Wallpaper_Spooky.png",
                normal="Combos/Wall_Stylize_Set_1/wall_not_clean_normal.png",
                rough="Combos/Wall_Stylize_Set_1/wall_not_clean_roughness.png")
WOOD = material("Bedroom_Wood_Floor", "Combos/Wood_Floor_Stylize_Set_2/wood_floor3_albedo.png",
                normal="Combos/Wood_Floor_Stylize_Set_2/wood_floor3_normal.png", rough_value=0.9)
METAL = material("Bedroom_Metal", "Combos/Metal_Stylize_Set_2/medieval_metal_albedo_set2.png",
                 normal="Combos/Metal_Stylize_Set_2/medieval_metal_normal_set2.png",
                 rough="Combos/Metal_Stylize_Set_2/medieval_metal_roughness_set2.png", rough_value=0.5)
FURNITURE = material("Bedroom_Furniture_Set1",
                     "BedRoom_Set_1/NEW/Bedroom_Furniture_Set1_Bedroom_Furniture_Set1_AlbedoTransparency.png",
                     normal="BedRoom_Set_1/NEW/Bedroom_Furniture_Set1_Bedroom_Furniture_Set1_Normal.png", rough_value=0.9)
MATS = [WALL, WOOD, METAL, FURNITURE]
METAL_I, FURNITURE_I = 2, 3


def slot_for(material_name):
    """The kit's material slot for one of the old room's materials (by name)."""
    if "Metal" in material_name:
        return METAL_I
    if "Furniture" in material_name:
        return FURNITURE_I
    if "Wall" in material_name:
        return WALL_I
    return WOOD_I


# ----------------------------------------------------------------------------------------------------------------- the user's pieces
def user_faces(spec):
    """The user's faces from Bedroom_Walls.blend, each [(kit co, uv), ...]; None (with a note) if they changed."""
    if not os.path.exists(BEDROOM_WALLS):
        print("NOTE: no", BEDROOM_WALLS)
        return None
    before = (set(bpy.data.meshes), set(bpy.data.materials), set(bpy.data.images))
    with bpy.data.libraries.load(BEDROOM_WALLS, link=False) as (src, dst):
        dst.meshes = ["Bedroom_Base_Z"] if "Bedroom_Base_Z" in src.meshes else []
    me = next((m for m in bpy.data.meshes if m not in before[0]), None)
    faces = None
    try:
        if me is None or len(me.polygons) <= max(spec["faces"]) or not me.uv_layers:
            raise ValueError("Bedroom_Base_Z changed")
        uvl = me.uv_layers.active.data
        cos = [me.vertices[v].co for i in spec["faces"] for v in me.polygons[i].vertices]
        lo = tuple(min(c[k] for c in cos) for k in range(3))
        hi = tuple(max(c[k] for c in cos) for k in range(3))
        if any(abs(a - b) > 0.01 for a, b in zip(lo + hi, spec["bounds"][0] + spec["bounds"][1])):
            raise ValueError("those faces aren't that piece any more: %s %s" % (lo, hi))
        faces = []
        for i in spec["faces"]:
            p = me.polygons[i]
            corners = [(spec["turn"](me.vertices[me.loops[li].vertex_index].co), tuple(uvl[li].uv), me.loops[li].vertex_index) for li in p.loop_indices]
            mat_name = me.materials[p.material_index].name if me.materials else ""
            faces.append((corners, slot_for(mat_name)))
    except ValueError as e:
        print("NOTE:", e)
    finally:
        tidy(before, me)
    return faces


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


def leaf_faces(object_name):
    """A whole door model from Bedroom_Walls.blend, turned into the wide doorway (see LEAVES): its faces with their UVs,
    wood or metal as the model has them. None (with a note) if it isn't there."""
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
            me = ob.data if ob.type == "MESH" else None
            tidy(before, me, ob)
        return None
    me = ob.data
    uvl = me.uv_layers.active.data
    cos = [v.co for v in me.vertices]
    x0, x1 = min(c.x for c in cos), max(c.x for c in cos)
    z0 = min(c.z for c in cos)
    # The planks: the biggest wood part. Its room side goes just behind the frame.
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
    turn = lambda c: (-c.x + FULL / 2.0 + (x0 + x1) / 2.0, -c.y + FRAME_BACK + py1, c.z - z0)
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
    """quads: lists of coordinates; verts are shared by position. Normals are put right afterwards (closed parts)."""
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
    c = lambda x, y, z: (x, y, z)
    quads = [
        [c(x0, y0, z0), c(x1, y0, z0), c(x1, y0, z1), c(x0, y0, z1)],
        [c(x0, y1, z0), c(x0, y1, z1), c(x1, y1, z1), c(x1, y1, z0)],
        [c(x0, y0, z0), c(x0, y0, z1), c(x0, y1, z1), c(x0, y1, z0)],
        [c(x1, y0, z0), c(x1, y1, z0), c(x1, y1, z1), c(x1, y0, z1)],
        [c(x0, y0, z0), c(x0, y1, z0), c(x1, y1, z0), c(x1, y0, z0)],
        [c(x0, y0, z1), c(x1, y0, z1), c(x1, y1, z1), c(x0, y1, z1)],
    ]
    return faces_from(bm, quads, mat, tag)


def slab(bm, x0, x1, hole=None):
    """One closed wallpaper slab from x0 to x1 (inner face y = 0, outer y = T). Hole (a, b, z0, z1): a doorway when z0 is
    0, else a window hole; the faces round it run straight to the slab's corners (the user's way: 14 / 16 quads)."""
    if hole is None:
        return box(bm, (x0, 0.0, 0.0), (x1, T, H), WALL_I, PART_SLAB)
    a, b, z0, z1 = hole
    quads = []
    for y in (0.0, T):
        p = lambda x, z: (x, y, z)
        if z0 <= 0.0:
            quads += [[p(x0, 0), p(a, 0), p(a, z1), p(x0, H)],
                      [p(x0, H), p(a, z1), p(b, z1), p(x1, H)],
                      [p(b, 0), p(x1, 0), p(x1, H), p(b, z1)]]
        else:
            quads += [[p(x0, 0), p(x1, 0), p(b, z0), p(a, z0)],
                      [p(x1, 0), p(x1, H), p(b, z1), p(b, z0)],
                      [p(x1, H), p(x0, H), p(a, z1), p(b, z1)],
                      [p(x0, H), p(x0, 0), p(a, z0), p(a, z1)]]
    f, k = (lambda x, z: (x, 0.0, z)), (lambda x, z: (x, T, z))
    # Opening's sides (and its bottom for a window), the slab's top, ends and bottom.
    quads += [[f(a, max(z0, 0)), k(a, max(z0, 0)), k(a, z1), f(a, z1)],
              [f(b, max(z0, 0)), f(b, z1), k(b, z1), k(b, max(z0, 0))],
              [f(a, z1), k(a, z1), k(b, z1), f(b, z1)]]
    if z0 > 0.0:
        quads += [[f(a, z0), f(b, z0), k(b, z0), k(a, z0)],
                  [f(x0, 0), k(x0, 0), k(x1, 0), f(x1, 0)]]
    else:
        quads += [[f(x0, 0), k(x0, 0), k(a, 0), f(a, 0)],
                  [f(b, 0), k(b, 0), k(x1, 0), f(x1, 0)]]
    quads += [[f(x0, H), f(x1, H), k(x1, H), k(x0, H)],
              [f(x0, 0), f(x0, H), k(x0, H), k(x0, 0)],
              [f(x1, 0), k(x1, 0), k(x1, H), f(x1, H)]]
    return faces_from(bm, quads, WALL_I, PART_SLAB)


def baseboard(bm, x0, x1):
    return box(bm, (x0, -BASE_D, -BASE_SINK), (x1, 0.0, BASE_H), WOOD_I, PART_TRIM)


def add_user(bm, faces):
    """The user's faces as they are: [(corners [(co, uv, their vertex index)], material index)]. Vertices are shared
    exactly where the user's own mesh shares them, so its parts stay as they were."""
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
    """Wallpaper as the user laid it out (band at the floor, drips at the top), wood with the grain along; the user's
    own faces keep theirs."""
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
                    l[uv].uv = (co.x * U_PER_M, V_FLOOR + co.y * U_PER_M)
                else:
                    along = co.x if ax == 1 else co.y
                    l[uv].uv = (along * U_PER_M, V_FLOOR + (co.z / H) * (V_TOP - V_FLOOR))
            elif f[part] == PART_FLOOR or ax == 2:
                l[uv].uv = (co.x * WOOD_PER_M, co.y * WOOD_PER_M)
            else:
                along = co.x if ax == 1 else co.y
                if f[part] in (PART_TRIM, PART_BEAM):
                    l[uv].uv = (co.z * WOOD_PER_M, along * WOOD_PER_M)
                else:
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
        # Writing into a mesh that's already there can drop the faces' material slots: put them back.
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
    baseboard(bm, 0.0, w)
    return finish(bm, name, coll)


def piece_door(name, coll, spec=DOOR):
    w, z0, z1 = spec["opening"]
    a, b = FULL / 2 - w / 2, FULL / 2 + w / 2
    bm = new_bm()
    slab(bm, 0.0, FULL, (a, b, 0.0, z1))
    baseboard(bm, 0.0, a)
    baseboard(bm, b, FULL)
    frame = user_faces(spec)
    if frame:
        add_user(bm, frame)
    return finish(bm, name, coll)


def piece_leaf(name, coll):
    """A door leaf on its own (it takes hits, wobbles and breaks in the game), placed for the wide doorway: put it
    where the BR_Kit_Door_Wide piece is and it sits in its doorway."""
    faces = leaf_faces(LEAVES[name])
    if not faces:
        return None
    bm = new_bm()
    add_user(bm, faces)
    return finish(bm, name, coll)


def piece_window(name, coll):
    w, z0, z1 = WINDOW["opening"]
    a, b = FULL / 2 - w / 2, FULL / 2 + w / 2
    bm = new_bm()
    slab(bm, 0.0, FULL, (a, b, z0, z1))
    baseboard(bm, 0.0, FULL)
    window = user_faces(WINDOW)
    if window:
        add_user(bm, window)
    return finish(bm, name, coll)


def piece_user(name, coll, spec):
    """One of the user's parts on its own, as it is (the roof slab, its trim ring, the lamp)."""
    faces = user_faces(spec)
    if not faces:
        return None
    bm = new_bm()
    add_user(bm, faces)
    return finish(bm, name, coll)


def piece_corner(name, coll):
    bm = new_bm()
    box(bm, (0.0, -POST, -BASE_SINK), (POST, 0.0, H), WOOD_I, PART_TRIM)
    return finish(bm, name, coll)


def piece_floor(name, coll):
    bm = new_bm()
    box(bm, (0.0, 0.0, -FLOOR_T), (FULL, FULL, 0.0), WOOD_I, PART_FLOOR)
    return finish(bm, name, coll)


def piece_beam(name, coll):
    bm = new_bm()
    box(bm, (0.0, 0.0, H - 0.22), (FULL, 0.22, H), WOOD_I, PART_BEAM)
    return finish(bm, name, coll)


def repair_slots(ob):
    """Material slots from the faces' parts (a 2026-11-19 live rebuild dropped them): slab = wallpaper; trim, floor and
    beam = wood; the user's own frames, window and roof parts = wood unless already set (doors, lamp keep theirs)."""
    me = ob.data
    part = me.attributes.get("part")
    if part is None or len(me.materials) < 2:
        return 0
    if [m.name for m in me.materials[:2]] != ["Bedroom_Wall", "Bedroom_Wood_Floor"]:
        return 0
    fixed = 0
    for p in me.polygons:
        tag = part.data[p.index].value
        want = WALL_I if tag == PART_SLAB else (WOOD_I if tag in (PART_TRIM, PART_FLOOR, PART_BEAM) else None)
        if tag == PART_USER and p.material_index == WALL_I:
            want = WOOD_I
        if want is not None and p.material_index != want:
            p.material_index = want
            fixed += 1
    me.update()
    return fixed


kit = bpy.data.collections.get("Bedroom_Kit")
if kit is None:
    kit = bpy.data.collections.new("Bedroom_Kit")
    sc.collection.children.link(kit)
existed = {o.name for o in bpy.data.objects}
pieces = {
    "BR_Kit_Wall_3m": piece_wall("BR_Kit_Wall_3m", FULL, kit),
    "BR_Kit_Wall_1p5m": piece_wall("BR_Kit_Wall_1p5m", HALF, kit),
    "BR_Kit_Door": piece_door("BR_Kit_Door", kit),
    "BR_Kit_Door_Wide": piece_door("BR_Kit_Door_Wide", kit, WIDE_DOOR),
    "BR_Kit_Door_Secret": piece_leaf("BR_Kit_Door_Secret", kit),
    "BR_Kit_Door_Locked": piece_leaf("BR_Kit_Door_Locked", kit),
    "BR_Kit_Door_Chained": piece_leaf("BR_Kit_Door_Chained", kit),
    "BR_Kit_Window": piece_window("BR_Kit_Window", kit),
    "BR_Kit_Corner": piece_corner("BR_Kit_Corner", kit),
    "BR_Kit_Floor_3m": piece_floor("BR_Kit_Floor_3m", kit),
    "BR_Kit_Beam_3m": piece_beam("BR_Kit_Beam_3m", kit),
    "BR_Kit_Roof": piece_user("BR_Kit_Roof", kit, ROOF),
    "BR_Kit_Roof_Rim": piece_user("BR_Kit_Roof_Rim", kit, ROOF_RIM),
    "BR_Kit_Lamp": piece_user("BR_Kit_Lamp", kit, LAMP),
}
pieces = {k: v for k, v in pieces.items() if v is not None}
for name, ob in pieces.items():
    n = repair_slots(ob)
    if n:
        print("material slots put back on", n, "faces of", name)
for name, ob in pieces.items():
    me = ob.data
    print("%-18s faces %3d (quads %3d) tris %4d" % (name, len(me.polygons), sum(1 for p in me.polygons if len(p.vertices) == 4),
                                                  sum(len(p.vertices) - 2 for p in me.polygons)))
# The catalogue row: pieces side by side, 4 m apart. In a live file the pieces already there stay where they are and
# new ones go to the end of the row.
if LIVE:
    kept = [ob for name, ob in pieces.items() if name in existed]
    x = max((ob.location.x for ob in kept), default=-16.0) + 4.0
    for name, ob in pieces.items():
        if name not in existed:
            ob.location = (x, -14.0, 0.0)
            x += 4.0
else:
    for i, ob in enumerate(pieces.values()):
        ob.location = (i * 4.0 - 12.0, -14.0, 0.0)
# In the catalogue the secret door stands in the wide doorway (they share a pivot); the locked door has its own place.
if "BR_Kit_Door_Secret" in pieces and "BR_Kit_Door_Wide" in pieces and (not LIVE or "BR_Kit_Door_Secret" not in existed):
    pieces["BR_Kit_Door_Secret"].location = pieces["BR_Kit_Door_Wide"].location.copy()

if LIVE:
    # The sample rooms already there: the small room's door becomes the wide doorway with the secret door, the big
    # room's first door the wide doorway with the locked door (its second stays an open passage).
    rooms = bpy.data.collections.get("Sample_Rooms")
    if rooms is not None and "BR_Kit_Door_Wide" in pieces:
        for inst_name, leaf in (("BR_Kit_Door_inst", "BR_Kit_Door_Secret"), ("BR_Kit_Door_inst.001", "BR_Kit_Door_Locked")):
            inst = bpy.data.objects.get(inst_name)
            if inst is None or leaf not in pieces:
                continue
            inst.data = pieces["BR_Kit_Door_Wide"].data
            inst.name = "BR_Kit_Door_Wide_inst"
            lo = bpy.data.objects.new(leaf + "_inst", pieces[leaf].data)
            rooms.objects.link(lo)
            lo.matrix_world = inst.matrix_world.copy()
    bpy.ops.ed.undo_push(message="Bedroom kit: doors")
else:
    rooms = bpy.data.collections.new("Sample_Rooms")
    sc.collection.children.link(rooms)

    def place(name, coll):
        ob = bpy.data.objects.new(name + "_inst", pieces[name].data)
        coll.objects.link(ob)
        return ob

    def frame(xa, ya, px, py):
        return Matrix(((xa.x, ya.x, 0, px), (xa.y, ya.y, 0, py), (0, 0, 1, 0), (0, 0, 0, 1)))

    def build_room(coll, origin, sides):
        """sides: 4 lists of piece names walking round the inside anticlockwise from the -Y wall. A piece's pivot (the
        inner face's left end, seen from inside) sits at the far end of its stretch; a post stands in each inside corner."""
        ox, oy = origin
        length = lambda nm: HALF if nm.split("+")[0] == "BR_Kit_Wall_1p5m" else FULL
        w = sum(length(p) for p in sides[0])
        d = sum(length(p) for p in sides[1])
        x = 0.0
        while x < w - 0.01:
            y = 0.0
            tw = min(FULL, w - x)
            while y < d - 0.01:
                td = min(FULL, d - y)
                fo = place("BR_Kit_Floor_3m", coll)
                fo.location = (ox + x, oy + y, 0.0)
                fo.scale = (tw / FULL, td / FULL, 1.0)
                y += td
            x += tw
        walls = [((ox, oy), (1, 0)), ((ox + w, oy), (0, 1)), ((ox + w, oy + d), (-1, 0)), ((ox, oy + d), (0, -1))]
        for (start, dirv), names in zip(walls, sides):
            sx, sy = start
            xa = Vector((-dirv[0], -dirv[1], 0.0))
            ya = Vector((dirv[1], -dirv[0], 0.0))
            run = 0.0
            for entry in names:
                # "Doorway+Leaf": a door leaf placed with its doorway.
                parts = entry.split("+")
                run += length(parts[0])
                for nm in parts:
                    place(nm, coll).matrix_world = frame(xa, ya, sx + dirv[0] * run, sy + dirv[1] * run)
            place("BR_Kit_Corner", coll).matrix_world = frame(xa, ya, sx + dirv[0] * run, sy + dirv[1] * run)
        return w, d

    mini_sides = [
        ["BR_Kit_Wall_3m", "BR_Kit_Wall_1p5m", "BR_Kit_Wall_3m"],
        ["BR_Kit_Wall_3m", "BR_Kit_Door_Wide+BR_Kit_Door_Secret", "BR_Kit_Wall_1p5m"],
        ["BR_Kit_Window", "BR_Kit_Wall_1p5m", "BR_Kit_Wall_3m"],
        ["BR_Kit_Wall_3m", "BR_Kit_Wall_1p5m", "BR_Kit_Wall_3m"],
    ]
    big_sides = [
        ["BR_Kit_Wall_1p5m", "BR_Kit_Window", "BR_Kit_Wall_3m"],
        ["BR_Kit_Wall_3m", "BR_Kit_Door_Wide+BR_Kit_Door_Locked", "BR_Kit_Wall_1p5m"],
        ["BR_Kit_Wall_3m", "BR_Kit_Window", "BR_Kit_Wall_1p5m"],
        ["BR_Kit_Wall_1p5m", "BR_Kit_Door", "BR_Kit_Wall_3m"],
    ]
    mw, md = build_room(rooms, (-3.75, -3.75), mini_sides)
    bw, bd = build_room(rooms, (8.0, -3.75), big_sides)
    print("mini room %.1f x %.1f m, big room %.1f x %.1f m" % (mw, md, bw, bd))

    for nm, energy, rot in (("Sun", 3.0, (50, 10, 30)), ("Fill", 1.4, (60, -10, 215))):
        light = bpy.data.objects.new(nm, bpy.data.lights.new(nm, "SUN"))
        light.data.energy = energy
        light.rotation_euler = tuple(math.radians(a) for a in rot)
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

        def shot(name, loc, target):
            cam.location = loc
            cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
            sc.render.filepath = os.path.join(RENDERS, name)
            bpy.ops.render.render(write_still=True)
            print("render", name)

        shot("1_pieces.png", (0.0, -30.0, 6.0), (0.0, -14.0, 1.0))
        shot("2_mini_inside.png", (2.6, 2.6, 1.6), (-2.0, -2.0, 1.2))
        shot("3_big_inside.png", (14.4, 2.6, 1.6), (9.5, -2.2, 1.2))
        shot("4_rooms_above.png", (5.9, -15.0, 15.0), (5.9, 0.0, 0.0))
