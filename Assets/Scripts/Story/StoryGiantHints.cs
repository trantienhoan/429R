using System.Collections.Generic;
using Game.PlayGround;
using Game.Rooms;
using UnityEngine;

namespace Game.Story
{
    /// <summary>
    /// The giant pumpkin's two story hints (user 2026-11-26: "I prefer telling story with visual than listening"; ideas 6
    /// and 7), Story Giant Settings. Main game rooms only (a room base's ceiling light near, as Laugh Chill). Made by
    /// itself in every scene when the settings exist; nothing to put in a scene.
    /// - The watcher: while the player looks at a window of the bedroom kit (or, during a room's fight with its walls
    ///   down, into the dark), now and then the giant's glowing eyes drift past outside, blink once and are gone. It grows
    ///   with each seed piece taken this run. Long quiet spells between sightings.
    /// - The laugh's shadow: when a room's laughing pumpkin laughs, the giant's laughing shadow shakes on the wall behind
    ///   it (seen from the player), as long as the laugh lasts: what it will become.
    /// </summary>
    public class StoryGiantHints : MonoBehaviour
    {
        private class Shadow
        {
            public RunAwayPumpkin pumpkin;
            public SpriteRenderer look;
            public float alpha;
            public float scale;
        }

        private class Watcher
        {
            public Transform root;
            public SpriteRenderer body;
            public SpriteRenderer left;
            public SpriteRenderer right;
            public Vector3 from;
            public Vector3 to;
            public float started;
            public float eyeSize;
        }

        private StoryGiantSettings settings;
        private Sprite eyeSprite;
        private readonly Dictionary<int, Shadow> shadows = new();
        private readonly List<int> ended = new();
        private readonly List<Transform> windows = new();
        private float nextWindowLook;
        private float nextWatch;
        private float nextCheck;
        private Watcher watcher;

        /// <summary>Sightings of the watcher so far this session (for tests).</summary>
        public static int Sightings { get; private set; }

        /// <summary>Laugh shadows thrown so far this session (for tests).</summary>
        public static int ShadowsThrown { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Make()
        {
            if (StoryGiantSettings.Instance == null || FindAnyObjectByType<StoryGiantHints>() != null) return;
            new GameObject("Story_Giant_Hints").AddComponent<StoryGiantHints>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Sightings = 0;
            ShadowsThrown = 0;
        }

        private void OnEnable()
        {
            settings = StoryGiantSettings.Instance;
            RunAwayPumpkin.Laughed += Heard;
            nextWatch = Time.time + 20f;
        }

        private void OnDisable()
        {
            RunAwayPumpkin.Laughed -= Heard;
        }

        private void Update()
        {
            if (settings == null || settings.Silhouette == null) return;
            float now = Time.time;
            UpdateShadows(Time.deltaTime);
            UpdateWatcher(now);
        }

        // ---- How big it is now: one step per seed piece taken this run ----

        private float GiantScale()
        {
            int pieces = 0;
            foreach (var name in settings.GrowBools)
                if (!string.IsNullOrEmpty(name) && GameStages.GetBool(name)) pieces++;
            return settings.StartScale + settings.ScalePerPiece * pieces;
        }

        // ---- The laugh's shadow ----

        private void Heard(RunAwayPumpkin pumpkin)
        {
            if (settings == null || settings.Silhouette == null || pumpkin == null) return;
            if (shadows.ContainsKey(pumpkin.GetInstanceID())) return;
            var cam = Camera.main;
            if (cam == null || !InMainRoom(pumpkin.transform.position)) return;
            if (!FindWall(pumpkin, cam.transform.position, out var hit)) return;

            float floor = Floor();
            float wallTop = hit.collider.bounds.max.y;
            float height = Mathf.Min(settings.ShadowHeight * GiantScale(), wallTop - floor - 0.05f);
            if (height < 0.5f) return;
            var go = new GameObject("Story_Giant_Shadow");
            go.transform.SetPositionAndRotation(new Vector3(hit.point.x, floor, hit.point.z) + hit.normal * 0.03f,
                Quaternion.LookRotation(-hit.normal, Vector3.up));
            var look = go.AddComponent<SpriteRenderer>();
            look.sprite = settings.Silhouette;
            if (settings.ShadowMaterial != null) look.sharedMaterial = settings.ShadowMaterial;
            look.color = new Color(0f, 0f, 0f, 0f);
            float scale = height / Mathf.Max(0.01f, settings.Silhouette.bounds.size.y);
            go.transform.localScale = Vector3.one * scale;
            shadows[pumpkin.GetInstanceID()] = new Shadow { pumpkin = pumpkin, look = look, scale = scale };
            ShadowsThrown++;
        }

        // The first tall, upright surface behind the pumpkin (seen from the player): a wall, not a box in the way.
        private bool FindWall(RunAwayPumpkin pumpkin, Vector3 viewer, out RaycastHit wall)
        {
            wall = default;
            var from = pumpkin.transform.position + Vector3.up * 0.3f;
            var away = Vector3.ProjectOnPlane(from - viewer, Vector3.up);
            if (away.sqrMagnitude < 0.01f) return false;
            var hits = Physics.RaycastAll(from, away.normalized, settings.WallReach, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider.transform.IsChildOf(pumpkin.transform)) continue;
                if (Mathf.Abs(hit.normal.y) > 0.3f || hit.collider.bounds.size.y < settings.TallestFurniture) continue;
                wall = hit;
                return true;
            }
            return false;
        }

