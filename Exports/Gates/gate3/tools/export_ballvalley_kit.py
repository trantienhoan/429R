"""Exports the Ball Valley kit (gate 3) for Unity, 2026-11-23.

Each piece of the BallValley_Kit collection (except Candy: the game has its own candies) goes to its own FBX in
Assets/Models/Gates/BallValley/<piece name>.fbx, at the origin (its pivot), with the user's FBX recipe for this project
(FBX_SCALE_ALL + bake space transform, -Z forward, Y up); the palette PNGs go to Assets/M_Textures/Gates/BallValley/.
Never saves the .blend (the user's file once they edit it).

Run: blender -b Exports/Gates/gate3/BallValley_Kit.blend --python Exports/Gates/gate3/tools/export_ballvalley_kit.py
      [-- <piece> <piece> ...]   (no names: every piece)"""
import bpy, os, sys, shutil

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
MODELS = os.path.join(REPO, "Assets", "Models", "Gates", "BallValley")
TEXTURES = os.path.join(REPO, "Assets", "M_Textures", "Gates", "BallValley")
PALETTE = os.path.join(REPO, "Exports", "Gates", "gate3", "Palette")
PREFIX = "BallValley_Kit_"
SKIP = {"Candy"}
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
if "--out" in ARGS:                       # a test export somewhere else (models and textures both go there)
    i = ARGS.index("--out")
    MODELS = TEXTURES = ARGS[i + 1]
    ARGS = ARGS[:i] + ARGS[i + 2:]

kit = bpy.data.collections.get("BallValley_Kit")
if kit is None:
    raise SystemExit("No BallValley_Kit collection in this file.")
os.makedirs(MODELS, exist_ok=True)
os.makedirs(TEXTURES, exist_ok=True)
for name in ("BallValley_Palette.png", "BallValley_Palette_Emission.png"):
    shutil.copy2(os.path.join(PALETTE, name), os.path.join(TEXTURES, name))
    print("texture", name)

pieces = []
for coll in kit.children:
    for ob in coll.objects:
        if ob.type == "MESH" and ob.name.startswith(PREFIX):
            short = ob.name[len(PREFIX):]
            if short in SKIP or (ARGS and short not in ARGS):
                continue
            pieces.append(ob)

for ob in bpy.context.view_layer.objects:
    ob.select_set(False)
for ob in pieces:
    was = ob.location.copy()
    hidden = ob.hide_get()
    ob.hide_set(False)
    ob.location = (0.0, 0.0, 0.0)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    path = os.path.join(MODELS, ob.name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, mesh_smooth_type="FACE", use_mesh_modifiers=True,
                             add_leaf_bones=False, bake_anim=False, path_mode="STRIP", use_custom_props=False)
    ob.select_set(False)
    ob.location = was
    ob.hide_set(hidden)
    tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
    print("exported %-40s %5d tris -> %s" % (ob.name, tris, os.path.relpath(path, REPO)))
print("done: %d pieces (the .blend was not saved)" % len(pieces))
