"""'The house eats the room' preview (2026-11-16): a corner of a 3 m kit room whose wall panels are split into chunky
blocks; leftover boxes fly into a purple hole in the middle, then the wall blocks peel off top-down in a wave and spiral
into it. Renders frames for a GIF/MP4.
  blender -b --factory-startup --python house_eats_room_preview.py -- <out_dir>"""
import bpy, bmesh, sys, os, math, random
from mathutils import Vector, Euler

OUT = sys.argv[sys.argv.index("--") + 1]
os.makedirs(OUT, exist_ok=True)
random.seed(4)
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
H, T, S = 3.0, 0.25, 9.0          # wall height, thickness, room size (3 panels of 3 m)
HOLE = Vector((S / 2, S / 2, 0.05))


def mat(name, rgb, emit=0.0):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (rgb[0], rgb[1], rgb[2], 1)
    return m


WALL, WALL2, WOOD, FLOOR, BOX, HOLEM, RING = (mat("wall", (0.36, 0.25, 0.42)), mat("wall2", (0.31, 0.21, 0.37)),
                                              mat("wood", (0.40, 0.24, 0.13)), mat("floor", (0.33, 0.20, 0.11)),
                                              mat("box", (0.75, 0.62, 0.45)), mat("hole", (0.08, 0.0, 0.14)),
                                              mat("ring", (0.75, 0.35, 1.0)))


def block(name, size, loc, m, bev=0.03):
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
    sc.collection.objects.link(o)
    b = o.modifiers.new("bevel", 'BEVEL')
    b.width = bev
    b.segments = 2
    return o


def panel_chunks(origin, rot, rows=5, cols=4):
    """One 3 m x 3 m panel as a staggered grid of chunky blocks (rough bricks), the wainscot row as planks."""
    out = []
    d = Vector((math.cos(rot), math.sin(rot), 0))
    n = Vector((-math.sin(rot), math.cos(rot), 0))            # into the room
    rh = (H - 0.5) / rows
    for r in range(rows):
        z0 = 0.5 + r * rh
        off = (0.5 if r % 2 else 0.0) * (3.0 / cols)
        edges = [0.0] + [min(3.0, max(0.0, off + c * 3.0 / cols + random.uniform(-0.12, 0.12))) for c in range(1, cols + 1)]
        edges = sorted(set([0.0, 3.0] + [e for e in edges if 0.15 < e < 2.85]))
        for a, b in zip(edges, edges[1:]):
            w = b - a
            c = Vector(origin) + d * (a + w / 2) + n * (T / 2) + Vector((0, 0, z0 + rh / 2))
            o = block("chunk", (w - 0.02, T * random.uniform(0.85, 1.0), rh - 0.02), c, WALL if (r + int(a)) % 2 else WALL2)
            o.rotation_euler = (0, 0, rot)
            out.append(o)
    for k in range(2):                       # wainscot: two planks
        c = Vector(origin) + d * (0.75 + 1.5 * k) + n * (T / 2) + Vector((0, 0, 0.25))
        o = block("plank", (1.48, T + 0.04, 0.48), c, WOOD)
        o.rotation_euler = (0, 0, rot)
        out.append(o)
    return out


# floor, hole, two walls (the far corner) of 3 panels each
for i in range(3):
    for j in range(3):
        block("floor", (2.98, 2.98, 0.1), (1.5 + 3 * i, 1.5 + 3 * j, -0.05), FLOOR, 0.02)
bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=1.1, depth=0.04, location=HOLE)
hole = bpy.context.object; hole.data.materials.append(HOLEM)
bpy.ops.mesh.primitive_torus_add(major_radius=1.15, minor_radius=0.07, location=HOLE + Vector((0, 0, 0.03)))
ring = bpy.context.object; ring.data.materials.append(RING)
chunks = []
for i in range(3):
    chunks += panel_chunks((3 * i, S, 0), 0.0)           # north wall, facing south (into the room)
    chunks += panel_chunks((S, 3 * i, 0), math.pi / 2)    # east wall ... facing west
