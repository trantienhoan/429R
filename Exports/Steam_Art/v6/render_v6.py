"""Steam art v6 (2026-11-10): the knight raises his glowing sword at a giant Pumpkin Boss looming over the back wall,
moon behind the boss's head, the Tree of Light glowing on the right, embers, violet night grade.
Opens the ORIGINAL Steam_Art.blend (never saved over), builds the v6 look on top, renders every shot into
renders_v6/raw/ (no logo), then v6/compose_v6.py lays the 429 logo on.
  blender -b Exports/Steam_Art/Steam_Art.blend --python Exports/Steam_Art/v6/render_v6.py -- [preview] [only=Main,Header] [save]
"""
import bpy, os, sys, math, time
from mathutils import Vector, Matrix, Quaternion
HERE = os.path.dirname(os.path.abspath(__file__))
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
PREVIEW = "preview" in ARGS
ONLY = next((a.split("=", 1)[1].split(",") for a in ARGS if a.startswith("only=")), None)
SPP = next((int(a.split("=", 1)[1]) for a in ARGS if a.startswith("samples=")), None)
PCT = next((int(a.split("=", 1)[1]) for a in ARGS if a.startswith("pct=")), None)
for mod in ("lib.py", "posekit.py", "pose_g.py", "boss_v6.py", "stage_v6.py", "cam_v6.py", "light_v6.py", "look_v6.py",
            "fx_v6.py", "grade_v6.py", "shots_v6.py"):
    exec(open(os.path.join(HERE, mod), encoding="utf-8").read(), globals())
OUT = os.path.join(HERE, "..", "renders_v6", "raw" + ("_preview" if PREVIEW else ""))
os.makedirs(OUT, exist_ok=True)
build_v6()
for name, shot in SHOTS.items():
    if ONLY and not any(o.lower() in name.lower() for o in ONLY):
        continue
    if "same" in shot:          # e.g. the library header = the store header picture
        src = os.path.join(OUT, shot["same"] + ".png")
        if os.path.exists(src):
            import shutil
            shutil.copyfile(src, os.path.join(OUT, name + ".png"))
            print("SHOT", name, "= copy of", shot["same"])
        continue
    t0 = time.time()
    setup_shot(name, shot)
    pct = PCT or (40 if PREVIEW else 100)
    spp = SPP or (48 if PREVIEW else shot.get("spp", 256))
    path = R("CAM_V6_" + name, pct=pct, spp=spp, out="__tmp_" + name, show_logo=False, hide=shot.get("hide", ()),
             exposure=shot.get("exposure", 0.0), transparent=shot.get("transparent", False))
    final = os.path.join(OUT, name + ".png")
    if os.path.exists(final):
        os.remove(final)
    os.replace(path, final)
    print("SHOT", name, "%.1fs" % (time.time() - t0), final)
if "save" in ARGS:
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "..", "Steam_Art_v6.blend"), copy=True)
    print("SAVED Steam_Art_v6.blend")
