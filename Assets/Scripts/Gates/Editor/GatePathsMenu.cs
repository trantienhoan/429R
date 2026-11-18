using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Gates.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Gates > Gate Paths: Ball Valley + Tree Defence (user, 2026-11-18: every gate ends like gate 3:
    /// break the giant pumpkin, its seed takes you to defend the tree on the rooftop, like the main game). In GAMESTAGES:
    /// - Gate 3 gets its own stage: "Stage 1 - The Start" sends next3 to new states made from gate 2's: "Clean for Path 3"
    ///   (fade out, smoke) -> "Demo? Path 3" (a demo build ends there) -> "Path 3 Holder" (a fresh CurrentlySpawnedStuffs)
    ///   -> "Path 3 ON" (spawns Ball_Valley at [LevelManager], the player at its middle, fade in; on to "Enemy 4 - Get the
    ///   Seed" once the 4 gear bools and The_Seed are true, as Path 2 ON). The old "Gate 3 Pick" stays, unused.
    /// - Every gate ends in the tree defence: "Rooftop_ON PG" (where gates 2 and 3 go after their seed) goes on to
    ///   "Prepare_Whack_6" (Whack 6 and 7 with the tree, then the Lid and the boss, as in the main game) instead of
    ///   straight to the Lid ("CP Boss Lid"); gate 1's "Path 1 ON" next1 goes to "Tween PlayGroundZ" (the same way out)
    ///   instead of "Tween PlayGroundZ To Boss".
    /// Running it again only refreshes what's off. The open scene is marked changed, not saved: SAVE IT.
    /// "Gate Paths: Back As Before" undoes the re-pointing (next3 -> Gate 3 Pick, Rooftop_ON PG -> CP Boss Lid, Path 1 ON
    /// next1 -> Tween PlayGroundZ To Boss); the new states stay, unused.
    /// </summary>
    public static class GatePathsMenu
    {
        private const string Log = "[Gates]";
        private const string StagesFsm = "GAMESTAGES";
        private const string ValleyPath = "Assets/Prefabs/BallValley/Ball_Valley.prefab";

        private const string StartState = "Stage 1 - The Start";
        private const string Clean2 = "Clean for Path 2";
        private const string Demo2 = "Demo? Path 2";
        private const string Holder2 = "Path 2 Holder";
        private const string On2 = "Path 2 ON";
        private const string Clean3 = "Clean for Path 3";
        private const string Demo3 = "Demo? Path 3";
        private const string Holder3 = "Path 3 Holder";
        private const string On3 = "Path 3 ON";
        private const string Pick3 = "Gate 3 Pick";
        private const string SeedState = "Enemy 4 - Get the Seed";
        private const string RoofPG = "Rooftop_ON PG";
        private const string LidState = "CP Boss Lid";
        private const string Whack6 = "Prepare_Whack_6";
        private const string On1 = "Path 1 ON";
        private const string Tween1Boss = "Tween PlayGroundZ To Boss";
        private const string TweenOut = "Tween PlayGroundZ";

        private static readonly MethodInfo CloneMethod =
            typeof(object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance);

        [MenuItem("Tools/429 Game/Gates/Gate Paths: Ball Valley + Tree Defence")]
        public static void SetUp()
        {
            if (EditorApplication.isPlaying) { Debug.LogError($"{Log} Stop play mode first; nothing changed."); return; }
            var valley = AssetDatabase.LoadAssetAtPath<GameObject>(ValleyPath);
            if (valley == null)
            {
                Debug.LogError($"{Log} No {ValleyPath} (run Tools > 429 Game > Ball Valley > Set Up Ball Valley first); nothing changed.");
                return;
            }
            var gs = FindStages();
            if (gs == null) return;
            var f = gs.Fsm;

            // Everything read from must be there and fully loaded before anything changes.
            var start = f.GetState(StartState);
            var clean2 = Loaded(f, Clean2);
            var demo2 = Loaded(f, Demo2);
            var holder2 = Loaded(f, Holder2);
            var on2 = Loaded(f, On2);
            var roofPG = f.GetState(RoofPG);
            var on1 = f.GetState(On1);
            string[] needed = { SeedState, Whack6, TweenOut };
            if (start == null || clean2 == null || holder2 == null || on2 == null || roofPG == null || on1 == null ||
                needed.Any(n => f.GetState(n) == null))
            {
                Debug.LogError($"{Log} GAMESTAGES is missing one of '{StartState}', '{Clean2}', '{Holder2}', '{On2}', '{RoofPG}', " +
                               $"'{On1}', '{SeedState}', '{Whack6}', '{TweenOut}' (or an action of theirs couldn't be read); nothing changed.", gs);
                return;
            }
            var next3 = start.Transitions.FirstOrDefault(t => t.EventName == "next3");
            var roofGo = roofPG.Transitions.FirstOrDefault(t => t.EventName == "FINISHED");
            var on1Go = on1.Transitions.FirstOrDefault(t => t.EventName == "next1");
            if (next3 == null || roofGo == null || on1Go == null)
            {
                Debug.LogError($"{Log} GAMESTAGES needs '{StartState}' next3, '{RoofPG}' FINISHED and '{On1}' next1; nothing changed.", gs);
                return;
            }
            foreach (var s in new[] { Clean3, Demo3, Holder3, On3 }.Select(n => f.GetState(n)).Where(s => s != null))
            {
                s.LoadActions();
                if (s.ActionData.ActionCount != s.Actions.Length)
                {
                    Debug.LogError($"{Log} GAMESTAGES '{s.Name}' has {s.ActionData.ActionCount} actions saved but only {s.Actions.Length} loaded; nothing changed.", gs);
                    return;
                }
            }
            var spawn2 = on2.Actions.OfType<CreateObject>().FirstOrDefault();
            var teleport2 = on2.Actions.OfType<TeleportPlayer>().FirstOrDefault();
            if (spawn2 == null || teleport2 == null)
            {
                Debug.LogError($"{Log} '{On2}' has no Create Object or Teleport Player to copy; nothing changed.", gs);
                return;
            }

            Undo.RecordObject(gs, "Gate Paths: Ball Valley + Tree Defence");
            var changes = new List<string>();
            var changed = new List<FsmState>();
            var near = clean2.Position;

            // 1. Gate 3's own stage, made from gate 2's states.
            if (f.GetState(Clean3) == null)
            {
                changed.Add(Copy(f, clean2, Clean3, near, 300f, map: new Dictionary<string, string> { { Demo2, Demo3 }, { Holder2, Holder3 } }));
                changes.Add($"'{Clean3}' made");
            }
            if (demo2 != null && f.GetState(Demo3) == null)
            {
                changed.Add(Copy(f, demo2, Demo3, near, 360f, map: new Dictionary<string, string> { { Holder2, Holder3 } }));
                changes.Add($"'{Demo3}' made");
            }
            if (f.GetState(Holder3) == null)
            {
                changed.Add(Copy(f, holder2, Holder3, near, 420f, map: new Dictionary<string, string> { { On2, On3 } }));
                changes.Add($"'{Holder3}' made");
            }
            var on3 = f.GetState(On3);
            if (on3 == null)
            {
                on3 = Copy(f, on2, On3, near, 480f, map: null);
                changed.Add(on3);
                changes.Add($"'{On3}' made");
            }
            // Its spawn is the valley, the player at its middle facing +Z.
            var spawn3 = on3.Actions.OfType<CreateObject>().FirstOrDefault();
            if (spawn3 != null && (spawn3.gameObject == null || spawn3.gameObject.UseVariable || spawn3.gameObject.Value != valley))
            {
                spawn3.gameObject = new FsmGameObject { Value = valley };
                if (!changed.Contains(on3)) changed.Add(on3);
                changes.Add($"'{On3}' spawns {valley.name}");
            }
            var teleport3 = on3.Actions.OfType<TeleportPlayer>().FirstOrDefault();
            if (teleport3 != null && (teleport3.yAngle == null || !Mathf.Approximately(teleport3.yAngle.Value, 0f)))
            {
                teleport3.turn = new FsmBool { Value = true };
                teleport3.yAngle = new FsmFloat { Value = 0f };
                if (!changed.Contains(on3)) changed.Add(on3);
                changes.Add($"'{On3}' faces +Z");
            }
            // The clean state's FINISHED goes to the demo check (or straight to the holder without one).
            var clean3 = f.GetState(Clean3);
            Point(clean3, "FINISHED", f.GetState(Demo3) != null ? Demo3 : Holder3, changes);
            if (f.GetState(Demo3) != null) Point(f.GetState(Demo3), "FINISHED", Holder3, changes);
            Point(f.GetState(Holder3), "FINISHED", On3, changes);
            Point(on3, "next1", SeedState, changes);
            if (next3.ToState != Clean3)
            {
                changes.Add($"'{StartState}' next3 -> '{Clean3}' (was '{next3.ToState}')");
                next3.ToState = Clean3;
            }

            // 2. Every gate ends in the tree defence.
            if (roofGo.ToState != Whack6)
            {
                changes.Add($"'{RoofPG}' -> '{Whack6}' (was '{roofGo.ToState}')");
                roofGo.ToState = Whack6;
            }
            if (on1Go.ToState != TweenOut)
            {
                changes.Add($"'{On1}' next1 -> '{TweenOut}' (was '{on1Go.ToState}')");
                on1Go.ToState = TweenOut;
            }

            if (changes.Count == 0)
            {
                Debug.Log($"{Log} Gate paths already set: gate 3 -> Ball Valley, every gate -> the tree defence.", gs);
                return;
            }
            foreach (var s in changed) s.SaveActions();
            EditorUtility.SetDirty(gs);
            EditorSceneManager.MarkSceneDirty(gs.gameObject.scene);
            Debug.Log($"{Log} Gate paths: {string.Join("; ", changes)}. SAVE THE SCENE.", gs);
        }

        [MenuItem("Tools/429 Game/Gates/Gate Paths: Ball Valley + Tree Defence", true)]
        private static bool CanSetUp() => !EditorApplication.isPlaying;

        [MenuItem("Tools/429 Game/Gates/Gate Paths: Back As Before")]
        public static void BackAsBefore()
        {
            if (EditorApplication.isPlaying) { Debug.LogError($"{Log} Stop play mode first; nothing changed."); return; }
            var gs = FindStages();
            if (gs == null) return;
            var f = gs.Fsm;
            Undo.RecordObject(gs, "Gate Paths: Back As Before");
            var changes = new List<string>();
            var next3 = f.GetState(StartState)?.Transitions.FirstOrDefault(t => t.EventName == "next3");
            if (next3 != null && f.GetState(Pick3) != null && next3.ToState != Pick3)
            {
                next3.ToState = Pick3;
                changes.Add($"'{StartState}' next3 -> '{Pick3}'");
            }
            var roofGo = f.GetState(RoofPG)?.Transitions.FirstOrDefault(t => t.EventName == "FINISHED");
            if (roofGo != null && f.GetState(LidState) != null && roofGo.ToState != LidState)
            {
                roofGo.ToState = LidState;
                changes.Add($"'{RoofPG}' -> '{LidState}'");
            }
            var on1Go = f.GetState(On1)?.Transitions.FirstOrDefault(t => t.EventName == "next1");
            if (on1Go != null && f.GetState(Tween1Boss) != null && on1Go.ToState != Tween1Boss)
            {
                on1Go.ToState = Tween1Boss;
                changes.Add($"'{On1}' next1 -> '{Tween1Boss}'");
            }
            if (changes.Count == 0)
            {
                Debug.Log($"{Log} Gate paths are already as before.", gs);
                return;
            }
            EditorUtility.SetDirty(gs);
            EditorSceneManager.MarkSceneDirty(gs.gameObject.scene);
            Debug.Log($"{Log} Gate paths back: {string.Join("; ", changes)}. SAVE THE SCENE.", gs);
        }

        [MenuItem("Tools/429 Game/Gates/Gate Paths: Back As Before", true)]
        private static bool CanGoBack() => !EditorApplication.isPlaying;

        // The state's transition on this event goes to this state (made or re-pointed).
        private static void Point(FsmState state, string eventName, string to, List<string> changes)
        {
            if (state == null) return;
            var t = state.Transitions.FirstOrDefault(x => x.EventName == eventName);
            if (t == null)
            {
                var e = eventName == "FINISHED" ? FsmEvent.Finished : FsmEvent.GetFsmEvent(eventName);
                state.Transitions = state.Transitions.Append(new FsmTransition { FsmEvent = e, ToState = to }).ToArray();
                changes.Add($"'{state.Name}' {eventName} -> '{to}'");
            }
            else if (t.ToState != to)
            {
                t.ToState = to;
                changes.Add($"'{state.Name}' {eventName} -> '{to}'");
            }
        }

        // A new state with copies of Source's actions and its transitions (targets renamed through Map).
        private static FsmState Copy(Fsm f, FsmState source, string name, Rect near, float down, Dictionary<string, string> map)
        {
            var state = new FsmState(f)
            {
                Name = name,
                Position = new Rect(near.x + 260f, near.y + down, near.width, near.height),
                Actions = source.Actions.Select(Clone).ToArray(),
                Transitions = source.Transitions.Select(t => new FsmTransition
                {
                    FsmEvent = t.FsmEvent,
                    ToState = map != null && map.TryGetValue(t.ToState, out var to) ? to : t.ToState,
                }).ToArray(),
            };
            f.States = f.States.Append(state).ToArray();
            return state;
        }

        private static FsmStateAction Clone(FsmStateAction action) => (FsmStateAction)CloneMethod.Invoke(action, null);

        private static PlayMakerFSM FindStages()
        {
            var gs = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(x => x.FsmName == StagesFsm && x.gameObject.scene.IsValid());
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
    }
}
