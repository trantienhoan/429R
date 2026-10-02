using TMPro;
using UnityEngine;

namespace Game.Paths
{
    /// <summary>
    /// A circle on the floor the player steps into to take a path. After standing in it for Seconds (head over the
    /// circle) the GAMESTAGES bool Game Stages Bool, e.g. Gate_1, is true, for the GAMESTAGES FSM to act on (its Bool All
    /// True with Cube_1 and Gate_1 sends Next1). Stepping out turns it false again and the next visit counts from the
    /// start; it's also false whenever the gate is gone. A 3-2-1 countdown shows on the floor while waiting. A Seed Lock
    /// opens one where it stood; one can also be placed by hand.
    /// </summary>
    [DisallowMultipleComponent]
    public class PathGate : MonoBehaviour
    {
        [Tooltip("GAMESTAGES bool that's true while the player has stood in the circle long enough, e.g. Gate_1; " +
                 "stepping out turns it false.")]
        [SerializeField] private string gameStagesBool = "Gate_1";
        [Tooltip("How close to the middle (metres, along the floor) the player's head must be to count as standing in it.")]
        [Min(0.1f)]
        [SerializeField] private float radius = 0.85f;
        [Tooltip("How long the player must stay in it, in seconds.")]
        [Min(0f)]
        [SerializeField] private float seconds = 3f;
        [Tooltip("Font of the countdown on the floor. Empty: no countdown.")]
        [SerializeField] private TMP_FontAsset font;
        [Tooltip("Optional countdown sound. A clip longer than a second (a whole countdown, like count_down.wav) plays " +
                 "once when the countdown starts; a shorter one plays on every number. Stepping out stops it.")]
        [SerializeField] private AudioClip tickSound;
        [Tooltip("Optional sound when the player has stood in it long enough.")]
        [SerializeField] private AudioClip doneSound;

        // A tick sound longer than this is a whole countdown, played once.
        private const float WholeCountdownLength = 1f;

        private float inside;
        private int shown;
        private bool on;
        private TextMeshPro countdown;
        private AudioSource ticks;
        private Transform head;

        /// <summary>Whether the player is standing in it and has been long enough (its GAMESTAGES bool is true).</summary>
        public bool IsOn => on;

        public string GameStagesBool => gameStagesBool;

        /// <summary>Sets it up when it's created from code, before its first frame.</summary>
        public void Configure(string boolName, float circleRadius, float holdSeconds, TMP_FontAsset countdownFont, AudioClip tick = null, AudioClip finished = null)
        {
            gameStagesBool = boolName;
            radius = circleRadius;
            seconds = holdSeconds;
            font = countdownFont;
            tickSound = tick;
            doneSound = finished;
        }

        private void Update()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (head == null) return;

            var offset = head.position - transform.position;
            float height = offset.y;
            offset.y = 0f;
            bool standingIn = offset.sqrMagnitude <= radius * radius && height > -0.5f && height < 3f;
            if (!standingIn)
            {
                inside = 0f;
                Show(0);
                StopTicks();
                if (on) SetOn(false, true);
                return;
            }
            if (on) return;

            // Game time: the pause menu stops the countdown.
            inside += Time.deltaTime;
            if (inside < seconds)
            {
                Show(Mathf.CeilToInt(seconds - inside));
                return;
            }

            Show(0);
            Play(doneSound);
            SetOn(true, true);
        }

        // Gone (the stage was cleared, or the game is closing): the player can't be standing in it.
        private void OnDisable()
        {
            inside = 0f;
            // The countdown is put right on the next frame it's back; switching it off now, while the gate itself is
            // being switched off, isn't allowed.
            shown = -1;
            if (on) SetOn(false, false);
        }

        private void SetOn(bool value, bool warnIfMissing)
        {
            on = value;
            GameStages.SetBool(gameStagesBool, value, this, warnIfMissing);
        }

        // Lies flat on the floor, upright for the player looking down at it.
        private void LateUpdate()
        {
            if (countdown == null || !countdown.gameObject.activeSelf || head == null) return;

            var forward = head.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = head.up;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f) countdown.transform.rotation = Quaternion.LookRotation(Vector3.down, forward);
        }

        private void Show(int number)
        {
            if (number == shown) return;
            bool starting = shown <= 0 && number > 0;
            shown = number;
            if (number > 0 && tickSound != null && (starting || tickSound.length <= WholeCountdownLength)) Ticks().PlayOneShot(tickSound);

            if (font == null) return;
            if (countdown == null) CreateCountdown();
            countdown.gameObject.SetActive(number > 0);
            if (number > 0) countdown.text = number.ToString();
        }

        private void CreateCountdown()
        {
            var go = new GameObject("Countdown");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            countdown = go.AddComponent<TextMeshPro>();
            countdown.font = font;
            // About 40 cm tall numbers.
            countdown.fontSize = 6f;
            countdown.fontStyle = FontStyles.Bold;
            countdown.alignment = TextAlignmentOptions.Center;
            countdown.color = Color.white;
            countdown.rectTransform.sizeDelta = new Vector2(1.5f, 1f);
        }

        // The done sound plays on its own, so stepping out right after doesn't cut it off.
        private void Play(AudioClip clip)
        {
            if (clip != null) AudioSource.PlayClipAtPoint(clip, transform.position + Vector3.up);
        }

        // The countdown has its own speaker over the circle, so it can be stopped.
        private AudioSource Ticks()
        {
            if (ticks != null) return ticks;

            var go = new GameObject("Countdown Sound");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up;
            ticks = go.AddComponent<AudioSource>();
            ticks.playOnAwake = false;
            ticks.spatialBlend = 1f;
            return ticks;
        }

        private void StopTicks()
        {
            if (ticks != null) ticks.Stop();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.6f);
            const int segments = 32;
            var previous = transform.position + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                var next = transform.position + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
#endif
    }
}
