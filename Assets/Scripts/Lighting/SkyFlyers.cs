using System.Collections.Generic;
using UnityEngine;

namespace Game.Lighting
{
    /// <summary>
    /// Flying slimes in the Halloween sky (user, 2026-11-16: real flying bats instead of the painted ones, "you can reuse
    /// the slime for that"): a few copies of the Flying_Slime model (just the model: no AI, colliders or shadows) loop
    /// round the sky picture's moon with their bat wings beating, and now and then one swoops far out across the sky and
    /// back. They keep a fixed distance from the head, as the sky does, so they look far away wherever the player goes
    /// (the house's 40 m far plane: they fly inside it; past the fog's 22 m they're dark silhouettes, without fog, as in
    /// the gate 3 valley, their faces glow). Made by Tools > 429 Game > Look > Sky: Halloween (the scene's "Sky Flyers").
    /// Since 2026-11-18 (user: "smaller and rarer to see them, not all the time") the sky is empty most of the time: now
    /// and then (Visit Every) one to three of them fly in from far away, loop round the moon for a while (Visit Seconds)
    /// and fly off again.
    /// </summary>
    public class SkyFlyers : MonoBehaviour
    {
        [Tooltip("The Flying_Slime model (Models/Enemies/Flying_Slime.fbx): its Slime_Wing_L/R beat, the rest is still.")]
        [SerializeField] private GameObject model;
        [Tooltip("Their material (Materials/Flying_Slime/Flying_Slime_Mat, 429/Slime Lit).")]
        [SerializeField] private Material material;
        [Min(0)]
        [SerializeField] private int count = 6;

        [Header("Where")]
        [Tooltip("The moon in the sky picture: longitude round from +Z toward +X, and degrees up (tools/halloween_sky.py).")]
        [SerializeField] private float moonLongitude = 200f;
        [SerializeField] private float moonLatitude = 32f;
        [Tooltip("How far from the head they fly (m), nearest and farthest. Under the head's far plane (40 m in the house).")]
        [SerializeField] private Vector2 distance = new Vector2(27f, 33f);
        [Tooltip("How big they look: wing tip to wing tip, in degrees of the view (the moon is about 9).")]
        // 3 - 4.6 read as dots, 5.5 - 8 looked funny (user, 2026-11-16: "make them smaller, they are really far away");
        // 2.5 - 3.8 still too big (user, 2026-11-18: "look smaller"). Was "wingspanDegrees": renamed so the scene's saved
        // 2.5 - 3.8 is left behind and this applies without editing the scene.
        [SerializeField] private Vector2 wingspan = new Vector2(1.4f, 2.2f);

        [Header("Visits (user, 2026-11-18: rarer, not all the time)")]
        [Tooltip("Seconds of empty sky between visits.")]
        [SerializeField] private Vector2 visitEvery = new Vector2(35f, 80f);
        [Tooltip("Seconds a visit lasts (they loop round the moon meanwhile).")]
        [SerializeField] private Vector2 visitSeconds = new Vector2(12f, 22f);
        [Tooltip("How many slimes come on a visit, fewest and most (at most Count).")]
        [SerializeField] private Vector2Int visitSlimes = new Vector2Int(1, 3);
        [Tooltip("Seconds before the first visit.")]
        [SerializeField] private Vector2 firstVisitAfter = new Vector2(15f, 40f);
        [Tooltip("Seconds a slime takes to fly in from far away, and to fly off again.")]
        [Min(0.1f)]
        [SerializeField] private float arriveSeconds = 3f;

        [Header("Flying")]
        [Tooltip("How far from the moon they loop (degrees), smallest and biggest loop.")]
        [SerializeField] private Vector2 loopDegrees = new Vector2(7f, 20f);
        [Tooltip("How fast they go round (degrees of their loop a second).")]
        [SerializeField] private Vector2 loopSpeed = new Vector2(16f, 34f);
        [Tooltip("Seconds between one slime's swoops far out across the sky and back (any one of them).")]
        [SerializeField] private Vector2 swoopEvery = new Vector2(9f, 20f);
        [Tooltip("Seconds a swoop takes, out and back.")]
        [SerializeField] private float swoopSeconds = 7f;
        [Tooltip("How far round the sky a swoop goes from the moon (degrees).")]
        [SerializeField] private Vector2 swoopReach = new Vector2(55f, 95f);

