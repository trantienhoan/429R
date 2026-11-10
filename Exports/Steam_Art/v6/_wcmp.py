import bpy, hashlib, sys
out = []
for o in bpy.data.objects:
    if o.type == 'MESH' and o.name.startswith('Pie_Knight') or (o.type=='MESH' and 'knight' in o.name.lower()):
        me = o.data
        h = hashlib.md5()
        for v in me.vertices:
            for g in v.groups:
                h.update(("%d:%d:%.4f" % (v.index, g.group, g.weight)).encode())
        arm = o.find_armature()
        print("MESH", o.name, "verts", len(me.vertices), "groups", len(o.vertex_groups), "wHash", h.hexdigest()[:10], "arm", arm.name if arm else None)
