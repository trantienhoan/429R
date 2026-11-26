# A piece of the Seed (user 2026-11-26: "re-model those pieces of the Seed"; then "use the model of the Boss Seed for The
# Seed"): a 1/5 wedge of the Boss Seed's body (The_Boss_Seed.fbx, the furniture atlas), its outside keeping its UVs (olive
# cap, orange pumpkin ribs), its two cut faces closed, dented and painted from a light green spot of the same atlas (the
# Seed's glowing inside). No stem.
# Out: Exports/Story/Seed_Piece.blend (for the user) and Assets/Models/Story/Seed_Piece.fbx.
import bpy, bmesh, math, os, random
from mathutils import Vector, Matrix

REPO = "K:/Repository/429R/"
OUT_BLEND = REPO + "Exports/Story/Seed_Piece.blend"
OUT_FBX = REPO + "Assets/Models/Story/Seed_Piece.fbx"
PICS = "K:/WORK_2026/08_04/_claude/seed_piece_2026-11-26/"
WEDGE_DEG = 72.0
random.seed(429)

for ob in list(bpy.data.objects): bpy.data.objects.remove(ob, do_unlink=True)
bpy.ops.import_scene.fbx(filepath=REPO + "Assets/Models/Bedroom_Stuffs/The_Boss_Seed.fbx")
seed = next(o for o in bpy.data.objects if o.type == 'MESH')
# The textures, from this repo (the FBX points at the other PC's folder).
for img in bpy.data.images:
    if not img.filepath: continue
    name = os.path.basename(img.filepath.replace("\\", "/"))
    for root, dirs, files in os.walk(REPO + "Assets/M_Textures"):
        if name in files:
            img.filepath = os.path.join(root, name).replace("\\", "/")
            img.reload()
            break

# A light green spot in the leaf texture (bright, clearly green), for the cut faces.
leaf_mat_index = 0
leaf_img = None
for n in seed.data.materials[0].node_tree.nodes:
    if n.type == 'TEX_IMAGE' and n.image and "albedo" in n.image.name.lower(): leaf_img = n.image
W, H = leaf_img.size
px = leaf_img.pixels[:]
best, spot = -1.0, (0.5, 0.5)
for y in range(0, H, 8):
    for x in range(0, W, 8):
        i = (y * W + x) * 4
        r, g, b = px[i], px[i + 1], px[i + 2]
        score = g * 1.2 + (g - r) * 0.8 + (g - b) * 0.6 - abs(r - b) * 0.2
        if g > 0.55 and g > r + 0.12 and g > b + 0.2 and score > best:
            best, spot = score, ((x + 0.5) / W, (y + 0.5) / H)
# The furniture atlas has no bright green (its greens are a muted sea green): the inside is its warm straw spot, like a
# pumpkin's flesh (the pieces' green aura gives the glow). The lair code's flat spot (LairDecor Straw).
spot = (0.1836, 0.6445)
print("INSIDE SPOT (straw)", spot)

bm = bmesh.new(); bm.from_mesh(seed.data)
# Keep the body only (the biggest loose part): the leaf cards go.
bm.verts.ensure_lookup_table()
seen, parts = set(), []
for v in bm.verts:
    if v.index in seen: continue
    stack, part = [v], []
    while stack:
        x = stack.pop()
        if x.index in seen: continue
        seen.add(x.index); part.append(x)
        stack.extend(e.other_vert(x) for e in x.link_edges if e.other_vert(x).index not in seen)
    parts.append(part)
body = max(parts, key=len)
keep = set(v.index for v in body)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.index not in keep], context='VERTS')
# The stem goes (it rises from the cap's top, mesh Y up): every vertex above the cap.
CAP_TOP = 9.5
bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.y > CAP_TOP], context='VERTS')
print('STEM removed: body now', len(bm.verts), 'verts')

# The wedge: between two vertical planes through the middle (mesh space: Y is up).
centre = Vector((0.0, 0.0, 0.0))
a0, a1 = math.radians(0.0), math.radians(WEDGE_DEG)
n0 = Vector((math.sin(a0), 0.0, -math.cos(a0)))      # keeps the side towards a1
n1 = Vector((-math.sin(a1), 0.0, math.cos(a1)))
for n in (n0, n1):
    geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
    bmesh.ops.bisect_plane(bm, geom=geom, plane_co=centre, plane_no=n, clear_inner=True)
# Close the two cut sides.
before = set(bm.faces)
boundary = [e for e in bm.edges if e.is_boundary]
bmesh.ops.holes_fill(bm, edges=boundary, sides=0)
cut_faces = [f for f in bm.faces if f not in before]
bmesh.ops.triangulate(bm, faces=cut_faces)
cut_faces = [f for f in bm.faces if f not in before]
# A broken look: the cut faces' middle points dent in a little, unevenly.
bmesh.ops.subdivide_edges(bm, edges=list({e for f in cut_faces for e in f.edges if not e.is_boundary and all(l.face in cut_faces for l in e.link_loops)}), cuts=1, use_grid_fill=False)
cut_faces = [f for f in bm.faces if f not in before]
uv = bm.loops.layers.uv.active
for f in cut_faces:
    f.material_index = leaf_mat_index
    f.smooth = False
