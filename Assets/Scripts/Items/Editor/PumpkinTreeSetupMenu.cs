using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Menu command for the rooftop's Pumpkin_Tree (Tools > 429 Game > Items).</summary>
    public static class PumpkinTreeSetupMenu
    {
        private const string TreePath = "Assets/Prefabs/Items/Breakable/Pumpkin_Tree.prefab";

        /// <summary>
        /// Makes the pumpkins the Pumpkin_Tree grows stick to their branch slots: in its Pumpkin_Spawn FSM, right after
        /// Spawn Pumpkin's Create Object, a Stick To Anchor sticks the new pumpkin_growing to the slot it was made at. So
        /// it sways, shakes and jiggles with the tree, and when the tree dies the pumpkins still growing go with it.
        /// Running it again changes nothing.
        /// </summary>
        [MenuItem("Tools/429 Game/Items/Stick Growing Pumpkins To The Tree")]
        public static void StickPumpkins()
        {
            string note;
            var root = PrefabUtility.LoadPrefabContents(TreePath);
            try
            {
                var spawner = root.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Pumpkin_Spawn");
                if (spawner != null && !spawner.Fsm.Initialized) spawner.Fsm.Init(spawner);
                if (spawner != null) foreach (var s in spawner.FsmStates) s.LoadActions();
                var state = spawner != null ? spawner.FsmStates.FirstOrDefault(s => s.Name == "Spawn Pumpkin") : null;
                var create = state?.Actions.OfType<CreateObject>().FirstOrDefault();
                var where = state?.Actions.OfType<GetPosition>().FirstOrDefault();

                if (create == null || where == null || create.storeObject == null || create.storeObject.IsNone)
                {
                    note = "WARNING: its Pumpkin_Spawn FSM has no 'Spawn Pumpkin' state with a Get Position and a Create Object " +
                           "that stores the pumpkin, so nothing changed";
                }
                else if (state.Actions.OfType<StickToAnchor>().Any())
                {
                    note = "the growing pumpkins already stick to the tree";
                }
                else
                {
                    var stick = new StickToAnchor();
                    stick.Reset();
                    // The pumpkin Create Object just made, on the slot Get Position read.
                    stick.gameObject = new FsmOwnerDefault
                    {
                        OwnerOption = OwnerDefaultOption.SpecifyGameObject,
                        GameObject = create.storeObject,
                    };
                    stick.anchor = where.gameObject.GameObject;
                    var actions = state.Actions.ToList();
                    actions.Insert(actions.IndexOf(create) + 1, stick);
                    state.Actions = actions.ToArray();
                    foreach (var s in spawner.FsmStates) s.SaveActions();
                    EditorUtility.SetDirty(spawner);
                    PrefabUtility.SaveAsPrefabAsset(root, TreePath);
                    note = "the pumpkins it grows now stick to their branch slots (Pumpkin_Spawn > Spawn Pumpkin > Stick To Anchor) " +
                           "and are destroyed when the tree dies";
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log($"[Pumpkin Tree] {note}.");
        }

        [MenuItem("Tools/429 Game/Items/Stick Growing Pumpkins To The Tree", true)]
        private static bool CanStick() => !EditorApplication.isPlaying;

        // The player's hits on the tree are weak, the bugs' bites big (user, 2026-11-23). Whole numbers.
        private const string WeakState = "Deal 1 damage";
        private const string BiteState = "Bug Bite";
        private const float BugBite = 3f;
        private const float OldHealth = 100f;
        private const float NewHealth = 300f;

        private static readonly MethodInfo CloneMethod =
            typeof(object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>
        /// The user's rule (2026-11-23): "Player hit to the Tree count as weak, only bug should do big damage to the Tree".
        /// - Every hit of the player's is one weak hit, its Damage FSM's "Deal 1 damage" (1 health, then 0.5 s before the
        ///   next one counts): punches, swings and throws as before, and now the player's blasts too (Player Hits Weak;
        ///   they took their whole damage off at once with no wait: an exploding punch 1-2 every 0.2 s per hand, an
        ///   explosive throw 1-4, a smoke ball 2).
        /// - A bug's bite (the "Damage" event Spider_Tree sends) goes to a new state "Bug Bite": 3 health (was 1, the same
        ///   as the player's hit).
        /// - Health 100 -> 300, so the bugs still need the same 100 bites to break it.
        /// Running it again changes nothing (health is only moved from 100).
        /// </summary>
        [MenuItem("Tools/429 Game/Items/Pumpkin Tree: Weak Player Hits, Big Bug Bites")]
        public static void WeakPlayerHits()
        {
            var notes = new List<string>();
            string problem;
            var root = PrefabUtility.LoadPrefabContents(TreePath);
            try
            {
                problem = WeakPlayerHits(root, notes);
                if (problem == null && notes.Count > 0) PrefabUtility.SaveAsPrefabAsset(root, TreePath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            if (problem != null) Debug.LogWarning($"[Pumpkin Tree] {problem}; nothing changed.");
            else Debug.Log("[Pumpkin Tree] " + (notes.Count > 0 ? string.Join("; ", notes) : "already set: player hits weak (1), bug bites 3, health 300") + ".");
        }

        // The changes on the loaded prefab; a problem (nothing to save), or null.
        private static string WeakPlayerHits(GameObject root, List<string> notes)
        {
            {
                var fsms = root.GetComponents<PlayMakerFSM>();
                var damage = fsms.FirstOrDefault(f => f.FsmName == "Damage");
                var health = fsms.FirstOrDefault(f => f.FsmName == "Health");
                if (damage == null || health == null) return "no Damage or Health FSM on its root";
                if (!damage.Fsm.Initialized) damage.Fsm.Init(damage);
                foreach (var s in damage.FsmStates) s.LoadActions();
                var weak = damage.FsmStates.FirstOrDefault(s => s.Name == WeakState);
                if (weak == null || weak.ActionData.ActionCount != weak.Actions.Length || !weak.Actions.OfType<FloatSubtract>().Any())
                    return $"its Damage FSM has no readable '{WeakState}' with a Float Subtract";

                // 1. The bugs' bite: a state of its own, a copy of the weak hit (the same shake, smoke, sound and wait)
                //    that takes Bug Bite.
                var bite = damage.FsmStates.FirstOrDefault(s => s.Name == BiteState);
                if (bite == null)
                {
                    bite = new FsmState(damage.Fsm)
                    {
                        Name = BiteState,
                        Position = new Rect(weak.Position.x, weak.Position.y + 110f, weak.Position.width, weak.Position.height),
                        Actions = weak.Actions.Select(a => (FsmStateAction)CloneMethod.Invoke(a, null)).ToArray(),
                        Transitions = weak.Transitions.Select(t => new FsmTransition { FsmEvent = t.FsmEvent, ToState = t.ToState }).ToArray(),
                    };
                    damage.Fsm.States = damage.Fsm.States.Append(bite).ToArray();
                    notes.Add($"made '{BiteState}' (a copy of '{WeakState}')");
                }
                else bite.LoadActions();
                var take = bite.Actions.OfType<FloatSubtract>().FirstOrDefault();
                if (take != null && (take.subtract == null || take.subtract.UseVariable || !Mathf.Approximately(take.subtract.Value, BugBite)))
                {
                    // A new value of its own (the copy shared the weak hit's).
                    take.subtract = new FsmFloat { Value = BugBite };
                    notes.Add($"'{BiteState}' takes {BugBite}");
                }

                // 2. The "Damage" event (the bugs' bites) goes there.
                var global = damage.Fsm.GlobalTransitions.FirstOrDefault(t => t.EventName == "Damage");
                if (global == null)
                {
                    damage.Fsm.GlobalTransitions = damage.Fsm.GlobalTransitions
                        .Append(new FsmTransition { FsmEvent = FsmEvent.GetFsmEvent("Damage"), ToState = BiteState }).ToArray();
                    notes.Add($"global Damage -> '{BiteState}' (new)");
                }
                else if (global.ToState != BiteState)
                {
                    notes.Add($"global Damage -> '{BiteState}' (was '{global.ToState}')");
                    global.ToState = BiteState;
                }
                if (notes.Count > 0)
                {
                    foreach (var s in damage.FsmStates) s.SaveActions();
                    EditorUtility.SetDirty(damage);
                }

                // 3. Health 100 -> 300 (the Health FSM's saved value).
                var so = new SerializedObject(health);
                var floats = so.FindProperty("fsm.variables.floatVariables");
                for (int i = 0; floats != null && i < floats.arraySize; i++)
                {
                    var variable = floats.GetArrayElementAtIndex(i);
                    if (variable.FindPropertyRelative("name").stringValue != "health") continue;
                    var value = variable.FindPropertyRelative("value");
                    if (Mathf.Approximately(value.floatValue, OldHealth))
                    {
                        value.floatValue = NewHealth;
                        notes.Add($"health {OldHealth} -> {NewHealth}");
                    }
                    else if (!Mathf.Approximately(value.floatValue, NewHealth)) notes.Add($"health {value.floatValue} kept (only {OldHealth} is moved)");
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                // 4. The player's blasts count as one weak hit too.
                if (root.GetComponent<Game.Combat.PlayerHitsWeak>() == null)
                {
                    root.AddComponent<Game.Combat.PlayerHitsWeak>();
                    notes.Add("added Player Hits Weak (the player's blasts = one weak hit)");
                }
                return null;
            }
        }

        [MenuItem("Tools/429 Game/Items/Pumpkin Tree: Weak Player Hits, Big Bug Bites", true)]
        private static bool CanWeakPlayerHits() => !EditorApplication.isPlaying;
    }
}
