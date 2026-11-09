# Steam art, rendered from your own models

**Pipeline:** `build_steam_art.py` (only to start over) -> `assemble_v2.py` -> `knight_pass.py` -> `lighting_pass.py` -> `render_all.py`

- **Steam_Art.blend**: a copy of `Assets/Blender/Knight_429.blend` with a night scene added.
  - The v2 pieces come from `assets/` (see `assets/README.md`): the "429" logo, the hero pumpkin, 8 small pumpkins and the far background (moon, castle, mountains, graveyard, clouds, bats).
  - It also has candles, lights, and one camera per Steam image (`CAM_...`).
  - The original Knight_429.blend is untouched.
- **Each logo** (`LOGO_...`, in the Logos collection) is a child of its camera, so it stays put in that picture when you move the camera.
  - All logos share one mesh, `Logo_429_v2_Mesh` (the v2 logo joined into one piece by `assemble_v2.py`).
- **Retired pieces** (old pumpkins, old logo mesh, old castle, mountains, bats, moon mesh, haze) are not deleted: they sit in the hidden collection `V2_Replaced`. The old `Moon` object still gives the moon's position to the lighting.
- **Templates** (`V2_Templates`, hidden): the appended logo collection (with its reflector cards) and the small-pumpkin template.

## 1. build_steam_art.py (only to start over)

Rebuilds Steam_Art.blend from Knight_429.blend from scratch. It overwrites your tweaks, the v2 assets and the lighting pass. Run `assemble_v2.py` and `lighting_pass.py` after it.

    "E:/Steam/steamapps/common/Blender/blender.exe" -b Assets/Blender/Knight_429.blend --python Exports/Steam_Art/build_steam_art.py

## 2. assemble_v2.py (swap in the v2 assets)

Appends the assets from `assets/` and saves over Steam_Art.blend.

- **Logo:** `Logo_429.blend` is joined into `Logo_429_v2_Mesh`, and every `LOGO_...` uses it, at the same width in its picture as the old logo. The back plate's darker look is kept as a vertex attribute (`logo_plate`).
- **Hero pumpkin (v4):** the `Menace_Pumpkin` collection from `assets/Menace_Pumpkin.blend` = your game pumpkin (pumpkin_0) with only the eyes, nose and mouth recarved. Its root gets the old pumpkin's own transform, moved 7 cm camera-left (`HERO_NUDGE`) so the knight's wave is clear.
- **Small pumpkins (v4):** real copies of `Menace_Pumpkin_Small` (`Menace_Pumpkin_Small_0..7_...`, in `V2_Small_Pumpkins`) on the old spots, each with its own light in the mouth. The old v2/v3 `Hero_Pumpkin_v2` / `Small_Pumpkin_v2` are removed on each run (`Hero_Pumpkin.blend` is no longer used).
- **Background:** the `Background_v2` collection, already in place for the cameras.
- It is safe to run again: each run removes what the last one appended and appends fresh, so a rebuilt asset file is picked up. It also removes the `LP_...` objects, so **always run knight_pass.py and lighting_pass.py after it**.
- The first run backs the file up to `variants/Steam_Art_before_v2.blend`.

      "E:/Steam/steamapps/common/Blender/blender.exe" -b Exports/Steam_Art/Steam_Art.blend --python Exports/Steam_Art/assemble_v2.py

## 2b. knight_pass.py (smiley eyes + right-hand wave)

Gives the Pie Knight happy "^ ^" eyes (new materials `KP_Smile_Eye_A/_B` on his eye domes, no texture edited) and a new action `Steam_Wave_Pose` (a copy of `Pie_Knight_Pose`, which is kept): forearm up beside the helmet, open glove at visor height, palm to the camera. Cartoon stretch (arm 60 % longer, glove 1.5x) so the hand clears his big helmet. Adds `KP_Hand_Light` (lights only the knight). Saves over Steam_Art.blend; `-- nosave` to test. All numbers at the top of the script.

    "E:/Steam/steamapps/common/Blender/blender.exe" -b Exports/Steam_Art/Steam_Art.blend --python Exports/Steam_Art/knight_pass.py

## 3. lighting_pass.py (lighting + camera framing)

Relights Steam_Art.blend and saves over it. The look is moonlit, with glowing lantern pumpkins and a green halo on the logo.

