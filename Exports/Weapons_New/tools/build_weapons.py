# Base models of 4 new weapons (user, 2026-11-20: "do pan, giant fly swatter, baseball bat and rolling pin. Do base
# model for them, I will fix it then you help me put them in the game"). Writes Exports/Weapons_New/New_Weapons.blend
# (never overwrites it without --force: the user fixes the models there).
# Like the game's broom: long axis up (+Z here = Unity +Y), pivot at the handle end (the rolling pin: its middle), real
# VR sizes a bit chunky. Clean meshes: rings of quads, n-gon caps only on round ends; material slots named by colour
# role (wood, wood_dark, wood_light, iron, iron_light, plastic, plastic_dark, tape), which the game's palette colouring
# will use later. An empty "Grip" child marks where the hand goes (becomes the prefab's Attach point).
#   blender -b --factory-startup --python build_weapons.py -- <out.blend> [--force]
import bpy, bmesh, sys, os, math
from mathutils import Vector

a = sys.argv[sys.argv.index("--") + 1:]
OUT = a[0]
if os.path.exists(OUT) and "--force" not in a:
    print("EXISTS, not overwritten (use --force):", OUT); sys.exit(0)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

COLOURS = {
    "wood": "8a5630", "wood_dark": "4e2c16", "wood_light": "c08a54", "iron": "38343a", "iron_light": "8a8890",
    "plastic": "7e3fc0", "plastic_dark": "4a2080", "tape": "1e1a1c",
}
def srgb(h):
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c] + [1]
MATS = {}
for name, hx in COLOURS.items():
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = srgb(hx)
    b.inputs["Roughness"].default_value = 0.8 if not name.startswith("iron") else 0.5
    if name.startswith("iron"):
        b.inputs["Metallic"].default_value = 0.6
    m.diffuse_color = srgb(hx)
    MATS[name] = m

class Mesh:
    def __init__(self):
        self.v, self.f, self.m = [], [], []

    def add(self, vs, fs, ms):
        o = len(self.v); self.v += vs; self.f += [[i + o for i in f] for f in fs]; self.m += ms

    def lathe(self, profile, n, mats, axis="Z", centre=(0, 0, 0), cap_lo=True, cap_hi=True):
        """profile: [(radius, height)], rings around axis; mats: per segment (len(profile)-1) or one name."""
        if isinstance(mats, str):
            mats = [mats] * (len(profile) - 1)
        c = Vector(centre)
        def P(r, h, t):
            x, y = r * math.cos(t), r * math.sin(t)
            if axis == "Z": return tuple(c + Vector((x, y, h)))
            if axis == "X": return tuple(c + Vector((h, x, y)))
            return tuple(c + Vector((x, h, y)))
        vs, fs, ms = [], [], []
        for (r, h) in profile:
            for k in range(n):
                vs.append(P(r, h, 2 * math.pi * k / n))
        for i in range(len(profile) - 1):
            for k in range(n):
                a0, a1 = i * n + k, i * n + (k + 1) % n
                b0, b1 = a0 + n, a1 + n
                fs.append((a0, a1, b1, b0)); ms.append(mats[i])
        if cap_lo:
            fs.append(tuple(reversed(range(n)))); ms.append(mats[0])
        if cap_hi:
            last = (len(profile) - 1) * n
            fs.append(tuple(last + k for k in range(n))); ms.append(mats[-1])
        self.add(vs, fs, ms)

    def box(self, c, s, mat):
        cx, cy, cz = c; sx, sy, sz = s[0] / 2, s[1] / 2, s[2] / 2
        p = [(-sx, -sy, -sz), (sx, -sy, -sz), (sx, sy, -sz), (-sx, sy, -sz), (-sx, -sy, sz), (sx, -sy, sz), (sx, sy, sz), (-sx, sy, sz)]
        vs = [(cx + x, cy + y, cz + z) for x, y, z in p]
        fs = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
        self.add(vs, fs, [mat] * 6)

    def make(self, name, coll, grip=None, x=0.0):
        me = bpy.data.meshes.new(name)
        me.from_pydata(self.v, [], self.f)
        names = sorted(set(self.m), key=list(COLOURS).index)
        for n in names:
            me.materials.append(MATS[n])
        for poly, mn in zip(me.polygons, self.m):
            poly.material_index = names.index(mn)
        me.validate(); me.update()
        ob = bpy.data.objects.new(name, me)
        coll.objects.link(ob)
        ob.location.x = x
        if grip is not None:
            g = bpy.data.objects.new(name + "_Grip", None)
            g.empty_display_type = 'ARROWS'; g.empty_display_size = 0.06
            g.parent = ob; g.location = grip
            coll.objects.link(g)
        tris = sum(len(p.vertices) - 2 for p in me.polygons)
        lo = Vector([min(v.co[i] for v in me.vertices) for i in range(3)]); hi = Vector([max(v.co[i] for v in me.vertices) for i in range(3)])
        print(f"{name}: {len(me.polygons)} faces, {tris} tris, size {tuple(round(v, 3) for v in (hi - lo))}")
        return ob

