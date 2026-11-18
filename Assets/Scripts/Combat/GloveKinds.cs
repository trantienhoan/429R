using System;
using System.Collections.Generic;
using Game.Leveling;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>What a kind of gloves unlocks by itself while it's worn (user, 2026-11-12), on top of its points.</summary>
    [Flags]
    public enum GloveUnlocks
    {
        None = 0,
        // The flying Jab, Swing and Uppercut at once (Boxing).
        FlyingPunches = 1,
        // Explosive Objects level 1: everything thrown hard enough explodes (Throwing).
        ExplosiveObjects = 2,
        // Impact (small): a thing knocked with a held weapon into another explodes (Soldier).
        Impact = 4,
    }

    /// <summary>
    /// The kinds of boxing gloves (user, 2026-11-12): the Knight at the start carries one pair, a kind picked at random
    /// each run (Glove Pickup, Gloves), and the pair the player picks up gives level-up points for the run. Each kind has
    /// its glove for each hand (a prefab: its look and its punch shape), the stat it adds to and what it unlocks (Boxing:
    /// the flying punches, Throwing: exploding throws, Soldier: Impact; user, 2026-11-12). To add a kind: make its
    /// two gloves (copy Glove_Boxing_L / _R in Assets/Prefabs/Player/Gloves and change their look), then add a line here
    /// with a new Id. The game finds this file by its name in Resources (Assets/Resources/Glove Kinds.asset); without
    /// it there are no gloves and grip + trigger punches bare-handed as before. Tools > 429 Game > Combat > Set Up Gloves
    /// makes it with the first three kinds.
    /// </summary>
    [CreateAssetMenu(fileName = "Glove Kinds", menuName = "429 Game/Glove Kinds")]
    public class GloveKinds : ScriptableObject
    {
        /// <summary>One kind of gloves.</summary>
        [Serializable]
        public class Kind
        {
            [Tooltip("Its name in the save. Never rename it once there are saves (a save with an unknown name has no " +
                     "gloves). E.g. Boxing.")]
            public string id = "Boxing";
            [Tooltip("Its name for the player, e.g. Boxing Gloves.")]
            public string title = "Boxing Gloves";
            [Tooltip("The level-up stat the gloves add points to, for the run they're worn.")]
            public Stat stat = Stat.FistSize;
            [Tooltip("How many points (user, 2026-11-12: 3). They count like eaten candies' points (on top of the " +
                     "player's own, the candies' and the gloves' together at most Candy Max Points, Player Level's 10, a " +
                     "stat; nothing in a special stage), but they also unlock what the stat's points do while worn: Fist " +
                     "Size the flying punches, Throw Power exploding throws.")]
            [Min(0f)]
            public float points = 3f;
            [Tooltip("The glove on the left hand: a prefab whose pivot sits at the knuckles, +Z where the punch goes, +Y the " +
                     "top of the fist (the thumb side), sized for a fist (1 = as it shows at 0 Fist Size). Its renderers are " +
                     "its look; its Box, Sphere or Capsule colliders (e.g. under a child \"Punch Shape\") are what hits. " +
                     "Empty: the right glove's.")]
            public GameObject left;
            [Tooltip("The glove on the right hand (see Left). Empty: the left glove's.")]
            public GameObject right;
            [Tooltip("Its colour, e.g. for the puff when the pair is picked up.")]
            public Color colour = new(0.8f, 0.08f, 0.06f);
            [Tooltip("Flying punches are easy to throw with these gloves (user, 2026-11-12: Boxing): the Fist's own flying-punch " +
                     "conditions (Jab Speed, ...). Off: the Fist's harder ones (Flying punches without easy gloves), so " +
                     "the Boxing gloves feel easier.")]
            public bool easyFlyingPunches;
            [Tooltip("What these gloves unlock by themselves while worn (user, 2026-11-12): Boxing all three flying punches, " +
                     "Throwing Explosive Objects (level 1), Soldier Impact (small). Their points count towards the next " +
                     "levels too (Player Level).")]
            public GloveUnlocks unlocks;

            /// <summary>The glove for this hand (the other hand's if it has none).</summary>
            public GameObject For(bool leftHand)
            {
                var own = leftHand ? left : right;
                return own != null ? own : leftHand ? right : left;
            }
        }

        [Tooltip("Gloves make the real punch (user, 2026-11-12). Without a pair, a fist only taps for 1 (the Fists' Bare " +
                 "Fists, user 2026-11-18; untick it there for no punching at all): no flying punches, no hard hits. Untick " +
                 "this to punch bare-handed in full again when no gloves are worn (gloves still work when worn).")]
        [SerializeField] private bool needGloves = true;
        [Tooltip("The kinds the Knight can carry, one picked at random each run (never the same kind two runs in a row).")]
        [SerializeField] private List<Kind> kinds = new();
        // What Tools > 429 Game > Combat > Set Up Gloves has already set here (1: Boxing's Easy Flying Punches), so running
        // it again never undoes a change made by hand.
        [SerializeField, HideInInspector] private int menuVersion;

        [Header("Pop")]
        [Tooltip("How long a glove takes to pop onto the fist, in seconds.")]
        [Min(0.01f)]
        [SerializeField] private float popSeconds = 0.14f;
        [Tooltip("How far past its size it pops before it settles (0.2 = a fifth bigger for a moment).")]
        [Range(0f, 1f)]
        [SerializeField] private float popOvershoot = 0.2f;
        [Tooltip("How long it takes to pop away when the fist opens, in seconds.")]
        [Min(0.01f)]
        [SerializeField] private float awaySeconds = 0.09f;
        [Tooltip("Played as a glove pops on (a soft fwump). Empty: silent.")]
        [SerializeField] private AudioClip popSound;
        [Range(0f, 1f)]
        [SerializeField] private float popVolume = 0.55f;
        [Tooltip("A puff of smoke where a glove pops on (user, 2026-11-18; the shared Smoke_Flash_Small_Puff). Empty: none.")]
        [SerializeField] private GameObject popPuff;
        [Tooltip("How big that puff is, times the effect's own size. It grows with the glove (Fist Size). Keep it small: " +
                 "the glove is right in front of the eyes (0.3 hid the view for a moment; 0.08 hugs the knuckles).")]
        [Min(0f)]
        [SerializeField] private float popPuffSize = 0.08f;
        [Tooltip("Played as a glove pops away. Empty: silent.")]
        [SerializeField] private AudioClip awaySound;
        [Range(0f, 1f)]
        [SerializeField] private float awayVolume = 0.25f;
        [Tooltip("The away sound's pitch (above 1: higher and lighter than the pop).")]
        [Range(0.5f, 2f)]
        [SerializeField] private float awayPitch = 1.25f;
        [Tooltip("When a pair is picked up, both gloves pop onto the empty hands for this many seconds, so the player sees " +
                 "they have them. 0 = they don't.")]
        [Min(0f)]
        [SerializeField] private float showOnPickupSeconds = 0.9f;

        public bool NeedGloves => needGloves;
        public int Count => kinds != null ? kinds.Count : 0;
        public IReadOnlyList<Kind> All => kinds ?? (IReadOnlyList<Kind>)Array.Empty<Kind>();
        public float PopSeconds => popSeconds;
        public float PopOvershoot => popOvershoot;
        public float AwaySeconds => awaySeconds;
        public AudioClip PopSound => popSound;
        public float PopVolume => popVolume;
        public GameObject PopPuff => popPuff;
        public float PopPuffSize => popPuffSize;
        public AudioClip AwaySound => awaySound;
        public float AwayVolume => awayVolume;
        public float AwayPitch => awayPitch;
        public float ShowOnPickupSeconds => showOnPickupSeconds;

        /// <summary>The kind with this Id, or null.</summary>
        public Kind Find(string id)
        {
            if (string.IsNullOrEmpty(id) || kinds == null) return null;
            foreach (var kind in kinds)
            {
                if (kind != null && kind.id == id) return kind;
            }
            return null;
        }
    }
}
