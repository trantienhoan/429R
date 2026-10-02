using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;

namespace Game.Enemies
{
    /// <summary>
    /// A ghost balloon. It floats where it's placed, bobbing, drifting and swaying with its string swinging below, and
    /// gets knocked about by whatever hits it. Woken (hit by the player: something they hold or throw, or their hand;
    /// or, with Wake On Near, when the player comes close), it giggles and shakes, then chases the player's face and
    /// pops in it: a puff of smoke, a pop, a bite of damage, and 3-5 spiders dropped on the floor. Swatted hard while
    /// it chases, it pops where it is instead, without the bite (the spiders still drop). It flies through walls, being
    /// a ghost. Goes on the balloon's root; Tools > 429 Game > Enemies > Set Up Ghost Balloons sets up Ghost_Balloon_1-3.
    /// </summary>
    [DisallowMultipleComponent]
    public class GhostBalloon : MonoBehaviour
    {
        public enum WakeOn
        {
            Hit,
            Near,
            HitOrNear,
        }

        private enum Mood
        {
            Floating,
            Waking,
            Chasing,
            Popped,
        }

        [Header("Floating")]
        [Tooltip("How far it bobs up and down, in metres.")]
        [Min(0f)]
        [SerializeField] private float bobHeight = 0.12f;
        [Tooltip("Seconds for one bob up and down.")]
        [Min(0.1f)]
        [SerializeField] private float bobSeconds = 3f;
        [Tooltip("How far it wanders around its spot, in metres.")]
        [Min(0f)]
        [SerializeField] private float drift = 0.3f;
        [Tooltip("How far it sways, in degrees.")]
        [Min(0f)]
        [SerializeField] private float sway = 6f;
        [Tooltip("How hard a hit knocks it away, in metres per second. It drifts back to its spot afterwards.")]
        [Min(0f)]
        [SerializeField] private float knock = 1.5f;

        [Header("Waking")]
        [Tooltip("Hit: when the player hits it. Near: when the player comes close. Hit Or Near: either.")]
        [SerializeField] private WakeOn wakeOn = WakeOn.Hit;
        [Tooltip("With Near: how close the player must come, in metres along the floor (it may float high above them).")]
        [Min(0f)]
        [SerializeField] private float wakeDistance = 3f;
        [Tooltip("Played when it wakes, e.g. a creepy laugh.")]
        [SerializeField] private AudioClip wakeSound;
        [Tooltip("Seconds it shakes after waking before it gives chase.")]
        [Min(0f)]
        [SerializeField] private float wakeSeconds = 0.8f;

        [Header("Chasing")]
        [Tooltip("How fast it flies at the player, in metres per second.")]
        [Min(0.1f)]
        [SerializeField] private float chaseSpeed = 2.5f;
        [Tooltip("How quickly it gets up to speed and turns, in metres per second per second.")]
        [Min(0.1f)]
        [SerializeField] private float chaseAcceleration = 4f;
        [Tooltip("It pops when its middle is this close to the player's face, in metres.")]
        [Min(0.05f)]
        [SerializeField] private float popDistance = 0.45f;
        [Tooltip("It gives up and pops where it is after chasing this many seconds.")]
        [Min(1f)]
        [SerializeField] private float giveUpSeconds = 12f;
        [Tooltip("Swatted at least this fast (metres per second) while chasing, it pops where it is, without hurting the " +
                 "player. 0 = it can't be swatted.")]
        [Min(0f)]
        [SerializeField] private float swatSpeed = 3f;
        [Tooltip("Turns it while it chases, in degrees, if its face isn't on its blue Z arrow.")]
        [SerializeField] private float faceAngle;

