---
name: playmaker-429r
description: Reading and changing PlayMaker FSMs in the 429R Unity project safely, above all GAMESTAGES (the game's flow FSM in 2 Game Scene) and prefab FSMs - reading states and transitions straight from the scene YAML, tracing the flow (rooms, gates, rooftop, boss, replay), changing FSMs through re-runnable Tools > 429 Game menus, and the serialization traps that silently lose edits. Use whenever a task touches GAMESTAGES, a PlayMaker state, event, variable or action, the game flow, or a "Set Up ..." editor menu that edits FSMs.
---

# PlayMaker in 429R

The user is a PlayMaker designer: the game's logic is mostly FSMs, with C# components where code is simpler. Only
`Assets/Scenes/2 Game Scene.unity` ships. Its FSM **GAMESTAGES** (on `-GAMESTAGES`) runs the whole flow.

## Ground rules (the user's)

- Smallest PlayMaker-friendly change first: one action, one transition, one value. Offer bigger systems as options.
- Scene and FSM changes are made by a **re-runnable `Tools > 429 Game > ...` menu** (an Editor script that can run
  twice safely, with an undo menu when it rewires the flow), not by hand-editing YAML and not by one-off code.
- **The user saves the scene** (and .blend files). After a menu run, say the scene must be saved. Before analysing a
  scene change the user made, check it is on disk (`git diff --stat`, or the file's time): unsaved edits aren't there.
- Never edit .cs files during Play. Short notice texts: a title plus one short line, the user's words, no pop-up
  effect words.

## Read the flow without Unity

`scripts/gamestages.py` reads any FSM straight from the YAML on disk:

```
uv run --python 3.12 python -I .claude/skills/playmaker-429r/scripts/gamestages.py dump "Assets/Scenes/2 Game Scene.unity" gs.json
uv run --python 3.12 python -I .claude/skills/playmaker-429r/scripts/gamestages.py trace gs.json "Rooftop_ON PG" 40
uv run --python 3.12 python -I .claude/skills/playmaker-429r/scripts/gamestages.py show gs.json "Path 3 ON" "BOSS"
uv run --python 3.12 python -I .claude/skills/playmaker-429r/scripts/gamestages.py name "Assets/Scenes/2 Game Scene.unity" 84818218
```

YAML facts: the FSM block is `fsm:` > `name: GAMESTAGES` > `states:`; each state has `transitions:` (`- fsmEvent:` >
`name:`, then `toState:`) and `actionData:` (`actionNames`, `stringParams` = event names, `fsmStringParams` /
`fsmGameObjectParams` = values, `byteData` = packed bools/ints). Object references are `{fileID: N}`: the GameObject is
`--- !u!1 &N` with its `m_Name` below (the `name` command). Notice texts are multi-line strings with blank lines. Older
prefabs have an empty `m_EditorClassIdentifier`: find PlayMakerFSM by `m_Script` fileID `1384774831`, guid
`e743331561ef77147ae48cda9bcb8209`. Files use CRLF (ripgrep `$` needs `\r?$`).

## The flow (2026-11-22; re-check with the script, it changes)

```
Init - Stage1 ... Walk Tuttorial -> Move to Fairy -> Send death -> Stage 1 - The Start
  next -> the house: Clean Stage 1 -> 2 - The BR ... rooms ... Rooftop_ON
  next1/2/3 (Cube_N bought AND Gate_N stood in) -> Clean for Path N -> Demo? Path N -> ... -> Path N ON
    -> (4 gear bools + The_Seed) -> ... -> Tween PlayGroundZ -> Destroy PlayGroundZ -> Rooftop_ON PG
Rooftop_ON / Rooftop_ON PG -> Prepare_Whack_6 (CP Rooftop) -> Whack 6 -> Just Wait -> Prepare_Whack_7 -> Whack 7
  -> Damage The Lid (CP Boss) -> Wait For the Punch -> clean it -> BOSS -> wait -> fairy reward (sends Win)
  -> Thank For Playing (waits GAMEOVER) -> clean all (StartNewRun) -> recreate -> Restart Loop -> Recreate
  -> Checkpoint? -> Walk Tuttorial (a new run)
Global transitions: GameOver -> Game Over, Win -> wait, DOOR_SHORTCUT -> Door Shortcut.
```

- Bools: `Cube_1-3` = a gate bought (kept for good, Unlocks); `Gate_1-3` = standing in that gate's circle (false when
  stepping out); gear bools `Bracer_Gear`, `Chest_Gear`, `Head_Gear`, `Shield_Gear` (+ old Sword/Boots), `The_Seed`,
  `BOSS`, `BOSSPUNCH`, `GAMEOVER`, `whack_6/7`, `Pumpkin_*`. "Restart Loop" and "Game Over" reset them for a new run.
- Checkpoints (SetCheckpoint / Go To Checkpoint): CP Bedroom, CP 2.5 - BR, CP LivingRoom, CP Scale LR, CP Rooftop,
  CP Boss (a save left at CP Boss loads as a new run). Gates set none.
- Killing any real Pumpkin_Boss_Z sends the global "Win" (PumpkinBossBrain `gameStagesEvent`): the run is won.
- Code access: `Game.GameStages.Fsm()` (the PlayMakerFSM), `GetBool`, `SetBool(name, value, context)`, `ActiveState()`.

## Changing FSMs from an Editor menu

- Scene FSM: find it in the open scene (`Game.GameStages.Fsm()` in Play; in Edit mode
  `Resources.FindObjectsOfTypeAll<PlayMakerFSM>()` with `gameObject.scene.IsValid()`, object `-GAMESTAGES`, FsmName
  `GAMESTAGES`). Then `EditorUtility.SetDirty(fsm)` + `EditorSceneManager.MarkSceneDirty(scene)`; the user saves.
- Prefab FSM: `PrefabUtility.LoadPrefabContents` -> edit -> `SaveAsPrefabAsset` -> `UnloadPrefabContents`. Call
  `fsm.Fsm.Init(fsm)` / `state.LoadActions()` before reading actions, or they read as zeros and nulls.
- After changing a state's actions, `state.SaveActions()`. Transitions are `FsmTransition { FsmEvent, ToState }`; the
  event must exist in `fsm.Fsm.Events` (`FsmEvent.GetFsmEvent(name)`; global ones need `IsGlobal`). Copy the helpers in
  `Assets/Scripts/Gates/Editor/GatePathsMenu.cs` (re-point a transition, clone a state) instead of writing new ones.
- **Variables:** set saved values through `new SerializedObject(fsm)` > `fsm.variables.floatVariables` > `value`, then
  `ApplyModifiedProperties`. `FsmVariables.GetFsmFloat(n).Value = x` inside LoadPrefabContents does NOT save.
- **A new variable plus actions that point at it, in one edit:** give the actions the SAME FsmGameObject (or FsmFloat...)
  instance you added to the Variables array (or `fsm.FsmVariables.GetFsmGameObject(name)` for an existing one).
  Separate instances that only share the name save blank. Renaming a reference in place is not kept: assign a new one.
- **Reading back** a prefab FSM in the same session can show default values even though the file is right: verify by
  diffing the YAML or in Play, not by reading the asset again.
- **Every-frame actions never finish:** a state that leaves only on FINISHED must not hold an `everyFrame` action
  (the 2026-11-05 "no replay after winning" bug).
- **Prefab Mode:** a prefab the user has open in Prefab Mode keeps showing the old copy after a LoadPrefabContents
  edit. If it's that prefab and not dirty: `StageUtility.GoToMainStage()` then `PrefabStageUtility.OpenPrefab(path)`.
- Old menus that rewire GAMESTAGES (e.g. `Gate3PickMenu`, `CauldronDefenceMenu`'s "Use Defend The Cauldron") can undo
  newer flow changes: read a menu before running it.

## Check a change

In Edit or Play mode, list a state's transitions as the open scene has them (unsaved changes included):

```csharp
var fsm = Game.GameStages.Fsm();          // in Edit mode find -GAMESTAGES as above
var sb = new System.Text.StringBuilder();
foreach (var st in fsm.Fsm.States)
{
    if (st.Name != "Path 3 ON" && st.Name != "Rooftop_ON PG") continue;
    sb.Append(st.Name).Append(": ");
    foreach (var t in st.Transitions) sb.Append(t.EventName).Append(" -> ").Append(t.ToState).Append("; ");
    sb.Append(" || ");
}
return sb.ToString();
```

After the user saves, the dump script shows the same on disk. Play-test the path itself with the
`unity-429r-playtest` skill.

## Custom actions

They live in `Assets/PlayMaker Custom Actions/<Category>/` (e.g. Save/CheckpointActions.cs, Player/GiantSmash.cs,
Steam/DemoActions.cs): `[ActionCategory]`, `[Tooltip]` on the class and fields, `Reset()` for defaults, `OnEnter()`
doing the work and calling `Finish()` unless it waits. Keep each one small and named for what the designer sees.
