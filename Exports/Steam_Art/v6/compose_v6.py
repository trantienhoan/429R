"""Steam art v6: lay the 429 logo on the raw renders and gather the full Steam upload set into renders_v6/.
The logo is the existing library logo render (same design), the icons are the user's painted badge.
  uv run --with pillow python Exports/Steam_Art/v6/compose_v6.py [preview]"""
import os, sys, json, shutil
from PIL import Image, ImageFilter
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.join(HERE, "..")
PREVIEW = "preview" in sys.argv
RAW = os.path.join(ROOT, "renders_v6", "raw" + ("_preview" if PREVIEW else ""))
OUT = os.path.join(ROOT, "renders_v6", "preview" if PREVIEW else "")
os.makedirs(OUT, exist_ok=True)
LOGO = os.path.join(ROOT, "renders", "Library_Logo_1280x720.png")
meta = json.load(open(os.path.join(RAW, "shots.json")))
logo = Image.open(LOGO).convert("RGBA")


def lay_logo(S, x, y, w, shadow=0.55):
    W, H = S.size
    lw = int(round(w * W))
    lh = int(round(logo.height * lw / logo.width))
    L = logo.resize((lw, lh), Image.LANCZOS)
    px, py = int(round(x * W)), int(round(y * H))
    a = L.split()[3]
    sh = Image.new("RGBA", (lw, lh), (4, 6, 10, 0))
    sh.putalpha(a.filter(ImageFilter.GaussianBlur(max(2, lw * 0.025))).point(lambda v: int(min(255, v * shadow * 1.6))))
    S.alpha_composite(sh, (px + int(lw * 0.005), py + int(lh * 0.02)))
    S.alpha_composite(L, (px, py))
    return S


for name, m in meta.items():
    if name.startswith("Promo_Short"):       # the vertical trailer's end card art (used by the trailer build, not Steam)
        continue
    src = os.path.join(RAW, name + ".png")
    if not os.path.exists(src):
        continue
    S = Image.open(src).convert("RGBA")
    if m.get("logo"):
        S = lay_logo(S, *m["logo"])
    w, h = m["size"]
    if not PREVIEW and S.size != (w, h):
        S = S.resize((w, h), Image.LANCZOS)
    S.convert("RGB").save(os.path.join(OUT, name + ".png"))
    print("WROTE", name, S.size)
if not PREVIEW:
    # 2026-11-10 user: "use the new icon for Steam" -> the icons come from renders_v6/icon_new (the v6 icon), not the
    # painted badge in renders/ (kept there and in renders_v6/_old_badge_icons)
    for f, src in (("Library_Logo_1280x720.png", os.path.join(ROOT, "renders")),
                   ("Icon_Shortcut_512x512.png", os.path.join(ROOT, "renders_v6", "icon_new")),
                   ("Icon_App_184x184.jpg", os.path.join(ROOT, "renders_v6", "icon_new"))):
        shutil.copyfile(os.path.join(src, f), os.path.join(OUT, f))
        print("COPIED", f, "from", os.path.relpath(src, ROOT))
