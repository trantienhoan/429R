"""Builds the hero jack-o'-lantern for the Steam key art -> assets/Hero_Pumpkin.blend

    "E:/Steam/steamapps/common/Blender/blender.exe" -b --factory-startup --python Exports/Steam_Art/assets/pumpkin_build.py

Everything is procedural (no downloads, no image textures).

FACING: the carved face looks toward LOCAL -Y (Blender "front").  Local +X = the pumpkin's screen-right when seen from the
front, +Z = up.  To face a camera standing at +Y (as in Steam_Art.blend), rotate the root 180 deg about Z
(the old pumpkin_0 used rot Z = -3.545 rad).

Collections:
  Hero_Pumpkin_v2   root empty "Hero_Pumpkin_Root" (bottom centre, at the origin) + body, stem, leaf, tendrils,
                    point light "Hero_Pumpkin_Inner_Light".  ~0.48 m wide, ~0.40 m tall (stem included).
  Small_Pumpkin_v2  root empty "Small_Pumpkin_v2_Root" (bottom centre) + lighter mesh, stem, 1 tendril,
                    point light "Small_Pumpkin_v2_Inner_Light".  ~0.16 m wide.  Instance it (collection instance) 8x.
  Pumpkin_Test_Rig  floor, cameras, test lights (excluded from the view layer; pumpkin_test_render.py switches it on).

Glow: the carved look comes mostly from emission, not the light.  PV2_Pumpkin_Inner and PV2_Pumpkin_Cut_Flesh each
have a Value node named "Glow" (0.55 / 1.0).  Keep the visible glow at about strength 1 under AgX or it washes to
peach.  The inner point light (1.3 W) only throws spill through the holes; raise it for more floor/knight spill.
Materials are shared by both pumpkins; the object property "tex_k" (= 1/size) keeps textures and bumps to scale.

Test renders:  blender -b assets/Hero_Pumpkin.blend --python assets/pumpkin_test_render.py -- tag=x cams=Hero,Front
"""
import bpy, bmesh, math, random, os
from mathutils import Vector, Matrix, noise
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "Hero_Pumpkin.blend")

# ------------------------------------------------------------------ tunables
HERO = dict(
    name="Hero_Pumpkin", seed=429, size=1.0,
    Rx=0.243, Ry=0.233, Rz=0.163, lobes=9, lobe_var=0.22, amp_var=0.045, groove=0.095, groove_var=0.35,
    wiggle=0.035, n_top=2.7, n_bot=2.25, dimple_top=0.050, dimple_sig=0.36, dimple_bot=0.022, dimple_sig_b=0.30,
    segs=360, rings=170, wall=0.025, lump=0.010,
    stem_h=0.130, stem_ns=70, stem_na=96, r_top=0.0215, r_mid=0.026, r_flare=0.040, stem_twist=1.25,
    stem_bend=(0.040, 0.020), stem_lean=(0.010, 0.006),
    tendrils=3, leaf=True, light_w=1.3, light_r=0.035,
)
SMALL = dict(
    name="Small_Pumpkin_v2", seed=77, size=1.0 / 3.0,
    Rx=0.081, Ry=0.078, Rz=0.051, lobes=8, lobe_var=0.18, amp_var=0.04, groove=0.09, groove_var=0.3,
    wiggle=0.03, n_top=2.6, n_bot=2.3, dimple_top=0.017, dimple_sig=0.36, dimple_bot=0.007, dimple_sig_b=0.3,
    segs=144, rings=72, wall=0.0095, lump=0.0025,
    stem_h=0.040, stem_ns=24, stem_na=40, r_top=0.0065, r_mid=0.0078, r_flare=0.011, stem_twist=1.0,
    stem_bend=(-0.012, 0.006), stem_lean=(0.0, 0.0),
    tendrils=1, leaf=False, light_w=0.15, light_r=0.014,
)

# The game's pumpkin_0 face, in degrees: (azimuth from -Y toward +X, elevation from the body centre).
EYE_R = [(9.0, 11.0, 1.0), (39.0, 25.5, 3.5), (39.5, 14.0, 7.0), (30.0, 4.5, 7.0), (19.0, 4.0, 5.0)]   # (a, b, fillet)
NOSE = [(0.0, 7.5, 1.4), (6.2, -3.6, 1.6), (-6.2, -3.6, 1.6)]
MOUTH_W, MOUTH_CORNER_B = 50.0, 4.5
UPPER_TEETH = [(-20.0, -9.5), (9.5, 20.0)]          # hang from the top lip
LOWER_TEETH = [(-33.0, -24.0), (-4.5, 4.5), (24.0, 33.0)]  # rise from the bottom lip
TOOTH_H = 6.5
INNER_EMIT = 0.55    # glow of the scooped inside
CUT_EMIT = 1.0       # glow of the cut walls at their inner edge


