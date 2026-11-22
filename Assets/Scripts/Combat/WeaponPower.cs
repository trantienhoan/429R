using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.Combat
{
    /// <summary>
    /// How hard this weapon (or thrown thing) hits compared with a bare fist. Power multiplies its hit speed before the
    /// damage is worked out, so a hammer (2) turns a normal swing into a hard hit. Damage is what a normal hit does, a
    /// whole number (a hard hit does twice): a fist 1, a hammer 2; thrown, its Thrown Bonus more (a book 2), so throwing
    /// beats punching. Anything without it counts as 1 and 1.
    /// Goes on the weapon's root (the object with the Rigidbody). Level-ups add whole points to it, one stat for each move
    /// (user, 2026-11-19): Fist Size to flying punches (and +1 every 3 points to the closed punch); Weapon Efficiency and
    /// Weapon Master (a bonus stat) only while the thing is held in the hand; a thing off the hand (thrown) gets its
    /// Thrown Bonus and Throw Power's (+1 every 2 points) (user, 2026-11-18: "weapon damage only counts when the object
    /// is still in hand of Player, throw damage only counts when objects is off hand of Player").
    /// Tools > 429 Game > Combat > Balance Damage sets both for every weapon and throwable.
    /// </summary>
    [DisallowMultipleComponent]
    public class WeaponPower : MonoBehaviour
    {
        [Tooltip("Multiplies this weapon's hit speed. 1 = a bare swing, 2 = twice as hard, below 1 = softer (e.g. a pillow).")]
        [Min(0f)]
        [SerializeField] private float power = 1f;
        [Tooltip("What a normal hit does, a whole number (a hard hit does twice): 1 = a fist or a book, 2 = a hammer.")]
        [Min(0f)]
        [SerializeField] private float damage = 1f;
        [Tooltip("Extra damage when it hits after being thrown (not held), a whole number: throwing beats punching (user, " +
                 "2026-10-27). 1 = a thrown book does 2, a thrown hammer 3. 0 for the bottles: their blast is their extra.")]
        [Min(0)]
        [SerializeField] private int thrownBonus = 1;

        [Header("Tricks, while it's held in the hand (user, 2026-11-20)")]
        [Tooltip("A hit that hurts an enemy stuns it this many seconds, with dizzy stars over its head (a hard hit 1.5x). " +
                 "0 = none (the enemy's own short stun). The frying pan.")]
        [Min(0f)]
        [SerializeField] private float stunSeconds;
        [Tooltip("A hit that hurts an enemy knocks it back, times its normal knock-back distance and hop (a hard hit 1.5x, " +
                 "a smash farther still). 0 = as before (only hard hits knock back). The baseball bat.")]
        [Min(0f)]
        [SerializeField] private float knockBackTimes;
        [Tooltip("Played where it hits an enemy, one at random (e.g. the pan's metal clangs).")]
        [SerializeField] private AudioClip[] hitSounds = System.Array.Empty<AudioClip>();
        [Range(0f, 1f)]
        [SerializeField] private float hitVolume = 0.9f;

        /// <summary>Seconds a hit stuns (0 = none).</summary>
        public float StunSeconds => stunSeconds;
        /// <summary>Knock-back multiplier on every hit that hurts (0 = none).</summary>
        public float KnockBackTimes => knockBackTimes;

        /// <summary>Plays one of its hit sounds at a point (if it has any).</summary>
        public void PlayHitSound(Vector3 at)
        {
            if (hitSounds == null || hitSounds.Length == 0) return;
            var clip = hitSounds[Random.Range(0, hitSounds.Length)];
            if (clip != null) AudioSource.PlayClipAtPoint(clip, at, hitVolume);
        }

        /// <summary>The weapon that made this collision, if it's held in the hand and has a trick (a stun, a knock-back or
        /// hit sounds), else null.</summary>
        public static WeaponPower HeldTrick(Collision collision)
        {
            var weapon = Find(collision);
            if (weapon == null || (weapon.stunSeconds <= 0f && weapon.knockBackTimes <= 0f && (weapon.hitSounds == null || weapon.hitSounds.Length == 0)))
                return null;
            return weapon.TryGetComponent(out XRGrabInteractable grab) && grab.isSelected ? weapon : null;
        }

        public float Power
        {
            get => power;
            set => power = Mathf.Max(0f, value);
        }

        public float Damage
        {
            get => damage;
            set => damage = Mathf.Max(0f, value);
        }

        public int ThrownBonus
        {
            get => thrownBonus;
            set => thrownBonus = Mathf.Max(0, value);
        }

        /// <summary>
        /// Extra damage multiplier set while the game runs (not saved), on top of Damage: e.g. 1.5 while a weapon is held in
        /// two hands (TwoHandedGrip), or 2 for punches while the Elon Boong buff is on (PlayerBuffs). 1 = none.
        /// </summary>
        [System.NonSerialized] public float boost = 1f;

        /// <summary>The Power of whatever made this collision, or 1 if it has none.</summary>
        public static float Of(Collision collision)
        {
            var weapon = Find(collision);
            return weapon != null ? weapon.power : 1f;
        }

        /// <summary>The Damage of whatever made this collision (with Weapon Efficiency, but not for fists), or 1 if it has none.</summary>
        public static float DamageOf(Collision collision)
        {
            return DamageOf(Find(collision));
        }

        /// <summary>The Damage of the weapon on this body, counted as for its hits (e.g. a fist ball's, with its boost), or 1 if it has none.</summary>
        public static float DamageOf(Rigidbody body)
        {
            return DamageOf(body != null && body.TryGetComponent(out WeaponPower weapon) ? weapon : null);
        }

        // A whole number, at least 1 (halves round up). One stat, one move (user, 2026-11-19): the closed fist does its
        // own (1) plus Fist Size's punch bonus (+1 every 3 points); a flying jab gets Fist Size's full bonus (eaten Fist
        // Size candies' included). A thing the player can hold: in the hand, Weapon Efficiency's bonus and Weapon
        // Master's (weapon damage); off the hand (thrown), its Thrown Bonus plus Throw Power's (+1 every 2 points); lying
        // about, its own. A weapon that can't be held (a gun's shot) is the held gun's: Weapon Efficiency's. A boost (two
        // hands, the Elon Boong) multiplies. (Until 2026-11-18 Weapon Efficiency was added to thrown things too.)
        private static float DamageOf(WeaponPower weapon)
        {
            if (weapon == null) return 1f;
            float own = Mathf.Max(1f, HitSettings.Whole(weapon.damage));
            float bonus;
            if (Fist.IsFlyingFist(weapon)) bonus = Game.Leveling.PlayerLevel.FlyingJabBonus;
            else if (Fist.IsFistBall(weapon)) bonus = Game.Leveling.PlayerLevel.PunchDamageBonus;
            else if (!weapon.TryGetComponent(out XRGrabInteractable grab)) bonus = Game.Leveling.PlayerLevel.WeaponDamageBonus;
            else if (grab.isSelected) bonus = Game.Leveling.PlayerLevel.WeaponDamageBonus + Game.Leveling.PlayerLevel.WeaponMasterBonus;
            else if (HeldThings.InUse(grab)) bonus = weapon.thrownBonus + Game.Leveling.PlayerLevel.ThrowDamageBonus;
            else bonus = 0f;
            return Mathf.Max(1f, HitSettings.Whole((own + bonus) * weapon.boost));
        }

        /// <summary>
        /// The part of a thrown thing's Damage that's Throw Power's bonus (0 if it isn't thrown). It's added flat after a
        /// hard or smashing throw's times (user, 2026-11-22: "why Player can easily do 25+ damage with a throw?": the
        /// times used to multiply the bonus too, (1 + 1 + 6) x 4 = 32).
        /// </summary>
        public static float ThrowBonusOf(Collision collision)
        {
            var weapon = Find(collision);
            if (weapon == null || Fist.IsFistBall(weapon) || Fist.IsFlyingFist(weapon) || !IsThrown(weapon)) return 0f;
            return Mathf.Max(0f, HitSettings.Whole(Game.Leveling.PlayerLevel.ThrowDamageBonus * weapon.boost));
        }

        /// <summary>Let go a moment ago and still flying: thrown (not held, not a fist, not a shot).</summary>
        public static bool IsThrown(Component weapon)
        {
            return weapon != null && weapon.TryGetComponent(out XRGrabInteractable grab) && !grab.isSelected && HeldThings.InUse(grab);
        }

        /// <summary>The Weapon Master bonus for this weapon: its extra damage while it's held in the hand, else 0.</summary>
        public static int MasterBonus(Component weapon)
        {
            return weapon != null && weapon.TryGetComponent(out XRGrabInteractable grab) && grab.isSelected
                ? Game.Leveling.PlayerLevel.WeaponMasterBonus
                : 0;
        }

        /// <summary>Whether whatever made this collision is a weapon, a fist or a throwable (has a Weapon Power).</summary>
        public static bool Has(Collision collision) => Find(collision) != null;

        private static WeaponPower Find(Collision collision)
        {
            WeaponPower weapon = null;
            if (collision.rigidbody != null) collision.rigidbody.TryGetComponent(out weapon);
            if (weapon == null && collision.collider != null) weapon = collision.collider.GetComponentInParent<WeaponPower>();
            return weapon;
        }
    }
}
