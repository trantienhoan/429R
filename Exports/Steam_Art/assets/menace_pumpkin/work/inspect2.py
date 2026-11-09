import bpy, bmesh, math
from mathutils import Vector
ob = bpy.data.objects["pumpkin_0"]; me=ob.data
uv=me.uv_layers["UVMap"].data
us=[l.uv[0] for l in uv]; vs=[l.uv[1] for l in uv]
print("UVRANGE", min(us),max(us),min(vs),max(vs))
# per face: centre, normal, uv centre
import collections
# islands by uv
bm=bmesh.new(); bm.from_mesh(me)
ul=bm.loops.layers.uv["UVMap"]
# distribution of faces by facing direction (local) - front is where?
img=bpy.data.images["Bedroom_Furniture_Set1_Bedroom_Furniture_Set1_AlbedoTranspare"]
W,H=img.size
import numpy as np
px=np.array(img.pixels[:],dtype=np.float32).reshape(H,W,4)
# sample texture at each face's uv centre; find bright yellow faces
rows=[]
for f in bm.faces:
    uc=sum((l[ul].uv for l in f.loops),Vector((0,0)))/len(f.loops)
    x=int(uc.x%1*W); y=int(uc.y%1*H)
    c=px[min(y,H-1),min(x,W-1)]
    rows.append((f.index,f.calc_center_median(),f.normal.copy(),uc,c))
yel=[r for r in rows if r[4][0]>0.6 and r[4][1]>0.45]
print("yellowish faces", len(yel))
for r in yel[:40]:
    print("  Y", r[0], [round(a,3) for a in r[1]], [round(a,2) for a in r[2]], [round(a,3) for a in r[3]], [round(a,2) for a in r[4][:3]])
# uv bbox per colour class
print("count faces", len(rows))
# overall uv bbox of all faces
