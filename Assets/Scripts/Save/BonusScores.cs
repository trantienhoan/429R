using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Saving
{
    /// <summary>
    /// The bonus round's leaderboard: the player's best rounds (Kept of them), best first: the most pumpkins broken, then
    /// the most perfect jabs. The Save Manager keeps it in the save slot.
    /// </summary>
    public static class BonusScores
    {
        /// <summary>One bonus round.</summary>
        [Serializable]
        public class Entry
        {
            public int pumpkins;
            public int pumpkinCount;
            public int punches;
            public int perfectJabs;
            public string playedAtUtc;
        }

        /// <summary>How many rounds the leaderboard keeps.</summary>
        public const int Kept = 5;

        private static readonly List<Entry> best = new();

        /// <summary>Raised when a round makes the leaderboard.</summary>
        public static event Action Changed;

        /// <summary>The best rounds, best first.</summary>
        public static IReadOnlyList<Entry> Best => best;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            best.Clear();
            Changed = null;
        }

        /// <summary>
        /// Adds a round: its place on the leaderboard (0 = the best), or -1 if it didn't make it. A round as good as one
        /// already there goes after it.
        /// </summary>
        public static int Add(int pumpkins, int pumpkinCount, int punches, int perfectJabs)
        {
            var entry = new Entry
            {
                pumpkins = pumpkins,
                pumpkinCount = pumpkinCount,
                punches = punches,
                perfectJabs = perfectJabs,
                playedAtUtc = DateTime.UtcNow.ToString("o"),
            };
            int place = 0;
            while (place < best.Count && !Beats(entry, best[place])) place++;
            if (place >= Kept) return -1;
            best.Insert(place, entry);
            if (best.Count > Kept) best.RemoveRange(Kept, best.Count - Kept);
            Changed?.Invoke();
            return place;
        }

        private static bool Beats(Entry a, Entry b)
        {
            if (a.pumpkins != b.pumpkins) return a.pumpkins > b.pumpkins;
            return a.perfectJabs > b.perfectJabs;
        }

        public static List<Entry> ToSaveData() => new(best);

        /// <summary>Forgets the leaderboard (Reset Progress).</summary>
        public static void Reset() => best.Clear();

        /// <summary>The saved leaderboard (null: none; it's saved best first), in place of the one there.</summary>
        public static void LoadSaveData(IEnumerable<Entry> saved)
        {
            best.Clear();
            if (saved == null) return;
            foreach (var entry in saved)
            {
                if (entry != null && best.Count < Kept) best.Add(entry);
            }
        }
    }
}
