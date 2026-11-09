# Bakes a soft light from above (and a little from the front) onto the knight's armour atlas: how much light each texel
# gets, no colour -> knight_toplight.png. Rest pose, only the knight's parts. Run on Steam_Art.blend; NEVER saves it.
import bpy, os, math
HERE = os.path.dirname(os.path.abspath(__file__))
sc = bpy.context.scene
arm = bpy.data.objects["Pie_Knight_Armature"]
arm.data.pose_position = "REST"
knight = [o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith("Pie_Knight")]
for o in bpy.data.objects:
    if o.type in ("MESH", "CURVE", "VOLUME", "CURVES", "FONT"):
        o.hide_render = o not in knight
    if o.type == "LIGHT":
        o.hide_render = True
for o in knight:
    o.hide_render = False
    o.hide_set(False)
# world off, one big soft light above-front of the knight (he faces his armature's +Z, i.e. world -Y-ish here)
if sc.world and sc.world.node_tree:
    for n in sc.world.node_tree.nodes:
        if n.type == "BACKGROUND":
            n.inputs["Strength"].default_value = 0.0
M = arm.matrix_world.to_3x3()
from mathutils import Vector
F = (M @ Vector((0, 0, 1))).normalized()
up = Vector((0, 0, 1))
L = bpy.data.objects.new("KN_TopLight", bpy.data.lights.new("KN_TopLight", "SUN"))
sc.collection.objects.link(L)
L.data.energy = 3.0
L.data.angle = math.radians(60)               # very soft
d = (up * 1.0 + F * 0.45).normalized()        # comes from above and a little in front
L.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
sc.render.engine = "CYCLES"
sc.cycles.samples = 96
img = bpy.data.images.new("KN_TOP", 2048, 2048, alpha=False, float_buffer=True)
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
sc.render.bake.use_pass_direct = True
sc.render.bake.use_pass_indirect = True
sc.render.bake.use_pass_color = False
bpy.ops.object.bake(type="DIFFUSE", pass_filter={"DIRECT", "INDIRECT"})
img.filepath_raw = os.path.join(HERE, "knight_toplight.png")
img.file_format = "PNG"
img.save()
print("BAKED TOPLIGHT", img.filepath_raw)
