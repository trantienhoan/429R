"""Swaps the assets into Steam_Art.blend: the "429" logo (no pumpkin top since v4), the user's own game pumpkin with a
menace face (Menace_Pumpkin.blend: the hero + 8 small copies) and the far background. Opens Exports/Steam_Art/Steam_Art.blend, APPENDS from Exports/Steam_Art/assets/ and SAVES OVER it.
Safe to run again: each run removes what the previous run appended and appends the assets fresh (so a rebuilt asset
file is picked up). The old pieces are not deleted: they move to the excluded collection V2_Replaced.
The first run backs the file up to variants/Steam_Art_before_v2.blend.
  "E:/Steam/steamapps/common/Blender/blender.exe" -b Exports/Steam_Art/Steam_Art.blend --python Exports/Steam_Art/assemble_v2.py
Then run knight_pass.py, then lighting_pass.py (it sets the logo sizes, lights and framing).
"""
import bpy, os, shutil
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))   # this folder, on whichever PC (was C:/Github_Repository/...)
SRC = os.path.join(HERE, "Steam_Art.blend")
BACKUP = os.path.join(HERE, "variants", "Steam_Art_before_v2.blend")
ASSETS = os.path.join(HERE, "assets")
if os.path.normcase(os.path.abspath(bpy.data.filepath or "")) != os.path.normcase(os.path.abspath(SRC)):
    bpy.ops.wm.open_mainfile(filepath=SRC)
sc = bpy.context.scene
OB = bpy.data.objects
if not sc.get("assembled_v2") and not os.path.exists(BACKUP):
    os.makedirs(os.path.dirname(BACKUP), exist_ok=True)
    shutil.copy2(SRC, BACKUP)
    print("BACKUP", BACKUP)

APPENDED = ["Logo_429_v2", "Logo_429_v2_Reflectors", "Menace_Pumpkin", "Menace_Pumpkin_Small", "Background_v2"]
LEGACY = ["Hero_Pumpkin_v2", "Small_Pumpkin_v2"]       # v2/v3 pumpkins: removed if an older run left them
# v4: the hero sits on the old game pumpkin's own transform (the menace mesh is in pumpkin_0's frame), nudged
# camera-left (+X) so the knight's waving glove (screen-left of his helmet) is not hidden behind it.
HERO_NUDGE = Vector((0.07, 0.0, 0.0))
HERO_SCALE = 1.0
OLD_LOGO_W = 1.515            # width of the old Logo_429_Mesh (Arial Black "429")


def get_coll(name, parent=None):
    c = bpy.data.collections.get(name)
    if c is None:
        c = bpy.data.collections.new(name)
    parent = parent or sc.collection
    if c.name not in parent.children:
        parent.children.link(c)
    return c


def layer_coll(name, lc=None):
    lc = lc or bpy.context.view_layer.layer_collection
    if lc.collection.name == name:
        return lc
    for ch in lc.children:
        r = layer_coll(name, ch)
        if r:
            return r
    return None


def hide_coll(name):
    """Kept in the view layer (so the objects keep valid world matrices for the scripts) but never rendered or shown."""
    lc = layer_coll(name)
    lc.exclude = False
    lc.hide_viewport = True
    lc.collection.hide_render = True
def move_to(o, c):
    for old in list(o.users_collection):
        if old != c:
            old.objects.unlink(o)
    if o.name not in c.objects:
        c.objects.link(o)




# ---------------------------------------------------------------- 1. undo the previous run
# lighting_pass's LP_* objects (logo reflector copies, halos, lights) hold on to the appended meshes and materials;
# drop them (lighting_pass rebuilds them) and point the logos back at the old mesh, so the purge frees every v2 datablock.
for o in [o for o in OB if o.name.startswith("LP_")]:
    bpy.data.objects.remove(o, do_unlink=True)
