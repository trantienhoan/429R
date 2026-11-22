# Builds Exports/Remodel/Furniture_Fix/Bedroom_Furniture_Fix.blend (user, 2026-11-20: "bring their 3D model into a new
# blend file, even the broken parts prefab, we are going to fix them there a bit, then you help me re-import them").
# The storage room's + bedroom's breakable furniture (the Set Up Breakable Furniture pieces in those sets) and their
# broken-pieces FBX, each source FBX imported into its own collection "<file>.fbx" (custom props: source path, layout
# offset), grouped per piece: "<Piece>" > "<Piece> - whole" / "<Piece> - broken". Laid out in a row (whole in front,
# broken behind); export_furniture_fix.py moves them back and exports each collection to its FBX.
# Run: blender -b --factory-startup --python build_furniture_fix.py -- <furniture.json> <out.blend>
import bpy, json, sys, os, math
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
data = json.load(open(args[0], encoding="utf-8"))
out = args[1]
root = data["root"]

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'

images = {}
def image(path):
    if not path:
        return None
    if path not in images:
        full = os.path.join(root, path)
        images[path] = bpy.data.images.load(full, check_existing=True) if os.path.exists(full) else None
    return images[path]

import re
def base_name(name):
    return re.sub(r"\.\d{3}$", "", name)

tex_for = {}   # material base name -> the game texture (from Unity, slot by slot)

def show_texture(mat, tex_path):
    """A clean material showing the game texture (its name stays the FBX's)."""
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    outn = nt.nodes.new("ShaderNodeOutputMaterial"); outn.location = (300, 0)
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs["BSDF"], outn.inputs["Surface"])
    bsdf.inputs["Roughness"].default_value = 0.85
    img = image(tex_path)
    if img is not None:
        tex = nt.nodes.new("ShaderNodeTexImage"); tex.location = (-320, 0)
        tex.image = img
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])

top = bpy.data.collections.new("Furniture_Fix")
scene.collection.children.link(top)
report = []
x = 0.0
label_spots = []
imported_files = {}
for group in data["groups"]:
    gcol = bpy.data.collections.new(group["name"])
    top.children.link(gcol)
    sides = {False: bpy.data.collections.new(group["name"] + " - whole"), True: bpy.data.collections.new(group["name"] + " - broken")}
    used_sides = set()
    part_cols = []
    for part in group["parts"]:
        fbx = part["fbx"]
        broken = part["broken"]
        if fbx in imported_files:
            report.append(f"{group['name']}: {os.path.basename(fbx)} already in {imported_files[fbx]} (shared)")
            continue
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=os.path.join(root, fbx))
        new = [o for o in bpy.data.objects if o not in before]
        col = bpy.data.collections.new(os.path.basename(fbx))
        col["source_fbx"] = fbx
        col["broken"] = broken
        for o in new:
            for c in list(o.users_collection):
                c.objects.unlink(o)
            col.objects.link(o)
        # One material per name, as in the game (each FBX import brings its own copy: "Wood_Furniture.007"), and which
        # game texture it shows (from Unity, slot by slot; mesh names as in Unity).
        for o in new:
            if o.type != "MESH":
                continue
            texs = part["meshes"].get(o.data.name) or part["meshes"].get(o.name) or []
            for i, slot in enumerate(o.material_slots):
                if slot.material is None:
                    continue
                base = base_name(slot.material.name)
                keep = bpy.data.materials.get(base) or slot.material
                if keep.name != base:
                    keep.name = base
                slot.material = keep
                if i < len(texs) and texs[i] and base not in tex_for:
                    tex_for[base] = texs[i]
        if col.name not in [c.name for c in sides[broken].children]:
            sides[broken].children.link(col)
        used_sides.add(broken)
        part_cols.append(col)
        imported_files[fbx] = group["name"]
        report.append(f"{group['name']}: {os.path.basename(fbx)} -> {len(new)} objects ({', '.join(sorted(o.name for o in new))[:160]})")
    for b in (False, True):
        if b in used_sides:
            gcol.children.link(sides[b])
    # Layout: this piece's whole set at x, its broken set behind it (+y), both on the floor.
    def bounds(cols):
        lo = Vector((1e9, 1e9, 1e9)); hi = Vector((-1e9, -1e9, -1e9))
        for c in cols:
            for o in c.all_objects:
                if o.type != "MESH":
                    continue
                for v in o.bound_box:
                    w = o.matrix_world @ Vector(v)
                    lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
        return lo, hi
    whole_cols = [c for c in part_cols if not c["broken"]]
    broken_cols = [c for c in part_cols if c["broken"]]
    width = 0.0
    y_whole_back = 0.0
    for cols, is_broken in ((whole_cols, False), (broken_cols, True)):
        if not cols:
            continue
        # Whole on the left, its broken pieces right beside it (as they sit in the game, assembled), on the floor.
        bpy.context.view_layer.update()
        lo, hi = bounds(cols)
        size = hi - lo
        offset = Vector((x - lo.x, -lo.y - size.y / 2.0, -lo.z))
        for c in cols:
            c["layout_offset"] = list(offset)
            for o in c.objects:
                if o.parent is None:
                    o.location += offset
        bpy.context.view_layer.update()
        label_spots.append((group["name"] + (" (broken)" if is_broken else ""), x, size.x))
        x += max(size.x, 0.4) + (0.6 if not is_broken and broken_cols else 1.6)
    if not broken_cols:
        x += 0.0

