using System;
using System.Collections.Generic;
using System.IO;
using Game.Combat;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Menu command that sets up hit-strength damage for enemies and weapons (Tools > 429 Game > Combat).</summary>
    public static class HitDamageSetupMenu
    {
        private const string SettingsPath = "Assets/Data/Combat/Hit Settings.asset";
        private const string EnemyFolder = "Assets/Prefabs/Enemies";
        private const string WeaponFolder = "Assets/Prefabs/Weapon";
        private const string EnemyTag = "Enemy";

        /// <summary>
        /// Makes the Hit Settings asset, adds Enemy Hit Damage to every enemy prefab tagged Enemy that has a Health FSM,
        /// and Weapon Power to every weapon. Prefabs that already have them keep their settings.
        /// </summary>
        [MenuItem("Tools/429 Game/Combat/Set Up Hit Damage")]
        public static void SetUpHitDamage()
        {
            var settings = EnsureSettings();
            var enemies = new List<string>();
            var noHealth = new List<string>();
            var weapons = new List<string>();

            foreach (var path in PrefabsIn(EnemyFolder))
                EditPrefab(path, root => AddEnemyHitDamage(root, settings, enemies, noHealth));
            foreach (var path in PrefabsIn(WeaponFolder))
                EditPrefab(path, root => AddWeaponPower(root, weapons));

            AssetDatabase.SaveAssets();
            Selection.activeObject = settings;
            Debug.Log($"[Hit Damage] Enemies set up: {List(enemies)}. " +
                      (noHealth.Count > 0 ? $"Skipped (no Health FSM with a \"health\" variable): {List(noHealth)}. " : "") +
                      $"Weapon power: {List(weapons)}. Tune the numbers in '{AssetDatabase.GetAssetPath(settings)}' and on each prefab.");
        }

        private static bool AddEnemyHitDamage(GameObject root, HitSettings settings, List<string> added, List<string> noHealth)
        {
            if (!root.CompareTag(EnemyTag) || root.GetComponent<EnemyHitDamage>() != null) return false;
            if (!HasHealthFsm(root))
            {
                noHealth.Add(root.name);
                return false;
            }

            // The mini-bosses ("The..." bugs) need harder hits and slide further.
            bool miniBoss = root.name.StartsWith("The", StringComparison.Ordinal);
            var hitDamage = root.AddComponent<EnemyHitDamage>();
            var serialized = new SerializedObject(hitDamage);
            serialized.FindProperty("settings").objectReferenceValue = settings;
            serialized.FindProperty("toughness").floatValue = miniBoss ? 1.5f : 1f;
            serialized.FindProperty("knockBackDistance").floatValue = miniBoss ? 2.5f : 1.5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            added.Add(miniBoss ? root.name + " (mini-boss)" : root.name);
            return true;
        }

        private static bool AddWeaponPower(GameObject root, List<string> added)
        {
            if (root.GetComponent<WeaponPower>() != null || root.GetComponent<Rigidbody>() == null) return false;

            float power = StartingPower(root.name);
            var weapon = root.AddComponent<WeaponPower>();
            var serialized = new SerializedObject(weapon);
            serialized.FindProperty("power").floatValue = power;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            added.Add($"{root.name} x{power}");
            return true;
        }

        // A first guess per weapon; change Power on each weapon after playing.
        private static float StartingPower(string weapon)
        {
            // Bullets are fast already: Bus (35 m/s) and Elon (55 m/s) land big hits at these.
            if (weapon.Contains("Bullet")) return weapon.Contains("Elon") ? 0.25f : 0.3f;
            if (weapon.Contains("Pillow")) return 0.6f;
            if (weapon.Contains("Broom")) return 1.3f;
            if (weapon.Contains("Hammer")) return 2f;
            if (weapon.Contains("Flip_Flop")) return 1.2f;
            if (weapon.Contains("Boong_Gun")) return 1.6f;
            // Bus_Boong and Elon_Boong swung as clubs.
            if (weapon.Contains("Boong")) return 1.3f;
            return 1f;
        }

        private static bool HasHealthFsm(GameObject root)
        {
            foreach (var fsm in root.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName.EndsWith("Health", StringComparison.Ordinal) && fsm.FsmVariables.FindFsmFloat("health") != null)
                    return true;
            }
            return false;
        }

        private static HitSettings EnsureSettings()
        {
            var existing = AssetDatabase.FindAssets("t:" + nameof(HitSettings));
            if (existing.Length > 0)
                return AssetDatabase.LoadAssetAtPath<HitSettings>(AssetDatabase.GUIDToAssetPath(existing[0]));

            SetupUtility.EnsureFolder(Path.GetDirectoryName(SettingsPath)?.Replace('\\', '/'));
            var settings = ScriptableObject.CreateInstance<HitSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }

        private static IEnumerable<string> PrefabsIn(string folder)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
                yield return AssetDatabase.GUIDToAssetPath(guid);
        }

        private static void EditPrefab(string path, Func<GameObject, bool> edit)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (edit(root)) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string List(List<string> names)
        {
            return names.Count > 0 ? string.Join(", ", names) : "none";
        }
    }
}