def smoothstep(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


def link(obj, coll, parent=None):
    coll.objects.link(obj)
    if parent is not None:
        obj.parent = parent
    return obj


def set_attr(me, name, domain, values, kind="FLOAT"):
    if name in me.attributes:
        me.attributes.remove(me.attributes[name])
    a = me.attributes.new(name, kind, domain)
    if kind == "FLOAT":
        a.data.foreach_set("value", values)
    else:
        a.data.foreach_set("vector", values)


# ================================================================== body
def body_base(P):
    """Returns (verts, faces, groove) of the uncarved, closed body surface."""
    rng = random.Random(P["seed"])
    N = P["lobes"]
    w = [1 + rng.uniform(-P["lobe_var"], P["lobe_var"]) for _ in range(N)]
    w[0] = max(w[0], 1.08)                       # the face lobe is broad
    tot = sum(w)
    b = [0.0]
    for x in w:
        b.append(b[-1] + x / tot * 2 * math.pi)
    off = -(b[0] + b[1]) / 2
    b = [x + off for x in b]
    amps = [1 + rng.uniform(-P["amp_var"], P["amp_var"]) for _ in range(N)]
    amps[0] = 1 + P["amp_var"] * 0.6
    gdep = [1 + rng.uniform(-P["groove_var"], P["groove_var"]) for _ in range(N)]
    zs = [1 + rng.uniform(-0.035, 0.045) for _ in range(N)]
    wig = [rng.uniform(0, 6.28) for _ in range(N)]
    S, R = P["segs"], P["rings"]
    Rx, Ry, Rz = P["Rx"], P["Ry"], P["Rz"]
    zc = Rz
    sv = Vector((rng.uniform(0, 50), rng.uniform(0, 50), rng.uniform(0, 50)))

    def lobe(lam, phi):
        bs = [b[k] + P["wiggle"] * math.sin(2.4 * phi + wig[k % N]) for k in range(N + 1)]
        l = (lam - bs[0]) % (2 * math.pi) + bs[0]
        for k in range(N):
            if l <= bs[k + 1]:
                return k, (l - bs[k]) / (bs[k + 1] - bs[k])
        return N - 1, 1.0

    pts, info = [], []
    def add(phi, lam):
        c, s = math.cos(phi), math.sin(phi)
        n = P["n_top"] if s > 0 else P["n_bot"]
        ex = 2.0 / n
        xr = max(c, 0.0) ** ex
        zr = math.copysign(abs(s) ** ex, s)
        k, t = lobe(lam, phi)
        zr *= zs[k] ** (0.5 + 0.5 * math.sin(math.pi * t)) if s > 0 else 1.0
        p = Vector((Rx * xr * math.sin(lam), -Ry * xr * math.cos(lam), zc + Rz * zr))
        tt, tb = math.pi / 2 - phi, phi + math.pi / 2
        p.z -= P["dimple_top"] * math.exp(-(tt / P["dimple_sig"]) ** 2)
        p.z += P["dimple_bot"] * math.exp(-(tb / P["dimple_sig_b"]) ** 2)
        p.x += 0.025 * (p.z - zc)                # a slight lean
        pts.append(p)
        info.append((phi, lam, k, t))

    add(-math.pi / 2, 0.0)
    for i in range(1, R):
        phi = -math.pi / 2 + math.pi * i / R
        for j in range(S):
            add(phi, 2 * math.pi * j / S)
    add(math.pi / 2, 0.0)
    faces = []
    top = len(pts) - 1
    vid = lambda i, j: 1 + (i - 1) * S + (j % S)
    for j in range(S):
        faces.append((0, vid(1, j + 1), vid(1, j)))
        faces.append((top, vid(R - 1, j), vid(R - 1, j + 1)))
    for i in range(1, R - 1):
        for j in range(S):
            faces.append((vid(i, j), vid(i, j + 1), vid(i + 1, j + 1), vid(i + 1, j)))
    # normals of the smooth base, then grooves / lobes / lumps along them
    me = bpy.data.meshes.new("tmp")
    me.from_pydata([p[:] for p in pts], [], faces)
    me.update()
    nrm = [v.normal.copy() for v in me.vertices]
    bpy.data.meshes.remove(me)
    groove = []
    out = []
    for p, n, (phi, lam, k, t) in zip(pts, nrm, info):
        tt, tb = math.pi / 2 - phi, phi + math.pi / 2
        fade = smoothstep(0.03, 0.30, tt) * smoothstep(0.05, 0.45, tb)
        sn = math.sin(math.pi * t)
        bulge = sn ** 0.55
        valley = (1 - bulge) ** 1.25
        gk = gdep[k] if t < 0.5 else gdep[(k + 1) % N]
        g = valley * fade
        d = -P["groove"] * Rx * gk * g
        d += (amps[k] - 1) * Rx * bulge * fade
        q = p * (1.0 / P["size"]) + sv
        d += P["lump"] * (noise.noise(q * 3.0) + 0.45 * noise.noise(q * 7.5) + 0.2 * noise.noise(q * 17.0))
        out.append(p + n * d)
        groove.append(min(1.0, g * gk))
    zmin = min(p.z for p in out)
    out = [p - Vector((0, 0, zmin)) for p in out]
    return out, faces, groove


def inner_shell(P, verts, faces):
    """Inner wall = the skin offset inward by `wall` along lightly smoothed vertex normals (it keeps the ribs, so
    the wall stays even), then kept a wall below the skin in the stem cavity / above it at the blossom end
    (vertical rays), with only those clamped spots relaxed.  Solidify folded through the skin in the cavity."""
    wall = P["wall"]
    nv = len(verts)
    nbs = [set() for _ in range(nv)]
    for f in faces:
        for a in range(len(f)):
            i, j = f[a], f[(a + 1) % len(f)]
            nbs[i].add(j)
            nbs[j].add(i)
    nbs = [list(n) for n in nbs]
    me = bpy.data.meshes.new("tmp_n")
    me.from_pydata([v[:] for v in verts], [], faces)
    me.update()
    nrm = [v.normal.copy() for v in me.vertices]
    bpy.data.meshes.remove(me)
    for it in range(3):
        nrm = [(nrm[i] * 2 + sum((nrm[k] for k in nbs[i]), Vector())).normalized() for i in range(nv)]
    pts = [verts[i] - nrm[i] * wall for i in range(nv)]
    bvh = BVHTree.FromPolygons([v[:] for v in verts], faces)
    def clamp(i):
        q = pts[i]
        hit = bvh.ray_cast(Vector((q.x, q.y, 2.0)), Vector((0, 0, -1)))
        if hit[0] is not None and q.z > hit[0].z - wall:
            pts[i] = Vector((q.x, q.y, hit[0].z - wall))
            return True
        hit = bvh.ray_cast(Vector((q.x, q.y, -2.0)), Vector((0, 0, 1)))
        if hit[0] is not None and q.z < hit[0].z + wall:
            pts[i] = Vector((q.x, q.y, hit[0].z + wall))
            return True
        return False
    zs = sorted(v.z for v in verts)
    zlo, zhi = zs[0], zs[-1]
    cand = [i for i in range(nv) if verts[i].z > zhi - 0.45 * (zhi - zlo) or verts[i].z < zlo + 0.25 * (zhi - zlo)]
    hot = set(i for i in cand if clamp(i))
    for ring in range(3):                              # relax a margin around the clamped area too
        hot |= set(k for i in list(hot) for k in nbs[i])
    hot = list(hot)
    for it in range(8):
        new = {i: pts[i] * 0.5 + sum((pts[k] for k in nbs[i]), Vector()) * (0.5 / len(nbs[i])) for i in hot}
        for i, q in new.items():
            pts[i] = q
        for i in hot:
            clamp(i)
    return pts


def fillet(poly, seg=6):
    pts = [Vector((a, b)) for a, b, r in poly]
    rad = [r for a, b, r in poly]
    n = len(pts)
    out = []
    for i in range(n):
        p0, p1, p2 = pts[i - 1], pts[i], pts[(i + 1) % n]
        d1, d2 = p0 - p1, p2 - p1
        a, c = min(rad[i], 0.45 * d1.length), min(rad[i], 0.45 * d2.length)
        q0, q2 = p1 + d1.normalized() * a, p1 + d2.normalized() * c
        for k in range(seg + 1):
            t = k / seg
            out.append((1 - t) ** 2 * q0 + 2 * (1 - t) * t * p1 + t * t * q2)
    dense = []
    for i in range(len(out)):
        a, c = out[i], out[(i + 1) % len(out)]
        m = max(1, int((c - a).length / 1.2))
        for k in range(m):
            dense.append(a.lerp(c, k / m))
    return dense


def mouth_outline():
    W = MOUTH_W
    top = lambda a: MOUTH_CORNER_B - 16.0 * max(0.0, 1 - (a / W) ** 2) ** 1.1
    bot = lambda a: MOUTH_CORNER_B - 34.0 * max(0.0, 1 - (a / W) ** 2) ** 0.75
    poly = [(-W, MOUTH_CORNER_B + 1.5, 1.0)]
    a = -W + 0.001
    xs = sorted(set([round(-W + i * 2.0, 3) for i in range(1, int(2 * W / 2.0))] +
                    [x for t in UPPER_TEETH for x in t]))
    # top lip, left -> right, teeth hang down
    for x in xs:
        inside = next((t for t in UPPER_TEETH if t[0] <= x <= t[1]), None)
        if inside and x == inside[0]:
            poly.append((x, top(x), 1.2))
            poly.append((x + 0.6, top(x) - TOOTH_H, 1.4))
        elif inside and x == inside[1]:
            poly.append((x - 0.6, top(x) - TOOTH_H, 1.4))
            poly.append((x, top(x), 1.2))
        elif inside:
            continue
        else:
            poly.append((x, top(x), 0.5))
    poly.append((W, MOUTH_CORNER_B + 1.5, 1.0))
    xs2 = sorted(set([round(-W + i * 2.0, 3) for i in range(1, int(2 * W / 2.0))] +
                     [x for t in LOWER_TEETH for x in t]), reverse=True)
    for x in xs2:
        inside = next((t for t in LOWER_TEETH if t[0] <= x <= t[1]), None)
        if inside and x == inside[1]:
            poly.append((x, bot(x), 1.2))
            poly.append((x - 0.6, bot(x) + TOOTH_H, 1.4))
        elif inside and x == inside[0]:
            poly.append((x + 0.6, bot(x) + TOOTH_H, 1.4))
            poly.append((x, bot(x), 1.2))
        elif inside:
            continue
        else:
            poly.append((x, bot(x), 0.5))
    return poly


def face_cutter(name, C, r_out, r_in, mat):
    def d(a, b):
        a, b = math.radians(a), math.radians(b)
        return Vector((math.sin(a) * math.cos(b), -math.cos(a) * math.cos(b), math.sin(b)))
    eye_l = [(-a, b, r) for a, b, r in reversed(EYE_R)]
    shapes = [EYE_R, eye_l, NOSE, mouth_outline()]
    bm = bmesh.new()
    for sh in shapes:
        pts = fillet(sh)
        fr = [bm.verts.new(C + d(p.x, p.y) * r_out) for p in pts]
        bk = [bm.verts.new(C + d(p.x, p.y) * r_in) for p in pts]
        n = len(pts)
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((fr[i], fr[j], bk[j], bk[i]))
        bm.faces.new(fr)
        bm.faces.new(bk)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat)
    return bpy.data.objects.new(name, me)


