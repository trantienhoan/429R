def cam_look(name, loc, target, lens, w, h, shift_x=0.0, shift_y=0.0, roll=0.0, fit='HORIZONTAL'):
    cd = bpy.data.cameras.get(name) or bpy.data.cameras.new(name)
    ob = bpy.data.objects.get(name) or bpy.data.objects.new(name, cd)
    if ob.name not in SC.collection.objects and not ob.users_collection: SC.collection.objects.link(ob)
    cd.lens = lens; cd.clip_start = 0.01; cd.clip_end = 300; cd.sensor_fit = fit; cd.sensor_width = 36; cd.sensor_height = 36
    cd.shift_x = shift_x; cd.shift_y = shift_y
    ob.location = Vector(loc)
    d = Vector(target) - Vector(loc)
    q = d.to_track_quat('-Z', 'Y')
    if roll: q = q @ Quaternion((0, 0, 1), math.radians(roll))
    ob.rotation_euler = q.to_euler()
    ob["steam_w"], ob["steam_h"] = w, h
    return ob
def project(cam_name, p, w=None, h=None):
    from bpy_extras.object_utils import world_to_camera_view
    cam = bpy.data.objects[cam_name]
    SC.render.resolution_x, SC.render.resolution_y = int(cam["steam_w"]), int(cam["steam_h"])
    v = world_to_camera_view(SC, cam, Vector(p))
    return round(v.x, 3), round(1 - v.y, 3)   # (x from left, y from top), 0..1
