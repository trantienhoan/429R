# Headless: blender -b --factory-startup --python make_lair_box.py
# The lair's chewed box (user 2026-11-25, the storage-room concept: a box the bugs chewed open, by their nest),
# remodelled from the user's own Paper_Box_2 (Assets/Models/Bedroom_Stuffs/Paper_Box_2.fbx, same UVs and texture): a
# gnawed bite out of one top edge and a ragged hole in one side; the inside walls show a black spot of the atlas. Adds
# the object Lair_Chewed_Box to Exports/Lair/Lair_Story_Props.blend (my own file) and writes
# Assets/Models/Lair/Lair_Chewed_Box.fbx. Then in Unity: Tools > 429 Game > Lair > Set Up Lair Story Props.
import bpy, bmesh, os, math
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", ".."))
SRC = os.path.join(REPO, "Assets", "Models", "Bedroom_Stuffs", "Paper_Box_2.fbx")
OUT_FBX = os.path.join(REPO, "Assets", "Models", "Lair", "Lair_Chewed_Box.fbx")
BLEND = os.path.join(HERE, "Lair_Story_Props.blend")
NAME = "Lair_Chewed_Box"
BLACK_UV = (0.8164, 0.0742)          # a flat black spot of the furniture atlas (Bedroom_Set_1_Mat)

if os.path.exists(BLEND):
    bpy.ops.wm.open_mainfile(filepath=BLEND)
for o in list(bpy.data.objects):
    if o.name.startswith(NAME) or o.name.startswith("_cut") or o.name == "Paper_Box_2":
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
xs = [v.co.x for v in ob.data.vertices]; ys = [v.co.y for v in ob.data.vertices]; zs = [v.co.z for v in ob.data.vertices]
x0, x1, y0, y1, z0, z1 = min(xs), max(xs), min(ys), max(ys), min(zs), max(zs)
W, D, H = x1 - x0, y1 - y0, z1 - z0
print("BOX x %.3f..%.3f y %.3f..%.3f z %.3f..%.3f" % (x0, x1, y0, y1, z0, z1))


def prism(name, outline, axis_from, axis_to):
    """A closed prism: the outline (points in 3D on one plane) pushed from axis_from to axis_to (offsets)."""
    bm = bmesh.new()
    a = [bm.verts.new(Vector(p) + Vector(axis_from)) for p in outline]
    b = [bm.verts.new(Vector(p) + Vector(axis_to)) for p in outline]
    bm.faces.new(a)
    bm.faces.new(list(reversed(b)))
    for i in range(len(outline)):
        j = (i + 1) % len(outline)
        bm.faces.new((a[i], b[i], b[j], a[j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    return o


cutters = []
# A gnawed bite out of the top front-right edge: three overlapping round bites.
for (fx, fz, r) in ((0.78, 1.0, 0.075), (0.62, 1.0, 0.06), (0.9, 0.9, 0.055)):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, radius=r * max(W, H),
                                         location=(x0 + fx * W, y0, z0 + fz * H))
    cutters.append(bpy.context.active_object)
# A ragged hole low in the front side, going a third of the way in.
cx, cz = x0 + 0.35 * W, z0 + 0.38 * H
ragged = [(0.0, 0.11), (0.05, 0.09), (0.1, 0.1), (0.12, 0.04), (0.1, -0.02), (0.13, -0.08), (0.06, -0.1),
          (0.0, -0.07), (-0.06, -0.11), (-0.1, -0.05), (-0.12, 0.02), (-0.08, 0.08)]
outline = [(cx + px * W, 0.0, cz + pz * H * 1.1) for (px, pz) in ragged]
cutters.append(prism("_cut_hole", outline, (0, y0 - 0.05, 0), (0, y0 + D * 0.33, 0)))
for c in cutters:
    c.name = "_cut"
    if not c.data.uv_layers:
        c.data.uv_layers.new(name=ob.data.uv_layers.active.name)
    for d in c.data.uv_layers[0].data:
        d.uv = BLACK_UV
    c.data.materials.clear()
    for m in ob.data.materials:
        c.data.materials.append(m)

faces_before = len(ob.data.polygons)
for c in cutters:
    mod = ob.modifiers.new("bite", 'BOOLEAN')
    mod.operation = 'DIFFERENCE'
    mod.solver = 'FLOAT'         # the box is not closed everywhere (its tape): the exact solver kept cutter bits
    mod.object = c
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.modifier_apply(modifier=mod.name)
for c in cutters:
    bpy.data.objects.remove(c)
print("CHEWED faces %d -> %d, dims %s" % (faces_before, len(ob.data.polygons), tuple(round(d, 3) for d in ob.dimensions)))

os.makedirs(os.path.dirname(OUT_FBX), exist_ok=True)
for o in bpy.context.view_layer.objects:
    o.select_set(o == ob)
ob.location = (0, 0, 0)
bpy.ops.export_scene.fbx(filepath=OUT_FBX, use_selection=True, object_types={'MESH'},
                         apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
                         axis_forward='-Z', axis_up='Y', mesh_smooth_type='OFF', add_leaf_bones=False,
                         bake_anim=False, path_mode='AUTO', use_mesh_modifiers=True)
ob.location = (2.0, 0, 0)   # beside the jar and the banner in the .blend
print("EXPORTED", OUT_FBX)
bpy.ops.wm.save_as_mainfile(filepath=BLEND)
print("SAVED", BLEND)
