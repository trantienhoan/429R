# Steam art, rendered from your own models

**Pipeline (v5):** `build_steam_art.py` (only to start over) -> `assemble_v2.py` -> `knight_pass.py` -> `story_pass.py` -> `lighting_pass.py` -> `render_all.py`

The scripts find their own folder, so they run on either PC (they used to name `C:/Github_Repository/...`). Blender is at `E:/Steam/steamapps/common/Blender/blender.exe` on one PC and `G:/steam/steamapps/common/Blender/blender.exe` on the other. If textures render purple or missing, run `fix_paths.py` once.

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

## v5 (2026-11-09): the story, "the knight guards the Tree of Light"

**Run order:**
1. `knight_pass.py`
2. `story_pass.py`
3. `lighting_pass.py`
4. `render_all.py`

Each saves `Steam_Art.blend` (the last one only renders). The v4 file is backed up as `variants/Steam_Art_v4_before_story_backup.blend`. The v4 renders are in `variants/renders_v4/`, the v4 scripts in `variants/*_v4_*backup.py`.

- **`fix_paths.py` (new):**
  - Texture paths that pointed at the other PC's checkout now point at this one, written relative (`//../../Assets/...`).
  - `story_pass.py` calls it too.
- **`knight_pass.py`:**
  - The pose is now `POSE = "fist_forward"`, from the user's notes on 2026-11-09:
    - "lower his arm"
    - "no fist bump, fist go to audience, sword point to his left"
    - "make the blade's surface face the camera"
  - His right arm reaches toward the Main camera (`FP_AIM_CAM`, `FP_AIM_MIX`), and the sword in his fist points across his body to HIS left (`FP_SWORD_DIR`).
  - The fist rolls about the blade so its flat side faces the camera (`FP_BLADE_FACE_CAM`, about 20 deg off at best).
  - The sword and shield now show (`SHOW_SWORD` / `SHOW_SHIELD`: None = shown only with this pose).
  - The old wave is still there: `POSE = "wave"`.
- **`story_pass.py` (new; everything it makes is `ST_...` in the collection `ST_Story`):**
  - **The Tree of Light:** the game's `Pumpkin_Tree.fbx`, the tree the rooftop grows at `Pumpkin_Tree_Pos` when the Lid breaks. Notes:
    - The old Tree-of-Light POT in Knight.blend is no longer in the game.
    - The tree's grow animation, last frame, is baked into a plain mesh.
    - It uses its game textures, the orange maple leaves, and a warm light in the colour of its Rooftop_Light.
    - It stands behind the knight (`TREE_AT`, `TREE_HEIGHT`).
  - **7 tree pumpkins** (`pumpkin_1.fbx`) hang under its branch tips.
  - **The Pumpkin Boss** (`Pumpkin_Boss_Z.blend`, only read):
    - It is posed with `pumpkin_boss_transform` at frame 100: both pumpkin fists raised.
    - It is moved so its Face bone is at `BOSS_FACE_AT`, over the back wall.
    - Its body is darkened (`BOSS_DIM`), the yellow of its eyes and mouth glows (`BOSS_GLOW`), and it gets a cool rim light.
  - **The menace pumpkins** turn 35 % toward the knight (`PUMP_TURN`), and the hero pumpkin moves to `HERO_AT`.
  - **Icon cameras:** `CAM_Icon_Shortcut_512x512` (PNG) and `CAM_Icon_App_184x184` (JPG), on his helmet.
  - **Library logo camera:** gets `trim` and `render_scale`.
- **`lighting_pass.py`** (framing tables), following Steam's rules (checked 2026-11-09):
  - **Library Hero:** aimed at the knight, so he and the tree trunk sit in Steam's 860x380 safe area in the middle. In Steamworks, put the library logo at **"centered top"**: left bottom would cover the big pumpkin.
  - **Small capsule:** the logo nearly fills it (Steam shows it down to 120x45).
  - **Page background:** no characters (`STORY_CHARS` hidden) and 1.3 stops darker (`EXPOSURE`), because Steam wants it ambient and not too bright.
- **`render_all.py`:** new camera properties:
  - `exposure`
  - `format` ("JPG")
  - `render_scale`
  - `trim`: the library logo's transparent margins are cut off and the logo is scaled to just fit 1280 x 720. Steam wants the PNG to BE the logo, 1280 wide and/or 720 tall.

