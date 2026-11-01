using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game.Combat
{
    /// <summary>
    /// Hot hands, a punch combo for each hand on its own (user, 2026-10-31; each hand separately, 2026-11-01): Hits Needed
    /// punches by one hand landing on anything (enemies, furniture, walls, props; the fist's own and flying ones) within
    /// Within Seconds overheat that hand, so it glows bright red with embers coming off it. It stays hot while it keeps
    /// hitting: every hit of its own starts Keep Seconds again, and Keep Seconds with none cools it down. While hot, that
    /// hand's punches (the fist's own and flying ones) and the things it throws do Damage Bonus more (user, 2026-10-28: not
    /// weapons in the hand, shots or blasts). Once it has cooled down it takes Hits Needed hits within Within Seconds again.
    /// The last second before it ends, its glow flickers. Goes on the XR Origin (Tools > 429 Game > Player > Hot Hands And
    /// Punch Shockwave puts it there).
    /// </summary>
    public class HotHands : MonoBehaviour
    {
        [Header("The combo")]
        [Tooltip("Hits it takes (one hand's punches landing on anything)...")]
        [Min(1)]
        [SerializeField] private int hitsNeeded = 17;
        [Tooltip("...within this many seconds, to overheat that hand.")]
        [Min(0.1f)]
        [SerializeField] private float withinSeconds = 3f;
        [Tooltip("How long a hand stays hot after its last hit, in seconds (each of its hits while hot starts it again).")]
        [Min(0.1f)]
        [SerializeField] private float keepSeconds = 3f;
        [Tooltip("While hot, that hand's punches and throws do this much more damage (whole points: 1 = a punch 2).")]
        [Min(0)]
        [SerializeField] private int damageBonus = 1;

        [Header("Looks")]
        [Tooltip("A hot hand's glow (bright: an HDR colour).")]
        [ColorUsage(false, true)]
        [SerializeField] private Color glow = new(4f, 0.32f, 0.1f);
        [Tooltip("Its colour while hot (multiplies the armour's own).")]
        [SerializeField] private Color tint = new(1f, 0.42f, 0.35f);
        [Tooltip("Seconds to heat up, and to cool down.")]
        [Min(0.01f)]
        [SerializeField] private float heatUpSeconds = 0.2f;
        [Min(0.01f)]
        [SerializeField] private float coolDownSeconds = 0.6f;
        [Tooltip("Comes off a hand while it's hot (e.g. Hot_Hands_Embers). Empty: none.")]
        [SerializeField] private GameObject embers;
        [Tooltip("Played as a hand overheats. Empty: none.")]
        [SerializeField] private AudioClip startSound;
        [Range(0f, 1f)]
        [SerializeField] private float startVolume = 0.8f;

        // One hand's heat.
        private class Hand
        {
            public Fist fist;
            public readonly Queue<float> hits = new();
            // The stroke each thing was last hit by, and when: one hit per thing per swing.
            public readonly Dictionary<UnityEngine.Object, (int stroke, float at)> lastHitOn = new();
            public readonly HashSet<Renderer> painted = new();
            public bool hot;
            public float until;
            public float heat;
            public ParticleSystem embers;
        }

        private static HotHands instance;

        /// <summary>Either hand is hot now.</summary>
        public static bool Active
        {
            get
            {
                if (instance == null) return false;
                foreach (var hand in instance.hands.Values) if (hand.hot) return true;
                return false;
            }
        }

        /// <summary>This fist's hand is hot now.</summary>
        public static bool IsHot(Fist fist) => instance != null && fist != null && instance.hands.TryGetValue(fist, out var hand) && hand.hot;

        /// <summary>How much more damage this fist's punches do: Damage Bonus while its hand is hot, else 0.</summary>
        public static int BonusFor(Fist fist) => IsHot(fist) ? instance.damageBonus : 0;

        /// <summary>How much more damage a thing thrown by this hand (the interactor that let go of it) does.</summary>
        public static int BonusForThrower(IXRSelectInteractor thrower)
        {
            if (instance == null || thrower == null || thrower.transform == null) return 0;
            foreach (var fist in instance.hands.Keys)
                if (fist != null && thrower.transform.IsChildOf(fist.transform)) return BonusFor(fist);
            return 0;
        }

        /// <summary>The hot glow's colour (for things that glow with the hands, e.g. the punch's shockwave).</summary>
        public static Color GlowColor => instance != null ? instance.glow : new Color(4f, 0.32f, 0.1f);

        /// <summary>A hand overheated (true) or cooled down (false).</summary>
        public static event Action<bool> Changed;

        private const float SameBumpSeconds = 0.3f;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private readonly Dictionary<Fist, Hand> hands = new();
        private readonly List<Renderer> renderers = new();
        private readonly Dictionary<Renderer, (Color emission, Color colour)> plain = new();
        private MaterialPropertyBlock block;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            Changed = null;
        }

        private void OnEnable()
        {
            instance = this;
            Fist.Landed += OnLanded;
        }

        private void OnDisable()
        {
            Fist.Landed -= OnLanded;
            foreach (var hand in hands.Values)
            {
                hand.hot = false;
                hand.heat = 0f;
                Untint(hand);
                if (hand.embers != null) hand.embers.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            if (instance == this) instance = null;
        }

        private void Start()
        {
            foreach (var fist in FindObjectsByType<Fist>(FindObjectsInactive.Include, FindObjectsSortMode.None)) MakeEmbers(HandOf(fist));
        }

        private Hand HandOf(Fist fist)
        {
            if (fist == null) return null;
            if (!hands.TryGetValue(fist, out var hand)) hands[fist] = hand = new Hand { fist = fist };
            return hand;
        }

        // Embers on the hand. A hand still switched off at the start has no skin yet: it gets them when it first
        // overheats.
        private void MakeEmbers(Hand hand)
        {
            if (embers == null || hand == null || hand.embers != null || hand.fist.HandSkin == null) return;
            var made = Instantiate(embers, hand.fist.HandSkin.transform, false);
            made.name = "Hot Hands Embers";
            foreach (var system in made.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = system.main;
                main.playOnAwake = false;
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            // The first system plays the ones under it.
            hand.embers = made.GetComponentInChildren<ParticleSystem>(true);
        }

        // A hit by one hand: hot, it keeps that hand hot; cold, it counts towards overheating it.
        private void OnLanded(Fist.PunchLanded punch)
        {
            var hand = HandOf(punch.By);
            if (hand == null) return;
            float now = Time.time;
            if (!NewHit(hand, punch, now)) return;
            if (hand.hot)
            {
                hand.until = now + keepSeconds;
                return;
            }
            hand.hits.Enqueue(now);
            while (hand.hits.Count > 0 && now - hand.hits.Peek() > withinSeconds) hand.hits.Dequeue();
            if (hand.hits.Count >= hitsNeeded) HeatUp(hand, now);
        }

        // Each thing a swing hits is a hit (user, 2026-10-31: "if in 1 swing Player hit 5 things then that'd be count as 5
        // hits"), but a thing only once a swing: not again for its other colliders, the fist bouncing on it or the other
        // fists of a triple punch. A bump with no swing counts once per Same Bump Seconds on a thing.
        private bool NewHit(Hand hand, Fist.PunchLanded punch, float now)
        {
            if (punch.Hit == null) return true;
            UnityEngine.Object thing = punch.Hit.attachedRigidbody != null ? punch.Hit.attachedRigidbody : punch.Hit;
            var seen = hand.lastHitOn;
            if (seen.TryGetValue(thing, out var last) &&
                (punch.Stroke != 0 ? last.stroke == punch.Stroke : now - last.at < SameBumpSeconds)) return false;
            if (seen.Count > 64)
            {
                var old = new List<UnityEngine.Object>();
                foreach (var entry in seen) if (entry.Key == null || now - entry.Value.at > withinSeconds) old.Add(entry.Key);
                foreach (var key in old) seen.Remove(key);
            }
            seen[thing] = (punch.Stroke, now);
            return true;
        }

        private void HeatUp(Hand hand, float now)
        {
            hand.hot = true;
            hand.until = now + keepSeconds;
            hand.hits.Clear();
            MakeEmbers(hand);
            if (hand.embers != null) hand.embers.Play(true);
            if (startSound != null) AudioSource.PlayClipAtPoint(startSound, hand.fist.transform.position, startVolume);
            Changed?.Invoke(true);
        }

        private void CoolDown(Hand hand)
        {
            hand.hot = false;
            hand.hits.Clear();
            if (hand.embers != null) hand.embers.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Changed?.Invoke(false);
        }

        private void Update()
        {
            foreach (var hand in hands.Values)
            {
                if (hand.fist == null) continue;
                if (hand.hot && Time.time >= hand.until) CoolDown(hand);
                hand.heat = Mathf.MoveTowards(hand.heat, hand.hot ? 1f : 0f, Time.deltaTime / (hand.hot ? heatUpSeconds : coolDownSeconds));
                if (hand.heat > 0f) Tint(hand);
                else if (hand.painted.Count > 0) Untint(hand);
            }
        }

        // The hand (and its flying punches' echoes) glows, flickering in the last second.
        private void Tint(Hand hand)
        {
            block ??= new MaterialPropertyBlock();
            renderers.Clear();
            hand.fist.CollectHandRenderers(renderers);
            float left = hand.until - Time.time;
            float flicker = hand.hot && left < 1f ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 18f)) : 0.88f + 0.12f * Mathf.Sin(Time.time * 9f);
            foreach (var part in renderers)
            {
                if (part == null) continue;
                if (!plain.TryGetValue(part, out var colours))
                {
                    var material = part.sharedMaterial;
                    colours = (material != null && material.HasProperty(EmissionColor) ? material.GetColor(EmissionColor) : Color.black,
                               material != null && material.HasProperty(BaseColor) ? material.GetColor(BaseColor) : Color.white);
                    plain[part] = colours;
                }
                part.GetPropertyBlock(block);
                block.SetColor(EmissionColor, Color.Lerp(colours.emission, glow * flicker, hand.heat));
                block.SetColor(BaseColor, Color.Lerp(colours.colour, colours.colour * tint, hand.heat));
                part.SetPropertyBlock(block);
                hand.painted.Add(part);
            }
        }

        private static void Untint(Hand hand)
        {
            foreach (var part in hand.painted) if (part != null) part.SetPropertyBlock(null);
            hand.painted.Clear();
        }

        /// <summary>For tests: overheat this fist's hand now (or both hands, with none), as Hits Needed hits would.</summary>
        public static void HeatUpNow(Fist fist = null)
        {
            if (instance == null) return;
            if (fist != null)
            {
                var hand = instance.HandOf(fist);
                if (!hand.hot) instance.HeatUp(hand, Time.time);
                return;
            }
            foreach (var hand in instance.hands.Values) if (!hand.hot) instance.HeatUp(hand, Time.time);
        }
    }
}