        [Header("Look")]
        [Tooltip("Their skin colour (the slime's paint colour): nearly black, so they read as silhouettes on the sky " +
                 "(a purple one melted into the violet; play test 2026-11-16). Past the house fog they're black anyway.")]
        [SerializeField] private Color skin = new Color(0.1f, 0.06f, 0.14f);
        [Tooltip("The glow of their carved faces (the body's glow).")]
        [SerializeField] private Color faceGlow = new Color(1.6f, 1.6f, 1.6f);
        [Tooltip("Their eyes' glow (the Flying Slime's Eye Glow).")]
        [SerializeField] private Color eyeGlow = new Color(2.2f, 0.92f, 0.13f);
        [Tooltip("Wing beats a second.")]
        [SerializeField] private Vector2 flapsPerSecond = new Vector2(4.5f, 6.5f);
        [Tooltip("Degrees the wings go up, and down, from where they're modelled (as the Flying Slime).")]
        [SerializeField] private float flapUp = 38f;
        [SerializeField] private float flapDown = 26f;
        [Tooltip("Degrees they lean into their loop.")]
        [SerializeField] private float bank = 22f;
        [Tooltip("Degrees they turn their faces toward the player while flying past, so the glowing eyes show.")]
        [SerializeField] private float faceTurn = 35f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private class Flyer
        {
            public Transform root, wingLeft, wingRight;
            public Quaternion wingLeftRest, wingRightRest;
            public float wingLeftSide, wingRightSide;
            public float distance, scale, loop, loopSpeed, phase, flaps, flapPhase, squashX, squashY;
            // A swoop: out to Out and back, from SwoopAt for swoopSeconds.
            public float swoopAt = -1f;
            public Vector3 swoopOut, lastDir;
            // A visit: flies in from Far at ArriveAt, loops, flies back off toward Far from LeaveAt.
            public bool visiting, justShown;
            public float arriveAt, leaveAt;
            public Vector3 far;
        }

        private readonly List<Flyer> flyers = new List<Flyer>();
        private readonly List<Flyer> idle = new List<Flyer>();
        private MaterialPropertyBlock block;
        private float nextSwoop, nextVisit;
        private Vector3 moon, moonRight, moonUp;

        private void Start()
        {
            if (model == null || count <= 0) { enabled = false; return; }
            block = new MaterialPropertyBlock();
            moon = SkyDirection(moonLongitude, moonLatitude);
            moonRight = Vector3.Cross(Vector3.up, moon).normalized;
            moonUp = Vector3.Cross(moon, moonRight).normalized;
            float width = ModelWidth();
            for (int i = 0; i < count; i++) flyers.Add(Make(i, width));
            // An empty sky until the first visit.
            foreach (var f in flyers) f.root.gameObject.SetActive(false);
            nextSwoop = Time.time + Random.Range(3f, 6f);
            nextVisit = Time.time + Random.Range(firstVisitAfter.x, firstVisitAfter.y);
        }

        // One to three of the slimes not out already come: each a little after the last, from far off to one side of the
        // moon, and they leave toward there again when the visit is over.
        private void StartVisit(float now)
        {
            float length = Random.Range(visitSeconds.x, visitSeconds.y);
            nextVisit = now + length + arriveSeconds * 2f + Random.Range(visitEvery.x, visitEvery.y);
            idle.Clear();
            foreach (var f in flyers) if (!f.visiting && f.root != null) idle.Add(f);
            int want = Mathf.Min(idle.Count, Random.Range(Mathf.Max(1, visitSlimes.x), Mathf.Max(visitSlimes.x, visitSlimes.y) + 1));
            float side = Random.value < 0.5f ? -1f : 1f;
            float at = now;
            for (int i = 0; i < want; i++)
            {
                var f = idle[Random.Range(0, idle.Count)];
                idle.Remove(f);
                f.visiting = true;
                f.justShown = true;
                f.swoopAt = -1f;
                f.arriveAt = at;
                f.leaveAt = at + length * Random.Range(0.85f, 1f);
                f.far = SkyDirection(moonLongitude + side * Random.Range(70f, 120f), Random.Range(12f, 30f));
                at += Random.Range(0.6f, 1.8f);
            }
        }

