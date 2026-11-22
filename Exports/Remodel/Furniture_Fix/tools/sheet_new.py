# Numbered sheet of the new pieces (pieces_new/*_whole.png) -> Furniture_new_v1.jpg. Run from the Furniture_Fix folder.
import json
from PIL import Image, ImageDraw, ImageFont
info = json.load(open("pieces_new/pieces.json"))
T = 300; COLS = 5
font = ImageFont.truetype("arial.ttf", 22)
s = Image.new("RGB", (COLS * T, 3 * T), (70, 70, 74)); d = ImageDraw.Draw(s)
for i, p in enumerate(info):
    s.paste(Image.open(f"pieces_new/{p['name']}_whole.png").convert("RGB").resize((T, T)), ((i % COLS) * T, (i // COLS) * T))
    d.text(((i % COLS) * T + 8, (i // COLS) * T + 6), str(i + 1), fill=(255, 240, 200), font=font)
s.save("Furniture_new_v1.jpg", quality=90)
print("sheet", s.size)
