# Secret room sheets (2026-11-20): the bedroom's north wall with the moved door, the opening, the room lit with its giant
# pumpkin, after the candies, with the Giant Spider, the W/C door and the fight (folded). Into Exports/Rooms_2026-11-20/.
import os
from PIL import Image, ImageDraw, ImageFont
SRC = r"K:/WORK_2026/08_04/_claude/rooms_2026-11-20/walls/"
OUT = r"K:/Repository/429R/Exports/Rooms_2026-11-20/"
try:
    font = ImageFont.truetype("arialbd.ttf", 24); small = ImageFont.truetype("arial.ttf", 19)
except Exception:
    font = small = ImageFont.load_default()

SHEETS = {
    "7_secret_room_1_door_and_candies": ("Bedroom secret room: the moved door, opening it, the giant pumpkin -> 33 candies", [
        ("2_bedroom_stuffs_0N.png", "Bedroom north wall: the chained door moved left, by the hung shelf + broom, mat with it"),
        ("6_candies_0dark_a_bedroom_to_secret.png", "Door broken: the secret room behind it, still dark"),
        ("6_candies_1lit_b_inside_from_door.png", "Walked in: its light on, the giant pumpkin (2.7 m, 2 health)"),
        ("6_candies_1lit_c_corner_NE.png", "From its far corner: same size as the bedroom, no window or 2nd door"),
        ("6_candies_2after_c_corner_NE.png", "Pumpkin broken: 33 random candies"),
        ("6_candies_1lit_e_aerial.png", "From above (roofs off): the bedroom and the secret room north of it"),
    ]),
    "7_secret_room_2_spider_and_fold": ("Bedroom secret room: the Giant Spider, the W/C door, and the fight", [
        ("6_spider_1lit_d_corner_NW.png", "Secret room lit (chaos run), giant pumpkin"),
        ("6_spider_2spider_b_inside_from_door.png", "Pumpkin broken: the Giant Spider (33 health) climbs out"),
        ("6_spider_2spider_c_corner_NE.png", "The Giant Spider (white boss x1.6, grows in over 7 s)"),
        ("6_spider_3_wc_door_broken.png", "W/C door broken: behind furniture, past it only the fall edge"),
        ("6_candies_4_fight_folded_aerial.png", "Fight starts (EnemyManager on): the secret room breaks away, floor and all"),
    ]),
}
TW, TH, GAP, HEAD = 900, 506, 12, 46
for name, (title, tiles) in SHEETS.items():
    tiles = [(f, cap) for f, cap in tiles if os.path.exists(SRC + f)]
    if not tiles:
        continue
    cols = 2; rows = (len(tiles) + 1) // 2
    sheet = Image.new("RGB", (GAP + cols * (TW + GAP), HEAD + rows * (TH + 34 + GAP) + GAP), (22, 18, 26))
    d = ImageDraw.Draw(sheet)
    d.text((GAP, 10), title, font=font, fill=(255, 190, 110))
    for k, (f, cap) in enumerate(tiles):
        x = GAP + (k % cols) * (TW + GAP); y = HEAD + (k // cols) * (TH + 34 + GAP)
        d.text((x, y), cap, font=small, fill=(235, 225, 210))
        sheet.paste(Image.open(SRC + f).convert("RGB").resize((TW, TH), Image.LANCZOS), (x, y + 30))
    sheet.save(OUT + name + ".jpg", quality=88)
    print(OUT + name + ".jpg", len(tiles))
