"""Menace pumpkin: the user's own game pumpkin (pumpkin_0 from Assets/Blender/Knight_429.blend) with only its eyes,
nose and mouth recarved to look menacing -> assets/Menace_Pumpkin.blend

    "E:/Steam/steamapps/common/Blender/blender.exe" -b --factory-startup --python Exports/Steam_Art/assets/menace_pumpkin_build.py
    ... -- style=lantern out=menace_pumpkin/Menace_Pumpkin_Lantern.blend     (the alternative look, see STYLE)

Knight_429.blend is only READ (pumpkin_0 is appended into an empty session); nothing in Assets/ is written.

What it does
  1. Copies pumpkin_0's mesh and finds its old face pits (eyes, mouth: faces the outer skin can't reach without
     crossing a sharp rim; plus the shallow nose pit).
  2. Deletes those pits and closes each hole with a new patch of skin that follows the pumpkin's curve
     (radius and UVs interpolated from the rim), so the skin is whole again.
  3. Cuts the new face with exact booleans (shapes in the tunables, drawn on the front view):
     STYLE "game" (default): pits like the game pumpkin - dark sockets, a glowing yellow almond pupil sunk in each eye,
       a dark nose, a wide jagged grin whose back glows.  Solid pumpkin, no hollow.
     STYLE "lantern": hollowed (WALL thick) and the face cut right through, glowing walls + glowing inside.
  4. Texture: copies of the game atlas (albedo, metal/smooth, normal) where the texels under the new skin patches
     (the old painted face cut-outs) are refilled with skin -> menace_pumpkin/Menace_Pumpkin_*.png, also packed
     into the .blend.  The game textures are never written.

FACING / SCALE: exactly pumpkin_0's own local frame.  Face looks toward LOCAL -Y, +Z up, root = pumpkin_0's origin
(bottom centre, z 0.005 = floor contact).  Unscaled it is 0.558 x 0.564 x 0.463 m (stem included), the same mesh size
as pumpkin_0.  Give the root pumpkin_0's scene transform (loc (0.143, 0.272, 0.001), rot (-0.0012, -0.0695, -3.545),
scale 0.828) and it sits exactly like the old pumpkin (0.462 m wide).

Collections
  Menace_Pumpkin        root "Menace_Pumpkin_Root" + "Menace_Pumpkin_Body" + point light "Menace_Pumpkin_Inner_Light"
                        ("game": inside the mouth pit at local (0, -0.256, 0.105); "lantern": body centre).
  Menace_Pumpkin_Small  root "Menace_Pumpkin_Small_Root" + "Menace_Pumpkin_Small_Body" (the SAME mesh) + its own light
                        "Menace_Pumpkin_Small_Inner_Light".  Unscaled like the hero: scale the root like the old small
                        pumpkins (s * 0.828, s = 0.2-0.32).  Light energy 0.1 W suits scale ~0.25.
  Menace_Pumpkin_Test_Rig (excluded) floor + the untouched pumpkin_0 for comparison (menace_pumpkin_test_render.py).

Materials ("Glow" Value node in each glowing one; emission ramps with the point attribute "carve_t",
0 = skin edge .. 1 = deep):
  MP_Pumpkin_Skin        the game material on the new texture copies + a little subsurface
  game:    MP_Pumpkin_Socket (dark walls, faint warm glow deep down), MP_Pumpkin_Pupil, MP_Pumpkin_Mouth_Glow
  lantern: MP_Pumpkin_Cut_Flesh, MP_Pumpkin_Inner
Under AgX keep Glow near 1 or less: higher washes the yellow to cream.
"""
import bpy, bmesh, math, os, sys
import numpy as np
from mathutils import Vector, Matrix, geometry as mg
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
SRC_BLEND = os.path.join(REPO, "Assets", "Blender", "Knight_429.blend")
OUT_DIR = os.path.join(HERE, "menace_pumpkin")
ARGS = dict(a.split("=", 1) for a in (sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []) if "=" in a)
STYLE = ARGS.get("style", "game")          # "game" (default) or "lantern"
OUT = os.path.join(HERE, ARGS.get("out", "Menace_Pumpkin.blend"))
os.makedirs(OUT_DIR, exist_ok=True)