def build_body(P, coll, root, mats):
    verts, faces, groove = body_base(P)
    inner = inner_shell(P, verts, faces)
    nv = len(verts)
    me = bpy.data.meshes.new(P["name"] + "_Body")
    me.from_pydata([v[:] for v in verts + inner], [], faces + [tuple(i + nv for i in reversed(f)) for f in faces])
    me.update()
    nf = len(faces)
    for k, poly in enumerate(me.polygons):
        poly.use_smooth = True
        poly.material_index = 0 if k < nf else 1
    me.materials.append(mats["skin"])
    me.materials.append(mats["inner"])
    set_attr(me, "groove", "POINT", groove + groove)
    ob = link(bpy.data.objects.new(P["name"] + "_Body", me), coll, root)
    # BVH of the outer skin (for depth + groove lookups after the carve)
    bvh = BVHTree.FromPolygons([v[:] for v in verts], faces)
    zc = P["Rz"] + (max(v.z for v in verts) - 2 * P["Rz"]) * 0.0
    zmid = (max(v.z for v in verts) + min(v.z for v in verts)) * 0.5
    C = Vector((0, 0.0, zmid + 0.02 * P["size"]))
    cut = face_cutter(P["name"] + "_Face_Cutter", C, P["Rx"] * 2.5, P["Rx"] * 0.45, mats["cut"])
    link(cut, coll, root)
    bo = ob.modifiers.new("Carve", "BOOLEAN")
    bo.operation = "DIFFERENCE"
    bo.object = cut
    bo.material_mode = "TRANSFER"
    bo.solver = "MANIFOLD"
    dg = bpy.context.evaluated_depsgraph_get()
    me2 = bpy.data.meshes.new_from_object(ob.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    ncut = sum(1 for f in me2.polygons if f.material_index == 2)
    print("CARVE", bo.solver, len(me2.polygons), "faces,", ncut, "cut faces")
    if ncut == 0 or len(me2.polygons) < len(me.polygons) * 0.9:     # manifold failed -> exact
        bpy.data.meshes.remove(me2)
        bo.solver = "EXACT"
        dg = bpy.context.evaluated_depsgraph_get()
        me2 = bpy.data.meshes.new_from_object(ob.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    ob.modifiers.clear()
    me2.name = P["name"] + "_Body"
    ob.data = me2
    bpy.data.meshes.remove(me)
    bpy.data.objects.remove(cut)
    me = me2
    # material slots: make sure skin / inner / cut are 0 / 1 / 2
    names = [m.name if m else "" for m in me.materials]
    print("BODY materials", names, "faces", len(me.polygons))
    # depth through the wall (0 = skin, 1 = inside) and groove, looked up from the skin
    depth, grv = [], []
    for v in me.vertices:
        loc, nrm, fi, dist = bvh.find_nearest(v.co)
        depth.append(min(1.0, dist / P["wall"]))
        f = faces[fi]
        grv.append(sum(groove[i] for i in f) / len(f))
    set_attr(me, "cut_depth", "POINT", depth)
    set_attr(me, "groove", "POINT", grv)
    # sharp edges where materials meet (rim of every hole)
    bm = bmesh.new()
    bm.from_mesh(me)
    for f in bm.faces:
        f.smooth = True
    for e in bm.edges:
        if len(e.link_faces) == 2 and e.link_faces[0].material_index != e.link_faces[1].material_index:
            e.smooth = False
    bm.to_mesh(me)
    bm.free()
    ob["tex_k"] = 1.0 / P["size"]
    return ob, bvh, verts, faces


# ================================================================== stem
def build_stem(P, coll, root, mats, body_verts, bvh):
    rng = random.Random(P["seed"] + 11)
    s_ = P["size"]
    top_pole = body_verts[-1]
    base = Vector((top_pole.x, top_pole.y, top_pole.z - 0.014 * s_))
    H, ns, na = P["stem_h"], P["stem_ns"], P["stem_na"]
    bx, by = P["stem_bend"]
    lx, ly = P["stem_lean"]
    sv = Vector((rng.uniform(0, 40), rng.uniform(0, 40), 0))

    def spine(s):
        return base + Vector((lx * s + bx * s ** 2.4, ly * s + by * s ** 2.4, H * s - 0.15 * H * s ** 3))

    frames = []
    nprev = Vector((1, 0, 0))
    for i in range(ns + 1):
        s = i / ns
        t = (spine(min(1, s + 0.01)) - spine(max(0, s - 0.01))).normalized()
        n = (nprev - t * nprev.dot(t)).normalized()
        frames.append((spine(s), t, n, t.cross(n)))
        nprev = n
    verts, stem_t, sdir, sridge = [], [], [], []
    for i, (c, t, n, bvec) in enumerate(frames):
        s = i / ns
        r = P["r_top"] + (P["r_mid"] - P["r_top"]) * (1 - s) ** 1.5 + P["r_flare"] * math.exp(-s / 0.11)
        r *= 1 + 0.10 * smoothstep(0.86, 1.0, s)                 # knobby broken end
        ra = 0.30 * math.exp(-s / 0.18) + 0.13
        tw = P["stem_twist"] * s
        for j in range(na):
            th = 2 * math.pi * j / na
            ph = th + tw
            ridge = abs(math.cos(5 * ph + 0.35 * math.sin(3 * ph))) ** 0.55
            q = Vector((math.cos(ph) * 3, math.sin(ph) * 3, s * 4)) + sv
            rr = r * (1 + ra * (ridge - 0.62) + 0.06 * noise.noise(q) + 0.03 * noise.noise(q * 4.0))
            if i == ns:
                rr *= 1 + 0.05 * noise.noise(q * 3.0)
            p = c + (n * math.cos(th) + bvec * math.sin(th)) * rr
            if i == ns:
                p += t * (0.006 * s_ * noise.noise(Vector((math.cos(th) * 2.5, math.sin(th) * 2.5, 7.0)) + sv))
            if i == 0:
                p -= t * 0.004 * s_
            if s < 0.3:                                   # the foot spreads over the skin of the cavity
                hit = bvh.ray_cast(Vector((p.x, p.y, p.z + 0.2)), Vector((0, 0, -1)))
                if hit[0] is not None and p.z < hit[0].z + 0.0012 * s_ and i > 0:
                    p.z = hit[0].z + 0.0012 * s_
            verts.append(p)
            sridge.append(ridge)
            stem_t.append(s)
            sdir.append((math.cos(ph), math.sin(ph), s))
    faces, cap = [], []
    for i in range(ns):
        for j in range(na):
            a, b = i * na + j, i * na + (j + 1) % na
            faces.append((a, b, b + na, a + na))
            cap.append(0.0)
    # dry, cracked, slightly sunken end
    c, t, n, bvec = frames[-1]
    last = ns * na
    rings = 4
    prev = list(range(last, last + na))
    for k in range(1, rings + 1):
        f = 1 - k / (rings + 0.6)
        cur = []
        for j in range(na):
            p0 = verts[last + j]
            q = Vector((math.cos(2 * math.pi * j / na) * 4, math.sin(2 * math.pi * j / na) * 4, k * 1.7)) + sv
            p = c + (p0 - c) * f + t * (-0.003 * s_ * math.sin(math.pi * k / (rings + 1)) + 0.0015 * s_ * noise.noise(q))
            if k == 1:
                p += t * 0.0012 * s_
            verts.append(p)
            stem_t.append(1.0)
            sridge.append(0.5)
            sdir.append(sdir[last + j])
            cur.append(len(verts) - 1)
        for j in range(na):
            faces.append((prev[j], prev[(j + 1) % na], cur[(j + 1) % na], cur[j]))
            cap.append(1.0)
        prev = cur
    verts.append(c - t * 0.0025 * s_)
    stem_t.append(1.0)
    sridge.append(0.5)
    sdir.append((0, 0, 1))
    ci = len(verts) - 1
    for j in range(na):
        faces.append((prev[j], prev[(j + 1) % na], ci))
        cap.append(1.0)
    # bottom (hidden inside the body): close it
    verts.append(frames[0][0] - frames[0][1] * 0.01 * s_)
    stem_t.append(0.0)
    sridge.append(0.5)
    sdir.append((0, 0, 0))
    bi = len(verts) - 1
    for j in range(na):
        faces.append(((j + 1) % na, j, bi))
        cap.append(0.0)
    me = bpy.data.meshes.new(P["name"] + "_Stem")
    me.from_pydata([v[:] for v in verts], [], faces)
    me.update()
    for poly in me.polygons:
        poly.use_smooth = True
    set_attr(me, "stem_t", "POINT", stem_t)
    set_attr(me, "stem_ridge", "POINT", sridge)
    set_attr(me, "stem_cap", "FACE", cap)
    set_attr(me, "stem_dir", "POINT", [x for d in sdir for x in d], "FLOAT_VECTOR")
    # sharp rim at the broken end
    bm = bmesh.new()
    bm.from_mesh(me)
    for e in bm.edges:
        if all(last <= v.index < last + na for v in e.verts):
            e.smooth = False
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mats["stem"])
    ob = link(bpy.data.objects.new(P["name"] + "_Stem", me), coll, root)
    ob["tex_k"] = 1.0 / s_
    return ob, frames


# ================================================================== tendrils / leaf
def curve_object(name, pts, radius, taper, coll, root, mat, tex_k):
    cu = bpy.data.curves.new(name, "CURVE")
    cu.dimensions = "3D"
    cu.bevel_depth = radius
    cu.bevel_resolution = 3
    cu.use_fill_caps = True
    cu.twist_mode = "MINIMUM"
    sp = cu.splines.new("NURBS")
    sp.points.add(len(pts) - 1)
    for i, p in enumerate(pts):
        sp.points[i].co = (p.x, p.y, p.z, 1.0)
        sp.points[i].radius = taper(i / (len(pts) - 1))
    sp.order_u = 4
    sp.use_endpoint_u = True
    sp.resolution_u = 3
    tmp = bpy.data.objects.new(name + "_crv", cu)
    coll.objects.link(tmp)
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg))
    me.name = name
    bpy.data.objects.remove(tmp)
    bpy.data.curves.remove(cu)
    me.materials.clear()
    me.materials.append(mat)
    for poly in me.polygons:
        poly.use_smooth = True
    ob = link(bpy.data.objects.new(name, me), coll, root)
    ob["tex_k"] = tex_k
    return ob


