"""Profile picture (2026-11-10, user: "Render a picture of the knight standing alone with blurry background. To use as
profile."). The user's posed knight (Steam_Art_v6_pose.blend, never saved, pose untouched) alone and sharp, over the
same night scene blurred like a fast portrait lens. Two passes with the same camera:
  A = the knight only (+ his sword sparkles), transparent, no depth of field;
  B = the scene WITHOUT the knight (he still casts his shadow and shows in the wet floor), strong depth of field.
The boss, spiders and the burning pumpkin are hidden. The compositor's bloom is off in both passes; compose_profile.py
lays A over B and adds the glow on the final picture.
  blender -b Exports/Steam_Art/Steam_Art_v6_pose.blend --python Exports/Steam_Art/v6/render_profile.py -- [preview]
      [lens=85] [fstop=1.2] [frame=0.66] [moon=0.78,0.22] [moonscale=1.2] [low=0.06] [yaw=0]
Output: Exports/Steam_Art/renders_v6/profile/_A_knight.png, _B_background.png (then run compose_profile.py)."""
import bpy, os, sys, math, json
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
ARGV = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ARGS = dict(a.split("=", 1) for a in ARGV if "=" in a)
PREVIEW = "preview" in ARGV
ST = json.load(open(os.path.join(HERE, "v6_state.json")))
ORIG = {n: Matrix(m) for n, m in ST["moon_orig"].items()}
SKY_MOON_SET = Vector(ST["sky_moon_set"])
for mod in ("lib.py", "posekit.py", "cam_v6.py", "light_v6.py", "look_v6.py"):
    exec(open(os.path.join(HERE, mod), encoding="utf-8").read(), globals())

prefs = bpy.context.preferences.addons["cycles"].preferences
for t in ("OPTIX", "CUDA"):
    try:
        prefs.compute_device_type = t
        prefs.get_devices()
        if any(d.type == t for d in prefs.devices):
            for d in prefs.devices:
                d.use = d.type == t
            break
    except Exception:
        pass
SC.cycles.device = "GPU"
SC.cycles.use_denoising = True

OUT = os.path.join(HERE, "..", "renders_v6", "profile" + ("_preview" if PREVIEW else ""))
os.makedirs(OUT, exist_ok=True)
RES = int(ARGS.get("res", 512 if PREVIEW else 2048))
SPP = int(ARGS.get("samples", 32 if PREVIEW else 384))
LENS = float(ARGS.get("lens", 85))
FSTOP = float(ARGS.get("fstop", 1.2))
FRAME = float(ARGS.get("frame", 0.66))          # frame height in metres at the knight
LOW = float(ARGS.get("low", 0.06))              # camera this much below the frame centre (a slightly heroic angle)
YAW = float(ARGS.get("yaw", 0.0))               # turn the camera round the knight (degrees, + = to the knight's left)
MOON_AT = tuple(float(v) for v in ARGS.get("moon", "0.80,0.15").split(","))   # moon centre in the picture (x, y from top)
MOON_SCALE = float(ARGS.get("moonscale", 1.0))

HIDE = ("ST_Hero", "Menace_Pumpkin_Body", "ST_Spider_", "pumpkin_boss", "ST_Pumpkin_Boss")
KNIGHT = ("Pie_Knight", "V6_Spark_")             # what pass A sees
for o in bpy.data.objects:
    if o.name.startswith(HIDE):
        o.hide_render = True

# ---- camera: in front of the knight, from the main camera's side (the user posed him for that view); LEVEL (upright
# lines stay upright) at CAM_Z, framed with lens shift so his body fills BODY_H of the height, centred at (CX, CY)
from bpy_extras.object_utils import world_to_camera_view
KN = head_w("Spine1")
mc = bpy.data.objects["CAM_V6_Main_Capsule_1232x706"].location
d = Vector((mc.x - KN.x, mc.y - KN.y, 0.0)).normalized()
d = Matrix.Rotation(math.radians(YAW), 3, "Z") @ d
knight_meshes = [o for o in bpy.data.objects if o.name.startswith("Pie_Knight") and o.type == "MESH" and not o.hide_render]
for o in knight_meshes:
    a_, b_ = wbbox([o.name])
    print("KNIGHT part %-24s z %.3f..%.3f" % (o.name, a_.z, b_.z))