**Steam uploads:**
- Header 920x430
- Small 462x174
- Main 1232x706
- Vertical 748x896
- Page Background 1438x810
- Library Capsule 600x900
- Library Header 920x430
- Library Hero 3840x1240 (no text)
- Library Logo (PNG, transparent, about 1214x720 after trimming)
- Shortcut Icon 512x512 PNG
- App Icon 184x184 JPG

Still to do: at least 5 gameplay **screenshots** (1920x1080 or bigger, 16:9, real gameplay only, so not these renders; at least 4 must be "all ages").

### v5 update (2026-11-09, later)
- **The user's own edits in Blender:** the Pumpkin Tree turned about 106 deg and raised 5 cm, and the Pumpkin Boss moved 1 m further back. They are now written into `story_pass.py` (`TREE_EXACT`, `BOSS_FACE_AT`), so re-running it keeps them.
  - The tree's pumpkins are re-hung on the turned branches.
  - The boss's two lights follow him.
  - The user's file is backed up as `variants/Steam_Art_user_edit_2026-11-09.blend`.
- **The rooftop as it is in the game now** (`story_pass.py`, `ROOF_FROM_UNITY = True`):
  - Unity's `Assets/Models/Walls/Rooftop_Base_Z.fbx` and the rebuilt Lid (`Rooftop_Base_Z_Lid.fbx`) replace this file's old rooftop. The old one stays in the file, hidden from renders.
  - Materials come from the Unity ones:
    - floor `rooftop_cursed_*`, with its glowing cracks (emission 2.2);
    - pit `rooftop_pit_cursed_*`;
    - walls `rooftop_wall_*`;
    - Lid `rooftop_lid_cursed_*` (emission 1.8).
  - The new rooftop takes the old rooftop rig's pose (`ROOF_POSE_FROM`), so the walls stand where they did and the front ones stay down.
  - Left out (`ROOF_SKIP`): the back gate (the boss stands in that gap) and the stepped pit door under the Lid.
  - `lighting_pass.py` keeps the moonlight off these new floor pieces, as it did for the old floor.
- The renders before this update are in `variants/renders_v5a/`.

### v5 update 2 (2026-11-09, evening): the user's hand pose + promo pictures
- **The user posed the knight by hand in Blender:**
  - his right arm, the fist closed round the grip (index fingers curled), the sword turned in the hand;
  - the big menace pumpkin moved next to him (`Menace_Pumpkin_Body` offset under its root).
- **`knight_pass.py` now defaults to `POSE = "keep"`:**
  - It leaves the pose, the sword and the shield alone, and only remakes the smile eyes and the hand light, which follows the glove.
  - Setting any other POSE REBUILDS the pose and loses the user's.
  - The user's file is backed up as `variants/Steam_Art_user_edit2_2026-11-09.blend`.
- **Note:** the user's Blender had the file open from before an update and saved over it. Running `story_pass.py` + `lighting_pass.py` again put the rooftop update back without touching the user's pose or pumpkin move. Always compare a dump against `.blend1` / the scripts before rerunning.
- **Promo pictures (not Steam sizes; for Reddit and other posts), rendered by `render_all.py` with the rest:**
  - `Promo_KeyArt_1920x1080` (16:9): closer in; the boss looms top-left, the tree top-right, the knight and pumpkin in the middle, the logo bottom-left on the floor.
  - `Promo_Portrait_1080x1350` (4:5, fills a phone in the Reddit app): the logo on top.
  - Their cameras and logos are made by `story_pass.py` (`PROMO`) and framed in `lighting_pass.py` (`P_T`, `P_DIR`, `LOGOS`, `HIDES`).
- The renders before this update are in `variants/renders_v5b/`.

### v5 update 3 (2026-11-09, night): spiders, glowing pumpkin, the user's pose kept for good
- **Spiders** (`story_pass.py`, `SPIDERS`): 4 game spiders (Spider_Enemy_NEW.blend, only read) stand just in front of the Pumpkin Boss, in the back gate's opening.
  - Their poses are the user's 4 picks: Spider_Attack f20, Spider_Alert f30, Spider_Attack_2 f25, Spider_Appear f30.
  - Each one faces the knight, with its feet on the rooftop floor (a ray straight down).
  - They have their own warm key light and cool rim (`SPIDER_KEY_W`, `SPIDER_RIM_W`). The user asked for them smaller, so they are about a third smaller now.