def snap_out(p, bvh, off):
    loc, nrm, fi, dist = bvh.find_nearest(p)
    if loc is None:
        return p
    if (p - loc).dot(nrm) < off:
        return loc + nrm * off
    return p


def tendril_points(start, tdir, bvh, s_, rng, run_len, k0, k1, tors, turns_len):
    """Runs over the skin for run_len, then lifts off and coils (curvature k0 -> k1, torsion tors)."""
    pts = [start.copy()]
    p = start.copy()
    t = tdir.normalized()
    ds = 0.0035 * s_
    n = int(run_len / ds)
    for i in range(n):
        p = p + t * ds
        p = snap_out(p, bvh, 0.003 * s_)
        t = (p - pts[-1]).normalized()
        t = (t + Vector((rng.uniform(-.15, .15), rng.uniform(-.15, .15), 0))).normalized()
        pts.append(p.copy())
    # lift and coil
    up = Vector((0, 0, 1))
    bvec = t.cross(up).normalized()
    t = (t + up * 0.22).normalized()
    L = turns_len
    m = int(L / (0.0018 * s_))
    for i in range(m):
        u = i / m
        k = (k0 + (k1 - k0) * u ** 1.6) / s_
        th = k * 0.0018 * s_
        t = (Matrix.Rotation(th, 3, bvec) @ t).normalized()
        bvec = (Matrix.Rotation(tors * 0.0018 * s_ / s_, 3, t) @ bvec).normalized()
        p = p + t * 0.0018 * s_
        p = snap_out(p, bvh, 0.004 * s_)
        pts.append(p.copy())
    return pts[::2]


def build_leaf(P, coll, root, mats, bvh, stem_frames):
    rng = random.Random(P["seed"] + 23)
    s_ = P["size"]
    L = 0.120 * s_
    nr, na = 30, 200
    lobes = [(-1.95, 0.70), (-1.0, 0.92), (0.0, 1.0), (1.0, 0.9), (1.95, 0.68)]

    def outline(th):
        v = 0.0
        for c, a in lobes:
            v = max(v, a * math.exp(-((th - c) / 0.42) ** 2))
        r = 0.42 + 0.58 * v
        r *= 1 - 0.035 * abs(math.sin(th * 22)) - 0.02 * abs(math.sin(th * 51 + 1))
        r *= 0.25 + 0.75 * smoothstep(math.pi, 2.55, abs(th))       # notch at the stalk
        return L * r

    veins = []
    for c, a in lobes:
        veins.append((Vector((0, 0)), Vector((math.sin(c), math.cos(c))) * L * a * 0.92, 1.0))
        dirv = Vector((math.sin(c), math.cos(c)))
        side = Vector((dirv.y, -dirv.x))
        for kk in range(1, 5):
            o = dirv * L * a * 0.18 * kk
            for sg in (-1, 1):
                e = o + (dirv * 0.75 + side * sg * 0.85).normalized() * L * 0.20 * (1 - kk * 0.13)
                veins.append((o, e, 0.45))

    def seg_dist(p, a, b):
        ab = b - a
        t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-12)))
        return (p - (a + ab * t)).length, t

    verts, vein, lr = [Vector((0, 0, 0))], [1.0], [0.0]
    for i in range(1, nr + 1):
        for j in range(na):
            th = -math.pi + 2 * math.pi * j / na
            rho = (i / nr) * outline(th)
            p2 = Vector((rho * math.sin(th), rho * math.cos(th)))
            vm = 0.0
            for a, b, wgt in veins:
                d, t = seg_dist(p2, a, b)
                width = (0.0028 if wgt > 0.9 else 0.0016) * s_ * (1.15 - 0.7 * t)
                vm = max(vm, wgt * math.exp(-(d / width) ** 2))
            z = -0.55 * rho ** 2 / L                                   # droop
            z += 0.007 * s_ * math.sin(5 * th + 1.3) * (i / nr) ** 2   # wavy edge
            z += 0.0022 * s_ * noise.noise(Vector((p2.x * 60, p2.y * 60, 3.0)))
            z -= 0.0016 * s_ * vm                                      # veins sunken from above
            z += 0.0011 * s_ * (1 - vm) * (i / nr)                    # puffy between veins
            verts.append(Vector((p2.x, p2.y, z)))
            vein.append(vm)
            lr.append(i / nr)
    faces = []
    for j in range(na):
        faces.append((0, 1 + j, 1 + (j + 1) % na))
    for i in range(1, nr):
        for j in range(na):
            a = 1 + (i - 1) * na + j
            b = 1 + (i - 1) * na + (j + 1) % na
            faces.append((a, a + na, b + na, b))
    # place it on the back-left shoulder (screen-left from the front), drooping outward
    top = stem_frames[0][0]
    ang = math.radians(135)       # direction from the stem: -X, +Y  (back-left)
    out = Vector((math.cos(ang) * -1, math.sin(ang), 0)).normalized()
    out = Vector((-0.93, 0.37, 0)).normalized()
    centre = top + out * 0.09 * s_ + Vector((0, 0, 0.030 * s_))
    rot = Matrix.Rotation(math.atan2(-out.x, out.y), 4, "Z") @ Matrix.Rotation(math.radians(-8), 4, "X")
    placed = [centre + (rot @ v) for v in verts]
    # drape: never below the skin (ray down), then relax the lift so it bends instead of crumpling
    off = 0.0035 * s_
    need = []
    for w in placed:
        hit = bvh.ray_cast(Vector((w.x, w.y, w.z + 0.3)), Vector((0, 0, -1)))
        need.append(max(0.0, hit[0].z + off - w.z) if hit[0] is not None else 0.0)
    def nb(idx):
        if idx == 0:
            return [1 + j for j in range(0, na, 8)]
        i, j = (idx - 1) // na + 1, (idx - 1) % na
        r = [1 + (i - 1) * na + (j + 1) % na, 1 + (i - 1) * na + (j - 1) % na]
        r.append(0 if i == 1 else 1 + (i - 2) * na + j)
        if i < nr:
            r.append(1 + i * na + j)
        return r
    nbs = [nb(i) for i in range(len(placed))]
    dz = list(need)
    for it in range(40):
        dz = [max(need[i], 0.5 * dz[i] + 0.5 * sum(dz[k] for k in nbs[i]) / len(nbs[i])) for i in range(len(dz))]
    placed = [w + Vector((0, 0, d)) for w, d in zip(placed, dz)]
    me = bpy.data.meshes.new(P["name"] + "_Leaf")
    me.from_pydata([v[:] for v in placed], [], faces)
    me.update()
    for poly in me.polygons:
        poly.use_smooth = True
    set_attr(me, "vein", "POINT", vein)
    set_attr(me, "leaf_r", "POINT", lr)
    me.materials.append(mats["leaf"])
    ob = link(bpy.data.objects.new(P["name"] + "_Leaf", me), coll, root)
    ob["tex_k"] = 1.0 / s_
    so = ob.modifiers.new("Thick", "SOLIDIFY")
    so.thickness = 0.0012 * s_
    so.offset = 0.0
    # stalk from the stem foot to the leaf centre
    a = top + Vector((0, 0, 0.012 * s_)) + out * 0.02 * s_
    b = centre + Vector((0, 0, 0.004 * s_))
    pts = []
    for i in range(16):
        u = i / 15
        p = a.lerp(b, u) + Vector((0, 0, 0.018 * s_ * math.sin(math.pi * u)))
        pts.append(snap_out(p, bvh, 0.004 * s_))
    curve_object(P["name"] + "_Leaf_Stalk", pts, 0.0032 * s_, lambda u: 1.0 - 0.45 * u, coll, root, mats["vine"], 1.0 / s_)
    return ob


