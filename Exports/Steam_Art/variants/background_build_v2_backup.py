"""Far background v2 for the 429 Steam key art.
Builds C:/Github_Repository/429R/Exports/Steam_Art/assets/Background.blend from nothing (procedural only).
  "E:/Steam/steamapps/common/Blender/blender.exe" -b --python Exports/Steam_Art/assets/background_build.py

Collections
  Background_v2    everything to append (world-space, already placed for the Steam_Art cameras; root empty BG2_Root)
  BG_Ref_Cameras   copies of Steam_Art's CAM_* cameras (REF_CAM_*) + a stand-in moonlight; excluded from the view layer
The cameras all look along F = (-0.2003, -0.9797, 0) (yaw 168.45 deg) from about P = (0.75, 2.4, 0.42).
Placement below is written as (azimuth deg to camera-right, elevation deg, distance m) from P.
"""
import bpy, bmesh, math, random, os
from mathutils import Vector, Matrix, Euler, noise

OUT = r"C:/Github_Repository/429R/Exports/Steam_Art/assets/Background.blend"
SRC = r"C:/Github_Repository/429R/Exports/Steam_Art/Steam_Art.blend"

# ------------------------------------------------------------------ frame
P = Vector((0.75, 2.40, 0.42))
F = Vector((-0.2003, -0.9797, 0.0)).normalized()
R = Vector((-0.9797, 0.2003, 0.0)).normalized()
UP = Vector((0, 0, 1))
MOON = Vector((-4.1, -43.57, 3.19))          # same spot as Steam_Art's Moon (lighting_pass aims sun + beams at it)
MOON_R = 2.2
MOON_DIR = (MOON - P).normalized()


def hdir(az):
    a = math.radians(az)
    return F * math.cos(a) + R * math.sin(a)


def gpos(az, dist, z):
    v = P + hdir(az) * dist
    v.z = z
    return v


def z_for_el(el, dist):
    return P.z + dist * math.tan(math.radians(el))


def azel(v):
    d = v - P
    h = Vector((d.x, d.y, 0))
    az = math.degrees(math.atan2(h.dot(R), h.dot(F)))
    el = math.degrees(math.atan2(d.z, h.length))
    return az, el, d.length


MOON_AZ, MOON_EL, MOON_D = azel(MOON)
MOON_ANG = math.degrees(math.atan(MOON_R / MOON_D))


def yaw_facing(pos):
    d = pos - P
    d.z = 0
    d.normalize()
    return math.atan2(-d.x, d.y)


# ------------------------------------------------------------------ empty the startup file
for o in list(bpy.data.objects):
    bpy.data.objects.remove(o, do_unlink=True)
for c in list(bpy.data.collections):
    bpy.data.collections.remove(c)
for blocks in (bpy.data.meshes, bpy.data.materials, bpy.data.lights, bpy.data.cameras, bpy.data.curves):
    for b in list(blocks):
        blocks.remove(b)
sc = bpy.context.scene

C_BG = bpy.data.collections.new("Background_v2")
sc.collection.children.link(C_BG)
C_REF = bpy.data.collections.new("BG_Ref_Cameras")
sc.collection.children.link(C_REF)
ROOT = bpy.data.objects.new("BG2_Root", None)
C_BG.objects.link(ROOT)


def add_obj(name, data, coll=None, parent=True, shadow=True, camera_only=False):
    o = bpy.data.objects.new(name, data)
    (coll or C_BG).objects.link(o)
    if parent and (coll or C_BG) == C_BG:
        o.parent = ROOT
    o.visible_shadow = shadow
    if camera_only:
        o.visible_diffuse = o.visible_glossy = o.visible_transmission = o.visible_volume_scatter = False
        o.visible_shadow = False
    return o


# ------------------------------------------------------------------ node helper
class NB:
    def __init__(s, mat):
        mat.use_nodes = True
        s.nt = mat.node_tree
        s.N, s.L = s.nt.nodes, s.nt.links
        for n in list(s.N):
            s.N.remove(n)
        s.out = s.N.new("ShaderNodeOutputMaterial")

    @staticmethod
    def sock(x):
        if isinstance(x, bpy.types.NodeSocket):
            return x
        if isinstance(x, bpy.types.Node):
            return x.outputs[0]
        return None

    def put(s, sockt, v):
        so = s.sock(v)
        if so is not None:
            s.L.new(so, sockt)
        else:
            if isinstance(v, tuple) and len(v) == 3 and sockt.type == "RGBA":
                v = (*v, 1.0)
            sockt.default_value = v

    def inp(s, n, key):
        if isinstance(key, int):
            return n.inputs[key]
        for x in n.inputs:
            if x.identifier == key:
                return x
        for x in n.inputs:
            if x.name == key and getattr(x, "enabled", True):
                return x
        raise KeyError(key)

    def node(s, kind, ins=None, **props):
        n = s.N.new(kind)
        for k, v in props.items():
            setattr(n, k, v)
        for k, v in (ins or {}).items():
            s.put(s.inp(n, k), v)
        return n

    def m(s, op, a, b=None, c=None, clamp=False):
        n = s.N.new("ShaderNodeMath")
        n.operation = op
        n.use_clamp = clamp
        for i, v in enumerate((a, b, c)):
            if v is not None:
                s.put(n.inputs[i], v)
        return n.outputs[0]

    def v(s, op, a, b=None, scale=None):
        n = s.N.new("ShaderNodeVectorMath")
        n.operation = op
        s.put(n.inputs[0], a)
        if b is not None:
            s.put(n.inputs[1], b)
        if scale is not None:
            s.put(n.inputs[3], scale)
        return n.outputs["Value"] if op in ("DOT_PRODUCT", "LENGTH", "DISTANCE") else n.outputs["Vector"]

    def mix(s, fac, a, b, blend="MIX"):
        n = s.N.new("ShaderNodeMix")
        n.data_type = "RGBA"
        n.blend_type = blend
        s.put(s.inp(n, "Factor_Float"), fac)
        s.put(s.inp(n, "A_Color"), a)
        s.put(s.inp(n, "B_Color"), b)
        return n.outputs["Result"] if False else next(o for o in n.outputs if o.identifier == "Result_Color")

    def ramp(s, fac, stops, interp="LINEAR"):
        n = s.N.new("ShaderNodeValToRGB")
        cr = n.color_ramp
        cr.interpolation = interp
        while len(cr.elements) < len(stops):
            cr.elements.new(0.5)
        for e, (p, c) in zip(cr.elements, stops):
            e.position = p
            e.color = c if len(c) == 4 else (*c, 1.0)
        s.put(n.inputs[0], fac)
        return n

    def sep(s, vec):
        n = s.N.new("ShaderNodeSeparateXYZ")
        s.put(n.inputs[0], vec)
        return n.outputs

    def noise(s, vec=None, scale=5.0, detail=4.0, rough=0.5, kind="FBM", dims="3D", lac=2.0, distortion=0.0):
        n = s.N.new("ShaderNodeTexNoise")
        n.noise_dimensions = dims
        n.noise_type = kind
        if vec is not None:
            s.put(n.inputs["Vector"], vec)
        n.inputs["Scale"].default_value = scale
        n.inputs["Detail"].default_value = detail
        n.inputs["Roughness"].default_value = rough
        n.inputs["Lacunarity"].default_value = lac
        n.inputs["Distortion"].default_value = distortion
        return n

    def bsdf(s, color, rough=0.8, metal=0.0, spec=0.5, normal=None, coat=0.0):
        n = s.N.new("ShaderNodeBsdfPrincipled")
        s.put(n.inputs["Base Color"], color)
        s.put(n.inputs["Roughness"], rough)
        s.put(n.inputs["Metallic"], metal)
        n.inputs["Specular IOR Level"].default_value = spec
        n.inputs["Coat Weight"].default_value = coat
        if normal is not None:
            s.L.new(normal, n.inputs["Normal"])
        return n.outputs[0]

    def emit(s, color, strength=1.0):
        n = s.N.new("ShaderNodeEmission")
        s.put(n.inputs["Color"], color)
        s.put(n.inputs["Strength"], strength)
        return n.outputs[0]

    def mixsh(s, fac, a, b):
        n = s.N.new("ShaderNodeMixShader")
        s.put(n.inputs[0], fac)
        s.L.new(a, n.inputs[1])
        s.L.new(b, n.inputs[2])
        return n.outputs[0]

    def add(s, a, b):
        n = s.N.new("ShaderNodeAddShader")
        s.L.new(a, n.inputs[0])
        s.L.new(b, n.inputs[1])
        return n.outputs[0]

    def done(s, shader):
        s.L.new(shader, s.out.inputs["Surface"])

    def bump(s, height, strength=0.3, dist=0.05):
        n = s.N.new("ShaderNodeBump")
        n.inputs["Strength"].default_value = strength
        n.inputs["Distance"].default_value = dist
        s.put(n.inputs["Height"], height)
        return n.outputs[0]


# ------------------------------------------------------------------ look settings
FOG = (0.006, 0.016, 0.040)        # night haze colour (emission)
FOG_GLOW = 1.0                     # haze brightening toward the moon
WARM = (1.0, 0.36, 0.07)


def view_info(nb):
    geo = nb.node("ShaderNodeNewGeometry")
    cam = nb.node("ShaderNodeCameraData")
    vdir = nb.v("SCALE", geo.outputs["Incoming"], scale=-1.0)
    return geo, cam.outputs["View Distance"], vdir


def moon_glow(nb, vdir, power, gain):
    c = nb.m("MAXIMUM", nb.v("DOT_PRODUCT", vdir, tuple(MOON_DIR)), 0.0)
    return nb.m("MULTIPLY", nb.m("POWER", c, power), gain)


def fog_wrap(nb, shader, density=0.012, low=0.8, z_ref=0.0, z_fall=4.0, maxf=0.97, glow=FOG_GLOW, col=FOG):
    """Aerial perspective without a volume: blend toward a haze emission by camera distance (thicker low down)."""
    geo, dist, vdir = view_info(nb)
    fd = nb.m("SUBTRACT", 1.0, nb.m("EXPONENT", nb.m("MULTIPLY", dist, -density)))
    z = nb.sep(geo.outputs["Position"])[2]
    hz = nb.m("EXPONENT", nb.m("DIVIDE", nb.m("MAXIMUM", nb.m("SUBTRACT", z, z_ref), 0.0), -z_fall))
    fac = nb.m("MULTIPLY", nb.m("MULTIPLY", fd, nb.m("ADD", 1.0, nb.m("MULTIPLY", hz, low))), maxf, clamp=True)
    g = nb.m("ADD", 1.0, nb.m("ADD", moon_glow(nb, vdir, 40.0, glow * 0.8), moon_glow(nb, vdir, 1200.0, glow * 2.5)))
    em = nb.emit(col, g)
    return nb.mixsh(fac, shader, em)


MATS = {}


def new_mat(name):
    m = bpy.data.materials.new(name)
    MATS[name] = m
    return m


# ---- sky backdrop: gradient + moon glow + stars + faint cloud texture (camera rays only)
def mat_sky():
    m = new_mat("BG2_Sky")
    nb = NB(m)
    geo, dist, vdir = view_info(nb)
    dz = nb.sep(vdir)[2]
    el = nb.m("MULTIPLY", nb.m("ARCSINE", dz), 57.2958)
    grad = nb.ramp(nb.m("DIVIDE", el, 30.0, clamp=True), [
        (0.0, (0.010, 0.025, 0.058)), (0.10, (0.006, 0.015, 0.040)), (0.35, (0.002, 0.006, 0.020)), (1.0, (0.0008, 0.002, 0.008))])
    halo = nb.m("ADD", nb.m("ADD", moon_glow(nb, vdir, 60.0, 0.30), moon_glow(nb, vdir, 8.0, 0.10)), nb.m("ADD", moon_glow(nb, vdir, 1500.0, 1.4), moon_glow(nb, vdir, 12000.0, 2.5)))
    halo_col = nb.mix(halo, (0, 0, 0, 1), (0.36, 0.48, 0.70, 1))
    # big soft cloud banks (darken + silver near moon)
    cl = nb.noise(nb.v("SCALE", vdir, scale=1.0), scale=3.2, detail=8, rough=0.62, distortion=0.35)
    clm = nb.m("MULTIPLY", nb.m("SMOOTH_MIN", nb.m("SUBTRACT", cl.outputs["Fac"], 0.48), 0.0, 0.0) if False else nb.m("SUBTRACT", cl.outputs["Fac"], 0.47), 3.0, clamp=True)
    sky = nb.mix(nb.m("MULTIPLY", clm, 0.55), grad.outputs[0], (0.003, 0.006, 0.014, 1))
    sky = nb.mix(1.0, sky, halo_col, "ADD")
    # stars: two voronoi layers on the view direction
    def stars(scale, thresh, gain):
        vo = nb.node("ShaderNodeTexVoronoi", {"Vector": nb.v("SCALE", vdir, scale=scale)}, feature="F1")
        vo.inputs["Randomness"].default_value = 1.0
        d = vo.outputs["Distance"]
        core = nb.m("SUBTRACT", 1.0, nb.m("DIVIDE", d, thresh), clamp=True)
        core = nb.m("POWER", core, 2.0)
        rnd = nb.sep(vo.outputs["Color"])[0]
        keep = nb.m("GREATER_THAN", rnd, 0.55)
        bright = nb.m("MULTIPLY", nb.m("POWER", rnd, 6.0), gain)
        return nb.m("MULTIPLY", nb.m("MULTIPLY", core, keep), bright)
    st = nb.m("ADD", stars(260.0, 0.12, 18.0), stars(650.0, 0.10, 8.0))
    vis = nb.m("MULTIPLY", nb.m("SUBTRACT", 1.0, nb.m("MULTIPLY", clm, 0.9), clamp=True),
               nb.m("SUBTRACT", 1.0, nb.m("MULTIPLY", halo, 1.6), clamp=True))
    vis = nb.m("MULTIPLY", vis, nb.m("DIVIDE", nb.m("SUBTRACT", el, 1.5), 4.0, clamp=True))
    st = nb.m("MULTIPLY", st, vis)
    sky = nb.mix(1.0, sky, nb.mix(st, (0, 0, 0, 1), (0.8, 0.88, 1.0, 1)), "ADD")
    nb.done(nb.emit(sky, 1.0))
    return m


