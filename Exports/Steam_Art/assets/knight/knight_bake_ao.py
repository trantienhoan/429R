# Bakes ambient occlusion of the Pie Knight (rest pose, only the knight's parts as occluders) onto his armour atlas UVs.
# Run on Steam_Art.blend; NEVER saves the .blend. Writes K:/WORK_2026/08_04/_claude/steam_art/knight/knight_ao.png
import bpy, os
OUT = r"K:/WORK_2026/08_04/_claude/steam_art/knight"
os.makedirs(OUT, exist_ok=True)
sc = bpy.context.scene
arm = bpy.data.objects["Pie_Knight_Armature"]
arm.data.pose_position = "REST"
knight = [o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith("Pie_Knight")]
for o in bpy.data.objects:
    o.hide_render = o not in knight and o.type in ("MESH", "CURVE", "VOLUME", "CURVES", "FONT")
for o in knight:
    o.hide_render = False
    o.hide_viewport = False
    o.hide_set(False)
sc.render.engine = "CYCLES"
sc.cycles.samples = int(os.environ.get("AO_SPP", "96"))
sc.world.light_settings.distance = float(os.environ.get("AO_DIST", "0.03"))
img = bpy.data.images.new("KN_AO", 2048, 2048, alpha=False, float_buffer=True)
mats = {s.material for o in knight for s in o.material_slots if s.material}
for m in mats:
    n = m.node_tree.nodes.new("ShaderNodeTexImage")
    n.image = img
    n.name = "KN_AO_TARGET"
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
bpy.ops.object.bake(type="AO")
img.filepath_raw = os.path.join(OUT, "knight_ao.png")
img.file_format = "PNG"
img.save()
print("BAKED AO", img.filepath_raw)
