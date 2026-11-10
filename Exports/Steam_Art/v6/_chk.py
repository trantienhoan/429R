import bpy, os
sc = bpy.context.scene
prefs = bpy.context.preferences.addons['cycles'].preferences
prefs.compute_device_type = 'OPTIX'; prefs.get_devices()
for d in prefs.devices: d.use = (d.type == 'OPTIX')
sc.cycles.device = 'GPU'
print("CAM", sc.camera.name if sc.camera else None, "frame", sc.frame_current)
arm = bpy.data.objects.get("Pie_Knight_Armature")
print("ARM", arm.location[:], "action", arm.animation_data.action.name if arm and arm.animation_data and arm.animation_data.action else None)
for o in bpy.data.objects:
    if o.name.startswith("Pie_Knight"):
        print("K", o.name, o.type, "parent", o.parent.name if o.parent else None, o.parent_bone, "hide_r", o.hide_render, "mods", [m.type for m in o.modifiers])
for b in arm.pose.bones:
    if any(abs(s - 1) > 1e-3 for s in b.scale): print("SCALED", b.name, tuple(round(s, 2) for s in b.scale))
sc.cycles.samples = 128
sc.render.resolution_percentage = 100
sc.render.film_transparent = False
sc.render.image_settings.file_format = 'PNG'
sc.render.filepath = os.path.join(os.path.dirname(bpy.data.filepath), "v6", "prev", "user_pose_main_1858.png")
bpy.ops.render.render(write_still=True)
print("DONE")
