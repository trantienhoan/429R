"""Rune library (2026-11-17): chunky glowing rune meshes to stick on anything.
24 Elder Futhark runes + 12 alchemy symbols, each in two looks:
  Inlay  = a raised glowing strip (sticks out 2 cm, 1 cm sunk in so it hides small gaps)
  Carved = a dark groove with a thin glowing line inside (fake carving, no boolean on the stone)
Every rune: 0.6 m tall, lies flat in XY facing +Z, pivot = its centre on the surface (z 0). Coloured from the
Rooftop palette (glow_gold by default). Saves Exports/Remodel/Rune_Library.blend and renders a sheet.
  blender -b --factory-startup --python Exports/Remodel/tools/rune_library.py -- <render_dir>"""
import bpy, bmesh, sys, os, math
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else os.path.join(REPO, "Exports", "Remodel", "rune_renders")
exec(open(os.path.join(HERE, "palette_kit.py"), encoding="utf-8").read())

SIZE = 0.6          # rune height in metres
W_STROKE = 0.115    # stroke width in glyph units (x SIZE = 7 cm)

# ---- glyphs: polylines in a box about 0.6 wide x 1 tall; ("arc", cx, cy, r, a0, a1) for arcs, ("dot", x, y, r)
P = lambda *pts: list(pts)
FUTHARK = {
    "Fehu":     [P((0, 0), (0, 1)), P((0, 0.5), (0.45, 0.78)), P((0, 0.76), (0.4, 1.0))],
    "Uruz":     [P((0, 0), (0, 1), (0.5, 0.72), (0.5, 0))],
    "Thurisaz": [P((0, 0), (0, 1)), P((0, 0.78), (0.42, 0.5), (0, 0.22))],
    "Ansuz":    [P((0, 0), (0, 1)), P((0, 1), (0.42, 0.74)), P((0, 0.74), (0.42, 0.48))],
    "Raidho":   [P((0, 0), (0, 1), (0.42, 0.78), (0, 0.55), (0.45, 0))],
    "Kenaz":    [P((0.42, 0.85), (0, 0.5), (0.42, 0.15))],
    "Gebo":     [P((0, 0), (0.62, 1)), P((0.62, 0), (0, 1))],
    "Wunjo":    [P((0, 0), (0, 1), (0.42, 0.78), (0, 0.55))],
    "Hagalaz":  [P((0, 0), (0, 1)), P((0.5, 0), (0.5, 1)), P((0, 0.65), (0.5, 0.35))],
    "Naudiz":   [P((0.25, 0), (0.25, 1)), P((0, 0.65), (0.5, 0.35))],
    "Isa":      [P((0, 0), (0, 1))],
    "Jera":     [P((0.22, 0.88), (0, 0.66), (0.22, 0.44)), P((0.34, 0.56), (0.56, 0.34), (0.34, 0.12))],
    "Eihwaz":   [P((0.25, 0), (0.25, 1)), P((0.25, 1), (0.52, 0.78)), P((0.25, 0), (-0.02, 0.22))],
    "Perthro":  [P((0, 0), (0, 1)), P((0, 1), (0.34, 0.76), (0.56, 0.96)), P((0, 0), (0.34, 0.24), (0.56, 0.04))],
    "Algiz":    [P((0.3, 0), (0.3, 1)), P((0.3, 0.55), (0, 0.95)), P((0.3, 0.55), (0.6, 0.95))],
    "Sowilo":   [P((0.42, 1), (0, 0.64), (0.45, 0.36), (0.03, 0))],
    "Tiwaz":    [P((0.3, 0), (0.3, 1)), P((0, 0.7), (0.3, 1), (0.6, 0.7))],
    "Berkano":  [P((0, 0), (0, 1), (0.4, 0.76), (0, 0.5), (0.4, 0.24), (0, 0))],
    "Ehwaz":    [P((0, 0), (0, 1), (0.3, 0.66), (0.6, 1), (0.6, 0))],
    "Mannaz":   [P((0, 0), (0, 1)), P((0.6, 0), (0.6, 1)), P((0, 1), (0.6, 0.56)), P((0.6, 1), (0, 0.56))],
    "Laguz":    [P((0, 0), (0, 1), (0.42, 0.7))],
    "Ingwaz":   [P((0.3, 0.2), (0.6, 0.5), (0.3, 0.8), (0, 0.5), (0.3, 0.2))],
    "Dagaz":    [P((0, 0), (0, 1)), P((0.72, 0), (0.72, 1)), P((0, 1), (0.72, 0)), P((0, 0), (0.72, 1))],
    "Othala":   [P((0, 0.04), (0.6, 0.64), (0.3, 0.95), (0, 0.64), (0.6, 0.04))],
}
ALCHEMY = {
    "Fire":    [P((0, 0.05), (0.66, 0.05), (0.33, 0.95), (0, 0.05))],
    "Water":   [P((0, 0.95), (0.66, 0.95), (0.33, 0.05), (0, 0.95))],
    "Air":     [P((0, 0.05), (0.66, 0.05), (0.33, 0.95), (0, 0.05)), P((-0.08, 0.38), (0.74, 0.38))],
    "Earth":   [P((0, 0.95), (0.66, 0.95), (0.33, 0.05), (0, 0.95)), P((-0.08, 0.62), (0.74, 0.62))],
    "Salt":    [("arc", 0.42, 0.5, 0.42, 0, 360), P((0, 0.5), (0.84, 0.5))],
    "Sun":     [("arc", 0.42, 0.5, 0.42, 0, 360), ("dot", 0.42, 0.5, 0.1)],
    "Moon":    [("arc", 0.45, 0.5, 0.45, 50, 310), ("arc", 0.68, 0.5, 0.34, 75, 285)],
    "Venus":   [("arc", 0.3, 0.72, 0.26, 0, 360), P((0.3, 0.46), (0.3, 0)), P((0.08, 0.2), (0.52, 0.2))],
    "Mercury": [("arc", 0.3, 0.62, 0.2, 0, 360), P((0.3, 0.42), (0.3, 0)), P((0.1, 0.18), (0.5, 0.18)),
                ("arc", 0.3, 0.98, 0.2, 200, 340)],
    "Sulfur":  [P((0, 0.5), (0.6, 0.5), (0.3, 1.0), (0, 0.5)), P((0.3, 0.5), (0.3, 0)), P((0.1, 0.22), (0.5, 0.22))],
    "Mars":    [("arc", 0.34, 0.34, 0.3, 0, 360), P((0.56, 0.56), (0.9, 0.9)), P((0.6, 0.9), (0.9, 0.9), (0.9, 0.6))],
    "Star":    [P(*[(0.5 + 0.5 * math.sin(math.radians(144 * k)), 0.5 + 0.5 * math.cos(math.radians(144 * k)))
                    for k in range(6)])],
}


