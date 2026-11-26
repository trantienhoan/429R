using TMPro;
using UnityEngine;

namespace Game.Story
{
    /// <summary>
    /// The story's seed pieces (user's story, 2026-11-23: each room's chest holds a green glowing piece of a seed; touching
    /// it, a bright light blinds you, the tiny green girl's voice says "please find me", and when the light is gone you are
    /// in the next room). Main game only: the gates' seeds stay as they are. The game reads
    /// Assets/Resources/Story Seed Settings.asset; without it, these defaults and no voice.
    /// </summary>
    [CreateAssetMenu(menuName = "429 Game/Story/Story Seed Settings", fileName = "Story Seed Settings")]
    public class StorySeedSettings : ScriptableObject
    {
        [Tooltip("The seed's glow on the main game: these particles are tinted this colour.")]
        [SerializeField] private Color glow = new Color(0.55f, 1f, 0.45f, 1f);
        [Tooltip("The seed's particle objects that make its glow and halo ('shadow' is its warm halo); its smoke puff " +
                 "keeps its colours.")]
        [SerializeField] private string[] glowParts = { "circles", "GrowthParticles", "stars", "shadow" };
        [Tooltip("The bright light that fills the view when a seed is taken (the Fairy's own light colour).")]
        [SerializeField] private Color lightColor = new Color(0.87f, 1f, 0.81f, 1f);
        [Tooltip("The girl's voice when a seed is taken: \"please find me\". Put the user's recording here.")]
        [SerializeField] private AudioClip voice;
        [Tooltip("More takes of that voice (e.g. \"I am here\"): one of all of them is said at random.")]
        [SerializeField] private AudioClip[] moreVoices = new AudioClip[0];
        [Range(0f, 1f)]
        [SerializeField] private float voiceVolume = 0.8f;
        [Tooltip("Seconds the light takes to fill the view.")]
        [Min(0f)]
        [SerializeField] private float lightIn = 0.35f;
        [Tooltip("Seconds the light takes to fade once the next room has started (it saves its checkpoint).")]
        [Min(0f)]
        [SerializeField] private float lightOut = 1.6f;
        [Tooltip("The light never stays longer than this, in seconds, even when no next room starts.")]
        [Min(0.5f)]
        [SerializeField] private float mostSeconds = 9f;
        [Tooltip("GAMESTAGES states that wait for a seed on the main game: the start, room 1, the bedroom, the living " +
                 "room and the Jenga room. A seed anywhere else (a gate) keeps its own look and gives no light.")]
        [SerializeField] private string[] mainStates =
            { "Stage 1 - The Start", "Mini Enemy", "Enemy1 - Wait", "Enemy2 - Wait", "Enemy 3 Stuffs", "Thank For Playing" };

        [Header("\"Fight\": when the walls drop (the Story Fight Voice actions in the 4 rooms' GAMESTAGES states)")]
        [Tooltip("\"Fight\", heard in the player's head once the walls have sunk and the spiders come. Put the user's " +
                 "recording here; empty = none.")]
        [SerializeField] private AudioClip fightVoice;
        [Range(0f, 1f)]
        [SerializeField] private float fightVolume = 1f;

        [Header("The ending: the whole seed (The_Boss_Seed, from the boss's chest)")]
        [Tooltip("The happy voice when the whole seed is taken (\"you protected the Green\"). Put the user's recording " +
                 "here; empty = none (the thank-you song plays anyway).")]
        [SerializeField] private AudioClip endVoice;
        [Tooltip("Shown in the light when the whole seed is taken (the user's words).")]
        [SerializeField] private string endTitle = "THE GREEN PROTECTOR";
        [SerializeField] private string endLine = "I will do it again and again.";
        [SerializeField] private TMP_FontAsset titleFont;
        [SerializeField] private Color titleColor = new Color(0.1f, 0.32f, 0.12f, 1f);
        [Tooltip("Seconds the light stays for the whole seed (paused time not counted), then it fades.")]
        [Min(1f)]
        [SerializeField] private float endHoldSeconds = 5f;

