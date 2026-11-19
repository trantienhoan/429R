using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Rooms
{
    /// <summary>
    /// How a kit room comes and goes (user, 2026-11-19: "the walls come, form a room ... then to go the next room, the
    /// rooftop and all the walls go, the floor still stays, and new walls and rooftop come"). It replaces the old rigged
    /// Bedroom_Base_Z's clips with the same look and timing:
    /// - Come: every wall piece (with its posts, windows, door frames and door leaves) starts lying flat on the floor and
    ///   tiny, then unfolds upright and grows, all together, with a little overshoot (the old clips' pop-up-book look);
    ///   the roof comes last, flying in from the side and landing on the walls.
    /// - Go: the roof lifts off and flies away first; the walls hop, fold back down flat and shrink to nothing.
    /// - The floor is never touched. Walls don't block (colliders, NavMesh carving) while they move or are gone.
    /// - A set that isn't there is hidden, not switched off (the old room shrank it to nothing), waiting folded and tiny:
    ///   its doors' FSMs run from the start, so the secret door shuts as the player stands on the floor, as it always did.
    /// - A set's floor parts ("Floor...": the playroom arms' floors) grow in with it and stay after it goes, as the old
    ///   living room's floor did; parts broken away in the game (the playroom's ceiling, a brick wall) are skipped.
    /// Sets (bedroom: Mini, Big; living room: Living, Play) are the rooms that share the floor. The room's Animator states
    /// (the old names: Bedroom_Mini_COME, Bedroom_GO, COME, COME2, GO4 ...) call Play through a Room Kit State behaviour,
    /// so GAMESTAGES' Animator Play actions, its light switch and Drop Wall Hung Props keep working unchanged.
    /// Built by Tools > 429 Game > Rooms > Build Bedroom Kit Room / Build Living Room Kit Room.
    /// </summary>
    [DisallowMultipleComponent]
    public class RoomKit : MonoBehaviour
    {
        [System.Serializable]
        public class Set
        {
            public string name = "Mini";
            [Tooltip("The set's parts are this object's children (walls, posts, door frames, windows) and Roof.")]
            public Transform root;
            [Tooltip("The roof (slab, trim ring, lamp): flies in last, flies off first.")]
            public Transform roof;
        }

        [SerializeField] private Set[] sets = System.Array.Empty<Set>();

        [Header("Come")]
        [Tooltip("Degrees a wall lies folded outward before it stands up.")]
        [SerializeField] private float foldAngle = 90f;
        [Tooltip("How hard the walls overshoot as they stand (0 = none; the old clips had a little).")]
        [Min(0f)]
        [SerializeField] private float overshoot = 1.4f;
        [Tooltip("Share of the come time before the roof starts flying in.")]
        [Range(0f, 0.95f)]
        [SerializeField] private float roofStarts = 0.5f;
        [Tooltip("Where the roof flies in from and off to (from the room's middle, m).")]
        [SerializeField] private Vector3 roofAway = new Vector3(7f, 4f, 2f);
        [Tooltip("Turns the roof makes on the way in or out (degrees round the up axis).")]
        [SerializeField] private float roofSpin = 180f;

        [Header("Go")]
        [Tooltip("Share of the go time the roof takes to fly off (the walls start folding a little after it lifts).")]
        [Range(0.05f, 1f)]
        [SerializeField] private float roofLeaves = 0.45f;
        [Tooltip("Share of the go time before the walls start folding.")]
        [Range(0f, 0.9f)]
        [SerializeField] private float wallsWait = 0.15f;
        [Tooltip("How high the walls hop before they fold (m).")]
        [Min(0f)]
        [SerializeField] private float hop = 0.25f;

        [Header("Puffs (the shared 3D smoke)")]
        [Tooltip("Played at the foot of each wall as it lands (Come) and as it folds away (Go). Empty: none.")]
        [SerializeField] private GameObject puff;
        [Min(0.01f)]
        [SerializeField] private float puffScale = 1f;
        [Tooltip("Most puffs at once (a room has a dozen walls).")]
        [Min(0)]
        [SerializeField] private int maxPuffs = 6;

        private const float Tiny = 0.001f;

        private class Part
        {
            public Transform t;
            public Vector3 pos;
            public Quaternion rot;
            public Vector3 scale;
            // A floor part ("Floor...", the playroom arms' floors): grows in with its set and stays after it goes, as
            // the old room's floor did.
            public bool flat;
        }

        private class SetState
        {
            public Set set;
            public readonly List<Part> walls = new();
            public Part roof;
            // What shows and what blocks, and whether it was on in the prefab (put back as it was).
            public readonly List<(Renderer r, bool on, bool floor)> renderers = new();
            public readonly List<(Collider c, bool on)> colliders = new();
            public readonly List<(NavMeshObstacle o, bool on)> obstacles = new();
            public bool shown;
            public Coroutine running;
        }

        private readonly Dictionary<string, SetState> states = new();

        /// <summary>Whether this set is standing (came, not gone).</summary>
        public bool IsUp(string set) => states.TryGetValue(set, out var s) && s.shown && s.running == null;

        private void Awake()
        {
            foreach (var set in sets)
            {
                if (set == null || set.root == null || string.IsNullOrEmpty(set.name)) continue;
                var s = new SetState { set = set };
                foreach (Transform child in set.root)
                {
                    var part = new Part { t = child, pos = child.localPosition, rot = child.localRotation, scale = child.localScale, flat = child.name.StartsWith("Floor") };
                    if (child == set.roof) s.roof = part;
                    else s.walls.Add(part);
                }
                foreach (var r in set.root.GetComponentsInChildren<Renderer>(true))
                    s.renderers.Add((r, r.enabled, s.walls.Any(w => w.flat && r.transform.IsChildOf(w.t))));
                foreach (var c in set.root.GetComponentsInChildren<Collider>(true)) s.colliders.Add((c, c.enabled));
                foreach (var o in set.root.GetComponentsInChildren<NavMeshObstacle>(true)) s.obstacles.Add((o, o.enabled));
                states[set.name] = s;
                // START: the floor only; the sets wait folded and tiny, as a come starts.
                Pose(s, 0f, true);
                Show(s, false, false);
                Block(s, false);
            }
        }

        /// <summary>Plays a set's come (true) or go (false) over Seconds.</summary>
        public void Play(string set, bool come, float seconds)
        {
            if (!states.TryGetValue(set, out var s)) return;
            if (s.running != null) StopCoroutine(s.running);
            s.running = StartCoroutine(come ? Come(s, Mathf.Max(0.05f, seconds)) : Go(s, Mathf.Max(0.05f, seconds)));
        }

        /// <summary>Takes a set away at once, without its go (a checkpoint that starts with the next room).</summary>
        public void Hide(string set)
        {
            if (!states.TryGetValue(set, out var s)) return;
            if (s.running != null) StopCoroutine(s.running);
            s.running = null;
            Pose(s, 0f, true);
            Show(s, false, false);
            Block(s, false);
        }

        private IEnumerator Come(SetState s, float seconds)
        {
            Block(s, false);
            Pose(s, 0f, true);
            Show(s, true, true);
            int puffs = 0;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                Pose(s, t / seconds, true);
                yield return null;
            }
            Pose(s, 1f, true);
            foreach (var w in s.walls)
            {
                if (puffs++ >= maxPuffs) break;
                Puff(w.t);
            }
            Block(s, true);
            s.running = null;
        }

        private IEnumerator Go(SetState s, float seconds)
        {
            Block(s, false);
            int puffs = 0;
            bool puffed = false;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float p = t / seconds;
                Pose(s, p, false);
                if (!puffed && p >= wallsWait + 0.1f)
                {
                    puffed = true;
                    foreach (var w in s.walls)
                    {
                        if (puffs++ >= maxPuffs) break;
                        Puff(w.t);
                    }
                }
                yield return null;
            }
            // Gone: folded and tiny, unseen; its floor parts stay.
            Pose(s, 1f, false);
            Show(s, false, true);
            // A wall portal (a broken playroom brick wall) closes with its walls: the old room swung it under the floor.
            foreach (var portal in s.set.root.GetComponentsInChildren<Game.Enemies.WallPortal>()) portal.gameObject.SetActive(false);
            s.running = null;
        }

        // Every part's pose at progress p (0..1) of a come or a go.
        private void Pose(SetState s, float p, bool come)
        {
            p = Mathf.Clamp01(p);
            if (come)
            {
                // Walls: unfold (ease out) and grow (back-out overshoot) over the whole come.
                float up = EaseOutCubic(p);
                float grow = Mathf.Max(Tiny, EaseOutBack(p, overshoot));
                foreach (var w in s.walls) Wall(w, foldAngle * (1f - up), grow, 0f);
                if (s.roof != null) Roof(s.roof, Mathf.InverseLerp(roofStarts, 1f, p), true);
            }
            else
            {
                // Walls: wait, hop up, then fold flat and shrink (back-in).
                float q = Mathf.InverseLerp(wallsWait, 1f, p);
                float down = EaseInCubic(q);
                float shrink = Mathf.Max(Tiny, 1f - EaseInBack(q, overshoot));
                float lift = hop * Mathf.Sin(Mathf.Clamp01(q * 2f) * Mathf.PI);
                foreach (var w in s.walls) if (!w.flat) Wall(w, foldAngle * down, shrink, lift);
                if (s.roof != null) Roof(s.roof, Mathf.InverseLerp(0f, roofLeaves, p), false);
            }
        }

        // A wall piece folded outward about its foot (its pivot is the inner face's bottom edge; its local +Z faces the
        // room, so outward is a turn of the top towards -Z about the local X axis). A floor part ("Floor...": an arm's
        // floor that comes with its walls) only grows and shrinks. A part broken away in the game is skipped.
        private static void Wall(Part w, float angle, float size, float lift)
        {
            if (w.t == null) return;
            w.t.localPosition = w.pos + (w.flat ? Vector3.zero : Vector3.up * lift);
            w.t.localRotation = w.flat ? w.rot : w.rot * Quaternion.AngleAxis(-angle, Vector3.right);
            w.t.localScale = w.scale * size;
        }

        // The roof flying in (q 0 -> 1 = away -> landed) or off (q 0 -> 1 = on -> away), on an arc, turning.
        private void Roof(Part r, float q, bool coming)
        {
            if (r.t == null) return; // broken in the game (the playroom's ceiling falls in pieces at its fight)
            float k = coming ? EaseOutCubic(q) : EaseInCubic(q);
            float away = coming ? 1f - k : k;
            var arc = Vector3.up * (Mathf.Sin(away * Mathf.PI) * 1.5f);
            r.t.localPosition = r.pos + roofAway * away + arc;
            r.t.localRotation = r.rot * Quaternion.AngleAxis(roofSpin * away, Vector3.up);
            float size = coming ? Mathf.Max(Tiny, Mathf.Clamp01(q * 4f)) : Mathf.Max(Tiny, 1f - Mathf.Clamp01((q - 0.75f) * 4f));
            r.t.localScale = r.scale * size;
        }

        // Shows or hides a set's looks; keepFloors leaves its floor parts as they are (they stay after a go).
        private static void Show(SetState s, bool on, bool keepFloors)
        {
            s.shown = on;
            foreach (var (r, was, floor) in s.renderers)
                if (r != null && !(keepFloors && floor)) r.enabled = on && was;
        }

        private static void Block(SetState s, bool on)
        {
            foreach (var (c, was) in s.colliders) if (c != null) c.enabled = on && was;
            foreach (var (o, was) in s.obstacles) if (o != null) o.enabled = on && was;
        }

        private void Puff(Transform wall)
        {
            if (puff == null || wall == null) return;
            // The middle of the wall's foot.
            var r = wall.GetComponent<Renderer>();
            var at = r != null ? new Vector3(r.bounds.center.x, transform.position.y + 0.1f, r.bounds.center.z) : wall.position;
            var made = Instantiate(puff, at, Quaternion.identity);
            made.transform.localScale *= puffScale;
        }

        private static float EaseOutCubic(float t) { t = Mathf.Clamp01(t); float u = 1f - t; return 1f - u * u * u; }
        private static float EaseInCubic(float t) { t = Mathf.Clamp01(t); return t * t * t; }

        private static float EaseOutBack(float t, float s)
        {
            t = Mathf.Clamp01(t) - 1f;
            return t * t * ((s + 1f) * t + s) + 1f;
        }

        private static float EaseInBack(float t, float s)
        {
            t = Mathf.Clamp01(t);
            return t * t * ((s + 1f) * t - s);
        }
    }
}
