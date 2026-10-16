using UnityEngine;

namespace Game.Menus
{
    /// <summary>
    /// The whole game's volume (the pause menu's Volume - / +): 0 to 100% in 10% steps, kept between sessions in
    /// PlayerPrefs (100% for a new player). It sets AudioListener.volume, so every sound follows; the headset's own
    /// volume buttons still work on top.
    /// </summary>
    public static class GameVolume
    {
        /// <summary>One press of - or +.</summary>
        public const float Step = 0.1f;

        // Where the player's choice is kept.
        private const string LevelKey = "429 Game.Volume";
        private static float? level;

        /// <summary>The player's choice, 0 to 1; setting it saves it and changes every sound straight away.</summary>
        public static float Level
        {
            get => level ??= Mathf.Clamp01(PlayerPrefs.GetFloat(LevelKey, 1f));
            set
            {
                float now = Mathf.Clamp01(Mathf.Round(value / Step) * Step);
                level = now;
                PlayerPrefs.SetFloat(LevelKey, now);
                PlayerPrefs.Save();
                AudioListener.volume = now;
            }
        }

        public static string Label() => $"Volume: {Mathf.RoundToInt(Level * 100f)}%";

        /// <summary>The settings were wiped (reset to a new install): the choice is read again, 100% for a new player.</summary>
        public static void ForgetChoice()
        {
            level = null;
            AudioListener.volume = Level;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => level = null;

        // As the game starts: the volume kept from last time.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyKept() => AudioListener.volume = Level;
    }
}
