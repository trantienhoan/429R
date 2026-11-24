# Gate 3 (Ball Valley): hand-over notes

Session: "Gate 3: Ball Valley" (2026-11-22/23). Plan: `Exports/Gates/GATES_PLAN.md` (§0 order, §2 contract, §4 brief).
Work files and backups: `C:/Users/ADMIN/Documents/429R_claude_backups/2026-11-22/gate3/`.

## 0. 2026-11-23 evening (coordinator, second PC): seed balls

- The user: "make some balls bigger a little and have red aura, push that ball into hole and the hole will spit out a seed".
- Built: 4 of the 15 balls (picked at random) are Seed Ball Size (1.25x) bigger, with a red aura: Seed Ball Aura Look
  = Effects/Chaos_Aura (the pumpkin boss's red aura), a louder copy per ball (`BallPlayer.LouderAura`: wisps more solid
  and thicker; its glow smaller and lifted, as at full size it cut a hard line on the lawn), plus a red glow pulse and
  sparks (`BallPlayer.sizeTimes`, `aura`, `auraLook`). The player's own pocket of one spits its gear seed (Seed_Bracer,
  Seed_Chest, Seed_Head, Seed_Shield, in that order) out of the hole in a red streak onto the lawn, half way to the middle.
  The seed moves in if the holes come near it; Gate Exit eats any still waiting. `BallValleyGate` (Seed Balls header);
  `BallValleyStage.DropBalls` now returns the balls; `Ball_Valley.prefab` has the 4 seeds; Set Up Ball Valley fills them
  if empty.
- Editor-tested 2026-11-23 evening (`_claude_test`, 0 errors): seed balls r 1.0-1.1 m (others 0.83); each pocket spat
  its seed 4-12 m clear of the holes; eating each set its gear bool (all 4); a seed ball knocked in by the others drops
  back still big and red. Not tried in the headset. Test hook: BallValleyGate's "Test: Pocket A Seed Ball (Mine)".
- Gate 3's fix list: `../GATES_REVIEW_2026-11-23.md` §5.

## 1. The kit (step 1, 2026-11-23)

