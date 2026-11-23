# Gate 2 (Slime Bowling): hand-over notes

Session: gate 2, started 2026-11-22 night. New order from the coordinator 2026-11-23 (`../GATES_PLAN.md` §0):
1 kit, 2 logic fixes, 3 GateRun (waits for the coordinator), 4 fun and pacing, 5 kit into Unity + full run.

## 1. The SlimeBowl kit (step 1, done, waiting for the user's fixes)

- `SlimeBowl_Kit.blend`, built by `tools/slimebowl_kit.py` (headless, ~10 s):
  `blender -b --factory-startup --python Exports/Gates/gate2/tools/slimebowl_kit.py -- Exports/Gates/gate2/SlimeBowl_Kit.blend`
  An existing .blend is never overwritten without `--force` (the new one goes to `*_generated.blend`). Once the user
  edits the .blend it's theirs: re-run only with `--force` on purpose.
- Renders next to it: `arena_concept_angle.png` (the concept's camera), `arena_eye_level.png` (from the island, down
  lane 0), `kit_sheet.png` (every piece, real size and triangles in the label).
- 23 pieces, `SlimeBowl_Kit_<Piece>`, one collection each, pivots on snap points, the game's real metres
  (BowlingLayout.cs): Island (5 x 5 m top at 0.02), Approach_Stone, Vine (ball cup at 0.8 m: balls at hand height),
  Lantern_Post, Pumpkin_Small, Lane_End (1.2 m), Lane_4m, Lane_Arrows, Rail_4m (the 0.1 x 0.2 m rail), Trestle,
  Deck (r 2 m stone stage at 21.3 m, cut where the wood ends at 19.6 m), Rune_Ring_Green/Red/Blue/Yellow (r 1 m),
  Brazier, Flame (a mesh: no particles), Pumpkin_Arch (behind the deck, 1,745 tris), Slime_Pin (0.66 m),
  Lantern, Chain_Rock, Floating_Rock, Cloud_Puff. Placement table: the script's header.
- One palette: `textures/SlimeBowl_Palette.png` (alpha = gloss for URP smoothness) + `_Emission.png`, 31 swatches,
  UVs set by the script (palette_kit.py method).
- `Sample_Arena` and `Kit_Sheet` collections are not exported. `Sample_*` objects (balls, giant slime, moon) are
  stand-ins, not kit pieces.
- Not done: the export (FBX to `Assets/Models/Gates/SlimeBowl/`, palette to `Assets/M_Textures/Gates/SlimeBowl/`) and
  the `Tools > 429 Game > Gates > Build SlimeBowl Kit` menu: after the user's fixes (step 5). Ideas for then: the deck
  stone reaches 23.3 m (the floor collider ends at 22.6 m: extend it), Flame + Brazier replace the 8 Danger_Flame
  copies, a gate 2 giant pumpkin look (bowling-ball pumpkin with finger holes).

## 2. Logic fixes (step 2, done, editor-tested 2026-11-23)

Changed (backups: `Documents/429R_claude_backups/2026-11-23/gate2/backup_before_step2/`):
- `BowlingStage.cs`: the giant pumpkin comes through `GateBossFinale.Begin` (lane hooks in its set-up; the seed spot
  kept on the dirt) and the exit is `GateExit` only. Gate 2's own exit copy is gone (ToTheRooftop, GearGuard,
  RooftopSpot, MovePlayer...). The arena rides under the finale's object, so GateExit takes it out of
  CurrentlySpawnedStuffs (the floor stays under the player) and removes it after the move. When the new rooftop is up
  the arena's Fall Edges go off and fog/moving/return spot are given back. (Since step 3 the finale comes through
  GateRun's `Finale`, which calls the same GateBossFinale.)
- The boss's candies (its Break Candies, same number) drop on the dirt beside the player, clear of The_Seed.
- Thumbstick back for the boss fight; `arenaCentre` follows the boss's hops.
- Bowl at the boss: a ball and the boss meeting at 3 m/s or more = +4 damage (8 dizzy) on top of the normal hit.
- The thumbstick movers are looked up once, not every 0.5 s.
- `BowlingRollAssist.cs`: new `LaneAim` (point + steer) and `BallHit` hooks. A bowl whose line would cross the lane's
  mouth more than 0.6 m off-centre (it rolled off the dirt) is steered into the lane.