- All the numbers are at the top of the script.
- It also sets the camera framing (the `cam_euler` / `cam_look` lines) and each logo's place and **width** (`LOGOS`).
- The pumpkins (v4 menace pumpkins, solid like the game one) each have a light sunk in the mouth pit (`HERO_INNER_W`, `SMALL_INNER_W`). It skips the pumpkins as receivers, so the pit walls shape it into a warm glow on the floor in front. The glow you see in the eyes and mouth is `GLOW_PUPIL` / `GLOW_MOUTH` / `GLOW_SOCKET` (the `MP_*` materials' "Glow" value). The cool top fill skips the pumpkins (it made the skin chalky).
- The moonlight lights the characters and props only, not the floor or the far background (the background brings its own moon lights).
- `HAZE_NEAR_GAIN` and `GROUND_ALBEDO_GAIN` darken the far ground seen through the wall gap.
- Each logo carries the logo asset's own reflector cards (`LOGO_REFLECTORS`), so the gold has something warm to mirror.
- It is safe to run again: everything it adds is named `LP_...` and gets rebuilt on each run.
- The first run backs up the unlit file to `variants/Steam_Art_before_lighting.blend`.

      "E:/Steam/steamapps/common/Blender/blender.exe" -b Exports/Steam_Art/Steam_Art.blend --python Exports/Steam_Art/lighting_pass.py

## 4. render_all.py (render)

Renders every image at its Steam size into `renders/` (about 3 minutes). With `preview` it renders smaller and faster into `renders_preview/`. `pct=` and `samples=` change the size and samples; `only=` takes one name or a comma list.

    "E:/Steam/steamapps/common/Blender/blender.exe" -b Steam_Art.blend --python render_all.py
    "E:/Steam/steamapps/common/Blender/blender.exe" -b Steam_Art.blend --python render_all.py -- preview
    "E:/Steam/steamapps/common/Blender/blender.exe" -b Steam_Art.blend --python render_all.py -- preview pct=100 only=Main,Vertical

**Steam rules the render script follows:**
- The library hero has no logo.
- The page background has no logo.
- The library logo is the logo alone on a transparent background. Its `_Glow` and `_Halo` parts are hidden there.

`variants/` holds the three lighting tests (moonlit, pumpkinglow, magic) and the verdict that picked the final recipe.

## v3 (2026-11-08)
- **Logo** (`assets/logo_build.py`, style E is now the default): faceted heavy serif digits (Bookman Bold outlines cut into flat chisel facets), 0.30 deep gold side walls, sharp chamfer bevels, dark cracked enamel, warm reflector cards plus one black card, and a green fire card baked into the logo mesh (`Logo_Fire_v3`, camera rays only, alpha = flame). The old `_Halo` shell is gone. Each logo is turned (`LOGO_YAW`, `LOGO_PITCH` in lighting_pass.py).
- **Pumpkin** (`assets/pumpkin_build.py`): taller, deeper ribs, thicker carved walls, teeth 10, no leaf, hotter carving colours, more subsurface.
- **Background** (`assets/background_build.py`): bumpy rising hillside, about 3x the graves, mid trees, two frame trees, a lit winding path up to the castle, castle 0.95 and moved left, heavy cloud masses, darker sky, and an empty `Moon_Anchor` (lighting_pass reads it).
- **lighting_pass.py**: plain AgX plus a curves S-curve, darker values, new logo sizes, knight warm/green lights, per-picture hides (camera property `hide_objs`, read by render_all.py). The v2 scripts and blends are backed up in `variants/*_v2_backup.*`.

## v4 (2026-11-09)
- **Logo**: just "429" (the pumpkin top on the 9 is off, `logo_build.py` `stem=0`), same width, so the logo sizes are unchanged.
- **Pumpkins**: your own game pumpkin with a menace face (`assets/Menace_Pumpkin.blend`, built by `assets/menace_pumpkin_build.py` from Knight_429.blend, which it only reads) replaces the v3 pumpkin, hero and small ones. Small pumpkin 7 is hidden in every shot (it sat in the gap between the pumpkin and the waving hand).
- **Knight**: `knight_pass.py` (smiley eyes, waves with his right hand) is now part of the pipeline.
- Backups of v3: `variants/assemble_v3_backup.py`, `lighting_pass_v3_backup.py`, `README_v3_backup.md`, `Steam_Art_v3_backup.blend`, `renders_v3/`.
