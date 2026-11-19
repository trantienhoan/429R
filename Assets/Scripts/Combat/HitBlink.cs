using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// The hit blink (user, 2026-11-19): an enemy a hit hurts goes red for a moment, its paint (Base Color) turned towards
    /// red and a red glow (Emission Color) added, full for the first third, then fading back. Hit Settings' Hit Blink sets
    /// the colour and how long; Hit Settings.Blink plays it (Enemy Hit Damage, the Fat Pumpkin, the pumpkin ball boss).
    /// It uses property blocks, like the bee boss's bomb flash and the angry Fat Pumpkin: no material copies. Each body
    /// part's own property block is kept from before the blink and put back after it (none: cleared, so the part batches
    /// as before). It runs after the brains, so the red wins over a tint they set meanwhile.
    /// </summary>
    [DefaultExecutionOrder(10001)]
    [DisallowMultipleComponent]
    public class HitBlink : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        // The share of the blink it stays full red, before it fades.
        private const float FullShare = 0.35f;

        private class Part
        {
            public Renderer renderer;
            public bool glows;
            public readonly MaterialPropertyBlock before = new();
        }

        private readonly List<Part> parts = new();
        private static MaterialPropertyBlock block;
        private bool blinking;
        private float startedAt;
        private float seconds;
        private Color colour;
        private float tint;
        private float glow;

        /// <summary>
        /// Blinks Target (an enemy) this colour for this many seconds: its paint Tint of the way to it, glowing Glow of it.
        /// A new blink while one runs starts again at full red.
        /// </summary>
        public static void Play(GameObject target, Color colour, float tint, float glow, float seconds)
        {
            if (target == null || seconds <= 0f || !target.activeInHierarchy) return;
            if (!target.TryGetComponent(out HitBlink blink)) blink = target.AddComponent<HitBlink>();
            blink.Blink(colour, tint, glow, seconds);
        }

        private void Blink(Color newColour, float newTint, float newGlow, float newSeconds)
        {
            block ??= new MaterialPropertyBlock();
            if (!blinking)
            {
                Collect();
                // What each part had before the blink, to put back after it.
                foreach (var part in parts) part.renderer.GetPropertyBlock(part.before);
                seconds = newSeconds;
            }
            else
            {
                seconds = Mathf.Max(newSeconds, seconds - (Time.time - startedAt));
            }
            colour = newColour;
            tint = newTint;
            glow = newGlow;
            startedAt = Time.time;
            blinking = true;
            enabled = true;
            Show(1f);
        }

        // Its body parts: the meshes with a paint colour (not particles, trails or lines). Found again when one has gone
        // (wings knocked off and destroyed).
        private void Collect()
        {
            bool fresh = parts.Count > 0;
            foreach (var part in parts)
            {
                if (part.renderer == null) fresh = false;
            }
            if (fresh) return;
            parts.Clear();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                var material = r.sharedMaterial;
                if (material == null || !material.HasProperty(BaseColorId)) continue;
                parts.Add(new Part { renderer = r, glows = material.HasProperty(EmissionColorId) });
            }
        }

        private void Update()
        {
            if (!blinking)
            {
                enabled = false;
                return;
            }
            float t = (Time.time - startedAt) / Mathf.Max(0.01f, seconds);
            if (t >= 1f) End();
            else Show(t <= FullShare ? 1f : 1f - Mathf.InverseLerp(FullShare, 1f, t));
        }

        // Red this much (1 = full): the paint towards the colour, plus its glow.
        private void Show(float amount)
        {
            foreach (var part in parts)
            {
                var r = part.renderer;
                if (r == null) continue;
                var material = r.sharedMaterial;
                if (material == null || !material.HasProperty(BaseColorId)) continue;
                r.GetPropertyBlock(block);
                var paint = material.GetColor(BaseColorId);
                var red = Color.Lerp(paint, colour, tint * amount);
                red.a = paint.a;
                block.SetColor(BaseColorId, red);
                if (part.glows && material.HasProperty(EmissionColorId))
                    block.SetColor(EmissionColorId, material.GetColor(EmissionColorId) + colour * (glow * amount));
                r.SetPropertyBlock(block);
            }
        }

        // Each part gets back what it had before the blink (nothing: cleared).
        private void End()
        {
            blinking = false;
            foreach (var part in parts)
            {
                if (part.renderer == null) continue;
                part.renderer.SetPropertyBlock(part.before.isEmpty ? null : part.before);
            }
            enabled = false;
        }

        // Switched off or put back in a pool mid-blink: it never stays red.
        private void OnDisable()
        {
            if (blinking) End();
        }
    }
}
