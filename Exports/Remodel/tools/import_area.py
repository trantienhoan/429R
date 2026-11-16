"""Builds a remodel .blend for one area from the game's current models (2026-11-16).
  blender -b --factory-startup --python Exports/Remodel/tools/import_area.py -- <plan.json> <out.blend>
Every model gets an Empty "MODEL <file stem>" (custom props: source = its game file, prefab, section) holding the imported
objects, laid out in rows by section; its old broken pieces sit hidden in "<stem> (old broken)" for reference. The game's
files are only read. The export script (export_area.py) puts each model back over its source file."""
import bpy, sys, os, json
from mathutils import Vector
ARGS = sys.argv[sys.argv.index("--") + 1:]
PLAN, OUT = ARGS[0], ARGS[1]
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))   # the repo
plan = json.load(open(PLAN))
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'

def coll(name, parent=None):
    c = bpy.data.collections.get(name) or bpy.data.collections.new(name)
    if c.name not in (parent or scene.collection).children: (parent or scene.collection).children.link(c)
    return c

def import_fbx(path, into):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path, use_custom_normals=True, ignore_leaf_bones=True, automatic_bone_orientation=False)
    new = [o for o in bpy.data.objects if o not in before]
    for o in new:
        for c in list(o.users_collection): c.objects.unlink(o)
        into.objects.link(o)
    return new

def bounds(objs):
    pts = [o.matrix_world @ Vector(c) for o in objs if o.type == 'MESH' for c in o.bound_box]
    if not pts: return Vector((0, 0, 0)), Vector((1, 1, 1))
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi

rows = {}
report = []
for item in plan:
    src = os.path.join(ROOT, item["model"])
    stem = os.path.splitext(os.path.basename(src))[0]
    sec = coll(item["section"])
    mc = coll(stem, sec)
    objs = import_fbx(src, mc)
    empty = bpy.data.objects.new("MODEL " + stem, None)
    empty.empty_display_type = 'PLAIN_AXES'
    empty.empty_display_size = 0.3
    mc.objects.link(empty)
    empty["source"] = item["model"]
    empty["prefab"] = item["prefab"]
    empty["section"] = item["section"]
    for o in objs:
        if o.parent is None:
            o.parent = empty
    # old broken pieces, hidden, same spot
    for b in item.get("broken", []):
        bc = coll(stem + " (old broken)", mc)
        bobjs = import_fbx(os.path.join(ROOT, b), bc)
        for o in bobjs:
            if o.parent is None: o.parent = empty
        bc.hide_viewport = True
        bc.hide_render = True
    lo, hi = bounds(objs)
    size = hi - lo
    # the room stays where it is (the stage); everything else in rows beside it
    if item["section"] == "A_Rooftop" and stem == "Rooftop_Base_Z":
        empty.location = (0, 0, 0)
    else:
        r = rows.setdefault(item["section"], {"x": 30.0})
        y = {"A_Rooftop": -10, "B_Tree": 0, "C_Start": 12, "D_Round2": 22, "E_Sky": 60}[item["section"]]
        empty.location = (r["x"] - lo.x, y, -lo.z)
        r["x"] += max(1.5, size.x) + 2.0
    report.append("%-10s %-28s %3d objs  size %s" % (item["section"], stem, len(objs), tuple(round(v, 2) for v in size)))

# labels above each model
lab = coll("Labels")
for e in [o for o in bpy.data.objects if o.name.startswith("MODEL ")]:
    t = bpy.data.curves.new(e.name[6:] + "_label", 'FONT'); t.body = e.name[6:] + "\n(" + e["prefab"] + ")"; t.size = 0.35
    to = bpy.data.objects.new(e.name[6:] + " label", t); lab.objects.link(to)
    to.location = e.location + Vector((0, -1.2, 0)); to.rotation_euler = (1.5708, 0, 0)
lab.hide_render = True
bpy.ops.wm.save_as_mainfile(filepath=OUT)
print("\n".join(report))
print("SAVED", OUT)
