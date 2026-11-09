import bpy, bmesh, os
from mathutils import Vector
from mathutils.bvhtree import BVHTree
OUT=r"C:/Github_Repository/429R/Exports/Steam_Art/assets/menace_pumpkin/work"
src=bpy.data.objects["pumpkin_0"]
bm=bmesh.new(); bm.from_mesh(src.data)
hull=bm.copy()
r=bmesh.ops.convex_hull(hull, input=hull.verts[:])
# remove interior/unused
bmesh.ops.delete(hull, geom=list(set(r["geom_interior"])|set(r["geom_unused"])), context='VERTS')
tree=BVHTree.FromBMesh(hull)
depth=[]
for v in bm.verts:
    loc,n,i,d=tree.find_nearest(v.co)
    depth.append(d)
import collections
hist=collections.Counter(round(d*100) for d in depth)
print("DEPTH cm hist", sorted(hist.items()))
# face classes
sharp=bm.edges.layers.bool.get("sharp_edge")
print("sharp count", sum(1 for e in bm.edges if sharp and e[sharp]))
for f in bm.faces:
    pass
# front verts depth > 1.5cm with y<0
deep=[v for v in bm.verts if depth[v.index]>0.02]
print("deep verts",len(deep), "front", len([v for v in deep if v.co.y<-0.05]))
for v in deep:
    if v.co.y>=-0.05: print("  backdeep",[round(c,3) for c in v.co], round(depth[v.index],3))