if "Logo_429_Mesh" in OB:
    for lg in [o for o in OB if o.name.startswith("LOGO_") and o.type == "MESH"]:
        lg.data = OB["Logo_429_Mesh"].data
for cname in ["V2_Small_Pumpkins"] + APPENDED + LEGACY:
    c = bpy.data.collections.get(cname)
    if c:
        for o in list(c.all_objects):
            bpy.data.objects.remove(o, do_unlink=True)
for name in ("Logo_429_v2_Mesh", "Logo_429_v2_Halo_Src"):
    if name in OB:
        bpy.data.objects.remove(OB[name], do_unlink=True)
for cname in ["V2_Small_Pumpkins", "V2_Templates"] + APPENDED + LEGACY:
    c = bpy.data.collections.get(cname)
    if c:
        bpy.data.collections.remove(c)
for _ in range(3):
    bpy.data.orphans_purge(do_local_ids=True, do_linked_ids=True, do_recursive=True)

# ---------------------------------------------------------------- 2. old pieces -> V2_Replaced (excluded, kept for reference)
C_OLD = get_coll("V2_Replaced")
OLD_PREFIX = ("Castle", "Mountain_", "Bat_", "Small_Pumpkin_Glow_")
old_small = sorted([o for o in OB if o.type == "MESH" and o.name.startswith("Small_Pumpkin_") and o.name[14:].isdigit()],
                   key=lambda o: o.name)
for o in list(OB):
    if (o.name.startswith(OLD_PREFIX) or o.name in ("Far_Ground", "Moon", "Haze", "Hero_Pumpkin", "Logo_429_Mesh")
            or o in old_small):
        move_to(o, C_OLD)
        o.hide_render = True
hide_coll("V2_Replaced")
hero_old = OB["Hero_Pumpkin"]
logo_old = OB["Logo_429_Mesh"]

# ---------------------------------------------------------------- 3. append the assets
def append(blend, names):
    with bpy.data.libraries.load(os.path.join(ASSETS, blend), link=False) as (src, dst):
        dst.collections = [n for n in names if n in src.collections]
    return {c.name: c for c in dst.collections if c}

got = {}
got.update(append("Logo_429.blend", ["Logo_429_v2"]))
got.update(append("Menace_Pumpkin.blend", ["Menace_Pumpkin", "Menace_Pumpkin_Small"]))
got.update(append("Background.blend", ["Background_v2"]))
for n in ("Logo_429_v2", "Menace_Pumpkin", "Menace_Pumpkin_Small", "Background_v2"):
    if n not in got or got[n].name != n:
        raise RuntimeError(f"append failed or name clash: {n} -> {got.get(n)}")
C_TPL = get_coll("V2_Templates")
for n in ("Logo_429_v2", "Menace_Pumpkin_Small"):
    C_TPL.children.link(got[n])
sc.collection.children.link(got["Menace_Pumpkin"])
sc.collection.children.link(got["Background_v2"])
bpy.context.view_layer.update()

