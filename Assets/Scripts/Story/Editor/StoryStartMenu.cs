using System.Collections.Generic;
using System.Linq;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Story > Set Up Story Start (user 2026-11-25: the start rooftop three ways: the first meeting in
    /// the dark, then the empty jail until the final boss is beaten, then the free Fairy with her board; see
    /// Scripts/Story/StoryStart.cs). Wires GAMESTAGES:
    /// - "Move to Fairy": Story Start Meeting FIRST (Knight Point = Knight_pos under Fairy_Pos, Seed = Seed_Bracer, Freed
    ///   Event = "freed") and a new transition freed -> "Stage 1 - The Start";
    /// - "Stage 1 - The Start": Story Start The Start right after the start pumpkin's point is switched on (Start Pumpkin
    ///   Point = Start_Pumpkin_Pos, Walk Signs = Walk Tutorial Signs);
    /// - "Thank For Playing": Story Remember (Beat The Boss) FIRST;
    /// - "Walk Tuttorial"'s Story Darkness (Set Up Dark Walk): Light Up Seconds 6 for later runs.
    /// Re-runnable; "Undo Story Start" takes it all out again (the dark walk stays: Undo Dark Walk). Changes the scene: save
    /// it after.
    /// </summary>
    public static class StoryStartMenu
    {
        private const string Tag = "[Story Start]";
        private const string SeedPath = "Assets/Prefabs/Items/Chest/Seed_Bracer.prefab";
        private const string Meet = "Move to Fairy";
        private const string Start = "Stage 1 - The Start";
        private const string Thanks = "Thank For Playing";
        private const string Walk = "Walk Tuttorial";
        private const string Freed = "freed";

        [MenuItem("Tools/429 Game/Story/Set Up Story Start")]
        public static void SetUp() => Run(false);

        [MenuItem("Tools/429 Game/Story/Undo Story Start")]
        public static void UndoStoryStart() => Run(true);

        [MenuItem("Tools/429 Game/Story/Set Up Story Start", true)]
        [MenuItem("Tools/429 Game/Story/Undo Story Start", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static void Run(bool undo)
        {
            var stages = FindStages();
            if (stages == null) return;
            var f = stages.Fsm;
            var done = new List<string>();

            var meet = Loaded(f, Meet);
            var start = Loaded(f, Start);
            var thanks = Loaded(f, Thanks);
            var walk = Loaded(f, Walk);
            if (meet == null || start == null || thanks == null)
            {
                Debug.LogError($"{Tag} GAMESTAGES is missing '{Meet}', '{Start}' or '{Thanks}' (or an action there can't be read); nothing changed.");
                return;
            }

            if (undo)
            {
                if (Remove<StoryStartMeeting>(meet)) done.Add($"Story Start Meeting out of '{Meet}'");
                if (Remove<StoryStartTheStart>(start)) done.Add($"Story Start The Start out of '{Start}'");
                if (Remove<StoryRemember>(thanks)) done.Add($"Story Remember out of '{Thanks}'");
                int before = meet.Transitions.Length;
                meet.Transitions = meet.Transitions.Where(t => t.EventName != Freed).ToArray();
                if (meet.Transitions.Length != before) done.Add($"'{Meet}' {Freed} transition removed");
            }
            else
            {
                var knight = FindChild("Fairy_Pos", "Knight_pos");
                var pumpkin = FindSceneObject("Start_Pumpkin_Pos");
                var signs = FindSceneObject("Walk Tutorial Signs");
                var seed = AssetDatabase.LoadAssetAtPath<GameObject>(SeedPath);
                if (knight == null || pumpkin == null || seed == null)
                {
                    Debug.LogError($"{Tag} Missing Knight_pos (under Fairy_Pos), Start_Pumpkin_Pos or {SeedPath}; nothing changed.");
                    return;
                }
                var freed = FsmEvent.GetFsmEvent(Freed);
                if (!f.Events.Contains(freed)) f.Events = f.Events.Append(freed).ToArray();

                if (!meet.Actions.OfType<StoryStartMeeting>().Any())
                {
                    var a = new StoryStartMeeting();
                    a.Reset();
                    a.knightPoint = new FsmGameObject { Value = knight };
                    a.seed = new FsmGameObject { Value = seed };
                    a.freedEvent = freed;
                    meet.Actions = new FsmStateAction[] { a }.Concat(meet.Actions).ToArray();
                    meet.SaveActions();
                    done.Add($"Story Start Meeting first in '{Meet}'");
                }
                if (!meet.Transitions.Any(t => t.EventName == Freed))
                {
                    meet.Transitions = meet.Transitions.Append(new FsmTransition { FsmEvent = freed, ToState = Start }).ToArray();
                    done.Add($"'{Meet}' {Freed} -> '{Start}'");
                }
                if (!start.Actions.OfType<StoryStartTheStart>().Any())
                {
                    var a = new StoryStartTheStart();
                    a.Reset();
                    a.startPumpkinPoint = new FsmGameObject { Value = pumpkin };
                    a.walkSigns = new FsmGameObject { Value = signs };
                    // Right after the start pumpkin's point is switched on.
                    int at = System.Array.FindIndex(start.Actions, x => x is ActivateGameObject act &&
                        act.gameObject != null && act.gameObject.GameObject != null && act.gameObject.GameObject.Value == pumpkin);
                    var list = start.Actions.ToList();
                    list.Insert(at >= 0 ? at + 1 : 0, a);
                    start.Actions = list.ToArray();
                    start.SaveActions();
                    done.Add($"Story Start The Start in '{Start}' (after the pumpkin point: {at >= 0})");
                }
                if (!thanks.Actions.OfType<StoryRemember>().Any())
                {
                    var a = new StoryRemember();
                    a.Reset();
                    a.what = StoryRemember.What.BeatTheBoss;
                    thanks.Actions = new FsmStateAction[] { a }.Concat(thanks.Actions).ToArray();
                    thanks.SaveActions();
                    done.Add($"Story Remember (Beat The Boss) first in '{Thanks}'");
                }
                if (walk != null)
                {
                    foreach (var dark in walk.Actions.OfType<StoryDarkness>())
                    {
                        if (dark.mode != StoryDarkness.Mode.BeginDark || (dark.lightUpSeconds != null && !dark.lightUpSeconds.IsNone && dark.lightUpSeconds.Value > 0f)) continue;
                        dark.lightUpSeconds = 6f;
                        walk.SaveActions();
                        done.Add($"'{Walk}' Story Darkness Light Up Seconds 6");
                    }
                }
            }
            if (done.Count == 0)
            {
                Debug.Log($"{Tag} Nothing to change (already {(undo ? "undone" : "set up")}).");
                return;
            }
            EditorUtility.SetDirty(stages);
            EditorSceneManager.MarkSceneDirty(stages.gameObject.scene);
            Debug.Log($"{Tag} {string.Join("; ", done)}. SAVE 2 Game Scene.");
        }

        private static bool Remove<T>(FsmState state) where T : FsmStateAction
        {
            if (!state.Actions.OfType<T>().Any()) return false;
            state.Actions = state.Actions.Where(a => !(a is T)).ToArray();
            state.SaveActions();
            return true;
        }

        private static PlayMakerFSM FindStages()
        {
            foreach (var fsm in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
            {
                if (!fsm.gameObject.scene.IsValid() || fsm.FsmName != "GAMESTAGES") continue;
                if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
                return fsm;
            }
            Debug.LogError(Tag + " No GAMESTAGES in the open scene: open 2 Game Scene and run it again.");
            return null;
        }

        // The state with its actions loaded, or null if it's missing or an action couldn't be read (saving would drop it).
        private static FsmState Loaded(Fsm f, string name)
        {
            var state = f.GetState(name);
            if (state == null) return null;
            state.LoadActions();
            return state.ActionData.ActionCount == state.Actions.Length ? state : null;
        }

        private static GameObject FindSceneObject(string name)
        {
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                if (go.name == name && go.scene.IsValid() && go.scene.isLoaded) return go;
            return null;
        }

        private static GameObject FindChild(string parent, string name)
        {
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                if (go.name == name && go.scene.IsValid() && go.scene.isLoaded && go.transform.parent != null && go.transform.parent.name == parent)
                    return go;
            return null;
        }
    }
}
