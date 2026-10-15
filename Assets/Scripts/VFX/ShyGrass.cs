using Game.PlayGround;
using UnityEngine;

namespace Game.VFX
{
    /// <summary>
    /// Tall grass with a shy streak. Left alone, its blades sway lightly, each in its own time, as if in a soft breeze.
    /// When the player comes close (Shy Distance), the blades duck: they bend away from the player and down to a
    /// crouch, shivering a little. Once the player has gone (past Brave Distance) for a moment, they rise again slowly,
    /// stopping halfway for a peek. With a Hideout on it, it hides a laughing pumpkin only while it's standing (ducked,
    /// whatever is inside can be seen). Goes on the clump's root; each blade is a child pivot at its foot.
    /// Tools > 429 Game > PlayGround > Make Tall Grass makes the Tall_Grass prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public class ShyGrass : MonoBehaviour
    {
        [Header("Swaying")]
        [Tooltip("The blades, each turning at its foot. Empty: every child.")]
        [SerializeField] private Transform[] blades = System.Array.Empty<Transform>();
        [Tooltip("How far the blades sway, in degrees.")]
        [Range(0f, 20f)]
        [SerializeField] private float swayDegrees = 4f;
        [Tooltip("Sways per second (each blade a little faster or slower).")]
        [Range(0.05f, 3f)]
        [SerializeField] private float swaySpeed = 0.8f;

        [Header("Shy")]
        [Tooltip("The player's head this close (metres, along the floor) and the grass ducks.")]
        [Min(0.1f)]
        [SerializeField] private float shyDistance = 2.2f;
        [Tooltip("It stands up again once the player is this far away.")]
        [Min(0.1f)]
        [SerializeField] private float braveDistance = 3f;
        [Tooltip("Seconds to duck.")]
        [Min(0.05f)]
        [SerializeField] private float hideSeconds = 0.3f;
        [Tooltip("Seconds it waits after the player has gone, then how long it takes to stand up again.")]
        [Min(0f)]
        [SerializeField] private float peekAfter = 1.2f;
        [Min(0.05f)]
        [SerializeField] private float riseSeconds = 1.6f;
        [Tooltip("Seconds it stops halfway up to peek.")]
        [Min(0f)]
        [SerializeField] private float peekSeconds = 0.5f;
        [Tooltip("How far the blades bend away from the player when ducking, in degrees.")]
        [Range(0f, 85f)]
        [SerializeField] private float leanDegrees = 60f;
        [Tooltip("How tall it is when ducking, as a share of its height.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float crouch = 0.45f;
        [Tooltip("How much it trembles while ducking, in degrees.")]
        [Range(0f, 10f)]
        [SerializeField] private float shiverDegrees = 2.5f;
        [Tooltip("Played as it ducks (optional).")]
        [SerializeField] private AudioClip hideSound;
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.4f;

        private struct Blade
        {
            public Transform pivot;
            public Quaternion rest;
            public Vector3 restScale;
            public float phase;
            public float speed;
            public float amount;
            // Ducked share (0..1), each a moment behind or ahead of the others.
            public float down;
            public float pace;
        }

        private Blade[] list = System.Array.Empty<Blade>();
        private Renderer[] shown = System.Array.Empty<Renderer>();
        private Hideout hideout;
        private Transform head;
        private bool shy;
        private float goneSince = -1f;
        private float down;
        private float peekLeft;
        private Vector3 away = Vector3.forward;

        /// <summary>Ducking (or still down).</summary>
        public bool IsHiding => down > 0.5f;

        private void Awake()
        {
            TryGetComponent(out hideout);
            if (blades == null || blades.Length == 0)
            {
                blades = new Transform[transform.childCount];
                for (int i = 0; i < blades.Length; i++) blades[i] = transform.GetChild(i);
            }
            list = new Blade[blades.Length];
            for (int i = 0; i < blades.Length; i++)
            {
                var pivot = blades[i];
                if (pivot == null) continue;
                list[i] = new Blade
                {
                    pivot = pivot,
                    rest = pivot.localRotation,
                    restScale = pivot.localScale,
                    phase = Random.Range(0f, Mathf.PI * 2f),
                    speed = Random.Range(0.8f, 1.25f),
                    amount = Random.Range(0.75f, 1.25f),
                    pace = Random.Range(0.8f, 1.3f),
                };
            }
            shown = GetComponentsInChildren<Renderer>();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            Watch(dt);

            // Nobody sees it and it's at rest: nothing to move.
            if (down <= 0f && !Seen()) return;

            float time = Time.time;
            // The breeze turns slowly; the blades sway across it.
            var breeze = Quaternion.Euler(0f, 40f * Mathf.Sin(time * 0.07f), 0f) * Vector3.right;
            var swayAxis = transform.InverseTransformDirection(breeze);
            var leanAxis = transform.InverseTransformDirection(Vector3.Cross(Vector3.up, away));

            for (int i = 0; i < list.Length; i++)
            {
                ref var b = ref list[i];
                if (b.pivot == null) continue;
                // Each blade ducks a touch sooner or later than the rest.
                float rate = down > b.down ? b.pace / hideSeconds : b.pace / riseSeconds;
                b.down = Mathf.MoveTowards(b.down, down, rate * dt);
                float d = Smooth(b.down);

                float t = time * swaySpeed * b.speed * Mathf.PI * 2f;
                float sway = swayDegrees * b.amount * (Mathf.Sin(t + b.phase) + 0.35f * Mathf.Sin(t * 2.3f + b.phase * 1.7f)) * (1f - 0.7f * d);
                float shiver = shiverDegrees * d * Mathf.Sin(time * 55f + b.phase * 3f);

                b.pivot.localRotation = Quaternion.AngleAxis(sway + shiver, swayAxis) *
                                        Quaternion.AngleAxis(leanDegrees * d * b.amount, leanAxis) * b.rest;
                b.pivot.localScale = new Vector3(b.restScale.x, b.restScale.y * Mathf.Lerp(1f, crouch, d), b.restScale.z);
            }

            // Ducked, it hides nothing.
            if (hideout != null) hideout.Covering = down < 0.5f;
        }

        // Is the player close? Ducks at once; stands up a while after they've gone, with a peek halfway.
        private void Watch(float dt)
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (head != null)
            {
                var flat = transform.position - head.position;
                flat.y = 0f;
                float distance = flat.magnitude;
                if (!shy && distance < shyDistance)
                {
                    shy = true;
                    goneSince = -1f;
                    // Away from the player, as they come near.
                    if (flat.sqrMagnitude > 0.0001f) away = flat.normalized;
                    if (hideSound != null) AudioSource.PlayClipAtPoint(hideSound, transform.position, volume);
                }
                else if (shy && distance > Mathf.Max(braveDistance, shyDistance + 0.1f))
                {
                    if (goneSince < 0f) goneSince = Time.time;
                    if (Time.time - goneSince >= peekAfter)
                    {
                        shy = false;
                        peekLeft = peekSeconds;
                    }
                }
                else if (shy)
                {
                    goneSince = -1f;
                    // Keep leaning away from where the player is now.
                    if (flat.sqrMagnitude > 0.0001f) away = Vector3.Slerp(away, flat.normalized, dt * 3f);
                }
            }

            if (shy)
            {
                down = Mathf.MoveTowards(down, 1f, dt / hideSeconds);
                return;
            }
            // Rising: a pause halfway up to peek.
            if (peekLeft > 0f && down <= 0.55f)
            {
                peekLeft -= dt;
                return;
            }
            down = Mathf.MoveTowards(down, 0f, dt / riseSeconds);
        }

        private bool Seen()
        {
            foreach (var part in shown)
            {
                if (part != null && part.isVisible) return true;
            }
            return false;
        }

        private static float Smooth(float x) => x * x * (3f - 2f * x);
    }
}
