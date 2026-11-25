"""The free Fairy's board pictures (user 2026-11-25: "less text, more image maybe? Like show the picture of the storage
room, where the choice will lead Player there"). Crops existing in-game shots to cards and writes them where the board
loads them (Assets/Resources/FairyBoard/):
- Path.png: the storage room as it is now (aged wardrobe in the NE corner; re-shot 2026-11-25 evening after the user
  asked "is this old picture? I remember put a shelf there"),
- Gate_1/2/3.png: mystery cards, a big "???" in the game's Socake font on a dark glowing card (user: "for those gate
  that we are still working on, just do some stupid image ??? ??? ???"); swap in real pictures when the gates are done,
- Stone.png: a blocking stone's pumpkin face, for the "hit a stone" hint (the K: PC's start-rooftop picture),
  cropped tight and lifted more so it still reads as a small thumbnail.
- Arrow.png: the board's arrow (points right; the left one is the same picture flipped), white with a dark
  edge so the board can tint it.
Cards are 16:9 (320 x 180), the stone square (160 x 160), lifted a little so the night shots read on the dark board.
Run: python -I make_fairy_board_pictures.py
"""
import os
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(REPO, "Assets", "Resources", "FairyBoard")
SHOTS = "K:/WORK_2026/08_04/_claude"

CARDS = [
    # name, source, crop box (left, top, right, bottom) or None for a centred 16:9, size, lift
    ("Path", SHOTS + "/dark_tutorial_2026-11-25/board_room/r1_north_old_spot.png", (40, 60, 760, 465), (320, 180), 1.45),
    ("Stone", SHOTS + "/dark_tutorial_2026-11-25/board2/stone_Cube_1.png", (290, 100, 510, 320), (160, 160), 1.6),
]


def centred(im, aspect):
    w, h = im.size
    if w / h > aspect:
        nw = int(h * aspect)
        return ((w - nw) // 2, 0, (w - nw) // 2 + nw, h)
    nh = int(w / aspect)
    return (0, (h - nh) // 2, w, (h - nh) // 2 + nh)


FONT = os.path.join(REPO, "Assets", "Fonts", "socake", "Socake.ttf")
# Each gate's mystery card a little different in colour (green, orange, purple), all "???".
MYSTERY = [("Gate_1", (60, 150, 70)), ("Gate_2", (190, 110, 40)), ("Gate_3", (130, 70, 170))]


def mystery(colour, size=(320, 180)):
    w, h = size
    im = Image.new("RGB", size, (14, 20, 16))
    glow = Image.new("RGB", size, (0, 0, 0))
    d = ImageDraw.Draw(glow)
    d.ellipse([w * 0.15, h * 0.05, w * 0.85, h * 0.95], fill=colour)
    glow = glow.filter(ImageFilter.GaussianBlur(28))
    im = Image.blend(im, glow, 0.55)
    d = ImageDraw.Draw(im)
    font = ImageFont.truetype(FONT, 96)
    text = "???"
    box = d.textbbox((0, 0), text, font=font)
    x = (w - (box[2] - box[0])) / 2 - box[0]
    y = (h - (box[3] - box[1])) / 2 - box[1]
    d.text((x + 3, y + 4), text, font=font, fill=(0, 0, 0))
    d.text((x, y), text, font=font, fill=(240, 240, 225))
    return im


def arrow(size=96, k=4):
    """A rounded right-pointing triangle, white inside a soft dark edge, on transparent."""
    big = size * k
    shape = Image.new("L", (big, big), 0)
    ImageDraw.Draw(shape).polygon([(0.16 * big, 0.08 * big), (0.86 * big, 0.50 * big), (0.16 * big, 0.92 * big)], fill=255)
    shape = shape.filter(ImageFilter.GaussianBlur(3 * k)).point(lambda v: 255 if v > 110 else 0)  # rounded corners
    edge = shape.filter(ImageFilter.MaxFilter(6 * k + 1)).filter(ImageFilter.GaussianBlur(2 * k))
    im = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    im.paste((10, 14, 12, 255), (0, 0), edge.point(lambda v: int(v * 0.85)))
    im.paste((255, 255, 255, 255), (0, 0), shape)
    return im.resize((size, size), Image.LANCZOS)


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, src, box, size, lift in CARDS:
        im = Image.open(src).convert("RGB")
        im = im.crop(box or centred(im, size[0] / size[1]))
        im = im.resize(size, Image.LANCZOS)
        im = ImageEnhance.Brightness(im).enhance(lift)
        im = ImageEnhance.Contrast(im).enhance(1.3 if name == "Stone" else 1.08)
        path = os.path.join(OUT, name + ".png")
        im.save(path)
        print("wrote", path, im.size)
    for name, colour in MYSTERY:
        path = os.path.join(OUT, name + ".png")
        mystery(colour).save(path)
        print("wrote", path, "(mystery)")
    path = os.path.join(OUT, "Arrow.png")
    arrow().save(path)
    print("wrote", path, "(arrow)")


if __name__ == "__main__":
    main()
