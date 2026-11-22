# Lists what's in New_Weapons.blend (read only).
import bpy
from mathutils import Vector
print("FILE", bpy.data.filepath, "dirty", bpy.data.is_dirty)
for c in bpy.data.collections:
    print("COLL", c.name, "objs", [o.name for o in c.objects], "children", [k.name for k in c.children], "hide_render", c.hide_render)
for o in bpy.data.objects:
    line = f"OBJ {o.name} type {o.type} parent {o.parent.name if o.parent else '-'} loc {tuple(round(v,3) for v in o.location)} rot {tuple(round(v,3) for v in o.rotation_euler)} scale {tuple(round(v,3) for v in o.scale)} hidden {o.hide_get() if bpy.context.view_layer.objects.get(o.name) else 'n/a'} render_hidden {o.hide_render} colls {[c.name for c in o.users_collection]}"
    if o.type == "MESH":
        me = o.data
        lo = Vector([min(v.co[i] for v in me.vertices) for i in range(3)]) if me.vertices else Vector()
        hi = Vector([max(v.co[i] for v in me.vertices) for i in range(3)]) if me.vertices else Vector()
        tris = sum(len(p.vertices) - 2 for p in me.polygons)
        line += f" | mesh {me.name} faces {len(me.polygons)} tris {tris} local bounds {tuple(round(v,3) for v in lo)}..{tuple(round(v,3) for v in hi)} mats {[m.name if m else None for m in me.materials]} uv {[u.name for u in me.uv_layers]} mods {[m.type for m in o.modifiers]}"
    print(line)
for m in bpy.data.materials:
    if m.users == 0: continue
    imgs = [n.image.filepath for n in (m.node_tree.nodes if m.node_tree else []) if n.type == "TEX_IMAGE" and n.image]
    b = next((n for n in (m.node_tree.nodes if m.node_tree else []) if n.type == "BSDF_PRINCIPLED"), None)
    col = tuple(round(x, 3) for x in b.inputs["Base Color"].default_value) if b else None
    print("MAT", m.name, "users", m.users, "images", imgs, "base", col)
for i in bpy.data.images:
    print("IMG", i.name, i.filepath, i.size[:])
