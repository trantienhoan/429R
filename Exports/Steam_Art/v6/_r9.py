PICKS = [('pumpkin_boss_attack', [430, 445, 456, 470]), ('pumpkin_boss_punch_R', [25, 35, 45]), ('pumpkin_boss_throw', [25, 35, 45]),
         ('pumpkin_boss_jump_slam', [35, 54, 65]), ('pumpkin_boss_cyclone', [55, 73, 90]), ('pumpkin_boss_come', [215, 235, 255]),
         ('pumpkin_boss_laugh', [30, 50]), ('pumpkin_boss_vomit', [40, 60])]
n = 0
for an, frames in PICKS:
    a = acts[an]
    ad = arm.animation_data; ad.action = a
    if a.slots: ad.action_slot = a.slots[0]
    for f in frames:
        SC.frame_set(f)
        bpy.context.view_layer.update()
        lo, hi = wbbox([body.name]); c = (lo + hi) / 2; s = max(hi.x - lo.x, hi.z - lo.z)
        cam = tmpcam("CAM_TMP_BB", c + Vector((0.0, s * 1.9, 0.0)), c, lens=35, w=320, h=320)
        R("CAM_TMP_BB", pct=100, spp=8, out="bc_%02d_%s_%03d" % (n, an.replace('pumpkin_boss_', ''), f), show_logo=False)
        n += 1
print("size", tuple(round(v, 2) for v in (hi - lo)))
