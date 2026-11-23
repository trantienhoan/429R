# Headless: blender -b <Whack_Machine.blend> --python export_machine.py -- <out.fbx>
# Exports the "Whack_Machine (export)" collection as FBX with the project's settings (-Z forward, Y up, Apply
# Transform, FBX_SCALE_ALL, no smoothing). Hidden objects are shown for the export only. Never saves the .blend.
import bpy, sys
out = sys.argv[sys.argv.index("--") + 1]
col = bpy.data.collections["Whack_Machine (export)"]
for o in bpy.context.view_layer.objects:
    o.select_set(False)
objs = list(col.all_objects)
for o in objs:
    o.hide_set(False)
    o.hide_viewport = False
    o.select_set(True)
    line = f"{o.type:6} {o.name:24} loc {tuple(round(v, 3) for v in o.matrix_world.translation)}"
    if o.type == 'MESH':
        line += f" v{len(o.data.vertices)} f{len(o.data.polygons)} mats {[m.name for m in o.data.materials if m]} mods {[m.type for m in o.modifiers]}"
    print(line)
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={'MESH', 'EMPTY'},
                         apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
                         axis_forward='-Z', axis_up='Y', mesh_smooth_type='OFF', add_leaf_bones=False,
                         bake_anim=False, path_mode='AUTO', use_mesh_modifiers=True)
print("EXPORTED", out, len(objs), "objects")