# ------------------------------------------------------------------ tunables
C = Vector((0.0, -0.005, 0.19))     # body centre (projection centre for patches and cuts)
# style "game": pits like the game pumpkin (dark sockets, glowing yellow pupils, glowing mouth back)
EYE_DEPTH, PUPIL_DEPTH, NOSE_DEPTH, MOUTH_DEPTH = 0.024, 0.040, 0.022, 0.032
T_REF = 0.034                       # carve_t = depth under the skin / T_REF
GLOW_SOCKET = 0.12                  # MP_Pumpkin_Socket "Glow" (warm glow creeping up the dark walls from deep down)
GLOW_PUPIL = 0.9                    # MP_Pumpkin_Pupil "Glow"
GLOW_MOUTH = 0.85                   # MP_Pumpkin_Mouth_Glow "Glow"
PUPIL_R = (0.112, 0.228, 0.082, 0.036, 29.0)   # almond: centre x, z, length, width, tilt (deg) of the right pupil
# style "lantern": hollow with the face cut right through
WALL = 0.030                        # carved wall thickness (unscaled metres)
GLOW_CUT = 0.7                      # MP_Pumpkin_Cut_Flesh "Glow"
GLOW_INNER = 0.5                    # MP_Pumpkin_Inner "Glow"
LIGHT_W, SMALL_LIGHT_W = 1.3, 0.1

# The new face, drawn on the FRONT view (x = screen right, z = up; viewer at -Y).  Each shape is projected onto the
# skin along +Y and cut toward C.  Eyes are mirrored.
EYE_R = [(0.036, 0.214), (0.085, 0.240), (0.135, 0.270), (0.172, 0.293), (0.190, 0.300),
         (0.196, 0.282), (0.192, 0.250), (0.178, 0.222), (0.152, 0.200), (0.118, 0.188),
         (0.082, 0.189), (0.055, 0.198)]
NOSE = [(-0.034, 0.156), (0.002, 0.162), (0.042, 0.156), (0.016, 0.207), (0.004, 0.246), (-0.010, 0.207)]
MOUTH_HALF_W = 0.222
MOUTH_CORNER_Z = 0.192
UPPER_MID_Z, LOWER_MID_Z = 0.132, 0.048
UPPER_POW, LOWER_POW = 2.0, 2.4
# teeth: (x centre, half width, height as a share of the mouth's opening there)
UPPER_TEETH = [(-0.112, 0.026, 0.62), (0.010, 0.027, 0.66), (0.128, 0.024, 0.60)]
LOWER_TEETH = [(-0.170, 0.020, 0.55), (-0.050, 0.026, 0.62), (0.070, 0.026, 0.62), (0.178, 0.018, 0.5)]


def log(*a):
    print("[menace]", *a)
    sys.stdout.flush()


# ------------------------------------------------------------------ 0. empty session + the user's pumpkin
for o in list(bpy.data.objects):
    bpy.data.objects.remove(o)
with bpy.data.libraries.load(SRC_BLEND, link=False) as (src, dst):
    dst.objects = ["pumpkin_0"]
src_ob = dst.objects[0]
src_mat = src_ob.data.materials[0]
log("appended", src_ob.name, len(src_ob.data.polygons), "faces")

bm = bmesh.new()
bm.from_mesh(src_ob.data)
bm.verts.ensure_lookup_table()
bm.faces.ensure_lookup_table()
uvl = bm.loops.layers.uv["UVMap"]

# ------------------------------------------------------------------ 1. find the old face pits
outer = {max(bm.faces, key=lambda f: f.calc_center_median().y).index}       # a face on the back
st = list(outer)
while st:
    f = bm.faces[st.pop()]
    for e in f.edges:
        if len(e.link_faces) == 2 and math.degrees(e.calc_face_angle()) < 55.0:
            for g in e.link_faces:
                if g.index not in outer:
                    outer.add(g.index)
                    st.append(g.index)
OLD_NOSE = [(-0.027, 0.155), (0.041, 0.155), (0.040, 0.212), (0.019, 0.240), (-0.006, 0.240), (-0.024, 0.212)]


def in_poly(p, poly):
    x, y = p
    ins = False
    j = len(poly) - 1
    for i in range(len(poly)):
        xi, yi = poly[i]
        xj, yj = poly[j]
        if (yi > y) != (yj > y) and x < (xj - xi) * (y - yi) / (yj - yi + 1e-12) + xi:
            ins = not ins
        j = i
    return ins


cav = []
for f in bm.faces:
    c = f.calc_center_median()
    if c.y > 0.0 or c.z > 0.34:
        continue
    if f.index not in outer or (c.y < -0.2 and in_poly((c.x, c.z), OLD_NOSE)):
        cav.append(f)
log("old pit faces", len(cav))
bmesh.ops.delete(bm, geom=cav, context="FACES")
bm.verts.ensure_lookup_table()

