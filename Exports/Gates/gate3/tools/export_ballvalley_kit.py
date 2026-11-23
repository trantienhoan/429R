"""Exports the Ball Valley kit (gate 3) for Unity, 2026-11-23.

Each piece of the BallValley_Kit collection (except Candy: the game has its own candies) goes to its own FBX in
Assets/Models/Gates/BallValley/<piece name>.fbx, at the origin (its pivot), with the user's FBX recipe for this project
(FBX_SCALE_ALL + bake space transform, -Z forward, Y up); the palette PNGs go to Assets/M_Textures/Gates/BallValley/.
Never saves the .blend (the user's file once they edit it).

A piece = the object BallValley_Kit_<Piece> plus any visible mesh objects parented to it (2026-11-23, for the user's
remodel): those are joined into a temporary copy with modifiers, rotation and scale applied (Unity keeps only the mesh,
so an object's own rotation/scale would otherwise be lost); the piece's origin stays its pivot. A plain piece (no parts,
no rotation/scale) exports as before. Check a file first with tools/check_kit.py.

Run: blender -b Exports/Gates/gate3/BallValley_Kit.blend --python Exports/Gates/gate3/tools/export_ballvalley_kit.py
      [-- <piece> <piece> ...] [--out <folder>]   (no names: every piece; --out: a test export elsewhere)"""
import bpy, json, os, sys, shutil

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


def fbx(path):
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, mesh_smooth_type="FACE", use_mesh_modifiers=True,
                             add_leaf_bones=False, bake_anim=False, path_mode="STRIP", use_custom_props=False)


def deselect_all():
    for o in bpy.context.view_layer.objects:
        o.select_set(False)


def parts_of(ob):
    return [c for c in ob.children_recursive if c.type == "MESH" and not c.hide_get() and not c.hide_render]


def plain(ob):
    return not parts_of(ob) and all(abs(v) < 1e-6 for v in ob.rotation_euler) and all(abs(v - 1) < 1e-6 for v in ob.scale) \
        and ob.parent is None


def export_plain(ob, path):
    was = ob.location.copy()
    hidden = ob.hide_get()
    ob.hide_set(False)
    ob.location = (0.0, 0.0, 0.0)
    deselect_all()
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    fbx(path)
    ob.select_set(False)
    ob.location = was
    ob.hide_set(hidden)
    return ob.data


# The piece and its parts joined on a temporary copy (modifiers, rotation and scale applied; pivot = the piece's origin),
# exported under the piece's own object and mesh names, then removed.
def export_joined(ob, path):
    scene_coll = bpy.context.scene.collection
    copies = []
    for p in [ob] + parts_of(ob):
        c = p.copy()
        c.data = p.data.copy()
        mw = p.matrix_world.copy()
        scene_coll.objects.link(c)
        c.parent = None
        c.matrix_world = mw
        c.hide_set(False)
        copies.append(c)
    deselect_all()
    for c in copies:
        c.select_set(True)
    bpy.context.view_layer.objects.active = copies[0]
    bpy.ops.object.convert(target="MESH")          # modifiers applied on the copies
    # One UV set (Unity reads the first): every part's active one, under the piece's own name; the rest dropped.
    uv_name = ob.data.uv_layers.active.name if ob.data.uv_layers.active else "UVMap"
    for c in copies:
        layers = c.data.uv_layers
        if not layers:
            continue
        keep = layers.active
        for other in [l for l in layers if l != keep]:
            layers.remove(other)
        layers[0].name = uv_name
    if len(copies) > 1:
        bpy.ops.object.join()
    j = bpy.context.view_layer.objects.active
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    old_ob, old_me = ob.name, ob.data.name
    ob.name, ob.data.name = old_ob + "__orig", old_me + "__orig"     # (in memory only: the .blend is never saved)
    j.name, j.data.name = old_ob, old_me
    j.location = (0.0, 0.0, 0.0)
    deselect_all()
    j.select_set(True)
    fbx(path)
    me = j.data
    bpy.data.objects.remove(j, do_unlink=True)
    me.name = old_me + "__export"
    ob.name, ob.data.name = old_ob, old_me
    return me


# Which Blender material each piece wears (slot order), for Build Ball Valley Kit: it puts the matching Unity material
# on the piece (BallValley_Palette = the kit's palette material, Bedroom_Furniture_Set1 = the house's Bedroom_Set_1_Mat).
MANIFEST = os.path.join(MODELS if "--out" in sys.argv else os.path.join(REPO, "Exports", "Gates", "gate3"), "kit_materials.json")
try:
    materials = json.load(open(MANIFEST, encoding="utf-8"))
except (OSError, ValueError):
    materials = {}

for ob in pieces:
    path = os.path.join(MODELS, ob.name + ".fbx")
    joined = not plain(ob)
    n_parts = len(parts_of(ob))
    me = export_joined(ob, path) if joined else export_plain(ob, path)
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    used = sorted({p.material_index for p in me.polygons})
    materials[ob.name] = [me.materials[i].name if i < len(me.materials) and me.materials[i] else "" for i in used] or [""]
    print("exported %-40s %5d tris%s %s -> %s" % (ob.name, tris,
          (" (%d parts joined, rotation/scale applied)" % (n_parts + 1) if n_parts else " (rotation/scale applied)") if joined else "",
          materials[ob.name], os.path.relpath(path, REPO)))
    if joined:
        bpy.data.meshes.remove(me)
json.dump(materials, open(MANIFEST, "w", encoding="utf-8"), indent=1, sort_keys=True)
print("materials ->", os.path.relpath(MANIFEST, REPO))
# Pieces on the house's furniture texture: gate 3's glow map for them too (tools/glow_mask.py; Build Ball Valley Kit uses it).
if bpy.data.materials.get("Bedroom_Furniture_Set1") and "--out" not in sys.argv:
    import runpy
    saved_argv = sys.argv
    sys.argv = [sys.argv[0], "--"]
    try:
        runpy.run_path(os.path.join(HERE, "glow_mask.py"), run_name="__main__")
    finally:
        sys.argv = saved_argv
print("done: %d pieces (the .blend was not saved)" % len(pieces))
