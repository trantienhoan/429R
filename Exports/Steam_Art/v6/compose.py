"""v6 compositing: lays the transparent 429 logo over a scene render (Pillow). Usage:
compose.py scene.png logo.png out.png x y width [shadow]"""
import sys
from PIL import Image, ImageFilter, ImageChops
scene_p, logo_p, out_p = sys.argv[1:4]
x, y, w = float(sys.argv[4]), float(sys.argv[5]), float(sys.argv[6])
shadow = float(sys.argv[7]) if len(sys.argv) > 7 else 0.55
S = Image.open(scene_p).convert("RGBA")
L = Image.open(logo_p).convert("RGBA")
W, H = S.size
lw = int(round(w * W)); lh = int(round(L.height * lw / L.width))
L = L.resize((lw, lh), Image.LANCZOS)
px, py = int(round(x * W)), int(round(y * H))
# soft dark backing so the logo reads on any background
a = L.split()[3]
sh = Image.new("RGBA", (lw, lh), (4, 6, 10, 0))
blur = a.filter(ImageFilter.GaussianBlur(max(2, lw * 0.025))).point(lambda v: int(min(255, v * shadow * 1.6)))
sh.putalpha(blur)
S.alpha_composite(sh, (px + int(lw * 0.005), py + int(lh * 0.02)))
S.alpha_composite(L, (px, py))
S.convert("RGB").save(out_p)
print("composed", out_p, (px, py, lw, lh))
