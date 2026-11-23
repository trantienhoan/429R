---
name: unity-429r-playtest
description: Play-testing the 429R VR game ("Four Twenty Knight - 429") in the open Unity Editor through MCP for Unity - starting a run on the throwaway save profile, jumping GAMESTAGES to a room, gate or the rooftop, closing notices, faking punches and grabs, timing a run, taking pictures, offline compile checks, and cleaning up. Use whenever work in this repo needs Play mode, a bot or gate run, or proof in Unity that a change works ("test it", "check it in game", "play it through"), and before any edit-compile-Play cycle.
---

# Play-testing 429R in the Unity Editor

Unity 6 (6000.3.10f1) VR game. The flow is the PlayMaker FSM GAMESTAGES in `Assets/Scenes/2 Game Scene.unity` (see the
`playmaker-429r` skill). Tests run in the user's open Editor through MCP for Unity: `execute_code` runs a C# method
body, `manage_editor` plays/stops, `read_console` reads errors. Ready-made C# for each step: `references/snippets.md`.

## User rules (each one cost a redo once)

- **Saves:** only the throwaway profile `_claude_test` (slot 3). Never read or write the user's 3 slots. Guard every
  save-touching step with `if (Game.Saving.SaveManager.Profile != "_claude_test") return "WRONG PROFILE";`, and delete
  `%USERPROFILE%/AppData/LocalLow/MeLikePie/Four Twenty Knight - 429/Saves/_claude_test` afterwards.
- **No .cs edits during Play.** A reload mid-Play resets statics: the profile goes back to "Local" and GAMESTAGES
  restarts. The Editor preference "Script Changes While Playing" is "Recompile After Finished Playing" (set
  2026-11-22), so a save waits for Play to end; still edit .cs only out of Play.
- **The user saves scenes and .blend files.** A test never saves `2 Game Scene`; menus that edit it leave it dirty.
- **Several Claude sessions may share this Editor.** If the session's notes folder has `UNITY_LOCK.txt`, write your
  session name, the time and what you're doing in it before compiling, running a menu or entering Play; empty it when
  done (about 20 minutes at most). Someone else's name in it: wait, never take over.
- Report numbers (seconds, hp, counts, frame ms), not adjectives. Keep reports short.

## Before Play

1. Compile-check changed C# offline (no Unity needed; a broken .cs in Assets blocks everyone's Play):
   `uv run --python 3.12 python -I .claude/skills/unity-429r-playtest/scripts/csc_check.py Assembly-CSharp.csproj [new.cs ...]`
   Use `Assembly-CSharp-Editor.csproj` for Editor scripts; files Unity hasn't added to the .csproj yet go as extra
   arguments. `python` alone is the Store stub on this PC: always go through `uv`.
2. In Unity: `refresh_unity` with scope "all" and mode "force", wait until `mcpforunity://editor/state` shows neither
   compiling nor updating, and `read_console` shows 0 errors. Only then press Play.

## A run

1. `manage_editor` play; wait for `EditorApplication.isPlaying`.
2. At once: `Application.runInBackground = true;` and `Game.Saving.SaveManager.Profile = "_claude_test";`.
3. Skip the opening, close the Health Warning, wait for the save-slot menu, `StartSlot(2)`, wait for
   `Checkpoints.HasResumed` (snippets 1-4). The run starts on the tutorial rooftop with the caged Fairy at (0,0,0).
4. Jump with `Game.GameStages.Fsm().Fsm.SetState("<state>")`:
   - gates: "Clean for Path 1", "Clean for Path 2", "Clean for Path 3", then close the WORKING SITE notice;
   - rooms: the CP states ("CP Bedroom", "CP 2.5 - BR", "CP LivingRoom", "CP Scale LR", "CP Rooftop", "CP Boss"). A jump
     from the start leaves the start rooftop and its pumpkin tree under the later room: destroy the `Rooftop_Base`
     global's object first. Never use CP jumps for room pictures (old rooms stack): play the rooms through.
