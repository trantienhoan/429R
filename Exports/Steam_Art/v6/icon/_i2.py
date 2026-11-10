exec(open(os.path.join(HERE, "icon", "icon_scene.py"), encoding="utf-8").read(), globals())
build_icon()
icon_render("icon_t1", pct=50, spp=64)
