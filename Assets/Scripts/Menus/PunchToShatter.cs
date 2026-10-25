using System;
using System.Collections.Generic;
using Game.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Menus
{
    /// <summary>
    /// A pop-up (the level-up notice, a tutorial, the Level Up menu, the bonus board, the health warning, the end notice)
    /// that a punch shatters: a squeezed fist going through it at least Min Speed fast (Shatter Settings) breaks it into shards in its
    /// own colours, with the glass sound, and the pop-up closes at once. It watches the hands themselves, not physics, so
    /// it also works while a notice has the game paused. A pop-up adds it to its panel when it starts (Add); it costs
    /// nothing while the panel is hidden.
    /// </summary>
    [DisallowMultipleComponent]
    public class PunchToShatter : MonoBehaviour
    {
        // How far in front of or behind the panel a fist still touches it, and how far past its edges, in metres.
        private const float Depth = 0.12f;
        private const float Margin = 0.03f;
        // A panel fading in or out this see-through can't be hit.
        private const float MinAlpha = 0.25f;
        private static readonly Color FallbackColour = new(0.08f, 0.05f, 0.12f, 0.9f);
        // How much lighter than the panel its shards are, and the share of them in the text's colour.
        private const float Lighten = 0.45f;
        private const float AccentShare = 0.35f;

        private RectTransform panel;
        private CanvasGroup group;
        private Action shattered;
        private Func<bool> ready;
        private Fist[] fists = Array.Empty<Fist>();
        private Vector3[] last = Array.Empty<Vector3>();

        /// <summary>
        /// Lets a punch shatter this pop-up's panel: 'shattered' hides and closes the pop-up at once (the shards take its
        /// place). 'ready' (optional) says when it can be broken, e.g. not before it can be read.
        /// </summary>
        public static PunchToShatter Add(RectTransform panel, Action shattered, Func<bool> ready = null)
        {
            if (panel == null || shattered == null) return null;
            if (!panel.TryGetComponent(out PunchToShatter it)) it = panel.gameObject.AddComponent<PunchToShatter>();
            it.panel = panel;
            it.group = panel.GetComponent<CanvasGroup>();
            it.shattered = shattered;
            it.ready = ready;
            return it;
        }

        private void OnEnable()
        {
            fists = FindObjectsByType<Fist>(FindObjectsSortMode.None);
            last = new Vector3[fists.Length];
            for (int i = 0; i < fists.Length; i++) last[i] = fists[i].Knuckles;
        }

        private void Update()
        {
            if (panel == null || shattered == null) return;
            float dt = Time.unscaledDeltaTime;
            bool hittable = (group == null || group.alpha >= MinAlpha) && (ready == null || ready());
            float minSpeed = ShatterSettings.Instance.MinSpeed;
            for (int i = 0; i < fists.Length; i++)
            {
                var fist = fists[i];
                if (fist == null || !fist.isActiveAndEnabled) continue;
                var now = fist.Knuckles;
                var before = last[i];
                last[i] = now;
                if (!hittable || dt <= 0f || !fist.Squeezing || (now - before).magnitude < minSpeed * dt) continue;
                if (!Through(before, now, out var at)) continue;
                Shatter(at, now - before);
                return;
            }
        }

        // The fist's path this frame touches the panel: it ends within Depth of it, or goes right through it, over it.
        private bool Through(Vector3 from, Vector3 to, out Vector3 at)
        {
            var normal = panel.forward;
            float a = Vector3.Dot(from - panel.position, normal);
            float b = Vector3.Dot(to - panel.position, normal);
            at = to;
            if (Mathf.Abs(b) > Depth)
            {
                if (Mathf.Sign(a) == Mathf.Sign(b)) return false;
                at = Vector3.Lerp(from, to, a / (a - b));
            }
            var local = panel.InverseTransformPoint(at);
            var rect = panel.rect;
            float margin = Margin / Mathf.Max(0.0001f, panel.lossyScale.x);
            return local.x >= rect.xMin - margin && local.x <= rect.xMax + margin &&
                   local.y >= rect.yMin - margin && local.y <= rect.yMax + margin;
        }

        private void Shatter(Vector3 at, Vector3 motion)
        {
            Colours(out var main, out var accent);
            ShardBurst.Make(panel, at, motion, main, accent);
            var close = shattered;
            close();
        }

        // The panel's own look: its background (the first image, lightened so its shards show against a dark room, like
        // glass catching the light) and its text, for a few bright shards.
        private void Colours(out Color main, out Color accent)
        {
            main = FallbackColour;
            accent = Color.white;
            foreach (var image in panel.GetComponentsInChildren<Image>())
            {
                if (image.color.a < 0.3f) continue;
                main = image.color;
                break;
            }
            var text = panel.GetComponentInChildren<TMP_Text>();
            if (text != null) accent = text.color;
            main = Color.Lerp(main, Color.white, Lighten);
            main.a = Mathf.Max(main.a, 0.85f);
            accent.a = Mathf.Max(accent.a, 0.85f);
        }

        /// <summary>The shards of a shattered panel: they fly off the punch, spin, fall and fade, then they're gone.</summary>
        private class ShardBurst : MonoBehaviour
        {
            private class Shard
            {
                public RectTransform piece;
                public Image look;
                public Vector3 velocity;
                public Vector3 spin;
                public float life;
                public Vector3 size;
                public Color colour;
            }

            private const float Gravity = 6f;
            private readonly List<Shard> shards = new();
            private float age;
            private float lasts;

            public static void Make(RectTransform panel, Vector3 at, Vector3 motion, Color main, Color accent)
            {
                var settings = ShatterSettings.Instance;
                var root = new GameObject("Shards", typeof(RectTransform));
                root.transform.SetPositionAndRotation(panel.position, panel.rotation);
                root.transform.localScale = panel.lossyScale;
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                var area = (RectTransform)root.transform;
                area.sizeDelta = panel.rect.size;
                area.pivot = panel.pivot;

                var burst = root.AddComponent<ShardBurst>();
                burst.lasts = settings.ShardSeconds;
                var rect = panel.rect;
                var push = motion.sqrMagnitude > 0.000001f ? motion.normalized : panel.forward;
                for (int i = 0; i < settings.Shards; i++)
                {
                    var go = new GameObject("Shard", typeof(RectTransform), typeof(Image));
                    var piece = (RectTransform)go.transform;
                    piece.SetParent(area, false);
                    // Slivers and chips, anywhere on the panel.
                    float width = rect.width * UnityEngine.Random.Range(0.05f, 0.16f);
                    piece.sizeDelta = new Vector2(width, width * UnityEngine.Random.Range(0.25f, 1f));
                    piece.localPosition = new Vector3(UnityEngine.Random.Range(rect.xMin, rect.xMax), UnityEngine.Random.Range(rect.yMin, rect.yMax), 0f);
                    piece.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
                    var look = go.GetComponent<Image>();
                    look.raycastTarget = false;
                    var colour = UnityEngine.Random.value < AccentShare ? accent : main;
                    look.color = colour;

                    // Away from the punch along the panel, on with the punch, and a little up.
                    var outward = Vector3.ProjectOnPlane(piece.position - at, panel.forward);
                    if (outward.sqrMagnitude < 0.0001f) outward = UnityEngine.Random.insideUnitCircle;
                    burst.shards.Add(new Shard
                    {
                        piece = piece,
                        look = look,
                        velocity = outward.normalized * UnityEngine.Random.Range(0.6f, 1.8f) + push * UnityEngine.Random.Range(0.5f, 1.5f) +
                                   Vector3.up * UnityEngine.Random.Range(0.2f, 0.9f),
                        spin = new Vector3(UnityEngine.Random.Range(-540f, 540f), UnityEngine.Random.Range(-540f, 540f), UnityEngine.Random.Range(-720f, 720f)),
                        life = burst.lasts * UnityEngine.Random.Range(0.7f, 1f),
                        size = piece.localScale,
                        colour = colour,
                    });
                }

                if (settings.Sound != null)
                {
                    var speaker = root.AddComponent<AudioSource>();
                    speaker.playOnAwake = false;
                    speaker.spatialBlend = 0f;
                    speaker.ignoreListenerPause = true;
                    speaker.PlayOneShot(settings.Sound, settings.Volume);
                    burst.lasts = Mathf.Max(burst.lasts, settings.Sound.length);
                }
            }

            private void Update()
            {
                // Unscaled: it plays out while a notice still has the game paused.
                float dt = Time.unscaledDeltaTime;
                age += dt;
                foreach (var shard in shards)
                {
                    if (shard.piece == null) continue;
                    shard.velocity += Vector3.down * (Gravity * dt);
                    shard.piece.position += shard.velocity * dt;
                    shard.piece.Rotate(shard.spin * dt, Space.Self);
                    float left = Mathf.Clamp01(1f - age / shard.life);
                    shard.piece.localScale = shard.size * Mathf.Lerp(0.4f, 1f, left);
                    var c = shard.colour;
                    c.a *= left;
                    shard.look.color = c;
                }
                if (age >= lasts) Destroy(gameObject);
            }
        }
    }
}
