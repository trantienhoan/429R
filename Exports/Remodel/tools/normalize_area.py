"""Bakes the FBX round-trip rotation/scale into static meshes so they model at scale 1, no rotation, without moving
anything in the world. Each baked object keeps its old local matrix in obj["export_matrix"] (16 floats, row major)
for export_area.py. Skinned/rigged/animated meshes and armatures are left as they are (only held in place)."""
import bpy
from mathutils import Matrix
def depth(o):
    d = 0
    while o.parent: o = o.parent; d += 1
    return d
objs = sorted(bpy.data.objects, key=depth)
old_world = {o.name: o.matrix_world.copy() for o in objs}
baked = kept = 0
for o in objs:
    W = old_world[o.name]
    # rigged (an Armature modifier, an armature parent) or animated (its own keys): left as they are
    rigged = o.type == 'MESH' and (any(m.type == 'ARMATURE' for m in o.modifiers) or (o.parent and o.parent.type == 'ARMATURE')
                                   or bool(o.animation_data and o.animation_data.action))
    if o.type == 'MESH' and not rigged and "export_matrix" not in o:
        if o.data.users > 1: o.data = o.data.copy()
        o["export_matrix"] = [v for row in o.matrix_basis for v in row]
        loc, rot, sca = W.decompose()
        baked_m = Matrix.LocRotScale(None, rot, sca)
        o["baked_matrix"] = [v for row in baked_m for v in row]   # what went into the vertices (export undoes exactly this)
        o.data.transform(baked_m)
        o.matrix_world = Matrix.Translation(loc)
        baked += 1
    else:
        o.matrix_world = W
        kept += rigged
bpy.context.view_layer.update()
bad = [o.name for o in objs if (o.matrix_world.translation - old_world[o.name].translation).length > 1e-3]
print("baked", baked, "rigged kept", kept, "moved by mistake:", bad[:5])
bpy.ops.wm.save_mainfile()
