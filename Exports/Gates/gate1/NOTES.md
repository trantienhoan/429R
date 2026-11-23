# Gate 1 (PlayGround, "cook and defend"): hand-over notes

Work folder (tests, backups): `C:/Users/ADMIN/Documents/429R_claude_backups/2026-11-23/gate1/`.
Plan and rules: `Exports/Gates/GATES_PLAN.md` (§0 the 2026-11-23 order, §2 the gate contract).

## 1. The kit (2026-11-23)

- **File:** `Exports/Gates/gate1/PlayGround_Kit.blend`, made by `tools/playground_kit.py` (headless Blender; never
  overwrites the .blend without `--force`, so once you edit it, it's yours). Re-run:
  `E:/Steam/steamapps/common/Blender/blender.exe -b --factory-startup --python Exports/Gates/gate1/tools/playground_kit.py -- Exports/Gates/gate1/PlayGround_Kit.blend Exports/Gates/gate1/kit_renders`
- **Pieces (40):** collection `PlayGround_Kit`, one collection per piece, objects `PlayGround_Kit_<Piece>`, pivot = the
  middle of the foot (a fence piece: its left post, pieces chain along +X), front = -Y, real metres.
  - Trees: Tree_Jelly_A (pink/lilac, 6.3 m), Tree_Jelly_B (aqua/teal), Tree_Lantern (3.8 m, 3 jack-o'-lanterns),
    Tree_Mushroom (3.1 m), Tree_Dead (4.9 m), Bush_Jelly, Stump_Slime.
  - Rocks and statues: Boulder_Slime, Geode_Candy (glowing crystals), Gravestone (cracked), Statue_Knight (holds a
    pumpkin, 2.4 m), Totem_Pumpkin (4 heads + wings, 2.8 m), Stepping_Stones, Fence_Picket (2 m).
  - Playground: Swing_Set (3.6 x 2.6 m), Seesaw_Bone, Slide (2 m high, 5.3 m long), Dome_CandyCane, Merry_Go_Round
    (pumpkin seats), Sandbox_Candy.
  - Toys: Rocking_Horse (skull head), Jack_In_The_Box, Toy_Block, Tin_Robot, Beach_Ball, Puddle_Slime.
  - Edge: Monster_Gate (3.85 m, purple mist: where waves can come in), Hill_Slime (10 x 6.6 m).
  - Giant_Stew_Pumpkin: gate 1's own giant pumpkin for the finale (E1): radius 1.5 (gate 1's Finale Radius), pivot =
    the ball's middle, face to -Y; glowing slime boiling over the top, drips, bubbles, an angry green-glow face. In
    Unity it becomes the model of a variant of the shared Pumpkin_Ball_Boss (step 5), like gate 3's jack-o'-lantern.
  - Breakables, each with a `_Broken` set (3-5 chunks, each its own object): Crate, Barrel, Candy_Basket, Clay_Pot,
    Pumpkin_Stack, Lollipop_Sign.
  - All quads, all closed, 44-1,264 triangles a piece (`kit_renders/kit_report.txt`).
- **Colour:** one palette, `textures/PlayGround_Palette.png` (32 colour columns, dark at the bottom, light at the top;
  alpha = gloss) + `PlayGround_Palette_Emission.png` (the glowing columns: slime, lanterns, mist, crystals, eyes), one
  material `PlayGround_Kit_Palette`. A face's colour = the column its UVs sit in. Recolour a part by moving its UVs to
  another column (or repaint a column).
- **Sample_Arena** (not exported): the pieces laid out like the concept around a stand-in cauldron, at the game's size
  (today's field inside the walls is about 15.2 x 16.6 m: the fence pieces at the front mark its line; the toys ring
  stands just outside it, the trees and hills further out).
- **Renders:** `kit_renders/kit_sheet.png`, `kit_sheet_small.png`, `arena_concept_angle.png`, `arena_eye_level.png`,
  `kit_vs_concept.jpg` (the concept beside the arena).
- **In Unity (2026-11-23, 01:00):** exported by `tools/export_playground_kit.py` (41 FBX in
  `Assets/Models/Gates/PlayGround/`, palette in `Assets/M_Textures/Gates/PlayGround/`), then
  **Tools > 429 Game > PlayGround > Build PlayGround Kit** (re-runnable, never touches the scene): material
  `PlayGround_Kit_Mat`, 41 prefabs in `Assets/Prefabs/PlayGround/Kit/` (looks only for now; broken sets keep their
  chunks as children), the boss variant `Assets/Prefabs/PlayGround/Pumpkin_Ball_Boss_Stew.prefab` (a real variant of
  the shared giant pumpkin wearing the stew pumpkin, face to its front), and the cauldron's Finale Boss pointed at it.
  Checked in Play: the stew pumpkin drops in the finale (`kit_renders/giant_stew_pumpkin_ingame.png`).
- **After a kit change:** run the kit script (or edit the .blend), then the export script, then Build PlayGround Kit.
- **Stage dressing (2026-11-23):** **Tools > 429 Game > PlayGround > Dress PlayGround With Kit** (re-runnable; undo:
  Remove Kit Dressing) puts 60 kit pieces under `Play_Ground_Z/Kit_Dressing` round the field, like the concept: the 4
  monster gates on the corners where the waves come in, slime puddles in the field, the playground toys just outside
  the walls, trees, then slime hills at the edge. 28,920 triangles, one material, looks only (no colliders): the field,
  its walls, props and their gameplay are untouched. Pictures: `kit_renders/g1_dressed_eye.png`, `g1_dressed_high.png`.
- **Not done yet:** kit pieces with gameplay (the breakables' Broken sets, hit/drop logic): the old props still do that.

## 2. Logic fixes (2026-11-23, compile-checked, tested in Play)

- `CauldronDefenceMenu` ("Use Defend The Cauldron" / "Use Cooking Rounds"): no longer touches GAMESTAGES. It used to
  re-point "Path 1 ON" next1 to "Tween PlayGroundZ To Boss" and silently drop the rooftop's tree defence on every
  re-run. Now it only sets up the cauldron prefab and warns if next1 doesn't go to "Tween PlayGroundZ".
- `CookingRewards`: the first-clear notice (it pauses the game) waits until the giant pumpkin is broken and the player
  stands on the rooftop; none after a Game Over.
- `CauldronDefence`:
  - a spare log every 45 s while a right recipe waits for the fire (was one, ever);
  - a new magic hat 4 s after the hat is gone with its recipe unread (the purple hole of a death can eat it);
  - the pot spits out a tree pumpkin when the recipe needs one and none has been about for 40 s;
  - the win finds the Fairy Shield under GAMESTAGES (no more whole-scene search: a hitch);
  - its enemy wave is forgotten when it goes (a leak after a loss);
  - monster gates: the waves (and mini bosses) come in at the field's corners instead of anywhere on the edge; 1-3
    corners are open by phase (Open Gates 1/2/2/3, all 4 after a loss), they move every 25 s, a newly open corner puffs.
    Spawn From Gates off = the old random edge. The kit's Monster_Gate will stand on these corners (step 5).
- A wrong recipe costs less: one small slime in the colour of a candy the recipe still needs (it drops that candy),
  not a big one that splits (set by "Use Defend The Cauldron"; only replaces the old Slime_Big).
- Stale "10 minutes" in comments and tooltips: now the 5-minute cook.
- To apply the prefab side (the spare hat, the small slime): Tools > 429 Game > PlayGround > Use Defend The Cauldron
  (re-runnable; prefab only, GAMESTAGES untouched; backup of the first run in 429R_claude_backups/2026-11-16/gate1_defend).

## 2a. The gates' one flow: cook and defend in 3 rounds (GateRun)

- `Scripts/Gates/GateRun.cs` (the coordinator's) runs the card, the start chest, the 3 round chests, the rounds' time
  limits, the HUD line and the finale; gate 1's own part is `PlayGroundGame` (an IGateGame on Play_Ground_Z).
- **The start chest's seed** (Bracer) pops the cauldron out in the field's middle (the old Cauldron_Pos chest spawner
  is switched off: Gate Run drops the start chest in front of the player).
- **Each round:** a new recipe (the magic hat; from round 2 it hides somewhere new) -> fill the pot -> light the fire
  (round 1) -> the cook, defended (the monsters come in at the open monster gates) -> the round's mini bosses come out
  -> beaten = Gate Run drops the round's gear chest. The HUD line: FIND THE HAT, FILL THE POT n/m, LIGHT THE FIRE,
  DEFEND THE POT, BEAT THE SLIME n/m.
  | Round | Recipe | Cook | Monsters (max alive) | Gates | Mini bosses | Time limit |
  |---|---|---|---|---|---|---|
  | 1 | 2 candies + 1 tree pumpkin | 45 s | small spiders (4) | 1 | Slime_Big_Blue | 150 s |
  | 2 | 3 candies + 1 tree pumpkin | 60 s | small + normal spiders (5) | 2 | Slime_Big_Red | 165 s |
  | 3 | 4 candies + 1 tree pumpkin | 75 s | spiders + roaches (6) | 3 | Slime_Big_Yellow + Slime_Big | 180 s |
- **After round 3's chest:** the field is swept (purple hole), the pot bursts, the stew pumpkin drops where it stood.
- **A round that runs out of time** ends anyway (its bosses go without reward, the dish is dropped); its chest still drops.
- **Start chest:** Gate Run's start chest is the cauldron chest (`Treasure_Chest_and_Seed_Bracer_Cauldron`), dropped in
  the field's middle; its seed pops the cauldron there as before. Seed not eaten in 25 s: the cauldron pops out by
  itself (a seed eaten later doesn't make a second one). Round chests drop in front of the player, never on the pot.
- **Pressure on the pot:** a quarter of each round's monsters always go for the pot (Pot Biter Share 0.25; 0 = the
  old rule only: near the pot they go for the player, far away for the pot).
- **Look:** the house fog is lifted to 12-36 m while the PlayGround is out (PlayGroundGame Lift Fog), put back when it
  goes (the exit or a Game Over). The stew pumpkin charges at 10 m/s (the shared one 13) in the small field.
- **The hat each round (fixed 01:55, checked in Play):** when it hides for a new round its card goes back in, and a hat
  just moved ignores hits for 2 s; only the player's own hits and throws count (HeldThings.ByPlayer), not a candy or log
  lying about. Before, a new round's recipe could show by itself (HUD said FILL THE POT instead of FIND THE HAT).
- **Code:** `CauldronDefence` has a round mode (BeginRound / EndRoundNow / FinaleFor; the old single 5-minute dish is
  unchanged when nothing drives it); `Cauldron` (UseOneDish resets the dish, StopDish, InPot); `PlayGroundField`
  (HideHatNow). Set up by **Tools > 429 Game > PlayGround > Use Gate Run (3 Rounds)** (undo: Back To One Dish); it also
  removed the 2 dead inactive copies of the cauldron and the hat that were nested in Play_Ground_Z.
- **Backups** of every prefab before a menu first changed it: `Documents/429R_claude_backups/2026-11-23/gate1/`
  (`Play_Ground_Z_before_gate_run.prefab`, `Play_Ground_Z_before_dressing.prefab`) and
  `2026-11-16/gate1_defend/Fire_Woods_n_Cauldron.prefab`; scripts before the edits in `.../gate1/backup_before*/`.

## 2a+. The GateRun test (Editor, `_claude_test`, 2026-11-23 01:33; scripted player, cooks x10)

From "Clean for Path 1": WORKING SITE notice -> the card -> the cauldron chest 2.8 m ahead (field middle) -> its seed
-> one cauldron at the field's middle -> round 1 (HUD: FIND THE HAT -> DEFEND THE POT -> BEAT THE SLIME 0/1) -> the
Chest chest 2.7 m ahead -> round 2 left to run out (limit cut to 15 s for the test; the HUD counts the last seconds;
its chest still dropped) -> round 3 (2 mini bosses, BEAT THE SLIME 0/2) -> the Shield chest -> BREAK IT! -> the stew
pumpkin (r 1.5, 150 hp, charge 10 m/s; HUD GIANT PUMPKIN 150) -> The_Seed -> Whack 6, 10.1 s after the seed. All 4
gear bools set by their seeds; 0 game errors; no GearGuard message; fog 12-36 m in the gate, 6-22 m on the rooftop.
Whole gate in the test: 94 s (cooks x10, bosses killed at once). Real play estimate: about 7-9 minutes.

## 2b. Measured (Editor, `_claude_test`, 2026-11-23 00:52-00:58; scripted player, cook at x10)

- Through GAMESTAGES from "Clean for Path 1": notice -> start chest -> its seed (Bracer) at 2.3 s -> cauldron ->
  recipe + fire -> 4 mini bosses at 25/50/75/90 % -> 4 chests (all 4 gear bools set before the win) -> win -> the
  giant pumpkin in 6.5 s after the win -> broken -> The_Seed -> Tween -> Rooftop_ON PG -> **Whack 6, 10.1 s after the
  seed**. 0 game errors, no GearGuard message.
- The shield (and its purple hole) sits at the world origin = the PlayGround's middle (GAMESTAGES' Shield_Sphere).
  - A death with a lucky-coin revive does NOT open it: nothing was eaten, the player got up at 60 hp.
  - At the win it eats the field: trees, table, skull, barrels, baskets, pipes, the hat, tree pumpkins, and an unopened
    chest (the 4th mini boss's reward chest dropped 1 s before the win and was eaten). Candies stay.
- The pot took no bites while the player stood within 7 m (the waves went for the player); the player lost about
  100 hp over the (x10) cook. Editor frame time during the cook: 5.4 ms average, 43 ms worst (one spike).

## For the coordinator

- No scene or GAMESTAGES changes from gate 1, no requests. Everything is in my own prefabs (Play_Ground_Z,
  Fire_Woods_n_Cauldron, Pumpkin_Ball_Boss_Stew, the kit) and scripts.
- **E2 (one reward budget):** gate 1's giant pumpkin still breaks into 42 candies (CauldronDefence Finale Candies, the old
  Daruma prize; the shared one gives 14). Set it when the user picks the number.
- **E5:** the WORKING SITE notice still shows at Path 1 ON before GateRun's card.
- Seen, not gate 1's: two errors at Play exit ("Cannot set the parent of 'Left/Right Controller' while activating or
  deactivating 'DeadCam'"), and the start rooftop's MOVE/TELEPORT tutorial boards stay visible south of the PlayGround.
- Old code I propose to delete (ask the user first; nothing deleted): PlayGroundRounds + its menus, the cooking rounds,
  candy pot, wild bell slimes and laughing pumpkins in Cauldron.cs, the Daruma hook (SpecialStage3Trigger on the
  cauldron), CauldronDefence's old single-dish mode (share-timed mini bosses, its own chests, Win, GearGuard) once the
  user is happy with the 3 rounds.

## Headset checklist (for the user)

Path: buy gate 1 on the start rooftop, stand in its circle 3 s.
1. The card (COOK AND DEFEND) is readable; the start chest lands in front of you; eating its seed pops the cauldron
   out (does the push-back feel OK?).
2. FIND THE HAT: the hat is findable (round 1 where the PlayGround puts it, rounds 2-3 next to a tree, bush or tall
   grass); hitting it shows the recipe card; the recipe shows over the pot.
3. FILL THE POT: candies and a tree pumpkin go in; a wrong recipe spits ONE small slime that drops a missing candy.
   With no tree pumpkin about for 40 s the pot spits one out.
4. LIGHT THE FIRE: a log in the fire starts the cook (a spare log pops out after 45 s).
5. DEFEND THE POT: the monsters come in at the corners (a purple puff where a corner opens); can you see where they
   come from? Near the pot (7 m) they go for you; far away they bite the pot (its bell rings).
6. BEAT THE SLIME: the round's mini boss(es) at the end of the cook; then the gear chest in front of you.
7. The HUD line under your view (ROUND n/3 + the goal) is readable and not in the way.
8. The finale: the stew pumpkin's look, its charges in the small field (about 11 x 12 m), breaking it, The_Seed, then
   the rooftop (Whack 6).
9. How long the whole gate takes (aim: 6-8 minutes) and whether it felt fun.

## 3. Open questions for the user

1. **Doom boss:** if the pot breaks, the chaos Pumpkin_Boss_Z comes; killing it today counts as beating the whole game
   (Thank For Playing). Keep, or should it only end the gate (on to the giant pumpkin / the rooftop)?
2. **Hats:** round 1 where the PlayGround puts it, rounds 2-3 hidden next to a tree, bush or tall grass at random. Keep,
   or will you place fixed spots?
3. **Cook length:** 45 / 60 / 75 s of cooking (logs make it faster) with round limits 150 / 165 / 180 s: the whole gate
   is about 7-9 minutes. Shorter (e.g. 40 / 50 / 60 s) for 6-7 minutes?
4. **Your placement copies:** the inactive cauldron and hat copies you kept under `Cauldron_Pos` and `Recipe_Hat_Pos`
   were removed (the brief asked; Spawn Previews still draws the hat, and the cauldron now pops out in the field's
   middle). Want them back? They're in `Play_Ground_Z_before_gate_run.prefab` in the backup folder.