# ================================================================== materials
def node(nt, kind, x=0, y=0, **inputs):
    n = nt.nodes.new(kind)
    n.location = (x, y)
    for k, v in inputs.items():
        n.inputs[k].default_value = v
    return n


def mix_col(nt, fac, a, b, x=0, y=0, blend="MIX"):
    m = nt.nodes.new("ShaderNodeMix")
    m.data_type = "RGBA"
    m.blend_type = blend
    m.location = (x, y)
    ins = [s for s in m.inputs if s.type == "RGBA"]
    L = nt.links.new
    if isinstance(fac, (int, float)):
        m.inputs[0].default_value = fac
    else:
        L(fac, m.inputs[0])
    for s, v in zip(ins, (a, b)):
        if isinstance(v, tuple):
            s.default_value = v + (1.0,) if len(v) == 3 else v
        else:
            L(v, s)
    return next(s for s in m.outputs if s.type == "RGBA")


def ramp(nt, inp, stops, x=0, y=0):
    r = nt.nodes.new("ShaderNodeValToRGB")
    r.location = (x, y)
    el = r.color_ramp.elements
    while len(el) > len(stops):
        el.remove(el[-1])
    while len(el) < len(stops):
        el.new(0.5)
    for e, (pos, col) in zip(el, stops):
        e.position = pos
        e.color = col + (1.0,) if len(col) == 3 else col
    nt.links.new(inp, r.inputs["Fac"])
    return r.outputs["Color"]


def math_n(nt, op, a, b=None, x=0, y=0, clamp=False):
    m = nt.nodes.new("ShaderNodeMath")
    m.operation = op
    m.use_clamp = clamp
    m.location = (x, y)
    for i, v in enumerate((a, b)):
        if v is None:
            continue
        if isinstance(v, (int, float)):
            m.inputs[i].default_value = v
        else:
            nt.links.new(v, m.inputs[i])
    return m.outputs[0]


def maprange(nt, v, f0, f1, t0=0.0, t1=1.0, clamp=True):
    m = nt.nodes.new("ShaderNodeMapRange")
    m.clamp = clamp
    nt.links.new(v, m.inputs["Value"])
    m.inputs["From Min"].default_value, m.inputs["From Max"].default_value = f0, f1
    m.inputs["To Min"].default_value, m.inputs["To Max"].default_value = t0, t1
    return m.outputs["Result"]


def new_mat(name):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = node(nt, "ShaderNodeOutputMaterial", 1400, 0)
    pr = node(nt, "ShaderNodeBsdfPrincipled", 1100, 0)
    nt.links.new(pr.outputs[0], out.inputs["Surface"])
    return m, nt, pr


def coords(nt):
    tc = node(nt, "ShaderNodeTexCoord", -1600, 0)
    k = nt.nodes.new("ShaderNodeAttribute")
    k.attribute_type = "OBJECT"
    k.attribute_name = "tex_k"
    k.location = (-1600, -250)
    vm = nt.nodes.new("ShaderNodeVectorMath")
    vm.operation = "SCALE"
    vm.location = (-1400, 0)
    nt.links.new(tc.outputs["Object"], vm.inputs[0])
    nt.links.new(k.outputs["Fac"], vm.inputs["Scale"])
    return vm.outputs[0], k.outputs["Fac"]


def attr(nt, name, x=-1400, y=300, out="Fac"):
    a = nt.nodes.new("ShaderNodeAttribute")
    a.attribute_name = name
    a.location = (x, y)
    return a.outputs[out]


def tex_noise(nt, vec, scale, detail=3.0, rough=0.5, distort=0.0, x=0, y=0):
    n = node(nt, "ShaderNodeTexNoise", x, y, Scale=scale, Detail=detail, Roughness=rough, Distortion=distort)
    nt.links.new(vec, n.inputs["Vector"])
    return n


def vor(nt, vec, scale, feature="F1", x=0, y=0, rand=1.0):
    v = nt.nodes.new("ShaderNodeTexVoronoi")
    v.feature = feature
    v.location = (x, y)
    v.inputs["Scale"].default_value = scale
    v.inputs["Randomness"].default_value = rand
    nt.links.new(vec, v.inputs["Vector"])
    return v


def bump_chain(nt, height, dist, strength, bevel_r=None, k=None):
    """k = the object's tex_k (1 / size): bump depth and bevel radius shrink with the pumpkin."""
    b = node(nt, "ShaderNodeBump", 900, -500, Strength=strength, Distance=dist)
    nt.links.new(height, b.inputs["Height"])
    if k is not None:
        nt.links.new(math_n(nt, "DIVIDE", dist, k), b.inputs["Distance"])
    if bevel_r:
        bv = node(nt, "ShaderNodeBevel", 700, -650, Radius=bevel_r)
        bv.samples = 8
        if k is not None:
            nt.links.new(math_n(nt, "DIVIDE", bevel_r, k), bv.inputs["Radius"])
        nt.links.new(bv.outputs["Normal"], b.inputs["Normal"])
    return b.outputs["Normal"]


def scale_vec(nt, vec, sx, sy, sz):
    vm = nt.nodes.new("ShaderNodeVectorMath")
    vm.operation = "MULTIPLY"
    vm.inputs[1].default_value = (sx, sy, sz)
    nt.links.new(vec, vm.inputs[0])
    return vm.outputs[0]


