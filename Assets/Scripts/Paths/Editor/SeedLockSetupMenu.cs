using System.Collections.Generic;
using Game.Inventory;
using Game.Paths;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Menu command that turns the path blocks into seed locks (Tools > 429 Game > Paths).</summary>
    public static class SeedLockSetupMenu
    {
        private const string BlockPath = "Assets/Prefabs/Items/Stone_Cube_Dice {0}.prefab";
        private const string ItemsFolder = "Assets/Data/Inventory/Items/";
        private const string EffectsFolder = "Assets/Lana Studio/Hyper Casual FX/Prefabs/Area/";

        // Each path gets its own colour of circle.
        private static readonly string[] GateEffects = { "Area_circles_blue", "Area_heal_green", "Area_star_ellow" };

        /// <summary>
        /// Puts a Seed Lock on Stone_Cube_Dice 1, 2 and 3: 50 Yellow, 5 Green or 1 Boss Seed opens it, Cube_N and
        /// Gate_N are its GAMESTAGES bools. Settings already on a block are kept; only empty ones are filled in.
        /// </summary>
        [MenuItem("Tools/429 Game/Paths/Set Up Seed Locks")]
        public static void SetUpSeedLocks()
        {
            var yellow = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemsFolder + "Yellow_Seed.asset");
            var green = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemsFolder + "Green_Seed.asset");
            var boss = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemsFolder + "Boss_Seed.asset");
            if (yellow == null || green == null || boss == null)
            {
                Debug.LogError($"[Seed Locks] The seed items (Yellow_Seed, Green_Seed, Boss_Seed) aren't in {ItemsFolder}.");
                return;
            }
            var font = SetupUtility.ResolveFont();

            var notes = new List<string>();
            for (int n = 1; n <= 3; n++)
            {
                string path = string.Format(BlockPath, n);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    notes.Add($"no '{path}'");
                    continue;
                }

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool added = !root.TryGetComponent<SeedLock>(out var seedLock);
                    if (added) seedLock = root.AddComponent<SeedLock>();

                    var serialized = new SerializedObject(seedLock);
                    var prices = serialized.FindProperty("prices");
                    if (prices.arraySize == 0)
                    {
                        SetPrice(prices, 0, yellow, 50);
                        SetPrice(prices, 1, green, 5);
                        SetPrice(prices, 2, boss, 1);
                    }
                    if (added)
                    {
                        serialized.FindProperty("cubeBool").stringValue = $"Cube_{n}";
                        serialized.FindProperty("gateBool").stringValue = $"Gate_{n}";
                    }
                    SetIfEmpty(serialized, "font", font);
                    SetIfEmpty(serialized, "gateEffect", AssetDatabase.LoadAssetAtPath<GameObject>(EffectsFolder + GateEffects[n - 1] + ".prefab"));
                    serialized.ApplyModifiedPropertiesWithoutUndo();

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    notes.Add($"{root.name}: {(added ? "added" : "updated")} (Cube_{n} / Gate_{n})");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            Debug.Log("[Seed Locks] " + string.Join("; ", notes) +
                      ". Add the bools Cube_1-3 and Gate_1-3 to the GAMESTAGES FSM's Variables if they aren't there yet.");
        }

        private static void SetPrice(SerializedProperty prices, int index, ItemDefinition item, int amount)
        {
            if (prices.arraySize <= index) prices.arraySize = index + 1;
            var price = prices.GetArrayElementAtIndex(index);
            price.FindPropertyRelative("item").objectReferenceValue = item;
            price.FindPropertyRelative("amount").intValue = amount;
        }

        private static void SetIfEmpty(SerializedObject serialized, string field, Object value)
        {
            var property = serialized.FindProperty(field);
            if (property != null && property.objectReferenceValue == null) property.objectReferenceValue = value;
        }
    }
}
