import bpy, math, os, sys
import numpy as np
from mathutils import Vector
OUT=r"C:/Github_Repository/429R/Exports/Steam_Art/assets/menace_pumpkin/work"
src=bpy.data.objects["pumpkin_0"]
sc=bpy.data.scenes.new("_look")
bpy.context.window.scene=sc if bpy.context.window else None
ob=src.copy(); sc.collection.objects.link(ob)
ob.location=(0,0,0); ob.rotation_euler=(0,0,0); ob.scale=(1,1,1)
cam=bpy.data.objects.new("cam",bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera=cam
cam.data.lens=50
for nm,loc in [("k",(-1,-2,2)),("f",(2,-1,1)),("b",(0,2,2))]:
    L=bpy.data.objects.new(nm,bpy.data.lights.new(nm,"SUN")); sc.collection.objects.link(L)
    L.rotation_euler=(Vector(loc)).to_track_quat('Z','Y').to_euler(); L.data.energy=3 if nm=="k" else 1.5
w=bpy.data.worlds.new("w"); w.color=(0.2,0.2,0.22); sc.world=w
sc.render.engine="BLENDER_EEVEE" if "BLENDER_EEVEE" in [i.identifier for i in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items] else "BLENDER_EEVEE_NEXT"
sc.render.resolution_x=600; sc.render.resolution_y=600
sc.view_settings.view_transform="Standard"
c=Vector((0,0,0.23))
for tag,d in [("mY",(0,-1,0.15)),("pY",(0,1,0.15)),("pX",(1,0,0.15)),("mX",(-1,0,0.15))]:
    dv=Vector(d).normalized()
    cam.location=c+dv*1.6
    cam.rotation_euler=(-dv).to_track_quat('-Z','Y').to_euler()
    sc.render.filepath=os.path.join(OUT,"orig_%s.png"%tag)
    bpy.ops.render.render(write_still=True, scene=sc.name)
# texture crop
img=bpy.data.images["Bedroom_Furniture_Set1_Bedroom_Furniture_Set1_AlbedoTranspare"]
W,H=img.size
px=np.array(img.pixels[:],dtype=np.float32).reshape(H,W,4)
x0,x1,y0,y1=int(0.10*W),int(0.93*W),int(0.41*H),int(0.87*H)
crop=px[y0:y1,x0:x1].copy()
# overlay uv edges
me=src.data; uv=me.uv_layers["UVMap"].data
for p in me.polygons:
    pts=[uv[i].uv.copy() for i in p.loop_indices]
    for a,b in zip(pts,pts[1:]+pts[:1]):
        for t in np.linspace(0,1,40):
            u=a.x+(b.x-a.x)*t; v=a.y+(b.y-a.y)*t
            xx=int(u*W)-x0; yy=int(v*H)-y0
            if 0<=xx<crop.shape[1] and 0<=yy<crop.shape[0]: crop[yy,xx]=(0,1,1,1)
im=bpy.data.images.new("crop",crop.shape[1],crop.shape[0],alpha=True)
im.pixels.foreach_set(crop.ravel()); im.filepath_raw=os.path.join(OUT,"tex_crop_uv.png"); im.file_format="PNG"; im.save()
im2=bpy.data.images.new("crop2",crop.shape[1],crop.shape[0],alpha=True)
im2.pixels.foreach_set(px[y0:y1,x0:x1].ravel()); im2.filepath_raw=os.path.join(OUT,"tex_crop.png"); im2.file_format="PNG"; im2.save()
print("CROP px",x0,x1,y0,y1)