def mat_skin():
    m, nt, pr = new_mat("PV2_Pumpkin_Skin")
    L = nt.links.new
    P, K = coords(nt)
    g = attr(nt, "groove")
    # meridian streaks: noise on (normalised xy, z) -> no seams
    sep = node(nt, "ShaderNodeSeparateXYZ", -1200, 600)
    L(P, sep.inputs[0])
    comb = node(nt, "ShaderNodeCombineXYZ", -1050, 600)
    L(sep.outputs[0], comb.inputs[0])
    L(sep.outputs[1], comb.inputs[1])
    nrm = nt.nodes.new("ShaderNodeVectorMath")
    nrm.operation = "NORMALIZE"
    L(comb.outputs[0], nrm.inputs[0])
    comb2 = node(nt, "ShaderNodeCombineXYZ", -800, 600)
    sep2 = node(nt, "ShaderNodeSeparateXYZ", -950, 700)
    L(nrm.outputs[0], sep2.inputs[0])
    L(math_n(nt, "MULTIPLY", sep2.outputs[0], 16.0), comb2.inputs[0])
    L(math_n(nt, "MULTIPLY", sep2.outputs[1], 16.0), comb2.inputs[1])
    L(math_n(nt, "MULTIPLY", sep.outputs[2], 2.2), comb2.inputs[2])
    streak = tex_noise(nt, comb2.outputs[0], 1.0, 3.0, 0.45, 0.0, -600, 600).outputs["Fac"]
    # radial distance from the stem axis (darker, greener crown)
    rxy = nt.nodes.new("ShaderNodeVectorMath")
    rxy.operation = "LENGTH"
    L(comb.outputs[0], rxy.inputs[0])
    crown = maprange(nt, rxy.outputs["Value"], 0.035, 0.10, 1.0, 0.0)
    crown = math_n(nt, "MULTIPLY", crown, maprange(nt, sep.outputs[2], 0.16, 0.30, 0.0, 1.0))
    big = tex_noise(nt, P, 2.6, 2.0, 0.5, 0.0, -600, 300).outputs["Fac"]
    # colour
    base = ramp(nt, g, [(0.0, (0.86, 0.28, 0.014)), (0.30, (0.70, 0.17, 0.008)), (0.70, (0.36, 0.06, 0.005)),
                        (1.0, (0.14, 0.022, 0.004))], -300, 300)
    warm = mix_col(nt, maprange(nt, big, 0.45, 0.70, 0.0, 0.55), base, (0.95, 0.42, 0.04), -50, 300)
    sk = ramp(nt, streak, [(0.30, (0.86, 0.84, 0.84)), (0.5, (1.0, 1.0, 1.0)), (0.72, (1.10, 1.06, 1.0))], -300, 600)
    col = mix_col(nt, 1.0, warm, sk, 150, 400, "MULTIPLY")
    col = mix_col(nt, math_n(nt, "MULTIPLY", crown, 0.75), col, (0.30, 0.22, 0.05), 300, 400)
    # blemishes (sparse brown spots), pits, scratches
    vb = vor(nt, P, 24.0, "F1", -900, -100)
    spot_r = maprange(nt, vb.outputs["Distance"], 0.06, 0.16, 1.0, 0.0)
    sep_c = node(nt, "ShaderNodeSeparateColor", -700, -200)
    L(vb.outputs["Color"], sep_c.inputs[0])
    spot = math_n(nt, "MULTIPLY", spot_r, math_n(nt, "GREATER_THAN", sep_c.outputs[0], 0.80))
    spotn = tex_noise(nt, P, 90.0, 4.0, 0.6).outputs["Fac"]
    spot = math_n(nt, "MULTIPLY", spot, maprange(nt, spotn, 0.3, 0.6, 0.4, 1.0), clamp=True)
    col = mix_col(nt, math_n(nt, "MULTIPLY", spot, 0.85), col, (0.22, 0.10, 0.03), 450, 400)
    sc_v = scale_vec(nt, P, 26.0, 26.0, 7.0)
    scn = tex_noise(nt, sc_v, 1.0, 2.0, 0.5, 0.4).outputs["Fac"]
    scr = maprange(nt, math_n(nt, "ABSOLUTE", math_n(nt, "SUBTRACT", scn, 0.5)), 0.0, 0.006, 1.0, 0.0)
    patch = maprange(nt, tex_noise(nt, P, 5.0, 2.0).outputs["Fac"], 0.68, 0.74, 0.0, 1.0)
    scr = math_n(nt, "MULTIPLY", scr, patch)
    col = mix_col(nt, math_n(nt, "MULTIPLY", scr, 0.55), col, (0.30, 0.13, 0.04), 600, 400)
    # pale freckles
    vf = vor(nt, P, 70.0, "F1", -900, -900)
    sep_f = node(nt, "ShaderNodeSeparateColor", -700, -900)
    L(vf.outputs["Color"], sep_f.inputs[0])
    frk = math_n(nt, "MULTIPLY", maprange(nt, vf.outputs["Distance"], 0.05, 0.12, 1.0, 0.0),
                 math_n(nt, "GREATER_THAN", sep_f.outputs[0], 0.88))
    col = mix_col(nt, math_n(nt, "MULTIPLY", frk, 0.45), col, (0.95, 0.55, 0.12), 700, 400)
    # creases soak up shadow (painterly depth in the ribs)
    ao = nt.nodes.new("ShaderNodeAmbientOcclusion")
    ao.location = (700, 600)
    ao.samples = 8
    ao.inputs["Distance"].default_value = 0.035
    aoc = ramp(nt, ao.outputs["AO"], [(0.25, (0.45, 0.30, 0.25)), (0.9, (1.0, 1.0, 1.0))], 850, 600)
    col = mix_col(nt, 1.0, col, aoc, 950, 400, "MULTIPLY")
    L(col, pr.inputs["Base Color"])
    # height
    pits = vor(nt, P, 150.0, "F1", -900, -500)
    pitm = maprange(nt, pits.outputs["Distance"], 0.0, 0.5, 0.0, 1.0)
    fine = tex_noise(nt, P, 220.0, 3.0, 0.55).outputs["Fac"]
    mid = tex_noise(nt, P, 30.0, 4.0, 0.55, 0.25).outputs["Fac"]
    h = math_n(nt, "MULTIPLY", pitm, 0.18)
    h = math_n(nt, "ADD", h, math_n(nt, "MULTIPLY", fine, 0.25))
    h = math_n(nt, "ADD", h, math_n(nt, "MULTIPLY", mid, 0.6))
    h = math_n(nt, "SUBTRACT", h, math_n(nt, "MULTIPLY", spot, 0.7))
    h = math_n(nt, "SUBTRACT", h, math_n(nt, "MULTIPLY", scr, 0.35))
    h = math_n(nt, "ADD", h, math_n(nt, "MULTIPLY", streak, 0.25))
    streak2 = tex_noise(nt, scale_vec(nt, comb2.outputs[0], 4.5, 4.5, 1.2), 1.0, 2.0, 0.5).outputs["Fac"]
    h = math_n(nt, "ADD", h, math_n(nt, "MULTIPLY", streak2, 0.3))
    ham = vor(nt, P, 55.0, "SMOOTH_F1", -900, -1200)
    h = math_n(nt, "ADD", h, math_n(nt, "MULTIPLY", ham.outputs["Distance"], 0.16))
    L(bump_chain(nt, h, 0.0012, 0.65, bevel_r=0.0022, k=K), pr.inputs["Normal"])
    rough = math_n(nt, "ADD", maprange(nt, mid, 0.3, 0.7, 0.30, 0.46), math_n(nt, "MULTIPLY", spot, 0.35))
    L(rough, pr.inputs["Roughness"])
    pr.subsurface_method = "BURLEY"     # random walk carried the inner light through the whole wall
    pr.inputs["Subsurface Weight"].default_value = 0.25
    pr.inputs["Subsurface Radius"].default_value = (1.0, 0.38, 0.12)
    pr.inputs["Subsurface Scale"].default_value = 0.003
    pr.inputs["Coat Weight"].default_value = 0.22
    pr.inputs["Coat Roughness"].default_value = 0.32
    pr.inputs["Specular IOR Level"].default_value = 0.55
    return m


def glow_value(nt, v):
    """A Value node named "Glow" so the scene can turn the carved glow up/down without digging into the graph."""
    n = nt.nodes.new("ShaderNodeValue")
    n.name = n.label = "Glow"
    n.outputs[0].default_value = v
    n.location = (600, 300)
    return n.outputs[0]


def mat_inner():
    m, nt, pr = new_mat("PV2_Pumpkin_Inner")
    P, K = coords(nt)
    fib = tex_noise(nt, P, 9.0, 2.0, 0.45, 0.0).outputs["Fac"]
    strands = tex_noise(nt, scale_vec(nt, P, 25, 25, 70), 1.0, 2.0, 0.4, 0.0).outputs["Fac"]
    col = ramp(nt, fib, [(0.3, (0.10, 0.035, 0.005)), (0.7, (0.16, 0.06, 0.008))])   # dark: a bright closed shell would act like an integrating sphere
    nt.links.new(col, pr.inputs["Base Color"])
    pr.inputs["Roughness"].default_value = 0.6
    glow = glow_value(nt, INNER_EMIT)
    nt.links.new(ramp(nt, fib, [(0.3, (1.0, 0.34, 0.0)), (0.75, (1.0, 0.50, 0.02))]), pr.inputs["Emission Color"])
    nt.links.new(math_n(nt, "MULTIPLY", maprange(nt, fib, 0.3, 0.7, 0.6, 1.25), glow), pr.inputs["Emission Strength"])
    nt.links.new(bump_chain(nt, strands, 0.0015, 0.25, k=K), pr.inputs["Normal"])
    return m


