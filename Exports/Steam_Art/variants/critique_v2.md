# Critique v2 (art director pass, 2026-11-08)

Looked at: all 9 `renders_preview/*.png` (60 %), zoom crops of Main (logo, pumpkin, background),
`assets/logo_tests/23_final_zoom.png`, `assets/pumpkin_tests/final_Hero.png` + `final_Small.png`,
`assets/background_tests/r8_crop_center.png`, and the GPT Main / Vertical / Library Logo.

## Verdict in one line
Better models than v1, but the pictures are still **mid-grey-blue soup with a sticker logo**. The GPT art wins on
value contrast (black frame, hot pumpkin, bright moon), on logo drama (faceted heavy gold, green fire, 3D depth) and on
a background that has a *story* (castle on a crag with a winding lit path, layered mountains, heavy moonlit clouds, trees
framing the corners). Ours has a flat pale plane in the wall gap, a small far castle and a milky sky.

## Global problems (all images)
1. **Values.** Sky, far ground, wall and floor all sit in the same 20-40 % blue band. Nothing is near black except the
   vignette, so the moon, the pumpkin and the logo do not pop. GPT: near-black corners and foreground, 3 clear depth planes.
2. **Far ground in the wall gap** is one flat grey-blue plane with a few rock lumps and lanterns. This is exactly the
   user's "from afar looks too simple". It is the brightest large area after the moon and pulls the eye.
3. **Castle** is small, far right, cut by the frame in Header/Page_BG/Library_Hero, and sits on a vague hill. The
   GPT castle is the second focal point: big, centre-right, on a crag, with a lit switchback path leading to it.
4. **Sky** is a smooth gradient with a few thin wisps. No cloud mass, no moonlit cloud edges, no big dark cloud shapes to
   frame the moon. Bats are fine.
5. **Logo** reads as a rounded candy sticker: bubble letterforms with a double tube outline, flat front-on, pale champagne
   gold, flat mint enamel with a reflected light band, hard lime outline instead of a glow. No visible extrusion depth.
6. **Hero pumpkin**: carving glow is pale peach (AgX washes it), cut walls are uniform pale flesh, teeth are small notches,
   the skin is smooth plastic orange with no colour variation. The leaves on top read as a grey-green plastic bag.
   The moon puts a white rim/bloom on its top-left.
7. **Tangents**: knight touches the pumpkin's right edge in Header/Library_Header/Small; small pumpkin peeks out under
   the "9" in Header and Main; pumpkins cut by the frame edge in Main, Vertical, Library_Capsule, Page_BG, Library_Hero.
8. **Knight** is under-lit on his front: dark green mass, gold trim barely reads, helmet top has a blown white moon
   highlight. The pumpkin's warm light barely reaches him.

## Per image

### Main_Capsule_1232x706
- Logo too small (~33 % of width; GPT ~42 % and twice the height) and it sits over the busiest patch (pillar + candle
  + graveyard). Width 0.212 -> 0.27, move to x -0.19, y +0.03 so the "9" stem clears the pillar top.
- Small pumpkin glowing under the "9": move/hide `Small_Pumpkin_v2_*` that sits behind the logo for this camera.
- Bottom-left: two small pumpkins cut by the frame. Pull them in or out of frame entirely.
- Bottom-right quarter is empty brown floor. Add one small pumpkin + a candle cluster there to balance the logo.
- Pumpkin/knight gap is too tight; separate by 3-4 cm.

### Header_Capsule_920x430 and Library_Header_920x430
- Moon cut at the top edge, castle crammed into the top-right corner and cut: tilt both cameras up +1.5 deg
  (86.85 -> 88.3) and slide them 0.05 m toward +X so the castle is fully in.
- Knight tangent to the pumpkin: open a gap.
- Logo ~35 % width; on a header capsule it must be ~45 %. Width 0.227 -> 0.29.
- Small pumpkin peeks under the logo (same fix as Main).

### Small_Capsule_462x174
- At this size only the logo matters. Logo 0.258 -> 0.33 width and give it a dark backdrop (darken the pillars/candles
  behind it, or lower the candle `LP_` lights for this camera).
- Knight + pumpkin + logo compete in a 174 px tall strip. Consider cropping the knight to his head and helmet eyes, or dropping him.
- Background lanterns are bright dots next to the logo. Kill them for this shot.

### Vertical_Capsule_748x896 and Library_Capsule_600x900
- The middle 35 % band (flat wall tops, pale ground plane, boxy pillars) is the weakest part of the whole set.
  Hide it: castle on a crag rising behind the wall, dense graveyard + dead trees, a lit path.
- Hero pumpkin touches the left frame edge in Library_Capsule; small pumpkin cut at the left edge in both.
- Characters could be ~15 % larger and higher (the bottom 15 % is plain floor).
- Logo placement is good (top, on sky). Once the logo has a real glow, add dark cloud mass behind it so the gold pops.

### Page_BG_1438x810
- No logo: correct. Castle cut at the top-right; left half is empty dark floor. Steam dims this image, so keep it dark,
  but frame it: a bare tree branch across the top-left corner and the castle fully in frame.