# ---- moon: craters + maria, warm-cream, limb darkening
def mat_moon():
    m = new_mat("BG2_Moon")
    nb = NB(m)
    tc = nb.node("ShaderNodeTexCoord")
    obj = tc.outputs["Object"]
    # maria: big irregular dark seas
    ma1 = nb.noise(obj, scale=0.9, detail=6, rough=0.55, distortion=0.6)
    ma2 = nb.noise(obj, scale=2.3, detail=4, rough=0.5)
    mv = nb.m("ADD", ma1.outputs["Fac"], nb.m("MULTIPLY", nb.m("SUBTRACT", ma2.outputs["Fac"], 0.5), 0.35))
    mar = nb.ramp(mv, [(0.47, (1.0, 1.0, 1.0)), (0.56, (0.66, 0.66, 0.68))])

    def craters(scale, rmin, rvar, keep_th, depth, rim_gain, width):
        vo = nb.node("ShaderNodeTexVoronoi", {"Vector": obj}, feature="F1")
        vo.inputs["Scale"].default_value = scale
        d = vo.outputs["Distance"]
        rc = nb.sep(vo.outputs["Color"])
        rr = nb.m("ADD", rmin, nb.m("MULTIPLY", rc[0], rvar))
        keep = nb.m("GREATER_THAN", rc[1], keep_th)
        floor = nb.m("DIVIDE", nb.m("SUBTRACT", rr, d), nb.m("MULTIPLY", rr, 0.35), clamp=True)
        rim = nb.m("EXPONENT", nb.m("MULTIPLY", nb.m("POWER", nb.m("MULTIPLY", nb.m("SUBTRACT", d, rr), width), 2.0), -1.0))
        v = nb.m("SUBTRACT", nb.m("MULTIPLY", rim, rim_gain), nb.m("MULTIPLY", floor, depth))
        return nb.m("MULTIPLY", v, keep)
    c1 = craters(3.5, 0.10, 0.16, 0.72, 0.10, 0.10, 22.0)
    c2 = craters(9.0, 0.08, 0.14, 0.6, 0.08, 0.07, 30.0)
    fine = nb.noise(obj, scale=22.0, detail=6, rough=0.6)
    tex = nb.m("ADD", nb.m("ADD", 0.9, nb.m("MULTIPLY", nb.m("SUBTRACT", fine.outputs["Fac"], 0.5), 0.25)), nb.m("ADD", c1, c2))
    col = nb.mix(1.0, mar.outputs[0], (1.0, 0.92, 0.76, 1), "MULTIPLY")
    col = nb.mix(1.0, col, nb.node("ShaderNodeCombineColor", {"Red": tex, "Green": tex, "Blue": tex}).outputs[0], "MULTIPLY")
    lw = nb.node("ShaderNodeLayerWeight", {"Blend": 0.5})
    limb = nb.m("SUBTRACT", 1.0, nb.m("MULTIPLY", nb.m("POWER", lw.outputs["Facing"], 2.0), 0.4))
    nb.done(nb.emit(col, nb.m("MULTIPLY", limb, 2.3)))
    return m


def mat_halo():
    m = new_mat("BG2_Moon_Halo")
    nb = NB(m)
    tc = nb.node("ShaderNodeTexCoord")
    r = nb.v("LENGTH", tc.outputs["Object"])
    # plane is 2 units across; moon disk = 1/HALO_SCALE
    g1 = nb.m("EXPONENT", nb.m("MULTIPLY", nb.m("MAXIMUM", nb.m("SUBTRACT", r, 0.22), 0.0), -9.0))
    g2 = nb.m("EXPONENT", nb.m("MULTIPLY", nb.m("MAXIMUM", nb.m("SUBTRACT", r, 0.22), 0.0), -2.6))
    ringr = nb.m("EXPONENT", nb.m("MULTIPLY", nb.m("POWER", nb.m("SUBTRACT", r, 0.62), 2.0), -260.0))
    edge = nb.m("SUBTRACT", 1.0, nb.m("DIVIDE", nb.m("MAXIMUM", nb.m("SUBTRACT", r, 0.85), 0.0), 0.15), clamp=True)
    a = nb.m("MULTIPLY", nb.m("ADD", nb.m("ADD", nb.m("MULTIPLY", g1, 0.9), nb.m("MULTIPLY", g2, 0.28)), nb.m("MULTIPLY", ringr, 0.07)), edge)
    col = nb.mix(nb.m("MULTIPLY", g1, 0.8), (0.30, 0.42, 0.62, 1), (0.95, 0.86, 0.66, 1))
    em = nb.emit(col, nb.m("MULTIPLY", a, 0.55))
    tr = nb.node("ShaderNodeBsdfTransparent").outputs[0]
    nb.done(nb.add(tr, em))
    return m


# ---- clouds (wispy cards): transparent + emission, silver lining toward the moon
def mat_cloud(name, wscale=0.5, dark=(0.012, 0.022, 0.048), lit=(0.55, 0.66, 0.86), density=1.0, thresh=0.5, seed=0.0, lit_gain=1.0,
              stretch=(1.0, 1.0, 2.6), delta=0.5):
    """World-space billowy clouds on cards; edges facing the moon are lit (density gradient toward the moon)."""
    m = new_mat(name)
    nb = NB(m)
    tc = nb.node("ShaderNodeTexCoord")
    geo, dist, vdir = view_info(nb)
    wp = geo.outputs["Position"]

    def dens_at(pos):
        g = nb.v("ADD", nb.v("MULTIPLY", nb.v("SCALE", pos, scale=wscale), stretch), (seed, seed * 0.37, seed * 0.11))
        warp = nb.noise(g, scale=0.7, detail=3, rough=0.5)
        gw = nb.v("ADD", g, nb.v("SCALE", warp.outputs["Color"], scale=0.9))
        return nb.noise(gw, scale=1.0, detail=9, rough=0.58, lac=2.1).outputs["Fac"]
    d0 = dens_at(wp)
    tomoon = nb.v("NORMALIZE", nb.v("SUBTRACT", tuple(MOON), wp))
    d1 = dens_at(nb.v("ADD", wp, nb.v("SCALE", tomoon, scale=delta)))
    o = nb.sep(tc.outputs["Object"])
    ex = nb.m("SUBTRACT", 1.0, nb.m("POWER", nb.m("ABSOLUTE", o[0]), 3.0), clamp=True)
    ez = nb.m("SUBTRACT", 1.0, nb.m("POWER", nb.m("ABSOLUTE", o[2]), 2.0), clamp=True)
    shape = nb.m("MULTIPLY", ex, ez)
    th = nb.m("ADD", thresh, nb.m("MULTIPLY", nb.m("SUBTRACT", 1.0, shape), 0.3))
    dens = nb.m("MULTIPLY", nb.m("SUBTRACT", d0, th), 5.0 * density, clamp=True)
    edge = nb.m("MULTIPLY", nb.m("SUBTRACT", d0, d1), 14.0, clamp=True)
    near = nb.m("ADD", nb.m("ADD", moon_glow(nb, vdir, 8.0, 0.25), moon_glow(nb, vdir, 60.0, 0.6)), moon_glow(nb, vdir, 700.0, 1.8))
    litf = nb.m("MULTIPLY", nb.m("MULTIPLY", nb.m("ADD", 0.12, edge), near), nb.m("MULTIPLY", nb.m("SUBTRACT", 1.0, nb.m("MULTIPLY", dens, 0.45)), lit_gain))
    col = nb.mix(nb.m("MINIMUM", litf, 1.0), dark, lit)
    em = nb.emit(col, nb.m("ADD", 1.0, nb.m("MULTIPLY", litf, 1.2)))
    tr = nb.node("ShaderNodeBsdfTransparent").outputs[0]
    nb.done(nb.mixsh(dens, tr, em))
    return m


# ---- low fog cards
def mat_fog():
    m = new_mat("BG2_Fog")
    nb = NB(m)
    tc = nb.node("ShaderNodeTexCoord")
    geo, dist, vdir = view_info(nb)
    wpos = geo.outputs["Position"]
    n1 = nb.noise(nb.v("MULTIPLY", wpos, (0.22, 0.22, 0.9)), scale=1.0, detail=6, rough=0.6, distortion=0.6)
    o = nb.sep(tc.outputs["Object"])
    ex = nb.m("SUBTRACT", 1.0, nb.m("POWER", nb.m("ABSOLUTE", o[0]), 4.0), clamp=True)
    ey = nb.m("SUBTRACT", 1.0, nb.m("POWER", nb.m("ABSOLUTE", o[2]), 1.5), clamp=True)
    d = nb.m("MULTIPLY", nb.m("MULTIPLY", nb.m("SUBTRACT", n1.outputs["Fac"], 0.42), 2.5, clamp=True), nb.m("MULTIPLY", ex, ey))
    d = nb.m("MULTIPLY", d, nb.m("MULTIPLY", nb.m("POWER", nb.m("SUBTRACT", 1.0, nb.m("MULTIPLY", nb.m("ADD", o[2], 1.0), 0.5), clamp=True), 1.3), 0.75))
    g = nb.m("ADD", 1.0, nb.m("ADD", moon_glow(nb, vdir, 40.0, 1.0), moon_glow(nb, vdir, 900.0, 3.0)))
    em = nb.emit((0.010, 0.024, 0.055), g)
    tr = nb.node("ShaderNodeBsdfTransparent").outputs[0]
    nb.done(nb.mixsh(d, tr, em))
    return m


# ---- rock (mountains, castle crag); per-object 'tint' via object colour
def mat_rock(name, base=(0.030, 0.034, 0.046), density=0.012, low=0.8, z_fall=5.0, snow=0.0, scale=0.35, snow_z=5.0, rim_gain=0.25):
    m = new_mat(name)
    nb = NB(m)
    geo, dist, vdir = view_info(nb)
    wpos = geo.outputs["Position"]
    nz = nb.sep(geo.outputs["Normal"])[2]
    strata = nb.noise(nb.v("MULTIPLY", wpos, (scale * 0.4, scale * 0.4, scale * 6.0)), scale=1.0, detail=3, rough=0.5)
    det = nb.noise(wpos, scale=scale * 6.0, detail=8, rough=0.62)
    big = nb.noise(wpos, scale=scale * 0.6, detail=3, rough=0.5)
    shade = nb.m("ADD", nb.m("MULTIPLY", strata.outputs["Fac"], 0.7), nb.m("MULTIPLY", det.outputs["Fac"], 0.6))
    shade = nb.m("ADD", shade, nb.m("MULTIPLY", big.outputs["Fac"], 0.5))
    col = nb.mix(nb.m("MULTIPLY", shade, 0.55), (base[0] * 0.55, base[1] * 0.55, base[2] * 0.6, 1), (base[0] * 1.6, base[1] * 1.6, base[2] * 1.5, 1))
    # flat tops: frosty / mossy lighter
    flat = nb.m("MULTIPLY", nb.m("SUBTRACT", nz, 0.72), 4.0, clamp=True)
    if snow > 0:
        zpos = nb.sep(wpos)[2]
        snowm = nb.m("MULTIPLY", nb.m("MULTIPLY", nb.m("SUBTRACT", nz, 0.22), 2.5, clamp=True), nb.m("DIVIDE", nb.m("SUBTRACT", zpos, snow_z), 2.0, clamp=True))
        brk = nb.noise(wpos, scale=scale * 3.0, detail=6, rough=0.7)
        snowm = nb.m("MULTIPLY", snowm, nb.m("MULTIPLY", nb.m("SUBTRACT", brk.outputs["Fac"], 0.38), 4.0, clamp=True))
        col = nb.mix(nb.m("MULTIPLY", snowm, snow), col, (0.30, 0.36, 0.46, 1))
    else:
        col = nb.mix(nb.m("MULTIPLY", flat, 0.5), col, (base[0] * 1.3, base[1] * 1.6, base[2] * 1.4, 1))
    bump = nb.bump(nb.m("ADD", det.outputs["Fac"], nb.m("MULTIPLY", strata.outputs["Fac"], 0.6)), strength=0.45, dist=0.3)
    sh = nb.bsdf(col, rough=0.85, normal=bump, spec=0.35)
    lw = nb.node("ShaderNodeLayerWeight", {"Blend": 0.35, "Normal": bump})
    rim = nb.m("MULTIPLY", nb.m("POWER", lw.outputs["Facing"], 3.0), nb.m("MULTIPLY", nb.m("SUBTRACT", nz, 0.15), 1.4, clamp=True))
    rim = nb.m("MULTIPLY", rim, nb.m("ADD", 0.35, moon_glow(nb, vdir, 30.0, 1.2)))
    sh = nb.add(sh, nb.emit((0.20, 0.32, 0.62), nb.m("MULTIPLY", rim, rim_gain)))
    nb.done(fog_wrap(nb, sh, density=density, low=low, z_fall=z_fall))
    return m