# ------------------------------------------------------------------ 2. close each hole with curved skin
bnd = [e for e in bm.edges if e.is_boundary]
seen, loops = set(), []
for e0 in bnd:
    if e0.index in seen:
        continue
    seen.add(e0.index)
    vs = [e0.verts[0], e0.verts[1]]
    cur = e0
    while True:
        v = vs[-1]
        nxt = [e for e in v.link_edges if e.is_boundary and e.index not in seen]
        if not nxt:
            break
        cur = nxt[0]
        seen.add(cur.index)
        w = cur.other_vert(v)
        if w == vs[0]:
            break
        vs.append(w)
    loops.append(vs)
log("holes", len(loops), [len(l) for l in loops])

fill_faces = []
for vs in loops:
    nb = len(vs)
    cen = sum((v.co for v in vs), Vector()) / nb
    n = (cen - C).normalized()
    u = n.cross(Vector((0, 0, 1))).normalized()
    if u.length < 0.5:
        u = n.cross(Vector((1, 0, 0))).normalized()
    v_ = n.cross(u).normalized()

    def g2(p):
        w = p - C
        d = w.dot(n)
        return Vector((w.dot(u) / d, w.dot(v_) / d))

    b2 = [g2(v.co) for v in vs]
    bR = [(v.co - C).length for v in vs]
    bUV = []
    for v in vs:
        ls = [l for l in v.link_loops]
        bUV.append(ls[0][uvl].uv.copy())
    xs = [p.x for p in b2]
    ys = [p.y for p in b2]
    s = 0.045
    inner = []
    for gx in np.arange(min(xs) + s / 2, max(xs), s):
        for gy in np.arange(min(ys) + s / 2, max(ys), s):
            p = Vector((gx, gy))
            if not in_poly(p, b2):
                continue
            ok = True
            for i in range(nb):
                a, b = b2[i], b2[(i + 1) % nb]
                q, t = mg.intersect_point_line(p, a, b)
                t = max(0.0, min(1.0, t))
                if (p - (a + (b - a) * t)).length < 0.55 * s:
                    ok = False
                    break
            if ok:
                inner.append(p)
    pts = b2 + inner
    out = mg.delaunay_2d_cdt(pts, [], [list(range(nb))], 1, 1e-7)
    overts, oedges, ofaces, orig_v = out[0], out[1], out[2], out[3]
    # map output verts -> value slots
    nv = len(overts)
    R = np.zeros(nv)
    UV = np.zeros((nv, 2))
    fixed = np.zeros(nv, bool)
    bmv = [None] * nv
    for i in range(nv):
        ov = [k for k in orig_v[i] if k < nb]
        if ov:
            k = ov[0]
            R[i] = bR[k]
            UV[i] = bUV[k]
            fixed[i] = True
            bmv[i] = vs[k]
    R[~fixed] = np.mean(bR)
    UV[~fixed] = np.mean(np.array([tuple(x) for x in bUV]), axis=0)
    nbr = [set() for _ in range(nv)]
    for f in ofaces:
        for a in range(len(f)):
            nbr[f[a]].add(f[(a + 1) % len(f)])
            nbr[f[(a + 1) % len(f)]].add(f[a])
    free = [i for i in range(nv) if not fixed[i]]
    for _ in range(800):
        for i in free:
            if nbr[i]:
                ids = list(nbr[i])
                R[i] = R[ids].mean()
                UV[i] = UV[ids].mean(axis=0)
    for i in free:
        p = overts[i]
        d = (n + u * p.x + v_ * p.y).normalized()
        bmv[i] = bm.verts.new(C + d * R[i])
    for f in ofaces:
        vv = [bmv[i] for i in f]
        try:
            nf = bm.faces.new(vv)
        except ValueError:
            continue
        nf.normal_update()
        if nf.normal.dot(nf.calc_center_median() - C) < 0:
            nf.normal_flip()
            f = list(reversed(f))
        for l in nf.loops:
            i = f[[bmv[j] for j in f].index(l.vert)]
            l[uvl].uv = UV[i]
        nf.smooth = True
        nf.material_index = 0
        fill_faces.append(nf)
log("patch faces", len(fill_faces))
bm.normal_update()
bad = [e for e in bm.edges if not e.is_manifold]
log("non-manifold edges after fill", len(bad))

# remember where the patches land in the texture (to repaint those texels as skin)
fill_uv_tris = []
for f in fill_faces:
    uvs = [l[uvl].uv.copy() for l in f.loops]
    for k in range(1, len(uvs) - 1):
        fill_uv_tris.append((uvs[0], uvs[k], uvs[k + 1]))

filled_me = bpy.data.meshes.new("Menace_Pumpkin_Filled")
bm.to_mesh(filled_me)
outer_bvh = BVHTree.FromBMesh(bm)




# ------------------------------------------------------------------ 3. cutters (the new face; + the scooped inside for "lantern")
cut_coll = bpy.data.collections.new("MP_Cutters")
bpy.context.scene.collection.children.link(cut_coll)