coll = bpy.data.collections.new("New_Weapons")
scene.collection.children.link(coll)
N = 12

# 1 Frying pan: wooden handle up the Z axis (pivot at its end), the pan at the top, its flat bottom facing +X (hits
#   with the flat). Pan 0.32 across.
pan = Mesh()
pan.lathe([(0.017, 0.0), (0.02, 0.012), (0.019, 0.20), (0.016, 0.255)], N, ["wood_dark", "wood", "wood"])        # handle
pan.lathe([(0.017, 0.255), (0.019, 0.26), (0.019, 0.30), (0.014, 0.31)], N, "iron", cap_lo=False)                  # ferrule
R = 0.16
centre = (0, 0, 0.31 + R - 0.01)
# the bowl, round the X axis: outer bottom -> rim -> inner bottom (closed with n-gon caps both sides)
pan.lathe([(R - 0.025, -0.032), (R - 0.004, -0.02), (R, 0.022), (R - 0.008, 0.024), (R - 0.03, -0.018)], 24,
          ["iron", "iron", "iron", "iron_light"], axis="X", centre=centre)
pan.make("Weapon_Frying_Pan", coll, grip=(0, 0, 0.12), x=0.0)

# 2 Giant fly swatter: thin plastic handle, a square head with a grid of bars (it reads as a mesh), 0.30 x 0.28.
sw = Mesh()
sw.lathe([(0.016, 0.0), (0.016, 0.015), (0.012, 0.03), (0.011, 0.5), (0.013, 0.56)], 8, ["plastic_dark", "plastic_dark", "plastic", "plastic"])
H0, HW, HH, T = 0.56, 0.28, 0.30, 0.012
sw.box((0, 0, H0 + 0.02), (T * 1.4, 0.06, 0.04), "plastic")                                          # neck
z0, z1 = H0 + 0.04, H0 + 0.04 + HH
bar = 0.022
sw.box((0, 0, z0 + bar / 2), (T, HW, bar), "plastic")                                                # frame
sw.box((0, 0, z1 - bar / 2), (T, HW, bar), "plastic")
sw.box((0, -HW / 2 + bar / 2, (z0 + z1) / 2), (T, bar, HH - 2 * bar), "plastic")
sw.box((0, HW / 2 - bar / 2, (z0 + z1) / 2), (T, bar, HH - 2 * bar), "plastic")
for k in range(1, 5):                                                                                # grid
    y = -HW / 2 + bar + (HW - 2 * bar) * k / 5
    sw.box((0, y, (z0 + z1) / 2), (T * 0.7, 0.008, HH - 2 * bar), "plastic_dark")
    z = z0 + bar + (HH - 2 * bar) * k / 5
    sw.box((0, 0, z), (T * 0.6, HW - 2 * bar, 0.008), "plastic_dark")
sw.make("Weapon_Fly_Swatter", coll, grip=(0, 0, 0.1), x=0.5)

# 3 Baseball bat: one lathe, knob at the bottom (pivot), taped handle, fat barrel, rounded end. 0.84 long.
bat = Mesh()
prof = [(0.0, 0.0), (0.026, 0.004), (0.028, 0.016), (0.017, 0.03), (0.016, 0.22), (0.017, 0.33), (0.024, 0.48),
        (0.04, 0.62), (0.046, 0.75), (0.044, 0.815), (0.032, 0.835), (0.0, 0.842)]
bat_mats = ["wood_dark", "wood_dark", "wood_dark", "tape", "wood", "wood", "wood", "wood", "wood", "wood", "wood"]
# poles closed by rings at radius 0: use small caps instead (drop the 0-radius points)
bat.lathe(prof[1:-1], N, bat_mats[1:-1])
bat.make("Weapon_Baseball_Bat", coll, grip=(0, 0, 0.12), x=1.0)

# 4 Rolling pin: roller 0.30 long in the middle, a handle each side with a knob; pivot in the middle (two hands).
rp = Mesh()
rp.lathe([(0.03, -0.15), (0.036, -0.143), (0.036, 0.143), (0.03, 0.15)], N, ["wood_light", "wood_light", "wood_light"])
for s in (-1, 1):
    pts = [(0.014, 0.15), (0.016, 0.165), (0.015, 0.24), (0.021, 0.252), (0.02, 0.27), (0.012, 0.278)]
    if s < 0:
        pts = [(r, -h) for r, h in reversed(pts)]
    rp.lathe(pts, N, "wood")
rp.make("Weapon_Rolling_Pin", coll, grip=(0, 0, 0.21), x=1.5)

# A light and a camera-friendly world for previews.
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN")); sun.rotation_euler = (0.9, 0.2, 0.8)
sun.data.energy = 3.0
scene.collection.objects.link(sun)
scene.world = bpy.data.worlds.new("World"); scene.world.use_nodes = True
scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.8
os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=OUT)
print("SAVED", OUT)
