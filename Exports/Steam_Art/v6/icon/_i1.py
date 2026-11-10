import os, math
exec(open(os.path.join(HERE, "lib.py"), encoding="utf-8").read(), globals())
exec(open(os.path.join(HERE, "posekit.py"), encoding="utf-8").read(), globals())
exec(open(os.path.join(HERE, "cam_v6.py"), encoding="utf-8").read(), globals())
exec(open(os.path.join(HERE, "light_v6.py"), encoding="utf-8").read(), globals())
prefs = bpy.context.preferences.addons['cycles'].preferences
prefs.compute_device_type = 'OPTIX'; prefs.get_devices()
for d in prefs.devices: d.use = (d.type == 'OPTIX')
SC.cycles.device = 'GPU'
print("logo mats", [m.name for m in bpy.data.objects["LOGO_Library_Logo_1280x720"].data.materials])
for m in bpy.data.objects["LOGO_Library_Logo_1280x720"].data.materials:
    b = next((n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if b:
        print("  ", m.name, "base", tuple(round(x, 3) for x in b.inputs["Base Color"].default_value) if not b.inputs["Base Color"].is_linked else "linked",
              "metal", b.inputs["Metallic"].default_value if not b.inputs["Metallic"].is_linked else "linked",
              "rough", b.inputs["Roughness"].default_value if not b.inputs["Roughness"].is_linked else "linked")
HD = head_w("Head"); KN = head_w("Spine1")
mc = bpy.data.objects["CAM_V6_Main_Capsule_1232x706"]
print("head", tuple(round(v, 3) for v in HD), "spine1", tuple(round(v, 3) for v in KN), "main cam", tuple(round(v, 3) for v in mc.location))
sw = bpy.data.objects["Pie_Knight_Sword"]
print("sword tip", tuple(round(v, 3) for v in sw.matrix_world @ Vector((0, 0, -0.32))), "hilt", tuple(round(v, 3) for v in sw.matrix_world @ Vector((0, 0, 0.40))))
