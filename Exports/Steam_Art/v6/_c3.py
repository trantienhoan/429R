from bpy_extras.object_utils import world_to_camera_view
cam = bpy.data.objects["CAM_V6_T1"]
SC.camera = cam
SC.render.resolution_x, SC.render.resolution_y = 1232, 706
for nm, p in [("head", head_w("Head")), ("helmet_top", head_w("Head") + Vector((0,0,0.2))), ("feet", ARM.location), ("fist", head_w("RightHand")), ("boss", BOSS_FACE_V6)]:
    v = world_to_camera_view(SC, cam, Vector(p))
    print(nm, tuple(round(x, 3) for x in v))
print("cam loc", tuple(round(x,3) for x in cam.location), "dir", tuple(round(x,3) for x in cam.matrix_world.to_quaternion() @ Vector((0,0,-1))))
