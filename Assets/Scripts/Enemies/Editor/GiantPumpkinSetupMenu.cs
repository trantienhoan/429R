using System.Linq;
using Game.Enemies;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Menu command that sets up the rooftop's giant pumpkins (Tools > 429 Game > Enemies).</summary>
    public static class GiantPumpkinSetupMenu
    {
        private const string GiantPath = "Assets/Prefabs/Enemies/Pumpkin_Boss.prefab";
        private const string RooftopPath = "Assets/Prefabs/Bases/Rooftop_Base_Z.prefab";
        private const string PoundEffectPath = "Assets/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Smoke_Blast.prefab";

        /// <summary>
        /// Puts Giant Pumpkin on Pumpkin_Boss (its hands break the pumpkins they hit, its pounds hurt the player only
        /// when they land near them), and takes the hit every player got, wherever they stood, out of the rooftop's
        /// End state. Running it again changes nothing.
        /// </summary>
        [MenuItem("Tools/429 Game/Enemies/Set Up Giant Pumpkins")]
        public static void SetUp()
        {
            string giantNote, roofNote;

            var giant = PrefabUtility.LoadPrefabContents(GiantPath);
            try
            {
                if (giant.GetComponent<GiantPumpkin>() == null)
                {
                    var serialized = new SerializedObject(giant.AddComponent<GiantPumpkin>());
                    serialized.FindProperty("poundEffect").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PoundEffectPath);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(giant, GiantPath);
                    giantNote = "added Giant Pumpkin to Pumpkin_Boss";
                }
                else giantNote = "Pumpkin_Boss already has Giant Pumpkin";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(giant);
            }

            var roof = PrefabUtility.LoadPrefabContents(RooftopPath);
            try
            {
                var info = roof.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Info");
                if (info == null)
                {
                    roofNote = "WARNING: Rooftop_Base_Z has no Info FSM";
                }
                else
                {
                    if (!info.Fsm.Initialized) info.Fsm.Init(info);
                    foreach (var s in info.FsmStates) s.LoadActions();
                    var end = info.FsmStates.FirstOrDefault(s => s.Name == "End");
                    int before = end != null ? end.Actions.Length : 0;
                    if (end != null)
                        end.Actions = end.Actions.Where(a => !(a is SendEvent send && send.sendEvent != null && send.sendEvent.Name == "Pumpkin_Boss_Damage")).ToArray();
                    if (end != null && end.Actions.Length < before)
                    {
                        foreach (var s in info.FsmStates) s.SaveActions();
                        EditorUtility.SetDirty(info);
                        PrefabUtility.SaveAsPrefabAsset(roof, RooftopPath);
                        roofNote = "the rooftop's End no longer hurts the player wherever they are (the giants' pounds do, near them)";
                    }
                    else roofNote = "the rooftop's End already leaves the player's hit to the giants";
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(roof);
            }

            Debug.Log($"[Giant Pumpkins] {giantNote}; {roofNote}.");
        }
    }
}
