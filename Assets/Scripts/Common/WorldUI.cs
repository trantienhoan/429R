using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// Builds small world-space panels in code (e.g. the cauldron's cooking meter, the magic hat's recipe card), for
    /// objects that make their own panel when the game runs instead of keeping one in their prefab.
    /// </summary>
    public static class WorldUI
    {
        /// <summary>A world-space canvas, width x height panel pixels at 'scale' metres per pixel, pivot at its bottom middle.</summary>
        public static RectTransform CreatePanel(string name, float width, float height, float scale, Color background)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) root.layer = uiLayer;

            var panel = (RectTransform)root.transform;
            panel.sizeDelta = new Vector2(width, height);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.localScale = Vector3.one * scale;
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            root.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;

            if (background.a > 0f)
            {
                var image = CreateImage("Background", panel, background);
                Stretch(image.rectTransform);
            }
            return panel;
        }

        public static Image CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static TMP_Text CreateText(string name, Transform parent, string text, float size, FontStyles style, TextAlignmentOptions alignment, Color color, TMP_FontAsset font)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>Puts a graphic 'left' and 'top' pixels in from its parent's top-left corner.</summary>
        public static void Place(Graphic graphic, float left, float top, float width, float height)
        {
            var rect = graphic.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Turns a panel to the viewer: a world-space canvas reads right when its forward points away from them.</summary>
        public static void Face(RectTransform panel, Transform head)
        {
            if (panel == null || head == null) return;
            var away = panel.TransformPoint(panel.rect.center) - head.position;
            if (away.sqrMagnitude > 0.0001f) panel.rotation = Quaternion.LookRotation(away, Vector3.up);
        }
    }
}
