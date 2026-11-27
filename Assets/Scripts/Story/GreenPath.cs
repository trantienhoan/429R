using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Story
{
    /// <summary>
    /// The green light path (user 2026-11-27): a glowing band on the floor, in the boss arena border's style but the
    /// Seed's green, from the player's feet to where they should go: the teleport zone (a ring on the floor with a
    /// glowing curtain, embers and a soft light) or, leading, up to someone (the Fairy). It goes round furniture (the
    /// room's NavMesh when there is one; else a straight line), sits on the floor's height at every point, draws itself
    /// out from the feet and sends bright pulses along itself towards where it leads.
    /// To a zone it is a walk of at least Min Walk metres (user: "make player walk at least 12m"): when the way there is
    /// shorter, it wanders through the room's open floor first (one to three stops, the walk closest to Min Walk, turning
    /// least, never crossing itself or leaving the room).
    /// Made and driven by <see cref="GreenPathRun"/> (the Story Green Path action); its parts are made by Tools > 429
    /// Game > Story > Set Up Green Path (Prefabs/Story/Green_Path).
    /// </summary>
    public class GreenPath : MonoBehaviour
    {
        [Header("Parts (made by Set Up Green Path)")]
        [SerializeField] private LineRenderer line;
        [SerializeField] private ParticleSystem embers;
        [SerializeField] private ParticleSystem glow;
        [SerializeField] private ParticleSystem sparks;
        [SerializeField] private Transform zone;
        [SerializeField] private LineRenderer zoneRing;
        [SerializeField] private ParticleSystem zoneGlow;
        [SerializeField] private ParticleSystem zoneCurtain;
        [SerializeField] private ParticleSystem zoneEmbers;
        [SerializeField] private Light zoneLight;
        [SerializeField] private AudioSource zoneHum;
        [SerializeField] private AudioSource voice;

        [Header("How much of each")]
        [Tooltip("Embers a second for every metre of path.")]
        [SerializeField] private float embersPerMetre = 3f;
        [Tooltip("Soft floor glows a second for every metre of path.")]
        [SerializeField] private float glowPerMetre = 0.9f;
        [Tooltip("Sparks a second off each running pulse.")]
        [SerializeField] private float sparksPerSecond = 50f;
        [SerializeField] private float curtainPerSecond = 18f;
        [SerializeField] private float zoneEmbersPerSecond = 14f;
        [SerializeField] private float zoneGlowPerSecond = 1.6f;
        [Tooltip("The zone's soft light on the floor round it; it dims while the teleport works (the dark: only the path, " +
                 "the ring and the tube glow).")]
        [SerializeField] private float zoneLightIntensity = 1.1f;
        [Tooltip("Open floor wanted round the zone: metres to the NavMesh's edge, which already keeps its agents' radius " +
                 "(0.5 m) off walls and furniture.")]
        [SerializeField] private float zoneClearance = 0.6f;
        [Tooltip("The band's width (metres).")]
        [SerializeField] private float lineWidth = 0.3f;
        [Tooltip("The zone ring's width (metres).")]
        [SerializeField] private float ringWidth = 0.07f;

        private const int RingPoints = 48;
        // Smoothing the corners takes a little off the walk: planned this much longer.
        private const float SmoothingMargin = 0.8f;

        private GreenPathSettings settings;
        private readonly List<Vector3> points = new List<Vector3>();
        private readonly List<float> along = new List<float>();
        private readonly List<Vector3> corners = new List<Vector3>();
        private readonly float[] heads = new float[8];
        private Vector3[] buffer = new Vector3[64];
        private float length, drawn, alpha, fadeSpeed = 2.5f, pulseClock, zoneShow, leftAt = -1f;
        private bool hasZone, leaving, built, zoneAtEnd;
        private Vector3 end, middle, zoneAt;
        private float owedEmbers, owedGlow, owedSparks, owedCurtain, owedZoneEmbers, owedZoneGlow;
        private Gradient gradient;
        private readonly GradientAlphaKey[] alphaKeys = new GradientAlphaKey[8];
        private readonly List<Vector2> keyList = new List<Vector2>();

        /// <summary>Where it ends: the zone's middle (or short of who it leads to), on the floor.</summary>
        public Vector3 End => end;

        /// <summary>Whether it has a teleport zone (at its end, or put elsewhere: Place Zone At).</summary>
        public bool HasZone => hasZone;

        /// <summary>The teleport zone's middle, on the floor.</summary>
        public Vector3 ZoneAt => hasZone ? zoneAt : end;

        /// <summary>How clear and bright the portal is now from where the player stands (0..1; 1 while it works).</summary>
        public float Nearness { get; private set; }

        /// <summary>The zone stays hidden while this is on (e.g. until the hallway reaches it).</summary>
        public bool ZoneHeld { get; set; }

        /// <summary>The teleport zone goes here (the hallway's far end) instead of at the path's end.</summary>
        public void PlaceZoneAt(Vector3 at)
        {
            hasZone = true;
            zoneAtEnd = false;
            zoneAt = at;
            PlaceZone();
        }

        /// <summary>Drawn all the way out (the zone shows then).</summary>
        public bool Drawn => built && drawn >= length - 0.001f;

        /// <summary>Its length in metres.</summary>
        public float Length => length;

        /// <summary>Its points (on the floor), for tests.</summary>
        public IReadOnlyList<Vector3> Points => points;

        /// <summary>Whether it found its way on the NavMesh (else it is a straight line).</summary>
        public bool OnNavMesh { get; private set; }

        /// <summary>How many stops its wander has on the way to the zone.</summary>
        public int Stops { get; private set; }

        /// <summary>The zone glows harder while it works (0 = as normal, 1 = the teleport under way).</summary>
        public float Charge { get; set; }

        /// <summary>
        /// Makes the path from the player's feet to To. Zone: it ends in the teleport zone (its spot moved to the nearest
        /// open walkable floor), after a walk of at least Min Walk metres round Middle (the room's middle). Stop Short: it
        /// ends this far before To (leading up to someone). Null without looks.
        /// </summary>
        public static GreenPath Make(GreenPathSettings settings, Vector3 from, Vector3 to, bool zone, float stopShort,
            Vector3 middle, float minWalk)
        {
            if (settings == null || settings.PathPrefab == null) return null;
            var go = Instantiate(settings.PathPrefab);
            go.name = "Green_Path";
            var path = go.GetComponent<GreenPath>();
            if (path == null)
            {
                Destroy(go);
                return null;
            }
            path.settings = settings;
            path.hasZone = zone;
            path.zoneAtEnd = zone;
            path.middle = middle;
            path.Build(from, to, zone, stopShort, minWalk);
            path.Say(settings.AppearSound, settings.AppearVolume, from);
            return path;
        }

        /// <summary>
        /// The player went another way: a new start from their feet that joins the path where it is nearest to them, the
        /// rest as it was (its wander and its end stay). It draws again from their feet.
        /// </summary>
        public void Rebuild(Vector3 from, Vector3 to, float stopShort)
        {
            if (!built || points.Count < 2)
            {
                Build(from, zoneAtEnd ? end : to, false, stopShort, 0f);
                drawn = 0f;
                return;
            }
            int join = 0;
            float best = float.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                float d = Flat(points[i] - from);
                if (d >= best) continue;
                best = d;
                join = i;
            }
            var route = new List<Vector3>();
            if (!FilterFor(from, points[join], out var filter) || !Leg(from, points[join], filter, route, out _))
            {
                route.Clear();
                route.Add(from);
                route.Add(points[join]);
            }
            Smooth(route, 2);
            for (int i = join + 1; i < points.Count; i++) route.Add(points[i]);
            Resample(route);
            end = points[points.Count - 1];
            drawn = 0f;
        }

        /// <summary>Fades it away over these seconds, then it goes.</summary>
        public void Leave(float seconds)
        {
            leaving = true;
            fadeSpeed = 1f / Mathf.Max(0.02f, seconds);
        }

        /// <summary>How far (metres, along the floor) a point is from the drawn path.</summary>
        public float DistanceOff(Vector3 p)
        {
            if (points.Count == 0) return 0f;
            if (points.Count == 1) return Flat(p - points[0]);
            float best = float.MaxValue;
            for (int i = 1; i < points.Count; i++) best = Mathf.Min(best, FlatToSegment(p, points[i - 1], points[i]));
            return best;
        }

        // ---------- the way ----------

        private void Build(Vector3 from, Vector3 to, bool snapEnd, float stopShort, float minWalk)
        {
            var target = to;
            if (snapEnd && settings.ExitSnap > 0f && SnapOpen(to, settings.ExitSnap, zoneClearance, out var walkable)) to = walkable;
            // Rounding the corners takes some of the walk off: planned longer again until the drawn path is long enough.
            float aim = minWalk > 0f ? minWalk + SmoothingMargin : 0f;
            for (int attempt = 0; ; attempt++)
            {
                OnNavMesh = Plan(from, to, aim, corners);
                if (!OnNavMesh)
                {
                    corners.Clear();
                    corners.Add(from);
                    corners.Add(to);
                }
                Smooth(corners, 2);
                Resample(corners);
                if (minWalk <= 0f || !OnNavMesh || length >= minWalk || attempt >= 3) break;
                aim += minWalk - length + 0.5f;
            }
            if (stopShort > 0f) CutShort(target, stopShort);
            end = points[points.Count - 1];
            built = true;
            if (zoneAtEnd) zoneAt = end;
            if (hasZone) PlaceZone();
        }

        // The way on the NavMesh (the agent type with floor under both ends), round the furniture. To a zone shorter than
        // Min Walk, it wanders: of many tries with one to three stops on open floor round the room's middle (never further
        // out than the zone or the player, so never out of the room), the one whose real walk on the NavMesh is closest
        // to Min Walk (and no shorter), turning least and never crossing itself. The tries are the same for the same
        // start and end. It starts at From and ends at To exactly.
        private bool Plan(Vector3 from, Vector3 to, float minWalk, List<Vector3> into)
        {
            into.Clear();
            Stops = 0;
            if (!FilterFor(from, to, out var filter)) return false;
            var direct = new List<Vector3>();
            if (!Leg(from, to, filter, direct, out _))
            {
                direct.Clear();
                direct.Add(from);
                direct.Add(to);
            }
            if (minWalk <= 0f || FlatLength(direct) >= minWalk)
            {
                into.AddRange(direct);
                return true;
            }
            float reach = Mathf.Max(3f, Mathf.Max(Flat(from - middle), Flat(to - middle)) + 0.5f);
            var spots = OpenSpots(middle, reach, 0.25f, filter);
            var random = new System.Random(Mathf.RoundToInt(from.x * 7f) * 7919 + Mathf.RoundToInt(from.z * 7f) * 104729 +
                                           Mathf.RoundToInt(to.x * 7f) * 1299709 + Mathf.RoundToInt(to.z * 7f));
            List<Vector3> best = null;
            float bestScore = float.MaxValue;
            int bestStops = 0;
            var route = new List<Vector3>();
            var leg = new List<Vector3>();
            var pick = new List<Vector3>();
            for (int trial = 0; trial < Tries && spots.Count > 0; trial++)
            {
                int wanted = 1 + random.Next(3);
                pick.Clear();
                for (int tries = 0; tries < 24 && pick.Count < wanted; tries++)
                {
                    var p = spots[random.Next(spots.Count)];
                    if (Flat(p - from) < 1.2f || Flat(p - to) < 1.2f) continue;
                    bool near = false;
                    foreach (var q in pick)
                    {
                        if (Flat(p - q) >= 1.2f) continue;
                        near = true;
                        break;
                    }
                    if (!near) pick.Add(p);
                }
                if (pick.Count == 0) continue;
                route.Clear();
                route.Add(from);
                bool ok = true;
                var last = from;
                for (int i = 0; i <= pick.Count && ok; i++)
                {
                    var next = i < pick.Count ? pick[i] : to;
                    if (!Leg(last, next, filter, leg, out bool there) || !there) ok = false;
                    else for (int k = 1; k < leg.Count; k++) route.Add(leg[k]);
                    last = next;
                }
                if (!ok) continue;
                float walk = FlatLength(route);
                float score = walk < minWalk
                    ? 1000f + (minWalk - walk) * 10f
                    : walk - minWalk + Turning(route, out int hairpins) * 0.6f + hairpins * 2f + Crossings(route) * 25f +
                      CloseParts(route) * 0.4f;
                if (score >= bestScore) continue;
                bestScore = score;
                best = new List<Vector3>(route);
                bestStops = pick.Count;
            }
            into.AddRange(best ?? direct);
            Stops = best != null ? bestStops : 0;
            return true;
        }

        // Tries of a wander (each one to three NavMesh legs).
        private const int Tries = 120;

        // Walkable spots 0.8 m apart round the middle, each with this much open floor round it (to the NavMesh's edge).
        private static List<Vector3> OpenSpots(Vector3 middle, float reach, float clearance, NavMeshQueryFilter filter)
        {
            var spots = new List<Vector3>();
            const float gap = 0.8f;
            int n = Mathf.CeilToInt(reach / gap);
            for (int x = -n; x <= n; x++)
            {
                for (int z = -n; z <= n; z++)
                {
                    var c = middle + new Vector3(x * gap, 0f, z * gap);
                    if (Flat(c - middle) > reach) continue;
                    if (!NavMesh.SamplePosition(c, out var hit, 0.4f, filter)) continue;
                    if (!NavMesh.FindClosestEdge(hit.position, out var edge, filter) || edge.distance < clearance) continue;
                    spots.Add(hit.position);
                }
            }
            return spots;
        }

        private static float FlatLength(List<Vector3> route)
        {
            float sum = 0f;
            for (int i = 1; i < route.Count; i++) sum += Flat(route[i] - route[i - 1]);
            return sum;
        }

        // How much it turns: the angles (radians) at its corners, past 20 degrees each; Hairpins: corners past 120 degrees
        // (it doubles back).
        private static float Turning(List<Vector3> route, out int hairpins)
        {
            float sum = 0f;
            hairpins = 0;
            for (int i = 1; i < route.Count - 1; i++)
            {
                var a = new Vector2(route[i].x - route[i - 1].x, route[i].z - route[i - 1].z);
                var b = new Vector2(route[i + 1].x - route[i].x, route[i + 1].z - route[i].z);
                if (a.sqrMagnitude < 0.0001f || b.sqrMagnitude < 0.0001f) continue;
                float turn = Vector2.Angle(a, b) * Mathf.Deg2Rad;
                if (turn > 0.35f) sum += turn;
                if (turn > 2.1f) hairpins++;
            }
            return sum;
        }

        // How much of it runs close by another part of itself (a spur out and back, a thin loop): points every half metre,
        // pairs under 0.9 m apart that are more than 2.5 m apart along it.
        private static int CloseParts(List<Vector3> route)
        {
            var pts = new List<Vector3> { route[0] };
            var at = new List<float> { 0f };
            float total = 0f;
            for (int i = 1; i < route.Count; i++)
            {
                var a = route[i - 1];
                var b = route[i];
                float seg = Flat(b - a);
                int n = Mathf.Max(1, Mathf.CeilToInt(seg / 0.5f));
                for (int k = 1; k <= n; k++)
                {
                    pts.Add(Vector3.Lerp(a, b, k / (float)n));
                    total += seg / n;
                    at.Add(total);
                }
            }
            int close = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                for (int j = i + 1; j < pts.Count; j++)
                {
                    if (at[j] - at[i] < 2.5f) continue;
                    if (Flat(pts[i] - pts[j]) < 0.9f) close++;
                }
            }
            return close;
        }

        // How many times it crosses itself (its segments, along the floor).
        private static int Crossings(List<Vector3> route)
        {
            int count = 0;
            for (int i = 1; i < route.Count; i++)
            {
                for (int j = i + 2; j < route.Count; j++)
                {
                    if (Cross(route[i - 1], route[i], route[j - 1], route[j])) count++;
                }
            }
            return count;
        }

        private static bool Cross(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            float d1 = Side(c, d, a), d2 = Side(c, d, b), d3 = Side(a, b, c), d4 = Side(a, b, d);
            return d1 * d2 < 0f && d3 * d4 < 0f;
        }

        private static float Side(Vector3 a, Vector3 b, Vector3 p) => (b.x - a.x) * (p.z - a.z) - (b.z - a.z) * (p.x - a.x);

        // The agent type whose NavMesh joins both ends (one that only gets part of the way when none does).
        private static bool FilterFor(Vector3 from, Vector3 to, out NavMeshQueryFilter filter)
        {
            var path = new NavMeshPath();
            int types = NavMesh.GetSettingsCount();
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < types; i++)
                {
                    var f = new NavMeshQueryFilter { agentTypeID = NavMesh.GetSettingsByIndex(i).agentTypeID, areaMask = NavMesh.AllAreas };
                    if (!NavMesh.SamplePosition(from, out var a, 1.5f, f)) continue;
                    if (!NavMesh.SamplePosition(to, out var b, 2.5f, f)) continue;
                    if (!NavMesh.CalculatePath(a.position, b.position, f, path) || path.status == NavMeshPathStatus.PathInvalid) continue;
                    if (pass == 0 && path.status != NavMeshPathStatus.PathComplete) continue;
                    filter = f;
                    return true;
                }
            }
            filter = default;
            return false;
        }

        // One leg on the NavMesh, starting at A and ending at B exactly (the NavMesh's own ends are a little off); There:
        // the NavMesh joins them all the way (else the rest is a straight line). False when there is no NavMesh leg.
        private static bool Leg(Vector3 a, Vector3 b, NavMeshQueryFilter filter, List<Vector3> into, out bool there)
        {
            into.Clear();
            there = false;
            if (!NavMesh.SamplePosition(a, out var ha, 1.5f, filter)) return false;
            if (!NavMesh.SamplePosition(b, out var hb, 2.5f, filter)) return false;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(ha.position, hb.position, filter, path)) return false;
            if (path.status == NavMeshPathStatus.PathInvalid || path.corners.Length < 2) return false;
            there = path.status == NavMeshPathStatus.PathComplete;
            into.AddRange(path.corners);
            into[0] = a;
            if (there) into[into.Count - 1] = b;
            else into.Add(b);
            return true;
        }

        private static float LegLength(Vector3 a, Vector3 b, NavMeshQueryFilter filter, List<Vector3> scratch, out bool there)
        {
            if (!Leg(a, b, filter, scratch, out there)) return Flat(b - a);
            float sum = 0f;
            for (int i = 1; i < scratch.Count; i++) sum += Flat(scratch[i] - scratch[i - 1]);
            return sum;
        }

        // The nearest walkable floor to a spot, within Reach (any agent type's NavMesh), with Clearance metres of open
        // floor round it if there is such a spot (the NavMesh's edges are the walls and the furniture): rings of
        // candidates every 0.35 m out from the spot; one short of room counts as further away (4 m for every metre short).
        private static bool SnapOpen(Vector3 spot, float reach, float clearance, out Vector3 walkable)
        {
            walkable = spot;
            float best = float.MaxValue;
            const float gap = 0.35f;
            int types = NavMesh.GetSettingsCount();
            for (int i = 0; i < types; i++)
            {
                var filter = new NavMeshQueryFilter { agentTypeID = NavMesh.GetSettingsByIndex(i).agentTypeID, areaMask = NavMesh.AllAreas };
                if (!NavMesh.SamplePosition(spot, out _, reach + 0.5f, filter)) continue;
                for (float r = 0f; r <= reach + 0.01f; r += gap)
                {
                    if (best <= r) break;
                    int around = r <= 0f ? 1 : Mathf.Max(8, Mathf.RoundToInt(2f * Mathf.PI * r / gap));
                    for (int k = 0; k < around; k++)
                    {
                        float a = k * Mathf.PI * 2f / around;
                        var c = spot + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                        if (!NavMesh.SamplePosition(c, out var hit, 0.3f, filter)) continue;
                        if (!NavMesh.FindClosestEdge(hit.position, out var edge, filter)) continue;
                        float score = Flat(hit.position - spot) + Mathf.Max(0f, clearance - edge.distance) * 4f;
                        if (score >= best) continue;
                        best = score;
                        walkable = hit.position;
                    }
                }
            }
            return best < float.MaxValue;
        }

        // Rounds the corners a little (twice cut, the ends kept).
        private static void Smooth(List<Vector3> pts, int times)
        {
            for (int k = 0; k < times && pts.Count >= 3; k++)
            {
                var res = new List<Vector3>(pts.Count * 2) { pts[0] };
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    var a = pts[i];
                    var b = pts[i + 1];
                    if (i > 0) res.Add(Vector3.Lerp(a, b, 0.25f));
                    if (i < pts.Count - 2) res.Add(Vector3.Lerp(a, b, 0.75f));
                }
                res.Add(pts[pts.Count - 1]);
                pts.Clear();
                pts.AddRange(res);
            }
        }

        // A point every Step metres, each on the floor's own height.
        private void Resample(List<Vector3> route)
        {
            points.Clear();
            along.Clear();
            float step = Mathf.Max(0.05f, settings.Step);
            points.Add(OnFloor(route[0]));
            along.Add(0f);
            float total = 0f;
            for (int i = 1; i < route.Count; i++)
            {
                var a = route[i - 1];
                var b = route[i];
                int n = Mathf.Max(1, Mathf.CeilToInt(Flat(b - a) / step));
                for (int k = 1; k <= n; k++)
                {
                    var p = OnFloor(Vector3.Lerp(a, b, k / (float)n));
                    total += Vector3.Distance(points[points.Count - 1], p);
                    points.Add(p);
                    along.Add(total);
                }
            }
            length = total;
        }

        // Leading up to someone: the path stops Short metres before them.
        private void CutShort(Vector3 target, float shortBy)
        {
            for (int i = 1; i < points.Count; i++)
            {
                if (Flat(points[i] - target) > shortBy) continue;
                int keep = Mathf.Max(2, i);
                if (keep < points.Count)
                {
                    points.RemoveRange(keep, points.Count - keep);
                    along.RemoveRange(keep, along.Count - keep);
                }
                length = along[along.Count - 1];
                return;
            }
        }

        private Vector3 OnFloor(Vector3 p) => new Vector3(p.x, FloorAt(p, p.y) + settings.FloorLift, p.z);

        /// <summary>The floor's height under a point (the highest flat, solid surface near Guess; Guess when none).</summary>
        public static float FloorHeight(Vector3 p, float guess) => FloorAt(p, guess);

        // The floor's height under a point: the highest flat, solid surface near the guess (not the player, not a loose
        // thing, not a table top); the guess when there is none.
        private static float FloorAt(Vector3 p, float guess)
        {
            var hits = Physics.RaycastAll(new Vector3(p.x, guess + 1f, p.z), Vector3.down, 2.4f, ~(1 << 2), QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            foreach (var h in hits)
            {
                if (h.normal.y < 0.6f || h.point.y > guess + 0.3f) continue;
                var body = h.collider.attachedRigidbody;
                if (body != null && !body.isKinematic) continue;
                if (h.collider.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>() != null) continue;
                if (h.point.y > best) best = h.point.y;
            }
            return best > float.NegativeInfinity ? best : guess;
        }

        private Vector3 PointAt(float d)
        {
            if (points.Count == 0) return transform.position;
            if (d <= 0f) return points[0];
            for (int i = 1; i < points.Count; i++)
            {
                if (along[i] < d) continue;
                float seg = along[i] - along[i - 1];
                return Vector3.Lerp(points[i - 1], points[i], seg > 0f ? (d - along[i - 1]) / seg : 1f);
            }
            return points[points.Count - 1];
        }

        private void PlaceZone()
        {
            if (zone == null) return;
            zone.position = zoneAt;
            zone.rotation = Quaternion.identity;
            if (zoneRing == null) return;
            float r = settings.ZoneRadius;
            zoneRing.useWorldSpace = true;
            zoneRing.loop = true;
            zoneRing.positionCount = RingPoints;
            for (int i = 0; i < RingPoints; i++)
            {
                float a = i * Mathf.PI * 2f / RingPoints;
                zoneRing.SetPosition(i, zoneAt + new Vector3(Mathf.Cos(a) * r, 0.005f, Mathf.Sin(a) * r));
            }
            zoneRing.widthMultiplier = ringWidth;
        }

        // ---------- every frame ----------

        private void Awake()
        {
            gradient = new Gradient();
            if (line != null)
            {
                line.positionCount = 0;
                line.widthMultiplier = lineWidth;
            }
            if (zoneRing != null) zoneRing.positionCount = 0;
            if (zoneLight != null) zoneLight.intensity = 0f;
        }

        private void Update()
        {
            if (!built) return;
            float dt = Time.deltaTime;
            alpha = Mathf.MoveTowards(alpha, leaving ? 0f : 1f, dt * fadeSpeed);
            if (!leaving) drawn = Mathf.Min(length, drawn + dt * settings.DrawSpeed);
            pulseClock += dt;
            UpdateLine();
            if (!leaving) Emit(dt);
            UpdateZone(dt);
            if (leaving && alpha <= 0f)
            {
                if (leftAt < 0f)
                {
                    leftAt = Time.time;
                    if (zoneHum != null) zoneHum.Stop();
                }
                // The last embers fade out by themselves first.
                if (Time.time - leftAt > 2.5f) Destroy(gameObject);
            }
        }

        // Where the bright pulses' heads are now (metres along): while it draws, its drawing tip; then pulses running
        // towards the end, Pulse Spacing apart (at least half the path, so no more than two show at once).
        private int Heads()
        {
            if (drawn < length - 0.001f)
            {
                heads[0] = drawn;
                return 1;
            }
            float speed = Mathf.Max(0.2f, settings.PulseSpeed);
            float spacing = Mathf.Max(settings.PulseSpacing, length * 0.5f + 0.5f);
            float first = (pulseClock * speed) % spacing;
            int n = 0;
            for (float h = first; h <= length + settings.PulseLength && n < heads.Length; h += spacing) heads[n++] = h;
            return n;
        }

        private void UpdateLine()
        {
            if (line == null) return;
            float upTo = Mathf.Min(drawn, length);
            int count = 0;
            for (int i = 0; i < points.Count; i++)
            {
                if (count + 1 >= buffer.Length) System.Array.Resize(ref buffer, buffer.Length * 2);
                if (along[i] <= upTo)
                {
                    buffer[count++] = points[i];
                    continue;
                }
                if (i > 0)
                {
                    float seg = along[i] - along[i - 1];
                    buffer[count++] = Vector3.Lerp(points[i - 1], points[i], seg > 0f ? (upTo - along[i - 1]) / seg : 1f);
                }
                break;
            }
            if (count < 2 || upTo <= 0.01f)
            {
                line.positionCount = 0;
                return;
            }
            line.positionCount = count;
            for (int i = 0; i < count; i++) line.SetPosition(i, buffer[i]);

            // The band's brightness: Band Brightness all along, bright pulses running towards the end (additive: alpha is
            // brightness), all times the fade. Three keys a pulse (the gradient holds eight): two pulses at most.
            float baseA = settings.BandBrightness * alpha;
            float peak = alpha;
            float tail = settings.PulseLength;
            keyList.Clear();
            keyList.Add(new Vector2(0f, baseA));
            keyList.Add(new Vector2(1f, baseA));
            int n = Heads();
            int shown = 0;
            for (int i = 0; i < n && shown < 2; i++)
            {
                float head = heads[i];
                if (head - tail > upTo) continue;
                AddKey(head - tail, upTo, baseA);
                AddKey(head, upTo, peak);
                AddKey(head + 0.15f, upTo, baseA);
                shown++;
            }
            keyList.Sort((a, b) => a.x.CompareTo(b.x));
            int keys = 0;
            float last = -1f;
            foreach (var k in keyList)
            {
                if (keys >= alphaKeys.Length) break;
                float x = Mathf.Max(k.x, last + 0.0005f);
                if (x > 1f) break;
                alphaKeys[keys++] = new GradientAlphaKey(k.y, x);
                last = x;
            }
            var colour = settings.Colour;
            var alphaSet = new GradientAlphaKey[keys];
            System.Array.Copy(alphaKeys, alphaSet, keys);
            gradient.SetKeys(new[] { new GradientColorKey(colour, 0f), new GradientColorKey(colour, 1f) }, alphaSet);
            line.colorGradient = gradient;
        }

        private void AddKey(float metres, float upTo, float a)
        {
            float x = metres / Mathf.Max(0.01f, upTo);
            if (x <= 0f || x >= 1f) return;
            keyList.Add(new Vector2(x, a));
        }

        private void Emit(float dt)
        {
            float upTo = Mathf.Min(drawn, length);
            if (upTo <= 0.05f) return;
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            if (embers != null)
            {
                owedEmbers += embersPerMetre * upTo * dt;
                for (; owedEmbers >= 1f; owedEmbers -= 1f)
                {
                    p.position = Jitter(PointAt(Random.Range(0f, upTo)), 0.08f) + Vector3.up * 0.03f;
                    embers.Emit(p, 1);
                }
            }
            if (glow != null)
            {
                owedGlow += glowPerMetre * upTo * dt;
                for (; owedGlow >= 1f; owedGlow -= 1f)
                {
                    p.position = PointAt(Random.Range(0f, upTo)) + Vector3.up * 0.01f;
                    glow.Emit(p, 1);
                }
            }
            if (sparks == null) return;
            int n = Heads();
            owedSparks += sparksPerSecond * dt;
            int count = Mathf.FloorToInt(owedSparks);
            owedSparks -= count;
            for (int i = 0; i < n; i++)
            {
                if (heads[i] < 0f || heads[i] > upTo + 0.01f) continue;
                var at = PointAt(heads[i]);
                for (int k = 0; k < count; k++)
                {
                    p.position = Jitter(at, 0.06f) + Vector3.up * 0.04f;
                    sparks.Emit(p, 1);
                }
            }
        }

        private void UpdateZone(float dt)
        {
            if (!hasZone || zone == null) return;
            float show = Drawn && !ZoneHeld ? alpha : 0f;
            zoneShow = Mathf.MoveTowards(zoneShow, show, dt * 2.5f);
            float charge = Mathf.Clamp01(Charge);
            // Only clearer and brighter the closer the player comes (user 2026-11-27); all of it while it works.
            Nearness = charge > 0f ? 1f : NearnessNow();
            float near = Nearness;
            if (zoneRing != null)
            {
                var c = settings.Colour;
                c.a = zoneShow * near * (0.75f + 0.25f * Mathf.Sin(Time.time * 3.2f)) * (1f + charge);
                zoneRing.startColor = c;
                zoneRing.endColor = c;
            }
            if (zoneLight != null) zoneLight.intensity = zoneLightIntensity * zoneShow * near * (1f - 0.65f * charge);
            if (zoneHum != null)
            {
                if (zoneHum.clip == null) zoneHum.clip = settings.ZoneHum;
                zoneHum.volume = settings.ZoneHumVolume * zoneShow * near * (1f + charge);
                zoneHum.pitch = 1f + 0.35f * charge;
                if (zoneShow > 0.01f && zoneHum.clip != null && !zoneHum.isPlaying) zoneHum.Play();
            }
            if (zoneShow <= 0.01f || leaving) return;
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            float r = settings.ZoneRadius;
            float more = (1f + 3f * charge) * near;
            if (zoneCurtain != null)
            {
                owedCurtain += curtainPerSecond * dt * zoneShow * more;
                for (; owedCurtain >= 1f; owedCurtain -= 1f)
                {
                    float a = Random.value * Mathf.PI * 2f;
                    p.position = zoneAt + new Vector3(Mathf.Cos(a) * r, 0.05f, Mathf.Sin(a) * r);
                    zoneCurtain.Emit(p, 1);
                }
            }
            if (zoneEmbers != null)
            {
                owedZoneEmbers += zoneEmbersPerSecond * dt * zoneShow * more;
                for (; owedZoneEmbers >= 1f; owedZoneEmbers -= 1f)
                {
                    var d = Random.insideUnitCircle * r;
                    p.position = zoneAt + new Vector3(d.x, 0.04f, d.y);
                    zoneEmbers.Emit(p, 1);
                }
            }
            if (zoneGlow != null)
            {
                owedZoneGlow += zoneGlowPerSecond * dt * zoneShow * near;
                for (; owedZoneGlow >= 1f; owedZoneGlow -= 1f)
                {
                    p.position = zoneAt + Vector3.up * 0.015f;
                    zoneGlow.Emit(p, 1);
                }
            }
        }

        // Faint far off, fully clear within Clear At (flat metres from the player's head to the zone's middle).
        private float NearnessNow()
        {
            var cam = Camera.main;
            if (cam == null) return 1f;
            float d = Flat(cam.transform.position - zoneAt);
            float k = 1f - Mathf.InverseLerp(settings.ZoneClearAt, settings.ZoneFaintAt, d);
            k = k * k * (3f - 2f * k);
            return Mathf.Lerp(settings.ZoneFarShare, 1f, k);
        }

        private void Say(AudioClip clip, float volume, Vector3 at)
        {
            if (voice == null || clip == null) return;
            voice.transform.position = at + Vector3.up * 1f;
            voice.PlayOneShot(clip, volume);
        }

        private static Vector3 Jitter(Vector3 p, float r)
        {
            var d = Random.insideUnitCircle * r;
            return new Vector3(p.x + d.x, p.y, p.z + d.y);
        }

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        private static float FlatToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = new Vector2(b.x - a.x, b.z - a.z);
            var ap = new Vector2(p.x - a.x, p.z - a.z);
            float len = ab.sqrMagnitude;
            float t = len > 0f ? Mathf.Clamp01(Vector2.Dot(ap, ab) / len) : 0f;
            return (ap - ab * t).magnitude;
        }
    }
}
