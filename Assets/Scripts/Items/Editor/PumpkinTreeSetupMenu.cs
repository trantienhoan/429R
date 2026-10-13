using System.Linq;
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
    }
}
