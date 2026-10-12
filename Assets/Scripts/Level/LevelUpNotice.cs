using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Leveling
{
    /// <summary>
    /// The Level Up notice, shown in front of the player (like the Health Warning) at the levels that need one: at
    /// Teach Levels (level 1) it explains how to open the Level Up menu (two fists bumped together twice), and at
    /// Remind Levels (level 3) it's a short reminder of that. Every other level up only plays the Level Up Sound, which
    /// plays at all of them. A notice stays until a trigger is pulled (after a short reading time), the menu is opened,
    /// or its seconds are over. If the player looks away it glides back in front of them. The game doesn't pause.
    /// Tools > 429 Game > Player > Set Up Level System builds it.
    /// </summary>
    public class LevelUpNotice : MonoBehaviour
    {
        [SerializeField] private CanvasGroup panel;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text body;
        [Tooltip("Shown once the reading time is over.")]
        [SerializeField] private TMP_Text continueText;
        [Tooltip("The headset camera. Left empty, Camera.main is used.")]
        [SerializeField] private Transform head;

        [Header("Text ({level} and {points} are filled in)")]
        [SerializeField] private string titleText = "LEVEL UP!";
        [TextArea(3, 8)]
        [SerializeField] private string teachText =
            "You reached level {level} and have {points} points to spend.\n\n" +
            "Make two fists (hold grip and trigger on both hands) and bump them together twice to open the Level Up menu.";
        [TextArea(2, 6)]
        [SerializeField] private string shortText =
            "Level {level}: {points} points to spend.\nBump your fists together twice to open the Level Up menu.";

        [Header("When")]
        [Tooltip("Reaching one of these levels shows the notice that teaches how to open the Level Up menu (Teach Text).")]
        [SerializeField] private int[] teachLevels = { 1 };
        [Tooltip("Reaching one of these levels shows the short reminder (Short Text). At every other level there's no " +
                 "notice, only the sound.")]
        [SerializeField] private int[] remindLevels = { 3 };
        [Tooltip("Played at every level up, with or without a notice.")]
        [SerializeField] private AudioClip levelUpSound;

        [Header("Timing")]
        [Tooltip("Seconds before a notice can be closed with a trigger, so it gets read.")]
        [Min(0f)]
        [SerializeField] private float readingTime = 2f;
        [Tooltip("The teaching notice goes away by itself after this many seconds.")]
        [Min(1f)]
        [SerializeField] private float teachSeconds = 30f;
        [Tooltip("The reminder goes away by itself after this many seconds.")]
        [Min(1f)]
        [SerializeField] private float shortSeconds = 5f;
        [Tooltip("Defaults to either trigger, and Enter or Space on the keyboard.")]
        [SerializeField] private InputActionProperty continueButton = new(CreateDefaultButton());
        [Tooltip("Where the notice sits, in metres from the head (x right, y up, z forward).")]
        [SerializeField] private Vector3 panelOffset = new(0f, 0.05f, 1.3f);
        [Tooltip("Looking further away than this (degrees), the notice glides back in front of the player.")]
        [SerializeField] private float followAngle = 35f;
        [Tooltip("Seconds to fade in and out.")]
        [SerializeField] private float fadeTime = 0.4f;

        private float shownAt;
        private float stays;
        private int soundFrame = -1;
        private bool showing;
        private bool closing;
        private bool gliding;

        private static InputAction CreateDefaultButton()
        {
            var action = new InputAction("Continue", InputActionType.Button);
            action.AddBinding("<XRController>{LeftHand}/triggerPressed");
            action.AddBinding("<XRController>{RightHand}/triggerPressed");
            action.AddBinding("<Keyboard>/enter");
            action.AddBinding("<Keyboard>/space");
            return action;
        }

        private void Awake()
        {
            if (panel != null) panel.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            continueButton.action?.Enable();
            PlayerLevel.LeveledUp += OnLeveledUp;
            LevelUpMenu.Opened += Close;
        }

        private void OnDisable()
        {
            // Only switch off our own button; a referenced action may be used elsewhere.
            if (continueButton.reference == null) continueButton.action?.Disable();
            PlayerLevel.LeveledUp -= OnLeveledUp;
            LevelUpMenu.Opened -= Close;
        }

        // Every level up is heard; only some are shown.
        private void OnLeveledUp(int level)
        {
            PlaySound();
            if (System.Array.IndexOf(teachLevels, level) >= 0) Show(level, true);
            else if (System.Array.IndexOf(remindLevels, level) >= 0) Show(level, false);
        }

        // Once, when several levels are gained at the same moment.
        private void PlaySound()
        {
            if (levelUpSound == null || soundFrame == Time.frameCount) return;
            soundFrame = Time.frameCount;
            if (head == null && Camera.main != null) head = Camera.main.transform;
            AudioSource.PlayClipAtPoint(levelUpSound, head != null ? head.position : transform.position);
        }

        /// <summary>
        /// Fades the notice in, in front of the player, for a level: the one that teaches how to open the Level Up
        /// menu, or the short reminder.
        /// </summary>
        public void Show(int level, bool teaching)
        {
            if (panel == null) return;

            int points = PlayerLevel.Instance != null ? PlayerLevel.Instance.UnspentPoints : 0;
            // Several levels gained at once: the notice names the one the player is at now.
            if (PlayerLevel.Instance != null) level = Mathf.Max(level, PlayerLevel.Instance.Level);
            if (title != null) title.text = Fill(titleText, level, points);
            if (body != null) body.text = Fill(teaching ? teachText : shortText, level, points);
            if (continueText != null) continueText.enabled = false;

            shownAt = Time.unscaledTime;
            stays = teaching ? teachSeconds : shortSeconds;
            showing = true;
            closing = false;
            gliding = false;
            panel.alpha = 0f;
            panel.interactable = false;
            panel.blocksRaycasts = false;
            panel.gameObject.SetActive(true);
            if (TryGetPlacement(out var position, out var rotation)) panel.transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>Fades the notice out.</summary>
        public void Close()
        {
            if (showing) closing = true;
        }

        private static string Fill(string text, int level, int points)
        {
            return string.IsNullOrEmpty(text) ? "" : text.Replace("{level}", level.ToString()).Replace("{points}", points.ToString());
        }

        private void Update()
        {
            if (!showing) return;

            // Unscaled time: the notice works the same if something pauses the game underneath it.
            float shown = Time.unscaledTime - shownAt;
            bool ready = shown >= readingTime;
            if (continueText != null) continueText.enabled = ready && !closing;
            if (!closing && (shown >= stays || (ready && continueButton.action != null && continueButton.action.WasPressedThisFrame())))
                closing = true;

            float step = fadeTime > 0f ? Time.unscaledDeltaTime / fadeTime : 1f;
            panel.alpha = Mathf.MoveTowards(panel.alpha, closing ? 0f : 1f, step);
            if (closing && panel.alpha <= 0f)
            {
                panel.gameObject.SetActive(false);
                showing = false;
                return;
            }

            FollowGaze();
        }

        // Stays put while it's read, and glides back (no snapping, which is uncomfortable in VR) once the player looks away.
        private void FollowGaze()
        {
            if (!TryGetPlacement(out var position, out var rotation)) return;

            var toPanel = panel.transform.position - head.position;
            var wanted = position - head.position;
            if (!gliding && (Vector3.Angle(toPanel, wanted) > followAngle || Mathf.Abs(toPanel.magnitude - wanted.magnitude) > 0.5f))
                gliding = true;
            if (!gliding) return;

            float blend = 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime);
            panel.transform.SetPositionAndRotation(
                Vector3.Lerp(panel.transform.position, position, blend),
                Quaternion.Slerp(panel.transform.rotation, rotation, blend));
            if (Vector3.Distance(panel.transform.position, position) < 0.02f) gliding = false;
        }

        private bool TryGetPlacement(out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = default;
            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (head == null) return false;

            // Face the way the head looks, ignoring up/down tilt (looking down, the head's up points forward).
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;

            var yaw = Quaternion.LookRotation(forward.normalized, Vector3.up);
            position = head.position + yaw * panelOffset;
            // A world-space canvas is readable when its forward points away from the viewer.
            rotation = Quaternion.LookRotation(position - head.position, Vector3.up);
            return true;
        }
    }
}
