# Living room + playroom kit: plan (2026-11-19)

**Goal (user):** "Check the next stage, analyze them, check their animation and then create a kit of livingroom and playroom to replace the current livingroom_Base."

Built the same way as the bedroom kit (see `BEDROOM_KIT_PLAN.md`, the clean-mesh rules).

## 1. What the two stages use today

**One rigged room for both stages:** `Prefabs/Bases/Livingroom_Base_Z`, an instance of `Models/Walls/Livingroom_Base_Z.fbx`. Its source is the `Livingroom_Base_Z` object in `Assets/Blender/Bedroom_Walls.blend`, whose rest pose is the end of COME. It has one skinned mesh with 3 materials (Livingroom_Wall_Spooky, Wood_Floor_Mat 1, Bedroom_Metal_Mat 1) and 12 bones.

- **Living room (room 1):** Wall_05–08, 8.55 × 8.55 m inside, 3.58 m high, slabs 0.30–0.37 m, a wood floor (±4.75) and a wood roof slab with a lamp frame.
  - **East wall:** the big double door. A 2.47 × 2.08 m doorway with a frame; X-braced leaves, chains and padlocks; static.
  - **North wall** (east end): a 1.40 × 2.08 m doorway with an X-braced chained door. Static; a collider blocks it.
  - **South wall** (west end): the same doorway with the breakable horseshoe door. It is a second **shortcut door**: Health → Toss Coin → DOOR_SHORTCUT or Hurry Pumpkin Boss, like the bedroom's secret one.
  - The 1.40 m doorways use the same 14-quad frame as the bedroom's Wall05.
  - No windows, baseboards or posts. The panelling is painted into the paper: you took the 3D panelling off on 10-22.
- **Playroom (room 2):** Wall_01–04, a cross, scaled 1.5× in the game.
  - At 1×: a 7.9 m square with an arm in the middle of each side. The arms are 1.6–1.9 m wide and reach ±7.5 m.
  - In the game (×1.5): ±5.96 m square, arms to ±11.2 m, walls 5.27 m.
  - **Brick walls:** each arm ends in a Brick_Wall_Breakable, a separate object with Health and Damage FSMs. Breaking one opens a Wall_Portal.
  - **Ceiling:** Livingroom_Base_Z_Roof_Sub, a separate object. EM3's start breaks it into Roof_Sub_Drop pieces. Its Light_Pos is where Pumpkin_7 puts the playroom light.
- **Textures:**
  - Wallpaper: `Combos/Wall_Stylize_Set_1/Livingroom_Wallpaper_Spooky.png` (+ `_Normal`). Green stripes, wood panelling painted at the bottom 30 %, a dark band at the top. It runs once up the wall (v 0 → 1 over 3.58 m).
  - Floor and frames: wood_floor3.
  - Doors' iron: medieval_metal.

## 2. How the rooms come and go today

| Animator state | Clip | Plays | Played by |
|---|---|---|---|
| COME (default) | LivingRoom_COME 2.67 s × 0.45 | 5.9 s | the room spawning (3 - LivingRoom / CP Scale LR) |
| LivingRoom_GO | 3.33 s | 3.3 s | Enemy2 - Wait (EM2 starts) |
| COME2 | Livingroom_COME2 1 s × 0.25 | 4.0 s | Scale LR, after LR_GO tweens the whole base to 1.5× (2 s) |
| GO4 | Livingroom_GO4 | 1.7 s | Enemy 3 Stuffs (EM3 starts) |
| LivingRoom_GO2/GO3, Livingroom_Setup | under 0.4 s | | nothing now |

**The flow:**
1. **3 - LivingRoom:** the room spawns at Livingroom_Base_Pos. COME brings room 1 in: the roof flies in, the walls unfold.
2. **Magic table, then LR - Stuffs / LR - Chaos:** the furniture.
3. **Enemy2 - Wait:** the light goes off and room 1 GOES (the walls fold away, the roof flies off). The hung decor drops.
4. **Clean Enemy 2.**
5. **LR_GO:** the base grows to 1.5×.
6. **Scale LR:** COME2 brings the playroom.
7. **Pumpkin_7:** the light comes on, plus a second light at the ceiling's Light_Pos.
8. **Jenga Tower** (or Jenga Tower 2, Chaos).
9. **Enemy 3 Stuffs:** the light goes off, GO4 swings the playroom walls down and out, and the ceiling breaks.
10. **Clean Enemy 3 → Bonus Round PR → Rooftop_ON:** the base is destroyed.

Also used:
- **Light path:** `Livingroom_Base_Z_Armature/Wall_08/Rooftop_LivingRoom/Point Light` (GAMESTAGES Find Child).
- **On the root:** DropWallHungProps (states LivingRoom_GO and GO4, radius 9) and the Info FSM (it registers the global Livingroom_Base).
- **Copied over as they are:** Fall Edges (the round-1 ring at ±6.6) and the Teleport Area.
- **EM2's boss** comes out at the scene's Door_Break_Drop_Pos_3 (4.25, 0, 2.37), in front of the big door.

