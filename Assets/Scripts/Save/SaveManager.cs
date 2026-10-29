using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Game.Inventory;
using Game.Leveling;
using Game.Menus;
using Game.Shopping;
using UnityEngine;

namespace Game.Saving
{
    /// <summary>
    /// Keeps the player's progress between sessions, in three save slots (Saves/&lt;Profile&gt;/slot1.json to slot3.json):
    /// what they carry (e.g. candies), how many of each item they ever collected and spent, what they bought, what they
    /// unlocked for good (e.g. paths opened with seeds), lasting boosts (e.g. the higher health cap from Boss Seeds, see
    /// PlayerBoosts), their level, EXP and stat points (see Player Level), the checkpoint they reached this run (see
    /// Checkpoints) and the bonus round's leaderboard (see BonusScores). Keep one in the scene.
    /// Nothing is read until a slot is picked: the Save Slot Menu asks as the game starts (ChooseSlot), and the slot is
    /// remembered until the game closes, so Re-Dream (which reloads the scene) goes straight back into it. Picking a slot
    /// with a save goes on from its last checkpoint. After that the game saves at once at every checkpoint (with the
    /// small "Saved" sign), shortly after anything changes, when the game pauses and when it closes. Dying takes all the
    /// Wraps (Candy: the PlayerDeath FSM's "Inventory Remove All", even when a lucky clover gets the player up); the other
    /// items are kept through a restart. The save from before there were slots (save.json) is
    /// left on disk as a backup and never read or written again.
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
            // What the player unlocked for good, e.g. paths bought open with seeds (see Unlocks).
            public List<string> unlocks = new();
            // How much Boss Seeds have raised the health cap (see PlayerBoosts).
            public float healthCapBonus;
            // The player's level, EXP and stat points (see Player Level).
            public PlayerLevel.SavedLevel level = new();
            // The checkpoint reached this run, the revives left and the health kept by Get a Rest (see Checkpoints).
            public Checkpoints.SavedCheckpoint checkpoint = new();
            // The bonus round's leaderboard, best first (see BonusScores).
            public List<BonusScores.Entry> bonusScores = new();
        }

        /// <summary>What the start menu shows about a save slot.</summary>
        public struct SlotInfo
        {
            public int Slot;
            /// <summary>The slot holds a save that can be read.</summary>
            public bool HasSave;
            /// <summary>A file is there but can't be read: picking the slot starts fresh (a copy is kept as .damaged).</summary>
            public bool Damaged;
            public int Level;
            /// <summary>The last checkpoint reached ("" = the very start).</summary>
            public string Checkpoint;
            /// <summary>When it was last saved, in local time (DateTime.MinValue: unknown).</summary>
            public DateTime SavedAt;
        }

        /// <summary>How many save slots each player has.</summary>
        public const int SlotCount = 3;

        private const int CurrentVersion = 1;
        private const string LegacyFileName = "save.json";
        private const string DefaultProfile = "Local";
        // In a Steam player's folder: the Local folder's slots were already checked for copying in (see UseProfile).
        private const string LocalCheckedFileName = "local_slots_checked.txt";
        private const float RetryDelay = 5f;
        private static readonly string[] FileSuffixes = { "", ".bak", ".tmp", ".damaged" };

        [Tooltip("Left empty, PlayerInventory.Instance is used.")]
        [SerializeField] private PlayerInventory inventory;
        [Tooltip("Left empty, the shop in the scene is used.")]
        [SerializeField] private Shop shop;
        [Tooltip("Seconds from a change to writing the file, so a burst of pickups is written once. (Checkpoints save at once.)")]
        [Min(0f)]
        [SerializeField] private float saveDelay = 1.5f;

        private bool loaded;
        private bool dirty;
        private float saveAt;
        private bool subscribed;

        // The slot whose save was last read this session (the unlocks start empty when another one is read).
        private static int lastReadSlot;

        public static SaveManager Instance { get; private set; }

        /// <summary>
        /// The current player's folder under Saves. "Local" until Steam is set up; the Steam integration
        /// sets it to the player's Steam ID before the scene loads, so each account gets its own saves.
        /// </summary>
        public static string Profile { get; set; } = DefaultProfile;

        /// <summary>
        /// The save slot being played (1 to SlotCount), or 0 while none has been picked. Kept until the game closes, also
        /// when the scene reloads (Re-Dream).
        /// </summary>
        public static int CurrentSlot { get; private set; }

        /// <summary>Whether the picked slot's save has been read in this scene (the game only saves after that).</summary>
        public static bool IsLoaded => Instance != null && Instance.loaded;

        /// <summary>Raised when a save slot has been picked and read (also an empty one), with the slot's number.</summary>
        public static event Action<int> SlotChosen;

        /// <summary>Raised after the game saved because the player reached a checkpoint (the "Saved" sign shows).</summary>
        public static event Action CheckpointSaved;

        /// <summary>The folder holding every player's save folder ("Local", and one per Steam ID).</summary>
        public static string SavesRoot => Path.Combine(Application.persistentDataPath, "Saves");
        public static string SaveFolder => Path.Combine(SavesRoot, Profile);

        /// <summary>A save slot's file (slot 1 to SlotCount) in the current player's folder.</summary>
        public static string SlotPath(int slot) => Path.Combine(SaveFolder, SlotFileName(slot));

        /// <summary>The file of the slot being played (slot 1's while none has been picked).</summary>
        public static string SavePath => SlotPath(CurrentSlot > 0 ? CurrentSlot : 1);

        /// <summary>The single save from before there were slots: left on disk as a backup, never read or written.</summary>
        public static string LegacySavePath => Path.Combine(SaveFolder, LegacyFileName);

        public static bool IsValidSlot(int slot) => slot >= 1 && slot <= SlotCount;

        private static string SlotFileName(int slot) => $"slot{slot}.json";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            Profile = DefaultProfile;
            CurrentSlot = 0;
            lastReadSlot = 0;
            SlotChosen = null;
            CheckpointSaved = null;
        }

        /// <summary>
        /// Switches to another player's save folder, e.g. their Steam ID. Call it before the scene loads.
        /// Only the first time that folder is used (it has no slot saves yet), the slot saves made before in the Local
        /// folder are copied over, so earlier progress is kept. A small file in the folder (local_slots_checked.txt)
        /// remembers it's done, so slots the player deletes later (Delete Save, Reset Progress) don't come back.
        /// </summary>
        public static void UseProfile(string profile)
        {
            if (string.IsNullOrEmpty(profile)) return;

            string localFolder = Path.Combine(SavesRoot, DefaultProfile);
            Profile = profile;
            string marker = Path.Combine(SaveFolder, LocalCheckedFileName);
            if (profile == DefaultProfile || File.Exists(marker)) return;

            try
            {
                Directory.CreateDirectory(SaveFolder);
                if (!HasSlotFiles(SaveFolder) && HasSlotFiles(localFolder))
                {
                    for (int slot = 1; slot <= SlotCount; slot++)
                    {
                        foreach (var suffix in new[] { "", ".bak" })
                        {
                            string from = Path.Combine(localFolder, SlotFileName(slot) + suffix);
                            if (File.Exists(from)) File.Copy(from, Path.Combine(SaveFolder, SlotFileName(slot) + suffix));
                        }
                    }
                    Debug.Log($"[SaveManager] Copied the save slots in '{localFolder}' to '{SaveFolder}'.");
                }
                // Checked (copied or not): never again for this player.
                File.WriteAllText(marker, "The save slots in the Local folder were checked and copied in if there were any (once per player).");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveManager] Couldn't copy the save slots in '{localFolder}' to '{SaveFolder}' (tried again next time): {e.Message}");
            }
        }

        private static bool HasSlotFiles(string folder)
        {
            for (int slot = 1; slot <= SlotCount; slot++)
            {
                string path = Path.Combine(folder, SlotFileName(slot));
                if (File.Exists(path) || File.Exists(path + ".bak")) return true;
            }
            return false;
        }

        /// <summary>
        /// Picks the save slot to play (1 to SlotCount) and reads it: its save (going on from its last checkpoint), or a
        /// fresh start if it's empty. The Save Slot Menu calls it. Once per scene; the slot is then remembered until the
        /// game closes. False if the slot isn't 1 to SlotCount or a slot was already read in this scene.
        /// </summary>
        public static bool ChooseSlot(int slot)
        {
            if (!IsValidSlot(slot))
            {
                Debug.LogWarning($"[SaveManager] There is no save slot {slot} (1 to {SlotCount}).");
                return false;
            }
            if (Instance != null) return Instance.ReadSlot(slot);

            // No Save Manager in the scene: nothing to read, but the choice still counts.
            CurrentSlot = slot;
            SlotChosen?.Invoke(slot);
            return true;
        }

        /// <summary>
        /// Forgets which slot is being played, so the start menu asks again the next time the scene loads (Reset
        /// Progress does it).
        /// </summary>
        public static void ForgetSlot()
        {
            CurrentSlot = 0;
            lastReadSlot = 0;
        }

        /// <summary>What a save slot holds, for the start menu: whether it has a save, the level, the last checkpoint and when.</summary>
        public static SlotInfo SlotSummary(int slot)
        {
            var info = new SlotInfo { Slot = slot, Checkpoint = "", SavedAt = DateTime.MinValue };
            if (!IsValidSlot(slot)) return info;

            string path = SlotPath(slot);
            var data = ReadFile(path, null, false) ?? ReadFile(path + ".bak", null, false);
            if (data == null)
            {
                info.Damaged = File.Exists(path) || File.Exists(path + ".bak");
                return info;
            }

            info.HasSave = true;
            info.Level = data.level != null ? Mathf.Max(0, data.level.level) : 0;
            info.Checkpoint = data.checkpoint != null && data.checkpoint.checkpoint != null ? data.checkpoint.checkpoint : "";
            if (!string.IsNullOrEmpty(data.savedAtUtc) &&
                DateTime.TryParse(data.savedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at))
                info.SavedAt = at.Kind == DateTimeKind.Local ? at : at.ToLocalTime();
            return info;
        }

        /// <summary>
        /// Deletes a save slot (its file and backups), e.g. Delete Save in the start menu. Deleted while the game runs,
        /// Steam Cloud deletes its copy too. Not the slot being played (Reset Progress empties that one). False if it
        /// couldn't (the reason is logged).
        /// </summary>
        public static bool DeleteSlot(int slot)
        {
            if (!IsValidSlot(slot)) return false;
            if (slot == CurrentSlot && IsLoaded)
            {
                Debug.LogWarning($"[SaveManager] Slot {slot} is being played; use Reset Progress to empty it.");
                return false;
            }

            try
            {
                int deleted = DeleteSlotFiles(Profile, slot);
                Debug.Log($"[SaveManager] Deleted save slot {slot} ({deleted} file(s) in '{SaveFolder}').");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveManager] Couldn't delete save slot {slot} in '{SaveFolder}': {e.Message}");
                return false;
            }
        }

        /// <summary>Deletes the save slot being played (with its backups). Returns how many files were deleted.</summary>
        public static int DeleteSaveFiles()
        {
            return CurrentSlot > 0 ? DeleteSlotFiles(Profile, CurrentSlot) : 0;
        }

        /// <summary>
        /// Deletes every save slot on this computer (Local and each Steam ID folder), with their backups. Deleting only
        /// one isn't enough: a new Steam player (no local_slots_checked.txt yet) gets the Local ones copied in. The old
        /// save.json backups are left alone. Returns how many files were deleted.
        /// </summary>
        public static int DeleteAllSaveFiles()
        {
            if (!Directory.Exists(SavesRoot)) return 0;

            int deleted = 0;
            foreach (var folder in Directory.GetDirectories(SavesRoot))
            {
                for (int slot = 1; slot <= SlotCount; slot++) deleted += DeleteSlotFiles(Path.GetFileName(folder), slot);
            }
            return deleted;
        }

        private static int DeleteSlotFiles(string profile, int slot)
        {
            int deleted = 0;
            string path = Path.Combine(SavesRoot, profile, SlotFileName(slot));
            foreach (var suffix in FileSuffixes)
            {
                if (!File.Exists(path + suffix)) continue;
                File.Delete(path + suffix);
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
            // A fresh scene: GAMESTAGES' Go To Checkpoint waits until the slot is read again.
            Checkpoints.ForgetLoaded();
        }

        // In Start, so the inventory has set itself up in Awake first.
        private void Start()
        {
            if (Instance != this) return;

            FindParts();
            if (inventory == null)
                Debug.LogWarning("[SaveManager] There is no PlayerInventory in the scene, so nothing is saved.", this);
            // Already picked (e.g. the start menu's Editor Start Slot, on the first frame).
            if (loaded) return;

            // The scene reloaded (Re-Dream): the slot picked earlier goes on at once, without the start menu.
            if (CurrentSlot > 0)
            {
                ReadSlot(CurrentSlot);
                return;
            }

            // No start menu in the scene: slot 1, so the game doesn't wait for a choice nobody can make.
            if (FindAnyObjectByType<SaveSlotMenu>() == null)
            {
                Debug.LogWarning("[SaveManager] There's no Save Slot Menu in the scene, so save slot 1 is used. " +
                                 "Run Tools > 429 Game > Menu > Set Up Save Slot Menu.", this);
                ReadSlot(1);
            }
        }

        private void OnDestroy()
        {
            if (Instance != this) return;

            if (subscribed)
            {
                if (inventory != null) inventory.Changed -= OnInventoryChanged;
                Shop.AnyPurchased -= OnPurchased;
                Unlocks.Changed -= MarkChanged;
                PlayerBoosts.Changed -= MarkChanged;
                PlayerLevel.Changed -= MarkChanged;
                Checkpoints.Changed -= MarkChanged;
                Checkpoints.Reached -= OnCheckpointReached;
                PauseMenu.PauseChanged -= OnPauseChanged;
            }
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

        /// <summary>Asks for a save shortly, e.g. after the player's progress changed. Nothing before a slot is read.</summary>
        public void MarkChanged()
        {
            if (!loaded) return;
            if (!dirty) saveAt = Time.unscaledTime + saveDelay;
            dirty = true;
        }

        public void SaveIfChanged()
        {
            if (dirty) SaveNow();
        }

        /// <summary>
        /// Empties the save slot being played: what the player carries, what they ever collected and spent, what they
        /// bought, what they unlocked, and their level. The slot's files are deleted (Steam Cloud deletes its copy too, as
        /// the game is running), nothing more is saved, and the slot is forgotten, so the start menu asks for a slot again.
        /// The other slots are kept. Restart the scene afterwards so the world matches, e.g. paths opened with seeds.
        /// </summary>
        public void ResetProgress()
        {
            if (inventory != null)
            {
                inventory.LoadSaveData(null);
                inventory.LoadStatsSaveData(null);
            }
            if (shop != null) shop.LoadSaveData(null);
            Unlocks.Reset();
            PlayerBoosts.Reset();
            PlayerLevel.Load(null);
            Checkpoints.Reset();
            BonusScores.Reset();

            int slot = CurrentSlot;
            if (slot > 0)
            {
                try
                {
                    DeleteSlotFiles(Profile, slot);
                    Debug.Log($"[SaveManager] Progress reset: save slot {slot} is empty now.", this);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SaveManager] Couldn't delete save slot {slot} ({e.Message}).", this);
                }
            }
            // Nothing is written until a slot is picked again (after the scene restarts).
            loaded = false;
            dirty = false;
            ForgetSlot();
        }

        /// <summary>Writes the save slot now. Returns false if it couldn't (no slot read yet, or the reason is logged).</summary>
        public bool SaveNow()
        {
            // Never overwrite a save that hasn't been read yet.
            if (!loaded || inventory == null || !IsValidSlot(CurrentSlot)) return false;

            var data = new SaveData
            {
                version = CurrentVersion,
                savedAtUtc = DateTime.UtcNow.ToString("o"),
                inventory = inventory.ToSaveData(),
                itemStats = inventory.StatsToSaveData(),
                shopPurchases = shop != null ? shop.ToSaveData() : new List<Shop.SavedPurchase>(),
                unlocks = Unlocks.ToSaveData(),
                healthCapBonus = PlayerBoosts.HealthCapBonus,
                level = PlayerLevel.Save(),
                checkpoint = Checkpoints.ToSaveData(),
                bonusScores = BonusScores.ToSaveData(),
            };

            string path = SlotPath(CurrentSlot);
            try
            {
                WriteReplacing(path, JsonUtility.ToJson(data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveManager] Couldn't write '{path}' ({e.Message}); trying again in {RetryDelay} s.", this);
                dirty = true;
                saveAt = Time.unscaledTime + RetryDelay;
                return false;
            }

            dirty = false;
            return true;
        }

        private void FindParts()
        {
            if (inventory == null) inventory = PlayerInventory.Instance;
            if (shop == null) shop = Shop.Main;
        }

        private bool ReadSlot(int slot)
        {
            if (loaded)
            {
                Debug.LogWarning($"[SaveManager] Save slot {CurrentSlot} is already being played; slot {slot} wasn't read.", this);
                return false;
            }

            FindParts();
            CurrentSlot = slot;
            Load();
            Subscribe();
            SlotChosen?.Invoke(slot);
            // Written straight away: the slot shows as used in the start menu, and Get a Rest's health is given back once.
            SaveNow();
            return true;
        }

        // Saves when the player's progress changes. After loading, so loading itself doesn't count as a change.
        private void Subscribe()
        {
            if (subscribed) return;
            subscribed = true;
            if (inventory != null) inventory.Changed += OnInventoryChanged;
            Shop.AnyPurchased += OnPurchased;
            Unlocks.Changed += MarkChanged;
            PlayerBoosts.Changed += MarkChanged;
            PlayerLevel.Changed += MarkChanged;
            Checkpoints.Changed += MarkChanged;
            Checkpoints.Reached += OnCheckpointReached;
            BonusScores.Changed += MarkChanged;
            PauseMenu.PauseChanged += OnPauseChanged;
        }

        private void Load()
        {
            string path = SlotPath(CurrentSlot);
            var data = ReadFile(path, this, true) ?? ReadFile(path + ".bak", this, true);
            if (data != null)
            {
                if (data.version > CurrentVersion)
                    Debug.LogWarning($"[SaveManager] The save comes from a newer version of the game ({data.version}); loading what this version knows.", this);

                if (inventory != null)
                {
                    inventory.LoadSaveData(data.inventory);
                    inventory.LoadStatsSaveData(data.itemStats);
                }
                if (shop != null) shop.LoadSaveData(data.shopPurchases);
                Debug.Log($"[SaveManager] Loaded save slot {CurrentSlot} ('{path}').", this);
            }
            else if (File.Exists(path))
            {
                // Unreadable: keep a copy before the next save replaces it, so it can still be recovered by hand.
                try { File.Copy(path, path + ".damaged", true); }
                catch (Exception) { }
                Debug.LogWarning($"[SaveManager] Save slot {CurrentSlot} couldn't be read, so it starts fresh. A copy was kept as '{path}.damaged'.", this);
            }
            else
            {
                Debug.Log($"[SaveManager] Save slot {CurrentSlot} is empty: a new game.", this);
            }
            // A new game (also one that starts fresh): its player hasn't seen the tutorials yet, so they show again.
            if (data == null) ShowOnce.ForgetForNewSave();

            // Another slot than the one read before this session (after Reset Progress): its unlocks start empty, rather
            // than adding to the other slot's.
            if (lastReadSlot != CurrentSlot) Unlocks.Reset();
            lastReadSlot = CurrentSlot;

            loaded = true;
            // The level first: its Extra Revive bonus counts in the checkpoint's revives.
            PlayerLevel.Load(data?.level);
            // Before anything else reads it: GAMESTAGES' Go To Checkpoint is waiting for it to know where to go on from.
            Checkpoints.LoadSaveData(data?.checkpoint);
            // Also with no save, so whatever waits for the unlocks knows they're read.
            Unlocks.LoadSaveData(data?.unlocks);
            PlayerBoosts.LoadSaveData(data != null ? data.healthCapBonus : 0f);
            BonusScores.LoadSaveData(data?.bonusScores);

            // The health the player had when they picked Get a Rest, once the cap bonuses are in (they heal).
            float rested = Checkpoints.TakeRestHealth();
            if (rested > 0f && PlayerBoosts.SetHealth(rested))
                Debug.Log($"[SaveManager] Health back to {rested:0} (as at Get a Rest).", this);
        }

        private static SaveData ReadFile(string path, UnityEngine.Object context, bool logProblems)
        {
            if (!File.Exists(path)) return null;

            try
            {
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                if (data != null) return data;
                if (logProblems) Debug.LogWarning($"[SaveManager] '{path}' is empty; skipped.", context);
            }
            catch (Exception e)
            {
                if (logProblems) Debug.LogWarning($"[SaveManager] '{path}' is damaged ({e.Message}); skipped.", context);
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

        // A new checkpoint: saved at once (not a moment later), and the "Saved" sign shows.
        private void OnCheckpointReached(string checkpoint)
        {
            if (SaveNow()) CheckpointSaved?.Invoke();
        }

        private void OnPauseChanged()
        {
            if (PauseMenu.IsPaused) SaveIfChanged();
        }
    }
}
