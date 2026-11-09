"""Menace pumpkin: the user's own game pumpkin (pumpkin_0 from Assets/Blender/Knight_429.blend) with only its eyes and
mouth recarved to look menacing -> assets/Menace_Pumpkin.blend

    "E:/Steam/steamapps/common/Blender/blender.exe" -b --factory-startup --python Exports/Steam_Art/assets/menace_pumpkin_build.py

Knight_429.blend is only READ (pumpkin_0 is appended into an empty session); nothing in Assets/ is written.

What it does
  1. Copies pumpkin_0's mesh and finds its old face pits (eyes, nose, mouth: the faces the outer skin can't reach
     without crossing a sharp rim, plus the shallow nose pit).
  2. Deletes those pits and closes each hole with a new patch of skin that follows the pumpkin's curve
     (radius and UVs interpolated from the rim), so the shell is whole again.
  3. Hollows it (cavity = the skin moved WALL inward) and cuts the new face right through the wall
     (exact boolean): slanted angry eyes, a sharp nose and a wide jagged grin. Shapes are in FACE below.
  4. Texture: copies of the game atlas (albedo, metal/smooth, normal) with the old painted face cut-outs that the new
     skin patches land on filled with skin colour -> menace_pumpkin/Menace_Pumpkin_*.png (also packed in the .blend).
     The game textures are never written.

FACING / SCALE: exactly pumpkin_0's own local frame.  Face looks toward LOCAL -Y, +Z up, root = pumpkin_0's origin
(bottom centre).  Put the root where pumpkin_0 was (loc (0.143, 0.272, 0.001), rot (-0.0012, -0.0695, -3.545),
scale 0.828) and it sits exactly like the old pumpkin.  Unscaled it is 0.462 x 0.467 x 0.468 m (stem included).

Collections
  Menace_Pumpkin        root "Menace_Pumpkin_Root" + "Menace_Pumpkin_Body" + point light "Menace_Pumpkin_Inner_Light"
  Menace_Pumpkin_Small  root "Menace_Pumpkin_Small_Root" + "Menace_Pumpkin_Small_Body" (same mesh) + its own light
                        "Menace_Pumpkin_Small_Inner_Light".  Unscaled like the hero: scale the root like the old small
                        pumpkins (s * 0.828, s = 0.2-0.32).  Light energy is set for scale ~0.25.
  Menace_Pumpkin_Test_Rig (excluded) floor, cameras, lights for menace_pumpkin_test_render.py

Materials: MP_Pumpkin_Skin (the game look + a little subsurface), MP_Pumpkin_Cut_Flesh (carved walls, glow rises toward
the inside through the point attribute "carve_t" 0 = skin edge .. 1 = inner wall), MP_Pumpkin_Inner (scooped inside).
Each glow material has a Value node named "Glow".
"""
import bpy, bmesh, math, os, sys
import numpy as np
from mathutils import Vector, Matrix, geometry as mg
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
SRC_BLEND = os.path.join(REPO, "Assets", "Blender", "Knight_429.blend")
OUT_DIR = os.path.join(HERE, "menace_pumpkin")
OUT = os.path.join(HERE, "Menace_Pumpkin.blend")
os.makedirs(OUT_DIR, exist_ok=True)

# ------------------------------------------------------------------ tunables
C = Vector((0.0, -0.005, 0.19))     # body centre (projection centre for patches and cuts)
WALL = 0.030                        # carved wall thickness (unscaled metres)
GLOW_CUT = 1.0                      # MP_Pumpkin_Cut_Flesh "Glow"
GLOW_INNER = 0.6                    # MP_Pumpkin_Inner "Glow"
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


