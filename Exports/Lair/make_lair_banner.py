# Headless: blender -b --factory-startup --python make_lair_banner.py
# The lair's torn clover banner (user 2026-11-25, the storage-room concept: the clover banner nibbled by the bugs),
# remodelled from the user's own Banner_Hang (Assets/Models/Bedroom_Stuffs/Banner_Hang.fbx, same UVs and texture):
# bites cut out of the cloth's edges, two moth holes, the pennant's tip torn off. The cut faces keep the cloth's
# own UVs; the thin bite walls take the cloth's colour from one spot of it. Adds the object Lair_Torn_Banner to
# Exports/Lair/Lair_Story_Props.blend (my own file; the jar is kept) and writes Assets/Models/Lair/Lair_Torn_Banner.fbx.
# Then in Unity: Tools > 429 Game > Lair > Set Up Lair Story Props.
import bpy, bmesh, os, math
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", ".."))
SRC = os.path.join(REPO, "Assets", "Models", "Bedroom_Stuffs", "Banner_Hang.fbx")
OUT_FBX = os.path.join(REPO, "Assets", "Models", "Lair", "Lair_Torn_Banner.fbx")
BLEND = os.path.join(HERE, "Lair_Story_Props.blend")
NAME = "Lair_Torn_Banner"

if os.path.exists(BLEND):
    bpy.ops.wm.open_mainfile(filepath=BLEND)
for o in list(bpy.data.objects):
    if o.name.startswith(NAME) or o.name.startswith("_cut") or o.name == "Banner_Hang":
        bpy.data.objects.remove(o)
before = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=SRC)
ob = next(o for o in bpy.data.objects if o not in before and o.type == 'MESH')
for o in bpy.context.view_layer.objects:
    o.select_set(o == ob)
bpy.context.view_layer.objects.active = ob
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
ob.name = NAME
ob.data.name = NAME

# The cloth: the big +X faces below the rod. Its edges and a spot of its colour (for the bite walls).
bm = bmesh.new()
bm.from_mesh(ob.data)
uvl = bm.loops.layers.uv.active
cloth = [f for f in bm.faces if f.normal.x > 0.9 and f.calc_area() > 0.002]
ys = [v.co.y for f in cloth for v in f.verts]
zs = [v.co.z for f in cloth for v in f.verts]
y0, y1, z0, z1 = min(ys), max(ys), min(zs), max(zs)
xs = [v.co.x for v in bm.verts]
xmid = (min(xs) + max(xs)) / 2
low = min(cloth, key=lambda f: f.calc_center_median().z)
edge_uv = Vector((0, 0))
for l in low.loops:
    edge_uv += l[uvl].uv
edge_uv /= len(low.loops)
bm.free()
print("CLOTH y %.3f..%.3f z %.3f..%.3f, edge uv %s" % (y0, y1, z0, z1, tuple(round(c, 3) for c in edge_uv)))

# The cutters: cylinders through the cloth (along X) on its outline, plus a ragged cut through the pennant's tip.
# The cloth is a pennant: straight sides down to a V that ends in the tip at the bottom middle.
bm = bmesh.new()
bm.from_mesh(ob.data)
side = [v.co.z for f in bm.faces if f.normal.x > 0.9 and f.calc_area() > 0.002 for v in f.verts if abs(v.co.y) > 0.9 * y1]
bm.free()
zs_side = min(side)                                   # where the sides meet the V
tip = Vector((0.0, (y0 + y1) / 2, z0))
left_v, right_v = Vector((0.0, y0, zs_side)), Vector((0.0, y1, zs_side))
print("PENNANT sides down to z %.3f, tip z %.3f" % (zs_side, z0))
bites = [  # (y, z, radius in m)
    (*left_v.lerp(tip, 0.32).yz, 0.07), (*left_v.lerp(tip, 0.7).yz, 0.05),    # the left V edge
    (*right_v.lerp(tip, 0.5).yz, 0.08),                                       # the right V edge
    (y1, zs_side + 0.2, 0.06),                                                # the right side (a second bite
    #                                                         higher up there made the boolean drop the rope)
    (y0, zs_side + 0.34, 0.055),                                              # the left side
    (y0 + 0.09, zs_side + 0.1, 0.022), (y1 - 0.1, zs_side + 0.62, 0.02),      # moth holes
]
cutters = []
for (by, bz, br) in bites:
    bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=br, depth=0.4, location=(xmid, by, bz), rotation=(0, math.pi / 2, 0))
    cutters.append(bpy.context.active_object)
