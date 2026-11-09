using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.Bowling
{
    /// <summary>
    /// Grows bowling pumpkins out of the pad's sprout spots: at most Max On Pad on the pad at once, and only at the first
    /// Open Spots spots (the vines grown so far). When there are fewer, it waits Regrow Delay, then grows a new one at
    /// the free open spot most in front of the player (never right under them): it starts tiny with a puff and a whoosh
    /// and grows over Grow Seconds, and can only be picked up once grown. No new ball while Max Alive of its balls exist
    /// (on the pad or rolling). A ball stuck off the pad (on a lane, too slow to reach anything) pops after Stall
    /// Seconds. Put it on a child aligned with the pad (its local X/Z run along the pad's sides); the balls become its
    /// children. Begin starts it; Stop And Clear pops every ball it made.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BowlingBallSupply : MonoBehaviour
    {
        [Header("Balls")]
        [Tooltip("The ball to grow (Prefabs/Bowling/Bowling_Ball).")]
        [SerializeField] private GameObject ballPrefab;
        [Tooltip("Where balls grow, on the pad. A spot is used only when no ball is near it.")]
        [SerializeField] private Transform[] spots;
        [Tooltip("Most balls on the pad at once (growing ones count).")]
        [Min(1)]
        [SerializeField] private int maxOnPad = 2;
        [Tooltip("Only the first this many spots are used (the stage opens more as vines grow).")]
        [Min(0)]
        [SerializeField] private int openSpots = 8;
        [Tooltip("No new ball while this many of its balls exist (on the pad, held or rolling).")]
        [Min(1)]
        [SerializeField] private int maxAlive = 9;
        [Tooltip("Seconds a ball takes to grow.")]
        [Min(0.1f)]
        [SerializeField] private float growSeconds = 3f;
        [Tooltip("Seconds to wait, once there are too few balls on the pad, before the next one starts growing.")]
        [Min(0f)]
        [SerializeField] private float regrowDelay = 1f;

        [Header("Pad")]
        [Tooltip("Half the pad's width, in metres, along this object's local X and Z (dirt 5 m: 2.5 + 0.1 = 2.6). " +
                 "A ball inside it is on the pad.")]
        [Min(0.1f)]
        [SerializeField] private float padHalfSize = 2.6f;
        [Tooltip("A ball off the pad that has moved slower than Stall Speed this many seconds is popped.")]
        [Min(0.5f)]
        [SerializeField] private float stallSeconds = 8f;
        [Tooltip("Slower than this, in metres per second, a ball off the pad counts as stuck.")]
        [Min(0f)]
        [SerializeField] private float stallSpeed = 0.2f;
        [Tooltip("A spot closer than this to the player's feet, in metres, is skipped.")]
        [Min(0f)]
        [SerializeField] private float keepFromHead = 0.6f;
        [Tooltip("A spot with a ball closer than this, in metres, is taken.")]
        [Min(0.1f)]
        [SerializeField] private float spotTaken = 0.5f;

        [Header("Look and sound")]
        [Tooltip("Puff when a ball starts growing or pops (Smoke_Flash_Small_Puff).")]
        [SerializeField] private GameObject puff;
        [Tooltip("Size of the puff (Smoke_Flash_Small_Puff's root is scale 3, so about 0.5).")]
        [Min(0f)]
        [SerializeField] private float puffScale = 0.5f;
        [Tooltip("Played when a ball starts growing (whoosh).")]
        [SerializeField] private AudioClip growSound;
        [Tooltip("Played when a stuck ball pops, and when the balls are cleared (bubble_pop_1).")]
        [SerializeField] private AudioClip popSound;
        [Tooltip("Volume of both sounds.")]
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.7f;

        private sealed class Ball
        {
            public GameObject go;
            public Rigidbody body;
            public XRGrabInteractable grab;
            public bool growing;
            public float stillFor;
        }

        private readonly List<Ball> balls = new();
        private readonly List<Rigidbody> live = new();
        private float waited;
        private Transform head;
        private XROrigin origin;

        /// <summary>True between Begin and Stop And Clear: balls keep growing.</summary>
        public bool Running { get; private set; }

        /// <summary>Balls on the pad (grown or still growing).</summary>
        public int OnPad
        {
            get
            {
                int n = 0;
                foreach (var ball in balls)
                {
                    if (ball.go != null && (ball.growing || IsOnPad(ball.go.transform.position))) n++;
                }
                return n;
            }
        }

        /// <summary>Every live ball it made.</summary>
        public int Alive
        {
            get
            {
                int n = 0;
                foreach (var ball in balls)
                {
                    if (ball.go != null) n++;
                }
                return n;
            }
        }

        /// <summary>Most balls on the pad at once (growing ones count); at least 1.</summary>
        public int MaxOnPad
        {
            get => maxOnPad;
            set => maxOnPad = Mathf.Max(1, value);
        }

        /// <summary>Only spots 0 .. Open Spots - 1 grow balls (clamped to the spots there are).</summary>
        public int OpenSpots
        {
            get => openSpots;
            set => openSpots = Mathf.Clamp(value, 0, spots != null ? spots.Length : 0);
        }

        /// <summary>Its live balls that have finished growing and are not held (rolling or lying about).</summary>
        public IReadOnlyList<Rigidbody> Balls
        {
            get
            {
                live.Clear();
                foreach (var ball in balls)
                {
                    if (ball.go == null || ball.growing || ball.body == null) continue;
                    if (ball.grab != null && ball.grab.isSelected) continue;
                    live.Add(ball.body);
                }
                return live;
            }
        }

        /// <summary>A ball has finished growing and can be picked up.</summary>
        public event System.Action<GameObject> Grown;

        /// <summary>Starts growing balls: the first ones at once (ready after Grow Seconds).</summary>
        public void Begin()
        {
            if (ballPrefab == null)
            {
                Debug.LogError("[Bowling] Ball Supply has no ball prefab.", this);
                return;
            }
            Running = true;
            waited = 0f;
            for (int i = OnPad; i < maxOnPad; i++)
            {
                if (!Sprout()) break;
            }
        }

        /// <summary>Stops growing and pops every ball it made, held ones too (they're let go of first).</summary>
        public void StopAndClear()
        {
            Running = false;
            StopAllCoroutines();
            bool any = false;
            foreach (var ball in balls)
            {
                if (ball.go == null) continue;
                LetGo(ball.grab);
                Puff(ball.go.transform.position);
                Destroy(ball.go);
                any = true;
            }
            balls.Clear();
            if (any) Sound(popSound, transform.position);
        }

        private void OnDestroy()
        {
            // Balls that left their parent (carried off) go with the supply.
            foreach (var ball in balls)
            {
                if (ball.go != null && !ball.go.transform.IsChildOf(transform)) Destroy(ball.go);
            }
            balls.Clear();
        }

        private void Update()
        {
            balls.RemoveAll(b => b.go == null);
            float dt = Time.deltaTime;
            for (int i = balls.Count - 1; i >= 0; i--)
            {
                var ball = balls[i];
                if (ball.growing || ball.body == null) continue;
                bool held = ball.grab != null && ball.grab.isSelected;
                var v = ball.body.linearVelocity;
                if (held || ball.body.isKinematic || IsOnPad(ball.go.transform.position) ||
                    new Vector2(v.x, v.z).magnitude >= stallSpeed)
                {
                    ball.stillFor = 0f;
                    continue;
                }
                ball.stillFor += dt;
                if (ball.stillFor < stallSeconds) continue;
                Puff(ball.go.transform.position);
                Sound(popSound, ball.go.transform.position);
                Destroy(ball.go);
                balls.RemoveAt(i);
            }

            if (!Running || ballPrefab == null) return;
            if (OnPad >= maxOnPad || Alive >= maxAlive)
            {
                waited = 0f;
                return;
            }
            waited += dt;
            if (waited >= regrowDelay && Sprout()) waited = 0f;
        }

        // ------------------------------------------------------------------ growing

        private bool Sprout()
        {
            if (Alive >= maxAlive) return false;
            var spot = PickSpot();
            if (spot == null) return false;
            // Made at full size first, so the ball's Bowling Roll Assist reads its real radius in Awake.
            var go = Instantiate(ballPrefab, spot.position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
            var ball = new Ball
            {
                go = go,
                body = go.GetComponent<Rigidbody>(),
                grab = go.GetComponent<XRGrabInteractable>(),
                growing = true,
            };
            balls.Add(ball);
            StartCoroutine(Grow(ball, spot.position));
            return true;
        }

        // From tiny to full size, sitting on the floor, with a little overshoot (PlayGroundField.GrowIn's curve).
        private IEnumerator Grow(Ball ball, Vector3 floor)
        {
            var go = ball.go;
            var full = go.transform.localScale;
            float lift = Lift(go);
            var mode = CollisionDetectionMode.Discrete;
            var smoothing = RigidbodyInterpolation.None;
            if (ball.body != null)
            {
                mode = ball.body.collisionDetectionMode;
                smoothing = ball.body.interpolation;
                ball.body.interpolation = RigidbodyInterpolation.None; // moved by hand while it grows
                // Kinematic bodies can't use Continuous Dynamic.
                ball.body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                ball.body.isKinematic = true;
            }
            if (ball.grab != null) ball.grab.enabled = false;
            go.transform.localScale = full * 0.05f;
            go.transform.position = floor + Vector3.up * (lift * 0.05f);
            Puff(floor + Vector3.up * 0.2f);
            Sound(growSound, floor);

            for (float t = 0f; t < growSeconds; t += Time.deltaTime)
            {
                if (go == null) yield break;
                float k = t / growSeconds;
                float s = Mathf.SmoothStep(0.05f, 1f, k) + Mathf.Sin(k * Mathf.PI) * 0.08f;
                go.transform.localScale = full * s;
                go.transform.position = floor + Vector3.up * (lift * s);
                yield return null;
            }
            if (go == null) yield break;

            go.transform.localScale = full;
            go.transform.position = floor + Vector3.up * lift;
            if (ball.body != null)
            {
                ball.body.isKinematic = false;
                ball.body.collisionDetectionMode = mode;
                ball.body.interpolation = smoothing;
                ball.body.linearVelocity = Vector3.zero;
                ball.body.angularVelocity = Vector3.zero;
            }
            if (ball.grab != null) ball.grab.enabled = true;
            ball.growing = false;
            ball.stillFor = 0f;
            Grown?.Invoke(go);
        }

        // How far above the floor the ball's pivot sits when it rests on it, at full size.
        private static float Lift(GameObject go)
        {
            var t = go.transform;
            if (go.TryGetComponent(out SphereCollider sphere))
            {
                var scale = t.lossyScale;
                float r = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                float centre = t.TransformPoint(sphere.center).y - t.position.y;
                return Mathf.Max(0f, r - centre) + 0.005f;
            }
            if (go.GetComponentInChildren<Collider>() is { } other) return Mathf.Max(0f, t.position.y - other.bounds.min.y) + 0.005f;
            return 0.2f;
        }

        // The free spot most in front of the player, not right under them.
        private Transform PickSpot()
        {
            if (spots == null || spots.Length == 0) return null;
            FindHead();
            var feet = head != null ? new Vector3(head.position.x, 0f, head.position.z) : transform.position;
            var forward = head != null ? Vector3.ProjectOnPlane(head.forward, Vector3.up) : transform.forward;
            if (forward.sqrMagnitude < 0.0001f) forward = transform.forward;
            forward.Normalize();

            Transform best = null;
            float bestScore = float.MinValue;
            int open = Mathf.Min(openSpots, spots.Length);
            for (int i = 0; i < open; i++)
            {
                var spot = spots[i];
                if (spot == null || IsTaken(spot.position)) continue;
                var toSpot = spot.position - feet;
                toSpot.y = 0f;
                if (toSpot.magnitude < keepFromHead) continue;
                float score = Vector3.Dot(toSpot.normalized, forward);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = spot;
                }
            }
            return best;
        }

        private bool IsTaken(Vector3 spot)
        {
            foreach (var ball in balls)
            {
                if (ball.go == null) continue;
                var d = ball.go.transform.position - spot;
                d.y = 0f;
                if (d.sqrMagnitude < spotTaken * spotTaken) return true;
            }
            return false;
        }

        private bool IsOnPad(Vector3 position)
        {
            var local = transform.InverseTransformPoint(position);
            return Mathf.Abs(local.x) <= padHalfSize && Mathf.Abs(local.z) <= padHalfSize;
        }

        private void FindHead()
        {
            if (head != null) return;
            if (origin == null) origin = FindAnyObjectByType<XROrigin>();
            head = origin != null && origin.Camera != null ? origin.Camera.transform
                : (Camera.main != null ? Camera.main.transform : null);
        }

        // ------------------------------------------------------------------ helpers

        private static void LetGo(XRGrabInteractable grab)
        {
            if (grab == null || !grab.isSelected || grab.interactionManager == null) return;
            grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
        }

        private void Puff(Vector3 at)
        {
            if (puff != null) EffectPool.Play(puff, at, puffScale);
        }

        private void Sound(AudioClip clip, Vector3 at)
        {
            if (clip != null) AudioSource.PlayClipAtPoint(clip, at, volume);
        }
    }
}
