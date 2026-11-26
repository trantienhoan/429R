using System;
using System.Collections.Generic;
using Game.Leveling;
using Game.Saving;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// The Knight's armor the player has found this run (user 2026-11-26, Knight Armor Settings): its armor is taken by
    /// hits before health (the player's damage action, Float Subtract Random with Player Toughness, calls Absorb), and each
    /// piece adds run points to its stat (Player Level's run points, like a glove's). Kept with the run through
    /// checkpoints; a new run (or Reset Progress) takes it all off, and everything not found hides again.
    /// </summary>
    public static class KnightArmor
    {
        private static readonly List<string> found = new();

        /// <summary>Armor left now (taken by hits before health).</summary>
        public static float Armor { get; private set; }

        /// <summary>All the armor the found pieces give (Armor when nothing has hit it yet).</summary>
        public static float MaxArmor { get; private set; }

        /// <summary>The ids of the pieces found this run.</summary>
        public static IReadOnlyList<string> Found => found;

        /// <summary>Anything here changed (the HUD).</summary>
        public static event Action Changed;

        /// <summary>A hit took this much armor.</summary>
        public static event Action<float> TookHit;

        /// <summary>A hit took the last of the armor.</summary>
        public static event Action Broke;

        /// <summary>A piece was put on.</summary>
        public static event Action<KnightArmorSettings.Piece> Equipped;

        /// <summary>The run's armor was set anew (a new run, or the save read): what isn't found hides again.</summary>
        public static event Action RunSet;

        /// <summary>One of her lost things was found: how many now, of how many.</summary>
        public static event Action<int, int> KeepsakeFound;

        public static bool Has(string id) => found.Contains(id);

        /// <summary>Her lost things found this run.</summary>
        public static int KeepsakesFound
        {
            get
            {
                int count = 0;
                foreach (var id in found) if (id.StartsWith(KnightArmorSettings.KeepsakePrefix)) count++;
                return count;
            }
        }

        /// <summary>One of her lost things is found (kept with the run). False if it already was.</summary>
        public static bool Collect(string id)
        {
            if (string.IsNullOrEmpty(id) || !id.StartsWith(KnightArmorSettings.KeepsakePrefix) || found.Contains(id)) return false;
            found.Add(id);
            Checkpoints.RunChanged();
            var settings = KnightArmorSettings.Instance;
            KeepsakeFound?.Invoke(KeepsakesFound, settings != null ? settings.KeepsakeCount : KeepsakesFound);
            return true;
        }

        /// <summary>
        /// A hit on the player of this much (after Toughness): the armor takes what it can, the rest is returned for
        /// health. No armor: all of it is returned.
        /// </summary>
        public static float Absorb(float damage)
        {
            if (damage <= 0f || Armor <= 0f) return damage;
            float taken = Mathf.Min(damage, Armor);
            Armor -= taken;
            if (Armor < 0.01f) Armor = 0f;
            TookHit?.Invoke(taken);
            if (Armor <= 0f) Broke?.Invoke();
            Changed?.Invoke();
            return damage - taken;
        }

        /// <summary>The run points the found pieces give a stat (Player Level adds them like a glove's).</summary>
        public static float PointsFor(Stat stat)
        {
            if (found.Count == 0) return 0f;
            var settings = KnightArmorSettings.Instance;
            if (settings == null) return 0f;
            float points = 0f;
            foreach (var id in found)
            {
                var piece = settings.Find(id);
                if (piece != null && piece.stat == stat) points += piece.points;
            }
            return points;
        }

        /// <summary>Puts a piece on: its armor and its stat for the rest of the run. False if unknown or already on.</summary>
        public static bool Equip(string id)
        {
            var piece = KnightArmorSettings.Instance != null ? KnightArmorSettings.Instance.Find(id) : null;
            if (piece == null || found.Contains(piece.id)) return false;
            found.Add(piece.id);
            Armor += piece.armor;
            MaxArmor += piece.armor;
            // Its stat counts now; a raised health cap heals by as much (as a glove's does).
            PlayerLevel.GlovesChanged(true);
            Equipped?.Invoke(piece);
            Changed?.Invoke();
            Checkpoints.RunChanged();
            return true;
        }

        /// <summary>The found pieces for the save ("Helmet,Boots"; "" = none).</summary>
        public static string SaveIds => string.Join(",", found);

        /// <summary>The run's armor as the save kept it (Checkpoints). Armor Left below 0: all of it.</summary>
        public static void Load(string ids, float armorLeft)
        {
            Clear();
            var settings = KnightArmorSettings.Instance;
            if (!string.IsNullOrEmpty(ids) && settings != null)
            {
                foreach (var id in ids.Split(','))
                {
                    // Her lost things found this run come back with it too.
                    if (id.Trim().StartsWith(KnightArmorSettings.KeepsakePrefix))
                    {
                        if (!found.Contains(id.Trim())) found.Add(id.Trim());
                        continue;
                    }
                    var piece = settings.Find(id.Trim());
                    if (piece == null || found.Contains(piece.id)) continue;
                    found.Add(piece.id);
                    MaxArmor += piece.armor;
                }
            }
            Armor = armorLeft >= 0f ? Mathf.Min(armorLeft, MaxArmor) : MaxArmor;
            PlayerLevel.GlovesChanged(false);
            Changed?.Invoke();
            RunSet?.Invoke();
        }

        /// <summary>A new run (or Reset Progress): no armor; everything hides again.</summary>
        public static void NewRun()
        {
            Clear();
            PlayerLevel.GlovesChanged(false);
            Changed?.Invoke();
            RunSet?.Invoke();
        }

        private static void Clear()
        {
            found.Clear();
            Armor = 0f;
            MaxArmor = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Clear();
    }
}
