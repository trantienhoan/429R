# Renders New_Weapons.blend: each weapon from two angles, side by side (never saves). -- <out.png>
import bpy, sys, math
from mathutils import Vector
out = sys.argv[sys.argv.index("--") + 1]
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x, scene.render.resolution_y = 1600, 900
scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.18, 0.18, 0.2, 1)
cam = bpy.data.objects.new("_cam", bpy.data.cameras.new("_cam")); scene.collection.objects.link(cam); scene.camera = cam
cam.data.type = 'ORTHO'
weps = [o for o in scene.objects if o.type == "MESH"]
# a second copy of each, turned, to show the other side
copies = []
for o in weps:
    c = o.copy(); c.data = o.data; scene.collection.objects.link(c)
    c.location = o.location + Vector((0, 0.9, 0)); c.rotation_euler = (0, 0, math.radians(90)); copies.append(c)
lo = Vector((1e9,) * 3); hi = Vector((-1e9,) * 3)
bpy.context.view_layer.update()
for o in weps + copies:
    for v in o.bound_box:
        w = o.matrix_world @ Vector(v); lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
mid = (lo + hi) / 2
d = Vector((1.0, -0.35, 0.25)).normalized()
cam.location = mid + d * 10
cam.rotation_euler = (mid - cam.location).to_track_quat('-Z', 'Y').to_euler()
cam.data.ortho_scale = max(hi.x - lo.x, hi.y - lo.y, hi.z - lo.z) * 1.6
scene.render.filepath = out
bpy.ops.render.render(write_still=True)
print("rendered", out)
