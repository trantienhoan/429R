# Room picture sheets (2026-11-20): for each room + variant, the 4 walls (N, E, S, W) as a 2x2 grid with what's on each
# wall, and the plan (north up, east right) beside them. Output: K:/Repository/429R/Exports/Rooms_2026-11-20/.
import os, glob
from PIL import Image, ImageDraw, ImageFont
SRC = r"K:/WORK_2026/08_04/_claude/rooms_2026-11-20/walls/"
OUT = r"K:/Repository/429R/Exports/Rooms_2026-11-20/"
os.makedirs(OUT, exist_ok=True)
ROOMS = {
    "1_storage": ("1. Storage room (first 'bedroom')", {
        "N": "NORTH wall: secret horseshoe door (breakable, coin toss shortcut)", "E": "EAST wall",
        "S": "SOUTH wall: window", "W": "WEST wall"}),
    "2_bedroom": ("2. Bedroom", {
        "N": "NORTH wall: chained horseshoe door (300 hp, breaks, leads nowhere)", "E": "EAST wall: window",
        "S": "SOUTH wall: window", "W": "WEST wall: W/C door (300 hp, breaks, leads nowhere)"}),
    "3_living": ("3. Living room", {
        "N": "NORTH wall: X-braced chained door (fixed, does nothing)", "E": "EAST wall: big double door (fixed, does nothing)",
        "S": "SOUTH wall: horseshoe door (breakable, coin toss shortcut)", "W": "WEST wall"}),
    "4_playroom": ("4. Playroom (living room x1.5, a cross)", {
        "N": "NORTH arm: brick wall (breaks, opens a portal)", "E": "EAST arm: brick wall (portal)",
        "S": "SOUTH arm: brick wall (portal)", "W": "WEST arm: brick wall (portal)"}),
}
VARIANTS = {"stuffs": "normal furniture (Stuffs)", "chaos": "CHAOS furniture", "pumpkin": "PUMPKIN room"}
try:
    font = ImageFont.truetype("arialbd.ttf", 22); small = ImageFont.truetype("arial.ttf", 18)
except Exception:
    font = small = ImageFont.load_default()
TW, TH, PL, GAP, HEAD = 800, 450, 912, 10, 40
made = []
for room, (title, walls) in ROOMS.items():
    for var, vname in VARIANTS.items():
        files = [SRC + f"{room}_{var}_{i}{d}.png" for i, d in enumerate("NESW")]
        plan = SRC + f"{room}_{var}_plan.png"
        if not all(os.path.exists(f) for f in files):
            continue
        W = GAP * 3 + TW * 2 + GAP + PL; H = HEAD + GAP + (TH + 30) * 2 + GAP
        sheet = Image.new("RGB", (W, max(H, HEAD + PL + 30 + GAP * 2)), (22, 18, 26))
        d = ImageDraw.Draw(sheet)
        d.text((GAP, 8), f"{title}  -  {vname}", font=font, fill=(255, 190, 110))
        for k, f in enumerate(files):
            im = Image.open(f).convert("RGB").resize((TW, TH), Image.LANCZOS)
            x = GAP + (k % 2) * (TW + GAP); y = HEAD + GAP + (k // 2) * (TH + 30)
            d.text((x, y), walls["NESW"[k]], font=small, fill=(235, 225, 210))
            sheet.paste(im, (x, y + 26))
        if os.path.exists(plan):
            im = Image.open(plan).convert("RGB").resize((PL, PL), Image.LANCZOS)
            x = GAP * 3 + TW * 2; y = HEAD + GAP
            d.text((x, y), "PLAN from under the roof (north up, east right)", font=small, fill=(235, 225, 210))
            sheet.paste(im, (x, y + 26))
            cx, cy = x + PL // 2, y + 26
            d.text((cx - 6, cy + 4), "N", font=font, fill=(255, 230, 120))
            d.text((x + PL - 24, y + 26 + PL // 2 - 12), "E", font=font, fill=(255, 230, 120))
            d.text((cx - 6, y + 26 + PL - 30), "S", font=font, fill=(255, 230, 120))
            d.text((x + 6, y + 26 + PL // 2 - 12), "W", font=font, fill=(255, 230, 120))
        out = OUT + f"{room}_{var}.jpg"
        sheet.save(out, quality=88)
        made.append(out)
print("\n".join(made))