def mat_stone(name, base, rough=0.8, density=0.012, scale=3.0, moss=0.0, spec=0.4, coat=0.0):
    m = new_mat(name)
    nb = NB(m)
    geo, dist, vdir = view_info(nb)
    wpos = geo.outputs["Position"]
    det = nb.noise(wpos, scale=scale, detail=6, rough=0.6)
    blk = nb.node("ShaderNodeTexVoronoi", {"Vector": nb.v("MULTIPLY", wpos, (1.0, 1.0, 2.2))}, feature="F1")
    blk.inputs["Scale"].default_value = scale * 2.2
    rnd = nb.sep(blk.outputs["Color"])[0]
    shade = nb.m("ADD", nb.m("MULTIPLY", det.outputs["Fac"], 0.8), nb.m("MULTIPLY", rnd, 0.5))
    col = nb.mix(nb.m("MULTIPLY", shade, 0.8), tuple(c * 0.6 for c in base) + (1,), tuple(c * 1.45 for c in base) + (1,))
    if moss > 0:
        nz = nb.sep(geo.outputs["Normal"])[2]
        mz = nb.m("MULTIPLY", nb.m("MULTIPLY", nb.m("SUBTRACT", nz, 0.3), 2.0, clamp=True), moss)
        col = nb.mix(mz, col, (0.020, 0.032, 0.022, 1))
    bump = nb.bump(det.outputs["Fac"], strength=0.25, dist=0.02)
    sh = nb.bsdf(col, rough=rough, normal=bump, spec=spec, coat=coat)
    nb.done(fog_wrap(nb, sh, density=density))
    return m


def mat_metal(name, base, rough=0.35, density=0.012):
    m = new_mat(name)
    nb = NB(m)
    sh = nb.bsdf(base, rough=rough, metal=1.0)
    nb.done(fog_wrap(nb, sh, density=density))
    return m


def mat_window():
    m = new_mat("BG2_Window")
    nb = NB(m)
    at = nb.node("ShaderNodeAttribute", attribute_name="win")
    f = at.outputs["Fac"]
    hue = nb.node("ShaderNodeAttribute", attribute_name="hue").outputs["Fac"]
    col = nb.mix(hue, (1.0, 0.42, 0.10, 1), (1.0, 0.72, 0.30, 1))
    em = nb.emit(col, nb.m("MULTIPLY", f, 9.0))
    glass = nb.bsdf((0.01, 0.012, 0.02, 1), rough=0.2)
    sh = nb.mixsh(nb.m("GREATER_THAN", f, 0.01), glass, em)
    nb.done(fog_wrap(nb, sh, density=0.006, maxf=0.6, glow=1.0))
    return m


def mat_lantern():
    m = new_mat("BG2_Lantern")
    nb = NB(m)
    nb.done(nb.emit(WARM, 5.0))
    return m


def mat_path():
    m = new_mat("BG2_Path")
    nb = NB(m)
    geo, dist, vdir = view_info(nb)
    det = nb.noise(geo.outputs["Position"], scale=6.0, detail=4)
    col = nb.mix(det.outputs["Fac"], (0.035, 0.030, 0.028, 1), (0.070, 0.058, 0.048, 1))
    sh = nb.bsdf(col, rough=1.0, spec=0.1)
    sh = nb.add(sh, nb.emit((1.0, 0.45, 0.14), 0.012))
    nb.done(fog_wrap(nb, sh, density=0.012))
    return m


def mat_bark():
    m = new_mat("BG2_DeadTree")
    nb = NB(m)
    geo, dist, vdir = view_info(nb)
    det = nb.noise(nb.v("MULTIPLY", geo.outputs["Position"], (6.0, 6.0, 1.5)), scale=4.0, detail=5)
    col = nb.mix(det.outputs["Fac"], (0.010, 0.010, 0.014, 1), (0.030, 0.028, 0.034, 1))
    bump = nb.bump(det.outputs["Fac"], strength=0.5, dist=0.01)
    sh = nb.bsdf(col, rough=0.75, normal=bump)
    nb.done(fog_wrap(nb, sh, density=0.010, low=0.8, z_fall=1.0))
    return m


def mat_ground():
    m = new_mat("BG2_Ground")
    nb = NB(m)
    geo, dist, vdir = view_info(nb)
    wpos = geo.outputs["Position"]
    det = nb.noise(wpos, scale=1.4, detail=8, rough=0.6)
    patch = nb.noise(wpos, scale=0.35, detail=4, rough=0.6)
    col = nb.mix(det.outputs["Fac"], (0.010, 0.011, 0.012, 1), (0.030, 0.030, 0.032, 1))
    col = nb.mix(nb.m("MULTIPLY", nb.m("SUBTRACT", patch.outputs["Fac"], 0.5), 4.0, clamp=True), col, (0.026, 0.022, 0.016, 1))
    bump = nb.bump(nb.noise(wpos, scale=9.0, detail=6).outputs["Fac"], strength=0.4, dist=0.05)
    sh = nb.bsdf(col, rough=1.0, normal=bump, spec=0.08)
    nb.done(fog_wrap(nb, sh, density=0.010, low=0.8, z_fall=0.8))
    return m


def mat_grass():
    m = new_mat("BG2_DeadGrass")
    nb = NB(m)
    geo, dist, vdir = view_info(nb)
    det = nb.noise(geo.outputs["Position"], scale=3.0, detail=3)
    col = nb.mix(det.outputs["Fac"], (0.020, 0.018, 0.014, 1), (0.055, 0.046, 0.030, 1))
    sh = nb.bsdf(col, rough=0.9, spec=0.1)
    nb.done(fog_wrap(nb, sh, density=0.010, low=0.8, z_fall=0.8))
    return m


def mat_bat():
    m = new_mat("BG2_Bat")
    nb = NB(m)
    sh = nb.bsdf((0.006, 0.005, 0.008, 1), rough=0.6)
    nb.done(sh)
    return m


# ------------------------------------------------------------------ mesh builder (one bmesh, material slots)
class MB:
    def __init__(s, mats):
        s.bm = bmesh.new()
        s.mats = list(mats)
        s.win = s.bm.faces.layers.float.new("win")
        s.hue = s.bm.faces.layers.float.new("hue")

    def mi(s, name):
        return s.mats.index(name)

    def tag(s, faces, mat, smooth=False, win=0.0, hue=0.0):
        i = s.mi(mat)
        for f in faces:
            f.material_index = i
            f.smooth = smooth
            f[s.win] = win
            f[s.hue] = hue

    def lathe(s, prof, seg, mat, M=Matrix(), smooth=True, cap_bottom=True, phase=0.0):
        """prof: list of (r, z) from bottom to top. r==0 at an end = pole."""
        bm = s.bm
        rings = []
        for (r, z) in prof:
            if r <= 1e-6:
                rings.append([bm.verts.new(M @ Vector((0, 0, z)))])
            else:
                rings.append([bm.verts.new(M @ Vector((r * math.cos(phase + 2 * math.pi * k / seg), r * math.sin(phase + 2 * math.pi * k / seg), z))) for k in range(seg)])
        faces = []
        for a, b in zip(rings[:-1], rings[1:]):
            if len(a) == 1 and len(b) == 1:
                continue
            for k in range(seg):
                if len(a) == 1:
                    faces.append(bm.faces.new((a[0], b[k], b[(k + 1) % seg])))
                elif len(b) == 1:
                    faces.append(bm.faces.new((a[k], a[(k + 1) % seg], b[0])))
                else:
                    faces.append(bm.faces.new((a[k], a[(k + 1) % seg], b[(k + 1) % seg], b[k])))
        s.tag(faces, mat, smooth)
        caps = []
        if cap_bottom and len(rings[0]) > 1:
            caps.append(bm.faces.new(list(reversed(rings[0]))))
        if len(rings[-1]) > 1:
            caps.append(bm.faces.new(rings[-1]))
        s.tag(caps, mat, False)
        return faces

    def box(s, center, size, mat, rot=None, win=0.0, M=Matrix()):
        bm = s.bm
        R_ = (rot or Euler((0, 0, 0))).to_matrix().to_4x4()
        T = M @ Matrix.Translation(Vector(center)) @ R_ @ Matrix.Diagonal((size[0], size[1], size[2], 1.0))
        ret = bmesh.ops.create_cube(bm, size=1.0, matrix=T)
        faces = list({f for v in ret["verts"] for f in v.link_faces})
        s.tag(faces, mat, False, win)
        return faces

    def poly_prism(s, pts2d, thick, mat, M=Matrix(), win=0.0):
        """2D outline in local XZ, extruded along Y (front face at y=-thick/2)."""
        bm = s.bm
        front = [bm.verts.new(M @ Vector((x, -thick / 2, z))) for (x, z) in pts2d]
        back = [bm.verts.new(M @ Vector((x, thick / 2, z))) for (x, z) in pts2d]
        faces = [bm.faces.new(list(reversed(front))) if False else bm.faces.new(front), bm.faces.new(list(reversed(back)))]
        n = len(pts2d)
        for k in range(n):
            faces.append(bm.faces.new((front[k], back[k], back[(k + 1) % n], front[(k + 1) % n])))
        s.tag(faces, mat, False, win)
        return faces

    def quad_win(s, center, normal, w, h, mat, lit, hue, pointed=True):
        bm = s.bm
        n = Vector(normal).normalized()
        up = UP - n * n.dot(UP)
        if up.length < 1e-4:
            up = Vector((0, 1, 0))
        up.normalize()
        rt = up.cross(n)
        c = Vector(center) + n * 0.004
        pts = [(-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2 - w * 0.5)]
        if pointed:
            pts += [(0, h / 2)]
        pts += [(-w / 2, h / 2 - w * 0.5)]
        vs = [bm.verts.new(c + rt * x + up * y) for (x, y) in pts]
        f = bm.faces.new(vs)
        s.tag([f], mat, False, lit, hue)
        return f

    def to_object(s, name, shadow=True, coll=None):
        me = bpy.data.meshes.new(name)
        bmesh.ops.recalc_face_normals(s.bm, faces=s.bm.faces)
        s.bm.to_mesh(me)
        s.bm.free()
        for mn in s.mats:
            me.materials.append(MATS[mn])
        return add_obj(name, me, coll, shadow=shadow)


def grid_object(name, nx, ny, fn, mat, shadow=True, smooth=True):
    """Regular grid; fn(i, j) -> world Vector."""
    me = bpy.data.meshes.new(name)
    verts = [fn(i, j) for j in range(ny + 1) for i in range(nx + 1)]
    faces = []
    for j in range(ny):
        for i in range(nx):
            a = j * (nx + 1) + i
            faces.append((a, a + 1, a + nx + 2, a + nx + 1))
    me.from_pydata([tuple(v) for v in verts], [], faces)
    me.materials.append(MATS[mat])
    me.shade_smooth() if smooth else None
    me.validate()
    o = add_obj(name, me, shadow=shadow)
    return o


# ================================================================== materials
mat_sky(); mat_moon(); mat_halo(); mat_fog()
mat_cloud("BG2_Cloud_Wisp", wscale=0.45, density=0.8, thresh=0.5, stretch=(0.6, 0.6, 2.0), seed=3.0, delta=0.8)
mat_cloud("BG2_Cloud_Bank", wscale=0.07, dark=(0.011, 0.020, 0.044), lit=(0.46, 0.56, 0.76), density=1.2, thresh=0.44, stretch=(1.0, 1.0, 2.4), seed=11.0, lit_gain=1.0, delta=5.0)
mat_rock("BG2_Mountain_Far", base=(0.030, 0.036, 0.052), density=0.0055, low=0.9, z_fall=9.0, snow=0.8, scale=0.12, snow_z=5.0, rim_gain=0.35)
mat_rock("BG2_Mountain_Mid", base=(0.024, 0.028, 0.040), density=0.0075, low=1.0, z_fall=5.0, snow=0.35, scale=0.2, snow_z=2.6, rim_gain=0.3)
mat_rock("BG2_Hill_Rock", base=(0.028, 0.030, 0.038), density=0.012, low=1.2, z_fall=2.0, snow=0.0, scale=0.6)
mat_stone("BG2_Castle_Stone", (0.075, 0.070, 0.080), rough=0.85, density=0.011, scale=4.0)
mat_stone("BG2_Castle_Roof", (0.016, 0.022, 0.040), rough=0.38, density=0.011, scale=9.0, spec=0.6, coat=0.3)
mat_metal("BG2_Gold_Finial", (0.95, 0.68, 0.30), rough=0.3, density=0.011)
mat_window(); mat_lantern(); mat_path(); mat_bark(); mat_ground(); mat_bat(); mat_grass()
mat_stone("BG2_Tombstone", (0.060, 0.062, 0.070), rough=0.8, density=0.012, scale=12.0, moss=0.7)
mat_metal("BG2_Iron", (0.05, 0.05, 0.055), rough=0.45, density=0.012)


