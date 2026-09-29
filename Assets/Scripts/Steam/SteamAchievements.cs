#if STEAMWORKS_NET && (UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX)
#define STEAM_BUILD
#endif
using UnityEngine;
#if STEAM_BUILD
using Steamworks;
#endif

namespace Game.Steam
{
    /// <summary>
    /// Unlocks Steam achievements and adds to Steam stats. Where Steam isn't running (the Quest build, or the Editor
    /// with Steam closed) it does nothing, so it's safe to call from anywhere. Achievements and stats must first be
    /// created and published on the Steamworks site; the names used here are their API names.
    /// FSMs use the actions in PlayMaker's "Steam" category.
    /// </summary>
    public static class SteamAchievements
    {
        /// <summary>True when Steam is running and achievements can be unlocked.</summary>
        public static bool IsAvailable
        {
            get
            {
#if STEAM_BUILD
                return SteamBootstrap.Initialized;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Unlocks an achievement and uploads it at once, so Steam's popup shows straight away.
        /// Returns false if Steam isn't running or there's no achievement with that API name.
        /// </summary>
        public static bool Unlock(string apiName)
        {
#if STEAM_BUILD
            if (!SteamBootstrap.Initialized || string.IsNullOrEmpty(apiName)) return false;
            if (SteamUserStats.GetAchievement(apiName, out bool unlocked) && unlocked) return true;

            if (!SteamUserStats.SetAchievement(apiName))
            {
                Debug.LogWarning($"[Steam] There's no achievement called '{apiName}'. Check its API name on the Steamworks site and that the change is published.");
                return false;
            }
            Store();
            Debug.Log($"[Steam] Achievement unlocked: {apiName}");
            return true;
#else
            return false;
#endif
        }

        /// <summary>
        /// Adds to an integer stat, e.g. +1 per bug squashed. An achievement that uses the stat as its progress unlocks
        /// by itself when the stat reaches its target. Stats upload every few seconds and when the game closes.
        /// </summary>
        public static bool AddToStat(string apiName, int amount)
        {
#if STEAM_BUILD
            if (!SteamBootstrap.Initialized || string.IsNullOrEmpty(apiName)) return false;

            if (!SteamUserStats.GetStat(apiName, out int value))
            {
                Debug.LogWarning($"[Steam] There's no stat called '{apiName}'. Check its API name on the Steamworks site, that it's an INT stat set by the client, and that the change is published.");
                return false;
            }
            if (!SteamUserStats.SetStat(apiName, value + amount)) return false;

            statsChanged = true;
            return true;
#else
            return false;
#endif
        }

#if STEAM_BUILD
        // Steam limits how often stats may be uploaded, so changes are gathered and sent at most this often.
        private const float StoreInterval = 5f;

        private static bool statsChanged;
        private static float nextStore;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            statsChanged = false;
            nextStore = 0f;
        }

        /// <summary>Called by SteamBootstrap every frame: uploads changed stats when it's time.</summary>
        internal static void Tick()
        {
            if (statsChanged && Time.unscaledTime >= nextStore) Store();
        }

        /// <summary>Called by SteamBootstrap as the game closes, so no stat change is lost.</summary>
        internal static void StoreIfChanged()
        {
            if (statsChanged) Store();
        }

        private static void Store()
        {
            statsChanged = false;
            nextStore = Time.unscaledTime + StoreInterval;
            SteamUserStats.StoreStats();
        }
#endif
    }
}
