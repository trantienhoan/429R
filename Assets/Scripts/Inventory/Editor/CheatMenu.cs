using System.Collections.Generic;
using System.IO;
using Game.Inventory;
using Game.Saving;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Cheats for testing (Tools > 429 Game > Cheats). Editor only: none of this ends up in the game.</summary>
    public static class CheatMenu
    {
        private const int SeedAmount = 300;
        private static readonly string[] Seeds = { "Yellow_Seed", "Green_Seed", "Boss_Seed" };

        /// <summary>
        /// Adds 300 Yellow, Green and Boss Seeds. While playing they go straight into the inventory (and are saved
        /// like any pickup); otherwise into every save on this computer, so they're there the next time the game starts.
        /// Lifetime "collected" stats aren't touched.
        /// </summary>
        [MenuItem("Tools/429 Game/Cheats/Give 300 Of Each Seed")]
        public static void GiveSeeds()
        {
            var items = new List<ItemDefinition>();
            foreach (var seed in Seeds)
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{SetupUtility.ItemsFolder}/{seed}.asset");
                if (item == null)
                {
                    Debug.LogError($"[Cheats] There's no '{seed}' item in {SetupUtility.ItemsFolder}.");
                    return;
                }
                items.Add(item);
            }

            if (EditorApplication.isPlaying) GiveWhilePlaying(items);
            else GiveInSaveFiles(items);
        }

        private static void GiveWhilePlaying(List<ItemDefinition> items)
        {
            var inventory = PlayerInventory.Instance;
            if (inventory == null)
            {
                Debug.LogWarning("[Cheats] There's no PlayerInventory in the scene.");
                return;
            }

            // Through the save data, not Add, so it doesn't count as collecting them.
            var contents = inventory.ToSaveData();
            foreach (var item in items) AddTo(contents, item.Id, SeedAmount);
            inventory.LoadSaveData(contents);
            Debug.Log($"[Cheats] Gave {SeedAmount} of each seed. Now: {Counts(inventory, items)}.");
        }

        private static void GiveInSaveFiles(List<ItemDefinition> items)
        {
            // Every player's save (Local and each Steam ID); with none yet, a new Local one.
            var paths = new List<string>();
            if (Directory.Exists(SaveManager.SavesRoot))
            {
                foreach (var folder in Directory.GetDirectories(SaveManager.SavesRoot))
                {
                    string path = Path.Combine(folder, "save.json");
                    if (File.Exists(path)) paths.Add(path);
                }
            }
            if (paths.Count == 0) paths.Add(SaveManager.SavePath);

            foreach (var path in paths)
            {
                var data = File.Exists(path) ? JsonUtility.FromJson<SaveManager.SaveData>(File.ReadAllText(path)) : new SaveManager.SaveData();
                if (data == null)
                {
                    Debug.LogWarning($"[Cheats] Couldn't read '{path}'; skipped.");
                    continue;
                }

                foreach (var item in items) AddTo(data.inventory, item.Id, SeedAmount);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonUtility.ToJson(data, true));
                Debug.Log($"[Cheats] Added {SeedAmount} of each seed to '{path}'.");
            }
        }

        private static void AddTo(List<PlayerInventory.SavedEntry> entries, string id, int amount)
        {
            foreach (var entry in entries)
            {
                if (entry.id != id) continue;
                entry.count += amount;
                return;
            }
            entries.Add(new PlayerInventory.SavedEntry { id = id, count = amount });
        }

        private static string Counts(PlayerInventory inventory, List<ItemDefinition> items)
        {
            var parts = new List<string>();
            foreach (var item in items) parts.Add($"{inventory.GetCount(item)} {item.DisplayName}");
            return string.Join(", ", parts);
        }
    }
}
