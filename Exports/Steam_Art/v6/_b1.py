exec(open(os.path.join(HERE, "lib.py"), encoding="utf-8").read(), globals())
exec(open(os.path.join(HERE, "boss_v6.py"), encoding="utf-8").read(), globals())
boss_v6()
# blocking camera: low hero angle, lens shift up (verticals stay straight)
cd = bpy.data.cameras.get("CAM_V6_Main") or bpy.data.cameras.new("CAM_V6_Main")
ob = bpy.data.objects.get("CAM_V6_Main") or bpy.data.objects.new("CAM_V6_Main", cd)
if ob.name not in SC.collection.objects: SC.collection.objects.link(ob)
cd.lens = 26; cd.clip_start = 0.01; cd.clip_end = 300; cd.sensor_fit = 'HORIZONTAL'; cd.sensor_width = 36
ob.location = (-0.10, 1.90, 0.12)
ob.rotation_euler = (math.radians(90 + 3), 0, math.radians(180 - 4))
cd.shift_y = 0.22; cd.shift_x = 0.0
ob["steam_w"], ob["steam_h"] = 1232, 706
R("CAM_V6_Main", pct=50, spp=24, out="block_1", show_logo=False)
