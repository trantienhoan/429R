"""Render the waving hand from the Main camera direction for several extra hand twists."""
import bpy, sys, os, importlib.util
from mathutils import Vector
args = sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
HERE=os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location("knight_pass", os.path.join(HERE,"..","..","knight_pass.py"))
kp = importlib.util.module_from_spec(spec); spec.loader.exec_module(kp)
sc=bpy.context.scene; sc.cycles.samples=16
arm=bpy.data.objects["Pie_Knight_Armature"]
main=bpy.data.objects["CAM_Main_Capsule_1232x706"]
cd=bpy.data.cameras.new("KPT_Hand"); cd.lens=50; cd.clip_start=0.01
cam=bpy.data.objects.new("KPT_Hand",cd); sc.collection.objects.link(cam); sc.camera=cam
sc.render.resolution_x=sc.render.resolution_y=300; sc.render.resolution_percentage=100
for kv in [a for a in args if "=" in a and a.split("=")[0].isupper()]:
    k,v=kv.split("=",1); setattr(kp,k,eval(v))
bpy.data.objects["Pie_Knight_Helmet"].hide_render = ("hidehelm" in args)
for tw in [float(x) for x in args[0].split(",")]:
    kp.HAND_TWIST=tw
    kp.build_action(arm)
    h=arm.matrix_world@arm.pose.bones["mixamorig:RightHand"].tail
    d=(main.matrix_world.translation-h).normalized()
    cam.location=h+d*0.28; cam.rotation_euler=(h-cam.location).to_track_quat('-Z','Y').to_euler()
    sc.render.filepath=os.path.join(HERE,"tests",f"hand_tw{int(tw)}.png")
    bpy.ops.render.render(write_still=True)
