"""Renders Icon_Scene.blend's camera on a transparent background (bloom kept, no grade; like render_all.py does for the
library logo) -> icon_render_1024.png next to this script.   -- preview  = 512 px, 48 samples."""
import bpy, os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
sc = bpy.context.scene
sc.camera = bpy.data.objects["IC_Camera"]
prev = "preview" in args
GRADE_DEFAULT = ["green_gamma=1.0", "lift=1.05", "gold_dark_hue=24", "gold_light_hue=37"]   # tuned to the badge (after the judges)
sc.render.resolution_x = sc.render.resolution_y = 1024
sc.render.resolution_percentage = 50 if prev else 100
sc.cycles.samples = 48 if prev else 256
if "full" in args:
    sc.render.resolution_percentage = 100
sc.render.film_transparent = True
# the Steam scene's grade (bloom, colour balance, saturation, the S-curve that gives AgX its contrast) is kept; only
# its vignette is skipped (darkening toward a colour, it laid a grey veil over the transparent icon)
sc.render.use_compositing = True
tree = sc.compositing_node_group
sa = next((n for n in tree.nodes if n.bl_idname == "CompositorNodeSetAlpha"), None) if tree else None
cu = next((n for n in tree.nodes if n.bl_idname == "CompositorNodeCurveRGB"), None) if tree else None
if sa and cu:
    tree.links.new(cu.outputs["Image"], sa.inputs["Image"])
sc.render.image_settings.file_format = "PNG"
sc.render.image_settings.color_mode = "RGBA"
sc.render.image_settings.color_depth = "8"          # 8-bit: what Steam / Pillow expect (16-bit RGBA confuses viewers)
sc.render.filepath = os.path.join(HERE, "icon_render_preview.png" if prev else "icon_render_1024.png")
raw = sc.render.filepath.replace(".png", "_raw.png")
sc.render.filepath = raw
bpy.ops.render.render(write_still=True)
# masks for the grade, each one object group alone with every other object a holdout (so it's cut where hidden):
# the helmet (the badge's helmet is an olive green, its digits emerald: the same brightness, only a mask tells them
# apart) and the field (the disc: the badge's dark fill is a warm yellow-green, its digits a cool emerald)
MASKS = {"helmet": ["Pie_Knight_Helmet"], "field": ["IC_Disc", "IC_Disc_Front"]}
mask_files = {}
was = {o.name: o.is_holdout for o in sc.objects}
sc.cycles.samples = 16
sc.render.use_compositing = False
for key, names in MASKS.items():
    if not all(n in bpy.data.objects for n in names):
        continue
    for o in sc.objects:
        o.is_holdout = o.name not in names
    sc.render.filepath = raw.replace("_raw.png", "_%s.png" % key)
    bpy.ops.render.render(write_still=True)
    mask_files[key] = sc.render.filepath
for o in sc.objects:
    o.is_holdout = was.get(o.name, False)
# then the colour grade onto the badge's palette (grade_icon.py, system Python with numpy + Pillow)
import subprocess, shutil
graded = raw.replace("_raw.png", ".png")
py = shutil.which("python") or shutil.which("py")
grade_args = [a for a in args if "=" in a] or GRADE_DEFAULT
grade_args = grade_args + ["%s=%s" % (k, f) for k, f in mask_files.items() if os.path.exists(f)]
if py:
    subprocess.run([py, os.path.join(HERE, "grade_icon.py"), raw, graded] + grade_args, check=False)
else:
    shutil.copy(raw, graded)
print("RENDERED", graded)
