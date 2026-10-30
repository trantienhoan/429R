using System.Collections.Generic;
using UnityEngine;

namespace Game.Menus
{
    /// <summary>
    /// Remembers which tutorials the player has seen, for each save slot (user, 2026-10-30: they open every time the
    /// player starts a NEW save slot, not every time a new game starts): the save keeps them (Save Manager), so a new save
    /// shows them all, and a slot that has seen one doesn't show it again, also after a new run in it (Re-Dream, beating
    /// the game, dying with no revive left). Saves from before this are taken to have seen every tutorial. Before any slot
    /// is read, the install's own memory decides, as before: on a PC build a small file in the game's folder (it goes when
    /// the game is uninstalled), on Quest and in the editor the app's settings (PlayerPrefs; a Quest uninstall wipes them).
    /// Tools > 429 Game > Menu > Show Tutorials Again forgets them (in Play mode, also the slot being played).
    /// </summary>
    public static class ShowOnce
    {
        /// <summary>Where the settings keep them, before the key.</summary>
        public const string PrefsPrefix = "429 Game.Tutorial.";

        /// <summary>Every key used, so they can all be forgotten.</summary>
        public static readonly string[] Keys = { "StartPumpkinTutorial", "WalkTutorialSigns" };

        /// <summary>Raised when the save slot being played has seen another tutorial (the save writes it).</summary>
        public static event System.Action Changed;

        // The tutorials the save slot being played has seen; null while no slot has been read.
        private static HashSet<string> slotSeen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            slotSeen = null;
            Changed = null;
        }

        /// <summary>Whether this tutorial was seen: in the save slot being played (before one is read: on this install).</summary>
        public static bool WasShown(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (slotSeen != null) return slotSeen.Contains(key);
            var marker = Marker(key);
            try
            {
                if (marker != null && System.IO.File.Exists(marker)) return true;
            }
            catch (System.Exception)
            {
                // Can't look there: the settings below decide.
            }
            return PlayerPrefs.GetInt(PrefsPrefix + key, 0) != 0;
        }

        /// <summary>This tutorial has been seen: not again in this save slot (before one is read: on this install).</summary>
        public static void Remember(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (slotSeen != null)
            {
                if (slotSeen.Add(key)) Changed?.Invoke();
                return;
            }
            var marker = Marker(key);
            if (marker != null)
            {
                try
                {
                    System.IO.File.WriteAllText(marker, "seen");
                    return;
                }
                catch (System.Exception)
                {
                    // The game's folder can't be written to: remembered in the settings instead.
                }
            }
            PlayerPrefs.SetInt(PrefsPrefix + key, 1);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// A save slot was read (Save Manager): the tutorials its player has seen, none for a new save, or null for a save
        /// from before they were kept (every one). Tutorial notices and the walk signs that are up already go by it too.
        /// </summary>
        public static void UseSlot(IEnumerable<string> seen)
        {
            slotSeen = seen != null ? new HashSet<string>(seen) : new HashSet<string>(Keys);
            HutongGames.PlayMaker.Actions.ShowNotice.ForgetShown();
            OncePerInstall.Recheck();
        }

        /// <summary>The tutorials the save slot being played has seen, for its save.</summary>
        public static List<string> SlotToSaveData() => slotSeen != null ? new List<string>(slotSeen) : new List<string>();

        /// <summary>Forgets every tutorial, so they all show again (this install, and the save slot being played).</summary>
        public static void ForgetAll()
        {
            if (slotSeen != null && slotSeen.Count > 0)
            {
                slotSeen.Clear();
                Changed?.Invoke();
            }
            foreach (var key in Keys)
            {
                PlayerPrefs.DeleteKey(PrefsPrefix + key);
                var marker = Marker(key);
                try
                {
                    if (marker != null && System.IO.File.Exists(marker)) System.IO.File.Delete(marker);
                }
                catch (System.Exception)
                {
                    // Nothing more to do.
                }
            }
            PlayerPrefs.Save();
        }

        // On a PC build: a file next to the game's data, which goes when the game is uninstalled. Elsewhere none.
        private static string Marker(string key)
        {
#if UNITY_STANDALONE && !UNITY_EDITOR
            return System.IO.Path.Combine(Application.dataPath, "Seen_" + key + ".txt");
#else
            return null;
#endif
        }
    }
}
