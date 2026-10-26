# Cockroach Face Cling Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A cockroach's Face Leap can cling to the player's face (crawling, biting every ~2 s) until any hand slap or punch knocks it off.

**Architecture:** A new `Cling` state in `SpiderBrain` entered from the face leap at Hit From when the attack has `clingToFace`. While clinging, LateUpdate poses the roach's `models` (and body colliders) on the head instead of `ShowLeap`; Update bites on a timer, runs the ghost-balloon hand-slap check, and lets go on a hit, a slap, death, the player going down, or a head jump (teleport / fall edge). Letting go = the hit path: `Flinch` + fall from the face.

**Tech Stack:** Unity 6000.3.10f1, C#, NavMeshAgent, Animator, PlayMaker events (player damage), XR Interaction Toolkit (controllers via `XRRig.FindController`).

**Spec:** `docs/superpowers/specs/2026-10-26-cockroach-cling-design.md`

## Global Constraints
- Knocked off by any hand slap or punch (open hand or fist), never by a slow hand near the face.
- Bites every 2 s through the normal bite event (`BiteEvent`, damage flash), no shove while on the face.
- Crawl = the Animator's `run` state at animator speed 0.35 (the state itself is speed 3); `bite` played on each bite. No new animation.
- Belly towards the player, head up: models rotation = `LookRotation(camera.up, camera.forward)`; position = camera + forward 0.15 − up 0.06 (+ small sideways wiggle). Never fully covering the view.
- One roach at a time (static `clinger`, reset in `ResetStatics`); others keep Through Player.
- Bosses (Cockroach_Boss, Cockroach_Boss_PumpkinButt) and spider bosses don't cling.
- Tests: Play-mode C# 6 scripts via MCP for Unity (`.superpowers/sdd/2026-10-26-cockroach-cling/`), runner snapshots + restores Saves (copy `unity_test.ps1` from the candy plan's workspace). Back up changed prefabs/scripts to `K:/WORK_2026/08_04/_claude/backups/cockroach_cling_2026-10-26/`. No commits.

## Review Focus
1. A hand moving slowly near the face (eating candy) must not knock it off (Task 1 test: slow hand → still clinging).
2. Killed while clinging → it falls to the floor, nothing left floating at the face (Task 1 test).
3. The player teleporting / falling off an edge while it clings → it lets go (Task 1 test: move the XR Origin 2 m).
4. Two clingers leaping → only one clings, the other passes through (Task 1 test).
5. After letting go, the model's rotation is back to normal (upright, on the floor) (Task 1 test).

---

### Task 1: The Cling state in SpiderBrain

**Files:**
- Modify: `Assets/Scripts/Enemies/SpiderBrain.cs` (AttackMove ~l.121-144, State enum l.255, fields near l.989, ResetStatics l.1044, Update switch l.1189, DoAttack l.1596-1659, TryBite tail l.1848-1861, LateUpdate l.2974, OnAnyHit l.3233, OnWeakHit l.3255, Die l.3304, OnDisable l.1123)
- Test: `t_cling_*.cs` (Play-mode steps)

**Interfaces:**
- Produces: `AttackMove.clingToFace` (bool), `AttackMove.clingBiteSeconds` (float, 2); brain fields under `[Header("Face Cling")]`: `clingOffset` (Vector3 (0, -0.06, 0.15)), `clingCrawlSpeed` (0.35), `clingSlapSpeed` (1.2 m/s), `clingStunSeconds` (0.8), `clingLetGoJump` (0.75 m head jump in one frame).
- Produces: `public bool IsClinging` (for tests and other scripts).

- [ ] **Step 1: Write the Play-mode test steps**: spawn `Cockroach_Normal` 1.6 m in front of the camera; at runtime keep only its Face Leap and set `clingToFace = true` (reflection on the instance). Checks: within 4 s `IsClinging`; 3 s later still clinging, models within 0.3 m of the camera, at least one bite (PlayerHealth FSM health dropped); a slow hand (0.3 m/s) into it → still clinging; a fast hand (controller moved 0.3 m in one frame into it) → not clinging, state Flinch, after 1.5 s on the floor (models within 0.1 m of home height) and upright; re-cling, then move the XR Origin 2 m → lets go; re-cling, then kill it (health 0) → falls; two clingers → only one `IsClinging`.
- [ ] **Step 2: Run** — expected FAIL (compile: `clingToFace` / `IsClinging` missing).
- [ ] **Step 3: Implement**: State `Cling`; `StartCling()` at Hit From in DoAttack when `move.leapToFace && move.clingToFace && clinger == null && !PlayerDown()` (no shove, no pass); `DoCling()` (bite timer → `BitePlayer()` extracted from TryBite's success tail with a `shove` flag; crawl/bite animation; `CheckSlaps()` like GhostBalloon with `XRRig.FindController`; let go on player down (`DeathCameraFall.IsDown` or `Checkpoints.IsDying`) or head jump); `PoseOnFace()` in LateUpdate (models + collider centres) instead of ShowLeap; `LetGo(bool knocked)`: clinger = null, restore models' local rotation, warp the agent to the NavMesh under the face, `leapLift` = face height so ShowLeap/FallIfLifted drop it, Enter Flinch for `clingStunSeconds` with the hit look (knocked) and attack cooldown; OnAnyHit / OnWeakHit / Die / OnDisable call `LetGo` first when clinging.
- [ ] **Step 4: Compile, run the test** — expected every line PASS, saves restored.

### Task 2: Menu + prefabs

**Files:**
- Create: `Assets/Scripts/Enemies/Editor/CockroachClingMenu.cs` (pattern: `CockroachLeapThroughMenu`)
- Test: `t_cling_menu_check.cs` (edit mode, fresh `LoadPrefabContents`)

**Interfaces:** consumes `AttackMove.clingToFace`. Produces menu `Tools/429 Game/Enemies/Cockroaches Cling To The Face`.

- [ ] **Step 1: Write the check**: Face Leap `clingToFace` true on Cockroach_Smarter, Normal, Small and on the cockroaches inside Bedroom_Bed_Metal, Livingroom_Book_Shelf_Metal, Livingroom_Table_2_Metal; false on Cockroach_Boss, Cockroach_Boss_PumpkinButt, Spider_Boss(_PumpkinButt); Through Player still true everywhere (the fallback).
- [ ] **Step 2: Run** — expected FAIL.
- [ ] **Step 3: Implement** the menu (base prefab first; furniture roaches only if they have their own attack override); back up the prefabs first.
- [ ] **Step 4: Run the menu twice, the check, `git diff --stat`** — expected PASS, only those prefabs.

### Task 3: Real leap + picture

- [ ] **Step 1: Play test** with the prefab as saved (no runtime changes): a Cockroach_Normal clings within 6 s of being spawned 1.6 m away (retry spawn up to 3 times — it picks attacks at random), then a fast hand knocks it off.
- [ ] **Step 2: Game-view picture** while it clings (`ScreenCapture`), check it covers part of the view, belly and legs visible, not clipped; adjust `clingOffset` if not, re-run Task 1's test.