# a part that sits under the floor (the shield in the user's file) is never seen in their renders: keep it out
UNDER = [o.name for o in knight_meshes if wbbox([o.name])[1].z < 0.02]
print("UNDER THE FLOOR (left out):", UNDER, "| camera-visible:", {o.name: o.visible_camera for o in knight_meshes})
# the shield reaches under the floor and never shows in the user's renders: it must not steer the framing
NOT_FRAMED = ("Pie_Knight_Sword", "Pie_Knight_Shield")
body = [o for o in knight_meshes if o.name not in NOT_FRAMED and o.name not in UNDER and o.visible_camera]
sword = [o for o in knight_meshes if o.name == "Pie_Knight_Sword"]
lo, hi = wbbox([o.name for o in body])
CAM_Z = float(ARGS.get("camz", 0.12))
BODY_H = float(ARGS.get("bodyh", 0.78))
CX, CY = (float(v) for v in ARGS.get("centre", "0.53,0.52").split(","))
look = Vector(((lo.x + hi.x) * 0.5, (lo.y + hi.y) * 0.5, CAM_Z))
tan_half = 18.0 / LENS
dist = (FRAME / 2) / tan_half
cam = cam_look("CAM_PROFILE", look + d * dist, look, LENS, RES, RES)
cam.data.clip_start = 0.05
SC.render.resolution_x = SC.render.resolution_y = RES
SC.render.resolution_percentage = 100


def project(objs):
    dg = bpy.context.evaluated_depsgraph_get()
    xs, ys = [], []
    for o in objs:
        ev = o.evaluated_get(dg)
        me = ev.to_mesh()
        for v in me.vertices:
            p = world_to_camera_view(SC, cam, o.matrix_world @ v.co)
            xs.append(p.x)
            ys.append(1 - p.y)
        ev.to_mesh_clear()
    return min(xs), max(xs), min(ys), max(ys)


for it in range(6):
    x0, x1, y0, y1 = project(body)
    dist *= (y1 - y0) / BODY_H
    cam.location = look + d * dist
    bpy.context.view_layer.update()
    x0, x1, y0, y1 = project(body)
    cam.data.shift_x += (x0 + x1) * 0.5 - CX
    cam.data.shift_y -= (y0 + y1) * 0.5 - CY
    bpy.context.view_layer.update()
for label, objs in (("body", body), ("sword", sword)):
    print("PICTURE %-5s x %.3f-%.3f  y %.3f-%.3f (0..1 from the top-left)" % ((label,) + project(objs)))
print("CAMERA dist %.3f m, height %.3f, shift %.3f %.3f" % (dist, CAM_Z, cam.data.shift_x, cam.data.shift_y))
cR = cam.matrix_world.col[0].xyz.normalized()
cU = cam.matrix_world.col[1].xyz.normalized()
cF = -cam.matrix_world.col[2].xyz.normalized()

# ---- the moon: at MOON_AT in the picture (lens shift included), far behind the knight
mx, my = MOON_AT
tx = ((mx - 0.5) + cam.data.shift_x) * 2 * tan_half
ty = ((0.5 - my) + cam.data.shift_y) * 2 * tan_half
moon_to(cF + cR * tx + cU * ty, scale=MOON_SCALE, origin=cam.location)

# ---- no bloom in the passes (added on the final picture)
tree = SC.compositing_node_group
glare = next((n for n in tree.nodes if n.bl_idname == "CompositorNodeGlare"), None) if tree else None
if glare:
    for i in glare.inputs:
        if i.name == "Strength" and not i.is_linked:
            i.default_value = 0.0
SC.camera = cam
SC.cycles.samples = SPP
SC.render.image_settings.file_format = "PNG"
SC.render.image_settings.color_depth = "8"
VIS_TYPES = ("MESH", "CURVE", "FONT", "VOLUME", "CURVES", "META", "SURFACE", "POINTCLOUD")
base_cam = {o.name: o.visible_camera for o in bpy.data.objects}


def render_pass(name, knight_seen, rest_seen, transparent, dof):
    for o in bpy.data.objects:
        if o.type in VIS_TYPES:
            is_k = o.name.startswith(KNIGHT) and o.name not in UNDER
            o.visible_camera = base_cam[o.name] and (knight_seen if is_k else rest_seen) and o.name not in UNDER
    SC.render.film_transparent = transparent
    SC.render.image_settings.color_mode = "RGBA" if transparent else "RGB"
    cam.data.dof.use_dof = dof
    if dof:
        cam.data.dof.focus_distance = (helmet_focus - cam.location).length
        cam.data.dof.aperture_fstop = FSTOP
        cam.data.dof.aperture_blades = 0
    SC.render.filepath = os.path.join(OUT, name)
    bpy.ops.render.render(write_still=True)
    print("PASS", name)


helmet_focus = head_w("Head") + Vector((0, 0, 0.11))
render_pass("_A_knight.png", True, False, True, False)
render_pass("_B_background.png", False, True, False, True)
for o in bpy.data.objects:
    o.visible_camera = base_cam[o.name]
print("PROFILE PASSES DONE", OUT)
