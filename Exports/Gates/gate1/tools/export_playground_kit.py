"""Exports the PlayGround kit (gate 1) for Unity, 2026-11-23 (the same way as gate 3's export_ballvalley_kit.py).

Each piece of the PlayGround_Kit collection goes to its own FBX in Assets/Models/Gates/PlayGround/<piece name>.fbx, at
the origin (its pivot), with the project's FBX recipe (FBX_SCALE_ALL + bake space transform, -Z forward, Y up). A
breakable's broken set (PlayGround_Kit_<Piece>_Broken) goes to one FBX with its chunks where they lie round the set's
pivot (each chunk its own object, its origin in its middle). The palette PNGs go to Assets/M_Textures/Gates/PlayGround/.
Never saves the .blend (once the user edits it, it's theirs).

Run: blender -b Exports/Gates/gate1/PlayGround_Kit.blend --python Exports/Gates/gate1/tools/export_playground_kit.py
      [-- <piece> <piece> ...]   (no names: every piece)"""
import bpy, os, sys, shutil
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
MODELS = os.path.join(REPO, "Assets", "Models", "Gates", "PlayGround")
TEXTURES = os.path.join(REPO, "Assets", "M_Textures", "Gates", "PlayGround")
PALETTE = os.path.join(REPO, "Exports", "Gates", "gate1", "textures")
PREFIX = "PlayGround_Kit_"
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

kit = bpy.data.collections.get("PlayGround_Kit")
if kit is None:
    raise SystemExit("No PlayGround_Kit collection in this file.")
os.makedirs(MODELS, exist_ok=True)
os.makedirs(TEXTURES, exist_ok=True)
for name in ("PlayGround_Palette.png", "PlayGround_Palette_Emission.png"):
    shutil.copy2(os.path.join(PALETTE, name), os.path.join(TEXTURES, name))
    print("texture", name)

for ob in bpy.context.view_layer.objects:
    ob.select_set(False)
count = 0
for coll in kit.children:
    if not coll.name.startswith(PREFIX):
        continue
    short = coll.name[len(PREFIX):]
    if ARGS and short not in ARGS:
        continue
    objs = [o for o in coll.objects if o.type == "MESH"]
    if not objs:
        continue
    # the set's pivot: the collection's instance offset (the piece's place on the kit sheet; a lifted piece's own z)
    pivot = Vector(coll.instance_offset)
    saved = []
    for ob in objs:
        saved.append((ob, ob.location.copy(), ob.hide_get()))
        ob.hide_set(False)
        ob.location = ob.location - pivot if len(objs) > 1 else Vector((0.0, 0.0, 0.0))
        ob.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    path = os.path.join(MODELS, coll.name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, mesh_smooth_type="FACE", use_mesh_modifiers=True,
                             add_leaf_bones=False, bake_anim=False, path_mode="STRIP", use_custom_props=False)
    for ob, loc, hidden in saved:
        ob.select_set(False)
        ob.location = loc
        ob.hide_set(hidden)
    tris = sum(len(p.vertices) - 2 for o in objs for p in o.data.polygons)
    print("exported %-40s %2d object(s) %5d tris -> %s" % (coll.name, len(objs), tris, os.path.relpath(path, REPO)))
    count += 1
print("done: %d pieces (the .blend was not saved)" % count)
