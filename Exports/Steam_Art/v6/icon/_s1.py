import os, math, numpy as np
for mod in ("lib.py", "posekit.py", "cam_v6.py", "light_v6.py"):
    exec(open(os.path.join(HERE, mod), encoding="utf-8").read(), globals())
exec(open(os.path.join(HERE, "icon", "icon_scene.py"), encoding="utf-8").read(), globals())
prefs = bpy.context.preferences.addons['cycles'].preferences
prefs.compute_device_type = 'OPTIX'; prefs.get_devices()
for d in prefs.devices: d.use = (d.type == 'OPTIX')
SC.cycles.device = 'GPU'
P.update(sword_tilt=10.0, sword_scale=0.82, ring_gold=(1.0, 0.70, 0.28), ring_light=10.0, moon_str=3.0, moon_col=(1.0, 0.92, 0.74),
         halo_str=1.0, embers=12)
build_icon()
eyes_boost(1.8)
def measure(tag):
    p = icon_render("_m_" + tag, pct=25, spp=4, mask=True)
    img = bpy.data.images.load(p, check_existing=False)
    N = img.size[0]
    a = np.array(img.pixels[:], dtype=np.float32).reshape(N, N, 4)[::-1, :, 3]
    bpy.data.images.remove(img)
    yy, xx = np.mgrid[0:N, 0:N] + 0.5
    m = 0.04 * N; R = 0.16 * N
    cx = np.clip(xx, m + R, N - m - R); cy = np.clip(yy, m + R, N - m - R)
    inside = (xx >= m) & (xx <= N - m) & (yy >= m) & (yy <= N - m) & (np.hypot(xx - cx, yy - cy) <= R)
    out_px = int(((a > 0.05) & ~inside).sum())
    q = (a > 0.05) & (xx < N / 2) & (yy < N / 2)
    reach = (np.hypot(xx - N / 2, yy - N / 2)[q].max() / N) if q.any() else 0
    ys, xs = np.where(a > 0.05)
    return out_px, reach, xs.min() / N, ys.min() / N
for tilt, sc in [(10, 0.82), (10, 0.74), (16, 0.74), (16, 0.70), (22, 0.70), (22, 0.66), (4, 0.74), (12, 0.70)]:
    P.update(sword_tilt=tilt, sword_scale=sc)
    icon_sword()
    out_px, reach, x0, y0 = measure("t%d_s%d" % (tilt, sc * 100))
    print("tilt %2d scale %.2f -> outside safe zone %4d px | reach %.3f (ring outer 0.489) | left %.3f top %.3f" % (tilt, sc, out_px, reach, x0, y0))
