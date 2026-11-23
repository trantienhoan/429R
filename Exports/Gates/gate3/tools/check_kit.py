"""Checks the Ball Valley kit .blend before an export to Unity (gate 3, 2026-11-23). Reads only: never saves the .blend.

For every piece (a sub-collection of BallValley_Kit holding BallValley_Kit_<Piece>):
- what will export: only MESH objects named BallValley_Kit_<Piece> (each to its own FBX); other objects in the piece's
  collection are reported (they would be left out, unless joined into the piece);
- rotation/scale not applied (Unity keeps only the mesh, so an object's own rotation/scale would be lost);
- material slots other than BallValley_Palette (Unity puts ONE material on each piece: the palette's);
- faces whose UVs don't sit inside one palette swatch column (new faces need palette UVs, else they show stripes);
- size, triangles, the lowest point, and the game-critical shapes: each hole's pit opening (radius and centre from the
  piece's pivot, as Unity measures it: the ring at lawn level where the pit wall drops below -1 m), the rim's length,
  the giant pumpkin's radius and its glowing face (swatch glow_face, used to turn it to face forward).
Writes a JSON summary; with --before <json> it also lists what changed since then.

Run (headless):
  blender -b Exports/Gates/gate3/BallValley_Kit.blend --python Exports/Gates/gate3/tools/check_kit.py -- <out.json> [--before <old.json>] [--render <folder>]
--render also renders the kit sheet and the Sample_Arena from the concept's angle into <folder> (kit_check_*.png)."""
import bpy, bmesh, json, math, os, sys
from mathutils import Vector

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = ARGS[0] if ARGS and not ARGS[0].startswith("--") else None
BEFORE = ARGS[ARGS.index("--before") + 1] if "--before" in ARGS else None
RENDER = ARGS[ARGS.index("--render") + 1] if "--render" in ARGS else None
PREFIX = "BallValley_Kit_"
COLS, SW = 32, 32                     # the palette: 32 swatch columns of 32 px
MARGIN = 2.0 / (COLS * SW)            # a face may come this close to its column's sides (the kit keeps 4 px)
SWATCH_NAMES = ["moss", "moss_dark", "stone", "stone_dark", "root", "bark", "earth", "licorice_red", "licorice_black",
                "jelly_pink", "jelly_green", "jelly_blue", "jelly_purple", "pumpkin", "stem", "grave", "wood", "iron",
                "pit", "glow_purple", "glow_lantern", "glow_face", "glow_pit", "white", "black", "ball_red", "ball_blue",
                "ball_yellow", "ball_green", "ball_purple", "candy_pink", "ball"]
GLOW_FACE = SWATCH_NAMES.index("glow_face")
USED_IN_UNITY = {"Rim_Straight_4m", "Hole_Corner", "Hole_Side", "Tree_Jelly", "Boulder_Mossy", "Gravestone", "Lantern_Post",
                 "Gazebo_Ruined", "Tree_Twisted", "Pumpkin_Lantern", "Giant_Jack_O_Lantern"}

kit = bpy.data.collections.get("BallValley_Kit")
if kit is None:
    raise SystemExit("No BallValley_Kit collection in this file.")
deps = bpy.context.evaluated_depsgraph_get()
# A copy of the .blend away from its Palette folder: point its pictures at gate 3's palette (in memory only).
PALETTE_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Palette")
for img in bpy.data.images:
    if img.filepath and not os.path.exists(bpy.path.abspath(img.filepath)):
        alt = os.path.join(PALETTE_DIR, img.filepath.replace("\\", "/").split("/")[-1])   # ("//x" reads as a network path)
        if os.path.exists(alt):
            img.filepath = alt
            img.reload()


def parts_of(ob):
    """Visible mesh objects parented to the piece: the export joins them into it."""
    return [c for c in ob.children_recursive if c.type == "MESH" and not c.hide_get() and not c.hide_render]


def evaluated_bmesh(ob):
    """The piece as it will export (its parts joined, modifiers, rotation and scale applied, pivot = its origin)."""
    bm = bmesh.new()
    origin = ob.matrix_world.translation.copy()
    for p in [ob] + parts_of(ob):
        ev = p.evaluated_get(deps)
        me = ev.to_mesh()
        part = bmesh.new()
        part.from_mesh(me)
        ev.to_mesh_clear()
        m = p.matrix_world.copy()
        m.translation = m.translation - origin
        part.transform(m)
        tmp = bpy.data.meshes.new("_check_tmp")
        part.to_mesh(tmp)
        part.free()
        if tmp.uv_layers:
            tmp.uv_layers.active.name = "UVMap"   # one UV layer when joined
        bm.from_mesh(tmp)
        bpy.data.meshes.remove(tmp)
    return bm


