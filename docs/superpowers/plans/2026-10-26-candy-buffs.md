# Candy Buffs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Each eaten Candy_1..8 adds 0.5 of a stat point (Toughness, Move Speed, Throw Power or punch damage) for the run, up to 10 per stat, cleared when a new run starts.

**Architecture:** Player Level (scene singleton) keeps a float candy-points array next to its level points; its point math (`Worth`, `PunchDamageScale`) adds them. Checkpoints raises a new `RunStarted` event that clears them. A PlayMaker action calls Player Level from each candy's eat state; an editor menu puts it there.

**Tech Stack:** Unity 6000.3.10f1, C#, PlayMaker (HutongGames.PlayMaker), XR Interaction Toolkit.

**Spec:** `docs/superpowers/specs/2026-10-26-candy-buffs-design.md`

## Global Constraints
- Points per candy 0.5; limit 10 candy points per stat (serialized `candyMaxPoints = 10` on Player Level).
- Mapping: Candy_1, 2, 6, 7 → Toughness; Candy_3, 4 → Throw Power; Candy_5 → Move Speed; Candy_8 → punch damage.
- Candy punch points feed `PunchDamageScale` only — never `FistSizePoints` (size, flying punch size, punch unlocks).
- Never saved; a new scene starts at 0 (instance field, not static).
- No test framework in the project: tests are Play-mode C# scripts run through the Editor bridge (MCP for Unity `execute_code`, C# 6: no tuples, local functions, `out var`).
- Play tests that eat a candy change the inventory, which saves: back up `%USERPROFILE%/AppData/LocalLow/MeLikePie/Four Twenty Knight - 429/Saves` first and restore it after; verify by hash.
- Commit only when the user asks.

## Review Focus
1. Revive-in-place (clover) must KEEP candy points — `Checkpoints.Die()` with revives left must not raise `RunStarted` (pinned in Task 1 test).
2. Many candies at once (Candy Helmet) must stop at exactly 10 per stat (Task 1 test).
3. Candy 8 must not change fist size or unlock punches: `PunchSizeBonus` / `FistSizeBonus` unchanged (Task 1 test).
4. Move Speed must re-apply to the move providers at once, also while a hurt-slow is running (Task 1 test reads the provider speed).
5. Running the menu twice must not add a second action per candy (Task 3 test).

---

### Task 1: Candy points on Player Level, cleared on a new run

**Files:**
- Modify: `Assets/Scripts/Save/Checkpoints.cs` (event + raise in `StartNewRun()` ~l.169; clear in `ResetStatics()` ~l.83)
- Modify: `Assets/Scripts/Level/PlayerLevel.cs` (fields ~l.93-188, `PunchDamageScale` l.370, `BlastSteps` l.395, `Worth` l.450, `OnEnable/OnDisable` l.722-738)
- Test: `<scratchpad>/t_candy_points.cs` (Play-mode script)

