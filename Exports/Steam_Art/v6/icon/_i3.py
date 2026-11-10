exec(open(os.path.join(HERE, "icon", "icon_scene.py"), encoding="utf-8").read(), globals())
P.update(ortho=0.66, aim_up=-0.035, aim_side=-0.035, moon_r=0.20, moon_up=0.075, moon_side=0.035,
         moon_str=6.5, crater=0.38, halo_str=0.9, halo_k=0.045, halo_col=(0.45, 0.6, 1.0),
         sky_in=(0.018, 0.026, 0.13), sky_out=(0.004, 0.004, 0.03), sky_r=0.34,
         ring_r=0.307, ring_tube=0.017)
build_icon()
icon_render("icon_t2", pct=50, spp=64)