        private void UpdateShadows(float dt)
        {
            if (shadows.Count == 0) return;
            ended.Clear();
            foreach (var pair in shadows)
            {
                var shadow = pair.Value;
                if (shadow.look == null)
                {
                    ended.Add(pair.Key);
                    continue;
                }
                bool laughing = shadow.pumpkin != null && shadow.pumpkin.IsLaughing;
                float seconds = laughing ? settings.ShadowIn : settings.ShadowOut;
                shadow.alpha = Mathf.MoveTowards(shadow.alpha, laughing ? 1f : 0f, dt / Mathf.Max(0.01f, seconds));
                shadow.look.color = new Color(0f, 0f, 0f, shadow.alpha * settings.ShadowDarkness);
                // It shakes with the laugh.
                float shake = laughing ? 1f + 0.035f * Mathf.Sin(Time.time * 22f) : 1f;
                shadow.look.transform.localScale = new Vector3(shadow.scale, shadow.scale * shake, 1f);
                if (laughing || shadow.alpha > 0f) continue;
                Destroy(shadow.look.gameObject);
                ended.Add(pair.Key);
            }
            foreach (var id in ended) shadows.Remove(id);
        }

        // ---- The watcher ----

        private void UpdateWatcher(float now)
        {
            if (watcher != null)
            {
                Animate(now);
                return;
            }
            if (now < nextWatch || now < nextCheck) return;
            nextCheck = now + 0.5f;
            var cam = Camera.main;
            if (cam == null) return;
            var head = cam.transform;
            var level = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (level.sqrMagnitude < 0.01f || Vector3.Angle(head.forward, level) > 30f) return;
            level.Normalize();
            if (!InMainRoom(head.position)) return;
            if (!WindowAhead(head.position, level, out var centre, out var outward, settings.BehindWindow) &&
                !DarkAhead(head.position, level, out centre, out outward)) return;
            if (Random.value >= settings.WatchChance) return;
            Show(centre, outward, now);
        }

        // A window of the bedroom kit the player looks at: the point behind it, and which way is out.
        private bool WindowAhead(Vector3 head, Vector3 level, out Vector3 centre, out Vector3 outward, float behind)
        {
            centre = outward = default;
            if (Time.time >= nextWindowLook)
            {
                nextWindowLook = Time.time + 3f;
                windows.Clear();
                foreach (var kit in FindObjectsByType<RoomKit>(FindObjectsSortMode.None))
                foreach (var t in kit.GetComponentsInChildren<Transform>())
                foreach (var name in settings.WindowNames)
                    if (!string.IsNullOrEmpty(name) && t.name.StartsWith(name)) windows.Add(t);
            }
            float best = settings.LookAngle;
            bool found = false;
            foreach (var window in windows)
            {
                if (window == null || !window.gameObject.activeInHierarchy || !Bounds(window.gameObject, out var bounds)) continue;
                var flat = Vector3.ProjectOnPlane(bounds.center - head, Vector3.up);
                float distance = flat.magnitude;
                if (distance < 0.5f || distance > settings.WindowReach) continue;
                float angle = Vector3.Angle(level, flat);
                if (angle >= best) continue;
                best = angle;
                outward = flat / distance;
                centre = bounds.center + outward * behind;
                found = true;
            }
            return found;
        }

        // During a room's fight (its walls down): nothing in the way for a good distance, so far out in the dark.
        private bool DarkAhead(Vector3 head, Vector3 level, out Vector3 centre, out Vector3 outward)
        {
            centre = outward = default;
            string state = GameStages.ActiveState();
            if (string.IsNullOrEmpty(state) || System.Array.IndexOf(settings.FightStates, state) < 0) return false;
            if (Physics.Raycast(head, level, settings.DarkClear, ~0, QueryTriggerInteraction.Ignore)) return false;
            outward = level;
            centre = head + level * settings.InTheDark + Vector3.up * 0.5f;
            return true;
        }