- `Exports/Gates/gate3/BallValley_Kit.blend`, built by `tools/ballvalley_kit.py` (headless Blender; never overwrites the
  .blend without `--force`: once you edit it, it's yours):
  `blender -b --factory-startup --python Exports/Gates/gate3/tools/ballvalley_kit.py -- Exports/Gates/gate3/BallValley_Kit.blend Exports/Gates/gate3/kit_renders`
- Pieces (collection `BallValley_Kit`, one sub-collection each, objects `BallValley_Kit_<Piece>`, real metres, all quads,
  closed parts): Rim_Straight_4m, Rim_End, Hole_Corner, Hole_Side, Lawn_Tile_5m, Cliff_Straight_4m, Tree_Jelly,
  Boulder_Mossy, Gravestone, Lantern_Post, Gazebo_Ruined, Tree_Twisted, Pumpkin_Lantern, Giant_Jack_O_Lantern,
  Ball_Small/Medium/Big/Tail (Candy: sample only). Biggest: Hole_Corner 2,080 tris, Hole_Side 1,640, Tree_Jelly 1,160.
- Sizes from the game: lawn 50 x 25 m, pits r 2.55 (corners) / 2.36 (sides) set 0.75 m out from the lawn corner/edge,
  rim 1.2 m high and 1.45 m deep, giant pumpkin r 2.6, balls r 0.8 / 1.2 / 2.0, tail 0.5.
- One palette: `Exports/Gates/gate3/Palette/BallValley_Palette.png` (32 swatches x 32 px, dark at the bottom, light at
  the top, alpha = smoothness) + `_Emission.png` (glows: runes, lanterns, the pumpkin's face, the pit floor).
- Collections not exported: `Sample_Arena` (laid out like the concept), `Kit_Sheet` (ground for the sheet render),
  `Render_Stuff`. Renders: `kit_renders/` (kit_sheet, arena_concept_angle, arena_above, arena_eye_level).
- Export for Unity (step 5): `tools/export_ballvalley_kit.py` -> `Assets/Models/Gates/BallValley/*.fbx` +
  `Assets/M_Textures/Gates/BallValley/` (not run yet).

## 2. Baseline run (step 2, measured before any change)

2026-11-23 00:31, Editor, `_claude_test`, GAMESTAGES "Clean for Path 3", no headset (the player stands still at the start
spot; hp topped up to 120 under 40 so the run goes on; the boss broken with Special Hit 25 s after it landed):
- Times (from Clean for Path 3): Path 3 ON 1.3 s; shrinks at 46 / 96 / 146 / 196 s; the last phase (finale) 198.8 s;
  boss lands 200.8 s; won 226.7 s; The_Seed auto-eaten after 20 s; Rooftop_ON PG 246.7 s; Whack 6 251.8 s.
- Balls: the bots pocketed 14 of 15 (11 gone by 64 s), the player 0. One ball left at the finale.
- Health: 307 hp lost in 227 s (3 top-ups): ~50 hp/min from crashes (8 hp a hit), 2 hole falls (55 each: one at 130 s,
  one at 198 s when the last shrink moved the bottom-middle hole onto the start spot), boss: a bump 16, a charge 44.
- Candies on the field 30 at the start, 21-30 in phase 1, ~17 in phase 3, 8-12 at the end. Slimes 3-6. Loose balls 0-23.
- Frame time (Editor): 4-6 ms average; spikes 248 ms when the valley builds, 844 ms at 20 s, 862 ms at 198 s (the last
  phase: the trees and rocks crumble into slimes), 18-34 ms every ~15 s.
- Console: 0 errors (4 setup warnings unrelated to the gate). After Whack 6: fog on, far plane 40 m (look given back).

## 3. Changes

Step 2 (2026-11-23, flow-independent; compiled in Unity 00:49, not yet play-tested):
- `BallValleyStage`: null-safe loose-ball loops; the kill feed logs only in the Editor / dev builds; the Tuning tooltip
  says the gate's numbers replace it; a third of the candies at the start (the spawner fills the rest in ~3 s).
- `Valley`: the floor and teleport-lawn MeshColliders are re-cooked at most every 0.1 s while the valley shrinks (the
  last size always built).
- `BallValleyPlayer`: switched off mid-fall or mid-dash gives the body, the mover and the view back; the charge needs
  the fists touching (0.16 m, field renamed `fistsClose` so the old 0.3 m on the prefab is dropped) and low (0.22 m under
  the eyes: a guard by the chin doesn't charge); a full charge dashes only when the fists are pulled 0.32 m apart within
  0.6 s (opening a hand fizzles it); the dash is level when looking down (the head's top takes over); while the valley
  shrinks, a hole moving onto the player pushes them along 0.6 m from its rim (no unavoidable falls).
- `Sfx` + Set Up Ball Valley's sound list: a pocket plays lowDown (not the player's fall whistle), a bot growing plays
  pluck_001/002 (not 429R's levelup), the charge full plays highUp, a pocket by the player plays ka_ching (Kenney / the
  user's Audio). Needs Set Up Ball Valley run again (refreshes the prefab's sounds).
- `BallValleyGate`: Candy Every 0.3 s (was 0.16 s); its header says its numbers replace the stage's Tuning.
- `BallValleyMenu`: Watch gives the stage BALLS' numbers back after the gate's Awake; stale header line fixed.
- Kit hooks (inert until Build Ball Valley Kit fills them): `BallValleyStage` kit fields (material, rim, holes, trees,
  rocks, decor, night palette); `Valley`/`ValleyShrink` lay the kit rim, turn the kit holes to each corner/side (their
  opening and collar measured by the same code as BALLS' pockets), colour the lawn/cliffs/chasm from the kit palette;
  `Tuning` Knock Friction / Knock Recover Speed (BALLS' values unless set).
- New: `Editor/BallValleyKitMenu.cs` (Build Ball Valley Kit; Ball Valley: BALLS Look (No Kit)),
  `Exports/Gates/gate3/tools/night_palette.py` -> `Assets/BallValley/Kit/BALLS_Palette_Night.png`.

Step 3 (2026-11-23, the shared flow): gate 3 runs on `Gates/GateRun.cs` (the coordinator's).
- `BallValleyGate` is now GateRun's `IGateGame`: card "RUN AND SNOOKER / Punch the balls into the holes."; start chest
  (Bracer); rounds 1-3: 5 balls drop in each round (tempers 0.35-0.55 / 0.45-0.65 / 0.55-0.8, hunters 1 / 1 / 2), the
  valley shrinks a step from round 2 (1 -> 0.83 -> 0.67, 4 s, earth_rumble), the goal BALLS n/5 (any pocket counts;
  the player's own pops 3 candies out of the hole + ka_ching), 120 s a round (leftovers stay); Chest/Head/Shield chests
  from GateRun on the lawn in front of the player, clear of the holes; finale: leftover balls go, shrink to 0.5 (6 s),
  the giant pumpkin 2 s later on the far half (GateRun.Finale -> GateBossFinale: it now also drops the seed if the boss
  vanishes). The valley detaches when Gate Exit starts (GAMESTAGES clears Currently Spawned Stuffs as the seed is
  eaten) and goes once the player is on the rooftop.
- Removed: its own rules notice, HUD line, 15-balls-at-once shrink clock, ALL IN!/FIELD SHRINKS!/LAST STAND! flashes,
  the old Won() (gear bools now come from the 4 chests: no GearGuard flip).
- `BallValleyStage`: `ShrinkToPhase`, `DropBalls`, `maxHunters`, `botsHuntBots` (off in the gate: bots graze and hunt
  only the player), `startBalls`; the Moment of Truth is off in the gate (no crumble, no fury ramp before the boss).
- Punched balls roll on like snooker balls (braking 1.5 m/s^2 instead of 5, the bot back in control under 2.5 m/s).
- A throw by the giant pumpkin into a hole costs no fall damage (2.5 s grace): its heavy hit was the price.
- Set Up Ball Valley adds/configures GateRun (re-runnable, keeps tuned numbers): chests, puff, The_Seed, finale boss
  (gate 3's own variant once Build Ball Valley Kit made it).
- The dash calls `Locomotion/ScriptedMoveVignette` (the coordinator's, from requests/gate3-dash-vignette.md).

Test 2026-11-23 01:10 (Editor, `_claude_test`, Clean for Path 3, standing player, nobody opening chests): card closed
3.7/6.3 s, start chest 6.6 s (its seed not eaten: GateRun went on after 25 s), round 1 33 s (5 pockets credited to the
player: 3 candies popped from each hole), won 42 s; round 2 70 s (shrink 1 -> 0.83, limit cut to 15 s: ended by its
timer at 85 s, its 5 balls stayed); round 3 94 s (10 balls, shrink -> 0.67, 5 pockets by nobody), won 103 s; finale
129 s (balls cleared, shrink -> 0.5 in 6 s), boss in 137 s, broken 159 s (Special Hit), The_Seed eaten 162 s, Rooftop_ON
PG 162 s, Whack 6 167 s, the valley gone, fog on, far 40, all 4 gear bools from the chests, 0 errors, no GearGuard
warning. Hitches: 106 ms at the valley's start (was 248), one 859 ms during the boss (the baseline had two of these
before any change: likely the Editor's garbage collection; check in a build).
Vignette test (PC Controls switch the vignette off without a headset, so it was switched on for the test): dash at
Low -> aperture 0.91, dash at Off -> 1.00 (nothing), a shove in the gate at Low -> 0.90; each released after. The user's
comfort level (Medium, a PlayerPrefs value) was put back.

Step 5 (2026-11-23, the kit in Unity): `export_ballvalley_kit.py` -> `Assets/Models/Gates/BallValley/` (18 FBX) +
`Assets/M_Textures/Gates/BallValley/`; Tools > 429 Game > Ball Valley > Build Ball Valley Kit: material
`BallValley_Kit_Mat`, 18 prefabs in `Assets/Prefabs/BallValley/Kit/`, gate 3's giant pumpkin
`Assets/Prefabs/BallValley/Pumpkin_Ball_Boss_BallValley.prefab` (variant, the kit's jack-o'-lantern), and Ball_Valley
wears the kit (rim, holes measured r 2.70 / 2.51, jelly trees, boulders + gravestones, 21 dressing pieces round it,
BALLS' mountains/ground/grass on the night palette). Back to BALLS' look: Ball Valley: BALLS Look (No Kit).
Pictures: `kit_renders/unity_kit_v1_top.png`, `unity_kit_v1_eye_level.png`.

Final run 2026-11-23 01:27 (kit on, `_claude_test`, Clean for Path 3, a "perfect player": every pocket credited to the
player at 1 a second, chests opened and seeds eaten 2 s after they show): card 3.7/6.3 s, start chest 6.6 s, its seed
eaten 13.9 s, round 1 14.7-23.7 s, round 2 33.3-42.3 s (0.83), round 3 51.9-60.9 s (0.67), finale 67.3 s, gate 3's own
giant pumpkin (Pumpkin_Ball_Boss_BallValley) in at 75.3 s, broken 97.3 s, The_Seed eaten 100.8 s, Whack 6 105.9 s;
15 pockets, all by the player (45 candies popped); 22 hp lost; valley gone, fog on, far 40, all 4 gear bools + The_Seed;
0 errors, no GearGuard warning; longest frame 117 ms (the valley's start), 59 ms at the rooftop.
Last edit after it (compile-checked, not played): the valley goes the moment Gate Exit puts the player on the rooftop
(was 0.3 s later, its look showed through the fade-in).

## 3b. What to run / save

- Nothing in the scene changed. Prefabs changed by menus (already run): `Prefabs/BallValley/Ball_Valley` (Set Up Ball
  Valley: GateRun + sounds; Build Ball Valley Kit: the kit), new `Prefabs/BallValley/Kit/*`,
  `Prefabs/BallValley/Pumpkin_Ball_Boss_BallValley`. Backups: `429R_claude_backups/2026-11-22/gate3/backup_before_step2/`
  (before any change) and `.../2026-11-23/gate3/prefab_backups/` (before each kit dressing).
- After editing `BallValley_Kit.blend`: run `tools/export_ballvalley_kit.py`, then Build Ball Valley Kit.
- For the coordinator (Phase 2): Path 3 ON still shows "WORKING SITE" (E5); E2 reward number; E7 Enemy 4 check.

## 3c. Open (the user's call)

- Pocket reward: 3 candies per ball the player pockets (BallValleyGate Pocket Candies).
- The kit (v1): shapes and colours; BALLS' mountains round it are only re-tinted (light lavender from above): darker,
  or kit hills instead?
- Round limit 120 s (GateRun Round Seconds); 5 balls a round; hunters 1/1/2.
- Not fixed (shared code or rare): GateExit's rooftop wait has no hard timeout; a player hugging the border can make
  the giant pumpkin skip charges; balls slower than 2.5 m/s pass through the player's body until 0.65 m.
- ~850 ms hitches every few minutes in the Editor (also before any change): likely the Editor's garbage collection;
  check with the profiler in a build.

## 3d. K: PC session, 2026-11-23 morning (groundwork, nothing changed in Assets)

- Pictures (Unity, `_claude_bot`, GAMESTAGES Clean for Path 3 at 60 s into a run, the Blender render's camera
  (-19, 21, -42) -> (4.5, -3, 8), 56 deg): `kit_renders/status_2026-11-23_look.png` (concept / Blender kit / Unity round 1),
  `status_2026-11-23_play.png` (eyes at the start spot, the nearest hole at 9 m, finale from above and from the eyes),
  `unity_kit_v1_concept_angle.png`. Script: `K:/WORK_2026/08_04/_claude/gate3_2026-11-23/g3_shots2.cs`.
- Test gotcha: jumping to Clean for Path 3 in the first ~25 s of a run shows the opening's fires, smoke and a near-black
  lawn; wait until Time.time > 60. The MOVE/TELEPORT boards stay in the valley after a test jump (known).
- BALLS' `Map_Static` keeps the mountains as their own part `Mountains` (3,108 tris, 590 x 145 x 507 at BALLS scale =
  up to 36 m tall in the game): they can be darkened, lowered or hidden for kit hills on their own.
- Kit pieces used in Unity: 11 of 18 (not used: Ball_Small/Medium/Big/Tail, Lawn_Tile_5m, Cliff_Straight_4m, Rim_End).
- **The user's remodel of BallValley_Kit.blend (started 2026-11-23 ~09:00).** Before: everything backed up in
  `K:/WORK_2026/08_04/_claude/gate3_2026-11-23/backup_before_remodel/` (.blend, 18 FBX + metas, 20 prefabs, palette);
  baseline `kit_before.json` + `check_before/` renders there. After the user saves:
  1. `blender -b Exports/Gates/gate3/BallValley_Kit.blend --python Exports/Gates/gate3/tools/check_kit.py -- <out.json>
     --before K:/WORK_2026/08_04/_claude/gate3_2026-11-23/kit_before.json --render <folder>`: what changed, problems
     (left-out objects, other materials, faces off the palette, hole openings, the giant's face), sheet + arena renders.
  2. Faces off the palette: ask the user which swatch, UVs in their live Blender with their OK (they save).
  3. `tools/export_ballvalley_kit.py` (now joins visible meshes parented to a piece and applies rotation/scale on a
     temporary copy; a plain piece exports byte-for-byte as before), then Unity: refresh, Build Ball Valley Kit, pictures
     with `g3_shots2.cs` (same camera) for before/after.
  Game-critical shapes (baseline): holes' pivot = the lawn corner / long-edge middle, pit opening r 2.70 (corner) /
  2.51 (side) centred 0.75 m out, wall from lawn level down to -6.3 m; Rim_Straight_4m 3.94 m along X, bulk on +Y
  (outside); giant jack-o'-lantern r 2.60 round its pivot, 52 faces on glow_face (its front); decor/trees/rocks: origin
  at the bottom middle.
- **Remodel set up (2026-11-23 10:31 save -> Unity 10:47):** the user moved 14 pieces onto the house's furniture
  texture (Blender material `Bedroom_Furniture_Set1`, its PNGs in `Assets/M_Textures/BedRoom_Set_1/NEW/`, linked by an
  absolute K: path: on the second PC the .blend shows them pink, the export is unaffected); balls + candy stay on the
  palette. New: stone border with vines, angry jack-o'-lantern (r 2.55, face to Blender -Y), orange jelly trees, stone
  gazebo, 1,146-tri pumpkin lanterns (were 200; 10 round the field), holes 1,512 / 1,104 tris (were 2,080 / 1,640), pits
  unchanged (r 2.70 / 2.51). Code: the export writes `kit_materials.json` (piece -> Blender material); Build Ball
  Valley Kit puts the Unity material on each piece (Bedroom_Furniture_Set1 -> `Assets/Materials/Bedroom_Set_1_Mat`,
  the house's, never edited here; BallValley_Palette -> BallValley_Kit_Mat), measures the giant's model radius (was a
  fixed 2.6) and falls back to the modelled front for its face; `ValleyShrink` draws the 6 holes with their piece's
  material (was always the palette one). The code-built lawn, cliffs and chasm stay on the palette. Compiled 0 errors,
  menu run, Play pictures `kit_renders/remodel_2026-11-23_unity.png` + `_blender.png`. Lost with the palette: every
  glow (pit rings, lantern glass, pumpkin faces): the house material's emission is the whole atlas at 13%.
- **Glow back (user: "Yes, glow them", 11:15):** `tools/glow_mask.py` paints
  `Assets/M_Textures/Gates/BallValley/BallValley_House_Glow.png` (2048, black but for the texels of the glow faces:
  the purple ring at lawn level in both holes -> lavender 0.80/0.55/1.00, Lantern_Post's yellow glass -> 1.00/0.82/0.45,
  the yellow face cut-outs of Pumpkin_Lantern + the giant -> 1.00/0.78/0.30; 295 texels + 3 px spread, never on texels
  a non-glowing kit face uses: 3 left dark). The export runs it by itself when Bedroom_Furniture_Set1 is in the .blend.
  Build Ball Valley Kit makes `BallValley_House_Glow_Mat` (a copy of the house's Bedroom_Set_1_Mat refreshed from it
  on every run, emission = that map x 1.4 like the palette kit) and puts it on the house-textured pieces. The house's
  own material is untouched (git: no diff). Play pictures: `kit_renders/glow_2026-11-23_unity.png`; zoom of the map
  on the texture: `glow_2026-11-23_map_zoom.png`.
- Border hugging (the giant pumpkin skips its charge when pressed against the border towards the player,
  PumpkinBallBoss.cs:485) can be fixed on gate 3's side: `boss.chargeAlong` in BallValleyGate.SetUpBoss (charge along
  the wall). Not built: waiting for the user.

## 3e. THE NEW FLOW (user, 2026-11-23 afternoon, K: PC): replaces sections 3's rounds, chests and giant pumpkin

User: "we open our eyes in middle of a field, crazy balls ... candy tree, jello volcano, boulder ... The field gets
smaller as time go ... a 5 minutes song ... faster and faster till ... 6 holes meet each others and kill the Player ...
push all the balls ... the last boss Black Pumpkin Ball only comes out when all other 15 balls are pocketed ... at least 3
times heavier ... broken by Player or push into hole ... rain candies and create chest that contains The Seed inside ...
No need for other seeds in this gate game." Built, compiled (0 errors), Play-tested through GAMESTAGES; not in the headset.
- **No Gate Run on Ball_Valley** (Set Up Ball Valley removes it): its start chest, rounds and gear chests aren't this
  gate's. `BallValleyGate` does the card ("RUN AND SNOOKER / Push all 15 balls in before the holes meet."), its own HUD
  line (BALLS n/15, then BLACK PUMPKIN hp; seconds left under 60), ScriptedMoveVignette.Launches while it runs, and keeps
  The_Seed from the purple hole (KeepFromPurpleHole). Gate Exit (shared) sets the 4 gear bools on the way out.
- **The flow:** card -> 15 balls drop in at once (4 s) -> the song: the field shrinks over 300 s from 1 to 0.1
  (size = lerp(1, 0.1, k^2.2): 150 s 0.80, 240 s 0.44, 270 s 0.29), bots hunt each other (botsHuntBots on) and the
  player (hunters 1 -> 3, tempers 0.45-0.65 -> 0.8-1); a pocket counts if the player touched the ball within 6 s
  (punch, dash, body, or a ball they hit: BallPlayer.lastHumanTouch, a chain keeps the first touch's time): gone + 3
  candies out of the hole; else it drops back in after 3-6 s (stage.DropBackIn) -> all 15: loose tail balls puff away,
  the Black Pumpkin Ball drops in (the kit's jack-o'-lantern x tint 0.13/0.1/0.15, r 1.2, Mass Times 3 = 6.75 vs 2.25,
  150 health by EnemyHitDamage.Judge like the giant pumpkins, the dash 10, a punch keeps 1/3 of the fist's push) ->
  broken or pocketed: the clock stops, the music fades, 30 EXP, 42 candies rain on the lawn over 3 s, the main game's
  `Items/Chest/Treasure_Chest_and_Seed` drops 1.6 m in front of the player -> The Seed eaten (or after 90 s set for
  them) -> GateExit -> Rooftop_ON PG -> Whack 6. The song runs out first: the holes have met (lawn 5 x 2.5 m, all pit),
  the player dies with no revive (Daruma's way: UseAllRevives, PlayerHealth 0, DamageToPlayer), waiting while a revive
  or coin toss is under way, retried up to 3 times -> Game Over -> a new run.
- **Song:** `Pumpkin_Playroom` (150 s, chill) then `Spider_Queen_Rampage` (180 s; the last part loops), pitch 0.92 ->
  1.3 with k^1.4, volume 0.5, paused with the game. No 5-minute song exists yet: swap it in on Ball Valley Gate's Song.
- **The field:** 5 candy trees anywhere (90 health x size; each hit knocks a candy out, 10 per tree; broken: 5),
  6 boulders (160 x size; broken: 8 candies), 3 jello volcanoes (`BallValley_Kit_Volcano.blend` by
  `tools/volcano_piece.py`, 256 tris, palette; never break; spit 2-4 candies every 7-13 s / Hurry 1 -> 2.2, and 1 when
  hit), slimes 5 -> 8. Player punches hurt trees/boulders/volcanoes 40 (perfect x1.5), the dash 120.
- **Code (gate 3 files only):** BallValleyGate.cs (rewritten), new JelloVolcano.cs + BlackPumpkinBall.cs;
  BallValleyStage (driveScale/drivePending, DropBackIn, treesAnywhere, volcanoCount, treeHitCandies, kitVolcanoes,
  SpawnVolcano), BallPlayer (massTimes, lastHumanTouch, dressHead, face-less LateUpdate guard), Props (hit candies,
  onHit), BallValleyPlayer (prop punches/dash, Heavy, credit), menus (Set Up removes Gate Run and fills the gate;
  Build Kit gives the volcano + black look; Watch resets the gate's settings). Backups:
  `K:/WORK_2026/08_04/_claude/gate3_2026-11-23/backup_before_flow/` (prefabs, the old BallValleyGate from HEAD).
- **Tests** (`K:/WORK_2026/08_04/_claude/gate3_2026-11-23/flow/`, `_claude_bot`): `g3_flow.cs` (god mode): Clean for
  Path 3 -> card -> 15 balls (5 trees, 6 boulders, 3 volcanoes) -> a bots' pocket back in after 3.5 s, not counted -> tree
  +1 candy, boulder broken by 4 punches +8, volcano +1 -> clock 150: 0.799, pitch 1.07 -> clock 240: 0.441, hunters 3 ->
  15 pocketed -> black ball r 1.20 mass 6.75, look on, loose balls 0 -> hit 50 -> 99 -> broken -> +38 candies on the lawn,
  EXP +30 (level 0 -> 1), chest at (-5.1, 0.1, 3.2) -> opened -> seed eaten -> Whack 6 in 5.4 s: gears TTTT, The_Seed,
  valley + gate gone, fog on, far 40, vignette off. Frames (Editor) 12.3 ms average, 3 over 100 ms (the clock jumps).
  `g3_collapse.cs` (no god mode): clock to 290 -> falls took the player to -5 at 299.5 s (coin revive) -> at 300 the kill
  waited, then Game Over -> Recreate -> Walk Tuttorial (a new run). The first try (kill during the revive) stuck at health
  0 with no Game Over: fixed by the wait + retry. Pictures: `kit_renders/flow_2026-11-23_gate3.png`.
- **Seen, kept:** the seed chest opens on any bump (a slime or falling candy opened it once): the seed still waits.
  Black ball damage 51 for a hit of 50 (HitSettings.Whole rounding).
- **Not done / open:** the headset run; a real 5-minute song; the user's balance calls (15 balls in 300 s, hunters 1-3,
  the kill at the end); the mountains (M1/M2/M3); the old giant-pumpkin code paths in BallValleyPlayer (gate.Boss is
  always null now) are harmless; `gates_2026-11-23/t4_gate.cs` TG_PATH 3 uses Gate Run's hooks: use `flow/g3_flow.cs`.

## 4. Requests to the coordinator

- `requests/gate3-dash-vignette.md`: the comfort vignette during scripted moves (gate 3's dash, PlayerLaunch throws).
- `requests/gate3-gateexit-timeout.md` (2026-11-23): a hard timeout on GateExit's rooftop wait (GAMESTAGES to
  Rooftop_ON PG after 45 s).
- `requests/gate3-new-flow-info.md` (2026-11-23): gate 3 no longer uses Gate Run (the user's flow; contract #2 and #8
  changed by the user); nothing to apply, information for Phase 2 and the end-to-end check.

## 5. Headset checklist (for the user, the new flow)

1. Arrival: black-out, eyes open in the middle of the field, the card: is the chaos (15 balls, slimes) fun or too much?
2. Snooker: punch a ball (or send one into another) towards a hole: does BALLS n/15 count it, and does a ball the others
   knock in coming back from the sky read clearly?
3. Candy trees (a candy flies out per hit), boulders (4 punches), jello volcanoes (spit every 7-13 s): readable, fun?
4. The song and the shrink: chill at first, faster at the end? Is 5 minutes for 15 balls fair? The holes meeting.
5. The Black Pumpkin Ball: does it feel 3 times heavier (punches move it a third as far)? 150 health by punches; or
   pocket it.
6. The end: candy rain, the chest in front of you (punch it open), eat The Seed: to the rooftop.
7. The dash and its comfort vignette; shoves; the frame rate on Quest (15 balls + tails, slimes, 3 volcanoes).
