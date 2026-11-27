using Game.Rooms;
using UnityEditor;
using UnityEngine;

namespace Game.Rooms.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Story > Boss Fight Under The Opening Sky (re-runnable; Remove Boss Fight Sky): puts a Boss Fight
    /// Sky on the Boss_Area prefab (child "Boss Sky") pointing at the opening's own sky, moon and hidden ground in
    /// Resources/Opening_Battle, so the last fight looks like the opening (user 2026-11-27) and any later change to the
    /// opening's sky shows there too. Prefab only, no scene change.
    /// </summary>
    internal static class BossFightSkyMenu
    {
        private const string BossAreaPath = "Assets/Prefabs/Bases/Boss_Area.prefab";
        private const string OpeningPath = "Assets/Resources/Opening_Battle.prefab";
        internal const string ChildName = "Boss Sky";

        [MenuItem("Tools/429 Game/Story/Boss Fight Under The Opening Sky")]
        private static void Add()
        {
            var opening = AssetDatabase.LoadAssetAtPath<GameObject>(OpeningPath);
            var sky = opening != null ? opening.transform.Find("Sky") : null;
            var moon = opening != null ? opening.transform.Find("Moon") : null;
            var ground = opening != null ? opening.transform.Find("Dream Floor/Ground") : null;
            if (sky == null || moon == null || moon.GetComponent<Light>() == null)
            {
                EditorUtility.DisplayDialog("Boss Fight Sky", "Resources/Opening_Battle has no Sky or Moon (run Set Up Opening first).", "OK");
                return;
            }
            var root = PrefabUtility.LoadPrefabContents(BossAreaPath);
            try
            {
                var holder = root.transform.Find(ChildName);
                if (holder == null)
                {
                    holder = new GameObject(ChildName).transform;
                    holder.SetParent(root.transform, false);
                }
                holder.localPosition = Vector3.zero;
                var look = holder.GetComponent<BossFightSky>() ?? holder.gameObject.AddComponent<BossFightSky>();
                var so = new SerializedObject(look);
                so.FindProperty("sky").objectReferenceValue = sky.gameObject;
                so.FindProperty("moon").objectReferenceValue = moon.GetComponent<Light>();
                so.FindProperty("groundMask").objectReferenceValue = ground != null ? ground.gameObject : null;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, BossAreaPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log("[Boss Fight Sky] Boss_Area/Boss Sky: the opening's Sky, Moon and hidden ground over the boss fight " +
                      "(GAMESTAGES BOSS .. Thank For Playing). Prefab saved; no scene change.");
        }

        [MenuItem("Tools/429 Game/Story/Remove Boss Fight Sky")]
        private static void Remove()
        {
            var root = PrefabUtility.LoadPrefabContents(BossAreaPath);
            try
            {
                var holder = root.transform.Find(ChildName);
                if (holder == null) return;
                Object.DestroyImmediate(holder.gameObject);
                PrefabUtility.SaveAsPrefabAsset(root, BossAreaPath);
                Debug.Log("[Boss Fight Sky] Removed from Boss_Area.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