- `BowlingArenaSetupMenu.cs`: a re-run keeps every Bowling Stage value it doesn't build (finale boss, strikes add up,
  rules, numbers). Re-run once on 2026-11-23: "kept 60 values". Its own budget warnings (13 lights, particle max 1,454)
  are old: the kit replaces the fires.
- New `Editor/BowlingPlayMenu.cs`: Tools > 429 Game > Bowling > Play Bowling (Play mode).

Measured (editor, `_claude_test`, deleted after):
- Clean for Path 2 -> Path 2 ON 1 s; start chest 1 s later; slime 4 s after the seed; first rack 24 s after the seed
  (27 s after arriving, + the time to open the chest).
- 4 lanes lit: 659 particles alive (53 systems), 5 lights on, 4.0 ms editor frame.
- Boss: drop 1.4 s, wind-up 1.6 s, charge 1.5 s, dizzy at 3.9 m out, hop 1.2 s: lane mode works after the 11-20 aim lock.
- Win -> seed -> Rooftop_ON PG -> Whack 6 in 5 s; player 3.5 m south of the roof's middle; arena gone; fog back to
  6-22; GateExit.Running false; 0 console errors; no gear warning.

## 3. On GateRun (step 3, done, editor-tested 2026-11-23)

- `Bowling_Arena` has `GateRun` (card "BOWL AND FIGHT / Strike the slime pins. Fight what hatches.", the shared Bracer
  + Chest/Head/Shield chests, 120 s rounds, finale boss = the shared giant pumpkin until gate 2's own variant
  `Prefabs/Bowling/Pumpkin_Ball_Boss_Bowling` exists, The_Seed). Made by `Tools > 429 Game > Bowling > Gate 2: Use
  Gate Run` (new `Editor/Gate2RunMenu.cs`, re-runnable: an existing Gate Run keeps its values; it also moved the old
  start pace: no laps, 5 s descent, 1 s delay, racks every 8/12/16 s, and removed the old Rules Sign).
  `Set Up Bowling Arena` calls the same and keeps the old Gate Run's values.
- `BowlingStage` implements `IGateGame`: GateStart = first vines/balls + the slime comes; RoundBegin(1..3) = that
  round's slimes (1, 2, 4), `run.Goal("STRIKES", n)`, more vines from round 2; strikes call `run.Progress`, all slimes
  hurt = `run.RoundWon()`; round 3's small slimes pop on 1 strike (Round Strikes 3/2/1); RoundTimeUp hurts the rest;
  FinaleBegin = `run.Finale(laneEnd, 1.5 s, lane hooks, seed on the dirt)`. `run.chestSpot` keeps chests on the dirt.
- Gone from gate 2 (GateRun or the new round 3 does it): its own chest drops and seed watching, the rules sign, the win
  sign, the old phase 3 (wing loss) and the 4-slime showdown. The code is in git (b568870) and in
  `Documents/429R_claude_backups/2026-11-23/gate2/backup_before_gaterun/`. `Slime_Showdown_*` prefabs are now unused.
- Kept for `GateFinalesMenu` only (hidden): `pumpkinFinale`, `finaleBoss`, `rulesText` (request
  `requests/gate2-gatefinales.md`).