## 3. The kit (made today)

`Exports/Remodel/Livingroom_Kit.blend`, made by `tools/livingroom_kit.py`. Renders are in `livingroom_kit_renders/`.

- **Grid:** 3 m with 1.5 m halves, **3 m high**, pivot at the inner face's bottom-left. Like the bedroom kit.
- **Walls:** one closed slab each, 0.31 m thick like your living room walls. A doorway takes 14 quads.
- **Wallpaper:** once up the wall (v 0 → 1 over 3 m) and repeating every 3 m (4 panels), so pieces meet without a seam. The panelling is 0.9 m high (it was 1.07 m on the 3.58 m walls).
- **Your parts, taken as they are** from Livingroom_Base_Z, found by where they sit. If you change one, the script notes it and leaves it out.

| Piece | What it is | Faces |
|---|---|---|
| LR_Kit_Wall_3m, LR_Kit_Wall_1p5m | slab | 6 each |
| LR_Kit_Door | slab with the 1.40 × 2.08 m doorway + your frame | 28 |
| LR_Kit_Door_Big | slab with the 2.47 × 2.08 m doorway + your big door's frame | 28 |
| LR_Kit_Leaf_X | your X-braced chained door (old north door) | 771 |
| LR_Kit_Leaf_Double | your big double door, chains and padlocks | 1529 |
| LR_Kit_Leaf_Horseshoe | the horseshoe shortcut door, with its chain (the bedroom's model) | 954 |
| LR_Kit_Floor_3m | wood tile | 6 |
| LR_Kit_Roof, LR_Kit_Lamp | your roof slab and lamp frame (Unity scales the slab to the room) | 6, 9 |

- **Leaves:** each shares its doorway's pivot, so placed with it, it stands in the doorway.
- **Sample rooms:**
  - **Living room:** 9 × 9 m (old 8.55). X door north, big door east, horseshoe door south, where the old ones were.
  - **Playroom:** at 1× (the game makes it 1.5×). A 7.5 m square (11.25 m in game, old 11.9) and arms 1.5 m wide (2.25 m in game, old 2.4–2.9) and 3.3 m long.
- **Corners:** where an arm leaves the square, the arm's walls start behind the square's wall, so no two faces lie on each other.

## 4. Swap: plan (to build after you check the kit)

**Prefab:** one, `Livingroom_Kit_Base`, built by a menu like the bedroom's. Livingroom_Base_Pos spawns it instead of Livingroom_Base_Z. GAMESTAGES needs **no changes**.

**Sets:**
- **Living (room 1):** its walls, doors, roof and lamp.
- **Play (room 2):** built at 1×. LR_GO's tween and CP Scale LR still make the whole room 1.5×.
- The floor never moves: the living room's 9 × 9 m. The playroom's arm floors come and go with its walls.

**Animator with the old names:**
- COME (default, so it plays when the room spawns), LivingRoom_GO, COME2, GO4, with the old timings.
- No-op GO2, GO3 and Setup states.
- RoomKit does the come and go in code, the same as the bedroom: walls unfold and grow, the roof flies in last; going, the roof flies off first and the walls fold away.

**Moved over from the old room:**
- Info FSM, DropWallHungProps (LivingRoom_GO, GO4, radius 9), Fall Edges, Teleport Area.
- The light at its old path.

**Doors:**
- **Horseshoe:** the old door's parts (health, wobble, break pieces, coin toss) on the kit leaf.
- **X door and big double door:** static leaves with colliders, like the old ones.

**Reused as they are:**
- The playroom's breakable ceiling (Roof_Sub, with Light_Pos and the drop), sized to the cross.
- The four brick walls with their portals, one at each arm's end.

**Furniture fit:**
- **Living room sets:** the room is 0.45 m wider than the old one, so pieces fit. Hung pictures move **out** to the new walls.
- **Playroom sets** (Jenga, Chaos): the square is 0.65 m smaller at 1.5×, so pieces near the walls move in. The candles near the arm ends move with them.
- The same menu as the bedroom: groups move together.

**Test:** the play bot through both stages: come/go pictures, the light, the hole, the shortcut door, the brick walls and the ceiling break, the bonus round. Then remove the old base from these stages.

**Your call before building:**
1. **Sizes:** living room 9 × 9 m (old 8.55). Playroom square 7.5 m (old 7.9), arms 1.5 m wide (old 1.6–1.9). All at 1×.
2. **Height:** 3 m, like the bedroom kit (old 3.58 m; the playroom becomes 4.5 m in game, was 5.27 m).
3. **Ceiling and brick walls:** reuse the old breakable ones (recommended), or model kit versions.
