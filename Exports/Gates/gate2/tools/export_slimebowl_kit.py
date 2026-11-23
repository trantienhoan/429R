"""Exports the SlimeBowl kit (gate 2) to Unity, 2026-11-23: every SlimeBowl_Kit_* piece (the originals in the
SlimeBowl_Kit collections, not the Sample_Arena or Kit_Sheet copies) as one mesh in one FBX, names kept, each piece's
pivot at its own origin (the bedroom kit's recipe: FBX_SCALE_ALL, baked space, -Z forward, Y up: Blender +Y comes out as
Unity -Z, Blender +X as Unity -X, Z up as Y up). Copies the palette textures next to it. Never saves the .blend.
  blender -b Exports/Gates/gate2/SlimeBowl_Kit.blend --python Exports/Gates/gate2/tools/export_slimebowl_kit.py
Out: Assets/Models/Gates/SlimeBowl/SlimeBowl_Kit.fbx and Assets/M_Textures/Gates/SlimeBowl/SlimeBowl_Palette(_Emission).png.
Then in Unity: Tools > 429 Game > Bowling > Build SlimeBowl Kit.
Optional: `-- <out.fbx>` writes the FBX there instead (a test; no textures copied).
2026-11-23: a piece the user scaled or turned in Object Mode keeps that size and turn (baked into its mesh here, in this
unsaved session; its origin stays the pivot). Before, the export reset them and the change was lost."""
import bpy, os, sys, shutil
from mathutils import Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
TEST = bool(ARGS)
OUT = os.path.abspath(ARGS[0]) if TEST else os.path.join(REPO, "Assets", "Models", "Gates", "SlimeBowl", "SlimeBowl_Kit.fbx")
TEX = os.path.join(REPO, "Assets", "M_Textures", "Gates", "SlimeBowl")

kit = bpy.data.collections.get("SlimeBowl_Kit")
if kit is None:
    raise SystemExit("No SlimeBowl_Kit collection in " + bpy.data.filepath)
pieces = [o for o in kit.all_objects if o.type == "MESH" and o.name.startswith("SlimeBowl_Kit_")]
for o in bpy.data.objects:
    o.select_set(False)
baked = []
for o in pieces:
    o.hide_set(False)
    o.hide_viewport = False
    # Object-mode turn and size into the mesh (its own copy, so the arena's linked copies don't matter here).
    rot_scale = o.matrix_basis.to_3x3().to_4x4()
    if any(abs(rot_scale[i][j] - (1.0 if i == j else 0.0)) > 1e-6 for i in range(3) for j in range(3)):
        o.data = o.data.copy()
        o.data.transform(rot_scale)
        baked.append(o.name)
    o.matrix_basis = Matrix.Identity(4)
    o.select_set(True)
if baked:
    print("kept the object-mode size/turn of", baked)
print("exporting", len(pieces), sorted(o.name for o in pieces))
os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
                         apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True, axis_forward="-Z", axis_up="Y",
                         mesh_smooth_type="OFF", use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False,
                         path_mode="STRIP", embed_textures=False)
print("saved", OUT)

if TEST:
    raise SystemExit(0)
os.makedirs(TEX, exist_ok=True)
copied = set()
for img in bpy.data.images:
    if not img.filepath:
        continue
    src = bpy.path.abspath(img.filepath)
    if os.path.basename(src).startswith("SlimeBowl_Palette") and os.path.exists(src) and src not in copied:
        shutil.copy2(src, os.path.join(TEX, os.path.basename(src)))
        copied.add(src)
        print("copied", os.path.basename(src))
