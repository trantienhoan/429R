"""Exports the living room kit pieces (LR_Kit_*, the originals, not the sample rooms' copies) to Unity, one mesh per
piece, names kept, each piece's pivot at its own origin. Runs the kit script in live mode first (only adds pieces the
file lacks; never touches the user's pieces), never saves the .blend.
  blender -b Exports/Remodel/Livingroom_Kit.blend --python Exports/Remodel/tools/export_livingroom_kit.py -- <out.fbx>"""
import bpy, os, sys

OUT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else None
HERE = os.path.dirname(os.path.abspath(__file__))
KIT = os.path.join(HERE, "livingroom_kit.py")
exec(open(KIT, encoding="utf-8").read(), {"LIVINGROOM_KIT_LIVE": True, "__file__": KIT, "__name__": "livingroom_kit_live"})

coll = bpy.data.collections["Livingroom_Kit"]
pieces = [o for o in coll.objects if o.type == "MESH" and o.name.startswith("LR_Kit_") and "_inst" not in o.name]
for o in bpy.data.objects:
    o.select_set(False)
for o in pieces:
    o.hide_set(False)
    o.hide_viewport = False
    o.location = (0.0, 0.0, 0.0)
    o.rotation_euler = (0.0, 0.0, 0.0)
    o.scale = (1.0, 1.0, 1.0)
    o.select_set(True)
print("exporting", len(pieces), sorted(o.name for o in pieces))
os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
                         apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True, axis_forward="-Z", axis_up="Y",
                         mesh_smooth_type="OFF", use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False)
print("saved", OUT)
