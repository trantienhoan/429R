# Gates plan: 3 gates, 1 ending, then replay (2026-11-22)

**Status 2026-11-23:** Phase 0 and Phase 1 done: all 3 gates run on GateRun with their kits in Unity, each tested
to Whack 6; the end-to-end check (E6, all 3 routes to the boss and a new run) passed; E7 fine (no bee swarm).
Nothing committed after the user's baseline b568870; no scene change. Open: the headset runs, the kits' fixes, and
the user's calls (E2, E3, E4, E5 and each gate's questions in `gateN/NOTES.md`).
Made from: `Assets/NOTES_Next_Time.md`, Claude's memory, the gate code, and GAMESTAGES read from the saved scene
(`2 Game Scene.unity` on disk, last saved 2026-11-20 21:48; unsaved editor changes, if any, are not in it).

## 0. UPDATE 2026-11-23 (the user's go): read this first

User: "For each gate create a kit for easy change and fix. Make the flow simple and resemble for 3 gates, only the
gameplay is different a bit for each gate: cook and defend, bowling and fighting, running and snookering. Feel good and
having fun is what I want for players."

**Concepts (approved by the user):** gate 1 `Exports/PlayGround_Concept/concept_v1.png`, gate 2
`Exports/Bowling_Concept/concept_v1.png` (the Slime Bowl), gate 3 `Exports/BallValley_Concept/concept_v1.png` (the
haunted pool table). The concepts set the look; the game's real layout and sizes win where they differ (e.g. Ball
Valley's 6 holes stay at the 4 corners + the 2 long-side middles).

### One flow for all three gates (the gate template)

```
ARRIVE   fade in; the gate's card: title + one short line (after any other notice)
START    the start chest drops in front of the player; its seed (Bracer) starts the gate
ROUND 1  the gate's verb, with one goal counting up on the HUD line  -> the Chest gear chest
ROUND 2  the same verb, a bit harder                                  -> the Head gear chest
ROUND 3  the same verb, hardest                                       -> the Shield gear chest
FINALE   the gate's giant pumpkin (its own look) drops in: break it
EXIT     The_Seed -> GateExit -> the rooftop (shared)
```
- The same in every gate: the card, the HUD line (one line under the view, a short flash on big moments), the chests
  (the same chest prefabs = the same candy + EXP), a round starts when the last chest's seed is eaten (or after 20 s),
  every round has a time limit that ends it anyway (never stuck), a whole gate takes about 6-8 minutes.
- Only the verb differs:
  - **Gate 1, cook and defend:** the cook is split into 3 rounds; each round ends with its slime mini boss; beat it for
    the chest. The pot bursts after round 3.
  - **Gate 2, bowl and fight:** round 1 one slime (3 strikes splits it), round 2 two slimes, round 3 four small slimes
    (1 strike each: fast and fun); fight what hatches. Then the giant pumpkin bowls down the lanes.
  - **Gate 3, run and snooker:** pocket 5 balls a round (15 in all) while the field shrinks a step each round; the
    round's timer ends it anyway. Then the field shrinks to its smallest for the giant pumpkin.
