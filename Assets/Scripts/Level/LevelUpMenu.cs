using System;
using System.Collections.Generic;
using Game.Combat;
using Game.Menus;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Leveling
{
    /// <summary>
    /// The Level Up menu: the player's level, EXP and unspent points, and the six stats with a + each. From the first
    /// bonus level on, a gold arrow on its right opens (and closes) the Bonus Stats panel beside it: the bonus points
    /// and the bonus stats (Fist Size, Power, Extra Revive). The arrow glows while there are bonus points to spend. Bumping both
    /// fists together twice (Fist Bump) opens it in front of the player, and closes it again. The game doesn't pause
    /// while it's open. Pulling a trigger (a click) while pointing anywhere but at the menu or its Bonus Stats panel
    /// closes it, and so does walking away from it or pausing the game. Tools > 429 Game > Player > Set Up Level System
    /// builds it (Add Bonus Stats Panel adds the arrow and the panel to a menu built before).
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
        [Tooltip("Shows what each stat gives under its name (e.g. \"+30 health\"). Off: just the names, kept simple.")]
        [SerializeField] private bool showEffects;
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

        [Header("Bonus stats")]
        [Tooltip("The arrow on the menu's right that opens and closes the Bonus Stats panel. Shown from the first bonus level on.")]
        [SerializeField] private Button bonusArrow;
        [Tooltip("The Bonus Stats panel beside the menu. It's part of the menu's panel, so clicks on it don't close the menu.")]
        [SerializeField] private RectTransform bonusPanel;
        [SerializeField] private TMP_Text bonusPointsText;
        [SerializeField] private Transform bonusRowParent;
        [Tooltip("The arrow's colour. It glows brighter while there are bonus points to spend.")]
        [SerializeField] private Color arrowColor = new(0.85f, 0.62f, 0.15f, 1f);

        // Clicks this soon after it opens don't close it: the fists that bumped it open are still letting go.
        private const float OpenGrace = 0.5f;

        private readonly List<LevelUpStatRow> rows = new();
        private readonly List<LevelUpStatRow> bonusRows = new();
        private PointerInside pointer;
        private float openedAt;
        private int shownRows = -1;
        private bool bonusOpen;
        private TMP_Text arrowText;
        private Image arrowImage;

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
            // On the panel, it also hears the pointer on the arrow and the Bonus Stats panel: they're under it.
            if (!panel.TryGetComponent(out pointer)) pointer = panel.gameObject.AddComponent<PointerInside>();
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
            if (bonusArrow != null)
            {
                bonusArrow.onClick.AddListener(ToggleBonus);
                arrowText = bonusArrow.GetComponentInChildren<TMP_Text>(true);
                arrowImage = bonusArrow.targetGraphic as Image;
            }
            if (bonusPanel != null) bonusPanel.gameObject.SetActive(false);
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
            // A click anywhere but on the menu (or its Bonus Stats panel) closes it.
            if (Time.unscaledTime - openedAt >= OpenGrace && clickButton.action != null && clickButton.action.WasPressedThisFrame()
                && (pointer == null || !pointer.IsInside))
            {
                Close();
                return;
            }
            GlowArrow();
        }

        // The arrow glows while there are bonus points to spend.
        private void GlowArrow()
        {
            if (arrowImage == null || !bonusArrow.gameObject.activeInHierarchy) return;
            var level = PlayerLevel.Instance;
            bool waiting = level != null && level.UnspentBonusPoints > 0;
            float glow = waiting ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f) : 0f;
            arrowImage.color = Color.Lerp(arrowColor, new Color(1f, 0.95f, 0.6f, arrowColor.a), glow * 0.6f);
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        /// <summary>Opens the menu in front of the player. Not while the game is paused, or with no Player Level.</summary>
        public void Open()
        {
            if (panel == null || IsOpen || PauseMenu.IsPaused || PauseMenu.IsHeld || PlayerLevel.Instance == null) return;
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
            // Opens with the Bonus Stats panel shut.
            bonusOpen = false;
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
            FillRows(rows, rowParent, PlayerLevel.NormalStats, false);
            FitRows(PlayerLevel.NormalStats.Length);
            RefreshBonus(level);
        }

        // The arrow (from the first bonus level on) and, while it's open, the Bonus Stats panel.
        private void RefreshBonus(PlayerLevel level)
        {
            bool unlocked = level.BonusPoints > 0;
            if (!unlocked) bonusOpen = false;
            if (bonusArrow != null)
            {
                bonusArrow.gameObject.SetActive(unlocked);
                if (arrowText != null) arrowText.text = bonusOpen ? "<" : ">";
            }
            if (bonusPanel == null) return;
            bonusPanel.gameObject.SetActive(bonusOpen);
            if (!bonusOpen) return;

            int bonus = level.UnspentBonusPoints;
            if (bonusPointsText != null)
                bonusPointsText.text = bonus == 1 ? "1 bonus point to spend" : bonus > 0 ? $"{bonus} bonus points to spend" : "No bonus points left";
            if (bonusRowParent != null) FillRows(bonusRows, bonusRowParent, PlayerLevel.BonusStats, true);
        }

        // One row a stat, made from the row template as needed; bonus stats in gold.
        private void FillRows(List<LevelUpStatRow> list, Transform parent, Stat[] stats, bool gold)
        {
            var level = PlayerLevel.Instance;
            while (list.Count < stats.Length) list.Add(Instantiate(rowTemplate, parent));
            for (int i = 0; i < list.Count; i++)
            {
                list[i].gameObject.SetActive(i < stats.Length);
                if (i >= stats.Length) continue;
                var stat = stats[i];
                list[i].Set(PlayerLevel.Name(stat), showEffects ? level.Describe(stat) : "", level.Points(stat), level.MaxPoints(stat),
                    level.CanRaise(stat), () => OnPlus(stat));
                list[i].Highlight(gold);
            }
        }

        /// <summary>Opens the Bonus Stats panel beside the menu, or closes it (the arrow on the menu's right).</summary>
        public void ToggleBonus()
        {
            if (!IsOpen || PlayerLevel.Instance == null || PlayerLevel.Instance.BonusPoints <= 0) return;
            bonusOpen = !bonusOpen;
            Play(pointSound);
            Refresh();
        }

        // Makes the panel as tall as the rows it shows, keeping the space above and below them.
        private void FitRows(int shown)
        {
            if (shown == shownRows || !(rowParent is RectTransform list)) return;
            shownRows = shown;
            float rowHeight = 84f;
            if (rowTemplate.TryGetComponent<UnityEngine.UI.LayoutElement>(out var layout))
                rowHeight = Mathf.Max(layout.minHeight, layout.preferredHeight);
            float spacing = list.TryGetComponent<UnityEngine.UI.VerticalLayoutGroup>(out var group) ? group.spacing : 0f;
            // The rows' area is stretched inside the panel: its offsets are the space above and below the rows.
            float around = list.offsetMin.y - list.offsetMax.y;
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, around + shown * rowHeight + Mathf.Max(0, shown - 1) * spacing);
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