def densify(poly, step=0.004):
    out = []
    m = len(poly)
    for i in range(m):
        a, b = Vector(poly[i]), Vector(poly[(i + 1) % m])
        k = max(1, int(round((b - a).length / step)))
        for j in range(k):
            out.append(a.lerp(b, j / k))
    return out


def mouth_outline():
    W = MOUTH_HALF_W

    def zu(x):
        return UPPER_MID_Z + (MOUTH_CORNER_Z - UPPER_MID_Z) * (abs(x) / W) ** UPPER_POW

    def zl(x):
        return LOWER_MID_Z + (MOUTH_CORNER_Z - LOWER_MID_Z) * (abs(x) / W) ** LOWER_POW

    def tooth(x, teeth):
        z = 0.0
        for (tx, hw, hk) in teeth:
            d = abs(x - tx) / hw
            if d < 1.0:
                z = max(z, (1.0 - d) ** 1.15 * hk)
        return z

    N = 160
    upper, lower = [], []
    for i in range(N + 1):
        x = -W + 2 * W * i / N
        gap = zu(x) - zl(x)
        upper.append((x, zu(x) - gap * tooth(x, UPPER_TEETH)))
    for i in range(N + 1):
        x = W - 2 * W * i / N
        gap = zu(x) - zl(x)
        lower.append((x, zl(x) + gap * tooth(x, LOWER_TEETH)))
    return upper + lower[1:-1]


def almond(cx, cz, length, width, ang_deg, n=48):
    a = math.radians(ang_deg)
    ca, sa = math.cos(a), math.sin(a)
    pts = []
    for i in range(n):
        t = 2 * math.pi * i / n
        x = 0.5 * length * math.cos(t)
        y = 0.5 * width * math.sin(t) * abs(math.sin(t)) ** 0.6
        pts.append((cx + x * ca - y * sa, cz + x * sa + y * ca))
    return pts


def project(p):
    hit = outer_bvh.ray_cast(Vector((p.x, -1.0, p.y)), Vector((0, 1, 0)))
    if hit[0] is None:
        raise RuntimeError(f"outline point {tuple(p)} misses the pumpkin")
    return hit[0], (hit[0] - C).normalized()


def cutter(name, poly2d, depth, side_mat, cap_mat, out_h=0.06, grid=0.009):
    """A prism over the outline: its walls run toward C, its caps follow the skin (out_h outside, depth inside)."""
    b2 = densify(poly2d)
    if (b2[0] - b2[-1]).length < 1e-6:
        b2 = b2[:-1]
    m = len(b2)
    xs, ys = [p.x for p in b2], [p.y for p in b2]
    inner = []
    for gx in np.arange(min(xs) + grid / 2, max(xs), grid):
        for gy in np.arange(min(ys) + grid / 2, max(ys), grid):
            p = Vector((gx, gy))
            if not in_poly(p, b2):
                continue
            ok = True
            for i in range(m):
                a, b = b2[i], b2[(i + 1) % m]
                q, t = mg.intersect_point_line(p, a, b)
                t = max(0.0, min(1.0, t))
                if (p - (a + (b - a) * t)).length < 0.6 * grid:
                    ok = False
                    break
            if ok:
                inner.append(p)
    res = mg.delaunay_2d_cdt(b2 + inner, [], [list(range(m))], 1, 1e-8)
    ov, of, orig = res[0], res[2], res[3]
    idx_of = {}
    for i, o in enumerate(orig):
        for k in o:
            if k < m:
                idx_of[k] = i
    if len(idx_of) != m:
        raise RuntimeError(f"{name}: CDT merged outline points")
    nv = len(ov)
    SD = [project(p) for p in ov]
    verts = [S + d * out_h for S, d in SD] + [S - d * depth for S, d in SD]
    faces, mats = [], []
    for f in of:
        faces.append(list(f))
        mats.append(0)
        faces.append([nv + i for i in reversed(f)])
        mats.append(1)
    for k in range(m):
        a, b = idx_of[k], idx_of[(k + 1) % m]
        faces.append([a, b, nv + b, nv + a])
        mats.append(0)
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in verts], [], faces)
    me.validate()
    me.uv_layers.new(name="UVMap")
    me.materials.append(side_mat)
    me.materials.append(cap_mat)
    me.polygons.foreach_set("material_index", mats)
    cb = bmesh.new()
    cb.from_mesh(me)
    bmesh.ops.recalc_face_normals(cb, faces=cb.faces[:])
    nm = len([e for e in cb.edges if not e.is_manifold])
    cb.to_mesh(me)
    cb.free()
    o = bpy.data.objects.new(name, me)
    cut_coll.objects.link(o)
    log("cutter", name, len(me.polygons), "faces, non-manifold", nm)
    return o


