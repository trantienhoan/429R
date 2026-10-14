using UnityEngine;

namespace Game.VFX
{
    /// <summary>
    /// Makes an object jiggle like jelly when it's hit: it squashes flat, springs back up taller and rocks away from the
    /// hit, wobbling a few times before it settles. It moves around its pivot, so put it on the object itself, or on an
    /// empty parent at the base of several meshes so they all jiggle together (e.g. a pumpkin and its cracked version).
    /// It jiggles by itself whenever the health in the Health FSM on it or above it drops (whatever did the damage); the
    /// Jiggle Object action (PlayMaker, Tween) jiggles it from any state, e.g. a little for a hit too weak to hurt.
    /// Quick hits add up (to a limit); soft ones wait until it has settled, so something touching it can't keep it going.
    /// It only changes its own scale and rotation, so tweens and Look At on a parent still work.
    /// </summary>
    [DisallowMultipleComponent]
    public class Jiggle : MonoBehaviour
    {
        [Tooltip("Jiggle by itself when the health in the Health FSM on this object or above it drops.")]
        [SerializeField] private bool jiggleWhenHurt = true;
        [Tooltip("How strong a hurting hit's jiggle is: 1 = the Squash and Tilt below.")]
        [Min(0f)]
        [SerializeField] private float hurtStrength = 1f;
        [Tooltip("How much a strength 1 hit squashes it: 0.3 = 30% shorter (and wider, so it keeps its volume). It then " +
                 "springs back up taller, a little less each wobble.")]
        [Range(0f, 0.6f)]
        [SerializeField] private float squash = 0.3f;
        [Tooltip("How far a strength 1 hit rocks it away from the hit, in degrees.")]
        [Range(0f, 45f)]
        [SerializeField] private float tilt = 16f;
        [Tooltip("Wobbles per second.")]
        [Range(1f, 12f)]
        [SerializeField] private float wobblesPerSecond = 4.5f;
        [Tooltip("Seconds until it has (almost) settled.")]
        [Range(0.2f, 3f)]
        [SerializeField] private float settleTime = 0.9f;
        [Tooltip("A soft jiggle (strength below 1, e.g. a touch too weak to hurt) is skipped while it's still jiggling " +
                 "and for this many seconds after the last one, so a hand resting against it can't keep it wobbling.")]
        [Min(0f)]
        [SerializeField] private float softHitPause = 0.75f;
        [Tooltip("The FSM holding the health, and its float variable.")]
        [SerializeField] private string healthFsm = "Health";
        [SerializeField] private string healthVariable = "health";

        // The rocking is a little slower than the squashing, so the two don't move in step (looks more alive).
        private const float TiltSpeed = 0.8f;
        private const float MaxSquash = 0.6f;
        private const float MaxStep = 1f / 240f;
        // Quick hits add up, but never past this many full hits' worth.
        private const float MostHits = 1.25f;

        private Vector3 restScale;
        private Quaternion restRotation;
        private bool hasRest;
        private bool moving;
        // Squash: + taller, - flatter. Tilt: a world-space axis whose length is the angle in degrees.
        private float squashAmount, squashSpeed;
        private Vector3 tiltAmount, tiltSpeed;

        private float nextSoftHit;

        private HutongGames.PlayMaker.FsmFloat health;
        private float lastHealth;
        private bool lookedForHealth;

        /// <summary>Jiggles away from the player. Strength 1 = a full jiggle (Squash and Tilt), 0.3 = a little one.</summary>
        public void Hit(float strength = 1f) => Hit(strength, null);

