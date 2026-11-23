using System;
using UnityEngine;

namespace Game.Bonus
{
    /// <summary>
    /// One pumpkin head in a hole of the whack-a-pumpkin machine (Special Stage Type 4, see WhackRound). It hides in the
    /// hole until the round pops it up as a kind (normal, gold, rainbow, clock or skull: one Look each), stays up a while,
    /// then goes back down. A mallet (WhackHammer) striking it while it's up, quickly enough (Min Whack Speed) and not
    /// upward, whacks it: it squashes and drops at once (Whacked). Gone down without a whack: Went Down. Hands and
    /// anything else do nothing. Its hit zone is a trigger, on only while it's up.
    /// </summary>
    [DisallowMultipleComponent]
    public class WhackHead : MonoBehaviour
    {
        public enum Kind { Normal, Gold, Rainbow, Clock, Skull }

        [Tooltip("One look per kind, in this order: Normal, Gold, Rainbow, Clock, Skull (only the one popped is on).")]
        [SerializeField] private GameObject[] looks = new GameObject[5];
        [Tooltip("The moving part (the looks and the hit zone): this object's child, at the hole's top when up.")]
        [SerializeField] private Transform body;
        [Tooltip("The hit zone (a trigger), on only while the head is up.")]
        [SerializeField] private Collider hitZone;
        [Tooltip("How far below the hole's top the head hides (metres).")]
        [Min(0f)]
        [SerializeField] private float hiddenDepth = 0.24f;
        [Tooltip("Seconds it takes to come up, and to go back down unwhacked.")]
        [Min(0.01f)]
        [SerializeField] private float riseSeconds = 0.12f;
        [Min(0.01f)]
        [SerializeField] private float sinkSeconds = 0.16f;
        [Tooltip("A whack: the head squashes this long, then drops back in this fast (seconds).")]
        [Min(0.01f)]
        [SerializeField] private float squashSeconds = 0.07f;
        [Min(0.01f)]
        [SerializeField] private float dropSeconds = 0.08f;

        private enum State { Down, Rising, Up, Squashed, Sinking }

        private State state = State.Down;
        private float since;
        private float upFor;
        private float minWhackSpeed = 1.2f;
        private Vector3 bodyScale = Vector3.one;
        private Renderer[] rainbowRenderers = Array.Empty<Renderer>();
        private MaterialPropertyBlock block;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>Whacked by a mallet (the head, the mallet).</summary>
        public event Action<WhackHead, WhackHammer> Whacked;

        /// <summary>Gone back down without a whack (or pulled down by the round: see Hide).</summary>
        public event Action<WhackHead> WentDown;

        /// <summary>The kind it popped up as.</summary>
        public Kind Current { get; private set; }

        /// <summary>Busy: up, or on its way up or down (it can't pop again yet).</summary>
        public bool Busy => state != State.Down;

        /// <summary>Up and whackable (coming up counts too).</summary>
        public bool Whackable => state == State.Rising || state == State.Up;

        /// <summary>The top of the head, where things pop out of it (world space).</summary>
        public Vector3 Top => body != null ? body.position + transform.up * 0.08f : transform.position;

        private void Awake()
        {
            if (body == null && transform.childCount > 0) body = transform.GetChild(0);
            if (body != null) bodyScale = body.localScale;
            if (hitZone != null) hitZone.enabled = false;
            int rainbow = (int)Kind.Rainbow;
            if (looks != null && looks.Length > rainbow && looks[rainbow] != null)
                rainbowRenderers = looks[rainbow].GetComponentsInChildren<Renderer>(true);
            Place(-hiddenDepth);
            ShowLook(-1);
        }

        /// <summary>Pops up as 'kind' for 'seconds' (then goes down unless whacked). Whacks need a blow this fast (m/s).</summary>
        public void Pop(Kind kind, float seconds, float whackSpeed)
        {
            if (Busy) return;
            Current = kind;
            upFor = Mathf.Max(0.1f, seconds);
            minWhackSpeed = whackSpeed;
            ShowLook((int)kind);
            if (body != null) body.localScale = bodyScale;
            state = State.Rising;
            since = Time.time;
            if (hitZone != null) hitZone.enabled = true;
        }

