# Headless: blender -b --factory-startup --python make_lair_jar.py
# The lair's glowing jar (user 2026-11-25: jars of the Fairy's drained green light on the shelves), remodelled from the
# user's own Empty_Bottle_4 (Assets/Models/Livingroom_Stuffs/Empty_Bottle_4.fbx, same UVs and texture): the neck and cork
# widened into a mason jar's mouth and lid, the neck shortened. Writes Exports/Lair/Lair_Story_Props.blend (its own
# file) and Assets/Models/Lair/Lair_Glow_Jar.fbx. The green glow is the Unity material (Tools > 429 Game > Lair > Set Up
# Lair Story Props).
import bpy, bmesh, os, math
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", ".."))
SRC = os.path.join(REPO, "Assets", "Models", "Livingroom_Stuffs", "Empty_Bottle_4.fbx")
OUT_FBX = os.path.join(REPO, "Assets", "Models", "Lair", "Lair_Glow_Jar.fbx")
BLEND = os.path.join(HERE, "Lair_Story_Props.blend")

for o in list(bpy.data.objects):
    bpy.data.objects.remove(o)
bpy.ops.import_scene.fbx(filepath=SRC)
ob = next(o for o in bpy.data.objects if o.type == 'MESH')
bpy.context.view_layer.objects.active = ob
ob.select_set(True)
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
me = ob.data
bm = bmesh.new()
bm.from_mesh(me)
zs = [v.co.z for v in bm.verts]
z0, z1 = min(zs), max(zs)
H = z1 - z0
rad = lambda v: math.hypot(v.co.x, v.co.y)
body = max(rad(v) for v in bm.verts if v.co.z < z0 + 0.55 * H)
# The shoulder: the lowest height where the outline has narrowed below 80% of the body.
rings = sorted({round(v.co.z, 4) for v in bm.verts})
ring_r = {z: max(rad(v) for v in bm.verts if abs(v.co.z - z) < 1e-4) for z in rings}
shoulder = next(z for z in rings if z > z0 + 0.4 * H and ring_r[z] < 0.8 * body)
neck = min(ring_r[z] for z in rings if z > shoulder)
mouth = 0.82 * body
print("BOTTLE height", round(H, 3), "body r", round(body, 3), "shoulder", round(shoulder - z0, 3), "neck r", round(neck, 3), "mouth r", round(mouth, 3))
for v in bm.verts:
    if v.co.z <= shoulder:
        continue
    r = rad(v)
    if r < 1e-6:
        new_r = 0.0
    elif r >= 0.7 * neck:
        # The neck becomes the jar's mouth; the lip stays only a little wider than it (not a brim), the cork fills it.
        new_r = mouth + (r - neck) * 0.6
    else:
        new_r = r * mouth / neck
    target = new_r                                               # straight up from the shoulder: a jar, no waist
    v.co.x *= target / r if r > 1e-6 else 1.0
    v.co.y *= target / r if r > 1e-6 else 1.0
    v.co.z = shoulder + (v.co.z - shoulder) * 0.62               # a short, wide neck
bm.to_mesh(me)
bm.free()
ob.name = "Lair_Glow_Jar"
me.name = "Lair_Glow_Jar"
print("JAR dims", tuple(round(d, 3) for d in ob.dimensions), "faces", len(me.polygons))
os.makedirs(os.path.dirname(OUT_FBX), exist_ok=True)
for o in bpy.context.view_layer.objects:
    o.select_set(o == ob)
bpy.ops.export_scene.fbx(filepath=OUT_FBX, use_selection=True, object_types={'MESH'},
                         apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
                         axis_forward='-Z', axis_up='Y', mesh_smooth_type='OFF', add_leaf_bones=False,
                         bake_anim=False, path_mode='AUTO', use_mesh_modifiers=True)
print("EXPORTED", OUT_FBX)
bpy.ops.wm.save_as_mainfile(filepath=BLEND)
print("SAVED", BLEND)
