

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
