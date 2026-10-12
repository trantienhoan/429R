using System;
using System.Collections.Generic;
using Game.Combat;
using Game.Menus;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Game.Leveling
{
    /// <summary>
    /// The Level Up menu: the player's level, EXP and unspent points, and the six stats with a + each. Bumping both
    /// fists together twice (Fist Bump) opens it in front of the player, and closes it again. The game doesn't pause
    /// while it's open. Pulling a trigger (a click) while pointing anywhere but at the menu closes it, and so does
    /// walking away from it or pausing the game. Tools > 429 Game > Player > Set Up Level System builds it.
    /// </summary>
    public class LevelUpMenu : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text pointsText;
        [SerializeField] private TMP_Text expText;
        [Tooltip("The EXP bar's fill: stretched from the bar's left edge as far as the EXP has got.")]
        [SerializeField] private RectTransform expFill;
        [SerializeField] private LevelUpStatRow rowTemplate;
        [SerializeField] private Transform rowParent;
        [Tooltip("The headset camera. Left empty, Camera.main is used.")]
        [SerializeField] private Transform head;

        [Tooltip("Where the menu opens, in metres from the head (x right, y up, z forward).")]
        [SerializeField] private Vector3 panelOffset = new(0f, -0.1f, 0.85f);
        [Tooltip("The menu closes by itself when the player is farther from it than this, in metres.")]
        [Min(0.5f)]
        [SerializeField] private float closeDistance = 3f;
        [Tooltip("A click with this button while not pointing at the menu closes it. Defaults to either trigger (and the " +
                 "left mouse button, for testing in the editor).")]
        [SerializeField] private InputActionProperty clickButton = new(CreateDefaultButton());
        [Tooltip("Optional sounds.")]
        [SerializeField] private AudioClip openSound;
        [SerializeField] private AudioClip closeSound;
        [SerializeField] private AudioClip pointSound;

        // Clicks this soon after it opens don't close it: the fists that bumped it open are still letting go.
        private const float OpenGrace = 0.5f;

        private readonly List<LevelUpStatRow> rows = new();
        private PointerInside pointer;
        private float openedAt;

        public static LevelUpMenu Instance { get; private set; }

        /// <summary>Raised when the menu opens.</summary>
        public static event Action Opened;

        public bool IsOpen => panel != null && panel.gameObject.activeSelf;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            Opened = null;
        }

        private static InputAction CreateDefaultButton()
        {
            var action = new InputAction("Click", InputActionType.Button);
            action.AddBinding("<XRController>{LeftHand}/triggerPressed");
            action.AddBinding("<XRController>{RightHand}/triggerPressed");
            action.AddBinding("<Mouse>/leftButton");
            return action;
        }

        private void Awake()
        {
            Instance = this;
            if (panel == null) return;
            if (!panel.TryGetComponent(out pointer)) pointer = panel.gameObject.AddComponent<PointerInside>();
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            clickButton.action?.Enable();
            FistBump.DoubleBumped += Toggle;
            PlayerLevel.Changed += Refresh;
            PauseMenu.PauseChanged += OnPauseChanged;
        }

        private void OnDisable()
        {
            // Only switch off our own button; a referenced action may be used elsewhere.
            if (clickButton.reference == null) clickButton.action?.Disable();
            FistBump.DoubleBumped -= Toggle;
            PlayerLevel.Changed -= Refresh;
            PauseMenu.PauseChanged -= OnPauseChanged;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!IsOpen) return;

            if (head != null && Vector3.Distance(head.position, panel.position) > closeDistance)
            {
                Close();
                return;
            }
            // A click anywhere but on the menu closes it.
            if (Time.unscaledTime - openedAt >= OpenGrace && clickButton.action != null && clickButton.action.WasPressedThisFrame()
                && (pointer == null || !pointer.IsInside))
                Close();
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        /// <summary>Opens the menu in front of the player. Not while the game is paused, or with no Player Level.</summary>
        public void Open()
        {
            if (panel == null || IsOpen || PauseMenu.IsPaused || PlayerLevel.Instance == null) return;
            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (head == null) return;

            // In front of where the head looks, ignoring up/down tilt, and turned to face the player.
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            var position = head.position + Quaternion.LookRotation(forward.normalized, Vector3.up) * panelOffset;
            // A world-space canvas is readable when its forward points away from the viewer.
            panel.SetPositionAndRotation(position, Quaternion.LookRotation(position - head.position, Vector3.up));

            panel.gameObject.SetActive(true);
            openedAt = Time.unscaledTime;
            Refresh();
            Play(openSound);
            Opened?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen) return;
            panel.gameObject.SetActive(false);
            Play(closeSound);
        }

        private void OnPauseChanged()
        {
            if (PauseMenu.IsPaused) Close();
        }

        private void Refresh()
        {
            var level = PlayerLevel.Instance;
            if (!IsOpen || level == null) return;

            if (levelText != null) levelText.text = level.IsMaxLevel ? $"LEVEL {level.Level} (MAX)" : $"LEVEL {level.Level}";
            int unspent = level.UnspentPoints;
            if (pointsText != null) pointsText.text = unspent == 1 ? "1 point to spend" : $"{unspent} points to spend";
            if (expText != null) expText.text = level.IsMaxLevel ? "EXP: top level reached" : $"EXP {level.Exp} / {level.ExpNeeded}";
            if (expFill != null) expFill.anchorMax = new Vector2(level.IsMaxLevel ? 1f : Mathf.Clamp01((float)level.Exp / level.ExpNeeded), 1f);

            if (rowTemplate == null || rowParent == null) return;
            while (rows.Count < PlayerLevel.StatCount) rows.Add(Instantiate(rowTemplate, rowParent));
            for (int i = 0; i < rows.Count; i++)
            {
                var stat = (Stat)i;
                rows[i].gameObject.SetActive(true);
                rows[i].Set(PlayerLevel.Name(stat), level.Describe(stat), level.Points(stat), level.MaxPointsPerStat,
                    level.CanRaise(stat), () => OnPlus(stat));
            }
        }

        private void OnPlus(Stat stat)
        {
            // Raising a stat refreshes the menu through Player Level's Changed.
            if (PlayerLevel.Instance != null && PlayerLevel.Instance.Raise(stat)) Play(pointSound);
        }

        private void Play(AudioClip clip)
        {
            if (clip != null && panel != null) AudioSource.PlayClipAtPoint(clip, panel.position);
        }

        /// <summary>On the menu's panel: whether any pointer (a hand's ray, a poking finger, the mouse) is on it.</summary>
        private class PointerInside : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            private readonly HashSet<int> pointers = new();

            public bool IsInside => pointers.Count > 0;

            public void OnPointerEnter(PointerEventData eventData) => pointers.Add(eventData.pointerId);

            public void OnPointerExit(PointerEventData eventData) => pointers.Remove(eventData.pointerId);

            private void OnDisable() => pointers.Clear();
        }
    }
}
