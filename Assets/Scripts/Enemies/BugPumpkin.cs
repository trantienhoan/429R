using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Enemies
{
    /// <summary>
    /// The pumpkin full of bugs the Pumpkin boss throws. It flies in an arc and bursts where it lands, letting out a
    /// few small bugs (within the game's enemy limit) and breaking like the other pumpkins. Hitting the player square
    /// on hurts them too. The boss throws it with <see cref="Launch"/>; it goes on the Bug_Pumpkin prefab's root, next to
    /// its Rigidbody and collider.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class BugPumpkin : MonoBehaviour
    {
        [Tooltip("How many bugs come out where it bursts.")]
        [Min(0)] [SerializeField] private int bugCount = 3;
        [Tooltip("What it breaks into (the pieces of a broken pumpkin).")]
        [SerializeField] private GameObject burstInto;
        [SerializeField] private GameObject smoke;
        [Tooltip("Sent to the player's PlayerHealth FSM when it hits them square on.")]
        [SerializeField] private string playerHitEvent = "PumpkinBossHit";
        [Tooltip("How close to the player's body it has to come to hit them, in metres.")]
        [Min(0.05f)] [SerializeField] private float hitRadius = 0.45f;
        [Tooltip("It bursts by itself after this many seconds, landed or not.")]
        [Min(0.5f)] [SerializeField] private float lifeSeconds = 6f;

        private Rigidbody body;
        private GameObject thrower;
        private GameObject[] bugs = System.Array.Empty<GameObject>();
        private float bornAt;
        private bool burst;
        private XROrigin player;

        public void Launch(Vector3 velocity, GameObject from, GameObject[] bugPrefabs)
        {
            body = GetComponent<Rigidbody>();
            thrower = from;
            bugs = bugPrefabs ?? System.Array.Empty<GameObject>();
            bornAt = Time.time;
            body.isKinematic = false;
            body.linearVelocity = velocity;
            body.angularVelocity = Random.insideUnitSphere * 6f;
            // It leaves the thrower's hand without bumping into the thrower.
            if (thrower != null)
            {
                foreach (var own in GetComponentsInChildren<Collider>())
                foreach (var other in thrower.GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(own, other, true);
            }
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            bornAt = Time.time;
        }

        private void Update()
        {
            if (burst) return;
            if (Time.time - bornAt > lifeSeconds) { Burst(transform.position); return; }
            if (player == null) player = FindAnyObjectByType<XROrigin>();
            if (player == null || player.Camera == null) return;
            var head = player.Camera.transform.position;
            float feet = player.transform.position.y;
            var p = transform.position;
            var closest = new Vector3(head.x, Mathf.Clamp(p.y, feet, head.y), head.z);
            if ((closest - p).magnitude > hitRadius + 0.2f) return;
            player.gameObject.GetComponents(fsms);
            foreach (var fsm in fsms)
            {
                if (fsm.FsmName == "PlayerHealth") fsm.SendEvent(playerHitEvent);
            }
            Burst(p);
        }

        private static readonly System.Collections.Generic.List<PlayMakerFSM> fsms = new();

        private void OnCollisionEnter(Collision collision)
        {
            if (burst || Time.time - bornAt < 0.12f) return;
            Burst(collision.contactCount > 0 ? collision.GetContact(0).point : transform.position);
        }

        private void Burst(Vector3 at)
        {
            if (burst) return;
            burst = true;
            if (burstInto != null) Instantiate(burstInto, at, transform.rotation, transform.parent);
            if (smoke != null) Instantiate(smoke, at, Quaternion.identity, transform.parent);
            for (int i = 0; i < bugCount && bugs.Length > 0; i++)
            {
                if (!EnemyLimit.TryTakeSlot()) break;
                var prefab = bugs[Random.Range(0, bugs.Length)];
                if (prefab == null) continue;
                var around = Random.insideUnitCircle * 1.2f;
                if (!NavMesh.SamplePosition(at + new Vector3(around.x, 0f, around.y), out var hit, 2.5f, NavMesh.AllAreas)) continue;
                var bug = Instantiate(prefab, hit.position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform.parent);
                if (bug.TryGetComponent(out SpiderBrain brain)) brain.ComeAtOnce();
            }
            Destroy(gameObject);
        }
    }
}
