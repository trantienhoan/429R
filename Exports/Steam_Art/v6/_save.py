for o in [o for o in bpy.data.objects if o.name.startswith("CAM_TMP_") or o.name.startswith("CAM_V6_T")]:
    bpy.data.objects.remove(o, do_unlink=True)
cam = bpy.data.objects["CAM_V6_Main_Capsule_1232x706"]
SC.camera = cam
SC.render.resolution_x, SC.render.resolution_y, SC.render.resolution_percentage = 1232, 706, 100
SC.cycles.samples = 128
bpy.data.collections["V6_TMP_BossBrowse"].hide_viewport = True
p = os.path.join(HERE, "..", "Steam_Art_v6_pose.blend")
bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(p), copy=True)
print("SAVED", os.path.abspath(p))
