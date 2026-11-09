import bpy
ob = bpy.data.objects.get("pumpkin_0")
print("OBJ", ob, ob.type, ob.location[:], ob.rotation_euler[:], ob.scale[:], ob.parent, [c.name for c in ob.users_collection])
me = ob.data
print("MESH", me.name, len(me.vertices), len(me.polygons), "users", me.users, [u.name for u in me.uv_layers], [a.name for a in me.attributes])
print("dims", ob.dimensions[:])
import mathutils
bb=[mathutils.Vector(c) for c in ob.bound_box]
print("bbox local min", [min(v[i] for v in bb) for i in range(3)], "max", [max(v[i] for v in bb) for i in range(3)])
print("mods", [(m.name,m.type) for m in ob.modifiers])
for i,ms in enumerate(ob.material_slots):
    m=ms.material
    print("SLOT",i,m.name if m else None)
    if m and m.use_nodes:
        for n in m.node_tree.nodes:
            extra = n.image.name+" "+n.image.filepath+" "+str(tuple(n.image.size)) if n.type=="TEX_IMAGE" and n.image else ""
            print("   node",n.type,n.name,extra)
        for l in m.node_tree.links:
            print("   link",l.from_node.name,l.from_socket.name,"->",l.to_node.name,l.to_socket.name)
import collections
cnt=collections.Counter(p.material_index for p in me.polygons)
print("mat idx counts",cnt)
# other objects using same mesh / names with pumpkin
for o in bpy.data.objects:
    if "umpkin" in o.name or o.data==me:
        print("  other", o.name, o.type, o.data.name if o.data else None, o.location[:])
# loose parts
import bmesh
bm=bmesh.new(); bm.from_mesh(me)
bm.verts.ensure_lookup_table()
seen=set(); parts=[]
for v in bm.verts:
    if v.index in seen: continue
    st=[v]; seen.add(v.index); comp=[]
    while st:
        x=st.pop(); comp.append(x)
        for e in x.link_edges:
            y=e.other_vert(x)
            if y.index not in seen: seen.add(y.index); st.append(y)
    parts.append(comp)
for comp in parts:
    xs=[v.co for v in comp]
    print("PART n=%d min=%s max=%s"%(len(comp),[round(min(c[i] for c in xs),3) for i in range(3)],[round(max(c[i] for c in xs),3) for i in range(3)]))
