using Unity.XR.CoreUtils;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Throws the player through the air in a real arc: up, away from what hit them, then back down onto the floor
    /// (big hits from bosses). Moves the XR Origin's Character Controller with gravity until it lands; walls stop the
    /// flight sideways. Bosses call <see cref="Launch"/>; PlayMaker uses the "Launch Player" action. Goes on the XR
    /// Origin, next to its Character Controller (added there by itself the first time a launch is asked for).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerLaunch : MonoBehaviour
    {
        [Tooltip("How fast the player falls while flying, in m/s². A bit more than real gravity keeps flights short.")]
        [Min(1f)]
        [SerializeField] private float gravity = 14f;
        [Tooltip("A flight ends after this many seconds at the most, landed or not.")]
        [Min(0.5f)]
        [SerializeField] private float maxFlightSeconds = 3f;
        [Tooltip("Camera shake events sent to the main camera's Info FSM: as the player is thrown, and as they land.")]
        [SerializeField] private string launchShake = "ShakeMed";
        [SerializeField] private string landShake = "ShakeSmall";

        private CharacterController body;
        private Vector3 velocity;
        private float flightStarted;
        private bool flying;

        /// <summary>Whether the player is in the air from a launch right now.</summary>
        public bool Flying => flying;

        /// <summary>
        /// Throws the player: <paramref name="height"/> metres up and <paramref name="distance"/> metres along the floor,
        /// away from <paramref name="from"/>. Returns false when there's no player to throw.
        /// </summary>
        public static bool Launch(Vector3 from, float height, float distance)
        {
            var launcher = Find();
            if (launcher == null) return false;
            launcher.Begin(from, height, distance);
            return true;
        }

        private static PlayerLaunch cached;

        private static PlayerLaunch Find()
        {
            if (cached != null) return cached;
            var origin = FindAnyObjectByType<XROrigin>();
            if (origin == null || !origin.TryGetComponent(out CharacterController _)) return null;
            cached = origin.GetComponent<PlayerLaunch>();
            if (cached == null) cached = origin.gameObject.AddComponent<PlayerLaunch>();
            return cached;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => cached = null;

        private void Awake()
        {
            body = GetComponent<CharacterController>();
        }

        private void Begin(Vector3 from, float height, float distance)
        {
            if (height <= 0f && distance <= 0f) return;
            var away = transform.position - from;
            away.y = 0f;
            // Straight under the attacker: thrown backwards from where the player looks.
            if (away.sqrMagnitude < 0.0001f)
            {
                var cam = Camera.main;
                away = cam != null ? -cam.transform.forward : -transform.forward;
                away.y = 0f;
            }
            away.Normalize();
            float up = Mathf.Sqrt(2f * gravity * Mathf.Max(0.05f, height));
            float air = 2f * up / gravity;
            var launch = away * (distance / air) + Vector3.up * up;
            // A second hit in the air throws again, never weaker than the flight already going.
            if (flying && velocity.y > launch.y) launch.y = velocity.y;
            if (!flying) launchedFrom = transform.position;
            velocity = launch;
            flightStarted = Time.time;
            flying = true;
            Shake(launchShake);
        }

        private void Update()
        {
            if (!flying || body == null || !body.enabled) return;
            velocity.y -= gravity * Time.deltaTime;
            groundBelow = null;
            var flags = body.Move(velocity * Time.deltaTime);
            // A wall takes the speed out of the flight; the fall carries on.
            if ((flags & CollisionFlags.Sides) != 0) { velocity.x *= 0.2f; velocity.z *= 0.2f; }
            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f) velocity.y = 0f;
            bool landed = velocity.y < 0f && (flags & CollisionFlags.Below) != 0;
            // Only the floor ends a flight: a pumpkin, a bug or the boss underneath is fallen through, so the player
            // doesn't end up standing on it.
            if (landed && groundBelow != null && !IsGround(groundBelow))
            {
                Physics.IgnoreCollision(body, groundBelow, true);
                passedThrough.Add(groundBelow);
                landed = false;
            }
            if (landed) { EndFlight(true); return; }
            // Never through the floor: well below where the throw started, the player is put back.
            if (transform.position.y < launchedFrom.y - FallLimit)
            {
                body.enabled = false;
                transform.position = launchedFrom;
                body.enabled = true;
                EndFlight(false);
                return;
            }
            if (Time.time - flightStarted > maxFlightSeconds)
            {
                // Out of time in the air: put the player down on the floor below.
                var hits = Physics.RaycastAll(transform.position + Vector3.up * 0.1f, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore);
                float best = float.MaxValue;
                foreach (var h in hits)
                {
                    if (h.collider == body || h.collider.transform.IsChildOf(transform) || !IsGround(h.collider)) continue;
                    best = Mathf.Min(best, h.distance);
                }
                if (best < float.MaxValue) body.Move(Vector3.down * Mathf.Max(0f, best - 0.1f));
                EndFlight(false);
            }
        }

        private Collider groundBelow;
        private readonly System.Collections.Generic.List<Collider> passedThrough = new();
        private Vector3 launchedFrom;
        private const float FallLimit = 3f;
        // Loose things this wide or smaller (pumpkins, candy, broken bits) aren't floor.
        private const float LooseMaxWidth = 1.5f;

        // Floor the player can land on: anything without a Rigidbody, kinematic platforms, and big heavy things; never
        // small loose things or an enemy's body.
        private static bool IsGround(Collider collider)
        {
            var rb = collider.attachedRigidbody;
            if (rb == null) return true;
            if (collider.CompareTag("Enemy") || collider.CompareTag("Monster") || rb.CompareTag("Enemy") || rb.CompareTag("Monster")) return false;
            if (rb.GetComponent<Game.Enemies.PumpkinBossBrain>() != null || rb.GetComponent<EnemyHitDamage>() != null) return false;
            if (rb.isKinematic) return true;
            var size = collider.bounds.size;
            return Mathf.Max(size.x, size.z) > LooseMaxWidth;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (flying && hit.normal.y > 0.5f) groundBelow = hit.collider;
        }

        private void EndFlight(bool landed)
        {
            flying = false;
            velocity = Vector3.zero;
            // Solid again once the flight is over.
            foreach (var c in passedThrough)
            {
                if (c != null) Physics.IgnoreCollision(body, c, false);
            }
            passedThrough.Clear();
            if (landed) Shake(landShake);
        }

        private static void Shake(string eventName)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            var cam = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmGameObject("MainCam");
            if (cam == null || cam.Value == null) return;
            foreach (var fsm in cam.Value.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == "Info") fsm.SendEvent(eventName);
            }
        }
    }
}
