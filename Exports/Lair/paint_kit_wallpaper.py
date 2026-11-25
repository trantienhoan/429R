"""The storage room + bedroom kit wallpaper (2026-11-25, the lair concept Exports/Lair_Concept/concept_v1.png).

The old spooky wallpaper (K: PC: _claude/spooky_rooms/make_spooky_textures.py, 2026-10-22). The room kit's wallpaper
faces map it like the old bedroom mesh did: the floor at v 0.444, the 3 m wall top at v 0.966 (v 0.174 a metre), and
6 m of wall per picture width (u = -local x / 6). This keeps the same damask, peeling patches, drips and ceiling bloom,
and adds, for the lair concept:
- dark wood wainscoting from the floor to 0.9 m, raised panels 0.75 m wide, a chair rail on top (over the old mouldy
  floor band);
- the grime and a little mould just above the rail.
Writes Assets/M_Textures/Combos/Wall_Stylize_Set_1/Bedroom_Wallpaper_Spooky.png (back it up first).
Run: python paint_kit_wallpaper.py [out.png]
"""
import os
import random
import sys
import numpy as np
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "M_Textures"))
OUT = PROJECT + "/Combos/Wall_Stylize_Set_1/Bedroom_Wallpaper_Spooky.png"
SRC_PLASTER = PROJECT + "/Combos/Wall_Stylize_Set_1/wall_not_clean_albedo.png"

FLOOR_V = 0.444                  # v at the floor on the kit walls
PER_METRE = (0.966 - 0.444) / 3.0  # v per metre up the wall
WALL_METRES = 6.0                # metres of wall per picture width
WAINSCOT = FLOOR_V + 0.90 * PER_METRE   # top of the wainscoting (v)
BAY = 0.75                       # panel bay width (m); 8 per picture width


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def value_noise(u, v, cells_u, cells_v, seed):
    rng = np.random.default_rng(seed)
    grid = rng.random((cells_v + 1, cells_u + 1))
    grid[:, -1] = grid[:, 0]
    x = u * cells_u
    y = v * cells_v
    i0 = np.floor(x).astype(int) % cells_u
    j0 = np.clip(np.floor(y).astype(int), 0, cells_v - 1)
    fx = x - np.floor(x)
    fy = y - np.floor(y)
    fx = fx * fx * (3 - 2 * fx)
    fy = fy * fy * (3 - 2 * fy)
    a = grid[j0, i0]; b = grid[j0, i0 + 1]; c = grid[j0 + 1, i0]; d = grid[j0 + 1, i0 + 1]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def fbm(u, v, cu, cv, seed, octaves=4):
    total, amp, norm = 0.0, 1.0, 0.0
    for o in range(octaves):
        total = total + amp * value_noise(u, v, cu * 2 ** o, cv * 2 ** o, seed + o)
        norm += amp
        amp *= 0.5
    return total / norm


def uv_grid(size):
    u = (np.arange(size) + 0.5) / size
    v = 1.0 - (np.arange(size) + 0.5) / size     # image row 0 is the top (v = 1)
    return np.meshgrid(u, v)


def damask(u, v):
    du, dv = 1.0 / 16.0, 0.0706
    col = np.floor(u / du)
    cx = (u / du - col) * 2 - 1
    vy = v / dv + np.where(col % 2 == 1, 0.5, 0.0)
    cy = (vy - np.floor(vy)) * 2 - 1
    x, y = np.abs(cx), cy

    def ellipse(px, py, rx, ry):
        return np.sqrt((px / rx) ** 2 + (py / ry) ** 2)

    motif = np.zeros_like(u, dtype=bool)
    motif |= ellipse(x, y - 0.02, 0.2, 0.44) < 1.0
    motif &= ~(ellipse(x, y - 0.05, 0.09, 0.26) < 1.0)
    motif |= ellipse(x, y - 0.05, 0.04, 0.14) < 1.0
    ring = np.sqrt((x - 0.46) ** 2 + (y - 0.18) ** 2)
    motif |= np.abs(ring - 0.17) < 0.045
    motif |= ring < 0.05
    motif |= ellipse(x - 0.3, y + 0.46, 0.2, 0.08) < 1.0
    motif |= np.sqrt(x ** 2 + (y - 0.74) ** 2) < 0.09
    motif |= np.sqrt(x ** 2 + (y + 0.8) ** 2) < 0.07
    motif |= np.abs(x + np.abs(y) - 0.96) < 0.022
    return np.asarray(Image.fromarray((motif * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.9))) / 255.0


