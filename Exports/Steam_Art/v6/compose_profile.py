"""Profile picture, step 2: the sharp knight (pass A) over the blurred scene (pass B), then a soft glow (the bloom the
passes left out), a gentle vignette. Writes Knight_Profile_2048.png, Knight_Profile_800.png and a check sheet
(the circle crop most sites use, at 400 / 64 / 32 px).
  uv run --with pillow --with numpy python compose_profile.py [preview]"""
import os, sys
import numpy as np
from PIL import Image, ImageFilter, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
PREVIEW = "preview" in sys.argv
D = os.path.join(HERE, "..", "renders_v6", "profile" + ("_preview" if PREVIEW else ""))
A = np.asarray(Image.open(os.path.join(D, "_A_knight.png")).convert("RGBA")).astype(np.float32) / 255.0
B = np.asarray(Image.open(os.path.join(D, "_B_background.png")).convert("RGB")).astype(np.float32) / 255.0
a = A[..., 3:4]
C = A[..., :3] * a + B * (1.0 - a)
N = C.shape[0]


def blur(x, r):
    im = Image.fromarray(np.clip(x * 255, 0, 255).astype(np.uint8))
    return np.asarray(im.filter(ImageFilter.GaussianBlur(r))).astype(np.float32) / 255.0


# glow: the brightest parts (eyes, blade, moon, embers) spread softly, like the compositor bloom
lum = C.max(-1, keepdims=True)
bright = C * np.clip((lum - 0.62) / 0.38, 0, 1) ** 1.5
glow = 0.55 * blur(bright, N * 0.006) + 0.40 * blur(bright, N * 0.022) + 0.25 * blur(bright, N * 0.06)
C = 1.0 - (1.0 - C) * (1.0 - np.clip(glow, 0, 1))
# gentle vignette toward the night blue (the circle crop hides the corners anyway)
yy, xx = np.mgrid[0:N, 0:N] / (N - 1.0)
r = np.hypot(xx - 0.5, yy - 0.47) / 0.72
v = 1.0 - 0.30 * np.clip((r - 0.45) / 0.55, 0, 1) ** 1.6
C = C * v[..., None]
out = Image.fromarray(np.clip(C * 255 + 0.5, 0, 255).astype(np.uint8))
tag = "_preview" if PREVIEW else ""
out.save(os.path.join(D, "Knight_Profile_%d%s.png" % (N, tag)))
if not PREVIEW:
    out.resize((800, 800), Image.LANCZOS).save(os.path.join(D, "Knight_Profile_800.png"))

# check sheet: the square, the circle crop (most profile pictures are shown round) and small sizes
def circle(im, s):
    im = im.resize((s, s), Image.LANCZOS).convert("RGBA")
    m = Image.new("L", (s * 4, s * 4), 0)
    ImageDraw.Draw(m).ellipse([0, 0, s * 4 - 1, s * 4 - 1], fill=255)
    im.putalpha(m.resize((s, s), Image.LANCZOS))
    return im


sheet = Image.new("RGBA", (1000, 470), (24, 26, 33, 255))
sheet.alpha_composite(out.resize((420, 420), Image.LANCZOS).convert("RGBA"), (20, 25))
sheet.alpha_composite(circle(out, 400), (460, 35))
sheet.alpha_composite(circle(out, 64), (880, 60))
sheet.alpha_composite(circle(out, 32), (896, 150))
sheet.convert("RGB").save(os.path.join(D, "_check%s.png" % tag))
print("composed", D, N)
