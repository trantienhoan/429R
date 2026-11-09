# Bakes the knight's surface directions (object-space normals, with the normal map's detail) onto his armour atlas ->
# knight_normal_os.png; and writes knight_frame.txt with the world "up" and "front" in that object space, so
# improve_knight_texture.py can shade "lit from above" without any cast shadows. Rest pose. NEVER saves the .blend.
import bpy, os
from mathutils import Vector
HERE = os.path.dirname(os.path.abspath(__file__))
sc = bpy.context.scene
arm = bpy.data.objects["Pie_Knight_Armature"]
arm.data.pose_position = "REST"
knight = [o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith("Pie_Knight")]
m0 = knight[0].matrix_world.to_3x3().normalized()
for o in knight:
    d = (o.matrix_world.to_3x3().normalized() - m0)
    print("FRAME", o.name, "differs" if max(abs(x) for r in d for x in r) > 1e-3 else "same")
for o in bpy.data.objects:
    if o.type in ("MESH", "CURVE", "VOLUME", "CURVES", "FONT"):
        o.hide_render = o not in knight
for o in knight:
    o.hide_render = False
    o.hide_set(False)
sc.render.engine = "CYCLES"
sc.cycles.samples = 4
img = bpy.data.images.new("KN_NRM", 2048, 2048, alpha=False, float_buffer=True)
img.colorspace_settings.name = "Non-Color"
for m in {s.material for o in knight for s in o.material_slots if s.material}:
    n = m.node_tree.nodes.new("ShaderNodeTexImage")
    n.image = img
    for x in m.node_tree.nodes:
        x.select = False
    n.select = True
    m.node_tree.nodes.active = n
bpy.ops.object.select_all(action="DESELECT")
for o in knight:
    o.select_set(True)
bpy.context.view_layer.objects.active = knight[0]
sc.render.bake.margin = 16
sc.render.bake.use_clear = True
sc.render.bake.normal_space = "OBJECT"
bpy.ops.object.bake(type="NORMAL")
img.filepath_raw = os.path.join(HERE, "knight_normal_os.png")
img.file_format = "PNG"
img.save()
inv = m0.inverted()
A3 = arm.matrix_world.to_3x3()
front = (A3 @ Vector((0, 0, 1))).normalized()          # the knight faces his armature's +Z
up_os = (inv @ Vector((0, 0, 1))).normalized()
front_os = (inv @ front).normalized()
open(os.path.join(HERE, "knight_frame.txt"), "w").write("%f %f %f\n%f %f %f\n" % (*up_os, *front_os))
print("BAKED NORMALS", img.filepath_raw, "up_os", tuple(round(v, 3) for v in up_os), "front_os", tuple(round(v, 3) for v in front_os))
