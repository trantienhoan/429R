import bpy, math, os, bmesh
from mathutils import Vector
OUT=r"C:/Github_Repository/429R/Exports/Steam_Art/assets/menace_pumpkin/work"
src=bpy.data.objects["pumpkin_0"]
me=src.data
bm=bmesh.new(); bm.from_mesh(me)
bnd=[e for e in bm.edges if e.is_boundary]
print("boundary edges",len(bnd), "nonmanifold", len([e for e in bm.edges if not e.is_manifold]))
# boundary loops
seen=set(); loops=[]
for e in bnd:
    if e.index in seen: continue
    st=[e]; comp=[]; seen.add(e.index)
    while st:
        x=st.pop(); comp.append(x)
        for v in x.verts:
            for y in v.link_edges:
                if y.is_boundary and y.index not in seen: seen.add(y.index); st.append(y)
    vs={v for x in comp for v in x.verts}
    cs=[v.co for v in vs]
    loops.append(comp)
    print("LOOP n=%d c=%s min=%s max=%s"%(len(comp),[round(sum(c[i] for c in cs)/len(cs),3) for i in range(3)],[round(min(c[i] for c in cs),3) for i in range(3)],[round(max(c[i] for c in cs),3) for i in range(3)]))
# faces in front region y<-0.1 counts; faces whose normal points inward
inward=0
for f in bm.faces:
    c=f.calc_center_median(); r=c-Vector((0,0,0.23))
    if r.dot(f.normal)<0: inward+=1
print("inward faces",inward)
sc=bpy.data.scenes.new("_look")
ob=src.copy(); ob.data=src.data; sc.collection.objects.link(ob)
ob.location=(0,0,0); ob.rotation_euler=(0,0,0); ob.scale=(1,1,1)
m=src.active_material.copy(); ob.material_slots[0].link='OBJECT'; ob.material_slots[0].material=m
nt=m.node_tree
bs=next(n for n in nt.nodes if n.type=="BSDF_PRINCIPLED")
tex=next(n for n in nt.nodes if n.type=="TEX_IMAGE" and n.name=="Image Texture")
wf=nt.nodes.new("ShaderNodeWireframe"); wf.use_pixel_size=True; wf.inputs[0].default_value=1.0
mix=nt.nodes.new("ShaderNodeMix"); mix.data_type='RGBA'
nt.links.new(wf.outputs[0],mix.inputs[0]); nt.links.new(tex.outputs[0],mix.inputs[6]); mix.inputs[7].default_value=(0,1,1,1)
nt.links.new(mix.outputs[2],bs.inputs["Base Color"])
cam=bpy.data.objects.new("cam",bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera=cam
cam.data.lens=50
for nm,loc in [("k",(-1,-2,2)),("f",(2,-1,1))]:
    L=bpy.data.objects.new(nm,bpy.data.lights.new(nm,"SUN")); sc.collection.objects.link(L)
    L.rotation_euler=(Vector(loc)).to_track_quat('Z','Y').to_euler(); L.data.energy=3 if nm=="k" else 1.5
w=bpy.data.worlds.new("w"); w.color=(0.2,0.2,0.22); sc.world=w
sc.render.engine="BLENDER_EEVEE"
sc.render.resolution_x=1000; sc.render.resolution_y=1000
sc.view_settings.view_transform="Standard"
c=Vector((0,-0.0,0.2))
for tag,d,dist in [("wire_front",(0,-1,0.1),0.95),("wire_34",(0.6,-1,0.35),0.95),("wire_in",(0,-1,0.05),0.35)]:
    dv=Vector(d).normalized()
    cam.location=c+dv*dist if tag!="wire_in" else Vector((0,-0.05,0.25))
    cam.rotation_euler=(-dv).to_track_quat('-Z','Y').to_euler()
    if tag=="wire_in": cam.data.lens=18
    sc.render.filepath=os.path.join(OUT,"%s.png"%tag)
    bpy.ops.render.render(write_still=True, scene=sc.name)
