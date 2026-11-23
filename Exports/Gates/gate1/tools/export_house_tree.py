"""Gate 1, 2026-11-23: exports the user's textured tree Big_Tree_01_01.001 (picture sheet #2, 5.5 x 5.1 x 6.8 m,
material Bedroom_Furniture_Set1) from Assets/Blender/Bedroom_Walls.blend to
Assets/Models/Gates/PlayGround/PG_Big_Tree_Textured.fbx: pivot = the middle of its foot, at the origin; the project's FBX
recipe. Never saves the .blend. (The other house models are the gate 2 session's House_Pieces exports.)
Run: blender -b Assets/Blender/Bedroom_Walls.blend --python Exports/Gates/gate1/tools/export_house_tree.py"""
import bpy, os
from mathutils import Vector, Matrix
HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
OUT = os.path.join(REPO, "Assets", "Models", "Gates", "PlayGround", "PG_Big_Tree_Textured.fbx")
src = bpy.data.objects["Big_Tree_01_01.001"]
dg = bpy.context.evaluated_depsgraph_get()
me = bpy.data.meshes.new_from_object(src.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
me.transform(src.matrix_world)
vs = [v.co for v in me.vertices]
mn = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
mx = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
me.transform(Matrix.Translation(-Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z))))
ob = bpy.data.objects.new("PG_Big_Tree_Textured", me)
bpy.context.scene.collection.objects.link(ob)
for o in bpy.context.view_layer.objects:
    o.select_set(False)
ob.select_set(True)
bpy.context.view_layer.objects.active = ob
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
                         apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                         mesh_smooth_type="FACE", use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False,
                         path_mode="STRIP", use_custom_props=False)
size = mx - mn
print("exported %s: %d tris, %.2f x %.2f x %.2f m, materials %s (the .blend was not saved)" % (os.path.relpath(OUT, REPO),
      sum(len(p.vertices) - 2 for p in me.polygons), size.x, size.y, size.z, [m.name for m in me.materials if m]))