def mat_banner():
    m = new_mat("BG2_Banner")
    nb = NB(m)
    n = nb.node("ShaderNodeBsdfPrincipled")
    n.inputs["Base Color"].default_value = (0.32, 0.025, 0.04, 1)
    n.inputs["Roughness"].default_value = 0.7
    n.inputs["Sheen Weight"].default_value = 0.6
    n.inputs["Sheen Tint"].default_value = (1.0, 0.5, 0.5, 1)
    tr = nb.node("ShaderNodeBsdfTranslucent", {"Color": (0.5, 0.05, 0.03, 1)}).outputs[0]
    sh = nb.mixsh(0.3, n.outputs[0], tr)
    nb.done(fog_wrap(nb, sh, density=0.011))
    return m


mat_banner()

# ================================================================== sky backdrop + moon + halo
def build_sky():
    # a cylinder segment (radius 230 around P) covering az -70..70, el -4..40
    rad = 230.0
    nx, ny = 64, 16
    def fn(i, j):
        az = -70 + 140 * i / nx
        el = -4 + 44 * j / ny
        v = P + hdir(az) * rad
        v.z = P.z + rad * math.tan(math.radians(el))
        return v
    o = grid_object("BG2_Sky_Backdrop", nx, ny, fn, "BG2_Sky", shadow=False, smooth=False)
    o.visible_diffuse = o.visible_glossy = o.visible_transmission = o.visible_volume_scatter = False
    return o


def build_moon():
    me = bpy.data.meshes.new("BG2_Moon")
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=64, v_segments=32, radius=MOON_R)
    bm.to_mesh(me)
    bm.free()
    me.shade_smooth()
    me.materials.append(MATS["BG2_Moon"])
    o = add_obj("BG2_Moon", me, camera_only=True)
    o.location = MOON
    o.rotation_euler = (0.3, 0.2, yaw_facing(MOON) + 0.6)
    # halo card just behind the moon, facing the cameras
    hs = MOON_R * 4.6
    me2 = bpy.data.meshes.new("BG2_Moon_Halo")
    me2.from_pydata([(-1, 0, -1), (1, 0, -1), (1, 0, 1), (-1, 0, 1)], [], [(0, 1, 2, 3)])
    me2.materials.append(MATS["BG2_Moon_Halo"])
    h = add_obj("BG2_Moon_Halo", me2, camera_only=True)
    h.location = MOON + MOON_DIR * (MOON_R * 1.05)
    h.rotation_euler = (0, 0, yaw_facing(MOON))
    h.scale = (hs, hs, hs)
    return o


build_sky()
build_moon()

# ================================================================== mountains (displaced high-res arcs, ridged noise)
def interp(pts, x):
    if x <= pts[0][0]:
        return pts[0][1]
    for (x0, y0), (x1, y1) in zip(pts[:-1], pts[1:]):
        if x <= x1:
            t = (x - x0) / (x1 - x0)
            t = t * t * (3 - 2 * t) * 0.35 + t * 0.65
            return y0 + (y1 - y0) * t
    return pts[-1][1]


def mountain_layer(name, d0, d1, az0, az1, nx, ny, sil, seed, mat, ridge_scale=0.05, crest=0.62, gully=0.45, jag=0.18):
    dmid = (d0 + d1) * 0.5
    sv = Vector((seed * 17.3, seed * 5.1, seed * 2.7))
    def fn(i, j):
        t = j / ny
        az = az0 + (az1 - az0) * i / nx
        d = d0 + (d1 - d0) * t
        arc = math.radians(az) * dmid
        hs = z_for_el(sil(az), dmid) - 0.0
        # crest position varies along the range
        c = crest + 0.18 * noise.noise(Vector((arc * 0.02, 0.0, seed)) + sv)
        if t < c:
            f = (t / c) ** 1.35
        else:
            f = 1.0 - ((t - c) / (1 - c)) ** 1.6 * 0.85
        # ridges running down-slope (stretched along depth), domain-warped, sharpened creases
        wv = noise.noise(Vector((arc * ridge_scale * 0.5, d * ridge_scale * 0.25, 5.0)) + sv)
        q = Vector((arc * ridge_scale + wv * 0.9, d * ridge_scale * 0.3, 0.0)) + sv
        rm = noise.ridged_multi_fractal(q, 1.0, 2.1, 7, 1.0, 2.4)
        rm = max(0.0, min(1.0, rm / 1.9)) ** 1.5
        # sharp spikes along the crest line (alpine peaks)
        sp = noise.ridged_multi_fractal(Vector((arc * ridge_scale * 2.2 + wv * 0.5, 0.0, 3.3)) + sv, 1.0, 2.0, 5, 1.0, 2.2)
        sp = max(0.0, min(1.0, sp / 1.9)) ** 2.0
        jg = noise.fractal(Vector((arc * ridge_scale * 5, d * ridge_scale * 2.5, 1.3)) + sv, 0.7, 2.2, 6)
        crestf = max(0.0, 1.0 - abs(t - c) / 0.35)
        h = hs * f * ((1 - gully) + gully * rm) * (0.8 + 0.6 * sp * crestf) + hs * jag * jg * f
        p = P + hdir(az) * d
        p.z = h
        return p
    return grid_object(name, nx, ny, fn, mat)


ma = MOON_AZ
# far peaks: tall jagged ranges on both sides, a deep V under the moon
SIL_FAR = [(-75, 2.0), (-55, 3.0), (-44, 4.4), (-38, 3.3), (-33, 5.4), (-29, 4.1), (-24, 5.9), (-20.5, 4.4), (-17, 4.9), (-13.5, 3.4),
           (ma - 4.5, 2.0), (ma - 2.2, 0.75), (ma + 0.5, 0.6), (ma + 3.0, 1.1), (ma + 6.5, 2.4), (5, 3.2), (8, 2.6), (12, 4.0), (16, 3.2),
           (20, 5.0), (24, 4.0), (28, 5.6), (32, 4.4), (37, 5.2), (43, 3.6), (55, 4.6), (75, 2.4)]
SIL_MID = [(-75, 1.4), (-58, 2.4), (-48, 1.7), (-40, 3.0), (-35, 2.2), (-30, 3.1), (-26, 2.0), (-22, 2.7), (-18, 1.8), (-14, 2.2),
           (ma - 4.2, 1.0), (ma - 1.5, 0.45), (ma + 1.5, 0.4), (ma + 4.0, 0.9), (2.5, 1.6), (6, 1.3), (10, 2.1), (15, 1.6),
           (19, 2.7), (23, 2.0), (27, 3.1), (31, 2.3), (36, 2.9), (42, 2.0), (52, 2.6), (75, 1.5)]
mountain_layer("BG2_Mountains_Far", 150, 190, -75, 75, 560, 70, lambda a: interp(SIL_FAR, a), 1, "BG2_Mountain_Far", ridge_scale=0.045, gully=0.55, jag=0.22)
mountain_layer("BG2_Mountains_Mid", 92, 122, -75, 75, 560, 70, lambda a: interp(SIL_MID, a), 2, "BG2_Mountain_Mid", ridge_scale=0.07, gully=0.5, jag=0.26)

# ================================================================== near ground (rolling hills, valley, graveyard mound)
GY_AZ, GY_D = -4.0, 17.0           # graveyard centre
CH_AZ, CH_D = 12.5, 46.0           # castle hill centre


def ground_z(az, d):
    arc = math.radians(az) * d
    base = -0.35 + 0.012 * (d - 9.0)
    rolling = 0.35 * noise.fractal(Vector((arc * 0.08, d * 0.08, 4.2)), 0.7, 2.0, 4) * min(1.0, (d - 9) / 10)
    # graveyard mound
    g = gpos(GY_AZ, GY_D, 0)
    p = gpos(az, d, 0)
    rr = ((p - g).length / 5.5)
    mound = 0.95 * math.exp(-rr * rr * 1.4)
    # hills beyond (L3): rolling forested hills at 28-42 m, lower under the moon
    hl = max(0.0, 1 - abs(d - 34) / 14.0)
    hill_sil = interp([(-75, 1.6), (-40, 1.3), (-25, 1.5), (-15, 1.05), (ma - 3, 0.35), (ma + 3, 0.30), (3, 0.6), (7, 0.9), (20, 1.1), (35, 1.4), (75, 1.0)], az)
    hh = z_for_el(hill_sil, 34) * hl ** 0.8 * (0.75 + 0.25 * noise.noise(Vector((arc * 0.1, d * 0.05, 9.0))))
    return base + rolling + mound + max(0.0, hh)


def build_ground():
    nx, ny = 360, 150
    def fn(i, j):
        az = -70 + 140 * i / nx
        d = 8.5 + (110 - 8.5) * (j / ny) ** 1.8
        p = P + hdir(az) * d
        p.z = ground_z(az, d)
        return p
    return grid_object("BG2_Ground", nx, ny, fn, "BG2_Ground", shadow=False)


build_ground()

# ================================================================== castle crag + castle
CH_POS = gpos(CH_AZ, CH_D, 0.0)
CH_YAW = yaw_facing(CH_POS)
CH_M = Matrix.Translation(CH_POS) @ Matrix.Rotation(CH_YAW, 4, "Z")
PLATEAU = z_for_el(2.05, CH_D)           # world z of the castle floor
CASTLE_SCALE = 0.80


def crag_h(x, y):
    """Local crag height (local x right, y away from camera). Plateau top = PLATEAU."""
    # plateau footprint (elliptic) + a pinnacle for the barbican on the left
    r = math.sqrt((x / 4.4) ** 2 + (y / 3.2) ** 2)
    ang = math.atan2(y, x)
    wob = 0.18 * noise.noise(Vector((math.cos(ang) * 2, math.sin(ang) * 2, 3.3))) + 0.08 * noise.noise(Vector((math.cos(ang) * 7, math.sin(ang) * 7, 1.1)))
    rr = r * (1 + wob)
    top = PLATEAU
    if rr < 0.92:
        h = top - 0.08 * rr
    else:
        k = (rr - 0.92) / 1.9
        h = top - (top + 0.6) * (1 - math.exp(-k * 2.2)) - 0.25 * k
    # barbican pinnacle (left, slightly forward)
    px, py = -6.0 * CASTLE_SCALE / 0.8, -0.6
    pr = math.sqrt(((x - px) / 1.0) ** 2 + ((y - py) / 0.9) ** 2)
    pin = (top - 0.45) - (top + 0.6) * min(1.0, max(0.0, (pr - 0.55)) * 0.75) ** 0.8
    h = max(h, pin)
    # cliffs: terrace strata + ridged crags
    q = Vector((x * 0.9, y * 0.9, 7.7))
    cr = noise.ridged_multi_fractal(q, 0.9, 2.1, 5, 1.0, 2.0) / 1.6
    steep = max(0.0, min(1.0, (top - h) / 1.2))
    step = 0.32
    terr = math.floor(h / step) * step + step * (max(0.0, (h / step) % 1.0 - 0.6) / 0.4) ** 2
    h = h * (1 - 0.55 * steep) + terr * 0.55 * steep + 0.35 * cr * steep
    return h


def build_crag():
    nx, ny = 260, 160
    X0, X1, Y0, Y1 = -9.5, 7.5, -7.0, 6.0
    def fn(i, j):
        x = X0 + (X1 - X0) * i / nx
        y = Y0 + (Y1 - Y0) * j / ny
        return CH_M @ Vector((x, y, crag_h(x, y)))
    return grid_object("BG2_Castle_Crag", nx, ny, fn, "BG2_Hill_Rock")


build_crag()

CM = ["BG2_Castle_Stone", "BG2_Castle_Roof", "BG2_Window", "BG2_Gold_Finial", "BG2_Lantern"]
mb = MB(CM)
rng = random.Random(429)
S = CASTLE_SCALE
LM = CH_M @ Matrix.Translation((0, 0, PLATEAU - 0.05)) @ Matrix.Diagonal((S, S, S, 1))


def windows_on_cylinder(cx, cy, r, z0, z1, rows, per_row=5, w=0.085, h=0.15, spread=70):
    for k in range(rows):
        z = z0 + (z1 - z0) * (k + 0.5) / rows
        for a in range(per_row):
            ang = math.radians(-90 + (a - (per_row - 1) / 2) * (2 * spread / max(1, per_row - 1)) + rng.uniform(-6, 6))
            if rng.random() < 0.18:
                continue
            nrm = Vector((math.cos(ang), math.sin(ang), 0))
            c = Vector((cx, cy, z)) + nrm * r
            lit = rng.random() < 0.72
            mb.quad_win(LM @ c, (LM.to_3x3() @ nrm).normalized(), w * S, h * S, "BG2_Window", rng.uniform(0.55, 1.0) if lit else 0.0, rng.random())


def merlons_ring(cx, cy, r, z, n, w=0.07, h=0.09, d=0.06):
    for k in range(n):
        ang = 2 * math.pi * k / n
        c = (cx + r * math.cos(ang), cy + r * math.sin(ang), z + h / 2)
        mb.box(c, (w, d, h), "BG2_Castle_Stone", Euler((0, 0, ang + math.pi / 2)), M=LM)


def merlons_line(p0, p1, z, step=0.15, w=0.075, h=0.09, d=0.07):
    p0, p1 = Vector(p0), Vector(p1)
    L = (p1 - p0).length
    n = max(2, int(L / step))
    ang = math.atan2(p1.y - p0.y, p1.x - p0.x)
    for k in range(n + 1):
        if k % 2:
            continue
        c = p0 + (p1 - p0) * (k / n)
        mb.box((c.x, c.y, z + h / 2), (w, d, h), "BG2_Castle_Stone", Euler((0, 0, ang)), M=LM)


