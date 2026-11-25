# Headless: blender -b --factory-startup --python make_lair_webs.py -- <source.blend> [--force]
# FIRST BUILD ONLY: the user has since fixed the pieces by hand; re-export their file with export_lair_props.py.
# The storage-room lair's webs and egg sacs (user 2026-11-25: "remodel them instead of creating new"), remodelled from
# our own Spider_Web and Web_Ball meshes (from Assets/Blender/Bedroom_Walls.blend, copied once into <source.blend> so
# the user's file is never opened for writing):
#   Lair_Web_Round   the whole web, bigger                         origin: the hub
#   Lair_Web_Corner  its lower half: hung across a corner          origin: the hub (top middle, under the ceiling)
#   Lair_Web_Sheet   stretched wide and sagging, between posts     origin: the middle
#   Lair_Web_Strand  one long spoke: a hanging thread, 1 m          origin: the top end
#   Lair_Egg_Sac     the web ball pulled into a teardrop           origin: its tip (where a thread holds it)
#   Lair_Egg_Sac_Cluster  three sacs on a small web                origin: the web's hub
# Webs stand upright in Blender's XZ plane, facing -Y. Writes Exports/Lair/Lair_Props.blend (its own file), the egg sac
# textures in Assets/M_Textures/Lair/ and one FBX per piece in Assets/Models/Lair/ (the project's export settings).
import bpy, bmesh, math, os, sys
import numpy as np
from mathutils import Vector, Matrix

REPO = r"K:/Repository/429R"
SRC = sys.argv[sys.argv.index("--") + 1]
# The user fixed the pieces by hand in Lair_Props.blend (2026-11-25, all on Spider_Web's grey patch): rebuilding would
# throw that away. To re-export their file use export_lair_props.py; pass --force to rebuild from scratch anyway.
if os.path.exists(REPO + "/Exports/Lair/Lair_Props.blend") and "--force" not in sys.argv:
    raise SystemExit("Lair_Props.blend exists (the user's hand-fixed pieces): use export_lair_props.py, or --force.")
BLEND = REPO + "/Exports/Lair/Lair_Props.blend"
FBX_DIR = REPO + "/Assets/Models/Lair/"
TEX_DIR = REPO + "/Assets/M_Textures/Lair/"
os.makedirs(FBX_DIR, exist_ok=True)
os.makedirs(TEX_DIR, exist_ok=True)

for o in list(bpy.data.objects):
    bpy.data.objects.remove(o)
for m in list(bpy.data.meshes):
    bpy.data.meshes.remove(m)
with bpy.data.libraries.load(SRC, link=False) as (src, dst):
    dst.meshes = list(src.meshes)
web_src = bpy.data.meshes["Spider_Web"]
ball_src = next(m for m in bpy.data.meshes if m != web_src)
print("SOURCE", web_src.name, len(web_src.vertices), ball_src.name, len(ball_src.vertices))

col = bpy.data.collections.new("Lair (export)")
bpy.context.scene.collection.children.link(col)


def bm_copy(me):
    bm = bmesh.new()
    bm.from_mesh(me)
    return bm


def finish(bm, name, location):
    me = bpy.data.meshes.new(name)
    bm.normal_update()
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    col.objects.link(ob)
    ob.location = location
    return ob


def keep_faces(bm, keep):
    gone = [f for f in bm.faces if not keep(f)]
    bmesh.ops.delete(bm, geom=gone, context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')


def two_sided(bm):
    # The user's material (the furniture atlas) draws only the front of a face: every web strand gets a back face too
    # (a flipped copy), so it shows from both sides with the same texture.
    faces = [f for f in bm.faces if f.material_index == 0]
    dup = bmesh.ops.duplicate(bm, geom=faces)
    new_faces = [g for g in dup["geom"] if isinstance(g, bmesh.types.BMFace)]
    bmesh.ops.reverse_faces(bm, faces=new_faces)


def upright(bm):
    # The source web lies in XY (facing +Z); stand it up in XZ, facing -Y.
    bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, 'X'))


# ---- Webs ----
# The user's style (2026-11-25: "I want your new props follow my style and texture"): every web keeps Spider_Web's own
# material (the furniture atlas) and its own UVs, so its strands show the same grey painted patch.
ATLAS = REPO + "/Assets/M_Textures/BedRoom_Set_1/NEW/Bedroom_Furniture_Set1_Bedroom_Furniture_Set1_AlbedoTransparency.png"
atlas_img = bpy.data.images.load(ATLAS, check_existing=True)
web_mat = web_src.materials[0] if len(web_src.materials) and web_src.materials[0] else bpy.data.materials.new("Bedroom_Furniture_Set1")
if web_mat.use_nodes:
    for n in web_mat.node_tree.nodes:
        if n.type == "TEX_IMAGE":
            n.image = atlas_img

# 1. Round: the whole web, 1.3x (strands about 1.6 cm, about 1 m across).
bm = bm_copy(web_src)
bmesh.ops.scale(bm, verts=bm.verts, vec=(1.3, 1.3, 1.0))
upright(bm)
two_sided(bm)
round_ob = finish(bm, "Lair_Web_Round", (0, 0, 1.5))

