# Headless: blender -b <Whack_Machine.blend> --python export_mallet.py -- <out.fbx>
# Exports the "Whack_Mallet (export)" collection (the mallet moved to the origin for the export, so its pivot = the
# grip is the FBX origin), with the project's settings. Never saves the .blend.
import bpy, sys
out = sys.argv[sys.argv.index("--") + 1]
col = bpy.data.collections["Whack_Mallet (export)"]
for o in bpy.context.view_layer.objects:
    o.select_set(False)
mallet = bpy.data.objects["Whack_Mallet"]
mallet.location = (0.0, 0.0, 0.0)
objs = list(col.all_objects)
for o in objs:
    o.hide_set(False)
    o.hide_viewport = False
    o.select_set(True)
    print(o.type, o.name, "world", tuple(round(v, 3) for v in o.matrix_world.translation))
bpy.context.view_layer.update()
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={'MESH', 'EMPTY'},
                         apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
                         axis_forward='-Z', axis_up='Y', mesh_smooth_type='OFF', add_leaf_bones=False,
                         bake_anim=False, path_mode='AUTO', use_mesh_modifiers=True)
print("EXPORTED", out, len(objs), "objects")
