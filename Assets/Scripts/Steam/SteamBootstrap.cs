#if STEAMWORKS_NET && (UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX)
using Game.Saving;
using Steamworks;
using UnityEngine;

namespace Game.Steam
{
    /// <summary>
    /// Starts Steam before the first scene loads and points the save system at a folder named after the player's
    /// Steam ID (Saves/&lt;Steam ID&gt;), which is the folder Steam Auto-Cloud syncs. Nothing to add to the scene.
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

#if !UNITY_EDITOR
            // Started outside Steam: Steam starts the game again through itself, and this copy closes.
            if (AppId != TestAppId && SteamAPI.RestartAppIfNecessary(new AppId_t(AppId)))
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
            SaveManager.UseProfile(SteamUser.GetSteamID().m_SteamID.ToString());

            var runner = new GameObject("Steam");
            DontDestroyOnLoad(runner);
            runner.AddComponent<SteamBootstrap>();
            Debug.Log($"[Steam] Started. Saves go to '{SaveManager.SaveFolder}'.");
        }

        private void Update()
        {
            // Delivers Steam's callbacks (overlay, achievements, ...).
            SteamAPI.RunCallbacks();
        }

        private void OnApplicationQuit()
        {
            if (!Initialized) return;

            Initialized = false;
            SteamAPI.Shutdown();
        }
    }
}
#endif