        [Header("Pop")]
        [Tooltip("Effect where it pops, e.g. a puff of smoke.")]
        [SerializeField] private GameObject popEffect;
        [Tooltip("Size of the pop effect. The Hyper Casual FX smoke is about 13 m wide, so 0.12 makes it about 1.5 m.")]
        [Min(0.01f)]
        [SerializeField] private float popEffectScale = 0.12f;
        [SerializeField] private AudioClip popSound;
        [Range(0f, 1f)]
        [SerializeField] private float popVolume = 1f;
        [Tooltip("Event sent to the player's Damage FSM when it pops in their face, like a spider's bite: Damage, " +
                 "DamageHeavy, ... Empty: no damage.")]
        [SerializeField] private string playerEvent = "Damage";
        [Tooltip("Spider prefabs it drops; each picks one at random.")]
        [SerializeField] private GameObject[] spiders = System.Array.Empty<GameObject>();
        [Min(0)]
        [SerializeField] private int minSpiders = 3;
        [Min(0)]
        [SerializeField] private int maxSpiders = 5;
        [Tooltip("How far apart the spiders land, in metres.")]
        [Min(0f)]
        [SerializeField] private float spiderSpread = 0.8f;

        private const string PlayerVariable = "Player";
        private const string PlayerDamageFsm = "Damage";
        private const string SpawnedStuffVariable = "CurrentlySpawnedStuffs";
        // Spring back to its spot after a knock: slow and floaty.
        private const float KnockSpring = 3f;
        private const float KnockDamping = 1.6f;

        private Rigidbody body;
        private Vector3 middle;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private Vector3 knockOffset;
        private Vector3 knockVelocity;
        private Vector3 velocity;
        private Quaternion wakeRotation;
        private float phase;
        private float seed;
        private float moodSince;
        private Mood mood;
        private Transform head;

        /// <summary>Its balloon part (not the string), in the root's own space: the middle of the top of its mesh, and its radius.</summary>
        public static bool FindBalloon(GameObject root, out Vector3 center, out float radius)
        {
            center = Vector3.zero;
            radius = 0.25f;
            var filter = root.GetComponentInChildren<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return false;

            var bounds = filter.sharedMesh.bounds;
            // Its widest across is the balloon; the string hangs below it.
            float meshRadius = Mathf.Max(bounds.extents.x, bounds.extents.z);
            var top = new Vector3(bounds.center.x, bounds.max.y - meshRadius, bounds.center.z);
            center = root.transform.InverseTransformPoint(filter.transform.TransformPoint(top));
            var edge = root.transform.InverseTransformPoint(filter.transform.TransformPoint(top + Vector3.right * meshRadius));
            radius = Vector3.Distance(center, edge);
            return true;
        }

        private void Awake()
        {
            if (!TryGetComponent(out body)) body = gameObject.AddComponent<Rigidbody>();
            // Moved by this script; things that hit it still bounce off and tell it so.
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            FindBalloon(gameObject, out middle, out float radius);
            if (GetComponentInChildren<Collider>() == null)
            {
                var sphere = gameObject.AddComponent<SphereCollider>();
                sphere.center = middle;
                sphere.radius = radius;
            }

            phase = Random.value * Mathf.PI * 2f;
            seed = Random.value * 100f;
        }

        private void Start()
        {
            // Its spot, kept relative to whatever it's placed in, so it goes along when that moves.
            homePosition = transform.localPosition;
            homeRotation = transform.localRotation;
        }

        private void Update()
        {
            if (mood != Mood.Floating || wakeOn == WakeOn.Hit || FindHead() == null) return;

            var away = head.position - Middle();
            away.y = 0f;
            if (away.sqrMagnitude <= wakeDistance * wakeDistance) Wake();
        }

        private void FixedUpdate()
        {
            switch (mood)
            {
                case Mood.Floating:
                    Float();
                    break;
                case Mood.Waking:
                    Shake();
                    break;
                case Mood.Chasing:
                    Chase();
                    break;
            }
        }

