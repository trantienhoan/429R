"""Exports remodelled models from a remodel .blend back to their game files (2026-11-16).
  blender -b Exports/Remodel/<Area>_Remodel.blend --python Exports/Remodel/tools/export_area.py -- [only=Name1,Name2] [out=<folder>] [dry]
For each "MODEL <stem>" empty: its objects (not the hidden "(old broken)" ones) are written to the empty's "source" FBX
(or to out=<folder>/<stem>.fbx), placed relative to the empty and given back the transform the old file had
(obj["export_matrix"]: the rotation/scale baked out by normalize_area.py), so Unity sees the same pivot, size and
facing as before. Rigged/animated models go out with their armature and actions. Backs up an FBX before replacing it."""
import bpy, sys, os, shutil, time
from mathutils import Matrix
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ONLY = next((a[5:].split(",") for a in ARGS if a.startswith("only=")), None)
OUTDIR = next((a[4:] for a in ARGS if a.startswith("out=")), None)
DRY = "dry" in ARGS
REPO = os.path.abspath(os.path.join(os.path.dirname(bpy.data.filepath), "..", ".."))
BACKUP = os.path.join(REPO, "Exports", "Remodel", "_fbx_backups", time.strftime("%Y-%m-%d_%H%M"))

def model_objects(empty):
    out = []
    def walk(o):
        for c in o.children:
            if any(col.name.endswith("(old broken)") for col in c.users_collection): continue
            out.append(c); walk(c)
    walk(empty)
    return out

for empty in [o for o in bpy.data.objects if o.name.startswith("MODEL ")]:
    stem = empty.name[6:]
    if ONLY and stem not in ONLY: continue
    src = os.path.join(REPO, empty["source"])
    dst = os.path.join(OUTDIR, stem + ".fbx") if OUTDIR else src
    objs = model_objects(empty)
    # a temporary scene with copies, the empty as the origin
    tmp = bpy.data.scenes.new("_export_" + stem)
    copies = {}
    # Unity finds the meshes by their names in the file: the copies take the exact names (originals renamed meanwhile)
    renamed = {}
    for o in objs:
        name = o.name
        o.name = name + "__src"
        renamed[o] = name
        c = o.copy()
        if o.data is not None and o.type == 'MESH':
            mesh_name = o.data.name
            c.data = o.data.copy()
        c.name = name
        tmp.collection.objects.link(c)
        copies[o] = c
    for o, c in copies.items():
        c.parent = copies.get(o.parent)
        if c.parent is None:
            c.matrix_parent_inverse = Matrix.Identity(4)
        if "export_matrix" in o:
            em = o["export_matrix"]
            old = Matrix([em[0:4], em[4:8], em[8:12], em[12:16]])
            loc, rot, sca = old.decompose()
            # the vertices carry rotation/scale now: move them back into the object's transform
            if c.type == 'MESH':
                bm = o.get("baked_matrix")
                baked_m = Matrix([bm[0:4], bm[4:8], bm[8:12], bm[12:16]]) if bm else Matrix.LocRotScale(None, rot, sca)
                c.data.transform(baked_m.inverted())
            c.matrix_basis = old
        elif c.parent is None:
            # placed relative to the MODEL empty (the layout offset is dropped)
            c.matrix_basis = empty.matrix_world.inverted() @ o.matrix_world
    if DRY:
        print("DRY", stem, "->", dst, len(copies), "objects")
        for c in list(copies.values()): bpy.data.objects.remove(c)
        bpy.data.scenes.remove(tmp)
        for o, name in renamed.items(): o.name = name
        continue
    if os.path.exists(dst) and not OUTDIR:
        os.makedirs(BACKUP, exist_ok=True); shutil.copy2(dst, os.path.join(BACKUP, os.path.basename(dst)))
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    win = bpy.context.window
    old_scene = win.scene if win else None
    if win: win.scene = tmp
    with bpy.context.temp_override(scene=tmp):
        bpy.ops.object.select_all(action='DESELECT') if False else None
        bpy.ops.export_scene.fbx(filepath=dst, use_selection=False, use_active_collection=False,
                                 object_types={'MESH', 'ARMATURE', 'EMPTY'}, apply_unit_scale=True, apply_scale_options='FBX_SCALE_NONE',
                                 bake_space_transform=False, axis_forward='-Z', axis_up='Y', add_leaf_bones=False,
                                 mesh_smooth_type='OFF', use_mesh_modifiers=True, bake_anim=True, bake_anim_use_nla_strips=False,
                                 bake_anim_use_all_actions=False, path_mode='AUTO')
    if win and old_scene: win.scene = old_scene
    for c in list(copies.values()):
        data = c.data if c.type == 'MESH' else None
        bpy.data.objects.remove(c)
        if data is not None and data.users == 0: bpy.data.meshes.remove(data)
    bpy.data.scenes.remove(tmp)
    for o, name in renamed.items(): o.name = name
    print("EXPORTED", stem, "->", dst, len(copies), "objects")