def spire(cx, cy, r, z, hgt, seg=16, flare=1.28, finial=True, gold=True):
    prof = [(r * flare, z), (r * flare * 0.93, z + hgt * 0.06)]
    for k in range(1, 9):
        t = k / 9
        prof.append((r * flare * 0.93 * (1 - t) ** 1.45, z + hgt * (0.06 + 0.94 * t)))
    prof.append((0.0, z + hgt))
    mb.lathe(prof, seg, "BG2_Castle_Roof", LM, smooth=True)
    if finial:
        fz = z + hgt
        prof2 = [(0.012, fz - 0.02), (0.012, fz + 0.04), (0.03, fz + 0.06), (0.032, fz + 0.075), (0.028, fz + 0.09), (0.006, fz + 0.1), (0.005, fz + 0.24), (0.0, fz + 0.27)]
        mb.lathe(prof2, 8, "BG2_Gold_Finial" if gold else "BG2_Castle_Roof", LM, smooth=True)


def round_tower(cx, cy, r, h, roof_h, seg=20, rows=None, crenel=True, roof=True, balcony=True, wins=True):
    prof = [(r * 1.12, -0.6), (r * 1.08, 0.0), (r, 0.12), (r * 0.97, h - 0.22)]
    if balcony:
        prof += [(r * 1.13, h - 0.16), (r * 1.16, h - 0.02), (r * 1.16, h)]
    else:
        prof += [(r * 0.97, h)]
    mb.lathe(prof, seg, "BG2_Castle_Stone", LM, smooth=True)
    if crenel and balcony:
        merlons_ring(cx, cy, r * 1.12, h, max(8, int(r * 40)), w=0.06, h=0.08, d=0.05) if False else None
    if wins:
        windows_on_cylinder(cx, cy, r * 0.97, 0.35, h - 0.35, rows or max(2, int(h / 0.45)), per_row=3 if r < 0.25 else 5)
    if roof:
        spire(cx, cy, r, h, roof_h, seg=seg)


def place_round_tower(cx, cy, r, h, roof_h, **kw):
    # build lathes in local tower coordinates via translated LM
    global LM
    keep = LM
    LM = keep @ Matrix.Translation((cx, cy, 0))
    round_tower(0, 0, r, h, roof_h, **kw)
    if kw.get("balcony", True) and kw.get("crenel", True):
        merlons_ring(0, 0, r * 1.12, h, max(10, int(r * 44)), w=0.065, h=0.085, d=0.05)
    LM = keep


def square_tower(cx, cy, wx, wy, h, roof_h, bartizans=True, pyramid=True):
    global LM
    mb.box((cx, cy, (h - 0.6) / 2), (wx, wy, h + 0.6), "BG2_Castle_Stone", M=LM)
    # corbel band
    mb.box((cx, cy, h - 0.08), (wx * 1.12, wy * 1.12, 0.16), "BG2_Castle_Stone", M=LM)
    merlons_line((cx - wx * 0.56, cy - wy * 0.56), (cx + wx * 0.56, cy - wy * 0.56), h)
    merlons_line((cx - wx * 0.56, cy + wy * 0.56), (cx + wx * 0.56, cy + wy * 0.56), h)
    merlons_line((cx - wx * 0.56, cy - wy * 0.56), (cx - wx * 0.56, cy + wy * 0.56), h)
    merlons_line((cx + wx * 0.56, cy - wy * 0.56), (cx + wx * 0.56, cy + wy * 0.56), h)
    # windows on the front face (pairs per floor)
    floors = int((h - 0.4) / 0.42)
    for k in range(floors):
        z = 0.35 + k * 0.42
        for xo in (-wx * 0.22, wx * 0.22):
            lit = rng.random() < 0.7
            mb.quad_win(LM @ Vector((cx + xo, cy - wy / 2, z)), (LM.to_3x3() @ Vector((0, -1, 0))).normalized(), 0.08 * S, 0.16 * S, "BG2_Window", rng.uniform(0.5, 1) if lit else 0.0, rng.random())
        if rng.random() < 0.6:
            lit = rng.random() < 0.6
            for side in (-1, 1):
                mb.quad_win(LM @ Vector((cx + side * wx / 2, cy - wy * 0.15, z)), (LM.to_3x3() @ Vector((side, 0, 0))).normalized(), 0.07 * S, 0.14 * S, "BG2_Window", rng.uniform(0.4, 1) if lit and rng.random() < 0.6 else 0.0, rng.random())
    if pyramid:
        # octagonal steep spire rising from inside the parapet
        keep = LM
        LM = keep @ Matrix.Translation((cx, cy, 0))
        spire(0, 0, min(wx, wy) * 0.42, h + 0.02, roof_h, seg=8, flare=1.15)
        LM = keep
    if bartizans:
        for sx in (-1, 1):
            for sy in (-1, 1):
                bx, by = cx + sx * wx * 0.56, cy + sy * wy * 0.56
                keep = LM
                LM = keep @ Matrix.Translation((bx, by, 0))
                mb.lathe([(0.0, h - 0.55), (0.04, h - 0.5), (0.09, h - 0.34), (0.1, h - 0.2), (0.1, h + 0.08)], 12, "BG2_Castle_Stone", LM)
                spire(0, 0, 0.1, h + 0.08, 0.42, seg=12, flare=1.2, gold=False, finial=False)
                LM = keep


def curtain_wall(p0, p1, h, thick=0.2, wins=0):
    p0, p1 = Vector(p0), Vector(p1)
    c = (p0 + p1) / 2
    L = (p1 - p0).length
    ang = math.atan2(p1.y - p0.y, p1.x - p0.x)
    mb.box((c.x, c.y, (h - 0.7) / 2), (L, thick, h + 0.7), "BG2_Castle_Stone", Euler((0, 0, ang)), M=LM)
    off = Vector((-math.sin(ang), math.cos(ang))) * (thick * 0.5)
    merlons_line(p0 - off, p1 - off, h, step=0.13)
    for k in range(wins):
        t = (k + 0.5) / wins
        q = p0 + (p1 - p0) * t - off
        lit = rng.random() < 0.55
        mb.quad_win(LM @ Vector((q.x, q.y, h * 0.55)), (LM.to_3x3() @ Vector((-off.x, -off.y, 0))).normalized(), 0.06 * S, 0.1 * S, "BG2_Window", rng.uniform(0.4, 0.9) if lit else 0.0, rng.random())


def great_hall(x0, x1, yf, yb, h, roof_h):
    global LM
    cx, cy = (x0 + x1) / 2, (yf + yb) / 2
    wx, wy = x1 - x0, yb - yf
    mb.box((cx, cy, (h - 0.6) / 2), (wx, wy, h + 0.6), "BG2_Castle_Stone", M=LM)
    # steep gable roof (prism along x)
    pts = [(-wy / 2 - 0.06, h), (wy / 2 + 0.06, h), (0, h + roof_h)]
    me_pts = [(y, z) for (y, z) in pts]
    M = LM @ Matrix.Translation((cx, cy, 0)) @ Matrix.Rotation(math.radians(90), 4, "Z")
    mb.poly_prism(me_pts, wx + 0.1, "BG2_Castle_Roof", M)
    # ridge crest spikes + small fleche (slender spire) in the middle
    for k in range(9):
        x = x0 + wx * (k + 0.5) / 9
        mb.box((x, cy, h + roof_h + 0.03), (0.02, 0.02, 0.07), "BG2_Gold_Finial", M=LM)
    keep_m = LM @ Matrix.Translation((cx + wx * 0.12, cy, h + roof_h * 0.85))
    mb.lathe([(0.08, 0), (0.08, 0.22), (0.1, 0.24)], 8, "BG2_Castle_Stone", keep_m)
    keep = LM
    LM = keep_m
    spire(0, 0, 0.08, 0.24, 0.85, seg=8, flare=1.2)
    LM = keep
    # flying buttresses on the front: pier with pinnacle + sloped arch to the wall top
    nb_ = 5
    for k in range(nb_):
        x = x0 + wx * (k + 0.5) / nb_
        py = yf - 0.42
        mb.box((x, py, (h * 0.78 - 0.6) / 2), (0.1, 0.14, h * 0.78 + 0.6), "BG2_Castle_Stone", M=LM)
        keep = LM
        LM = keep @ Matrix.Translation((x, py, 0))
        spire(0, 0, 0.055, h * 0.78, 0.32, seg=6, flare=1.1, finial=False)
        LM = keep
        # arch: thin slanted slab from pier top to wall
        y_mid = (py + yf) / 2
        z0, z1 = h * 0.62, h * 0.9
        L = math.hypot(yf - py, z1 - z0)
        ang = math.atan2(z1 - z0, yf - py)
        mb.box((x, y_mid, (z0 + z1) / 2), (0.05, L, 0.07), "BG2_Castle_Stone", Euler((ang, 0, 0)), M=LM)
        # tall lancet window between buttresses
        if k < nb_ - 1:
            xw = x + wx / nb_ / 2
            lit = rng.random() < 0.85
            mb.quad_win(LM @ Vector((xw, yf, h * 0.55)), (LM.to_3x3() @ Vector((0, -1, 0))).normalized(), 0.1 * S, 0.42 * S, "BG2_Window", rng.uniform(0.7, 1) if lit else 0.0, rng.random() * 0.5)
    # rose window on the visible gable end (left end faces the camera a bit)
    mb.quad_win(LM @ Vector((x0, cy, h + roof_h * 0.3)), (LM.to_3x3() @ Vector((-1, 0, 0))).normalized(), 0.16 * S, 0.16 * S, "BG2_Window", 0.9, 0.2, pointed=False)


def gatehouse(cx, cy):
    for sx in (-0.34, 0.34):
        place_round_tower(cx + sx, cy, 0.2, 1.25, 0.7, seg=14, rows=2)
    mb.box((cx, cy + 0.05, 0.25), (0.5, 0.3, 1.7), "BG2_Castle_Stone", M=LM)
    merlons_line((cx - 0.25, cy - 0.11), (cx + 0.25, cy - 0.11), 1.1, step=0.12)
    # glowing gate arch
    mb.quad_win(LM @ Vector((cx, cy - 0.1, 0.24)), (LM.to_3x3() @ Vector((0, -1, 0))).normalized(), 0.22 * S, 0.4 * S, "BG2_Window", 1.0, 0.1)


def bridge(x0, x1, y, z_deck, piers, z_bottom=-2.4):
    """Arched viaduct along local x from x0 to x1."""
    pts = [(x0, z_deck + 0.1), (x0, z_bottom)]
    span = (x1 - x0) / piers
    pw = 0.16
    for k in range(piers):
        a = x0 + k * span + (pw / 2 if k else 0.0)
        b = x0 + (k + 1) * span - (pw / 2 if k < piers - 1 else 0.0)
        top = z_deck - 0.12
        spring = top - (b - a) * 0.6
        pts.append((a, z_bottom) if k == 0 else (a, z_bottom))
        pts.append((a, spring))
        n = 10
        for q in range(1, n):
            t = q / n
            ang = math.pi * t
            # pointed (gothic) arch: two arcs
            xa = a + (b - a) * t
            za = spring + (b - a) * 0.6 * math.sin(ang) ** 0.8
            pts.append((xa, za))
        pts.append((b, spring))
        pts.append((b, z_bottom))
    pts.append((x1, z_bottom))
    pts.append((x1, z_deck + 0.1))
    # remove consecutive duplicates
    clean = []
    for p_ in pts:
        if not clean or (abs(clean[-1][0] - p_[0]) > 1e-4 or abs(clean[-1][1] - p_[1]) > 1e-4):
            clean.append(p_)
    M = LM @ Matrix.Translation((0, y, 0))
    mb.poly_prism(list(reversed(clean)), 0.26, "BG2_Castle_Stone", M)
    # parapets + lanterns
    for side in (-0.15, 0.15):
        mb.box(((x0 + x1) / 2, y + side, z_deck + 0.16), (x1 - x0, 0.04, 0.12), "BG2_Castle_Stone", M=LM)
    for k in range(piers + 1):
        x = x0 + k * span
        mb.box((x, y - 0.15, z_deck + 0.3), (0.025, 0.025, 0.3), "BG2_Castle_Stone", M=LM)
        mb.box((x, y - 0.15, z_deck + 0.47), (0.05, 0.05, 0.06), "BG2_Lantern", M=LM)


