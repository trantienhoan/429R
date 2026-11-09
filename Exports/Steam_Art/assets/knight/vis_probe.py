"""blender -b knight_dev.blend --python vis_probe.py -- "V:KEY=val;KEY=val" ... : fraction of right-hand verts visible (BVH vs helmet + hero pumpkin)."""
import bpy, sys, os, importlib.util
from mathutils import Vector
from mathutils.bvhtree import BVHTree
args = sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
HERE=os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location("knight_pass", os.path.join(HERE,"..","..","knight_pass.py"))
kp = importlib.util.module_from_spec(spec); spec.loader.exec_module(kp)
HAND={"mixamorig:RightHand","mixamorig:RightHandIndex1","mixamorig:RightHandIndex2","mixamorig:RightHandIndex3","Thumb_01.R","Thumb_02.R","Thumb_03.R"}
def bvh_of(o, dg):
    ev=o.evaluated_get(dg); me=ev.to_mesh()
    vs=[o.matrix_world@v.co for v in me.vertices]; ps=[tuple(p.vertices) for p in me.polygons]
    ev.to_mesh_clear(); return BVHTree.FromPolygons(vs, ps)
def run_variant(spec_s):
    for kv in spec_s[2:].split(";"):
        if "=" in kv:
            k,v=kv.split("=",1); setattr(kp,k,eval(v))
    arm=bpy.data.objects["Pie_Knight_Armature"]
    kp.build_action(arm)
    dg=bpy.context.evaluated_depsgraph_get(); dg.update()
    o=bpy.data.objects["Pie_Knight"]; ev=o.evaluated_get(dg); me=ev.to_mesh()
    gidx={g.index for g in o.vertex_groups if g.name in HAND}
    vs=[o.matrix_world@me.vertices[v.index].co for v in o.data.vertices if any(g.group in gidx and g.weight>0.5 for g in v.groups)]
    ev.to_mesh_clear()
    occ={"helmet":[bvh_of(bpy.data.objects["Pie_Knight_Helmet"],dg)]}
    occ["pumpkin"]=[bvh_of(ob,dg) for ob in bpy.data.objects if ob.type=="MESH" and not ob.hide_render and ob.name.startswith(("Hero_Pumpkin","PV2_Hero","pumpkin_0")) ]
    res=[]
    for cn in ("CAM_Main_Capsule_1232x706","CAM_Vertical_Capsule_748x896"):
        cam=bpy.data.objects[cn]; c=cam.matrix_world.translation
        cnt={"vis":0,"helmet":0,"pumpkin":0}
        for p in vs:
            d=(p-c); L=d.length; d.normalize(); who="vis"
            for k,bs in occ.items():
                if any(b.ray_cast(c,d,L-0.003)[0] is not None for b in bs): who=k; break
            cnt[who]+=1
        res.append(f"{cn[4:8]} {cnt}")
    print("VIS", spec_s, "|", " | ".join(res), "| n pumpkin meshes", len(occ["pumpkin"]))
for v in [a for a in args if a.startswith("V:")]: run_variant(v)