def _bm_box(bm, a, b, w, z0, z1, ext=True):
    """Solid bar from a to b (2D), width w; ext: ends extended by w/2 (lone bars, arc caps)."""
    a, b = Vector((a[0], a[1], 0)), Vector((b[0], b[1], 0))
    d = (b - a)
    L = d.length
    if L < 1e-6:
        return
    d /= L
    n = Vector((-d.y, d.x, 0))
    e = w / 2 if ext else 0.0
    a2, b2 = a - d * e, b + d * e
    ring = [a2 + n * w / 2, b2 + n * w / 2, b2 - n * w / 2, a2 - n * w / 2]
    _bm_prism(bm, ring, z0, z1)


def _bm_prism(bm, ring, z0, z1):
    lo = [bm.verts.new((p.x, p.y, z0)) for p in ring]
    hi = [bm.verts.new((p.x, p.y, z1)) for p in ring]
    bm.faces.new(list(reversed(lo)))
    bm.faces.new(hi)
    k = len(ring)
    for i in range(k):
        j = (i + 1) % k
        bm.faces.new((lo[i], lo[j], hi[j], hi[i]))


def _bm_arc(bm, cx, cy, r, a0, a1, w, z0, z1):
    full = abs(a1 - a0) >= 359.9
    n = max(6, int(abs(a1 - a0) / 20))
    angs = [math.radians(a0 + (a1 - a0) * i / n) for i in range(n + (0 if full else 1))]
    outer = [Vector((cx + (r + w / 2) * math.cos(t), cy + (r + w / 2) * math.sin(t), 0)) for t in angs]
    inner = [Vector((cx + (r - w / 2) * math.cos(t), cy + (r - w / 2) * math.sin(t), 0)) for t in angs]
    lo_o = [bm.verts.new((p.x, p.y, z0)) for p in outer]; hi_o = [bm.verts.new((p.x, p.y, z1)) for p in outer]
    lo_i = [bm.verts.new((p.x, p.y, z0)) for p in inner]; hi_i = [bm.verts.new((p.x, p.y, z1)) for p in inner]
    m = len(angs)
    segs = m if full else m - 1
    for i in range(segs):
        j = (i + 1) % m
        bm.faces.new((hi_i[i], hi_o[i], hi_o[j], hi_i[j]))          # top
        bm.faces.new((lo_i[j], lo_o[j], lo_o[i], lo_i[i]))          # bottom
        bm.faces.new((lo_o[i], lo_o[j], hi_o[j], hi_o[i]))          # outer wall
        bm.faces.new((lo_i[j], lo_i[i], hi_i[i], hi_i[j]))          # inner wall
    if not full:
        for i, s in ((0, 1), (m - 1, -1)):
            q = (lo_i[i], lo_o[i], hi_o[i], hi_i[i])
            bm.faces.new(q if s < 0 else tuple(reversed(q)))
    # round end caps: overlap with a small square so the joints stay chunky
    if not full:
        for t in (angs[0], angs[-1]):
            c = Vector((cx + r * math.cos(t), cy + r * math.sin(t), 0))
            tg = Vector((-math.sin(t), math.cos(t), 0))
            _bm_box(bm, c - tg * 0.001, c + tg * 0.001, w, z0, z1)