        // Hit by something the player holds or threw, or by the player's hand.
        private void OnCollisionEnter(Collision collision)
        {
            if (mood == Mood.Popped) return;

            bool byPlayer = collision.collider.GetComponentInParent<XROrigin>() != null;
            bool byItem = collision.rigidbody != null && collision.rigidbody.GetComponentInParent<XRGrabInteractable>() != null;
            if (!byPlayer && !byItem) return;

            float speed = collision.relativeVelocity.magnitude;
            if (mood == Mood.Chasing)
            {
                if (swatSpeed > 0f && speed >= swatSpeed) Pop(false);
                return;
            }

            // Knocked away from where it was hit.
            var hitPoint = collision.contactCount > 0 ? collision.GetContact(0).point : collision.transform.position;
            var away = Middle() - hitPoint;
            if (away.sqrMagnitude > 0.0001f) knockVelocity += away.normalized * (knock * Mathf.Clamp(speed / 4f, 0.5f, 2f));

            if (mood == Mood.Floating && wakeOn != WakeOn.Near) Wake();
        }

        /// <summary>Wakes it up: it giggles, shakes, then chases the player.</summary>
        public void Wake()
        {
            if (mood != Mood.Floating) return;

            mood = Mood.Waking;
            moodSince = Time.time;
            wakeRotation = transform.rotation;
            velocity = Vector3.zero;
            if (wakeSound != null) AudioSource.PlayClipAtPoint(wakeSound, Middle());
        }

        /// <summary>Pops it: smoke, sound and spiders; with In Face, the player also takes the Player Event.</summary>
        public void Pop(bool inFace)
        {
            if (mood == Mood.Popped) return;
            mood = Mood.Popped;

            var at = Middle();
            if (popEffect != null)
            {
                var effect = Instantiate(popEffect, at, Quaternion.identity);
                effect.transform.localScale = Vector3.one * popEffectScale;
                Destroy(effect, 4f);
            }
            if (popSound != null) AudioSource.PlayClipAtPoint(popSound, at, popVolume);
            if (inFace && !string.IsNullOrEmpty(playerEvent)) SendToPlayer(playerEvent);

            DropSpiders(inFace && head != null ? head.position : at);
            Destroy(gameObject);
        }

        private void Float()
        {
            float dt = Time.fixedDeltaTime;
            float t = Time.time;
            knockVelocity += (-knockOffset * KnockSpring - knockVelocity * KnockDamping) * dt;
            knockOffset += knockVelocity * dt;

            var parent = transform.parent;
            var home = parent != null ? parent.TransformPoint(homePosition) : homePosition;
            var homeTurn = parent != null ? parent.rotation * homeRotation : homeRotation;

            var bob = Vector3.up * (Mathf.Sin(t / bobSeconds * Mathf.PI * 2f + phase) * bobHeight);
            var wander = new Vector3(Mathf.PerlinNoise(seed, t * 0.1f) - 0.5f, 0f, Mathf.PerlinNoise(seed + 7f, t * 0.1f) - 0.5f) * (2f * drift);
            var swaying = Quaternion.Euler(Mathf.Sin(t * 0.7f + phase) * sway, 0f, Mathf.Cos(t * 0.53f + phase) * sway);
            var turn = Lean(knockVelocity, 12f) * homeTurn * swaying;

            // Swings around its balloon, so the string swings below it.
            var balloon = home + homeTurn * Scaled(middle) + bob + wander + knockOffset;
            Place(balloon, turn);
        }

        private void Shake()
        {
            var jitter = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(-8f, 8f), Random.Range(-8f, 8f));
            Place(Middle(), jitter * wakeRotation);
            if (Time.time - moodSince < wakeSeconds) return;

            mood = Mood.Chasing;
            moodSince = Time.time;
        }

