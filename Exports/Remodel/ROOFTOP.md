# Rooftop remodel: list + concept (2026-11-16)

Concept: `Exports/Rooftop_Concept/concept_v1.png` (+ `concept_v1_zoom_start.png`, `concept_v1_zoom_centre.png`).
How to get the look: `Exports/Rooftop_Concept/TEXTURE_STYLE.md`.

**The look in one line:** a toy diorama at night. Chunky shapes with bevelled edges, flat colours with soft gradients
(darker low, lighter high), no cracks or photo grain, soft dark contact shadows (baked AO), warm orange light pools
against cool blue-violet moonlight.

**Palette:** stone = cool plum-grey; wood = warm brown; iron = dark rusty; pumpkins = saturated orange; magic = purple
+ gold runes; sky = deep indigo-violet with a big orange moon.

**Rules (Quest):** under ~2,000 triangles a prop, one shared atlas per family, bevel 1-2 segments, low smoothness
(0.1-0.25). Breakables: built from loose parts or 5-8 chunky Cell Fracture pieces (see the chat on broken prefabs).

What has to stay (gameplay): the flat cross shape (teleport), the round stage in the middle (the tree grows there, the
boss bursts out of it), 4 monster gates, the long start arm (tutorial), room for the Tip Knight, the Fairy, the 3 gate
dice, whack barrels and candy baskets, no walls in the middle.

---

## A. The rooftop itself (do first: it sets the look)

