using System.Collections.Generic;
using Game.Locomotion;
using UnityEngine;

namespace Game.Story
{
    /// <summary>
    /// The green path's hallway out of the room (user 2026-11-27: "the path I want should be more like a dark hallway,
    /// with green light on 2 sides of the path, lead the Player outside of the room's floor to somewhere out the fall
    /// edge, create a walk path for Player can safe walk on it without falling to the fall edge"). A dark floor that
    /// reaches out from the room's doorway over the drop, lit by green light down both sides: a glowing line along each
    /// edge, green dots on the floor along both edges like a runway's lights (user 2026-11-27: "remove the pole, leave
    /// only the green dot on the ground, like the one that lit the way for plane") with their glow, a few green lamps
    /// over the floor and embers. Its floor is solid and a
    /// teleport area, and no Fall Edge counts over it (Fall Edge's safe walks), so the player walks out safely; stepping
    /// off its sides still falls. The teleport zone is at its far end (the Green Path's zone).
    /// Made by <see cref="GreenPathRun"/>; its parts by Tools > 429 Game > Story > Set Up Green Path
    /// (Prefabs/Story/Green_Hallway).
    /// </summary>
    public class GreenHallway : MonoBehaviour
    {
        [Header("Parts (made by Set Up Green Path)")]
        [Tooltip("The floor: a box (solid, a teleport area), scaled to the hallway.")]
        [SerializeField] private Transform floor;
        [Tooltip("The light line along an edge (copied for the other).")]
        [SerializeField] private LineRenderer edge;
        [Tooltip("A green light dot on the floor (off; copied along both edges).")]
        [SerializeField] private GameObject post;
        [Tooltip("A green lamp over the floor (off; copied along it).")]
        [SerializeField] private Light lamp;
        [SerializeField] private ParticleSystem embers;
        [SerializeField] private ParticleSystem lampGlow;

        [Header("Size and light")]
        [Tooltip("The floor's width and thickness (metres).")]
        [SerializeField] private float width = 1.6f;
        [SerializeField] private float thickness = 0.12f;
        [Tooltip("Metres between the light dots along each edge, and how far past the floor's edge they sit (below 0: " +
                 "on the floor, in from its edge).")]
        [SerializeField] private float postSpacing = 1.5f;
        [SerializeField] private float postOut = -0.06f;
        [Tooltip("The dots' soft glow: this high over the floor (metres).")]
        [SerializeField] private float glowHeight = 0.05f;
        [Tooltip("The edge lines' width (metres).")]
        [SerializeField] private float edgeWidth = 0.1f;
        [Tooltip("How fast it reaches out from the doorway (metres a second).")]
        [SerializeField] private float formSpeed = 8f;
        [Tooltip("Metres between the lamps over the floor (four at most).")]
        [SerializeField] private float lampSpacing = 5f;
        [SerializeField] private float lampIntensity = 1.4f;
        [SerializeField] private float lampRange = 3.4f;
        [SerializeField] private float lampHeight = 1.3f;
        [SerializeField] private float embersPerMetre = 2.5f;
        [Tooltip("Seconds between each orb's soft glows.")]
        [SerializeField] private float glowEvery = 0.35f;

        private readonly List<Transform> posts = new List<Transform>();
        private readonly List<float> postAt = new List<float>();
        private readonly List<Light> lamps = new List<Light>();
        private readonly List<float> lampAt = new List<float>();
        private LineRenderer[] edges = new LineRenderer[0];
        private BoxCollider floorBox;
        private Color colour = new Color(0.55f, 1f, 0.45f, 1f);
        private float length, formed, alpha, fadeSpeed = 2f, owedEmbers, nextGlow, leftAt = -1f;
        private bool leaving, built;

        /// <summary>Its length (metres) once formed.</summary>
        public float Length => length;

        /// <summary>The way out (along the floor, from the doorway).</summary>
        public Vector3 Outward => transform.forward;

        /// <summary>Reached all the way out.</summary>
        public bool Formed => built && formed >= length - 0.01f;

        /// <summary>The floor's top, this many metres out along the middle.</summary>
        public Vector3 PointAt(float metres) => transform.TransformPoint(new Vector3(0f, 0f, metres));

        /// <summary>Whether a point (e.g. under the head) is over its floor as far as it has formed.</summary>
        public bool Holds(Vector3 point)
        {
            if (!built) return false;
            var local = transform.InverseTransformPoint(point);
            return Mathf.Abs(local.x) <= width * 0.5f && local.z >= -0.2f && local.z <= formed + 0.1f;
        }

        /// <summary>
        /// A hallway from Start (on the floor at the doorway) out along Outward for Length metres, in the path's colour.
        /// Null without the prefab.
        /// </summary>
        public static GreenHallway Make(GameObject prefab, Vector3 start, Vector3 outward, float length, Color colour)
        {
            if (prefab == null) return null;
            outward.y = 0f;
            if (outward.sqrMagnitude < 0.0001f) return null;
            var go = Instantiate(prefab, start, Quaternion.LookRotation(outward.normalized, Vector3.up));
            go.name = "Green_Hallway";
            var hall = go.GetComponent<GreenHallway>();
            if (hall == null)
            {
                Destroy(go);
                return null;
            }
            hall.colour = colour;
            hall.Build(length);
            return hall;
        }

