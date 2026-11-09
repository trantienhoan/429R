"""Test renders of assets/Hero_Pumpkin.blend into assets/pumpkin_tests/.
  blender -b assets/Hero_Pumpkin.blend --python assets/pumpkin_test_render.py -- tag=r1 cams=Hero,Front res=900x700 samples=96"""
import bpy, os, sys
args = dict(a.split("=", 1) for a in (sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []) if "=" in a)
tag = args.get("tag", "test")
cams = args.get("cams", "Hero").split(",")
rx, ry = [int(x) for x in args.get("res", "900x700").split("x")]
sc = bpy.context.scene
for lc in bpy.context.view_layer.layer_collection.children:
    if lc.name == "Pumpkin_Test_Rig":
        lc.exclude = False
prefs = bpy.context.preferences.addons["cycles"].preferences
try:
    prefs.compute_device_type = "OPTIX"
    prefs.get_devices()
    for d in prefs.devices:
        d.use = d.type == "OPTIX"
    sc.cycles.device = "GPU"
except Exception as e:
    print("GPU fail", e)
sc.cycles.samples = int(args.get("samples", 96))
sc.render.resolution_x, sc.render.resolution_y, sc.render.resolution_percentage = rx, ry, 100
if args.get("hide_small"):
    for o in bpy.data.collections["Small_Pumpkin_v2"].all_objects:
        o.hide_render = True
if args.get("hide_hero"):
    for o in bpy.data.collections["Hero_Pumpkin_v2"].all_objects:
        o.hide_render = True
if "light" in args:
    bpy.data.objects["Hero_Pumpkin_Inner_Light"].data.energy = float(args["light"])
for key, mname in (("glow_inner", "PV2_Pumpkin_Inner"), ("glow_cut", "PV2_Pumpkin_Cut_Flesh")):
    if key in args:
        bpy.data.materials[mname].node_tree.nodes["Glow"].outputs[0].default_value = float(args[key])
if args.get("dark"):   # only the inner lights
    for o in bpy.data.collections["Pumpkin_Test_Rig"].objects:
        if o.type == "LIGHT":
            o.hide_render = True
out = os.path.join(os.path.dirname(bpy.data.filepath), "pumpkin_tests")
os.makedirs(out, exist_ok=True)
for c in cams:
    sc.camera = bpy.data.objects["Test_Cam_" + c]
    sc.render.filepath = os.path.join(out, f"{tag}_{c}.png")
    bpy.ops.render.render(write_still=True)
    print("RENDERED", sc.render.filepath)
