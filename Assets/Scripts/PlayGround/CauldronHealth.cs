using System;
using Game.Enemies;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.PlayGround
{
    /// <summary>
    /// The cauldron's health in the cauldron defence (user, 2026-11-16: "cauldron logic works almost the same as Pumpkin
    /// Tree"). Like the tree it only takes bites from the monsters sent at it (Spider Brain's Go For: a bite lands here as
    /// a Bite Target), so the player's own hits and blasts never hurt it. Each bite takes Small/Normal/Boss Bite health
    /// by the biter's size; the pot jiggles like the tree (squash and spring), knocks (Hit Sounds) and puffs a little
    /// smoke, and its bell rings now and then (Alarm Every), so a player far away hears it's under attack. Its health
    /// shows as the red bar under the countdown above the pot (the cauldron's Health Share), with a warning there at half
    /// and at a quarter. At 0 it's Broken: the Cauldron Defence next to it smashes the pot and lets the chaos boss out.
    /// It takes no damage until the defence arms it (Arm: the countdown has started) or once it's disarmed.
    /// Goes on the cauldron's root (Fire_Woods_n_Cauldron), next to the Cauldron; Tools > 429 Game > PlayGround >
    /// Use Defend The Cauldron adds it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Cauldron))]
    public class CauldronHealth : MonoBehaviour, IBiteTarget
    {
        [Header("Health")]
        [Tooltip("Its health. A small bug's bite takes Small Bite, a normal one's Normal Bite. With two bugs biting at a " +
                 "time (the bugs' attack turns) it loses about 1-1.5 a second while the player is away, so 150 lasts about " +
                 "two minutes of nobody minding it, spread over the 5-minute cook.")]
        [Min(1f)] [SerializeField] private float maxHealth = 150f;
        [Min(0f)] [SerializeField] private float smallBite = 1f;
        [Min(0f)] [SerializeField] private float normalBite = 2f;
        [Tooltip("A boss's bite (they hunt the player in the defence, so only one sent at it by hand would bite).")]
        [Min(0f)] [SerializeField] private float bossBite = 4f;

        [Header("Look and sound")]
        [Tooltip("Played as a bite lands; one at random.")]
        [SerializeField] private AudioClip[] hitSounds = Array.Empty<AudioClip>();
        [Range(0f, 1f)] [SerializeField] private float hitVolume = 0.8f;
        [Tooltip("A little smoke where the bite lands (pooled; the shared 3D smoke, e.g. Smoke_Flash_Small_Puff).")]
        [SerializeField] private GameObject hitPuff;
        [Min(0f)] [SerializeField] private float hitPuffScale = 0.15f;
        [Tooltip("How far a bite squashes the pot, as a share of its height (more on a bigger bite, at most twice this).")]
        [Range(0f, 0.3f)] [SerializeField] private float squash = 0.06f;
        [Tooltip("Its bell rings as it's bitten, at most this often (seconds): the player hears it from across the field.")]
        [Min(0.5f)] [SerializeField] private float alarmEvery = 3f;
        [Tooltip("Shown on the countdown at half and at a quarter health.")]
        [SerializeField] private string halfWarning = "THE CAULDRON IS UNDER ATTACK!";
        [SerializeField] private string quarterWarning = "THE CAULDRON IS BREAKING!";

        /// <summary>Raised once when its health reaches 0.</summary>
        public event Action<CauldronHealth> Broken;

        private Cauldron cauldron;
        private float health;
        private bool armed;
        private bool broken;
        private float nextAlarm;
        private bool warnedHalf;
        private bool warnedQuarter;
        // The pot's jiggle: its rest size, and the spring's squash and speed.
        private Transform pot;
        private Vector3 potRest;
        private float squashNow;
        private float squashSpeed;
        private bool jiggling;

        public float Health => health;
        public float MaxHealth => maxHealth;
        public float Share => maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 0f;
        public bool IsBroken => broken;
        /// <summary>Bites count (the countdown is on).</summary>
        public bool Armed => armed;

        private void Awake()
        {
            cauldron = GetComponent<Cauldron>();
            health = maxHealth;
        }

        private void Start()
        {
            pot = cauldron != null ? cauldron.Pot : null;
            if (pot != null) potRest = pot.localScale;
            if (hitPuff != null) EffectPool.Warm(hitPuff, 2);
        }

        /// <summary>From now on bites hurt it; its health shows under the countdown.</summary>
        public void Arm()
        {
            if (broken) return;
            armed = true;
            if (cauldron != null) cauldron.HealthShare = Share;
        }

        /// <summary>Bites no longer hurt it (the defence is over); the health bar goes.</summary>
        public void Disarm()
        {
            armed = false;
            if (cauldron != null) cauldron.HealthShare = -1f;
        }

        /// <summary>A monster's bite landed on it (Spider Brain's Go For).</summary>
        public void Bitten(SpiderBrain by)
        {
            if (by == null) return;
            float damage = by.IsBoss ? bossBite : by.SizeName == "Small" ? smallBite : normalBite;
            TakeDamage(damage, by.transform.position);
        }

        /// <summary>Takes this much health (also for tests), from a hit at 'from'.</summary>
        public void TakeDamage(float damage, Vector3 from)
        {
            if (!armed || broken || damage <= 0f) return;
            health = Mathf.Max(0f, health - damage);
            if (cauldron != null) cauldron.HealthShare = Share;
            HitLook(damage, from);
            Warn();
            if (health > 0f) return;
            broken = true;
            armed = false;
            Broken?.Invoke(this);
        }

        // The pot squashes (bigger bites more), a knock, a little smoke on its side towards the biter, and now and then
        // the bell.
        private void HitLook(float damage, Vector3 from)
        {
            if (pot != null)
            {
                if (!jiggling) potRest = pot.localScale;
                jiggling = true;
                squashSpeed -= Mathf.Min(squash * damage, squash * 2f) * 14f;
            }
            var middle = cauldron != null ? cauldron.Mouth - Vector3.up * 0.6f : transform.position + Vector3.up * 0.5f;
            var toward = from - middle;
            toward.y = 0f;
            var at = middle + (toward.sqrMagnitude > 0.0001f ? toward.normalized * 0.5f : Vector3.zero);
            if (hitPuff != null) EffectPool.Play(hitPuff, at, hitPuffScale);
            if (hitSounds.Length > 0)
            {
                var clip = hitSounds[Random.Range(0, hitSounds.Length)];
                if (clip != null) AudioSource.PlayClipAtPoint(clip, at, hitVolume);
            }
            if (cauldron != null && Time.time >= nextAlarm)
            {
                nextAlarm = Time.time + alarmEvery;
                cauldron.RingBell();
            }
        }

        // At half and at a quarter: a warning on the countdown and the bell at once.
        private void Warn()
        {
            if (cauldron == null) return;
            if (!warnedQuarter && Share <= 0.25f)
            {
                warnedQuarter = warnedHalf = true;
                cauldron.ShowNote(quarterWarning, new Color(1f, 0.3f, 0.25f), 4f);
                cauldron.RingBell();
            }
            else if (!warnedHalf && Share <= 0.5f)
            {
                warnedHalf = true;
                cauldron.ShowNote(halfWarning, new Color(1f, 0.75f, 0.3f), 4f);
                cauldron.RingBell();
            }
        }

        // The pot's jelly spring (like the tree's Damage Jiggle): squashed down, it springs back past its size and settles.
        private void Update()
        {
            if (!jiggling || pot == null) return;
            const float springiness = 200f;
            const float settle = 7f;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            squashSpeed += (-springiness * squashNow - 2f * settle * squashSpeed) * dt;
            squashNow += squashSpeed * dt;
            if (Mathf.Abs(squashNow) < 0.0005f && Mathf.Abs(squashSpeed) < 0.005f)
            {
                squashNow = 0f;
                squashSpeed = 0f;
                jiggling = false;
                pot.localScale = potRest;
                return;
            }
            float wide = 1f - squashNow * 0.5f;
            pot.localScale = new Vector3(potRest.x * wide, potRest.y * (1f + squashNow), potRest.z * wide);
        }

        private void OnDisable()
        {
            if (pot != null && jiggling) pot.localScale = potRest;
            jiggling = false;
            squashNow = 0f;
            squashSpeed = 0f;
        }
    }
}
