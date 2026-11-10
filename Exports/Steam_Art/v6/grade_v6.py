"""v6 grade: the compositor's own nodes re-tuned (bloom, CDL split tone, saturation, S-curve, vignette)."""
def set_in(node, name, value, kind=None):
    for i in node.inputs:
        if i.name == name and not i.is_linked and hasattr(i, "default_value"):
            dv = i.default_value
            is_vec = hasattr(dv, "__len__") and not isinstance(dv, str)
            if kind == "color" and not is_vec: continue
            if kind == "float" and is_vec: continue
            if isinstance(dv, str): continue
            i.default_value = value
            return True
    return False
def grade_v6(bloom=0.6, thresh=0.8, bsize=0.72, offset=(0.004, 0.0, 0.014), power=(1.0, 1.0, 1.0), slope=(1.05, 1.0, 0.95),
             sat=1.14, curve=((0.18, 0.12), (0.62, 0.68)), vig=(0.22, 0.80, 0.0, 0.9), vig_col=(0.0, 0.002, 0.008)):
    tree = SC.compositing_node_group
    g = tree.nodes["Glare"]
    set_in(g, "Threshold", thresh); set_in(g, "Strength", bloom); set_in(g, "Size", bsize)
    cb = tree.nodes["Color Balance"]
    set_in(cb, "Offset", (*offset, 1.0), "color"); set_in(cb, "Power", (*power, 1.0), "color"); set_in(cb, "Slope", (*slope, 1.0), "color")
    hs = tree.nodes["Hue/Saturation/Value"]; set_in(hs, "Saturation", sat)
    cv = tree.nodes["RGB Curves"]; c = cv.mapping.curves[3]
    while len(c.points) > 2: c.points.remove(c.points[1])
    for x, y in curve: c.points.new(x, y)
    cv.mapping.update()
    mr = tree.nodes["Map Range"]
    set_in(mr, "From Min", vig[0], "float"); set_in(mr, "From Max", vig[1], "float"); set_in(mr, "To Min", vig[2], "float"); set_in(mr, "To Max", vig[3], "float")
    mx = tree.nodes["Mix"]
    for i in mx.inputs:
        if i.name == "B" and not i.is_linked and hasattr(i.default_value, "__len__") and len(i.default_value) == 4:
            i.default_value = (*vig_col, 1.0)