        [Header("The ending: the girl's green shower (the Fairy's reward)")]
        [Tooltip("Whose sparkles make the shower: a seed prefab's 'stars' and 'circles' particles, tinted the glow colour.")]
        [SerializeField] private GameObject showerLook;
        [Min(0.5f)]
        [SerializeField] private float showerSeconds = 5f;
        [Tooltip("How wide the shower is round the player, in metres.")]
        [Min(0.2f)]
        [SerializeField] private float showerRadius = 1.2f;
        [Tooltip("How high above the player's head it starts, in metres.")]
        [SerializeField] private float showerHeight = 1.4f;
        [Tooltip("Sparkles a second, for each of the two particle kinds.")]
        [Min(1f)]
        [SerializeField] private float showerRate = 30f;
        [Tooltip("Sparkles start only in the outer part of the ring (0.3 = the outer 30%), so none fall through the eyes.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float showerRing = 0.3f;
        [Tooltip("Size of the shower's sparkles ('stars') and soft glows ('circles') against how big they are on the " +
                 "seed itself (1 = the same).")]
        [SerializeField] private float showerStarScale = 2f;
        [SerializeField] private float showerGlowScale = 1f;

        [Header("The seal breaks (the rooftop lid): the pieces become the Seed and the tree grows")]
        [Tooltip("User 2026-11-26: \"when the seal on the rooftop is broken, all the collected pieces fly out, circling and " +
                 "join in as whole seed, dig to the ground and grow up as the Pumpkin Tree\". The item the collected pieces " +
                 "are (Yellow_Seed, \"A piece of the Seed\"): they're used up at the seal (Spend Pieces). How many fly is " +
                 "the seeds taken this run (the start + 4 rooms = 5), not this item's count, which carried over between " +
                 "runs (user: \"why we have so many pieces?\").")]
        [SerializeField] private Game.Inventory.ItemDefinition pieceItem;
        [Tooltip("The pieces are used up: they become the Seed (the count goes back to 0 for the next run).")]
        [SerializeField] private bool spendPieces = true;
        [Min(1)]
        [SerializeField] private int mostPiecesShown = 15;
        [Tooltip("What they join into: the whole Seed's look (Items/Chest/The_Seed; it only shows).")]
        [SerializeField] private GameObject wholeSeedLook;
        [Tooltip("Its size while it hangs and digs, against the seed prefab's own (they read from far off).")]
        [Min(0.1f)]
        [SerializeField] private float wholeSeedSize = 2.2f;
        [Tooltip("The green light round the pieces and the Seed.")]
        [SerializeField] private Color flightLight = new Color(0.55f, 1f, 0.45f, 1f);
        [Tooltip("The earth thrown up as the Seed digs in (user 2026-11-26: brown dirt; Prefabs/Story/Seed_Dig_Dirt, a " +
                 "browner copy of the boss's scrape dust). Empty: the Seeds Into Soil action's own puff.")]
        [SerializeField] private GameObject digPuff;
        [Tooltip("Its size as the Seed goes down (several), and for the last burst when it's in.")]
        [Min(0.05f)]
        [SerializeField] private float digPuffSize = 0.7f;
        [Min(0.05f)]
        [SerializeField] private float lastPuffSize = 1.5f;
        [Tooltip("Sounds (the user's ElevenLabs list: seed_chime, seed_whoosh, seed_merge, seed_plunge, tree_grow). " +
                 "Empty = none.")]
        [SerializeField] private AudioClip pieceChime;
        [SerializeField] private AudioClip flightWhoosh;
        [SerializeField] private AudioClip mergeSound;
        [SerializeField] private AudioClip plungeSound;
        [SerializeField] private AudioClip treeGrowSound;
        [Range(0f, 1f)]
        [SerializeField] private float flightVolume = 0.9f;

        private const string ResourcePath = "Story Seed Settings";
        private static StorySeedSettings instance;

        /// <summary>The settings in Resources (or the defaults).</summary>
        public static StorySeedSettings Instance
        {
            get
            {
                if (instance != null) return instance;
                instance = Resources.Load<StorySeedSettings>(ResourcePath);
                if (instance == null)
                {
                    instance = CreateInstance<StorySeedSettings>();
                    instance.hideFlags = HideFlags.HideAndDontSave;
                }
                return instance;
            }
        }

        public Color Glow => glow;
        public string[] GlowParts => glowParts;
        public Color LightColor => lightColor;
        public AudioClip Voice => voice;
        public float VoiceVolume => voiceVolume;

        /// <summary>The voice or one of its other takes, at random (null when there are none).</summary>
        public AudioClip RandomVoice()
        {
            int count = voice != null ? 1 : 0;
            if (moreVoices != null) foreach (var take in moreVoices) if (take != null) count++;
            if (count == 0) return null;
            int pick = Random.Range(0, count);
            if (voice != null && pick-- == 0) return voice;
            foreach (var take in moreVoices)
            {
                if (take == null) continue;
                if (pick-- == 0) return take;
            }
            return voice;
        }
        public float LightIn => lightIn;
        public float LightOut => lightOut;
        public float MostSeconds => mostSeconds;
        public string[] MainStates => mainStates;
        public AudioClip FightVoice => fightVoice;
        public float FightVolume => fightVolume;
        public AudioClip EndVoice => endVoice;
        public string EndTitle => endTitle;
        public string EndLine => endLine;
        public TMP_FontAsset TitleFont => titleFont;
        public Color TitleColor => titleColor;
        public float EndHoldSeconds => endHoldSeconds;
        public GameObject ShowerLook => showerLook;
        public float ShowerSeconds => showerSeconds;
        public float ShowerRadius => showerRadius;
        public float ShowerHeight => showerHeight;
        public float ShowerRate => showerRate;
        public float ShowerRing => showerRing;
        public float ShowerStarScale => showerStarScale;
        public float ShowerGlowScale => showerGlowScale;
        public Game.Inventory.ItemDefinition PieceItem => pieceItem;
        public bool SpendPieces => spendPieces;
        public int MostPiecesShown => mostPiecesShown;
        public GameObject WholeSeedLook => wholeSeedLook;
        public float WholeSeedSize => wholeSeedSize;
        public Color FlightLight => flightLight;
        public GameObject DigPuff => digPuff;
        public float DigPuffSize => digPuffSize;
        public float LastPuffSize => lastPuffSize;
        public AudioClip PieceChime => pieceChime;
        public AudioClip FlightWhoosh => flightWhoosh;
        public AudioClip MergeSound => mergeSound;
        public AudioClip PlungeSound => plungeSound;
        public AudioClip TreeGrowSound => treeGrowSound;
        public float FlightVolume => flightVolume;
    }
}
