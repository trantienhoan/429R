# Steam art lighting: verdict

I looked at every preview of all three variants, the current renders and the GPT reference (Main_Capsule_1232x706_F.png).

## Scores (1-10)

| | moonlit | pumpkinglow | magic |
|---|---|---|---|
| Thumbnail impact (Small) | 8 | 7 | 6 |
| Focal point | 8 | 8 | 6 |
| Value contrast | 7 | 8 | 6 |
| Colour contrast | 8 | 6 | 7 |
| Knight face/eyes | 9 | 6 | 6 |
| Logo readability | 8 | 8 | 8 |
| Mood/polish | 8 | 7 | 7 |
| Render cost (10 = cheap) | 8 | 7 | 6 |
| **Total** | **64** | **57** | **52** |

- **moonlit**: the knight reads best (blue-white helmet rim, green eyes, orange/cool split) and it has clear warm/cool contrast. Weak spots: the pumpkin shells are flat, light orange; the face holes look painted on, not lit from inside; the moon plus 17 shafts pull focus from the subjects; the far haze lifts the top third to a blue-grey.
- **pumpkinglow**: the closest to the GPT art's lantern look (hot face holes, warm floor pools, black surroundings). Weak spots: the knight sinks into the dark, the castle and mountains disappear, the image is almost all orange, and the moon is a dull grey.
- **magic**: its green logo halo is the best logo treatment, closest to the reference. Weak spots: green spill makes the floor and pillars muddy, the knight becomes a dark-green blob, 110 fireflies are noise at thumbnail size, and the Library Logo shows a pale green outline because *_Halo is not hidden.

## Winner: moonlit (base script `variants/light_moonlit.py`)

Copy it to `variants/light_final.py` (it saves `variants/Steam_Art_final.blend`) and add the following, in this order.

### A. From pumpkinglow (lantern pumpkins)
1. **Jack-o'-lantern material:** replace moonlit's face block (the Map Range 0.66-0.86 -> 14 and the `Moonlit_Tint` emission link) with pumpkinglow's cut-wall + flesh emission. That is the block from `hsv` to `ecol` in light_pumpkinglow.py (lines ~101-150: `PG_Walls`, `PG_Face_Strength` 12, colours (1,0.3,0.03) -> (1,0.62,0.16)). Remove the old Emission Color/Strength links first. Also apply skin roughness 0.42.
2. **Hero face lights:** add `PG_No_Pumpkins` (hero + all Small_Pumpkin_* meshes, EXCLUDE). Add `PG_Face_Light` (point, (1,0.46,0.12), **12 W**, not 16) and `PG_Face_Spill` (spot 110 deg, **9 W**). Link both to that collection, and give them `distance_cutoff()` 0.6->1.6 and 0.5->1.3. Copy the `distance_cutoff` function as it is.
3. **Darker shell:** in moonlit, set Key_Warm `KEY_ENERGY` 0.8 -> **0.45**, so the skin goes deeper orange and the holes carry the glow. Keep moonlit's Pumpkin_Spill. Skip PG_Knight_Warm and PG_Shell_Fill: moonlit's rims already light the knight and the shell.
4. **Small-pumpkin pools:** move the Small_Pumpkin_Glow_* lights the way pumpkinglow does: z 0.035, toward the camera, energy 3.0*s, colour (1,0.42,0.09), radius 0.03, linked to PG_No_Pumpkins. These replace moonlit's 1.3x boost.
5. **Ambient:** use pumpkinglow's world Light Path trick (Is Camera Ray mix, non-camera background (0.005,0.007,0.016)). Keep moonlit's own gradient colours for the visible sky.

### B. From magic (logo)
6. **Logo halo:** delete moonlit's `LOGO_*_Glow` cards and their `Steam_Logo_Halo` material. Add magic's shell halo `LOGO_*_Halo`: shared mesh, Displace 0.03, scale 1.012, LayerWeight fade, emission (0.04,1,0.1) 4.0, no shadow/diffuse/glossy. Name the material **`Steam_Logo_Halo_Shell`** so it does not clash with the old one.
7. **Gold:** emission 0.35, colour (1,0.78,0.22), roughness 0.25. Add magic's `_Reflector_Low` / `_Reflector_High` glossy-only cards. Green face: emission 0.3.
8. **Back-pillar glow:** add only `Magic_Pillar_Glow_L` and `Magic_Pillar_Glow_R` at (±0.9,-1.85,0.15), both GREEN, at **3.5 W** each. They give a faint green echo on the back pillars. No floor glow, no R2/L2, no fireflies, no mist.

### C. Moonlit trims
9. `MOON_STRENGTH` 14 -> **10**. `BEAM_STRENGTH` 0.4 -> **0.22**. Set the far `Haze` density 0.0022 -> **0.0016**, so the top of the image goes darker navy and the moon stops competing with the pumpkin.

### D. Render script
10. Copy render_moonlit.py to render_final.py. In the transparent Library Logo branch, hide names ending in `_Halo` as well as `_Glow`. Check that Library_Logo_1280x720 has only the logo on a clear background.

Framing (not lighting): on the Library Capsule the logo covers the moon, and the empty floor at the bottom is large. That is for the camera step.
