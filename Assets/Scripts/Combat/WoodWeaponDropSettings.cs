using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Wooden furniture drops wooden weapons (user 2026-11-27: "check furnitures in the game, make them wood furniture have
    /// high chance of dropping wooden weapon like bat, broom..."). Resources/Wood Weapon Drop Settings; Tools > 429 Game >
    /// Combat > Set Up Wood Weapon Drops makes it (Wood Weapon Drops does the dropping).
    /// </summary>
    public class WoodWeaponDropSettings : ScriptableObject
    {
        [System.Serializable]
        public class Drop
        {
            public GameObject weapon;
            [Tooltip("How often this one is picked, against the others.")]
            [Min(0f)]
            public float weight = 1f;
        }

        [Tooltip("The chance a broken wooden piece of furniture drops a wooden weapon.")]
        [Range(0f, 1f)]
        [SerializeField] private float chance = 0.4f;
        [Tooltip("No more drops while this many of the weapons it dropped are still about (lying, held, not yet broken or " +
                 "eaten by the purple hole), so a room never fills up with them.")]
        [Min(1)]
        [SerializeField] private int mostAbout = 3;
        [Tooltip("The wooden weapons and how often each is picked.")]
        [SerializeField] private Drop[] weapons = new Drop[0];
        [Tooltip("Furniture counts as wooden when the name of its main material (its first renderer's first material) has " +
                 "one of these (any case): Wood_Furniture_Mat, Wood_Floor_Mat, Lair_Wood_Aged_Mat, PG_Wood_Boards_Mat.")]
        [SerializeField] private string[] woodWords = { "Wood" };
        [Tooltip("Never from these (a word in the name, any case): the metal versions, doors, chests, barrels, webs, lights, " +
                 "the playroom's Jenga blocks.")]
        [SerializeField] private string[] skipNames = { "Metal", "Door", "Chest", "Barrel", "Web", "Light", "Candle", "Chandelier", "Jenga" };
        [Tooltip("A small puff where the weapon falls out (empty: none).")]
        [SerializeField] private GameObject puff;
        [Min(0.01f)]
        [SerializeField] private float puffScale = 0.6f;

        public float Chance => chance;
        public int MostAbout => mostAbout;
        public string[] WoodWords => woodWords;
        public string[] SkipNames => skipNames;
        public GameObject Puff => puff;
        public float PuffScale => puffScale;

        /// <summary>A weapon picked by weight (null if there are none).</summary>
        public GameObject Pick()
        {
            float total = 0f;
            foreach (var d in weapons)
                if (d != null && d.weapon != null) total += d.weight;
            if (total <= 0f) return null;
            float roll = Random.value * total;
            foreach (var d in weapons)
            {
                if (d == null || d.weapon == null) continue;
                roll -= d.weight;
                if (roll <= 0f) return d.weapon;
            }
            return null;
        }

        private static WoodWeaponDropSettings instance;
        private static bool looked;

        /// <summary>Resources/Wood Weapon Drop Settings, or null (then nothing drops).</summary>
        public static WoodWeaponDropSettings Instance
        {
            get
            {
                if (instance == null && !looked)
                {
                    looked = true;
                    instance = Resources.Load<WoodWeaponDropSettings>("Wood Weapon Drop Settings");
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
