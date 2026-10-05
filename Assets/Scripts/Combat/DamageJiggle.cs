using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Makes an object jiggle like jelly when it loses health: it squashes down, springs up taller and wobbles back to
    /// its size, from its pivot (a tree's base stays on the ground). It watches the health in its Health FSM, so every
    /// kind of damage counts: weapon hits through its Damage FSM, grenade blasts, anything that lowers it. Bigger hits
    /// jiggle harder. Its children (fruit, lights, effects) jiggle along. Goes next to the Health FSM, e.g. on the
    /// Pumpkin_Tree.
    /// </summary>
    [DisallowMultipleComponent]
    public class DamageJiggle : MonoBehaviour
    {
        [Tooltip("The FSM holding its health, and the float variable in it. Empty: it only jiggles when a script says so.")]
        [SerializeField] private string healthFsm = "Health";
        [SerializeField] private string healthVariable = "health";

        [Header("Jiggle")]
        [Tooltip("How far one point of damage squashes it, as a share of its height: 0.1 = 10%.")]
        [Min(0f)]
        [SerializeField] private float strength = 0.1f;
        [Tooltip("The most it ever squashes, however big the hit, as a share of its height.")]
        [Min(0f)]
        [SerializeField] private float maxSquash = 0.25f;
        [Tooltip("How fast it wobbles: higher = quicker, stiffer jiggles.")]
        [Min(1f)]
        [SerializeField] private float springiness = 200f;
        [Tooltip("How quickly the wobbling dies down: higher = settles sooner.")]
        [Min(0f)]
        [SerializeField] private float settle = 5f;
        [Tooltip("How much fatter it gets as it squashes, keeping its bulk (0 = only its height changes).")]
        [Range(0f, 1f)]
        [SerializeField] private float bulge = 0.5f;

        private HutongGames.PlayMaker.FsmFloat health;
        private float lastHealth;
        private float squash;
        private float squashSpeed;
        private bool jiggling;
        private Vector3 restScale;
        private Vector3 written;

        private void Start()
        {
            // Empty Health Fsm: only jiggles when a script tells it to (Jiggle), e.g. the PlayGround's ghost tree.
            if (!string.IsNullOrEmpty(healthFsm))
            {
                foreach (var fsm in GetComponents<PlayMakerFSM>())
                {
                    if (fsm.FsmName != healthFsm) continue;
                    // Find, not Get: Get hands back a new, unconnected variable when the name doesn't exist.
                    health = fsm.FsmVariables.FindFsmFloat(healthVariable);
                    break;
                }
                if (health == null) Debug.LogWarning($"[DamageJiggle] '{name}' has no '{healthFsm}' FSM with a float '{healthVariable}'; it only jiggles when told to.", this);
            }
            if (health != null) lastHealth = health.Value;

            restScale = transform.localScale;
            written = restScale;
        }

        /// <summary>Jiggles it as if it took this much damage.</summary>
        public void Jiggle(float damage = 1f)
        {
            if (!jiggling)
            {
                restScale = transform.localScale;
                jiggling = true;
            }
            // Pushed down; the spring brings it back, past its size and down again, smaller each time.
            float push = Mathf.Min(strength * Mathf.Max(damage, 0f), maxSquash);
            squashSpeed -= push * Mathf.Sqrt(springiness);
        }

        private void Update()
        {
            if (health == null) return;

            float now = health.Value;
            if (now < lastHealth - 0.0001f) Jiggle(lastHealth - now);
            lastHealth = now;
        }

        // After animations and tweens, on top of them.
        private void LateUpdate()
        {
            if (!jiggling) return;

            // Something else resized it meanwhile (a tween, say): that's its size now.
            if (transform.localScale != written) restScale = transform.localScale;

            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            squashSpeed += (-springiness * squash - settle * squashSpeed) * dt;
            squash = Mathf.Clamp(squash + squashSpeed * dt, -maxSquash, maxSquash);

            if (Mathf.Abs(squash) < 0.0005f && Mathf.Abs(squashSpeed) < 0.005f)
            {
                squash = 0f;
                squashSpeed = 0f;
                jiggling = false;
                transform.localScale = restScale;
                written = restScale;
                return;
            }

            float wide = 1f - squash * bulge;
            transform.localScale = Vector3.Scale(restScale, new Vector3(wide, 1f + squash, wide));
            written = transform.localScale;
        }

        private void OnDisable()
        {
            if (!jiggling) return;
            jiggling = false;
            squash = 0f;
            squashSpeed = 0f;
            transform.localScale = restScale;
        }
    }
}
