using System;
using System.Collections.Generic;
using System.IO;
using Game.Inventory;
using Game.Menus;
using Game.Shopping;
using UnityEngine;

namespace Game.Saving
{
    /// <summary>
    /// Keeps the player's progress between sessions: what they carry (e.g. candies), how many of each item they
    /// ever collected and spent, and what they bought. Keep one in the scene. It loads when the game starts and
    /// saves shortly after anything changes, when the game pauses and when it closes. Dying doesn't touch the
    /// inventory, so candies are kept through a restart too. FSMs can also save at checkpoints with the
    /// "Save Game" action (PlayMaker's "Save" category).
    /// </summary>
    [DisallowMultipleComponent]
    public class SaveManager : MonoBehaviour
    {
        /// <summary>Everything in the save file. Add new fields at the end; older files just leave them empty.</summary>
        [Serializable]
        public class SaveData
        {
            public int version;
            public string savedAtUtc;
            public List<PlayerInventory.SavedEntry> inventory = new();
            public List<PlayerInventory.SavedStats> itemStats = new();
            public List<Shop.SavedPurchase> shopPurchases = new();
        }

        private const int CurrentVersion = 1;
        private const string FileName = "save.json";
        private const string DefaultProfile = "Local";
        private const float RetryDelay = 5f;

        [Tooltip("Left empty, PlayerInventory.Instance is used.")]
        [SerializeField] private PlayerInventory inventory;
        [Tooltip("Left empty, the shop in the scene is used.")]
        [SerializeField] private Shop shop;
        [Tooltip("Seconds from a change to writing the file, so a burst of pickups is written once.")]
        [Min(0f)]
        [SerializeField] private float saveDelay = 1.5f;

        private bool loaded;
        private bool dirty;
        private float saveAt;

        public static SaveManager Instance { get; private set; }

        /// <summary>
        /// The current player's folder under Saves. "Local" until Steam is set up; the Steam integration
        /// sets it to the player's Steam ID before the scene loads, so each account gets its own save.
        /// </summary>
        public static string Profile { get; set; } = DefaultProfile;

        public static string SaveFolder => Path.Combine(Application.persistentDataPath, "Saves", Profile);
        public static string SavePath => Path.Combine(SaveFolder, FileName);
        private static string BackupPath => SavePath + ".bak";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            Profile = DefaultProfile;
        }

        /// <summary>
        /// Switches to another player's save folder, e.g. their Steam ID. Call it before the scene loads.
        /// The first time, a save made before (in the Local folder) is copied over, so earlier progress is kept.
        /// </summary>
        public static void UseProfile(string profile)
        {
            if (string.IsNullOrEmpty(profile)) return;

            string localSave = Path.Combine(Application.persistentDataPath, "Saves", DefaultProfile, FileName);
            Profile = profile;
            if (File.Exists(SavePath) || !File.Exists(localSave)) return;

            try
            {
                Directory.CreateDirectory(SaveFolder);
                File.Copy(localSave, SavePath);
                Debug.Log($"[SaveManager] Copied the save in '{localSave}' to '{SaveFolder}'.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveManager] Couldn't copy the save in '{localSave}' to '{SaveFolder}': {e.Message}");
            }
        }

