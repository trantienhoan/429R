using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Menus
{
    /// <summary>
    /// Fires once a UI element has been held down (the trigger on a pointer ray, a poke, or the mouse) for Hold Seconds
    /// without letting go or pointing away. It counts real time, so it works while the game is paused. Its graphic
    /// slowly turns Warning Color while held, so the player can tell something is happening. The pause menu's title
    /// uses it for Reset Progress. The graphic needs Raycast Target on.
    /// </summary>
    [DisallowMultipleComponent]
    public class PressAndHold : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Min(0.1f)]
        [SerializeField] private float holdSeconds = 7f;
        [Tooltip("The graphic tinted while held. Left empty, the one on this object.")]
        [SerializeField] private Graphic tint;
        [SerializeField] private Color warningColor = new(1f, 0.3f, 0.25f, 1f);

        private float pressedAt = -1f;
        private Color normalColor = Color.white;

        /// <summary>Raised once each time it has been held long enough.</summary>
        public event Action Held;

        private void Awake()
        {
            if (tint == null) tint = GetComponent<Graphic>();
            if (tint != null) normalColor = tint.color;
        }

        private void OnDisable() => Cancel();

        public void OnPointerDown(PointerEventData eventData) => pressedAt = Time.unscaledTime;

        public void OnPointerUp(PointerEventData eventData) => Cancel();

        public void OnPointerExit(PointerEventData eventData) => Cancel();

        private void Update()
        {
            if (pressedAt < 0f) return;
            float held = (Time.unscaledTime - pressedAt) / holdSeconds;
            if (tint != null) tint.color = Color.Lerp(normalColor, warningColor, Mathf.Clamp01(held));
            if (held < 1f) return;
            // Once per press: let go and hold again to fire again.
            Cancel();
            Held?.Invoke();
        }

        public void Cancel()
        {
            pressedAt = -1f;
            if (tint != null) tint.color = normalColor;
        }
    }
}
