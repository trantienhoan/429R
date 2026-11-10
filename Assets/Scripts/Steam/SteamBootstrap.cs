#if STEAMWORKS_NET && (UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX)
using Game.Saving;
using Steamworks;
using UnityEngine;

namespace Game.Steam
{
    /// <summary>
    /// Starts Steam before the first scene loads and points the save system at a folder named after the player's
    /// Steam ID (Saves/&lt;Steam ID&gt;, holding their save slots slot1.json to slot3.json), which is the folder Steam
    /// Auto-Cloud syncs. The first time, the Local folder's save slots are copied in. Nothing to add to the scene.
    /// Without Steam (e.g. in the Editor with Steam closed), the game still plays and saves in the Local folder.
    /// </summary>
    public class SteamBootstrap : MonoBehaviour
    {
        /// <summary>Your game's Steam App ID. Keep it the same as steam_appid.txt in the project folder.</summary>
        public const uint AppId = 4665910;

        // Valve's "Spacewar" test app. Never used to restart through Steam, which would start Spacewar instead.
        private const uint TestAppId = 480;

        public static bool Initialized { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            Initialized = false;

            // The free demo is its own Steam app (DemoBuild, Demo Settings' App ID).
            uint appId = DemoBuild.IsDemo ? DemoBuild.DemoAppId : AppId;
#if !UNITY_EDITOR
            if (appId == 0)
            {
                Debug.LogError("[Steam] This is the demo build but Demo Settings has no App ID (Tools > 429 Game > Steam Demo > Demo Settings).");
            }
            // Started outside Steam: Steam starts the game again through itself, and this copy closes.
            else if (appId != TestAppId && SteamAPI.RestartAppIfNecessary(new AppId_t(appId)))
            {
                Application.Quit();
                return;
            }
#endif

            if (!Packsize.Test()) Debug.LogError("[Steam] This Steamworks.NET was built for another platform.");
            if (!DllCheck.Test()) Debug.LogError("[Steam] steam_api64.dll is the wrong version for this Steamworks.NET.");

            var result = SteamAPI.InitEx(out var error);
            if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
            {
                Debug.LogWarning($"[Steam] Steam didn't start ({result}: {error}). Is Steam running and signed in? " +
                                 "Playing without Steam; saves go to the Local folder.");
                return;
            }

            Initialized = true;
            // The demo saves in its own folder (<Steam ID>_Demo); the full game copies it in on its first start.
            string steamId = SteamUser.GetSteamID().m_SteamID.ToString();
            SaveManager.UseProfile(DemoBuild.IsDemo ? steamId + SaveManager.DemoProfileSuffix : steamId);

            var runner = new GameObject("Steam");
            DontDestroyOnLoad(runner);
            runner.AddComponent<SteamBootstrap>();
            Debug.Log($"[Steam] Started{(DemoBuild.IsDemo ? " (the demo, App ID " + appId + ")" : "")}. Saves go to '{SaveManager.SaveFolder}'.");
        }

        private void Update()
        {
            // Delivers Steam's callbacks (overlay, achievements, ...).
            SteamAPI.RunCallbacks();
            SteamAchievements.Tick();
        }

        private void OnApplicationQuit()
        {
            if (!Initialized) return;

            SteamAchievements.StoreIfChanged();
            Initialized = false;
            SteamAPI.Shutdown();
        }
    }
}
#endif
