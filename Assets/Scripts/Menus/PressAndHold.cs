using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Menus
{
    /// <summary>
    /// Fires once a UI element has been held down (the trigger on a pointer ray, a poke, or the mouse) for Hold Seconds.
    /// Each pointer counts on its own: a second controller pressing too doesn't restart it, and it only stops when every
    /// pointer has let go or pointed away. It counts real time, so it works while the game is paused. While held its
    /// graphic slowly turns Warning Color and its Fill (if any) grows from the left edge, so the player can tell something
    /// is happening; let go and it drains back. With Faster With Both Triggers on, holding both controllers' triggers
    /// takes half the time. The pause menu's title uses it for Reset Progress, the start menu's save slots to start a
    /// slot. The graphic needs Raycast Target on.
    /// </summary>
    [DisallowMultipleComponent]
    public class PressAndHold : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Min(0.1f)]
        [SerializeField] private float holdSeconds = 7f;
        [Tooltip("The graphic tinted while held. Left empty, the one on this object.")]
        [SerializeField] private Graphic tint;
        [SerializeField] private Color warningColor = new(1f, 0.3f, 0.25f, 1f);
        [Tooltip("Optional: grows from the left edge to the right edge as it's held (its right anchor follows the progress).")]
        [SerializeField] private RectTransform fill;
        [Tooltip("Seconds to drain back to empty after letting go (0: at once).")]
        [Min(0f)]
        [SerializeField] private float drainSeconds;

        [Header("Both hands")]
        [Tooltip("Holding both controllers' triggers down while it's held takes half the time.")]
        [SerializeField] private bool fasterWithBothTriggers;
        [Tooltip("The left hand's trigger. Defaults to the left trigger, and Shift on the keyboard (Shift + the mouse in the Editor = both hands).")]
        [SerializeField] private InputActionProperty leftTrigger = new(CreateDefaultTrigger("Left", "<XRController>{LeftHand}/triggerPressed", "<Keyboard>/shift"));
        [Tooltip("The right hand's trigger. Defaults to the right trigger, and the left mouse button.")]
        [SerializeField] private InputActionProperty rightTrigger = new(CreateDefaultTrigger("Right", "<XRController>{RightHand}/triggerPressed", "<Mouse>/leftButton"));

        // The pointers pressing it right now (each controller's ray and poke, the mouse).
        private readonly HashSet<int> pressing = new();
        private float progress;
        // Fired this press: it waits until every pointer has let go before it can fire again.
        private bool fired;
        private Color normalColor = Color.white;

        /// <summary>Raised once each time it has been held long enough.</summary>
        public event Action Held;

        /// <summary>How far the hold is, from 0 to 1.</summary>
        public float Progress => progress;

        /// <summary>Whether a pointer is pressing it right now.</summary>
        public bool IsPressed => pressing.Count > 0;

        /// <summary>The seconds to hold it (half with both triggers, when that's on).</summary>
        public float HoldSeconds
        {
            get => holdSeconds;
            set => holdSeconds = Mathf.Max(0.1f, value);
        }

        private static InputAction CreateDefaultTrigger(string hand, params string[] bindings)
        {
            var action = new InputAction(hand + " Trigger", InputActionType.Button);
            foreach (var binding in bindings) action.AddBinding(binding);
            return action;
        }

        private void Awake()
        {
            if (tint == null) tint = GetComponent<Graphic>();
            if (tint != null) normalColor = tint.color;
            Show();
        }

        private void OnEnable()
        {
            if (!fasterWithBothTriggers) return;
            leftTrigger.action?.Enable();
            rightTrigger.action?.Enable();
        }

        private void OnDisable()
        {
            // Only switch off our own actions; referenced ones may be used elsewhere.
            if (leftTrigger.reference == null) leftTrigger.action?.Disable();
            if (rightTrigger.reference == null) rightTrigger.action?.Disable();
            // Not Cancel: no switching objects on or off while the menu around it is being switched off.
            pressing.Clear();
            fired = false;
            progress = 0f;
            Show(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null) return;
            pressing.Add(eventData.pointerId);
        }

        public void OnPointerUp(PointerEventData eventData) => LetGo(eventData);

        public void OnPointerExit(PointerEventData eventData) => LetGo(eventData);

        private void LetGo(PointerEventData eventData)
        {
            if (eventData != null) pressing.Remove(eventData.pointerId);
            if (pressing.Count == 0) fired = false;
        }

        private void Update()
        {
            if (pressing.Count == 0 && progress <= 0f) return;

            float dt = Time.unscaledDeltaTime;
            if (pressing.Count > 0 && !fired)
            {
                float speed = BothHands() ? 2f : 1f;
                progress = Mathf.Min(1f, progress + dt * speed / holdSeconds);
            }
            else
            {
                progress = drainSeconds > 0f ? Mathf.Max(0f, progress - dt / drainSeconds) : 0f;
            }
            Show();

            if (progress < 1f || fired) return;
            // Once per press: let go (every pointer) and hold again to fire again.
            fired = true;
            progress = 0f;
            Show();
            Held?.Invoke();
        }

        // Both triggers down, or two pointers (e.g. both rays) pressing it at once.
        private bool BothHands()
        {
            if (!fasterWithBothTriggers) return false;
            if (pressing.Count >= 2) return true;
            return IsDown(leftTrigger) && IsDown(rightTrigger);
        }

        private static bool IsDown(InputActionProperty property)
        {
            var action = property.action;
            return action != null && action.enabled && action.IsPressed();
        }

        /// <summary>Stops the hold at once, back to empty.</summary>
        public void Cancel()
        {
            pressing.Clear();
            fired = false;
            progress = 0f;
            Show();
        }

        // switchFill: the fill is switched off while empty (not drawn at all) and on while held.
        private void Show(bool switchFill = true)
        {
            if (tint != null) tint.color = Color.Lerp(normalColor, warningColor, progress);
            if (fill == null) return;
            fill.anchorMax = new Vector2(progress, fill.anchorMax.y);
            if (switchFill && fill.gameObject.activeSelf != progress > 0f) fill.gameObject.SetActive(progress > 0f);
        }
    }
}
