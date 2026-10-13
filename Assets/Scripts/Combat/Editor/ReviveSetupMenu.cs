using System.Linq;
using Game.Combat;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Menu command that sets up getting back up after dying (Tools > 429 Game > Player).</summary>
    public static class ReviveSetupMenu
    {
        private const string Revive = "Revive";

        /// <summary>
        /// Puts Player Revive on the XR Origin and makes its PlayerDeath FSM start with a choice: "Revive?" (Try Revive)
        /// uses up one of the run's revives and goes to "Not Yet" (Revive Player: "Not yet", the fall backwards, full
        /// health, the wake-up aura), then "Revived" switches PlayerDeath off again; with no revive left it goes on to
        /// "Dying..." as before (the game over). Running it again changes nothing.
        /// </summary>
        [MenuItem("Tools/429 Game/Player/Set Up Revive")]
        public static void SetUp()
        {
            var origin = Object.FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
            var death = origin != null ? origin.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "PlayerDeath") : null;
            if (death == null)
            {
                Debug.LogError("[Revive] Needs the XR Origin with its PlayerDeath FSM in the open scene. Nothing changed.");
                return;
            }

            string reviveNote = "the XR Origin already has Player Revive";
            if (!origin.TryGetComponent<PlayerRevive>(out _))
            {
                Undo.AddComponent<PlayerRevive>(origin.gameObject);
                reviveNote = "added Player Revive to the XR Origin (drop the \"Not yet\" sound into its Not Yet Sound)";
            }

            string fsmNote;
            var f = death.Fsm;
            if (!f.Initialized) f.Init(death);
            foreach (var s in f.States) s.LoadActions();
            if (f.States.Any(s => s.Name == "Revive?"))
            {
                fsmNote = "PlayerDeath already starts with Revive?";
            }
            else
            {
                Undo.RecordObject(death, "Set Up Revive");
                string dying = f.StartState;
                var near = f.States.First(s => s.Name == dying).Position;
                var reviveEvent = FsmEvent.GetFsmEvent(Revive);
                if (!f.Events.Contains(reviveEvent)) f.Events = f.Events.Append(reviveEvent).ToArray();

                var choose = NewState(f, "Revive?", near, -260f, 0f);
                var tryRevive = New<TryRevive>();
                tryRevive.revive = reviveEvent;
                tryRevive.gameOver = FsmEvent.Finished;
                choose.Actions = new FsmStateAction[] { tryRevive };
                choose.Transitions = new[] { Go(Revive, "Not Yet"), Go("FINISHED", dying) };

                var getUp = NewState(f, "Not Yet", near, -260f, 120f);
                getUp.Actions = new FsmStateAction[] { New<RevivePlayer>() };
                getUp.Transitions = new[] { Go("FINISHED", "Revived") };

                // As DEAD does at the end: PlayerDeath off again, so the next death starts it over.
                var done = NewState(f, "Revived", near, -260f, 240f);
                var off = New<EnableFSM>();
                off.gameObject = new FsmOwnerDefault { OwnerOption = OwnerDefaultOption.UseOwner };
                off.fsmName = "PlayerDeath";
                off.enable = false;
                off.resetOnExit = false;
                done.Actions = new FsmStateAction[] { off };
                done.Transitions = new FsmTransition[0];

                f.StartState = "Revive?";
                foreach (var s in f.States) s.SaveActions();
                EditorUtility.SetDirty(death);
                fsmNote = $"PlayerDeath now starts with Revive? (a revive left: Not Yet, Revived; none: {dying})";
            }

            EditorSceneManager.MarkSceneDirty(origin.gameObject.scene);
            Selection.activeGameObject = origin.gameObject;
            Debug.Log($"[Revive] {reviveNote}; {fsmNote}. Save the scene.", origin);
        }

        [MenuItem("Tools/429 Game/Player/Set Up Revive", true)]
        private static bool CanSetUp() => !EditorApplication.isPlaying;

        private static FsmState NewState(Fsm f, string name, Rect near, float x, float y)
        {
            var state = new FsmState(f) { Name = name, Position = new Rect(near.x + x, near.y + y, near.width, near.height) };
            f.States = f.States.Append(state).ToArray();
            return state;
        }

        private static T New<T>() where T : FsmStateAction, new()
        {
            var action = new T();
            action.Reset();
            return action;
        }

        private static FsmTransition Go(string eventName, string toState) =>
            new() { FsmEvent = FsmEvent.GetFsmEvent(eventName), ToState = toState };
    }
}