- Feel good: quick start (something to do within 10 s), clear goal, juicy hits and pops (the user's own clips), candy
  bursts, no long waits, readable from eye height, comfort first in VR.
- **Shared code:** the coordinator builds `Scripts/Gates/GateRun.cs` (card, HUD line, start + round chests and their
  seeds, rounds and their timers, the finale hand-off). Each gate keeps its own gameplay and calls GateRun for the
  rest; the coordinator messages each gate session when it is ready (with the API).

### GateRun: the shared flow (`Assets/Scripts/Gates/GateRun.cs`, ready 2026-11-23)

Put `GateRun` on the gate's stage root (your own prefab) and implement `IGateGame` on your stage component:

| Your method | When | You do |
|---|---|---|
| `GateStart(run)` | the start chest's seed is eaten (or 25 s passed) | set the gate up (must also work if the seed was never eaten) |
| `RoundBegin(run, round)` | round 1, 2, 3 starts | start its play: `run.Goal("STRIKES", 3)`, `run.Progress(n)`, `run.RoundWon()` |
| `RoundTimeUp(run, round)` | its time limit ran out | tidy up (it counts as won, the chest still drops) |
| `FinaleBegin(run)` | round 3's chest seed eaten | `run.Finale(landAt, delay, setUp, seedSpot)` (GateBossFinale + GateExit inside) |

- Fields on GateRun: the card (title + one line), the start chest and the 3 round chests (added in the editor: the
  shared Bracer chest and the PG Chest/Head/Shield chests), the gear bools, round time limits (150 s each), the finale
  boss (set YOUR variant) and The_Seed, the HUD line, the user's sounds for round start/won.
- Optional: `run.chestSpot = () => floorPoint` (e.g. keep chests on the island), `run.Flash(text)`, `run.Line(text)`
  (your own HUD text; null = back to the round line), events `RoundStarted`, `RoundEnded`, `FinaleStarted`.
- Right-click GateRun in Play: "Test: Win This Round", "Test: Stop Waiting For This Chest", "Test: Skip To The Finale".
- Remove from your gate what GateRun now does: your own rules notice, HUD line, start/phase chest drops and seed
  watching, your own exit copy (gate 2) and gear guard. Keep your gameplay.
- Never stuck: a round that runs out ends anyway; a chest seed not eaten in 25 s lets the gate go on (Gate Exit eats it
  for the player at the end); a missing finale drops the giant pumpkin in front of the player; a giant pumpkin that
  vanishes without breaking still drops the seed (GateBossFinale).

### A kit per gate (easy to change and fix)

- `Exports/Gates/gateN/<Name>_Kit.blend` (Name: PlayGround / SlimeBowl / BallValley), built by
  `Exports/Gates/gateN/tools/<name>_kit.py`: re-runnable, never overwrites the .blend without `--force` (once the user
  edits it, it's theirs). One collection per piece, pieces named `<Name>_Kit_<Piece>`, pivots on snap points, real
  metres from the game. One palette texture + material per gate (`Exports/Remodel/tools/palette_kit.py` method). The
  user's clean mesh style (closed, fewest quads round openings, separate trim pieces).
- A `Sample_Arena` collection (not exported) lays the pieces out like the concept, and renders (`kit_sheet.png`, and the
  arena from the concept's angle) go next to the .blend for the user to compare with the concept.
- Piece lists: Prompt 3 in each gate's PROMPTS file (gate 1: `Exports/PlayGround_Concept/PROMPTS.md` Prompt 3 and
  `BUILD_LIST.md`).
- Export by script to `Assets/Models/Gates/<Name>/` (textures `Assets/M_Textures/Gates/<Name>/`); then a re-runnable
  `Tools > 429 Game > Gates > Build <Name> Kit` menu makes one prefab per piece and dresses the stage with them,
  keeping the gameplay colliders and sizes.
- Headless Blender (`blender -b`) for builds and renders; the live Blender only with `BLENDER_LOCK.txt`.

### New order for each gate session
1. The kit: script, .blend, renders; show the user and take their fixes.
2. Logic fixes from your brief that don't depend on the flow (measure first if Unity is free).
3. When the coordinator says GateRun is ready: move your gate onto it (card, chests, rounds, HUD, finale).
4. The rest of your brief (fun and pacing) on the new rounds.
5. The kit into Unity (export, prefabs, dress the stage), then the full run to Whack 6.
New folders each gate owns: `Assets/Models/Gates/<Name>/`, `Assets/M_Textures/Gates/<Name>/`, `Exports/Gates/gateN/`.

## 1. Where the gates stand

| | Gate 1: PlayGround | Gate 2: Slime Bowling | Gate 3: Ball Valley |
|---|---|---|---|
| **Play today** | Find the hat, cook one dish (5 min; logs speed it up), defend the pot from waves; 4 slime mini bosses drop the gear chests | A Giant Flying Slime poops 6-pin racks on 4 lanes; 3 strikes (they add up) hit a slime and split it; 2 phases | Push 15 bot balls into 6 holes while the field shrinks; eat candy; two-fist dash |
| **Length (estimate)** | 5-9 min | 4-5 min | 5-7 min |
| **Its giant pumpkin** | Radius 1.5, 150 hp, 42 candies | Radius 1.3, 100 hp, 14 candies, rolls the lanes | Radius 2.6, 150 hp, 14 candies |
| **Tested** | Editor only (11-18); the purple hole and balance changes since then never tried here | Editor only (11-18); lane mode not re-tested since 11-20 | Editor only (11-18) |
| **Look** | Old PlayGround + slime repaint; concept art exists, nothing built; the house fog stays (dark) | All placeholder (cube lanes, code-built dirt); concept prompts only | BALLS web-game art + painted Halloween sky; no concept art |
| **Worst problems** | A setup menu re-run silently drops the rooftop finale; no first-step hint; logs bunch all 4 mini bosses | Its own copy of the exit (not GateExit); the Shield chest never drops; a setup menu re-run brings back the old finale; ~30 s idle start; particles ~2x budget | Pockets don't matter (the boss comes at ~194 s anyway); holes hard to see; no gear chests; no fallback if the boss vanishes |

None of the three has been played through in the headset. Full findings (code facts with line numbers) for each gate:
`gate1_findings.md`, `gate2_findings.md`, `gate3_findings.md` in the shared notes folder ("the notes folder" below),
copied with its `README.md` and `requests/done/` to `Exports/Gates/session_notes/` on 2026-11-23 (the folder itself is
on the second PC only).

## 2. The one ending (every route)

### Today (checked in the saved scene)

All four routes (the house + 3 gates) already meet at the rooftop and loop back to the start:

```
Gate N stage -> its giant pumpkin (PumpkinBallBoss via GateBossFinale) -> The_Seed (GateExit)
  -> Path N ON sees the 4 gear bools + The_Seed -> (gates 2/3: Enemy 4 - Get the Seed)
  -> Tween PlayGroundZ -> Destroy PlayGroundZ (+ Clear Spawned Stuffs)
  -> Rooftop_ON PG --+
House route -> Rooftop_ON -+-> Prepare_Whack_6 (CP Rooftop) -> Whack 6 (bees + Bee_Boss)
  -> Whack 7 (bees + Bee_Boss_PumpkinButt) -> Damage The Lid (CP Boss) -> the roof smash
  -> BOSS (Pumpkin_Boss_Z) -> fairy reward (Win) -> Thank For Playing (song, waits for GAMEOVER)
  -> clean all (StartNewRun) -> Restart Loop (resets the gears, The_Seed, BOSS, whack_6/7, Pumpkin_N)
  -> Walk Tuttorial -> Stage 1 - The Start (the house, or stand 3 s in a bought gate's circle)
```

- A new run keeps the level, candy and bought gates (Cube_N). It resets the checkpoint, revives and gloves.
- Gate_N is only "standing in the circle", so a replay never forces the same gate.
- A death in a gate: revive coins work in place; a Game Over starts a new run (gates have no checkpoint).
- A demo build stops at every gate (Demo? Path N -> Demo End).

### The gate contract (keeps the ending one)

Every gate session keeps these. The coordinator checks them in Phase 2.

1. **In:** Path N ON spawns the stage, moves the player, fades in. One rules notice: a title plus one short line.
2. **Middle:** 2-3 phases with a goal the player can see counting down. The start chest gives Bracer; each phase ends
   with one chest (Chest, Head, Shield gear).
3. **Finale:** `GateBossFinale.Begin(...)` brings the gate's giant pumpkin. No other boss code.
4. **Out:** `GateExit` only (The_Seed, gear guard, black, rooftop, player 3.5 m from the tree). No exits of its own.
5. **Clean:** everything spawned sits under CurrentlySpawnedStuffs or the stage root. Global changes (fog, sky, far
   clip, teleport floor, statics, time scale) are put back on exit AND on Game Over / OnDestroy.
6. **Never stuck:** every wait has a fallback (like the chest seeds' 300 s auto-give and GateExit's 20 s seed).
7. **Same arrival power:** each gate hands over about the same candy + EXP before the rooftop (one budget: E2, the
   user's number).
8. **All four chests in play:** Bracer, Chest, Head and Shield all drop during a normal run. GateExit's gear guard is
   only a safety net: it logs nothing in a normal run.
9. **Survives a death:** a revive with the coins in the middle of the gate doesn't break it (the purple hole eats loose
   things within 14 m of the shield, unless their names contain Boss, _Base, Skydome, Valley or Island).

### What to improve in the ending (owner: the coordinating session)

- **E1 Three looks for the giant pumpkin.** All 3 gates point their `finaleBoss` at the same
  `Prefabs/Gates/Pumpkin_Ball_Boss` (the bowling pumpkin, grown). Each gate session makes ITS OWN prefab variant of it
  in its own prefab folder (`Prefabs/PlayGround/`, `Prefabs/Bowling/`, `Prefabs/BallValley/`) and points its stage's
  `finaleBoss` at it, so it doesn't feel like the final boss twice: e.g. gate 1 a stew pumpkin dripping slime, gate 2 a
  bowling-ball pumpkin with finger holes, gate 3 a grassy rolling pumpkin. Done in Phase 0: `GateFinalesMenu` keeps a
  finale boss that is a variant of the giant pumpkin (and no longer rewrites the cook time or a custom rules sign). The
  base prefab and `PumpkinBallBoss.cs` stay with the coordinator.
- **E2 One reward budget.** Today the giant pumpkin breaks into 14 candies (gate 1: 42) and 30 EXP. Pick one number
  for all three (the user's call), set in one place (`GateFinalesMenu`).
- **E3 Ending length.** After a gate the player still has Whack 6 + Whack 7 (2 bee bosses), the lid and
  Pumpkin_Boss_Z. The user chose this on 2026-11-18 ("like the main game"). Measure it once from the gate's seed to
  the boss (minutes) in Phase 2 and show the user before changing anything.
- **E4 Replay hook (option, small).** Nothing remembers which gates were cleared. Option: save `Gate_Cleared_N`
  (Unlocks), show a small gold mark on a cleared gate's circle, and give one gift stat point when all 3 are cleared
  (like CookingRewards). Needs a GAMESTAGES string "Route" set in Path N ON and one action in "fairy reward".
- **E5 WORKING SITE notice.** All 3 Path N ON states show "WORKING SITE". Remove it per gate once that gate passes
  its checks (`PlayGroundNoticeSetup` needs a per-path remove).
- **E6 End-to-end check per route** on `_claude_test`: Stage 1 -> gate -> giant pumpkin -> rooftop -> Whack 6/7 ->
  boss (killed by a test hook) -> GAMEOVER -> Walk Tuttorial -> Stage 1. Check: gears and seed reset, fog/sky back,
  nothing left from the gate, the gate can be taken again.
- **E7 Enemy Manager 4 on the way out.** Gates 2 and 3 leave through "Enemy 4 - Get the Seed", which switches on
  `-EnemyManager 4` (recursive). Gate 1 skips that state. Check in E6 that no bee swarm follows the player onto the
  rooftop; if it does, gates 2/3 go straight to "Tween PlayGroundZ" like gate 1.
- **E8 Traps in old menus (done in Phase 0).** `Paths/Editor/Gate3PickMenu`'s two menus are greyed out and refuse to
  run (either would cut Ball Valley out again). The purple hole's keep names now match in any case
  (`pumpkin_boss_Z_Doom` counts as a boss).
  `GateExit` waits for the rooftop forever (it only warns and shows the view): fine, but log the GAMESTAGES state.
- **Already the user's rule, keep it:** killing a real Pumpkin_Boss_Z anywhere sends GAMESTAGES' global "Win" ->
  "wait" -> Thank For Playing -> a new run. Gate 1's Doom boss (the wrecked-pot punishment) is one; the gate 1 session
  asks the user whether that should still count as beating the game.

## 3. Order, and who touches what

### Order

0. **Before:** the user saves the scene in Unity and OKs one baseline commit (101 files changed since the last
   commit, 2026-11-20 17:14). Then each session's changes are easy to see and undo.
1. **Phase 0 (coordinating session): DONE 2026-11-22.**
   - Unity's "Script Changes While Playing" = "Recompile After Finished Playing" (user OK'd).
   - Skills in `.claude/skills/`: `unity-429r-playtest` (the test kit + snippets + `scripts/csc_check.py`, the offline
     compile check), `playmaker-429r` (+ `scripts/gamestages.py`, the GAMESTAGES reader), `unity-mcp-orchestrator`
     (MCP for Unity's own skill, v10.3.0); `blender-429r` now works on both PCs.
   - E1 menu guard and E8 fixes (above). Compile-checked (Roslyn, 0 errors) and compiled in Unity.
   - The notes folder has `UNITY_LOCK.txt`, `BLENDER_LOCK.txt` (empty = free) and `requests/`.
   - `Scripts/Gates/**` is frozen until Phase 2.
2. **Phase 1:** the 3 gate sessions in parallel (design, code, art). Unity in turns (lock below).
3. **Phase 2 (coordinating session):** all GAMESTAGES/scene edits the gates asked for, in one go; E4/E5; the user
   saves the scene; E6 for all 3 routes.
4. **Phase 3:** the user plays each gate in the headset; fixes go back to the gate's own session.

### File ownership

| Who | Owns (may edit) | Must not edit |
|---|---|---|
| **Gate 1 session** | `Scripts/PlayGround/**` except the files below, `Prefabs/PlayGround/**`, `Prefabs/Bases/Play_Ground_Z.prefab`, `Prefabs/Enemies/PLAYGROUND/**` (the slimes; gate 2 uses Slime_Big/_Blue/_Red as bases), `pumpkin_boss_Z_Doom`'s overrides, its own giant pumpkin variant, `Exports/PlayGround_Concept/`, `Exports/Gates/gate1/` | `BowlingRollAssist.cs` (gate 2); `RunAwayPumpkin`, `LaughingPumpkin`, `NearGrabOnly`, `PumpkinBoom`, `CookingIngredient`, `Hideout` (the main game uses them) |
| **Gate 2 session** | `Scripts/Bowling/**`, `Scripts/PlayGround/BowlingRollAssist.cs`, `Prefabs/Bowling/**`, its own giant pumpkin variant, `Exports/Bowling_Concept/`, `Exports/Gates/gate2/` | the PlayGround slimes and `PG_Treasure_Chest_*` (gate 1's); `Flying_Slime`, `Slime_Bomber`, `Daruma_Bomber` (shared bases: make variants under `Prefabs/Bowling/`) |
| **Gate 3 session** | `Scripts/BallValley/**`, `Prefabs/BallValley/**`, `Assets/BallValley/**`, its own giant pumpkin variant, `Exports/Gates/gate3/` | `Assets/BallValley/Sky/Halloween_Skybox_Mat*` and `Midnight_Sky*` (the main game's sky, also gate 2's) |
| **Coordinator** (the session that starts the 3) | `2 Game Scene.unity` (GAMESTAGES), `Scripts/Gates/**`, `Prefabs/Gates/Pumpkin_Ball_Boss.prefab`, shared scripts (SpiderBrain, SlimeBody, EnemyHitDamage, Fist, FistBump, HitSettings, WeaponPower, PlayerHealth, PlayerLaunch, PumpkinBossBrain, PurpleHole, TutorialNotice, SaveManager...), `Prefabs/Items/Chest/**`, candies, smokes, `Stun_Stars`, `Resources/*.asset`, `Assets/NOTES_Next_Time.md`, `MEMORY.md` | gate folders (asks the gate's session) |

Shared prefabs found by following each gate's root prefab (Play_Ground_Z + Fire_Woods_n_Cauldron, Bowling_Arena,
Ball_Valley) through its references:
- **All 3 gates:** Pumpkin_Ball_Boss, The_Seed, Candy_1/2/4/5/7/8, Candy_Drop, Stun_Stars, the shared smokes, Hit_Blast.
- **Gates 1 and 2 only (27):** the PlayGround slimes (Slime_Big, _Blue, _Red, Slime_Small, Flying_Slime,
  Slime_Bomber, Daruma_Bomber), the gear chests and seeds (Seed_Chest/Head/Shield, Treasure_Chest_and_Seed_*,
  PG_Treasure_Chest_*), Firework_Win, Random_Breakable_Candy, White_Bug_Glow/Pop, Spider_Web_Ball.
- **Gate 3** shares nothing beyond the all-3 list: it is the safest to run in parallel.

### Rules for all sessions

- Edit only files you own. Need a change in a shared file: write a request in the notes folder,
  `requests/<gate>-<short-name>.md` (what, why, the exact change). The coordinator applies it.
- Put gate-only code in your own folder (a small component of your own) instead of growing a shared script.
- **No scene edits in Phase 1.** A GAMESTAGES or scene change is written as a re-runnable `Tools > 429 Game` menu in
  your own `Editor` folder and left for Phase 2.
- **Unity is shared** (one editor, one project). Before compiling, running a menu or entering Play, take the lock:
  write `UNITY_LOCK.txt` in the notes folder (session, start time, what). Give it back within about 20 minutes.
  Delete the `_claude_test` save after each turn.
- **C# safety:** compile-check offline with Unity's Roslyn before a .cs lands in Assets. A broken .cs stops everyone's
  Play. Ask the user to set Preferences > General > Script Changes While Playing = "Recompile After Finished Playing",
  so one session's save doesn't reload another's Play test.
- **Blender:** one session at a time drives the live Blender (`BLENDER_LOCK.txt`). Headless `blender -b` runs are fine.
  Never save the user's .blend files; write new files.
- No commits (the user commits). Short reports: what changed, what to save, what to test.
- Why not git worktrees: each worktree is a second Unity project (a long Library import, several GB), and three copies
  of the 7 MB scene would need a hand merge of GAMESTAGES.

## 4. Session briefs

### How to start them

- One `start_session` each, context "fresh", `use_worktree: false` (one shared checkout, the ownership table keeps them
  apart), titles "Gate 1: PlayGround", "Gate 2: Slime Bowling", "Gate 3: Ball Valley".
- Paste the brief as the task. Each brief tells the session to read this file first (§2 contract, §3 rules) and its
  findings file in the notes folder (`gate1_findings.md` etc., written 2026-11-22 from the code).
- Run order inside Phase 1: all three may start together; the Unity lock serialises the play tests. If Unity time gets
  tight, give it in this order: gate 3 (shares the least), gate 2, gate 1.

### Common test kit (all gates)

- **Unity link:** MCP for Unity on :8080 (the user presses Window > MCP for Unity > Start Server). If the session's
  UnityMCP tools don't connect, run C# through the HTTP runner:
  `sh C:/Users/ADMIN/Documents/429R_claude_backups/2026-11-20/rooms/pt.sh <file.cs> [timeout]` (a C# 6 method body).
- **Start a test:** the `pt_start.py` pattern (`Documents/429R_claude_backups/2026-11-11/pc2_fixes/`): Play ->
  `SaveManager.Profile = "_claude_test"` -> skip the opening -> close the Health Warning -> `SaveSlotMenu.StartSlot(2)`.
  Guard every save step: `if (SaveManager.Profile != "_claude_test") return "WRONG PROFILE";`.
- **Jump to a gate:** GAMESTAGES `Fsm.SetState("Clean for Path N")` (see `gate_notice_test.cs` in the rooms folder),
  then close the WORKING SITE notice (`TutorialNotice.Find().Close()`).
- **After .cs edits, before Play:** refresh with scope "all" + mode "force" and wait until Unity is neither compiling
  nor updating (a reload during Play resets the save profile to Local).
- **After each turn:** delete `%USERPROFILE%/AppData/LocalLow/MeLikePie/Four Twenty Knight - 429/Saves/_claude_test`,
  give back the Unity lock.
- **Pictures:** `Camera.Render` of a copy of Camera.main into a PNG (post-processing on); the dark shapes at the
  camera are the player's own hands.
- **Work folder:** `C:/Users/ADMIN/Documents/429R_claude_backups/<date>/gateN/` (tests, backups before each edit).
- **Notes:** write your hand-over in `Exports/Gates/gateN/NOTES.md`. Don't edit `Assets/NOTES_Next_Time.md` or
  `MEMORY.md` (the coordinator merges them); you may update your own gate's memory file.

### Done looks like (all gates)

- A normal run from Stage 1 through the gate to the rooftop (Whack 6 starting) plays through GAMESTAGES, not only
  test buttons, with 0 console errors, on `_claude_test`.
- The §2 contract holds. Every Unity change is a re-runnable `Tools > 429 Game` menu with backups.
- The gate's findings are fixed or listed as the user's decisions. A short headset checklist for the user.
- A report of at most 10 lines: what changed, what to run/save, what to test in the headset, requests for the
  coordinator.

### Brief: Gate 1, PlayGround ("Defend the Cauldron")

> **Task:** make gate 1 (the PlayGround) fun, clear and solid from Path 1 ON to its seed, and start its real look.
> Read first: `Exports/Gates/GATES_PLAN.md` (§2 contract, §3 rules, §4 kit) and `gate1_findings.md` in the notes
> folder. Background: NOTES_Next_Time.md items 8, 8b, 9, 9b (the 11-07 cooking-rounds section is history); memory
> gates-end-like-gate3 (playground-redesign is the older design); `Exports/PlayGround_Concept/` (concept_v1.png,
> BUILD_LIST.md).
>
> **Today:** Bracer chest -> its seed pops the cauldron; hit the hat for the recipe (4 candies + 1 tree pumpkin); the
> right dish starts a 5-minute cook (logs speed it up); waves attack the player near the pot, else the pot (150 hp);
> 4 slime mini bosses at 25/50/75/90% drop the gear chests; the pot bursts -> the giant pumpkin -> The_Seed -> rooftop.
> Pot wrecked -> endless waves + the Doom boss (sure death). About 5-9 min.
>
> **You own:** `Scripts/PlayGround/**` EXCEPT `BowlingRollAssist.cs` (gate 2) and the files the main game uses
> (`RunAwayPumpkin`, `LaughingPumpkin`, `NearGrabOnly`, `PumpkinBoom`, `CookingIngredient`, `Hideout`: coordinator);
> `Prefabs/PlayGround/**`, `Prefabs/Bases/Play_Ground_Z.prefab`, `Prefabs/Enemies/PLAYGROUND/**` (gate 2 uses
> Slime_Big/_Blue/_Red as bases: tell the coordinator before changing them), `pumpkin_boss_Z_Doom` (its overrides
> only), your giant pumpkin variant, `Exports/PlayGround_Concept/`, `Exports/Gates/gate1/`.
>
> **Work, in this order:**
> 1. **Measure first:** one normal run on `_claude_test` from "Clean for Path 1" (time to the cook, cook length with
>    and without logs, hp lost, pot hp at the end, candies, frame time in the busiest wave).
> 2. **Logic:** the menu trap ("Use Defend The Cauldron" re-points Path 1 ON to the old "Tween PlayGroundZ To Boss",
>    dropping the rooftop tree defence: make it leave Path 1 ON alone); a fallback timer on the start-chest wait; the
>    purple hole at the win and after a mid-gate death (it reaches 14 m from the origin, where the PlayGround is: the
>    Bracer chest, hat, trees or table must not be eaten while still needed); the CookingRewards notice must not pause
>    the game while the giant pumpkin is in; stale "10 minutes" docs and prefab labels.
> 3. **Clear goal:** a first-step hint before the cook (title + one line, e.g. find the hat); the hat placed or hidden
>    somewhere worth finding (the user places hats: ask); tree pumpkins easy to get; a wrong recipe costs less than a
>    boss slime every time.
> 4. **Pace:** logs shouldn't bunch all 4 mini bosses (cap the speed-up, or time the bosses by real seconds); waves
>    that come from a few gates/directions instead of random edge puffs; real pressure on the pot; the finale arena
>    fits a 13 m/s charge (the roam area is ~11x12 m).
> 5. **Loss rule:** today a wrecked pot means endless waves + the Doom boss, and killing that boss sends the global
>    "Win" (the whole game is won, skipping the rooftop). Ask the user if that stays.
> 6. **Look:** gate 1 never lifts the house fog (dark). Start `BUILD_LIST.md` from the concept: a piece list with sizes
>    for the user to model (swing, slide, sandbox, rocking horse, monster gates, ring fence), then palette UVs + assembly
>    + Unity swap after the user's models; remove the dead nested cauldron/hat copies inside Play_Ground_Z. Give your
>    giant pumpkin variant its own look (e.g. a stew pumpkin dripping slime).
> 7. **Clean-up (ask first):** the old rounds code in Cauldron.cs, PlayGroundRounds, the laughing-pumpkin rounds and the
>    Daruma hook are kept behind ticks; propose what to delete.
>
> **Ask the user:** the Doom-boss rule; where the hats go; cook length (5 min today, with logs 75-300 s).
>
> **Test:** CauldronDefence "Test: Break The Cauldron", `TestSkipTo`; Cauldron "Test: Put The Right Recipe In",
> `TestSetProgress`, `TestSpeed`; `CauldronHealth.TakeDamage`; `MagicHat.ShowRecipe`. Jump: GAMESTAGES
> "Clean for Path 1", close the notice, send FINISHED to the start chest. Full = through to Whack 6 starting, with no
> GearGuard warning and the pot surviving a normal run. Headset checklist for the user: the hint, the hat search,
> waves around the pot, the finale charge in the small arena.

### Brief: Gate 2, Slime Bowling

> **Task:** make gate 2 (Slime Bowling) fun, readable and solid from Path 2 ON to its seed, move it onto the shared
> finale and exit, and start its real look. Read first: `Exports/Gates/GATES_PLAN.md` (§2 contract, §3 rules, §4 kit)
> and `gate2_findings.md` in the notes folder. Background: NOTES_Next_Time.md "2026-11-08: Gate 2 = Slime Bowling",
> items 9, 9b; memory gate2-bowling, gates-end-like-gate3; `Exports/Bowling_Concept/PROMPTS.md`.
>
> **Today:** a 5 m dirt island in an X of four 20 m lanes; a Giant Flying Slime poops 6-pin racks; 3 strikes (they
> add up) hit a slime and split it; after phase 2 the giant pumpkin rolls the lanes; The_Seed -> rooftop. About 4-5 min.
>
> **You own:** `Scripts/Bowling/**`, `Scripts/PlayGround/BowlingRollAssist.cs`, `Prefabs/Bowling/**`, your giant
> pumpkin variant, `Exports/Bowling_Concept/`, `Exports/Gates/gate2/`. Read-only for you: the PlayGround slimes
> (`Prefabs/Enemies/PLAYGROUND/**`, gate 1's), `Flying_Slime` / `Slime_Bomber` / `Daruma_Bomber` (shared bases: make
> your own variants under `Prefabs/Bowling/` if you need changes), the chests and seeds.
>
> **Work, in this order:**
> 1. **Measure first:** one normal run on `_claude_test` from "Clean for Path 2" (time to the first rack, per phase,
>    the boss; strikes vs misses; hp lost; frame time and particle count with 4 lanes lit).
> 2. **Contract #3/#4:** gate 2 spawns its boss with `PumpkinBallBoss.Spawn` and runs its OWN copy of the exit
>    (`ToTheRooftop` + `GearGuard`). Move it to `GateBossFinale.Begin(...)` (lane hooks in the set-up callback, seed spot
>    kept off the void) and `GateExit`, then delete the copy.
> 3. **Logic:** the Shield chest never drops on the pumpkin path (GearGuard force-sets Shield_Gear and warns every win):
>    drop it (e.g. when the boss breaks, next to The_Seed). Fallback timers on the start-chest wait. Make "Set Up
>    Bowling Arena" keep the finale fields (today a re-run empties `finaleBoss` and silently brings back phase 3 + the
>    showdown, and the sign says "in a row" again). Boss candies must land on the dirt (they fly past the fall edges
>    today); unlock the thumbstick for the boss fight; update `arenaCentre` after hops.
> 4. **Pace and feel:** less waiting at the start (first rack ~30 s in, 10 s of laps); balls ready at hand height
>    instead of on the floor; a clear strike moment near the player (sound + the counter) and a miss sound; rules sign
>    shorter and readable from the dirt; "n/3" reads as a total, not a streak; approach marks so balls don't roll off
>    the dirt (or side offset in the roll assist). Keep it easy to finish; strikes make it faster.
> 5. **Boss:** check that a rolled ball hurts the giant pumpkin (EnemyHitDamage counts it as the player's?), and the
>    lane mode after the 2026-11-20 aim-lock change. Today its loop is ~9 s with 5.5 s of waiting.
> 6. **Speed:** particles (up to ~1,450 with 8 Danger_Flame copies vs a 650 budget), 5 spot lights, lanes never going
>    dark, a `FindObjectsByType` scan every 0.5 s.
> 7. **Look:** everything is placeholder (cube lanes, code-built dirt, quad decks). Start the concept in
>    `Exports/Bowling_Concept/PROMPTS.md` (floating rock island, wooden lanes, stone rune decks, braziers, a ball-return
>    cauldron): a piece list with sizes for the user to model (lane, rail, deck, brazier, island rock), then palette UVs
>    + assembly + Unity swap after the user's models. Cheaper braziers can replace Danger_Flame. Give your giant
>    pumpkin variant its own look (e.g. a bowling-ball pumpkin with finger holes).
>
> **Ask the user:** whether phase 3 + the 4-slime showdown are dead for good (then delete them, or keep them behind
> the tick); the strike/miss feedback style.
>
> **Test:** right-click BowlingStage in Play: Eat The Seed, Strike The First Rack, Hit Slime 0, Next Phase, Showdown
> Now (straight to the giant pumpkin), Win Now; PinSet: Force Strike, Hatch Now. Full = GAMESTAGES "Clean for Path 2"
> on `_claude_test`, through to Whack 6 starting, with no GearGuard warning. Add a "Play Bowling (Play mode)" menu like
> Ball Valley's. Headset checklist for the user: bowling feel at 18 m, ball pick-up height, strike readability, the
> boss on the lanes.

### Brief: Gate 3, Ball Valley

> **Task:** make gate 3 (Ball Valley) fun, readable and solid from Path 3 ON to its seed, and give it a look that
> fits 429. Read first: `Exports/Gates/GATES_PLAN.md` (§2 contract, §3 rules, §4 kit) and the findings
> `gate3_findings.md` in the notes folder. Background: NOTES_Next_Time.md items 7b, 7c, 9, 9b; memory
> gates-end-like-gate3.
>
> **Today:** 15 bot balls, push them into 6 holes while the 50x25 m field shrinks (40/90/140/190 s), candy on the
> lawn, a two-fist dash. All balls in or the last phase -> the giant pumpkin -> The_Seed -> GateExit. About 5-7 min.
>
> **You own:** `Scripts/BallValley/**`, `Prefabs/BallValley/**`, `Assets/BallValley/**` EXCEPT
> `Assets/BallValley/Sky/Halloween_Skybox_Mat*` and `Midnight_Sky*` (the main game's sky), your giant pumpkin
> variant, `Exports/Gates/gate3/`. Never rename Ball_Valley (the purple hole spares names with "Valley").
>
> **Work, in this order:**
> 1. **Measure first:** one normal run on `_claude_test` from "Clean for Path 3": time per phase, hp lost, candies
>    eaten, balls the player pocketed, frame time at the busiest moment (boss + balls). Write it down.
> 2. **Logic:** a fallback when the boss is gone without Broken (exit anyway); null checks in the loose-ball loops;
>    BallValleyPlayer restores the body + mover on OnDisable (no black view stuck); logFeed off in builds; the gate's
>    numbers live on BallValleyGate (Awake overrides the stage Tuning: say so in its tooltips).
> 3. **Make the goal matter:** a ball the PLAYER pockets (last pusher) pays a small candy pop at the hole and brings
>    the next shrink sooner; a mark on every hole (rim glow or 2 torches, cheap); a pocket sound that is not the
>    player's fall whistle; no "levelup" sound when a bot grows (use the user's existing clips only).
> 4. **Contract #2:** a gear chest at each shrink phase end (the shared Treasure_Chest_and_Seed_* prefabs, used as
>    they are) instead of GearGuard flipping all bools at the end; Bracer from a start chest like gates 1/2.
> 5. **Fair finale:** clear the leftover bots when the boss drops (or most of them), and check pits + boss charges in
>    the 25x12.5 m arena (a charge into a pit = DamageHeavy + 55 today).
> 6. **Dash and comfort:** a stricter charge gesture (0.3 m is a boxing guard), a level dash when looking down, and
>    ask the coordinator to run the comfort vignette during dashes/shoves (shared code: a request).
> 7. **Speed:** candy spawn rate (3 FSMs each), floor/teleport MeshColliders re-cooked every frame while shrinking,
>    BuildWorld in one frame.
> 8. **Look:** no concept art exists. Make `Exports/Gates/gate3/Valley_now_sheet.jpg` (screenshots) + `PROMPTS.md`
>    (Gemini prompts like `Exports/PlayGround_Concept/PROMPTS.md`: a Halloween toy-diorama valley, same layout and
>    sizes). Cheap first step for the user to judge: a night re-tint of `BALLS_Palette.png` (paint over, keep the
>    layout). The user models any new pieces; you do UVs (the palette method in `Exports/Remodel/tools/palette_kit.py`)
>    and the Unity swap. Give your giant pumpkin variant its own look (e.g. a grassy rolling pumpkin).
>
> **Ask the user** (one short question each, when you reach it): pocket reward size; whether the HUD lines (ALL IN!,
> FIELD SHRINKS!, BOSS INCOMING!) break the "no effect words" rule; the re-tint vs a full remodel.
>
> **Test:** fast = Play -> Tools > 429 Game > Ball Valley > Play Ball Valley, then
> `Balls.BallValleyStage.I.ShrinkToLast(1)` (finale), `Die(null)` on every bot (ALL IN), `gate.Boss.SpecialHit(n)`.
> Full = GAMESTAGES "Clean for Path 3" on `_claude_test`, through to Whack 6 starting. Headset checklist for the
> user: dash feel and comfort, shoves, hole readability, the boss charges.

## 5. Skills shortlist

Searched 2026-11-22: Anthropic's skills repo and plugin marketplace, the big community lists, Unity, Blender and
game-design packs. **No PlayMaker-aware skill, plugin or MCP exists anywhere.** Nothing is installed; each needs the
user's OK.

| # | What | Link | Install | Fit for 429R |
|---|---|---|---|---|
| 1 | **INSTALLED 2026-11-22. Our own project skills** (made with skill-creator, already installed): `unity-429r-playtest` (the §4 test kit and the memory's test gotchas) and `playmaker-429r` (how to read/edit GAMESTAGES and FSMs safely: the scene dump script, SerializedObject for FSM variables, re-runnable menus, the user saves the scene). Also fix `blender-429r`'s old paths (it still says G:/steam and K:/). | (local) `.claude/skills/` | Claude writes them in the repo; the user reads them first | **Highest**: every gate session runs the same tests the same way |
| 2 | **INSTALLED 2026-11-22 (as `unity-mcp-orchestrator`). MCP for Unity's own skill** `unity-mcp-skill` (how to use the MCP well: read editor state first, batch calls, compile and console checks, screenshots, several editors) | https://github.com/CoplayDev/unity-mcp/tree/v10.3.0/unity-mcp-skill (the tag matching the project's package; MIT) | Copy that folder (SKILL.md + references) into `.claude/skills/`; take a newer copy only after updating the package | **High**: the MCP the project already uses |
| 3 | **Unity's official plugin** (33 skills: physics collisions, AI navigation, audio, URP post, XR...) | https://github.com/Unity-Technologies/unity-agent-plugin | `/plugin marketplace add Unity-Technologies/unity-agent-plugin`, then `/plugin install unity@unity-agent-plugin` | **Medium**: use its skills as advice only. Acting in the editor needs the beta Unity CLI plus a package in the project (a second editor bridge next to MCP for Unity); `create-xr` changes XR/graphics settings: never on the shipped project. Unity Companion License |
| 4 | **MCP for Unity custom tool** for PlayMaker: a C# editor class marked `[McpForUnityTool("...")]` that lists/edits FSM states, transitions and variables through PlayMaker's API | https://coplaydev.github.io/unity-mcp/guides/custom-tools | Claude writes it in an `Editor/` folder (later, after skill 1) | **High, but it must be built** |
| 5 | **superpowers** (brainstorming, writing-plans, systematic-debugging, verification-before-completion, parallel agents) | https://github.com/obra/superpowers | `/plugin install superpowers@claude-plugins-official` | **Medium-low**: good habits, but heavy process that triggers itself (clashes with "keep it simple"); its TDD/worktree steps don't know Unity |
| 6 | **hookify** (plain-English warn/block rules on tool calls), e.g. "warn before editing 2 Game Scene.unity" for the gate sessions | https://github.com/anthropics/claude-plugins-official/tree/main/plugins/hookify | `/plugin install hookify@claude-plugins-official` | **Medium-low**: its hooks call `python3`, which isn't set up on this PC (uv's Python only); a plain project hook does the same |
| 7 | **Claude Code Game Studios** (75 game-dev skills: playtest-report, balance-check, bug-triage...) | https://github.com/Donchitos/Claude-Code-Game-Studios | Don't install (its skills need its own hooks, docs and agents); read 2-3 as templates for skill 1 | **Low** |

Looked at and left out: csharp-lsp (needs the .NET SDK, not on this PC; the Roslyn check already works),
feature-dev (overlaps code-review), IvanMurzak/Unity-MCP and other Unity bridges (a second editor bridge), Unity's
own AI MCP (needs a Unity AI subscription), Blender packs (nothing better than `blender-429r` + MCP for Blender),
the awesome lists (no Unity, Blender or game-design entries). Built-in git worktrees: see §3 (why not for Unity).

## 6. Questions for the user

Answered 2026-11-22: start (Phase 0, then the 3 together); baseline committed by the user; Unity preference set by
Claude; skills 1 and 2 added.

Can wait until Phase 2:
5. **Ending length (E3):** keep Whack 6 + Whack 7 after every gate, as you chose on 2026-11-18?
6. **Rewards (E2):** one candy + EXP number for every gate's giant pumpkin (today 14, gate 1: 42)?
7. **Replay hook (E4):** gold marks on cleared gates + a gift point for all 3?
8. **Giant pumpkin looks (E1):** do you model 3 looks, or should Claude make them from the current pumpkin (tint, hat)?

Each brief also lists its own questions (gate 1: the Doom-boss rule, the hats, cook length; gate 2: delete the old
showdown?, strike feedback; gate 3: pocket reward, HUD words, re-tint vs remodel).