# The torn-off tip: a ragged band across the V's bottom.
bm = bmesh.new()
pts = [(-0.16, -0.02), (-0.12, 0.085), (-0.07, 0.06), (-0.03, 0.11), (0.02, 0.075), (0.06, 0.1), (0.1, 0.05), (0.16, 0.07), (0.16, -0.02)]
ring = [(tip.y + py, z0 + pz) for (py, pz) in pts]
front = [bm.verts.new((xmid + 0.2, y, z)) for (y, z) in ring]
back = [bm.verts.new((xmid - 0.2, y, z)) for (y, z) in ring]
bm.faces.new(front)
bm.faces.new(list(reversed(back)))
for i in range(len(ring)):
    j = (i + 1) % len(ring)
    bm.faces.new((front[i], back[i], back[j], front[j]))
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
me = bpy.data.meshes.new("_cut_tip")
bm.to_mesh(me)
bm.free()
corner = bpy.data.objects.new("_cut_tip", me)
bpy.context.scene.collection.objects.link(corner)
cutters.append(corner)
for c in cutters:
    c.name = "_cut"
    if not c.data.uv_layers:
        c.data.uv_layers.new(name=ob.data.uv_layers.active.name)
    for d in c.data.uv_layers[0].data:
        d.uv = edge_uv
    c.data.materials.clear()
    for m in ob.data.materials:
        c.data.materials.append(m)

faces_before = len(ob.data.polygons)
# Only the cloth is cut: the rod, ropes and hook (everything wholly above the bites) are set aside first, as the
# boolean ate bits of the thin open ropes, then joined back.
TOP = zs_side + 0.7
top = ob.copy()
top.data = ob.data.copy()
bpy.context.scene.collection.objects.link(top)
for target, keep_top in ((ob, False), (top, True)):
    bm = bmesh.new()
    bm.from_mesh(target.data)
    gone = [f for f in bm.faces if (min(v.co.z for v in f.verts) > TOP) != keep_top]
    bmesh.ops.delete(bm, geom=gone, context='FACES')
    bm.to_mesh(target.data)
    bm.free()
for c in cutters:
    mod = ob.modifiers.new("bite", 'BOOLEAN')
    mod.operation = 'DIFFERENCE'
    mod.solver = 'FLOAT'
    mod.object = c
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.modifier_apply(modifier=mod.name)
for c in cutters:
    bpy.data.objects.remove(c)
for o in bpy.context.view_layer.objects:
    o.select_set(o in (ob, top))
bpy.context.view_layer.objects.active = ob
bpy.ops.object.join()
print("BANNER faces %d -> %d, dims %s" % (faces_before, len(ob.data.polygons), tuple(round(d, 3) for d in ob.dimensions)))

os.makedirs(os.path.dirname(OUT_FBX), exist_ok=True)
for o in bpy.context.view_layer.objects:
    o.select_set(o == ob)
keep = ob.location.copy()
ob.location = (0, 0, 0)
bpy.ops.export_scene.fbx(filepath=OUT_FBX, use_selection=True, object_types={'MESH'},
                         apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
                         axis_forward='-Z', axis_up='Y', mesh_smooth_type='OFF', add_leaf_bones=False,
                         bake_anim=False, path_mode='AUTO', use_mesh_modifiers=True)
ob.location = (1.0, 0, 0)   # beside the jar in the .blend
print("EXPORTED", OUT_FBX)
bpy.ops.wm.save_as_mainfile(filepath=BLEND)
print("SAVED", BLEND)