        // How far in a visiting slime is (0 = far off and tiny, 1 = on its loop); below 0 = not out (yet, or any more).
        private float Presence(Flyer f, float now)
        {
            if (!f.visiting || now < f.arriveAt) return -1f;
            if (now < f.leaveAt) return Mathf.Clamp01((now - f.arriveAt) / arriveSeconds);
            float p = 1f - (now - f.leaveAt) / arriveSeconds;
            if (p <= 0f)
            {
                f.visiting = false;
                f.swoopAt = -1f;
                return -1f;
            }
            return p;
        }

        /// <summary>A direction of the sky picture (BallValleySky's layout, which the skybox is turned to match).</summary>
        public static Vector3 SkyDirection(float longitude, float latitude)
        {
            float lon = longitude * Mathf.Deg2Rad, lat = latitude * Mathf.Deg2Rad, c = Mathf.Cos(lat);
            return new Vector3(c * Mathf.Sin(lon), Mathf.Sin(lat), c * Mathf.Cos(lon));
        }

        // The model's wing tip to wing tip, at scale 1.
        private float ModelWidth()
        {
            var probe = Instantiate(model);
            probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            probe.transform.localScale = Vector3.one;
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;
            foreach (var r in probe.GetComponentsInChildren<Renderer>())
            {
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            Destroy(probe);
            return any ? Mathf.Max(0.01f, bounds.size.x) : 1f;
        }

        private Flyer Make(int index, float width)
        {
            var go = Instantiate(model, transform);
            go.name = "Sky Slime " + (index + 1);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            var f = new Flyer { root = go.transform };
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (material != null) r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                bool eye = r.name.Contains("Eye");
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, eye ? Color.white : skin);
                block.SetColor(EmissionId, eye ? eyeGlow : faceGlow);
                r.SetPropertyBlock(block);
                if (r.name.Contains("Wing_L")) f.wingLeft = r.transform;
                else if (r.name.Contains("Wing_R")) f.wingRight = r.transform;
            }
            if (f.wingLeft) { f.wingLeftRest = f.wingLeft.localRotation; f.wingLeftSide = WingSide(f.wingLeft, -1f); }
            if (f.wingRight) { f.wingRightRest = f.wingRight.localRotation; f.wingRightSide = WingSide(f.wingRight, 1f); }

            // Spread out: each its own loop, speed (some the other way round), distance and size.
            float t = count > 1 ? index / (float)(count - 1) : 0.5f;
            f.distance = Random.Range(distance.x, distance.y);
            float span = 2f * f.distance * Mathf.Tan(Random.Range(wingspan.x, wingspan.y) * 0.5f * Mathf.Deg2Rad);
            f.scale = span / width;
            go.transform.localScale = Vector3.one * f.scale;
            f.loop = Mathf.Lerp(loopDegrees.x, loopDegrees.y, t) * Random.Range(0.85f, 1.15f);
            f.loopSpeed = Random.Range(loopSpeed.x, loopSpeed.y) * (index % 2 == 0 ? 1f : -1f);
            f.phase = Random.Range(0f, 360f);
            f.flaps = Random.Range(flapsPerSecond.x, flapsPerSecond.y);
            f.flapPhase = Random.value;
            f.squashX = Random.Range(1.2f, 1.6f);    // loops wider than tall
            f.squashY = Random.Range(0.6f, 0.9f);
            return f;
        }

        // Which way the wing points out (its mesh's middle, left or right of its hinge), so flapping lifts its tip.
        private static float WingSide(Transform wing, float fallback)
        {
            var filter = wing.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return fallback;
            float x = filter.sharedMesh.bounds.center.x;
            return Mathf.Abs(x) < 0.001f ? fallback : Mathf.Sign(x);
        }