def mat_cut():
    m, nt, pr = new_mat("PV2_Pumpkin_Cut_Flesh")
    L = nt.links.new
    P, K = coords(nt)
    d = attr(nt, "cut_depth")
    fib = tex_noise(nt, scale_vec(nt, P, 160, 160, 160), 1.0, 4.0, 0.6, 0.3).outputs["Fac"]
    col = ramp(nt, d, [(0.0, (0.50, 0.13, 0.012)), (0.06, (0.78, 0.32, 0.03)), (0.12, (0.95, 0.64, 0.20)),
                       (0.45, (0.92, 0.50, 0.10)), (1.0, (0.80, 0.36, 0.05))])
    col = mix_col(nt, maprange(nt, fib, 0.35, 0.65, 0.0, 0.25), col, (0.85, 0.50, 0.14))
    L(col, pr.inputs["Base Color"])
    glow = glow_value(nt, CUT_EMIT)
    L(ramp(nt, d, [(0.2, (1.0, 0.30, 0.0)), (0.7, (1.0, 0.45, 0.02)), (1.0, (1.0, 0.56, 0.05))]), pr.inputs["Emission Color"])
    e = maprange(nt, d, 0.15, 1.0, 0.0, 1.0)
    L(math_n(nt, "MULTIPLY", math_n(nt, "POWER", e, 1.3), glow), pr.inputs["Emission Strength"])
    pr.inputs["Roughness"].default_value = 0.42
    pr.subsurface_method = "BURLEY"
    pr.inputs["Subsurface Weight"].default_value = 0.5
    pr.inputs["Subsurface Radius"].default_value = (1.0, 0.6, 0.25)
    pr.inputs["Subsurface Scale"].default_value = 0.0025
    knife = tex_noise(nt, scale_vec(nt, P, 12, 12, 60), 1.0, 2.0, 0.5).outputs["Fac"]
    h = math_n(nt, "ADD", math_n(nt, "MULTIPLY", fib, 0.5), knife)
    L(bump_chain(nt, h, 0.001, 0.35, bevel_r=0.0015, k=K), pr.inputs["Normal"])
    return m


def mat_stem():
    m, nt, pr = new_mat("PV2_Pumpkin_Stem")
    L = nt.links.new
    P, K = coords(nt)
    t = attr(nt, "stem_t", y=500)
    cap = attr(nt, "stem_cap", y=700)
    sd = attr(nt, "stem_dir", y=900, out="Vector")
    fibv = scale_vec(nt, sd, 40.0, 40.0, 3.0)
    fib = tex_noise(nt, fibv, 1.0, 6.0, 0.65, 0.0, -600, 900).outputs["Fac"]
    blot = tex_noise(nt, P, 40.0, 3.0, 0.5).outputs["Fac"]
    rid = attr(nt, "stem_ridge", y=1100)
    col = ramp(nt, t, [(0.0, (0.045, 0.075, 0.018)), (0.3, (0.075, 0.095, 0.028)), (0.65, (0.12, 0.11, 0.045)),
                       (0.97, (0.24, 0.19, 0.10))])
    sk = ramp(nt, fib, [(0.25, (0.62, 0.62, 0.62)), (0.5, (1, 1, 1)), (0.8, (1.3, 1.25, 1.1))])
    col = mix_col(nt, 1.0, col, sk, 0, 0, "MULTIPLY")
    rk = ramp(nt, rid, [(0.0, (0.35, 0.35, 0.35)), (0.55, (0.9, 0.9, 0.9)), (1.0, (1.45, 1.45, 1.3))])
    col = mix_col(nt, 1.0, col, rk, 0, 0, "MULTIPLY")
    col = mix_col(nt, maprange(nt, blot, 0.55, 0.75, 0.0, 0.4), col, (0.08, 0.06, 0.03))
    # broken end: pale dry fibres with dark cracks
    cr = vor(nt, scale_vec(nt, P, 1, 1, 1), 160.0, "DISTANCE_TO_EDGE", -600, -300)
    crack = maprange(nt, cr.outputs["Distance"], 0.0, 0.06, 1.0, 0.0)
    endc = ramp(nt, tex_noise(nt, P, 300.0, 4.0).outputs["Fac"], [(0.3, (0.52, 0.43, 0.28)), (0.7, (0.70, 0.60, 0.42))])
    endc = mix_col(nt, crack, endc, (0.10, 0.07, 0.04))
    col = mix_col(nt, cap, col, endc)
    L(col, pr.inputs["Base Color"])
    h = math_n(nt, "ADD", fib, math_n(nt, "MULTIPLY", cap, math_n(nt, "MULTIPLY", crack, -1.5)))
    L(bump_chain(nt, h, 0.0015, 0.7, bevel_r=0.001, k=K), pr.inputs["Normal"])
    L(maprange(nt, t, 0.0, 1.0, 0.5, 0.85), pr.inputs["Roughness"])
    pr.inputs["Sheen Weight"].default_value = 0.25
    pr.inputs["Sheen Roughness"].default_value = 0.5
    return m


def mat_vine():
    m, nt, pr = new_mat("PV2_Pumpkin_Vine")
    P, K = coords(nt)
    n = tex_noise(nt, P, 120.0, 3.0).outputs["Fac"]
    nt.links.new(ramp(nt, n, [(0.3, (0.13, 0.17, 0.04)), (0.7, (0.24, 0.24, 0.07))]), pr.inputs["Base Color"])
    pr.inputs["Roughness"].default_value = 0.5
    pr.inputs["Subsurface Weight"].default_value = 0.2
    pr.inputs["Subsurface Radius"].default_value = (0.4, 1.0, 0.2)
    pr.inputs["Subsurface Scale"].default_value = 0.002
    nt.links.new(bump_chain(nt, n, 0.0006, 0.4, k=K), pr.inputs["Normal"])
    return m


def mat_leaf():
    m, nt, pr = new_mat("PV2_Pumpkin_Leaf")
    L = nt.links.new
    P, K = coords(nt)
    v = attr(nt, "vein")
    r = attr(nt, "leaf_r", y=500)
    n = tex_noise(nt, P, 60.0, 4.0, 0.6).outputs["Fac"]
    dry = tex_noise(nt, P, 14.0, 3.0, 0.6).outputs["Fac"]
    col = ramp(nt, n, [(0.3, (0.025, 0.065, 0.012)), (0.7, (0.05, 0.10, 0.018))])
    edge = math_n(nt, "MULTIPLY", maprange(nt, r, 0.6, 1.0, 0.0, 1.0), maprange(nt, dry, 0.40, 0.62, 0.2, 1.0), clamp=True)
    col = mix_col(nt, math_n(nt, "MULTIPLY", edge, 0.85), col, (0.30, 0.22, 0.04))
    col = mix_col(nt, math_n(nt, "MULTIPLY", maprange(nt, dry, 0.62, 0.70, 0.0, 1.0), edge), col, (0.16, 0.08, 0.025))
    col = mix_col(nt, math_n(nt, "MULTIPLY", v, 0.6), col, (0.20, 0.24, 0.08))
    L(col, pr.inputs["Base Color"])
    pr.inputs["Roughness"].default_value = 0.62
    pr.inputs["Subsurface Weight"].default_value = 0.3
    pr.inputs["Subsurface Radius"].default_value = (0.3, 1.0, 0.15)
    pr.inputs["Subsurface Scale"].default_value = 0.002
    pr.inputs["Sheen Weight"].default_value = 0.1
    h = math_n(nt, "SUBTRACT", math_n(nt, "MULTIPLY", n, 0.3), math_n(nt, "MULTIPLY", v, 0.5))
    L(bump_chain(nt, h, 0.0008, 0.5, k=K), pr.inputs["Normal"])
    return m


def make_mats():
    return dict(skin=mat_skin(), inner=mat_inner(), cut=mat_cut(), stem=mat_stem(), vine=mat_vine(), leaf=mat_leaf())