5. Notices pause the game (`Time.timeScale` 0): close them (`Game.Menus.TutorialNotice.Find().Close()`). A big EXP
   reward can level up the test player: set EXP 0 in the test or close the card.
6. Drive anything time-critical from an `EditorApplication.update` (or `InputSystem.onBeforeUpdate`) lambda keyed on
   `Time.realtimeSinceStartup`, log into `AppDomain.CurrentDomain.SetData(key, ...)`, read the log once at the end
   (snippet 7). Never poll with execute_code during a motion: each call stalls the Editor 0.3-0.8 s.
7. Stop Play, delete the `_claude_test` folder, empty the lock.

## Hands, punches and grabs (no headset)

- `PcControls` (Editor-only keyboard and mouse) adds itself 0.5 s into Play when no headset is on. For scripted grabs,
  switch off `PcControls.Active` and every `Fist`, then use the near-far interactor's
  `selectInput.inputSourceMode = ManualValue` with `QueueManualState(...)` (only when the value changes) and
  `interactionManager.SelectEnter`.
- Simulated XR controllers pile up across Play sessions: remove them at the start and end of a test.
- To pose a hand, disable its TrackedPoseDriver first (it snaps the hand back every frame).
- A fake punch: raise `Fist.Landed` through its static backing field, or throw a non-grabbable Rigidbody with
  `WeaponPower` from the player's side (it counts as the player's hit, `HeldThings.ByPlayer`).
- With no headset the camera is only 0.7 m above the floor: place test props by floor height.

## Pictures

- Render `Camera.main` into a PNG (snippet 6) in `C:/Users/ADMIN/Documents/429R_claude_backups/<date>/<task>/` on the
  second PC (`K:/WORK_2026/08_04/_claude/` on the K: PC). Never put pictures in `Assets/`: MCP's `manage_camera`
  screenshot without `include_image` saves there.
- The dark shapes at the camera are the player's own hands. A "Render Graph Execution error" (uber post) from such a
  render is harmless.

## Gates

Test hooks per gate are listed in `Exports/Gates/GATES_PLAN.md` §4: Ball Valley's "Play Ball Valley (Play mode)" menu
and `Balls.BallValleyStage.I.ShrinkToLast(1)`; BowlingStage's right-click buttons (Eat The Seed ... Win Now);
CauldronDefence "Test: Break The Cauldron" / `TestSkipTo`, Cauldron "Test: Put The Right Recipe In". A full gate test
goes through GAMESTAGES from "Clean for Path N" until Whack 6 starts on the rooftop.

## When MCP isn't there

- The MCP for Unity server runs only after Window > MCP for Unity > Start Server (port 8080; a plain GET answers 406).
  If the session's UnityMCP tools failed at start, a ToolSearch for "UnityMCP" reconnects. The HTTP runner
  `sh C:/Users/ADMIN/Documents/429R_claude_backups/2026-11-20/rooms/pt.sh <file.cs> [timeout]` also works (a C# 6
  method body).
- Unity closed: a hidden batch run, `Unity.exe -batchmode -projectPath . -executeMethod <Class.Method> -logFile <log>`
  (no -quit; template `Documents/429R_claude_backups/2026-11-02/fat_pumpkin/FatPumpkinBatchTest.cs.txt`). Afterwards
  `git restore` PlayMaker's `EditorStartupPrefs.asset` and delete the stray `.meta` files Unity made.

## Gotchas seen in this project

- Reading `state.Actions` of a PlayMaker FSM on an inactive object in Play spams "Fsm not initialized".
- A `HashSet<T>` is not a non-generic `ICollection`: casting it in a logger throws every frame and looks like a stall.
- C# inside Python strings: "\n" becomes a real newline ("Newline in constant"). Join log lines with " || ".
- Freshly moved colliders report stale bounds until `Physics.SyncTransforms()`.
- execute_code refuses code over 50,000 characters: strip comments and indentation from long test scripts.
- Unity crashed once inside its own Profiler during a long bot run: switch Profiler recording off first.
- `Gate3PickMenu` and other old setup menus rewrite GAMESTAGES: never run a menu just to "see what it does".
