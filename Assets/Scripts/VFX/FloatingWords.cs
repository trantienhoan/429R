using TMPro;
using UnityEngine;

namespace Game.VFX
{
    /// <summary>
    /// A few words that pop up at a spot in the world, float up and fade (e.g. "CANDY HELMET!" where the white bug died).
    /// They face the player, stay readable at any distance and show through walls. Show() makes them; nothing to put in
    /// a scene.
    /// </summary>
    public class FloatingWords : MonoBehaviour
    {
        private const string OverlayShader = "TextMeshPro/Mobile/Distance Field Overlay";
        // How tall the words look, in degrees of the view, and how long and how far up they float.
        private const float Degrees = 2.6f;
        private const float RiseMetres = 0.5f;

        private static Material material;

        private TextMeshPro text;
        private Color colour;
        private Vector3 from;
        private float start;
        private float seconds;

        /// <summary>Pops the words up at this point, in this colour, for this many seconds.</summary>
        public static void Show(string words, Color colour, Vector3 at, float seconds = 1.8f)
        {
            var font = TMP_Settings.defaultFontAsset;
            if (font == null) return;
            if (material == null)
            {
                material = new Material(font.material) { name = "Floating Words" };
                var overlay = Shader.Find(OverlayShader);
                if (overlay != null) material.shader = overlay;
                if (material.HasProperty(ShaderUtilities.ID_OutlineWidth))
                {
                    material.EnableKeyword(ShaderUtilities.Keyword_Outline);
                    material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
                    material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.1f, 0.02f, 0.08f, 1f));
                }
            }

            var go = new GameObject("Floating Words");
            var popup = go.AddComponent<FloatingWords>();
            popup.text = go.AddComponent<TextMeshPro>();
            popup.text.font = font;
            popup.text.fontSharedMaterial = material;
            popup.text.text = words;
            popup.text.fontSize = 10f;
            popup.text.fontStyle = FontStyles.Bold;
            popup.text.alignment = TextAlignmentOptions.Center;
            popup.text.textWrappingMode = TextWrappingModes.NoWrap;
            popup.text.color = colour;
            popup.colour = colour;
            popup.from = at;
            popup.start = Time.time;
            popup.seconds = Mathf.Max(0.2f, seconds);
            popup.Place(0f);
        }

        private void LateUpdate()
        {
            float t = (Time.time - start) / seconds;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            Place(t);
            // A quick pop in, then a fade over the last third.
            float pop = Mathf.Clamp01(t / 0.08f);
            transform.localScale *= Mathf.Lerp(0.6f, 1f, pop);
            var c = colour;
            c.a = t < 0.66f ? 1f : 1f - (t - 0.66f) / 0.34f;
            text.color = c;
        }

        private void Place(float t)
        {
            var eyes = Camera.main;
            transform.position = from + Vector3.up * (RiseMetres * (1f - (1f - t) * (1f - t)));
            if (eyes == null) return;
            var look = transform.position - eyes.transform.position;
            if (look.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(look, Vector3.up);
            // A TextMeshPro at font size 10 is about 1 unit tall at scale 1: sized to Degrees at this distance.
            float size = 2f * look.magnitude * Mathf.Tan(Degrees * 0.5f * Mathf.Deg2Rad);
            transform.localScale = Vector3.one * size;
        }
    }
}
