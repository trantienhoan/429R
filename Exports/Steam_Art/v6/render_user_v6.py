"""Steam art v6, from the USER'S posed file (Steam_Art_v6_pose.blend, 2026-11-10). The knight's pose and the boss
stay exactly as the user saved them. Per picture only: the camera, the moon (just up-right of the boss head as that
camera sees it) and, for the tall pictures, the boss slid along the line the sword points at (so the sword still
aims at its face). Spiders and the burning pumpkin are hidden. Renders into renders_v6/raw/ (no logo); then
v6/compose_v6.py lays the 429 logo on and copies the badge icons + library logo.
  blender -b Exports/Steam_Art/Steam_Art_v6_pose.blend --python Exports/Steam_Art/v6/render_user_v6.py -- [preview] [only=Main,Header]
"""
import bpy, os, sys, math, time, json
from mathutils import Vector, Matrix
HERE = os.path.dirname(os.path.abspath(__file__))
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
PREVIEW = "preview" in ARGS
ONLY = next((a.split("=", 1)[1].split(",") for a in ARGS if a.startswith("only=")), None)
ST = json.load(open(os.path.join(HERE, "v6_state.json")))
ORIG = {n: Matrix(m) for n, m in ST["moon_orig"].items()}
SKY_MOON_SET = Vector(ST["sky_moon_set"])
for mod in ("lib.py", "posekit.py", "cam_v6.py", "light_v6.py", "look_v6.py", "fx_v6.py"):
    exec(open(os.path.join(HERE, mod), encoding="utf-8").read(), globals())

# GPU
prefs = bpy.context.preferences.addons['cycles'].preferences
for t in ('OPTIX', 'CUDA'):
    try:
        prefs.compute_device_type = t
        prefs.get_devices()
        if any(d.type == t for d in prefs.devices):
            for d in prefs.devices:
                d.use = (d.type == t)
            break
    except Exception:
        pass
SC.cycles.device = 'GPU'
SC.cycles.use_denoising = True

HIDE_MAIN = ("ST_Hero", "Menace_Pumpkin_Body", "ST_Spider_", "pumpkin_boss")   # V6_TMP_L (sun) stays on: it is in the file's F12 look
HIDE_NOCHARS = HIDE_MAIN + ("Pie_Knight", "ST_Pumpkin_Boss", "V6_Spark", "V6_Sword_Glow")
L0 = (-0.20, 1.15, 0.07)
T0 = (0.05, 0.0, 0.40)
SHOTS = {
    "Main_Capsule_1232x706": dict(size=(1232, 706), cam=(L0, T0, 22), logo=(0.02, 0.47, 0.44)),
    "Header_Capsule_920x430": dict(size=(920, 430), cam=(L0, (0.05, 0.0, 0.43), 19), logo=(0.03, 0.50, 0.36)),
    "Small_Capsule_462x174": dict(size=(462, 174), cam=((-0.30, 0.95, 0.20), (0.02, 0.0, 0.30), 26), logo=(0.02, 0.06, 0.56)),
    "Vertical_Capsule_748x896": dict(size=(748, 896), fit="VERTICAL", cam=((-0.27, 1.50, 0.13), (-0.27, 0.13, 0.30), 34),
                                     blade=0.74, moon=(0.12, 0.10), logo=(0.13, 0.66, 0.74)),
    "Library_Capsule_600x900": dict(size=(600, 900), fit="VERTICAL", cam=((-0.27, 1.55, 0.13), (-0.27, 0.13, 0.31), 32),
                                    blade=0.76, moon=(0.12, 0.10), logo=(0.09, 0.70, 0.82)),
    "Library_Header_920x430": dict(same="Header_Capsule_920x430"),
    "Library_Hero_3840x1240": dict(size=(3840, 1240), cam=((-0.32, 1.50, 0.09), (-0.10, 0.0, 0.46), 17)),
    "Page_BG_1438x810": dict(size=(1438, 810), cam=((-0.32, 1.50, 0.09), (-0.10, 0.0, 0.46), 22), hide=HIDE_NOCHARS,
                             exposure=-1.1),
    "Promo_KeyArt_1920x1080": dict(size=(1920, 1080), cam=(L0, T0, 21), logo=(0.02, 0.50, 0.42)),
    "Promo_Portrait_1080x1350": dict(size=(1080, 1350), fit="VERTICAL", cam=((-0.27, 1.50, 0.13), (-0.27, 0.13, 0.30), 32),
                                     blade=0.74, moon=(0.12, 0.10), logo=(0.12, 0.68, 0.76)),
    # 2026-11-10: the end card of the vertical trailer (Shorts/TikTok): sky at the top for the logo, the boss face on the
    # sword line at 30 % from the top, the knight in the middle, floor at the bottom (under the phone app's buttons)
    "Promo_Short_1080x1920": dict(size=(1080, 1920), fit="VERTICAL", cam=((-0.17, 1.62, 0.14), (-0.17, 0.13, 0.37), 30),
                                  blade=0.70),
}