        /// <summary>Fades it away over these seconds, then it goes (its floor stops being safe at once).</summary>
        public void Leave(float seconds)
        {
            leaving = true;
            fadeSpeed = 1f / Mathf.Max(0.02f, seconds);
            if (floorBox != null) FallEdge.RemoveSafeWalk(floorBox);
        }

        private void Build(float metres)
        {
            length = Mathf.Max(1f, metres);
            if (floor != null)
            {
                floorBox = floor.GetComponent<BoxCollider>();
                if (floorBox != null) FallEdge.AddSafeWalk(floorBox);
            }
            if (edge != null)
            {
                var other = Instantiate(edge, transform);
                other.name = edge.name + " (other side)";
                edges = new[] { edge, other };
                foreach (var e in edges)
                {
                    e.useWorldSpace = true;
                    e.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    e.widthMultiplier = edgeWidth;
                    e.positionCount = 0;
                }
            }
            if (post != null)
            {
                post.SetActive(false);
                for (float d = 0.6f; d <= length - 0.2f; d += postSpacing)
                {
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var p = Instantiate(post, transform);
                        p.name = post.name + " " + d.ToString("0.0") + (side < 0f ? " L" : " R");
                        p.transform.localPosition = new Vector3(side * (width * 0.5f + postOut), 0f, d);
                        p.transform.localRotation = Quaternion.identity;
                        p.SetActive(false);
                        posts.Add(p.transform);
                        postAt.Add(d);
                    }
                }
            }
            if (lamp != null)
            {
                lamp.gameObject.SetActive(false);
                int count = Mathf.Clamp(Mathf.RoundToInt(length / Mathf.Max(1f, lampSpacing)), 1, 4);
                for (int i = 0; i < count; i++)
                {
                    float d = length * (i + 0.5f) / count;
                    var l = Instantiate(lamp, transform);
                    l.name = lamp.name + " " + (i + 1);
                    l.transform.localPosition = new Vector3(0f, lampHeight, d);
                    l.color = colour;
                    l.range = lampRange;
                    l.intensity = 0f;
                    l.gameObject.SetActive(true);
                    lamps.Add(l);
                    lampAt.Add(d);
                }
            }
            built = true;
            Shape();
        }

        private void Update()
        {
            if (!built) return;
            float dt = Time.deltaTime;
            alpha = Mathf.MoveTowards(alpha, leaving ? 0f : 1f, dt * fadeSpeed);
            if (!leaving && formed < length)
            {
                formed = Mathf.Min(length, formed + dt * formSpeed);
                Shape();
            }
            for (int i = 0; i < posts.Count; i++)
            {
                bool on = !leaving && postAt[i] <= formed;
                if (posts[i].gameObject.activeSelf != on) posts[i].gameObject.SetActive(on);
            }
            for (int i = 0; i < lamps.Count; i++)
                lamps[i].intensity = lampAt[i] <= formed ? lampIntensity * alpha : 0f;
            foreach (var e in edges)
            {
                var c = colour;
                c.a = alpha;
                e.startColor = c;
                e.endColor = c;
            }
            if (!leaving) Emit(dt);
            if (leaving && alpha <= 0f)
            {
                if (leftAt < 0f)
                {
                    leftAt = Time.time;
                    if (floor != null) floor.gameObject.SetActive(false);
                    foreach (var e in edges) e.positionCount = 0;
                }
                if (Time.time - leftAt > 2f) Destroy(gameObject);
            }
        }

        // The floor and the edge lines as far as it has formed.
        private void Shape()
        {
            float z = Mathf.Max(0.01f, formed);
            if (floor != null)
            {
                floor.localPosition = new Vector3(0f, -thickness * 0.5f, z * 0.5f);
                floor.localRotation = Quaternion.identity;
                floor.localScale = new Vector3(width, thickness, z);
            }
            for (int i = 0; i < edges.Length; i++)
            {
                float side = i == 0 ? -1f : 1f;
                float x = side * (width * 0.5f - 0.06f);
                if (formed < 0.15f)
                {
                    edges[i].positionCount = 0;
                    continue;
                }
                edges[i].positionCount = 2;
                edges[i].SetPosition(0, transform.TransformPoint(new Vector3(x, 0.02f, 0.05f)));
                edges[i].SetPosition(1, transform.TransformPoint(new Vector3(x, 0.02f, formed)));
            }
        }

        private void Emit(float dt)
        {
            if (formed <= 0.2f) return;
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            if (embers != null)
            {
                owedEmbers += embersPerMetre * formed * dt;
                for (; owedEmbers >= 1f; owedEmbers -= 1f)
                {
                    float side = Random.value < 0.5f ? -1f : 1f;
                    p.position = transform.TransformPoint(new Vector3(side * (width * 0.5f + Random.Range(-0.05f, 0.12f)), 0.05f, Random.Range(0f, formed)));
                    embers.Emit(p, 1);
                }
            }
            if (lampGlow != null && Time.time >= nextGlow)
            {
                nextGlow = Time.time + glowEvery;
                foreach (var t in posts)
                {
                    if (!t.gameObject.activeSelf) continue;
                    p.position = t.TransformPoint(new Vector3(0f, glowHeight, 0f));
                    lampGlow.Emit(p, 1);
                }
            }
        }

        private void OnDestroy()
        {
            if (floorBox != null) FallEdge.RemoveSafeWalk(floorBox);
        }
    }
}
