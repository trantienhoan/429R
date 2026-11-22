# Each piece's whole model from -Y, +X, +Y, -X (ortho), to tell which way it faces; plus object transforms.
#   blender -b Bedroom_Furniture_Fix.blend --python render_sides.py -- <out folder>
import bpy, sys, os, json, math
from mathutils import Vector
out = sys.argv[sys.argv.index("--") + 1:][0]
os.makedirs(out, exist_ok=True)
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = scene.render.resolution_y = 260
for o in scene.objects:
    if o.type == "FONT":
        o.hide_render = True
cam = bpy.data.objects.new("_cam", bpy.data.cameras.new("_cam")); scene.collection.objects.link(cam)
cam.data.type = 'ORTHO'; scene.camera = cam
def bounds(objs):
    lo = Vector((1e9,) * 3); hi = Vector((-1e9,) * 3)
    for o in objs:
        for v in o.bound_box:
            w = o.matrix_world @ Vector(v); lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
    return lo, hi
info = {}
for g in bpy.data.collections["Furniture_Fix"].children:
    whole = [o for c in g.children if c.name.endswith(" - whole") for o in c.all_objects if o.type == "MESH"]
    for o in scene.objects:
        if o.type == "MESH":
            o.hide_render = o not in whole
    lo, hi = bounds(whole); mid = (lo + hi) / 2
    for tag, d in (("mY", (0, -1, 0.15)), ("pX", (1, 0, 0.15)), ("pY", (0, 1, 0.15)), ("mX", (-1, 0, 0.15))):
        dv = Vector(d).normalized()
        cam.location = mid + dv * 20
        cam.rotation_euler = (mid - cam.location).to_track_quat('-Z', 'Y').to_euler()
        cam.data.ortho_scale = max((hi - lo).length, 0.3)
        scene.render.filepath = os.path.join(out, f"{g.name}_{tag}.png")
        bpy.ops.render.render(write_still=True)
    info[g.name] = {o.name: {"loc": [round(v, 3) for v in o.location], "rot": [round(math.degrees(v), 1) for v in o.rotation_euler], "scale": [round(v, 3) for v in o.scale], "dims": [round(v, 3) for v in o.dimensions]} for o in whole}
json.dump(info, open(os.path.join(out, "transforms.json"), "w"), indent=1)
print("DONE")