# fix facing: north wall should face -y, east wall -x
for o in chunks:
    if abs(o.location.y - (S + T / 2)) < 0.3 or o.location.y > S - 0.5:
        o.location.y = S - T / 2
    elif o.location.x > S - 0.5:
        o.location.x = S - T / 2
boxes = []
for k in range(6):
    p = (random.uniform(2, 7), random.uniform(1.5, 4), 0.25)
    b = block("box", (0.5, 0.5, 0.45), p, BOX)
    b.rotation_euler = (0, 0, random.uniform(0, 3))
    boxes.append(b)


def fly(o, start, frames=34, spin=1.0):
    """Shake, pop up, spiral down into the hole and shrink away."""
    p0 = o.location.copy()
    r0 = o.rotation_euler.copy()
    o.keyframe_insert("location", frame=start)
    o.keyframe_insert("scale", frame=start)
    o.keyframe_insert("rotation_euler", frame=start)
    # a little shake
    o.location = p0 + Vector((random.uniform(-0.04, 0.04), random.uniform(-0.04, 0.04), 0.02))
    o.keyframe_insert("location", frame=start + 3)
    # pop towards the room and up
    to_hole = (HOLE - p0); to_hole.z = 0
    lift = p0 + to_hole.normalized() * 0.6 + Vector((0, 0, 0.5))
    o.location = lift
    o.rotation_euler = Euler((r0.x + random.uniform(-1, 1), r0.y + random.uniform(-1, 1), r0.z + spin), 'XYZ')
    o.keyframe_insert("location", frame=start + 9)
    o.keyframe_insert("rotation_euler", frame=start + 9)
    # spiral: a quarter turn round the hole, 1.6 m above it
    rel = lift - HOLE; rel.z = 0
    ang = math.atan2(rel.y, rel.x) + 1.6
    rad = max(0.8, rel.length * 0.45)
    o.location = HOLE + Vector((math.cos(ang) * rad, math.sin(ang) * rad, 1.6))
    o.rotation_euler = Euler((r0.x + 3, r0.y + 2, r0.z + 4 * spin), 'XYZ')
    o.keyframe_insert("location", frame=start + frames - 10)
    o.keyframe_insert("rotation_euler", frame=start + frames - 10)
    # into the hole
    o.location = HOLE + Vector((0, 0, -0.3))
    o.scale = (0.05, 0.05, 0.05)
    o.keyframe_insert("location", frame=start + frames)
    o.keyframe_insert("scale", frame=start + frames)
    o.scale = (1, 1, 1)


for k, b in enumerate(boxes):
    fly(b, 4 + 3 * k, 30)
# the walls peel off top-down in a wave that runs along them
for o in chunks:
    along = (o.location.x + (S - o.location.y)) / (2 * S)        # 0 at the north-west end ... 1 at the south-east end
    top = 1.0 - o.location.z / H
    fly(o, int(30 + 40 * top + 30 * along + random.uniform(0, 6)), 32, random.choice((-1, 1)))
for o in bpy.data.objects:
    if o.animation_data and o.animation_data.action:
        for fc in o.animation_data.action.fcurves if hasattr(o.animation_data.action, "fcurves") else []:
            for kp in fc.keyframe_points:
                kp.interpolation = 'BEZIER'

c = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
sc.collection.objects.link(c)
c.location = (1.2, 0.8, 2.6)
c.rotation_euler = (Vector((6.0, 7.0, 1.0)) - c.location).to_track_quat('-Z', 'Y').to_euler()
c.data.lens = 18
sc.camera = c
sc.render.engine = 'BLENDER_WORKBENCH'
sh = sc.display.shading
sh.light = 'STUDIO'; sh.color_type = 'MATERIAL'; sh.show_cavity = True; sh.show_shadows = True; sh.shadow_intensity = 0.35
sc.world = bpy.data.worlds.new("w")
sc.render.resolution_x, sc.render.resolution_y = 960, 540
sc.frame_start, sc.frame_end = 1, 150
sc.render.fps = 30
sc.render.filepath = os.path.join(OUT, "f_")
bpy.ops.render.render(animation=True)
print("DONE", len(chunks), "chunks")
