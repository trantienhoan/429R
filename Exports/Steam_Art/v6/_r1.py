# temp cameras: top-down ortho and a wide 3/4 view (deleted after)
def tmpcam(name, loc, rot, ortho=None, lens=24):
    cd = bpy.data.cameras.new(name)
    if ortho:
        cd.type = 'ORTHO'; cd.ortho_scale = ortho
    cd.lens = lens
    cd.clip_end = 200
    ob = bpy.data.objects.new(name, cd)
    SC.collection.objects.link(ob)
    ob.location = loc; ob.rotation_euler = rot
    ob["steam_w"], ob["steam_h"] = 1200, 900
    return ob
top = tmpcam("CAM_TMP_Top", (0, -1.5, 12), (0, 0, 0), ortho=9.0)
side = tmpcam("CAM_TMP_Wide", (3.2, 3.6, 2.2), (math.radians(68), 0, math.radians(140)), lens=20)
R("CAM_TMP_Top", pct=60, spp=16, out="plan_top", show_logo=False)
R("CAM_TMP_Wide", pct=60, spp=16, out="plan_wide", show_logo=False)
for ob in (top, side):
    bpy.data.objects.remove(ob, do_unlink=True)
