# Spider Web Meter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Spider web balls build a web meter (20% each, Giant Web = 100%) that melts when the player isn't hit; at 100% the player walks at 25% with no teleport for 5 s, then gets a 3 s web-free break; the web shows as rings on the health heart.

**Architecture:** A new `PlayerWeb` component on the XR Origin (auto-added like `HurtSlow`) owns the meter, melting, lock and break, and does the actual slow through `PlayerSlow.Apply`. `WebBall` calls `PlayerWeb.Add` / `Fill` instead of slowing. `PlayerHealthHUD` reads `PlayerWeb` and shows one of 5 web sprites over the heart, drawn once by an Editor menu.

**Tech Stack:** Unity 6000.3.10f1, C#, XR Interaction Toolkit (`ContinuousMoveProvider`, teleport rays via `PlayerSlow`), uGUI `Image` + TMP on the head-locked HUD canvas, PlayMaker (PlayerHealth FSM, read only).

**Spec:** `docs/superpowers/specs/2026-10-26-spider-web-meter-design.md`

## Global Constraints
- Normal web ball = +20% (0.2). Giant Web = straight to 100%. Locked at 100%: walk speed share **0.25**, **no teleport**, **5 s**; then meter **0** and a **3 s** break where nothing adds web (balls still hurt).
- Hits during the lock or the break add nothing and never extend either.
- Melting: starts **2 s** after the last web hit, **0.1 per second** (20% per 2 s), never below 0. Rings shown = `ceil(level × 5)` (0 at level 0); 5 while locked; 0 during the break.
- Runs on game time (`Time.time`): a pause freezes the meter, the lock and the break.
- Going down (health ≤ 0, `Checkpoints.IsDying`, `PlayerRevive.Instance.IsReviving`) or `Checkpoints.RunStarted` clears everything: meter 0, lock and break ended, full speed and teleport back at once.
- Spiders (`Spider_Smarter.prefab`): `webCooldown` (3, 5), `maxWebShooters` 2. Small spiders still never web (`small.webs` false, untouched).
- Unchanged: web ball damage/flight/aim/swat/shield block, Giant Web aim/size/unswattable/damage event, roach Face Leap slow, bug pumpkin slow, Hurt Slow. `PlayerSlow` only gains `EndWebSlow()`.
- Heart look A: white web clipped to the heart, 8 spokes, one ring per 20%; sprite 5 has thicker lines. While locked: heart paler, shaking, white `0:05`-style countdown under the heart. HUD stays up while rings > 0 or locked; hidden with "Health: hidden".
- Code style: match the surrounding files (plain-English tooltips and `///` summaries, `[Header]`s, same comment density). No haptics. Never read `.blend` files.
- Tests: Play-mode C# 6 step files run through MCP for Unity with `unity_test.ps1` (copy from `.superpowers/sdd/2026-10-26-cockroach-cling/`; it snapshots, restores and verifies the 3 real save slots). Step files that poll start with `w_` and return `READY` when done.
- Before editing, back up every file a task changes to `K:/WORK_2026/08_04/_claude/backups/web_meter_2026-10-26/`. **No commits** (the user commits themselves; don't remind them).
- "2 Game Scene" may hold the user's unsaved work: after the HUD menu, save the scene only if it was not dirty before the menu ran; otherwise leave it dirty and tell the user.

## Review Focus
1. Pause while webbed: `Time.timeScale = 0` for 3 real seconds leaves the meter, the rings and the lock's seconds unchanged (Task 1 test `t1_pause`).
2. Dying while locked: the player goes down mid-lock → meter 0, `PlayerSlow.TeleportBlocked` false, speed share 1 at once, no break left over (Task 1 test `t1_down`).
3. A lone spider at its fastest (a hit every ~4.75 s: 1.75 s shot + 3 s wait, release at 1.25 s) never fills: hits 4.5 s apart × 6 keep `Level ≤ 0.2` and never lock (Task 1 test `t1_lone`).
4. A Giant Web landing in the break adds nothing and doesn't start a new lock (Task 2 test `t2_giant_in_break`).
5. The web picture never covers the health number: the Web image is a child of the heart, stretched to it, and doesn't overlap the number's rect (Task 3 check `t3_layout`).
Reviewer also checks by reading: a swatted ball or a legendary-shield block still returns before the player branch in `WebBall.HitSomething` (that path is unchanged code, so it adds no web).

---

### Task 1: PlayerWeb (meter, melt, lock, break, clear) + PlayerSlow.EndWebSlow

**Files:**
- Create: `Assets/Scripts/Locomotion/PlayerWeb.cs`
- Modify: `Assets/Scripts/Locomotion/PlayerSlow.cs` (add `EndWebSlow` next to `EndHurt`, ~l.89-96)
- Test: `.superpowers/sdd/2026-10-26-spider-web-meter/t1/*.cs`, run by `run_task1.ps1`

**Interfaces:**
- Produces (namespace `Game.Locomotion`):
  - `public class PlayerWeb : MonoBehaviour` — `[DisallowMultipleComponent]`; puts itself on the XR Origin that has the PlayerHealth FSM at scene load (copy `HurtSlow.AddToPlayer`/`OnSceneLoaded`/`AddNow`); a hand-added one is used instead.
  - Settings (`[SerializeField]`, tooltips): `lockSpeed` 0.25, `lockSeconds` 5, `breakSeconds` 3, `meltDelay` 2, `meltPerSecond` 0.1.
  - `public static void Add(float share)` — adds `share` (clamped 0..1) unless locked, on a break, down, or no `PlayerWeb`; resets the melt delay; reaching ≥ 0.999 starts the lock.
  - `public static void Fill()` — same as `Add(1f)`.
  - `public static float Level { get; }` (0..1; 1 while locked; 0 during the break and with no `PlayerWeb`)
  - `public static int Rings { get; }` (0..5 per Global Constraints)
  - `public static bool Locked { get; }`, `public static float LockSecondsLeft { get; }` (0 when not locked), `public static bool OnBreak { get; }`
  - Static instance reset in a `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)] ResetStatics`.
- Produces in `PlayerSlow`: `public static void EndWebSlow()` — ends the `Apply`-style slow (all of them: web, roach, bug pumpkin) and gives teleport back at once; no-op if none. Summary says so.

- [ ] **Step 1: Write the Play-mode steps** (`t1/`, header like `cockroach-cling/t1/_header.txt` with `res(...)`; read state through the public statics and `XROrigin`'s `PlayerSlow` (`CurrentShare`, `TeleportBlocked`)). Each step's checks, in order:
  - `t1_add`: `Add(0.2f)` → `Level` within 0.2 ± 0.01, `Rings == 1`, `!Locked`, `TeleportBlocked == false`, `CurrentShare > 0.99`.
  - `t1_melt` (after a 1.5 s gap): `Level` still 0.2 ± 0.01; (after 3 s more) `Rings == 0`, `Level == 0`.
  - `t1_lock`: `Add(0.2f)` × 5 in one step → `Locked`, `Rings == 5`, `TeleportBlocked`, `CurrentShare` within 0.25 ± 0.02, `LockSecondsLeft` within 5 ± 0.2.
  - `t1_no_extend` (1 s later): remember `LockSecondsLeft`, `Add(0.2f)`, `Fill()` → `LockSecondsLeft` not larger than remembered + 0.05.
  - `w_t1_unlocked` until `!Locked`: then `OnBreak`, `Level == 0`, `Rings == 0`, `TeleportBlocked == false`; (0.6 s later, past the 0.5 s ease) `CurrentShare > 0.99`.
  - `t1_break`: `Add(0.2f)`; `Fill()` → `Level == 0`, `!Locked`; `w_t1_break_over` until `!OnBreak`; then `Add(0.2f)` → `Level` 0.2 ± 0.01.
  - `t1_lone`: wait out the melt, then 6 hits 4.5 s apart (`Add(0.2f)` steps with `z_wait` gaps) → after each, `Level ≤ 0.21` and never `Locked`.
  - `t1_pause`: `Add(0.2f)` × 3, record `Level`; set `Time.timeScale = 0`; after 3 s real, `Level` and `Rings` unchanged (± 0.01); `Fill` to lock, timeScale 0 again 2 s → `LockSecondsLeft` unchanged (± 0.05); restore `Time.timeScale = 1`.
  - `t1_down`: lock with `Fill()`; set `Checkpoints.IsDying` true via reflection on its private setter; next frame → `Level == 0`, `!Locked`, `!OnBreak`, `TeleportBlocked == false`, `CurrentShare > 0.99`; set it back false.
  - `t1_new_run`: `Add(0.2f)` × 2, invoke `Checkpoints.StartNewRun()` → `Level == 0`.
  - Last step prints `ALL PASS` if no `FAIL`.
- [ ] **Step 2: Run** `run_task1.ps1` — expected: FAIL (compile error: `Game.Locomotion.PlayerWeb` does not exist).
- [ ] **Step 3: Implement** `PlayerSlow.EndWebSlow()` and `PlayerWeb`. The down test is the same as `HurtSlow.Update`'s `down`; clear once on the change to down. Lock start = `PlayerSlow.Apply(lockSpeed, lockSeconds, true)`; Clear while locked = `PlayerSlow.EndWebSlow()`. Subscribe to `Checkpoints.RunStarted` in `OnEnable`, unsubscribe in `OnDisable`. Class summary in the house style (what the player feels, the numbers, where it lives, how to change them).
- [ ] **Step 4: Compile** (refresh the AssetDatabase through MCP, wait until not compiling, no new errors in the console), **run** `run_task1.ps1` — expected: every line PASS, `ALL PASS`, "saves restored and verified".

### Task 2: Web balls fill the meter; Giant Web; spiders shoot more

**Files:**
- Modify: `Assets/Scripts/Enemies/WebBall.cs` (fields l.26-31, summary l.11-19, `MakeGiant` l.103-117, `HitSomething` l.193-200)
- Modify: `Assets/Scripts/Enemies/SpiderBrain.cs` (Giant Web settings l.729-733, `LaunchWeb` l.3348)
- Modify: `Assets/Prefabs/Enemies/Enemies_Smarter/Spider_Smarter.prefab` (through `PrefabUtility.LoadPrefabContents` + `SerializedObject` + `SaveAsPrefabAsset` in an edit-mode snippet, not by hand)
- Test: `t2/*.cs` (Play mode), `t2_prefab_check.cs` (edit mode)

**Interfaces:**
- Consumes: `PlayerWeb.Add(float)`, `PlayerWeb.Fill()`, `PlayerWeb.Level/Locked/OnBreak` (Task 1).
- Produces: `WebBall.webAdded` (`[SerializeField] float`, `[Range(0,1)]`, default 0.2, tooltip "How much web it adds to the player's web meter when it hits them (0.2 = 20%; at 100% they're webbed)") replacing `slowTo`/`slowSeconds`; `public void MakeGiant(string eventName, string fsmName, float size)` (fills the meter; the slow parameters are gone). `SpiderBrain` loses `giantWebSlowTo` and `giantWebSlowSeconds`.

- [ ] **Step 1: Write the steps.** Spawn `Spider_Web_Ball.prefab` (load with `AssetDatabase.LoadAssetAtPath` in Play) 1.2 m in front of the camera at chest height (camera − 0.45 m), set its private `playerEvent` to `""` by reflection (no damage, so Hurt Slow stays out of it), `Launch(WebBall.Aim(from, chest, 6f, 9.81f), null)`.
  - `t2_ball` (0.8 s later): `PlayerWeb.Level` 0.2 ± 0.01, `TeleportBlocked == false`, `CurrentShare > 0.99`.
  - `t2_scenery`: a 1 m cube between the ball and the player → after 0.8 s `Level` unchanged, cube destroyed after.
  - `t2_giant`: wait out the melt; a ball with `MakeGiant("", "Damage", 2.5f)` → `Locked`, `TeleportBlocked`.
  - `t2_giant_in_break`: `w_` until `OnBreak`; another giant ball → after 0.8 s `Level == 0`, `!Locked`.
  - `t2_prefab_check.cs` (edit mode, fresh `LoadPrefabContents`): `Spider_Smarter` `webCooldown == (3,5)`, `maxWebShooters == 2`; `Spider_Normal`, `Spider_Tree`, `Spider_Boss`, `Spider_Boss_PumpkinButt` have no override of those two (their brains read (3,5) and 2); `Spider_Web_Ball` `webAdded == 0.2`.
- [ ] **Step 2: Run** — expected: FAIL (compile: `MakeGiant` takes 5 arguments / no `webAdded`).
- [ ] **Step 3: Implement**: `HitSomething`'s player branch calls `PlayerWeb.Add(webAdded)` where it called `PlayerSlow.Apply`; `MakeGiant` sets `webAdded = 1f`; update the class summary (the web builds up; 100% = webbed). `LaunchWeb` passes `(specialPlayerEvent, PlayerHealthFsm, giantWebSize)`; fix the `giantWebSpeed` tooltip if it mentions the slow. Then the prefab snippet sets the two `Spider_Smarter` values.
- [ ] **Step 4: Compile, run** `run_task2.ps1` and the prefab check (`-EditMode`) — expected: `ALL PASS` both; `git diff --stat` on `Assets/Prefabs` shows only `Spider_Smarter.prefab` changed by this task.

### Task 3: The web on the heart

**Files:**
- Modify: `Assets/Scripts/Combat/PlayerHealthHUD.cs` (parts l.33-46, `Refresh` l.292-362, `UpdateTimers`/`LayOutTimers`/`BuildTimers` l.468-570, summary l.8-21)
- Modify: `Assets/Scripts/Combat/Editor/PlayerHealthHUDSetupMenu.cs` (new menu next to `UpdateHud`; reuses `PrepareSprite`, `FindOrCreateImage`, `SetupUtility`)
- Create: `Assets/Images/heart_web_1.png` … `heart_web_5.png` (written by the menu)
- Modify: `Assets/Scenes/2 Game Scene.unity` (the menu adds the Web image; see the scene-save rule)
- Test: `t3/*.cs` (Play mode), `t3_layout.cs` (edit mode)

**Interfaces:**
- Consumes: `PlayerWeb.Rings`, `PlayerWeb.Locked`, `PlayerWeb.LockSecondsLeft` (Task 1).
- Produces: `PlayerHealthHUD` fields `[SerializeField] private Image web;` and `[SerializeField] private Sprite[] webSprites;` (index 0 = 1 ring … 4 = 5 rings), tuning `webPop` 0.15 / `webPopSeconds` 0.25, `webLockPale` 0.3, `webShake` 3 (canvas units). Menu `Tools/429 Game/Player/Add Web To Health HUD` (disabled while playing), safe to run again.

- [ ] **Step 1: Write the checks.**
  - `t3_layout.cs` (edit mode, after the menu): 5 assets `Assets/Images/heart_web_{1..5}.png` exist, imported as Sprite, 256 × 256; the HUD's `web` is set, is a child of `heart`, stretched to it (anchors 0..1, offsets 0), sits above the crack and below the shine, `enabled == false`; its world rect doesn't overlap the number's; `webSprites` has 5 entries in order. Pixel check: sprite 1 has opaque white pixels near the heart's centre and none outside `heart_white.png`'s alpha; sprite 5 has more opaque pixels than sprite 4.
  - Play steps: `PlayerWeb.Add(0.2f)` → `web.enabled`, `web.sprite == webSprites[0]`, HUD `CanvasGroup.alpha == 1`; `Add` × 2 → `webSprites[2]`; `Fill()` → `webSprites[4]`, the web countdown row active with text `0:05` or `0:04`; `w_` until `!PlayerWeb.Locked` → `web.enabled == false`; `PlayerHealthHUD.Shown = false` while webbed → alpha 0 (set it back to its old value in the same step).
- [ ] **Step 2: Run** — expected: FAIL (no menu, no `web` field).
- [ ] **Step 3: Implement.**
  - Menu: draws the 5 PNGs at 256 × 256, white, look A — centre (0.5 w, 0.49 h from the top), 8 spokes at 22.5° + i·45° reaching past the heart edge, rings 1..n at radii 0.09/0.18/0.28/0.38/0.48 × width, each ring = 8 quadratic sags between neighbouring spokes (control point at 0.8 × radius on the mid angle); line width 3 px (sprites 1-4), 4.5 px (sprite 5); anti-aliased; alpha × `heart_white.png` alpha. Then `PrepareSprite` each, `FindOrCreateImage(heart, "Web")`, place like `AddCrack` (above the crack), `enabled = false`, set `web` and `webSprites`, mark the scene dirty, log "Save the scene".
  - HUD: `webbed = PlayerWeb.Rings > 0 || PlayerWeb.Locked` keeps the HUD up like `buffed` (and restarts the fade when it ends); web sprite by `Rings`, pop on a ring increase (unscaled time); while locked: heart fill colour lerped `webLockPale` towards white, heart jitters `webShake` canvas units, and the countdown is one extra timer row (index `PlayerBuffs.Count`, colour `#F1EFE8`, same blink-red rule) shown while locked — `UpdateTimers` runs when `buffed || locked`.
  - Back up the scene, check `scene.isDirty`, run the menu, save the scene only if it was clean (Global Constraints).
- [ ] **Step 4: Compile, run** the menu twice (second run changes nothing), `t3_layout.cs` (`-EditMode`) and `run_task3.ps1` — expected: `ALL PASS`.

### Task 4: Real spiders + pictures

**Files:**
- Test: `t4/*.cs` (Play mode, prefabs as saved, no runtime changes to spiders)

- [ ] **Step 1: Write the steps.** First set the PlayerHealth FSM's `PlayerHealth` float to 5000 (the spiders also bite); remember the old value only for the report (saves are restored by the runner anyway).
  - Group: spawn 3 `Spider_Normal` 4 m from the player at 120° apart (on the NavMesh); `w_t4_group` polls up to 40 s until `PlayerWeb.Locked` → PASS "a group webbed the player in N s"; destroy them.
  - Lone: after the lock and break end, spawn 1 `Spider_Normal` 4 m away; poll for 30 s recording the highest `PlayerWeb.Level` and whether `Locked` ever came on → PASS if never locked; report the highest level and how many hits.
  - Pictures: during a group run, `ScreenCapture.CaptureScreenshot` of the Game view at 3 rings (`.superpowers/sdd/2026-10-26-spider-web-meter/web_3_rings.png`) and while locked (`web_locked.png`).
- [ ] **Step 2: Run** `run_task4.ps1` — expected: both PASS. If the group doesn't lock within 40 s, record how many balls hit (count `Level` rises) before changing anything — it's a feel question for the user, not a test to force.
- [ ] **Step 3: Look at both pictures**: web visible on the heart, number readable, nothing clipped. Send them to the user with the results.