def _glyph_parts(strokes, w, z0, z1, scale, offset):
    """One object per stroke (for the boolean union)."""
    objs = []
    for s in strokes:
        bm = bmesh.new()
        if isinstance(s, tuple) and s[0] == "arc":
            _, cx, cy, r, a0, a1 = s
            _bm_arc(bm, cx, cy, r, a0, a1, w, z0, z1)
        elif isinstance(s, tuple) and s[0] == "dot":
            _, cx, cy, r = s
            ring = [Vector((cx + r * math.cos(2 * math.pi * i / 10), cy + r * math.sin(2 * math.pi * i / 10), 0)) for i in range(10)]
            _bm_prism(bm, ring, z0, z1)
        else:
            closed = len(s) > 2 and abs(s[0][0] - s[-1][0]) < 1e-6 and abs(s[0][1] - s[-1][1]) < 1e-6
            for a, b in zip(s, s[1:]):
                one = bmesh.new()
                _bm_box(one, a, b, w, z0, z1, ext=False)
                me = bpy.data.meshes.new("stroke")
                one.to_mesh(me); one.free()
                objs.append(bpy.data.objects.new("stroke", me))
            # joints: an octagon at every bend (and the start of a closed loop) so corners are clean, no spikes
            joints = list(s[1:-1]) + ([s[0]] if closed else [])
            r = (w / 2) / math.cos(math.pi / 8)
            for (jx, jy) in joints:
                one = bmesh.new()
                ring = [Vector((jx + r * math.cos(math.pi / 8 + k * math.pi / 4),
                                jy + r * math.sin(math.pi / 8 + k * math.pi / 4), 0)) for k in range(8)]
                _bm_prism(one, ring, z0, z1)
                me = bpy.data.meshes.new("joint")
                one.to_mesh(me); one.free()
                objs.append(bpy.data.objects.new("joint", me))
            bm.free()
            continue
        me = bpy.data.meshes.new("stroke")
        bm.to_mesh(me); bm.free()
        objs.append(bpy.data.objects.new("stroke", me))
    for o in objs:
        for v in o.data.vertices:
            v.co.x = (v.co.x + offset[0]) * scale
            v.co.y = (v.co.y + offset[1]) * scale
    return objs