        /// <summary>Deletes the current player's save. Returns how many files were deleted.</summary>
        public static int DeleteSaveFiles()
        {
            int deleted = 0;
            foreach (var path in new[] { SavePath, BackupPath, SavePath + ".tmp" })
            {
                if (!File.Exists(path)) continue;
                File.Delete(path);
                deleted++;
            }
            return deleted;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[SaveManager] The scene already has a save manager ({Instance.name}); '{name}' is ignored.", this);
                enabled = false;
                return;
            }
            Instance = this;
        }

        // In Start, so the inventory has set itself up in Awake first.
        private void Start()
        {
            if (Instance != this) return;

            if (inventory == null) inventory = PlayerInventory.Instance;
            if (shop == null) shop = Shop.Main;
            if (inventory == null)
            {
                Debug.LogWarning("[SaveManager] There is no PlayerInventory in the scene, so nothing is saved.", this);
                return;
            }

            Load();

            // Subscribed after loading, so loading itself doesn't count as a change.
            inventory.Changed += OnInventoryChanged;
            Shop.AnyPurchased += OnPurchased;
            PauseMenu.PauseChanged += OnPauseChanged;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;

            if (inventory != null) inventory.Changed -= OnInventoryChanged;
            Shop.AnyPurchased -= OnPurchased;
            PauseMenu.PauseChanged -= OnPauseChanged;
            Instance = null;
        }

        private void Update()
        {
            // Unscaled time, so a change made just before pausing is still written.
            if (dirty && Time.unscaledTime >= saveAt) SaveNow();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) SaveIfChanged();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveIfChanged();
        }

        private void OnApplicationQuit()
        {
            SaveIfChanged();
        }

        /// <summary>Asks for a save shortly, e.g. after the player's progress changed.</summary>
        public void MarkChanged()
        {
            if (!dirty) saveAt = Time.unscaledTime + saveDelay;
            dirty = true;
        }

        public void SaveIfChanged()
        {
            if (dirty) SaveNow();
        }

        /// <summary>Writes the save file now. Returns false if it couldn't (the reason is logged).</summary>
        public bool SaveNow()
        {
            // Never overwrite a save that hasn't been read yet.
            if (!loaded || inventory == null) return false;

            var data = new SaveData
            {
                version = CurrentVersion,
                savedAtUtc = DateTime.UtcNow.ToString("o"),
                inventory = inventory.ToSaveData(),
                itemStats = inventory.StatsToSaveData(),
                shopPurchases = shop != null ? shop.ToSaveData() : new List<Shop.SavedPurchase>(),
            };

            try
            {
                WriteReplacing(SavePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveManager] Couldn't write '{SavePath}' ({e.Message}); trying again in {RetryDelay} s.", this);
                dirty = true;
                saveAt = Time.unscaledTime + RetryDelay;
                return false;
            }

            dirty = false;
            return true;
        }

        private void Load()
        {
            var data = Read(SavePath) ?? Read(BackupPath);
            if (data != null)
            {
                if (data.version > CurrentVersion)
                    Debug.LogWarning($"[SaveManager] The save comes from a newer version of the game ({data.version}); loading what this version knows.", this);

                inventory.LoadSaveData(data.inventory);
                inventory.LoadStatsSaveData(data.itemStats);
                if (shop != null) shop.LoadSaveData(data.shopPurchases);
                Debug.Log($"[SaveManager] Loaded '{SavePath}'.", this);
            }
            else if (File.Exists(SavePath))
            {
                // Unreadable: keep a copy before the next save replaces it, so it can still be recovered by hand.
                try { File.Copy(SavePath, SavePath + ".damaged", true); }
                catch (Exception) { }
                Debug.LogWarning($"[SaveManager] The save couldn't be read, so the game starts fresh. A copy was kept as '{SavePath}.damaged'.", this);
            }

            loaded = true;
        }

        private SaveData Read(string path)
        {
            if (!File.Exists(path)) return null;

            try
            {
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                if (data != null) return data;
                Debug.LogWarning($"[SaveManager] '{path}' is empty; skipped.", this);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveManager] '{path}' is damaged ({e.Message}); skipped.", this);
            }
            return null;
        }

        // Writes a temporary file first and then swaps it in, keeping the previous save as a backup,
        // so a crash or power cut in the middle of writing can't leave a half-written save.
        private static void WriteReplacing(string path, string contents)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            File.WriteAllText(temp, contents);

            if (!File.Exists(path))
            {
                File.Move(temp, path);
                return;
            }

            try
            {
                File.Replace(temp, path, path + ".bak");
            }
            catch (PlatformNotSupportedException)
            {
                File.Copy(path, path + ".bak", true);
                File.Delete(path);
                File.Move(temp, path);
            }
        }

        private void OnInventoryChanged(ItemDefinition item, int change, int newCount)
        {
            MarkChanged();
        }

        private void OnPurchased(Shop source, ItemDefinition item, int price, GameObject spawned)
        {
            MarkChanged();
        }

        private void OnPauseChanged()
        {
            if (PauseMenu.IsPaused) SaveIfChanged();
        }
    }
}
