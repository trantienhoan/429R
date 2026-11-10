arm.location = (40, 0, 0); arm.rotation_euler = (0, 0, math.radians(180))
bpy.context.view_layer.update()
lo, hi = wbbox([body.name]); c = (lo + hi) / 2
print("boss size", tuple(round(v,2) for v in (hi - lo)))
cam = tmpcam("CAM_TMP_BB", c + Vector((0.0, 9.0, 0.3)), c + Vector((0, 0, 0.3)), lens=35, w=300, h=300)
if "V6_TMP_L" not in bpy.data.objects:
    lt = bpy.data.objects.new("V6_TMP_L", bpy.data.lights.new("V6_TMP_L", "SUN")); col.objects.link(lt)
    lt.data.energy = 3.0; lt.rotation_euler = (math.radians(50), 0, math.radians(200))
fr = SC.frame_current
n = 0
for an in CLIPS:
    a = acts.get(an)
    if not a: print("missing", an); continue
    ad = arm.animation_data or arm.animation_data_create(); ad.action = a
    if a.slots: ad.action_slot = a.slots[0]
    f0, f1 = a.frame_range
    for i in range(6):
        f = int(round(f0 + (f1 - f0) * i / 5.0))
        SC.frame_set(f)
        R("CAM_TMP_BB", pct=100, spp=8, out="bb_%02d_%s_%03d" % (CLIPS.index(an), an.replace('pumpkin_boss_', ''), f), show_logo=False)
        n += 1
SC.frame_set(fr)
print("N", n)
