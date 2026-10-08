using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Menus
{
    /// <summary>
    /// Health and safety notice shown in front of the player when the game starts, once per launch. After a few
    /// seconds of reading time a trigger (or Enter/Space) closes it, and it fades away. If the player looks away it
    /// glides back in front of them. Tools > 429 Game > Menu > Set Up Health Warning builds it.
    /// </summary>
    public class HealthWarning : MonoBehaviour
    {
        [SerializeField] private CanvasGroup panel;
        [Tooltip("Shown once the reading time is over.")]
        [SerializeField] private TMP_Text continueText;
        [Tooltip("The headset camera. Left empty, Camera.main is used.")]
        [SerializeField] private Transform head;

        [Tooltip("Seconds before the notice can be closed, so it gets read.")]
        [SerializeField] private float readingTime = 4f;
        [Tooltip("Defaults to either trigger, and Enter or Space on the keyboard. (A and the menu button open the pause menu.)")]
        [SerializeField] private InputActionProperty continueButton = new(CreateDefaultButton());
        [Tooltip("Where the notice sits, in metres from your head (x right, y up, z forward).")]
        [SerializeField] private Vector3 panelOffset = new(0f, 0f, 1.5f);
        [Tooltip("Looking further away than this (degrees), the notice glides back in front of you.")]
        [SerializeField] private float followAngle = 35f;
        [Tooltip("Seconds to fade in and out.")]
        [SerializeField] private float fadeTime = 0.6f;

        // The game scene can reload (the loop restarts after the boss); the notice is for the start of a play session.
        private static bool shownThisLaunch;

        private float shownAt;
        private bool closing;
        private bool gliding;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            shownThisLaunch = false;
        }

        private static InputAction CreateDefaultButton()
        {
            var action = new InputAction("Continue", InputActionType.Button);
            action.AddBinding("<XRController>{LeftHand}/triggerPressed");
            action.AddBinding("<XRController>{RightHand}/triggerPressed");
            action.AddBinding("<Keyboard>/enter");
            action.AddBinding("<Keyboard>/space");
            return action;
        }

        private void OnEnable()
        {
            continueButton.action?.Enable();
        }

        private void OnDisable()
        {
            // Only switch off our own button; a referenced action may be used elsewhere.
            if (continueButton.reference == null) continueButton.action?.Disable();
        }

        private void Start()
        {
            if (panel == null || shownThisLaunch)
            {
                if (panel != null) panel.gameObject.SetActive(false);
                enabled = false;
                return;
            }

            shownThisLaunch = true;
            shownAt = Time.unscaledTime;
            panel.alpha = 0f;
            panel.interactable = false;
            panel.blocksRaycasts = false;
            if (continueText != null) continueText.enabled = false;
            panel.gameObject.SetActive(true);
            if (TryGetPlacement(out var position, out var rotation)) panel.transform.SetPositionAndRotation(position, rotation);
        }

        private void Update()
        {
            // Unscaled time: the notice works the same if something pauses the game underneath it.
            float step = fadeTime > 0f ? Time.unscaledDeltaTime / fadeTime : 1f;
            bool ready = Time.unscaledTime - shownAt >= readingTime;
            if (continueText != null) continueText.enabled = ready;
            if (ready && !closing && continueButton.action != null && continueButton.action.WasPressedThisFrame()) closing = true;

            panel.alpha = Mathf.MoveTowards(panel.alpha, closing ? 0f : 1f, step);
            if (closing && panel.alpha <= 0f)
            {
                panel.gameObject.SetActive(false);
                enabled = false;
                return;
            }

            FollowGaze();
        }

        // Stays put while you read, and glides back (no snapping, which is uncomfortable in VR) once you look away.
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
