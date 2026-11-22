# Exports the user's frying pan + baseball bat (and their broken parts) from New_Weapons.blend to Assets/Models/Weapons/
# (user, 2026-11-20: "only pan and baseball can be used ... I already make the broken parts"). Never saves the .blend.
# Each weapon and its pieces are moved so the weapon's pivot (handle end) is at the origin, exported with the axes baked
# (Blender Z up -> Unity Y up, identity object rotation, scale 1), then put back. Also writes grips.json: each weapon's
# Grip empty (the hand's spot) and each broken piece's position, in Unity axes, for the prefab setup.
#   blender -b New_Weapons.blend --python export_weapons.py -- <repo root>
import bpy, sys, os, json
from mathutils import Vector

root = sys.argv[sys.argv.index("--") + 1].rstrip("/\\") + "/"
out_dir = root + "Assets/Models/Weapons/"
os.makedirs(out_dir, exist_ok=True)
JOBS = {
    "Weapon_Frying_Pan": ["Weapon_Frying_Pan_break.000", "Weapon_Frying_Pan_break.001"],
    "Weapon_Baseball_Bat": ["Weapon_Baseball_Bat_cell", "Weapon_Baseball_Bat_cell.001", "Weapon_Baseball_Bat_cell.002"],
}
def to_unity(v):  # Blender (x, y, z) -> Unity (x, y, z) as the FBX exporter's default axes give it (-Z forward, Y up)
    return [round(-v.x, 5), round(v.z, 5), round(-v.y, 5)]

info = {}
def export(objs, path):
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'}, axis_forward='-Z', axis_up='Y',
                             apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
                             use_mesh_modifiers=True, mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False,
                             path_mode='AUTO')
for main, pieces in JOBS.items():
    w = bpy.data.objects[main]
    origin = w.location.copy()
    parts = [bpy.data.objects[p] for p in pieces]
    for o in [w] + parts:
        o.location -= origin
    bpy.context.view_layer.update()
    export([w], out_dir + main + ".fbx")
    export(parts, out_dir + main + "_Broken.fbx")
    grip = next((c for c in w.children if c.type == "EMPTY"), None)
    info[main] = {"grip": to_unity(grip.matrix_world.translation) if grip else None,
                  "pieces": {p.name: to_unity(p.location) for p in parts}}
    for o in [w] + parts:
        o.location += origin
json.dump(info, open(out_dir + "../../../Exports/Weapons_New/tools/grips.json", "w"), indent=1)
print("EXPORTED", json.dumps(info))