# Clean materials with the game textures; the FBX's copies and its dead image links (old K:/E: paths) go.
for m in list(bpy.data.materials):
    if m.users == 0:
        bpy.data.materials.remove(m)
# By name (Blender's slot order isn't Unity's): the game material each FBX material became.
BY_NAME = {
    "Wood_Furniture": "Assets/M_Textures/Combos/Wood_Stylize_Set_2/wood_stylize_furniture_albedo.png",
    "Sofa_Furniture": "Assets/M_Textures/Combos/Wood_Stylize_Set_2/wood_stylize_furniture_albedo.png",
    "Bedroom_Wood_Floor": "Assets/M_Textures/Combos/Wood_Floor_Stylize_Set_2/wood_floor2_albedo.png",
    "Bedroom_Metal": "Assets/M_Textures/Combos/Metal_Stylize_Set_2/medieval_metal_albedo_set2.png",
    "Bedroom_Furniture_Set1": "Assets/M_Textures/BedRoom_Set_1/NEW/Bedroom_Furniture_Set1_Bedroom_Furniture_Set1_AlbedoTransparency.png",
    "Bedroom_Furniture_Set2": "Assets/M_Textures/Bedroom_Furniture_Set2/furniture_set2_Bedroom_Furniture_Set2_Diffuse.png",
}
tex_for.update(BY_NAME)
for m in bpy.data.materials:
    show_texture(m, tex_for.get(m.name, ""))
for img in list(bpy.data.images):
    if img.users == 0:
        bpy.data.images.remove(img)
report.append("materials: " + ", ".join(f"{m.name} = {os.path.basename(tex_for.get(m.name, '(none)'))}" for m in bpy.data.materials))

# A label per piece (text objects, not exported: outside the FBX collections).
labels = bpy.data.collections.new("Labels")
scene.collection.children.link(labels)
for text, lx, w in label_spots:
    curve = bpy.data.curves.new("Label_" + text, "FONT")
    curve.body = text.replace(" (broken)", "\nbroken")
    curve.size = 0.16
    curve.align_x = 'CENTER'
    t = bpy.data.objects.new("Label_" + text, curve)
    t.location = (lx + w / 2.0, -1.4, 0.0)
    t.rotation_euler = (math.radians(70), 0.0, 0.0)
    labels.objects.link(t)

# Light + a simple world, so it reads when opened.
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
sun.rotation_euler = (0.8, 0.2, 0.6)
scene.collection.objects.link(sun)
world = bpy.data.worlds.new("World"); scene.world = world
world.use_nodes = True
bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
bg.inputs["Strength"].default_value = 0.6

os.makedirs(os.path.dirname(out), exist_ok=True)
bpy.ops.file.pack_all() if False else None
bpy.ops.wm.save_as_mainfile(filepath=out, relative_remap=True)
print("REPORT")
print("\n".join(report))
mats = sorted({m.name for m in bpy.data.materials})
print("MATERIALS", mats)
print("OBJECTS", len([o for o in bpy.data.objects if o.type == "MESH"]))
