using UnityEngine;

namespace Game.Locomotion
{
    /// <summary>
    /// Fades the headset view to a colour and back: a small sphere around the eyes, drawn over the scene. (Camera fade
    /// actions that draw on the screen only show on the PC monitor, not in the headset.) Goes on that sphere, a child of
    /// the Main Camera; Tools > 429 Game > Player > Set Up Death Fall makes it, and the death fall uses it. Anything that
    /// should stay visible over the fade, like the GAME_OVER picture, uses the "UI Over Fade" material.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshRenderer))]
    public class HeadsetFade : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [Tooltip("The colour the view fades to.")]
        [SerializeField] private Color color = Color.black;

        private MeshRenderer sphere;
        private MaterialPropertyBlock block;
        private float alpha;
        private float from;
        private float target;
        private float duration;
        private float delay;
        private float elapsed;
        private bool fading;

        /// <summary>How dark the view is right now, from 0 (clear) to 1 (fully faded).</summary>
        public float Alpha => alpha;

        /// <summary>Fades the view out to the colour over 'seconds', starting after 'after' seconds.</summary>
        public void FadeOut(float seconds, float after = 0f)
        {
            FadeTo(1f, seconds, after);
        }

        /// <summary>Fades the view back in over 'seconds', starting after 'after' seconds.</summary>
        public void FadeIn(float seconds, float after = 0f)
        {
            FadeTo(0f, seconds, after);
        }

        public void FadeTo(float amount, float seconds, float after = 0f)
        {
            from = alpha;
            target = Mathf.Clamp01(amount);
            duration = Mathf.Max(0f, seconds);
            delay = Mathf.Max(0f, after);
            elapsed = 0f;
            fading = true;
        }

        private void Awake()
        {
            sphere = GetComponent<MeshRenderer>();
            block = new MaterialPropertyBlock();
            Apply(0f);
        }

        // Real time, so a fade still finishes while the game is paused.
        private void Update()
        {
            if (!fading) return;

            elapsed += Time.unscaledDeltaTime;
            if (elapsed < delay) return;

            float t = duration > 0f ? Mathf.Clamp01((elapsed - delay) / duration) : 1f;
            Apply(Mathf.Lerp(from, target, t * t * (3f - 2f * t)));
            if (t >= 1f) fading = false;
        }

        private void Apply(float amount)
        {
            alpha = amount;
            // Not drawn at all while the view is clear.
            sphere.enabled = alpha > 0.001f;
            if (!sphere.enabled) return;

            var faded = color;
            faded.a *= alpha;
            block.SetColor(BaseColor, faded);
            sphere.SetPropertyBlock(block);
        }
    }
}
