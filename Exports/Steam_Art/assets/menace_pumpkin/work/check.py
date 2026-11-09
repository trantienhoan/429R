import bpy
for o in bpy.data.objects: print("OB", o.name, o.type, tuple(round(x,3) for x in o.location), o.parent.name if o.parent else None, tuple(round(x,3) for x in o.dimensions), o.data.energy if o.type=="LIGHT" else "")
print("COLL", [(c.name,[o.name for o in c.objects]) for c in bpy.data.collections])
print("IMG", [(i.name, i.packed_file is not None, i.filepath) for i in bpy.data.images])
