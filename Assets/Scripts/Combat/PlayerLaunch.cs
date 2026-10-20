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
        private bool shakeOnLand;
        // The player's health (the PlayerHealth FSM on this object), looked up when first needed.
        private HutongGames.PlayMaker.FsmFloat health;

        /// <summary>Whether the player is in the air from a launch right now.</summary>
        public bool Flying => flying;

        /// <summary>
        /// Throws the player: <paramref name="height"/> metres up and <paramref name="distance"/> metres along the floor,
        /// away from <paramref name="from"/> (as seen from the player's head, where they really stand). The player's
        /// Toughness (a level-up stat) cuts both. <paramref name="shake"/> off: no camera shake (small shoves). Returns
        /// false when there's no player to throw, or they're down.
        /// </summary>
        public static bool Launch(Vector3 from, float height, float distance, bool shake = true)
        {
            var launcher = Find();
            if (launcher == null) return false;
            // Down, dying or getting back up: nothing throws them, so they get up where they fell (bites still come while
            // they're down, they just don't hurt). A flight already going just lands.
            if (launcher.PlayerDown()) return false;
            float share = Game.Leveling.PlayerLevel.PushShare;
            launcher.Begin(from, height * share, distance * share, shake);
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

        /// <summary>
        /// Ends a flight at once, where the player is (no landing shake), e.g. they fell over a Fall Edge: the fall puts
        /// them back itself, and the rest of the throw mustn't carry on from there.
        /// </summary>
        public void Stop()
        {
            if (!flying) return;
            if (body != null && body.enabled) EndFlight(false);
            // The body is off (something else is moving the rig): the flight just ends; what it passed through is made
            // solid again when the next flight ends.
            else { flying = false; velocity = Vector3.zero; }
        }

        // The player is down: health out, the death (or the lucky coins) under way, or getting back up from a revive.
        private bool PlayerDown()
        {
            if (health == null)
            {
                foreach (var fsm in GetComponents<PlayMakerFSM>())
                {
                    if (fsm.FsmName == "PlayerHealth") health = fsm.FsmVariables.FindFsmFloat("PlayerHealth");
                }
            }
            return Game.Saving.Checkpoints.IsDying ||
                   (PlayerRevive.Instance != null && PlayerRevive.Instance.IsReviving) ||
                   (health != null && health.Value <= 0f);
        }

        private void Begin(Vector3 from, float height, float distance, bool shake)
        {
            if (height <= 0f && distance <= 0f) return;
            // The body is off while something else moves the rig (a fall over a Fall Edge): no throw, not even later.
            if (body == null || !body.enabled) return;
            // A small shove (no shake, e.g. a bug's bite) while already flying is lost in the throw: the flight carries on
            // as it was, so the view isn't jerked about in the air.
            if (flying && !shake) return;
            // Away from the hit as seen from the head: in room-scale the rig's middle can be a metre or two from where the
            // player really stands.
            var origin = GetComponent<XROrigin>();
            var standing = origin != null && origin.Camera != null ? origin.Camera.transform.position : transform.position;
            var away = standing - from;
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
            // A second hit in the air throws again, never weaker than the flight already going: up, and along the floor
            // (a weaker push keeps the throw's own speed and direction).
            if (flying && velocity.y > launch.y) launch.y = velocity.y;
            if (flying && velocity.x * velocity.x + velocity.z * velocity.z > launch.x * launch.x + launch.z * launch.z)
            {
                launch.x = velocity.x;
                launch.z = velocity.z;
            }
            if (!flying) launchedFrom = transform.position;
            // Lands with a shake if any throw of this flight shook.
            shakeOnLand = shake || (flying && shakeOnLand);
            velocity = launch;
            flightStarted = Time.time;
            flying = true;
            if (shake) Shake(launchShake);
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
            if (rb.GetComponent<Game.Enemies.PumpkinBossBrain>() != null) return false;
            // An enemy's body isn't floor; furniture that takes hits (sofas, tables) is.
            if (rb.TryGetComponent<EnemyHitDamage>(out var hits) && !hits.IsFurniture) return false;
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
            if (landed && shakeOnLand) Shake(landShake);
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
