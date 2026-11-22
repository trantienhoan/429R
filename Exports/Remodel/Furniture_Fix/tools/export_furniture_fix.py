# Exports Bedroom_Furniture_Fix.blend's FBX collections back to their game FBX files (or to a test folder), each
# collection's objects moved back by its layout_offset first (and back again after; the .blend isn't saved).
#   blender -b Exports/Remodel/Furniture_Fix/Bedroom_Furniture_Fix.blend --python export_furniture_fix.py -- <repo root>
#       [--only name1.fbx,name2.fbx] [--to <folder under the repo, e.g. Assets/_claude_fbx_test>]
# Without --to, writes over Assets/... (the original paths); back the originals up first.
import bpy, sys, os
from mathutils import Vector

a = sys.argv[sys.argv.index("--") + 1:]
root = a[0].rstrip("/\\") + "/"
only = None
to = None
if "--only" in a:
    only = set(a[a.index("--only") + 1].split(","))
if "--to" in a:
    to = a[a.index("--to") + 1]

done = []
for col in bpy.data.collections:
    src = col.get("source_fbx")
    if not src or (only and os.path.basename(src) not in only):
        continue
    off = Vector(col.get("layout_offset", (0, 0, 0)))
    roots = [o for o in col.objects if o.parent is None]
    for o in roots:
        o.location -= off
    bpy.context.view_layer.update()
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for o in col.all_objects:
        o.select_set(True)
    dst = os.path.join(root, to, os.path.basename(src)) if to else os.path.join(root, src)
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=dst, use_selection=True, object_types={'MESH', 'EMPTY'},
                             axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_NONE',
                             bake_space_transform=False, use_mesh_modifiers=True, mesh_smooth_type='FACE',
                             add_leaf_bones=False, bake_anim=False, path_mode='AUTO')
    for o in roots:
        o.location += off
    done.append(f"{os.path.basename(src)} -> {dst} ({len(col.all_objects)} objects)")
print("EXPORTED")
print("\n".join(done))