# ---- castle layout (local units before CASTLE_SCALE; front = -y = toward the cameras)
# curtain walls around the plateau front
curtain_wall((-3.1, -1.25), (-1.0, -1.45), 0.95, wins=3)
curtain_wall((-0.2, -1.45), (2.5, -1.15), 0.95, wins=3)
curtain_wall((-3.1, -1.25), (-3.0, 1.4), 0.9)
curtain_wall((2.5, -1.15), (2.9, 1.2), 0.9)
gatehouse(-0.6, -1.5)
# corner towers
place_round_tower(-3.1, -1.25, 0.38, 1.9, 1.15)
place_round_tower(2.55, -1.15, 0.34, 1.6, 1.0)
place_round_tower(2.95, 1.1, 0.28, 2.1, 1.0)
place_round_tower(-3.0, 1.35, 0.3, 2.3, 1.05)
# great hall (cathedral) with flying buttresses
great_hall(0.0, 2.2, -0.55, 0.35, 1.55, 0.95)
place_round_tower(2.2, -0.55, 0.16, 1.85, 0.7, seg=12, rows=3, balcony=False, crenel=False)
# central keep (tallest)
square_tower(-1.2, 0.1, 0.95, 0.95, 2.75, 1.25)
# slender high tower behind
place_round_tower(0.6, 0.95, 0.27, 3.0, 1.05)
place_round_tower(-2.1, 0.75, 0.24, 2.5, 0.95)
place_round_tower(1.6, 1.3, 0.22, 2.6, 0.9)
place_round_tower(-0.2, 1.6, 0.3, 2.2, 0.9, rows=2)
# small turrets on the keep sides
place_round_tower(-1.85, -0.5, 0.15, 2.1, 0.6, seg=12, rows=3, balcony=False, crenel=False)
# bridge to the barbican pinnacle on the left + barbican tower
bridge(-6.9, -3.4, -0.75, -0.2, 3)
place_round_tower(-7.5, -0.6, 0.3, 1.6, 0.95, rows=2)


def house(cx, cy, w, d, h, roof_h, yaw=0.0):
    """Little steep-roofed house on the crag below the walls (castle units, z from the crag)."""
    X, Y = cx * S, cy * S
    zc = (crag_h(X, Y) - (PLATEAU - 0.05)) / S
    M = LM @ Matrix.Translation((cx, cy, zc - 0.15)) @ Matrix.Rotation(yaw, 4, "Z")
    mb.box((0, 0, (h + 0.15) / 2 - 0.2), (w, d, h + 0.55), "BG2_Castle_Stone", M=M)
    mb.poly_prism([(-w / 2 - 0.05, h), (w / 2 + 0.05, h), (0, h + roof_h)], d + 0.08, "BG2_Castle_Roof", M)
    mb.box((w * 0.3, d * 0.2, h + roof_h * 0.7), (0.07, 0.07, roof_h * 0.8), "BG2_Castle_Stone", M=M)   # chimney
    for xo in ((-w * 0.22, w * 0.22) if w > 0.45 else (0.0,)):
        lit = rng.random() < 0.75
        mb.quad_win(M @ Vector((xo, -d / 2, h * 0.5)), (M.to_3x3() @ Vector((0, -1, 0))).normalized(), 0.07 * S, 0.1 * S,
                    "BG2_Window", rng.uniform(0.6, 1.0) if lit else 0.0, rng.random(), pointed=False)


HOUSES = [(-2.4, -1.95, 0.5, 0.35, 0.4, 0.4, 0.2), (-1.6, -2.15, 0.42, 0.34, 0.36, 0.38, -0.15), (0.35, -2.1, 0.55, 0.36, 0.42, 0.42, 0.1),
          (1.2, -1.9, 0.4, 0.32, 0.38, 0.36, -0.25), (2.0, -1.85, 0.46, 0.34, 0.4, 0.4, 0.3), (-0.7, -2.55, 0.48, 0.34, 0.36, 0.38, 0.05),
          (0.95, -2.6, 0.42, 0.32, 0.34, 0.36, -0.1), (2.7, -2.2, 0.38, 0.3, 0.34, 0.34, 0.4), (-2.9, -2.4, 0.4, 0.3, 0.34, 0.34, -0.3),
          (-0.1, -3.05, 0.4, 0.3, 0.32, 0.34, 0.2), (1.8, -2.9, 0.4, 0.3, 0.32, 0.34, -0.2)]
for hs_ in HOUSES:
    house(*hs_)


def pennant(cx, cy, ztop, length=0.42, height=0.15, phase=0.0):
    """Waving swallow-tail pennant flying toward camera-right from a finial tip."""
    bm = mb.bm
    n = 8
    top, bot = [], []
    for k in range(n + 1):
        t = k / n
        x = length * t
        wave = 0.05 * math.sin(t * 5.0 + phase) * t
        hh = height * (1 - 0.75 * t)
        top.append(bm.verts.new(LM @ Vector((cx + x, cy + wave, ztop - 0.02 - 0.03 * t))))
        bot.append(bm.verts.new(LM @ Vector((cx + x, cy + wave, ztop - 0.02 - hh - 0.03 * t))))
    faces = [bm.faces.new((top[k], top[k + 1], bot[k + 1], bot[k])) for k in range(n)]
    mb.tag(faces, "BG2_Banner", smooth=True)
    # swallow-tail notch
    mid = bm.verts.new(LM @ Vector((cx + length * 0.86, cy, ztop - 0.02 - height * 0.18 - 0.03)))
    mb.tag([bm.faces.new((top[-1], mid, bot[-1]))], "BG2_Banner")


mb.mats.append("BG2_Banner")
for (cx, cy, zt, ph) in [(-1.2, 0.1, 2.75 + 0.02 + 1.25 + 0.25, 0.0), (0.6, 0.95, 3.0 + 1.05 + 0.25, 1.3), (-3.1, -1.25, 1.9 + 1.15 + 0.25, 2.1),
                         (2.95, 1.1, 2.1 + 1.0 + 0.25, 0.7), (-7.5, -0.6, 1.6 + 0.95 + 0.25, 1.8)]:
    pennant(cx, cy, zt, phase=ph)
castle = mb.to_object("BG2_Castle")

# ================================================================== winding lit path down the crag
def build_path():
    mbp = MB(["BG2_Path", "BG2_Lantern", "BG2_Castle_Stone"])
    # local crag coords; from the barbican (left pinnacle) down in switchbacks toward the cameras/left
    ctrl = []
    bx = -6.0 * CASTLE_SCALE / 0.8 * 0.8 / CASTLE_SCALE * CASTLE_SCALE
    pts = [(-6.0, -1.3), (-5.2, -2.0), (-3.6, -2.4), (-2.4, -2.9), (-3.4, -3.6), (-5.4, -4.0), (-6.6, -4.6), (-5.6, -5.4), (-3.8, -6.0), (-2.6, -6.6)]
    # smooth (Catmull-Rom)
    def cr(p0, p1, p2, p3, t):
        return 0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3)
    V = [Vector(p) for p in pts]
    V = [V[0]] + V + [V[-1]]
    path = []
    for k in range(1, len(V) - 2):
        for q in range(12):
            path.append(cr(V[k - 1], V[k], V[k + 1], V[k + 2], q / 12))
    path.append(V[-2])
    w = 0.18
    prev_l = prev_r = None
    lantern_every = 9
    for k, p in enumerate(path):
        nxt = path[min(k + 1, len(path) - 1)] - path[max(k - 1, 0)]
        nxt.normalize()
        side = Vector((-nxt.y, nxt.x))
        l2, r2 = p + side * w, p - side * w
        zl = crag_ground(l2.x, l2.y) + 0.04
        zr = crag_ground(r2.x, r2.y) + 0.04
        vl = mbp.bm.verts.new(CH_M @ Vector((l2.x, l2.y, zl)))
        vr = mbp.bm.verts.new(CH_M @ Vector((r2.x, r2.y, zr)))
        if prev_l:
            f = mbp.bm.faces.new((prev_l, prev_r, vr, vl))
            mbp.tag([f], "BG2_Path")
        prev_l, prev_r = vl, vr
        if k % lantern_every == 4:
            q = p + side * (w + 0.06)
            zq = crag_ground(q.x, q.y)
            mbp.box(CH_M @ Vector((q.x, q.y, zq + 0.12)), (0.02, 0.02, 0.24), "BG2_Castle_Stone")
            mbp.lathe([(0.0, zq + 0.22), (0.045, zq + 0.25), (0.045, zq + 0.31), (0.0, zq + 0.34)], 8, "BG2_Lantern", CH_M @ Matrix.Translation((q.x, q.y, 0)))
    return mbp.to_object("BG2_Castle_Path", shadow=False)


def crag_ground(x, y):
    # crag surface, or the valley ground beyond it
    p = CH_M @ Vector((x, y, 0))
    az, el, d = azel(p)
    dh = (p - P)
    dh.z = 0
    return max(crag_h(x, y), ground_z(az, dh.length))


build_path()

# ================================================================== dead trees (twisted curve trees -> mesh)
def make_tree(name, base, height, seed, lean=(0.0, 0.0), spread=1.0, gnarl=1.0, depth=4, roots=True):
    rng_ = random.Random(seed)
    cu = bpy.data.curves.new(name, "CURVE")
    cu.dimensions = "3D"
    cu.bevel_depth = height * 0.05
    cu.bevel_resolution = 1
    cu.use_fill_caps = True
    cu.resolution_u = 3

    def rvec():
        return Vector((rng_.uniform(-1, 1), rng_.uniform(-1, 1), rng_.uniform(-1, 1)))

    def branch(start, d, length, r0, lvl):
        n = max(4, int(5 + length / height * 10))
        pts = []
        p = start.copy()
        d = d.normalized()
        curl = rvec() * 0.25 * gnarl
        for k in range(n + 1):
            t = k / n
            pts.append((p.copy(), r0 * (1 - 0.82 * t ** 0.9) + 0.004))
            d = (d + rvec() * 0.28 * gnarl + curl * 0.6 + Vector((0, 0, 0.05 if lvl < depth else -0.02))).normalized()
            p = p + d * (length / n)
        sp = cu.splines.new("NURBS")
        sp.points.add(len(pts) - 1)
        for i, (q, r) in enumerate(pts):
            sp.points[i].co = (q.x, q.y, q.z, 1.0)
            sp.points[i].radius = r / (height * 0.05)
        sp.order_u = 3
        sp.use_endpoint_u = True
        if lvl <= 0:
            return
        kids = rng_.randint(2, 3) if lvl > 1 else rng_.randint(1, 3)
        for c in range(kids):
            t = rng_.uniform(0.35, 0.95) if c else rng_.uniform(0.75, 0.98)
            idx = min(len(pts) - 1, int(t * n))
            q, rr = pts[idx]
            hd = Vector((d.x, d.y, 0))
            out = Vector((rng_.uniform(-1, 1), rng_.uniform(-1, 1), 0)).normalized()
            nd = (d * 0.55 + out * spread * rng_.uniform(0.6, 1.1) + Vector((0, 0, rng_.uniform(-0.1, 0.45)))).normalized()
            branch(q, nd, length * rng_.uniform(0.5, 0.72), rr * 0.82, lvl - 1)

    trunk_d = Vector((lean[0], lean[1], 1.0))
    branch(Vector((0, 0, -0.05 * height)), trunk_d, height * 0.48, height * 0.05, depth)
    if roots:
        for k in range(rng_.randint(3, 5)):
            a = rng_.uniform(0, 2 * math.pi)
            sp = cu.splines.new("NURBS")
            pts = [Vector((0, 0, 0.08 * height)), Vector((math.cos(a) * 0.08, math.sin(a) * 0.08, 0.02)) * height,
                   Vector((math.cos(a) * 0.16, math.sin(a) * 0.16, -0.03)) * height, Vector((math.cos(a) * 0.24, math.sin(a) * 0.24, -0.08)) * height]
            sp.points.add(len(pts) - 1)
            for i, q in enumerate(pts):
                sp.points[i].co = (q.x, q.y, q.z, 1)
                sp.points[i].radius = (0.75 - i * 0.22)
            sp.order_u = 3
            sp.use_endpoint_u = True
    tmp = bpy.data.objects.new(name + "_crv", cu)
    sc.collection.objects.link(tmp)
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg))
    me.name = name
    bpy.data.objects.remove(tmp, do_unlink=True)
    bpy.data.curves.remove(cu)
    me.materials.clear()
    me.materials.append(MATS["BG2_DeadTree"])
    o = add_obj(name, me, shadow=False)
    o.location = base
    o.rotation_euler = (0, 0, rng_.uniform(0, 6.28))
    return o


TREES = []
trng = random.Random(77)
# feature trees (near, framing the sides), graveyard tree, forest on the far hills
feature = [(-24.0, 13.0, 2.3, 0.8), (-17.5, 15.0, 1.7, 0.6), (21.0, 13.5, 2.1, -0.7), (27.0, 12.0, 2.5, -0.5), (1.5, 18.5, 1.6, 0.3)]
for k, (az, d, hgt, lean) in enumerate(feature):
    z = ground_z(az, d) - 0.05
    TREES.append(make_tree(f"BG2_DeadTree_Big_{k}", gpos(az, d, z), hgt, 100 + k, lean=(lean * 0.25, 0.0), spread=1.15, gnarl=1.15, depth=4))
k = 0
for i in range(140):
    az = trng.uniform(-70, 70)
    if abs(az - MOON_AZ) < MOON_ANG * 1.4 or 6 < az < 19:
        continue
    d = trng.uniform(26, 44)
    z = ground_z(az, d)
    hgt = trng.uniform(0.8, 1.6) * (d / 34.0)
    TREES.append(make_tree(f"BG2_DeadTree_Forest_{k}", gpos(az, d, z - 0.05), hgt, 300 + i, lean=(trng.uniform(-0.2, 0.2), trng.uniform(-0.2, 0.2)),
                           spread=0.9, gnarl=1.0, depth=3, roots=False))
    k += 1

