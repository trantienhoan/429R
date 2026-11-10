P.update(sword_tilt=16.0, sword_scale=0.70)
icon_sword()
icon_render("icon_final2", pct=100, spp=256)
icon_render("icon_final2_mask", pct=100, spp=32, mask=True)
SC.camera = bpy.data.objects["CAM_ICON"]
SC.render.resolution_x = SC.render.resolution_y = 1024; SC.render.resolution_percentage = 100; SC.render.film_transparent = True
bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(os.path.join(HERE, "..", "Icon_v6_work.blend")))
print("SAVED")