def opening(bm):
    """FindOpening (ValleyShrink.cs) in Blender's space (Z up): pit-wall triangles reach below -1 m; the ring = their
    vertices within 0.35 m of lawn level."""
    bm.verts.ensure_lookup_table()
    tri = bm.calc_loop_triangles()
    ring = set()
    for t in tri:
        zs = [l.vert.co.z for l in t]
        if all(z > -1.0 for z in zs):
            continue
        for l in t:
            if abs(l.vert.co.z) < 0.35:
                ring.add(l.vert.index)
    if len(ring) < 8:
        return None
    pts = [bm.verts[i].co for i in ring]
    cx = sum(p.x for p in pts) / len(pts)
    cy = sum(p.y for p in pts) / len(pts)
    r = sum(math.hypot(p.x - cx, p.y - cy) for p in pts) / len(pts)
    return {"radius": round(r, 3), "centre": [round(cx, 3), round(cy, 3)], "ring_verts": len(ring)}


report = {"file": bpy.data.filepath, "pieces": {}}
for coll in kit.children:
    short = coll.name[len(PREFIX):] if coll.name.startswith(PREFIX) else coll.name
    meshes = [o for o in coll.all_objects if o.type == "MESH"]
    named = [o for o in meshes if o.name == PREFIX + short]
    info = {"collection": coll.name, "used_in_unity": short in USED_IN_UNITY, "problems": [], "notes": []}
    parts = parts_of(named[0]) if named else []
    others = [o.name for o in coll.all_objects if o not in named and o not in parts]
    if not named:
        info["problems"].append("no object named %s%s: nothing of this piece exports" % (PREFIX, short))
    if others:
        info["problems"].append("left out of the export (parent them to the piece, Ctrl+P, or join them in with Ctrl+J; "
                                "hidden ones stay out): " + ", ".join(others))
    for ob in named[:1]:
        if parts:
            info["notes"].append("joined in on export: " + ", ".join(p.name for p in parts))
        if any(abs(v) > 1e-4 for v in ob.rotation_euler) or any(abs(v - 1) > 1e-4 for v in ob.scale):
            info["notes"].append("rotation/scale applied on export (rot %s, scale %s)"
                                 % (tuple(round(math.degrees(v), 1) for v in ob.rotation_euler), tuple(round(v, 3) for v in ob.scale)))
        mats = sorted({s.material.name if s.material else "(empty)" for p in [ob] + parts for s in p.material_slots})
        if any(m != "BallValley_Palette" for m in mats):
            info["problems"].append("material slots %s: Unity uses only the palette material (other slots' faces may vanish)" % mats)
        mods = [m.type for p in [ob] + parts for m in p.modifiers]
        if mods:
            info["notes"].append("modifiers applied on export: " + ", ".join(mods))
        bm = evaluated_bmesh(ob)
        uv = bm.loops.layers.uv.active
        off, cols = 0, {}
        for f in bm.faces:
            if uv is None:
                off += 1
                continue
            us = [l[uv].uv.x for l in f.loops]
            c = int(math.floor(sum(us) / len(us) * COLS))
            lo, hi = c / COLS - MARGIN, (c + 1) / COLS + MARGIN
            if c < 0 or c >= COLS or min(us) < lo or max(us) > hi:
                off += 1
            else:
                cols[SWATCH_NAMES[c]] = cols.get(SWATCH_NAMES[c], 0) + 1
        tris = sum(len(f.verts) - 2 for f in bm.faces)
        xs = [v.co.x for v in bm.verts]; ys = [v.co.y for v in bm.verts]; zs = [v.co.z for v in bm.verts]
        info.update({
            "object": ob.name, "tris": tris, "faces": len(bm.faces), "verts": len(bm.verts),
            "size": [round(max(xs) - min(xs), 3), round(max(ys) - min(ys), 3), round(max(zs) - min(zs), 3)] if xs else [0, 0, 0],
            "min": [round(min(xs), 3), round(min(ys), 3), round(min(zs), 3)] if xs else [0, 0, 0],
            "max": [round(max(xs), 3), round(max(ys), 3), round(max(zs), 3)] if xs else [0, 0, 0],
            "swatches": dict(sorted(cols.items(), key=lambda kv: -kv[1])),
            "faces_off_palette": off,
            "ngons": sum(1 for f in bm.faces if len(f.verts) > 4),
            "shape_key": round(sum(v.co.x * 3.1 + v.co.y * 1.7 + v.co.z * 0.9 for v in bm.verts), 2),
        })
        if off:
            info["problems"].append("%d of %d faces not on one palette swatch (new faces need palette UVs: Claude can do them)" % (off, len(bm.faces)))
        if short.startswith("Hole_"):
            op = opening(bm)
            info["opening"] = op
            if op is None:
                info["problems"].append("no pit opening found (a round pit wall from lawn level down below -1 m): the game falls back to BALLS' holes")
        if short == "Rim_Straight_4m":
            info["rim_length_x"] = info["size"][0]
            info["rim_bulk_side_y"] = 1 if (max(ys) + min(ys)) / 2 >= 0 else -1
        if short == "Giant_Jack_O_Lantern":
            info["radius_xy"] = round(max(info["size"][0], info["size"][1]) / 2, 3)
            info["glow_face_faces"] = cols.get("glow_face", 0)
            if not cols.get("glow_face"):
                info["problems"].append("no faces on the glow_face swatch: Unity can't tell which way it faces (and it won't glow)")
        bm.free()
    report["pieces"][short] = info