# ================================================================== graveyard: crooked tombstones, iron fence, gate
def build_graveyard():
    gm = MB(["BG2_Tombstone", "BG2_Iron", "BG2_Lantern"])
    grng = random.Random(13)
    yaw_g = yaw_facing(gpos(GY_AZ, GY_D, 0))

    def stone(kind, M):
        if kind == 0:     # round top slab
            w, h = 0.16, 0.24
            pts = [(-w / 2, -0.1), (w / 2, -0.1), (w / 2, h - w / 2)]
            for q in range(1, 10):
                a = math.pi * q / 10
                pts.append((w / 2 * math.cos(a), h - w / 2 + w / 2 * math.sin(a)))
            pts.append((-w / 2, h - w / 2))
            gm.poly_prism(pts, 0.05, "BG2_Tombstone", M)
        elif kind == 1:   # cross
            gm.box((0, 0, 0.1), (0.045, 0.045, 0.42), "BG2_Tombstone", M=M)
            gm.box((0, 0, 0.21), (0.19, 0.045, 0.045), "BG2_Tombstone", M=M)
            gm.box((0, 0, -0.07), (0.1, 0.08, 0.06), "BG2_Tombstone", M=M)
        elif kind == 2:   # pointed slab
            w, h = 0.18, 0.22
            gm.poly_prism([(-w / 2, -0.1), (w / 2, -0.1), (w / 2, h - 0.06), (0, h + 0.02), (-w / 2, h - 0.06)], 0.06, "BG2_Tombstone", M)
        elif kind == 3:   # obelisk
            gm.lathe([(0.07, -0.1), (0.07, 0.0), (0.045, 0.03), (0.035, 0.42), (0.0, 0.5)], 4, "BG2_Tombstone", M, smooth=False, phase=math.pi / 4)
        else:             # wide double slab
            w, h = 0.3, 0.17
            pts = [(-w / 2, -0.1), (w / 2, -0.1), (w / 2, h)]
            for q in range(1, 8):
                a = math.pi * q / 8
                pts.append((w / 2 * math.cos(a) * 1.0, h + 0.06 * math.sin(a)))
            pts.append((-w / 2, h))
            gm.poly_prism(pts, 0.06, "BG2_Tombstone", M)

    # rows of stones on the mound
    for row in range(4):
        for c in range(9):
            if grng.random() < 0.22:
                continue
            lx = -2.4 + c * 0.6 + grng.uniform(-0.15, 0.15) + (0.3 if row % 2 else 0.0)
            ly = -1.4 + row * 0.7 + grng.uniform(-0.12, 0.12)
            wp = gpos(GY_AZ, GY_D, 0) + Matrix.Rotation(yaw_g, 3, "Z") @ Vector((lx, ly, 0))
            az, el, dd = azel(Vector((wp.x, wp.y, P.z)))
            dh = Vector((wp.x - P.x, wp.y - P.y)).length
            z = ground_z(az, dh)
            sc_ = grng.uniform(0.9, 1.35) * (1.25 if row == 0 else 1.0)
            M = Matrix.Translation((wp.x, wp.y, z)) @ Matrix.Rotation(yaw_g + grng.uniform(-0.35, 0.35), 4, "Z") @ \
                Euler((grng.uniform(-0.22, 0.22), grng.uniform(-0.2, 0.2), 0)).to_matrix().to_4x4() @ Matrix.Diagonal((sc_, sc_, sc_, 1))
            stone(grng.choice([0, 0, 1, 1, 2, 3, 4]), M)
    # iron fence in front (a gentle arc) with a gate
    posts = []
    n = 46
    for k in range(n + 1):
        lx = -3.2 + 6.4 * k / n
        ly = -2.05 + 0.12 * (lx / 3.2) ** 2
        posts.append((lx, ly))
    rot = Matrix.Rotation(yaw_g, 3, "Z")
    base = gpos(GY_AZ, GY_D, 0)
    prev = None
    for k, (lx, ly) in enumerate(posts):
        if 21 <= k <= 25:
            prev = None
            continue      # gate gap
        wp = base + rot @ Vector((lx, ly, 0))
        az, el, dd = azel(Vector((wp.x, wp.y, P.z)))
        dh = Vector((wp.x - P.x, wp.y - P.y)).length
        z = ground_z(az, dh) - 0.04
        tilt = Euler((grng.uniform(-0.1, 0.1) + (0.25 if k in (7, 8, 33) else 0), grng.uniform(-0.08, 0.08), 0))
        M = Matrix.Translation((wp.x, wp.y, z)) @ Matrix.Rotation(yaw_g, 4, "Z") @ tilt.to_matrix().to_4x4()
        hp = 0.36 if k % 6 else 0.42
        gm.box((0, 0, hp / 2), (0.012, 0.012, hp), "BG2_Iron", M=M)
        gm.lathe([(0.0, hp - 0.005), (0.018, hp + 0.01), (0.0, hp + 0.06)], 4, "BG2_Iron", M, smooth=False)
        top = M @ Vector((0, 0, hp - 0.06))
        low = M @ Vector((0, 0, 0.08))
        if prev is not None:
            for a_, b_ in ((prev[0], top), (prev[1], low)):
                c = (a_ + b_) / 2
                L = (b_ - a_).length
                d_ = (b_ - a_).normalized()
                yaw = math.atan2(d_.y, d_.x)
                pitch = math.asin(max(-1, min(1, d_.z)))
                gm.box(c, (L, 0.01, 0.012), "BG2_Iron", Euler((0, -pitch, yaw), "XYZ"))
        prev = (top, low)
    # gate pillars with lanterns
    for k in (20, 26):
        lx, ly = posts[k]
        wp = base + rot @ Vector((lx, ly, 0))
        az, el, dd = azel(Vector((wp.x, wp.y, P.z)))
        dh = Vector((wp.x - P.x, wp.y - P.y)).length
        z = ground_z(az, dh) - 0.05
        M = Matrix.Translation((wp.x, wp.y, z)) @ Matrix.Rotation(yaw_g, 4, "Z")
        gm.box((0, 0, 0.25), (0.1, 0.1, 0.5), "BG2_Tombstone", M=M)
        gm.box((0, 0, 0.52), (0.13, 0.13, 0.04), "BG2_Tombstone", M=M)
        gm.lathe([(0.0, 0.54), (0.035, 0.57), (0.035, 0.63), (0.0, 0.67)], 8, "BG2_Lantern", M)
    # half-open gate leaf
    lx, ly = posts[21]
    wp = base + rot @ Vector((lx, ly, 0))
    az, el, dd = azel(Vector((wp.x, wp.y, P.z)))
    z = ground_z(az, Vector((wp.x - P.x, wp.y - P.y)).length)
    M = Matrix.Translation((wp.x, wp.y, z)) @ Matrix.Rotation(yaw_g - 0.9, 4, "Z")
    for q in range(6):
        gm.box((0.04 + q * 0.06, 0, 0.2), (0.01, 0.01, 0.4), "BG2_Iron", M=M)
    gm.box((0.18, 0, 0.32), (0.36, 0.01, 0.012), "BG2_Iron", M=M)
    gm.box((0.18, 0, 0.08), (0.36, 0.01, 0.012), "BG2_Iron", M=M)
    # mausoleum behind the stones (pointed roof, cross, faint warm door)
    wp = base + rot @ Vector((2.0, 0.9, 0))
    az, el, dd = azel(Vector((wp.x, wp.y, P.z)))
    z = ground_z(az, Vector((wp.x - P.x, wp.y - P.y)).length) - 0.08
    M = Matrix.Translation((wp.x, wp.y, z)) @ Matrix.Rotation(yaw_g - 0.25, 4, "Z")
    gm.box((0, 0, 0.04), (0.78, 0.66, 0.12), "BG2_Tombstone", M=M)
    gm.box((0, 0.04, 0.32), (0.6, 0.5, 0.5), "BG2_Tombstone", M=M)
    for xo in (-0.25, -0.12, 0.12, 0.25):
        gm.lathe([(0.03, 0.08), (0.025, 0.12), (0.025, 0.5), (0.034, 0.54)], 8, "BG2_Tombstone", M @ Matrix.Translation((xo, -0.27, 0)))
    gm.box((0, -0.26, 0.56), (0.64, 0.1, 0.06), "BG2_Tombstone", M=M)
    gm.poly_prism([(-0.36, 0.58), (0.36, 0.58), (0.0, 0.92)], 0.62, "BG2_Tombstone", M @ Matrix.Translation((0, 0.02, 0)) @ Matrix.Rotation(0, 4, "Z"))
    gm.box((0, -0.02, 1.0), (0.025, 0.025, 0.18), "BG2_Tombstone", M=M)
    gm.box((0, -0.02, 1.03), (0.1, 0.025, 0.025), "BG2_Tombstone", M=M)
    gm.poly_prism([(-0.09, 0.08), (0.09, 0.08), (0.09, 0.3), (0.0, 0.38), (-0.09, 0.3)], 0.01, "BG2_Lantern", M @ Matrix.Translation((0, -0.215, 0)))
    return gm.to_object("BG2_Graveyard", shadow=False)


build_graveyard()

# ================================================================== near ground dressing: path to the graveyard gate, rocks, dead grass
NEAR_LANTERNS = []


def build_near_dressing():
    nm = MB(["BG2_Path", "BG2_Hill_Rock", "BG2_DeadGrass", "BG2_Lantern"])
    r_ = random.Random(21)
    yaw_g = yaw_facing(gpos(GY_AZ, GY_D, 0))
    gate = gpos(GY_AZ, GY_D, 0) + Matrix.Rotation(yaw_g, 3, "Z") @ Vector((0.0, -2.05, 0))
    start = gpos(-3.2, 8.0, 0)
    ctrl = [start, start.lerp(gate, 0.33) + R * 0.5, start.lerp(gate, 0.66) - R * 0.6, gate]
    pts = []
    for k in range(60):
        t = k / 59
        a, b, c, d = ctrl
        q = a * (1 - t) ** 3 + b * 3 * t * (1 - t) ** 2 + c * 3 * t * t * (1 - t) + d * t ** 3
        pts.append(q)
    prev = None

    def gz(v):
        az, el, dd = azel(Vector((v.x, v.y, P.z)))
        return ground_z(az, Vector((v.x - P.x, v.y - P.y)).length)
    for k, q in enumerate(pts):
        tng = (pts[min(k + 1, 59)] - pts[max(k - 1, 0)])
        tng.z = 0
        tng.normalize()
        side = Vector((-tng.y, tng.x, 0))
        w = 0.32 - 0.12 * k / 59 + 0.04 * math.sin(k * 0.9)
        l, r = q + side * w, q - side * w
        vl = nm.bm.verts.new((l.x, l.y, gz(l) + 0.015))
        vr = nm.bm.verts.new((r.x, r.y, gz(r) + 0.015))
        if prev:
            nm.tag([nm.bm.faces.new((prev[0], prev[1], vr, vl))], "BG2_Path")
        prev = (vl, vr)
    # little lantern posts along the near path (lead the eye to the graveyard gate)
    for k, t in enumerate((0.18, 0.5, 0.8)):
        i = int(t * 59)
        q = pts[i]
        tng = (pts[min(i + 1, 59)] - pts[max(i - 1, 0)])
        tng.z = 0
        tng.normalize()
        side = Vector((-tng.y, tng.x, 0)) * (0.42 if k % 2 else -0.42)
        b = q + side
        zb = gz(b)
        M = Matrix.Translation((b.x, b.y, zb)) @ Matrix.Rotation(r_.uniform(-0.1, 0.1), 4, "X")
        nm.box((0, 0, 0.2), (0.022, 0.022, 0.4), "BG2_Hill_Rock", M=M)
        nm.box((0.04, 0, 0.39), (0.1, 0.014, 0.014), "BG2_Hill_Rock", M=M)
        nm.lathe([(0.0, 0.3), (0.03, 0.32), (0.03, 0.37), (0.0, 0.39)], 8, "BG2_Lantern", M @ Matrix.Translation((0.08, 0, 0)))
        NEAR_LANTERNS.append(tuple(M @ Vector((0.08, 0, 0.345))))
    # rocks (displaced icospheres)
    for i in range(170):
        az = r_.uniform(-45, 45)
        d = 8.6 + (r_.random() ** 1.6) * 18
        v = gpos(az, d, 0)
        z = ground_z(az, d)
        s_ = r_.uniform(0.04, 0.22) * (1.6 if r_.random() < 0.1 else 1.0)
        ret = bmesh.ops.create_icosphere(nm.bm, subdivisions=3, radius=1.0,
                                         matrix=Matrix.Translation((v.x, v.y, z + s_ * 0.15)) @ Matrix.Rotation(r_.uniform(0, 6.3), 4, "Z") @ Matrix.Diagonal((s_ * r_.uniform(0.9, 1.6), s_, s_ * r_.uniform(0.45, 0.8), 1)))
        sv = Vector((r_.random() * 50, r_.random() * 50, 0))
        for vt in ret["verts"]:
            lp = vt.co - Vector((v.x, v.y, z))
            nn = lp.normalized()
            vt.co += nn * s_ * (0.32 * noise.noise(nn * 1.4 + sv) + 0.12 * noise.noise(nn * 4.0 + sv) + 0.05 * noise.noise(nn * 9.0 + sv))
            if vt.co.z < z:
                vt.co.z = z - 0.01
        nm.tag(list({f for vt in ret["verts"] for f in vt.link_faces}), "BG2_Hill_Rock", smooth=True)
    # dead grass tufts
    for i in range(520):
        az = r_.uniform(-45, 45)
        d = 8.6 + (r_.random() ** 1.3) * 16
        v = gpos(az, d, 0)
        z = ground_z(az, d) - 0.01
        hgt = r_.uniform(0.06, 0.16) * (1 + (d - 8.6) / 30)
        for b in range(r_.randint(6, 11)):
            ang = r_.uniform(0, 6.283)
            lean = r_.uniform(0.15, 0.7)
            dirv = Vector((math.cos(ang) * lean, math.sin(ang) * lean, 1.0)).normalized()
            base = Vector((v.x + r_.uniform(-0.03, 0.03), v.y + r_.uniform(-0.03, 0.03), z))
            side = Vector((-math.sin(ang), math.cos(ang), 0)) * 0.006
            h_ = hgt * r_.uniform(0.6, 1.1)
            mid = base + dirv * h_ * 0.55
            tip = base + (dirv + Vector((math.cos(ang), math.sin(ang), -0.5)) * lean * 0.6).normalized() * h_
            a1, a2 = nm.bm.verts.new(base - side), nm.bm.verts.new(base + side)
            b1, b2 = nm.bm.verts.new(mid - side * 0.6), nm.bm.verts.new(mid + side * 0.6)
            t_ = nm.bm.verts.new(tip)
            nm.tag([nm.bm.faces.new((a1, a2, b2, b1)), nm.bm.faces.new((b1, b2, t_))], "BG2_DeadGrass")
    return nm.to_object("BG2_Near_Dressing", shadow=False)


