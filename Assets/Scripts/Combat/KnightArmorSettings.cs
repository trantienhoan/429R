using System;
using Game.Leveling;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// The Knight's armor hunt (user 2026-11-26): the armor of the Knight who came before, hidden at random each run in
    /// breakable furniture, boxes and the seed pieces' chests. An armor piece is eaten for armor (taken before health)
    /// and run points in one stat; the legendary sword and shield hide the same way but are found as weapons to grab,
    /// never eaten. Assets/Resources/Knight Armor Settings.asset (Tools > 429 Game > Items > Set Up Knight Armor Hunt).
    /// Without it nothing is hidden.
    /// User, later that day: "the knight armor right now are spawn too many, I just in bedroom stage and already get them
    /// all ... make them really rare", the sword and shield "even rarer than the rest of armor": each room's set now has
    /// only a small chance to hide one piece (Room Chance) and a smaller one to hide a legendary weapon (Weapon Room
    /// Chance). Tools > 429 Game > Items > Knight Armor: Rarer, Grab And Eat sets the numbers.
    /// </summary>
    [CreateAssetMenu(menuName = "429 Game/Combat/Knight Armor Settings", fileName = "Knight Armor Settings")]
    public class KnightArmorSettings : ScriptableObject
    {
        private const string ResourceName = "Knight Armor Settings";

        [Serializable]
        public class Piece
        {
            [Tooltip("Its name in the save: never change it once there are saves.")]
            public string id = "Helmet";
            [Tooltip("What pops out of its hiding place (a Knight Armor Pickup).")]
            public GameObject pickup;
            [Tooltip("Armor it gives: taken by hits before health.")]
            [Min(0f)] public float armor = 15f;
            [Tooltip("The stat it raises for the run, and by how many points (like a glove's).")]
            public Stat stat = Stat.Toughness;
            [Min(0f)] public float points = 2f;
        }

        [Header("The Knight's armor: eaten for armor and a stat, for this run")]
        [SerializeField] private Piece[] pieces = new Piece[0];

        [Header("The legendary weapons: hidden the same way, found as weapons to grab (never eaten for stats)")]
        [SerializeField] private GameObject[] weapons = new GameObject[0];

        [Header("Her lost things (story idea 8): one hides in each room's set; all of them in one run = her thanks")]
        [Tooltip("What pops out: Knight Armor Pickups whose id is Keepsake:0, Keepsake:1 ... (her flowers).")]
        [SerializeField] private GameObject[] keepsakes = new GameObject[0];
        [Tooltip("One found (her happy sound).")]
        [SerializeField] private AudioClip keepsakeSound;
        [Range(0f, 1f)] [SerializeField] private float keepsakeVolume = 0.9f;
        [Tooltip("The last one found: her thanks (with her green shower, and health back to full).")]
        [SerializeField] private AudioClip allKeepsakesSound;
        [Range(0f, 1f)] [SerializeField] private float allKeepsakesVolume = 1f;

        [Header("Hiding (each run, at random)")]
        [Tooltip("When a room's breakable furniture and boxes have all come (a set), the chance one armor piece still not " +
                 "found hides in it (0-1; one at most).")]
        [Range(0f, 1f)] [SerializeField] private float roomChance = 0.08f;
        [Tooltip("The chance a room's set hides one of the legendary weapons (0-1; one at most): rarer than the armor.")]
        [Range(0f, 1f)] [SerializeField] private float weaponRoomChance = 0.03f;
        [Tooltip("A seed piece's chest holds an armor piece this often (0-1; never a weapon).")]
        [Range(0f, 1f)] [SerializeField] private float chestChance = 0.05f;
        [Tooltip("A secret room's giant pumpkin (behind the bedroom's chained door, the living room's double door) holds an " +
                 "armor piece this often (0-1; user 2026-11-27: \"increase chance of obtains them when break stuffs in " +
                 "secret rooms\"). Breaking it lets it out.")]
        [Range(0f, 1f)] [SerializeField] private float secretRoomChance = 0.5f;
        [Tooltip("At most this many armor pieces hide in one room, whatever hides them (its furniture sets, its seed " +
                 "chest, one lost in an earlier room). A secret room counts as a room of its own (user 2026-11-27: \"1 " +
                 "armour piece can find per room. secret room counts as another room\"). The legendary weapons aren't armor: " +
                 "they keep their own chance.")]
        [Min(1)] [SerializeField] private int piecesPerRoom = 1;
        [Tooltip("A new room starts when GAMESTAGES enters a state whose name starts with one of these: the clean-ups " +
                 "between rooms, the rooftop coming (also after a gate), a checkpoint, a new run.")]
        [SerializeField] private string[] roomStarts =
            { "Clean", "clean", "Destroy PlayGroundZ", "Rooftop_ON", "Boss PG", "CP ", "Game Over", "Restart Loop" };
        [Tooltip("Only things at least this big (metres, their longest side) can hide one.")]
        [Min(0f)] [SerializeField] private float smallestSpot = 0.4f;
        [Tooltip("Things whose name has any of these never hide one.")]
        [SerializeField] private string[] skipNames = { "Pumpkin", "Book", "Bottle", "Candy", "Flip_Flop", "Web" };
        [Tooltip("A room's things count as all there once none has come for this many seconds.")]
        [Min(0.5f)] [SerializeField] private float roomSettles = 2.5f;
        [Tooltip("A batch needs at least this many things to count as a room's set.")]
        [Min(1)] [SerializeField] private int smallestRoom = 4;

        [Header("The hint")]
        [Tooltip("A glint on a thing that hides one, now and then.")]
        [SerializeField] private GameObject glint;
        [Min(0.01f)] [SerializeField] private float glintScale = 0.35f;
        [Tooltip("Seconds between two glints (at random in this range).")]
        [SerializeField] private Vector2 glintEvery = new Vector2(5f, 9f);

        [Header("Found")]
        [Tooltip("The puff where it pops out (Smoke_Flash_Small_Puff).")]
        [SerializeField] private GameObject foundPuff;
        [SerializeField] private AudioClip foundSound;
        [Range(0f, 1f)] [SerializeField] private float foundVolume = 0.9f;
        [Tooltip("An armor piece being put on.")]
        [SerializeField] private AudioClip equipSound;
        [Range(0f, 1f)] [SerializeField] private float equipVolume = 1f;
        [Tooltip("A found armor piece floats this high over the floor the player stands on (metres), in reach, until it " +
                 "is grabbed; let go, it falls like a candy. It is eaten at the mouth. User 2026-11-27: \"make the armour " +
                 "piece more close to the ground instead of flying up so high\" (was 1.1).")]
        [Min(0f)] [SerializeField] private float floatHeight = 0.4f;

        [Header("Armor taking hits")]
        [SerializeField] private AudioClip[] hitSounds = new AudioClip[0];
        [Range(0f, 1f)] [SerializeField] private float hitVolume = 0.7f;
        [Tooltip("The last of the armor is gone.")]
        [SerializeField] private AudioClip breakSound;
        [Range(0f, 1f)] [SerializeField] private float breakVolume = 0.9f;

        [Header("HUD (next to the health number)")]
        [SerializeField] private Sprite icon;
        [SerializeField] private Color numberColor = new Color(0.8f, 0.95f, 0.75f, 1f);

        /// <summary>The ids of her lost things start with this ("Keepsake:0").</summary>
        public const string KeepsakePrefix = "Keepsake:";

        public Piece[] Pieces => pieces;
        public GameObject[] Weapons => weapons;
        public GameObject[] Keepsakes => keepsakes;
        public AudioClip KeepsakeSound => keepsakeSound;
        public float KeepsakeVolume => keepsakeVolume;
        public AudioClip AllKeepsakesSound => allKeepsakesSound;
        public float AllKeepsakesVolume => allKeepsakesVolume;

        /// <summary>How many of her lost things there are (empty slots not counted).</summary>
        public int KeepsakeCount
        {
            get
            {
                int count = 0;
                if (keepsakes != null) foreach (var k in keepsakes) if (k != null) count++;
                return count;
            }
        }
        public float RoomChance => roomChance;
        public float WeaponRoomChance => weaponRoomChance;
        public float ChestChance => chestChance;
        public float SecretRoomChance => secretRoomChance;
        public int PiecesPerRoom => piecesPerRoom;
        public string[] RoomStarts => roomStarts;
        public float SmallestSpot => smallestSpot;
        public string[] SkipNames => skipNames;
        public float RoomSettles => roomSettles;
        public int SmallestRoom => smallestRoom;
        public GameObject Glint => glint;
        public float GlintScale => glintScale;
        public Vector2 GlintEvery => glintEvery;
        public GameObject FoundPuff => foundPuff;
        public AudioClip FoundSound => foundSound;
        public float FoundVolume => foundVolume;
        public AudioClip EquipSound => equipSound;
        public float EquipVolume => equipVolume;
        public float FloatHeight => floatHeight;
        public AudioClip[] HitSounds => hitSounds;
        public float HitVolume => hitVolume;
        public AudioClip BreakSound => breakSound;
        public float BreakVolume => breakVolume;
        public Sprite Icon => icon;
        public Color NumberColor => numberColor;

        /// <summary>The armor piece with this id, or null.</summary>
        public Piece Find(string id)
        {
            if (pieces == null || string.IsNullOrEmpty(id)) return null;
            foreach (var piece in pieces)
                if (piece != null && piece.id == id) return piece;
            return null;
        }

        private static KnightArmorSettings instance;
        private static bool looked;

        /// <summary>The settings in Resources, or null when there are none (then nothing is hidden).</summary>
        public static KnightArmorSettings Instance
        {
            get
            {
                if (!looked)
                {
                    instance = Resources.Load<KnightArmorSettings>(ResourceName);
                    looked = true;
                }
                return instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            looked = false;
        }
    }
}
