import numpy as np
for n in ("Pie_Knight_Sword", "Pie_Knight_Shield"):
    o = bpy.data.objects[n]
    co = np.array([v.co[:] for v in o.data.vertices])
    print(n, "local min", co.min(0).round(3), "max", co.max(0).round(3), "nverts", len(co), "parent", o.parent_type, o.parent_bone, "mods", [m.type for m in o.modifiers], "vgroups", [g.name for g in o.vertex_groups][:6])
    # where along the long axis is the grip (thin part)? width profile along the longest axis
    ax = int(np.argmax(co.max(0) - co.min(0)))
    lo, hi = co[:, ax].min(), co[:, ax].max()
    for i in range(12):
        a = lo + (hi - lo) * i / 12; b = lo + (hi - lo) * (i + 1) / 12
        m = (co[:, ax] >= a) & (co[:, ax] < b)
        if m.any():
            other = [k for k in range(3) if k != ax]
            w = (co[m][:, other].max(0) - co[m][:, other].min(0)).round(3)
            print("  slice", i, round(a, 3), "n", int(m.sum()), "width", w)
    print("  matrix_parent_inverse", [list(map(lambda x: round(x, 3), r)) for r in o.matrix_parent_inverse])
    print("  local loc/rot/scale", tuple(round(v,3) for v in o.location), tuple(round(v,3) for v in o.rotation_euler), tuple(round(v,3) for v in o.scale))
