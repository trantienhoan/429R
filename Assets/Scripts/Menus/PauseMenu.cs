using System;
using Game.Locomotion;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Game.Menus
{
    /// <summary>
    /// Pause menu. The menu button on the left controller (or A on the right, or Esc) stops the game
    /// and shows Resume, Quit, a random tip and two options in front of you: how arm swinging works, and the
    /// player's height (to play seated, or stand taller or shorter).
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text tipText;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button quitButton;
        [Tooltip("The headset camera. Left empty, Camera.main is used.")]
        [SerializeField] private Transform head;

        [Header("Options")]
        [Tooltip("Each press switches arm swinging to the next choice: hold X, no button, off.")]
        [SerializeField] private Button armSwingButton;
        [SerializeField] private TMP_Text armSwingText;
        [SerializeField] private Button heightDownButton;
        [SerializeField] private Button heightUpButton;
        [SerializeField] private TMP_Text heightText;
        [Tooltip("Left empty, the one in the scene is used.")]
        [SerializeField] private ArmSwingMoveProvider armSwing;
        [Tooltip("Left empty, the one on the XR Origin is used.")]
        [SerializeField] private PlayerHeight playerHeight;

        [Header("Opening")]
        [Tooltip("Defaults to the menu button on the left controller, A on the right controller and Esc on the keyboard.")]
        [SerializeField] private InputActionProperty menuButton = new(CreateDefaultButton());
        [Tooltip("Pause when the Meta system menu opens or the headset is taken off (not in the Editor).")]
        [SerializeField] private bool pauseOnFocusLoss = true;
        [Tooltip("Also pause all sound while the menu is open.")]
        [SerializeField] private bool pauseAudio = true;
        [Tooltip("Where the menu opens, in metres from your head (x right, y up, z forward), turning with your head.")]
        [SerializeField] private Vector3 panelOffset = new(0f, -0.1f, 0.6f);

        [Header("Tips")]
        [Tooltip("One is picked at random each time the menu opens.")]
        [TextArea(2, 4)]
        [SerializeField] private string[] tips =
        {
            "Hold Y on your left controller to see your inventory.",
            "Hold B on your right controller to open the Candy Shop.",
            "Every candy you eat heals you and is kept in your inventory.",
            "Trade your candy for weapons in the Candy Shop.",
            "Press the menu button any time to pause.",
        };

        private float timeScaleBeforePause = 1f;
        private int lastTip = -1;

        public static bool IsPaused { get; private set; }

        /// <summary>Raised when the game pauses or resumes; read IsPaused for which.</summary>
        public static event Action PauseChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsPaused = false;
            PauseChanged = null;
        }

        private static InputAction CreateDefaultButton()
        {
            var action = new InputAction("Pause Menu", InputActionType.Button);
            action.AddBinding("<XRController>{LeftHand}/menuButton");
            action.AddBinding("<XRController>{RightHand}/primaryButton");
            action.AddBinding("<Keyboard>/escape");
            return action;
        }

        private void Awake()
        {
            if (panel != null) panel.SetActive(false);
            if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
            if (armSwingButton != null) armSwingButton.onClick.AddListener(NextArmSwingMode);
            if (heightDownButton != null) heightDownButton.onClick.AddListener(() => ChangeHeight(-1));
            if (heightUpButton != null) heightUpButton.onClick.AddListener(() => ChangeHeight(1));
        }

        private void OnEnable()
        {
            menuButton.action?.Enable();
        }

        private void OnDisable()
        {
            // Only switch off our own button; a referenced action may be used elsewhere.
            if (menuButton.reference == null) menuButton.action?.Disable();
            Resume();
        }

        private void Update()
        {
            if (menuButton.action == null || !menuButton.action.WasPressedThisFrame()) return;

            if (IsPaused) Resume();
            else Pause();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && pauseOnFocusLoss && !Application.isEditor) Pause();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && pauseOnFocusLoss && !Application.isEditor) Pause();
        }

        public void Pause()
        {
            if (IsPaused) return;

            IsPaused = true;
            timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            if (pauseAudio) AudioListener.pause = true;

            ShowRandomTip();
            ShowOptions();
            PlacePanel();
            if (panel != null) panel.SetActive(true);
            PauseChanged?.Invoke();
        }

        public void Resume()
        {
            if (!IsPaused) return;

            IsPaused = false;
            Time.timeScale = timeScaleBeforePause;
            if (pauseAudio) AudioListener.pause = false;

            if (panel != null) panel.SetActive(false);
            PauseChanged?.Invoke();
        }

        public void Quit()
        {
            Resume();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void NextArmSwingMode()
        {
            FindOptions();
            if (armSwing == null) return;

            armSwing.Mode = armSwing.Mode switch
            {
                ArmSwingMoveProvider.SwingMode.HoldButton => ArmSwingMoveProvider.SwingMode.NoButton,
                ArmSwingMoveProvider.SwingMode.NoButton => ArmSwingMoveProvider.SwingMode.Off,
                _ => ArmSwingMoveProvider.SwingMode.HoldButton,
            };
            ShowOptions();
        }

        private void ChangeHeight(int steps)
        {
            FindOptions();
            if (playerHeight == null) return;

            float before = playerHeight.Offset;
            playerHeight.Step(steps);
            // The view and hands move with the height, so the menu moves too and stays where the hands can reach it.
            if (panel != null) panel.transform.position += playerHeight.transform.up * (playerHeight.Offset - before);
            ShowOptions();
        }

        private void ShowOptions()
        {
            FindOptions();
            if (armSwingButton != null) armSwingButton.interactable = armSwing != null;
            if (armSwingText != null) armSwingText.text = armSwing != null ? ArmSwingLabel(armSwing.Mode) : "Arm swing: not set up";

            if (heightDownButton != null) heightDownButton.interactable = playerHeight != null;
            if (heightUpButton != null) heightUpButton.interactable = playerHeight != null;
            if (heightText != null) heightText.text = playerHeight != null ? HeightLabel(playerHeight.Offset) : "Height: not set up";
        }

        private void FindOptions()
        {
            if (armSwing == null) armSwing = FindAnyObjectByType<ArmSwingMoveProvider>(FindObjectsInactive.Include);
            if (playerHeight == null) playerHeight = FindAnyObjectByType<PlayerHeight>(FindObjectsInactive.Include);
        }

        private static string ArmSwingLabel(ArmSwingMoveProvider.SwingMode mode)
        {
            return mode switch
            {
                ArmSwingMoveProvider.SwingMode.HoldButton => "Arm swing: hold X",
                ArmSwingMoveProvider.SwingMode.NoButton => "Arm swing: no button",
                _ => "Arm swing: off",
            };
        }

        private static string HeightLabel(float offset)
        {
            int centimetres = Mathf.RoundToInt(offset * 100f);
            return centimetres == 0 ? "Height: normal" : $"Height: {centimetres:+0;-0} cm";
        }

        private void ShowRandomTip()
        {
            if (tipText == null) return;

            if (tips == null || tips.Length == 0)
            {
                tipText.text = "";
                return;
            }

            // Avoid showing the same tip twice in a row.
            int index = Random.Range(0, tips.Length);
            if (tips.Length > 1 && index == lastTip) index = (index + 1) % tips.Length;
            lastTip = index;
            tipText.text = "Tip: " + tips[index];
        }

        private void PlacePanel()
        {
            if (panel == null) return;
            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (head == null) return;

            // Face the way the head looks, ignoring up/down tilt (looking down, the head's up points forward).
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;

            var yaw = Quaternion.LookRotation(forward.normalized, Vector3.up);
            var position = head.position + yaw * panelOffset;
            // A world-space canvas is readable when its forward points away from the viewer.
            panel.transform.SetPositionAndRotation(position, Quaternion.LookRotation(position - head.position, Vector3.up));
        }
    }
}
