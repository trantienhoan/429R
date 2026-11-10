import bpy, os
sc = bpy.context.scene
prefs = bpy.context.preferences.addons['cycles'].preferences
prefs.compute_device_type = 'OPTIX'; prefs.get_devices()
for d in prefs.devices: d.use = (d.type == 'OPTIX')
sc.cycles.device = 'GPU'; sc.cycles.samples = 64; sc.render.resolution_percentage = 50
lc = bpy.data.collections["Logos"]
vis = [o for o in lc.all_objects if not o.hide_render]
print("LOGO RIG parts still on in your file:", len(vis), sorted(set(o.type for o in vis)))
lights = [o for o in vis if o.type == "LIGHT"]
print("  lights:", len(lights), "total W", round(sum(o.data.energy for o in lights), 1),
      "unlinked:", [o.name for o in lights if not o.light_linking.receiver_collection][:6])
def shot(tag):
    sc.render.filepath = os.path.join(os.path.dirname(bpy.data.filepath), "v6", "prev", "why_" + tag + ".png")
    bpy.ops.render.render(write_still=True)
for o in bpy.data.objects:
    if o.name.startswith(("ST_Spider_", "ST_Hero", "Menace_Pumpkin_Body")): o.hide_render = True
shot("a_file_look")                  # your file as is, spiders/pumpkin hidden
for o in vis: o.hide_render = True
shot("b_logo_rigs_off")              # + old logo rigs off (what my script does)
print("DONE")
