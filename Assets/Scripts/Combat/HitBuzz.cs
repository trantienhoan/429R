using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game.Combat
{
    /// <summary>
    /// The controllers buzz only when the player deals damage: a punch, or a weapon or thing held in the hand, that hurts
    /// something. The more damage, the stronger and longer the buzz (1 damage a light tap, 6 or more a full buzz). Thrown
    /// things don't buzz (no hand is holding them), and nor do grabbing, touching or hits that do no damage.
    /// Enemy Hit Damage tells it about every hit that hurts (Dealt); for PlayMaker breakables without it (boxes, books,
    /// bottles, most pumpkins) the fist and Hit Rumble ask it to watch the thing they hit for a moment (Watch).
    /// Other buzzes (forming a fist, an overheating gun, the shop...) go through Other, which is silent while Only Damage
    /// is on.
    /// </summary>
    public static class HitBuzz
    {
        /// <summary>Only damage buzzes; every other buzz in the game is silent.</summary>
        public static bool OnlyDamage = true;

        // The buzz for the least damage (1) and from Full Damage on.
        private const float WeakStrength = 0.25f;
        private const float FullStrength = 1f;
        private const float WeakSeconds = 0.04f;
        private const float FullSeconds = 0.15f;
        private const float FullDamage = 6f;
        // One buzz per hand at most this often (a swing that hits twice).
        private const float Cooldown = 0.06f;
        // How long a PlayMaker breakable is watched after a hit.
        private const float WatchSeconds = 0.25f;
        // A far grab still flying in is further from the hand than this (metres): it isn't in the hand yet.
        private const float InHandDistance = 0.3f;

        private static readonly Dictionary<XRBaseInputInteractor, float> nextBuzz = new();
        private static readonly List<XRBaseInputInteractor> hands = new();
        private static Watcher watcher;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OnlyDamage = true;
            nextBuzz.Clear();
            hands.Clear();
            watcher = null;
        }

        /// <summary>A hit just did this much damage: the hand(s) behind it (a fist, or a hand holding what hit) buzz.</summary>
        public static void Dealt(Collision collision, float damage)
        {
            if (damage <= 0f || !HandsBehind(collision, hands)) return;
            foreach (var hand in hands) Buzz(hand, damage);
        }

        /// <summary>Buzzes this hand for this much damage ('right Away': even just after another buzz, e.g. a knock-out).</summary>
        public static void Buzz(XRBaseInputInteractor hand, float damage, bool rightAway = false)
        {
            if (hand == null || damage <= 0f) return;
            float now = Time.unscaledTime;
            if (!rightAway && nextBuzz.TryGetValue(hand, out float next) && now < next) return;
            nextBuzz[hand] = now + Cooldown;
            float hard = Mathf.InverseLerp(1f, FullDamage, damage);
            float strength = Mathf.Lerp(WeakStrength, FullStrength, hard);
            hand.SendHapticImpulse(strength, Mathf.Lerp(WeakSeconds, FullSeconds, hard));
            LastBuzz = $"{hand.transform.parent?.name} {strength:0.00} for {damage:0.#} damage at {now:0.00}";
        }

        /// <summary>The last damage buzz: which hand, how strong, for how much damage (for checking it works).</summary>
        public static string LastBuzz { get; private set; } = "";

        /// <summary>A buzz that isn't for damage (e.g. a gun overheating): silent while Only Damage is on.</summary>
        public static void Other(XRBaseInputInteractor hand, float strength, float seconds)
        {
            if (!OnlyDamage && hand != null) hand.SendHapticImpulse(strength, seconds);
        }

        /// <summary>
        /// A fist ball or a held thing ('hitter', its Rigidbody) hit something: if that's a PlayMaker breakable (no Enemy
        /// Hit Damage, which reports by itself) that loses health from this hit in the next moment, the hand(s) behind the
        /// hitter buzz.
        /// </summary>
        public static void Watch(Collision collision, Rigidbody hitter)
        {
            var target = collision.rigidbody != null ? collision.rigidbody.gameObject : collision.gameObject;
            if (target == null || hitter == null || target.GetComponentInParent<EnemyHitDamage>() != null) return;

            HutongGames.PlayMaker.FsmFloat health = null;
            PlayMakerFSM damageFsm = null;
            for (var t = target.transform; t != null && health == null; t = t.parent)
                Game.Weapons.Blast.HasHealth(t.gameObject, out health, out damageFsm);
            if (health == null && damageFsm == null) return;
            // Things that can't be broken (metal Jenga blocks, the dice, barrels with 10000) don't buzz.
            if (health != null && health.Value >= 9000f) return;
            hands.Clear();
            AddHands(hitter, hands);
            if (hands.Count == 0) return;

            if (watcher == null)
            {
                var go = new GameObject("Hit Buzz") { hideFlags = HideFlags.HideAndDontSave };
                Object.DontDestroyOnLoad(go);
                watcher = go.AddComponent<Watcher>();
            }
            watcher.Add(target, health, damageFsm, hands);
        }

        // For Dealt: what made the hit is the collision's other body (the hurt one called this).
        private static bool HandsBehind(Collision collision, List<XRBaseInputInteractor> into)
        {
            into.Clear();
            var source = collision.rigidbody;
            if (source == null) return false;
            AddHands(source, into);
            return into.Count > 0;
        }

        private static void AddHands(Rigidbody source, List<XRBaseInputInteractor> into)
        {
            var fistHand = Fist.HandOf(source);
            if (fistHand != null)
            {
                into.Add(fistHand);
                return;
            }
            if (!source.TryGetComponent<XRGrabInteractable>(out var grab) || !grab.isSelected) return;
            foreach (var interactor in grab.interactorsSelecting)
            {
                // Hands are input interactors; a far grab still flying in isn't in the hand yet.
                if (interactor is XRBaseInputInteractor hand && GentleFarGrab.HandDistance(hand.transform, grab) <= InHandDistance)
                    into.Add(hand);
            }
        }

        // Watches PlayMaker breakables for a moment after a hit: health down (or its Damage FSM in a damage state, or the
        // thing broken and gone) means the hit hurt it.
        private class Watcher : MonoBehaviour
        {
            private struct Watch
            {
                public GameObject target;
                public HutongGames.PlayMaker.FsmFloat health;
                public float start;
                public PlayMakerFSM damageFsm;
                public string state;
                public XRBaseInputInteractor[] hands;
                public float until;
            }

            private readonly List<Watch> watches = new();

            public void Add(GameObject target, HutongGames.PlayMaker.FsmFloat health, PlayMakerFSM damageFsm, List<XRBaseInputInteractor> by)
            {
                var watch = new Watch
                {
                    target = target,
                    health = health,
                    start = health != null ? health.Value : 0f,
                    damageFsm = damageFsm,
                    state = damageFsm != null ? damageFsm.ActiveStateName : "",
                    hands = by.ToArray(),
                    until = Time.unscaledTime + WatchSeconds,
                };
                // Its Damage FSM may already have handled this hit (both sides of a collision are told in any order):
                // a damage state entered this very moment. One entered earlier is still playing an earlier hit.
                if (InDamageState(watch.damageFsm) && JustEntered(watch.damageFsm)) Done(watch, 1f);
                else watches.Add(watch);
            }

            private void Update()
            {
                for (int i = watches.Count - 1; i >= 0; i--)
                {
                    var w = watches[i];
                    float damage = 0f;
                    if (w.target == null) damage = 1f;
                    else if (w.health != null && w.health.Value < w.start - 0.01f) damage = w.start - w.health.Value;
                    else if (w.damageFsm != null && w.damageFsm.ActiveStateName != w.state && InDamageState(w.damageFsm)) damage = 1f;
                    if (damage > 0f) Done(w, damage);
                    if (damage > 0f || Time.unscaledTime > w.until) watches.RemoveAt(i);
                }
            }

            private static bool JustEntered(PlayMakerFSM fsm)
            {
                var state = fsm.Fsm.ActiveState;
                return state != null && Time.realtimeSinceStartup - state.RealStartTime < 0.05f;
            }

            private static bool InDamageState(PlayMakerFSM fsm)
            {
                return fsm != null && fsm.enabled && fsm.ActiveStateName.IndexOf("damage", System.StringComparison.OrdinalIgnoreCase) >= 0;
            }

            private static void Done(Watch w, float damage)
            {
                foreach (var hand in w.hands) Buzz(hand, damage);
            }
        }
    }
}