if BEFORE and os.path.exists(BEFORE):
    old = json.load(open(BEFORE, encoding="utf-8"))["pieces"]
    changes = []
    for k, v in report["pieces"].items():
        o = old.get(k)
        if o is None:
            changes.append("NEW piece " + k)
            continue
        diff = []
        for key in ("tris", "size", "faces_off_palette"):
            if o.get(key) != v.get(key):
                diff.append("%s %s -> %s" % (key, o.get(key), v.get(key)))
        if o.get("shape_key") != v.get("shape_key") and not diff:
            diff.append("shape moved (same size and tris)")
        for key in ("opening", "rim_length_x", "rim_bulk_side_y", "radius_xy", "glow_face_faces"):
            if key in v and o.get(key) != v.get(key):
                diff.append("%s %s -> %s" % (key, o.get(key), v.get(key)))
        if diff:
            changes.append(k + ": " + "; ".join(diff))
    for k in old:
        if k not in report["pieces"]:
            changes.append("GONE piece " + k)
    report["changes_since_before"] = changes

for k, v in report["pieces"].items():
    line = "%-22s %s tris %5s  size %-22s" % (k, "U" if v["used_in_unity"] else " ", v.get("tris", "-"), v.get("size", "-"))
    if "opening" in v and v["opening"]:
        line += "  pit r %.2f at %s" % (v["opening"]["radius"], v["opening"]["centre"])
    if "radius_xy" in v:
        line += "  r %.2f, face %d faces" % (v["radius_xy"], v["glow_face_faces"])
    print("PIECE", line)
    for p in v["problems"]:
        print("  PROBLEM", p)
    for n in v["notes"]:
        print("  NOTE", n)
for c in report.get("changes_since_before", []):
    print("CHANGED", c)

if OUT:
    os.makedirs(os.path.dirname(os.path.abspath(OUT)) or ".", exist_ok=True)
    json.dump(report, open(OUT, "w", encoding="utf-8"), indent=1)
    print("saved", OUT)

if RENDER:
    os.makedirs(RENDER, exist_ok=True)
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    try:
        sc.view_settings.view_transform = "Standard"
    except TypeError:
        pass
    sc.render.resolution_x, sc.render.resolution_y = 1600, 900
    cam = sc.camera or next((o for o in bpy.data.objects if o.type == "CAMERA"), None)
    if cam is None:
        cam = bpy.data.objects.new("Check_Cam", bpy.data.cameras.new("Check_Cam"))
        sc.collection.objects.link(cam)
    sc.camera = cam
    colls = {c.name: c for c in bpy.data.collections}

    def show(*names):
        for n, c in colls.items():
            if n in ("BallValley_Kit", "Sample_Arena", "Kit_Sheet", "Render_Stuff"):
                c.hide_render = n not in names

    def shot(name, loc, target, lens):
        cam.data.lens = lens
        cam.data.clip_end = 1000
        cam.location = loc
        cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = os.path.join(RENDER, name)
        bpy.ops.render.render(write_still=True)
        print("render", sc.render.filepath)

    # the arena from the concept's angle (the same camera as kit_renders/arena_concept_angle.png)
    show("Sample_Arena", "Render_Stuff")
    for c in kit.children:
        c.hide_render = True
    shot("kit_check_arena_concept_angle.png", (-19, -42, 21), (4.5, 8.0, -3.0), 19)
    # the kit sheet: the pieces where they stand in the kit (rows from y -70 to -42), the original sheet's camera and suns
    show("BallValley_Kit", "Kit_Sheet")
    for c in kit.children:
        c.hide_render = False
    xs, ys = [], []
    for c in kit.children:
        for o in c.objects:
            xs.append(o.location.x); ys.append(o.location.y)
    sx, sy = min(xs), min(ys)
    for name, power, colour, rot in (("Check_Sun", 3.2, (1, 0.97, 0.92), (50, 0, 30)), ("Check_Fill", 1.2, (0.75, 0.8, 1.0), (60, 0, 210))):
        ld = bpy.data.lights.new(name, "SUN")
        ld.energy, ld.color = power, colour
        lo = bpy.data.objects.new(name, ld)
        lo.rotation_euler = tuple(math.radians(a) for a in rot)
        sc.collection.objects.link(lo)
    if sc.world and sc.world.use_nodes:
        bg = next((n for n in sc.world.node_tree.nodes if n.type == "BACKGROUND"), None)
        if bg:
            bg.inputs[0].default_value = (0.16, 0.16, 0.18, 1.0)
    shot("kit_check_sheet.png", (sx + 26, sy - 36, 30), (sx + 26, sy + 14, 0.5), 32)
