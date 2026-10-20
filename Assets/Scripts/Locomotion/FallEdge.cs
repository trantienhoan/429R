using System.Collections.Generic;
using Game.Enemies;
using Game.Weapons;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.Locomotion
{
    /// <summary>
    /// Where the floor ends: a box put past an edge (e.g. the rooftop's rim), inside the base it belongs to, so it comes
    /// and goes with that base. Shown as a red box in the Scene view; nothing shows in the game.
    /// - The player stepping in (the floor under their head, so reaching or leaning out doesn't count) falls: the view
    ///   drops and blacks out, they're put back where they stood a moment before and it costs health (Fall Out Of World
    ///   on the XR Origin does that part).
    /// - Anything else that gets over it falls away and is gone: its health runs out first, so enemies and breakables
    ///   die the usual way (counts, drops; but no EXP, the player didn't earn it), then it drops out of sight and is
    ///   destroyed. Things whose name has a Keep
    ///   Names word (the seeds, the revive coin) come back next to the player instead, so nothing needed is lost.
    ///   Things in a hand, the level's own fixed parts (kinematic, frozen in place, the immovable stone blocks) and the
    ///   pumpkin boss never fall.
    /// - Enemies don't walk in: it cuts a hole in their NavMesh while it's there.
    /// - Only While Smaller Than: it counts only while its base is small (the Living Room's round-1 ring, which is floor
    ///   once round 2 grows the room to 1.5).
    /// Keep it clear of enemy spawn points.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public class FallEdge : MonoBehaviour
    {
        [Tooltip("Things whose name has one of these words aren't lost: they come back next to the player.")]
        [SerializeField] private string[] keepNames = { "Seed", "Coin" };
        [Tooltip("Seconds something falls (out of sight) before it's destroyed.")]
        [Min(0.1f)]
        [SerializeField] private float fallSeconds = 1.5f;
        [Tooltip("Enemies don't walk in (it cuts a hole in their NavMesh).")]
        [SerializeField] private bool blockEnemies = true;
        [Tooltip("0 = always. Otherwise it only counts while its base is smaller than this (the base's scale), e.g. 1.25 " +
                 "for the Living Room's round-1 ring: round 2 grows the room to 1.5 and has floor there.")]
        [Min(0f)]
        [SerializeField] private float onlyWhileSmallerThan;

        private const float Every = 0.1f;
        private static readonly List<FallEdge> all = new();
        // Things already falling (from any edge), so each is handled once.
        private static readonly HashSet<GameObject> falling = new();
        private static readonly Collider[] hits = new Collider[128];

        private BoxCollider box;
        private NavMeshObstacle hole;
        private float nextLook;
        private bool working;
        private float workingSince;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            all.Clear();
            falling.Clear();
        }

        /// <summary>Whether this point is over any Fall Edge.</summary>
        public static bool Covers(Vector3 point) => At(point) != null;

        /// <summary>The Fall Edge over this point (the newest, if several), or null.</summary>
        public static FallEdge At(Vector3 point)
        {
            FallEdge newest = null;
            foreach (var edge in all)
            {
                if (edge.Contains(point) && (newest == null || edge.WorkingFor < newest.WorkingFor)) newest = edge;
            }
            return newest;
        }

        /// <summary>
        /// Seconds since it started counting (its base appeared, or shrank below Only While Smaller Than). An edge that
        /// has only just appeared round the player didn't get stepped into.
        /// </summary>
        public float WorkingFor => Time.time - workingSince;

        private bool Working => onlyWhileSmallerThan <= 0f || Mathf.Abs(transform.lossyScale.x) < onlyWhileSmallerThan;

        // Added in the editor: a see-through box (it blocks nothing); a new box reaches from under the floor to above
        // the head, a box already sized is kept.
        private void Reset()
        {
            box = GetComponent<BoxCollider>();
            box.isTrigger = true;
            if (box.size == Vector3.one && box.center == Vector3.zero)
            {
                box.center = new Vector3(0f, 0.5f, 0f);
                box.size = new Vector3(2f, 3f, 2f);
            }
            gameObject.layer = 2;   // Ignore Raycast: rays (pointing, teleporting, line of sight) go through it
        }

        private void Awake()
        {
            box = GetComponent<BoxCollider>();
            box.isTrigger = true;
            if (blockEnemies && !TryGetComponent(out hole))
            {
                hole = gameObject.AddComponent<NavMeshObstacle>();
                hole.shape = NavMeshObstacleShape.Box;
                hole.center = box.center;
                hole.size = box.size;
                hole.carving = true;
                hole.carveOnlyStationary = true;
            }
        }

        private void OnEnable()
        {
            all.Add(this);
            working = Working;
            workingSince = Time.time;
            if (hole != null) hole.enabled = working;
        }

        private void OnDisable() => all.Remove(this);

        public bool Contains(Vector3 point)
        {
            if (box == null || !working) return false;
            var local = transform.InverseTransformPoint(point) - box.center;
            var half = box.size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        private void FixedUpdate()
        {
            // Switched on or off by its base's size (Only While Smaller Than).
            bool now = Working;
            if (now != working)
            {
                working = now;
                if (now) workingSince = Time.time;
                if (hole != null) hole.enabled = now;
            }
            if (!working || Time.time < nextLook) return;
            nextLook = Time.time + Every;

            var scale = transform.lossyScale;
            var half = Vector3.Scale(box.size * 0.5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            int count = Physics.OverlapBoxNonAlloc(transform.TransformPoint(box.center), half, hits, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) Take(hits[i]);
        }

        private void Take(Collider part)
        {
            if (part == null || part.GetComponentInParent<XROrigin>() != null) return;
            var agent = part.GetComponentInParent<NavMeshAgent>();
            var body = part.attachedRigidbody;
            var thing = agent != null ? agent.gameObject : body != null ? body.gameObject : null;
            if (thing == null || falling.Contains(thing)) return;
            // The level's own fixed parts (kinematic, frozen in place, or the immovable stone blocks), and things held in a
            // hand or a holder, stay.
            if (agent == null && body.isKinematic) return;
            if (body != null && ((body.constraints & RigidbodyConstraints.FreezePosition) == RigidbodyConstraints.FreezePosition ||
                                 Game.Combat.PushWeight.IsImmovable(body))) return;
            if (thing.TryGetComponent<XRGrabInteractable>(out var grab) && grab.isSelected) return;
            if (thing.GetComponent<PumpkinBossBrain>() != null) return;
            // The player's own fists (the punching balls and the flying ones, which are pooled and reused) never fall.
            if (body != null && Game.Combat.Fist.IsFistBall(body)) return;
            // Only once it's over the edge, not just touching it.
            var center = agent != null || body == null ? thing.transform.position : body.worldCenterOfMass;
            if (!Contains(center)) return;

            if (Kept(thing.name)) BringBack(thing);
            else Lose(thing, agent);
        }

        private bool Kept(string name)
        {
            foreach (var word in keepNames)
            {
                if (!string.IsNullOrEmpty(word) && name.IndexOf(word, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        // Dropped next to the player instead, a little in front of them.
        private static void BringBack(GameObject thing)
        {
            var head = Camera.main != null ? Camera.main.transform : null;
            if (head == null) return;
            var ahead = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            ahead = ahead.sqrMagnitude > 0.01f ? ahead.normalized : Vector3.forward;
            var origin = head.GetComponentInParent<XROrigin>();
            float floor = origin != null ? origin.transform.position.y : head.position.y - 1.6f;
            var spot = new Vector3(head.position.x, floor + 0.8f, head.position.z) + ahead * 0.7f;
            if (thing.TryGetComponent<Rigidbody>(out var body))
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = spot;
            }
            thing.transform.position = spot;
        }

        // Its health runs out (so it dies the usual way, but gives no EXP: the player didn't earn it), then it drops
        // through the floor and is destroyed.
        private void Lose(GameObject thing, NavMeshAgent agent)
        {
            falling.Add(thing);
            RunOutOfHealth(thing);
            if (agent != null) agent.enabled = false;
            if (thing.TryGetComponent<LandOnNavMesh>(out var land)) land.enabled = false;
            foreach (var part in thing.GetComponentsInChildren<Collider>()) part.enabled = false;
            if (!thing.TryGetComponent<Rigidbody>(out var body)) body = thing.AddComponent<Rigidbody>();
            body.isKinematic = false;
            body.useGravity = true;
            Destroy(thing, fallSeconds);
        }

        private static void RunOutOfHealth(GameObject thing)
        {
            foreach (var part in thing.GetComponentsInChildren<Transform>())
            {
                if (!Blast.HasHealth(part.gameObject, out var health, out _) || health == null) continue;
                // Still alive: the edge takes it, not the player, so no EXP (one the player already broke keeps its EXP).
                if (health.Value > 0f) Game.Leveling.PlayerLevel.NoRewardFor(thing);
                health.Value = 0f;
                return;
            }
        }

        private void OnDestroy()
        {
            falling.RemoveWhere(x => x == null);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var shape = box != null ? box : GetComponent<BoxCollider>();
            if (shape == null) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            // Red, or orange for one that only counts while its base is small.
            var tint = onlyWhileSmallerThan > 0f ? new Color(1f, 0.55f, 0.1f) : new Color(1f, 0.12f, 0.12f);
            Gizmos.color = new Color(tint.r, tint.g, tint.b, 0.18f);
            Gizmos.DrawCube(shape.center, shape.size);
            Gizmos.color = new Color(tint.r, tint.g, tint.b, 0.9f);
            Gizmos.DrawWireCube(shape.center, shape.size);
        }
#endif
    }
}