# ================================================================== assembly
def build_pumpkin(P, mats, parent_coll):
    coll = bpy.data.collections.new(P["name"] + ("_v2" if P["name"] == "Hero_Pumpkin" else ""))
    parent_coll.children.link(coll)
    root_name = "Hero_Pumpkin_Root" if P["name"] == "Hero_Pumpkin" else P["name"] + "_Root"
    root = bpy.data.objects.new(root_name, None)
    root.empty_display_type = "ARROWS"
    root.empty_display_size = 0.1 * P["size"] * 3 if P["size"] < 1 else 0.15
    root["facing"] = "-Y (local). Rotate 180 deg about Z to face a camera at +Y."
    coll.objects.link(root)
    body, bvh, bverts, bfaces = build_body(P, coll, root, mats)
    stem, frames = build_stem(P, coll, root, mats, bverts, bvh)
    s_ = P["size"]
    rng = random.Random(P["seed"] + 5)
    base = frames[0][0] + Vector((0, 0, 0.012 * s_))
    tex_k = 1.0 / s_
    specs = [  # direction over the skin, run length, k0, k1, torsion, coil length
        (Vector((-0.80, -0.60, 0)), 0.075, 70, 150, 45, 0.17),    # front-left over the shoulder, spring coil
        (Vector((0.85, 0.52, 0)), 0.095, 60, 140, -40, 0.20),     # right-back, coil in the silhouette
        (Vector((0.30, -0.95, 0)), 0.045, 90, 170, 50, 0.10),     # small front curl
    ][:P["tendrils"]]
    for i, (d, run, k0, k1, tor, cl) in enumerate(specs):
        start = snap_out(base + d * 0.035 * s_, bvh, 0.003 * s_)
        pts = tendril_points(start, d, bvh, s_, rng, run * s_, k0, k1, tor, cl * s_)
        curve_object(f"{P['name']}_Tendril_{i + 1}", pts, 0.0030 * s_, lambda u: 1.0 - 0.6 * u ** 0.8, coll, root,
                     mats["vine"], tex_k)
    if P["leaf"]:
        build_leaf(P, coll, root, mats, bvh, frames)
    zmid = (max(v.z for v in bverts) + min(v.z for v in bverts)) * 0.5
    ld = bpy.data.lights.new(root_name.replace("_Root", "") + "_Inner_Light", "POINT")
    ld.energy = P["light_w"]
    ld.color = (1.0, 0.42, 0.07)
    ld.shadow_soft_size = P["light_r"]
    lo = bpy.data.objects.new(root_name.replace("_Root", "") + "_Inner_Light", ld)
    lo.location = (0, 0.01 * s_, zmid - 0.02 * s_)
    lo.visible_camera = False
    lo.visible_glossy = True
    link(lo, coll, root)
    # name the hero light exactly as asked
    if P["name"] == "Hero_Pumpkin":
        lo.name = ld.name = "Hero_Pumpkin_Inner_Light"
    return coll, root


def test_rig(scene_coll):
    coll = bpy.data.collections.new("Pumpkin_Test_Rig")
    scene_coll.children.link(coll)
    # floor: dark worn stone
    bpy.ops.mesh.primitive_plane_add(size=8, location=(0, 0, 0))
    fl = bpy.context.active_object
    fl.name = "Test_Floor"
    for c in fl.users_collection:
        c.objects.unlink(fl)
    coll.objects.link(fl)
    m, nt, pr = new_mat("Test_Floor_Stone")
    tc = node(nt, "ShaderNodeTexCoord", -900, 0)
    v = vor(nt, tc.outputs["Object"], 7.0, "DISTANCE_TO_EDGE")
    n = tex_noise(nt, tc.outputs["Object"], 12.0, 6.0, 0.6)
    col = ramp(nt, n.outputs["Fac"], [(0.3, (0.05, 0.045, 0.045)), (0.7, (0.12, 0.10, 0.09))])
    col = mix_col(nt, maprange(nt, v.outputs["Distance"], 0.0, 0.04, 1.0, 0.0), col, (0.015, 0.012, 0.012))
    nt.links.new(col, pr.inputs["Base Color"])
    pr.inputs["Roughness"].default_value = 0.8
    nt.links.new(bump_chain(nt, maprange(nt, v.outputs["Distance"], 0.0, 0.05), 0.003, 0.6), pr.inputs["Normal"])
    fl.data.materials.append(m)
    # back wall card (dark blue) so rim light reads
    def light(name, kind, loc, target, energy, color, size):
        ld = bpy.data.lights.new(name, kind)
        ld.energy, ld.color = energy, color
        if kind == "AREA":
            ld.size = size
        else:
            ld.shadow_soft_size = size
        o = bpy.data.objects.new(name, ld)
        o.location = loc
        o.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        coll.objects.link(o)
        return o
    light("Test_Moon_Rim", "AREA", (-0.9, 1.1, 0.9), (0, 0, 0.18), 40.0, (0.55, 0.68, 1.0), 0.6)
    light("Test_Moon_Rim_2", "AREA", (0.9, 0.9, 0.7), (0, 0, 0.18), 22.0, (0.55, 0.68, 1.0), 0.5)
    light("Test_Key_Warm", "AREA", (0.55, -1.2, 0.85), (0, 0, 0.2), 10.0, (1.0, 0.72, 0.48), 0.5)
    light("Test_Fill_Cool", "AREA", (-1.0, -0.9, 1.4), (0, 0, 0.2), 6.0, (0.45, 0.55, 1.0), 1.2)
    def cam(name, loc, target, lens):
        cd = bpy.data.cameras.new(name)
        cd.lens = lens
        o = bpy.data.objects.new(name, cd)
        o.location = loc
        o.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        coll.objects.link(o)
        return o
    cam("Test_Cam_Hero", (0.42, -1.25, 0.42), (0.0, 0.0, 0.19), 55)
    cam("Test_Cam_Front", (0.0, -1.3, 0.30), (0.0, 0.0, 0.19), 55)
    cam("Test_Cam_Side", (-1.1, -0.55, 0.55), (0.0, 0.0, 0.19), 55)
    cam("Test_Cam_Top", (0.25, -0.55, 1.05), (0.0, 0.0, 0.25), 50)
    cam("Test_Cam_Stem", (0.30, -0.42, 0.52), (0.0, 0.0, 0.30), 60)
    cam("Test_Cam_Mouth", (0.10, -0.62, 0.20), (0.0, -0.2, 0.10), 70)
    cam("Test_Cam_Small", (0.62, -0.40, 0.16), (0.45, 0.0, 0.055), 55)
    cam("Test_Cam_Far", (0.9, -2.1, 0.45), (0.0, 0.0, 0.2), 50)     # roughly the key-art distance
    return coll


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.unit_settings.system = "METRIC"
    mats = make_mats()
    hero_coll, hero_root = build_pumpkin(HERO, mats, sc.collection)
    small_coll, small_root = build_pumpkin(SMALL, mats, sc.collection)
    small_root.location = (0.45, 0.0, 0.0)      # beside the hero in this file only (bottom centre still its origin)
    small_coll.instance_offset = small_root.location   # a collection instance of it lands on the instancer
    rig = test_rig(sc.collection)
    w = bpy.data.worlds.new("Pumpkin_Test_World")
    w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (0.010, 0.014, 0.030, 1)
    bg.inputs["Strength"].default_value = 1.0
    sc.world = w
    # render defaults for the tests
    try:
        sc.render.engine = "CYCLES"
    except TypeError:
        pass
    sc.cycles.samples = 96
    sc.cycles.use_denoising = True
    sc.cycles.max_bounces = 8
    sc.view_settings.view_transform = "AgX"
    looks = [i.identifier for i in bpy.types.ColorManagedViewSettings.bl_rna.properties["look"].enum_items]
    if "AgX - Punchy" in looks:
        sc.view_settings.look = "AgX - Punchy"
    sc.camera = bpy.data.objects["Test_Cam_Hero"]
    # exclude the rig from the view layer (the test script turns it on)
    for lc in bpy.context.view_layer.layer_collection.children:
        if lc.collection == rig:
            lc.exclude = True
    for o in bpy.data.objects:
        if o.type == "MESH":
            print("OBJ", o.name, len(o.data.vertices), "verts", len(o.data.polygons), "faces",
                  tuple(round(x, 3) for x in o.dimensions))
    bpy.ops.wm.save_as_mainfile(filepath=OUT, compress=True)
    print("SAVED", OUT)


main()
