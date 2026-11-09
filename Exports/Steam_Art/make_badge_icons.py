"""The user's own painted badge (assets/icon/old_steam_icon_badge.webp: the knight peeking over the gold-and-green
"429" in a gold ring with green fire, transparent background) made into the icon files. The user prefers it to any
3D-rendered icon (2026-11-09: "this is the old steam icon ... I like it better that way").

    python Exports/Steam_Art/make_badge_icons.py          (system Python with Pillow + numpy; not Blender)

Writes into renders/:
  Icon_Shortcut_512x512.png          Steam "Shortcut Icon": square, transparent background
  Icon_App_184x184.jpg               Steam "App Icon": 184 x 184 JPG (no transparency -> on a dark backdrop)
  Promo_YouTube_Profile_800x800.png  YouTube profile picture: shown as a CIRCLE, so the whole badge (the knight's ears
                                     too) is sized to sit inside that circle, on the dark backdrop
The 3D icon cameras in Steam_Art.blend are skipped by render_all.py (camera property "skip"), so they never
overwrite these files.
"""
import os
import numpy as np
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
import sys
_A = dict(x.split("=", 1) for x in sys.argv[1:] if "=" in x)
# src=3d -> the Blender render of the badge (assets/icon/icon_render_1024.png), written into assets/icon/3d/ so the
# user can compare before anything in renders/ changes; out= picks another folder
SRC = os.path.join(HERE, "assets", "icon", "icon_render_1024.png") if _A.get("src") == "3d" else     _A.get("src", os.path.join(HERE, "assets", "icon", "old_steam_icon_badge.webp"))
OUT = _A.get("out", os.path.join(HERE, "assets", "icon", "3d") if _A.get("src") == "3d" else os.path.join(HERE, "renders"))
BACK = (8, 20, 11)              # dark green-black backdrop where transparency is not allowed (matches the art)


def badge():
    im = Image.open(SRC).convert("RGBA")
    a = np.asarray(im)[..., 3]
    ys, xs = np.where(a > 8)
    box = (xs.min(), ys.min(), xs.max() + 1, ys.max() + 1)
    return im.crop(box)


def square(im, pad):
    """The badge centred in a transparent square, `pad` = margin as a share of the square."""
    w, h = im.size
    side = int(round(max(w, h) / (1.0 - 2 * pad)))
    sq = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    sq.alpha_composite(im, ((side - w) // 2, (side - h) // 2))
    return sq


def on_backdrop(sq, glow=True):
    side = sq.size[0]
    yy, xx = np.mgrid[0:side, 0:side]
    r = np.clip(np.hypot(xx - side / 2, yy - side / 2) / (side * 0.72), 0, 1)[..., None]
    inner = np.array([20, 46, 24], np.float32)
    outer = np.array(BACK, np.float32)
    k = (1 - r) ** 1.4
    bg = Image.fromarray((inner * k + outer * (1 - k)).astype(np.uint8), "RGB").convert("RGBA")
    if glow:                    # a soft green glow round the badge
        a = sq.getchannel("A").filter(ImageFilter.GaussianBlur(side * 0.025))
        g = Image.new("RGBA", sq.size, (90, 255, 110, 0))
        g.putalpha(a.point(lambda v: int(v * 0.35)))
        bg.alpha_composite(g)
    bg.alpha_composite(sq)
    return bg.convert("RGB")


def main():
    os.makedirs(OUT, exist_ok=True)
    b = badge()
    sq = square(b, 0.02)
    sq.resize((512, 512), Image.LANCZOS).save(os.path.join(OUT, "Icon_Shortcut_512x512.png"))
    on_backdrop(square(b, 0.03)).resize((184, 184), Image.LANCZOS).save(
        os.path.join(OUT, "Icon_App_184x184.jpg"), quality=95)
    # the circle crop keeps a disc of the square's full width: the badge's corners (its ears) must sit inside it,
    # so it is drawn at ~86 % of the circle and centred
    on_backdrop(square(b, 0.075)).resize((800, 800), Image.LANCZOS).save(
        os.path.join(OUT, "Promo_YouTube_Profile_800x800.png"))
    print("badge icons written to", OUT, "(badge %dx%d)" % b.size)


if __name__ == "__main__":
    main()
