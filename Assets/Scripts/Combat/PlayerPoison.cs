using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using FsmFloat = HutongGames.PlayMaker.FsmFloat;

namespace Game.Combat
{
    /// <summary>
    /// Poison on the player (user, 2026-11-10: the bee bosses' poison stings). Each dose (Add: a sting shot that hits
    /// them, a bee boss's dive sting that lands) takes its Per Second off their health once a second for its Seconds, on
    /// top of the hit itself. Up to Max Doses add up; one more replaces the oldest. Every tick is cut like any hit on the
    /// player (Toughness, the boong high; whole, at least 1), but poison never takes them below Lowest Health: only a
    /// real hit can finish them. A tick isn't a hit: it goes through Player Boosts' Set Health, so it gives no hurt slow
    /// and no red lines; the health heart pulses purple meanwhile and each tick pops a purple "-2" (Player Health HUD;
    /// a pop with a real hit in it stays in the hit colour).
    /// Going down (dying, reviving) or a new run clears it; a pause freezes it.
    /// Puts itself on the XR Origin when the game starts, with the numbers below. To change them, add Player Poison to
    /// the XR Origin yourself (Add Component > Player Poison) and set them there: that one is used instead.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerPoison : MonoBehaviour
    {
        [Tooltip("At most this many doses work at once (they add up); one more replaces the oldest.")]
        [Range(1, MaxSlots)]
        [SerializeField] private int maxDoses = 3;
        [Tooltip("Seconds between ticks: each tick takes a dose's Per Second times this.")]
        [Min(0.2f)]
        [SerializeField] private float tickSeconds = 1f;
        [Tooltip("Poison never takes the player's health below this (user, 2026-11-10: only a real hit can finish them).")]
        [Min(1f)]
        [SerializeField] private float lowestHealth = 1f;

        private const string HealthFsm = "PlayerHealth";
        private const string HealthVariable = "PlayerHealth";
        private const int MaxSlots = 5;

        private struct Dose
        {
            public float perSecond;
            public int ticksLeft;
            public float addedAt;
        }

        private static PlayerPoison instance;

        // Fixed slots, so poisoning the player never makes garbage.
        private readonly Dose[] doses = new Dose[MaxSlots];
        private float nextTick;
        private FsmFloat health;
        private bool wasDown;

        /// <summary>True while any poison is working on the player.</summary>
        public static bool Active => instance != null && instance.Working();

        /// <summary>Seconds until the last dose is over (0 when not poisoned).</summary>
        public static float SecondsLeft => instance != null ? instance.Left() : 0f;

        /// <summary>The frame of the last tick that took health.</summary>
        public static int LastTickFrame { get; private set; } = -10;

        /// <summary>
        /// All the health poison ticks have taken so far (it only grows). Read it again later: the growth is how
        /// much of the health lost in between was poison; anything else came from real hits (or heals). The health
        /// HUD pops a drop with a real hit in it in the hit colour, and poison ticks on their own in purple.
        /// </summary>
        public static double HealthTaken { get; private set; }

        /// <summary>
        /// Poisons the player: <paramref name="perSecond"/> health a second (before Toughness) for
        /// <paramref name="seconds"/>. Nothing while they're down.
        /// </summary>
        public static void Add(float perSecond, float seconds)
        {
            if (instance != null && perSecond > 0f && seconds > 0f) instance.AddNow(perSecond, seconds);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            LastTickFrame = -10;
            HealthTaken = 0d;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AddToPlayer()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AddNow();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AddNow();

        // On the XR Origin that has the PlayerHealth FSM, unless it has one already.
        private static void AddNow()
        {
            var origin = FindAnyObjectByType<XROrigin>();
            if (origin == null || origin.GetComponent<PlayerPoison>() != null) return;
            foreach (var fsm in origin.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != HealthFsm) continue;
                origin.gameObject.AddComponent<PlayerPoison>();
                return;
            }
        }

        private void OnEnable()
        {
            instance = this;
            Game.Saving.Checkpoints.RunStarted += Clear;
        }

        private void OnDisable()
        {
            Game.Saving.Checkpoints.RunStarted -= Clear;
            if (instance == this) instance = null;
        }

        private void Start()
        {
            foreach (var fsm in GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == HealthFsm) health = fsm.FsmVariables.FindFsmFloat(HealthVariable);
            }
        }

