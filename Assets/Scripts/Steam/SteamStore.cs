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
    /// Opens a game's Steam store page (the demo's "Wishlist 429" ending). In the Steam overlay when Steam is running
    /// (in VR: the SteamVR dashboard); otherwise, or with the overlay switched off, in the web browser on the desktop.
    /// </summary>
    public static class SteamStore
    {
        public static string PageUrl(uint appId) => "https://store.steampowered.com/app/" + appId + "/";

        public static void Open(uint appId)
        {
            if (appId == 0) appId = DemoBuild.FullGameAppId;
#if STEAM_BUILD
            if (SteamBootstrap.Initialized)
            {
                SteamFriends.ActivateGameOverlayToStore(new AppId_t(appId), EOverlayToStoreFlag.k_EOverlayToStoreFlag_None);
                if (SteamUtils.IsOverlayEnabled())
                {
                    Debug.Log($"[Steam] Store page {appId} opened in the Steam overlay.");
                    return;
                }
            }
#endif
            Application.OpenURL(PageUrl(appId));
            Debug.Log($"[Steam] Store page {appId} opened in the web browser.");
        }
    }
}
