using UnityEditor;

namespace Game.EditorTools
{
    /// <summary>Steam menu commands (Tools > 429 Game > Steam).</summary>
    public static class SteamAchievementsSetupMenu
    {
        /// <summary>
        /// Tools > 429 Game > Steam > Reset My Achievements And Stats (in Play mode, with Steam running): takes back every
        /// achievement of this game and sets its stats to 0, for the Steam account signed in on this PC only. For testing.
        /// </summary>
        [MenuItem("Tools/429 Game/Steam/Reset My Achievements And Stats")]
        public static void ResetMine()
        {
            if (!EditorUtility.DisplayDialog("Reset Steam achievements",
                    "Take back every achievement of this game and set all its stats to 0, for the Steam account signed in on this PC?",
                    "Reset", "Cancel")) return;
            if (!Game.Steam.SteamAchievements.ResetAll())
                UnityEngine.Debug.LogWarning("[Steam] Not reset: Steam isn't running in this Play session.");
        }

        [MenuItem("Tools/429 Game/Steam/Reset My Achievements And Stats", true)]
        private static bool CanReset() => EditorApplication.isPlaying && Game.Steam.SteamAchievements.IsAvailable;
    }
}
