import bpy, os, numpy as np
sc = bpy.context.scene
prefs = bpy.context.preferences.addons['cycles'].preferences
prefs.compute_device_type = 'OPTIX'; prefs.get_devices()
for d in prefs.devices: d.use = (d.type == 'OPTIX')
sc.cycles.device = 'GPU'; sc.cycles.samples = 32; sc.render.resolution_percentage = 25
tmp = [o for o in bpy.data.objects if o.name.startswith("V6_TMP_")]
print("TMP objects:", [(o.name, o.type, o.data.type if o.type == 'LIGHT' else '', round(o.data.energy, 2) if o.type == 'LIGHT' else '', o.hide_render, [c.name for c in o.users_collection]) for o in tmp])
for o in tmp: o.hide_render = True
p = os.path.join(os.path.dirname(bpy.data.filepath), "v6", "prev", "bis_9_tmp_hidden.png")
sc.render.filepath = p; bpy.ops.render.render(write_still=True)
img = bpy.data.images.load(p, check_existing=False)
print("BIS 9_tmp_hidden mean", (np.array(img.pixels[:]).reshape(-1, 4)[:, :3].mean(0) * 255).round(1))