        /// <summary>Goes down now if it's up, without counting as a miss (Went Down is not raised): the round is over.</summary>
        public void Hide()
        {
            if (!Busy || state == State.Sinking || state == State.Squashed) return;
            if (hitZone != null) hitZone.enabled = false;
            StartSinking(true);
        }

        private void StartSinking(bool silent)
        {
            state = State.Sinking;
            since = Time.time;
            quiet = silent;
            sinkFrom = body != null ? Mathf.Min(0f, body.localPosition.y) : 0f;
        }

        private bool quiet;
        private float sinkFrom;

        private void Update()
        {
            float t = Time.time - since;
            switch (state)
            {
                case State.Rising:
                {
                    float k = Mathf.Clamp01(t / riseSeconds);
                    // A little overshoot: it pops.
                    float pop = Mathf.Sin(k * Mathf.PI * 0.5f) + Mathf.Sin(k * Mathf.PI) * 0.12f;
                    Place(Mathf.Lerp(-hiddenDepth, 0f, pop));
                    if (k >= 1f)
                    {
                        state = State.Up;
                        since = Time.time;
                        Place(0f);
                    }
                    break;
                }
                case State.Up:
                    if (t >= upFor)
                    {
                        if (hitZone != null) hitZone.enabled = false;
                        StartSinking(false);
                    }
                    break;
                case State.Squashed:
                {
                    float k = Mathf.Clamp01(t / squashSeconds);
                    if (body != null) body.localScale = Vector3.Scale(bodyScale, new Vector3(1f + 0.35f * k, 1f - 0.5f * k, 1f + 0.35f * k));
                    if (k >= 1f) StartSinking(true);
                    break;
                }
                case State.Sinking:
                {
                    float seconds = quiet ? dropSeconds : sinkSeconds;
                    float k = Mathf.Clamp01(t / seconds);
                    Place(Mathf.Lerp(sinkFrom, -hiddenDepth, k * k));
                    if (k >= 1f)
                    {
                        Place(-hiddenDepth);
                        ShowLook(-1);
                        if (body != null) body.localScale = bodyScale;
                        state = State.Down;
                        if (!quiet) WentDown?.Invoke(this);
                        quiet = false;
                    }
                    break;
                }
            }
            if (state != State.Down && Current == Kind.Rainbow) Rainbow();
        }

        private void OnTriggerEnter(Collider other) => TryWhack(other);

        private void OnTriggerStay(Collider other) => TryWhack(other);

        private void TryWhack(Collider other)
        {
            if (!Whackable) return;
            var hammer = other.GetComponentInParent<WhackHammer>();
            if (hammer == null) return;
            var v = hammer.Velocity;
            // Quick enough, and not swinging up (a mallet brought up from under the head doesn't count).
            if (v.magnitude < minWhackSpeed || v.y > 0.25f * v.magnitude) return;
            if (hitZone != null) hitZone.enabled = false;
            state = State.Squashed;
            since = Time.time;
            Whacked?.Invoke(this, hammer);
        }

        private void Place(float y)
        {
            if (body == null) return;
            var p = body.localPosition;
            p.y = y;
            body.localPosition = p;
        }

        private void ShowLook(int index)
        {
            if (looks == null) return;
            for (int i = 0; i < looks.Length; i++)
                if (looks[i] != null) looks[i].SetActive(i == index);
        }

        // The rainbow head cycles through the colours.
        private void Rainbow()
        {
            if (rainbowRenderers.Length == 0) return;
            block ??= new MaterialPropertyBlock();
            var colour = Color.HSVToRGB(Mathf.Repeat(Time.time * 0.9f, 1f), 0.75f, 1f);
            foreach (var r in rainbowRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColor, colour);
                block.SetColor(ColorId, colour);
                r.SetPropertyBlock(block);
            }
        }
    }
}
