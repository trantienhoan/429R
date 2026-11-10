import bpy, os, json, numpy as np
from mathutils import Vector, Matrix
HERE = os.path.join(os.path.dirname(bpy.data.filepath), "v6")
sc = bpy.context.scene
prefs = bpy.context.preferences.addons['cycles'].preferences
prefs.compute_device_type = 'OPTIX'; prefs.get_devices()
for d in prefs.devices: d.use = (d.type == 'OPTIX')
sc.cycles.device = 'GPU'
def meas(tag):
    sc.cycles.samples = 32; sc.render.resolution_percentage = 25
    p = os.path.join(HERE, "prev", "bis_" + tag + ".png")
    sc.render.filepath = p
    bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(p, check_existing=False)
    a = np.array(img.pixels[:]).reshape(-1, 4)[:, :3].mean(0) * 255
    bpy.data.images.remove(img)
    print("BIS %-28s mean %s  fmt %s %s %s  look %s exp %.2f denoise %s" % (tag, a.round(1), sc.render.image_settings.file_format, sc.render.image_settings.color_mode,
          sc.render.image_settings.color_depth, sc.view_settings.look, sc.view_settings.exposure, sc.cycles.use_denoising))
meas("0_plain")
for o in bpy.data.objects:
    if o.name.startswith(("ST_Hero", "Menace_Pumpkin_Body", "ST_Spider_", "pumpkin_boss")): o.hide_render = True
meas("1_hidden")
ST = json.load(open(os.path.join(HERE, "v6_state.json")))
ORIG = {n: Matrix(m) for n, m in ST["moon_orig"].items()}
SKY_MOON_SET = Vector(ST["sky_moon_set"])
for mod in ("lib.py", "posekit.py", "cam_v6.py", "light_v6.py", "look_v6.py", "fx_v6.py"):
    exec(open(os.path.join(HERE, mod), encoding="utf-8").read(), globals())
meas("2_modules")
sc.cycles.use_denoising = True
meas("3_denoise_on")
sc.render.image_settings.file_format = "PNG"; sc.render.image_settings.color_mode = "RGB"; sc.render.film_transparent = False
meas("4_png_rgb")
FILE_MOON = {n: bpy.data.objects[n].matrix_world.copy() for n in MOON_GROUP if n in bpy.data.objects}
for n, M in FILE_MOON.items():
    bpy.data.objects[n].matrix_world = M
meas("5_moon_reset")
SKY_NT = bpy.data.materials["BG2_Sky"].node_tree
FILE_SKY = [(nd.name, i, tuple(s.default_value)) for nd in SKY_NT.nodes if nd.bl_idname == "ShaderNodeVectorMath"
            for i, s in enumerate(nd.inputs) if not s.is_linked and hasattr(s, "default_value")
            and hasattr(s.default_value, "__len__") and len(s.default_value) == 3]
for nn, i, v in FILE_SKY:
    SKY_NT.nodes[nn].inputs[i].default_value = v
meas("6_sky_reset")
BP = [n for n in ("ST_Pumpkin_Boss", "V6_Boss_Under", "V6_Boss_Rim", "ST_Boss_Face_Light") if n in bpy.data.objects]
for n in BP:
    bpy.data.objects[n].matrix_world = bpy.data.objects[n].matrix_world.copy()
meas("7_boss_reset")
p = R("CAM_V6_Main_Capsule_1232x706", pct=25, spp=32, out="bis_8_R", show_logo=False, hide=("ST_Hero",), exposure=0.0, transparent=False)
img = bpy.data.images.load(p, check_existing=False)
print("BIS 8_R()                        mean", (np.array(img.pixels[:]).reshape(-1, 4)[:, :3].mean(0) * 255).round(1))
