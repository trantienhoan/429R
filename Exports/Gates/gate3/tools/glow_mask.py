"""Gate 3's glow map on the house's furniture texture (2026-11-23, user: "glow them").

The user's remodel put the kit on the house's Bedroom_Furniture_Set1 texture (Unity: Bedroom_Set_1_Mat), whose glow is
the whole texture at 13%: the pit rings, lantern glass and pumpkin faces stopped glowing. This paints an emission map
for gate 3's own copy of that material (Build Ball Valley Kit makes it): black everywhere, the glow colour only on the
texels those faces use. Read-only on the .blend.

Glow faces (by piece, the texture colour under each face, and its height):
- Hole_Corner / Hole_Side: the purple ring at lawn level (|z| < 0.1 m) -> lavender;
- Lantern_Post: the yellow glass -> warm lantern light;
- Pumpkin_Lantern / Giant_Jack_O_Lantern: the yellow face cut-outs -> pumpkin glow.
A texel any NON-glowing kit face also uses is never painted (reported), so nothing else lights up.

Run: blender -b Exports/Gates/gate3/BallValley_Kit.blend --python Exports/Gates/gate3/tools/glow_mask.py -- [<out.png>]
(default out: Assets/M_Textures/Gates/BallValley/BallValley_House_Glow.png)"""
import bpy, bmesh, os, sys
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = ARGS[0] if ARGS else os.path.join(REPO, "Assets", "M_Textures", "Gates", "BallValley", "BallValley_House_Glow.png")
MAT = "Bedroom_Furniture_Set1"
PREFIX = "BallValley_Kit_"
DILATE = 3                                    # px round each glow face's own texels (bilinear, no dark seams)

RING = (0.80, 0.55, 1.00)                     # the old kit's glow_purple, light end
LANTERN = (1.00, 0.82, 0.45)                  # glow_lantern
FACE = (1.00, 0.78, 0.30)                     # glow_face


def is_yellow(c):
    return c[0] > 0.8 and c[1] > 0.75 and c[2] < 0.6


def is_ring_purple(c):
    return 0.22 < c[0] < 0.48 and c[1] < 0.3 and c[2] > 0.32 and c[2] > c[1] + 0.12


def glow_of(short, c, z):
    if short in ("Hole_Corner", "Hole_Side") and abs(z) < 0.1 and is_ring_purple(c):
        return RING
    if short == "Lantern_Post" and is_yellow(c):
        return LANTERN
    if short in ("Pumpkin_Lantern", "Giant_Jack_O_Lantern") and is_yellow(c):
        return FACE
    return None


mat = bpy.data.materials.get(MAT)
if mat is None:
    raise SystemExit("No material %s in this file." % MAT)
img = next(n.image for n in mat.node_tree.nodes if n.type == "TEX_IMAGE" and n.image and
           any(l.to_socket.name == "Base Color" for l in mat.node_tree.links if l.from_node == n))
W, H = img.size
albedo = np.array(img.pixels[:], dtype=np.float32).reshape(H, W, 4)[:, :, :3]

glow = np.zeros((H, W, 3), dtype=np.float32)
glow_hit = np.zeros((H, W), dtype=bool)
other_hit = np.zeros((H, W), dtype=bool)
counts = {}


def raster(tri_uv, mask):
    """Marks the texels whose centres fall in a UV triangle (plus the texel under each corner, for slivers)."""
    p = np.array([[u * W - 0.5, v * H - 0.5] for u, v in tri_uv])
    for x, y in p:
        xi, yi = int(round(x)), int(round(y))
        if 0 <= xi < W and 0 <= yi < H:
            mask[yi, xi] = True
    x0, y0 = np.floor(p.min(axis=0)).astype(int)
    x1, y1 = np.ceil(p.max(axis=0)).astype(int)
    x0, y0, x1, y1 = max(x0, 0), max(y0, 0), min(x1, W - 1), min(y1, H - 1)
    if x1 < x0 or y1 < y0:
        return
    xs, ys = np.meshgrid(np.arange(x0, x1 + 1), np.arange(y0, y1 + 1))
    (ax, ay), (bx, by), (cx, cy) = p
    d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
    if abs(d) < 1e-12:
        return
    l1 = ((by - cy) * (xs - cx) + (cx - bx) * (ys - cy)) / d
    l2 = ((cy - ay) * (xs - cx) + (ax - cx) * (ys - cy)) / d
    inside = (l1 >= -1e-6) & (l2 >= -1e-6) & (1 - l1 - l2 >= -1e-6)
    mask[y0:y1 + 1, x0:x1 + 1] |= inside


deps = bpy.context.evaluated_depsgraph_get()
kit = bpy.data.collections["BallValley_Kit"]
for coll in kit.children:
    for ob in coll.objects:
        if ob.type != "MESH" or not ob.name.startswith(PREFIX):
            continue
        short = ob.name[len(PREFIX):]
        me = ob.evaluated_get(deps).to_mesh()
        bm = bmesh.new()
        bm.from_mesh(me)
        uv = bm.loops.layers.uv.active
        n_glow = 0
        for f in bm.faces:
            slot = ob.material_slots[f.material_index].material if f.material_index < len(ob.material_slots) else None
            if slot is None or slot.name != MAT or uv is None:
                continue
            uvs = [(l[uv].uv.x, l[uv].uv.y) for l in f.loops]
            cu = sum(u for u, _ in uvs) / len(uvs)
            cv = sum(v for _, v in uvs) / len(uvs)
            c = albedo[min(H - 1, max(0, int(cv % 1 * H))), min(W - 1, max(0, int(cu % 1 * W)))]
            g = glow_of(short, c, f.calc_center_median().z)
            mask = np.zeros((H, W), dtype=bool) if g else other_hit
            for i in range(1, len(uvs) - 1):
                raster((uvs[0], uvs[i], uvs[i + 1]), mask)
            if g:
                glow[mask] = g
                glow_hit |= mask
                n_glow += 1
        counts[short] = n_glow
        bm.free()
        ob.evaluated_get(deps).to_mesh_clear()

# A few px round the glow texels (same colour), never onto texels a non-glowing face uses.
spread = glow_hit.copy()
col = glow.copy()
for _ in range(DILATE):
    grow = np.zeros_like(spread)
    acc = np.zeros_like(col)
    for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
        sh = np.roll(np.roll(spread, dy, 0), dx, 1)
        grow |= sh
        acc = np.where(sh[:, :, None] & ~spread[:, :, None], np.roll(np.roll(col, dy, 0), dx, 1), acc)
    new = grow & ~spread & ~other_hit
    col[new] = acc[new]
    spread |= new
clash = glow_hit & other_hit
col[other_hit] = 0.0
spread &= ~other_hit

out = bpy.data.images.new("_claude_glow", W, H, alpha=True)
rgba = np.concatenate([col, np.ones((H, W, 1), dtype=np.float32)], axis=2)
out.pixels[:] = rgba.ravel()
out.filepath_raw = OUT
out.file_format = "PNG"
os.makedirs(os.path.dirname(OUT), exist_ok=True)
out.save()
bpy.data.images.remove(out)
for k in sorted(counts):
    if counts[k]:
        print("GLOW %-22s %4d faces" % (k, counts[k]))
print("GLOW texels %d (+%d spread), %d left dark because a non-glowing face uses them too" %
      (int(glow_hit.sum() - clash.sum()), int(spread.sum() - (glow_hit.sum() - clash.sum())), int(clash.sum())))
print("saved", OUT)
