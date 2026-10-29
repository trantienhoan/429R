using UnityEngine;

namespace Game.Menus
{
    /// <summary>
    /// Remembers which tutorials the player has seen, for this install of the game: a new install shows them again, and so
    /// does starting a new save (Forget For New Save). On a PC build it's a small file in the game's folder (it goes when
    /// the game is uninstalled; settings in the registry stay); on Quest and in the editor it's the app's settings
    /// (PlayerPrefs; a Quest uninstall wipes them). Tools > 429 Game > Menu > Show Tutorials Again forgets them in the
    /// editor.
    /// </summary>
    public static class ShowOnce
    {
        /// <summary>Where the settings keep them, before the key.</summary>
        public const string PrefsPrefix = "429 Game.Tutorial.";

        /// <summary>Every key used, so they can all be forgotten.</summary>
        public static readonly string[] Keys = { "StartPumpkinTutorial", "WalkTutorialSigns" };

        /// <summary>Whether this tutorial was seen on this install.</summary>
        public static bool WasShown(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
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

        /// <summary>This tutorial has been seen: not again on this install.</summary>
        public static void Remember(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
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
        /// A new save was started (an empty save slot picked; user, 2026-10-29: "everytime Player start a new save, the
        /// tutorial pop up should on"): every tutorial shows again, the tutorial notices and the walk signs (also if
        /// they're up already).
        /// </summary>
        public static void ForgetForNewSave()
        {
            ForgetAll();
            HutongGames.PlayMaker.Actions.ShowNotice.ForgetShown();
            OncePerInstall.ShowAgain();
        }

        /// <summary>Forgets every tutorial, so they all show again.</summary>
        public static void ForgetAll()
        {
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
