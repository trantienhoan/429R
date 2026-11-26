using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.Bonus
{
    /// <summary>
    /// A whack-a-pumpkin mallet (Special Stage Type 4, see WhackRound). It knows how fast its striking head moves (a
    /// pumpkin head counts a whack only from a quick, not-upward blow: see WhackHead), and it floats: it waits on its
    /// post until grabbed, and when let go it stays in the air a moment, then flies back to the post (Return After), so
    /// it can't be lost. The round makes two and takes them away at the end.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class WhackHammer : MonoBehaviour
    {
        [Tooltip("The middle of the striking head, in this object's space: its speed decides a whack.")]
        [SerializeField] private Vector3 strikePoint = new(0f, 0f, 0.25f);
        [Tooltip("Let go, it waits this long in the air, then flies back to its post (seconds).")]
        [Min(0f)]
        [SerializeField] private float returnAfter = 1.2f;
        [Tooltip("How long the flight back to the post takes (seconds).")]
        [Min(0.05f)]
        [SerializeField] private float returnSeconds = 0.45f;
        [Tooltip("How far it bobs up and down on its post (metres), and how fast (bobs per second).")]
        [SerializeField] private float bob = 0.02f;
        [SerializeField] private float bobSpeed = 0.6f;

        // The speed is measured over about this long (seconds): a blow that stops on the head still counts its swing.
        private const float SpeedWindow = 0.06f;
        private const int Samples = 12;

        private XRGrabInteractable grab;
        private Rigidbody body;
        private readonly Vector3[] points = new Vector3[Samples];
        private readonly float[] times = new float[Samples];
        private int newest = -1;
        private int count;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private bool hasHome;
        private float letGoAt = -1f;
        private bool returning;
        private float returnStart;
        private Vector3 returnFrom;
        private Quaternion returnFromTurn;

        /// <summary>The striking head's speed now (metres per second, world space).</summary>
        public Vector3 Velocity { get; private set; }

        /// <summary>Whether a hand holds it.</summary>
        public bool Held => grab != null && grab.isSelected;

        /// <summary>Where the striking head is now (world space).</summary>
        public Vector3 StrikePoint => transform.TransformPoint(strikePoint);

        public XRGrabInteractable Grab => grab;

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
            // It floats (kinematic) and flies back to its post, never thrown: XRI's throw on let-go only warned "Cannot
            // throw a kinematic Rigidbody" each time (3 times in the 2026-11-26 run's log).
            grab.throwOnDetach = false;
        }

        /// <summary>Its post: it waits there (kinematic, bobbing) until grabbed, and flies back there when let go.</summary>
        public void SetHome(Vector3 position, Quaternion rotation)
        {
            homePosition = position;
            homeRotation = rotation;
            hasHome = true;
            transform.SetPositionAndRotation(position, rotation);
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }
        }

        private void OnEnable()
        {
            if (grab == null) grab = GetComponent<XRGrabInteractable>();
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnLetGo);
        }

        private void OnDisable()
        {
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnLetGo);
        }

        private void OnGrabbed(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args)
        {
            returning = false;
            letGoAt = -1f;
        }

        private void OnLetGo(UnityEngine.XR.Interaction.Toolkit.SelectExitEventArgs args)
        {
            if (grab.isSelected) return;
            letGoAt = Time.time;
        }

        private void Update()
        {
            Measure();
            if (!hasHome || Held) return;

            if (returning)
            {
                float t = Mathf.Clamp01((Time.time - returnStart) / returnSeconds);
                float ease = t * t * (3f - 2f * t);
                transform.SetPositionAndRotation(Vector3.Lerp(returnFrom, homePosition, ease),
                                                 Quaternion.Slerp(returnFromTurn, homeRotation, ease));
                if (t >= 1f) returning = false;
                return;
            }
            if (letGoAt >= 0f)
            {
                // Held still in the air where it was let go (it isn't thrown), then back to its post.
                if (body != null && !body.isKinematic)
                {
                    body.isKinematic = true;
                    body.useGravity = false;
                }
                if (Time.time - letGoAt < returnAfter) return;
                letGoAt = -1f;
                returning = true;
                returnStart = Time.time;
                returnFrom = transform.position;
                returnFromTurn = transform.rotation;
                return;
            }
            // On its post: a slow bob, so it looks ready to take.
            if (bob > 0f) transform.position = homePosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed * Mathf.PI * 2f) * bob);
        }

        // The striking head's speed over the last Speed Window (newest place minus the one about that long ago).
        private void Measure()
        {
            float now = Time.time;
            newest = (newest + 1) % Samples;
            points[newest] = StrikePoint;
            times[newest] = now;
            if (count < Samples) count++;
            int oldest = newest;
            for (int i = 1; i < count; i++)
            {
                int index = (newest - i + Samples) % Samples;
                oldest = index;
                if (now - times[index] >= SpeedWindow) break;
            }
            float dt = now - times[oldest];
            Velocity = dt > 0.0001f ? (points[newest] - points[oldest]) / dt : Vector3.zero;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.TransformPoint(strikePoint), 0.04f);
        }
    }
}
