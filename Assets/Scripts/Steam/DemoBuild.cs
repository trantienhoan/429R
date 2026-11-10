using UnityEngine;

namespace Game.Steam
{
    /// <summary>
    /// The free Steam demo of 429 (user, 2026-11-10). One project, two builds: Tools > 429 Game > Steam Demo > Build Steam
    /// Demo compiles the Windows player with the DEMO_BUILD define (the project's own settings never change). In the demo:
    /// - GAMESTAGES ends after the bedroom bonus round: "Bonus Round BR" -> "Demo?" -> "Demo End" (the thank-you board)
    ///   -> "Demo Store" (a punch opens the 429 store page in the Steam overlay, then the demo starts over). The full game
    ///   goes straight on from "Demo?" to "3 - LivingRoom". Tools > 429 Game > Steam Demo > Set Up Demo Ending adds them.
    /// - Steam starts as the demo app (Demo Settings' App ID); achievements and stats are off (Valve's advice).
    /// - Saves go to the player's own demo folder (Saves/&lt;Steam ID&gt;_Demo). The full game's first start copies those
    ///   save slots in, so what the player earned in the demo is waiting when they buy the game, and playing the demo
    ///   can never touch a full-game save. A checkpoint past the demo (e.g. saved as the bonus round began) starts the
    ///   demo over.
    /// In the Editor, Tools > 429 Game > Steam Demo > Play As Demo makes play mode act like the demo.
    /// </summary>
    public static class DemoBuild
    {
        /// <summary>The scripting define the demo build is compiled with.</summary>
        public const string Define = "DEMO_BUILD";

        /// <summary>The full game's Steam App ID (the store page the demo points to).</summary>
        public const uint FullGameAppId = 4665910;

#if UNITY_EDITOR
        /// <summary>EditorPrefs key of Tools > 429 Game > Steam Demo > Play As Demo.</summary>
        public const string PlayAsDemoKey = "429_PlayAsDemo";
#endif

        // The checkpoints inside the demo (the bedrooms). "CP LivingRoom" is set as the bedroom bonus round starts, so a
        // demo saved there (quit during the round) starts over instead of going on into the living room.
        private static readonly string[] DemoCheckpoints = { "CP Bedroom", "CP 2.5 - BR" };

        /// <summary>True in the demo build (and in the Editor while Play As Demo is ticked).</summary>
        public static bool IsDemo
        {
            get
            {
#if DEMO_BUILD
                return true;
#elif UNITY_EDITOR
                return UnityEditor.EditorPrefs.GetBool(PlayAsDemoKey, false);
#else
                return false;
#endif
            }
        }

        /// <summary>Whether the demo can go on from this checkpoint ("" = the start: always).</summary>
        public static bool IsInDemo(string checkpoint) =>
            string.IsNullOrEmpty(checkpoint) || System.Array.IndexOf(DemoCheckpoints, checkpoint) >= 0;

        /// <summary>The demo's own Steam App ID, from Demo Settings (Resources); 0 while it isn't set.</summary>
        public static uint DemoAppId
        {
            get
            {
                var settings = Resources.Load<DemoSettings>(DemoSettings.ResourceName);
                return settings != null && settings.demoAppId > 0 ? (uint)settings.demoAppId : 0u;
            }
        }

#if UNITY_EDITOR && !DEMO_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void SayPlayingAsDemo()
        {
            if (IsDemo)
                Debug.LogWarning("[Steam Demo] Play As Demo is on: play mode acts like the Steam demo (it ends after the " +
                                 "bedroom bonus round, saves go to the _Demo folder). Tools > 429 Game > Steam Demo > Play As Demo.");
        }
#endif
    }
}
