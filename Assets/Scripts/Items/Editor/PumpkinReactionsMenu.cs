using System.Collections.Generic;
using System.Linq;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Menu commands for how the room pumpkins react (Tools > 429 Game > Items).</summary>
    public static class PumpkinReactionsMenu
    {
        private const string Folder = "Assets/Prefabs/Items/Breakable/";
        private const string StartPumpkinPath = Folder + "StartGamePumpkin.prefab";
        private static readonly string[] JumpingPumpkins = { "Pumpkin_3", "Pumpkin_5", "Pumpkin_7" };
        // Longer than the longest ouch it plays (pumpkin_get_hit_1, 0.56 s), so the laughing starts again after it.
        private const float HurtSeconds = 0.7f;

        /// <summary>
        /// Pumpkin_3, 5 and 7 turn round to face the player once they land after each jump (Face Player After Jump), and
        /// stop spinning. Running it again changes nothing.
        /// </summary>
        [MenuItem("Tools/429 Game/Items/Pumpkins Face The Player After Jumping")]
        public static void FaceAfterJump()
        {
            var done = new List<string>();
            foreach (var name in JumpingPumpkins)
            {
                string path = Folder + name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { done.Add(name + " missing"); continue; }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.TryGetComponent<FacePlayerAfterJump>(out _)) { done.Add(name + " (already)"); continue; }
                    root.AddComponent<FacePlayerAfterJump>();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    done.Add(name);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            Debug.Log("[Pumpkins] Face the player after jumping: " + string.Join(", ", done) + ".");
        }

        /// <summary>
        /// Pumpkin_3, 5 and 7 break the candy they sit on after landing (Crush Candy), their own trail included. Running it
        /// again changes nothing.
        /// </summary>
        [MenuItem("Tools/429 Game/Items/Jumping Pumpkins Crush Candy")]
        public static void CrushCandy()
        {
            var done = new List<string>();
            foreach (var name in JumpingPumpkins)
            {
                string path = Folder + name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { done.Add(name + " missing"); continue; }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.TryGetComponent<Game.Inventory.CrushCandy>(out _)) { done.Add(name + " (already)"); continue; }
                    root.AddComponent<Game.Inventory.CrushCandy>();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    done.Add(name);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            Debug.Log("[Pumpkins] Crush candy: " + string.Join(", ", done) + ".");
        }

        /// <summary>
        /// StartGamePumpkin stops laughing while it says ouch. On pumpkin_Start: the Damage FSM's 'Deal 1 damage' first
        /// stops the sound playing (the laugh) before its ouch, and the Health FSM goes from Punch or Loop Punch (the
        /// laughing loop) to a new 'Hurt' state on Damage, which waits for the ouch to end (still facing the player and
        /// still checking the health), then back to Punch. Running it again changes nothing.
        /// </summary>
        [MenuItem("Tools/429 Game/Items/Start Pumpkin Stops Laughing When Hurt")]
        public static void QuietWhenHurt()
        {
            var notes = new List<string>();
            var root = PrefabUtility.LoadPrefabContents(StartPumpkinPath);
            try
            {
                var pumpkin = root.transform.Find("pumpkin_Start");
                var fsms = pumpkin != null ? pumpkin.GetComponents<PlayMakerFSM>() : new PlayMakerFSM[0];
                var health = fsms.FirstOrDefault(f => f.FsmName == "Health");
                var damage = fsms.FirstOrDefault(f => f.FsmName == "Damage");
                if (health == null || damage == null)
                {
                    Debug.LogError("[Pumpkins] StartGamePumpkin has no pumpkin_Start with a Health and a Damage FSM, so nothing changed.");
                    return;
                }
                foreach (var fsm in new[] { health, damage })
                {
                    if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
                    foreach (var s in fsm.FsmStates) s.LoadActions();
                }

                if (StopBeforeOuch(damage)) notes.Add("Damage > Deal 1 damage stops the laugh before the ouch (Audio Stop)");
                if (AddHurtState(health, notes)) notes.Add($"Health: Damage in Punch / Loop Punch goes to 'Hurt' (waits {HurtSeconds} s, then laughs again)");

                if (notes.Count > 0)
                {
                    foreach (var fsm in new[] { health, damage })
                    {
                        foreach (var s in fsm.FsmStates) s.SaveActions();
                        EditorUtility.SetDirty(fsm);
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, StartPumpkinPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log("[Pumpkins] Start pumpkin: " + (notes.Count > 0 ? string.Join("; ", notes) : "already quiet when hurt") + ".");
        }

        [MenuItem("Tools/429 Game/Items/Pumpkins Face The Player After Jumping", true)]
        [MenuItem("Tools/429 Game/Items/Jumping Pumpkins Crush Candy", true)]
        [MenuItem("Tools/429 Game/Items/Start Pumpkin Stops Laughing When Hurt", true)]
        private static bool NotPlaying() => !EditorApplication.isPlaying;

        // An Audio Stop on the pumpkin right before the ouch is picked and played.
        private static bool StopBeforeOuch(PlayMakerFSM damage)
        {
            var deal = damage.FsmStates.FirstOrDefault(s => s.Name == "Deal 1 damage");
            if (deal == null || deal.Actions.OfType<AudioStop>().Any()) return false;
            var actions = deal.Actions.ToList();
            int at = actions.FindIndex(a => a is ArrayGetRandom);
            if (at < 0) at = actions.FindIndex(a => a is AudioPlay);
            if (at < 0) return false;

            var stop = new AudioStop();
            stop.Reset();
            stop.gameObject = new FsmOwnerDefault();
            stop.fadeTime = 0f;
            actions.Insert(at, stop);
            deal.Actions = actions.ToArray();
            return true;
        }

        // Hurt: faces the player and checks the health like Punch does, but stays quiet while the ouch plays.
        private static bool AddHurtState(PlayMakerFSM health, List<string> notes)
        {
            var fsm = health.Fsm;
            if (fsm.States.Any(s => s.Name == "Hurt")) return false;
            var punch = fsm.States.FirstOrDefault(s => s.Name == "Punch");
            var loop = fsm.States.FirstOrDefault(s => s.Name == "Loop Punch");
            var crack = fsm.States.FirstOrDefault(s => s.Name == "Crack");
            var look = punch?.Actions.OfType<LookAt>().FirstOrDefault();
            var check = punch?.Actions.OfType<FloatCompare>().FirstOrDefault();
            if (punch == null || loop == null || crack == null || look == null || check == null)
            {
                notes.Add("WARNING: the Health FSM isn't laid out as expected (Punch, Loop Punch, Crack), so it wasn't changed");
                return false;
            }

            var damageEvent = FsmEvent.GetFsmEvent("Damage");
            if (!fsm.Events.Contains(damageEvent)) fsm.Events = fsm.Events.Append(damageEvent).ToArray();

            var face = new LookAt();
            face.Reset();
            face.gameObject = new FsmOwnerDefault();
            face.targetObject = look.targetObject;
            face.keepVertical = look.keepVertical;
            face.everyFrame = true;

            var cracked = new FloatCompare();
            cracked.Reset();
            cracked.float1 = check.float1;
            cracked.float2 = check.float2.Value;
            cracked.tolerance = check.tolerance.Value;
            cracked.equal = check.equal;
            cracked.lessThan = check.lessThan;
            cracked.everyFrame = true;

            var wait = new Wait();
            wait.Reset();
            wait.time = HurtSeconds;
            wait.finishEvent = FsmEvent.Finished;

            var hurt = new FsmState(fsm)
            {
                Name = "Hurt",
                Position = new Rect(punch.Position.x + 190f, punch.Position.y - 90f, 100f, 48f),
                Actions = new FsmStateAction[] { face, cracked, wait },
                Transitions = new[]
                {
                    new FsmTransition { FsmEvent = FsmEvent.Finished, ToState = punch.Name },
                    new FsmTransition { FsmEvent = check.equal ?? check.lessThan, ToState = crack.Name },
                    // Hit again: the ouch starts over, so the wait does too.
                    new FsmTransition { FsmEvent = damageEvent, ToState = "Hurt" },
                },
            };
            fsm.States = fsm.States.Append(hurt).ToArray();

            foreach (var laughing in new[] { punch, loop })
            {
                if (laughing.Transitions.Any(t => t.EventName == damageEvent.Name)) continue;
                laughing.Transitions = laughing.Transitions.Append(new FsmTransition { FsmEvent = damageEvent, ToState = hurt.Name }).ToArray();
            }
            return true;
        }
    }
}