def _union(objs, name, bevel):
    sc = bpy.context.scene
    tmp = bpy.data.collections.new("_tmp_union")
    sc.collection.children.link(tmp)
    base = objs[0]
    for o in objs:
        tmp.objects.link(o)
    rest = bpy.data.collections.new("_tmp_rest")
    sc.collection.children.link(rest)
    for o in objs[1:]:
        tmp.objects.unlink(o)
        rest.objects.link(o)
    if objs[1:]:
        md = base.modifiers.new("u", 'BOOLEAN')
        md.operation = 'UNION'
        md.operand_type = 'COLLECTION'
        md.collection = rest
        md.solver = 'EXACT'
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(base.evaluated_get(dg))
    o2 = bpy.data.objects.new(name + "_u", me)
    tmp.objects.link(o2)
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(1), verts=bm.verts, edges=bm.edges)
    bm.to_mesh(me); bm.free()
    if bevel > 0:
        bv = o2.modifiers.new("b", 'BEVEL')
        bv.width = bevel
        bv.segments = 1
        bv.limit_method = 'ANGLE'
        bv.angle_limit = math.radians(40)
        bv.harden_normals = False
        dg = bpy.context.evaluated_depsgraph_get()
        me2 = bpy.data.meshes.new_from_object(o2.evaluated_get(dg))
    else:
        me2 = me.copy()
    for o in list(tmp.objects) + list(rest.objects):
        bpy.data.objects.remove(o)
    bpy.data.collections.remove(tmp); bpy.data.collections.remove(rest)
    tri = bmesh.new(); tri.from_mesh(me2)
    bmesh.ops.triangulate(tri, faces=tri.faces)
    tri.free()
    me2.name = name
    return me2


def glyph_centre(strokes):
    xs, ys = [], []
    for s in strokes:
        if isinstance(s, tuple) and s[0] == "arc":
            _, cx, cy, r, a0, a1 = s
            n = 24
            for i in range(n + 1):
                t = math.radians(a0 + (a1 - a0) * i / n)
                xs.append(cx + r * math.cos(t)); ys.append(cy + r * math.sin(t))
        elif isinstance(s, tuple) and s[0] == "dot":
            xs.append(s[1]); ys.append(s[2])
        else:
            xs += [p[0] for p in s]; ys += [p[1] for p in s]
    return (-(min(xs) + max(xs)) / 2, -(min(ys) + max(ys)) / 2), max(max(ys) - min(ys), max(xs) - min(xs) * 0)


def build_rune(name, strokes, look, glow="glow_gold"):
    """Returns a new object '<name>_<look>' (not linked)."""
    off, _ = glyph_centre(strokes)
    s = SIZE
    if look == "Inlay":
        me = _union(_glyph_parts(strokes, W_STROKE, -0.01, 0.02, s, off), "Rune_%s_Inlay" % name, 0.0)
        parts = [(me, glow)]
    else:
        rim = _union(_glyph_parts(strokes, W_STROKE + 0.09, -0.01, 0.006, s, off), "rim", 0.0)
        core = _union(_glyph_parts(strokes, W_STROKE * 0.5, -0.01, 0.011, s, off), "core", 0.0)
        parts = [(rim, "groove"), (core, glow)]
    bm = bmesh.new()
    swatch = {}
    for me, sw in parts:
        start = len(bm.faces)
        bm.from_mesh(me)
        bm.faces.ensure_lookup_table()
        for f in bm.faces[start:]:
            swatch[f.index] = sw
    me = bpy.data.meshes.new("Rune_%s_%s" % (name, look))
    bm.to_mesh(me); bm.free()
    for m, _ in parts:
        bpy.data.meshes.remove(m)
    o = bpy.data.objects.new("Rune_%s_%s" % (name, look), me)
    o["rune"] = name
    o["look"] = look
    me.name = o.name
    palette_uv(o, lambda f, pid: (swatch[f.index], 0.5), lambda co: 0.0,
               v_override=lambda f, pid: 0.45 if swatch[f.index] != "groove" else 0.5)
    return o


