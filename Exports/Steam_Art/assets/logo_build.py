"""Rebuilds Exports/Steam_Art/assets/Logo_429.blend from scratch: the "429" title logo, v2.

Run headless (from anywhere):
    "E:/Steam/steamapps/common/Blender/blender.exe" -b --factory-startup --python logo_build.py -- [options]

Options (all optional):
    style=A|B|C      letterform/frame direction (default: the chosen one, see STYLE_DEFAULT)
    save=0|1         save Logo_429.blend (default 1)
    render=<png>     also render a test image with the test rig
    samples=<n>      Cycles samples for the test render (default 96)
    res=<w>x<h>      test render size (default 1280x720)
    view=front|three test camera: straight on (default) or a 3/4 view from the left
    sheet=1          build A, B and C one after another, render each, stack them into logo_tests/01_directions.png
    transparent=1    test render on a transparent background (like the Steam library logo)
    border=x0,y0,x1,y1  render only that part of the frame (0..1), for close-ups
    thumb=1          also write a <png>_thumb.png downscaled to Small Capsule logo size (about 190 px wide)
    stem=0|1         the gold pumpkin stem + leaf + vine on top of the 9 (default 0 = off, the logo is just "429")

Everything is procedural. Style D (the chosen one) uses hand-drawn digit outlines (path_loop / custom_smooth_loops),
lofted along a bevel profile into one mesh per digit: the gold frame rings are distance-true insets of the outline
(sdf_offsets), the enamel is one inlaid sheet (inlay_face) the frame's inner wall dips through, a dark back plate
(merged SDF outline of all digits) ties the digits together. An optional gold pumpkin stem with a curling vine and a
gold-rimmed enamel leaf can sit on the 9 (stem=1; off by default since r3). Styles A (faceted custom), B (Cooper Black) and C (Showcard Gothic) are the
explored alternatives, kept for the record.
Facing: the logo FRONT faces -Y (a viewer standing at -Y looking toward +Y reads it). Width 2.0, origin at
the centre of the lettering, Z up.
"""
import bpy, bmesh, math, sys, os
from mathutils import Vector, Matrix, geometry

HERE = os.path.dirname(os.path.abspath(__file__))
OUT_BLEND = os.path.join(HERE, "Logo_429.blend")
TESTS = os.path.join(HERE, "logo_tests")
STYLE_DEFAULT = "E"
LOGO_WIDTH = 2.0

SHOW_STEM = False               # r3 (2026-11-08): the user asked for the pumpkin top on the 9 to go; stem=1 brings it back

ARGS = {}
if "--" in sys.argv:
    for a in sys.argv[sys.argv.index("--") + 1:]:
        if "=" in a:
            k, v = a.split("=", 1)
            ARGS[k] = v
if "stem" in ARGS:
    SHOW_STEM = ARGS["stem"] == "1"


# ====================================================================== 2D helpers
def signed_area(pts):
    a = 0.0
    for i in range(len(pts)):
        p, q = pts[i - 1], pts[i]
        a += p.x * q.y - q.x * p.y
    return a * 0.5


def point_in_poly(p, poly):
    inside = False
    n = len(poly)
    for i in range(n):
        a, b = poly[i - 1], poly[i]
        if (a.y > p.y) != (b.y > p.y):
            x = a.x + (p.y - a.y) * (b.x - a.x) / (b.y - a.y)
            if x > p.x:
                inside = not inside
    return inside


def clean_loop(pts, eps=1e-5):
    out = []
    for p in pts:
        if not out or (p - out[-1]).length > eps:
            out.append(p)
    if len(out) > 2 and (out[0] - out[-1]).length <= eps:
        out.pop()
    # drop exactly collinear middle points
    res = []
    n = len(out)
    for i in range(n):
        a, b, c = out[i - 1], out[i], out[(i + 1) % n]
        e0, e1 = (b - a), (c - b)
        if e0.length < eps or e1.length < eps:
            continue
        cr = e0.x * e1.y - e0.y * e1.x
        if abs(cr) / (e0.length * e1.length) < 1e-4 and e0.dot(e1) > 0:
            continue
        res.append(b)
    return res


def orient_loops(loops):
    """Outer loops CCW, holes CW (material always on the LEFT of travel)."""
    res = []
    for i, L in enumerate(loops):
        depth = sum(1 for j, M in enumerate(loops) if j != i and point_in_poly(L[0], M))
        hole = depth % 2 == 1
        a = signed_area(L)
        if (a > 0) == hole:
            L = list(reversed(L))
        res.append(L)
    return res


def offset_loop(pts, d, miter_limit=4.0):
    """Offset a closed loop by d toward the material (left of travel). Same vertex count; folds are collapsed."""
    n = len(pts)
    out = []
    for i in range(n):
        p0, p1, p2 = pts[i - 1], pts[i], pts[(i + 1) % n]
        e0 = (p1 - p0).normalized()
        e1 = (p2 - p1).normalized()
        n0 = Vector((-e0.y, e0.x))
        n1 = Vector((-e1.y, e1.x))
        m = n0 + n1
        if m.length < 1e-6:
            m = n0.copy()
        m.normalize()
        c = max(m.dot(n0), 1.0 / miter_limit)
        out.append(p1 + m * (d / c))
    if abs(d) < 1e-9:
        return out
    # collapse segments that flipped direction (offset larger than the local curve radius)
    for _ in range(12):
        changed = False
        for i in range(n):
            j = (i + 1) % n
            if (out[j] - out[i]).dot(pts[j] - pts[i]) < 0:
                mid = (out[i] + out[j]) * 0.5
                out[i] = mid
                out[j] = mid.copy()
                changed = True
        if not changed:
            break
    return out


def xform_loops(loops, mat3):
    res = []
    for L in loops:
        res.append([(mat3 @ Vector((p.x, p.y, 1.0))).xy for p in L])
    return res


def affine(scale=1.0, rot=0.0, shear=0.0, tx=0.0, ty=0.0, sx=None, sy=None):
    sx = scale if sx is None else sx
    sy = scale if sy is None else sy
    S = Matrix(((sx, 0, 0), (0, sy, 0), (0, 0, 1)))
    H = Matrix(((1, shear, 0), (0, 1, 0), (0, 0, 1)))
    c, s = math.cos(math.radians(rot)), math.sin(math.radians(rot))
    R = Matrix(((c, -s, 0), (s, c, 0), (0, 0, 1)))
    T = Matrix(((1, 0, tx), (0, 1, ty), (0, 0, 1)))
    return T @ R @ H @ S


def bbox(loops):
    xs = [p.x for L in loops for p in L]
    ys = [p.y for L in loops for p in L]
    return min(xs), min(ys), max(xs), max(ys)


def resample_corners(pts, chamfer):
    """Tiny chamfer on every sharp corner (so bevel highlights never pinch to a point)."""
    if chamfer <= 0:
        return pts
    out = []
    n = len(pts)
    for i in range(n):
        a, b, c = pts[i - 1], pts[i], pts[(i + 1) % n]
        e0, e1 = (b - a), (c - b)
        ang = e0.angle(e1) if e0.length > 1e-9 and e1.length > 1e-9 else 0
        if ang > math.radians(40):
            t0 = min(chamfer, e0.length * 0.3)
            t1 = min(chamfer, e1.length * 0.3)
            p0 = b - e0.normalized() * t0
            p1 = b + e1.normalized() * t1
            for k in range(4):
                t = k / 3.0
                out.append(p0 * (1 - t) ** 2 + b * 2 * t * (1 - t) + p1 * t * t)
        else:
            out.append(b)
    return out


# ====================================================================== glyph sources
def font_loops(font_file, ch, seg_len=0.012):
    fnt = bpy.data.fonts.load(os.path.join("C:/Windows/Fonts", font_file), check_existing=True)
    cu = bpy.data.curves.new("tmpfont", "FONT")
    cu.body = ch
    cu.font = fnt
    cu.size = 1.0
    cu.resolution_u = 12
    ob = bpy.data.objects.new("tmpfont", cu)
    bpy.context.scene.collection.objects.link(ob)
    dg = bpy.context.evaluated_depsgraph_get()
    ev = ob.evaluated_get(dg)
    crv = ev.to_curve(dg)
    loops = []
    for sp in crv.splines:
        bp = sp.bezier_points
        n = len(bp)
        pts = []
        for i in range(n):
            a, b = bp[i], bp[(i + 1) % n]
            p0, h0, h1, p1 = a.co.xy, a.handle_right.xy, b.handle_left.xy, b.co.xy
            chord = (p1 - p0)
            straight = True
            if chord.length > 1e-9:
                nrm = Vector((-chord.y, chord.x)).normalized()
                straight = abs((h0 - p0).dot(nrm)) < 1e-4 and abs((h1 - p0).dot(nrm)) < 1e-4
            if straight:
                pts.append(p0.copy())
            else:
                ln = (h0 - p0).length + (h1 - h0).length + (p1 - h1).length
                segs = max(3, int(ln / seg_len))
                pp = geometry.interpolate_bezier(a.co, a.handle_right, b.handle_left, b.co, segs + 1)
                pts += [v.xy.copy() for v in pp[:-1]]
        L = clean_loop(pts)
        if len(L) >= 3:
            loops.append(L)
    ev.to_curve_clear()
    bpy.data.objects.remove(ob)
    bpy.data.curves.remove(cu)
    return orient_loops(loops)


def V(*pts):
    return [Vector(p) for p in pts]


def path_loop(cmds, seg_len=0.01):
    """Tiny path language: ("M", p) start, ("L", p) line, ("C", c1, c2, p) cubic bezier. Closed automatically."""
    pts = []
    cur = None
    for c in cmds:
        if c[0] == "M":
            cur = Vector(c[1])
            pts.append(cur.copy())
        elif c[0] == "L":
            cur = Vector(c[1])
            pts.append(cur.copy())
        else:
            p1, p2, p3 = Vector(c[1]), Vector(c[2]), Vector(c[3])
            ln = (p1 - cur).length + (p2 - p1).length + (p3 - p2).length
            n = max(4, int(ln / seg_len))
            for k in range(1, n + 1):
                t = k / n
                pts.append(cur * (1 - t) ** 3 + p1 * 3 * t * (1 - t) ** 2 + p2 * 3 * t * t * (1 - t) + p3 * t ** 3)
            cur = p3
    return clean_loop(pts)


def ellipse_loop(cx, cy, rx, ry, tilt=0.0, n=48, pinch=0.0):
    out = []
    for k in range(n):
        a = 2 * math.pi * k / n
        x, y = rx * math.cos(a), ry * math.sin(a)
        # pinch: pull the lower-right of the counter into a soft point (a 9's teardrop counter)
        w = max(0.0, math.cos(a + math.radians(50)))
        x *= 1 + pinch * w ** 3
        y *= 1 + pinch * w ** 3
        c, s_ = math.cos(math.radians(tilt)), math.sin(math.radians(tilt))
        out.append(Vector((cx + x * c - y * s_, cy + x * s_ + y * c)))
    return out


def custom_smooth_loops(ch):
    """Hand-built heavy storybook digits with wedge flares and smooth bowls, cap height ~1.0 (style D)."""
    if ch == "4":
        outer = V((0.45, 0.00), (0.93, 0.00), (0.85, 0.20), (0.96, 0.17), (1.02, 0.49), (0.83, 0.45),
                  (0.83, 0.93), (0.72, 1.08), (0.50, 1.01), (-0.08, 0.42), (-0.04, 0.20), (0.53, 0.20))
        hole = V((0.33, 0.45), (0.53, 0.45), (0.53, 0.65))
        return orient_loops([outer, hole])
    if ch == "2":
        outer = path_loop([
            ("M", (-0.03, 0.00)), ("L", (0.86, 0.00)), ("L", (0.915, 0.345)), ("L", (0.895, 0.36)),
            ("C", (0.86, 0.325), (0.81, 0.31), (0.73, 0.31)), ("L", (0.49, 0.31)),
            ("C", (0.60, 0.39), (0.86, 0.53), (0.87, 0.73)),
            ("C", (0.88, 0.94), (0.68, 1.07), (0.44, 1.07)),
            ("C", (0.20, 1.07), (0.03, 0.96), (-0.01, 0.76)),
            ("L", (-0.05, 0.60)), ("L", (0.30, 0.68)),
            ("C", (0.32, 0.75), (0.37, 0.79), (0.44, 0.79)),
            ("C", (0.50, 0.79), (0.53, 0.75), (0.52, 0.69)),
            ("C", (0.51, 0.61), (0.42, 0.55), (0.26, 0.48)),
            ("L", (-0.03, 0.29))])
        return orient_loops([outer])
    if ch == "9":
        outer = path_loop([
            ("M", (0.05, 0.03)),
            ("C", (0.17, -0.03), (0.30, -0.05), (0.45, -0.05)),
            ("C", (0.72, -0.05), (0.92, 0.18), (0.92, 0.52)),
            ("C", (0.92, 0.86), (0.72, 1.07), (0.46, 1.07)),
            ("C", (0.19, 1.07), (0.00, 0.89), (0.00, 0.66)),
            ("C", (0.00, 0.43), (0.17, 0.29), (0.40, 0.29)),
            ("C", (0.50, 0.29), (0.56, 0.31), (0.60, 0.345)),
            ("L", (0.615, 0.335)),
            ("C", (0.60, 0.28), (0.51, 0.24), (0.39, 0.24)),
            ("C", (0.31, 0.24), (0.25, 0.26), (0.20, 0.30)),
            ("L", (0.12, 0.27))])
        hole = ellipse_loop(0.46, 0.675, 0.14, 0.145, tilt=-20, pinch=0.3)
        return orient_loops([outer, hole])
    raise ValueError(ch)


