"""Room kit blockout (2026-11-16): grey-box wall pieces on a 3 m grid (1.5 m halves), a bedroom built from them at
3 m and 3.5 m high, two rooms joined by a door, and renders to judge the size.
  blender -b --factory-startup --python Exports/Remodel/tools/kit_blockout.py -- <out_dir>"""
import bpy, bmesh, sys, os, math
from mathutils import Vector

OUT = sys.argv[sys.argv.index("--") + 1]
REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
os.makedirs(OUT, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
T = 0.25   # wall thickness


def mat(name, rgb):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (rgb[0], rgb[1], rgb[2], 1)
    return m


WALL = mat("wall", (0.36, 0.25, 0.42))
WOOD = mat("wood", (0.40, 0.24, 0.13))
FLOOR = mat("floor", (0.33, 0.20, 0.11))
PERSON = mat("person", (0.15, 0.6, 0.3))


def box(name, size, loc, m, coll):
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
    bev = o.modifiers.new("bevel", 'BEVEL')
    bev.width = 0.03
    bev.segments = 2
    bev.limit_method = 'ANGLE'
    return o


def piece(kind, H, coll, at=(0, 0, 0), rot=0.0):
    """Pivot: bottom of the wall's inner face at its left end, so pieces snap on the grid."""
    objs = []
    W = 1.5 if kind == "half" else 3.0
    if kind in ("wall", "half"):
        objs.append(box(kind, (W, T, H), (W / 2, T / 2, H / 2), WALL, coll))
        objs.append(box(kind + "_base", (W, T + 0.04, 0.25), (W / 2, T / 2, 0.125), WOOD, coll))
    elif kind == "door":
        dw, dh = 1.2, 2.2
        side = (W - dw) / 2
        objs.append(box("door_l", (side, T, H), (side / 2, T / 2, H / 2), WALL, coll))
        objs.append(box("door_r", (side, T, H), (W - side / 2, T / 2, H / 2), WALL, coll))
        objs.append(box("door_top", (dw, T, H - dh), (W / 2, T / 2, dh + (H - dh) / 2), WALL, coll))
        objs.append(box("door_leaf", (dw - 0.08, 0.08, dh - 0.04), (W / 2, T + 0.02, dh / 2), WOOD, coll))
    elif kind == "window":
        ww, wh, sill = 1.2, 1.1, 1.0
        side = (W - ww) / 2
        objs.append(box("win_l", (side, T, H), (side / 2, T / 2, H / 2), WALL, coll))
        objs.append(box("win_r", (side, T, H), (W - side / 2, T / 2, H / 2), WALL, coll))
        objs.append(box("win_bottom", (ww, T, sill), (W / 2, T / 2, sill / 2), WALL, coll))
        objs.append(box("win_top", (ww, T, H - sill - wh), (W / 2, T / 2, sill + wh + (H - sill - wh) / 2), WALL, coll))
        objs.append(box("win_frame", (ww, T + 0.06, 0.08), (W / 2, T / 2, sill + wh / 2), WOOD, coll))
    elif kind == "corner":
        objs.append(box("corner", (T, T, H), (-T / 2, T / 2, H / 2), WOOD, coll))
    elif kind == "floor":
        objs.append(box("floor", (3.0, 3.0, 0.1), (1.5, 1.5, -0.05), FLOOR, coll))
    root = bpy.data.objects.new("KIT_" + kind, None)
    coll.objects.link(root)
    for o in objs:
        o.parent = root
    root.location = at
    root.rotation_euler = (0, 0, rot)
    return root


def person(coll, at):
    return box("person_1.75m", (0.45, 0.3, 1.75), (at[0], at[1], 0.875), PERSON, coll)


def room(H, n, coll, origin=(0, 0, 0), door_side=None, window_side=None, ceiling=False, open_side=None):
    """n x n panels of 3 m; walls face inwards. Ceiling: a flat ceiling with 3 beams. Open Side: no wall there (cut-away)."""
    ox, oy = origin[0], origin[1]
    S = 3.0 * n
    if ceiling:
        box("ceiling", (S, S, 0.1), (ox + S / 2, oy + S / 2, H + 0.05), WALL, coll)
        for b in range(1, n):
            box("beam", (S, 0.3, 0.3), (ox + S / 2, oy + 3 * b, H - 0.15), WOOD, coll)
    for i in range(n):
        for j in range(n):
            piece("floor", H, coll, (ox + 3 * i, oy + 3 * j, 0))
    sides = {"S": ((ox, oy, 0), 0.0), "E": ((ox + S, oy, 0), math.pi / 2),
             "N": ((ox + S, oy + S, 0), math.pi), "W": ((ox, oy + S, 0), -math.pi / 2)}
    for side, (start, rot) in sides.items():
        if side == open_side:
            continue
        d = Vector((math.cos(rot), math.sin(rot), 0))
        for i in range(n):
            kind = "wall"
            if side == door_side and i == n // 2:
                kind = "door"
            elif side == window_side and i == n // 2:
                kind = "window"
            piece(kind, H, coll, Vector(start) + d * (3 * i), rot)
        piece("corner", H, coll, start, rot)


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


def furnish(coll, ox, oy, S):
    import_prop("Assets/Models/Bedroom_Stuffs/Bedroom_Bed.fbx", (ox + 1.3, oy + S - 1.4, 0), 0, coll)
    import_prop("Assets/Models/Bedroom_Stuffs/Bedroom_Stuffs_Shelf.fbx", (ox + S - 0.5, oy + S / 2, 0), math.pi / 2, coll)
    for k in range(4):
        import_prop("Assets/Models/Bedroom_Stuffs/Paper_Box_1.fbx", (ox + 1 + 0.6 * k, oy + 1.0, 0), 0.3 * k, coll)
    person(coll, (ox + S / 2, oy + S / 2 - 1, 0))


def cam(name, loc, target, lens):
    c = bpy.data.objects.new(name, bpy.data.cameras.new(name))
    sc.collection.objects.link(c)
    c.location = loc
    c.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    c.data.lens = lens
    return c


def render(camobj, path):
    sc.camera = camobj
    sc.render.resolution_x, sc.render.resolution_y = 1600, 900
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


def only(coll):
    for cc in bpy.data.collections:
        if cc.name.startswith(("Kit", "Bedroom", "Two")):
            cc.hide_render = cc is not coll


sc.render.engine = 'BLENDER_WORKBENCH'
sh = sc.display.shading
sh.light = 'STUDIO'
sh.color_type = 'MATERIAL'
sh.show_cavity = True
sh.show_shadows = True
sh.shadow_intensity = 0.4
sc.world = bpy.data.worlds.new("w")

# 1) the kit pieces at 3 m, with a 1.75 m person
k = bpy.data.collections.new("Kit lineup (3 m high)")
sc.collection.children.link(k)
x = 0
for kind in ("wall", "half", "door", "window", "corner", "floor"):
    piece(kind, 3.0, k, (x, -20, 0))
    x += 4.2
person(k, (x, -19.5, 0))
only(k)
render(cam("c1", (12, -36, 6), (12, -19, 1.2), 24), os.path.join(OUT, "1_kit_pieces.png"))

# 2) the same 9 m bedroom at 3 m and at 3.5 m: eye level (1.6 m) from a corner, and from above
for H, ox, tag in ((3.0, 0, "3m"), (3.5, 40, "3.5m")):
    c = bpy.data.collections.new("Bedroom %s" % tag)
    sc.collection.children.link(c)
    room(H, 3, c, (ox, 0, 0), door_side="S", window_side="N", ceiling=True)
    furnish(c, ox, 0, 9)
    only(c)
    render(cam("eye" + tag, (ox + 1.0, 1.0, 1.6), (ox + 7.5, 7.5, 2.4), 16), os.path.join(OUT, "2_bedroom_eye_%s.png" % tag))
    pass

# 2b) both heights side by side, front wall off, same person + bed: the height difference at a glance
cmp_ = bpy.data.collections.new("Bedroom compare")
sc.collection.children.link(cmp_)
for H, ox in ((3.0, 200), (3.5, 211)):
    room(H, 3, cmp_, (ox, 0, 0), window_side="N", ceiling=True, open_side="S")
    person(cmp_, (ox + 4.5, 2.0, 0))
    import_prop("Assets/Models/Bedroom_Stuffs/Bedroom_Bed.fbx", (ox + 2.0, 6.5, 0), 0, cmp_)
    import_prop("Assets/Models/Bedroom_Stuffs/Bedroom_Stuffs_Shelf.fbx", (ox + 7.8, 7.5, 0), 0, cmp_)
only(cmp_)
cc = cam("cmp", (210, -22, 2.2), (210, 5, 1.6), 30)
render(cc, os.path.join(OUT, "3_compare_3m_left_3.5m_right.png"))

# 3) two rooms of one stage: a 9 m bedroom and a 12 m room joined by a door
t = bpy.data.collections.new("Two rooms")
sc.collection.children.link(t)
room(3.0, 3, t, (100, 0, 0), door_side="E", window_side="N")
furnish(t, 100, 0, 9)
room(3.0, 4, t, (109.0, -1.5, 0), door_side="W", window_side="E")
only(t)
render(cam("two", (108, -16, 20), (108, 4, 0), 22), os.path.join(OUT, "4_two_rooms_above.png"))

for cc in bpy.data.collections:
    cc.hide_render = False
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(REPO, "Exports", "Remodel", "Room_Kit_Blockout.blend"))
print("DONE")