- Test run (GAMESTAGES "Clean for Path 2", `_claude_test`, strikes simulated 3 s after each rack, seeds eaten 2.5 s
  after each chest): card + start chest 2.5 s; first rack 9.5 s after the start seed; round 1 44 s (racks every 16 s
  then: now 8 s), round 2 28 s, round 3 12 s; 4 chests dropped (Shield too); giant pumpkin 1.5 s after the Shield seed;
  The_Seed auto-eaten after 20 s; Rooftop_ON PG -> Whack 6; 0 console errors. Round 3 busiest: 5.0 ms editor frame,
  1,009 particles (budget 650: the kit's flame meshes replace Danger_Flame).

## 4. The kit into Unity (step 5, done 2026-11-23, editor-tested; pictures `unity_*.png` here)

- Kit rebuilt 2026-11-23 01:15 with a 24th piece, `Boss_Pumpkin` (gate 2's giant pumpkin: a bowling-ball pumpkin, its
  three finger holes are its eyes and nose, a carved grin; radius 1 m, the game grows it).
- Export: `blender -b Exports/Gates/gate2/SlimeBowl_Kit.blend --python Exports/Gates/gate2/tools/export_slimebowl_kit.py`
  -> `Assets/Models/Gates/SlimeBowl/SlimeBowl_Kit.fbx` (one mesh per piece, pivots kept) + the palette PNGs in
  `Assets/M_Textures/Gates/SlimeBowl/`. Never saves the .blend.
- Then `Tools > 429 Game > Bowling > Build SlimeBowl Kit` (`Editor/SlimeBowlKitMenu.cs`): import settings, material
  `Materials/Bowling/SlimeBowl_Palette` (URP Lit, gloss from the palette's alpha, glow map), piece prefabs in
  `Prefabs/Bowling/Kit/`, gate 2's giant pumpkin variant `Prefabs/Bowling/Pumpkin_Ball_Boss_Bowling` (set as Gate Run's
  Finale Boss), and the arena dressed (placeholder looks hidden, every collider kept; the Danger_Flame columns replaced
  by brazier flame meshes with `KitFlame`; balls wait in the vine cups at hand height: `KitSprout` pops a vine up when its
  spot opens, a 3-sphere cradle holds the ball, the spots are raised). `Remove SlimeBowl Kit (placeholder look)` undoes it.
- BowlingStage: the fog takes a dusk violet while the stage runs (`tintFog`, given back on exit); the 2 hidden fields for
  Gate Finales are gone (the coordinator's menu now leaves gates with Gate Run alone).
- Every palette colour glows a little (`BASE_GLOW` 0.16 in the kit script): the night arena was too dark in Unity.
- `Set Up Bowling Arena` re-applies the kit after a full rebuild (when the FBX exists).
- Run once (2026-11-23): 153 kit parts, 8 ball vines (balls rest at 1.01 m, their middle), 8 flames; the arena's particle
  max sum 1,454 -> 142; Gate Run's Finale Boss = `Pumpkin_Ball_Boss_Bowling`. Play test (Play Bowling): balls wait in
  the cups, lanes light with rune rings and flames, a round that ran out of time moved on, the giant pumpkin dropped,
  charged and got dizzy 4.1 m out, broke; 0 errors.
- To change a piece: edit `SlimeBowl_Kit.blend` (it's yours now: re-run the kit script only with `--force` if you want
  it rebuilt), run the export script, then `Build SlimeBowl Kit`.

## 5. Open

- DONE 2026-11-23 (K: PC, user: "add sounds, clean up"): strike = `Clear,_resonant_bell_1`, miss (a ball reached the rack,
  no strike) = Kenney `lowThreeTone`, played at the player (BowlingStage Strike/Miss Sound; Gate 2: Use Gate Run fills them).
  The 4 `Slime_Showdown_*` deleted (GiantSlimeSetupMenu no longer makes them). Giant pumpkin candies 42 (E2).
- The user's fixes to the kit (look, colours, sizes), then export + Build SlimeBowl Kit again.
- The deck's stone reaches 23.3 m but the lane's floor collider ends at 22.6 m (a ball rolling past the pins drops
  through the stone's last 0.7 m into the fall edge). Small; fix with a deck collider if it shows in the headset.
- (Done 01:40) Full GAMESTAGES run with the kit, `_claude_test`, strikes simulated 3 s after each rack: round 1 36 s,
  round 2 26 s, round 3 12 s; gate 2's own giant pumpkin; seed -> Rooftop_ON PG -> Whack 6; arrival to seed 109 s;
  fog back to 6-22 and the house colour; 4 gears set; 0 errors.

## 6. K: PC gate 2 session, 2026-11-23 morning: groundwork (no edits to the game)

- Pictures: `compare_concept_kit_unity.png` (concept | kit in Blender | Unity from the concept's camera | Unity from the
  island), `unity_concept_angle.png`, `deck_gap.png` (green = lane floor collider to 22.6 m, red = stone with no floor
  22.6-23.3 m, right in front of the pumpkin arch's portal). Scripts: `K:/WORK_2026/08_04/_claude/gate2_2026-11-23/`
  (`g2_look.cs` run by `gates_2026-11-23/run_test.sh`, `deck_gap.py` headless, `side_by_side.py`).
- Measured (editor, `_claude_bot`, chests skipped by the hook): card 2.5 s, start chest 3.0 s, round 1 at 4.8 s, first rack
  9.2 s into round 1 (14.0 s after arriving). Round 1 at 22 s: 59 particles, 3 lights, 2 racks. Round 3 at 20 s: 167
  particles, 5 lights, 4 racks, 5 flyers; at 35 s: 284 particles (budget 650), 4 racks, 4 balls; editor frame 8-9 ms avg.
- Brightness (0-1 mean): concept 0.30, Blender kit 0.17, Unity same camera 0.08 (fog off), Unity from the island 0.13.
- Seen: the start rooftop's tutorial boards stand on lanes 2/3 about 8 m out (thin slivers from above; the TELEPORT board
  behind the giant pumpkin in `unity_giant_pumpkin.png`): shared scene, the coordinator's. Gate Run's round start/won
  sounds are empty in all 3 gates.
- Waiting for the user's choices (model, flow, rewards, saving, ending).

## 7. Hand set up (2026-11-23 morning, K: PC; user: "no portals at the end, only fall edge, ball goes to fall edge =
## break ... wider path, bigger pins ... I set them up my style"; "Yes, one lane prefab")

- **One lane prefab:** `Prefabs/Bowling/Bowling_Lane.prefab` (made from lane 0 by `Tools > 429 Game > Bowling > Hand Set
  Up > Make One Lane Prefab`); the arena's 4 lanes are its instances (each keeps its turn, index and rune ring colour:
  Red/Blue/Yellow as overrides). The 4 lanes now share lane 0's small decorations. Undo: `Back To 4 Separate Lanes`.
  Arena prefab 704 KB -> 213 KB. Backups: `Exports/Gates/gate2/backups/` (the menus make one before each change).
- **Hand Set Up tick** on Bowling Stage (on): `Set Up Bowling Arena`, `Build SlimeBowl Kit` and `Remove SlimeBowl Kit`
  refuse to run (tested). Kit shapes now come in with `Hand Set Up > Import SlimeBowl Kit Pieces (no placing)`: import
  settings, the palette material, a prefab for every NEW piece; nothing placed or moved (the placed pieces are prefab
  instances whose mesh is the FBX's, so they change by themselves).
- **The lane's size is read from Unity, not from fixed numbers** (`BowlingLane`: Floor collider = width, start, end;
  Aim/slot 0 = head pin): the strike watch (PinSet: head pin = slot 0, half width = the floor's), the roll help
  (BowlingStage `mouthMargin` 0.4 m inside the floor's edge replaces `mouthSafe` 0.6), the giant pumpkin's lane runs and
  landing spot. Select a lane: green = its floor as the game reads it, yellow = the head pin line.
- **Fall edge = break:** a ball (not held) over any Fall Edge breaks at once (`BowlingBallSupply` Break At Fall Edge: a
  puff x1 and `pumpkin_explode_1`), a new one grows in a cup. `Fit Fall Edges To Lanes` re-fits the 12 boxes round the
  lanes' floors and the dirt (same boxes as before for 2 m lanes: checked).
- **Export fix:** `tools/export_slimebowl_kit.py` keeps a piece's Object Mode size/turn (was reset to 1/0 and lost).
  Tested: Lane_4m scaled 1.5 in X came out 3.0 m wide.
- Compile-checked (0 errors, runtime + editor). Play test (`_claude_bot`, `g2_hand.cs`, GAMESTAGES "Clean for Path 2"):
  lanes read 2.00 m, 2.40-22.60 m, head pin 20.80 m; a real ball rolled down lane 0 at 8 m/s reached the rack (miss: 0
  strikes, miss tone), ran on and broke at 22.6 m (`pumpkin_explode_1`, 4.9 s after the roll); a ball dropped beside lane
  0 broke in 0.11 s; rounds by the hook -> giant pumpkin -> 42 fountain -> Whack 6, 67 s; 0 game errors.
- **Next:** the user edits `SlimeBowl_Kit.blend` (wider lane, no portal, deck end), then: export -> `Import SlimeBowl Kit
  Pieces` -> the user sets the lane up by hand (Floor width, rails, slots, `Slime_Pin` Start/Full Size) -> `Fit Fall
  Edges To Lanes` -> Play test. Then the user's details on the flow.

## Headset checklist (draft)

- The card and the HUD line (STRIKES n/3, n/4) readable; chests land on the dirt in front of you.
- Balls waiting in the vine cups at hand height: easy to grab, don't fall off by themselves.
- The night look: bright enough? the violet fog, the flames, the rune rings, the pumpkin arches.
- Bowling feel at 18 m; balls rolling into the lane from beside its mouth; round 3's pops feel quick.
- Fighting what hatches while bowling (too many flyers in round 3?).
- The boss on the lanes: dodge the charge, punch it dizzy at the dirt's edge, bowl at it (10 a dizzy hit).
- Its candies land round you on the dirt; the seed in front; the rooftop after.