def custom_loops(ch):
    """Hand-built chiselled digits, cap height 1.0, faceted curves (style A)."""
    if ch == "4":
        outer = V((0.34, 0.00), (0.84, 0.00), (0.75, 0.11), (0.75, 0.21), (0.88, 0.17), (0.93, 0.46),
                  (0.75, 0.41), (0.75, 0.93), (0.67, 1.06), (0.49, 0.99), (-0.06, 0.37), (-0.03, 0.21),
                  (0.49, 0.21), (0.49, 0.11))
        hole = V((0.25, 0.41), (0.49, 0.41), (0.49, 0.68))
        return orient_loops([outer, hole])
    if ch == "2":
        outer = V((-0.02, 0.00), (0.80, 0.00), (0.86, 0.27), (0.71, 0.21), (0.36, 0.21), (0.70, 0.53),
                  (0.79, 0.67), (0.80, 0.82), (0.73, 0.94), (0.58, 1.02), (0.38, 1.04), (0.19, 0.99),
                  (0.05, 0.88), (-0.02, 0.70), (0.25, 0.69), (0.30, 0.78), (0.39, 0.82), (0.47, 0.80),
                  (0.52, 0.73), (0.50, 0.64), (0.41, 0.55), (-0.02, 0.19))
        return orient_loops([outer])
    if ch == "9":
        outer = V((0.02, 0.04), (0.20, -0.03), (0.42, -0.04), (0.62, 0.04), (0.77, 0.19), (0.84, 0.40),
                  (0.86, 0.64), (0.81, 0.83), (0.69, 0.97), (0.49, 1.04), (0.29, 1.02), (0.12, 0.92),
                  (0.02, 0.76), (0.01, 0.58), (0.08, 0.43), (0.22, 0.33), (0.41, 0.30), (0.56, 0.35),
                  (0.56, 0.27), (0.49, 0.19), (0.37, 0.16), (0.26, 0.18), (0.17, 0.24))
        hole = [Vector((0.435 + 0.165 * math.cos(a), 0.665 + 0.175 * math.sin(a)))
                for a in [math.radians(22.5 + 45 * k) for k in range(8)]]
        return orient_loops([outer, hole])
    raise ValueError(ch)


def dp_simplify(pts, tol):
    """Douglas-Peucker on a closed loop: keeps the real corners, turns curves into a few flat facets (chiselled look)."""
    n = len(pts)
    i1 = max(range(n), key=lambda k: (pts[k] - pts[0]).length)

    def rec(a, b):
        idx = [(a + k) % n for k in range(1, (b - a) % n)]
        if not idx:
            return []
        A, B = pts[a], pts[b]
        AB = B - A
        best, bi = -1.0, None
        for k in idx:
            Q = pts[k]
            d = (Q - A).length if AB.length < 1e-9 else abs(AB.x * (Q - A).y - AB.y * (Q - A).x) / AB.length
            if d > best:
                best, bi = d, k
        return rec(a, bi) + [bi] + rec(bi, b) if best > tol else []
    keep = [0] + rec(0, i1) + [i1] + rec(i1, 0)
    return [pts[k] for k in keep]


FACET_FONT = "BOOKOSB.TTF"      # heavy old-style serif figures; faceted + chiselled below so they read as a custom logo
FACET_TOL = 0.0085              # facet tolerance in font units (cap height ~0.72)


def facet_loops(ch):
    """Style E: heavy serif digits, every curve cut into flat chisel facets."""
    loops = font_loops(FACET_FONT, ch, 0.003)
    return orient_loops([clean_loop(dp_simplify(L, FACET_TOL), 1e-5) for L in loops])


# ====================================================================== bevel profiles
def build_profile(spec):
    """spec: list of (d, z, fillet, tag, rim). A corner with fillet>0 is rounded by a quadratic arc.
    Returns list of dicts with d, z, tag (for the band to the next point), rim, sharp."""
    pts = []
    n = len(spec)
    for i, (d, z, f, tag, rim) in enumerate(spec):
        if f > 0 and 0 < i < n - 1:
            a = Vector(spec[i - 1][:2])
            b = Vector((d, z))
            c = Vector(spec[i + 1][:2])
            ea, ec = (b - a), (c - b)
            ta = min(f, ea.length * 0.45)
            tc = min(f, ec.length * 0.45)
            p0 = b - ea.normalized() * ta
            p2 = b + ec.normalized() * tc
            segs = 5
            for k in range(segs + 1):
                t = k / segs
                q = p0 * (1 - t) ** 2 + b * 2 * t * (1 - t) + p2 * t * t
                pts.append(dict(d=q.x, z=q.y, tag=tag, rim=rim))
        else:
            pts.append(dict(d=d, z=z, tag=tag, rim=rim))
    # sharp rings: where the profile turns hard
    for i, p in enumerate(pts):
        p["sharp"] = False
        if 0 < i < len(pts) - 1:
            a = Vector((pts[i - 1]["d"], pts[i - 1]["z"]))
            b = Vector((p["d"], p["z"]))
            c = Vector((pts[i + 1]["d"], pts[i + 1]["z"]))
            if (b - a).length > 1e-7 and (c - b).length > 1e-7 and (b - a).angle(c - b) > math.radians(28):
                p["sharp"] = True
    return pts


MAT_INDEX = {"gold": 0, "green": 1, "glow": 2, "side": 3, "fire": 4}
E_DEPTH = 0.30                  # style E: extrusion depth of the gold side walls behind the front bevel


def profile_for(style):
    G, E, W = "gold", "green", "glow"
    if style == "E":      # chiselled: deep gold side wall, 2 sharp chamfers up to a knife ridge, inner bevel, step,
        S = "side"        # glowing groove, then dips under the inlaid enamel
        return build_profile([
            (0.000, -E_DEPTH, 0, S, 0), (0.000, 0.030, 0, G, 0), (0.0120, 0.052, 0, G, 0),
            (0.0270, 0.082, 0, G, 0), (0.0410, 0.062, 0, G, 0), (0.0440, 0.0615, 0, G, 0),
            (0.0455, 0.051, 0, W, 1.0), (0.0470, 0.048, 0, W, 1.0), (0.0490, 0.037, 0, W, 1.0)])
    if style == "A":      # chiselled: tall V-ridge gold frame, inner step, glowing groove, slightly domed enamel
        return build_profile([
            (0.000, 0.000, 0, G, 0), (0.000, 0.060, 0.004, G, 0), (0.024, 0.098, 0.003, G, 0),
            (0.046, 0.078, 0.002, G, 0), (0.054, 0.078, 0.002, G, 0), (0.057, 0.062, 0.0015, G, 0),
            (0.059, 0.056, 0, W, 1.0), (0.062, 0.056, 0, E, 1.0), (0.072, 0.064, 0.006, E, 0.55),
            (0.095, 0.068, 0, E, 0.0)])
    if style == "B":      # pillow: fat rounded gold frame, soft step, puffy enamel
        return build_profile([
            (0.000, 0.000, 0, G, 0), (0.000, 0.050, 0.030, G, 0), (0.026, 0.092, 0.012, G, 0),
            (0.040, 0.088, 0.010, G, 0), (0.050, 0.074, 0.004, G, 0), (0.056, 0.074, 0.003, G, 0),
            (0.059, 0.060, 0, W, 1.0), (0.062, 0.056, 0, E, 1.0), (0.074, 0.070, 0.010, E, 0.5),
            (0.100, 0.076, 0, E, 0.0)])
    if style == "D":      # heavy gold frame: rounded bead crown (always catches a light), inner bevel, one small
        return build_profile([                        # step, glowing groove, glossy enamel
            (0.000, -0.030, 0, G, 0), (0.000, 0.050, 0.017, G, 0), (0.028, 0.098, 0.019, G, 0),
            (0.052, 0.089, 0.008, G, 0), (0.0595, 0.080, 0.002, G, 0), (0.063, 0.0795, 0.0012, G, 0),
            (0.065, 0.064, 0, W, 1.0), (0.0665, 0.059, 0, W, 1.0), (0.0675, 0.050, 0, W, 1.0)])
    # C: stepped: two flat 45-degree bevels, crisp step, flat glossy enamel
    return build_profile([
        (0.000, 0.000, 0, G, 0), (0.000, 0.064, 0.003, G, 0), (0.022, 0.088, 0.003, G, 0),
        (0.036, 0.090, 0.004, G, 0), (0.044, 0.081, 0.002, G, 0), (0.053, 0.081, 0.002, G, 0),
        (0.058, 0.073, 0.0015, G, 0), (0.060, 0.058, 0, W, 1.0), (0.0635, 0.056, 0, E, 1.0),
        (0.070, 0.061, 0.003, E, 0.75), (0.082, 0.063, 0.004, E, 0.3), (0.110, 0.064, 0, E, 0.0)])


INLAY = {"D": dict(z=0.0605, d0=0.066, d1=0.098, dome=0.008), "E": dict(z=0.048, d0=0.047, d1=0.09, dome=0.006)}
PLATE = dict(out=0.052, zb=-0.065, zt=0.018, fillet=0.02, voxel=0.0045)


def plate_sdf_group():
    ng = bpy.data.node_groups.new("Logo_Plate_SDF", "GeometryNodeTree")
    ng.interface.new_socket("Geometry", in_out="INPUT", socket_type="NodeSocketGeometry")
    ng.interface.new_socket("Geometry", in_out="OUTPUT", socket_type="NodeSocketGeometry")
    gi = ng.nodes.new("NodeGroupInput")
    go = ng.nodes.new("NodeGroupOutput")
    m2s = ng.nodes.new("GeometryNodeMeshToSDFGrid")
    m2s.inputs["Voxel Size"].default_value = PLATE["voxel"]
    m2s.inputs["Band Width"].default_value = int(PLATE["out"] / PLATE["voxel"]) + 6
    off = ng.nodes.new("GeometryNodeSDFGridOffset")
    off.inputs["Distance"].default_value = PLATE["out"]
    fil = ng.nodes.new("GeometryNodeSDFGridFillet")
    fil.inputs["Iterations"].default_value = 2
    mean = ng.nodes.new("GeometryNodeSDFGridMean")
    g2m = ng.nodes.new("GeometryNodeGridToMesh")
    g2m.inputs["Threshold"].default_value = 0.0
    g2m.inputs["Adaptivity"].default_value = 0.0
    L = ng.links.new
    L(gi.outputs[0], m2s.inputs["Mesh"])
    L(m2s.outputs[0], off.inputs["Grid"])
    L(off.outputs[0], fil.inputs[0])
    for i_ in mean.inputs:
        if i_.name in ("Iterations",):
            i_.default_value = 3
        if i_.name in ("Width",):
            i_.default_value = 2
    L(fil.outputs[0], mean.inputs[0])
    L(mean.outputs[0], g2m.inputs["Grid"])
    sm = ng.nodes.new("GeometryNodeSetMaterial")
    sm.inputs["Material"].default_value = bpy.data.materials["Logo_Green_v2"]
    L(g2m.outputs[0], sm.inputs["Geometry"])
    L(sm.outputs[0], go.inputs[0])
    return ng


