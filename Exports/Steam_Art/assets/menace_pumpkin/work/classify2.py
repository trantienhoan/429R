import bpy, bmesh, os, math
from mathutils import Vector
from mathutils.bvhtree import BVHTree
OUT=r"C:/Github_Repository/429R/Exports/Steam_Art/assets/menace_pumpkin/work"
src=bpy.data.objects["pumpkin_0"]
bm=bmesh.new(); bm.from_mesh(src.data)
hull=bm.copy()
r=bmesh.ops.convex_hull(hull, input=hull.verts[:])
bmesh.ops.delete(hull, geom=list(set(r["geom_interior"])|set(r["geom_unused"])), context='VERTS')
tree=BVHTree.FromBMesh(hull)
dep={v.index:tree.find_nearest(v.co)[3] for v in bm.verts}
cav=set()
TH=float(__import__("os").environ.get("TH","50"))
bm.faces.ensure_lookup_table()
outer=set([max(bm.faces,key=lambda f:f.calc_center_median().y).index]); st=list(outer)
while st:
    f=bm.faces[st.pop()]
    for e in f.edges:
        if len(e.link_faces)==2 and math.degrees(e.calc_face_angle())<TH:
            for g in e.link_faces:
                if g.index not in outer: outer.add(g.index); st.append(g.index)
print("outer",len(outer))
for f in bm.faces:
    c=f.calc_center_median()
    if f.index not in outer and c.y<-0.0 and c.z<0.34: cav.add(f.index)
print("cavity faces",len(cav))
# connected components of cavity
bm.faces.ensure_lookup_table()
seen=set(); comps=[]
for fi in cav:
    if fi in seen: continue
    st=[fi]; seen.add(fi); comp=[]
    while st:
        x=st.pop(); comp.append(x)
        for e in bm.faces[x].edges:
            for g in e.link_faces:
                if g.index in cav and g.index not in seen: seen.add(g.index); st.append(g.index)
    cs=[bm.faces[i].calc_center_median() for i in comp]
    print("COMP",len(comp),[round(sum(c[i] for c in cs)/len(cs),3) for i in range(3)])
    comps.append(comp)
# colour render
me=src.data.copy()
ob=bpy.data.objects.new("_cls",me)
sc=bpy.data.scenes.new("_look"); sc.collection.objects.link(ob)
mA=bpy.data.materials.new("A"); mA.diffuse_color=(1,0.5,0.1,1)
mB=bpy.data.materials.new("B"); mB.diffuse_color=(0.1,0.3,1,1)
me.materials.clear(); me.materials.append(mA); me.materials.append(mB)
for p in me.polygons: p.material_index=1 if p.index in cav else 0
for m in (mA,mB):
    m.use_nodes=True; b=next(n for n in m.node_tree.nodes if n.type=="BSDF_PRINCIPLED"); b.inputs["Base Color"].default_value=m.diffuse_color
cam=bpy.data.objects.new("cam",bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera=cam
L=bpy.data.objects.new("L",bpy.data.lights.new("L","SUN")); sc.collection.objects.link(L); L.rotation_euler=Vector((-1,-2,2)).to_track_quat('Z','Y').to_euler(); L.data.energy=3
w=bpy.data.worlds.new("w"); w.color=(0.3,0.3,0.3); sc.world=w
sc.render.engine="BLENDER_EEVEE"; sc.render.resolution_x=700; sc.render.resolution_y=700
for tag,d in [("cls_front",(0,-1,0.1)),("cls_34",(0.8,-1,0.4))]:
    dv=Vector(d).normalized(); cam.location=Vector((0,0,0.2))+dv*0.95
    cam.rotation_euler=(-dv).to_track_quat('-Z','Y').to_euler()
    sc.render.filepath=os.path.join(OUT,tag+".png"); bpy.ops.render.render(write_still=True, scene=sc.name)
