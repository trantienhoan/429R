def tmpcam(name, loc, look_at, lens=50, w=900, h=900):
    cd = bpy.data.cameras.new(name); cd.lens = lens; cd.clip_start = 0.01; cd.clip_end = 200
    ob = bpy.data.objects.new(name, cd); SC.collection.objects.link(ob)
    ob.location = loc
    d = Vector(look_at) - Vector(loc)
    ob.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    ob["steam_w"], ob["steam_h"] = w, h
    return ob
k = Vector((-0.27, 0.13, 0.22))
c1 = tmpcam("CAM_TMP_K1", k + Vector((-0.25, 1.2, 0.12)), k, lens=50)
c2 = tmpcam("CAM_TMP_K2", k + Vector((-1.2, 0.1, 0.1)), k, lens=50)
R("CAM_TMP_K1", pct=60, spp=24, out="knight_front", show_logo=False)
R("CAM_TMP_K2", pct=60, spp=24, out="knight_side", show_logo=False)
for ob in (c1, c2): bpy.data.objects.remove(ob, do_unlink=True)