inner = {v for f in cut_faces for v in f.verts if all(lf in cut_faces for lf in v.link_faces)}
for v in inner:
    nrm = Vector((0, 0, 0))
    for lf in v.link_faces: nrm += lf.normal
    if nrm.length > 0: v.co -= nrm.normalized() * random.uniform(0.2, 0.9)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
for f in cut_faces:
    for loop in f.loops:
        # The light green spot, a touch lighter towards the middle (each corner a texel or two apart).
        loop[uv].uv = (spot[0] + random.uniform(-0.004, 0.004), spot[1] + random.uniform(-0.004, 0.004))
piece_me = bpy.data.meshes.new("Seed_Piece")
bm.to_mesh(piece_me); bm.free()
for m in seed.data.materials: piece_me.materials.append(m)
piece = bpy.data.objects.new("Seed_Piece", piece_me)
bpy.context.scene.collection.objects.link(piece)
piece.matrix_world = seed.matrix_world.copy()
# Its own middle as its pivot (it spins and flies about its middle), in mesh space.
mid = sum((v.co for v in piece_me.vertices), Vector()) / len(piece_me.vertices)
piece_me.transform(Matrix.Translation(-mid))
piece.matrix_world = seed.matrix_world @ Matrix.Translation(mid)
print("PIECE verts", len(piece_me.vertices), "faces", len(piece_me.polygons), "cut faces", len(cut_faces), "dims", tuple(round(d, 3) for d in piece.dimensions))

# The .blend for the user (the whole Boss Seed beside it for reference).
seed.location.x += 0.3
os.makedirs(os.path.dirname(OUT_BLEND), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND, compress=True)
bpy.ops.file.make_paths_relative()
bpy.ops.wm.save_mainfile(compress=True)
# The FBX: the piece only, the same set-up as the Seed's own FBX (its rotation and 0.01 scale), so it swaps in.
seed.location.x -= 0.3
os.makedirs(os.path.dirname(OUT_FBX), exist_ok=True)
for o in bpy.context.view_layer.objects: o.select_set(o == piece)
bpy.context.view_layer.objects.active = piece
saved = piece.matrix_world.copy()
piece.matrix_world = seed.matrix_world.copy()   # pivot back on the seed's own origin frame for the export
piece_me.transform(Matrix.Translation(mid))
bpy.ops.export_scene.fbx(filepath=OUT_FBX, use_selection=True, object_types={'MESH'}, mesh_smooth_type='FACE',
                         add_leaf_bones=False, bake_anim=False, path_mode='STRIP')
print("FBX", OUT_FBX)

# Pictures: the piece next to the whole seed, and 5 pieces making a seed.
piece_me.transform(Matrix.Translation(-mid)); piece.matrix_world = saved
scene = bpy.context.scene
try: scene.render.engine = "BLENDER_EEVEE"
except TypeError: scene.render.engine = "BLENDER_EEVEE_NEXT"
scene.world = bpy.data.worlds.new("w"); scene.world.color = (0.12, 0.2, 0.33)
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); scene.collection.objects.link(sun); sun.rotation_euler = (0.8, 0.2, 0.7); sun.data.energy = 3
# 4 more wedges round the up axis = the whole body again (to see they fit).
ring = []
for k in range(1, 5):
    c = piece.copy(); c.data = piece.data; scene.collection.objects.link(c)
    c.matrix_world = seed.matrix_world @ Matrix.Rotation(math.radians(WEDGE_DEG * k), 4, 'Y') @ Matrix.Translation(mid)
    ring.append(c)
seed.location = (0.32, 0, 0)
piece_alone = piece.copy(); piece_alone.data = piece.data; scene.collection.objects.link(piece_alone)
piece_alone.matrix_world = Matrix.Translation((-0.3, 0, 0.05)) @ Matrix.Rotation(math.radians(25), 4, 'Z') @ seed.matrix_world.to_3x3().to_4x4() @ Matrix.Diagonal((1, 1, 1, 1))
piece_alone.matrix_world = Matrix.Translation((-0.32, 0, 0)) @ (seed.matrix_world.to_3x3().to_4x4()) @ Matrix.Rotation(math.radians(200), 4, 'Y') @ Matrix.Translation(mid) @ Matrix.Scale(1.0, 4)
piece_alone.scale = seed.scale
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); scene.collection.objects.link(cam)
cam.location = (0.0, -0.95, 0.42)
cam.rotation_euler = (Vector((0, 0, 0.1)) - cam.location).to_track_quat("-Z", "Y").to_euler()
cam.data.lens = 50
scene.camera = cam
scene.render.resolution_x = 900; scene.render.resolution_y = 450
scene.render.filepath = PICS + "piece_vs_seed.png"
bpy.ops.render.render(write_still=True)
print("PICTURE", scene.render.filepath)
