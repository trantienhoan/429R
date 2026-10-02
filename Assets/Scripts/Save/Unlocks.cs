using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Saving
{
    /// <summary>
    /// Things the player has unlocked for good, by name, e.g. "Cube_1" for the first path bought open with seeds. The
    /// Save Manager keeps them in the save file, so they're still unlocked the next time the game is played.
    /// </summary>
    public static class Unlocks
    {
        private static readonly HashSet<string> unlocked = new();

        /// <summary>Raised when something new is unlocked.</summary>
        public static event Action Changed;

        /// <summary>Raised once the save has been read (also when there was none).</summary>
        public static event Action Loaded;

        /// <summary>Whether the save has been read yet; until then nothing counts as unlocked.</summary>
        public static bool IsLoaded { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            unlocked.Clear();
            IsLoaded = false;
            Changed = null;
            Loaded = null;
        }

        public static bool Has(string id)
        {
            return !string.IsNullOrEmpty(id) && unlocked.Contains(id);
        }

        /// <summary>Unlocks it for good (saved shortly after).</summary>
        public static void Add(string id)
        {
            if (string.IsNullOrEmpty(id) || !unlocked.Add(id)) return;
            Changed?.Invoke();
        }

        public static List<string> ToSaveData()
        {
            var list = new List<string>(unlocked);
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        /// <summary>Adds the saved unlocks. Anything unlocked before the save was read is kept too.</summary>
        public static void LoadSaveData(IEnumerable<string> saved)
        {
            if (saved != null)
            {
                foreach (var id in saved)
                {
                    if (!string.IsNullOrEmpty(id)) unlocked.Add(id);
                }
            }

            IsLoaded = true;
            Loaded?.Invoke();
        }
    }
}
