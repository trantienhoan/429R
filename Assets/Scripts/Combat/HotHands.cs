using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Hot hands, a kill combo: killing Kills Needed enemies within Within Seconds overheats the player's hands, so they
    /// glow bright red with embers coming off them, for Keep Seconds; every kill while hot starts those seconds again.
    /// While hot, the player's punches (the fist's own and flying ones) and throws do Damage Bonus more (user, 2026-10-28:
    /// not weapons in the hand, shots or blasts). Once the hands have cooled down it takes Kills Needed kills within Within
    /// Seconds again.
    /// A kill is an enemy that gives EXP dying of the player's doing (not furniture, not one a Fall Edge took). The last
    /// second before it ends, the glow flickers. Goes on the XR Origin (Tools > 429 Game > Player > Hot Hands And Punch
    /// Shockwave puts it there).
    /// </summary>
    public class HotHands : MonoBehaviour
    {
        [Header("The combo")]
        [Tooltip("Kills it takes...")]
        [Min(1)]
        [SerializeField] private int killsNeeded = 5;
        [Tooltip("...within this many seconds, to overheat the hands.")]
        [Min(0.1f)]
        [SerializeField] private float withinSeconds = 3f;
        [Tooltip("How long the hands stay hot after the last kill, in seconds (each kill while hot starts it again).")]
        [Min(0.1f)]
        [SerializeField] private float keepSeconds = 3f;
        [Tooltip("While hot, the player's punches and throws do this much more damage (whole points: 1 = a punch 2).")]
        [Min(0)]
        [SerializeField] private int damageBonus = 1;

        [Header("Looks")]
        [Tooltip("The hands' glow while hot (bright: an HDR colour).")]
        [ColorUsage(false, true)]
        [SerializeField] private Color glow = new(4f, 0.32f, 0.1f);
        [Tooltip("Their colour while hot (multiplies the armour's own).")]
        [SerializeField] private Color tint = new(1f, 0.42f, 0.35f);
        [Tooltip("Seconds to heat up, and to cool down.")]
        [Min(0.01f)]
        [SerializeField] private float heatUpSeconds = 0.2f;
        [Min(0.01f)]
        [SerializeField] private float coolDownSeconds = 0.6f;
        [Tooltip("Comes off each hand while hot (e.g. Hot_Hands_Embers). Empty: none.")]
        [SerializeField] private GameObject embers;
        [Tooltip("Played as the hands overheat. Empty: none.")]
        [SerializeField] private AudioClip startSound;
        [Range(0f, 1f)]
        [SerializeField] private float startVolume = 0.8f;

        private static HotHands instance;

        /// <summary>The hands are hot now.</summary>
        public static bool Active => instance != null && instance.hot;

        /// <summary>The player's punches and throws do this much more damage: Damage Bonus while hot, else 0.</summary>
        public static int DamageBonus => Active ? instance.damageBonus : 0;

        /// <summary>How hot the hands look now, 0 (cold) to 1 (overheated).</summary>
        public static float Heat => instance != null ? instance.heat : 0f;

        /// <summary>The hot glow's colour (for things that glow with the hands, e.g. the punch's shockwave).</summary>
        public static Color GlowColor => instance != null ? instance.glow : new Color(4f, 0.32f, 0.1f);

        /// <summary>The hands overheated (true) or cooled down (false).</summary>
        public static event Action<bool> Changed;

        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private readonly Queue<float> kills = new();
        private readonly List<Renderer> hands = new();
        private readonly Dictionary<Renderer, (Color emission, Color colour)> plain = new();
        private readonly List<ParticleSystem> emberSystems = new();
        private readonly HashSet<Fist> emberHands = new();
        private MaterialPropertyBlock block;
        private Fist[] fists = Array.Empty<Fist>();
        private bool hot;
        private float until;
        private float heat;
        private bool tinted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            Changed = null;
        }

        private void OnEnable()
        {
            instance = this;
            EnemyHitDamage.Died += OnDied;
        }

        private void OnDisable()
        {
            EnemyHitDamage.Died -= OnDied;
            hot = false;
            heat = 0f;
            Untint();
            foreach (var system in emberSystems) if (system != null) system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (instance == this) instance = null;
        }

        private void Start()
        {
            fists = FindObjectsByType<Fist>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            MakeEmbers();
        }

        // Embers on each hand. A hand still switched off at the start has no skin yet: it gets them when the hands
        // first overheat.
        private void MakeEmbers()
        {
            if (embers == null) return;
            foreach (var fist in fists)
            {
                if (fist == null || fist.HandSkin == null || emberHands.Contains(fist)) continue;
                emberHands.Add(fist);
                var made = Instantiate(embers, fist.HandSkin.transform, false);
                made.name = "Hot Hands Embers";
                foreach (var system in made.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = system.main;
                    main.playOnAwake = false;
                    system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
                // The first system plays the ones under it.
                var first = made.GetComponentInChildren<ParticleSystem>(true);
                if (first != null) emberSystems.Add(first);
            }
        }

        // A kill: hot, it keeps them hot; cold, it counts towards overheating.
        private void OnDied(EnemyHitDamage enemy)
        {
            if (enemy == null || !enemy.CountsAsKill || enemy.noReward) return;
            float now = Time.time;
            if (hot)
            {
                until = now + keepSeconds;
                return;
            }
            kills.Enqueue(now);
            while (kills.Count > 0 && now - kills.Peek() > withinSeconds) kills.Dequeue();
            if (kills.Count >= killsNeeded) HeatUp(now);
        }

        private void HeatUp(float now)
        {
            hot = true;
            until = now + keepSeconds;
            kills.Clear();
            MakeEmbers();
            foreach (var system in emberSystems) if (system != null) system.Play(true);
            var eyes = Camera.main;
            if (startSound != null) AudioSource.PlayClipAtPoint(startSound, eyes != null ? eyes.transform.position : transform.position, startVolume);
            Changed?.Invoke(true);
        }

        private void CoolDown()
        {
            hot = false;
            kills.Clear();
            foreach (var system in emberSystems) if (system != null) system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Changed?.Invoke(false);
        }

        private void Update()
        {
            if (hot && Time.time >= until) CoolDown();
            float target = hot ? 1f : 0f;
            heat = Mathf.MoveTowards(heat, target, Time.deltaTime / (hot ? heatUpSeconds : coolDownSeconds));
            if (heat > 0f) Tint();
            else if (tinted) Untint();
        }

        // The hands (and their flying punches' echoes) glow, flickering in the last second.
        private void Tint()
        {
            block ??= new MaterialPropertyBlock();
            hands.Clear();
            foreach (var fist in fists) if (fist != null) fist.CollectHandRenderers(hands);
            float left = until - Time.time;
            float flicker = hot && left < 1f ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 18f)) : 0.88f + 0.12f * Mathf.Sin(Time.time * 9f);
            foreach (var hand in hands)
            {
                if (hand == null) continue;
                if (!plain.TryGetValue(hand, out var colours))
                {
                    var material = hand.sharedMaterial;
                    colours = (material != null && material.HasProperty(EmissionColor) ? material.GetColor(EmissionColor) : Color.black,
                               material != null && material.HasProperty(BaseColor) ? material.GetColor(BaseColor) : Color.white);
                    plain[hand] = colours;
                }
                hand.GetPropertyBlock(block);
                block.SetColor(EmissionColor, Color.Lerp(colours.emission, glow * flicker, heat));
                block.SetColor(BaseColor, Color.Lerp(colours.colour, colours.colour * tint, heat));
                hand.SetPropertyBlock(block);
            }
            tinted = true;
        }

        private void Untint()
        {
            foreach (var hand in plain.Keys) if (hand != null) hand.SetPropertyBlock(null);
            tinted = false;
        }

        /// <summary>For tests: overheat the hands now, as Kills Needed kills would.</summary>
        public static void HeatUpNow()
        {
            if (instance != null && !instance.hot) instance.HeatUp(Time.time);
        }
    }
}
