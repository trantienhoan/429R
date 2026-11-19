# Bedroom kit: plan (2026-11-19)

**Goal (user):** build both bedroom stages from a modular kit with the textures we use now, swap it into the game, and remove the old bedroom. The purple hole (built the same day, instead of the Fairy Shield) becomes how a room leaves.

## 1. What the two bedroom stages use today

- **One rigged room for both stages:** `Prefabs/Bases/Bedroom_Base_Z` (from `Models/Walls/Bedroom_Base_Z.fbx`, source `Assets/Blender/Bedroom_Walls.blend`). One skinned mesh, 605 faces, 12 bones:
  - Floor.
  - Wall01–04: the big bedroom, stretched 1.4×.
  - Wall05–08: the mini bedroom.
  - Rooftop (the roof and the room light), Small_Room_Door, outline (the second door).
- **Sizes** at the end of the arrival clip, measured in the .blend:

  | | Inside | Walls | Roof |
  |---|---|---|---|
  | Mini bedroom | 8.1 × 7.7 m | 3.58 m | slab at 3.44 m |
  | Big bedroom | 7.3 × 7.4 m | 3.58 m | slab at 3.46 m |
  | Floor (both) | 9.1 × 9.1 m, 0.36 m thick | | |

  The big room is not bigger: the two are nearly the same size.
- **Animation:** Bedroom_Mini_COME / GO and Bedroom_COME / GO. The walls unfold; at a fight's start the walls and roof shrink to nothing and only the floor stays.
- **GAMESTAGES drives it:**

  | State | What happens |
  |---|---|
  | 2 - The BR | Bedroom_Base_Pos spawns the room into CurrentlySpawnedStuffs |
  | BRM_COME | Mini room comes |
  | Mini Enemy | Mini GO, light off |
  | Clean Mini | The hole |
  | 2.5 - BR | Big room comes |
  | Pumpkin_5 | Light on |
  | Enemy1 - Wait | Big GO, light off |
  | Clean Enemy 1 | The hole |
  | 3 - LivingRoom | Room destroyed |

  The light is found by its path: `Bedroom_Base_Z_Armature/Wall02/Rooftop/Point Light`.
- **On the room:**
  - Two breakable doors (EnemyHitDamage, break pieces, Door_Break_Drop_Pos), used by the Door Shortcut.
  - DropWallHungProps: hung shelves and pictures drop when the walls go.
  - Four Fall Edge triggers (the floor ends), a Teleport Area, and BoxColliders on the walls.
- **Textures:**
  - Wallpaper: `Bedroom_Wallpaper_Spooky.png`, plus the wall_not_clean normal and roughness maps. Your 10-22 UVs put the dirty band at the floor (v 0.44) and the drips at the top (v 0.99).
  - Floor and wood trim: `wood_floor3`.
  - Curtains and bunting: the Bedroom_Furniture_Set1 atlas and `Bedroom_Bunting_Halloween`.
- **Kept apart from the room, and placed for today's sizes:**
  - The furniture sets (--MiniBedroom--, --Bedroom--, the Chaos and Pumpkins versions).
  - The spawn points of EnemyManager 0 and 1.
  - The pumpkin rooms.
  - The flat NavMesh.
- **Watch out:** the living room is the same model scaled 1.5×. Keep the FBX and prefab until the living room gets its own kit.

## 2. The kit (made today)

`Exports/Remodel/Bedroom_Kit.blend`, made by `tools/bedroom_kit.py`:

