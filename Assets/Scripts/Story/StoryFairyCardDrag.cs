using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Story
{
    /// <summary>
    /// The Fairy board's card can be pulled sideways (user 2026-11-25: "click and hold the image on path on board can
    /// also move it"): press it and move the pointer left or right; the picture follows, and past a third of the card it
    /// turns to the next choice (pulled left) or the one before (pulled right). A pull is not a pick: the click that
    /// ends it is swallowed. Added to the card by Story Fairy Board.
    /// </summary>
    public class StoryFairyCardDrag : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Tooltip("Board pixels the pointer moves before a press counts as a pull (not a click).")]
        public float moveForPull = 12f;

        /// <summary>Board, its card picture, the card's width (board pixels) and the step to call (1 next, -1 before).</summary>
        public RectTransform Board;
        public RectTransform Picture;
        public float CardWidth = 300f;
        public System.Action<int> Step;
        /// <summary>False while there is only one choice.</summary>
        public System.Func<bool> CanPull;

        private float startX;
        private float offset;
        private bool pulled;

        /// <summary>True when the press that is ending was a pull: its click must not pick the card.</summary>
        public bool WasPulled => pulled;

        public void OnPointerDown(PointerEventData eventData)
        {
            pulled = false;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!BoardX(eventData, out startX)) startX = float.NaN;
            Slide(0f);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!BoardX(eventData, out float x)) return;
            if (float.IsNaN(startX)) startX = x;
            float dx = x - startX;
            if (Mathf.Abs(dx) > moveForPull) pulled = true;
            // Only one choice (The Path): nothing to pull to, the card stays put.
            if (CanPull != null && !CanPull()) return;
            if (Mathf.Abs(dx) > CardWidth / 3f)
            {
                Step?.Invoke(dx < 0f ? 1 : -1);
                startX = x;
                dx = 0f;
            }
            Slide(dx);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            Slide(0f);
        }

        private void OnDisable()
        {
            Slide(0f);
            pulled = false;
        }

        // Where the pointer's ray meets the board, along the board (board pixels); false when it is off the board.
        private bool BoardX(PointerEventData eventData, out float x)
        {
            x = 0f;
            var hit = eventData.pointerCurrentRaycast;
            if (Board == null || hit.gameObject == null) return false;
            x = Board.InverseTransformPoint(hit.worldPosition).x;
            return true;
        }

        private void Slide(float dx)
        {
            offset = dx;
            if (Picture == null) return;
            // The picture fills the card with a small inset: slide both edges together.
            Picture.anchoredPosition = new Vector2(offset, Picture.anchoredPosition.y);
        }
    }
}