        private void Show(Vector3 eyes, Vector3 outward, float now)
        {
            float scale = GiantScale();
            var sprite = settings.Silhouette;
            float unit = settings.WatcherHeight * scale / Mathf.Max(0.01f, sprite.bounds.size.y);
            var size = sprite.bounds.size;
            var root = new GameObject("Story_Giant_Watcher").transform;
            // Facing the player (its +X their right), the eyes at the root.
            root.rotation = Quaternion.LookRotation(outward, Vector3.up);
            var mid = (settings.LeftEye + settings.RightEye) * 0.5f;
            var body = new GameObject("Body").AddComponent<SpriteRenderer>();
            body.transform.SetParent(root, false);
            body.transform.localScale = Vector3.one * unit;
            body.transform.localPosition = -new Vector3((mid.x - 0.5f) * size.x, mid.y * size.y, 0f) * unit;
            body.sprite = sprite;
            if (settings.ShadowMaterial != null) body.sharedMaterial = settings.ShadowMaterial;
            body.color = new Color(0f, 0f, 0f, 0f);

            float spacing = Mathf.Abs(settings.RightEye.x - settings.LeftEye.x) * size.x * unit;
            var made = new Watcher
            {
                root = root,
                body = body,
                // Big glowing eyes, so they read in the dark (twice their spacing on the picture would touch).
                eyeSize = Mathf.Max(0.15f, spacing * 1.6f),
                started = now,
            };
            made.left = Eye(root, body.transform.localPosition + new Vector3((settings.LeftEye.x - 0.5f) * size.x, settings.LeftEye.y * size.y, 0f) * unit);
            made.right = Eye(root, body.transform.localPosition + new Vector3((settings.RightEye.x - 0.5f) * size.x, settings.RightEye.y * size.y, 0f) * unit);
            var along = Vector3.Cross(Vector3.up, outward).normalized;
            if (Random.value < 0.5f) along = -along;
            made.from = eyes - along * (settings.Drift * 0.5f);
            made.to = eyes + along * (settings.Drift * 0.5f);
            root.position = made.from;
            watcher = made;
            Sightings++;
        }

        private SpriteRenderer Eye(Transform root, Vector3 local)
        {
            if (eyeSprite == null && settings.EyeTexture != null)
            {
                var texture = settings.EyeTexture;
                eyeSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
            }
            var eye = new GameObject("Eye").AddComponent<SpriteRenderer>();
            eye.transform.SetParent(root, false);
            // A little in front of its body, toward the player.
            eye.transform.localPosition = local + new Vector3(0f, 0f, -0.05f);
            eye.sprite = eyeSprite;
            if (settings.EyeMaterial != null) eye.sharedMaterial = settings.EyeMaterial;
            eye.color = Color.black;
            return eye;
        }

        // Fades in, drifts past, blinks once, fades out.
        private void Animate(float now)
        {
            var w = watcher;
            float t = (now - w.started) / Mathf.Max(0.1f, settings.WatchSeconds);
            if (w.root == null || t >= 1f)
            {
                if (w.root != null) Destroy(w.root.gameObject);
                watcher = null;
                nextWatch = now + Random.Range(settings.WatchEvery.x, settings.WatchEvery.y);
                return;
            }
            w.root.position = Vector3.Lerp(w.from, w.to, Mathf.SmoothStep(0f, 1f, t));
            float alpha = Mathf.Min(Mathf.Clamp01(t / 0.15f), Mathf.Clamp01((1f - t) / 0.2f));
            w.body.color = new Color(0f, 0f, 0f, alpha * settings.WatcherDarkness);
            bool blink = t > 0.45f && t < 0.5f;
            float open = blink ? 0.12f : 1f;
            var glow = settings.EyeColor * alpha;
            foreach (var eye in new[] { w.left, w.right })
            {
                if (eye == null) continue;
                eye.color = new Color(glow.r, glow.g, glow.b, alpha);
                eye.transform.localScale = new Vector3(w.eyeSize, w.eyeSize * 0.6f * open, 1f);
            }
        }

        // ---- Helpers ----

        // In a main game room: a room base's ceiling light near (as Laugh Chill decides).
        private static bool InMainRoom(Vector3 at)
        {
            var laugh = StoryLaughSettings.Instance;
            if (laugh == null) return false;
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Point || !light.isActiveAndEnabled) continue;
                if (Vector3.Distance(light.transform.position, at) > laugh.LightRange) continue;
                for (var p = light.transform; p != null; p = p.parent)
                    foreach (var name in laugh.RoomBases)
                        if (!string.IsNullOrEmpty(name) && p.name.StartsWith(name)) return true;
            }
            return false;
        }

        private static float Floor()
        {
            var player = FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
            return player != null ? player.transform.position.y : 0f;
        }

        private static bool Bounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (var look in go.GetComponentsInChildren<Renderer>())
            {
                if (!look.enabled) continue;
                if (!any)
                {
                    bounds = look.bounds;
                    any = true;
                }
                else bounds.Encapsulate(look.bounds);
            }
            return any;
        }
    }
}
