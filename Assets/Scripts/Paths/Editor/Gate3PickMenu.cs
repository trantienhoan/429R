using System.Linq;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Paths > Gate 3 Picks Gate 1 Or 2 (user, 2026-11-12: "Gate 3, as for now, just let Player get
    /// in gate 1 or 2"): gate 3 has no stage of its own yet, so walking into it sends the player into gate 1's stage
    /// (the PlayGround) or gate 2's (the bowling arena), half and half. In GAMESTAGES, "Stage 1 - The Start" sends next3
    /// to a new state "Gate 3 Pick", whose Send Random Event sends next1 (-> where gate 1 goes) or next2 (-> where
    /// gate 2 goes). Running it again only re-points those. The open scene is marked changed, not saved: save it.
    /// "Gate 3 Back To Gate 1" points next3 straight at gate 1's stage again (as before; the pick state stays, unused).
    /// </summary>
    public static class Gate3PickMenu
    {
        private const string Log = "[Gates]";
        private const string StagesFsm = "GAMESTAGES";
        private const string StartState = "Stage 1 - The Start";
        private const string PickState = "Gate 3 Pick";

        [MenuItem("Tools/429 Game/Paths/Gate 3 Picks Gate 1 Or 2")]
        public static void PickOneOrTwo()
        {
            if (EditorApplication.isPlaying) { Debug.LogError($"{Log} Stop play mode first; nothing changed."); return; }
            var gs = FindStages();
            if (gs == null) return;
            var f = gs.Fsm;
            var start = f.GetState(StartState);
            var go1 = start?.Transitions.FirstOrDefault(t => t.EventName == "next1");
            var go2 = start?.Transitions.FirstOrDefault(t => t.EventName == "next2");
            var go3 = start?.Transitions.FirstOrDefault(t => t.EventName == "next3");
            var next1 = f.Events.FirstOrDefault(e => e != null && e.Name == "next1");
            var next2 = f.Events.FirstOrDefault(e => e != null && e.Name == "next2");
            if (go1 == null || go2 == null || go3 == null || next1 == null || next2 == null ||
                f.GetState(go1.ToState) == null || f.GetState(go2.ToState) == null)
            {
                Debug.LogError($"{Log} GAMESTAGES '{StartState}' needs next1, next2 and next3 transitions to real states; nothing changed.", gs);
                return;
            }
            if (go1.ToState == PickState || go2.ToState == PickState)
            {
                Debug.LogError($"{Log} Gate 1 or 2 points at '{PickState}' (it would pick itself); nothing changed.", gs);
                return;
            }

            Undo.RecordObject(gs, "Gate 3 Picks Gate 1 Or 2");
            var changes = new System.Collections.Generic.List<string>();
            var pick = f.GetState(PickState);
            if (pick == null)
            {
                var near = start.Position;
                var random = new SendRandomEvent
                {
                    events = new[] { next1, next2 },
                    weights = new[] { new FsmFloat { Value = 1f }, new FsmFloat { Value = 1f } },
                    delay = new FsmFloat { Value = 0f },
                };
                pick = new FsmState(f)
                {
                    Name = PickState,
                    Position = new Rect(near.x + near.width + 60f, near.y, near.width, near.height),
                    Actions = new FsmStateAction[] { random },
                    Transitions = new FsmTransition[0],
                };
                f.States = f.States.Append(pick).ToArray();
                pick.SaveActions();
                changes.Add($"'{PickState}' made (Send Random Event: next1 or next2, half and half)");
            }
            changes.AddRange(PointAt(pick, next1, go1.ToState));
            changes.AddRange(PointAt(pick, next2, go2.ToState));
            if (go3.ToState != PickState)
            {
                go3.ToState = PickState;
                changes.Add($"'{StartState}' next3 -> '{PickState}'");
            }

            if (changes.Count == 0)
            {
                Debug.Log($"{Log} Gate 3 already picks gate 1 ('{go1.ToState}') or gate 2 ('{go2.ToState}').", gs);
                return;
            }
            EditorUtility.SetDirty(gs);
            EditorSceneManager.MarkSceneDirty(gs.gameObject.scene);
            Debug.Log($"{Log} Gate 3 -> gate 1 or 2: {string.Join(", ", changes)}. Save the scene.", gs);
        }

        [MenuItem("Tools/429 Game/Paths/Gate 3 Picks Gate 1 Or 2", true)]
        private static bool CanPick() => !EditorApplication.isPlaying;

        /// <summary>Gate 3 goes straight to gate 1's stage again (next3 -> where next1 goes); the pick state stays.</summary>
        [MenuItem("Tools/429 Game/Paths/Gate 3 Back To Gate 1")]
        public static void BackToGateOne()
        {
            if (EditorApplication.isPlaying) { Debug.LogError($"{Log} Stop play mode first; nothing changed."); return; }
            var gs = FindStages();
            if (gs == null) return;
            var start = gs.Fsm.GetState(StartState);
            var go1 = start?.Transitions.FirstOrDefault(t => t.EventName == "next1");
            var go3 = start?.Transitions.FirstOrDefault(t => t.EventName == "next3");
            if (go1 == null || go3 == null)
            {
                Debug.LogError($"{Log} GAMESTAGES '{StartState}' has no next1 or next3 transition; nothing changed.", gs);
                return;
            }
            if (go3.ToState == go1.ToState)
            {
                Debug.Log($"{Log} Gate 3 already goes to gate 1's stage ('{go1.ToState}').", gs);
                return;
            }
            Undo.RecordObject(gs, "Gate 3 Back To Gate 1");
            go3.ToState = go1.ToState;
            EditorUtility.SetDirty(gs);
            EditorSceneManager.MarkSceneDirty(gs.gameObject.scene);
            Debug.Log($"{Log} '{StartState}' next3 -> '{go1.ToState}' (gate 1's stage). Save the scene.", gs);
        }

        [MenuItem("Tools/429 Game/Paths/Gate 3 Back To Gate 1", true)]
        private static bool CanGoBack() => !EditorApplication.isPlaying;

        // The pick state's transition on this event goes to this state (made or re-pointed).
        private static System.Collections.Generic.IEnumerable<string> PointAt(FsmState pick, FsmEvent e, string to)
        {
            var t = pick.Transitions.FirstOrDefault(x => x.EventName == e.Name);
            if (t == null)
            {
                pick.Transitions = pick.Transitions.Append(new FsmTransition { FsmEvent = e, ToState = to }).ToArray();
                yield return $"'{PickState}' {e.Name} -> '{to}'";
            }
            else if (t.ToState != to)
            {
                t.ToState = to;
                yield return $"'{PickState}' {e.Name} -> '{to}'";
            }
        }

        // The GAMESTAGES FSM in the open scene, set up for editing.
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
    }
}
