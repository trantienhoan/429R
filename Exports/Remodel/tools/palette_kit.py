"""Palette colouring for the remodel kit (2026-11-17).

One small texture for a whole family: every swatch is a column, dark at the bottom (v = 0) and light at the top
(v = 1), with a small tint shift across its width (left = cooler/darker, right = warmer/lighter) so pieces can differ a
little. A second texture holds the glow (emission) for glowing swatches. Faces are UV'd onto their swatch by script:
  u = swatch column (+ a per-part tint offset inside it), v = from the vertex height (darker low, lighter high)
so nobody unwraps anything by hand.

Use inside Blender:
  exec(open(r".../palette_kit.py", encoding="utf-8").read())
  paths = make_palette_images(folder)
  mat = palette_material(paths)
  palette_uv(obj, swatch_of_face, height_of_vertex)
"""
import bpy, bmesh, os, math
import numpy as np

# name: (dark hex, light hex, glows)
SWATCHES = [
    ("stone",        "4a4458", "a8a0b8", False),
    ("stone_light",  "565068", "bab2c8", False),
    ("stone_dark",   "38333f", "7a7286", False),
    ("wood",         "4a2c1a", "9c6a40", False),
    ("iron",         "262124", "5a4a46", False),
    ("gold",         "8a5a12", "f0c25a", False),
    ("pumpkin",      "a03c0a", "ff9a36", False),
    ("leaf",         "a0600e", "ffc444", False),
    ("mist",         "2e1450", "8a50d0", True),
    ("glow_cream",   "d8c49a", "fff2cc", True),
    ("glow_gold",    "ffb040", "fff0a8", True),
    ("glow_purple",  "9a4cff", "e2b8ff", True),
    ("glow_lantern", "ff9a2a", "ffe7a0", True),
    ("groove",       "17131d", "2a2433", False),
    ("moss",         "3a4a26", "7c9448", False),
    ("white",        "b8b0c0", "f4f0f8", False),
]
COLS = len(SWATCHES)
W, H = 32 * COLS, 256
INDEX = {s[0]: i for i, s in enumerate(SWATCHES)}


def _hex(h):
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)])


def make_palette_images(folder, name="Rooftop_Palette"):
    """Writes <name>.png (colour) and <name>_Emission.png (glow) into folder; returns both paths."""
    os.makedirs(folder, exist_ok=True)
    col = np.zeros((H, W, 4)); col[..., 3] = 1
    emi = np.zeros((H, W, 4)); emi[..., 3] = 1
    v = np.linspace(0, 1, H)[:, None]
    v = v * v * (3 - 2 * v) * 0.85 + v * 0.15                    # soft S curve
    for i, (n, dk, lt, glow) in enumerate(SWATCHES):
        a, b = _hex(dk), _hex(lt)
        x0 = i * 32
        t = (np.arange(32) + 0.5) / 32.0                          # across the column
        shift = (t - 0.5)[None, :, None]                          # -0.5 .. 0.5
        g = a[None, None, :] + (b - a)[None, None, :] * v[:, :, None]
        warm = np.array([1.06, 1.0, 0.92])                        # right side: warmer and a touch lighter
        g = g * (1 + 0.10 * shift) * (1 + (warm - 1) * shift * 2)
        col[:, x0:x0 + 32, :3] = np.clip(g, 0, 1)
        if glow:
            emi[:, x0:x0 + 32, :3] = np.clip(g, 0, 1)
    paths = []
    for arr, suffix in ((col, ""), (emi, "_Emission")):
        p = os.path.join(folder, name + suffix + ".png")
        img = bpy.data.images.new(name + suffix, W, H, alpha=False)
        img.pixels.foreach_set(arr.astype(np.float32).ravel())
        img.filepath_raw = p
        img.file_format = 'PNG'
        img.save()
        bpy.data.images.remove(img)
        paths.append(p)
    return paths


def palette_material(paths, name="Rooftop_Palette", glow_strength=1.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    tc = nt.nodes.new("ShaderNodeTexImage")
    te = nt.nodes.new("ShaderNodeTexImage")
    tc.image = bpy.data.images.load(paths[0], check_existing=True)
    te.image = bpy.data.images.load(paths[1], check_existing=True)
    tc.interpolation = te.interpolation = 'Linear'
    tc.location, te.location, bsdf.location, out.location = (-500, 200), (-500, -150), (-150, 100), (250, 100)
    nt.links.new(tc.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(te.outputs["Color"], bsdf.inputs["Emission Color"])
    bsdf.inputs["Emission Strength"].default_value = glow_strength
    bsdf.inputs["Roughness"].default_value = 0.85
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    nt.nodes.active = tc
    return m


def loose_parts(bm):
    """Face index -> loose part id (0 = the biggest part)."""
    part = {}
    comps = []
    for f in bm.faces:
        if f.index in part:
            continue
        cid = len(comps)
        stack = [f]
        part[f.index] = cid
        n = 0
        while stack:
            a = stack.pop()
            n += 1
            for e in a.edges:
                for b in e.link_faces:
                    if b.index not in part:
                        part[b.index] = cid
                        stack.append(b)
        comps.append(n)
    order = sorted(range(len(comps)), key=lambda c: -comps[c])
    rank = {c: r for r, c in enumerate(order)}
    return {fi: rank[c] for fi, c in part.items()}


def _hash(*a):
    h = 0
    for x in a:
        h = (h * 1103515245 + int(x) * 2654435761 + 12345) & 0x7fffffff
    return (h % 1000) / 1000.0


def palette_uv(obj, swatch_of_face, height_of_vertex, h_range=(-4.0, 7.0), top_bonus=0.10, tint_seed=0,
               v_override=None, mw=None):
    """UV every face of obj onto its swatch.
    swatch_of_face(face, part_id) -> swatch name (or (name, tint 0..1)).
    height_of_vertex(local_co) -> height in metres used for the dark-to-light gradient.
    v_override(face, part_id) -> fixed v (0..1) or None."""
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.faces.ensure_lookup_table()
    uv = bm.loops.layers.uv.verify()
    parts = loose_parts(bm)
    lo, hi = h_range
    for f in bm.faces:
        pid = parts[f.index]
        sw = swatch_of_face(f, pid)
        if isinstance(sw, tuple):
            sw, tint = sw
        else:
            tint = _hash(tint_seed, pid)
        col = INDEX[sw]
        u = (col * 32 + 4 + tint * 24) / W                       # stay 4 px inside the column
        nz = (mw.to_3x3() @ f.normal).normalized().z if mw is not None else f.normal.z
        bonus = top_bonus if nz > 0.7 else 0.0
        fixed = v_override(f, pid) if v_override else None
        for l in f.loops:
            if fixed is not None:
                v = fixed
            else:
                v = (height_of_vertex(l.vert.co) - lo) / (hi - lo) + bonus
            v = min(0.97, max(0.03, v))
            l[uv].uv = (u, v)
    bm.to_mesh(me)
    bm.free()
    me.update()
