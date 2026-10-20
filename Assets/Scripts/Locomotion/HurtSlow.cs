using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using FsmFloat = HutongGames.PlayMaker.FsmFloat;

namespace Game.Locomotion
{
    /// <summary>
    /// Every hit slows the player's walking for a moment: the bigger the hit, the slower and the longer, then it eases
    /// smoothly back to normal. It watches the player's health (the PlayerHealth FSM), so every kind of damage counts:
    /// bites, blasts, boss hits, falls. Heals, revives and the health given back on loading a save (Get a Rest) don't
    /// slow, and dying ends the slow. Toughness already makes the damage smaller, so it shortens the slow too. A pause
    /// freezes the slow with the game.
    /// Puts itself on the XR Origin when the game starts, with the numbers below. To change them, add Hurt Slow to the
    /// XR Origin yourself (Add Component > Hurt Slow) and set them there: that one is used instead.
    /// </summary>
    [DisallowMultipleComponent]
    public class HurtSlow : MonoBehaviour
    {
        [Tooltip("The FSM on this object that holds the player's health, and its float variable.")]
        [SerializeField] private string healthFsm = "PlayerHealth";
        [SerializeField] private string healthVariable = "PlayerHealth";
        [Tooltip("The variable in that FSM with the player's full health. A drop that puts the health back to exactly " +
                 "full is a reset (a new run), not a hit.")]
        [SerializeField] private string fullHealthVariable = "DefautPlayerHealth";
        [Tooltip("Damage from the smallest hit to the biggest (a bug bite does 6-9, a heavy hit 40-50, the Pumpkin boss " +
                 "70-140). Bigger hits count as the biggest.")]
        [SerializeField] private Vector2 damageRange = new(6f, 120f);
        [Tooltip("Share of the walking speed right after the smallest hit, and after the biggest (0.35 = 35%).")]
        [SerializeField] private Vector2 speedAtDamage = new(0.85f, 0.35f);
        [Tooltip("How long the slow lasts after the smallest hit, and after the biggest, in seconds.")]
        [SerializeField] private Vector2 secondsAtDamage = new(0.6f, 2.5f);
        [Tooltip("Part of that time it stays at its slowest before easing back (0.35 = the first 35%).")]
        [Range(0f, 1f)]
        [SerializeField] private float holdShare = 0.35f;
        [Tooltip("Hits close together slow a little more each, but never below this share of the walking speed.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float slowest = 0.3f;
        [Tooltip("Health drops smaller than this are ignored.")]
        [Min(0.01f)]
        [SerializeField] private float minDamage = 0.5f;

        private FsmFloat health;
        private FsmFloat fullHealth;
        private float lastHealth;
        // The player was down (or getting up) last frame.
        private bool wasDown;

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
            if (origin == null || origin.GetComponent<HurtSlow>() != null) return;
            foreach (var fsm in origin.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != "PlayerHealth") continue;
                origin.gameObject.AddComponent<HurtSlow>();
                return;
            }
        }

        private void Start()
        {
            foreach (var fsm in GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != healthFsm) continue;
                health = fsm.FsmVariables.FindFsmFloat(healthVariable);
                fullHealth = fsm.FsmVariables.FindFsmFloat(fullHealthVariable);
            }
            if (health == null)
            {
                Debug.LogWarning($"[Hurt Slow] Needs a '{healthFsm}' FSM with a '{healthVariable}' float on this object.", this);
                enabled = false;
                return;
            }
            lastHealth = health.Value;
        }

        private void OnEnable() => Game.Saving.PlayerBoosts.HealthSet += TakeHealthAsIs;
        private void OnDisable() => Game.Saving.PlayerBoosts.HealthSet -= TakeHealthAsIs;

        // The health was set by the game (Get a Rest's health given back on loading), not lowered by a hit.
        private void TakeHealthAsIs()
        {
            if (health != null) lastHealth = health.Value;
        }

        private void Update()
        {
            float now = health.Value, drop = lastHealth - now;
            lastHealth = now;

            // Down, or getting back up: they stand up at full speed (the slow is ended once, as they go down).
            bool down = now <= 0f || Game.Saving.Checkpoints.IsDying ||
                (Game.Combat.PlayerRevive.Instance != null && Game.Combat.PlayerRevive.Instance.IsReviving);
            if (down)
            {
                if (!wasDown) PlayerSlow.EndHurt();
                wasDown = true;
                return;
            }
            wasDown = false;
            // Heals, revives and tiny changes.
            if (drop < minDamage) return;
            // Back to exactly full: a new run, not a hit.
            if (fullHealth != null && Mathf.Abs(now - fullHealth.Value) < 0.01f) return;

            float t = Mathf.InverseLerp(damageRange.x, damageRange.y, drop);
            PlayerSlow.Hurt(Mathf.Lerp(speedAtDamage.x, speedAtDamage.y, t), Mathf.Lerp(secondsAtDamage.x, secondsAtDamage.y, t),
                holdShare, slowest);
        }
    }
}
