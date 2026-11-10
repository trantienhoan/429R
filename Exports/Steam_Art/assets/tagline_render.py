"""Render the tagline (assets/tagline/Logo_Tagline.blend) with the SAME camera, lights, reflector cards, world and
bloom that made the library logo (renders/Library_Logo_1280x720.png): it takes the logo's exact place in front of
CAM_Library_Logo_1280x720 (the logo itself hidden), transparent background, then is trimmed to its pixels.
Run on the ORIGINAL Steam_Art.blend (it is never saved):
  blender -b Exports/Steam_Art/Steam_Art.blend --python Exports/Steam_Art/assets/tagline_render.py -- [scale=2.0] [samples=192]
Output: assets/tagline/Tagline.png (transparent, trimmed)."""
import bpy, os, sys
import numpy as np
from mathutils import Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
ARGS = dict(a.split("=", 1) for a in (sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []) if "=" in a)
SRC = os.path.join(HERE, "tagline", "Logo_Tagline.blend")
OUT = os.path.join(HERE, "tagline", "Tagline.png")
sc = bpy.context.scene
prefs = bpy.context.preferences.addons["cycles"].preferences
prefs.compute_device_type = "OPTIX"
prefs.get_devices()
for d in prefs.devices:
    d.use = d.type == "OPTIX"
sc.cycles.device = "GPU"

with bpy.data.libraries.load(SRC, link=False) as (src, dst):
    dst.collections = ["Logo_Tagline"]
col = dst.collections[0]
sc.collection.children.link(col)
root = bpy.data.objects["Logo_Tagline_Root"]
cam = bpy.data.objects["CAM_Library_Logo_1280x720"]
logo = bpy.data.objects["LOGO_Library_Logo_1280x720"]
print("LOGO local loc", tuple(round(v, 3) for v in logo.location), "rot", tuple(round(v, 3) for v in logo.rotation_euler),
      "scale", tuple(round(v, 4) for v in logo.scale))
s = root.scale.x
root.matrix_world = logo.matrix_world @ Matrix.Diagonal((s, s, s, 1.0))
bpy.context.view_layer.update()
tag_objs = [o for o in col.all_objects]
# the logo's own lights only light what is in their receiver collections: put the tagline in them too
for o in bpy.data.objects:
    if o.type == "LIGHT" and o.parent == cam:
        rc = o.light_linking.receiver_collection
        if rc:
            for t in tag_objs:
                if t.name not in rc.objects:
                    rc.objects.link(t)
        print("LIGHT", o.name, o.data.type, round(o.data.energy, 2), "receivers", rc.name if rc else None)
# show only: the camera's own rig (lights, reflector cards) + the tagline; the logo and everything else hidden
keep = set(o for o in bpy.data.objects if o.parent == cam and o != logo) | set(tag_objs)
for o in bpy.data.objects:
    o.hide_render = not (o in keep or o.type == "CAMERA")
for o in keep:
    if o.name.endswith(("_Glow", "_Halo")):
        o.hide_render = True
sc.camera = cam
k = float(ARGS.get("scale", "2.0"))
sc.render.resolution_x, sc.render.resolution_y = int(1280 * k), int(720 * k)
sc.render.resolution_percentage = 100
sc.cycles.samples = int(ARGS.get("samples", "192"))
sc.render.film_transparent = True
sc.render.image_settings.file_format = "PNG"
sc.render.image_settings.color_mode = "RGBA"
sc.render.image_settings.color_depth = "8"
# like render_all.py on the transparent library logo: bloom only (the grade made a pale haze on soft alpha)
tree = sc.compositing_node_group
if tree:
    sa = next((n for n in tree.nodes if n.bl_idname == "CompositorNodeSetAlpha"), None)
    bl = next((n for n in tree.nodes if n.bl_idname == "CompositorNodeGlare"), None)
    if sa and bl:
        tree.links.new(bl.outputs["Image"], sa.inputs["Image"])
raw = os.path.join(HERE, "tagline", "_tagline_raw.png")
sc.render.filepath = raw
bpy.ops.render.render(write_still=True)
img = bpy.data.images.load(raw, check_existing=False)
w, h = img.size
px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
ys, xs = np.where(px[:, :, 3] > 0.02)
pad = int(0.01 * w)
x0, x1 = max(0, xs.min() - pad), min(w, xs.max() + 1 + pad)
y0, y1 = max(0, ys.min() - pad), min(h, ys.max() + 1 + pad)
crop = np.ascontiguousarray(px[y0:y1, x0:x1])
out = bpy.data.images.new("tagline_trim", x1 - x0, y1 - y0, alpha=True)
out.colorspace_settings.name = img.colorspace_settings.name
out.alpha_mode = "STRAIGHT"
out.pixels.foreach_set(crop.ravel())
out.filepath_raw = OUT
out.file_format = "PNG"
out.save()
print("TAGLINE PNG", OUT, "%dx%d" % (x1 - x0, y1 - y0))
