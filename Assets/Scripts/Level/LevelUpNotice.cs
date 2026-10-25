using Game.Menus;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Leveling
{
    /// <summary>
    /// The Level Up notice, shown in front of the player (like the Health Warning) at the levels that need one: at
    /// Teach Levels (level 1) it explains how to open the Level Up menu (two fists bumped together twice), at
    /// Remind Levels (level 3) it's a short reminder of that, and at Bonus Levels (level 10) it tells about the Bonus
    /// Stats and the arrow on the menu's right. Every other level up only plays the Level Up Sound, which
    /// plays at all of them. A notice comes up a moment after the level up (Show Delay), once the level-up aura has
    /// burst, and pauses the game while it's up (Pause Game). It stays until a trigger is pulled (after a short reading
    /// time) or its seconds are over. If the player looks away it glides back in front of them. While the pause menu is
    /// open the notice hides and waits. Tools > 429 Game > Player > Set Up Level System builds it.
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
        [TextArea(2, 6)]
        [SerializeField] private string bonusText =
            "Level {level}! Bonus Stats unlocked.\n\nOpen the Level Up menu (bump your fists twice) and press the arrow on its right.";

        [Header("New punches (Fist Size unlocks them; shown once the Level Up menu is closed)")]
        [SerializeField] private string punchTitle = "NEW PUNCH!";
        [Tooltip("The title when Swing and Uppercut are unlocked together.")]
        [SerializeField] private string punchesTitle = "NEW PUNCHES!";
        [TextArea(2, 6)]
        [SerializeField] private string jabText = "JAB\n\nPunch straight, fast.";
        [TextArea(2, 6)]
        [SerializeField] private string swingText = "SWING\n\nSweep your fist sideways, fast.";
        [TextArea(2, 6)]
        [SerializeField] private string uppercutText = "UPPERCUT\n\nDrive your fist up, fast.";
        [Tooltip("Shown instead of the two above when Swing and Uppercut are unlocked by the same point.")]
        [TextArea(2, 6)]
        [SerializeField] private string swingUppercutText = "SWING + UPPERCUT\n\nSweep sideways or drive up, fast.";
        [TextArea(2, 6)]
        [SerializeField] private string tripleText = "TRIPLE PUNCH\n\nEvery flying punch throws 3.";

        [Header("Exploding throws (the first Throw Power point; shown once the Level Up menu is closed)")]
        [SerializeField] private string throwsTitle = "NEW POWER!";
        [TextArea(2, 6)]
        [SerializeField] private string throwsText = "EXPLODING THROWS\n\nThrow books, bottles and flip-flops hard: BOOM!";

        [Header("When")]
        [Tooltip("Reaching one of these levels shows the notice that teaches how to open the Level Up menu (Teach Text).")]
        [SerializeField] private int[] teachLevels = { 1 };
        [Tooltip("Reaching one of these levels shows the short reminder (Short Text). At every other level there's no " +
                 "notice, only the sound.")]
        [SerializeField] private int[] remindLevels = { 3 };
        [Tooltip("Reaching one of these levels shows the Bonus Stats notice (Bonus Text). It stays like the teaching one.")]
        [SerializeField] private int[] bonusLevels = { 10 };
        [Tooltip("Played at every level up, with or without a notice.")]
        [SerializeField] private AudioClip levelUpSound;
        [Tooltip("The game is paused while a notice is up (time and sound; the level-up sound still plays).")]
        [SerializeField] private bool pauseGame = true;
        [Tooltip("Seconds of game time from the level up to the notice, so the level-up aura bursts first.")]
        [Min(0f)]
        [SerializeField] private float showDelay = 1.2f;

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

        // Which notice: when several levels come at once, the higher one wins.
        private enum Kind
        {
            Remind,
            Teach,
            Bonus,
        }

        private float shownAt;
        private float stays;
        // After the pause menu closes, the trigger that closed it doesn't close the notice too.
        private float buttonFrom;
        private int soundFrame = -1;
        private AudioSource sound;
        // A notice waiting for Show Delay: the level, and which notice.
        private int pendingLevel = -1;
        private Kind pendingKind;
        private float pendingAt;
        private bool holding;
        private bool showing;
        // New punches and powers waiting to be told about ("Jab", "Swing", "Uppercut", "Triple", "Throws"), in order.
        private const string Throws = "Throws";
        private readonly System.Collections.Generic.List<string> punches = new();
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
            // A punch shatters it: gone at once, the shards take its place.
            if (panel != null) Game.Menus.PunchToShatter.Add((RectTransform)panel.transform, () => { panel.alpha = 0f; Close(); });
            // Its own speaker for the level-up sound, heard even while the notice pauses the game's sound.
            sound = gameObject.AddComponent<AudioSource>();
            sound.playOnAwake = false;
            sound.spatialBlend = 0f;
            sound.ignoreListenerPause = true;
        }

        private void OnEnable()
        {
            continueButton.action?.Enable();
            PlayerLevel.LeveledUp += OnLeveledUp;
            PlayerLevel.PunchUnlocked += OnPunchUnlocked;
            PlayerLevel.ExplodingThrowsUnlocked += OnExplodingThrows;
            LevelUpMenu.Opened += Close;
        }

        private void OnPunchUnlocked(string punch) => punches.Add(punch);

        private void OnExplodingThrows() => punches.Add(Throws);

        private void OnDisable()
        {
            // Only switch off our own button; a referenced action may be used elsewhere.
            if (continueButton.reference == null) continueButton.action?.Disable();
            PlayerLevel.LeveledUp -= OnLeveledUp;
            PlayerLevel.PunchUnlocked -= OnPunchUnlocked;
            PlayerLevel.ExplodingThrowsUnlocked -= OnExplodingThrows;
            LevelUpMenu.Opened -= Close;
            // Never leave the game paused (e.g. the scene closing while a notice is up).
            Unpause();
            pendingLevel = -1;
        }

        // Every level up is heard; only some are shown, a moment later.
        private void OnLeveledUp(int level)
        {
            PlaySound();
            Kind kind;
            if (System.Array.IndexOf(bonusLevels, level) >= 0) kind = Kind.Bonus;
            else if (System.Array.IndexOf(teachLevels, level) >= 0) kind = Kind.Teach;
            else if (System.Array.IndexOf(remindLevels, level) >= 0) kind = Kind.Remind;
            else return;
            // Bonus wins over teaching, teaching over reminding, when several levels come at once.
            if (pendingLevel >= 0 && pendingKind > kind) return;
            pendingLevel = level;
            pendingKind = kind;
            pendingAt = Time.time + showDelay;
        }

        // Once, when several levels are gained at the same moment.
        private void PlaySound()
        {
            if (levelUpSound == null || sound == null || soundFrame == Time.frameCount) return;
            soundFrame = Time.frameCount;
            sound.PlayOneShot(levelUpSound);
        }

        /// <summary>
        /// Fades the notice in, in front of the player, for a level: the one that teaches how to open the Level Up
        /// menu, or the short reminder.
        /// </summary>
        public void Show(int level, bool teaching) => Show(level, teaching ? Kind.Teach : Kind.Remind);

        /// <summary>Fades the Bonus Stats notice in, in front of the player, for a level.</summary>
        public void ShowBonus(int level) => Show(level, Kind.Bonus);

        private void Show(int level, Kind kind)
        {
            if (panel == null) return;

            int points = PlayerLevel.Instance != null ? PlayerLevel.Instance.UnspentPoints : 0;
            // Several levels gained at once: the notice names the one the player is at now (the bonus one names the
            // level that unlocked the Bonus Stats).
            if (PlayerLevel.Instance != null && kind != Kind.Bonus) level = Mathf.Max(level, PlayerLevel.Instance.Level);
            string text = kind == Kind.Bonus ? bonusText : kind == Kind.Teach ? teachText : shortText;
            ShowText(Fill(titleText, level, points), Fill(text, level, points), kind == Kind.Remind ? shortSeconds : teachSeconds);
        }

        /// <summary>
        /// Fades in the notice that teaches a punch Fist Size has just unlocked ("Jab", "Swing", "Uppercut" or "Triple").
        /// </summary>
        public void ShowPunch(string punch)
        {
            string text = punch switch
            {
                "Jab" => jabText,
                "Swing" => swingText,
                "Uppercut" => uppercutText,
                _ => tripleText,
            };
            ShowText(punchTitle, text, teachSeconds);
        }

        // The next new punch (or exploding throws); Swing and Uppercut unlocked by the same point get one notice together.
        private void ShowNextPunch()
        {
            string punch = punches[0];
            punches.RemoveAt(0);
            if (punch == Throws)
                ShowText(throwsTitle, throwsText, teachSeconds);
            else if ((punch == "Swing" && punches.Remove("Uppercut")) || (punch == "Uppercut" && punches.Remove("Swing")))
                ShowText(punchesTitle, swingUppercutText, teachSeconds);
            else
                ShowPunch(punch);
        }

        private void ShowText(string heading, string message, float seconds)
        {
            if (panel == null) return;
            if (title != null) title.text = heading;
            if (body != null) body.text = message;
            if (continueText != null) continueText.enabled = false;

            shownAt = Time.unscaledTime;
            stays = seconds;
            showing = true;
            closing = false;
            gliding = false;
            panel.alpha = 0f;
            panel.interactable = false;
            panel.blocksRaycasts = false;
            panel.gameObject.SetActive(true);
            if (TryGetPlacement(out var position, out var rotation)) panel.transform.SetPositionAndRotation(position, rotation);
            if (pauseGame && !holding)
            {
                holding = true;
                PauseMenu.Hold(this);
            }
        }

        /// <summary>Whether a notice is up (or still fading out).</summary>
        public bool IsShowing => showing;

        /// <summary>Fades the notice out.</summary>
        public void Close()
        {
            if (showing) closing = true;
        }

        private static string Fill(string text, int level, int points)
        {
            return string.IsNullOrEmpty(text) ? "" : text.Replace("{level}", level.ToString()).Replace("{points}", points.ToString());
        }

        private void Unpause()
        {
            if (!holding) return;
            holding = false;
            PauseMenu.Release(this);
        }

        private void Update()
        {
            // Game time: a notice due while the pause menu is open waits for it to close.
            if (pendingLevel >= 0 && !showing && Time.time >= pendingAt)
            {
                Show(pendingLevel, pendingKind);
                pendingLevel = -1;
            }
            // A new punch is told about once the Level Up menu (where its point was spent) is closed.
            if (!showing && pendingLevel < 0 && punches.Count > 0 && !PauseMenu.IsPaused &&
                (LevelUpMenu.Instance == null || !LevelUpMenu.Instance.IsOpen))
                ShowNextPunch();
            if (!showing) return;

            // The pause menu sits in front: the notice hides and its clock stops (and the trigger that picks a menu
            // button doesn't close it too) until the menu is closed.
            if (PauseMenu.IsPaused)
            {
                shownAt += Time.unscaledDeltaTime;
                buttonFrom = Time.unscaledTime + 0.3f;
                panel.alpha = 0f;
                return;
            }

            // Unscaled time: the notice works the same if something pauses the game underneath it.
            float shown = Time.unscaledTime - shownAt;
            bool ready = shown >= readingTime;
            if (continueText != null) continueText.enabled = ready && !closing;
            bool pressed = Time.unscaledTime >= buttonFrom && continueButton.action != null && continueButton.action.WasPressedThisFrame();
            if (!closing && (shown >= stays || (ready && pressed)))
                closing = true;

            float step = fadeTime > 0f ? Time.unscaledDeltaTime / fadeTime : 1f;
            panel.alpha = Mathf.MoveTowards(panel.alpha, closing ? 0f : 1f, step);
            if (closing && panel.alpha <= 0f)
            {
                panel.gameObject.SetActive(false);
                showing = false;
                Unpause();
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
