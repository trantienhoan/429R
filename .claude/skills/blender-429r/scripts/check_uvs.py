"""
check_uvs.py - read-only report on how a texture is shared, to run BEFORE painting it.

Inside Blender (MCP execute_blender_code, or headless):

    IMAGE = "Cockroach_Enemy_Atlas"          # the image's name in bpy.data.images
    exec(open(r"K:/Repository/429R/.claude/skills/blender-429r/scripts/check_uvs.py", encoding="utf-8").read())

Prints: the image, every mesh that uses it, and for each mesh its faces, selected faces, texels covered, texels
stacked on its own other faces (mirrored halves, copies), faces outside the 0-1 square, and texel density; then
how many texels each pair of meshes shares. Changes nothing.
"""


import numpy as np

exec(open(r"K:/Repository/429R/.claude/skills/blender-429r/scripts/uvpaint.py", encoding="utf-8").read())

image = bpy.data.images[IMAGE]
W, H = image.size
objs = objects_using(image)
print(f"Image '{image.name}' {W}x{H}, file {bpy.path.abspath(image.filepath)}")
print(f"Used by {len(objs)} mesh(es): {', '.join(o.name for o in objs)}")
if not objs:
    print("No mesh shows this image through its material's Image Texture node.")

layouts = []
for ob in objs:
    lay = bake_layout([ob], W, H)
    layouts.append(lay)
    bm, owned = mesh_of(ob)
    try:
        uv = bm.loops.layers.uv.active
        faces = len(bm.faces)
        selected = sum(1 for f in bm.faces if f.select)
        outside = 0
        world_area = 0.0
        uv_area = 0.0
        m = ob.matrix_world
        for f in bm.faces:
            uvs = [l[uv].uv for l in f.loops] if uv else []
            if any(u.x < 0 or u.x > 1 or u.y < 0 or u.y > 1 for u in uvs):
                outside += 1
            world_area += f.calc_area() * abs(m.to_3x3().determinant()) ** (2 / 3)
            for i in range(1, len(uvs) - 1):
                a, b, c = uvs[0], uvs[i], uvs[i + 1]
                uv_area += abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) * 0.5
        density = np.sqrt(uv_area * W * H / world_area) if world_area > 0 else 0.0
    finally:
        done(bm, owned)
    print(f"- {ob.name} ({ob.mode} mode): {faces} faces, {selected} selected, {int(lay.mask.sum())} texels, "
          f"{int(lay.stacked.sum())} stacked on its own faces, {outside} faces outside 0-1, ~{density:.0f} px/m")

for i in range(len(objs)):
    for j in range(i + 1, len(objs)):
        shared = int((layouts[i].mask & layouts[j].mask).sum())
        if shared:
            share = 100.0 * shared / max(1, min(layouts[i].mask.sum(), layouts[j].mask.sum()))
            print(f"  {objs[i].name} and {objs[j].name} share {shared} texels ({share:.0f}% of the smaller)")
print("Done (nothing changed).")
