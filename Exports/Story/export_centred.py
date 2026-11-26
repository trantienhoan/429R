import bpy
from mathutils import Matrix
piece = bpy.data.objects["Seed_Piece"]
seed = next(o for o in bpy.data.objects if o.type == 'MESH' and o != piece and o.data.name != piece.data.name)
# Upright in Blender = the Seed's own rotation and 0.01 scale on the piece's centred mesh. Baked into the mesh (metres,
# Z up), then exported the way that lands upright in Unity (-Z forward, Y up, Apply Transform, FBX_SCALE_ALL); the
# first export kept the Seed FBX's node rotation and came out upside down.
piece.matrix_world = seed.matrix_world.to_3x3().to_4x4()
bpy.context.view_layer.objects.active = piece
for o in bpy.context.view_layer.objects: o.select_set(o == piece)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
# A fifth of the Seed reads small on the floor: 1.35 times as big in the game (the .blend keeps its true size).
piece.data.transform(Matrix.Scale(1.35, 4))
top = max(v.co.z for v in piece.data.vertices); bottom = min(v.co.z for v in piece.data.vertices)
leaf = [p for p in piece.data.polygons if piece.data.materials[p.material_index].name.startswith("Magical_Leaf")]
bark = [p for p in piece.data.polygons if piece.data.materials[p.material_index].name.startswith("Magical_Tree")]
print("LEAF faces mean z", round(sum(p.center.z for p in leaf) / max(1, len(leaf)), 4), "BARK mean z", round(sum(p.center.z for p in bark) / max(1, len(bark)), 4), "z", round(bottom, 3), round(top, 3))
bpy.ops.export_scene.fbx(filepath="K:/Repository/429R/Assets/Models/Story/Seed_Piece.fbx", use_selection=True,
                         object_types={'MESH'}, mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False,
                         axis_forward='-Z', axis_up='Y', bake_space_transform=True, apply_scale_options='FBX_SCALE_ALL',
                         path_mode='STRIP')
print("EXPORTED", [round(v, 3) for v in piece.dimensions])
