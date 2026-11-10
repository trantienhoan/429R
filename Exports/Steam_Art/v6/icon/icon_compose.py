"""v6 icon compose: the 3D badge render + the 2D "429" logo (the store logo, same design), the logo's green fire tucked
BEHIND the knight (masked by his alpha), the digits in front of his body. Writes the master + Steam sizes + previews.
  uv run --with pillow --with numpy python icon_compose.py <render.png> <mask.png> <out_prefix> [logo_x logo_y logo_w]
logo_x / logo_y = the logo's CENTRE (fractions of the icon), logo_w = its width (fraction)."""
import sys, os
import numpy as np
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
render_p, mask_p, out_prefix = sys.argv[1:4]
lx, ly, lw = (float(v) for v in sys.argv[4:7]) if len(sys.argv) >= 7 else (0.50, 0.73, 0.94)
FIRE_BEHIND = 0.95          # how much of the fire is hidden where the knight's HELMET is
HELMET_BOTTOM = float(os.environ.get("HELMET_BOTTOM", "0.60"))
FIRE_GAIN = float(os.environ.get("FIRE_GAIN", "1.0"))        # <1 = calmer fire (reads better small)
SHADOW_GAIN = float(os.environ.get("SHADOW_GAIN", "0.75"))   # dark backing behind the digits
SHADOW_BLUR = float(os.environ.get("SHADOW_BLUR", "90"))     # N / this = blur radius
DIGIT_POP = float(os.environ.get("DIGIT_POP", "1.0"))        # >1 = brighter, punchier digits   # icon fraction: below this the fire may cover his body

S = Image.open(render_p).convert("RGBA")
N = S.size[0]
M = Image.open(mask_p).convert("RGBA").resize(S.size, Image.LANCZOS)
knight = np.asarray(M).astype(np.float32)[..., 3] / 255.0
logo = Image.open(os.path.join(ROOT, "renders", "Library_Logo_1280x720.png")).convert("RGBA")
W = int(round(lw * N))
H = int(round(logo.height * W / logo.width))
logo = logo.resize((W, H), Image.LANCZOS)
la = np.asarray(logo).astype(np.float32)
x0 = int(round(lx * N - W / 2))
y0 = int(round(ly * N - H / 2))

# fire vs digits: the digits are the opaque core; everything soft around them is fire/glow
alpha = la[..., 3] / 255.0
digits = (alpha > 0.985).astype(np.float32)
digits = np.asarray(Image.fromarray((digits * 255).astype(np.uint8)).filter(ImageFilter.MinFilter(5)).filter(ImageFilter.MaxFilter(5))).astype(np.float32) / 255.0
# knight mask under the logo
km = np.zeros((H, W), np.float32)
ys, ye = max(0, y0), min(N, y0 + H)
xs, xe = max(0, x0), min(N, x0 + W)
km[ys - y0:ye - y0, xs - x0:xe - x0] = knight[ys:ye, xs:xe]
rows_y = (np.arange(H)[:, None] + y0) / N
km *= np.clip((HELMET_BOTTOM + 0.03 - rows_y) / 0.06, 0.0, 1.0)      # helmet only, soft edge
km = np.asarray(Image.fromarray((km * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(N / 512.0))).astype(np.float32) / 255.0
# the badge (disc + ring + knight) under the logo: fire and shadow stay inside it, only the digits break past the ring
badge = np.zeros((H, W), np.float32)
badge[ys - y0:ye - y0, xs - x0:xe - x0] = np.asarray(S).astype(np.float32)[ys:ye, xs:xe, 3] / 255.0
new_a = alpha * (1.0 - FIRE_BEHIND * km * (1.0 - digits))
new_a = new_a * (digits + (1.0 - digits) * badge * FIRE_GAIN)
if DIGIT_POP != 1.0:
    rgb = la[..., :3] / 255.0
    lum = rgb.mean(-1, keepdims=True)
    pop = np.clip(lum + (rgb - lum) * (1.0 + (DIGIT_POP - 1.0) * 0.6), 0, 1) ** (1.0 / DIGIT_POP)
    la[..., :3] = (rgb * (1 - digits[..., None]) + pop * digits[..., None]) * 255.0
la[..., 3] = np.clip(new_a * 255.0, 0, 255)
logo = Image.fromarray(la.astype(np.uint8), "RGBA")

# soft dark backing under the digits, so they lift off the moon/sky (inside the badge only)
sh = np.asarray(Image.fromarray((np.clip(digits * 255 * SHADOW_GAIN, 0, 255)).astype(np.uint8)).filter(ImageFilter.GaussianBlur(N / SHADOW_BLUR))).astype(np.float32)
sh_a = Image.fromarray(np.clip(sh * badge, 0, 255).astype(np.uint8))
shadow = Image.new("RGBA", (W, H), (2, 3, 12, 0)); shadow.putalpha(sh_a)
out = S.copy()
out.alpha_composite(shadow, (x0 + int(N * 0.004), y0 + int(N * 0.012)))
out.alpha_composite(logo, (x0, y0))
out.save(out_prefix + "_1024.png")

# Steam: 512 PNG (see-through), 184 JPG (on the store's dark blue), small checks
out.resize((512, 512), Image.LANCZOS).save(out_prefix + "_512.png")
bg = Image.new("RGBA", (184, 184), (22, 30, 46, 255))
bg.alpha_composite(out.resize((184, 184), Image.LANCZOS))
bg.convert("RGB").save(out_prefix + "_184.jpg", quality=95)
# preview sheet: big + 64 + 32 + 16 on Steam's dark and on a light background
sheet = Image.new("RGB", (1180, 560), (27, 40, 56))
sheet.paste(out.resize((512, 512), Image.LANCZOS), (20, 24), out.resize((512, 512), Image.LANCZOS))
x = 560
for s in (128, 64, 32, 16):
    im = out.resize((s, s), Image.LANCZOS)
    sheet.paste(im, (x, 40), im)
    light = Image.new("RGBA", (s + 16, s + 16), (238, 238, 238, 255)); light.alpha_composite(im, (8, 8))
    sheet.paste(light.convert("RGB"), (x - 8, 230))
    x += s + 40
sheet.save(out_prefix + "_sheet.png")
print("composed", out_prefix, "logo", (x0, y0, W, H))