### Library_Hero_3840x1240
- No logo: correct. Steam puts the library logo bottom-left by default, so the small pumpkins there will sit under it; keep
  that area simple and dark, but not cut pumpkins.
- Huge flat floor in the bottom 30 % and the flat far plane in the gap. Widest shot, so the background problems show most here.
- Right side: wall and pillar are a dark flat block. Add the castle crag or bare trees behind it.

### Library_Logo_1280x720
- Transparent, logo only: correct. But it fills only ~50 % width / 40 % height. Width 0.364 -> 0.55 (GPT fills ~95 %).
- The dark back plate outline is lumpy and reads as a sticker cut-out. Give it a hard bevelled edge or drop it and keep
  a soft green glow with alpha.
- The logo is slightly yawed: make it square to the camera, or make the turn a deliberate 6-8 deg.

## Ranked fixes (most important first)
1. **Remodel the far middle ground** (`assets/background_build.py`, BG2_Ground / BG2_Path): replace the flat plane with a
   displaced, rising hill (noise displacement 0.3-0.6 m) carrying a dense graveyard (3x the tombstones/crosses, tilted),
   bare-tree silhouettes and a winding lit switchback path (lantern every 2-3 m) up to the castle crag. No flat plane may show in the wall gap.
2. **Castle as second focal point**: CASTLE_SCALE 0.80 -> ~1.3, on a rocky crag (PLATEAU higher), moved left so it sits
   framed in the wall gap and never touches the frame in any camera. Add a warm rim from window light and a cool moon rim.
3. **Value/contrast pass** (`lighting_pass.py`): darken the sky top (world ramp element 0 -> ~0.004,0.01,0.02), cut
   `HAZE_NEAR_GAIN` 0.5 -> 0.25 and `GROUND_ALBEDO_GAIN` 0.55 -> 0.35, vignette To Max 0.72 -> 0.85, and drop Look
   "AgX - Punchy" for base AgX + compositor curves, so the pumpkin's orange stays saturated.
4. **Logo letterforms** (`assets/logo_build.py`): switch from rounded tube outlines to faceted, chamfered heavy serif
   digits (sharp 2-3 segment bevels, not round), extrusion 0.18 -> ~0.35, and show the depth with a small 6-10 deg turn
   (yaw or pitch) per camera in `LOGOS`. Keep the pumpkin-stem "9".
5. **Logo gold** (`Logo_Gold_v2`): base colour ~ (1.0, 0.72, 0.29), metallic 1, roughness 0.22-0.3, a darker anisotropic
   or noise wear; reflectors `Logo_Reflector_*_v2` warmer (orange) plus one BLACK card so the bevels have dark/bright edges.
   Champagne must become saturated gold.
6. **Logo glow**: replace the `_Halo` displaced-shell (HALO_STRENGTH/HALO_GROW) with a soft glow: an emissive card behind
   the logo with a radial or outline falloff (or noise "green fire" wisps), and per-logo compositor Glare (Fog Glow) on a
   logo-only view layer. Kill the reflected light band on the enamel (move/rotate `Logo_429_Reflector_Top`).
7. **Pumpkin carving glow** (`PV2_Pumpkin_Inner`, `PV2_Pumpkin_Cut_Flesh`): hot orange-yellow cavity (1.0, 0.42, 0.08),
   cut walls graded pale-yellow at the rind to deep orange inside, and a darker rind band on the outer lip. Raise
   `HERO_INNER_W` once the AgX look is fixed. Make the teeth bigger and blockier (`TOOTH_H` 6.5 -> ~10) to match pumpkin_0's toothy grin.
8. **Pumpkin skin** (`assets/pumpkin_build.py`): colour variation (yellow-green toward the stem, darker red-orange in the
   grooves), fine vertical striations + small blemishes in the bump, slightly higher SSS radius. Replace the pale leaves
   on top with a dark curled leaf or remove them.
9. **Sky**: big moonlit cloud masses (dark bellies, silver edges) framing the moon and behind the logo in the tall shots.
   Add more layers to CLOUDS in `background_build.py`; bare tree branches framing the top corners in the wide shots.
10. **Tangents and peekers**: separate the knight and pumpkin by 3-4 cm. Hide/move the small pumpkin behind the logo
    (Header, Main). Pull every cut pumpkin fully into frame or fully out (Main, Vertical, Library_Capsule, Page_BG, Hero).
11. **Logo sizes and framing in `lighting_pass.py`**: Header/Library_Header 0.227 -> 0.29, Main 0.212 -> 0.27,
    Small 0.258 -> 0.33, Library_Logo 0.364 -> 0.55; Header cameras tilt 86.85 -> ~88.3 deg so the moon is not cut.
12. **Knight and moon fixes**: a warm `LP_` area light from the pumpkin side onto the knight's front, plus a green rim;
    clamp the moon highlight on the helmet top and the pumpkin's top-left (light-link the moonlight off the hero pumpkin
    shell, or lower its spec). Move the moon anchor out of `V2_Replaced`: add an empty `Moon_Anchor` in
    `Background_v2` at MOON and make `lighting_pass.py` read it instead of `OB["Moon"]`.

Noise: previews at 48 spp are clean enough. Check the floor carving and the logo enamel for denoiser blotches at 100 %.