**Interfaces:**
- Produces: `Checkpoints.RunStarted` — `public static event Action RunStarted;` raised at the end of `StartNewRun()`.
- Produces: `PlayerLevel.AddCandyPoints(Stat stat, float points) -> float` (static; returns the stat's candy total after clamping to 0..candyMaxPoints; 0 and a warning for stats other than Toughness, MoveSpeed, ThrowPower, FistSize; no-op returning 0 without an Instance). Calls `ApplySpeed()` when `stat == Stat.MoveSpeed`.
- Produces: `PlayerLevel.CandyPoints(Stat stat) -> float` (static, 0 without an Instance), `PlayerLevel.ClearCandyPoints()` (static; re-applies speed).

- [ ] **Step 1: Write the Play-mode test** `t_candy_points.cs`, returning a PASS/FAIL line per check:
  - 25 × `AddCandyPoints(Toughness, 0.5f)` → `CandyPoints(Toughness) == 10`; `DamageShare` equals the value before minus `10 * damageCutPerPoint` (clamped at 0.1).
  - 3 × `AddCandyPoints(MoveSpeed, 0.5f)` → `SpeedScale` rose by `1.5 * speedPerPoint`, and a `ContinuousMoveProvider.moveSpeed` on the XR Origin changed.
  - 2 × `AddCandyPoints(ThrowPower, 0.5f)` → `ThrowScale` rose by `1 * throwPerPoint` (or is at `maxThrowScale`).
  - 20 × `AddCandyPoints(FistSize, 0.5f)` → `PunchDamageScale` rose by `10 * punchDamagePerPoint`; `PunchSizeBonus` and `FistSizeBonus` unchanged.
  - `AddCandyPoints(MaxHealth, 0.5f)` returns 0.
  - `Checkpoints.Die()` while `Current != "" && RevivesLeft > 0` is NOT exercised (would move the player); instead assert `RunStarted` fires once on `Checkpoints.StartNewRun()` and all four `CandyPoints` are 0 afterwards, `DamageShare`/`SpeedScale`/`PunchDamageScale` back to their starting values.
- [ ] **Step 2: Run it before the change** — expected: compile error (`AddCandyPoints` missing) = FAIL.
- [ ] **Step 3: Implement** in Checkpoints: the event, raised last in `StartNewRun()`, set to null in `ResetStatics()`. In PlayerLevel: `[Header("Candy")] [SerializeField] private float candyMaxPoints = 10f;`, `private readonly float[] candyPoints = new float[StatCount];`, the three statics above, subscribe `ClearCandyPoints` to `Checkpoints.RunStarted` in `OnEnable` (Instance only) and unsubscribe in `OnDisable`. `Worth` becomes `private float Worth(Stat stat)` and adds `candyPoints[(int)stat]` for Toughness, MoveSpeed and ThrowPower only; `BlastSteps` becomes `float` (`Mathf.Max(0f, Worth(Stat.ThrowPower) - 1f)`); `PunchDamageScale = 1f + (Points(Stat.FistSize) + candyPoints[(int)Stat.FistSize]) * punchDamagePerPoint`. Fix any other `Worth` use the compiler flags; doc comments in the file's style.
- [ ] **Step 4: Compile** (AssetDatabase.Refresh via the bridge; Editor.log has no `error CS`) and **run the test in Play mode** — expected: every line PASS; leave Play mode.

### Task 2: "Eat Candy Buff" PlayMaker action

**Files:**
- Create: `Assets/PlayMaker Custom Actions/Player/EatCandyBuff.cs` (pattern: `AddPlayerExp.cs`, enum field like `FacePlayer.ModelFront`)

**Interfaces:**
- Consumes: `PlayerLevel.AddCandyPoints(Stat, float)`.
- Produces: `HutongGames.PlayMaker.Actions.EatCandyBuff : FsmStateAction`, `[ActionCategory("Player")]`, nested `public enum Boost { Toughness, MoveSpeed, ThrowPower, PunchDamage }`, fields `public Boost boost;` and `public FsmFloat points;` (Reset: `Toughness`, `0.5f`). `OnEnter`: map Boost → Stat (PunchDamage → Stat.FistSize), `AddCandyPoints`, `Finish()`.

- [ ] **Step 1: Write the Play-mode test** `t_eat_action.cs`: build an `EatCandyBuff` in code (`boost = PunchDamage`, `points = 0.5f`), call `OnEnter()` twice → `CandyPoints(FistSize) == 1`, `PunchSizeBonus` unchanged; then `Checkpoints.StartNewRun()` to clean up.
- [ ] **Step 2: Run** — expected FAIL (type missing).
- [ ] **Step 3: Implement** the action; tooltips in the project's plain-words style (what each boost does, "0.5 = half a level-up point").
- [ ] **Step 4: Compile and run the test** — expected PASS.

### Task 3: Menu that gives every candy its buff

**Files:**
- Create: `Assets/Scripts/Inventory/Editor/CandyBuffSetupMenu.cs` (pattern: `CandyMouthSetupMenu.FixCandy`, `CheckpointSetupMenu.New<T>`: `Fsm.Init` if needed, `LoadActions`, set `state.Actions`, `state.SaveActions()`, `SaveAsPrefabAsset`)

**Interfaces:**
- Consumes: `EatCandyBuff`, `EatCandyBuff.Boost`.
- Produces: menu `Tools/429 Game/Player/Give Candies Their Buffs` → `CandyBuffSetupMenu.Run()`.

- [ ] **Step 1: Write the edit-mode test** `t_candy_menu_check.cs`: for Candy_1..8 load the prefab, find Damage FSM state "Health 1", count `EatCandyBuff` actions (must be exactly 1) and report boost/points; expected table = Global Constraints mapping, points 0.5.
- [ ] **Step 2: Run** — expected FAIL (0 actions).
- [ ] **Step 3: Implement** `Run()`: back up nothing (git tracks prefabs); for i in 1..8 open `Assets/Prefabs/Items/Breakable/Candy_i.prefab`, state "Health 1" of the "Damage" FSM: reuse an existing `EatCandyBuff` (set boost/points from the table) or append a new one after the existing actions; save the prefab; log one summary line. Safe to run again.
- [ ] **Step 4: Run the menu twice, then the check** — expected: exactly 1 action per candy with the mapping; `git diff --stat` shows only the 8 candy prefabs.

### Task 4: Real eat in Play mode

**Files:**
- Test: `<scratchpad>/t_candy_eat.cs` (Play-mode)

- [ ] **Step 1: Back up the Saves folder** (Global Constraints).
- [ ] **Step 2: Play test**: spawn Candy_5, then Candy_8, at the Mouth (Main Camera child "Mouth") with gravity off; after 1 s each: `CandyPoints(MoveSpeed) == 0.5`, `CandyPoints(FistSize) == 0.5`, candies destroyed, `PunchSizeBonus` unchanged; `Checkpoints.StartNewRun()` → 0.
- [ ] **Step 3: Leave Play mode, restore the Saves folder, verify hashes match the backup.**
