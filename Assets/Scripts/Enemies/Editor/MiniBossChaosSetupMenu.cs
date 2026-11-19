using System.Collections.Generic;
using Game.Enemies;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Makes (or updates) Resources/Mini Boss Chaos, the mini bosses' chaos rebirth settings (user, 2026-11-19): the six
    /// bug mini bosses, the red Chaos_Aura and the puff they come back in. Re-runnable; the numbers already in the asset
    /// (chance, health, damage, EXP) are kept. Tools > 429 Game > Enemies > Set Up Mini Boss Chaos Rebirth.
    /// </summary>
    public static class MiniBossChaosSetupMenu
    {
        private const string AssetPath = "Assets/Resources/" + MiniBossChaos.ResourceName + ".asset";
        private const string BossFolder = "Assets/Prefabs/Enemies/Enemies_Smarter/";
        private static readonly string[] Bosses =
        {
            "Spider_Boss", "Spider_Boss_PumpkinButt", "Cockroach_Boss", "Cockroach_Boss_PumpkinButt", "Bee_Boss",
            "Bee_Boss_PumpkinButt",
        };
        private const string AuraPath = "Assets/Prefabs/Effects/Chaos_Aura.prefab";
        private const string PuffPath = "Assets/Prefabs/Effects/Smoke/Smoke_Puff_Burst_Out.prefab";

        [MenuItem("Tools/429 Game/Enemies/Set Up Mini Boss Chaos Rebirth")]
        public static void SetUp()
        {
            Debug.Log(Apply());
        }

        /// <summary>Does it; returns what it did, for the Console.</summary>
        public static string Apply()
        {
            var settings = AssetDatabase.LoadAssetAtPath<MiniBossChaos>(AssetPath);
            bool made = settings == null;
            if (made)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
                settings = ScriptableObject.CreateInstance<MiniBossChaos>();
                AssetDatabase.CreateAsset(settings, AssetPath);
            }

            var found = new List<GameObject>();
            var missing = new List<string>();
            foreach (var name in Bosses)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossFolder + name + ".prefab");
                if (prefab != null) found.Add(prefab);
                else missing.Add(name);
            }

            var so = new SerializedObject(settings);
            var list = so.FindProperty("bosses");
            list.arraySize = found.Count;
            for (int i = 0; i < found.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
            so.FindProperty("aura").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(AuraPath);
            so.FindProperty("rebirthEffect").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PuffPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            return $"[Mini Boss Chaos] {(made ? "made" : "updated")} {AssetPath}: {found.Count} mini bosses" +
                   (missing.Count > 0 ? $", MISSING {string.Join(", ", missing)}" : "") +
                   $", aura {(settings.Aura != null ? "set" : "MISSING")}, chance {settings.Chance:0.##}, health x{settings.HealthTimes:0.##}, " +
                   $"damage x{settings.DamageTimes:0.##}, EXP x{settings.ExpTimes:0.##}.";
        }
    }
}