# 2. Corner: the lower half (face centres below the hub), 1.25x; hung across a corner, its straight top under the
#    ceiling and its ends on the two walls.
bm = bm_copy(web_src)
# A clean cut at the hub's height: the half below stays (its cut strands end on the ceiling).
geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
bmesh.ops.bisect_plane(bm, geom=geom, dist=0.0005, plane_co=(0, 0.004, 0), plane_no=(0, 1, 0), clear_outer=True)
bmesh.ops.scale(bm, verts=bm.verts, vec=(1.25, 1.25, 1.0))
upright(bm)
two_sided(bm)
corner_ob = finish(bm, "Lair_Web_Corner", (1.4, 0, 1.5))

# 3. Sheet: stretched wide and low, sagging away from the viewer and drooping in the middle (between two posts).
bm = bm_copy(web_src)
bmesh.ops.scale(bm, verts=bm.verts, vec=(1.7, 0.75, 1.0))
half = max(abs(v.co.x) for v in bm.verts)
for v in bm.verts:
    t = 1.0 - min(1.0, (v.co.x / half) ** 2)
    v.co.z += 0.07 * t            # belly (becomes +Y: away from the viewer)
    v.co.y -= 0.06 * t            # droop (becomes -Z: down)
upright(bm)
two_sided(bm)
sheet_ob = finish(bm, "Lair_Web_Sheet", (2.9, 0, 1.5))

# 4. Strand: the long up-left spoke only, laid along -Z from its hub end, 1 m long.
bm = bm_copy(web_src)
tip = max(bm.verts, key=lambda v: (v.co.x < 0 and v.co.y > 0) * v.co.length)
axis = Vector((tip.co.x, tip.co.y, 0)).normalized()
side = Vector((-axis.y, axis.x, 0))


def on_spoke(f):
    c = f.calc_center_median()
    along = c.dot(axis)
    return along > -0.01 and abs(c.dot(side)) < 0.012


keep_faces(bm, on_spoke)
length = max(v.co.dot(axis) for v in bm.verts) - min(v.co.dot(axis) for v in bm.verts)
start = min(v.co.dot(axis) for v in bm.verts)
for v in bm.verts:
    a = (v.co.dot(axis) - start) / max(length, 1e-6)
    s = v.co.dot(side)
    v.co = Vector((s * 0.9, 0.0, -a * 1.0))
two_sided(bm)
strand_ob = finish(bm, "Lair_Web_Strand", (4.2, 0, 2.5))
print("STRAND faces", len(strand_ob.data.polygons), "from a spoke", round(length, 3), "m long")

# ---- Egg sacs ----
# The sac keeps Web_Ball's own UVs; its texture is the web ball's painted patch from the atlas (the swirl), recoloured
# orange, glowing green through its lighter strokes. The patch is shared with other props, so the sac gets its own
# small texture: the UVs are only rescaled onto it (the layout stays).
sac_mat = bpy.data.materials.new("Lair_Egg_Sac")
sac_mat.use_nodes = True
nodes = sac_mat.node_tree.nodes
bsdf = next(n for n in nodes if n.type == "BSDF_PRINCIPLED")
bsdf.inputs["Roughness"].default_value = 0.65
AW, AH = atlas_img.size
atlas_px = np.array(atlas_img.pixels[:], dtype=np.float32).reshape(AH, AW, 4)
buv = np.array([(l.uv.x, l.uv.y) for l in ball_src.uv_layers.active.data])
PAD = 24
cx0 = int(max(0, np.floor(buv[:, 0].min() * AW) - PAD)); cx1 = int(min(AW, np.ceil(buv[:, 0].max() * AW) + PAD))
cy0 = int(max(0, np.floor(buv[:, 1].min() * AH) - PAD)); cy1 = int(min(AH, np.ceil(buv[:, 1].max() * AH) + PAD))
patch = atlas_px[cy0:cy1, cx0:cx1, :3]
lum = patch @ np.array([0.299, 0.587, 0.114], dtype=np.float32)
# The range of the grey patch itself (low colour, not the neighbouring wood and orange).
grey = (patch.max(axis=2) - patch.min(axis=2) < 0.08) & (lum > 0.25)
lo, hi = np.percentile(lum[grey], 3), np.percentile(lum[grey], 97)
g = np.clip((lum - lo) / max(hi - lo, 1e-4), 0, 1)[..., None]
dark = np.array([0.50, 0.20, 0.05]); mid = np.array([0.92, 0.46, 0.11]); light = np.array([1.0, 0.72, 0.36])
base = np.where(g < 0.5, dark + (mid - dark) * (g / 0.5), mid + (light - mid) * ((g - 0.5) / 0.5))
glow_amt = (0.08 + 0.75 * np.clip((g - 0.55) / 0.45, 0, 1) ** 1.6)
emis = np.array([0.35, 1.0, 0.25]) * glow_amt
print("SAC PATCH", cx0, cx1, cy0, cy1, "lum", round(float(lo), 3), round(float(hi), 3))