- **The big pumpkin beside the knight glows like a lantern** (`HERO_GLOW`): it has its own copies of the eye, mouth and socket materials, so the small pumpkins keep theirs, plus a small warm face light.
- **The user's knight pose is saved in `knight_pose_user.json`:** all bones, plus the sword and shield transforms, taken from `variants/Steam_Art_user_edit2_2026-11-09.blend`.
  - `story_pass.py` puts it back at the end of every run, because stepping through frames for the bakes moved his forearm once.
  - If the user re-poses the knight in Blender, run `save_user_pose.py` (in this folder) on the saved file, so the new pose is the one kept (or delete the json).

### v5 update 4 (2026-11-09, late): the big pumpkin has the user's ORIGINAL face, burning inside
- `story_pass.py` (`HERO_ORIGINAL_FACE`): the user's game pumpkin `pumpkin_0` (Knight_429.blend, only read) takes the menace pumpkin's place, at the same spot and turn. The menace body is hidden; the small pumpkins keep the menace faces.
- Its own carved pits (eyes, nose, jagged mouth) are found as `menace_pumpkin_build.py` finds them.
- **The fire:** they stay BLACK near the rim and in their upper part, and burn deep inside and low down, with flickering fire colours.
  - Vertex attributes `pit_depth` / `pit_v`; switches `PIT_FIRE_W`, `PIT_GLOW_DEPTH`, `PIT_GLOW_LOW`.
  - User: "still need a black part in that eye", "that's too much".
- **16 small embers** drift up above it (`EMBERS`).
- **Tried and turned off:**
  - a flame card like the logo's fire (`FLAME_CARD`: it hazed over the face);
  - cartoon flame tongues (`TONGUES` counts 0: "too much").
- **The eyes** (user: "a dark black eye then a burning fire as the pupil") are all black, with a round burning pupil in the middle (`PUPIL_SIZE`, `PUPIL_SOFT`, `PUPIL_DROP`, `PUPIL_W`; vertex attribute `pit_eye`). The mouth and nose keep the deep, low glow.
- **The icons:** the user picked the clean look.
  - The icon cameras have `clean_backdrop`. `render_all.py` renders only the knight as SEEN (the rest of the set still lights and reflects on him) and puts him on a dark radial backdrop with a soft green glow (`clean_backdrop()`), so he reads at Steam's 32 px list size.
  - The Page Background also hides `ST_Hero_` (the burning pumpkin).
- **The user went back to the OLD icon look:** the knight in front of the scene ("I like it better that way"). The Steam icons are `clean_backdrop = False` (`ICONS` in `story_pass.py`); the clean renderer is kept for the clean YouTube profile alternative.
- **YouTube** (user 2026-11-09), rendered by `render_all.py` with the rest:
  - `Promo_YouTube_Banner_2560x1440`: the logo, pumpkin and knight sit inside the 1235 x 338 middle that phones show. Desktop shows the 2560 x 423 strip with the spiders and walls; TVs show it all, with the boss, the Tree of Light and the sky. 24 mm, tilted 85.2.
  - `Promo_YouTube_Profile_800x800`: the scene look, framed for the circle crop.
  - `Promo_YouTube_Profile_Clean_800x800`: the dark-backdrop alternative.