# ------------------------------------------------------------------ 4. textures + materials

# ------------------------------------------------------------------ 4. materials + textures
def load_px(img):
    W, H = img.size
    return np.array(img.pixels[:], dtype=np.float32).reshape(H, W, 4)


def raster_mask(tris, W, H, grow=4):
    mask = np.zeros((H, W), bool)
    for a, b, c in tris:
        P = np.array([[a.x * W, a.y * H], [b.x * W, b.y * H], [c.x * W, c.y * H]])
        x0, y0 = np.floor(P.min(0)).astype(int) - 1
        x1, y1 = np.ceil(P.max(0)).astype(int) + 1
        x0, y0 = max(x0, 0), max(y0, 0)
        x1, y1 = min(x1, W - 1), min(y1, H - 1)
        if x1 < x0 or y1 < y0:
            continue
        gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        (ax, ay), (bx, by), (cx, cy) = P
        den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(den) < 1e-9:
            continue
        l1 = ((by - cy) * (gx - cx) + (cx - bx) * (gy - cy)) / den
        l2 = ((cy - ay) * (gx - cx) + (ax - cx) * (gy - cy)) / den
        l3 = 1 - l1 - l2
        ins = (l1 >= -0.02) & (l2 >= -0.02) & (l3 >= -0.02)
        mask[y0:y1 + 1, x0:x1 + 1] |= ins
    for _ in range(grow):
        m2 = mask.copy()
        m2[1:] |= mask[:-1]
        m2[:-1] |= mask[1:]
        m2[:, 1:] |= mask[:, :-1]
        m2[:, :-1] |= mask[:, 1:]
        mask = m2
    return mask


def push_pull(img, valid):
    H, W = valid.shape
    if H <= 2 or W <= 2:
        out = img.copy()
        if valid.any():
            out[~valid] = img[valid].mean(0)
        return out
    h2, w2 = H // 2, W // 2
    im = img[:h2 * 2, :w2 * 2]
    va = valid[:h2 * 2, :w2 * 2].astype(np.float32)
    s = (im * va[..., None]).reshape(h2, 2, w2, 2, -1).sum((1, 3))
    c = va.reshape(h2, 2, w2, 2).sum((1, 3))
    small = np.where(c[..., None] > 0, s / np.maximum(c, 1e-6)[..., None], 0)
    small = push_pull(small, c > 0)
    up = np.repeat(np.repeat(small, 2, 0), 2, 1)
    upf = np.zeros_like(img)
    upf[:h2 * 2, :w2 * 2] = up
    if H > h2 * 2:
        upf[h2 * 2:] = upf[h2 * 2 - 1]
    if W > w2 * 2:
        upf[:, w2 * 2:] = upf[:, w2 * 2 - 1:w2 * 2]
    return np.where(valid[..., None], img, upf)


def blur(a, r=2):
    out = a.copy()
    for _ in range(r):
        out = (out + np.roll(out, 1, 0) + np.roll(out, -1, 0) + np.roll(out, 1, 1) + np.roll(out, -1, 1)) / 5.0
    return out


def repaint(img, mask, out_name, kind):
    px = load_px(img)
    H, W = mask.shape
    # fill from the texels around the patch (outside the mask, inside the pumpkin's skin colours)
    valid = ~mask
    filled = push_pull(px[..., :3], valid)
    # soften the hard push-pull blocks inside the mask a little
    sm = blur(filled, 6)
    rgb = np.where(mask[..., None], sm, px[..., :3])
    if kind == "albedo":
        rng = np.random.default_rng(429)
        nz = blur(rng.standard_normal((H, W)).astype(np.float32), 3) * 0.06
        rgb = np.where(mask[..., None], np.clip(rgb * (1 + nz[..., None]), 0, 1), rgb)
    if kind == "normal":
        n = rgb * 2 - 1
        n = n / np.maximum(np.linalg.norm(n, axis=2, keepdims=True), 1e-6)
        rgb = np.where(mask[..., None], n * 0.5 + 0.5, rgb)
    out = np.concatenate([rgb, px[..., 3:]], axis=2)
    im = bpy.data.images.new(out_name, W, H, alpha=True, float_buffer=False)
    im.colorspace_settings.name = img.colorspace_settings.name
    im.pixels.foreach_set(out.ravel())
    path = os.path.join(OUT_DIR, out_name + ".png")
    im.filepath_raw = path
    im.file_format = "PNG"
    im.save()
    im.pack()
    log("wrote", path, "texels repainted", int(mask.sum()))
    return im