# YouTube (2026-11-10): framed round the user's knight, seen from the main camera's side.
#  Banner: YouTube shows only the centre 1235x338 on phones (x 662-1897, y 551-889 of 2560x1440), so the logo and the
#  knight sit in that strip; the boss, moon and tree fill the rest (TV / desktop). Profile: shown as a circle.
_KN = head_w("Spine1")
_HD = head_w("Head")
_MC = bpy.data.objects["CAM_V6_Main_Capsule_1232x706"].location
_d = Vector((_MC.x - _KN.x, _MC.y - _KN.y, 0.0)).normalized()
_bl = _KN + _d * 1.85
SHOTS["Promo_YouTube_Banner_2560x1440"] = dict(size=(2560, 1440), cam=((_bl.x, _bl.y, 0.12), (_KN.x, _KN.y, 0.24), 16),
                                               shift=(-0.12, 0.0), logo=(0.262, 0.395, 0.196))
_pl = _HD + _d * 0.80
SHOTS["Promo_YouTube_Profile_800x800"] = dict(size=(800, 800), cam=((_pl.x, _pl.y, _HD.z + 0.05), (_HD.x, _HD.y, _HD.z + 0.08), 62))

# the user's boss: where its face is now (the user may have moved/turned the baked boss)
BOSS = bpy.data.objects["ST_Pumpkin_Boss"]
FACE_USER = BOSS.matrix_world @ Matrix(ST["boss_mine"]).inverted() @ Vector(ST["face_mine"])
BOSS_PARTS = [n for n in ("ST_Pumpkin_Boss", "V6_Boss_Under", "V6_Boss_Rim", "ST_Boss_Face_Light") if n in bpy.data.objects]
BOSS_HOME = {n: bpy.data.objects[n].matrix_world.copy() for n in BOSS_PARTS}
print("USER boss face at", tuple(round(v, 3) for v in FACE_USER))

SW = bpy.data.objects["Pie_Knight_Sword"]
# 2026-11-10 user: "re-render it like my file" -> the sparkles, the moon and the boss stay exactly as saved. Only the
# tall pictures (their own cameras) slide the boss into frame, and the moon then keeps the SAME offset from the boss
# head as in the user's main camera view, so the boss keeps its glow.
FILE_MOON = {n: bpy.data.objects[n].matrix_world.copy() for n in MOON_GROUP if n in bpy.data.objects}
SKY_NT = bpy.data.materials["BG2_Sky"].node_tree
FILE_SKY = [(nd.name, i, tuple(s.default_value)) for nd in SKY_NT.nodes if nd.bl_idname == "ShaderNodeVectorMath"
            for i, s in enumerate(nd.inputs) if not s.is_linked and hasattr(s, "default_value")
            and hasattr(s.default_value, "__len__") and len(s.default_value) == 3]


def restore_moon():
    global SKY_MOON_SET
    for n, M in FILE_MOON.items():
        bpy.data.objects[n].matrix_world = M
    for nn, i, v in FILE_SKY:
        SKY_NT.nodes[nn].inputs[i].default_value = v
    SKY_MOON_SET = Vector(ST["sky_moon_set"])
    globals()["SKY_MOON_SET"] = SKY_MOON_SET


def tan_xy(cam, v):
    cR = cam.matrix_world.col[0].xyz.normalized()
    cU = cam.matrix_world.col[1].xyz.normalized()
    cF = -cam.matrix_world.col[2].xyz.normalized()
    v = Vector(v).normalized()
    return v.dot(cR) / v.dot(cF), v.dot(cU) / v.dot(cF)


MAIN_CAM = bpy.data.objects["CAM_V6_Main_Capsule_1232x706"]
_mx, _my = tan_xy(MAIN_CAM, FILE_MOON["BG2_Moon"].translation - MAIN_CAM.location)
_fx, _fy = tan_xy(MAIN_CAM, FACE_USER - MAIN_CAM.location)
MOON_OFF = (_mx - _fx, _my - _fy)
print("moon offset from the boss face in the main view (tangent units):", tuple(round(v, 3) for v in MOON_OFF))