        private void Update()
        {
            // Down, or getting back up: cleared once, as they go down, so they get up clean.
            if (IsDown())
            {
                if (!wasDown) Clear();
                wasDown = true;
                return;
            }
            wasDown = false;
            if (Time.time < nextTick || !Working()) return;
            // A long frame doesn't make it tick twice in a row.
            nextTick = Mathf.Max(nextTick + tickSeconds, Time.time + tickSeconds * 0.5f);
            float total = 0f;
            for (int i = 0; i < MaxSlots; i++)
            {
                if (doses[i].ticksLeft <= 0) continue;
                total += doses[i].perSecond;
                doses[i].ticksLeft--;
            }
            Hurt(total * tickSeconds);
        }

        private void AddNow(float perSecond, float seconds)
        {
            if (IsDown()) return;
            // A fresh poisoning ticks a whole tick after the sting (the sting's own hit is enough for now); a dose added
            // to one already working joins its next tick.
            if (!Working()) nextTick = Time.time + tickSeconds;
            int slot = -1;
            float oldest = float.MaxValue;
            int slots = Mathf.Clamp(maxDoses, 1, MaxSlots);
            for (int i = 0; i < slots; i++)
            {
                if (doses[i].ticksLeft <= 0)
                {
                    slot = i;
                    break;
                }
                if (doses[i].addedAt < oldest)
                {
                    oldest = doses[i].addedAt;
                    slot = i;
                }
            }
            doses[slot] = new Dose
            {
                perSecond = perSecond,
                ticksLeft = Mathf.Max(1, Mathf.RoundToInt(seconds / tickSeconds)),
                addedAt = Time.time,
            };
        }

        // One tick: cut like any hit on the player (Float Subtract Random with Player Toughness), and never below Lowest
        // Health. Set through Player Boosts, so Hurt Slow and the red lines know it isn't a hit.
        private void Hurt(float amount)
        {
            if (health == null || amount <= 0f) return;
            amount *= Game.Leveling.PlayerLevel.DamageShare * Game.Leveling.PlayerBuffs.DamageTakenScale;
            amount = Mathf.Max(1f, Mathf.Round(amount));
            float now = health.Value;
            float after = Mathf.Max(lowestHealth, now - amount);
            if (after >= now) return;
            LastTickFrame = Time.frameCount;
            // What the tick takes, so the health HUD can tell it from a real hit landing in the same drop.
            HealthTaken += now - after;
            Game.Saving.PlayerBoosts.SetHealth(after);
        }

        private bool Working()
        {
            for (int i = 0; i < MaxSlots; i++)
            {
                if (doses[i].ticksLeft > 0) return true;
            }
            return false;
        }

        private float Left()
        {
            int ticks = 0;
            for (int i = 0; i < MaxSlots; i++) ticks = Mathf.Max(ticks, doses[i].ticksLeft);
            return ticks <= 0 ? 0f : (ticks - 1) * tickSeconds + Mathf.Max(0f, nextTick - Time.time);
        }

        // The same "down" as Hurt Slow's and Player Web's: no health, the revive coins in the air, or getting back up.
        private bool IsDown()
        {
            return (health != null && health.Value <= 0f) || Game.Saving.Checkpoints.IsDying ||
                (PlayerRevive.Instance != null && PlayerRevive.Instance.IsReviving);
        }

        private void Clear()
        {
            for (int i = 0; i < MaxSlots; i++) doses[i] = default;
        }
    }
}