        private void Chase()
        {
            if (FindHead() == null) return;

            var balloon = Middle();
            var toFace = head.position - balloon;
            float distance = toFace.magnitude;
            if (distance <= popDistance)
            {
                Pop(true);
                return;
            }
            if (Time.time - moodSince > giveUpSeconds)
            {
                Pop(false);
                return;
            }

            // Straight at the face, weaving a little like it's giggling.
            float t = Time.time;
            var weave = new Vector3(Mathf.Sin(t * 3.1f + phase), Mathf.Sin(t * 2.3f + seed) * 0.5f, Mathf.Cos(t * 2.7f + phase)) * 0.4f;
            var wanted = toFace / distance * chaseSpeed + weave;
            velocity = Vector3.MoveTowards(velocity, wanted, chaseAcceleration * Time.fixedDeltaTime);

            var flat = toFace;
            flat.y = 0f;
            var facing = flat.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(flat) * Quaternion.Euler(0f, faceAngle, 0f) : transform.rotation;
            Place(balloon + velocity * Time.fixedDeltaTime, Lean(velocity, 8f) * facing);
        }

        // Puts the balloon part at 'balloon', turned 'turn', moving the root to match.
        private void Place(Vector3 balloon, Quaternion turn)
        {
            body.MoveRotation(turn);
            body.MovePosition(balloon - turn * Scaled(middle));
        }

        // Tips its top towards where it's moving, so the string trails behind.
        private static Quaternion Lean(Vector3 moving, float degreesPerSpeed)
        {
            moving.y = 0f;
            float speed = moving.magnitude;
            if (speed < 0.01f) return Quaternion.identity;
            return Quaternion.AngleAxis(Mathf.Min(speed * degreesPerSpeed, 25f), Vector3.Cross(Vector3.up, moving / speed));
        }

        private Vector3 Middle()
        {
            return transform.TransformPoint(middle);
        }

        private Vector3 Scaled(Vector3 local)
        {
            return Vector3.Scale(local, transform.lossyScale);
        }

        private Transform FindHead()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            return head;
        }

        private static void SendToPlayer(string eventName)
        {
            var player = FsmVariables.GlobalVariables.FindFsmGameObject(PlayerVariable);
            if (player == null || player.Value == null) return;
            foreach (var fsm in player.Value.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == PlayerDamageFsm) fsm.SendEvent(eventName);
            }
        }

        private void DropSpiders(Vector3 above)
        {
            if (spiders == null || spiders.Length == 0 || maxSpiders <= 0) return;

            var floor = FloorBelow(above);
            var holder = FsmVariables.GlobalVariables.FindFsmGameObject(SpawnedStuffVariable);
            var parent = holder != null && holder.Value != null ? holder.Value.transform : null;
            int count = Random.Range(Mathf.Min(minSpiders, maxSpiders), maxSpiders + 1);
            for (int i = 0; i < count; i++)
            {
                // Within the game's enemy limit, so a popped balloon can't swamp the Quest.
                if (!EnemyLimit.TryTakeSlot()) break;

                var prefab = spiders[Random.Range(0, spiders.Length)];
                if (prefab == null) continue;

                var spread = Random.insideUnitCircle * spiderSpread;
                var spot = floor + new Vector3(spread.x, 0f, spread.y);
                if (NavMesh.SamplePosition(spot, out var onMesh, 2f, NavMesh.AllAreas)) spot = onMesh.position;
                Instantiate(prefab, spot, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), parent);
            }
        }

        // The first thing under 'point' that isn't the player or this balloon.
        private Vector3 FloorBelow(Vector3 point)
        {
            var hits = Physics.RaycastAll(point, Vector3.down, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            var floor = point;
            foreach (var hit in hits)
            {
                if (hit.distance >= nearest || hit.collider.attachedRigidbody == body) continue;
                if (hit.collider.GetComponentInParent<XROrigin>() != null) continue;
                nearest = hit.distance;
                floor = hit.point;
            }
            return floor;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!FindBalloon(gameObject, out var center, out _)) return;
            if (wakeOn == WakeOn.Hit) return;

            Gizmos.color = new Color(0.6f, 0.4f, 1f, 0.6f);
            var around = transform.TransformPoint(center);
            const int segments = 32;
            var previous = around + new Vector3(wakeDistance, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                var next = around + new Vector3(Mathf.Cos(angle) * wakeDistance, 0f, Mathf.Sin(angle) * wakeDistance);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
#endif
    }
}
