using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using FsmFloat = HutongGames.PlayMaker.FsmFloat;

namespace Game.Locomotion
{
    /// <summary>
    /// The player's web meter. Every spider web ball that hits them adds web (the Web Ball's Web Added, 20%), shown as
    /// rings on the health heart. At 100% they're webbed: they walk at Lock Speed (25%) and can't teleport for Lock
    /// Seconds (5); then the web is gone and nothing adds web for Break Seconds (3), so they can get away. A spider boss's
    /// Giant Web fills it at once. Web melts away once the player hasn't been hit for Melt Delay seconds, so one spider
    /// is only a nuisance and a group is the danger. Hits while webbed or on the break still hurt but add nothing, and
    /// never make either longer. Going down (dying, reviving) or a new run clears it all; a pause freezes it.
    /// Puts itself on the XR Origin when the game starts, with the numbers below. To change them, add Player Web to the
    /// XR Origin yourself (Add Component > Player Web) and set them there: that one is used instead.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerWeb : MonoBehaviour
    {
        [Tooltip("The player's walking speed while webbed, as a share of normal (0.25 = 25%).")]
        [Range(0.05f, 1f)]
        [SerializeField] private float lockSpeed = 0.25f;
        [Tooltip("How long the player stays webbed once the web is full, in seconds. They can't teleport meanwhile.")]
        [Min(0.1f)]
        [SerializeField] private float lockSeconds = 5f;
        [Tooltip("Seconds after being webbed when nothing adds web (hits still hurt), so the player can get away.")]
        [Min(0f)]
        [SerializeField] private float breakSeconds = 3f;
        [Tooltip("Seconds after the last web hit before the web starts melting.")]
        [Min(0f)]
        [SerializeField] private float meltDelay = 2f;
        [Tooltip("How fast the web melts, as a share of a full web per second (0.1 = one ring every 2 seconds).")]
        [Min(0.01f)]
        [SerializeField] private float meltPerSecond = 0.1f;

        private const string HealthFsm = "PlayerHealth";
        private const string HealthVariable = "PlayerHealth";
        private const int RingCount = 5;
        private const float Full = 0.999f;

        private static PlayerWeb instance;

        // The web right after the last hit and when that was: it melts from there (worked out when asked, so a pause,
        // which stops the game's clock, stops the melting too).
        private float hitLevel;
        private float hitAt = -100f;
        private bool locked;
        private float lockUntil;
        private float breakUntil = -100f;
        private FsmFloat health;
        private bool wasDown;

        /// <summary>How much web the player has, 0 to 1 (1 while webbed, 0 on the break after it).</summary>
        public static float Level => instance == null || instance.OnBreakNow ? 0f : instance.locked ? 1f : instance.Melted();

        /// <summary>
        /// The web rings to show on the heart: one per 20% (rounded up), none on the break. All 5 only while webbed, so
        /// the full web always means the player is stuck (a web of 81-99% shows 4).
        /// </summary>
        public static int Rings
        {
            get
            {
                if (instance == null || instance.OnBreakNow) return 0;
                if (instance.locked) return RingCount;
                float level = instance.Melted();
                return level <= 0.0001f ? 0 : Mathf.Clamp(Mathf.CeilToInt(level * RingCount - 0.001f), 1, RingCount - 1);
            }
        }

        /// <summary>True while the player is webbed (slowed, no teleporting).</summary>
        public static bool Locked => instance != null && instance.locked;

        /// <summary>Seconds until the player is free again (0 when not webbed).</summary>
        public static float LockSecondsLeft => Locked ? Mathf.Max(0f, instance.lockUntil - Time.time) : 0f;

        /// <summary>True during the break after being webbed, when nothing adds web.</summary>
        public static bool OnBreak => instance != null && instance.OnBreakNow;

        /// <summary>
        /// Adds <paramref name="share"/> of a full web (0.2 = 20%), e.g. a web ball hit the player. Nothing while they're
        /// webbed, on the break after it, or down. A full web webs them.
        /// </summary>
        public static void Add(float share)
        {
            if (instance != null && share > 0f) instance.AddNow(Mathf.Clamp01(share));
        }

        /// <summary>Fills the web at once (a spider boss's Giant Web), as long as Add would add anything.</summary>
        public static void Fill() => Add(1f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

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
            if (origin == null || origin.GetComponent<PlayerWeb>() != null) return;
            foreach (var fsm in origin.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != HealthFsm) continue;
                origin.gameObject.AddComponent<PlayerWeb>();
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
            // Down, or getting back up: cleared once, as they go down, so they get up free.
            if (IsDown())
            {
                if (!wasDown) Clear();
                wasDown = true;
                return;
            }
            wasDown = false;
            if (!locked || Time.time < lockUntil) return;
            // Free again (Player Slow's slow ends at the same moment): the web is gone, then the break.
            locked = false;
            hitLevel = 0f;
            breakUntil = Time.time + breakSeconds;
        }

        private bool OnBreakNow => !locked && Time.time < breakUntil;

        private float Melted() => Mathf.Max(0f, hitLevel - Mathf.Max(0f, Time.time - hitAt - meltDelay) * meltPerSecond);

        private void AddNow(float share)
        {
            if (locked || OnBreakNow || IsDown()) return;
            hitLevel = Mathf.Min(1f, Melted() + share);
            hitAt = Time.time;
            if (hitLevel < Full) return;
            locked = true;
            lockUntil = Time.time + lockSeconds;
            PlayerSlow.Apply(lockSpeed, lockSeconds, true);
        }

        // The same "down" as Hurt Slow's: no health, the revive coins in the air, or getting back up.
        private bool IsDown()
        {
            return (health != null && health.Value <= 0f) || Game.Saving.Checkpoints.IsDying ||
                (Game.Combat.PlayerRevive.Instance != null && Game.Combat.PlayerRevive.Instance.IsReviving);
        }

        private void Clear()
        {
            bool wasLocked = locked;
            locked = false;
            hitLevel = 0f;
            hitAt = -100f;
            breakUntil = -100f;
            if (wasLocked) PlayerSlow.EndWebSlow();
        }
    }
}
