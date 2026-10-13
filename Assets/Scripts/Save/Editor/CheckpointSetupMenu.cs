using System.Linq;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Adds checkpoints to GAMESTAGES (Tools > 429 Game > Stages). Four stage starts become checkpoints (Set Checkpoint):
    /// the Bedroom ("2 - The BR"), the Living Room ("3 - LivingRoom"), the Rooftop whack ("Prepare_Whack_6") and the
    /// boss fight ("Damage The Lid": after the whack, where the tree of light grows). After dying, Recreate goes to
    /// "Checkpoint?", which sends the checkpoint's name (one revive a run) or FINISHED (start over: Walk Tuttorial). As
    /// the game starts, Init - Stage1 goes to "Resume?", which continues at the saved checkpoint. Each checkpoint has a
    /// "CP ..." state that rebuilds what Game Over cleared (the sky, the room) and then joins the normal chain. Beating
    /// the game starts a new run ("clean all"). Running it again leaves what's already there.
    /// </summary>
    public static class CheckpointSetupMenu
    {
        private static readonly (string checkpoint, string stage)[] Checkpoints =
        {
            ("CP Bedroom", "2 - The BR"),
            ("CP LivingRoom", "3 - LivingRoom"),
            ("CP Rooftop", "Prepare_Whack_6"),
            ("CP Boss", "Damage The Lid"),
        };

        [MenuItem("Tools/429 Game/Stages/Set Up Checkpoints")]
        public static void SetUp()
        {
            PlayMakerFSM fsm = null;
            foreach (var candidate in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.FsmName == "GAMESTAGES") fsm = candidate;
            }
            if (fsm == null)
            {
                Debug.LogError("[Checkpoints] No GAMESTAGES FSM in the open scene (open 2 Game Scene).");
                return;
            }
            var skydome = Find("Skydome_Base_Pos");
            var rooftop = Find("Rooftop_Base_Pos");
            if (skydome == null || rooftop == null)
            {
                Debug.LogError("[Checkpoints] Couldn't find Room_Bases/Skydome_Base_Pos and Rooftop_Base_Pos.");
                return;
            }

            if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
            var f = fsm.Fsm;
            foreach (var s in f.States) s.LoadActions();
            foreach (var (_, stage) in Checkpoints.Concat(new[] { ("", "Init - Stage1"), ("", "Recreate"), ("", "clean all"), ("", "Walk Tuttorial"), ("", "Rooftop_ON") }))
            {
                if (f.States.All(s => s.Name != stage))
                {
                    Debug.LogError($"[Checkpoints] GAMESTAGES has no state '{stage}'; nothing changed.");
                    return;
                }
            }

            // The checkpoints' names are events, for the transitions that go to them.
            foreach (var (checkpoint, _) in Checkpoints)
            {
                var e = FsmEvent.GetFsmEvent(checkpoint);
                if (!f.Events.Contains(e)) f.Events = f.Events.Append(e).ToArray();
            }

            // 1. Reaching a stage start sets its checkpoint.
            foreach (var (checkpoint, stage) in Checkpoints)
            {
                var state = State(f, stage);
                if (state.Actions.OfType<SetCheckpoint>().Any()) continue;
                var set = New<SetCheckpoint>();
                set.checkpoint = checkpoint;
                state.Actions = new FsmStateAction[] { set }.Concat(state.Actions).ToArray();
            }

            // 2. Beating the game starts a new run.
            var cleanAll = State(f, "clean all");
            if (!cleanAll.Actions.OfType<StartNewRun>().Any())
                cleanAll.Actions = new FsmStateAction[] { New<StartNewRun>() }.Concat(cleanAll.Actions).ToArray();

            // 3. Rebuilding each checkpoint's stage after Game Over cleared everything, then the normal chain.
            var basePos = State(f, "Recreate").Position;
            var cpBedroom = NewState(f, "CP Bedroom", basePos, 260f, 120f);
            cpBedroom.Actions = new FsmStateAction[] { Activate(skydome), Wait(0.3f) };
            cpBedroom.Transitions = new[] { Go("FINISHED", "2 - The BR") };

            var cpLivingRoom = NewState(f, "CP LivingRoom", basePos, 260f, 180f);
            cpLivingRoom.Actions = new FsmStateAction[] { Activate(skydome), Wait(0.3f) };
            cpLivingRoom.Transitions = new[] { Go("FINISHED", "3 - LivingRoom") };

            var cpRooftop = NewState(f, "CP Rooftop", basePos, 260f, 240f);
            cpRooftop.Actions = new FsmStateAction[] { Activate(skydome), Wait(0.3f) };
            cpRooftop.Transitions = new[] { Go("FINISHED", "Rooftop_ON") };

            // The boss: the rooftop again, its lid broken at once (as The Seed does after the whack), so the tree of
            // light grows and the song plays; then on as after the whack.
            var cpBoss = NewState(f, "CP Boss", basePos, 260f, 300f);
            cpBoss.Actions = new FsmStateAction[] { Activate(skydome), Activate(rooftop), Wait(2.1f) };
            cpBoss.Transitions = new[] { Go("FINISHED", "CP Boss Lid") };
            var cpBossLid = NewState(f, "CP Boss Lid", basePos, 520f, 300f);
            var gateOn = New<SetFsmBool>();
            gateOn.gameObject = Global("Rooftop_Base");
            gateOn.fsmName = "Info";
            gateOn.variableName = "Gate_On";
            gateOn.setValue = true;
            var breakLid = New<SendEvent>();
            breakLid.eventTarget = new FsmEventTarget { target = FsmEventTarget.EventTarget.GameObjectFSM, gameObject = Global("Lid"), fsmName = "Info", excludeSelf = false, sendToChildren = false };
            breakLid.sendEvent = FsmEvent.GetFsmEvent("Damage");
            cpBossLid.Actions = new FsmStateAction[] { gateOn, breakLid, Wait(0.5f) };
            cpBossLid.Transitions = new[] { Go("FINISHED", "Damage The Lid") };

            // 4. Where to go on from: after dying (uses a revive), and as the game starts (where the player quit).
            var died = NewState(f, "Checkpoint?", basePos, 0f, 120f);
            var whereDied = New<GoToCheckpoint>();
            whereDied.afterDying = true;
            died.Actions = new FsmStateAction[] { whereDied };
            died.Transitions = Routes();
            var resume = NewState(f, "Resume?", State(f, "Init - Stage1").Position, 0f, 120f);
            var whereQuit = New<GoToCheckpoint>();
            whereQuit.afterDying = false;
            resume.Actions = new FsmStateAction[] { whereQuit };
            resume.Transitions = Routes();

            Redirect(State(f, "Recreate"), "FINISHED", "Checkpoint?");
            Redirect(State(f, "Init - Stage1"), "FINISHED", "Resume?");

            foreach (var s in f.States) s.SaveActions();
            EditorUtility.SetDirty(fsm);
            EditorSceneManager.MarkSceneDirty(fsm.gameObject.scene);
            Debug.Log("[Checkpoints] GAMESTAGES has checkpoints at '2 - The BR', '3 - LivingRoom', 'Prepare_Whack_6' and " +
                      "'Damage The Lid'. After dying: Recreate -> Checkpoint? (one revive a run); as the game starts: " +
                      "Init - Stage1 -> Resume?. Save the scene.", fsm);
        }

        [MenuItem("Tools/429 Game/Stages/Set Up Checkpoints", true)]
        private static bool CanSetUp()
        {
            // Changes made while playing are thrown away.
            return !EditorApplication.isPlaying;
        }

        // No checkpoint: the start (as before); otherwise that checkpoint's rebuild.
        private static FsmTransition[] Routes()
        {
            return new[] { Go("FINISHED", "Walk Tuttorial") }
                .Concat(Checkpoints.Select(c => Go(c.checkpoint, c.checkpoint))).ToArray();
        }

        private static void Redirect(FsmState state, string eventName, string toState)
        {
            var transition = state.Transitions.FirstOrDefault(t => t.EventName == eventName);
            if (transition != null) transition.ToState = toState;
            else state.Transitions = state.Transitions.Append(Go(eventName, toState)).ToArray();
        }

        private static GameObject Find(string name)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == name) return t.gameObject;
            }
            return null;
        }

        private static FsmOwnerDefault Global(string variable)
        {
            var value = FsmVariables.GlobalVariables.FindFsmGameObject(variable) ?? new FsmGameObject(variable) { UseVariable = true };
            return new FsmOwnerDefault { OwnerOption = OwnerDefaultOption.SpecifyGameObject, GameObject = value };
        }

        private static ActivateGameObject Activate(GameObject target)
        {
            var action = New<ActivateGameObject>();
            action.gameObject = new FsmOwnerDefault { OwnerOption = OwnerDefaultOption.SpecifyGameObject, GameObject = target };
            action.activate = true;
            action.recursive = false;
            return action;
        }

        private static Wait Wait(float seconds)
        {
            var action = New<Wait>();
            action.time = seconds;
            action.finishEvent = FsmEvent.Finished;
            action.realTime = false;
            return action;
        }

        private static FsmState NewState(Fsm f, string name, Rect near, float x, float y)
        {
            var state = f.States.FirstOrDefault(s => s.Name == name);
            if (state != null) return state;
            state = new FsmState(f) { Name = name, Position = new Rect(near.x + x, near.y + y, near.width, near.height) };
            f.States = f.States.Append(state).ToArray();
            return state;
        }

        private static T New<T>() where T : FsmStateAction, new()
        {
            var action = new T();
            action.Reset();
            return action;
        }

        private static FsmState State(Fsm f, string name) => f.States.First(s => s.Name == name);

        private static FsmTransition Go(string eventName, string toState) =>
            new() { FsmEvent = FsmEvent.GetFsmEvent(eventName), ToState = toState };
    }
}