def wainscot(u, v, size):
    """Dark wood panelling, flat colours with soft gradients (the game's style). Laid out in metres on the wall."""
    wood = np.array([66, 42, 30], float)
    panel = np.array([84, 55, 38], float)
    light = np.array([128, 92, 64], float)
    dark = np.array([40, 25, 18], float)
    h = (v - FLOOR_V) / PER_METRE                # height above the floor (m)
    x = u * WALL_METRES                          # along the wall (m)
    rgb = np.zeros(u.shape + (3,)) + wood
    bx = x % BAY                                 # across one bay
    stile, p_bot, p_top = 0.085, 0.14, 0.77
    inside = (bx > stile) & (bx < BAY - stile) & (h > p_bot) & (h < p_top)
    t = np.clip((h - p_bot) / (p_top - p_bot), 0, 1)
    rgb = np.where(inside[..., None], panel * (0.86 + 0.22 * t)[..., None], rgb)
    bev = 0.022
    left = inside & (bx < stile + bev)
    right = inside & (bx > BAY - stile - bev)
    upper = inside & (h > p_top - bev)
    lower = inside & (h < p_bot + bev)
    for mask, colour, k in ((left, light, 0.55), (upper, light, 0.7), (right, dark, 0.6), (lower, dark, 0.6)):
        rgb[mask] = rgb[mask] * (1 - k) + colour * k
    grain = fbm(u, v * 0.12, 64, 3, 7, 3)
    rgb *= (0.9 + 0.18 * grain)[..., None]
    # The chair rail (0.85 - 0.9 m) with a shadow line under it.
    rail = (h >= 0.85) & (h <= 0.9)
    rr = np.clip((h - 0.85) / 0.05, 0, 1)
    rail_col = np.array([104, 72, 48], float) * (0.85 + 0.3 * np.sin(rr * np.pi))[..., None]
    rgb = np.where(rail[..., None], rail_col, rgb)
    under = (h < 0.85) & (h > 0.835)
    rgb[under] *= 0.6
    rgb *= (1 - smoothstep(0.08, 0.0, h) * 0.35)[..., None]
    return rgb


def wallpaper(size=2048):
    u, v = uv_grid(size)
    m = damask(u, v)
    base = np.array([84, 52, 70], float)
    ink = np.array([128, 88, 110], float)
    rgb = base[None, None, :] * (1 - m[..., None]) + ink[None, None, :] * m[..., None]
    rgb *= (0.82 + 0.3 * fbm(u, v, 6, 6, 11))[..., None]
    rgb *= (value_noise(u, v, 512, 512, 3) * 0.08 + 0.96)[..., None]

    # Peeling patches showing old plaster (above the rail, below the ceiling bloom).
    plaster = np.asarray(Image.open(SRC_PLASTER).convert("RGB").resize((size, size), Image.BICUBIC), float)
    plaster = plaster * np.array([0.36, 0.34, 0.32])
    rnd = random.Random(429)
    edge_noise = fbm(u, v, 64, 64, 77, 3)
    for _ in range(4):
        u0, v0 = rnd.random(), rnd.uniform(0.64, 0.86)
        r = rnd.uniform(0.018, 0.04)
        du_ = (u - u0 + 0.5) % 1.0 - 0.5
        dist = np.sqrt(du_ ** 2 + ((v - v0) * 0.88) ** 2) / r + (edge_noise - 0.5) * 0.9
        inside = dist < 1.0
        curl = (dist >= 1.0) & (dist < 1.12) & (v > v0)
        shadow = (dist >= 1.0) & (dist < 1.3) & (v <= v0)
        rgb[inside] = plaster[inside]
        rgb[curl] = rgb[curl] * 0.55 + np.array([110, 92, 88]) * 0.45
        rgb[shadow] *= 0.55

    # Water stains dripping from the top.
    wobble = fbm(u, v, 128, 32, 91, 2)
    top = 0.965
    for _ in range(9):
        u0 = rnd.random()
        length = rnd.uniform(0.08, 0.3)
        width = rnd.uniform(0.004, 0.014)
        du_ = (u - u0 + 0.5) % 1.0 - 0.5 + (wobble - 0.5) * 0.01
        along = np.clip((top - v) / length, 0, 1)
        on = (v <= top) & (v >= top - length)
        w = width * (1.0 - 0.6 * along)
        prof = np.exp(-(du_ / np.maximum(w, 1e-4)) ** 2)
        tide = np.exp(-((v - (top - length)) / 0.004) ** 2) * (np.abs(du_) < w * 1.4)
        stain = np.clip(on * prof * (1.0 - 0.5 * along) + tide * 0.6, 0, 1) * 0.85
        tint = np.array([0.6, 0.5, 0.34])
        rgb = rgb * (1 - stain[..., None]) + rgb * tint[None, None, :] * stain[..., None]
    bloom = fbm(u, v, 10, 10, 55, 3)
    band = smoothstep(0.85, 0.96, v) * smoothstep(0.45, 0.75, bloom)
    rgb = rgb * (1 - 0.45 * band[..., None]) + rgb * np.array([0.7, 0.6, 0.42])[None, None, :] * 0.45 * band[..., None]

    # Grime: darker at the ceiling, and just above the chair rail, where it's a little mouldy.
    ceiling = smoothstep(0.86, 0.965, v)
    above_rail = 1.0 - smoothstep(WAINSCOT, WAINSCOT + 0.05, v)
    rgb *= (1.0 - 0.45 * ceiling - 0.35 * above_rail)[..., None]
    mould = (fbm(u, v, 40, 40, 23, 3) > 0.6) * (1.0 - smoothstep(WAINSCOT, WAINSCOT + 0.035, v)) * 0.6
    rgb = rgb * (1 - mould[..., None]) + np.array([28, 36, 24])[None, None, :] * mould[..., None]

    # The wainscoting over the bottom.
    below = (v < WAINSCOT)[..., None]
    rgb = np.where(below, wainscot(u, v, size), rgb)
    return Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8))


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else OUT
    wallpaper().save(out)
    print("wrote", out)
