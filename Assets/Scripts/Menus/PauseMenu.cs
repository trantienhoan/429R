using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Game.Menus
{
    /// <summary>
    /// Pause menu. The menu button on the left controller (or A on the right, or Esc) stops the game
    /// and shows Resume, Quit and a random tip in front of you.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text tipText;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button quitButton;
        [Tooltip("The headset camera. Left empty, Camera.main is used.")]
        [SerializeField] private Transform head;

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
