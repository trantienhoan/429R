# Gate 3 (Ball Valley): hand-over notes

Session: "Gate 3: Ball Valley" (2026-11-22/23). Plan: `Exports/Gates/GATES_PLAN.md` (§0 order, §2 contract, §4 brief).
Work files and backups: `C:/Users/ADMIN/Documents/429R_claude_backups/2026-11-22/gate3/`.

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

## 4. Requests to the coordinator

- `requests/gate3-dash-vignette.md`: the comfort vignette during scripted moves (gate 3's dash, PlayerLaunch throws).

## 5. Headset checklist (for the user)

1. Start: the card, the Bracer chest in front of you (punch it open, eat the seed): is round 1 starting quickly?
2. Snooker: punch a ball towards a hole: does it roll far enough to go in? Does your own pocket feel good (ka-ching,
   3 candies out of the hole)? Is "BALLS n/5" readable?
3. The dash: fists touching at the chest for 1 s (a glow, then a "ready" tone), pull them apart: does it fire when you
   mean it and never from a boxing guard? Looking down at your fists: does it still go where you face? The vignette
   during the dash and shoves (your comfort level): enough, too much?
4. Shoves: a ball crashing into you, the giant pumpkin's charge: too far, too often?
5. Holes from the lawn: rune rings and jack-o'-lanterns readable? The shrink between rounds (earth rumble): does the
   push away from a moving hole feel fair?
6. The giant jack-o'-lantern in the small field: dodge its charge so it slams the border (dizzy), punch it.
7. Look: the kit at night in the headset (too dark? the mountains?), and the frame rate on Quest.
