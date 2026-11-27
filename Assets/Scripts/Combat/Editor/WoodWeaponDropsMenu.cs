using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Combat.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Combat > Set Up Wood Weapon Drops (user 2026-11-27: "check furnitures in the game, make them wood
    /// furniture have high chance of dropping wooden weapon like bat, broom..."). Re-runnable: makes Resources/Wood Weapon
    /// Drop Settings if missing (chance 0.4 a broken wooden piece, at most 3 of its weapons about; Weapon_Baseball_Bat 45,
    /// Hammer_Broom 40, Hammer_Tool 15 (the bat and the broom are all wood, the tool hammer has a wooden handle); the
    /// Knight armor's found puff), fills what is empty, turns it back on if it was off, and lists in the Console which
    /// furniture prefabs count as wooden. "Wood Weapon Drops: Off" sets the chance to 0. Wood Weapon Drops (made by itself
    /// at the game's start) does the dropping. Prefabs and settings only, no scene change.
    /// </summary>
    internal static class WoodWeaponDropsMenu
    {
        private const string Tag = "[Wood Weapon Drops]";
        private const string SettingsPath = "Assets/Resources/Wood Weapon Drop Settings.asset";
        private const string ArmorSettingsPath = "Assets/Resources/Knight Armor Settings.asset";
        private static readonly (string path, float weight)[] Weapons =
        {
            ("Assets/Prefabs/Weapon/Weapon_Baseball_Bat.prefab", 45f),
            ("Assets/Prefabs/Weapon/Hammer_Broom.prefab", 40f),
            ("Assets/Prefabs/Weapon/Hammer_Tool.prefab", 15f),
        };

        // Never from these: the metal versions, doors, chests, barrels, webs, lights, the playroom's Jenga blocks.
        private static readonly string[] SkipWords = { "Metal", "Door", "Chest", "Barrel", "Web", "Light", "Candle", "Chandelier", "Jenga" };

        [MenuItem("Tools/429 Game/Combat/Set Up Wood Weapon Drops")]
        private static void SetUp()
        {
            var settings = AssetDatabase.LoadAssetAtPath<WoodWeaponDropSettings>(SettingsPath);
            bool made = false;
            if (settings == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                settings = ScriptableObject.CreateInstance<WoodWeaponDropSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
                made = true;
            }
            var so = new SerializedObject(settings);
            var list = so.FindProperty("weapons");
            if (list.arraySize == 0)
            {
                list.arraySize = Weapons.Length;
                for (int i = 0; i < Weapons.Length; i++)
                {
                    var item = list.GetArrayElementAtIndex(i);
                    item.FindPropertyRelative("weapon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Weapons[i].path);
                    item.FindPropertyRelative("weight").floatValue = Weapons[i].weight;
                }
            }
            var puff = so.FindProperty("puff");
            if (puff.objectReferenceValue == null)
            {
                var armor = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ArmorSettingsPath);
                if (armor != null) puff.objectReferenceValue = new SerializedObject(armor).FindProperty("foundPuff").objectReferenceValue;
            }
            var chance = so.FindProperty("chance");
            if (chance.floatValue <= 0f) chance.floatValue = 0.4f;
            // The skip words it needs (added, never removed: the user's own stay).
            var skips = so.FindProperty("skipNames");
            foreach (var word in SkipWords)
            {
                bool has = false;
                for (int i = 0; i < skips.arraySize && !has; i++) has = skips.GetArrayElementAtIndex(i).stringValue == word;
                if (has) continue;
                skips.arraySize++;
                skips.GetArrayElementAtIndex(skips.arraySize - 1).stringValue = word;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            var woods = WoodenFurniture(settings, out var others);
            Debug.Log($"{Tag} {(made ? "Made" : "Updated")} {SettingsPath}: chance {chance.floatValue}, at most {settings.MostAbout} about. " +
                      $"Wooden furniture ({woods.Count}): {string.Join(", ", woods)}.\nNot wooden ({others.Count}): {string.Join(", ", others)}.");
        }

        [MenuItem("Tools/429 Game/Combat/Wood Weapon Drops: Off")]
        private static void Off()
        {
            var settings = AssetDatabase.LoadAssetAtPath<WoodWeaponDropSettings>(SettingsPath);
            if (settings == null)
            {
                Debug.Log($"{Tag} There are no settings: nothing drops already.");
                return;
            }
            var so = new SerializedObject(settings);
            so.FindProperty("chance").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} Off (chance 0). Set Up Wood Weapon Drops turns it back on.");
        }

        // The furniture prefabs that count as wooden by the settings' rule (as Wood Weapon Drops judges them).
        private static List<string> WoodenFurniture(WoodWeaponDropSettings settings, out List<string> others)
        {
            var woods = new List<string>();
            others = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (go == null || !go.TryGetComponent(out EnemyHitDamage hit) || !hit.IsFurniture || go.GetComponent<WeaponPower>() != null) continue;
                (Wooden(settings, go) ? woods : others).Add(go.name);
            }
            woods.Sort();
            others.Sort();
            return woods;
        }

        private static bool Wooden(WoodWeaponDropSettings settings, GameObject thing)
        {
            foreach (var skip in settings.SkipNames)
                if (!string.IsNullOrEmpty(skip) && thing.name.IndexOf(skip, System.StringComparison.OrdinalIgnoreCase) >= 0) return false;
            foreach (var r in thing.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                var m = r.sharedMaterial;
                if (m == null) continue;
                foreach (var word in settings.WoodWords)
                    if (!string.IsNullOrEmpty(word) && m.name.IndexOf(word, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
                return false;
            }
            return false;
        }
    }
}