        /// <summary>Jiggles away from <paramref name="from"/> (e.g. what hit it); none = away from the player.</summary>
        public void Hit(float strength, GameObject from)
        {
            if (strength <= 0f || !isActiveAndEnabled) return;
            bool soft = strength < 1f;
            if (soft && (moving || Time.time < nextSoftHit)) return;
            if (soft) nextSoftHit = Time.time + softHitPause;
            if (!moving) TakeRest();

            float speed = 2f * Mathf.PI * wobblesPerSecond;
            // A push towards flat first; the spring makes it bounce back up taller.
            float mostSquash = squash * speed * MostHits;
            squashSpeed = Mathf.Clamp(squashSpeed - squash * strength * speed, -mostSquash, mostSquash);

            var source = from != null ? from.transform.position
                : Camera.main != null ? Camera.main.transform.position
                : transform.position - transform.forward;
            var away = transform.position - source;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = Random.insideUnitSphere;
            away.y = 0f;
            // Turning around this axis tips its top away from the hit.
            var axis = Vector3.Cross(Vector3.up, away).normalized;
            tiltSpeed = Vector3.ClampMagnitude(tiltSpeed + axis * (tilt * strength * speed * TiltSpeed), tilt * speed * TiltSpeed * MostHits);
            moving = true;
        }

        private void Awake()
        {
            TakeRest();
        }

        private void OnDisable()
        {
            // Hidden or switched off mid-jiggle: back to its rest shape, so it comes back as it was.
            if (moving) Settle();
        }

        private void TakeRest()
        {
            restScale = transform.localScale;
            restRotation = transform.localRotation;
            hasRest = true;
        }

        private void Update()
        {
            if (!jiggleWhenHurt) return;
            if (!lookedForHealth) FindHealth();
            if (health == null) return;

            float now = health.Value;
            if (now < lastHealth - 0.01f) Hit(hurtStrength);
            lastHealth = now;
        }

        private void FindHealth()
        {
            lookedForHealth = true;
            foreach (var fsm in GetComponentsInParent<PlayMakerFSM>(true))
            {
                if (fsm.FsmName != healthFsm) continue;
                health = fsm.FsmVariables.FindFsmFloat(healthVariable);
                if (health != null)
                {
                    lastHealth = health.Value;
                    return;
                }
            }
        }

        private void LateUpdate()
        {
            if (!moving || !hasRest) return;

            float squashOmega = 2f * Mathf.PI * wobblesPerSecond;
            float tiltOmega = squashOmega * TiltSpeed;
            // How much it's held back each wobble, so it settles (to about 5%) within Settle Time.
            float squashDamping = 2f * Mathf.Min(1f, 3f / (squashOmega * settleTime)) * squashOmega;
            float tiltDamping = 2f * Mathf.Min(1f, 3f / (tiltOmega * settleTime)) * tiltOmega;

            // Small steps keep the spring steady on a slow frame.
            float left = Mathf.Min(Time.deltaTime, 0.1f);
            while (left > 0f)
            {
                float dt = Mathf.Min(left, MaxStep);
                left -= dt;
                squashSpeed += (-squashOmega * squashOmega * squashAmount - squashDamping * squashSpeed) * dt;
                squashAmount = Mathf.Clamp(squashAmount + squashSpeed * dt, -MaxSquash, MaxSquash);
                tiltSpeed += (-tiltOmega * tiltOmega * tiltAmount - tiltDamping * tiltSpeed) * dt;
                tiltAmount += tiltSpeed * dt;
            }
            tiltAmount = Vector3.ClampMagnitude(tiltAmount, 60f);

            if (Mathf.Abs(squashAmount) < 0.001f && Mathf.Abs(squashSpeed) < 0.01f &&
                tiltAmount.sqrMagnitude < 0.01f && tiltSpeed.sqrMagnitude < 0.25f)
            {
                Settle();
                return;
            }

            // Taller is thinner and flatter is wider, so it keeps its volume.
            float height = 1f + squashAmount;
            float width = 1f / Mathf.Sqrt(height);
            transform.localScale = Vector3.Scale(restScale, new Vector3(width, height, width));

            // The rocking is in the world (away from the hit), whichever way the parent faces now.
            var parentRotation = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
            float angle = tiltAmount.magnitude;
            var rock = angle > 0.0001f ? Quaternion.AngleAxis(angle, tiltAmount / angle) : Quaternion.identity;
            transform.localRotation = Quaternion.Inverse(parentRotation) * rock * parentRotation * restRotation;
        }

        private void Settle()
        {
            moving = false;
            squashAmount = squashSpeed = 0f;
            tiltAmount = tiltSpeed = Vector3.zero;
            transform.localScale = restScale;
            transform.localRotation = restRotation;
        }
    }
}
