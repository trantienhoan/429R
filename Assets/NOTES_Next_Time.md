# Next time: where we stopped (2026-11-11 night)

Two PCs: the **K: PC** (has K:/WORK_2026/08_04/_claude/ with the boss round-3 files) and the **second PC** (no K:). Claude's memory, backups and test scripts stay on each PC, so everything needed to go on is written here.

## RESUME HERE

**K: PC, 2026-11-23, WHACK-A-PUMPKIN = Special Stage Type 4 (user's design). Built + Play-tested with stand-in models. Nothing committed. THE USER MUST SAVE THE SCENE** (GAMESTAGES "Bonus Round PR" now plays Whack_Round).
- **Where:** after the Playroom fight (GAMESTAGES "Bonus Round PR"), earned like the others (every enemy dead before the Yellow Seed). It REPLACES the punch colored-slime game there (user: "save punch colored-slime game for another place"); Paint_Bees_Round is kept, unplugged. Re-running "Set Up Paint Bees Bonus" would put the slime game back.
- **Flow:** the nesting round's chest comes; opened, the whack machine rises where it was (half-ring table round a stand ring, 20 holes, scoreboard behind, a mallet on a post each side). Taking BOTH mallets starts the 60 s countdown (walking + teleport off). Heads pop; a quick not-upward mallet blow whacks (hands do nothing). Normal 1 candy, gold 5, rainbow 15, clock +3 s, skull -3 candies + combo lost (letting a skull go isn't a miss). Each whack in a row: 4% faster pops/shorter up time, +1 head at once every 6; a miss resets combo + speed. Time up: mallets go, leaderboard saved (WhackScores, in the save slot), the machine pours out every candy won (Candy Helmet eats them), fireworks, machine sinks, round Finished. Level-0 stats inside (Special Stage Stats).
- **Files:** Scripts/Bonus/WhackRound.cs, WhackMachine.cs, WhackHead.cs, WhackHammer.cs, WhackChestSeed.cs; Scripts/Save/WhackScores.cs (+ SaveManager whackScores); Scripts/Bonus/Editor/WhackSetupMenu.cs = **Tools > 429 Game > Bonus > Set Up Whack-a-Pumpkin** (re-runnable; tuned numbers on Whack_Round kept). Prefabs/Bonus/Whack/Whack_Machine, Whack_Hammer, Whack_Round; Models/Bonus/Whack/Whack_Standin_Meshes.asset; Materials/Whack/*.
- **Stand-ins:** machine = generated half-ring (PG wood/metal), mallet = the tool hammer minus breaking/damage, heads = pumpkin_0 (+ gold coin mat, rainbow tint, green clock, Skull.fbx). **User is modelling the machine + a wooden mallet, starting from `Exports/Whack/Whack_Machine.blend`** (made headless by `_claude/whack_2026-11-23/blend/make_whack_blend.py`: the exact layout with REAL cut holes, ends at 90 deg, old textures Wood_Furniture/Wood_Boards/Bedroom_Metal, empties Hole_00-19/Post_L/R/Board/Candy_Spout/Stand_Spot, new wooden mallet + Mallet_Strike_Point, Reference collection, README + export_fbx.py text inside; player looks +Y there). Sizes in `Exports/Whack/WHACK_MODEL_SPEC.md`. **2026-11-23 15:40 the user reshaped it** (12-segment cabinet + a metal border frame `Whack_Cabinet.001`, wood between r 0.49-1.06, end bands at +-87 deg; their top was inside-out and the holes filled): fixed LIVE in their Blender (not saved by me; backup of their 15:37 save in Documents/429R_claude_backups/2026-11-23/whack): normals flipped outward, holes re-arranged + re-cut (near 8 at 0.645 m +-10/30/50/70, far 12 at 0.925 m evenly +-79.5), rings moved with their holes, empties + ref pumpkin moved; Unity stand-in + spec use the same layout; then (user: "make all other holes follow the selected hole that I fixed") the user's own 12-sided ring on Hole_08 (48 v / 36 f, r 0.085-0.101, z 0.80-0.828) copied by translation onto the other 19 holes (Whack_Metal_Rims 1872 -> 960 verts), live, unsaved; then the table holes re-cut as 12-sided cups (user: "Yes, match them"): old cups deleted + openings filled, 12-gon cutters with corners every 30 deg from +Y at r 0.085 (exactly the rings' inner corners), 0.32 deep, dark inside; Whack_Cabinet 1048 -> 812 verts, watertight; the menu still needs the code to take `Models/Bonus/Whack/Whack_Machine.fbx` / `Whack_Mallet.fbx` when they come.
- **THE USER'S MACHINE IS IN THE GAME (2026-11-23 16:40, user: "the Whack_Cabinet is good to go, get it in the game and test").** Exported headless from their saved Exports/Whack/Whack_Machine.blend (`Exports/Whack/export_machine.py`: collection "Whack_Machine (export)", -Z fwd, Y up, Apply Transform, FBX_SCALE_ALL) to `Assets/Models/Bonus/Whack/Whack_Machine.fbx` (2.28 x 2.00 x 1.47 m; the user joined table + frame + rings into Whack_Cabinet, metal-lined holes with wooden bottoms, Whack_Scoreboard separate, and REMOVED the mallet posts: the mallets float at Post_L/R). Set Up Whack-a-Pumpkin now uses the FBX when it exists: material remaps Wood_Furniture_Mat / Wood_Boards_Mat -> Wood_Floor_Mat / Bedroom_Metal_Mat / Whack_Hole_Dark_Mat -> the game's materials, model turned 180 deg (the FBX comes in facing -Z), MeshColliders on its meshes, heads at Hole_00-19, mallet spots at Post_L/R, board text at Board, candies from Candy_Spout; the stand-in is only the fallback. Re-export after Blender edits: `blender -b Exports/Whack/Whack_Machine.blend --python Exports/Whack/export_machine.py -- Assets/Models/Bonus/Whack/Whack_Machine.fbx`, then run the menu. Play-tested end to end with it: 38 whacked / 29 missed / combo 6 / 76 candies, all eaten, machine sank, GAMESTAGES on, 0 errors. Pictures `Exports/Whack/model_*.png`, `ingame_model_*.png`. THE WOODEN MALLET IS IN TOO (17:00, user: "Yes, the mallet"): the user's reworked Whack_Mallet (bigger head 0.26 x 0.19 m, one wood material "Wood_Furniture", 0.49 m) exported headless by `Exports/Whack/export_mallet.py` (moved to the origin for the export) to `Assets/Models/Bonus/Whack/Whack_Mallet.fbx`; the menu now builds Whack_Hammer from it: Hammer_Tool's grab/body/weight kept, its look + bones + boxes removed, a new Attach at the old grip pose, the mallet turned so its handle and striking faces point like the tool hammer's, two capsules (handle, head) from its mesh, strike point = head centre; wood remapped to Wood_Furniture_Mat. Play-tested: F/G grab both, click swings whack (29 whacked / 30 missed / 46 candies, bot noise), all eaten, GAMESTAGES on, 0 errors. Headset feel unchecked. Seen in test shots, not from the machine: two purple light beams + a purple skull-shaped puff in the bonus stage (not looked into).
- **Play-tested** (`_claude_bot`, PC-controls bot, test `_claude/whack_2026-11-23/whack_test.cs`): chest -> machine -> F/G grab both -> GO -> click swings whack -> clock +3 s, skull -3, misses reset the combo -> 41 whacked / 26 missed / best combo 8 / **113 candies** -> fountain 113, all eaten, machine sank, GAMESTAGES -> Rooftop_ON -> Prepare_Whack_6, walking back. 0 errors. Pictures `Exports/Whack/`.
- **Candy total: KEEP IT GENEROUS (user, 2026-11-23)**: 113 for a so-so player (28 normal, 6 gold +30, 4 rainbow +60) vs 33-48 in the other bonuses is fine; don't lower it. **Open:** headset check (reach, table height 0.82, mallet feel); the user's machine + mallet models.

**K: PC, 2026-11-23 afternoon, GATE 3 (Ball Valley) = the user's new flow + their kit remodel. Built, compiled, Play-tested through GAMESTAGES (bot profile); not in the headset; nothing committed; no scene change.** Details: `Exports/Gates/gate3/NOTES.md` sections 3d and 3e; info for the coordinator: `Exports/Gates/session_notes/requests/gate3-new-flow-info.md`.
- **Kit:** the user remodelled `BallValley_Kit.blend` onto the house texture (Bedroom_Furniture_Set1). Build Ball Valley Kit maps it to `Bedroom_Set_1_Mat` through gate 3's own glow copy `BallValley_House_Glow_Mat` (pit rings, lantern glass, pumpkin faces glow; `tools/glow_mask.py`). New piece: the jello volcano (`BallValley_Kit_Volcano.blend`). Tools: `tools/check_kit.py` (what changed in the .blend), the export now joins parented parts and writes `kit_materials.json`.
- **Flow (no Gate Run, no gear seeds: user's call):** card -> 15 balls at once -> the field closes over a 300 s song (Pumpkin_Playroom + Spider_Queen_Rampage sped up 0.92 -> 1.3x; no 5-minute song exists yet) -> only the player's pockets count (others drop back in) -> all 15: the Black Pumpkin Ball (3x heavy, 150 health, break it or pocket it) -> 42 candies rain + the main game's `Treasure_Chest_and_Seed` -> The Seed -> GateExit -> rooftop. Song runs out: the holes meet, the player dies (no revive) -> Game Over. Candy trees, boulders, jello volcanoes anywhere on the lawn.
- **Tests (K:/WORK_2026/08_04/_claude/gate3_2026-11-23/flow/):** `g3_flow.cs` Clean for Path 3 -> Whack 6 PASS; `g3_collapse.cs` the loss -> Game Over -> new run PASS (after a fix: the kill waits for a revive to finish). `gates_2026-11-23/t4_gate.cs` TG_PATH 3 no longer applies.
- **User's next:** headset run of gate 3; a real 5-minute song; balance; the mountains (darker / lower / kit hills).

**K: PC, 2026-11-23, PC controls (Editor only; user: "I want head cam view move with mouse point"). Nothing committed; no scene change.** `Scripts/Locomotion/PcControls.cs` (backup `Documents/429R_claude_backups/2026-11-23/pc_look/`).
- **Mouse look at once:** no first click any more. The Game view takes the mouse when Play starts and whenever Unity + the Game view come back to the front; Esc lets it go (pause menu), a click in the Game view takes it back. If the Editor refuses the cursor lock (it did in every driven test, even after a click), the look still comes from the mouse's own movement and the pointer is pulled back to the Game view's middle each frame.
- **Esc fix (bug since 11-20):** the scene's Pause Menu already binds `<Keyboard>/escape`, and PC controls toggled it too, so one real Esc opened and shut it. Now only the pause menu reacts to the real key (PC controls still toggle it for the tests' pretend Esc), and the view turns to the menu whenever it opens. Esc does nothing while the pause is blocked (loading fade/opening), by design.
- **Tested** (`_claude_bot`): mouse held from the start with no click; Esc opens (view faces the menu) / Esc closes and takes the mouse back; the 11-20 test `_claude/pc_controls_2026-11-20/pc_test2.cs` passes end to end. Turning with a real mouse: **confirmed by the user** ("Yes, it works", 2026-11-23).

**K: PC, 2026-11-23, gate 1 (PlayGround) session: the user's own house models in the PlayGround. Nothing committed; no scene change.**
- **Brighter:** PlayGroundGame Lift Light x2 while the PlayGround is out (user: "Keep B (x2)"); the house light comes back at the exit.
- **House models:** the user picked 21 Bedroom_Walls.blend models ("Use your suggestion", sheet `Exports/Gates/gate1/kit_renders/bedroom_walls_models_2026-11-23.jpg`). **Tools > 429 Game > PlayGround > Place Bedroom Models** put 27 copies in `Play_Ground_Z/Bedroom_Models`: 13 breakables as SpawnPropHere spots on the floor strip round the field, toys (Tank, Dino, Sleigh x2) in the field on the Wall layer, the rest looks-only outside. 9 kit pieces switched off (`..._off`). Built once; move them by hand (a spot: move the spot, not its "Look (editor only)").
- **Play-tested** (`_claude_bot`): 13/13 props on the floor, gate 1 to Whack 6 in 67 s, fountains 8/16/12/42, nothing left over, 0 game errors. Before/after: `Exports/Gates/gate1/kit_renders/bedroom_models_before_after_2026-11-23.jpg`. Details: `Exports/Gates/gate1/NOTES.md` §0.
- **Headset check pending** (look, 4 extra point lights from the 2 pole lights).

**K: PC, 2026-11-23 afternoon (user: "check the notes for gate 1 2 3 and the main game, finish everything, if we can't, improve what's already good"). Nothing committed; no scene change.**
- **User's calls today:**
  - E2: every gate's giant pumpkin = 42 candies.
  - E5 WORKING SITE: KEEP it ("we are not done yet with those 3 gates").
  - E4 replay marks: not now.
  - Gate tuning: keep the numbers (gate 1 cook 45/60/75 + random hat spots, gate 3 3 candies a pocket + 120 s rounds); add gate 2's strike/miss sounds; delete the unused showdown slimes.
- **Changes:**
  - **E2:** `Prefabs/Gates/Pumpkin_Ball_Boss` Break Candies 14 -> 42 (the 3 gate variants inherit it; gate 1's CauldronDefence already 42; code default now 42).
  - **Gate 2 sounds:** BowlingStage Strike Sound / Miss Sound (+ volumes 0.9 / 0.6), played at the player (heard from 18 m). A strike plays `Clear,_resonant_bell_1`. A miss (`PinSet.Knocked`: a ball reached the rack, no strike) plays Kenney's `lowThreeTone`; a rack nobody bowled at stays quiet. Gate 2: Use Gate Run fills them where empty (run).
  - **Cleanup:** `Slime_Showdown_Blue/Green/Red/Yellow` deleted (unused; backups Documents/429R_claude_backups/2026-11-23/gates/showdown_slimes). GiantSlimeSetupMenu no longer makes them; CandyColourSetupMenu's KeepLook is empty.
  - **"Cannot set the parent of 'Left/Right Controller' while ... 'DeadCam'" at Play exit:** DeathCameraFall.OnDisable no longer moves the hands back while the game / Play mode is closing (Application.quitting). Compile-checked; the death fall can't be started without a headset in a test (DeadCam never took the camera), so it's not reproduced: watch the console at the next Play exit after a death.
  - **Saved door skull (HurryPumpkinBoss):** the play test found the saved hurry went onto `---GameManager---` (the door's top object), so it would outlive the room and turn the NEXT run's Pumpkin_1 into the boss at once after a Game Over. Now it goes on the door's kit room (RoomKit parent).
  - **This PC's Editor preference** Script Changes While Playing = Recompile After Finished Playing (EditorPrefs ScriptCompilationDuringPlay 1).
- **Play-tested today** (K: PC, bot profile, scripts K:/WORK_2026/08_04/_claude/gates_2026-11-23/):
  - **Gate 1 Doom path:** recipe in + fire lit -> Break The Cauldron -> Doom boss 1.5 s later -> killed -> The_Seed 5 s later -> Gate Exit -> Whack 6, 56 s. BOSS stayed false, no Thank For Playing: PASS.
  - **Saved skull:** door Health -> Boss before the furniture -> Saved Pumpkin Hurry on Bedroom_Kit_Base(Clone) -> Mini Stuffs -> pumpkin_boss_Z out 0.3 s later: PASS.
  - **Random tree pumpkin sizes**, 42 made: sizes 0.75-1.5, most 0.8-1.1; health 2 x17, 3 x10, 4 x7, 5 x3, 6 x3, 7 x2. The ends: 0.75 = scale 0.638 / mass 127 / health 2; 1.5 = 1.275 / 1013 / 7. PASS.
  - **The 3 gates by test hooks** (`t4_gate.cs`, TG_PATH 1-3): Clean for Path N -> chests skipped -> rounds won by GateRun's hook (gate 2: a forced strike each round first) -> the gate's own giant pumpkin -> Special Hit -> The_Seed -> Whack 6.
    - Gate 1 (Pumpkin_Ball_Boss_Stew) 69 s, gate 2 (Pumpkin_Ball_Boss_Bowling) 75 s, gate 3 (Pumpkin_Ball_Boss_BallValley) 66 s.
    - Candy fountains in each: 8, 12, 16 (the round chests) and 42 (the giant pumpkin): PASS. Gate 2 plays its own fountain with the boss's number.
    - Gate 2's strike bell heard each strike: PASS. The miss tone wasn't triggered (forced strikes only).
- **Still open for the user:**
  - the headset run of each gate;
  - the kits' looks;
  - gate 3's small ones (no hard timeout on GateExit's rooftop wait; border hugging skips boss charges; slow balls pass through the player's body);
  - gate 2's deck collider gap (0.7 m past the pins);
  - the vignette on launches in the main game.

**GATES: 3 SESSIONS, ONE FLOW, A KIT EACH (second PC, 2026-11-22 night to 11-23): DONE + editor-tested; nothing committed after the user's baseline b568870; no scene change.**
- **Start here:** `Exports/Gates/GATES_PLAN.md` (section 0 = the user's 11-23 go: one flow for all gates, a kit per gate, GateRun's API). Each gate's hand-over, headset checklist and open questions: `Exports/Gates/gate1/NOTES.md`, `gate2/NOTES.md`, `gate3/NOTES.md`.
- **Concepts (user-approved):** `Exports/PlayGround_Concept`, `Exports/Bowling_Concept` (the Slime Bowl), `Exports/BallValley_Concept` (the haunted pool table), each `concept_v1.png`; prompts next to them.
- **One flow, shared code:** `Scripts/Gates/GateRun.cs` on each gate's stage root: card -> start chest (Bracer) -> 3 rounds with time limits, each ending in a gear chest (Chest, Head, Shield) -> the gate's own giant pumpkin (run.Finale -> GateBossFinale -> GateExit) -> The_Seed -> rooftop. Gates implement IGateGame. Verbs: gate 1 cook and defend (PlayGroundGame), gate 2 bowl and fight, gate 3 run and snooker.
- **Kits:** `Exports/Gates/gateN/<Name>_Kit.blend` (PlayGround 41 pieces, SlimeBowl 23, BallValley ~20), built by `tools/<name>_kit.py` (headless; never overwrites without --force: once the user edits a .blend it's theirs), one palette per gate, renders next to them; in Unity under `Assets/Models/Gates/<Name>/`, `Assets/M_Textures/Gates/<Name>/` and each gate's `Kit` prefabs, dressed by each gate's Build ... Kit menu. Own giant pumpkins: Pumpkin_Ball_Boss_Stew / _Bowling / _BallValley.
- **Shared fixes (coordinator):** GateFinalesMenu leaves gates on GateRun alone; Gate3PickMenu retired (greyed); GateBossFinale drops the seed if the giant pumpkin vanishes; PurpleHole keeps `KeepFromPurpleHole` things (GateRun's chests) and, while a gate runs, seeds, and its keep names match any case; `Locomotion/ScriptedMoveVignette` = the comfort vignette during scripted moves (gate 3's dash; PlayerLaunch only while a gate runs). Unity's "Script Changes While Playing" = "Recompile After Finished Playing" (user OK; set on the second PC only, set it on the main PC too: Edit > Preferences > General). The sessions' shared notes (findings, the 3 done requests) are copied to `Exports/Gates/session_notes/`.
- **End-to-end check (E6, 2026-11-23, `_claude_test`):** all 3 routes in one Play: gate -> giant pumpkin -> seed (auto-eaten after 20 s) -> Rooftop_ON PG -> Whack 6/7 -> lid -> BOSS -> Thank For Playing -> new run, 194 s. At Whack 6: no gate leftovers, GateRun gone, fog 6-22, far 40, Enemy Manager 4 off, no bees (E7 fine), all gears + seed set. New run: all flags and the checkpoint reset. 0 game errors.
- **Skills added (`.claude/skills/`):** unity-429r-playtest (test kit + `scripts/csc_check.py`), playmaker-429r (+ `scripts/gamestages.py`), unity-mcp-orchestrator (MCP for Unity's own, v10.3.0); blender-429r works on both PCs now.
- **Open (the user's call):** the headset run of each gate; the kits' fixes (gate 1's reads sparser than its concept); E2 one candy number for the giant pumpkins (gate 1 still 42, others 14); E3 the rooftop tail after a gate; E4 replay marks; E5 drop the WORKING SITE notice per gate (a GAMESTAGES edit: the user saves the scene); the vignette on launches in the main game too; per gate: gate 1 the Doom-boss rule, hats, cook length; gate 2 strike/miss sounds, delete the old showdown slimes; gate 3 the pocket reward (3 candies), round limit 120 s, the mountains.
- **Seen, not fixed:** two errors at Play exit ("Cannot set the parent of 'Left/Right Controller' while activating or deactivating 'DeadCam'"), and the start rooftop's MOVE/TELEPORT boards stay visible south of the PlayGround after a gate jump (test jumps).

**ENEMY HEALTH + THROW DAMAGE (2026-11-22, user: "game feels a bit too easy when the enemies are so weak"): done + play-tested; nothing committed; no scene change.**
- **Audit** (all enemy prefabs, Documents/429R_claude_backups/2026-11-22/enemy_health.txt): base health suits the early game; late in a run the player's damage is much higher (Weapon Efficiency / Fist Size +1 damage per point, candies add up to 10 points a stat in a run: hits of 13-24 in the 11-22 record).
- **User numbers:** small spider/roach/bee 6-10 health, normal 10-16 (SpiderBrain small/normal health on Spider/Cockroach/Bee _Smarter, _Small, _Normal; backups 2026-11-22/bug_health_before). Random size still scales it (+-33%): spawned in Play, small 5-13, normal 8-20. Spider_Tree keeps its own 2-3; mini bosses, giants, slimes unchanged.
- **Throws doing 25+ (user):** a thrown thing's damage (own 1 + Thrown Bonus 1 + Throw Power's +1 per 2 points) was multiplied WHOLE by a hard throw (x3) or a smash (x4): (1+1+6) x 4 = 32 at full Throw Power. Now only its own damage is multiplied and Throw Power's bonus is added after (WeaponPower.ThrowBonusOf, HitSettings.Evaluate(power, damage, flat, thrown, flyingFist), both EnemyHitDamage sites). Checked: full Throw Power hard 24 -> 12, smash 32 -> 14; early (+1) hard 9 -> 7, smash 12 -> 9; normal throws unchanged.
- **Mini bosses x1.5 (user: "increase mini boss health too"):** Spider_Boss 67-84 -> 100-126, Spider_Boss_PumpkinButt 87-95 -> 130-142, Cockroach_Boss 77-86 -> 116-129, Cockroach_Boss_PumpkinButt 132-147 -> 198-220, Bee_Boss 72-80 -> 108-120, Bee_Boss_PumpkinButt 131-143 -> 196-214 (prefabs + BossAndWhiteBugSetupMenu numbers; backups 2026-11-22/boss_health_before). Plain bosses still get random size (spawned in Play: 84-162); Pumpkin Butt ones don't (130-217). Secret-room giants (33, user-set) and slimes unchanged. Chaos versions are x2 of the new numbers.

**QUICK FIXES FROM THE LATEST RECORD (second PC, user 2026-11-20 late): done + editor-tested; nothing committed; no scene change (code + prefabs only).**
- **Kit door gaps (user: "the door leaves a big gap with the frame, Player can look outside"; then with a picture: "the dark metal should overlap the frame inside the room"):** the leaves stood BEHIND the frame's trim inside the wall hole (~12 cm short of the hole per side, ~19 cm at the top). Tools > 429 Game > Rooms > **Fit Kit Room Doors** (KitDoorFitMenu; both Build ... Kit Room menus call it) now HANGS each closed door on the room side (+z, where the trim stands out): planks 3 mm in front of the frame face (bedroom doors 0.18 m out, living X/Double 0.05 m), own width (already 5 cm past the frame's inner edges, so the sides and hinges lie over the frame), a little taller (top 4 cm over the frame). Secret_Door (ajar) untouched. Backups 2026-11-20/door_fit; renders `door_fit/hung_*_plusZ.png`. (The first try, stretching the leaf into the hole, was replaced.)
- **Pumpkin_3 hopping out of the living room:** RoomKit walls only turned solid at the END of a come (5.9 s for the living room). Now `solidFrom` 0.4: wall colliders on 40% in (roof + NavMesh blocks still at the end).
- **Pumpkin 3/5/7 out of the room (user: "drop their health to 0"):** FallEdge already ran their health out, but only inside an edge's box. New LostOutOfRoom on Pumpkin_3/5/7 prefabs: over any fall edge's spot (any height) or 2.5 m below where it appeared -> health 0 (FallEdge.RunOut, no EXP), so the stage goes on. Not while held.
- **Secret door skull saved (user: door broken before the furniture came, the skull did nothing):** Hurry Pumpkin Boss with no pumpkin counting down now adds SavedPumpkinHurry to the room (the FSM's root): the room's runner (bedroom door: Pumpkin_1, living room: Pumpkin_4) turns giant as soon as its Wait 360s starts. Gone with the room. NOT play-tested (needs the real order of events).
- **Runner pumpkin stuck watching from a corner (user: in sight = bigger run range, out of sight = smaller):** RunAwayPumpkin `seenRangeTimes` 2 (Flee Range 4 -> 8 m while within 35 deg of where the player looks and nothing in between), `unseenRangeTimes` 0.75 (3 m). Checked 5x a second. Not play-tested in a room.
- **Sky cost (user: "is the new sky laggy?"):** no: Skybox/Panoramic, one 2048x1024 texture, one draw; Sky Flyers = at most 3 small models on rare visits. Lag is more likely the known spikes (room sets spawning, Mono PC build: see smoothness notes).
- **Death -> restart (user: "black out too soon, Player watches the lone sky"; then "Heehee stays long but the color still comes too soon ... I want black out longer"):** new GameOverHold (made by itself in any scene with GAMESTAGES): keeps the HEE HEE GAME_OVER canvas on through Game Over / Recreate / Checkpoint? / Walk Tuttorial / CP states + 3.5 s, then fades 1 s; cuts GAMESTAGES' Game Over wait 4.29 -> 1.5 s. The canvas is ONLY the words (the 23-06 record showed the coloured sky round them from 26:10), so it also puts a black quad on the camera (Game_Over_Black: UI/Default material, ZTest Always, queue 2999 = over the world, under the HEE HEE canvas). Tested: grey HEE HEE while dying, then HEE HEE on full black (brightness 0.015) until the rooftop has landed, fades (camera shots record2/death_strip.png, before the 3.5 s change).
  - **2026-11-22 (user: "now the heehee stay for too long"):** the 00-17-59 record showed ~9 s of HEE HEE on black (28:06-28:15). Cut: GAMESTAGES Game Over wait 1.5 -> 0.5 s, hold after the restart 3.5 -> 1 s, fade 1 -> 0.8 s (about 5 s of black now). Play-tested: grey HEE HEE 0-4.9 s, black HEE HEE from 4.9 s, fade 9.5-10.3 s as the rooftop lands.
- **Candy heal way over the cap (user 2026-11-22: "health = maximum health x2, eat candy only heal 5% normal"):** a 4th band in CandyHeal.AboveCap (FloatAddRandom.cs, used by Float Add Random, Player Health Bonus and CandyVacuum): from Player Level `candyTinyHealFrom` 2x max health on, only `candyTinyHealShare` 5% (random rounding, so it averages 5%). Bands now: full to max, 50% to 1.5x, 10% to 2x, 5% above. Play-tested on PlayerHealth's Health1 (roll 1-7, max 120), 300 candies each: health 100 -> 4.03 average, 200 -> 0.40 (10%), 250 -> 0.20 (5%).
- **Purple hole om nom nom:** it had om_nom_nom only at its close, as a Play Clip At Point (fades from 1 m: barely heard at the room's middle). Now om_nom_nom plays as things get swallowed (one at a time) and at the close, half 3D, full volume to 8 m. Tested: plays on the hole after a death.
- **Far grab ghosts on the way (user: "disable the grabbed object's collider and only re-activate it when it gets to Player hand ... if dropped mid way, still fly at Player and re-activate its collider in air before it touches the ground"):** GentleFarGrab now makes the pulled thing ignore every SOLID collider near its way (Physics.IgnoreCollision each physics step; triggers untouched, so FIREKILL props don't die: colliders are NOT switched off). Undone in the hand. Let go mid-way: after 2 physics steps (XRI's throw first) it's launched on an arc to just in front of the chest, still a ghost; turns solid within 0.9 m of the player (keeps 15% speed: drops at their feet), 0.15 m over the floor, or after 1.5 s. Settings under "Ghost on the way". Tested (book 4 m out, 1.1 m block in the way): before = stuck on the block, speed built to 131 m/s; now = straight through to the hand; dropped at 0.2 s = through the block, landed 0.14 m in front of the player. Test: rooms/farghost_test.cs (FG_MODE hold/drop; disables PC controls + Fist during the test).
- **Weapons still hit on the pull (user, 23-06 record):** weapons are 500-750 kg on velocity tracking and lag behind the grab point, so the ghost ended (pull over) while the weapon still flew. Now the ghost holds until the weapon's own grab point is within 0.12 m of the hand (`inHandDistance`, at most `catchUpSeconds` 1.2), and counts every solid collider under the weapon. A let-go while catching up flies to the player too. Tested: Hammer_Broom pulled 4 m through the block, solid 0.05 s after the pull ended, in the hand.
- **Chaos mini boss, fast kill (user: "when Miniboss dies within < 3 seconds after Player starts doing damage to it ... chance to turn into chaos version", then: "the health of miniboss never drop to 0, it stops at 1, 1 more hit and it gone, 50% chance to turn chaos at that stage, recover full health", then "raise chance to 66%"):** EnemyHitDamage.FirstHurtByPlayerAt (player hits, player blasts, knock-outs) + `SpareKillingBlow` hook (MaybeSpare in all 3 player-damage paths). SpiderBrain.SpareTooFastKill: the player's blow that would kill a mini boss (one in Mini Boss Chaos's list, so not slimes; not chaos/anomaly) within `fastKillSeconds` 3 of the first hurt leaves it on 1 health, ONCE; `fastKillChance` 0.66 (asset + code default) it turns chaos right there (health = its full start health x Health Times, aura, EXP, rebirth puff). Not chaos: the next hit kills it. The old no-hurt rebirth on death (75%) is unchanged and separate. Logs "[Mini Boss Chaos] ... too fast, left on 1 health[, turns chaos]". Tested (rooms/chaos_spare.cs): 10 too-fast kills -> 7 turned chaos at 2x full health, 3 held at 1 and died to the next hit.
- **Headset checks left:** door looks, runner range feel, far grab (weapons too) + mid-way drop, the black length after death.

**LIVING ROOM SECRET ROOM + OLD-LOOK GIANTS (second PC, user 2026-11-20 evening): BUILT + bot-tested; nothing committed; no scene change (prefabs only).**
- **User's ask:** "use [the pumpkin textures] for every current enemies bugs, still keep the old texture, the old texture used for bosses in secret room", "another secret room for livingroom ... behind that double doors. Reward = 33 candies or giant cockroach boss with 33 health ... The other door leads to fall edge. EnemyManager On = break secret room."
- **Bug looks:** every bug in Enemies_Smarter already had the pumpkin repaint (checked all prefabs); the legacy Spider/Cockroach/Bee/Tick/Flea prefabs aren't spawned; Paint_Bee keeps its own colour paint (a game rule, not the old look). The two secret giants now wear the OLD look: Spider_Giant -> Spider_Smol_Mat, new Cockroach_Giant -> Cockroach_Smol_Mat (SecretRoomSetupMenu.OldLook: `<Bug>_Smol_Pumpkin(_Ghost)_Mat` -> `<Bug>_Smol_Mat`). "Old" = the brown original; the white boss paint (`_White_Painted_Mat`) is the other choice if the user meant that.
- **Living room (Build Living Room Kit Room, re-run):**
  - **Double door (east):** now breakable: the bedroom kit's Chained_Door parts (Health 300, Damage, break pieces) on LR_Kit_Leaf_Double, called Double_Door; its doorway lost the stop box behind the leaf.
  - **X door (north):** breakable the same way (the W/C door's parts, X_Door); behind it only the round-1 north fall edge. A Wood_Box_Weapon_Main stands in front of it (Stuffs set).
  - **Secret room:** a 3rd RoomKit set "Secret" east of the living room (centre x 9.31, 9 x 9 m, same walls/roof/lamp, no door or window), Secret_Floor (9 tiles from x 4.5 to 14.12, stretched 7% so the doorway has floor), Secret Teleport Area, Secret Room Light. The same SecretRoom component (host set "Living"): opens when Double_Door breaks, light on walking in, giant pumpkin (2.7 m, 2 health), 50% Cockroach_Giant / else 33 candies; folds away whole when Living goes (Enemy2 - Wait plays LivingRoom_GO).
  - **Cockroach_Giant** (Prefabs/Enemies/Enemies_Smarter): Cockroach_Boss variant x1.6 (scale 3.04-3.52, body about 1.9 m long), 33 health, no flag, 3 candies on a player kill, cockroach_screech; no chaos rebirth (not in Mini Boss Chaos's list).
  - **SecretRoom.cs** works along X too: new `alongX`, fields renamed with FormerlySerializedAs (farEdge, sideEdgeA/B, newFarEdge; + moreFarEdges / moreSideEdges). A thin far edge (living room round-1 east) moves past the room keeping its depth. Bedroom menu updated to the new names.
- **Tested** (real bot runs from a new game, `_claude_bot`, tools in Documents/429R_claude_backups/2026-11-20/rooms: `run_secret_lr.sh <stuffs|chaos> <true|false>`, made by make_bot_secret_lr.py):
  - Stuffs + cockroach: 37 PASS, 0 FAIL (door + room + edges + light + pumpkin + Cockroach_Giant old look/33 hp/3 candies/no flags + X door fall + fold at Enemy2 - Wait). Pictures `walls/8_roach_*`.
  - Chaos + candies: 33 PASS, 0 FAIL (fountain of 33). Pictures `walls/8_candies_*`.
  - Bedroom again (after the SecretRoom change), stuffs + spider: 33 PASS, 1 FAIL = the known grow-in timing (Giant Spider measured at 1.72 while still growing to 2.0-2.4). Giant Spider shows the old brown look (`walls/6_spider_2spider_*`).
  - **Giant pumpkin was half under the floor** (user: "should sit on the floor whole"; its pivot is its middle, about 1 m sank): SecretRoom now lifts its bottom onto the floor (both rooms). Re-run chaos + candies: 34 PASS, bottom y 0.00 (`walls/8_candies_1lit_*`).
  - The giants "flying" in pictures = their normal jump attacks (spider pounce, roach face leap; the plain bosses do the same); user: keep them.
  - Both giants took 16-17 blasts of 6 to die: boss toughness on top of the 33 health (as before for the spider).
- **Hatching shake (user: "shake a few time like there is something inside it wanting to come out, like a egg about to hatch"):** SecretRoom.Hatching: every 2-4.5 s (half that once hit) the giant pumpkin rocks 3 times on its bottom edge, 6 degrees dying down (x1.4 once hit); numbers under "Hatching shake" on the Secret Room. Tested (LR run): 35 PASS, 9 rocks in 12 s, tilt 5.5, edge stays at the floor (-0.02).
- **Test gotcha:** execute_code refuses code over 50,000 characters, and run_secret_lr.sh then waits an hour for a bot that never started: make_bot_secret_lr.py now strips comments + indentation (42k).
- **Opening body slam way higher (user: "I want it jumps way higher"):** OpeningCutscene `slamExtraHeight` 8 m: an arc on top of the jump_slam clip, same take-off and landing times. Tested: the boss's root rose 8.00 m (y 1.15 -> 9.15) and landed where it did.
- **Road End Notice (user: "when Player get to the end of the road, show a notice"):** the start rooftop's long south arm (to z -33.4) leads nowhere: new RoadEndNotice on Rooftop_Base_Z (Tools > 429 Game > Menu > Road End Notice, run) shows the Tutorial Notice "NOTHING HERE YET" / "There is nothing here yet, please come back and break them pumpkins. Thank you for being here though. Love u and be safe!" when the head is in the last 3.6 m (x +-5.3); again only after walking 1.5 m+ away. Only on the rooftop spawned in "Walk Tuttorial" (the same prefab comes back for Rooftop_ON / CP Boss / Rooftop_ON PG fights: no notice there). Tested in Play: shows (game paused), not again while standing there, again after walking back and returning.
- **Candles by the big living room picture (user: "add 2 Candle_Hang on 2 sides of this big picture"):** 4 new spawn points, Candle_Hang_Picture_Pos_S / _N in --Livingroom-- and --Livingroom_Chaos-- at (-4.5, 1.6, -2.15) and (-4.5, 1.6, 1.45), rot 90 (copies of the playroom's Candle_Hand_Pos, start state "Create" instead of its 5 s wait, so StaggeredSpawn sees them finish), inactive and added to each set's StaggeredSpawn points. Scene SAVED (backup living_secret/2 Game Scene_before_picture_candles.unity). Seen in the chaos run's west wall photo: both lit beside the picture.
- **Mini boss looks (user: "Mini Boss Pumpkin Butt use the old texture instead pumpkin texture. White Mini Boss ... Green Mini Boss, like green pumpkin"):**
  - Spider/Cockroach/Bee_Boss_PumpkinButt: `<Bug>_Smol_Pumpkin_Mat` -> the old `<Bug>_Smol_Mat` (3 model renderers each; butt/crown/skull untouched).
  - Spider/Cockroach/Bee_Boss (white): ghost -> new `<Bug>_Smol_Pumpkin_Green_Mat` (copy of the pumpkin mat) with `<Bug>_Enemy_Pumpkin_Green_Color.png`: the orange rind recoloured to a deep green (hue ~105, 62% sat, 60% value), olive legs/stems, cream jaws/wings and glowing eyes (glow map) kept. Made by Documents/429R_claude_backups/2026-11-20/green_boss/green_pumpkin.py (`uv run --python 3.12 --with pillow --with numpy python green_pumpkin.py --write`; never overwrites the orange). Import settings copied from the orange texture.
  - Giants keep the old look (variant overrides), normal/small bugs the pumpkin. Ghost mats/textures kept (the dormant anomaly bug still uses them).
  - Renders: green_boss/renders/Bosses_green_and_old.png. Prefab backups: green_boss/prefabs_before/. Not seen in a dark room or the headset.
- **Pumpkin-butt mini bosses 55% bigger, 15% faster (user):** on the 3 variants only (SpiderBrain boss.scale x1.55, boss.speed x1.15, which multiplies walk/run/walk-back): Spider (1.25-1.50 -> 1.94-2.32, 1.17 -> 1.345), Cockroach (1.90-2.20 -> 2.94-3.41, 1.235 -> 1.42), Bee (2.20-2.50 -> 3.41-3.88, 1.1 -> 1.265). The white bosses keep theirs. Play check: grown sizes 2.26 / 3.34 / 3.47; chase speed spider 2.64 vs 2.29, roach 3.18 vs 2.77 (x1.15). The bee is now about 2.8 m tall in 3 m rooms. Their chaos rebirth comes from the same prefab, so it's big too.
- **Random size for every enemy (user: "every enemy can have random size and health, range 33%, smaller = faster, less health, bigger = more health, slower"; "build it like that"):**
  - SpiderBrain (all bugs, Spider_Tree, the PlayGround slimes) and FatPumpkin: "Random size" (randomSize on, sizeRoll 0.75-1.33 spread evenly in log size, so even odds smaller/bigger), on TOP of the size class's own scale roll. Health x roll, rounded, at least 1 (after the class's health roll; chaos rebirth still x2 after). Speed: x1.2 at 0.75, x1 at 1, x0.8 at 1.33, straight between (SpiderBrain Pace = kind.speed x roll pace, used for walk/run/walk-back; FatPumpkin walkSpeed). Fliers capped at 1.15 (flierMaxRoll). The white anomaly bug doesn't roll.
  - OFF (randomSize false) on Spider_Giant, Cockroach_Giant (exactly 33 hp) and the 3 pumpkin-butt bosses. Pumpkin_Boss_Z, the giant slimes of the special stages, Flying_Slime and tree pumpkins (own RandomPumpkinSize) don't use it.
  - Test hooks: SpiderBrain.ForceNextSizeRoll, FatPumpkin.ForceNextSizeRoll; SizeRoll on each.
  - Tested in Play (size_test.cs): 30 random small spiders 0.76-1.32 (mean 1.06, 17 bigger), 12 bees max 1.08 (cap), spiders at 0.75/1/1.33: health 6/9/12, Fat Pumpkin 1.33: health 8, walk 1.20; 0.78: health 5, walk 1.77. Bug chase speeds measured live were too noisy (attacks, leaps) to show it; the speed rule is the same code as the Fat Pumpkin's.
- **Fat Pumpkin easy until giant (user: "should be easy to kill at start, only in the giant phase its health would boost"):** before, every candy it ate added +2 health at once (it wakes by 4+ candies, so 20+ before the player got there) and Toughness 2 applied from the start. Now: Health 6 (x its size roll) and new smallToughness 1 until it turns giant; each candy's +2 is SAVED for the giant; turning giant = Giant Health 150 + what it saved (candies, won fights) on top (was max(health, 150)); the giant keeps Toughness 2. Tested in Play: woke 6 / toughness 1; 5 candies -> health 6, saved 10; a blast of 1 -> 5; giant -> 160, toughness 2.
- **Backups:** Documents/429R_claude_backups/2026-11-20/living_secret/ (Livingroom_Kit_Base, Bedroom_Kit_Base, Spider_Giant, the LR controller, SecretRoom.cs before, Rooftop_Base_Z before the notice, OpeningCutscene before).

**FRYING PAN + BASEBALL BAT IN THE GAME (user, 2026-11-20 late: "only pan and baseball can be used. Check Broom prefab, do pan and base the same way, try different stats, I already make the broken parts. Check and go."): DONE + Play-tested (PC controls); not committed; no scene change.**
- **Models:** the user's fixed New_Weapons.blend (game materials Wood_Furniture + Bedroom_Metal on their UVs; pan broken parts Weapon_Frying_Pan_break.000 (handle) / .001 (pan), bat cell fracture Weapon_Baseball_Bat_cell/.001/.002). Exported by Exports/Weapons_New/tools/export_weapons.py to Assets/Models/Weapons/Weapon_Frying_Pan(.fbx|_Broken.fbx), Weapon_Baseball_Bat(...) (axes baked: +Y up, pivot at the handle end, scale 1, no materials imported).
- **Menu:** Tools > 429 Game > Weapons > Set Up Frying Pan And Baseball Bat (Scripts/Weapons/Editor/PanAndBatSetupMenu.cs, re-runnable): prefabs Prefabs/Weapon/Weapon_Frying_Pan + Weapon_Baseball_Bat = copies of Hammer_Broom (grab, wear Health FSM, EnemyHitDamage furniture wear, smoke, sounds) with their mesh + Wood_Furniture_Mat/Bedroom_Metal_Mat, the 2 box colliders refitted (head / handle), Attach at the user's Grip (y 0.12) turned end for end from the broom's (the broom's business end is down), smoke at the head, broken pieces Prefabs/Items/Broken_Stuffs/<name>_Broken_Part (copies of Broom_Broken_Part). Items Data/Inventory/Items/Weapon_Frying_Pan / Weapon_Baseball_Bat (+ icons, in the item database), Candy Shop rows (pan 16, bat 18 candies), and Random_Weapon's FSM array variable WeaponArray (the weapon box: 13 now; NOTE the ArrayGetRandom uses the variable, so edits go through fsm.variables.arrayVariables).
- **Stats** (broom: power 1.3, damage 1, wear 16, two hands x1.5, 13 candies): pan power 1.7, damage 2, wear 40, toughness 6, one hand (TwoHandedGrip removed); bat power 2.2, damage 2, wear 24, toughness 5, two hands x1.6.
- **Tested (Play, PC controls, bot profile):** both grabbed by the handle, held upright, head up (pictures Documents/429R_claude_backups/2026-11-20/pan_bat/). Pan on a Spider_Normal (10 hp): 4, 4 (knocked it 7-8 m away), then a hard swing 6 (SMASH) killed it; wear 0.25 a hit. Bat: hit for 2, then the PC test's fixed chop swung over a spider biting at the hand (long bat; not a prefab issue). Wear 0 -> both vanish and drop their pieces. Test gotcha: PC-controls crosshair grabs right after Play starts can miss (and a dropped 500-mass weapon crushes a spider under it); a level view + G press works.
- **Tricks (user, same night: "add the pan stun and bat knock-back tricks. Use metal_hit_1 2 for Pan"): DONE + Play-tested.** WeaponPower has new held-only settings (Header "Tricks"): Stun Seconds, Knock Back Times, Hit Sounds (+ volume); WeaponPower.HeldTrick(collision) = the held weapon if it has any. EnemyHitDamage: a held trick weapon that hurts an enemy plays a hit sound at the contact, and (instead of the big-hit-only knock-back) knocks it back x Knock Back Times (0.4 if none) and stuns max(Knock Back Stun, Stun Seconds), a hard hit x1.5, a smash x1.5 farther; a stun shows dizzy stars on ANY enemy (KnockBackFromPlayer shows them on mini bosses only). Pan: stun 1.6 s (hard 2.4), small shove, sounds Audio/SFX/metal_hit_1 + _2. Bat: knock-back x2.5. Set by the same menu (re-run).
  - Tested (spider placed where the PC chop lands): bat normal hit 2 dmg -> flew 4.2 m, smash 6 -> 7.9 m, kill 4 -> 5.8 m (before: no knock-back on normal hits, smash ~1.5-2.25 m). Pan hard hits 4 dmg -> shoved ~1 m, spider in Flinch with Stun_Stars up, 3 hits killed it. Exact stun length and the sounds not measured (audio not checked by ear).
  - Test gotcha: the PC swap script's "drop" G press on an EMPTY hand starts an empty grab that keeps the hand busy, so the next G (the real grab) is ignored: on a fresh Play, skip the drop press.
- **Not done:** headset feel; the bat still uses the broom's hit sounds.

**NEW WEAPONS (user, 2026-11-20 late: "do pan, giant fly swatter, baseball bat and rolling pin. Do base model for them, I will fix it then you help me put them in the game, and add them in store and weapon_box"): BASE MODELS READY, the user fixes them next.**
- **File:** Exports/Weapons_New/New_Weapons.blend (tools/build_weapons.py; won't overwrite without --force), picture New_Weapons_v1.png. Objects Weapon_Frying_Pan (0.62 m, pan 0.32 across, flat bottom faces +X), Weapon_Fly_Swatter (0.9 m, 0.28 x 0.30 grid head, face +X), Weapon_Baseball_Bat (0.84 m, taped handle), Weapon_Rolling_Pin (0.56 m, pivot in the middle). Like the broom: long axis +Z (= Unity +Y), pivot at the handle end; each has a "<name>_Grip" empty = where the hand goes (the prefab's Attach). Material slots by colour role (wood, wood_dark, wood_light, iron, iron_light, plastic, plastic_dark, tape) for palette UVs later. 230-410 tris.
- **Hookup plan (after the user's fix):** export FBX to Assets/Models/Weapons/ (bake the axes, scale 1); prefabs from Prefabs/Weapon/Hammer_Broom.prefab as the template (XRGrabInteractable + Attach at the Grip, Rigidbody, WeaponPower, EnemyHitDamage, HitRumble, PlayMaker FSMs, box colliders fitted to the head + handle; TwoHandedGrip for the rolling pin); add to the shop list and to Prefabs/Random_Item/Random_Weapon.prefab (what Wood_Box_Weapon_Main drops). Tricks agreed: pan = hard hit stuns (stars) + flat blocks bites; swatter = small bugs flattened in one hit, 1 dmg on big; bat = hard swing knocks bugs much farther (smash knock-back up); rolling pin = two-handed, a hit from above squashes then stuns.

**WORKING SITE ON ALL 3 GATES (user, 2026-11-20 late: "Yes, add to both"): DONE + Play-tested; the scene is NOT saved (the user saves it: it also had unsaved changes from before that aren't Claude's).** Tools > 429 Game > Menu > PlayGround Work-In-Progress Notice now adds the WORKING SITE Show Notice (same text, 1.5 s after the fade, every visit) to GAMESTAGES "Path 1 ON", "Path 2 ON" (Slime Bowling) and "Path 3 ON" (Ball Valley); re-runnable. BallValleyGate's own rules notice ("BALL VALLEY ...") fired at the same 1.5 s and replaced it, so it now waits while another notice is up. Tested (jump to Clean for Path N): gate 2 shows WORKING SITE; gate 3 shows WORKING SITE, then BALL VALLEY, then play. Gate 3 Pick (random gate 1/2) is unused now (Stage 1's next3 goes to Clean for Path 3).

**FURNITURE REMODEL SKIPPED (user, 2026-11-20 late: "they are not good enough to use. I am going to skip this part for future. Let's make the game fun and playable first").** Nothing was exported to the game; the game FBX files are untouched. The file and tools below stay for later.

**FURNITURE FIX FILE (user, 2026-11-20 night: "bring [storage room + bedroom breakable furniture] 3D model into a new blend file, even the broken parts ... we fix them there a bit, then you help me re-import them"): FILE READY, the user is fixing; RE-IMPORT is the next step.**
- **File:** Exports/Remodel/Furniture_Fix/Bedroom_Furniture_Fix.blend (overview picture next to it). 15 pieces = the Set Up Breakable Furniture pieces in --MiniBedroom(_Chaos)-- and --Bedroom(_Chaos)--: Bed (+ pillow, sheet), Bed_Stool, Coffer, Shelf_01 (both shelf prefabs), Shelf_03, Stuffs_Shelf, Work_Table, Work_Table_Chair, LivingRoom_Chair, Livingroom_Table_2_small, Candy_Basket, Wood_Box (both box prefabs), Shelf_Hang, Shelf_Hang_2, Clock. 28 FBX files, 123 objects.
- **How it's laid out:** collection Furniture_Fix > "<Piece>" > "<Piece> - whole" / "<Piece> - broken" > one collection per source FBX ("<file>.fbx", custom props source_fbx + layout_offset). Whole piece left, its broken pieces (assembled) right beside it. Labels collection = text only, not exported. Materials merged to one per name (the names Unity remaps: Wood_Furniture, Bedroom_Metal, Bedroom_Wood_Floor, Bedroom_Furniture_Set1/2, Sofa_Furniture) showing the game textures (linked, not packed).
- **Rules for the user's edits (so the re-import keeps every prefab link):** keep object names; keep each object in its "<file>.fbx" collection; keep the material names and their slot order; new objects in a file's collection get exported into that FBX (they need a prefab change to show up). Moving a whole piece in the layout is fine only if moved with its whole collection (layout_offset undoes it).
- **Shared:** Work_Table_Chair and LivingRoom_Chair have NO own broken pieces: both prefabs drop Bedroom_Bed_Stool_Drop (Bedroom_Work_Table_Chair_Drop prefab). Shelf_01_Drop.fbx serves both shelves, Wood_Box_Drop.fbx both boxes. Shelf_03_Drop.fbx holds a Magical_Seed object. Wood_Box shows Bedroom_Wood_Floor in Blender but uses Jenga_Wood_Spooky in the game.
- **Re-import (tested on 4 files into a temp folder, then deleted):** `"E:/Steam/steamapps/common/Blender/blender.exe" -b Exports/Remodel/Furniture_Fix/Bedroom_Furniture_Fix.blend --python Exports/Remodel/Furniture_Fix/tools/export_furniture_fix.py -- C:/Github_Repository/429R [--only a.fbx,b.fbx]` writes each FBX over its original path (moves the layout back first; doesn't save the .blend). Round trip of untouched files: same mesh names, triangles, bounds, object transforms, material order. Originals backed up: Documents/429R_claude_backups/2026-11-20/furniture_fix/fbx_originals/ (28 FBX + meta). After export: refresh Unity, check the prefabs (render), colliders may need refitting if a shape changed a lot (BoxColliders on the prefabs aren't from the mesh).
- **Gemini first (user: "Dunno how to fix, maybe need a prompt for gemini to update to concept image"):** Exports/Remodel/Furniture_Fix/Furniture_now_sheet.jpg (the 15 pieces numbered, real sizes, broken pieces pulled apart; made by tools/render_pieces.py, pictures in pieces_now/) + PROMPTS.md (1: the 15 redesigned in the Bedroom_Concept/concept_v1.png look, same numbers and sizes; 2: how each breaks, exploded; 3: the room with them; 4: one piece front/side/back/top to model from). Waiting on the user's Gemini pick (concept_v1.png / concept_broken_v1.png in that folder).
- **New models, first pass (user: "help me model these" + their Gemini pick, 15 numbered pieces):** tools/model_furniture.py rebuilt all 15 WHOLE pieces into Bedroom_Furniture_Fix.blend from the concept (the user's clean style: closed boxes, quads, planks with 6 mm gaps, no bevels; 60-350 tris each), each built front -Y, turned to the old piece's facing (bed head +Y, desk drawers -Y, coffer/cupboard/dresser/rack/table fronts -X, chairs' backs -Y, wall shelves' wall -Y, clock face -Y) and fitted to the old footprint + floor, then written INTO the old objects (names + FBX transforms kept). Old meshes: hidden collection Old_Reference (*_OLD). Colours: Palette/Furniture_Palette.png (14 swatches: wood, wood_dark, wood_top, iron, 5 quilt, cream, copper, clock_face, black, pillow) via ../tools/palette_kit.py palette_uv, ONE material Furniture_Palette (matte). Picture: Furniture_new_v1.jpg (pieces_new/). Re-run = copy Documents/429R_claude_backups/2026-11-20/furniture_fix/Bedroom_Furniture_Fix_imported_originals.blend over the file, then `blender -b Bedroom_Furniture_Fix.blend --python tools/model_furniture.py -- <repo> --save` + tools/matte_palette.py. NOT done: broken pieces (still the old ones), the "?" on the weapon box (dropped), Unity (one palette material instead of 2 slots: the prefabs' renderers need the new material).
- Blender on this PC: E:/Steam/steamapps/common/Blender/blender.exe (5.3). The MCP for Blender add-on here is outdated (`uvx mcp-for-blender install-addon`, then re-enable it).
- **Not tried:** the headset; breaking the 300 hp doors by hand.

**START HERE TONIGHT (K: PC, 2026-11-20 day; the user goes on tonight on the other PC). Nothing committed today; the scene is saved.**
- **Bedroom secret room + door move + random tree pumpkin sizes:** built and Play-tested in real bot runs. Details are in "BEDROOM SECRET ROOM" below.
  - The secret room now breaks away whole when the bedroom fight starts (user: "when the EnemyManager is on, break the secret room"). Tested: 29 PASS, 0 FAIL.
  - Open question for the user: the 50/50 spider/candies split (Secret Room > Spider Chance on Bedroom_Kit_Base) was my default. The chained door's 300 hp is unchanged and takes a long time by hand.
  - The user's next step: they look at the pictures in `Exports/Rooms_2026-11-20/` (`7_secret_room_*.jpg`, plus the 10 room sheets `1_storage_* .. 4_playroom_*`) and say what to change or fix in each room. The other rooms are untouched.
- **Rooms photographed** (user: "play test like a normal player, so the previous stuffs always get cleaned up"): all 4 rooms in real runs, normal / chaos / pumpkin furniture, 4 walls + a plan each, in `Exports/Rooms_2026-11-20/`. NEVER use GAMESTAGES checkpoint jumps for room pictures (they stack old rooms).
- **Bug looks today** (each has its own entry below): pumpkin repaints of the spider, cockroach and bee, and of their bosses (ghost-white pumpkins on the white bosses, orange bugs under the pumpkin butts). The dormant white anomaly bug uses the ghost look too.
- **Also today** (entries below): aim lock at wind-up (8 enemy scripts) and PC controls in the Editor.
- **Unity crashed once (16:20) in its own Profiler** (ThreadDeserializer, while a bot run spawned a room's furniture). Switch Profiler recording off before long Play tests: Window > Analysis > Profiler, untick Record, or `tools/profiler_off.cs`. After that restart, "MCP for Unity" needed Start Session again, and Unity's Pipeline port 7801 stayed held by the dead process.
- **Test tools** are copied to `Exports/Rooms_2026-11-20/tools/` (the originals are on the K: PC under K:/WORK_2026/08_04/_claude/rooms_2026-11-20/). They are C# 6 scripts for the MCP for Unity :8080 runner (`pt.sh` -> `mcp_exec.ps1`) and use K: paths, so change the paths on the other PC.
  - `run_bot.sh <stuffs|chaos|pumpkin>` = room photo run.
  - `run_secret.sh <stuffs|chaos> <false|true>` = secret room test (true = spider outcome).
  - Both use the bot's own save profile `_claude_bot`.

**SMOOTHNESS + PURPLE HOLE + BEDROOM KIT + LIVING ROOM KIT (second PC, 2026-11-19 evening/night). Scene SAVED 20:09 (user OK); BOTH kit room swaps after it are NOT saved yet; nothing committed.**
- **Smoothness:**
  - **How it was measured:** an editor profile of the whole bedroom flow, with a bot playing on the throwaway save. Tools: Documents/429R_claude_backups/2026-11-19/smooth (prof_tick, bot_tick, smooth_run.py).
  - **Steady cost:** small, 2-3 ms of game work a frame.
  - **The freezes were room furniture sets.** Every spawn point creates its object the moment it's switched on, all in one frame: 97-162 ms.
  - **Fix:** StaggeredSpawn on the 10 room sets (Tools > 429 Game > Performance > Spread Room Set Spawns). Their simple spawn points are kept OFF in the scene and switched on 2 a frame. Restore Room Set Spawns undoes it. Result: mini room 162 -> 32 ms, bedroom 103 -> 7 ms.
  - **Sounds:** Preload Short Sounds set 182 clips under 1 MB to preload and 7 bigger ones to load in the background (189 .meta files changed).
  - **Not done, to suggest:**
    - Windows IL2CPP: the PC build is Mono, and first-time code costs 20+ ms; the module isn't installed.
    - SaveManager writes on the main thread (7.7 ms).
    - Enemy and piece spawn bursts (10-17 ms): pooling.
    - Smoke puffs at the head (GPU).
    - No headset check yet.
- **Purple hole** (user: "suck in everything else", candy stays):
  - **What it is:** PurpleHole on the Fairy Shield (Shield_Sphere), made by Tools > 429 Game > Effects > Purple Hole along with Prefabs/Effects/Purple_Hole.prefab, using the wall portal's materials. Sounds: whoosh.wav, om_nom_nom.wav.
  - **Where it opens:** all 9 shield uses: 8 GAMESTAGES clean-ups and PlayerDeath DEAD. It opens 5.5 m above the shield's centre.
  - **What it eats:** loose or breakable stage things within 14 m. They fly in as copies of their looks; the originals are removed at once.
  - **What it keeps:** candy, the player and what they hold, Fairy, Knight, bosses, anything named *_Base*/Skydome/Valley/Island, and anything bigger than 6 m.
  - **The shield** still sweeps, unseen, from 2.6 s. Smoke_Blast_Brown/Green are switched off while it eats.
  - **Tested:** the mini room clean-up ate 102 of 110 things, the room and candy stayed. Pictures in .../2026-11-19/purple_hole/pics.