- **Grid and sizes:** the 11-16 kit rules. 3 m grid with 1.5 m halves, **3 m high** (today's walls are 3.58 m, so decide in step 3).
- **Pivot:** at the inner face's bottom-left. The piece runs along +X with its outside at +Y, so pieces snap.
- **Modelled the way you modelled the old walls** (you asked for "a clean mesh, not a messy kit", 2026-11-19):
  - each wall is **one closed slab**, 0.345 m thick like yours, all wallpaper;
  - openings use the fewest quads: the faces round the opening run straight to the slab's corners (14 quads for a doorway, 16 for a window hole, as in your Wall01 and Wall02);
  - the trim is separate closed boxes: baseboards 12 cm × 5 cm, a hair below the floor, split at a door;
  - corner posts sit in the inside corners;
  - no bevels, no crown, all quads, no open edges.
- **Pieces:**

  | Piece | What it is | Quads |
  |---|---|---|
  | Wall 3 m, Wall 1.5 m | slab + baseboard | 12 each |
  | Door (open passage) | slab with a 1.11 × 2.09 m doorway, two baseboards, and **your door frame** from Wall01, as it is (UVs and all) | 40 (the same as your Wall01) |
  | Door Wide | slab with a 1.41 × 2.09 m doorway, two baseboards, and **your Wall05 frame** (the small room's secret-door frame), as it is | 40 |
  | Door Secret (leaf) | **your shortcut door** `Bedroom_Base_Z_Door` (horseshoe sign, boards, chain, padlock), whole and unchanged. A separate object, because in the game it takes hits, wobbles and breaks. It shares Door Wide's pivot, so placed with it, it stands in the doorway: boards facing the room, edges tucked 5 cm behind the jambs, 1.5 cm under the head. | 954 faces |
  | Door Locked (leaf) | **your locked door** `Bedroom_Base_Z_Door_2` (W/C sign), the same way | 932 faces |
  | Window | slab with the old 0.725 × 0.943 m window hole at 1.04 m, a baseboard, and **your window** from Wall02, as it is. The sill reaches 1.3 cm past the back face, as it did in the old wall. | 108 |
  | Corner post | 0.14 × 0.14 m, in the inside corner | 6 |
  | Floor | 3 × 3 m tile | 6 |
  | Beam | 3 m | 6 |

  The kit script takes your door frames and window from `Assets/Blender/Bedroom_Walls.blend` by face list, and the two door leaves by object name. If any of them change, the script notes it and leaves that part out.
- **The doors in the old room:**
  - The small bedroom's shortcut door is `Bedroom_Base_Z_Door`. "Set Up Bedroom Shortcut Door" gives it health, a wobble, break pieces, and the coin toss: clover = shortcut to the rooftop, skull = pumpkin boss.
  - The big bedroom has two locked doors on the "outline" bone: one `Bedroom_Base_Z_Door_2` (W/C) and one more `Bedroom_Base_Z_Door`. Both have Health and Damage FSMs.
  - In step 4 the leaves get these same components in Unity.

- **Same materials as today:**
  - **Bedroom_Wall:** the wallpaper, with the band at the floor and the drips at the top. 8 damask repeats per 3 m, so panels meet without a seam.
  - **Bedroom_Wood_Floor:** the floor, trims, casings, posts and beams, with the grain along each piece.
- **Sample rooms:** the mini and the big bedroom built from the pieces at today's size (7.5 × 7.5 m inside), to judge the look.
- **Renders:** `Exports/Remodel/bedroom_kit_renders/`. The pieces, inside each room, and both from above.
- **Not yet:** the mould spots repeat on every panel at the same place. A second 3 m wall using the other half of the texture (u 0.5 → 1.0) would break that up.
- **Re-running the script** never overwrites `Bedroom_Kit.blend` once it exists (it saves `*_generated.blend` instead), unless `--force` is passed. Your edits are safe.
- **Live mode:** the script can update the pieces inside your open kit file. Only the BR_Kit_* meshes are replaced, and nothing is saved (see the script's header).

## 2b. How rooms come and go today (checked 2026-11-19)

The old room's Animator (`Bedroom_Animator`) has these states. GAMESTAGES plays them directly; there are no transitions.

| State | Clip | Plays | What moves |
|---|---|---|---|
| START (default) | Bedroom_Base_Z_Start | 0.5 s | nothing: walls, roof and doors hidden (scale 0.001), the floor there |
| Bedroom_Mini_COME | 2.67 s | 2.7 s | walls 05-08 unfold from lying flat and grow from tiny, all together with a small overshoot; the roof flies in from the side and lands; the secret door grows into its doorway |
| Bedroom_Mini_GO | 3.33 s | 3.3 s | walls fold back down and shrink to nothing; the roof flies off; the secret door shrinks and drops |
| Bedroom_COME | 2.67 s × 0.666 | 4.0 s | walls 01-04 unfold and grow (stretched 1.4×); the roof lands; the two locked doors appear |
| Bedroom_GO | 3.33 s | 3.3 s | walls fold down and shrink, the roof flies off, the doors pop away |
| *_GO2, Setup | under 0.5 s | | door pops and poses (not used by GAMESTAGES now) |

The floor bone is never animated.

**The game's flow:**
1. **2 - The BR:** the rooftop room is removed and the bedroom room appears. Only its floor shows; it stays for both bedrooms.
2. **BRM_COME:** the mini room comes.
3. Magic table, furniture, stage pumpkin.
4. **Mini Enemy:** the walls, roof and door GO as the fight starts. The fight is on the open floor under the sky, the light goes off, and the hung decor drops 0.7 s later.
5. **Clean Mini:** the purple hole.
6. **2.5 - BR:** the big room comes; the light comes on in Pumpkin_5.
7. **Enemy1 - Wait:** the big room GOES as the fight starts.
8. **Clean Enemy 1:** the hole, then the bonus round.
9. **3 - LivingRoom:** the whole bedroom, floor and all, is removed and the living room's own floor and room come.

**The old roof:** a wood slab (one box) with a 16-face rim, a square lamp panel (9 faces) and the Point Light the GAMESTAGES light switch finds.

## 2c. The new kit room's animation (plan)

**One prefab, `Bedroom_Kit_Room`**, spawned by Bedroom_Base_Pos instead of Bedroom_Base_Z:
- **Floor:** 9 × 9 m of kit floor tiles. Always there, never animated, as today.
- **Mini set:** its walls (the window, the wide doorway with the secret door, the corner posts) and its roof: a wood slab like the old roof, your old lamp panel, and the room light.
- **Big set:** its walls (two windows, the wide doorway with the locked door, the open passage), corner posts and roof.
- **Moved over from the old room:** Fall Edges, Teleport Area, DropWallHungProps.

**The same Animator states and timings as today:** START, Bedroom_Mini_COME 2.7 s, Bedroom_Mini_GO 3.3 s, Bedroom_COME 4.0 s, Bedroom_GO 3.3 s.
- Each state tells a small **RoomKit** component which set to move and how, so the animation is made in code, not keyed in Blender.
- Because the state names stay, GAMESTAGES, the light switch (the light keeps its old path `.../Rooftop/Point Light`) and DropWallHungProps keep working with **no FSM changes**.

**COME:**
- Each wall piece starts lying flat on the floor and tiny. It unfolds upright and grows, all together with the same small overshoot as the old clips (the "pop-up book" look); its posts, window and door come with it.
- The roof comes last: it flies in from the side and lands on the walls.
- Optional: a shared 3D smoke puff where each wall lands.

**GO:**
- The roof lifts off and flies away.
- The walls fold back down flat and shrink to nothing; the doors shrink and drop.
- The floor stays.

**Details:**
- Wall colliders are off while walls move and on once they stand.
- Timings, overshoot and fold direction are numbers on the RoomKit component.
- **Doors:** the leaves get the old doors' parts: EnemyHitDamage, the Health and Damage FSMs, the break pieces, and for the secret door the wobble and the coin-toss shortcut.
- **Sizes:** both rooms 7.5 × 7.5 m (option A, so today's furniture sets, spawn points and NavMesh fit) unless you pick option B below.

## 3. Swap: next steps

1. **You check the kit:** shapes, trims, 3 m vs today's 3.58 m (the bee bosses' "stay high" hover must fit), door and window sizes. Then tweak.
2. **Room sizes:**
   - **A (recommended first):** both rooms 7.5 × 7.5 m on the same 9 × 9 floor. That keeps today's furniture sets, spawn points and NavMesh. The mini furniture may need moving in by up to 0.3 m.
   - **B:** mini 6 × 6, big 9 × 9. The rooms feel different, but the sets and spawn points move and the NavMesh is rebaked.
3. **Export:** FBX to `Assets/Models/Rooms/Bedroom_Kit.fbx` (one mesh per piece, names kept). In Unity, map the materials to Bedroom_Wall_Spooky and Wood_Floor_Mat 1. Make one prefab per piece, static, with BoxColliders.
4. **Room prefabs:** Bedroom_Mini_Kit and Bedroom_Big_Kit, built by an editor menu from a layout list (piece, cell, side). Each gets:
   - the room light (on a beam);
   - the door leaves (Door Secret in the small room, Door Locked in the big room) set into Door Wide. They get the old doors' parts: EnemyHitDamage, the Health and Damage FSMs, Door_Break_Drop_Pos with the break pieces, and for the secret one the Jiggle and the shortcut menu's coin toss;
   - the Fall Edges and Teleport Area moved over;
   - DropWallHungProps on the root.
5. **A RoomKit component** replaces the Animator clips:
   - **Come:** the walls rise from the floor one after another, with the shared smoke puffs.
   - **Go:** "the house eats the room". The purple hole pulls the walls in, top first, the pieces spiralling up as the leftovers do now, then the hole closes.
6. **GAMESTAGES**, rewired by a menu, not by hand:
   - the 6 Animator Play actions call RoomKit Come/Go;
   - the light path points to the new light;
   - Bedroom_Base_Pos spawns the kit room;
   - "3 - LivingRoom" destroys it as before.
7. **NavMesh, sets and spawns:** with option A, check that every furniture spot and spawn point is inside the new walls; with B, move them and rebake.
8. **Test** both stages with the play bot: furniture, both fights, the rush, the hole, the door shortcut, game over, and frame times.
9. **Remove the old bedroom from the bedroom stages.** Keep `Bedroom_Base_Z.fbx` and the prefab until the living room is swapped.

## 4. Built (2026-11-19, "Yes, build it")

- **One prefab, `Prefabs/Bases/Bedroom_Kit_Base`**, made by Tools > 429 Game > Rooms > Build Bedroom Kit Room. Use Bedroom Kit Room points `Bedroom_Base_Pos` at it (Use Old Bedroom Room goes back).
- **RoomKit** plays the come/go from the Animator's old state names, so GAMESTAGES is unchanged. Hidden sets stay switched on (invisible, not solid), like the old room's shrunk walls.
- **Doors:** the old doors' parts on the kit leaves. The secret door keeps the old hinge, starts ajar and slams shut when the player is on the floor. Its break pieces hide the chain (`Door_Break_Drop_No_Chain`).
- **Layout from the old come clips:** mini room = secret door north, window south. Big room = W/C door west, chained horseshoe door north (kit piece BR_Kit_Door_Chained), windows east and south.
- **Furniture fit:** 97 spawn points in the bedroom sets moved in by up to 0.37 m (the old mini room was 8.1 × 7.7 m). Touching pieces move together.
- **Not in the kit yet:** the bunting and curtains.