| | Today (prefab: model) | Concept |
|---|---|---|
| [ ] | **Floor, cross shape** (Rooftop_Base_Z: Rooftop_Base_Z.fbx) | Large soft plum-grey flagstones, rounded edges, soft dark gaps, slightly lighter centres. No cracks. |
| [ ] | **Battlements / walls** (same model; 12 wall colliders) | Chunky crenellated stone blocks, every block bevelled with a light top edge, a little colour change block to block. |
| [ ] | **Round stage in the middle** (Rooftop_Base_Z_Lid.fbx + Lid Running Light Ring; broken: Rooftop_Base_Z_Lid_Drop.fbx) | Two-step round stone dais, gold rune circle + pentagram carved and glowing on top. Breaks into a few big chunky stone slabs when the boss bursts out. |
| [ ] | **4 monster gates**: arch (Round_Gate), bars (Gate), stone pumpkin on top (Stone_Pumpkin_2.fbx); broken: Rooftop_Base_Z_Round_Gate_Drop, Stone_Pumpkin_Drop, Rooftop_Base_drop | Round stone arch, dark iron bars with glowing red runes, a carved stone jack-o'-lantern on the keystone, purple mist behind. |
| [ ] | **Pebbles** (Rooftop_Base_Z_Pebbles + 19 single pebbles) | Fewer, chunkier stones; or drop them (the concept's floor is clean). |
| [ ] | **Glowing path** (NEW, not in the game) | Glowing cream stepping-stone tiles down the start arm to the stage. |
| [ ] | **Rune circles under the barrels** (NEW) | Flat gold glowing rings on the floor where the 4 barrels stand. |

## B. The tree (the centrepiece)

| | Today | Concept |
|---|---|---|
| [ ] | **Pumpkin_Tree** (Pumpkin_Tree.fbx, Stone_Pumpkin.fbx; broken: Pumpkin_Tree_Drop.fbx) | Thick gnarled trunk with a big glowing carved jack-o'-lantern face, roots spreading over the dais, a chunky crown of golden-orange maple leaves (clumps, not single leaves) with small glowing pumpkins hanging in it. |
| [ ] | **pumpkin_growing** (the sapling) | The same tree, small: a short trunk with a few golden leaves and a tiny face. |
| [ ] | **Pumpkin_On_Tree** (pumpkin_1.fbx; broken: pumpkin_2_Drop.fbx) | The glowing fruit pumpkins from the crown: round, orange, small carved face, a stub stem with a leaf. Also used everywhere else (pumpkin rooms). |

## C. The start / tutorial arm

| | Today | Concept |
|---|---|---|
| [ ] | **StartGamePumpkin** (pumpkin_0.fbx, 429.fbx, Armour_Stand_Sign1.fbx) | The big start pumpkin; the sign as a chunky wooden signpost (concept: two signposts with arrows). |
| [ ] | **Pumpkin_00** (pumpkin_0.fbx, eye_wear.fbx; broken: pumpkin_0_drop) and **Pumpkin_0** (pumpkin_evil.fbx; broken: pumpkin_evil_0_drop) | Round chunky pumpkins, readable carved faces, same orange as the tree fruit. Pumpkin_0's model is shared by ALL runner pumpkins (each adds a hat). |
| [ ] | **Stone_Cube_Dice 1-3** (Stone_Cube2.fbx; broken: Stone_Cube2_Drop) | Carved stone dice on short pedestals, each face glowing a different colour (green, red, blue in the concept). |
| [ ] | **Door_1-4_Set** (Door_1_Frame.fbx: the 4 path circles at the start) | Match the gate dice / rune style. |
| [ ] | **Candle_Fat** | Fat wax candle; the concept uses lanterns too. |
| [ ] | **Lamp posts** (Rooftop_Pole_Light, used again in round 2; broken: Rooftop_Pole_Light_Drop) | Crooked wooden post with an arm and a hanging iron candle lantern, warm glow. |
| [ ] | **Crates, barrels, pumpkin piles at the start corner** (NEW decor, concept_v1_zoom_start) | Wooden crates with X braces, banded barrels, a pumpkin patch with leaves. Can reuse the bedroom's Wood_Box and the barrels. |
| [ ] | **The_Seed_Start** (Magical_Seed.fbx; also The_Seed) | Glowing magic seed. |

## D. Rooftop again: whack pumpkins, boss, ending

| | Today | Concept |
|---|---|---|
| [ ] | **Wood_Barrel_1-8** (Wood_Barrel_Jack.fbx; broken: Wood_Barrel_Drop) | Squat banded wooden barrels on gold rune circles. Built from staves + hoops (loose parts) so they break into those. |
| [ ] | **Livingroom_pumpkin_music_player_JACK_** (pumpkin_music_player.fbx; shared with the bedroom one) | Carved pumpkin music box. |
| [ ] | **Skull_And_Candle** (Skull_and_Candle.fbx; broken: Skull_drop) | Chunky cartoon skull with a melted candle on top. |
| [ ] | **Candy baskets** (Bedroom_Candy_Basket_P: Candy_Basket.fbx) | Wicker baskets full of sweets (concept shows them round the stage). |
| [ ] | **Treasure chests** (Treasure_Chest_and_Anims.fbx, rigged; all chest prefabs share it; broken: Treasure_Chest_Drop) | Wooden chest with iron bands; keep the opening animation. |
| [ ] | **The_Boss_Seed** (The_Boss_Seed.fbx) | The final glowing seed. |

## E. Backdrop (NEW, far away: very low poly, no collisions)

| | Concept |
|---|---|
| [ ] | The haunted house's steep slate roof slopes around the rooftop, with attic windows glowing orange |
| [ ] | Crooked chimneys with purple smoke |
| [ ] | Stone gargoyles on the corners, a witch weather vane |
| [ ] | A tiny sleeping village far below (boxes with orange windows) |
| [ ] | **Skydome** (Skydome.fbx): indigo-violet night, big orange harvest moon, orange glow on the horizon. Note: the rooftop must lift the house fog while it runs (the backdrop is past 22 m). |

## F. Characters and enemies seen on the rooftop (rigged: a separate, later track)

Fairy (Fairy_Girl.fbx), Knight / Tip Knight (Knight.fbx), the pumpkin boss (Pumpkin_Boss_Z.fbx), Bug_Pumpkin, the bees
(Bee_Smol_&_Anims.fbx), the whack pumpkins (Pumpkin_Mob_Pumpkin_6/7). The old `Pumpkin_Boss.fbx` is still referenced by
Rooftop_Base_Z: check it is still used before remodelling it.

## Suggested order
1. Floor + battlements (A) with the new materials: everything else is judged against them.
2. The round stage + the tree (A, B): the centrepiece.
3. Gates (A), then the start arm props (C).
4. Round-2 props (D).
5. Backdrop + sky (E), with the fog lifted on the rooftop.