        private void LateUpdate()
        {
            var head = Camera.main;
            if (head == null || flyers.Count == 0) return;
            var centre = head.transform.position;
            float now = Time.time, dt = Time.deltaTime;

            // Now and then a visit: a few fly in, loop round the moon a while, and fly off.
            if (now >= nextVisit) StartVisit(now);

            // Now and then one of those out (fully in, not leaving soon, not swooping) goes far out across the sky and back.
            if (now >= nextSwoop)
            {
                nextSwoop = now + Random.Range(swoopEvery.x, swoopEvery.y);
                var f = flyers[Random.Range(0, flyers.Count)];
                if (f.swoopAt < 0f && Presence(f, now) >= 1f && now + swoopSeconds < f.leaveAt)
                {
                    f.swoopAt = now;
                    float side = Random.value < 0.5f ? -1f : 1f;
                    f.swoopOut = SkyDirection(moonLongitude + side * Random.Range(swoopReach.x, swoopReach.y), Random.Range(20f, 42f));
                }
            }

            foreach (var f in flyers)
            {
                if (f.root == null) continue;
                float presence = Presence(f, now);
                if (presence < 0f)
                {
                    if (f.root.gameObject.activeSelf) f.root.gameObject.SetActive(false);
                    continue;
                }
                if (!f.root.gameObject.activeSelf) f.root.gameObject.SetActive(true);
                f.phase += f.loopSpeed * dt;
                var dir = LoopDirection(f);
                // Flying in from far off (small, low over to one side of the moon) and back off again.
                float inward = Mathf.SmoothStep(0f, 1f, presence);
                if (inward < 1f)
                {
                    dir = Vector3.Slerp(f.far, dir, inward);
                    dir = (dir + Vector3.up * (0.1f * Mathf.Sin(inward * Mathf.PI))).normalized;
                }
                f.root.localScale = Vector3.one * (f.scale * Mathf.Lerp(0.1f, 1f, inward));
                if (f.justShown)
                {
                    f.justShown = false;
                    f.lastDir = dir;
                }
                if (f.swoopAt >= 0f)
                {
                    float s = (now - f.swoopAt) / Mathf.Max(0.5f, swoopSeconds);
                    if (s >= 1f) f.swoopAt = -1f;
                    else
                    {
                        // Out along a high arc, then back into its loop (which kept going meanwhile).
                        float away = Mathf.Sin(s * Mathf.PI);
                        dir = Vector3.Slerp(dir, f.swoopOut, Mathf.SmoothStep(0f, 1f, away));
                        dir = (dir + Vector3.up * (0.12f * away)).normalized;
                    }
                }

                // Face the way it's going round the sky (as seen from the head), leaning into its loop.
                var moved = dir - f.lastDir;
                f.lastDir = dir;
                var pos = centre + dir * f.distance;
                if (moved.sqrMagnitude > 1e-10f)
                {
                    var forward = Vector3.ProjectOnPlane(moved, dir);
                    if (forward.sqrMagnitude > 1e-10f)
                    {
                        // Flying past, but turned partly toward the head so the face (and its glowing eyes) shows.
                        var facing = Vector3.Slerp(forward.normalized, -dir, faceTurn / 90f);
                        var look = Quaternion.LookRotation(facing, Vector3.up);
                        float lean = f.swoopAt >= 0f ? bank * 0.5f : bank * Mathf.Sign(f.loopSpeed);
                        var want = look * Quaternion.AngleAxis(lean, Vector3.forward);
                        f.root.rotation = Quaternion.Slerp(f.root.rotation, want, 1f - Mathf.Exp(-dt * 6f));
                    }
                }

                // Wing beat as the Flying Slime's: a quick downbeat (40%), a slower beat back up; up a little on each downbeat.
                f.flapPhase = Mathf.Repeat(f.flapPhase + f.flaps * dt, 1f);
                float stroke = f.flapPhase < 0.4f ? Mathf.Cos(f.flapPhase / 0.4f * Mathf.PI) : -Mathf.Cos((f.flapPhase - 0.4f) / 0.6f * Mathf.PI);
                float angle = stroke > 0f ? stroke * flapUp : stroke * flapDown;
                if (f.wingLeft) f.wingLeft.localRotation = f.wingLeftRest * Quaternion.AngleAxis(angle * f.wingLeftSide, Vector3.forward);
                if (f.wingRight) f.wingRight.localRotation = f.wingRightRest * Quaternion.AngleAxis(angle * f.wingRightSide, Vector3.forward);
                f.root.position = pos + Vector3.up * (-stroke * 0.06f * f.scale);
            }
        }

        // Its place on its loop round the moon (an ellipse on the sky, wider than tall).
        private Vector3 LoopDirection(Flyer f)
        {
            float a = f.phase * Mathf.Deg2Rad, r = f.loop * Mathf.Deg2Rad;
            var offset = moonRight * (Mathf.Cos(a) * f.squashX) + moonUp * (Mathf.Sin(a) * f.squashY);
            return (moon * Mathf.Cos(r) + offset * Mathf.Sin(r)).normalized;
        }

        private void OnDisable()
        {
            foreach (var f in flyers) if (f.root) f.root.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            // Only those out on a visit show again (the rest stay away until theirs).
            foreach (var f in flyers) if (f.root && f.visiting) f.root.gameObject.SetActive(true);
        }
    }
}
