SC.camera = bpy.data.objects["CAM_ICON"]
SC.render.resolution_x = SC.render.resolution_y = 1024
SC.render.resolution_percentage = 100
SC.render.film_transparent = True
bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(os.path.join(HERE, "..", "Icon_v6_work.blend")))
print("SAVED")
