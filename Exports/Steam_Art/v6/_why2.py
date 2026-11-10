import bpy, os
sc = bpy.context.scene
prefs = bpy.context.preferences.addons['cycles'].preferences
prefs.compute_device_type = 'OPTIX'; prefs.get_devices()
for d in prefs.devices: d.use = (d.type == 'OPTIX')
sc.cycles.device = 'GPU'; sc.cycles.samples = 64; sc.render.resolution_percentage = 50
def shot(tag):
    sc.render.filepath = os.path.join(os.path.dirname(bpy.data.filepath), "v6", "prev", "why2_" + tag + ".png")
    bpy.ops.render.render(write_still=True)
import mathutils
dg = bpy.context.evaluated_depsgraph_get()
hb = bpy.data.objects["pumpkin_boss"].evaluated_get(dg)
lo = mathutils.Vector((1e9,)*3); hi = -lo
for c in hb.bound_box:
    w = hb.matrix_world @ mathutils.Vector(c); lo = mathutils.Vector(map(min, lo, w)); hi = mathutils.Vector(map(max, hi, w))
print("HELPER boss bbox at frame", sc.frame_current, tuple(round(v,2) for v in lo), tuple(round(v,2) for v in hi))
shot("a_as_file")
for o in bpy.data.objects:
    if o.name.startswith("pumpkin_boss"): o.hide_render = True
shot("b_helper_hidden")
print("DONE")