- **Bedroom kit:**
  - **Files:** Exports/Remodel/BEDROOM_KIT_PLAN.md (analysis + swap steps) and Exports/Remodel/Bedroom_Kit.blend, made by tools/bedroom_kit.py (no overwrite without --force).
  - **What it is:** 3 m pieces with today's wallpaper and wood textures, sample rooms at 7.5 x 7.5 m.
  - **Window:** the user picked the old room's Wall02 window (86 faces), now in the kit window piece. Done live in their open Bedroom_Kit.blend, which THEY save; the script rebuilds the same piece from Bedroom_Walls.blend.
  - **Doors** (user: "2 kinds of door, 1 secret door as a short cut, 1 normal locked door"):
    - **BR_Kit_Door_Wide:** a doorway with the user's Wall05 frame.
    - **The two leaves**, separate objects that snap into it: BR_Kit_Door_Secret (Bedroom_Base_Z_Door, horseshoe sign, the small room's breakable shortcut door) and BR_Kit_Door_Locked (Bedroom_Base_Z_Door_2, W/C). Both whole and unchanged.
    - **BR_Kit_Door** stays as the open passage.
    - Done live (the user saves).
  - **Clean mesh** (user: "I want a clean mesh, not a messy kit"): the pieces were rebuilt the user's way (see the plan): one closed 0.345 m slab per wall with the fewest quads round openings, plus box baseboards, inner corner posts, and the user's door frame and window. No bevels, no crown, all quads, closed. Done live; the user saves.
  - **User edits:** removed the chain from BR_Kit_Door_Secret ("so it looks more breakable"), saved. The file is dirty again (roof, rim and lamp pieces added live, material slots repaired): THE USER SAVES Bedroom_Kit.blend. Export: `blender -b Exports/Remodel/Bedroom_Kit.blend --python Exports/Remodel/tools/export_bedroom_kit.py -- <repo>/Assets/Models/Rooms/Bedroom_Kit.fbx` (13 meshes).
- **Bedroom kit ROOM (user: "Yes, build it", same evening): BUILT, SWAPPED IN, editor-tested. The scene change is NOT SAVED yet (the user saves it).**
  - **Menus:** Tools > 429 Game > Rooms > Build Bedroom Kit Room (makes Prefabs/Bases/Bedroom_Kit_Base.prefab + Models/Animator/Bedroom_Kit_Animator.controller), Use Bedroom Kit Room (Bedroom_Base_Pos spawns it, and fits the furniture), Use Old Bedroom Room.
  - **How it moves:** RoomKit (Scripts/Rooms/RoomKit.cs) does the old clips in code: walls unfold from flat + grow with overshoot, the roof flies in last; going, the roof flies off first and the walls hop, fold and shrink. The floor never moves. The Animator keeps the old state names (RoomKitState on each), so GAMESTAGES, the light path and DropWallHungProps need no change. Hidden sets are invisible + not solid but stay ON, so the doors' FSMs run from the start like the old room.
  - **Doors:** the old doors' own parts (health, damage, wobble, break pieces, coin toss) on the kit leaves. The secret door hangs on the old hinge, stands ajar 14.4° and slams shut when the player is on the floor (its trigger now watches the kit's Floor Trigger; Close Door turns it in its own space). Its break pieces are Door_Break_Drop_No_Chain (a variant with the chain + padlock pieces unseen; they still turn into pumpkins).
  - **Layout** (from the old come clips): mini room secret door on the north wall, window on the south; big room W/C door on the west wall, chained door on the north wall, windows on the east (z -0.75) and south (x 0.75) walls.
  - **Furniture:** the mini room was 8.1 x 7.7 m, so 97 furniture spawn points in the 6 bedroom sets moved in (up to 0.37 m) to fit 7.5 x 7.5. Touching pieces move as a group (stacks stay neat), falling Chaos pieces start under the 3 m ceiling. Hung props now sit flush on the walls.
  - **Tested** (bot, throwaway save, 4 runs): both rooms come and go on time, light on/off, the hole, the furniture fit (0 pieces in the walls), the secret door's hits, wobble, break and coin (skull both times: the pumpkin boss folds the room). Pictures: Documents/429R_claude_backups/2026-11-19/bedroom_kit/build/play*. Not tried: the clover shortcut, the headset.
  - **Chained door (user: "Yes, add it"):** new kit piece BR_Kit_Door_Chained = the horseshoe door WITH chain + padlock (the old big room's second locked door), added live to the user's open Bedroom_Kit.blend (THEY SAVE it) and exported (14 meshes). In the big room's north wall, centred x -0.75 (old -1.4; the 1.5 m grid), with the old door's Health/Damage FSMs (300 health, tough furniture hits, regular break pieces with the chain). The welcome mat spots (Bedroom_Matt_2_Pos in --Bedroom-- and --Bedroom_Chaos--) follow it (+0.69 m). Tested: it breaks and the doorway opens.
  - **Not in the kit yet:** the bunting and curtains (part of the old wall mesh).
  - **Old room:** Bedroom_Base_Z.fbx/prefab stay (the living room is the same model).
- **Living room kit ROOM (user: "Yes, build it", same night): BUILT + SWAPPED IN (scene NOT saved).**
  - **Menus:** Tools > 429 Game > Rooms > Build Living Room Kit Room / Use Living Room Kit Room / Use Old Living Room Room (LivingroomKitSetupMenu.cs).
  - **Files:** prefab Prefabs/Bases/Livingroom_Kit_Base, controller Models/Animator/Livingroom_Kit_Animator, kit Models/Rooms/Livingroom_Kit.fbx (export: tools/export_livingroom_kit.py).
  - **Sets:**
    - **Living** (9 m): X door north, big double door east, the shortcut horseshoe door south with the old door's parts.
    - **Play:** the cross at 1x; the game still scales the room 1.5x. The 4 old brick walls (+ portals) close the arms, the old breakable ceiling is its roof, and its arm floors stay after GO4.
    - **Round-1 fall edges:** they watch a "Livingroom_Floor" marker under the Play floor, so they switch off when the playroom comes, as before.
  - **Furniture:** 36 spawn points moved. Living room pictures go out 0.21 m to the new walls, the playroom candles go in to the arm ends, and the 4 Jenga gates (2.33 m, wider than the 2.25 m arms) now stand in front of the arm openings.
  - **To redo the fits after a change:** reload the scene, then run BOTH Use … Kit Room menus once.
  - **Tested** (bot, throwaway save, 7 runs; save deleted after): the living room comes on spawn and goes at EM2 (light off); the playroom comes after the 1.5x scale and goes at GO4; the brick wall breaks; hung props sit flush (gap 0.01); the playroom fit is fine.
  - **Shortcut door tested** (user: "Yes, test it"): 75 health, two 60 hits break it, its chained pieces drop, the coin tosses (50/50) and the door is gone; the stage goes on. That run's coin was a skull (hurries the pumpkin boss); a clover broadcasts DOOR_SHORTCUT like the old door (GAMESTAGES global -> Door Shortcut).
  - **Game Over at the end of the playroom (2 test runs): SOLVED, a test artifact, not the kit.** A big-hit logger caught it: two Fat_Pumpkin_Enemy stomping the standing-still bot player (DamageHeavy 40-50 each, ~1 s apart). They are tree pumpkins woken by the candy from the bot's kills; the tree belongs to the starting rooftop, which the real game removes in "2 - The BR" (DestroyObject Rooftop_Base). The test jumps from Move to Fairy straight to CP LivingRoom / CP Scale LR, so that rooftop stays under the playroom. The bot can't kill fat pumpkins and only heals below 60, so two quick stomps kill (Game Over without revive coins).
  - **Test tools:** .../2026-11-19/livingroom_kit: lr_kit_run5.py + lr_kit_watch5.cs.txt (the kit watcher + player FSM snapshots at Rooftop_ON / Game Over + the shortcut door's states + a big-hit logger listing what's near the player). Gotchas: read prefab FSMs only after Fsm.Init, or LoadActions gives zeros and nulls (and console errors); for a clean test from a CP jump, destroy the Rooftop_Base var's object first.
- **Living room + playroom kit (user, same night: "check the next stage, analyze them, check their animation, then create a kit of livingroom and playroom"): KIT MADE.**
  - **Plan + analysis:** Exports/Remodel/LIVINGROOM_KIT_PLAN.md (rooms, doors, clips, flow, what the swap needs).
  - **Kit:** Exports/Remodel/Livingroom_Kit.blend (new file), made by tools/livingroom_kit.py. Renders in livingroom_kit_renders/.
  - **Pieces:** walls 3 / 1.5 m; LR_Kit_Door (1.40 m doorway + the user's frame) and LR_Kit_Door_Big (2.47 m); leaves X (chained), Double (big door) and Horseshoe (shortcut door, chained); floor; the old roof slab and lamp. Slabs 0.31 m, the wallpaper once up each 3 m wall, no baseboards (the panelling is painted).
  - **Sample rooms:** living room 9 x 9 m; the playroom cross at 1x (square 7.5 m, arms 1.5 x 3.3 m; the game scales it 1.5x).
  - **Waiting on the user:** check the kit and answer the sizes / 3 m height / reuse-the-breakable-ceiling-and-brick-walls questions, then "build it".
  - **Analysis tools:** Documents/429R_claude_backups/2026-11-19/livingroom_kit (lr_watch.cs.txt + lr_run.py: bot from CP LivingRoom; lr_walls.py: wall faces from the baked mesh). Gotchas: SkinnedMeshRenderer doesn't re-skin between renders in one editor call (bake it). After a CP jump from the rooftop start, the rooftop castle stays under the living room (test artifact).

**RUSH MOMENTS (user picked 2026-11-19, from their 10-22 wish "more enemies = more chaos"): BUILT + Editor-tested in both rooms (second PC, bot on a throwaway save; real saves untouched). Menu run in the open scene; the scene still needed SAVING at hand-off (check it has a Wave Rush on -EnemyManager 1 and 3). Not tried in the headset; nothing committed.** New WaveRush (Scripts/Enemies/WaveRush.cs) on -EnemyManager 1 (Bedroom) and -EnemyManager 3 (Playroom) via Tools > 429 Game > Enemies > Set Up Rush Moments. Once 6 of the room's 12 are down, 12 small bugs (Spider_Small / Cockroach_Small) pour in from the room's own spawn points (SpiderIdleSpawns / BeeSpawns), up to 8 at once (Enemy Limit still caps), with a laugh / roach screech + ShakeBigger; they join the manager's wave (and its CurrentlySpawnedStuffs holder, so Game Over clears them), so its own spawns and its boss wait; beat them all = 8 random candies burst where the last fell + a bell (give-up 45 s: reward by the player). Once per wave; re-armed on a new wave/loop (the manager's Init sets numberOfEnemies 0). All numbers on the Wave Rush component.
- Test (both rooms the same): rush started on the 6th kill; 8 alive at once, 12 in all; the manager sat in Wait for Slot while they were out; candies burst at the last kill; then the rest of the wave and the boss (Spider_Boss_PumpkinButt / Call Boss). A player standing still with 9 bugs on them died in ~20 s.
- The 11-09 balance list was already DONE 2026-11-10 (only a headset test left).

**00. Hit feel (K: PC, 2026-11-19, user picked "fixes 1-3"): built, compile-checked, Editor-tested with shots at a Spider_Normal (bot profile, real saves untouched). Not tried in the headset; nothing committed.**
- **Each fist or thing has its own hit window** (EnemyHitDamage, Fat Pumpkin): before, one shared 0.3 s wait per enemy silently ate a left-right combo's second punch and 2 of a Triple Punch's 3 fists. The same thing hitting again within 0.3 s is still ignored. Every Hit Counts now only means "a soft hit still does 1". Tested: two shots landing in the same frame both hurt; the same shot again 0.12 s later did not, 0.6 s later did.
- **SMASH, a third step** (Hit Settings > Smash): power 16 or more (twice a hard hit) does x3 (a throw x4, a flying jab +2), with a 1.6x pop, a crunch (rock_hit_1-3, swap them in Hit Settings), knock-back x1.5 farther / x1.4 higher and 0.3 s more stun. Tested: 6 damage from a weapon with Damage 2 (hard 4); knocked 2.25 m vs 1.50 m.
- **Hit freeze, the enemy only** (new HitFreeze.cs; Hit Settings > Hit Freeze): the player's hit that hurts stops the enemy dead (animation + walking): 0.04 s normal, 0.07 hard, 0.11 smash, +0.05 on a kill; no new freeze within 0.12 s after one (kills always). The knock-back waits for it (Knockback.Hold). Tested: the animator held at 0 for exactly those times and its speed came back, and the body stood still and then flew. Not on the Fat Pumpkin or the pumpkin ball boss (they get the smash crunch only). Not tried on pumpkin_boss_Z.
- **One stat, one move (fix 4, same day):** Fist Size now also adds to the closed punch: +1 damage every 3 points (Player Level > Punch Damage Every; a bare fist still taps for 1). Throw Power adds to thrown damage: +1 every 2 points (Throw Damage Every), on top of each thing's Thrown Bonus, so a hard throw triples it. Candy points count. The Level Up menu shows "punch +N" and "+N damage". Tested: punch 2/3/4 at Fist Size 3/6/9; at Throw Power 10 a thrown book did 7, and 21 when thrown hard (it did 2 / 6 before).
- **Red hit blink** (new HitBlink.cs; Hit Settings > Hit Blink): an enemy a hit hurts turns red (paint towards red + a little red glow), stays full for a third of the time, then fades: 0.14 s normal, 0.18 hard, 0.24 smash. It uses property blocks like the bee-boss bomb flash; each part's own block is put back afterwards (tested: cleared, so it batches again). Works on every EnemyHitDamage enemy (bugs, bosses, slimes, pumpkin_boss_Z) and the Fat Pumpkin. Not on furniture or things with Hide Hit Effect. The runner pumpkins and the pumpkin ball boss already had their own red flash and keep it. The chase pumpkins (Pumpkin_3/5/7, PlayMaker only) don't blink. Tested on a spider and a giant slime.
- Feel audit numbers (enemy health/toughness, damage per stat, hits to kill): Claude's memory 429r-feel-audit-2026-11-19 (K: PC).
- **EnemyManager 2 spawn points moved onto the NavMesh (scene SAVED by Claude, 2026-11-19; backup Documents/429R_claude_backups/2026-11-19/2 Game Scene_before_em2_spawns.unity).**
  - **What was wrong:** in the Living Room, 7 of the 8 points were off the NavMesh: 0.3-1.3 m past its edge (±3.67 m when the room first appears, ±4.25 after LivingRoom_GO), or under furniture.
  - **Why it rarely showed:** three things saved them. The FSM snaps each spawn to the NavMesh within 10 m, LandOnNavMesh catches any enemy that still appears off the mesh, and SpiderBrain re-places one that's been stuck off it for 3 s.
  - **The new spots** are good with both furniture sets (Stuffs and Chaos), at least 0.4 m from the NavMesh edge, with a full path to the player:
    - IdleSpawn (0.5, 3.25)
    - (1) (-0.75, -3.75)
    - (2) (-3.75, -1.75)
    - (4) (-0.5, 3.75)
    - (5) (0.25, -3.75)
    - (6) (3.5, -3.75)
    - (7) (-3.75, 2)
  - **Tested:** CP LivingRoom → LR - Stuffs → Enemy2 - Wait. Each roach appeared within 0.33 m of its point (only the FSM's ±0.3 m jitter), and all moved to the player.
  - **Door_Break_Drop_Pos_3 (the Cockroach_Boss) was left by the east door:** the nearest clear floor is 4.5 m away. LandOnNavMesh puts the boss on the floor 0.5-0.7 m away, and it walks fine.
  - **Side effects of that save:** the scene also got the two new Player Level values and SkyFlyers' current field list. The old `wingspanDegrees` was already unused, so no change in game.
  - **Also seen:** breaking a Living Room furniture piece drops Spider_Small spiders at the piece's height (y 1.1-1.3, once at z -6.1, outside the floor). LandOnNavMesh puts them on the floor.
- **The other EnemyManagers' spawn points (same day, scene SAVED; backup Documents/429R_claude_backups/2026-11-19/2 Game Scene_before_em0_em1_spawns.unity).** Measured with every furniture set, before and after each room's GO animation. A point counts as good when it is on the NavMesh, at least 0.4 m from its edge, with a full path to the player.
  - **EM0 (Mini Bedroom):** all 3 points were 0.4-1.0 m off the NavMesh. Moved:
    - (-3.09, 3.93) → (-3, 2.75)
    - (4.47, 0.63) → (3.5, 0.75)
    - (-3.28, -3.97) → (-3.25, -3.5)
  - **EM1 (Bedroom):** 5 of 6 points were 0.2-1.7 m off (IdleSpawn (4) at x 5.75 was outside the wall). Moved:
    - IdleSpawn (-1.62, 2.94) → (-2.75, 2)
    - (2) → (1.5, -3.5)
    - (3) → (3.5, 3.5)
    - (4) → (3.5, -0.5)
    - (5) → (3.5, -1.75)
    - (1) was fine, left as it was.
  - **EM3 (Living Room ×1.5) and EM4 (rooftop bee points):** already fine, unchanged. EM4's GhostSpawns aren't used any more.
  - **Tested:** the real waves (Mini Enemy, Enemy1 - Wait). Every spider appeared within 0.35 m of its point and walked to the player.
- **Mini boss chaos rebirth (user, 2026-11-19), built + Editor-tested:**
  - **When:** a mini boss (Spider/Cockroach/Bee Boss and their PumpkinButt versions) that the player kills without it ever hurting them comes back at once, 75% of the time, as a chaos one: ×2 health, ×1.5 damage, ×2.5 EXP, with the red Chaos_Aura (the chaos pumpkins' aura). It comes back once only.
  - **What counts as hurting the player:** a bite, a fist-blocked bite, a charge, a bomb, or a web ball or sting that lands.
  - **When it doesn't come back:** a boss the game kills (a fall, a crush, a stage clean-up).
  - **Handover:** the chaos one takes over the first one's death flags, waves, FlagWhenAllGone, and its name (the rooftop swarm counts bosses by name). So the stage only goes on when the chaos one is dead too.
  - **Where:** settings are in Assets/Resources/Mini Boss Chaos.asset (Tools > 429 Game > Enemies > Set Up Mini Boss Chaos Rebirth). Code: SpiderBrain (OnEnemyDied, BecomeChaos, LandOnPlayer), EnemyHitDamage.KilledByPlayer, WebBall, FlagWhenAllGone.Replace. Test hook: SpiderBrain.ForceNextChaosRebirth.
  - **The big Gate 1 slimes (also Boss size) are left out:** they split and get their colour from the cauldron.
  - **Tested:**
    - Spider: 76 → chaos 134 health, EXP 13, aura.
    - Bee PumpkinButt: 282 health, EXP 38.
    - Cockroach: 172 health.
    - Chaos bites 58 and 14, above a normal boss's maximums of 50 and 9.
    - It doesn't come back after biting, after a game kill, or when already chaos.
    - WhiteSpiderDown is set only when the chaos one dies.
  - **Not tried in the headset.**
- **Magic hat table smoke fixed (user: "scaling down at root, not puff out"):**
  - **Cause:** Door_smoke_black (a Smoke_Puff_Loop copy) on both hat tables and 9 treasure chests has its own FSM: Wait 4.2 s → TweenScale itself to 0.1 over 2 s → Deactivate Self. With the 3D puffs (Hierarchy scaling, Local space) that squeezed every puff into a column at its root.
  - **Fix:** new PuffOutWhenShrunk on the shared Prefabs/Effects/Smoke/Smoke_Puff_Loop (all copies get it). When the shrink starts, it stops that FSM, keeps the size, stops new puffs, lets the last ones drift up, grow and fade, then switches the smoke off and re-arms the FSM. Smokes that never shrink aren't touched.
  - **Tested on the LR hat table:** puffs stayed about 2× size and rose 0.02 → 0.65 m; the smoke was off 3.4 s after the shrink began.
  - **Treasure chests:** the user asked for the chest smoke too; it was already covered. All 9 Treasure_Chest_* and Bonus_Chest_Pumpkin have it (same FSM). Play-tested on Treasure_Chest_Seed with the same result.
  - The other Smoke_Puff_Loop copies (rooftop smoke_black, Pumpkin_Tree, the Bus/Elon Boong and Elon_Bullet smoke) are never shrunk, so it does nothing there. EffectPool turns copies off and on around a re-scale, which resets it.
  - **Rooftop smoke_black (user asked, same day):** never shrunk, but it vanished in one frame. The rooftop's Info "End" deletes the 4 *_Gate_Spawn 0.5 s after it starts, and a game over switches them off. New **Fade When Removed** option, on for those 4 copies only (Rooftop_Base_Z overrides; backup Documents/429R_claude_backups/2026-11-19/Rooftop_Base_Z_before_smoke_fade.prefab). It keeps a copy of the live puffs each frame, because a switched-off particle system has already dropped them by OnDisable. On removal those puffs go on in a loose copy where the smoke stood, with no new ones, deleted when the last one's life is over.
  - **Tested:** deleted (End) → 18 puffs faded out in 2.3 s; switched off (game over) → same spot and size, gone in 3.9 s. No errors.
  - **Pumpkin_Tree smoke (user asked, same day):** its Door_smoke_black loops 15-17 s with a sound, then its FSM's DestroySelf removed it in one frame; it also goes with the tree. Fade When Removed is now on for it too (Pumpkin_Tree override; backup .../Pumpkin_Tree_before_smoke_fade.prefab). Tested: on Destroy Self the 23 puffs went on in the same spot and size and were gone 2.3 s later.
  - Backup: Documents/429R_claude_backups/2026-11-19/Smoke_Puff_Loop_before_puff_out.prefab.
- **Music players' "Smoke_Spray" fixed too (user, same day).**
  - **What it is:** a copy of Prefabs/Effects/Music_Notes_Loop (the flat music notes). Its own FSM takes a 2 s break every 300 s: TweenScale to 0 in 0.1 s, Wait 2, back to Init.
  - **Fix:** PuffOutWhenShrunk with **Come Back** ticked on Music_Notes_Loop. At the break the notes stop coming and the last ones fade at full size; then the FSM restarts from Init and so do the notes.
  - **Covers:** all 6 copies (the Bedroom, Living Room, JACK and PlayGround music players, Play_Ground_Z, and Special Stage 3's Song Notes).
  - **Tested:** break forced in Play. Notes held 0.5 size, gone in 2 s, back to 6-7 notes.
  - Backup: Documents/429R_claude_backups/2026-11-19/Music_Notes_Loop_before_puff_out.prefab.

**PC CONTROLS, EDITOR ONLY (K: PC, user 2026-11-19/20): built + Editor-tested (bot profile `_claude_bot`, real saves untouched), not committed.** Play in the Editor with no headset and the game runs on keyboard and mouse. Never in a build (user: "Editor test only, don't build it in PCVR version"): Scripts/Locomotion/PcControls.cs is wrapped in `#if UNITY_EDITOR`, and it adds itself to the XR Origin 0.5 s into Play only when no headset is active.
- **Keys (user's map):**
  - WASD walk, the mouse looks, Space jumps.
  - Left / right mouse button: punch with that hand (a jab), or swing what that hand holds. **Shift**+click: the hard punch (the game's "swing" punch, a hook) or a hard swing. **Ctrl**+click: an uppercut or an upward swing.
  - **F / G:** grab what the crosshair is on (up to 4.3 m, like the VR far grab) with the left / right hand; press again to drop.
  - **Q / E:** throw what the left / right hand holds; hold longer to throw harder (0.6 s = full).
  - **Esc** pause menu, **I** inventory, **B** shop. While a menu is open the mouse is free: the hand carrying the menu comes up in front (the inventory on the left, the shop on the right), the other hand points where the mouse is, and the left button clicks. Esc also turns the view to face the pause menu.
  - The first click in the Game view only takes the mouse.
- **On/off:** Tools > 429 Game > Player > PC Controls (Editor) (ticked = on, the default).
- **Small changes to game scripts (no effect in VR or builds):** Fist.cs got `manualGrip` / `manualTrigger` (-1 = read the controller as before); WristInventoryUI.cs got `forceShow` (false = as before).
- **How it works:** it switches off the head/hand tracking and moves the camera and hands itself; the hands go through the game's own Fist and XRI grab/throw code, so damage, SMASH, flying punches (with Boxing gloves), throws and hit windows are the real ones.
  - Walking feeds the move provider; the jump moves the rig and lands on the floor below.
  - F/G pick what's under the crosshair and select it at once (XRI's own far ray is blocked by the floor in front of a thing lying on it).
  - Swings chop through the crosshair point and follow through; throws let go after 0.06 s at full speed so XRI's throw (each thing's Throw Velocity Scale, e.g. a book 4.29; Throw Assist caps at 16 m/s) sees the full speed.
- **Tested (Editor, Boxing gloves, a Spider_Normal held still):**
  - Jab 4-6 dmg (hard / SMASH); hook 6 SMASH; uppercut 6 SMASH. The right jab came out 2-4 (normal to hard), depending on the distance.
  - F grabbed a book 2.2 m away on the floor; a swing, a hard swing and an up-swing all hit it into the spider (1-2 dmg; a book is light).
  - Q full charge: the book landed 8.4-8.9 m away; E tap: 3.8-4.2 m.
  - G near grab + G drop.
  - Looking 48° up, Esc: the menu came up on screen and its Resume button clicked.
  - I shows the inventory; B opens the shop on the right hand, and the left hand clicked its Close button.
  - Walk 1.0 m/s; jump 0.58 m up, landed back on the floor.
- **Test hook:** `PcControls.Simulated` (a TestInput: move, look, pointer, punches, grabs, throws, hard, upper, jump, menu, inventory, shop) drives it from scripts. Test script: K:/WORK_2026/08_04/_claude/pc_controls_2026-11-20/pc_test2.cs, run in Play with K:/WORK_2026/08_04/_claude/playtest_2026-11-10/pt.sh (mcp_exec on :8080; start Play with pt_start.cs + pt_god_on.cs).
- **Not done:** no mouse look while a menu is open (Esc again to close it).
- **Next, user (2026-11-19):** enemy attacks lock on at the wind-up: DONE 2026-11-20, see AIM LOCK below.

**AIM LOCK AT WIND-UP (K: PC, user 2026-11-19/20: "lock on the position where they are going to land the attack, so Player can have time to actually dodge"): built + Editor-tested (bot profile, real saves untouched), not committed.** When an attack's wind-up starts, the enemy takes the player's spot (floor spot under the head + head height) and keeps it: it faces it, and the lunge / dive / throw / shot / charge goes there. The hit test still checks where the player really is, so standing still is hit as before, stepping away during the wind-up makes it miss. Every script has a **Lock Aim At Wind-Up** tick box (on); untick it for the old "follow the player until the strike".
- **Changed (backups of every file: Documents/429R_claude_backups/2026-11-20/aim_lock/):**
  - SpiderBrain: every bug bite / claw / face leap / pounce, bee sting dives (homing now darts at the locked spot), kamikaze dives, web and poison sting shots (no lead any more), Bull Charge, Giant Web.
  - PumpkinBossBrain: punches L/R (body turn and arm aim), two-arm slam, each pumpkin throw (no lead any more), slide charge (locked at the wind-up's start, not its end). Body slam and butt hops already picked their landing spot at the start.
  - FatPumpkin: bite and giant stomp. SlimeBomber: holds still through its wind-up, then dives in one straight line at the locked eyes (a fixed spot re-aimed every frame made it jiggle there forever when dodged). BoxBomb: aims at the eyes as it wakes (start of the shake). BowlingPumpkin: the 2 s wind-up. Gates/PumpkinBallBoss: the 1.6 s charge warning (lanes still win).
- **Tested (stay = player still, dodge = 1.5 m sideways jump at the wind-up's start):**
  - Spider bite, cockroach (face leap clung), Spider_Boss pounce, Bee_Boss sting dive, kamikaze dive, bee sting shot: stay HIT, dodge missed; bugs end 0.2-0.4 m from the locked spot.
  - Giant Web: stay hit, dodge missed. Bull Charge: aims 0.0 deg off the locked spot (the dodged player 13 deg off); its run couldn't be tried in the bedroom test spot (something right in front, charge length 0, same with the old code).
  - Box bomb: stay reached the player (swatted by the PC hands), dodge "missed: goes off in the air". Slime bomber: stay 2/2 hit the face, dodge flew past.
  - Fat Pumpkin bite: stay 3/3 bitten, dodge 3/3 missed. Bowling pumpkin rolls 0.0 deg off the locked spot (dodged player 35 deg off).
  - Pumpkin boss (old slide test, cases added): charge stay hit + thrown, charge dodge 3 m at the wind-up start: no hit, slid 0.0 deg off the spot; punch stay hit; throws stay hit, dodge 0 hits in 4.
- **Facts, not changed:**
  - **Boss punches:** the fists go to the locked spot (0.1-0.4 m), but the punch clip swings each arm in from its own side, so stepping about 2.5 m TOWARDS the punching arm (right for punch_R) at 3.5 m can still be caught by the swing (2 of 6 test punches); stepping the other way or back always dodged. User (2026-11-20): leave it as it is.
  - **Body slam / butt hop:** they lock already, but the landing reaches 3.8-3.9 m (1.7 x size), so a 1-2 m step is still hit; about 3.5 m is needed.
  - **Not wind-up attacks, left as they were:** the boss's cyclone (chases while spinning), the Bee_Boss_PumpkinButt ticking bomb (a 3.2 m blast round the bee after 4 s), cockroach cling bites, Paint Bees, ghost balloons, Ball Valley bots.
  - **Long wind-ups are now easy to sidestep:** Giant Web 2.3 s, Bull Charge 1.4 s, ball boss 1.6 s, bowling 2 s. Untick the box on any that should go back to aiming at the end.
- **Later the same day (user asked):** Fat Pumpkin's giant stomp (scaled 2.7, made giant): it lands exactly on the locked spot; stay 2/2 hit (stomp reach 1.31 m), a 1.5 m dodge 2/2 missed (landed 1.50 m away). Gate ball boss (Resize 0.8 m so it fits the bedroom, no arena lanes): stay 2/2 hit, a 2 m dodge 2/2 missed; it charged 0.0 deg off the locked spot (41.5 deg off the dodged player). Test: aim_lock_2026-11-20/aim_stomp_ball.cs. Not tried: the ball boss in its real Ball Valley arena, anything in the headset.
- **Test scripts:** K:/WORK_2026/08_04/_claude/aim_lock_2026-11-20 (aim_*.cs, run with playtest_2026-11-10/pt.sh in Play after pt_start.cs + pt_god_on.cs; boss/run.sh <case> for the boss: charge_hit, charge_lock_dodge, punch_flight, punch_lock_dodge, punch_sweep, throw_hit, throw_lock_dodge).

**BUGS CONCEPT (user, 2026-11-20): pictures + Gemini prompts ready, nothing built.** The user wants the spider, cockroach
and bee to look more like pumpkins. Exports/Bugs_Concept/:
- Bugs_now_overview.jpg (3 x 3: spider, cockroach and bee x normal, boss and pumpkin-butt boss);
- Spider/Cockroach/Bee_now_sheet.jpg (3 views each);
- Bugs_now_model_variants.jpg (each bug's 3 random model variants);
- screenshots/ (every render + _parts.txt);
- PROMPTS.md (Prompt 1 lineup, 2 turnaround per bug, 3 bosses, 4 pumpkin-butt bosses, optional variants; what has to stay: same body plans so the rigs and animations can be kept).

Renders are real materials in a preview scene, idle pose (bees in flight: their wings fold away in idle). Scripts are in
K:/WORK_2026/08_04/_claude/bugs_concept_2026-11-20 (bug_pics.cs via pt.sh in Edit mode, then bug_sheets.py). The
user's Gemini pick goes in as concept_v1.png.
- **Try 1** (gemini_try1.png): the user said it's fun but far from their pumpkin style (painterly, curly leafy vines).
  Round 2: Pumpkin_style_sheet.jpg (16 tiles: the game's in-use pumpkins with real materials, plus a carved face and rind
  close-ups; every render is in pumpkin_style/) and two new prompts in PROMPTS.md: a short follow-up for the same Gemini
  chat and a full one for a new chat. Both say: match these exact pumpkins, 3D game-model look, olive stem legs, no
  leaves or curly vines. Script: bugs_concept_2026-11-20/pumpkin_pics.cs (pumpkin_boss_Z alone; it timed out in the batch).
- **Try 2** (gemini_try2.png): the user likes it (it matches the pumpkins) but it's too cute and chubby; they want menaces that will bite and eat you. Round 3 prompt in PROMPTS.md: lean angular shapes, long thorny legs, narrow slit eyes, jaws torn open with rind teeth and pulp drool, scorched carvings, hunting poses, low camera.
- **Try 3** (gemini_try3.png, from the round 3b meaner teeth-and-claws prompt): user: "this looks nice". Round 4 in PROMPTS.md asks for the same design in a low-poly style (faceted, about 2-4k triangles each, today's spider render attached for the facet level), plus smooth-shaded and wireframe variations.

**BEDROOM SECRET ROOM + DOOR MOVE + RANDOM PUMPKIN SIZES (user, 2026-11-20): BUILT and PLAY-TESTED in real bot runs (see "Tested" at the end). Prefabs and scene saved; nothing committed.**
- **User's ask:** move the bedroom's chained door left, closer to the hung shelf + broom, with its welcome mat. Breaking that door pops up a room behind it that mirrors the bedroom (same size and light, no second door or window), with a big smoke puff. Its light comes on when the player walks in. In the middle stands a giant pumpkin almost touching the ceiling, with 2 health. Breaking it gives either 33 random candies or a Giant Spider. The Giant Spider is a variant of the white boss spider, has 33 health, sets no bool when killed, and drops 3 candies. The W/C door "behind the shelf only leads to fall edge". Pumpkin_On_Tree gets a random size, weight and health (bigger = heavier + more health).
- **Door:** BedroomKitSetupMenu BigSides north = 0.75 m filler, Door_Wide+Chained (doorway centre x -1.5, was -0.75; the shelf is at x -2.94), 3 m wall, 0.75 m filler. The filler `BR_Kit_Wall_0p75m` is cut from the 1.5 m piece with its wallpaper UVs kept, saved as `Assets/Models/Rooms/BR_Kit_Wall_0p75m.asset`. "Use Bedroom Kit Room" moved both mats -0.75 (scene saved 16:12, only those 2 lines).
- **Secret room:** a third RoomKit set "Secret" on Bedroom_Kit_Base, centre z 7.845, sharing the big room's north wall. The new `Game.Rooms.SecretRoom` on the room root (Scripts/Rooms/SecretRoom.cs) does the rest:
  - The chained door gone while Big stands opens the room: a puff, kit.Play("Secret", come), Secret_Floor grows in, the Secret Teleport Area turns on, the doorway NavMeshObstacle turns off, and the north fall edge moves to z 13.54 while the E/W edges extend (their NavMesh holes too).
  - The giant pumpkin pops in at 85% of the come; the light turns on when the head is inside.
  - On break: 50% (Spider Chance) Giant Spider (ComeAtOnce), else a CandyFountain of 33.
  - When Big goes (Enemy1 - Wait turns EnemyManager 1 on and plays Bedroom_GO) the secret room BREAKS AWAY whole (user, later the same day: "when the EnemyManager is on, break the secret room"):
    - big smoke puffs and the whoosh, its walls and roof go and its light goes off, and an unbroken giant pumpkin goes with it;
    - half way through the go its floor shrinks away, its teleport area turns off, and the fall edges go back to the bedroom's own (stored at Awake), so whatever is left in it (candies, a living Giant Spider) falls.
    - Before this change the floor stayed until "3 - LivingRoom" destroyed $Bedroom_Base.
  - Test hook: `SecretRoom.ForceSpider`.
- **Prefabs** (made by Build Bedroom Kit Room via SecretRoomSetupMenu):
  - `Prefabs/Items/Breakable/Secret_Giant_Pumpkin` is the tree pumpkin x5.33 = 2.7 m, kinematic, health 2, toughness 1, player hits only. It has no grab, cook, LiveLimit, bowler, Fat Pumpkin or random size; its self-break and small pieces are off.
  - `Prefabs/Enemies/Enemies_Smarter/Spider_Giant` is a Spider_Boss variant: boss scale x1.6 = 2.0-2.4, health 33, setOnDeath empty, noticeOnSpawn, plus the new `CandiesOnDeath` (3, player kills only). No chaos rebirth: MiniBossChaos has no prefab of that name.
- **Random pumpkin sizes:** the new `Game.Items.RandomPumpkinSize` on Pumpkin_On_Tree, added by Tools > 429 Game > Items > Random Pumpkin Sizes. Size is 0.75-1.5 (skewed, median ~1, about 1 in 11 at 1.4+). Scale goes x Size (it follows the Health FSM's 0.65 -> 0.85 grow tween for 1.2 s), mass x Size^3 (300 -> 127..1013), health x Size^2 rounded (3 -> 2..7). Test hook: `RandomPumpkinSize.ForceNextSize`.
- **Rebuilt prefab compared with the old one:** only the north wall pieces changed, plus the new Secret parts and the SecretRoom component.
- **Backups:** Documents/429R_claude_backups/2026-11-20/secret_room/ (Bedroom_Kit_Base.prefab + meta, Bedroom_Kit_Animator.controller, Pumpkin_On_Tree.prefab, 2 Game Scene.unity).
- **Tested** (real bot runs from a new game on `_claude_bot`: `run_secret.sh stuffs false` = candies, `run_secret.sh chaos true` = spider; tools also in `Exports/Rooms_2026-11-20/tools/`):
  - **Door and room:** the doorway is centred at x -1.5, and the room is shut until the door breaks. Then it opens with 16 wall parts and 9 floor tiles, the teleport area on and the doorway NavMesh obstacle off. Fall edges: none at (0, 8) once open, one at (0, 14) and at x ±5 beside it.
  - **Pumpkin and light:** the giant pumpkin stands 2.70 m tall; the light is off until the player walks in, then on. One blast of 1 takes it to 1 health, a second breaks it.
  - **Candies outcome:** a candy fountain of exactly 33.
  - **Spider outcome:** Spider_Giant out with health 33, setOnDeath empty. Killed by the player: a fountain of exactly 3, and no GAMESTAGES / EnemyManager bool changed. Its size was 1.7-1.84 a few seconds out: it grows in from 0.6 over 7 s (SpiderBrain growSeconds), to 2.0-2.4.
  - **W/C door:** it stands behind furniture (Bedroom_Candy_Basket / Bedroom_Shelf_01 in Stuffs); past its doorway is only the west fall edge (checked at x -4.6 and -6).
  - **Fight start, old version:** the room folded and its light went off (the floor stayed then).
  - **Fight start, new break:** see "Break test" below.
  - **Pictures:** `Exports/Rooms_2026-11-20/7_secret_room_*.jpg`; the single shots are in K:/WORK_2026/08_04/_claude/rooms_2026-11-20/walls/6_*.
  - **Not tried:** the headset; a real player breaking the 300 hp chained door by hand (the test set its health to 0); the Pumpkin_On_Tree sizes in Play (`tools/pumpkin_sizes.cs` makes 42 and lists size / scale / mass / health).
  - **Break test** (real run, candies): 29 PASS, 0 FAIL. At Enemy1 - Wait + 5 s: 0 secret wall renderers shown, the light off, 0 floor tiles left, the teleport area off, a fall edge back at (0, 8) and (0, 4.7), and none at (0, 4.3) (the bedroom floor's edge is as before).

**PUMPKIN BOSSES REPAINT (user, 2026-11-20: "now do the same repaint for the bosses"): APPLIED to all 6 boss prefabs, not committed, not seen in the headset.**
- **White bosses (Spider_Boss, Cockroach_Boss, Bee_Boss) = GHOST PUMPKINS:** the same paint as the normal bugs, but the orange rind is a pale cream-white pumpkin (light grooves, warm grey shadows) and the eyes glow hotter orange. Stems, legs, bee stripes and jaws stay as on the normal bugs (olive).
  - New textures `Assets/M_Textures/<Bug>_Smol/<Bug>_Enemy_Pumpkin_Ghost_Color.png` + `_Ghost_Glow.png` (they reuse the normal `<Bug>_Enemy_Pumpkin_Normal.png`).
  - New materials `Assets/Materials/<Bug>_Smol_Pumpkin_Ghost_Mat.mat` (copies of `<Bug>_Smol_Pumpkin_Mat` with the ghost textures), replacing `<Bug>_Smol_White_Painted_Mat` on each white boss's 3 model renderers.
- **Pumpkin-butt bosses** (Spider/Cockroach/Bee_Boss_PumpkinButt) now use the orange `<Bug>_Smol_Pumpkin_Mat` instead of the old brown `<Bug>_Smol_Mat`. Their pumpkin butt, crown and skull are untouched.
- **White anomaly bug** (user: "Yes, ghost it"): `<Bug>_Smarter.anomalyMaterial` is now `<Bug>_Smol_Pumpkin_Ghost_Mat` too (was `_White_Painted_Mat`; Normal/Small inherit it; backups in `Documents/429R_claude_backups/2026-11-20/anomaly_ghost_mat/`). It's DORMANT today: `anomalySwap` = Rainbow_Slime on all three bugs, so the rare roll always becomes the rainbow slime and the white bug only shows if Anomaly Swap is cleared.
- **Kept as they were:** every old material and texture; the Slime_Big/Small prefabs that share Cockroach_Smol_White_Painted_Mat; the legacy Enemies/Spider, Cockroach, Bee... prefabs (not in 2 Game Scene).
- **To switch back:** put `<Bug>_Smol_White_Painted_Mat` on the white boss's 3 models, `<Bug>_Smol_Mat` on the pumpkin-butt boss's 3 models. Prefab backups: `Documents/429R_claude_backups/2026-11-20/boss_pumpkin_mat/`.
- **Checked:** Unity prefab renders, before vs after: `Exports/Bugs_Concept/boss_repaint/Bosses_before_after.png`. Scripts: `K:/WORK_2026/08_04/_claude/boss_pumpkin_paint/` (unity_apply.cs, unity_render.cs, compose.py); the ghost paint is `PAINT_VARIANT=ghost` in the spider/cockroach/bee paint.py (added by `_claude/patch_ghost.py`).

**PUMPKIN BEE REPAINT (user, 2026-11-20: "now do the same repaint for the bee"): APPLIED to the normal and small bees, not committed, not seen in the headset.**
- **The paint** (old model and its shared UVs; bee_smol wins the shared texels):
  - The abdomen is the game's pumpkin rind with 12 front-to-back ribs and two dark olive bee bands.
  - The stinger is an olive pumpkin stem with a dark tip; the thorax is fuzzy olive.
  - The head is orange with dark sockets and RED glowing eyes. The old slit pupils stay dark: the eyes are head texels near the Eye bones that were reddish in the old atlas.
  - The wings are pale dried-leaf cream with faint veins; the jaws cream; the legs and antennae olive with dark tips.
- **New files:**
  - `Assets/M_Textures/Bee_Smol/Bee_Enemy_Pumpkin_Color/_Glow/_Normal.png`.
  - `Assets/Materials/Bee_Smol_Pumpkin_Mat.mat` (URP Lit, metallic 0, smoothness 0.1, and the old metallic/smoothness map REMOVED so it's matte; glow map x1, normal map).
- **Where it's used:** assigned on `Enemies_Smarter/Bee_Smarter.prefab` (backup in `K:/WORK_2026/08_04/_claude/backups/bee_pumpkin_mat/`), so Normal and Small use it.
  - Bee_Boss keeps white; Bee_Boss_PumpkinButt keeps the old Bee_Smol_Mat. **(Changed later the same day: see PUMPKIN BOSSES REPAINT.)**
  - PaintBeesSetupMenu still copies the old Bee_Smol_Mat as its template (unchanged).
  - To switch back, put Bee_Smol_Mat on Bee_Smarter's bee_smol, _2 and _3.
- **Checked:** Blender before/after (flying pose) and Unity prefab renders, in `Exports/Bugs_Concept/bee_repaint/`. Scripts in `K:/WORK_2026/08_04/_claude/bee_pumpkin_paint/` (paint.py is generated by make_paint.py from the cockroach script + bee_body.py.txt).

**PUMPKIN COCKROACH REPAINT (user, 2026-11-20: "now do the same repaint for the cockroach"): APPLIED to the normal and small cockroaches, not committed, not seen in the headset.**
- **The paint** (old model and its shared UVs, mirror-safe; Cockroach_Smol_3 wins the shared texels):
  - The wing-lid shell and the abdomen are the game's pumpkin rind, with 12 ribs running front to back (a long pumpkin) and a dark seam where the lids part. The lid undersides are pale pumpkin flesh.
  - The hind wings are pale cream.
  - The head is orange with dark sockets and yellow-glowing eyes. The eyes are found by position: no own group, so head texels near the Eye bones that were bright in the old atlas.
  - The jaws are cream with dark tips; the legs and antennae are olive with dark spines and tips.
  - No face, nothing busy.
- **New files:**
  - `Assets/M_Textures/Cockroach_Smol/Cockroach_Enemy_Pumpkin_Color/_Glow/_Normal.png`.
  - `Assets/Materials/Cockroach_Smol_Pumpkin_Mat.mat` (URP Lit, metallic 0, smoothness 0.1, glow map x1, normal map).
- **Where it's used:** assigned on `Enemies_Smarter/Cockroach_Smarter.prefab` (backup in `K:/WORK_2026/08_04/_claude/backups/roach_pumpkin_mat/`), so Normal and Small use it.
  - Cockroach_Boss keeps white; Cockroach_Boss_PumpkinButt keeps the old Cockroach_Smol_Mat. **(Changed later the same day: see PUMPKIN BOSSES REPAINT.)**
  - The old material and atlas are untouched. To switch back, put Cockroach_Smol_Mat on Cockroach_Smarter's Cockroach_Smol, _2 and _3.
- **Checked:** Blender before/after (with the lids open) and Unity prefab renders, in `Exports/Bugs_Concept/roach_repaint/`. Scripts in `K:/WORK_2026/08_04/_claude/roach_pumpkin_paint/`.

**PUMPKIN SPIDER REPAINT (user, 2026-11-20: "keep the base model, re-paint it only"): APPLIED to the normal and small spiders, not committed, not seen in the headset.**
- **The paint** (on the old model's existing UVs, shared by Spider_Smol/_2/_3, mirror-safe):
  - The back body is a clean, simple pumpkin, like the game's own pumpkins: bright orange, fine grain, 10 broad ribs with thin dark grooves, and an olive stem cut on top. No face (the user tried a carved face, two versions, and said remove it).
  - The head is orange with dark sockets and yellow-glowing eyes; cream fangs with dark tips.
  - The legs are olive stems, darker at the joints, with dark thorns and claw tips.
- **New files (nothing old overwritten):**
  - `Assets/M_Textures/Spider_Smol/Spider_Enemy_Pumpkin_Color.png`, `_Glow.png` (only the eyes glow) and `_Normal.png` (ribs pressed in). Import settings are copied from the old texture; the normal is a Normal Map.
  - `Assets/Materials/Spider_Smol_Pumpkin_Mat.mat`: a URP Lit copy of Spider_Smol_Mat, metallic 0, smoothness 0.1, emission = the glow map x1, normal map on.
- **Where it's used:** `Enemies_Smarter/Spider_Smarter.prefab`'s 3 renderers (backup in `K:/WORK_2026/08_04/_claude/backups/spider_pumpkin_mat/`), so Spider_Normal and Spider_Small use it.
  - Spider_Boss keeps the white material; Spider_Boss_PumpkinButt keeps the old brown Spider_Smol_Mat. **(Changed later the same day: see PUMPKIN BOSSES REPAINT.)**
  - The old material and textures are untouched. To switch back, put Spider_Smol_Mat on Spider_Smarter's Spider_Smol, _2 and _3.
- **Checked:** Blender before/after and Unity prefab renders (`Exports/Bugs_Concept/spider_repaint_test/`). Not yet seen in a dark room or the headset.
- **Scripts:** `K:/WORK_2026/08_04/_claude/spider_pumpkin_paint/`:
  - `paint.py` (FACE = False; set `SPIDER_PAINT_APPLY=1` to write straight into Assets);
  - `preview.py` / `compose2.py` (Blender before/after);
  - `unity_apply.cs` (import settings, material, prefab);
  - `unity_render.cs`.
- **The earlier new-model try** (`Assets/Blender/Spider_Pumpkin.blend`, below) isn't used by anything; the user went with this repaint instead.

**PUMPKIN SPIDER v1 (user, 2026-11-20: "remodel the spider with the old rig but new look, a new blend file"): built in Blender, NOT in Unity yet, not committed.**
- **Source:** concept `Exports/Bugs_Concept/gemini_try3.png`; analysis and plan in `Exports/Bugs_Concept/SPIDER_REMODEL_PLAN.md` (user approved).
- **File:** `Assets/Blender/Spider_Pumpkin.blend`. It holds the old Spider_Armature with its 57 bones and all 15 clips (NLA stash as before), the new mesh `Pumpkin_Spider`, and the old meshes hidden in an `Old_Spider_Reference` collection. `Spider_Enemy_NEW.blend` is untouched (md5 checked).
- **Mesh:**
  - 3,029 triangles (old about 1,800); smooth shading baked (sharp above 62°).
  - **Back body (Butt):** a ribbed pumpkin + stem with scorched cracks.
  - **Head:** a jack-o'-lantern about 0.25 m wide (1.8x the old head). It has carved eye, nose and mouth holes (dark scorched walls glowing deeper in), 4 small yellow brow eyes on the Eye bones, 13 teeth, 3 drool strands, and 2 sickle fangs on Jaw_Fang.L/R.
  - **Legs:** 6 thorny olive-stem legs with knobs at the joints and dark claw tips. The front pair is short thorny claws under the mouth.
  - All 37 vertex groups match bones.
- **Texture:** `Assets/M_Textures/Pumpkin_Spider/Pumpkin_Spider_Base_Color.png` + `_Emission.png` (1024, own UVs, one material "Pumpkin_Spider").
- **Checked:** EEVEE previews and a 12-clip pose sheet (`Exports/Bugs_Concept/pumpkin_spider_v1/`). The rig drives everything: Idle, Walk, Run, Alert, Attack, Attack_2 (the leg stretch), Hit, Death, Shoot_Web_Ball, Dodge, Appear and Walk_Back.
- **Scripts:** `K:/WORK_2026/08_04/_claude/pumpkin_spider/` (build.py rebuilds the whole file from Spider_Enemy_NEW.blend: don't run it after the user edits Spider_Pumpkin.blend by hand).
- **2026-11-20:** the user is tweaking Spider_Pumpkin.blend by hand ("I am going in to do it myself, you help later"): do NOT rerun build.py on it.
- **Next:** the user's look check in Blender; then the FBX + Unity prefab swap (the same Spider_Animator); later the 3 variants, the white boss texture, and the pumpkin-butt boss idea.

**0. NEXT (user, 2026-11-18 night: "Make the game run smoother and play fun first", before the remodel; build size left alone):** pick one first: (a) **smoothness**: profile a play session (bedroom bug fights, the 180-pumpkin rooms, bonus round, bosses: frame time, GC per frame, physics/animation/particles), fix the worst spikes, fade 3D smoke puffs right at the head; a code scan found no costly per-frame lookups, so it needs measuring (MCP server on, or Unity closed for a hidden run); (b) **fun**: the 15-point balance list (memory balance-pass-todo; the user died 3 times in the 11-18 recording, so check which points still stand). Also: Gloves.blend has unsaved changes in the user's Blender (thumb fix + chubby; the FBX already has them); nothing from 11-18 is committed.

**1. Pumpkin boss ROUND 3 (K: PC): prepared + compile-checked, NOT applied, NOT tested.** Everything is in K:/WORK_2026/08_04/_claude/boss_slide_2026-11-11/staged3/ (PumpkinBossBrain.cs, Editor/PumpkinBossSlideMenu.cs, PumpkinBossFx.cs, LiveNavMesh.cs; base3/ + base3_md5.txt = the repo files it was made from; they still matched at 17:05). What it does (all user decisions):
- **Cracks removed** (user: "not really good, remove it for now"; may model cracking parts later): brain `drawCracks` off; the menu takes the crack overlay off the renderer. The 1/5 damage on the pumpkin and the break at 20% stay. *Meanwhile* the cracks are hidden in the game by Boss_Butt_Cracks.mat `_Collapse = 1` (set 17:07; round 3 makes it proper).
- **Arms swing on hops:** each butt hop plays the user's `jump_slam` retimed (arms back on the crouch, up on take-off, forward-down on landing) with an Animator "HopSpeed" speed parameter; a "Face" override layer keeps his normal face (idle_true) while hopping; half the clip's hip turn reaches the waist.
- **Break and regrow instead of sinking** (user: the sinking up and down looked really bad): going down for punches/slam/clap/spin/dizzy, the pumpkin bursts (light burst, no candy) and his upper body drops 0.35 s to the old sunk height; going back up, the pumpkin regrows in 0.6 s with an overshoot. The transform grows the pumpkin out at 1.2 s. The real break (20%) still never regrows.
- **Heavier ground smoke:** wake puffs on both sides while sliding (thicker in the charge), a skid burst, a dust ring at hop take-off and a ground blast on every hop landing (boom/shake only near the player). Estimated peaks ~69 slide / ~93 charge / ~95 hop chain (limit ~98): measure.
- **Stone blocks (Stone_Cube_Dice 1/2/3, immovable):** hop arcs/landings checked against the pumpkin's real width; LiveNavMesh treats immovable bodies as never-walkable (all enemies path round them).
- **To resume:** (1) write gen_pairs3.py (copy of gen_pairs2.py: base3 → staged3 → pairs3), re-check the repo md5s against base3_md5.txt; (2) apply outside Play (fixlist_2026-11-11/rep.py), recompile, run Tools > 429 Game > Enemies > Pumpkin Boss Slide Setup (log should say the crack overlay was taken off); (3) run test/run_case3.sh cases (no_overlay, hop_arms, drop_regrow, trail_particles, block_hop, block_behind, bugs_path, pics before/after) + round-2 regressions (momentum, charge_hit, bounce_hit, butt, pose, hops, crush) + test3/bk_test.cs a2_hopPast and b3_headOn; (4) show the user pictures of the break-and-regrow, the hop arm swing and the smoke.
- **MERGE FIRST (2026-11-11 night):** the second PC changed `PumpkinBossBrain.cs` and `pumpkin_boss_Z.prefab` for the hop damage (item 2), so those two no longer match base3's md5. Build round 3's pairs from base3 → staged3 as planned, but apply them to the CURRENT files. The hop change touches only three places: (a) the hop fields after `hopStretch` (tooltips, `hopHitReach` 2, `hopHitEvent` "DamageHeavy", new `[Range(1f, 3f)] hopNearShare = 2.5f`); (b) the top of `HitPlayer(string healthEvent, Vector2 launch, Vector3 from, float share = 1f)` (`if (IsChaos) share *= chaosDamage;`, then `PlayerDamageShare.Next(share)` before the event and `Take()` after, only when share isn't 1); (c) the end of `HopLands()` (`float outside = hopHitReach > 0f ? Mathf.Clamp01((near - BodyRadius) / hopHitReach) : 0f;` and `HitPlayer(hopHitEvent, hopLaunch, transform.position, Mathf.Lerp(hopNearShare, 1f, outside));`). If a round-3 pair hits one of those spots, apply round 3, then redo the hop change from this description. The prefab only got the 3 hop values (hopHitReach 2, hopHitEvent DamageHeavy, hopNearShare 2.5).

**2. Done 2026-11-11 night (second PC), all compile-checked and Editor-tested** (save profile `_claude_test`, deleted after; the user's slots untouched):
- **Hop landing damage (user: the closer the player, the more damage; x2.5 right next to him):** a hop landing within 2 m of his pumpkin's edge sends **DamageHeavy** (40-50 before Toughness) times **x2.5 right next to him (100-125)**, falling off to x1 at 2 m; nothing further out. A chaos boss adds its x1.3 (all shares are capped at x3). Tested (a chaos boss): 0 m 135, 0.2 m 141, 1 m 91, 1.8 m 66, 2.4 m no hit, all in the expected ranges.
- **NEW PUNCH after dying in a stage (user: "Yeah"):** a punch (or exploding throws) bought inside a special stage, then a game over there: the notice now shows 3 s into the next run, once the player is up (never over the death). PlayerLevel.cs ("next run news"). Tested for real: Fist Size bought in a stage, death with no coins left, the restart about 4.5 s later, NEW PUNCH! (Jab) 3 s after that.
- **Wingless bees never sting:** any flier that has lost its wings now only bites (before, only the kamikaze bees stopped). SpiderBrain.TryStartWebShot. Tested on Bee_Boss_PumpkinButt with the sting cooldown forced off: with wings 9 stings in 8 s, without wings 0 in 12 s (chase and bite only).
- **Candy vacuum: it was already fixed** by the afternoon boss rework (the bug report was from the morning test, before it). Tested: 8 candies dropped round the boss were all eaten within 1 s (first form) and 2 s (true form on the giant pumpkin).
- **Fairy lock tower: now fully tested.** On release it moves to the Fairy's parent and the sound plays to its end (1.4 s) although the Fairy is destroyed 0.4 s in; then it removes itself. The brightness looks right in a picture (second PC: Documents/429R_claude_backups/2026-11-11/pc2_fixes/pics/tower_steady.png).
- **Git noise:** `.gitignore` now ignores `*.spp.meta` and `*.unitypackage.meta` (Unity makes them for the ignored Substance and package files). **Commit the deleted `Assets/Models/TEST/Build_Square_Slot.unitypackage.meta` once:** its package isn't in git, so Unity deletes the meta on every start.
- Backups of the changed files (second PC): Documents/429R_claude_backups/2026-11-11/pc2_fixes/originals; the test scripts are next to them (pt_*.py).

**2b. Done later 2026-11-11 night (second PC), compile-checked and Editor-tested unless said:**
- **Steam demo App ID 5604260** is in `Assets/Resources/Demo Settings.asset` (new: commit it). The demo has no store page of its own (user: leave it unticked). Next: Tools > 429 Game > Steam Demo > Build Steam Demo (Windows) → `E:/429_Demo`, then SteamPipe-upload to the demo's depot (usually 5604261; Claude can write the upload script).
- **Game icon = the new v6 icon:** `Assets/Images/429_icon.png` (the default icon, used by Windows) and the 4 Android icons (legacy, round, adaptive fg/bg). Old ones: Documents/429R_claude_backups/2026-11-11/game_icon/ (second PC).
- **Reddit banner:** `Exports/Steam_Art/renders_v6/Promo_Reddit_Banner_1920x640.png` + a 305 KB `.jpg` (Reddit's limit is 500 KB); the shot is in `v6/render_user_v6.py` (only=Reddit).
- **Box bombs calmer (user: "so dangerous"):** Paper_Box_1-5 chance 30% → **15%**, rest between attacks 3 → **7 s**, face hit 20-25 → **12-15** (faceShare 0.3). Not played in the headset.
- **Pumpkin rooms x4 (user):** bedroom 45 → **180**, mini bedroom 35 → **140**, Pumpkin_On_Tree limit 140 → 560, rain 6 s; refill stays **12 more below 12** (user). **No rolling pumpkin bombs while a pumpkin room runs** (user; TurnsIntoBowler.Hold/Release). Each dropped pumpkin can't be hurt for its first 2.5 s (a third broke landing on the pile), and the room's pumpkins and its stage pumpkin take **only the player's hits** (EnemyHitDamage.PlayerHitsOnly): one broken pumpkin's flying pieces broke its neighbours in a chain (474 re-dropped in 2.5 min). That is also the answer to "why does Pumpkin_1 get hit by a normal pumpkin break": the pieces hit the mini bedroom's stage pumpkin. Tested: 180/180 stay, the pile sleeps, ~160 fps in the Editor (empty room ~257, rain 136-187); breaking 3 removes exactly 3. **The scene values were saved by the user at 20:21.**
- **Flying punch 7 damage at Fist Size 1 (user asked why): not a bug, the bonuses stack.** A flying punch does (1 + Fist Size) x Elon Boong (x2) + 1 hard hit + 1 red-hot hands; eaten red candies count as Fist Size points (~0.6 each at 1 point; the user's 2026-11-10 rule). E.g. Elon + one red candy: 6, hard 7. The user may still want candies to give size only, or a cap: ask.
- **Fat Pumpkin + Elon Boong:** the Elon punch's small blast landed a moment before the punch and started the fat pumpkin's 0.2 s hit pause, so the punch was ignored (1 damage a punch). Blasts no longer start that pause (FatPumpkin.Hit). Not tried with a real punch.
- **Pumpkin health (user, 2026-11-11 late):** runners x3 (Pumpkin_0/1/2/4/6 = 12/15/18/24/24; Chaos 1/2/4/6 = 24/27/30/36), Mob Pumpkin_7 5, chase Pumpkin_5/3/7 = 3/4/5, Pumpkin_Tree 100. **Fat Pumpkin:** starts at 6, +2 health a candy (at once), giant 150 (plus what won fights saved up; a won fight no longer doubles a small one). BalanceEnemiesMenu matches. Prefab values; nothing in the scene overrides them (only Pumpkin_Big, a copy of Pumpkin_10, overrides its health to 50).
- **Only the player's hits hurt pumpkins (user):** EnemyHitDamage got a saved **Player Hits Only** setting, on for Pumpkin_0/00/1/2/4/6 (+ Chaos), 6 1, 6 2, 51, On_Tree, Big, StartGamePumpkin (and the bonus round's copies) and pumpkin_Z; furniture the player holds or just threw still counts as theirs. Fat, Laughing, Mob and chase pumpkins already took only the player's hits.
- **Fairy Shield pops Fat Pumpkins, giant too (user):** it bursts as usual with no candy and no EXP (FatPumpkin.shieldTag). Tested: a small and a 150-health giant both popped.
- **TO DO later (user):** Laughing pumpkins (Pumpkin_8, 9, 9 1, 10) have 1 health; the user wants "at least 500 stamina". Their stamina today is METRES they can run before they're tired and stunned (3/4/5 m by round; hits take Metres Per Damage off it), so 500 would mean they never tire: agree with the user what it should mean first.
- **Candy (user: leave it):** a candy adds 0.5 point (x1 + 0.2 per own point) up to 5, then 0.1 up to 10; Weapon Efficiency and Fist Size points (candy ones too) are +1 damage each, which is why damage climbs fast with candy.
- **Fat Pumpkin damage numbers:** DamageNumbers.Show(target, damage, big, kill) (new) for health kept outside a Health FSM; FatPumpkin calls it. Tested: two hits showed "2", "2".
- **Pumpkin_00 still moving in the hand:** HeldStill paused Look At only in the FSM's current state, so the Health FSM's state change (9 s after it appears, then every ~78 s) turned it ~18° and the hand dragged it back. Now Look At, Smooth Look At and Tween Punch are switched off in every state while held. Tested: held 14 s past the change, 0 turns (before: 14 turns, 88°). Runner pumpkins too.
- Backups (second PC): Documents/429R_claude_backups/2026-11-11/ (box_bombs, pumpkin_rooms, fat_pumpkin, held_still).

**3. User decisions (2026-11-11 night):**
- His forearms in the roar slide pose stay ~9% bigger ("Bigger = Cooler"). No change.
- Graffiti on the boss (user: remove it, or keep it simple). **Nothing like it exists in the project on the second PC:** no decal projector, no graffiti object, material or texture in any prefab or the scene (an old Graffiti material and textures were deleted in 2025). On the rooftop the yellow marks are the lid's carved pumpkin face and the pink skulls are the stone dice. If the K: PC still shows graffiti on the boss, it's only there: show Claude a picture of it there.
- The pumpkin's damage look: the user may MODEL cracking parts later (e.g. chunks Butt_Chunk_1-3 that burst off at 33/66/100%, or 2-3 damage-stage models); the code already tracks the damage 0-1 (ButtCracked).

**How a punch is bought (the user asked):** level up, then make two fists (grip + trigger on both hands) and bump them together twice to open the Level Up menu. Each point in **Fist Size** makes the fist bigger; the punches unlock at Jab 1 point, Swollen Hand 6, Triple Punch 10 (Swing and Uppercut are locked for now). NEW PUNCH! shows when the menu closes.

**4. Fairy bone cage → purple "lock tower" particles (K: PC): APPLIED and fully tested.** Menu Tools > 429 Game > Fairy > Fairy Lock Tower (run; re-runnable). Fairy.prefab: The_Cage keeps only its trigger box + kinematic body + Start FSM (the lock still breaks when the StartGamePumpkin drops / the Fairy Shield clears it); its bone mesh, the bone drop (The_Cage_Drop_Pos FSM) and the 3 Skull_Candle_Purple (they were EditorOnly anyway) are gone; a nested Fairy_Lock_Tower (Prefabs/Effects/Fairy_Lock_Tower.prefab; materials Data/Effects/Fairy Lock *.mat; script Scripts/VFX/FairyLockTower.cs) shows a turning rune circle, 8 circling purple bars, a top ring and spiralling sparks (~30 particles, no lights) and on release fades, sweeps a ring, bursts sparks and plays glass_break_2. **No scene change** (GAMESTAGES' Fairy_Pos spawns the Fairy). Backup: K: PC Documents/429R_claude_backups/2026-11-11/fairy_lock/Fairy.prefab. Now unused: Prefabs/Items/Broken_Stuffs/The_Cage_Drop.prefab, The_Cage.fbx (left for the user to delete; PumpkinLaughSoundMenu still lists the drop prefab).

**5. Not tried live yet:** Right Ctrl (video) and Right Shift (photo) work ONLY while the 429R Editor is the focused window (Scripts/Cam/CaptureKeys.cs).

**6. GLOVES (user, 2026-11-12, K: PC): APPLIED + Editor play-tested (all tests PASS), not yet tried in the headset, not committed.** Files: K:/WORK_2026/08_04/_claude/gloves_2026-11-12/ (staged/, apply.py, tests/TEST_PLAN.txt, tests/gl_log.txt, tests/pics/). The Set Up Gloves menu has been run (twice). Originals of the changed scripts: Documents/429R_claude_backups/2026-11-12/gloves/scripts_before.
- Fixed after the play test (WeaponSkills.cs): Bomber Man no longer blows up a thing you just let go of above the floor (only the throw's own speed counts, not falling); Impact / Weapon Master no longer miss real swings (the weapon's fastest speed over the last few physics steps counts). Re-tested: drops of 28/50 cm and a thing let go lying still = no blast; a 2 m/s toss = Bomber blast, 1.2 m/s = none; held-weapon test 5 of 5 runs PASS.
- Looks to check in the headset: the glove sits ~3.7 cm toward the pinky (looked fine in pictures); the skill blasts' flash is a white starburst that gets cut where it meets a wall (minor).
- Each round the Tip Knight has ONE random pair of gloves hanging from his boong (Cay_boong in Knight.fbx), never the same kind twice in a row; touch to take them; kept until Game Over (a revive keeps them). Kinds: **Boxing** (+3 Fist Size; glove points DO unlock flying punches for the round), **Throwing** (+3 Throw Power; throws explode from the start), **Soldier** (was Warrior; +3 Weapon Efficiency). Round-only points like candies (capped with candies at 10).
- **No gloves = grip + trigger does nothing** (no punching). With gloves, grip + trigger pops a glove onto the hand (the hand no longer swells); the glove is the punching collider, grows with Fist Size, and flying punches are glove copies. Placeholder gloves (red/green/blue mitts) until the user models the real ones (swap the "Model" child in Prefabs/Player/Gloves/Glove_<Kind>_<L|R>).
- Saved with the round (continuing from a checkpoint keeps them). Older saves without glove fields: no gloves until the next round (user: strict).
- Fixed on the way: the Level Up fist bump works with bare squeezed hands (it needed fists: without gloves nobody could spend points); the start pumpkin's "MAKE A FIST" tip page hides without gloves; the radio skip also takes a tap with a held thing. The rooftop "Wait For the Punch" isn't a player punch (it's the boss's roof smash), so nothing gets stuck. Kept (user): bare squeezed hands still shatter pop-ups; a once-per-slot "GLOVES!" tip (pauses); NEW PUNCH! waits for gloves; gloves punch in special stages (their points drop there).
- The old "Lock Swing And Uppercut" tick is gone (round 4 unlock tree below). A punch (or exploding throws) unlocked by gloves gets its NEW PUNCH! card ~3 s after the pickup, once per save slot.
- **Flying punches are harder without Boxing gloves** (user, 2026-11-12): Boxing keeps today's conditions (jab 2.6 m/s, perfect 5 m/s, 0.4 s, straightness 0.75, aim 60°; swing 3.2 / uppercut 2.8); Throwing/Soldier (any kind without "Easy Flying Punches" in Glove Kinds) use a harder set: 3.2 m/s, 6 m/s, 0.32 s, 0.85, 40°, swing 3.8 / uppercut 3.4. Both sets are fields on each controller's Fist (tune in the headset). It only matters for players with their own Fist Size points (non-Boxing gloves don't unlock the Jab).
- **Unlock trees (user, 2026-11-12; staged with the gloves):** only points the player SPENDS on level-ups (+ the gloves' +3) unlock; candy only raises the basic numbers. Fist Size: Jab 1, Swing 3, Uppercut 5, Swollen 6, Triple 10 (the old Swing/Uppercut lock is gone). Throw Power: Explosive Objects 1/4/7 (EVERY hard throw explodes; reach 0.6/0.9/1.2 m, damage 2/4/6), Bomber Man 10 (every throw explodes at impact and again 0.5 s later, 1.4 m, 6+6). Weapon Efficiency: Impact 1/4/7 (a held weapon knocks a thing into another thing: a blast hurts both, 0.5/0.8/1.1 m, 2/4/6), Weapon Master 10 (every weapon hit explodes, 0.9 m, 8; furniture too). Gloves: Boxing = Jab+Swing+Uppercut at once, Throwing = Explosive Objects 1, **Soldier** (was Warrior) = Impact 1; all +3 points towards the next levels. The old bonus stat "Weapon Master" now shows as **"Strong Grip"**. Own thrown blasts still bite within 0.5 m; Impact/Weapon Master never hurt the player. Never explode: candy, lollipops, seeds, coins, chests, cooking things, firewood, puzzle pieces, Jenga, bowling balls, magic hats, boongs, candles, Legendary gear. Numbers in Assets/Resources/Blast Skills.asset (made by the menu).
- **Notices fly out as cards (user):** unlock notices AND level-up notices pop out ~1.25 m in front of the player, never pause the game, break on any hit (a squeezed bare hand too), fade after 7-8 s; up to 3 at once. Only the once-per-slot GLOVES! tip still pauses.
- **Done:** applied, menu run, tests passed (above). Left: the user tries it in the headset (glove on the hand, hang spot on the Knight's boong, flying-punch feel with and without Boxing gloves) and models the real gloves (swap the "Model" child in Prefabs/Player/Gloves/Glove_<Kind>_<L|R>). Boss round 3 (item 1) changes different files, so it can still be applied on top.

**7. GATES (user, 2026-11-12): no gate 3 stage for now.** "We are doing too much and spread too thin": the PlayGround (gate 1) and the bowling stage (gate 2) are not in a playable state yet and need work first. Gate 3 now sends the player into gate 1's or gate 2's stage, half and half (GAMESTAGES: "Stage 1 - The Start" next3 -> new state "Gate 3 Pick" = Send Random Event next1/next2 -> Clean for Path 1 / Clean for Path 2). Made by Tools > 429 Game > Paths > Gate 3 Picks Gate 1 Or 2 (Scripts/Paths/Editor/Gate3PickMenu.cs; "Gate 3 Back To Gate 1" undoes it). **The scene must be SAVED to keep it** (2 Game Scene; run the menu again on the other PC after pulling, or pull the saved scene). Gate prices unchanged (50 Yellow / 5 Green / 1 Boss Seed each); a bought gate stays open for good (Unlocks).

**7b. GATE 3 = BALLS (user, 2026-11-16): being built (step 1 of 6).** Gate 3 becomes the user's web game BALLS (K:/Repository/BALLS/Balls) in VR: the player is a ball collider (head never rolls), last man standing in the BALLS valley (lawn inside the rock border, 6 holes, shrinking valley). Punch balls; squeeze both gloved fists = charge special move, dash where you LOOK, hitting everything on the path. Killing slimes HEALS the player; the player never grows; bots still grow baby balls. Teleport only onto the lawn inside the rock border. User answers: falling into a hole or 0 health = Game Over chain (coin toss may continue); the player always has gloves by then; 4 rounds, more bots and more aggressive bots each round; player keeps level stats; dash aim = where you look; keep the BALLS valley art + a NEW bluer sky sphere with a few clouds. Seeds like gate 2: start chest = Bracer, round wins = Chest/Head/Shield, final win = The_Seed, then GAMESTAGES goes to the rooftop/tree/Pumpkin_Boss_Z. Steps: 1 valley + bots (player watches; Quest frame rate), 2 player ball + rolling + lawn teleport + holes, 3 punches + special move + being hit, 4 slimes heal + shrink + last man standing, 5 rounds + seeds + GAMESTAGES Path 3 (replaces Gate 3 Pick), 6 headset tuning. Code goes to Assets/Scripts/BallValley (namespace Balls, 429R edits marked "// 429R:"), art to Assets/BallValley. Gate 3 Pick (random gate 1 or 2) stays until step 5.
- **Step 1 DONE + Editor-tested 2026-11-16** (bot profile): Tools > 429 Game > Ball Valley > Set Up Ball Valley run (prefab Assets/Prefabs/BallValley/Ball_Valley.prefab), Watch Ball Valley (Play mode) shows it. All checks pass, 0 errors; a round ends (winner) and round 2 starts with 6 bots; ~23k triangles, ~120 renderers, ~35 rigidbodies. Fixed on the way: bots kept BALLS' full-size hole distances (barely shot, the last phase stalled) → scaled, and in the Moment of Truth bots get angrier over 15 s; the opening cutscene's end put the house fog back over the valley → the valley keeps its own look; the sky now draws after the valley (cheaper on Quest). The game files ported from BALLS are rebuilt by tools/port.py: change them THERE. Quest frame rate: the user's headset check.

**7c. GATE 3 SKY (user, 2026-11-16: "more 429R style, halloween vibe"):** the valley uses its OWN sky (an inside-out ball that follows the head, unlit, no fog), showing a painted Halloween dusk: deep blue top, violet, orange glow at the horizon, a big harvest moon with bats, a few dark clouds lit orange underneath, stars. Picture: Assets/BallValley/Sky/BallValley_Sky.png (drawn by K:/WORK_2026/08_04/_claude/ballvalley_2026-11-16/tools/halloween_sky.py; paint over it or swap it freely, it's a normal 360 picture: left edge = +Z, top = straight up). The valley light now comes from the moon (Ball Valley Stage: Sun Angles 32/20/0, warm orange, violet ambient). **2026-11-16 later (user: "use this sky for main game"): Tools > 429 Game > Look > Sky: Halloween** makes Assets/BallValley/Sky/Halloween_Skybox_Mat (Skybox/Panoramic, same picture, rotation 270 so the moon is where it is in the valley) the scene skybox and the head camera clears to it (was a flat fog colour); "Sky: Fog Colour" undoes it. Ambient (Flat) and reflections (Custom, none) unchanged. **Menu run; the user must SAVE the scene.** Sky picture mipmaps OFF (no seam). The gate 3 King crown is removed (user). **Flying slimes instead of painted bats (user: "reuse the slime"):** the sky picture has no bats now; the menu also adds a scene object "Sky Flyers" (Scripts/Lighting/SkyFlyers.cs): 6 copies of the Flying_Slime model (no AI/colliders/shadows), nearly black, looping round the moon with beating wings, faces turned a bit toward the player, now and then one swoops far across the sky; 27-33 m from the head (inside the house's 40 m far plane; past the 22 m fog they're black silhouettes, in gate 3 their eyes glow). Tune on the Sky Flyers object (size, loops, swoops, colour). Before that: the OLD Skydome (Prefabs/Bases/Skydome, used from Walk Tutorial on) was NOT visible in the main game: its material Base Color is black, it is 75 m away while the camera only sees 40 m, and the house fog (6-22 m, near-black) covers everything past 22 m. So the main game "sky" is the dark fog colour. Left as is.

**8. GATE 1 REDO = "Defend the Cauldron" (user, 2026-11-16): APPLIED + Editor play-tested (bot profile); the user must SAVE the scene.** Menu Tools > 429 Game > PlayGround > Use Defend The Cauldron (run; "Use Cooking Rounds (Old Gate 1)" undoes). Changed: Cauldron.cs, SpiderBrain.cs, PlayGroundField.cs, DarumaStage.cs; new: CauldronDefence.cs, CauldronHealth.cs, IBiteTarget.cs, Editor/CauldronDefenceMenu.cs; prefab Fire_Woods_n_Cauldron (backup in Documents/429R_claude_backups/2026-11-16/gate1_defend), new Prefabs/Enemies/pumpkin_boss_Z_Doom; GAMESTAGES Path 1 ON next1 -> Tween PlayGroundZ To Boss -> Destroy PlayGroundZ To Boss -> Boss PG -> clean it -> BOSS. Numbers: near 7 m / far 8.5 m, cauldron 150 hp (bites 1/2/4), mini bosses Slime_Big_Blue/Red/Yellow at 25/50/75% -> Chest/Head/Shield seed chests, Daruma win 8 of each candy (48) auto-fed. Tests: K:/WORK_2026/08_04/_claude/gate1_redo_2026-11-16/tests (TEST_PLAN.txt). Less cooking, more fighting: chest seed (Bracer) pops the cauldron out (big smoke, player pushed back); the fire is small until firewood is thrown in, then big; find the hat (the user places/spawns the hats) for the recipe; ONE right recipe starts a 10-minute cook: walls drop, monsters keep coming and attack the player when the player is near the cauldron, the cauldron otherwise (cauldron has health like the Pumpkin Tree). Mini bosses at time stages drop the gear seeds (Chest/Head/Shield). Cauldron broken = Chaos Pumpkin_Boss_Z + endless monsters + teleport ground broken (sure death). Success -> the DARUMA game (Special Stage 3) -> a Daruma win -> straight to the FINAL BOSS (no tree defence; user changed this the same day). Daruma win gives a big candy reward (the cauldron fight gives few candies) so the player meets Pumpkin_Boss_Z stronger. The old 3 rounds, laughing pumpkins, white slimes and the Daruma stage leave gate 1 (code kept). Files: K:/WORK_2026/08_04/_claude/gate1_redo_2026-11-16/. Also 2026-11-16: the sky slimes are smaller (Sky Flyers wingspan 2.5-3.8 degrees; scene not saved yet).

**8b. GATE 1 ANSWERS (user, 2026-11-16) - done:** firewood: each log makes the fire roar for a while and the countdown faster (kept as built). A 4th mini boss: Slime_Big at 9:00 (90%) with a plain reward chest (PG_Treasure_Chest_Drop, no gear) - "more = fun". Daruma prize: 42 candies, each a random kind (DarumaStage Prize Candies; fed + topped up so all count; about +3.5 a stat). Laughing pumpkins / white slimes stay off. The Path 1 ON notice title is now "WORKING SITE" (PlayGroundNoticeSetup too). **"Quit boss = Re-dream" (every gate and the main game):** Checkpoints.NoResume = {"CP Boss"}: a save left at the boss fight loads as a new run (Re-Dream: candies/items/level kept, no checkpoint, all revives, no gloves), and the pause menu hides Get a Rest there (Checkpoints.AtBoss). Scene must be SAVED (notice text + sky + flyers + gate 1 GAMESTAGES).

**9b. 2026-11-18 later: MENUS RUN + EDITOR PLAY-TESTED (no headset). Scene SAVED 19:34 (user OK'd; GAMESTAGES gate paths checked in the file).** User rules: **fall into a hole = a normal fall** (view drops, black, put back where you stood 2 s before, FallDamage 55; 0 health = the usual death) and **gate 1's cook = 5 minutes** (prefab + CauldronDefenceMenu). Tested through GAMESTAGES on save profile _claude_test (deleted after):
- Gate 3: Path 3 ON -> valley, rules notice, 15 balls, candies (Candy_N, grabbable, hover 0.7 m), all in -> "ALL IN!" -> shrink 6 s -> boss 2 s later -> chase / wind up / charge / dizzy at the edge -> broken -> The_Seed where it broke + gear bools -> eaten -> Enemy 4 -> Rooftop_ON PG -> Whack 6, player 3.5 m south of the roof's middle facing it, valley look given back (fog, far 40). 0 errors.
- Fixed from the test: bots killed a standing player in a minute -> **1 hunter at a time (Max Hunters), a bot that hits the player leaves them alone 10 s, plain crashes only shove (hurt from 12 m/s or a special move, at most every 1.5 s)**: a standing player now loses ~30 hp in 40 s (was ~90); the shrinking border left a player outside the field -> it sweeps them in (not in a hole's gap: the hole may swallow them = a fall); the start spot avoided trees/rocks but counted candies; a candy destroyed by the game (candy limit) broke the bots' food search -> off the list on destroy; the boss pushed 3 s into a corner when the player was beyond the edge -> skips that charge.
- Gate 2: sky = the Halloween skybox (moon, stars, sky flyers): fine; its fog (14-38 m, near-black) leaves a dark band at the horizon (offered: tint it). Pumpkin finale (forced): drops on a lane end (21 m), charges down the lane, dizzy at the dirt's edge (3.9 m out), leaps to another lane; broken -> Won -> seed -> rooftop -> Whack 6. 0 errors.
- Gate 1: cook 300 s, the win -> no Daruma, the pumpkin drops where the pot burst; the PlayGround roam area is only ~11 x 12 m -> **gate 1's pumpkin radius 1.5** (Finale Radius); it landed 1 m up (the floor ray hit the pot) -> fixed; broken -> seed -> PlayGround sinks 11 m while the player is held in the black (no fall) -> rooftop -> Whack 6. 0 errors.
- The rooftop's Pumpkin Tree only appears at Roof_Start (the boss), so the exits stand the player south of the roof's middle during Whack 6/7.
- Not yet: the headset (feel of shoves, the dash, fists-together charge), a real punch on the boss and balls (only Special Hit was called), gates 1/2 played through their whole rounds.

**9. GATES END LIKE GATE 3 (user, 2026-11-18, second PC): CODE DONE + compile-checked, NOT play-tested yet (Unity MCP was off), menus NOT run yet.** User: gate 3 flow = run around, survive, eat candy, push balls into holes; 15 balls; all 15 in OR the last phase -> the field at its final small stage -> a giant pumpkin-head ball boss drops (bigger than the holes, too heavy to push out, only attacks hurt it) -> break it -> The_Seed -> defend the tree like the main game. "Gate 2 and 1 learn from gate 3 ... make it fun, make it simple." Also: "in gate 3 change all the berry on the field to random candy".
- **Gate 3 (Ball Valley, Scripts/BallValley):** Ball Valley Gate (15 balls, pocketed = gone, first shrink 40 s, last phase at ~190 s, smallest 0.5 = 25 x 12.5 m, finale shrink 6 s, boss 2 s later; rules notice; a lazy line under the view: BALLS n / FIELD SHRINKS! / BOSS hp / TAKE THE SEED!). Ball Valley Player (the player on foot: an invisible trigger ball the bots hunt; a ball crashing in throws the player (0.32 m per m/s x sqrt(weight), max 5 m) and hurts from 7 m/s or a special move ("Damage"); walking into a ball pushes it; punches knock balls (Fist.Push + Knock); punched slimes pop into candy; SPECIAL MOVE = both fists made and held within 0.3 m for 1 s, pull apart = 12 m/s dash for 0.35 s where you look, stops at a hole's rim, knocks balls at 16 m/s, 10 damage to the boss, 3 s cooldown; head over a pit = fall: view drops + black, put on the lawn, then the usual death (DamageToPlayer; coins may revive); teleport only onto the lawn (Valley.MakeTeleportFloor). Berries = random Candy_1/2/4/5/7/8 (Pellet.SetupCandy: hover 0.7 m, touch nothing, bots eat by rolling over; grabbed = a normal candy). BallPlayer.vrBody (follows the player, no own doings).
- **Giant pumpkin (Scripts/Gates/PumpkinBallBoss.cs, Prefabs/Gates/Pumpkin_Ball_Boss):** the Bowling Pumpkin's model/laughs/roll warning/roll/pieces grown to radius 2.6, Fat Pumpkin's stomp + yelps, health 150 (gate 2: 100, radius 1.3), mass = Push Weight Immovable, keeps its height (never falls), stays in the arena; drops in, chases at 3.2 m/s, every 3-5 s winds up 1.6 s (laugh + choo-choo) and charges at 13 m/s; a charge stopped by a wall/rock/arena edge = DIZZY 3.5 s (stars, hits x2); charge into the player = DamageHeavy + 6 m throw, rolling bump = Damage + 2.5 m; only the player's hits (Enemy Hit Damage's rule), blasts, the special move; breaks into 14 candies (gate 1: 42) + 30 EXP, then **Gate Exit** (Scripts/Gates/GateExit.cs: The_Seed where it broke, gear bools guarded, black, player held in place, rooftop, put 3.5 m in front of the Pumpkin Tree).
- **Gate 1:** Cauldron Defence Pumpkin Finale: after the pot bursts the giant pumpkin drops where it stood (roam area = its arena) instead of the Daruma game (Daruma code kept: untick Pumpkin Finale). Cook time still 10 min (user's number; suggested shorter).
- **Gate 2:** after phase 2's chest the slimes fly off and the giant pumpkin (radius 1.3) drops on a lane's far end, bowls itself down the lane, stops dizzy at the dirt's edge, leaps to another lane (Bowling Stage Pumpkin Finale; phase 3 + showdown kept behind the tick). Strikes now ADD UP (3 strikes hurt a slime, a miss doesn't reset). Exit spot = in front of the tree.
- **GAMESTAGES:** every gate -> "Rooftop_ON PG" -> **Prepare_Whack_6** (Whack 6/7 = the tree defence, then Lid, BOSS); gate 1's Path 1 ON next1 -> Tween PlayGroundZ; gate 3 -> new Clean for Path 3 / Demo? Path 3 / Path 3 Holder / Path 3 ON (spawns Ball_Valley, waits for 4 gears + The_Seed).
- **RUN IN THIS ORDER (out of Play), then SAVE the scene:** Tools > 429 Game > Gates > Set Up Pumpkin Ball Boss; Tools > 429 Game > Ball Valley > Set Up Ball Valley; Tools > 429 Game > Gates > Gate Finales: Giant Pumpkin In Gates 1 And 2; Tools > 429 Game > Gates > Gate Paths: Ball Valley + Tree Defence (scene). Undo: Gate Finales: As Before / Gate Paths: Back As Before. Test gate 3 alone: Play, then Tools > 429 Game > Ball Valley > Play Ball Valley (Play mode).
- **Not tested at all yet:** everything above (no editor run, no headset). Things to watch: candy pellets grabbable while kinematic + touching nothing; the bots' hunting of the VR body; shove distances; the dash's feel (comfort); the boss's charges in the shrunk valley and on the lanes; GAMESTAGES' rooftop after Path 3; FistBump double-bump while charging the special move (unlikely: a bump needs both hands moving at each other).

**10. NEW GLOVE MODEL (user's concept, 2026-11-18): DONE, in the 6 glove prefabs, not tried in the headset, not committed.** Red Boxing, green Throwing, blue Soldier (weapon handling). New file **Assets/Blender/Gloves.blend** (Export: Glove_R/Glove_L; Colour Previews; Lights; Camera), Assets/Models/Gloves/Gloves.fbx (1,320 tris a glove, same size and pivot as the placeholder), textures Assets/M_Textures/Gloves/Glove_*.png (1024). Materials Data/Gloves/Glove_*.mat now show the texture (base colour white; emission kept for Hot Hands); each prefab's Model = one "Glove" (Mitt + Cuff gone), the Knight's hanging preview too. Pictures: Exports/Gloves_Concept/model_v1_*.png, unity_v1_*.png. Rebuild/tweak: Documents/429R_claude_backups/2026-11-18/gloves_model/stage_full.py (second PC). Backups: Documents/429R_claude_backups/2026-11-18/gloves/. From the player's eyes the thumb and cuff show most; the pumpkin emblem stays on the outer side, as on the concept (user: keep it). **Later the same day: the USER's remodel** (fatter mitt, thumb pulled out of the mitt, "not 2 separated mesh"): Claude rebuilt the thumb inside its seam as clean rings (one piece with the mitt), softened the folds next to it, mirrored Glove_L to Glove_R, re-unwrapped, repainted (new contact shadows, stitches along the thumb seam) and re-exported; 1,374 tris a glove; Unity checked (unity_v2_*.png). The user's old thumb shell is still in the file, hidden (Glove_L.001, not exported). **Save Gloves.blend in Blender** to keep it. Never re-run stage_full.py "full" (it rebuilds the old shape over the user's remodel); repaint = paint_gloves.py. **Then (user): a smoke puff whenever a glove pops on** = Glove Kinds' new Pop Puff (Smoke_Flash_Small_Puff) at **Pop Puff Size 0.08** (0.3 hid the view; comparison Exports/Gloves_Concept/ingame_v3_pop_sizes.png), played from Fist.PuffGlove at the knuckles, grows with Fist Size, at most every 0.15 s a hand (the pickup's pop puffs too); **punch shape fitted to the fatter mitt** on all 6 prefabs: capsule r 0.058 -> 0.072, 0.16 -> 0.176 long, centre y -0.02 -> -0.026 (GlovesSetupMenu's numbers match). Play-tested hidden in the game scene (profile _claude_test, deleted): pickup pops both gloves (new mesh) with a puff, a later pop puffs again, the fists' Glove Shape = the new capsule; 0 errors. Not tried in the headset. **Then "chubby" (user: "make the fist look rounder ... chubby feeling"):** the glove reshaped smoothly (gloves_model/chubby.py: mitt 86% as long, middle 7.5% fatter, corners rounded, cuff 72% as long and 3.5% fuller; 23 -> 19 cm long), repainted (emblem/gold fist redrawn round on the new shape, the rest rides along), FBX re-exported; punch shape now centre y -0.028, **r 0.078, 0.156 long** (almost a ball). Mesh backup before it: Documents/429R_claude_backups/2026-11-18/gloves/Gloves_meshes_before_chubby.blend. **Damage rule (user 2026-11-18: "weapon damage only counts when the object is still in hand of Player, throw damage only counts when objects is off hand"):** WeaponPower.DamageOf: Weapon Efficiency (+ Weapon Master) only while held; a thrown thing gets only its Thrown Bonus (before, Weapon Efficiency was added to throws too and a hard throw tripled it); a thing lying about its own; a gun's shot keeps Weapon Efficiency. Everything else already followed the rule (Impact/Weapon Master/two hands = held; explosive throws, hard-throw x3, Hot Hands = thrown). **Bare fists (user: "a little damage when we make a fist without calling out gloves"):** Fist.bareFists (on): without gloves a fist taps for 1 (never hard, no Hot Hands bonus, no flying punches, no blasts, no Elon double, no uppercut knock-out), still shoves/stuns (Fist.IsBareFist + EnemyHitDamage.BareTap). Compile-checked only (Unity was open). **Recording 2026-11-18 21-58-54 (reviewed frame by frame; damage_video/ in the backups folder):** every number of 33+ was a thrown thing under the old rule ((1 + Weapon Efficiency 10-11 + 1) x 3 for a hard throw = 33-39, +Hot Hands 40-41), driven by "+WPN" candies (and the Soldier gloves' +3) inside a run, not by level-ups; held-weapon hits 22-26 and gloved flying punches 10-11 are by design; deaths at 07:12, 15:03, 28:34. Now a hard throw does about 6-9. The six **999s** were the game's own kill of a 666-health thing (no hit): DamageNumbers now shows no number for kills of 999+. Not changed (ask): overkill shown in full on tiny pumpkins; candy growth speed. **Midnight sky (user 2026-11-18: "change the sky in game to mid night sky, dark blue color"):** new picture Assets/BallValley/Sky/Midnight_Sky.png (2048x1024, painted by Documents/429R_claude_backups/2026-11-18/midnight_sky/midnight_sky.py: navy overhead, lighter blue horizon, a silver moon at the old moon's spot (lon 200, 32 up, so the Sky Flyers still loop it), moonlit clouds, small stars); the main game's Halloween_Skybox_Mat now uses it (one guid changed; backup in that folder), HalloweenSkyMenu's picture too. Gate 3's valley keeps BallValley_Sky.png. Done with Unity open (file edits, no scene change); not looked at in Unity yet. **Sky Flyers (the flying slimes round the moon), user 2026-11-18 "smaller and rarer, not all the time":** SkyFlyers.cs: wingspan 2.5-3.8 -> **1.4-2.2 degrees** (field renamed `wingspanDegrees` -> `wingspan` so the scene's saved value is dropped; no scene edit), and **visits**: the sky is empty, first visit after 15-40 s, then every 35-80 s 1-3 slimes fly in from far off (3 s), loop the moon 12-22 s and fly off. Compile-checked (Unity's Roslyn, 0 errors); NOT play-tested (the user had Unity open). Save the scene once to drop the old field. **Old Skydome: its Twinkling Stars removed (user)** (Prefabs/Bases/Skydome; backup Skydome_before_chubby.prefab in the gloves backup folder; Tools > 429 Game > VFX > Add Twinkling Stars To Skydome would put them back).

**Boss rework so far (APPLIED + Editor play-tested, 2 story fights won):** giant pumpkin butt (user's model Assets/Blender/Pumpkin_Boss_Z2.blend; butt slot split on import), snowman SLIDE in the user's roar pose (`transform` at 1.40 s, face from idle_true; slide_pose until < 0.6 m/s), SLIDE CHARGE (skids at the floor's end, bonks on props/stone blocks, 1 s recovery), BUTT BOUNCE, BUTT HOPS when the player is far (also over low walls/gaps), CRUSHING things in his path (not candy/Seed/Coin/held things/the player/bosses), throws now really fly (they pass through his own colliders), STUN GUARD 17 s (chained spells 3 → 1.8 → 1.5 s), pumpkin hits take 1/5, the pumpkin breaks at 20% (146 full damage) → 5 s dizzy → fights from the floor, pumpkin hidden in phase 2 and while sunk, candy vacuum fixed, 10% bigger. Details below and in Claude's memory.

**Crack painting tools (unused now, kept):** paint setup script + how-to in K:/WORK_2026/08_04/_claude/boss_slide_2026-11-11/cracks/ (boss_cracks_paint_setup.py, HOWTO_paint_cracks.txt); the user's painting Assets/M_Textures/Pumpkin_Boss/Boss_Butt_Cracks_Paint.png; menu Tools > 429 Game > Enemies > Rebuild Boss Cracks.

## 2026-11-11 (Claude, K: PC)

(Everything in this section was done on the K: PC; "this PC" below means the K: PC.)

**Pull from the other PC (Steam demo):** merged and checked state by state (the scene has both sides). GitHub Desktop "stashed" the local work during the pull; it was restored. The stash is still in GitHub Desktop (and backed up as git ref refs/claude/stash-backup-2026-11-11): drop it once you're happy.

**Steam demo (other PC's work), facts:**
- A build is the demo when the DEMO_BUILD define is set, or in the Editor with EditorPrefs 429_PlayAsDemo on (off on this PC).
- Demo checkpoints: CP Bedroom, CP 2.5 - BR. GAMESTAGES: Demo? → Demo? Door → Demo? Path 1/2 → Demo End (notice) → Demo Store (opens the Steam store page, restarts the demo).
- Demo saves go to the <SteamID>_Demo profile, apart from the real game's saves.
- Menus: Tools > 429 Game > Steam Demo > Set Up Demo Ending (already run + scene saved), Demo Settings (the App ID is still 0: set it before a real demo build), Play As Demo, Build Steam Demo (Windows) → E:/429_Demo.

**The 2026-11-10 open items: all fixed and Editor-tested** (except "boss over max", which is by design):
1. Candy Box broken at the face: its candies can't be eaten for 1.0 s (SpilledCandyMouthDelay; 0.5 s still let 4 of 42 in).
2. Box bomb beep only plays when the box really will fly.
3. No "+TOPH" etc. at a stat's candy max (no CandyEaten event when the points didn't go up).
4. Max Health candies give whole-number caps (127.5 → 128).
5. Daruma: a snap turn no longer counts as moving (the rig spot under the head is compared, not the rig origin).
6. A poison tick in the same frame as a hit no longer skips the red lines / hurt slow (PlayerBoosts.HealthSet).
7. SpawnPreview: no more error burst after a Play exit (tested). The burst on opening the Editor got its own fix (it waits until PlayMaker has given each FSM state its FSM); that one shows on the next Editor start.
8. Pumpkin boss health above max: **by design (user)**. capAtStartHealth stays 0.
9. First bee dive no longer lifts the player (StayOnGround ignores a flying bee's body).
10. Wall portals and Fat Pumpkin turning pause inside special stages (they shoved the player in Paint Bees).
- **Paint Bees falls: cause found, and the user said LET THEM BE (2026-11-11: stray Fat Pumpkins that follow you into a special stage may do whatever they want). Don't pause or remove them.** Fat Pumpkins born in the Living Room (Enemy 3 Stuffs) follow the player into Paint Bees: nothing pauses them there. They eat the round's candy (33 and 48 candies, health 272 and 438), one turns giant, and its stomp throws the player about 3.4 m towards the nearest Fall Edge. The Living Room's Fall Edges are still on around the field, so the player falls (-55) about every 7 s.

**New today (user):**
- **Pumpkin boss candy heal = like the player's:** a 1-7 roll per candy, full up to its start health, then the same reduced shares as the player's candy heal. It can go above max (by design). CandyVacuum.cs.
- **Fat Pumpkin gets tankier by eating:** each candy adds 2 x (1-7) health, no cap. Giant still at least 300, a duel winner still x2. FatPumpkin.cs.
- **Kamikaze bees** (Bee_Normal, Bee_Small): they mostly stay up and shoot poison stings (new Bee_Poison_Sting_Small). About 1 attack in 6 is a suicide dive: beep + flash, then it explodes on you like a box bomb (about 20-25 damage + poison) and dies with no EXP. Swat it during the dive: it dies normally, with EXP. In a Boss Rush they fire stings instead of diving. Bee bosses unchanged except below.
- **Wingless bees are a menace:** losing their wings makes them tougher (normal and small bees x2; Bee_Boss_PumpkinButt now loses its wings at 40% health, x3) and they walk at 80% speed (was 50%). Bee_Boss keeps its wings.
- Menu: Tools > 429 Game > Enemies > Set Up Kamikaze Bees (already run; prefab backups in Documents/429R_claude_backups/2026-11-11/suicide_bees). Test toggle in Play: "Kamikaze Bees: Every Attack Is A Dive (test)". No scene change.
- **Editor play test (11:35-12:30): all PASS.** Dive hits 20-25 + poison 2/s x5, shove ~0.55 m, bee gone, no EXP; swat = normal death + EXP; a 3 m sidestep dodges (1.8 m doesn't); 8 s timeout blows up harmlessly. Dives were 20 of 158 attacks (about 1 in 8; the roll is 1 in 6). Wingless: toughness 1.5→3 / 1→2 / PumpkinButt 2→6, speed x0.8; Bee_Boss keeps its wings. Bee bosses as yesterday (stings now hit 9-10 of 10-11). Boss candy heal: full roll below 729, half up to 1.5x, then ~10%, no cap. Fat Pumpkin: +2..+12 per candy seen (20 → 128 after 16), giant 300, duel x2.
- Seen in the tests, not changed: a wingless PumpkinButt fired 1 sting from the ground right after losing its wings, then only bit (26-49). When 6-8 candies drop around the pumpkin boss only 1-2 get vacuumed; the rest hover inside its body until removed (old, not from today's change). **Both handled 2026-11-11 night (RESUME HERE item 2):** wingless bees no longer sting; the vacuum was already fixed by the boss rework.

**Pumpkin boss rework (user, 2026-11-11 afternoon): giant pumpkin butt, no more walking.**
- New model: the user's Assets/Blender/Pumpkin_Boss_Z2.blend (legs removed, a giant pumpkin weighted 100% to Hips), exported to Models/Enemies/Pumpkin_Boss_Z.fbx. The old Pumpkin_Boss_Z.blend is untouched.
- The butt's own material slot is made on IMPORT (Scripts/Enemies/Editor/PumpkinBossButtSplit.cs: triangles moving only with Hips → submesh 1). No Blender slot needed; it redoes itself on every re-export.
- The "notice" and "laugh_true" clips (opening cutscene) aren't in either .blend: they're kept as Models/Enemies/Pumpkin_Boss_Z_notice.anim / _laugh_true.anim (taken from the last FBX that had them) and Opening_Boss.controller uses them. OpeningSetupMenu falls back to them.
- New behaviour (PumpkinBossBrain): the true form SLIDES on its pumpkin (momentum, lean, dust, grind sound), a SLIDE CHARGE (wind-up, straight 9 m/s, heavy hit; skids at the floor's edge; 1 s recovery after every charge), a BUTT BOUNCE instead of the leg jump, a STUN GUARD for every dizzy (17 s after each spell, user; chained spells 3 → 1.8 → 1.5 s), a CRACKABLE BUTT (hits on it take only 1/5 off his health but count in full towards breaking it; breaks at 20% of max health in full hits (user), then he fights from the floor; the crack LOOK was removed, see RESUME HERE), the candy vacuum aims at his mouth and no longer jams, and he's 10% bigger (1.87 / 1.87 / 2.31).
- Setup menu: Tools > 429 Game > Enemies > Pumpkin Boss Slide Setup (already run; re-run after a model re-export: it rebuilds the pumpkin collider + waist sphere from the mesh). Backups in Documents/429R_claude_backups/2026-11-11/boss_slide/.
- No walls in any boss fight (user): rooms are broken by the boss, the rooftop by the other pumpkins when the tree breaks.
- Editor play tests: round 1 (14:50) and round 2 (16:00) all passed; later decisions and fixes are in RESUME HERE at the top.

**Editor freeze (11-11 10:37):** the 429R Editor froze for good in Play while the video recorder was recording; it had to be ended. The recorder used to start on Right Ctrl pressed in ANY app; since 17:00 the keys only work while the 429R Editor is focused.

## TO DO NEXT SESSION (user, 2026-11-09): balance pass, the game is too easy at level 10

At level 10 the user can "close my eyes and still win".

**2026-11-10 (Claude, this PC): CODE DONE + ALL MENUS RUN + PLAY-TESTED IN THE EDITOR (no headset).** All 15 items plus the user's later "6 candies" changes are in C# and setup menus. Everything compiles in Unity.

**2026-11-10 evening: the Editor play test (bot profile `_claude_bot`, real saves untouched).**
- Big test: 116 checks passed, 0 still failing, 5 partial, 12 need a headset. The testers fixed on the way: Max Health candy eaten inside a special stage no longer heals on exit; Level Up menu's Fist Size row counts candy; Candy Box spill no longer launches candies (push capped 1 m/s) and no longer cracks its own candies; a blocked box bomb no longer stops the others waking (BoxBomb skips it for 1 s).
- Then 5 more fixes, all tested and passing, plus a full bot run twice through:
  1. Bee stings and web balls aim at a standing player again. They used to lead by the old Character Controller speed after a shove (0/11 hits, now 11/11). Lead only while the thumbstick or arm swing is walking you, never in a knock-back flight (SpiderBrain.PlayerWalking).
  2. A game over inside Daruma / nesting / Paint Bees no longer heals the dead player or shows the Level Up notice over the death (PlayerLevel.Resume(quiet), PlayBonusRound waits while the player is down).
  3. A poison tick merged with a real hit now pops RED (PlayerPoison.HealthTaken, PlayerHealthHUD).
  4. No more "linear velocity of a kinematic body" warning on PlayGround chest drops.
  5. Anomaly Chance comments say 0.003 (about 1 in 333).
- Not tested (headset): arm-swing walking lead, a real triple punch after a game over, Re-Dream's scene reload inside a stage.
- **Answered 2026-11-11 (user: yes) and done:** after buying a punch inside a stage and dying there, the NEW PUNCH notice now shows 3 s into the next run.
- **Open items found (ALL HANDLED 2026-11-11, see the top section):** pumpkin boss health can go slightly above max (1094 → 1101, cause unknown); a Candy Box broken at the face feeds 1-2 candies; a box bomb from behind shoves you into its loot; its "about to blow" beep plays even if it then calms down; "+TOPH" etc. shows at a stat's candy max; Max Health candies make the cap a fraction (127.5); Daruma counts a snap turn as moving when the head is ~10 cm+ off the play-area centre; first dive sting of a run can lift the player ~1.3 m; Paint Bees: something shoves a standing player north over Fall Edge North every 6-7 s; a poison tick in the same frame as a hit skips that hit's red lines and hurt slow (DamageVignette/HurtSlow).
- Editor-only noise (old, FIXED 2026-11-11): SpawnPreview.cs:146 made PlayMaker log a burst of "Fsm not initialized" errors after each Play exit.
- Test scripts and results: K:/WORK_2026/08_04/_claude/playtest_2026-11-10/ and playtest_fixes_2026-11-10/.

**Menus already run on this PC** (Tools > 429 Game > Balance 2026-11-10; re-runnable, backups in Documents/429R_claude_backups/2026-11-10/):
- **Enemies**, **Bee Bosses Stay High**, **PlayGround And Portal FX**
- **Six Candies And Stats**: it also runs **Candy Colours** and Player > **Give Candies Their Buffs**.

**The scene must be SAVED:** Six Candies And Stats changed the Player Level object in 2 Game Scene (push 0.04, fist size 0.05, last point worth 1).

**On the OTHER PC after pulling:** the prefabs are fine once committed. Without the saved scene, run Six Candies And Stats there (it only changes Player Level in the scene).

**Still to do:**
- Headset test (Editor play test done 2026-11-10, see above).
- Do NOT re-run _claude/unity_mcp/gate_fx_build.cs: it turns the portal Wisps back on.

**6 candies (user, 2026-11-10 later):** one candy per level-up stat. Candy_3 and Candy_6 are RETIRED: they're out of every spawn list (41 prefabs); their prefabs stay.

| Candy | Name | Stat | Slime colour (in Candy_Colours.asset) | Dropped by |
|---|---|---|---|---|
| Candy_1 | Pumpkin Candy | Max Health | orange | Slime_Big |
| Candy_2 | | Weapon Efficiency | blue | Slime_Big_Blue |
| Candy_4 | | Move Speed | yellow | Slime_Big_Yellow |
| Candy_5 | Marshmallow | Toughness | pink | NEW pink slime skin |
| Candy_7 | | Throw Power | green | NEW green slime skin |
| Candy_8 | | Fist Size | red | Slime_Big_Red |

- The list lives in code: Scripts/Inventory/CandyKinds.cs. Every candy setup menu reads it.
- **The candies' look is the user's job** (user, 2026-11-10: "revert what you did with my candy's material, I will do it myself"). Their materials, glow rings, recipe pictures and the basket deco candies are back to the committed originals, and the Candy Colours menu no longer touches candies. After repainting a candy, set its row's colour in Data/Inventory/Candy_Colours.asset to match, then re-run Candy Colours so the slimes match. My flat materials were moved out to Documents/429R_claude_backups/2026-11-10/candies/removed_candy_materials.
- **Wrong recipe:** the cauldron spits Slime_Big in the colour of a candy the recipe still needs (Slime_Pink_Mat / Slime_Green_Mat for the 2 new colours). Its splits drop that candy.

**Later 2026-11-10 (user): only the current 6 candies.**
- The user deleted the Candy_3 and Candy_6 prefabs.
- Their leftovers are gone: stale refs in the 3 Random_*Candy spawners, the Candy_3/6 recipe icons (moved to Documents/429R_claude_backups/2026-11-10/gone_candies), and the Candy_3-model deco in both candy baskets.
- Still in the project, unused: Models/Bedroom_Stuffs/Candy_3.fbx and Candy_6.fbx.

**The candies are being remodelled by the user:**
- 1 candy corn, 2 teal ball, 4 reversed candy corn, 5 pink marshmallow, 7 olive-iris eyeball, 8 red-iris eyeball.
- The recipe icons in Data/Cooking/Icons are out of date (user's job).
- The particles (glow ring, ground disc, appear puff) were recoloured to match each candy. Only the candy prefabs changed; backups are in .../2026-11-10/candy_particle_colours.

**Shop (user):**
- Every restock offers 2 random candies of the 6, at 6 wraps each. They are extra pink rows; the panel grows at runtime, so there is no scene change.
- The Candy Box costs 26 wraps and is on the normal list. Broken, it spills one of each candy. It is a copy of Paper_Box_0 with a pink glow.
- Menu: Tools > 429 Game > Balance 2026-11-10 > Shop Candies And Candy Box (already run). "(Remake Icons)" redraws the shop icons in Data/Inventory/Icons after a remodel; the eyeballs are drawn turned 180.
- Like Paper_Box_0, a Candy Box swept by the Fairy Shield pops with no candies.

**Recipe pictures:** Data/Cooking/Icons/Candy_N.png were redrawn from the new candy models. "Shop Candies And Candy Box (Remake Icons)" now redraws both the shop icons and the recipe pictures (backups first).

**Flying Explosive Box (box bombs, user 2026-11-10):** menu Tools > 429 Game > Enemies > Set Up Box Bombs. Already run: BoxBomb is on Paper_Box_1-5.
- Each box rolls 30% once when it spawns; this applies to any Paper_Box_1-5 anywhere, including boxes that fall out of other boxes. Paper_Box_0 and Candy_Box are paid items and are left out (one line in BoxBombSetupMenu.cs).
- When the player comes near, it shakes (rattle, beep, orange pulse) for 1.0 s, or 1.4 s if it is out of view. Then it flies straight at the face: 20-25 damage, plus a shove.
- Any grab defuses it for good. A punch or a throw while it is awake breaks it with its normal drop and no bang. Backing off past 3.2 m calms it down.
- 1 awake at a time, with a 3 s rest after each attack.
- Test menus (Play mode): Box Bombs: Arm Every New Box (test), Box Bombs: Wake Nearest (test).
- Play-tested in the Editor 2026-11-10 (bot playthroughs pass with box bombs on). Fixed then: a box with its line to the player blocked is skipped for 1 s, so it can't stop the boxes behind it waking.

**Slime colours = candy colours (user, 2026-11-10, from the slime picture):**

| Candy | Slime colour | Slime |
|---|---|---|
| Candy_1 | orange #F0A030 | Slime_Big |
| Candy_2 | teal #3FA39A | Slime_Big_Blue |
| Candy_4 | red-orange #D2541C | Slime_Big_Yellow |
| Candy_5 | coral #E8857A | wrong-recipe slime only |
| Candy_7 | olive #A39A2A | wrong-recipe slime only |
| Candy_8 | red #D0282C | Slime_Big_Red |

- Each colour has its own skin, Materials/Flying_Slime/Slime_Candy_N_Mat, made and recoloured by the Candy Colours menu (already run).
- The bowling showdown slimes keep their old look as overrides.
- Rainbow_Slime now wears Rainbow_Slime_Lit_Mat (Rainbow on), so it is rainbow in the Editor too.
- Set Up PlayGround Slimes now runs Candy Colours at its end, so a rebuild keeps these colours.
- The Paint Bees' yellow paint now drops Candy_7 (olive is the nearest hue).

**Level-up stat changes (user):**
- Toughness push -4% a point (was -8%).
- Weapon Efficiency +1 weapon damage EVERY point (was every 5).
- Fist Size +5% bigger a point (was 10%).
- The 10th point counts as 1 (was 7), so a full stat = 10.

**Candies changed:**
- A candy's points x (1 + 0.2 x your own points in that stat): 0 points 0.5 a candy, 5 points 1.0, 10 points 1.5. The slow part after +5 grows the same way.
- Candy_8 (Fist Size candy) makes the fist and flying punches bigger and the jab harder, but never unlocks a punch. The closed fist no longer gets candy damage.
- Max Health candy raises the health cap for the run.
- The HUD shows +HP / +TOPH / +WPN / +FIST / +THROW / +SPD.

**Facts to know:**
- Fist.JabReach is renamed to Fist.ComboReach.
- The bee poison writes health through PlayerBoosts.SetHealth, so it causes no red flash and no hurt slow.
- Path 2 (Bowling) goes through "Enemy 4 - Get the Seed" and "Tween PlayGroundZ" on purpose. $PlayGround_Z is empty there, so the tween ends at once and the arena handles its own exit.

**User decisions:**
- Item 6: the flying JAB starts at the hand and flies only as far as the fist's own size (a 15 cm fist flies 15 cm).
- Item 8: special stages drop the stats and the health cap to level 0, but the player KEEPS their current health.
- Item 12: one FLAT colour per candy.
- Item 2: the giant Fat Pumpkin (300 health) shrugs off hits.

Prefab changes come from the menus under **Tools > 429 Game > Balance 2026-11-10**, which are re-runnable and make backups in Documents/429R_claude_backups/2026-11-10/.

Before re-running **Aggressive Bosses And White Bugs**, check its health table is fixed. It still had the old, lower mini boss health.

1. **Rainbow Slime:** lower its spawn chance. Its reward is too good and it's too easy to catch.
2. **Fat Pumpkin:** must be tankier.
   - Raise its base health and its toughness.
   - When it turns giant and starts hunting the player, its health goes up to 300 (not x2/x3).
3. **Stun resistance:** every mini boss, and Pumpkin_Boss_Z, gets harder to stun once below 40% health (lower stun chance).
4. **Bee bosses (Bee_Boss, Bee_Boss_PumpkinButt):** both are too weak today.
   - They stay high, out of the player's reach, most of the time, shooting stings that poison (health lost over time).
   - Only rarely do they dive down and sting by themselves.
5. **Cockroach and Spider mini bosses:** +30% MOVE SPEED (scarier).
6. **Flying Jab range:** its reach = the Fist Size (today it reaches further).
7. **Candy healing:**
   - Above full health, a candy heals +50% (as now).
   - Once health is 1.5x max health, each candy heals only +10%.
8. **Special stages:** inside every special stage, temporarily remove all buff stats from level-up points and candies, so the player plays with default stats. Restore them after.
9. **Pumpkin_Boss_Z push:** x2.5 push power.
10. **Chaos Pumpkin_Boss_Z:** a 33% chance to spawn as a chaos version with more health, more damage, more EXP and a red glow aura (like the Chaos_Aura on the runner pumpkins).
12. **Candy colours:** redo the candy system so each candy kind is ONE colour, all different. Today at least 3 kinds are orange.
13. **Slime drops match their colour:** red slime → red candy, blue slime → blue candy, and so on. The White Slime is the special one: it drops a random candy.
14. **Cauldron recipe hidden until the hat:** hitting the PlayGround cauldron shows the recipe as `??? x ??? x ??? x` until the black hat is found and hit. Only then does the recipe show.
15. **Recipe hat aura:** add an aura particle to the Recipe_Hat so it's easier to spot. Keep it light: the user dislikes overdone particles.
11. **Defend the Tree stage:** the portal that spawns the spider-lings still uses the old smoke particles. Swap them for the shared 3D smoke (Smoke_Flash_Small_Puff; never Lana's Smoke_Blast).

## Steam page assets (current: 2026-11-10/11, made on the second PC, all in git under Exports/)

- **Store art v6:** the knight raises his glowing sword at the giant Pumpkin Boss over the back wall (moon, Tree of Light, violet night). The USER posed the knight in `Exports/Steam_Art/Steam_Art_v6_pose.blend`: that file is the source, never re-pose him, and renders must look like the user's own F12 render. The whole Steam set (header, main, small, vertical and library capsules, library hero and logo, page background) plus promo key art, portrait and the YouTube banner: `Exports/Steam_Art/renders_v6/`. Scripts: `Exports/Steam_Art/v6/` (render_user_v6.py, compose_v6.py).
- **Steam icon = the new v6 icon** (user: "use the new icon for Steam"): `renders_v6/icon_new/` (512 PNG, 184 JPG, 1024 master; the sword stays inside a 4% margin). The painted badge and the 2026-11-09 3D icon remake (`assets/icon/Icon_Scene.blend`) are retired; never re-run `icon_scene_build.py` (it wipes the user's hand edits).
- **Profile picture** (the knight alone, blurry background): `renders_v6/profile/Knight_Profile_2048.png`, an 800 PNG and an 800 JPG under 500 KB.
- **Screenshots:** 14 in `Exports/Steam_Screenshots/2026-11-09/`.
- **Trailer 3** (rough cut) and its vertical Short, with the v6 logo end card and the "A VR ADVENTURE GAME" line under the logo (made in Blender: `Exports/Steam_Art/assets/tagline/Tagline.png`): D:/Movie/StreamingHere/Trailer/Trailer3/ on the second PC; build scripts in Documents/429R_claude_backups/2026-11-10/trailer3/ there.
- **Shorts** 35 (the last pumpkin) and 36 (one HP left): D:/Movie/StreamingHere/Shorts/ on the second PC.
- **The user's job:** upload the v6 set, the icon, the screenshots and the trailer in Steamworks; set up the demo (see "Steam demo" above: the App ID is still 0).
- Older rounds (renders v4/v5, the README's v4-v8 icon notes) stay in `Exports/Steam_Art/` for reference.

**Done from 2026-11-08:** `2 Game Scene` was saved with Use Bowling For Gate 2. The Bowling_Arena prefab is in the scene in commit da31bc37.

# Earlier: 2026-11-07 / 11-08

## 2026-11-08: Gate 2 = Slime Bowling (replaces the bee version), built and play-tested in the editor (not in the headset)

**How it plays:**
- **The layout:** a 5 x 5 m dirt island in the middle of an X of 4 long dark lanes. The head pin sits 18.3 m from the dirt, like real bowling.
- **The look:** lights only over the dirt and over each lit pin deck. Each lit lane gets 2 fire columns and sparkles. The fire columns are the Bonus Area's **Danger_Flame** (x1.5, linked to its prefab, its point light off for Quest): about 660 particles alive with all 4 lanes lit.
- **Start:** you appear on the dirt with Gate 1's first chest (Bracer). Eating its Yellow Seed grows vines, and bowling pumpkins grow on the vine tips.
- **The Giant Flying Slime** comes down from 30 m up, its wing sound getting louder, and circles your head 2 laps.
  - It lights a lane and poops a rack of 6 slime pins (1-2-3).
  - It never poops on the same lane twice in a row; when no lane is free, it circles your head.
- **Pins:** they grow from thin and tall (0.66 m) to fat and short over 24 / 22 / 20 s, then burst into small flying slimes that attack.
- **Strikes and hits:**
  - A strike = one ball knocks all 6 down.
  - A hit = 3 strikes in a row on that slime's racks.
  - The 7th poop without a hit = 6 divers onto your head, and it counts as a hit.
- **Phases:**
  1. 1 green slime; a hit splits it into red and blue.
  2. A hit splits each one: red becomes red + yellow, blue becomes blue + green.
  3. A hit takes a wing.
- **Chests:** each phase end drops a chest (Chest, then Head, then Shield gear) in front of you. Its seed grows more vines, so more balls.
- **Showdown:** when all 4 have lost a wing, each flies to a lane, loses its other wing and drops as a ground mini boss. They wait until the last has landed, then all set off together; their own speeds bring them one by one (tested: Green, Red, Blue, Yellow about 1.5-2.5 s apart). You can walk again in the showdown.

  | Slime | Speed | Trait | Health |
  |---|---|---|---|
  | Green "Quick" | 3.6 m/s | rapid little bites, smallest | 45-55 |
  | Blue "Sticky" | 2.4 m/s | slowing globs + sticky bite, Giant Web | 70-80 |
  | Red "Bull" | 1.6 m/s | Bull Charge from 7 m at any health (pushes you) | 100-110 |
  | Yellow "Bomb" | 1.1 m/s | biggest, heavy slam, Ticking Bomb at low health | 90-100 |

  The numbers are in `GiantSlimeSetupMenu.cs` (Showdowns table); re-run Set Up Giant Slime And Showdown after changing them.
- **Win:** beat all 4 → The_Seed → rooftop at the Lid → boss. Any gear bool still missing is set at the win, so the run never stalls.

**Files:**
- Code: `Scripts/Bowling/` (BowlingStage, BowlingLayout, BowlingLane, GiantFlyingSlime, SlimePin, PinSet, BowlingBallSupply, VineGrow).
- Prefabs: `Prefabs/Bowling/`.
- The bee version is backed up in `Documents/429R_claude_backups/bowling_v1_2026-11-07/`.

**Menus** (Tools > 429 Game > Bowling, run in this order):
1. Set Up Bowling Look
2. Set Up Slime Pins
3. Set Up Giant Slime And Showdown
4. Set Up Bowling Arena
5. Use Bowling For Gate 2

Test buttons are on the arena's BowlingStage component (right-click it in play mode): Eat The Seed, Strike The First Rack, Hit Slime 0, Next Phase, Showdown Now, Win Now.

**To do (user):**
- **SAVE THE SCENE.** Use Bowling For Gate 2 edited GAMESTAGES (the Path 2 states, and Path 2 ON now waits for the 4 gears + The_Seed like Path 1). The scene on disk still sends Gate 2 to the PlayGround.
- Play it in the headset: strikes, pin sizes, the slime sizes, how hard the showdown is.
- Screenshots: `Documents/429R_claude_backups/2026-11-08/bowling_shots/`.

## 2026-11-07: PlayGround redesign (3 cooking rounds, then Daruma), built and play-tested in the editor

**The flow:**
1. The Bracer chest seed makes the cauldron.
2. Each round:
   - The black hat gives the recipe.
   - A wrong recipe boils 2.5 s, then spits out a **Slime_Big**. No wave starts, and the pot works again at once.
   - A right recipe cooks 30 / 45 / 60 s. The bubble tower rises, the walls play GO / GO2 / GO3, and minions come non-stop.
   - When the cook is done, the pot spits the laughing pumpkin and the round's mini boss comes out: **Slime_Big_Blue** (round 1), **_Red** (round 2), **_Yellow** (round 3).
   - Catching the pumpkin stops the minions at once and drops the chest.
   - Eating the seed clears everything except the cauldron, hat and candies. The walls then play COME_2 / COME3 and the field grows (14, then 23, then 32 m wide).
3. After round 3, the pot shakes and rings the bell, and white slimes come out on a timer. 8 candies shut it: a 3 s cook, a big puff, then the **Daruma game**.
4. Daruma:
   - Win: The_Seed appears on the slime's platform. Eating it fades to black, the PlayGround goes, and the player stands 4 m from the Lid, facing it. COOKING MASTER! shows then.
   - Loss: death and a new run. The coins can't save it.

**The rules in the code:**
- **Slimes** (`Scripts/Enemies/SlimeBody.cs`, on SpiderBrain):
  - A Big slime killed by the player splits into exactly 3 Small ones, which never split. A Small one drops 0–2 candies.
  - Clearing the field, the Fairy Shield and wave kills never split a slime.
  - Blue: sticky globs and Giant Web. Red: Bulldozer and Bull Charge. Yellow: Ticking Bomb, and it splits out of its own blast.
  - Menu: **Tools > 429 Game > PlayGround > Set Up PlayGround Slimes**.
- **Cauldron:**
  - Push-back: a head within 0.55 m of the pot is pushed about 0.6 m, at most every 2 s, never while holding food.
  - A punch or weapon hit rings a bell clip and lets out a white slime (1 per 3 s, 3 at most, no EXP).
  - Menu: **Set Up Cauldron**.
- **Laughing pumpkin:**
  - It runs 3 / 4 / 5 m, then is stunned for 4 / 3.5 / 3 s with stars, silent and catchable. Otherwise it laughs non-stop.
  - It hides only near, inside the arena, and is leashed back if it leaves.
  - After 300 s it turns into the real pumpkin_boss_Z: the minions stop, and there is no chest.
  - Menu: **Set Up Laughing Pumpkins**.
- **Walls and phases (PlayGroundField):**
  - The C# drives the Animator states; the old PlayGround_Manager chain is cut (Phase 1 → State 1).
  - It turns on the child **PlayGround_Phase_2 / _3** after COME_2 / COME3. While those don't exist, it uses the old random layout.
  - Menu: **Set Up PlayGround Walls**.
- Other menus: **Set Up PlayGround Rounds** and **Set Up Cooking Rewards**. **Bonus > Hook Special Stage 3 To Cauldron** does NOT rebuild Special_Stage_3.

**To do (user):**
- Build `PlayGround_Phase_2` / `_3` as inactive children next to Phase_1, with props only. Include props that drop tree pumpkins. Leave out Cauldron_Pos and Recipe_Hat_Pos.
- In GAMESTAGES `Path 1 ON`, remove the "STILL WORKING ON THIS!" ShowNotice when ready, then save the scene.

**Not tested yet:** the whole stage in the headset, and real fists/weapons on the pot (tests raised the punch event in code).
Backups: `Documents/429R_claude_backups/2026-11-07/before/`.

# 2026-11-06

For the user and for Claude on the other PC. This PC's Claude memory and test scripts (K:/WORK_2026/08_04/_claude/)
are NOT in git, so everything needed to continue is written here.

## Finished and Play-tested today

- **Main game wrap-up**
  - Paid dice fly away like a rocket (RocketAway).
  - After the cooking-gate Seed the run skips whacks 6/7 and goes straight to the lid (GAMESTAGES 'Rooftop_ON PG').
  - The music players have their music notes back (Music_Notes_Loop) and are tougher.
  - Rooftop gates got purple torches and a spawn circle; closed gates block the NavMesh, so Fat Pumpkins can't walk through.
- **Playground**
  - Ghost balloons were replaced by **PG_White_Slime** (drops 1-2 normal candies).
  - The **Slime Orchard** repaint covers bushes, grass, bark, ground and concrete.
  - Lollipops were removed and replaced by **PG_Candy_Basket**.
  - Playground-only wood and metal: **PG_Wood_*** and **PG_Metal_Mat**, also on the broken pieces.
- **Cooking rewards**
  - Each round chest bursts out candies and EXP (8/12/16/20 candies, 40/60/80/120 EXP).
  - The first time a save slot clears the mystery pot, it gets +1 permanent stat point (CookingRewards, PlayerLevel.giftPoints).

## In progress: Special Stage Type 3 ("red light, green light" with the Giant Slime)

**What it is:**
- A reward after the PlayGround's **2nd pumpkin** is caught with its whole wave beaten.
- The player is taken to a narrow bridge with no way back and no teleport. The Giant Slime sings with its back turned, then turns and looks.
- Moving while it looks = caught: it spits bomber slimes that knock you off the bridge.
- A fall = back to the start + fall damage, and the 60 s clock keeps running.
- Touch the slime in time = win: 200 EXP + 30 candies + fireworks, then back to the Playground.
- The song speed is random and gets trickier the closer you get (slow, then suddenly very fast).

**Files (all new):**
- `Scripts/Bonus/DarumaStage.cs`: the game itself.
- `Scripts/PlayGround/SpecialStage3Trigger.cs`: on `Prefabs/PlayGround/Fire_Woods_n_Cauldron`.
- `Scripts/Bonus/Editor/DarumaStageSetupMenu.cs`: menu **Tools > 429 Game > Bonus > Set Up Special Stage 3**. It rebuilds `Prefabs/Bonus/Special_Stage_3`, `Prefabs/Enemies/Daruma_Bomber` and `Materials/Special_Stage_3`.
- `Audio/SFX/Special_Stage_3/`: song notes 1-9, look, gotcha.
- Small hooks added to existing scripts:
  - SlimeBomber: `FaceHit` event.
  - FallOutOfWorld: `ReturnSpot` / `ReturnYaw` / `Fell`.
  - PlayGroundRounds: `LastWaveCleared`.
  - GiantSlime: `TurnSpeed`.

**Play-tested OK:**
- The stage starts, the player is moved there, teleport turns off and the fog is set.
- The song plays at varied speeds, the slime turns and looks, and standing still is not caught.
- Time-out: "TIME'S UP", then back to the Playground with teleport, fog and the fall spot all restored.

**Tested on the other PC (2026-11-06, hidden Unity batch runs, save profile `_claude_bot`, deleted after):**
- Caught: moving while it looks → caught, 14 bomber slimes out, pushed off the bridge, 1 fall (health 240 → 177), back exactly on the start spot.
- Win: touching the slime → won, then back exactly where the player came from; the stage is gone.
- Note: the 200 EXP win reward can level the player up; the level-up notice pauses the game until it's punched away, and the stage goes on after that.
- NOT tested yet: the real trigger (2nd pumpkin + wave beaten) in a full Playground run, and the whole stage in the headset.

**Done on the other PC (2026-11-06), all play-tested:**
1. **Knocked down** (health 0, lucky coins, getting up from a revive; the same check as Player Launch): moving while it looks doesn't count, and getting up isn't moving. No bombers or pushes until the player is up; the forced-push wait starts again once they're up. Tested: knocked down and moved 0.6 m during a look → not caught.
2. **Cleared away mid-game:** OnDestroy now puts a player still within 60 m of the stage back on their return spot. Tested: the stage destroyed mid-song → the player was 0.08 m from where they came from, fog restored.
3. **Host colour:** DarumaStage.HostColour sets `_BaseMap` = Flying_Slime_Paint (white where it takes the colour, alpha = where) and `_BaseColor` through a MaterialPropertyBlock: **green while it sings, red while it looks (and laughs at a time-out), gold** at the start and when it's over. The Paint Bees giant keeps its brown. New fields: hostPaint, singColour, lookColour, restColour. Not made bigger (still x3.5): say so if it should be x5.
4. **Rules sign:** now at (-2.1, 1.7, 2.6), about 3.2 m away, turned to face the start (-41°), text 3 (was 2.2) and 3.2 x 2.7. Both the prefab and the setup menu.
5. Balancing fact kept as is: a fall still costs the normal 55 health.

## Notes for Claude

- Only `2 Game Scene` ships; GAMESTAGES (PlayMaker) runs the flow.
- Never edit .cs files during Play mode: Unity recompiles mid-play and breaks the run.
- Play tests use their own save folder (`SaveManager.Profile = "_claude_bot"`); delete `Saves/_claude_bot` afterwards and never touch the user's 3 slots.
- Don't save the user's scenes or .blend files.
