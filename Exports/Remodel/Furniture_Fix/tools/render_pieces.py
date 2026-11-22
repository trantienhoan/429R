# Per piece of Bedroom_Furniture_Fix.blend: one picture of the whole piece and one of its broken pieces pulled apart
# (exploded from the middle), three-quarter view, plus each piece's size. Never saves the .blend.
#   blender -b Bedroom_Furniture_Fix.blend --python render_pieces.py -- <out folder>
import bpy, sys, os, json
from mathutils import Vector

out = sys.argv[sys.argv.index("--") + 1:][0]
os.makedirs(out, exist_ok=True)
scene = bpy.context.scene
for eng in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE", "BLENDER_WORKBENCH"):
    try:
        scene.render.engine = eng; break
    except TypeError:
        pass
scene.render.resolution_x = 640
scene.render.resolution_y = 640
scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.2, 0.2, 0.22, 1)
scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.0
for o in scene.objects:
    if o.type == "FONT":
        o.hide_render = True
cam = bpy.data.objects.new("_claude_cam", bpy.data.cameras.new("_claude_cam"))
scene.collection.objects.link(cam)
cam.data.type = 'ORTHO'
scene.camera = cam
fill = bpy.data.objects.new("_claude_fill", bpy.data.lights.new("_claude_fill", "SUN"))
fill.data.energy = 1.5
fill.rotation_euler = (1.0, 0.0, -2.3)
scene.collection.objects.link(fill)

def bounds(objs):
    lo = Vector((1e9,) * 3); hi = Vector((-1e9,) * 3)
    for o in objs:
        for v in o.bound_box:
            w = o.matrix_world @ Vector(v); lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
    return lo, hi

def shoot(objs, path, turn=0):
    keep = set(objs)
    for o in scene.objects:
        if o.type == "MESH":
            o.hide_render = o not in keep
    bpy.context.view_layer.update()
    lo, hi = bounds(objs)
    mid = (lo + hi) / 2
    import math as _m
    from mathutils import Matrix as _M
    d = (_M.Rotation(_m.radians(turn), 3, "Z") @ Vector((0.75, -1.0, 0.65))).normalized()
    cam.location = mid + d * 30
    cam.rotation_euler = (mid - cam.location).to_track_quat('-Z', 'Y').to_euler()
    cam.data.ortho_scale = max((hi - lo).length * 1.05, 0.3)
    cam.data.clip_end = 100
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)

info = []
top = bpy.data.collections["Furniture_Fix"]
for g in top.children:
    whole = [o for c in g.children if c.name.endswith(" - whole") for o in c.all_objects if o.type == "MESH"]
    broken = [o for c in g.children if c.name.endswith(" - broken") for o in c.all_objects if o.type == "MESH"]
    if not whole:
        continue
    lo, hi = bounds(whole)
    size = hi - lo
    shoot(whole, os.path.join(out, g.name + "_whole.png"), g.get("front_turn", 0))
    if broken:
        # Pull the pieces apart from the middle (render only; put back after).
        blo, bhi = bounds(broken)
        mid = (blo + bhi) / 2
        moved = []
        for o in broken:
            if o.parent is None:
                plo, phi = bounds([o])
                c = (plo + phi) / 2
                off = (c - mid) * 0.6
                off.z += (c.z - blo.z) * 0.15
                o.location += off
                moved.append((o, off))
        shoot(broken, os.path.join(out, g.name + "_broken.png"), g.get("front_turn", 0))
        for o, off in moved:
            o.location -= off
    info.append({"name": g.name, "size": [round(size.x, 2), round(size.y, 2), round(size.z, 2)], "broken_pieces": len(broken)})
json.dump(info, open(os.path.join(out, "pieces.json"), "w"), indent=1)
print("PIECES", json.dumps(info))