tex_nodes = {n.name: n for n in src_mat.node_tree.nodes if n.type == "TEX_IMAGE"}
alb_img = tex_nodes["Image Texture"].image
met_img = tex_nodes["Image Texture.001"].image
nrm_img = tex_nodes["Image Texture.002"].image
W_, H_ = alb_img.size
mask = raster_mask(fill_uv_tris, W_, H_, grow=5)
new_alb = repaint(alb_img, mask, "Menace_Pumpkin_Albedo", "albedo")
new_met = repaint(met_img, mask, "Menace_Pumpkin_MetallicSmoothness", "met")
new_nrm = repaint(nrm_img, mask, "Menace_Pumpkin_Normal", "normal")


def principled(nt):
    return next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")


skin = src_mat.copy()
skin.name = "MP_Pumpkin_Skin"
for n in skin.node_tree.nodes:
    if n.type == "TEX_IMAGE":
        n.image = {"Image Texture": new_alb, "Image Texture.001": new_met, "Image Texture.002": new_nrm}[n.name]
pb = principled(skin.node_tree)
pb.inputs["Roughness"].default_value = 0.55
pb.inputs["Subsurface Weight"].default_value = 0.12           # a little inner warmth in the skin
pb.inputs["Subsurface Radius"].default_value = (1.0, 0.35, 0.12)
pb.inputs["Subsurface Scale"].default_value = 0.012


