# The user's fixed piece (Exports/Story/Seed_Piece.blend, saved by the user; this script never saves it) to
# Assets/Models/Story/Seed_Piece.fbx: its own rotation and scale baked into the mesh (metres, Z up), its pivot at its
# middle (it spins and flies about it), at the size the user made it (no extra scale), exported the way that lands
# upright in Unity (-Z forward, Y up, Apply Transform, FBX_SCALE_ALL).
# Run: blender -b Exports/Story/Seed_Piece.blend --python Exports/Story/export_user_piece.py
import bpy
from mathutils import Matrix, Vector

piece = bpy.data.objects["Seed_Piece"]
me = piece.data.copy()
me.transform(piece.matrix_world.to_3x3().to_4x4())
lo = Vector((min(v.co.x for v in me.vertices), min(v.co.y for v in me.vertices), min(v.co.z for v in me.vertices)))
hi = Vector((max(v.co.x for v in me.vertices), max(v.co.y for v in me.vertices), max(v.co.z for v in me.vertices)))
me.transform(Matrix.Translation(-(lo + hi) / 2))
out = bpy.data.objects.new("Seed_Piece", me) if False else None
piece.data = me
piece.matrix_world = Matrix.Identity(4)
for o in bpy.context.view_layer.objects: o.select_set(o == piece)
bpy.context.view_layer.objects.active = piece
bpy.ops.export_scene.fbx(filepath=bpy.path.abspath("//../../Assets/Models/Story/Seed_Piece.fbx"), use_selection=True,
                         object_types={'MESH'}, mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False,
                         axis_forward='-Z', axis_up='Y', bake_space_transform=True, apply_scale_options='FBX_SCALE_ALL',
                         path_mode='STRIP')
print("EXPORTED", [round(v, 3) for v in piece.dimensions], "verts", len(me.vertices), "faces", len(me.polygons), "mats", [m.name for m in me.materials])
