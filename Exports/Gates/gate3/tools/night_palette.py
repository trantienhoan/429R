"""A night re-tint of BALLS_Palette.png for gate 3 (2026-11-23): BALLS' own models still in the valley with the kit on
(the mountains, the ground ring round the valley, the grass tufts) in moonlit night colours that sit with the kit and the
painted Halloween sky. Writes Assets/BallValley/Kit/BALLS_Palette_Night.png (the day palette is never touched).

Run: uv run --python 3.12 --with pillow --with numpy python -I Exports/Gates/gate3/tools/night_palette.py [--preview]"""
import os, sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", ".."))
SRC = os.path.join(REPO, "Assets", "BallValley", "Resources", "BALLS", "BALLS_Palette.png")
OUT = os.path.join(REPO, "Assets", "BallValley", "Kit", "BALLS_Palette_Night.png")

img = Image.open(SRC).convert("RGBA")
a = np.asarray(img).astype(np.float32) / 255.0
rgb, alpha = a[..., :3], a[..., 3:]
lin = rgb ** 2.2                                                   # to linear light
luma = (lin * np.array([0.2126, 0.7152, 0.0722])).sum(-1, keepdims=True)
lin = lin * 0.7 + luma * 0.3                                       # a little less colour at night
lin = lin * np.array([0.42, 0.48, 0.78])                           # moonlight: darker, blue
lin = lin + np.array([0.012, 0.006, 0.022]) * (1.0 - luma)         # violet in the shadows (the kit's stone)
out = np.clip(lin, 0, 1) ** (1 / 2.2)
os.makedirs(os.path.dirname(OUT), exist_ok=True)
Image.fromarray((np.concatenate([out, alpha], -1) * 255 + 0.5).astype(np.uint8), "RGBA").save(OUT)
print("wrote", os.path.relpath(OUT, REPO), img.size)
if "--preview" in sys.argv:
    side = Image.new("RGBA", (img.width * 2, img.height))
    side.paste(img, (0, 0))
    side.paste(Image.open(OUT), (img.width, 0))
    p = os.path.join(REPO, "Exports", "Gates", "gate3", "kit_renders", "night_palette_preview.png")
    side.save(p)
    print("preview", os.path.relpath(p, REPO))
