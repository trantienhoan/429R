# Storage room lair: build list (2026-11-25)

The user's pick: `concept_v1.png` (key art), `concept_v1_corners.png` (4 story corners), `concept_v1_props.png`
(prop sheet). User: "the storage room looking good, let's follow this concept. We already have most of the props,
still need more web stuffs, the room texture might need a bit improve, even the picture frame of the pumpkin
portrait."

Our current props, rendered: `K:/WORK_2026/08_04/_claude/lair_2026-11-25/our_props_sheet.png` (K: PC only).

## What we have, and what each concept prop needs

| Concept prop | What we have | To do |
|---|---|---|
| Horseshoe door, wardrobe, shelves, table + chairs, witch-hat table, boxes, small pumpkins, candles, skull | Already in the storage room | Keep. Claw scratches on the door = a decal. |
| Clover banner, torn and nibbled | `Banner_Hang` (clover) | A torn copy: nibbled edges cut into its texture (alpha cutout). |
| Candle with a green flame | `Candle_Land`, `Candle_Fat` | A green-flame copy (flame particles and glow tinted green). |
| Skull with a cobweb | `Skull_2`, `Skull_And_Candle` | Add a small web piece from the new web set. |
| Cobwebs: corner, round, between posts, hanging strands | `Spider_Web` (a flat floor web with 3 balls), `House_Spider_Web` (one thin strand) | **NEW web set** (the user's first ask). |
| Orange egg sacs, glowing green inside (single, hanging, cluster) | Grey web balls (`Web_Ball_On_*`, `Spider_Web_Ball`) | **NEW**: orange teardrop sacs with a green inner glow. |
| Glass jars of green light (3 sizes) + a cracked empty one | Potion bottles with skull labels (`Empty_Bottle_1-5`) | **NEW** mason jars (cork lid, glowing green inside). |
| Little pumpkins drinking from a jar | Small pumpkins | Placement: 3-4 small pumpkins round a jar. |
| The portrait: the evil pumpkin holding the fairy jar, ornate gilded frame | `Picture_Hang_4` (a plain flat 28-triangle frame, photo-like angry pumpkin) | **NEW** carved gilded frame + **NEW** painting (4 growth stages for the 4 rooms). |
| The nest: straw, candy wrappers, pumpkin seeds, green glow | None | **NEW** nest + pumpkin seeds. |
| Chewed box spilling candy | Paper boxes, candies | A static chewed-open box with candies (or candies placed by a broken box). |
| Specimen board: 4 spiders turning into a pumpkin spider | None | **NEW** frame + card painted from renders of our own spiders (old black look -> pumpkin look). |
| Bug holes in the floor and wainscoting, claw scratches, a floor crack | None | **NEW** decals (flat quads). |
| Curly pumpkin vine climbing from a crack | `SlimeBowl_Kit_Vine` (a stalk with leaves), `Bowling_Vines` (a flat vine patch) | **NEW** climbing wall vine pieces in the same green; the crack sprout can reuse the Slime Bowling stalk. |

## The room's look

- **Wainscoting**: the concept has dark wood panels on the lower metre with the damask above. The kit walls map the
  wallpaper over their full height (floor at v 0.444, 3 m at v 0.966), so it can be painted into
  `M_Textures/Combos/Wall_Stylize_Set_1/Bedroom_Wallpaper_Spooky.png`: panels from v 0 to about 0.29 (0.9 m) with a
  chair rail on top. That texture is the whole Bedroom kit's (the storage room AND the bedroom); the bedroom's own
  concept (`../Bedroom_Concept/concept_v1.png`) also asked for damask above dark wainscoting.
- **The old grime band**: the dark mouldy band in that texture was painted for the old bedroom mesh. CORRECTION
  (measured per wallpaper submesh): the kit walls also have the floor at v 0.444 (v 0.174 a metre, 6 m of wall per
  texture width), so the band sat at FLOOR level, not at 1.4 m as first written here. The repaint put the wainscoting
  over it and the grime just above the chair rail.
- **Damask**: the concept's is a little lighter and clearer; keep the drips and patches sparse.
- **Floor**: our planks already look like the concept's; bug holes go on as decals.

## Building

- Props under 2,000 triangles, bevelled chunky shapes, flat colours + soft gradients (`../Rooftop_Concept/TEXTURE_STYLE.md`).
- Webs: alpha-cutout quads and bent planes with one painted web texture (cheapest on Quest); egg sacs, jars, nest as
  small meshes. Glows by emission + bloom (and the seed's green particles), no real lights.
- Nothing in the open middle. Wall props are parented to the kit wall pieces, so they sink with the walls when the
  fight starts.
- Placement by a re-runnable menu on the storage room's set in `Assets/Prefabs/Bases/Bedroom_Kit_Base.prefab`.
- The same props grow stronger in the next rooms (the escalation table in `PROMPTS.md`).

## Suggested order

1. Web set + egg sacs. **DONE 2026-11-25** (user: "remodel them instead of creating new"): remodelled from our own
   `Spider_Web` and `Web_Ball` (in `Assets/Blender/Bedroom_Walls.blend`, which stays untouched) into
   `Exports/Lair/Lair_Props.blend` by `Exports/Lair/make_lair_webs.py` (source meshes copied in `Exports/Lair/lair_src.blend`):
   round web (187 tris), corner web (its lower half, cut at the hub, 111), sheet between posts (stretched, sagging, 187),
   hanging strand (one spoke, 1 m, 19), egg sac (the ball as a plump teardrop, smooth, 192), cluster (3 sacs on a small
   web). In the user's style (user: "I want your new props follow my style and texture"): the webs wear Spider_Web's own
   material (`Bedroom_Set_1_Mat`, the furniture atlas) on its own UVs, with a back face on every strand (that material
   draws one side; round 374 tris, corner 222, sheet 374, strand 38, cluster 950); the sacs keep Web_Ball's UVs and wear
   `Materials/Lair/Lair_Egg_Sac_Mat`: `M_Textures/Lair/Lair_Egg_Sac.png` = the web ball's own painted patch from the atlas
   recoloured orange (its strokes become the veins), `_Glow.png` = green through its lighter strokes (the patch is
   shared with other props, so the recolour lives in its own small texture; the UVs are only rescaled onto it). FBX in
   `Assets/Models/Lair/`; prefabs in `Assets/Prefabs/Lair/` (+ `Lair_Web_Strand_Sac`, a sac on a 0.6 m thread) by
   Tools > 429 Game > Lair > Set Up Lair Webs. Only looks (no colliders, no shadows).
   **PLACED 2026-11-25** (user: "save it, I see nothing"): Tools > 429 Game > Lair > Place Storage Room Webs puts 13 pieces
   into the storage room = `Bedroom_Kit_Base.prefab` set `Mini` (re-runnable, replaces its earlier `Lair_` children): 4 corner
   webs + a sagging web across the SW corner on the BR_Kit_Corner posts, 4 sacs on threads on the Roof (they fly off with it),
   sac clusters high on the east and west walls, round webs on the north wall (right) and the west wall, as children of
   the wall pieces (Room Kit shows, folds and hides them with their part). Play-tested: 17 renderers on in Mini Stuffs, 0
   left 4 s into Mini Enemy; pictures `K:/WORK_2026/08_04/_claude/lair_2026-11-25/ingame/`.
   Storage room facts for placing: inner walls at x/z +-3.70 (7.4 x 7.4 m), ceiling about 2.97 m, door north at
   x -2.25, window south at x +2.25 (the kit's `Mini` set).
2. Wallpaper with wainscoting. **DONE 2026-11-25** (`Exports/Lair/paint_kit_wallpaper.py`).
3. Portrait: gilded frame + painting. **DONE 2026-11-25** (Set Up Lair Portrait; one painting so far, the growth
   stages for rooms 2-4 are still to make).
4. Green jars (+ the drinking pumpkins). **DONE 2026-11-25** (Lair_Glow_Jar, Lair_Sipping_Pumpkin, Lair_Jar_Huddle).
5. Nest + seeds, decals (holes, scratches), climbing vine, specimen board, torn banner, green candle. **DONE 2026-11-25**
   (Set Up Lair Story Props; the floor crack was left out). Chewed box: part of the nest.
6. Place everything in the storage room; Play-test. **DONE 2026-11-25** (Place Storage Room Webs for the kit pieces,
   Place Storage Room Story Props for the spawn points; pictures `K:/WORK_2026/08_04/_claude/lair_2026-11-25/ingame_props*/`;
   2 Game Scene saved 13:53). Details: `Assets/NOTES_Next_Time.md` RESUME HERE.

## The portrait painting (Gemini, optional: a clean high-resolution painting to frame)

```text
A painted portrait for a picture frame in my VR game, no frame, no text: a small, sly evil jack-o'-lantern pumpkin
with angular glowing yellow eyes and a zig-zag grin, olive-green stem, wearing a dark little coat, holding a glass
jar with a cork lid in which a tiny green fairy glows weakly. Dark muted background with a soft green glow from the
jar. Same style as the attached images: chunky stylised game art, flat colours with soft painted gradients, bright
matte pumpkin orange. Portrait format, 4:5.
```
Then, in the same chat: "the same portrait, the pumpkin bigger and fatter, the fairy's light weaker" (room 2), "huge,
filling the frame, the fairy almost out" (room 3), "bursting out of the painting" (room 4).

## 2026-11-25 later: the user's fix + the room plan

- The user fixed the web pieces by hand in `Exports/Lair/Lair_Props.blend` (saved 11:45): single-sided again, and EVERY piece
  (egg sacs and cluster too) mapped onto Spider_Web's grey patch of the furniture atlas (u 0.624-0.763, v 0.453-0.604):
  grey wrapped sacs, no orange. Exported as-is by `Exports/Lair/export_lair_props.py` (never saves the .blend); all prefabs
  wear `Bedroom_Set_1_Mat`; my orange sac material and textures were removed. `make_lair_webs.py` now refuses to overwrite
  Lair_Props.blend (needs --force).
- Room plan: `storage_plan_now_vs_concept.png` (NOW from the --MiniBedroom-- markers vs PLAN): 1 move the rug + weapon crate
  (markers Bedroom_Matt / Wood_Box_Weapon_Main at -0.62, 0.44, on top of the rune circle) to the west side (-2.5, -1.7);
  2 portrait on the east wall (the picture-puzzle picture hangs at about z -1.9); 3 specimen board above the north east
  shelf; 4 jars on the east shelves; 5 nest + chewed box in the SE corner by the box tower; 6 small pumpkins round a jar
  (NE quadrant); 7 torn banner + green candle on the candle table; 8 vine up the west wall; 9 claw scratches on the door;
  + wainscot wallpaper. Kept: hat table (also used by the bedroom: its marker root is shared), shelves, cabinets, boxes.

## 2026-11-25 afternoon: the wardrobes

User: "check texture of the shelf ... any idea to improve the texture?" -> "Aged wood + lair remodel". The two storage-room
wardrobes (MiniBedroom_Shelf_01) are now Lair_Wardrobe (aged dark wood copy + iron hinges and corner straps) and
Lair_Wardrobe_Ajar (one door open on a dark inside with green eyes, claw scratches, a sac on a thread). Details in
`Assets/NOTES_Next_Time.md` RESUME HERE. The shared Wood_Furniture_Mat (92 prefabs) is unchanged; its roughness map
sitting in the Metallic slot is a known slip with no visible effect while reflections stay at 0.023.