def make_mat(name, base, rough, emit_lo, emit_hi, glow, t_lo=1.0, t_hi=1.0, sss=0.0, streaks=False):
    """Principled material; emission colour/strength ramp along the point attribute carve_t (skin edge 0 .. deep 1):
    strength = Glow * lerp(t_lo, t_hi, carve_t) * fibre noise.  A Value node named "Glow" scales it."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    L = nt.links.new
    p = principled(nt)
    p.inputs["Roughness"].default_value = rough
    if sss:
        p.inputs["Subsurface Weight"].default_value = sss
        p.inputs["Subsurface Radius"].default_value = (1.0, 0.4, 0.15)
        p.inputs["Subsurface Scale"].default_value = 0.01
    g = nt.nodes.new("ShaderNodeValue")
    g.name = g.label = "Glow"
    g.outputs[0].default_value = glow
    at = nt.nodes.new("ShaderNodeAttribute")
    at.attribute_type = "GEOMETRY"
    at.attribute_name = "carve_t"
    tc = nt.nodes.new("ShaderNodeTexCoord")
    mp = nt.nodes.new("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = (1.0, 1.0, 1.0) if not streaks else (1.0, 1.0, 6.0)
    L(tc.outputs["Object"], mp.inputs["Vector"])
    nz = nt.nodes.new("ShaderNodeTexNoise")
    nz.inputs["Scale"].default_value = 70.0
    nz.inputs["Detail"].default_value = 6.0
    L(mp.outputs["Vector"], nz.inputs["Vector"])
    # base colour: base darkened/lightened by the noise
    bmix = nt.nodes.new("ShaderNodeMix")
    bmix.data_type = "RGBA"
    bmix.inputs[6].default_value = (*[c * 0.55 for c in base], 1)
    bmix.inputs[7].default_value = (*[min(1.0, c * 1.35) for c in base], 1)
    L(nz.outputs["Fac"], bmix.inputs[0])
    L(bmix.outputs[2], p.inputs["Base Color"])
    ramp = nt.nodes.new("ShaderNodeMix")
    ramp.data_type = "RGBA"
    ramp.inputs[6].default_value = (*emit_lo, 1)
    ramp.inputs[7].default_value = (*emit_hi, 1)
    L(at.outputs["Fac"], ramp.inputs[0])
    mr = nt.nodes.new("ShaderNodeMapRange")
    mr.inputs["To Min"].default_value = t_lo
    mr.inputs["To Max"].default_value = t_hi
    L(at.outputs["Fac"], mr.inputs["Value"])
    nm = nt.nodes.new("ShaderNodeMapRange")
    nm.inputs["To Min"].default_value = 0.8
    nm.inputs["To Max"].default_value = 1.15
    L(nz.outputs["Fac"], nm.inputs["Value"])
    m1 = nt.nodes.new("ShaderNodeMath")
    m1.operation = "MULTIPLY"
    L(g.outputs[0], m1.inputs[0])
    L(nm.outputs["Result"], m1.inputs[1])
    m2 = nt.nodes.new("ShaderNodeMath")
    m2.operation = "MULTIPLY"
    L(m1.outputs[0], m2.inputs[0])
    L(mr.outputs["Result"], m2.inputs[1])
    L(ramp.outputs[2], p.inputs["Emission Color"])
    L(m2.outputs[0], p.inputs["Emission Strength"])
    return m


cutters = []
if STYLE == "lantern":
    cut_mat = make_mat("MP_Pumpkin_Cut_Flesh", (0.45, 0.25, 0.06), 0.6, (1.0, 0.55, 0.06), (1.0, 0.36, 0.02),
                       GLOW_CUT, 0.15, 1.0, sss=0.25)
    inner_mat = make_mat("MP_Pumpkin_Inner", (0.35, 0.15, 0.04), 0.7, (1.0, 0.30, 0.02), (1.0, 0.30, 0.02),
                         GLOW_INNER, sss=0.2)
    # the scooped inside: an icosphere pushed WALL inside the skin, smoothed
    cbm = bmesh.new()
    bmesh.ops.create_icosphere(cbm, subdivisions=4, radius=1.0)
    cbm.verts.ensure_lookup_table()
    dirs = [v.co.normalized() for v in cbm.verts]
    Rin = np.array([((outer_bvh.ray_cast(C, d)[0] or (C + d * 0.18)) - C).length for d in dirs])
    cnb = [[e.other_vert(v).index for e in v.link_edges] for v in cbm.verts]
    Rs = Rin - WALL
    for _ in range(6):
        Rs = np.array([0.5 * Rs[i] + 0.5 * Rs[cnb[i]].mean() for i in range(len(Rs))])
    Rs = np.minimum(Rs, Rin - WALL * 0.85)
    for v, d, r in zip(cbm.verts, dirs, Rs):
        v.co = C + d * r
    cav_me = bpy.data.meshes.new("MP_Cut_Cavity")
    cbm.to_mesh(cav_me)
    cav_me.uv_layers.new(name="UVMap")
    cav_me.materials.append(inner_mat)
    cav_ob = bpy.data.objects.new("MP_Cut_Cavity", cav_me)
    cut_coll.objects.link(cav_ob)
    cav_bvh = BVHTree.FromBMesh(cbm)
    cutters.append(cav_ob)
    D = WALL + 0.035
    cutters += [cutter("MP_Cut_Eye_R", EYE_R, D, cut_mat, cut_mat),
                cutter("MP_Cut_Eye_L", [(-x, z) for (x, z) in reversed(EYE_R)], D, cut_mat, cut_mat),
                cutter("MP_Cut_Nose", NOSE, D, cut_mat, cut_mat),
                cutter("MP_Cut_Mouth", mouth_outline(), D, cut_mat, cut_mat)]
    light_at = C.copy()
else:   # "game": dark sockets like the game pumpkin, glowing yellow pupils, a glowing mouth back
    sock_mat = make_mat("MP_Pumpkin_Socket", (0.075, 0.045, 0.03), 0.75, (1.0, 0.5, 0.12), (1.0, 0.45, 0.08),
                        GLOW_SOCKET, 0.0, 1.0, streaks=True)
    pupil_mat = make_mat("MP_Pumpkin_Pupil", (0.3, 0.22, 0.03), 0.5, (1.0, 0.74, 0.02), (1.0, 0.50, 0.01),
                         GLOW_PUPIL, 0.8, 1.0)
    mouth_mat = make_mat("MP_Pumpkin_Mouth_Glow", (0.3, 0.14, 0.03), 0.6, (1.0, 0.50, 0.02), (1.0, 0.36, 0.01),
                         GLOW_MOUTH, 0.8, 1.0)
    pr = PUPIL_R
    cutters += [cutter("MP_Cut_Eye_R", EYE_R, EYE_DEPTH, sock_mat, sock_mat),
                cutter("MP_Cut_Eye_L", [(-x, z) for (x, z) in reversed(EYE_R)], EYE_DEPTH, sock_mat, sock_mat),
                cutter("MP_Cut_Pupil_R", almond(*pr), PUPIL_DEPTH, pupil_mat, pupil_mat),
                cutter("MP_Cut_Pupil_L", [(-x, z) for (x, z) in reversed(almond(*pr))], PUPIL_DEPTH, pupil_mat, pupil_mat),
                cutter("MP_Cut_Nose", NOSE, NOSE_DEPTH, sock_mat, sock_mat),
                cutter("MP_Cut_Mouth", mouth_outline(), MOUTH_DEPTH, sock_mat, mouth_mat)]
    S, d = project(Vector((0.0, 0.5 * (UPPER_MID_Z + LOWER_MID_Z) + 0.01)))
    light_at = S - d * (MOUTH_DEPTH * 0.45)

# ------------------------------------------------------------------ 5. the boolean
filled_me.materials.clear()
filled_me.materials.append(skin)
work = bpy.data.objects.new("MP_Work", filled_me)
bpy.context.scene.collection.objects.link(work)
mod = work.modifiers.new("Carve", "BOOLEAN")
mod.operation = "DIFFERENCE"
mod.operand_type = "COLLECTION"
mod.collection = cut_coll
mod.solver = "EXACT"
mod.material_mode = "TRANSFER"
mod.use_hole_tolerant = True
bpy.context.view_layer.update()
dg = bpy.context.evaluated_depsgraph_get()
me = bpy.data.meshes.new_from_object(work.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
me.name = "Menace_Pumpkin_Mesh"
log("result", len(me.vertices), "verts", len(me.polygons), "faces", "mats", [m.name for m in me.materials])
rbm = bmesh.new()
rbm.from_mesh(me)
log("non-manifold edges after carve", len([e for e in rbm.edges if not e.is_manifold]))
rbm.free()

# carve_t: 0 at the skin .. 1 deep (lantern: at the inner wall; game: at T_REF below the skin)
vals = []
for v in me.vertices:
    do = outer_bvh.find_nearest(v.co)[3]
    if STYLE == "lantern":
        di = cav_bvh.find_nearest(v.co)[3]
        vals.append(do / max(do + di, 1e-6))
    else:
        vals.append(min(1.0, do / T_REF))
a = me.attributes.new("carve_t", "FLOAT", "POINT")
a.data.foreach_set("value", vals)
me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
me.set_sharp_from_angle(angle=math.radians(40.0))

# ------------------------------------------------------------------ 6. objects + collections
scn = bpy.context.scene
for c in list(scn.collection.children):
    scn.collection.children.unlink(c)
for o in list(scn.collection.objects):
    scn.collection.objects.unlink(o)


def new_coll(name):
    c = bpy.data.collections.new(name)
    scn.collection.children.link(c)
    return c


def build_set(prefix, coll, light_w, light_r, at):
    root = bpy.data.objects.new(prefix + "_Root", None)
    root.empty_display_size = 0.15
    root.empty_display_type = "PLAIN_AXES"
    coll.objects.link(root)
    root.location = at
    body = bpy.data.objects.new(prefix + "_Body", me)
    coll.objects.link(body)
    body.parent = root
    body["source"] = "pumpkin_0 (Assets/Blender/Knight_429.blend), face recarved, style " + STYLE
    L = bpy.data.lights.new(prefix + "_Inner_Light", "POINT")
    L.energy = light_w
    L.color = (1.0, 0.5, 0.12)
    L.shadow_soft_size = light_r
    lo = bpy.data.objects.new(prefix + "_Inner_Light", L)
    coll.objects.link(lo)
    lo.parent = root
    lo.location = light_at
    lo.visible_camera = False
    return root, body, lo


C_HERO = new_coll("Menace_Pumpkin")
C_SMALL = new_coll("Menace_Pumpkin_Small")
build_set("Menace_Pumpkin", C_HERO, LIGHT_W, 0.02, (0, 0, 0))
build_set("Menace_Pumpkin_Small", C_SMALL, SMALL_LIGHT_W, 0.008, (0.9, 0, 0))
scn["menace_style"] = STYLE

# test rig (excluded from the view layer; menace_pumpkin_test_render.py switches it on)
C_RIG = new_coll("Menace_Pumpkin_Test_Rig")
fl = bpy.data.meshes.new("MP_Test_Floor")
fl.from_pydata([(-4, -4, 0), (4, -4, 0), (4, 4, 0), (-4, 4, 0)], [], [[0, 1, 2, 3]])
flo = bpy.data.objects.new("MP_Test_Floor", fl)
fm = bpy.data.materials.new("MP_Test_Floor")
fm.use_nodes = True
principled(fm.node_tree).inputs["Base Color"].default_value = (0.05, 0.05, 0.06, 1)
principled(fm.node_tree).inputs["Roughness"].default_value = 0.8
fl.materials.append(fm)
C_RIG.objects.link(flo)
orig = bpy.data.objects.new("MP_Test_Original_pumpkin_0", src_ob.data)   # the untouched game pumpkin, for comparison
orig.location = (-0.9, 0, 0)
C_RIG.objects.link(orig)
for lc in scn.view_layers[0].layer_collection.children:
    if lc.name == "Menace_Pumpkin_Test_Rig":
        lc.exclude = True

# ------------------------------------------------------------------ 7. clean + save
for o in [work] + cutters:
    bpy.data.objects.remove(o)
bpy.data.collections.remove(cut_coll)
bpy.data.objects.remove(src_ob)
bpy.data.meshes.remove(filled_me)
bpy.data.orphans_purge(do_recursive=True)
scn.render.engine = "CYCLES"
bpy.ops.wm.save_as_mainfile(filepath=OUT, compress=True)
log("SAVED", OUT, "style", STYLE)