def tri_count(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


if __name__ == "__main__":
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    pal_dir = os.path.join(REPO, "Exports", "Remodel", "Palette")
    paths = make_palette_images(pal_dir)
    mat = palette_material(paths, glow_strength=1.0)
    os.makedirs(OUT, exist_ok=True)
    report = []
    for look, y0 in (("Inlay", 0.0), ("Carved", -6.0)):
        coll = bpy.data.collections.new("Runes " + look)
        sc.collection.children.link(coll)
        allg = list(FUTHARK.items()) + list(ALCHEMY.items())
        for i, (n, strokes) in enumerate(allg):
            o = build_rune(n, strokes, look)
            o.data.materials.append(mat)
            coll.objects.link(o)
            row, colx = divmod(i, 9)
            o.location = (colx * 1.0, y0 - row * 1.0, 0)
            report.append((o.name, tri_count(o)))
    # a stone slab behind each block of runes, for the sheet
    slab_bm = bmesh.new()
    bmesh.ops.create_cube(slab_bm, size=1.0)
    slab = bpy.data.meshes.new("slab")
    slab_bm.to_mesh(slab); slab_bm.free()
    for y in (-1.5, -7.5):
        s = bpy.data.objects.new("_sheet_slab", slab)
        s.scale = (9.6, 4.6, 0.1); s.location = (4.0, y, -0.05)
        sc.collection.objects.link(s)
        sm = bpy.data.materials.get("_slab") or bpy.data.materials.new("_slab")
        sm.diffuse_color = (0.09, 0.08, 0.11, 1)
        sm.use_nodes = True
        nb = next(n for n in sm.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        nb.inputs["Base Color"].default_value = (0.05, 0.045, 0.065, 1)
        if not s.data.materials:
            s.data.materials.append(sm)
    # render (EEVEE, dark, glow visible)
    try:
        sc.render.engine = 'BLENDER_EEVEE'
    except TypeError as e:
        print(e)
    w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == 'BACKGROUND')
    bg.inputs[0].default_value = (0.05, 0.05, 0.09, 1); bg.inputs[1].default_value = 1.0
    sun = bpy.data.objects.new("moon", bpy.data.lights.new("moon", 'SUN')); sc.collection.objects.link(sun)
    sun.data.energy = 1.5; sun.data.color = (0.75, 0.8, 1.0); sun.rotation_euler = (math.radians(40), 0, math.radians(-30))
    sc.view_settings.view_transform = 'Standard'
    sc.render.resolution_x, sc.render.resolution_y = 1800, 1100
    for name, (loc, tgt, ortho) in {"runes_inlay": ((4, -1.8, 12), (4, -1.5, 0), 10.4),
                                    "runes_carved": ((4, -7.8, 12), (4, -7.5, 0), 10.4)}.items():
        c = bpy.data.objects.new(name, bpy.data.cameras.new(name)); sc.collection.objects.link(c)
        c.location = loc
        c.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        c.data.type = 'ORTHO'; c.data.ortho_scale = ortho
        sc.camera = c
        sc.render.filepath = os.path.join(OUT, name + ".png")
        bpy.ops.render.render(write_still=True)
    # close-up at an angle to see the depth
    c = bpy.data.objects.new("close", bpy.data.cameras.new("close")); sc.collection.objects.link(c)
    c.location = (1.0, -4.6, 1.6)
    c.rotation_euler = (Vector((1.0, -2.0, 0)) - c.location).to_track_quat('-Z', 'Y').to_euler()
    c.data.lens = 40
    sc.camera = c
    sc.render.filepath = os.path.join(OUT, "runes_closeup.png")
    bpy.ops.render.render(write_still=True)
    for o in [o for o in bpy.data.objects if o.name.startswith("_sheet_slab")]:
        bpy.data.objects.remove(o)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(REPO, "Exports", "Remodel", "Rune_Library.blend"), relative_remap=True)
    inl = [t for n, t in report if n.endswith("Inlay")]
    car = [t for n, t in report if n.endswith("Carved")]
    print("TRIS inlay min/avg/max", min(inl), sum(inl) // len(inl), max(inl), "carved", min(car), sum(car) // len(car), max(car))
    print("DONE", len(report), "runes")
