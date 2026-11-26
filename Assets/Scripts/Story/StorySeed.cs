using UnityEngine;

namespace Game.Story
{
    /// <summary>
    /// On each main room's seed (Seed_Bracer, Seed_Shield, Seed_Head, Seed_Chest, Seed_Boot). Main game only, that is
    /// GAMESTAGES is in one of Story Seed Settings' main states when the seed shows up: its glow turns green, and Touched
    /// (the seed FSM's "Story Seed Touched" action, when the player takes it) fills the view with the bright light and
    /// plays the girl's voice until the next room starts. In a gate the seed keeps its own look and nothing happens.
    /// Set up by Tools > 429 Game > Story > Set Up Story Seeds.
    /// </summary>
    [DisallowMultipleComponent]
    public class StorySeed : MonoBehaviour
    {
        [Tooltip("The whole seed (The_Boss_Seed, the ending): its light holds a while with the story's last words in " +
                 "it and the end voice, instead of waiting for the next room with \"please find me\".")]
        [SerializeField] private bool wholeSeed;

        private bool onMainGame;

        public bool WholeSeed => wholeSeed;

        /// <summary>Whether GAMESTAGES is waiting for a seed on the main game right now.</summary>
        public static bool OnMainGame()
        {
            string state = GameStages.ActiveState();
            if (string.IsNullOrEmpty(state)) return false;
            foreach (var main in StorySeedSettings.Instance.MainStates)
                if (main == state) return true;
            return false;
        }

        // Same frame the seed is made, before its particles first play, so none come out in the old colour.
        private void OnEnable()
        {
            onMainGame = OnMainGame();
            if (onMainGame) Tint(StorySeedSettings.Instance.Glow, StorySeedSettings.Instance.GlowParts);
        }

        /// <summary>
        /// The player took the seed, on the main game only: a piece goes up in a green smoke bomb with the voice (user
        /// 2026-11-26: the bright light that filled the view until the next room blinded them); the whole seed keeps
        /// its light with the story's last words.
        /// </summary>
        public void Touched()
        {
            if (!onMainGame && !OnMainGame()) return;
            var settings = StorySeedSettings.Instance;
            if (wholeSeed) StoryLight.PlayEnd(settings);
            else if (settings.PieceSmoke == null) StoryLight.Play(settings);
            else
            {
                GreenSmokeBomb(transform.position, settings);
                Say(settings.RandomVoice(), settings.VoiceVolume);
            }
        }

        /// <summary>Green smoke bombs set off so far (for tests).</summary>
        public static int SmokeBombs { get; private set; }

        // Metres between the eyes and the middle of the green puff, at least.
        private const float SmokeKeepAway = 0.8f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => SmokeBombs = 0;

        // Its own copy of the smoke (not the effect pool's: the pool's copies are shared, and the boss escape uses the
        // same smoke white), tinted the seed's green before its first puff.
        private static void GreenSmokeBomb(Vector3 at, StorySeedSettings settings)
        {
            // Taken in the hand (or walked into), the seed is close to the eyes: the puff goes off at least Smoke Keep
            // Away from them, so it's a poof to see, not a cloud over the view.
            var cam = Camera.main;
            if (cam != null)
            {
                var eyes = cam.transform.position;
                var away = at - eyes;
                if (away.magnitude < SmokeKeepAway)
                    at = eyes + (away.sqrMagnitude > 0.0025f ? away.normalized : cam.transform.forward) * SmokeKeepAway;
            }
            var smoke = Instantiate(settings.PieceSmoke, at, Quaternion.identity);
            smoke.transform.localScale = settings.PieceSmoke.transform.localScale * settings.PieceSmokeSize;
            var parts = smoke.GetComponentsInChildren<ParticleSystem>(true);
            var names = new string[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                names[i] = parts[i].name;
            }
            TintGlow(smoke, settings.Glow, names);
            foreach (var part in parts)
                if (part.transform.parent == null || part.transform.parent.GetComponentInParent<ParticleSystem>() == null) part.Play(true);
            Destroy(smoke, 5f);
            SmokeBombs++;
        }

        private static AudioSource voice;

        // The girl's voice in the player's head, as the light used to play it.
        private static void Say(AudioClip clip, float volume)
        {
            if (clip == null) return;
            if (voice == null)
            {
                var go = new GameObject("Story_Seed_Voice");
                DontDestroyOnLoad(go);
                voice = go.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 0f;
            }
            voice.PlayOneShot(clip, volume);
        }

        private void Tint(Color glow, string[] parts) => TintGlow(gameObject, glow, parts);

        /// <summary>Tints a seed's glow particles (the named parts) to the glow colour, keeping each one's brightness.</summary>
        public static void TintGlow(GameObject seed, Color glow, string[] parts)
        {
            foreach (var particles in seed.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (System.Array.IndexOf(parts, particles.name) < 0) continue;
                var main = particles.main;
                main.startColor = Tint(main.startColor, glow);
                var overLife = particles.colorOverLifetime;
                if (overLife.enabled) overLife.color = Tint(overLife.color, glow);
            }
        }

        private static ParticleSystem.MinMaxGradient Tint(ParticleSystem.MinMaxGradient colors, Color glow)
        {
            switch (colors.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return new ParticleSystem.MinMaxGradient(Tint(colors.color, glow));
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(Tint(colors.colorMin, glow), Tint(colors.colorMax, glow));
                case ParticleSystemGradientMode.Gradient:
                    return new ParticleSystem.MinMaxGradient(Tint(colors.gradient, glow));
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(Tint(colors.gradientMin, glow), Tint(colors.gradientMax, glow));
                case ParticleSystemGradientMode.RandomColor:
                    var random = new ParticleSystem.MinMaxGradient(Tint(colors.gradient, glow));
                    random.mode = ParticleSystemGradientMode.RandomColor;
                    return random;
                default:
                    return colors;
            }
        }

        // Keeps how bright and how see-through each colour was; only its hue becomes the glow's.
        private static Color Tint(Color color, Color glow)
        {
            float bright = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            return new Color(glow.r * bright, glow.g * bright, glow.b * bright, color.a);
        }

        private static Gradient Tint(Gradient gradient, Color glow)
        {
            if (gradient == null) return null;
            var keys = gradient.colorKeys;
            for (int i = 0; i < keys.Length; i++) keys[i].color = Tint(keys[i].color, glow);
            var tinted = new Gradient { mode = gradient.mode };
            tinted.SetKeys(keys, gradient.alphaKeys);
            return tinted;
        }
    }
}