build_near_dressing()

# ================================================================== fog cards, clouds, bats
def card(name, mat, center, w, h, tilt=0.0, yaw=None, camera_only=True):
    me = bpy.data.meshes.new(name)
    me.from_pydata([(-1, 0, -1), (1, 0, -1), (1, 0, 1), (-1, 0, 1)], [], [(0, 1, 2, 3)])
    me.materials.append(MATS[mat])
    o = add_obj(name, me, camera_only=camera_only)
    o.location = center
    o.rotation_euler = (tilt, 0, yaw_facing(center) if yaw is None else yaw)
    o.scale = (w / 2, 1, h / 2)
    return o


frng = random.Random(5)
# low fog: long thin bands lying almost flat (tilted toward the cameras)
FOG_BANDS = [(-30, 12, 0.15, 10, 0.9), (8, 13, 0.05, 12, 0.8), (-8, 22, 0.45, 16, 1.0), (22, 22, 0.2, 18, 1.2), (-40, 24, 0.35, 18, 1.1),
             (12, 30, 0.25, 22, 1.4), (-20, 33, 0.45, 26, 1.6), (35, 36, 0.4, 26, 1.6), (0, 40, 0.1, 30, 1.8), (14, 43, 0.2, 18, 1.8),
             (8, 52, 0.0, 30, 2.2), (-30, 55, 0.1, 40, 2.4), (-6, 70, 0.0, 50, 3.0), (25, 75, 0.0, 50, 3.0)]
for k, (az, d, z, w, h) in enumerate(FOG_BANDS):
    c = gpos(az, d, ground_z(az, d) * 0.0 + z + 0.12 * (d / 20))
    card(f"BG2_Fog_{k}", "BG2_Fog", c, w, h * 0.7, tilt=math.radians(-15))
# clouds: wispy streaks across/around the moon (in front of it, at ~40 m) + darker banks high up (far)
CLOUDS = [(MOON_AZ + 1.5, MOON_EL - 1.3, 40, 10.0, 0.5, "BG2_Cloud_Wisp"), (MOON_AZ + 5.5, MOON_EL - 0.4, 41, 8.0, 0.8, "BG2_Cloud_Wisp"),
          (MOON_AZ - 1.5, MOON_EL + 3.2, 39, 12.0, 0.7, "BG2_Cloud_Wisp"), (MOON_AZ - 7.5, MOON_EL + 0.4, 42, 9.5, 0.9, "BG2_Cloud_Wisp"),
          (-24, 6.5, 150, 70, 9, "BG2_Cloud_Bank"), (16, 7.5, 150, 80, 10, "BG2_Cloud_Bank"), (-4, 10.5, 160, 90, 12, "BG2_Cloud_Bank"),
          (30, 11.5, 160, 70, 10, "BG2_Cloud_Bank"), (-38, 12.5, 160, 70, 12, "BG2_Cloud_Bank"), (5, 16.0, 170, 110, 16, "BG2_Cloud_Bank"),
          (MOON_AZ + 9.0, MOON_EL + 1.8, 120, 30, 4, "BG2_Cloud_Wisp"), (MOON_AZ - 12.0, MOON_EL + 3.0, 120, 34, 5, "BG2_Cloud_Wisp"),
          (MOON_AZ + 6.0, MOON_EL + 5.0, 100, 40, 7, "BG2_Cloud_Bank"), (MOON_AZ - 8.0, MOON_EL + 6.5, 100, 44, 8, "BG2_Cloud_Bank"),
          (24, 9.0, 140, 60, 10, "BG2_Cloud_Bank"), (-22, 9.5, 140, 60, 10, "BG2_Cloud_Bank"), (0, 20.0, 170, 120, 18, "BG2_Cloud_Bank"),
          (-30, 18.0, 170, 90, 16, "BG2_Cloud_Bank"), (32, 17.0, 170, 90, 16, "BG2_Cloud_Bank")]
for k, (az, el, d, w, h, mt) in enumerate(CLOUDS):
    c = P + hdir(az) * d
    c.z = z_for_el(el, d)
    o = card(f"BG2_Cloud_{k}", mt, c, w, h, tilt=0.0)
    o.rotation_euler.y = math.radians(frng.uniform(-9, 9))


def make_bat(name, loc, size, flap, yaw):
    bm = bmesh.new()
    # half wing outline (x>0), mirrored; scalloped trailing edge
    half = [(0.0, 0.10), (0.10, 0.12), (0.22, 0.20), (0.36, 0.26), (0.50, 0.18), (0.46, 0.10), (0.40, 0.02), (0.33, 0.07), (0.26, -0.02),
            (0.18, 0.03), (0.10, -0.06), (0.04, -0.02), (0.0, -0.10)]
    pts = half + [(-x, y) for (x, y) in reversed(half[1:-1])]
    vs = []
    for (x, y) in pts:
        z = abs(x) * math.sin(flap) * 0.9
        vs.append(bm.verts.new((x * size, 0, y * size + z * size)))
    bm.faces.new(vs)
    # body + ears
    bmesh.ops.create_uvsphere(bm, u_segments=8, v_segments=6, radius=0.05 * size, matrix=Matrix.Translation((0, 0, 0.02 * size)) @ Matrix.Diagonal((0.8, 0.8, 1.7, 1)))
    for sx in (-1, 1):
        a = bm.verts.new((sx * 0.02 * size, 0, 0.11 * size))
        b = bm.verts.new((sx * 0.035 * size, 0, 0.17 * size))
        c = bm.verts.new((sx * 0.045 * size, 0, 0.1 * size))
        bm.faces.new((a, c, b))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(MATS["BG2_Bat"])
    o = add_obj(name, me, shadow=False)
    o.location = loc
    o.rotation_euler = (0.0, 0.0, yaw)
    return o


BATS = [(MOON_AZ - 1.2, MOON_EL + 0.9, 30, 0.75, 0.5), (MOON_AZ + 1.4, MOON_EL - 0.4, 32, 0.6, -0.3), (MOON_AZ + 3.8, MOON_EL + 2.5, 28, 0.7, 0.7),
        (MOON_AZ - 4.8, MOON_EL + 3.1, 34, 0.55, -0.6), (MOON_AZ + 6.5, MOON_EL + 0.6, 36, 0.5, 0.2), (MOON_AZ - 2.6, MOON_EL + 4.6, 30, 0.6, 0.9),
        (16.0, 6.4, 40, 0.6, 0.4), (19.0, 7.2, 42, 0.5, -0.5)]
for k, (az, el, d, s_, flap) in enumerate(BATS):
    c = P + hdir(az) * d
    c.z = z_for_el(el, d)
    make_bat(f"BG2_Bat_{k}", c, s_ * 0.7, flap, yaw_facing(c) + frng.uniform(-0.4, 0.4))

# ================================================================== background-only lights (light-linked to Background_v2)
def bg_sun(name, direction, energy, color, angle=2.0):
    ld = bpy.data.lights.new(name, "SUN")
    ld.energy = energy
    ld.color = color
    ld.angle = math.radians(angle)
    o = add_obj(name, ld)
    o.rotation_euler = Vector(direction).normalized().to_track_quat("-Z", "Y").to_euler()
    o.light_linking.receiver_collection = C_BG
    return o


# rim from behind the scenery (moon side, raised so crests and roofs catch it)
rim_dir = -MOON_DIR
rim_dir = Vector((rim_dir.x, rim_dir.y, -0.42))
bg_sun("BG2_Rim_Moon", Vector((rim_dir.x, rim_dir.y, -0.5)), 3.0, (0.45, 0.62, 1.0), angle=3.0)
# cool side key from camera-left (moon side) so ridges, gullies and towers show their form
bg_sun("BG2_Key_Side", R * 0.75 + F * 0.45 + Vector((0, 0, -0.5)), 1.4, (0.36, 0.52, 1.0), angle=4.0)
# warm glow spilling from the castle (courtyard + gate) and the graveyard lanterns, background-only
def bg_point(name, loc, power, color, radius=0.1):
    ld = bpy.data.lights.new(name, "POINT")
    ld.energy = power
    ld.color = color
    ld.shadow_soft_size = radius
    o = add_obj(name, ld)
    o.location = loc
    o.light_linking.receiver_collection = C_BG
    return o


for k, (lx, ly, lz, pw) in enumerate([(-0.6, -1.95, 0.3, 30.0), (0.9, -0.9, 0.8, 30.0), (-1.9, -0.9, 1.0, 25.0), (-5.3, -1.1, 0.0, 15.0), (0.4, -2.6, -0.3, 20.0)]):
    bg_point(f"BG2_Castle_Glow_{k}", LM @ Vector((lx, ly, lz)), pw, (1.0, 0.5, 0.18), 0.3)
for k, (lx, ly) in enumerate([(-3.2 + 6.4 * 20 / 46, -2.05), (-3.2 + 6.4 * 26 / 46, -2.05)]):
    yaw_g = yaw_facing(gpos(GY_AZ, GY_D, 0))
    wp = gpos(GY_AZ, GY_D, 0) + Matrix.Rotation(yaw_g, 3, "Z") @ Vector((lx, ly - 0.08, 0))
    az, el, dd = azel(Vector((wp.x, wp.y, P.z)))
    wp.z = ground_z(az, Vector((wp.x - P.x, wp.y - P.y)).length) + 0.62
    bg_point(f"BG2_Gate_Lantern_Glow_{k}", wp, 2.5, (1.0, 0.5, 0.16), 0.03)
for k, (lx, ly, lz) in enumerate(NEAR_LANTERNS):
    bg_point(f"BG2_Path_Lantern_Glow_{k}", Vector((lx, ly, lz)), 1.6, (1.0, 0.5, 0.16), 0.03)
# faint fill from the cameras so front faces are not pure black
bg_sun("BG2_Fill_Front", Vector((F.x, F.y, -0.35)), 0.15, (0.45, 0.55, 0.9), angle=10.0)

# ================================================================== reference cameras + stand-in moonlight (not exported)
with bpy.data.libraries.load(SRC, link=False) as (src, dst):
    dst.objects = [n for n in src.objects if n.startswith("CAM_")]
for o in dst.objects:
    if o is None:
        continue
    o.parent = None
    o.name = "REF_" + o.name
    C_REF.objects.link(o)
ld = bpy.data.lights.new("REF_Moonlight", "SUN")
ld.energy = 3.0
ld.color = (0.55, 0.7, 1.0)
mo = bpy.data.objects.new("REF_Moonlight", ld)
C_REF.objects.link(mo)
mo.rotation_euler = (Vector((0, 0, 0.3)) - MOON).normalized().to_track_quat("-Z", "Y").to_euler()
sc.camera = bpy.data.objects.get("REF_CAM_Main_Capsule_1232x706")

# view layer: BG_Ref_Cameras excluded (cameras still usable by test scripts that toggle it)
vl = sc.view_layers[0]
vl.layer_collection.children["BG_Ref_Cameras"].exclude = False   # keep usable for test renders; Background_v2 is the export

# ------------------------------------------------------------------ world + render settings (standalone tests only)
w = bpy.data.worlds.new("BG_Test_World") if not sc.world else sc.world
sc.world = w
w.use_nodes = True
bgn = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
bgn.inputs["Color"].default_value = (0.004, 0.007, 0.016, 1)
bgn.inputs["Strength"].default_value = 1.0
sc.render.engine = "CYCLES"
sc.cycles.device = "GPU"
sc.cycles.samples = 64
sc.cycles.use_denoising = True
sc.cycles.transparent_max_bounces = 16
sc.view_settings.view_transform = "AgX"
for lk in ("AgX - Punchy", "Punchy"):
    try:
        sc.view_settings.look = lk
        break
    except Exception:
        pass
for c in bpy.data.objects:
    if c.type == "CAMERA":
        c.data.clip_end = 400.0

print("MOON az/el/ang", round(MOON_AZ, 2), round(MOON_EL, 2), round(MOON_ANG, 2))
print("OBJECTS", len(C_BG.objects), "TRIS?", sum(len(o.data.polygons) for o in C_BG.objects if o.type == "MESH"))
bpy.ops.wm.save_as_mainfile(filepath=OUT, compress=True)
print("SAVED", OUT)