def boss_on_blade(cam, w, h, target_y, face):
    """move needed so the boss face sits on the sword's line (screen space) at height target_y (0 bottom, 1 top)"""
    from bpy_extras.object_utils import world_to_camera_view
    SC.render.resolution_x, SC.render.resolution_y, SC.render.resolution_percentage = w, h, 100
    hilt = SW.matrix_world @ Vector((0, 0, 0.15))
    tip = SW.matrix_world @ Vector((0, 0, -0.32))
    a = world_to_camera_view(SC, cam, hilt)
    b = world_to_camera_view(SC, cam, tip)
    ax, ay, bx, by = a.x * w, a.y * h, b.x * w, b.y * h
    if by - ay <= 1e-6:
        return Vector((0, 0, 0))
    t = (target_y * h - by) / (by - ay)
    px = min(max(bx + (bx - ax) * t, 0.34 * w), 0.80 * w)
    py = target_y * h
    tr, br, bl, tl = cam.data.view_frame(scene=SC)
    local = bl + (br - bl) * (px / w) + (tl - bl) * (py / h)
    o = cam.matrix_world.translation.copy()
    d = (cam.matrix_world @ local - o).normalized()
    k = (face.y - o.y) / d.y
    return (o + d * k) - face


OUT = os.path.join(HERE, "..", "renders_v6", "raw" + ("_preview" if PREVIEW else ""))
os.makedirs(OUT, exist_ok=True)
meta = {}
for name, shot in SHOTS.items():
    if ONLY and not any(o.lower() in name.lower() for o in ONLY):
        continue
    if "same" in shot:
        src = os.path.join(OUT, shot["same"] + ".png")
        if os.path.exists(src):
            import shutil
            shutil.copyfile(src, os.path.join(OUT, name + ".png"))
            meta[name] = dict(meta.get(shot["same"], {}))
            print("SHOT", name, "= copy of", shot["same"])
        continue
    t0 = time.time()
    loc, tgt, lens = shot["cam"]
    w, h = shot["size"]
    if name == "Main_Capsule_1232x706" and bpy.data.objects.get("CAM_V6_" + name):
        cam = bpy.data.objects["CAM_V6_" + name]          # the user's own main camera, as saved
        print("MAIN CAM kept", tuple(round(v, 3) for v in cam.location), "lens", round(cam.data.lens, 1))
    else:
        cam = cam_look("CAM_V6_" + name, loc, tgt, lens, w, h, shift_x=shot.get("shift", (0, 0))[0], shift_y=shot.get("shift", (0, 0))[1], fit=shot.get("fit", "HORIZONTAL"))
    upd()
    # boss: home, or slid onto the sword line
    for n in BOSS_PARTS:
        bpy.data.objects[n].matrix_world = BOSS_HOME[n]
    delta = boss_on_blade(cam, w, h, shot["blade"], FACE_USER) if "blade" in shot else Vector((0, 0, 0))
    for n in BOSS_PARTS:
        bpy.data.objects[n].matrix_world = Matrix.Translation(delta) @ BOSS_HOME[n]
    upd()
    face = FACE_USER + delta
    restore_moon()
    if "blade" in shot:
        fx, fy = tan_xy(cam, face - cam.location)
        cR = cam.matrix_world.col[0].xyz.normalized()
        cU = cam.matrix_world.col[1].xyz.normalized()
        cF = -cam.matrix_world.col[2].xyz.normalized()
        moon_to(cF + cR * (fx + MOON_OFF[0]) + cU * (fy + MOON_OFF[1]), scale=1.9, origin=cam.location)
    pct = int(next((a.split("=", 1)[1] for a in ARGS if a.startswith("pct=")), 40 if PREVIEW else 100))
    spp = int(next((a.split("=", 1)[1] for a in ARGS if a.startswith("samples=")), 48 if PREVIEW else shot.get("spp", 256)))
    path = R(cam.name, pct=pct, spp=spp, out="__tmp_" + name, show_logo=False, hide=shot.get("hide", HIDE_MAIN),
             exposure=shot.get("exposure", 0.0), transparent=False)
    final = os.path.join(OUT, name + ".png")
    if os.path.exists(final):
        os.remove(final)
    os.replace(path, final)
    meta[name] = dict(size=shot["size"], logo=shot.get("logo"))
    print("SHOT", name, "boss moved", tuple(round(v, 2) for v in delta), "%.1fs" % (time.time() - t0))
for n in BOSS_PARTS:
    bpy.data.objects[n].matrix_world = BOSS_HOME[n]
old = {}
mp = os.path.join(OUT, "shots.json")
if os.path.exists(mp):
    old = json.load(open(mp))
old.update(meta)
json.dump(old, open(mp, "w"), indent=1)
print("ALL DONE")
