using Game.Menus;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Menu > Road End Notice (user, 2026-11-20): puts Road End Notice on Rooftop_Base_Z, so getting to
    /// the end of the start rooftop's long south arm shows "There is nothing here yet ..." (only on the rooftop of a run's
    /// start). Running it again changes nothing more; the prefab is saved, no scene change.
    /// </summary>
    public static class RoadEndNoticeSetup
    {
        private const string RooftopPath = "Assets/Prefabs/Bases/Rooftop_Base_Z.prefab";

        [MenuItem("Tools/429 Game/Menu/Road End Notice")]
        public static void Run()
        {
            var root = PrefabUtility.LoadPrefabContents(RooftopPath);
            try
            {
                if (root.GetComponent<RoadEndNotice>() != null)
                {
                    Debug.Log("[Road End Notice] Rooftop_Base_Z already has it.");
                    return;
                }
                root.AddComponent<RoadEndNotice>();
                PrefabUtility.SaveAsPrefabAsset(root, RooftopPath);
                Debug.Log("[Road End Notice] Rooftop_Base_Z: the end of the south arm (z -32.8 to -29.2) shows the note, on the run's start rooftop only. Text and zone on its Road End Notice.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Tools/429 Game/Menu/Road End Notice", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
