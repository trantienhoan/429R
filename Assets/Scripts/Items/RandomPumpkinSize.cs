using UnityEngine;

namespace Game.Items
{
    /// <summary>
    /// Every tree pumpkin a different size (user, 2026-11-20: "random in size, weight and health, the bigger it is, the
    /// heavier and more health it has"). When it's made it picks a Size (1 = the usual pumpkin) between Size Range, most
    /// near the usual and the biggest the rarest (Big Ones Rarer). Then:
    /// - its scale is Size times what it would be (its Health FSM's grow-in tween still plays, just bigger or smaller);
    /// - its weight (Rigidbody mass) is times Size ^ Mass Power (3: like a real one, by its volume);
    /// - its health is times Size ^ Health Power, rounded, at least 1 (3 health: 2 for the smallest, 7 for the biggest).
    /// On Pumpkin_On_Tree; Tools > 429 Game > Items > Random Pumpkin Sizes adds it.
    /// </summary>
    [DisallowMultipleComponent]
    public class RandomPumpkinSize : MonoBehaviour
    {
        [Tooltip("Its size compared to the usual pumpkin, random between.")]
        [SerializeField] private Vector2 sizeRange = new(0.75f, 1.5f);
        [Tooltip("Above 1: most come out near the small end and the usual size, the biggest are rare (1.5: about 1 in 11 " +
                 "is 1.4 or more). 1: evenly spread.")]
        [Range(0.5f, 4f)]
        [SerializeField] private float bigOnesRarer = 1.5f;
        [Tooltip("Mass = its mass x Size ^ this.")]
        [Range(0f, 4f)]
        [SerializeField] private float massPower = 3f;
        [Tooltip("Health = its health x Size ^ this, rounded.")]
        [Range(0f, 4f)]
        [SerializeField] private float healthPower = 2f;
        [SerializeField] private string healthFsm = "Health";
        [SerializeField] private string healthVariable = "health";
        [Tooltip("For how long after it's made its own grow-in (the Health FSM's scale tween) is followed.")]
        [Min(0f)]
        [SerializeField] private float followGrowSeconds = 1.2f;

        /// <summary>Test hook: the next pumpkin made gets this size (then it's random again).</summary>
        public static float? ForceNextSize;

        /// <summary>Its size compared to the usual pumpkin.</summary>
        public float Size { get; private set; } = 1f;
        /// <summary>Its health once set (after Start).</summary>
        public float StartHealth { get; private set; }

        private float followUntil;
        private Vector3 lastSet;

        private void Awake()
        {
            if (ForceNextSize.HasValue) { Size = ForceNextSize.Value; ForceNextSize = null; }
            else
            {
                float lo = Mathf.Min(sizeRange.x, sizeRange.y), hi = Mathf.Max(sizeRange.x, sizeRange.y);
                Size = Mathf.Lerp(lo, hi, Mathf.Pow(Random.value, bigOnesRarer));
            }
            if (TryGetComponent(out Rigidbody body)) body.mass *= Mathf.Pow(Size, massPower);
            Apply();
            followUntil = Time.time + followGrowSeconds;
        }

        private void Start()
        {
            foreach (var fsm in GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != healthFsm) continue;
                var h = fsm.FsmVariables.FindFsmFloat(healthVariable);
                if (h == null) continue;
                h.Value = Mathf.Max(1f, Mathf.Round(h.Value * Mathf.Pow(Size, healthPower)));
                StartHealth = h.Value;
            }
        }

        // The grow-in tween sets its scale outright each frame: each new value it sets is made Size times bigger.
        private void LateUpdate()
        {
            if (Time.time > followUntil) { enabled = false; return; }
            if (transform.localScale != lastSet) Apply();
        }

        private void Apply()
        {
            transform.localScale *= Size;
            lastSet = transform.localScale;
        }
    }
}