### The icons are the user's own painted badge (2026-11-09)
- The user's original Steam icon is kept in `assets/icon/old_steam_icon_badge.webp`: the knight peeking over the gold-and-green "429" in a gold ring with green fire, on a transparent background. The user likes it better than any 3D icon.
- Run `python make_badge_icons.py` (system Python with Pillow) after `render_all.py`. It writes:
  - `renders/Icon_Shortcut_512x512.png` (see-through);
  - `renders/Icon_App_184x184.jpg` (on a dark green backdrop, since JPG has no transparency);
  - `renders/Promo_YouTube_Profile_800x800.png` (the whole badge, the ears too, inside YouTube's circle).
- The 3D icon and profile cameras have `skip` (`ICONS_3D_SKIP` in `story_pass.py`), so a full `render_all.py` never overwrites these files. Naming them with `only=` still renders them.

### The knight's improved texture (2026-11-09)
- The user picked the improved armour colour map for the renders. `story_pass.py` switches it in with `KNIGHT_TEXTURE = "v2"`; `"original"` puts the game texture back. The game texture in Assets is never changed.
- `assets/knight/Knight_Armour_Set_Diffuse_v2.png` is made by `assets/knight/improve_knight_texture.py` (system Python). It keeps the user's design, colours and UVs, and adds:
  - soft crease shading (baked AO, `knight_bake_ao.py`);
  - edge highlights from the normal map;
  - a warmer gold;
  - a gentle lit-from-above shading (`knight_bake_normals.py` + `knight_dump_uv_mask.py`; not on the sword and shield).
- **The shamrocks on the helmet and chest were stretched by the UVs.** `paint_clovers.py` takes the user's own shamrock from the shield, erases the old ones, and paints it straight onto the surface:
  - It uses `knight_surface_2048.npz` from `knight_bake_surface.py`: per texel, the 3D position, the normal and which part it belongs to.
  - It matches the tone of the clover that was painted at each spot.
  - The clovers are drawn at 70 % ("scale it down to 70 %"); the sizes are in `CLOVERS`.
  - Drawn clovers I tried first (a four-leaf, a lime one) were rejected: the knight material's lime ramp turned bright greens pale mint. Use the user's own art.
- To rebuild after changing anything:
  1. `python assets/knight/improve_knight_texture.py`
  2. `story_pass.py`
  3. `lighting_pass.py`
  4. `render_all.py`

### The icon as a 3D scene (2026-11-09)
- `assets/icon/icon_scene_build.py` (run on `Steam_Art.blend`) saves a COPY, `assets/icon/Icon_Scene.blend`; `Steam_Art.blend` is not changed. It holds the user's badge rebuilt in 3D:
  - the knight (improved texture, his pose; his fists rest on the digits via IK, enlarged 1.35x like the badge's paws);
  - the real 3D "429";
  - a gold ring;
  - a dark green disc with painted green fire (`paint_disc_fire.py` -> `disc_fire.png`).
- Lights: key, rim x2, green under-glow, a warm light on the fists, and a warm light linked to the ring only.
- `assets/icon/icon_render.py` renders it transparent, 1024 px, 8-bit, with the Steam grade but without the vignette (the vignette veiled the transparent picture) -> `icon_render_1024.png`.
- Gotchas:
  - The Steam scene's gold reflector cards made everything milky in a small, bright icon, so they are off (`CARD_W 0`).
  - Pillow cannot read Blender's 16-bit RGBA PNGs, so icon_render saves 8-bit.
- `python make_badge_icons.py src=3d` makes the icon files from it into `assets/icon/3d/`. Plain `python make_badge_icons.py` (the user's painted badge) writes `renders/`, which is still the uploaded icon.
- **v2 of the 3D icon** (user feedback: "not even close"):
  - **Layout (badge units, R = ring radius):** follows the painted badge. The helmet's ears touch the ring; the 429 is 1.6 R wide with its top at C + 0.27 R, just under the visor, so it sits inside the circle.
  - **The hug:** the knight's arms spread over the tops of the 4 and the 9 ("hug 429 tight together"). The fists use IK with stretch; the hands don't inherit the stretch (`inherit_scale NONE`, `HAND_SCALE 1.85`); the right thumb is tucked; the bracers are hidden.
  - **Hidden body:** a second, feathered piece of the disc just behind the digits hides his body through their gaps (`PLATE_BELOW`).
  - **Eyes:** the normal round glowing eyes (the smile eye materials are swapped back).
  - **Ring:** slim and satin (`RING_W`, `RING_ROUGH`).
  - **Digits:** the green enamel is matte and darker (`ENAMEL_DIM`), so the gold outlines read.
  - **Fire:** flames along the bottom of the disc too.
  - **Camera:** straight on.
- **v3 of the 3D icon: the colour** (user: "the color is so off"). It was measured against the painted badge, judged by independent reviewer agents over 3 rounds, and fixed at the source:
  - **The scene** (`icon_scene_build.py`):
    - Standard view, without the Steam scene's warm teal/orange grade.
    - A near-black forest-green disc with a vignette, and lime fire wisps beside the head and along the bottom (`paint_disc_fire.py`), calm behind the 429.
    - Matte dark-emerald digits, with the crack glow x0.25 and the inner groove x0.3, both lime.
    - A satin ring with a soft light.
    - Chestnut paws (`BROWN_TINT`); a dark chocolate, matte face (`FACE_*`: masked by distance from his face, so the fists keep their colour).
    - A warmer satin helmet.
    - Warm rim lights (no cool blue).
  - **The grade** (`grade_icon.py`, run by `icon_render.py` on the raw render; it writes `icon_render_1024.png` from `icon_render_1024_raw.png`):
    - Hue bands mapped onto the badge's colours:
      - greens ramp from emerald shadows (124) to lime highlights (80);
      - golds ramp from amber (26) to warm gold (34), with a soft highlight roll-off;
      - browns go to chestnut (19).
    - Shadows are tinted green-black, with a toe.
    - Eye and flame cores are saturated lime-yellow.
    - Pale gold hairlines are warmed.
  - **Measured:**
    - green hue 118 (badge 117);
    - gold hue 37 (badge 34);
    - face about (38,23,6) (badge about (68,23,12)).
- **v4 of the 3D icon: the polish, and the right arm** (reviewers' notes after v3, plus the user: "the right arm of the knight is being twisted"):
  - **The right arm:** in the Steam art his right hand is posed round the sword hilt, with the fingers nearly straight. With the sword hidden, and the hand aimed on its own, it read as a flat, twisted mitten. `icon_scene_build.py` now makes his right hand an exact MIRROR of his left fist:
    - the fingers take the left hand's local rotations, flipped (w, x, -y, -z);
    - the wrist takes a Copy Rotation from the left hand in pose space, with Y and Z inverted (the armature's X is left-right);
    - the log prints both knuckle directions to check.
    - This changes only the icon copy; the user's pose in `Steam_Art.blend` is untouched.
  - **Helmet:**
    - The tint mask now covers every non-gold texel (`HELMET_MASK_LO/W`); it used to catch only about 40%.
    - The clovers are kept out of it with `assets/knight/Knight_Armour_Set_CloverMask.png`, the 70% clovers. `clover_mask.png` is the OLD 100% one and left a ghost clover.
    - The helmet's sheen is green and lower (`HELMET_SPEC_TINT`, `HELMET_SPEC`): a white sheen read as a grey haze on dark olive.
  - **Helmet mask pass:** `icon_render.py` renders a second pass, `*_helmet.png`, with every other object a holdout. The grade uses it to give the helmet its own olive hue (`helmet_hue` 96, `helmet_val` 1.3) while the digits stay emerald. The two have the same brightness, so only a mask can tell them apart.
  - **Face:** one smooth flat colour (`FACE_COLOUR`), so the texture grain no longer shows. The mask takes every near-neutral texel; the normal map is off on the face.
  - **Eyes:** `grade_icon.py` finds the two round eye glows (scipy labels, top half; it prints "eyes found") and paints them from the middle out (`EYE_STOPS`), anti-aliased, plus a soft lime halo on the face:
    - a small pale-yellow core;
    - a saturated lime body;
    - a deeper lime rim.
  - **Ring:** darkens toward the bottom, down to amber-brown (`RING_FALL`, `RING_HUE_DROP`), like the badge.
  - **Fire:** the tongues are max-combined, not summed, so they no longer fuse into a neon blob. They are wider, with dark gaps; the right ones sit further out past his helmet.
  - **Lights:** the fist light is 5.5, and the paws are darker (`BROWN_TINT`).
  - **Measured (badge in brackets):**
    - helmet #1E4901 (#214E05);
    - face #45180A (#44170C);
    - eyes #AFF409 (#BFF708);
    - ring bottom #97430B (#974008);
    - digits #063D07 (#055717 / #043B16);
    - paws #5E200E (#4D1D05).
    - Region stats come from `_claude/steam_art/color/measure.py`.
- **v5 (reviewers on v4):**
  - **His right fist** now sits on top of the 4's upright stroke (`FIST_RIGHT_RANGE`; `spot_top` finds the 4's highest flat top), like the badge's paw. On the 4's sloping side it looked like it was sliding off.
  - **The field:** a second mask pass, `*_field.png` (the disc and its front plate), lets the grade keep the field the badge's saturated emerald, a bit brighter. The badge's field measures hue 117-119, sat 0.98, v 0.26-0.30 (#034304..#064C02). Settings: `field_hue` 117, `field_val` 1.3; its flames are green, hue about 100, and only the hot cores are lime.
    - The warm grey bounce from the gold ring at the disc's edge is turned emerald too; it read khaki.
    - A reviewer's "warm yellow-green #233E02" for the field was WRONG (measured), and it made the edges khaki. Measure before acting on a reviewer's hex.
  - **The fire:** soft mid-green swirl strands round the disc, as in the badge (`paint_disc_fire.py`).
  - **Browns:** hue 17 (13 was too red, and it turned the gold trims' shadows copper); brown only below v 0.45.
- **v6: the USER hand-edited `assets/icon/Icon_Scene.blend` (2026-11-09 16:27).**
  - **Their edits:**
    - removed the knight's arms (73 vertices), so the paws peek over the digits like the badge's;
    - added a Subdivision modifier (smooth paws);
    - re-posed the thumbs, his right fingers and the forearms;
    - turned and tilted the digits in the logo mesh, like the badge. The 429 now breaks past the ring at the sides, as the badge does; this is intended;
    - moved the logo down 1 cm;
    - purged unused materials.
  - **From now on: NEVER re-run `icon_scene_build.py`.** It rebuilds from `Steam_Art.blend` and would wipe these edits. Only re-render with `icon_render.py`, or patch the saved file with small scripts that change one thing and save.
  - **Backups:** in `_claude/backups/icon/`, my last build before their edit and their edit as saved.
  - **My follow-ups on their file:**
    - the chest piece is hidden in renders (a stud of it peeked out behind the 2);
    - the helmet's inside faces are dark (Backfacing);
    - the "ic_arm" shadow attribute now covers the torso too.
  - **The leaf:** a subtle weed leaf in the KNIGHT'S LEFT eye (= the screen-right one), painted by `grade_icon.py`.
    - `leaf_eye=none` turns it off; `leaf_alpha` sets its strength.
- **v7-v8 (reviewers on the user's version), all without touching their pose, mesh or digit edits:**
  - **Changes to their saved file** (scripts in `_claude/steam_art/color/user_file_fixes2.py` and `3.py`; backups in `_claude/backups/icon/`):
    - the lime groove line inside each gold digit outline is now a dark keyline (`IC_Keyline`), like the badge's black line;
    - the helmet's inside is dark forest green, so there's no black hole under the chin;
    - the rim lights are x0.45 and warmer, with no chalky white edges;
    - the paw lights are brighter (the left one x1.5, the right one x1.3);
    - the face colour is x0.65.
  - **Grade** (`gold_light_hue` is now 31 in `GRADE_DEFAULT`):
    - **gold:** an orange-gold body with a pale-yellow lip only at the very top of the highlights (`GOLD_LIP_HUE`); gold in shadow is burnt orange (`GOLD_SHADOW_HUE`), never khaki;
    - **ring:** a near-black gutter just inside it, measured in the bottom sector because the digits break past the ring at the sides (`GUTTER_PX`);
    - **fire:** only its truly white-hot cores go pale yellow;
    - **eyes:** a bigger white-hot core, plus a glint on top of both eyes, so the leaf eye keeps its highlight; the leaf is smaller (0.55 of the eye);
    - **digit faces:** more emerald (`green_dark_hue` 131).
  - **Fire:** the bottom flames are low and calm, the wisps beside the head are taller, and the swirls are on the sides only (at the bottom they read as a zigzag).
  - **Measured v8 (badge in brackets):**
    - face #3F1609 (#41150A);
    - eyes #BFF614 (#BEFA0B);
    - clover #72EA14 (#75EB06);
    - helmet #256102 (#2A6504 / #214E05);
    - ring bottom #89390B (#974008).
