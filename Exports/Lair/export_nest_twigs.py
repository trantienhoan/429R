# Exports the Twigs object of Exports/Lair/Lair_Nest.blend to Assets/Models/Lair/Lair_Nest_Twigs.fbx for Unity.
# Run headless (the .blend is only read, never saved):
#   blender.exe -b Exports/Lair/Lair_Nest.blend --python Exports/Lair/export_nest_twigs.py
# Or in Blender: Scripting tab > open this file > Run Script.
# Unity smooths the twigs itself (Tools > 429 Game > Lair > Use Blender Nest Twigs sets the import), as in the game.
# Same recipe as the whack machine: -Z forward, Y up, Apply Transform, FBX_SCALE_ALL. The file keeps the nest's
# layout as in the Lair_Nest prefab's root (the prefab's Twigs object undoes the Nest's x1.35), so it lands in place.
import os, bpy

here = os.path.dirname(bpy.data.filepath) or os.path.dirname(os.path.abspath(__file__))
out = os.path.normpath(os.path.join(here, "..", "..", "Assets", "Models", "Lair", "Lair_Nest_Twigs.fbx"))
twigs = bpy.data.objects["Twigs"]
if bpy.context.mode != "OBJECT":
    bpy.ops.object.mode_set(mode="OBJECT")
for o in bpy.context.view_layer.objects:
    o.select_set(o == twigs)
bpy.context.view_layer.objects.active = twigs
bpy.ops.export_scene.fbx(
    filepath=out, use_selection=True, object_types={"MESH"},
    axis_forward="-Z", axis_up="Y", bake_space_transform=True, apply_scale_options="FBX_SCALE_ALL",
    use_mesh_modifiers=True, mesh_smooth_type="OFF", use_tspace=False, add_leaf_bones=False,
    bake_anim=False, path_mode="AUTO", embed_textures=False)
print("TWIGS_FBX_OK", out, len(twigs.data.vertices), "verts")
