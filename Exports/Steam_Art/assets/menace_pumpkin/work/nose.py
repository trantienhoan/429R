import bpy, bmesh
from mathutils.bvhtree import BVHTree
src=bpy.data.objects["pumpkin_0"]
bm=bmesh.new(); bm.from_mesh(src.data)
hull=bm.copy(); r=bmesh.ops.convex_hull(hull, input=hull.verts[:])
bmesh.ops.delete(hull, geom=list(set(r["geom_interior"])|set(r["geom_unused"])), context='VERTS')
tree=BVHTree.FromBMesh(hull)
for v in bm.verts:
    if abs(v.co.x)<0.07 and 0.13<v.co.z<0.26 and v.co.y<-0.1:
        print("NV %6.3f %6.3f %6.3f d=%.3f"%(v.co.x,v.co.y,v.co.z,tree.find_nearest(v.co)[3]))
