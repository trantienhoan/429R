# Information from gate 3: its new flow (no Gate Run), 2026-11-23 afternoon

From: Gate 3 (Ball Valley) session, K: PC. For the coordinator. Nothing to apply; read before Phase 2 / the E6 check.

**The user's design (in their words, 2026-11-23):** 15 balls at once on a field that closes over a 5-minute song (the
six holes meet and kill the player), only the player's pockets count, then the Black Pumpkin Ball; when it dies it rains
candy and "create chest that contains The Seed inside, check main game flow for that chest" ... "No need for other seeds
in this gate game."

**What that means for the shared parts:**
- `Prefabs/BallValley/Ball_Valley` has **no Gate Run** any more (Set Up Ball Valley removes it). Gate 3's own
  `BallValleyGate` shows the card and the HUD line, sets `ScriptedMoveVignette.Launches` while it runs, and marks the
  seed with `KeepFromPurpleHole` (PurpleHole's "while a gate runs" check reads `GateRun.Current`, which is null in gate
  3: no change needed).
- **Contract §2 #2 and #8 changed by the user for gate 3:** no start chest, no round chests, no Bracer/Chest/Head/Shield
  seeds. `GateExit.GearGuard` sets the four gear bools on the way out (it logs nothing when there are no chest seeds),
  so Path 3 ON's BoolAllTrue still passes. The run arrives at the rooftop without the gear chests' candy and EXP
  (gate 3 pays instead: 3 candies per own pocket, candy from trees, boulders, volcanoes and slimes, 42 candies + 30 EXP
  from the Black Pumpkin Ball). E2/E7 budget: worth a look in Phase 2.
- **The end:** the main game's `Items/Chest/Treasure_Chest_and_Seed` (as Whack 7's barrel pumpkin drops it) under
  Currently Spawned Stuffs; when GAMESTAGES' The_Seed is true, gate 3 calls `GateExit.Begin(stage, null, ...)`. Never
  stuck: after 90 s The_Seed is set for the player.
- **The loss:** the song runs out -> the player dies with no revive (DarumaStage's way: `Checkpoints.UseAllRevives`,
  PlayerHealth 0, `DamageToPlayer`), waiting while a coin toss or a revive is under way -> Game Over -> a new run.
- **Tests:** `gates_2026-11-23/t4_gate.cs` with TG_PATH 3 uses Gate Run's hooks and no longer applies to gate 3. Use
  `K:/WORK_2026/08_04/_claude/gate3_2026-11-23/flow/g3_flow.cs` (Clean for Path 3 -> Whack 6, test hooks on
  BallValleyGate: TestPocketAll, TestHitBlack/TestBreakBlack, TestClockTo) and `g3_collapse.cs` (the loss).
- Gate 3 still uses the shared `Prefabs/Gates/Pumpkin_Ball_Boss` only through its own variant
  `Pumpkin_Ball_Boss_BallValley`, for the Black Pumpkin Ball's laugh, break sound and break effects (no giant pumpkin
  in the gate any more).