def plate_outline(glyphs):
    """Merged outward offset of all digit outlines (2D loops), via an SDF grid (robust, no self-intersections)."""
    core_prof = build_profile([(0.0, -0.1, 0, "green", 0), (0.0, 0.1, 0, "green", 0)])
    bm_all = bmesh.new()
    for loops, zoff in glyphs:
        me = loft("tmp_core", loops, core_prof, cap_bottom=True, true_insets=False)
        bm_all.from_mesh(me)
        bpy.data.meshes.remove(me)
    me = bpy.data.meshes.new("tmp_plate_core")
    bm_all.to_mesh(me)
    bm_all.free()
    ob = bpy.data.objects.new("tmp_plate_core", me)
    bpy.context.scene.collection.objects.link(ob)
    md = ob.modifiers.new("SDF_Grow", "NODES")
    md.node_group = plate_sdf_group()
    dg = bpy.context.evaluated_depsgraph_get()
    ev = ob.evaluated_get(dg)
    bm = bmesh.new()
    bm.from_mesh(ev.to_mesh())
    ev.to_mesh_clear()
    ng = md.node_group
    bpy.data.objects.remove(ob)
    bpy.data.meshes.remove(me)
    bpy.data.node_groups.remove(ng)
    res = bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0, 0),
                                 plane_no=(0, 1, 0), dist=1e-7)
    cut = [e for e in res["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
    adj = {}
    for e in cut:
        a, b = e.verts
        adj.setdefault(a, []).append(b)
        adj.setdefault(b, []).append(a)
    seen = set()
    loops = []
    for v in list(adj):
        if v in seen:
            continue
        loop = [v]
        seen.add(v)
        prev, cur = None, v
        while True:
            nxt = next((n for n in adj[cur] if n is not prev and n not in seen), None)
            if nxt is None:
                break
            loop.append(nxt)
            seen.add(nxt)
            prev, cur = cur, nxt
        if len(loop) > 12:
            loops.append([Vector((q.co.x, q.co.z)) for q in loop])
    bm.free()
    out = []
    for L in loops:
        L = resample(clean_loop(L, 1e-5), 0.007)
        n = len(L)
        for _ in range(40):            # Taubin smoothing: removes voxel steps without shrinking
            for lam in (0.5, -0.53):
                L = [L[i] + ((L[i - 1] + L[(i + 1) % n]) * 0.5 - L[i]) * lam for i in range(n)]
        L = resample(L, 0.005)
        a = signed_area(L)
        if abs(a) > 0.002:
            out.append(L)
    out = orient_loops(out)
    return [L for L in out if signed_area(L) > 0 or abs(signed_area(L)) > 0.012]   # drop tiny plate holes


def resample(L, step):
    n = len(L)
    per = sum((L[(i + 1) % n] - L[i]).length for i in range(n))
    m = max(8, int(per / step))
    out = []
    seg = 0
    acc = 0.0
    target = 0.0
    i = 0
    pts = L + [L[0]]
    dist = [0.0]
    for k in range(1, len(pts)):
        dist.append(dist[-1] + (pts[k] - pts[k - 1]).length)
    k = 1
    for j in range(m):
        t = per * j / m
        while dist[k] < t:
            k += 1
        a, b = pts[k - 1], pts[k]
        span = dist[k] - dist[k - 1]
        f = (t - dist[k - 1]) / span if span > 1e-12 else 0
        out.append(a.lerp(b, f))
    return out


# ====================================================================== mesh builder
def untangle(R, passes=4):
    """Make a closed loop simple: tiny swallowtails are collapsed, crossings of far-apart parts become touches."""
    from mathutils.geometry import intersect_line_line_2d
    n = len(R)
    for _ in range(passes):
        hit = False
        for i in range(n):
            a0, a1 = R[i], R[(i + 1) % n]
            if (a1 - a0).length < 1e-9:
                continue
            for j in range(i + 2, n):
                if i == 0 and j == n - 1:
                    continue
                b0, b1 = R[j], R[(j + 1) % n]
                if (b1 - b0).length < 1e-9:
                    continue
                p = intersect_line_line_2d(a0, a1, b0, b1)
                if p is None:
                    continue
                p = Vector(p)
                if min((p - q).length for q in (a0, a1, b0, b1)) < 1e-7:
                    continue              # touching, not crossing
                inner = list(range(i + 1, j + 1))
                outer_ = list(range(j + 1, n)) + list(range(0, i + 1))
                whole = signed_area(R)
                a_in = signed_area([p] + [R[k] for k in inner])
                a_out = signed_area([p] + [R[k] for k in outer_])
                if a_in * whole < 0 and len(inner) <= len(outer_):      # inverted swallowtail: cut it off
                    for k in inner:
                        R[k] = p.copy()
                elif a_out * whole < 0:
                    for k in outer_:
                        R[k] = p.copy()
                elif len(inner) <= 24:
                    for k in inner:
                        R[k] = p.copy()
                else:
                    R[(i + 1) % n] = p.copy()
                    R[j] = p.copy()
                hit = True
        if not hit:
            break
    return R


def sdf_offsets(loops, ds, miter=4.0, K=40):
    """Distance-true insets: each outline vertex walks along its miter direction until it is exactly d away from
    the WHOLE glyph outline (all loops), or stops on the medial axis if the stroke is too thin. Rings of all
    loops therefore never cross each other (unlike a plain miter offset). Returns rings[loop][k] = [Vector]."""
    import numpy as np
    segA = np.array([[p.x, p.y] for L in loops for p in L])
    segB = np.array([[L[(i + 1) % len(L)].x, L[(i + 1) % len(L)].y] for L in loops for i in range(len(L))])
    AB = segB - segA
    ab2 = np.maximum((AB ** 2).sum(1), 1e-12)

    def dist(P):
        out = np.empty(len(P))
        for c in range(0, len(P), 2048):
            Q = P[c:c + 2048]
            AP = Q[:, None, :] - segA[None, :, :]
            t = np.clip((AP * AB[None]).sum(2) / ab2[None], 0.0, 1.0)
            C = segA[None] + t[..., None] * AB[None]
            out[c:c + 2048] = np.sqrt(((Q[:, None, :] - C) ** 2).sum(2)).min(1)
        return out
    rings = []
    for L in loops:
        n = len(L)
        P = np.array([[p.x, p.y] for p in L])
        M = np.zeros((n, 2))
        S = np.zeros(n)
        for i in range(n):
            p0, p1, p2 = L[i - 1], L[i], L[(i + 1) % n]
            e0 = (p1 - p0).normalized()
            e1 = (p2 - p1).normalized()
            n0 = Vector((-e0.y, e0.x))
            n1 = Vector((-e1.y, e1.x))
            m = n0 + n1
            if m.length < 1e-6:
                m = n0.copy()
            m.normalize()
            M[i] = (m.x, m.y)
            S[i] = 1.0 / max(m.dot(n0), 1.0 / miter)
        per = []
        prev_t = np.zeros(n)
        prev_d = 0.0
        for d_signed in ds:
            if abs(d_signed) <= 1e-9:
                per.append([Vector(p) for p in P])
                continue
            sgn = 1.0 if d_signed > 0 else -1.0
            d = abs(d_signed)
            T = (d * S)[:, None] * (np.arange(1, K + 1) / K)[None, :]          # n x K travel distances
            Q = P[:, None, :] + sgn * M[:, None, :] * T[..., None]
            D = dist(Q.reshape(-1, 2)).reshape(n, K)
            ok = D >= d * 0.995
            first = np.where(ok.any(1), ok.argmax(1), -1)
            res = np.empty((n, 2))
            for i in range(n):
                k = first[i]
                if k == -1:
                    k = int(D[i].argmax())
                    t = T[i, k] * 0.985
                elif k == 0:
                    t = T[i, 0] * min(1.0, d / max(D[i, 0], 1e-9))
                else:
                    d0, d1 = D[i, k - 1], D[i, k]
                    f = (d - d0) / (d1 - d0) if d1 > d0 else 1.0
                    t = T[i, k - 1] + (T[i, k] - T[i, k - 1]) * min(max(f, 0.0), 1.0)
                res[i] = P[i] + sgn * M[i] * t
            prev_d = d
            per.append(untangle([Vector(p) for p in res]))
        rings.append(per)
    return rings


def seg_dist_fn(loops):
    import numpy as np
    segA = np.array([[p.x, p.y] for L in loops for p in L])
    segB = np.array([[L[(i + 1) % len(L)].x, L[(i + 1) % len(L)].y] for L in loops for i in range(len(L))])
    AB = segB - segA
    ab2 = np.maximum((AB ** 2).sum(1), 1e-12)

    def dist(P):
        P = np.asarray(P, dtype=float)
        out = np.empty(len(P))
        for c in range(0, len(P), 1024):
            Q = P[c:c + 1024]
            AP = Q[:, None, :] - segA[None, :, :]
            t = np.clip((AP * AB[None]).sum(2) / ab2[None], 0.0, 1.0)
            C = segA[None] + t[..., None] * AB[None]
            out[c:c + 1024] = np.sqrt(((Q[:, None, :] - C) ** 2).sum(2)).min(1)
        return out
    return dist


def inlay_face(bm, rim_l, glyph_loops, to3, z, d0, d1, dome, mi, step=0.0075):
    """The enamel as one inlaid sheet cut from the ORIGINAL outline (always a clean simple shape): it runs under
    the gold frame and the frame's inner wall dips through it, so the visible enamel edge is exactly the frame
    edge. Interior grid points give it a soft pillow (dome) and a per-vertex 'rim' glow gradient."""
    import numpy as np
    dist = seg_dist_fn(glyph_loops)                     # distances are measured from the real outline
    glyph_loops = [clean_loop(r[0], 1e-5) for r in sdf_offsets(glyph_loops, [0.03], 12.0)]   # sheet edge hides under the gold
    x0, y0, x1, y1 = bbox(glyph_loops)
    pts = [p.copy() for L in glyph_loops for p in L]
    edges = []
    base = 0
    for L in glyph_loops:
        n = len(L)
        edges += [(base + i, base + (i + 1) % n) for i in range(n)]
        base += n
    inside = lambda p: sum(1 for L in glyph_loops if point_in_poly(p, L)) % 2 == 1
    grid = []
    y = y0 + step * 0.5
    row = 0
    while y < y1:
        x = x0 + step * (0.5 + 0.5 * (row % 2))
        while x < x1:
            q = Vector((x, y))
            if inside(q):
                grid.append(q)
            x += step
        y += step * 0.866
        row += 1
    if grid:
        dg = seg_dist_fn(glyph_loops)([[q.x, q.y] for q in grid])
        grid = [q for q, d in zip(grid, dg) if d > step * 0.45]
    allp = pts + grid
    vco, eo, fo, _, _, _ = geometry.delaunay_2d_cdt(allp, edges, [], 0, 1e-7)
    dv = dist([[v.x, v.y] for v in vco])
    smooth = lambda t: t * t * (3 - 2 * t)
    verts = []
    for v, d in zip(vco, dv):
        t = min(max((d - d0) / max(d1 - d0, 1e-6), 0.0), 1.0)
        bv = bm.verts.new(to3(Vector((v.x, v.y)), z + dome * smooth(t)))
        bv[rim_l] = 1.0 - smooth(t)
        verts.append(bv)
    for f in fo:
        c = sum((Vector(vco[i]) for i in f), Vector((0, 0))) / len(f)
        if not inside(c):
            continue
        a, b, cc = (Vector(vco[i]) for i in f[:3])
        cr = (b - a).x * (cc - a).y - (b - a).y * (cc - a).x
        vs = [verts[i] for i in f] if cr > 0 else [verts[i] for i in reversed(f)]
        try:
            nf = bm.faces.new(vs)
            nf.material_index = mi
            nf.smooth = True
        except ValueError:
            pass


def loft(name, glyph_loops, prof, zoff=0.0, cap_bottom=False, corner_deg=38.0, miter=4.0, true_insets=True, inlay=None):
    """Loft every loop of a glyph along the profile. Front faces -Y: 2D (x, y) + height z -> (x, -z, y)."""
    bm = bmesh.new()
    rim_l = bm.verts.layers.float.new("rim")
    to3 = lambda p, z: Vector((p.x, -(z + zoff), p.y))
    rings_all = []
    true_rings = sdf_offsets(glyph_loops, [max(pp["d"], 0.0) for pp in prof], 12.0) if true_insets else None
    for li, L in enumerate(glyph_loops):
        rings = []
        for k_, pp in enumerate(prof):
            pts = true_rings[li][k_] if (true_insets and pp["d"] >= 0) else offset_loop(L, pp["d"], miter)
            ring = [bm.verts.new(to3(p, pp["z"])) for p in pts]
            for v in ring:
                v[rim_l] = pp["rim"]
            rings.append(ring)
        rings_all.append(rings)
        n = len(L)
        for k in range(len(prof) - 1):
            r0, r1 = rings[k], rings[k + 1]
            mi = MAT_INDEX[prof[k]["tag"]]
            for i in range(n):
                j = (i + 1) % n
                try:
                    f = bm.faces.new((r0[i], r0[j], r1[j], r1[i]))
                    f.material_index = mi
                    f.smooth = True
                except ValueError:
                    pass
    bm.verts.ensure_lookup_table()

    def cap(level, flip, mi):
        polys = [[Vector((v.co.x, v.co.z, 0.0)) for v in rings[level]] for rings in rings_all]
        flat = [v for rings in rings_all for v in rings[level]]
        tris = geometry.tessellate_polygon(polys)
        for t in tris:
            a, b, c = (flat[t[0]], flat[t[1]], flat[t[2]])
            pa, pb, pc = (Vector((x.co.x, x.co.z)) for x in (a, b, c))
            cr = (pb - pa).x * (pc - pa).y - (pb - pa).y * (pc - pa).x
            vs = (a, b, c) if (cr > 0) != flip else (a, c, b)
            try:
                f = bm.faces.new(vs)
                f.material_index = mi
                f.smooth = True
            except ValueError:
                pass
    if inlay:
        inlay_face(bm, rim_l, glyph_loops, to3, inlay["z"], inlay["d0"], inlay["d1"], inlay["dome"], MAT_INDEX["green"])
    else:
        cap(len(prof) - 1, False, MAT_INDEX[prof[-1]["tag"]])
    if cap_bottom:
        cap(0, True, MAT_INDEX[prof[0]["tag"]])

    # sharp edges: hard profile turns (rings) and hard outline corners (longitudinal)
    for li, L in enumerate(glyph_loops):
        rings = rings_all[li]
        n = len(L)
        for k, pp in enumerate(prof):
            if pp["sharp"]:
                for i in range(n):
                    e = bm.edges.get((rings[k][i], rings[k][(i + 1) % n]))
                    if e:
                        e.smooth = False
        for i in range(n):
            a, b, c = L[i - 1], L[i], L[(i + 1) % n]
            if (b - a).length > 1e-9 and (c - b).length > 1e-9 and (b - a).angle(c - b) > math.radians(corner_deg):
                for k in range(len(prof) - 1):
                    e = bm.edges.get((rings[k][i], rings[k + 1][i]))
                    if e:
                        e.smooth = False
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.validate()
    return me


# ====================================================================== materials
def node(nt, t, loc=(0, 0), **inputs):
    n = nt.nodes.new(t)
    n.location = loc
    for k, v in inputs.items():
        n.inputs[k].default_value = v
    return n


def get_principled(nt):
    return next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")


def mat_gold():
    m = bpy.data.materials.new("Logo_Gold_v2")
    nt = m.node_tree
    L = nt.links.new
    p = get_principled(nt)
    tc = node(nt, "ShaderNodeTexCoord", (-1400, 0))
    # worn: large soft patches change roughness/tone, fine stretched scratches as bump
    n1 = node(nt, "ShaderNodeTexNoise", (-1100, 300), Scale=9.0, Detail=6.0, Roughness=0.6)
    L(tc.outputs["Object"], n1.inputs["Vector"])
    r1 = node(nt, "ShaderNodeMapRange", (-850, 300), **{"From Min": 0.35, "From Max": 0.7, "To Min": 0.22, "To Max": 0.36})
    L(n1.outputs["Fac"], r1.inputs["Value"])
    mp = node(nt, "ShaderNodeMapping", (-1200, -300))
    mp.inputs["Scale"].default_value = (90.0, 6.0, 90.0)
    mp.inputs["Rotation"].default_value = (0.3, 0.0, 0.5)
    L(tc.outputs["Object"], mp.inputs["Vector"])
    n2 = node(nt, "ShaderNodeTexNoise", (-1000, -300), Scale=3.0, Detail=2.0)
    L(mp.outputs["Vector"], n2.inputs["Vector"])
    n3 = node(nt, "ShaderNodeTexNoise", (-1000, -550), Scale=160.0, Detail=3.0)
    L(tc.outputs["Object"], n3.inputs["Vector"])
    mixb = node(nt, "ShaderNodeMath", (-780, -400))
    mixb.operation = "ADD"
    L(n2.outputs["Fac"], mixb.inputs[0])
    L(n3.outputs["Fac"], mixb.inputs[1])
    bev = node(nt, "ShaderNodeBevel", (-780, -650), Radius=0.0025)
    bev.samples = 6
    bump = node(nt, "ShaderNodeBump", (-560, -450), Strength=0.08, Distance=0.0006)
    L(mixb.outputs[0], bump.inputs["Height"])
    L(bev.outputs["Normal"], bump.inputs["Normal"])
    # cavity: dirtier, warmer, rougher gold in the recesses
    ao = node(nt, "ShaderNodeAmbientOcclusion", (-1100, 650), Distance=0.03)
    ao.samples = 8
    aor = node(nt, "ShaderNodeMapRange", (-850, 650), **{"From Min": 0.35, "From Max": 0.95})
    L(ao.outputs["AO"], aor.inputs["Value"])
    ramp = node(nt, "ShaderNodeValToRGB", (-600, 650))
    ramp.color_ramp.elements[0].color = (0.45, 0.26, 0.07, 1)
    ramp.color_ramp.elements[1].color = (1.0, 0.86, 0.55, 1)
    L(aor.outputs["Result"], ramp.inputs["Fac"])
    tone = node(nt, "ShaderNodeMix", (-350, 500))
    tone.data_type = "RGBA"
    tone.blend_type = "MULTIPLY"
    tone.inputs["Factor"].default_value = 0.0
    L(ramp.outputs["Color"], tone.inputs[6])
    nc = node(nt, "ShaderNodeValToRGB", (-600, 380))
    nc.color_ramp.elements[0].color = (1.0, 0.80, 0.30, 1)
    nc.color_ramp.elements[1].color = (1.0, 0.70, 0.20, 1)
    L(n1.outputs["Fac"], nc.inputs["Fac"])
    L(nc.outputs["Color"], tone.inputs[7])
    L(tone.outputs[2], p.inputs["Base Color"])
    p.inputs["Metallic"].default_value = 1.0
    rough = node(nt, "ShaderNodeMath", (-350, 250))
    rough.operation = "ADD"
    inv = node(nt, "ShaderNodeMath", (-600, 150))
    inv.operation = "MULTIPLY"
    inv.inputs[1].default_value = 0.0
    L(r1.outputs["Result"], rough.inputs[0])
    aoi = node(nt, "ShaderNodeMath", (-600, 100))
    aoi.operation = "SUBTRACT"
    aoi.inputs[0].default_value = 1.0
    L(aor.outputs["Result"], aoi.inputs[1])
    aom = node(nt, "ShaderNodeMath", (-450, 100))
    aom.operation = "MULTIPLY"
    aom.inputs[1].default_value = 0.2
    L(aoi.outputs[0], aom.inputs[0])
    L(aom.outputs[0], rough.inputs[1])
    L(rough.outputs[0], p.inputs["Roughness"])
    L(bump.outputs["Normal"], p.inputs["Normal"])
    p.inputs["Specular Tint"].default_value = (1.0, 0.88, 0.5, 1)
    return m


def mat_green():
    """Candy enamel: metallic green flake base under a clear tinted coat, darker toward the bottom,
    faint glowing magic veins, and a green glow rising near the groove (vertex attribute 'rim')."""
    m = bpy.data.materials.new("Logo_Green_v2")
    nt = m.node_tree
    L = nt.links.new
    p = get_principled(nt)
    tc = node(nt, "ShaderNodeTexCoord", (-1600, 0))
    sep = node(nt, "ShaderNodeSeparateXYZ", (-1400, 300))
    L(tc.outputs["Object"], sep.inputs["Vector"])
    grad = node(nt, "ShaderNodeMapRange", (-1200, 300), **{"From Min": -0.45, "From Max": 0.45})
    L(sep.outputs["Z"], grad.inputs["Value"])
    plate = node(nt, "ShaderNodeAttribute", (-1400, 600))
    plate.attribute_type = "OBJECT"
    plate.attribute_name = "logo_plate"
    ramp = node(nt, "ShaderNodeValToRGB", (-1000, 300))
    ramp.color_ramp.elements[0].color = (0.002, 0.035, 0.008, 1)
    ramp.color_ramp.elements[1].color = (0.015, 0.22, 0.04, 1)
    L(grad.outputs["Result"], ramp.inputs["Fac"])
    pl = node(nt, "ShaderNodeMix", (-700, 400))
    pl.data_type = "RGBA"
    L(plate.outputs["Fac"], pl.inputs["Factor"])
    L(ramp.outputs["Color"], pl.inputs[6])
    pl.inputs[7].default_value = (0.004, 0.018, 0.008, 1)
    L(pl.outputs[2], p.inputs["Base Color"])
    p.inputs["Metallic"].default_value = 0.5
    p.inputs["Roughness"].default_value = 0.32
    cw = node(nt, "ShaderNodeMapRange", (-700, 850), **{"To Min": 1.0, "To Max": 0.45})
    L(plate.outputs["Fac"], cw.inputs["Value"])
    L(cw.outputs["Result"], p.inputs["Coat Weight"])
    cr = node(nt, "ShaderNodeMapRange", (-700, 700), **{"To Min": 0.03, "To Max": 0.22})
    L(plate.outputs["Fac"], cr.inputs["Value"])
    L(cr.outputs["Result"], p.inputs["Coat Roughness"])
    p.inputs["Coat Tint"].default_value = (0.55, 0.9, 0.6, 1)
    # flakes (base normal only; the coat stays mirror smooth)
    vor = node(nt, "ShaderNodeTexVoronoi", (-1000, -200), Scale=900.0)
    L(tc.outputs["Object"], vor.inputs["Vector"])
    fl = node(nt, "ShaderNodeBump", (-700, -200), Strength=0.25, Distance=0.0003)
    L(vor.outputs["Color"], fl.inputs["Height"]) if False else L(vor.outputs["Distance"], fl.inputs["Height"])
    L(fl.outputs["Normal"], p.inputs["Normal"])
    bev = node(nt, "ShaderNodeBevel", (-700, -400), Radius=0.002)
    L(bev.outputs["Normal"], p.inputs["Coat Normal"])
    # magic veins: thin voronoi cell borders, distorted
    nz = node(nt, "ShaderNodeTexNoise", (-1300, -700), Scale=4.0, Detail=3.0)
    L(tc.outputs["Object"], nz.inputs["Vector"])
    mv = node(nt, "ShaderNodeVectorMath", (-1100, -700))
    mv.operation = "MULTIPLY_ADD"
    mv.inputs[1].default_value = (0.06, 0.06, 0.06)
    L(nz.outputs["Color"], mv.inputs[0])
    L(tc.outputs["Object"], mv.inputs[2])
    vv = node(nt, "ShaderNodeTexVoronoi", (-900, -700), Scale=11.0)
    vv.feature = "DISTANCE_TO_EDGE"
    L(mv.outputs["Vector"], vv.inputs["Vector"])
    vr = node(nt, "ShaderNodeMapRange", (-700, -700), **{"From Min": 0.0, "From Max": 0.035, "To Min": 1.0, "To Max": 0.0})
    L(vv.outputs["Distance"], vr.inputs["Value"])
    rim = node(nt, "ShaderNodeAttribute", (-900, -1000))
    rim.attribute_name = "rim"
    rp = node(nt, "ShaderNodeMath", (-700, -1000))
    rp.operation = "POWER"
    rp.inputs[1].default_value = 2.4
    L(rim.outputs["Fac"], rp.inputs[0])
    vw = node(nt, "ShaderNodeMath", (-500, -700))
    vw.operation = "MULTIPLY"
    vw.inputs[1].default_value = 0.025
    L(vr.outputs["Result"], vw.inputs[0])
    add = node(nt, "ShaderNodeMath", (-350, -800))
    add.operation = "ADD"
    L(vw.outputs[0], add.inputs[0])
    L(rp.outputs[0], add.inputs[1])
    noplate = node(nt, "ShaderNodeMath", (-200, -800))
    noplate.operation = "SUBTRACT"
    noplate.inputs[0].default_value = 1.0
    L(plate.outputs["Fac"], noplate.inputs[1])
    em = node(nt, "ShaderNodeMath", (-50, -800))
    em.operation = "MULTIPLY"
    boost = node(nt, "ShaderNodeMath", (-120, -650))
    boost.operation = "MULTIPLY"
    boost.inputs[1].default_value = 0.8
    L(add.outputs[0], boost.inputs[0])
    L(boost.outputs[0], em.inputs[0])
    L(noplate.outputs[0], em.inputs[1])
    L(em.outputs[0], p.inputs["Emission Strength"])
    p.inputs["Emission Color"].default_value = (0.18, 1.0, 0.22, 1)
    m["glow_strength_node"] = em.name
    return m


def mat_glow():
    m = bpy.data.materials.new("Logo_Glow_v2")
    nt = m.node_tree
    p = get_principled(nt)
    p.inputs["Base Color"].default_value = (0.1, 0.6, 0.12, 1)
    p.inputs["Roughness"].default_value = 0.3
    p.inputs["Emission Color"].default_value = (0.25, 1.0, 0.25, 1)
    # the magic flickers: brightness drifts along the groove
    tc = node(nt, "ShaderNodeTexCoord", (-900, -300))
    nz = node(nt, "ShaderNodeTexNoise", (-700, -300), Scale=7.0, Detail=2.0)
    nt.links.new(tc.outputs["Object"], nz.inputs["Vector"])
    mr = node(nt, "ShaderNodeMapRange", (-500, -300), **{"From Min": 0.3, "From Max": 0.7, "To Min": 1.4, "To Max": 4.2})
    nt.links.new(nz.outputs["Fac"], mr.inputs["Value"])
    nt.links.new(mr.outputs["Result"], p.inputs["Emission Strength"])
    return m


# ====================================================================== styles
STYLES = {
    # source, per-digit (scale, rot, dx, dy, zoff), shear, gap
    "A": dict(src="custom", digits=[(1.04, 3.0, 0.0, 0.02, 0.000), (0.94, -2.0, 0.0, -0.03, 0.010),
                                    (1.02, -4.0, 0.0, 0.03, 0.004)], shear=0.10, gap=0.035),
    "B": dict(src="COOPBL.TTF", digits=[(1.04, 6.0, 0.0, 0.04, 0.000), (0.92, -3.0, 0.0, -0.05, 0.010),
                                        (1.04, -5.0, 0.0, 0.05, 0.004)], shear=0.0, gap=0.03),
    "D": dict(src="custom_smooth", digits=[(1.04, 3.0, 0.0, 0.025, 0.000), (0.96, -3.0, 0.0, -0.03, 0.010),
                                           (1.02, 2.5, 0.0, 0.03, 0.005)], shear=0.08, gap=0.03, bold=0.013, sx=0.86),
    "C": dict(src="SHOWG.TTF", digits=[(1.04, 4.0, 0.0, 0.03, 0.0), (0.95, -2.5, 0.0, -0.035, 0.008),
                                       (1.03, 3.0, 0.0, 0.02, 0.004)], shear=0.10, gap=-0.005, sx=1.12, bold=0.014),
    # E: faceted heavy serif (Bookman bold outlines cut into flat chisel facets); digits overlap and step forward
    "E": dict(src="facet", digits=[(1.06, 4.0, 0.0, 0.03, 0.000), (0.93, -3.0, 0.0, -0.035, 0.060),
                                   (1.05, 3.5, 0.0, 0.035, 0.025)], shear=0.07, gap=-0.035, sx=1.0, plate=False, bold=0.018),
}


def glyphs_for(style):
    st = STYLES[style]
    raw = []
    for ch in "429":
        if st["src"] == "custom":
            loops = custom_loops(ch)
        elif st["src"] == "custom_smooth":
            loops = custom_smooth_loops(ch)
        elif st["src"] == "facet":
            loops = facet_loops(ch)
        else:
            loops = font_loops(st["src"], ch)
        raw.append(loops)
    # normalise: cap height of the 4 -> 1.0, baseline 0
    x0, y0, x1, y1 = bbox(raw[0])
    k = 1.0 / (y1 - y0)
    placed = []
    cursor = 0.0
    for (s, rot, dx, dy, zoff), loops in zip(st["digits"], raw):
        bx0, by0, bx1, by1 = bbox(loops)
        cx, cy = (bx0 + bx1) * 0.5, (by0 + by1) * 0.5
        m = affine(scale=k * s, sx=k * s * st.get("sx", 1.0), sy=k * s, tx=-cx * k * s, ty=-cy * k * s)
        m = affine(rot=rot, shear=st["shear"]) @ m
        L = xform_loops(loops, m)
        a0, b0, a1, b1 = bbox(L)
        L = xform_loops(L, affine(tx=cursor - a0 + dx, ty=dy))
        cursor += (a1 - a0) + st["gap"]
        placed.append((L, zoff))
    # fit to LOGO_WIDTH (including the back plate rim) and centre
    allL = [p for L, _ in placed for p in L]
    x0, y0, x1, y1 = bbox(allL)
    bold = st.get("bold", 0.0)
    rim_out = PLATE["out"] if st.get("plate", True) else 0.0
    k = (LOGO_WIDTH - 2 * (rim_out + bold)) / (x1 - x0)
    m = affine(scale=k, tx=-(x0 + x1) * 0.5 * k, ty=-(y0 + y1) * 0.5 * k)
    out = []
    for L, zoff in placed:
        L2 = xform_loops(L, m)
        if style == "A":
            L2 = [resample_corners(l, 0.004) for l in L2]
        if bold:
            L2 = [clean_loop(r[0], 2e-4) for r in sdf_offsets(L2, [-bold], 12.0)]
        out.append((L2, zoff))
    return out


# ====================================================================== the pumpkin-stem detail on the 9
def sweep(name, path, radius_fn, sides=10, lobes=0, lobe_amt=0.0, twist=0.0, cap_end=True):
    """Sweep a (lobed) circle along a polyline path (list of Vectors). radius_fn(t) in 0..1."""
    bm = bmesh.new()
    rings = []
    n = len(path)
    prev_nrm = None
    for i, p in enumerate(path):
        t = i / (n - 1)
        tan = (path[min(i + 1, n - 1)] - path[max(i - 1, 0)]).normalized()
        if prev_nrm is None:
            ref = Vector((0, 1, 0)) if abs(tan.y) < 0.9 else Vector((1, 0, 0))
            nrm = tan.cross(ref).normalized()
        else:
            nrm = (prev_nrm - tan * prev_nrm.dot(tan)).normalized()
        prev_nrm = nrm
        bin_ = tan.cross(nrm)
        r = radius_fn(t)
        ring = []
        for k in range(sides):
            a = 2 * math.pi * k / sides + twist * t
            rr = r * (1.0 + lobe_amt * math.cos(lobes * (2 * math.pi * k / sides))) if lobes else r
            ring.append(bm.verts.new(p + (nrm * math.cos(a) + bin_ * math.sin(a)) * rr))
        rings.append(ring)
    for i in range(n - 1):
        for k in range(sides):
            f = bm.faces.new((rings[i][k], rings[i][(k + 1) % sides], rings[i + 1][(k + 1) % sides], rings[i + 1][k]))
            f.smooth = True
    if cap_end:
        c = bm.verts.new(path[-1] + (path[-1] - path[-2]).normalized() * radius_fn(1.0) * 0.15)
        for k in range(sides):
            f = bm.faces.new((rings[-1][k], rings[-1][(k + 1) % sides], c))
            f.smooth = True
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    return me


def bez(p0, p1, p2, p3, n):
    return [p0 * (1 - t) ** 3 + p1 * 3 * t * (1 - t) ** 2 + p2 * 3 * t * t * (1 - t) + p3 * t ** 3
            for t in [i / (n - 1) for i in range(n)]]


# ====================================================================== build
def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def new_coll(name, parent=None):
    c = bpy.data.collections.new(name)
    (parent or bpy.context.scene.collection).children.link(c)
    return c


def build_logo(style):
    sc = bpy.context.scene
    col = new_coll("Logo_429_v2")
    root = bpy.data.objects.new("Logo_429_Root", None)
    root.empty_display_type = "ARROWS"
    root.empty_display_size = 0.3
    col.objects.link(root)
    root["facing"] = "-Y (front faces -Y; the viewer stands at -Y looking toward +Y), Z up"
    root["width"] = LOGO_WIDTH
    root["style"] = style
    if style == "E":
        return build_logo_e(col, root)
    gold, green, glow = mat_gold(), mat_green(), mat_glow()
    prof = profile_for(style)
    glyphs = glyphs_for(style)
    for (loops, zoff), ch in zip(glyphs, "429"):
        me = loft("Logo_429_Digit_" + ch, loops, prof, zoff=zoff, inlay=INLAY.get(style))
        for m in (gold, green, glow):
            me.materials.append(m)
        ob = bpy.data.objects.new("Logo_429_Digit_" + ch, me)
        col.objects.link(ob)
        ob.parent = root
    # back plate: the digit silhouettes become one signed-distance field, grown outward and filleted; its
    # mid-slice gives clean merged outline loops, which are lofted with a crisp rounded edge and a flat face
    loops = plate_outline(glyphs)
    pprof = build_profile([(0.0, PLATE["zb"], 0, "green", 0), (0.0, PLATE["zt"], PLATE["fillet"], "green", 0),
                           (0.024, PLATE["zt"], 0, "green", 0)])
    me = loft("Logo_429_Backplate", loops, pprof, cap_bottom=True, corner_deg=60, true_insets=False)
    me.materials.append(green)
    pl = bpy.data.objects.new("Logo_429_Backplate", me)
    col.objects.link(pl)
    pl["logo_plate"] = 1.0
    pl.parent = root
    if SHOW_STEM:
        add_stem(col, root, glyphs, gold, green, glow, prof)
    add_reflectors(col, root)
    return col, root


# ====================================================================== style E (v3): chiselled gold, deep walls, green fire
def mat_gold_e(name="Logo_Gold_v2", side=False):
    """Saturated polished gold (not champagne): warm base, darker worn cavities, slim bevel highlights on every facet edge.
    side=True: the extrusion walls, darker and brushed along the depth so the 3D depth reads."""
    m = bpy.data.materials.new(name)
    nt = m.node_tree
    L = nt.links.new
    p = get_principled(nt)
    tc = node(nt, "ShaderNodeTexCoord", (-1400, 0))
    n1 = node(nt, "ShaderNodeTexNoise", (-1100, 300), Scale=16.0, Detail=6.0, Roughness=0.62)
    L(tc.outputs["Object"], n1.inputs["Vector"])
    ao = node(nt, "ShaderNodeAmbientOcclusion", (-1100, 650), Distance=0.035)
    ao.samples = 8
    aor = node(nt, "ShaderNodeMapRange", (-850, 650), **{"From Min": 0.3, "From Max": 0.95})
    L(ao.outputs["AO"], aor.inputs["Value"])
    # wear: big soft patches (tone + roughness) and fine scratches
    wear = node(nt, "ShaderNodeMapRange", (-850, 300), **{"From Min": 0.38, "From Max": 0.68})
    L(n1.outputs["Fac"], wear.inputs["Value"])
    base = node(nt, "ShaderNodeValToRGB", (-600, 380))
    if side:
        base.color_ramp.elements[0].color = (0.55, 0.27, 0.05, 1)
        base.color_ramp.elements[1].color = (0.72, 0.40, 0.10, 1)
    else:
        base.color_ramp.elements[0].color = (0.96, 0.62, 0.20, 1)
        base.color_ramp.elements[1].color = (1.0, 0.75, 0.31, 1)
    L(wear.outputs["Result"], base.inputs["Fac"])
    cav = node(nt, "ShaderNodeValToRGB", (-600, 650))
    cav.color_ramp.elements[0].color = (0.30, 0.13, 0.03, 1)
    cav.color_ramp.elements[1].color = (1.0, 1.0, 1.0, 1)
    L(aor.outputs["Result"], cav.inputs["Fac"])
    tone = node(nt, "ShaderNodeMix", (-350, 500))
    tone.data_type, tone.blend_type = "RGBA", "MULTIPLY"
    tone.inputs["Factor"].default_value = 1.0
    L(base.outputs["Color"], tone.inputs[6])
    L(cav.outputs["Color"], tone.inputs[7])
    L(tone.outputs[2], p.inputs["Base Color"])
    p.inputs["Metallic"].default_value = 1.0
    rr = node(nt, "ShaderNodeMapRange", (-600, 150), **{"To Min": 0.30 if side else 0.22, "To Max": 0.42 if side else 0.30})
    L(wear.outputs["Result"], rr.inputs["Value"])
    aoi = node(nt, "ShaderNodeMapRange", (-600, 0), **{"To Min": 0.25, "To Max": 0.0})
    L(aor.outputs["Result"], aoi.inputs["Value"])
    rough = node(nt, "ShaderNodeMath", (-350, 100))
    rough.operation = "ADD"
    L(rr.outputs["Result"], rough.inputs[0])
    L(aoi.outputs["Result"], rough.inputs[1])
    L(rough.outputs[0], p.inputs["Roughness"])
    mp = node(nt, "ShaderNodeMapping", (-1200, -300))
    mp.inputs["Scale"].default_value = (70.0, 2.0, 70.0) if side else (90.0, 6.0, 90.0)
    mp.inputs["Rotation"].default_value = (0.0, 0.0, 0.0) if side else (0.3, 0.0, 0.5)
    L(tc.outputs["Object"], mp.inputs["Vector"])
    n2 = node(nt, "ShaderNodeTexNoise", (-1000, -300), Scale=3.0, Detail=2.0)
    L(mp.outputs["Vector"], n2.inputs["Vector"])
    n3 = node(nt, "ShaderNodeTexNoise", (-1000, -550), Scale=180.0, Detail=3.0)
    L(tc.outputs["Object"], n3.inputs["Vector"])
    hb = node(nt, "ShaderNodeMath", (-780, -400))
    hb.operation = "ADD"
    L(n2.outputs["Fac"], hb.inputs[0])
    L(n3.outputs["Fac"], hb.inputs[1])
    bev = node(nt, "ShaderNodeBevel", (-780, -650), Radius=0.0035)
    bev.samples = 8
    bump = node(nt, "ShaderNodeBump", (-560, -450), Strength=0.10 if side else 0.06, Distance=0.0006)
    L(hb.outputs[0], bump.inputs["Height"])
    L(bev.outputs["Normal"], bump.inputs["Normal"])
    L(bump.outputs["Normal"], p.inputs["Normal"])
    p.inputs["Specular Tint"].default_value = (1.0, 0.8, 0.45, 1)
    return m


def mat_enamel_e():
    """Deep emerald enamel under a mirror clear coat, with thin glowing magic cracks and a glow rising at the groove."""
    m = bpy.data.materials.new("Logo_Green_v2")
    nt = m.node_tree
    L = nt.links.new
    p = get_principled(nt)
    tc = node(nt, "ShaderNodeTexCoord", (-1600, 0))
    nz = node(nt, "ShaderNodeTexNoise", (-1300, -700), Scale=3.0, Detail=4.0, Roughness=0.6)
    L(tc.outputs["Object"], nz.inputs["Vector"])
    mv = node(nt, "ShaderNodeVectorMath", (-1100, -700))
    mv.operation = "MULTIPLY_ADD"
    mv.inputs[1].default_value = (0.09, 0.09, 0.09)
    L(nz.outputs["Color"], mv.inputs[0])
    L(tc.outputs["Object"], mv.inputs[2])
    vv = node(nt, "ShaderNodeTexVoronoi", (-900, -700), Scale=9.0)
    vv.feature = "DISTANCE_TO_EDGE"
    L(mv.outputs["Vector"], vv.inputs["Vector"])
    vr = node(nt, "ShaderNodeMapRange", (-700, -700), **{"From Min": 0.0, "From Max": 0.012, "To Min": 1.0, "To Max": 0.0})
    L(vv.outputs["Distance"], vr.inputs["Value"])
    vv2 = node(nt, "ShaderNodeTexVoronoi", (-900, -950), Scale=26.0)
    vv2.feature = "DISTANCE_TO_EDGE"
    L(mv.outputs["Vector"], vv2.inputs["Vector"])
    vr2 = node(nt, "ShaderNodeMapRange", (-700, -950), **{"From Min": 0.0, "From Max": 0.01, "To Min": 0.3, "To Max": 0.0})
    L(vv2.outputs["Distance"], vr2.inputs["Value"])
    crack = node(nt, "ShaderNodeMath", (-500, -800))
    crack.operation = "MAXIMUM"
    L(vr.outputs["Result"], crack.inputs[0])
    L(vr2.outputs["Result"], crack.inputs[1])
    mot = node(nt, "ShaderNodeTexNoise", (-1000, 300), Scale=6.0, Detail=5.0)
    L(tc.outputs["Object"], mot.inputs["Vector"])
    ramp = node(nt, "ShaderNodeValToRGB", (-700, 300))
    ramp.color_ramp.elements[0].color = (0.001, 0.012, 0.003, 1)
    ramp.color_ramp.elements[1].color = (0.006, 0.050, 0.012, 1)
    L(mot.outputs["Fac"], ramp.inputs["Fac"])
    L(ramp.outputs["Color"], p.inputs["Base Color"])
    p.inputs["Metallic"].default_value = 0.35
    p.inputs["Roughness"].default_value = 0.34
    p.inputs["Coat Weight"].default_value = 1.0
    p.inputs["Coat Roughness"].default_value = 0.04
    p.inputs["Coat Tint"].default_value = (0.25, 0.85, 0.35, 1)
    fl = node(nt, "ShaderNodeBump", (-500, -200), Strength=0.35, Distance=0.002)
    L(crack.outputs[0], fl.inputs["Height"])
    fl.invert = True
    L(fl.outputs["Normal"], p.inputs["Normal"])
    rim = node(nt, "ShaderNodeAttribute", (-900, -1200))
    rim.attribute_name = "rim"
    rp = node(nt, "ShaderNodeMath", (-700, -1200))
    rp.operation = "POWER"
    rp.inputs[1].default_value = 2.0
    L(rim.outputs["Fac"], rp.inputs[0])
    rk = node(nt, "ShaderNodeMath", (-550, -1200))
    rk.operation = "MULTIPLY"
    rk.inputs[1].default_value = 0.2
    L(rp.outputs[0], rk.inputs[0])
    cw = node(nt, "ShaderNodeMath", (-350, -800))
    cw.operation = "MULTIPLY"
    cw.inputs[1].default_value = 0.6
    L(crack.outputs[0], cw.inputs[0])
    em = node(nt, "ShaderNodeMath", (-150, -900))
    em.operation = "ADD"
    L(cw.outputs[0], em.inputs[0])
    L(rk.outputs[0], em.inputs[1])
    L(em.outputs[0], p.inputs["Emission Strength"])
    p.inputs["Emission Color"].default_value = (0.25, 1.0, 0.18, 1)
    m["glow_strength_node"] = em.name
    return m


FIRE = dict(margin=0.42, res=1024, reach=0.45)


def fire_sdf_image(glyphs):
    """Signed-free distance (outside only) from the merged digit silhouette, as an image across the fire card."""
    import numpy as np
    loops = [L for gl, _ in glyphs for L in gl]
    x0, y0, x1, y1 = bbox(loops)
    M = FIRE["margin"]
    x0, y0, x1, y1 = x0 - M, y0 - M, x1 + M, y1 + M * 1.35          # more room above for the flames
    W = FIRE["res"]
    H = int(W * (y1 - y0) / (x1 - x0))
    xs = x0 + (np.arange(W) + 0.5) / W * (x1 - x0)
    ys = y0 + (np.arange(H) + 0.5) / H * (y1 - y0)
    X, Y = np.meshgrid(xs, ys)
    P_ = np.stack([X.ravel(), Y.ravel()], 1)
    d = seg_dist_fn(loops)(P_)
    inside = np.zeros(len(P_), bool)
    for gl, _ in glyphs:                       # per digit even-odd, then union
        ins = np.zeros(len(P_), bool)
        for Lp in gl:
            A = np.array([[q.x, q.y] for q in Lp])
            B = np.roll(A, -1, 0)
            for (ax, ay), (bx, by) in zip(A, B):
                if abs(by - ay) < 1e-12:
                    continue
                c = (ay > P_[:, 1]) != (by > P_[:, 1])
                xi = ax + (P_[:, 1] - ay) * (bx - ax) / (by - ay)
                ins ^= c & (P_[:, 0] < xi)
        inside |= ins
    din = np.where(inside, d, 0.0)              # depth inside a digit (the glow fades in from the edges, no flat slab)
    d[inside] = 0.0
    v = np.clip(d / FIRE["reach"], 0.0, 1.0).reshape(H, W)
    gi = np.clip(din / 0.06, 0.0, 1.0).reshape(H, W)
    img = bpy.data.images.new("Logo_Fire_SDF", W, H, alpha=False)
    img.pixels = np.dstack([v, gi, np.zeros_like(v), np.ones_like(v)]).ravel().astype(np.float32)   # R: distance outside, G: depth inside
    path = os.path.join(HERE, "logo_fire_sdf.png")
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    img.source = "FILE"
    img.reload()
    img.pack()
    img.colorspace_settings.name = "Non-Color"
    return img, (x0, y0, x1, y1)


def mat_fire(img, bb):
    """Green magic fire behind the logo: glow falling off from the silhouette, licked upward by stretched noise.
    Camera rays only (no light, no shadow, invisible in reflections). Alpha = glow, so it also works on a
    transparent background (the Steam library logo)."""
    m = bpy.data.materials.new("Logo_Fire_v3")
    nt = m.node_tree
    nt.nodes.clear()
    L = nt.links.new
    out = node(nt, "ShaderNodeOutputMaterial", (900, 0))
    tc = node(nt, "ShaderNodeTexCoord", (-1600, 0))
    sep = node(nt, "ShaderNodeSeparateXYZ", (-1400, 0))
    # card coords from the OBJECT position (the logo's own space survives the join into one mesh; UVs did not render)
    bx0, by0, bx1, by1 = bb
    sub_ = node(nt, "ShaderNodeVectorMath", (-1550, 200))
    sub_.operation = "SUBTRACT"
    sub_.inputs[1].default_value = (bx0, 0.0, by0)
    L(tc.outputs["Object"], sub_.inputs[0])
    mul_ = node(nt, "ShaderNodeVectorMath", (-1450, 200))
    mul_.operation = "MULTIPLY"
    mul_.inputs[1].default_value = (1.0 / (bx1 - bx0), 0.0, 1.0 / (by1 - by0))
    L(sub_.outputs[0], mul_.inputs[0])
    s0 = node(nt, "ShaderNodeSeparateXYZ", (-1350, 200))
    L(mul_.outputs[0], s0.inputs[0])
    c0 = node(nt, "ShaderNodeCombineXYZ", (-1250, 200))
    L(s0.outputs["X"], c0.inputs["X"])
    L(s0.outputs["Z"], c0.inputs["Y"])
    uvw = c0.outputs[0]
    L(uvw, sep.inputs[0])
    # flame field: noise stretched vertically, scrolled so tongues rise
    mp = node(nt, "ShaderNodeMapping", (-1400, -300))
    mp.inputs["Scale"].default_value = (22.0, 3.6, 1.0)
    L(uvw, mp.inputs["Vector"])
    fn = node(nt, "ShaderNodeTexNoise", (-1200, -300), Scale=1.0, Detail=5.0, Roughness=0.62, Distortion=0.6)
    L(mp.outputs["Vector"], fn.inputs["Vector"])
    fr = node(nt, "ShaderNodeMapRange", (-1000, -300), **{"From Min": 0.42, "From Max": 0.70, "To Min": 0.0, "To Max": 1.0})
    L(fn.outputs["Fac"], fr.inputs["Value"])
    wob = node(nt, "ShaderNodeTexNoise", (-1200, -600), Scale=9.0, Detail=3.0)
    L(uvw, wob.inputs["Vector"])
    # sample the distance a little BELOW the pixel (more where the flame field is strong) -> glow tongues rise upward
    du = node(nt, "ShaderNodeMath", (-850, -600))
    du.operation = "MULTIPLY_ADD"
    du.inputs[1].default_value = 0.025
    du.inputs[2].default_value = -0.0125
    L(wob.outputs["Fac"], du.inputs[0])
    dv = node(nt, "ShaderNodeMath", (-850, -300))
    dv.operation = "MULTIPLY"
    dv.inputs[1].default_value = -0.11
    L(fr.outputs["Result"], dv.inputs[0])
    u2 = node(nt, "ShaderNodeMath", (-650, 100))
    u2.operation = "ADD"
    L(sep.outputs["X"], u2.inputs[0])
    L(du.outputs[0], u2.inputs[1])
    v2 = node(nt, "ShaderNodeMath", (-650, -100))
    v2.operation = "ADD"
    L(sep.outputs["Y"], v2.inputs[0])
    L(dv.outputs[0], v2.inputs[1])
    cmb = node(nt, "ShaderNodeCombineXYZ", (-480, 0))
    L(u2.outputs[0], cmb.inputs[0])
    L(v2.outputs[0], cmb.inputs[1])
    tx = node(nt, "ShaderNodeTexImage", (-300, 0))
    tx.image = img
    tx.extension = "EXTEND"
    tx.interpolation = "Cubic"
    L(cmb.outputs[0], tx.inputs["Vector"])
    # glow: tight bright core + soft wide falloff, both broken up by the flame field
    inv = node(nt, "ShaderNodeMath", (-100, 0))
    inv.operation = "SUBTRACT"
    inv.inputs[0].default_value = 1.0
    L(tx.outputs["Color"], inv.inputs[1])
    core = node(nt, "ShaderNodeMath", (80, 120))
    core.operation = "POWER"
    core.inputs[1].default_value = 16.0
    L(inv.outputs[0], core.inputs[0])
    soft = node(nt, "ShaderNodeMath", (80, -60))
    soft.operation = "POWER"
    soft.inputs[1].default_value = 4.0
    L(inv.outputs[0], soft.inputs[0])
    fm = node(nt, "ShaderNodeMapRange", (80, -260), **{"From Min": 0.0, "From Max": 1.0, "To Min": 0.35, "To Max": 1.0})
    L(fr.outputs["Result"], fm.inputs["Value"])
    sm = node(nt, "ShaderNodeMath", (250, -60))
    sm.operation = "MULTIPLY"
    L(soft.outputs[0], sm.inputs[0])
    L(fm.outputs["Result"], sm.inputs[1])
    sm2 = node(nt, "ShaderNodeMath", (380, -60))
    sm2.operation = "MULTIPLY"
    sm2.inputs[1].default_value = 0.4
    L(sm.outputs[0], sm2.inputs[0])
    g = node(nt, "ShaderNodeMath", (500, 40))
    g.operation = "ADD"
    g.use_clamp = True
    L(core.outputs[0], g.inputs[0])
    L(sm2.outputs[0], g.inputs[1])
    g.name = g.label = "Fire_Amount"
    sepc = node(nt, "ShaderNodeSeparateColor", (-200, -150))
    L(tx.outputs["Color"], sepc.inputs[0])
    L(sepc.outputs["Red"], inv.inputs[1])
    fin = node(nt, "ShaderNodeMath", (300, -300))
    fin.operation = "SUBTRACT"
    fin.inputs[0].default_value = 1.0
    L(sepc.outputs["Green"], fin.inputs[1])
    fin2 = node(nt, "ShaderNodeMath", (420, -300))
    fin2.operation = "POWER"
    fin2.inputs[1].default_value = 2.0
    L(fin.outputs[0], fin2.inputs[0])
    gf = node(nt, "ShaderNodeMath", (560, -100))
    gf.operation = "MULTIPLY"
    L(g.outputs[0], gf.inputs[0])
    L(fin2.outputs[0], gf.inputs[1])
    g = gf                       # inside a digit the glow fades out from the edge
    col = node(nt, "ShaderNodeValToRGB", (500, 300))
    els = col.color_ramp.elements
    els[0].position, els[0].color = 0.0, (0.0, 0.10, 0.005, 1)
    els[1].position, els[1].color = 0.6, (0.06, 1.0, 0.05, 1)
    e3 = els.new(1.0)
    e3.color = (0.30, 1.0, 0.10, 1)
    L(g.outputs[0], col.inputs["Fac"])
    em = node(nt, "ShaderNodeEmission", (680, 200))
    L(col.outputs["Color"], em.inputs["Color"])
    st = node(nt, "ShaderNodeValue", (500, 450))
    st.name = st.label = "Fire_Strength"
    st.outputs[0].default_value = 1.1
    sg = node(nt, "ShaderNodeMath", (600, 520))
    sg.operation = "MULTIPLY_ADD"
    sg.inputs[2].default_value = 0.25
    L(g.outputs[0], sg.inputs[0])
    sg.inputs[1].default_value = 1.0
    sg2 = node(nt, "ShaderNodeMath", (700, 520))
    sg2.operation = "MULTIPLY"
    L(sg.outputs[0], sg2.inputs[0])
    L(st.outputs[0], sg2.inputs[1])
    L(sg2.outputs[0], em.inputs["Strength"])
    tr = node(nt, "ShaderNodeBsdfTransparent", (680, 0))
    lp = node(nt, "ShaderNodeLightPath", (500, -350))
    camg = node(nt, "ShaderNodeMath", (680, -250))
    camg.operation = "MULTIPLY"
    al = node(nt, "ShaderNodeMapRange", (560, -250), **{"From Min": 0.05, "From Max": 0.32, "To Min": 0.0, "To Max": 1.0})
    al.interpolation_type = "SMOOTHSTEP"          # narrow soft edge: flames read as flames on a transparent background too
    L(g.outputs[0], al.inputs["Value"])
    L(al.outputs["Result"], camg.inputs[0])
    L(lp.outputs["Is Camera Ray"], camg.inputs[1])
    mx = node(nt, "ShaderNodeMixShader", (800, 0))
    L(camg.outputs[0], mx.inputs["Fac"])
    L(tr.outputs[0], mx.inputs[1])
    L(em.outputs[0], mx.inputs[2])
    L(mx.outputs[0], out.inputs["Surface"])
    try:
        m.emission_sampling = "NONE"
    except Exception:
        pass
    m.blend_method = "BLEND"
    return m


def build_logo_e(col, root):
    gold, green, glow = mat_gold_e(), mat_enamel_e(), mat_glow()
    side = mat_gold_e("Logo_Gold_Side_v3", side=True)
    prof = profile_for("E")
    glyphs = glyphs_for("E")
    import numpy as np
    for (loops, zoff), ch in zip(glyphs, "429"):
        dfn = seg_dist_fn(loops)
        x0, y0, x1, y1 = bbox(loops)
        gx, gy = np.meshgrid(np.linspace(x0, x1, 120), np.linspace(y0, y1, 120))
        pts = [Vector((a, b)) for a, b in zip(gx.ravel(), gy.ravel())]
        ins = [q for q in pts if sum(1 for Lp in loops if point_in_poly(q, Lp)) % 2 == 1]
        dd = dfn([[q.x, q.y] for q in ins])
        print("DIGIT", ch, "stroke half-width max %.3f  p75 %.3f" % (float(dd.max()), float(np.percentile(dd, 75))))
        me = loft("Logo_429_Digit_" + ch, loops, prof, zoff=zoff, inlay=INLAY["E"], corner_deg=11.0, cap_bottom=True)
        for m in (gold, green, glow, side):
            me.materials.append(m)
        ob = bpy.data.objects.new("Logo_429_Digit_" + ch, me)
        col.objects.link(ob)
        ob.parent = root
    # fire card behind everything
    img, (x0, y0, x1, y1) = fire_sdf_image(glyphs)
    fm = mat_fire(img, (x0, y0, x1, y1))
    yb = 0.06                    # inside the digits' depth: the walls hide the card inside, little parallax when turned
    me = bpy.data.meshes.new("Logo_429_Fire")
    me.from_pydata([(x0, yb, y0), (x1, yb, y0), (x1, yb, y1), (x0, yb, y1)], [], [(0, 1, 2, 3)])
    uv = me.uv_layers.new(name="UVMap")
    for li, c in enumerate([(0, 0), (1, 0), (1, 1), (0, 1)]):
        uv.data[li].uv = c
    for m in (gold, green, glow, side, fm):
        me.materials.append(m)
    for pl in me.polygons:
        pl.material_index = MAT_INDEX["fire"]
    fo = bpy.data.objects.new("Logo_429_Fire", me)
    col.objects.link(fo)
    fo.parent = root
    fo.visible_shadow = fo.visible_diffuse = fo.visible_glossy = False
    if SHOW_STEM:
        add_stem(col, root, glyphs, gold, green, glow, prof, front_z=0.080)
    add_reflectors(col, root, style="E")
    return col, root


def add_reflectors(col, root, style="D"):
    """Two soft emissive cards that only show up in REFLECTIONS (invisible to the camera, cast no light on
    diffuse surfaces, no shadows). They travel with the logo, so the polished gold always has something warm to
    mirror, whatever scene it is appended to. Delete the sub-collection to drop them."""
    rc = bpy.data.collections.new("Logo_429_v2_Reflectors")
    col.children.link(rc)
    cards = (
            ("Logo_429_Reflector_Top", (0.0, -1.5, 1.0), 3.0, 0.9, (1.0, 0.88, 0.66, 1), 3.0),
            ("Logo_429_Reflector_Front", (0.25, -2.2, 0.15), 3.4, 1.0, (1.0, 0.82, 0.55, 1), 0.9),
            ("Logo_429_Reflector_Low", (0.0, -1.4, -0.95), 3.0, 0.6, (1.0, 0.55, 0.2, 1), 1.4))
    if style == "E":      # warm orange cards + one BLACK card, so every chisel facet has a bright and a dark side
        cards = (
            ("Logo_429_Reflector_Top", (-0.6, -1.3, 1.25), 2.4, 0.7, (1.0, 0.62, 0.22, 1), 4.0),
            ("Logo_429_Reflector_Low", (0.3, -1.4, -0.95), 3.0, 0.6, (1.0, 0.42, 0.08, 1), 2.2),
            ("Logo_429_Reflector_Left", (-1.9, -1.2, 0.2), 0.8, 1.6, (1.0, 0.66, 0.30, 1), 1.4),
            ("Logo_429_Reflector_Black", (1.5, -1.6, 0.35), 1.6, 1.8, (0.0, 0.0, 0.0, 1), 0.0))
    for name, loc, w, h, color, strength in cards:
        o = card(rc, name, loc, (0, 0, 0), w, h, color, strength)
        o.data.materials[0].name = name.replace("Logo_429_", "Logo_") + "_v2"
        o.visible_transmission = False
        o.visible_volume_scatter = False
        o.parent = root


def leaf_outline(n=40):
    """A pumpkin-ish leaf, 1 long, base at (0,0), tip at (1,0): soft lobes along the edge."""
    pts = []
    for i in range(n):
        t = i / n * 2 * math.pi
        x = 0.5 - 0.5 * math.cos(t)
        w = math.sin(math.pi * x ** 0.85) ** 0.75 * 0.42
        w *= 1.0 + 0.16 * math.sin(3.0 * math.pi * x + 0.4) * (1 - x)
        pts.append(Vector((x, w if t < math.pi else -w * 0.92)))
    pts = clean_loop(pts)
    if signed_area(pts) < 0:
        pts.reverse()
    return pts


def add_stem(col, root, glyphs, gold, green, glow, prof, front_z=None):
    """A gold pumpkin stem growing out of the top of the 9, one gold-rimmed enamel leaf and a curling vine."""
    loops9, zoff9 = glyphs[2]
    outer = loops9[0]
    top = max(outer, key=lambda p: p.y)
    h = 0.045 + zoff9                       # stem axis height in front of the plate
    base = Vector((top.x - 0.005, -h, top.y - 0.045))

    def P(x, z, y=0.0):
        return base + Vector((x, -y, z))
    path = bez(P(0, 0), P(0.0, 0.09), P(0.03, 0.16), P(0.11, 0.19, 0.01), 18)
    me = sweep("Logo_429_Stem", path,
               lambda t: 0.040 * (1 - 0.42 * t) + 0.022 * max(0.0, 1 - t * 4.5) ** 2 + 0.006 * max(0.0, t - 0.85) / 0.15,
               sides=36, lobes=6, lobe_amt=0.13, twist=1.1)
    me.materials.append(gold)
    st = bpy.data.objects.new("Logo_429_Stem", me)
    col.objects.link(st)
    st.parent = root
    # leaf: lofted with a mini version of the frame profile, then curled toward the viewer
    s = 0.25
    ang = math.radians(152)
    rot = Matrix.Rotation(ang, 2)
    anchor = Vector((base.x - 0.02, base.z + 0.035))
    lf = [rot @ (p * s) + anchor for p in leaf_outline()]
    lprof = build_profile([(0.0, 0.0, 0, "gold", 0), (0.0, 0.010, 0.004, "gold", 0), (0.008, 0.019, 0.003, "gold", 0),
                           (0.012, 0.016, 0.001, "gold", 0), (0.013, 0.013, 0, "glow", 1.0), (0.015, 0.012, 0, "green", 1.0),
                           (0.024, 0.018, 0.004, "green", 0.3), (0.04, 0.019, 0, "green", 0.0)])
    me = loft("Logo_429_Leaf", [lf], lprof, zoff=h - 0.005, cap_bottom=True)
    for m in (gold, green, glow):
        me.materials.append(m)
    lo = bpy.data.objects.new("Logo_429_Leaf", me)
    col.objects.link(lo)
    lo.parent = root
    a3 = Vector((anchor.x, 0, anchor.y))
    dirv = Vector((math.cos(ang), 0, math.sin(ang)))
    bm = bmesh.new()
    bm.from_mesh(me)
    for v in bm.verts:
        d = v.co - a3
        along = max(0.0, d.dot(dirv)) / s
        side = (d - dirv * d.dot(dirv))
        side.y = 0
        v.co.y -= 0.06 * along ** 2 + 0.25 * side.length ** 2 / s    # tip and edges curl forward
    bm.to_mesh(me)
    bm.free()
    # midrib: a thin gold vein down the leaf
    rib = [Vector((anchor.x, 0, anchor.y)) + dirv * (s * 0.92 * t) for t in [i / 15 for i in range(16)]]
    for p in rib:
        along = (p - a3).dot(dirv) / s
        p.y = -(h - 0.005 + 0.019 + 0.003) - 0.06 * along ** 2
    me = sweep("Logo_429_Leaf_Rib", rib, lambda t: 0.0065 * (1 - 0.75 * t), sides=8)
    me.materials.append(gold)
    rb = bpy.data.objects.new("Logo_429_Leaf_Rib", me)
    col.objects.link(rb)
    rb.parent = root
    # vine: a tapering gold tendril leaving the stem base and curling over the 9's right shoulder
    pts = []
    c = P(0.15, 0.02, 0.0)
    for i in range(70):
        t = i / 69
        a = math.radians(175) - t * math.radians(620)
        r = 0.075 * (1 - 0.78 * t)
        p = c + Vector((math.cos(a) * r, 0.0, math.sin(a) * r))
        p += Vector((-0.07 * (1 - t) ** 4, 0.0, -0.02 * (1 - t) ** 4))
        p.y = -h - 0.015 - 0.01 * math.sin(t * 3)
        pts.append(p)
    me = sweep("Logo_429_Vine", pts, lambda t: 0.0105 * (1 - 0.78 * t), sides=12)
    me.materials.append(gold)
    vn = bpy.data.objects.new("Logo_429_Vine", me)
    col.objects.link(vn)
    vn.parent = root


# ====================================================================== test rig
def area(col, name, loc, target, size, color, power, shape="RECTANGLE", sy=None):
    ld = bpy.data.lights.new(name, "AREA")
    ld.shape = shape
    ld.size = size
    if sy:
        ld.size_y = sy
    ld.color = color
    ld.energy = power
    o = bpy.data.objects.new(name, ld)
    col.objects.link(o)
    o.location = loc
    d = (Vector(target) - Vector(loc))
    o.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    return o


def card(col, name, loc, target, w, h, color, strength):
    me = bpy.data.meshes.new(name)
    me.from_pydata([(-w / 2, -h / 2, 0), (w / 2, -h / 2, 0), (w / 2, h / 2, 0), (-w / 2, h / 2, 0)], [], [(0, 1, 2, 3)])
    m = bpy.data.materials.new(name + "_Mat")
    nt = m.node_tree
    nt.nodes.clear()
    o_ = nt.nodes.new("ShaderNodeOutputMaterial")
    e = nt.nodes.new("ShaderNodeEmission")
    e.inputs["Color"].default_value = color
    e.inputs["Strength"].default_value = strength
    # soft falloff toward the card edges
    tc = nt.nodes.new("ShaderNodeTexCoord")
    gr = nt.nodes.new("ShaderNodeTexGradient")
    gr.gradient_type = "QUADRATIC_SPHERE"
    mp = nt.nodes.new("ShaderNodeMapping")
    mp.inputs["Location"].default_value = (-0.5, -0.5, 0)
    mp.inputs["Scale"].default_value = (2.0 / w, 2.0 / h, 1.0)
    nt.links.new(tc.outputs["Object"], mp.inputs["Vector"])
    mp.vector_type = "TEXTURE"
    nt.links.new(tc.outputs["Object"], mp.inputs["Vector"])
    mul = nt.nodes.new("ShaderNodeMath")
    mul.operation = "MULTIPLY"
    mul.inputs[1].default_value = strength
    nt.links.new(e.outputs[0], o_.inputs["Surface"])
    me.materials.append(m)
    o = bpy.data.objects.new(name, me)
    col.objects.link(o)
    o.location = loc
    o.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("Z", "Y").to_euler()
    o.visible_camera = False
    o.visible_diffuse = False
    o.visible_shadow = False
    return o


def build_rig(view="front"):
    sc = bpy.context.scene
    rig = new_coll("Logo_Test_Rig")
    cam_d = bpy.data.cameras.new("Logo_Test_Cam")
    cam_d.lens = 85
    cam = bpy.data.objects.new("Logo_Test_Cam", cam_d)
    rig.objects.link(cam)
    if view == "three":
        cam.location = (-3.2, -6.4, 1.2)
    else:
        cam.location = (0.0, -7.6, 0.35)
    cam.rotation_euler = (Vector((0, 0, 0.0)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam
    # moonlit key from upper left, warm pumpkin bounce from below right, cool rims from behind
    area(rig, "Rig_Key", (-3.0, -4.5, 3.2), (0, 0, 0), 2.2, (1.0, 0.93, 0.82), 520)
    area(rig, "Rig_Fill_Cool", (4.0, -4.0, 0.8), (0, 0, 0), 3.0, (0.55, 0.68, 1.0), 160)
    area(rig, "Rig_Under_Warm", (1.5, -3.0, -2.6), (0, 0, 0), 2.0, (1.0, 0.5, 0.15), 220)
    area(rig, "Rig_Rim_L", (-3.5, 2.5, 2.0), (0, 0, 0), 1.0, (0.6, 1.0, 0.65), 380)
    area(rig, "Rig_Rim_R", (3.5, 2.5, 1.6), (0, 0, 0), 1.0, (0.75, 0.8, 1.0), 300)
    # world: very dark blue night
    w = bpy.data.worlds.new("Logo_Test_World")
    sc.world = w
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (0.004, 0.006, 0.012, 1)
    bg.inputs["Strength"].default_value = 1.0
    # render settings like the main pipeline (Cycles, AgX Punchy, bloom)
    sc.render.engine = "CYCLES"
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        prefs.compute_device_type = "OPTIX"
        prefs.get_devices()
        for d in prefs.devices:
            d.use = d.type == "OPTIX"
        sc.cycles.device = "GPU"
    except Exception as e:
        print("GPU setup failed", e)
    sc.cycles.use_denoising = True
    sc.view_settings.view_transform = "AgX"
    for lk in ("AgX - Punchy", "Punchy"):
        try:
            sc.view_settings.look = lk
            break
        except Exception:
            pass
    setup_bloom(sc)
    return rig


def setup_bloom(sc):
    try:
        tree = sc.compositing_node_group
        if tree is None:
            tree = bpy.data.node_groups.new("Logo_Test_Comp", "CompositorNodeTree")
            sc.compositing_node_group = tree
        tree.nodes.clear()
        if not any(getattr(s_, "in_out", None) == "OUTPUT" for s_ in tree.interface.items_tree):
            tree.interface.new_socket("Image", in_out="OUTPUT", socket_type="NodeSocketColor")
        rl = tree.nodes.new("CompositorNodeRLayers")
        bl = tree.nodes.new("CompositorNodeGlare")
        for nm, v in (("Type", "Bloom"), ("Quality", "High"), ("Threshold", 0.85), ("Strength", 0.55), ("Size", 0.72)):
            bl.inputs[nm].default_value = v
        go = tree.nodes.new("NodeGroupOutput")
        sa = tree.nodes.new("CompositorNodeSetAlpha")
        if "Type" in sa.inputs:
            sa.inputs["Type"].default_value = "Replace Alpha"
        tree.links.new(rl.outputs["Image"], bl.inputs["Image"])
        tree.links.new(bl.outputs["Image"], sa.inputs["Image"])
        tree.links.new(rl.outputs["Alpha"], sa.inputs["Alpha"])
        tree.links.new(sa.outputs["Image"], go.inputs[0])
    except Exception as e:
        print("bloom setup skipped:", e)


def exclude_rig():
    lc = bpy.context.view_layer.layer_collection.children.get("Logo_Test_Rig")
    return lc


def render(path, res="1280x720", samples=96):
    sc = bpy.context.scene
    w, h = (int(x) for x in res.split("x"))
    sc.render.resolution_x, sc.render.resolution_y = w, h
    sc.render.resolution_percentage = 100
    sc.cycles.samples = samples
    sc.render.filepath = path
    sc.render.image_settings.file_format = "PNG"
    if ARGS.get("transparent") == "1":
        sc.render.film_transparent = True
        sc.render.image_settings.color_mode = "RGBA"
    if ARGS.get("border"):
        x0, y0, x1, y1 = (float(v) for v in ARGS["border"].split(","))
        sc.render.use_border = True
        sc.render.use_crop_to_border = True
        sc.render.border_min_x, sc.render.border_min_y, sc.render.border_max_x, sc.render.border_max_y = x0, y0, x1, y1
    bpy.ops.render.render(write_still=True)
    print("RENDERED", path)


def thumb(path, width=190):
    img = bpy.data.images.load(path)
    w, h = img.size
    img.scale(width * 3, int(h * width * 3 / w))     # whole test frame; logo is ~1/3 of it
    out = path.replace(".png", "_thumb.png")
    img.filepath_raw = out
    img.file_format = "PNG"
    img.save()
    print("THUMB", out)


def stack(paths, out):
    import numpy as np
    imgs = [bpy.data.images.load(p) for p in paths]
    w, h = imgs[0].size
    arr = np.zeros((h * len(imgs), w, 4), dtype=np.float32)
    for i, im in enumerate(imgs):
        px = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)
        arr[(len(imgs) - 1 - i) * h:(len(imgs) - i) * h] = px
    o = bpy.data.images.new("stack", w, h * len(imgs), alpha=True)
    o.pixels = arr.ravel()
    o.filepath_raw = out
    o.file_format = "PNG"
    o.save()
    print("STACKED", out)


def main():
    os.makedirs(TESTS, exist_ok=True)
    if ARGS.get("sheet") == "1":
        outs = []
        for s in "ABC":
            reset()
            build_logo(s)
            build_rig(ARGS.get("view", "front"))
            p = os.path.join(TESTS, "_dir_%s.png" % s)
            render(p, ARGS.get("res", "1280x560"), int(ARGS.get("samples", 64)))
            outs.append(p)
        stack(outs, os.path.join(TESTS, ARGS.get("sheetname", "01_directions.png")))
        return
    style = ARGS.get("style", STYLE_DEFAULT)
    reset()
    build_logo(style)
    build_rig(ARGS.get("view", "front"))
    if ARGS.get("render"):
        p = ARGS["render"]
        if not os.path.isabs(p):
            p = os.path.join(TESTS, p)
        render(p, ARGS.get("res", "1280x720"), int(ARGS.get("samples", 96)))
        if ARGS.get("thumb") == "1":
            thumb(p)
    if ARGS.get("save", "1") == "1":
        lc = bpy.context.view_layer.layer_collection.children.get("Logo_Test_Rig")
        if lc:
            lc.exclude = True
        bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
        print("SAVED", OUT_BLEND)


main()
