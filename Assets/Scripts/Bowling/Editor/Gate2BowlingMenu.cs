using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Bowling.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Bowling > Use Bowling For Gate 2: Gate 2 (Cube_2 on Gate_2) takes the player to the bowling
    /// arena instead of the PlayGround. In GAMESTAGES, "Stage 1 - The Start" sends next2 to three new states:
    /// "Clean for Path 2" (fade out, smoke, as Clean for Path 1), "Path 2 Holder" (a fresh CurrentlySpawnedStuffs) and
    /// "Path 2 ON" (spawns Bowling_Arena at [LevelManager], puts the player in its middle facing lane 0, fades in, and
    /// goes on to "Enemy 4 - Get the Seed" once Bracer_Gear, Chest_Gear, Head_Gear, Shield_Gear and The_Seed are all
    /// true: a copy of Path 1 ON's Bool All True). Gates 1 and 3 still go to the PlayGround.
    /// The saved scene may not have these states yet: running it makes whatever is missing; running it again only
    /// refreshes the arena, the wait (an old The_Seed-only Bool Test is swapped in place) and the next2 target. The open
    /// scene is marked changed, not saved: save it yourself.
    /// "Gate 2 Back To PlayGround" points next2 back at Clean for Path 1 (the new states stay, unused).
    /// </summary>
    public static class Gate2BowlingMenu
    {
        private const string Log = "[Bowling]";
        private const string StagesFsm = "GAMESTAGES";
        private const string ArenaPath = "Assets/Prefabs/Bowling/Bowling_Arena.prefab";

        private const string StartState = "Stage 1 - The Start";
        private const string Clean1 = "Clean for Path 1";
        private const string Holder1 = "Re-Create Stuffs Hodler";
        private const string On1 = "Path 1 ON";
        private const string SeedState = "Enemy 4 - Get the Seed";

        private const string Clean2 = "Clean for Path 2";
        private const string Holder2 = "Path 2 Holder";
        private const string On2 = "Path 2 ON";

        // The player faces lane 0 (world yaw 45) in the arena's middle.
        private const float StartYaw = 45f;

        // What Path 2 ON waits for, as Path 1 ON: the 4 chest seeds' gear and The_Seed.
        private static readonly string[] WaitBools = { "Bracer_Gear", "Chest_Gear", "Head_Gear", "Shield_Gear", "The_Seed" };

        private static readonly MethodInfo CloneMethod =
            typeof(object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>Gate 2 leads to the bowling arena (see the class summary).</summary>
        [MenuItem("Tools/429 Game/Bowling/Use Bowling For Gate 2")]
        public static void UseBowling()
        {
            if (EditorApplication.isPlaying) { Debug.LogError($"{Log} Stop play mode first; nothing changed."); return; }
            var arena = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPath);
            if (arena == null)
            {
                Debug.LogError($"{Log} No {ArenaPath} (run Set Up Bowling Arena first); nothing changed.");
                return;
            }
            var gs = FindStages();
            if (gs == null) return;
            var f = gs.Fsm;

            // Everything it reads from must be there and fully loaded before anything changes.
            var start = f.GetState(StartState);
            var clean1 = Loaded(f, Clean1);
            var holder1 = Loaded(f, Holder1);
            var on1 = Loaded(f, On1);
            if (start == null || clean1 == null || holder1 == null || on1 == null || f.GetState(SeedState) == null)
            {
                Debug.LogError($"{Log} GAMESTAGES is missing '{StartState}', '{Clean1}', '{Holder1}', '{On1}' or '{SeedState}', " +
                               "or one of their actions couldn't be read; nothing changed.", gs);
                return;
            }
            var next2 = start.Transitions.FirstOrDefault(t => t.EventName == "next2");
            var next1 = f.Events.FirstOrDefault(e => e != null && e.Name == "next1");
            if (next2 == null || next1 == null)
            {
                Debug.LogError($"{Log} GAMESTAGES has no next2 transition on '{StartState}' or no next1 event; nothing changed.", gs);
                return;
            }
            var waitBools = WaitBools.Select(n => gs.FsmVariables.FindFsmBool(n)).ToArray();
            if (waitBools.Any(b => b == null))
            {
                Debug.LogError($"{Log} GAMESTAGES is missing a bool variable of {string.Join(", ", WaitBools)}; nothing changed.", gs);
                return;
            }
            var existing = new[] { Clean2, Holder2, On2 }.Select(n => f.GetState(n)).Where(s => s != null).ToList();
            foreach (var s in existing)
            {
                s.LoadActions();
                if (s.ActionData.ActionCount != s.Actions.Length)
                {
                    Debug.LogError($"{Log} GAMESTAGES '{s.Name}' has {s.ActionData.ActionCount} actions saved but only " +
                                   $"{s.Actions.Length} loaded; nothing changed.", gs);
                    return;
                }
            }

            // The source actions to copy, checked before anything changes.
            var fadeOut = clean1.Actions.OfType<HeadsetFade>().FirstOrDefault(a => a.fade == HeadsetFade.Direction.Out);
            var smoke = clean1.Actions.OfType<ActivateGameObject>().FirstOrDefault();
            var cleanWait = clean1.Actions.OfType<Wait>().FirstOrDefault();
            var destroy = holder1.Actions.OfType<DestroyObject>().FirstOrDefault();
            var create = holder1.Actions.OfType<CreateNewGameObject>().FirstOrDefault();
            var holderWait = holder1.Actions.OfType<Wait>().FirstOrDefault();
            var spawn = on1.Actions.OfType<CreateObject>().FirstOrDefault();
            var teleport = on1.Actions.OfType<TeleportPlayer>().FirstOrDefault();
            var fadeIn = on1.Actions.OfType<HeadsetFade>().FirstOrDefault(a => a.fade == HeadsetFade.Direction.In);
            var allTrue = on1.Actions.OfType<BoolAllTrue>().FirstOrDefault();
            if (fadeOut == null || smoke == null || cleanWait == null || destroy == null || create == null || holderWait == null ||
                spawn == null || teleport == null || fadeIn == null || allTrue == null)
            {
                Debug.LogError($"{Log} The Path 1 states don't have the actions to copy (Headset Fade, Activate Game Object, Wait, " +
                               "Destroy Object, Create New Game Object, Create Object, Teleport Player, Bool All True); nothing changed.", gs);
                return;
            }
            var path1Bools = (allTrue.boolVariables ?? new FsmBool[0]).Select(b => b?.Name).ToList();
            if (!WaitBools.All(path1Bools.Contains) || path1Bools.Count != WaitBools.Length)
            {
                Debug.LogWarning($"{Log} '{On1}' waits for [{string.Join(", ", path1Bools)}]; '{On2}' waits for " +
                                 $"[{string.Join(", ", WaitBools)}] anyway.", gs);
            }

            Undo.RecordObject(gs, "Use Bowling For Gate 2");
            var changes = new List<string>();
            var changed = new List<FsmState>();
            var near = clean1.Position;

            // 1. Fade out + smoke, as Clean for Path 1.
            if (f.GetState(Clean2) == null)
            {
                var s = NewState(f, Clean2, near, 60f, Holder2, Clone(fadeOut), Clone(smoke), Clone(cleanWait));
                changed.Add(s);
                changes.Add($"'{Clean2}' made");
            }

            // 2. A fresh CurrentlySpawnedStuffs (the start rooftop goes with the old one). Not its Set Game Object
            //    everyFrame: a no-op that can stall FINISHED.
            if (f.GetState(Holder2) == null)
            {
                var s = NewState(f, Holder2, near, 120f, On2, Clone(destroy), Clone(create), Clone(holderWait));
                changed.Add(s);
                changes.Add($"'{Holder2}' made");
            }

            // 3. The arena, the player in its middle facing lane 0, fade in; on once the 4 gear bools and The_Seed are true.
            var on2 = f.GetState(On2);
            if (on2 == null)
            {
                var arenaSpawn = Clone(spawn);
                arenaSpawn.gameObject = new FsmGameObject { Value = arena };
                var move = Clone(teleport);
                move.position = new FsmVector3 { Value = Vector3.zero };
                move.turn = new FsmBool { Value = true };
                move.yAngle = new FsmFloat { Value = StartYaw };
                on2 = NewState(f, On2, near, 180f, null, arenaSpawn, move, Clone(fadeIn), Wait(allTrue, waitBools, next1));
                on2.Transitions = new[] { new FsmTransition { FsmEvent = next1, ToState = SeedState } };
                changed.Add(on2);
                changes.Add($"'{On2}' made");
            }
            else
            {
                var arenaSpawn = on2.Actions.OfType<CreateObject>().FirstOrDefault();
                if (arenaSpawn == null)
                {
                    Debug.LogWarning($"{Log} '{On2}' has no Create Object; the arena isn't spawned there. Delete the state and run this again.", gs);
                }
                else if (arenaSpawn.gameObject == null || arenaSpawn.gameObject.UseVariable || arenaSpawn.gameObject.Value != arena)
                {
                    arenaSpawn.gameObject = new FsmGameObject { Value = arena };
                    if (!changed.Contains(on2)) changed.Add(on2);
                    changes.Add($"'{On2}' spawns {arena.name}");
                }

                // The wait: the 5 bools (an old The_Seed-only Bool Test is swapped in place).
                var actions = on2.Actions;
                int at = System.Array.FindIndex(actions, a => a is BoolAllTrue || a is BoolTest);
                if (at < 0 || !(actions[at] is BoolAllTrue current && Waits(current, waitBools, next1)))
                {
                    var wait = Wait(allTrue, waitBools, next1);
                    on2.Actions = at < 0 ? actions.Append(wait).ToArray() : actions.Select((a, i) => i == at ? wait : a).ToArray();
                    if (!changed.Contains(on2)) changed.Add(on2);
                    changes.Add($"'{On2}' waits for {string.Join(" + ", WaitBools)}");
                }

                // next1 -> Enemy 4 - Get the Seed.
                var go = on2.Transitions.FirstOrDefault(t => t.EventName == "next1");
                if (go == null)
                {
                    on2.Transitions = on2.Transitions.Append(new FsmTransition { FsmEvent = next1, ToState = SeedState }).ToArray();
                    changes.Add($"'{On2}' next1 -> '{SeedState}'");
                }
                else if (go.ToState != SeedState)
                {
                    go.ToState = SeedState;
                    changes.Add($"'{On2}' next1 -> '{SeedState}'");
                }
            }

            // 4. Gate 2 goes to the arena; gates 1 and 3 are left alone.
            if (next2.ToState != Clean2)
            {
                next2.ToState = Clean2;
                changes.Add($"'{StartState}' next2 -> '{Clean2}'");
            }

            if (changes.Count == 0)
            {
                Debug.Log($"{Log} Gate 2 already leads to the bowling arena.", gs);
                return;
            }
            foreach (var s in changed) s.SaveActions();
            EditorUtility.SetDirty(gs);
            EditorSceneManager.MarkSceneDirty(gs.gameObject.scene);
            Debug.Log($"{Log} Gate 2 -> bowling: {string.Join(", ", changes)}. Save the scene.", gs);
        }

        [MenuItem("Tools/429 Game/Bowling/Use Bowling For Gate 2", true)]
        private static bool CanUseBowling() => !EditorApplication.isPlaying;

        /// <summary>Gate 2 leads to the PlayGround again (next2 -> Clean for Path 1); the bowling states stay.</summary>
        [MenuItem("Tools/429 Game/Bowling/Gate 2 Back To PlayGround")]
        public static void BackToPlayGround()
        {
            if (EditorApplication.isPlaying) { Debug.LogError($"{Log} Stop play mode first; nothing changed."); return; }
            var gs = FindStages();
            if (gs == null) return;
            var f = gs.Fsm;
            var start = f.GetState(StartState);
            var next2 = start?.Transitions.FirstOrDefault(t => t.EventName == "next2");
            if (next2 == null || f.GetState(Clean1) == null)
            {
                Debug.LogError($"{Log} GAMESTAGES has no next2 transition on '{StartState}' or no '{Clean1}'; nothing changed.", gs);
                return;
            }
            if (next2.ToState == Clean1)
            {
                Debug.Log($"{Log} Gate 2 already leads to the PlayGround.", gs);
                return;
            }
            Undo.RecordObject(gs, "Gate 2 Back To PlayGround");
            next2.ToState = Clean1;
            EditorUtility.SetDirty(gs);
            EditorSceneManager.MarkSceneDirty(gs.gameObject.scene);
            Debug.Log($"{Log} '{StartState}' next2 -> '{Clean1}' (the PlayGround). Save the scene.", gs);
        }

        [MenuItem("Tools/429 Game/Bowling/Gate 2 Back To PlayGround", true)]
        private static bool CanGoBack() => !EditorApplication.isPlaying;

        // The GAMESTAGES FSM in the open scene, set up for editing.
        private static PlayMakerFSM FindStages()
        {
            var gs = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(f => f.FsmName == StagesFsm && f.gameObject.scene.IsValid());
            if (gs == null)
            {
                Debug.LogError($"{Log} No GAMESTAGES FSM in the open scene (open 2 Game Scene).");
                return null;
            }
            if (!gs.Fsm.Initialized) gs.Fsm.Init(gs);
            return gs;
        }

        // The state with its actions loaded, or null if it's missing or an action couldn't be read (saving would drop it).
        private static FsmState Loaded(Fsm f, string name)
        {
            var state = f.GetState(name);
            if (state == null) return null;
            state.LoadActions();
            return state.ActionData.ActionCount == state.Actions.Length ? state : null;
        }

        private static FsmState NewState(Fsm f, string name, Rect near, float down, string finishedTo, params FsmStateAction[] actions)
        {
            var state = new FsmState(f)
            {
                Name = name,
                Position = new Rect(near.x, near.y + down, near.width, near.height),
                Actions = actions,
                Transitions = finishedTo != null
                    ? new[] { new FsmTransition { FsmEvent = FsmEvent.Finished, ToState = finishedTo } }
                    : new FsmTransition[0],
            };
            f.States = f.States.Append(state).ToArray();
            return state;
        }

        // Path 1 ON's Bool All True, copied: these bools, every frame, sends next1 when all are true.
        private static BoolAllTrue Wait(BoolAllTrue source, FsmBool[] bools, FsmEvent next1)
        {
            var wait = Clone(source);
            wait.boolVariables = (FsmBool[])bools.Clone();
            wait.sendEvent = next1;
            wait.everyFrame = true;
            return wait;
        }

        // True when this Bool All True already waits for exactly these bools, every frame, then sends next1.
        private static bool Waits(BoolAllTrue wait, FsmBool[] bools, FsmEvent next1)
        {
            if (wait.boolVariables == null || wait.boolVariables.Length != bools.Length || !wait.everyFrame) return false;
            if (wait.sendEvent == null || wait.sendEvent.Name != next1.Name) return false;
            var names = wait.boolVariables.Select(b => b?.Name).ToList();
            return bools.All(b => names.Contains(b.Name));
        }

        // A copy of an action with the same settings (fields changed later get new objects, so the original stays as is).
        private static T Clone<T>(T action) where T : FsmStateAction => (T)CloneMethod.Invoke(action, null);
    }
}