# ---------------------------------------------------------------- 4. logo: one joined mesh shared by every LOGO_* camera child
logo_c = got["Logo_429_v2"]
dg = bpy.context.evaluated_depsgraph_get()
tmp = get_coll("V2_Tmp_Join")
parts = []
halo_src = None
for src in sorted(logo_c.objects, key=lambda o: o.name):
    if src.type != "MESH":
        continue
    me = bpy.data.meshes.new_from_object(src.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    me.transform(src.matrix_world)
    plate = 1.0 if src.get("logo_plate") else 0.0
    a = me.attributes.new("logo_plate", "FLOAT", "POINT")
    a.data.foreach_set("value", [plate] * len(me.vertices))
    o = bpy.data.objects.new("V2_Join_" + src.name, me)
    tmp.objects.link(o)
    parts.append(o)
    if src.name == "Logo_429_Backplate":           # halo source: the outer silhouette only
        hme = me.copy()
        hme.name = "Logo_429_v2_Halo_Mesh"
        halo_src = bpy.data.objects.new("Logo_429_v2_Halo_Src", hme)
        C_TPL.objects.link(halo_src)
bpy.context.view_layer.update()
with bpy.context.temp_override(active_object=parts[0], object=parts[0], selected_objects=parts, selected_editable_objects=parts):
    bpy.ops.object.join()
logo_obj = parts[0]
logo_obj.name = "Logo_429_v2_Mesh"
logo_obj.data.name = "Logo_429_v2_Mesh"
move_to(logo_obj, C_TPL)
bpy.data.collections.remove(tmp)
# the back plate's darker look came from an object property; joined, it reads the per-vertex attribute instead
for m in logo_obj.data.materials:
    for n in m.node_tree.nodes:
        if n.type == "ATTRIBUTE" and n.attribute_name == "logo_plate":
            n.attribute_type = "GEOMETRY"
logo_me = logo_obj.data
logo_w = logo_obj.dimensions.x
for lg in [o for o in OB if o.name.startswith("LOGO_") and o.type == "MESH"]:
    if "v2_width" not in lg:                       # camera-space width of the old logo, remembered on the first run
        lg["v2_width"] = lg.scale.x * OLD_LOGO_W
    lg.data = logo_me
    s = lg["v2_width"] / logo_w
    lg.scale = (s, s, s)
print("LOGO mesh", logo_me.name, "width", round(logo_w, 3), "polys", len(logo_me.polygons))

# ---------------------------------------------------------------- 5. hero pumpkin: the menace pumpkin on the old pumpkin's transform
# The menace mesh is pumpkin_0's own mesh, recarved, in pumpkin_0's own frame: the old object's matrix places it exactly.
root = OB["Menace_Pumpkin_Root"]
root.matrix_world = (Matrix.Translation(HERO_NUDGE) @ hero_old.matrix_basis.copy()) @ Matrix.Scale(HERO_SCALE, 4)
print("HERO at", tuple(round(v, 3) for v in root.matrix_world.translation), "scale", round(root.matrix_world.to_scale().x, 3))

# ---------------------------------------------------------------- 6. small pumpkins: real copies (light linking needs real objects)
tpl_root = OB["Menace_Pumpkin_Small_Root"]
tpl_parts = [o for o in got["Menace_Pumpkin_Small"].objects if o.parent == tpl_root]
C_SP = get_coll("V2_Small_Pumpkins")
HERO_S = hero_old.scale.x
for old in old_small:
    i = old.name[14:]
    r = bpy.data.objects.new(f"Menace_Pumpkin_Small_{i}_Root", None)
    r.empty_display_size = 0.05
    C_SP.objects.link(r)
    r.matrix_world = old.matrix_basis.copy()             # copies of pumpkin_0 too: same frame, scale s * 0.828
    r["size_k"] = old.scale.x / HERO_S                  # size relative to the hero (0.2-0.32); lighting_pass scales lights by it
    for p in tpl_parts:
        c = p.copy()
        if p.type == "LIGHT":
            c.data = p.data.copy()
        c.name = f"Menace_Pumpkin_Small_{i}_" + p.name[len("Menace_Pumpkin_Small_"):]
        C_SP.objects.link(c)
        c.parent = r
        c.matrix_parent_inverse = Matrix.Identity(4)
        c.matrix_basis = tpl_root.matrix_world.inverted() @ p.matrix_world
hide_coll("V2_Templates")

# ---------------------------------------------------------------- 7. background: the scene's moon object stays (lighting_pass aims by it)
bg_moon = OB.get("BG2_Moon")
if bg_moon:
    d = (bg_moon.matrix_world.translation - OB["Moon"].matrix_world.translation).length
    print("BG2_Moon offset from old Moon", round(d, 3))

sc["assembled_v2"] = 1
bpy.ops.wm.save_as_mainfile(filepath=SRC, copy=False)
print("SAVED", SRC)