def save_png(name, rgb):
    h, w = rgb.shape[:2]
    img = bpy.data.images.new(name, w, h, alpha=False)
    px = np.ones((h, w, 4), dtype=np.float32)
    px[..., :3] = np.clip(rgb, 0, 1)
    img.pixels.foreach_set(px.ravel())
    img.filepath_raw = TEX_DIR + name + ".png"
    img.file_format = 'PNG'
    img.save()
    return img


base_img = save_png("Lair_Egg_Sac", base)
emis_img = save_png("Lair_Egg_Sac_Glow", emis)
tex = nodes.new("ShaderNodeTexImage")
tex.image = base_img
sac_mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
etex = nodes.new("ShaderNodeTexImage")
etex.image = emis_img
sac_mat.node_tree.links.new(etex.outputs["Color"], bsdf.inputs["Emission Color"])
bsdf.inputs["Emission Strength"].default_value = 1.0


def egg_sac_bm(scale=1.0, seed=0):
    bm = bm_copy(ball_src)
    zmax = max(v.co.z for v in bm.verts)
    r = zmax
    rng = np.random.default_rng(seed)
    for vtx in bm.verts:
        t = vtx.co.z / r                      # -1 bottom .. 1 top
        # Plump at the bottom, a short soft point on top.
        squeeze = 1.0 - 0.45 * max(0.0, t) ** 2.2
        lift = 0.32 * r * max(0.0, t) ** 3
        belly = 1.0 + 0.08 * max(0.0, -t) * (1 + t)
        bump = 1.0 + 0.04 * (rng.random() - 0.5)
        vtx.co.x *= squeeze * bump * belly
        vtx.co.y *= squeeze * bump * belly
        vtx.co.z = vtx.co.z + lift
    # The web ball's own UVs, rescaled onto its patch texture (the layout stays).
    uv = bm.loops.layers.uv.active
    for f in bm.faces:
        for l in f.loops:
            x, y = l[uv].uv
            l[uv].uv = ((x * AW - cx0) / (cx1 - cx0), (y * AH - cy0) / (cy1 - cy0))
    zs = [vtx.co.z for vtx in bm.verts]
    z1 = max(zs)
    for f in bm.faces:
        f.smooth = True
    # Origin at the tip.
    bmesh.ops.translate(bm, verts=bm.verts, vec=(0, 0, -z1))
    bmesh.ops.scale(bm, verts=bm.verts, vec=(scale, scale, scale))
    return bm


bm = egg_sac_bm(1.0, 1)
sac_ob = finish(bm, "Lair_Egg_Sac", (5.4, 0, 1.6))
sac_ob.data.materials.append(sac_mat)

# Cluster: three sacs on a small web.
bm = bm_copy(web_src)
bmesh.ops.scale(bm, verts=bm.verts, vec=(0.55, 0.55, 1.0))
upright(bm)
for f in bm.faces:
    f.material_index = 0
two_sided(bm)
spots = [((-0.07, -0.025, 0.05), 0.85, 2), ((0.08, -0.03, 0.02), 1.0, 3), ((0.0, -0.04, -0.07), 0.9, 4)]
for (x, y, z), s, seed in spots:
    sac = egg_sac_bm(s, seed)
    tmp = bpy.data.meshes.new("_claude_tmp")
    sac.to_mesh(tmp)
    sac.free()
    for f in tmp.polygons:
        f.material_index = 1
    tmp.transform(Matrix.Translation((x, y, z + 0.08 * s)))
    bm.from_mesh(tmp)
    bpy.data.meshes.remove(tmp)
cluster_ob = finish(bm, "Lair_Egg_Sac_Cluster", (6.6, 0, 1.5))
cluster_ob.data.materials.append(web_mat)
cluster_ob.data.materials.append(sac_mat)

for ob in (round_ob, corner_ob, sheet_ob, strand_ob):
    ob.data.materials.append(web_mat)

for ob in col.objects:
    me = ob.data
    print("PIECE", ob.name, "faces", len(me.polygons), "tris", sum(len(p.vertices) - 2 for p in me.polygons),
          "size", tuple(round(d, 3) for d in ob.dimensions))

# ---- Export, one FBX per piece (at the origin), then the .blend ----
for ob in list(col.objects):
    keep = ob.location.copy()
    ob.location = (0, 0, 0)
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    out = FBX_DIR + ob.name + ".fbx"
    bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={'MESH'},
                             apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
                             axis_forward='-Z', axis_up='Y', mesh_smooth_type='OFF', add_leaf_bones=False,
                             bake_anim=False, path_mode='AUTO', use_mesh_modifiers=True)
    ob.location = keep
    print("EXPORTED", out)

os.makedirs(os.path.dirname(BLEND), exist_ok=True)
for m in (web_src, ball_src):
    m.use_fake_user = True
bpy.ops.wm.save_as_mainfile(filepath=BLEND)
print("SAVED", BLEND)
