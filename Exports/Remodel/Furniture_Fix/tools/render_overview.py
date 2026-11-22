# Renders an overview of Bedroom_Furniture_Fix.blend (never saves it). blender -b <blend> --python render_overview.py -- <out.png>
import bpy, sys, math
from mathutils import Vector
a = sys.argv[sys.argv.index("--") + 1:]
out = a[0]
parts = int(a[1]) if len(a) > 1 else 1
scene = bpy.context.scene
objs = [o for o in scene.objects if o.type == "MESH"]
lo = Vector((1e9,) * 3); hi = Vector((-1e9,) * 3)
for o in objs:
    for v in o.bound_box:
        w = o.matrix_world @ Vector(v); lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
mid = (lo + hi) / 2
cam = bpy.data.objects.new("_claude_cam", bpy.data.cameras.new("_claude_cam"))
scene.collection.objects.link(cam)
cam.data.type = 'ORTHO'
cam.data.ortho_scale = (hi.x - lo.x) * 1.05
d = Vector((0, -1, 0.35)).normalized()
cam.location = mid + d * 60
cam.rotation_euler = (mid - cam.location).to_track_quat('-Z', 'Y').to_euler()
cam.data.clip_end = 200
scene.camera = cam
for eng in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE", "BLENDER_WORKBENCH"):
    try:
        scene.render.engine = eng; break
    except TypeError:
        pass
scene.render.resolution_x = 2400
scene.render.resolution_y = int(2400 * max(0.25, (hi.y - lo.y + hi.z - lo.z) * 0.75 / (hi.x - lo.x)) )
scene.render.film_transparent = False
span = (hi.x - lo.x) / parts
cam.data.ortho_scale = span * 1.08
scene.render.resolution_x = 1800
scene.render.resolution_y = 700
for i in range(parts):
    c = Vector((lo.x + span * (i + 0.5), mid.y, mid.z * 0.8))
    cam.location = c + d * 60
    cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = out.replace(".png", f"_{i + 1}.png")
    bpy.ops.render.render(write_still=True)
print("rendered", scene.render.engine, scene.render.resolution_x, scene.render.resolution_y, "bounds", lo, hi)
