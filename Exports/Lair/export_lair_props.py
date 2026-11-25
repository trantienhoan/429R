# Headless: blender -b Exports/Lair/Lair_Props.blend --python Exports/Lair/export_lair_props.py
# Exports each object of the "Lair (export)" collection to Assets/Models/Lair/<name>.fbx with the project's settings
# (-Z forward, Y up, Apply Transform, FBX_SCALE_ALL, no smoothing), each at the origin. Changes nothing in the .blend
# and never saves it: Lair_Props.blend is the user's (they fixed the pieces by hand on 2026-11-25), so after an edit
# in Blender, save it and run this. Then in Unity: Tools > 429 Game > Lair > Set Up Lair Webs.
import bpy, os

REPO = os.path.abspath(os.path.join(os.path.dirname(bpy.data.filepath), "..", ".."))
OUT = os.path.join(REPO, "Assets", "Models", "Lair")
os.makedirs(OUT, exist_ok=True)
col = bpy.data.collections["Lair (export)"]
for ob in list(col.objects):
    if ob.type != 'MESH':
        continue
    keep = ob.location.copy()
    ob.location = (0, 0, 0)
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    ob.hide_set(False)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    path = os.path.join(OUT, ob.name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'},
                             apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
                             axis_forward='-Z', axis_up='Y', mesh_smooth_type='OFF', add_leaf_bones=False,
                             bake_anim=False, path_mode='AUTO', use_mesh_modifiers=True)
    ob.location = keep
    print("EXPORTED", path, "faces", len(ob.data.polygons))
