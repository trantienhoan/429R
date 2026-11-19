using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.Rooms
{
    /// <summary>
    /// The purple hole that eats the room (user, 2026-11-19: "the purple hole that sucks in everything to swap for the
    /// current fairy shield ... Keep candies out, suck in everything else"). Wherever the Fairy Shield used to sweep (the
    /// stage clean-ups, the door shortcut, the player's death), a purple hole opens in the sky above the room: everything
    /// the stage left lying about shakes, then spirals up into it, shrinking, and is gone; the hole closes with a gulp.
    /// Eaten: what the stage made that is loose or breakable (furniture, props, pieces, weapons lying about, bugs,
    /// pumpkins: things in CurrentlySpawnedStuffs, or made loose at the scene's top) within Reach. Kept: candy (it stays
    /// for the player), the player and what they hold, the Fairy, the Knight and bosses, the rooms and stages themselves
    /// (by name, and anything bigger than Biggest), and what's built into the scene. The old clean-up's smoke blasts are
    /// switched off while it eats (Hide While Eating), so the show can be seen.
    /// What flies is a copy of the thing's looks (the thing itself goes at once), so nothing breaks, drops or explodes on
    /// the way. The shield still sweeps, unseen, once the hole is done, so what the hole kept gets its usual treatment.
    /// Put on the Fairy Shield (Shield_Sphere) by Tools > 429 Game > Effects > Purple Hole, which also makes Hole Prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public class PurpleHole : MonoBehaviour
    {
        private const string Holder = "CurrentlySpawnedStuffs";

        [Header("The hole")]
        [Tooltip("The hole's looks (Purple_Hole.prefab): made where the hole opens, removed when it has closed.")]
        [SerializeField] private GameObject holePrefab;
        [Tooltip("Where it opens: this far straight above the shield's centre (m).")]
        [SerializeField] private float height = 5.5f;
        [Tooltip("Seconds to open and to close.")]
        [Min(0.05f)]
        [SerializeField] private float openSeconds = 0.5f;
        [Min(0.05f)]
        [SerializeField] private float closeSeconds = 0.45f;
        [Tooltip("Things this far (flat, m) from the shield's centre are eaten.")]
        [Min(1f)]
        [SerializeField] private float reach = 14f;

        [Header("Eating")]
        [Tooltip("Seconds over which things start to go, nearest first.")]
        [Min(0f)]
        [SerializeField] private float startSpread = 1.1f;
        [Tooltip("Seconds each thing shakes before it lifts off.")]
        [SerializeField] private Vector2 shakeSeconds = new Vector2(0.35f, 0.6f);
        [Tooltip("Seconds each thing takes to spiral up into the hole.")]
        [SerializeField] private Vector2 flySeconds = new Vector2(0.9f, 1.3f);
        [Tooltip("Turns around the hole on the way up.")]
        [SerializeField] private float spiralTurns = 1.25f;
        [Tooltip("How hard things shake (m).")]
        [SerializeField] private float shake = 0.05f;
        [Tooltip("Most things copied and set off in one frame.")]
        [Min(1)]
        [SerializeField] private int perFrame = 6;
        [Tooltip("Names (their start) never eaten, besides candy, the player and what they hold. Bosses: when the " +
                 "player dies in a boss fight, the boss gets the old shield treatment, not the hole.")]
        [SerializeField] private string[] keepNames = { "Fairy", "Knight" };
        [Tooltip("Names (anywhere in them) never eaten: bosses, and the rooms and stages themselves.")]
        [SerializeField] private string[] keepNamesContaining = { "Boss", "_Base", "Skydome", "Valley", "Island" };
        [Tooltip("Bigger than this (m, any side) is never eaten: rooms, valleys, islands, the stage's own parts.")]
        [Min(1f)]
        [SerializeField] private float biggest = 6f;
        [Tooltip("Switched off while the hole eats (the old clean-up's smoke blasts, which hide the show).")]
        [SerializeField] private GameObject[] hideWhileEating = System.Array.Empty<GameObject>();

        [Header("Sounds (the user's own clips)")]
        [SerializeField] private AudioClip openSound;
        [Range(0f, 1f)]
        [SerializeField] private float openVolume = 0.8f;
        [SerializeField] private AudioClip closeSound;
        [Range(0f, 1f)]
        [SerializeField] private float closeVolume = 0.9f;

        [Header("The shield")]
        [Tooltip("Seconds after the hole opens that the (unseen) shield starts touching things again.")]
        [Min(0f)]
        [SerializeField] private float shieldAfter = 2.6f;

        private Renderer shieldLook;
        private Collider shieldTouch;

        private void Awake()
        {
            shieldLook = GetComponent<Renderer>();
            shieldTouch = GetComponent<Collider>();
        }

        private void OnEnable()
        {
            if (shieldLook != null) shieldLook.enabled = false;
            if (shieldTouch != null) shieldTouch.enabled = false;
            // The hole runs on its own object: the shield switches itself off after its 3 s sweep.
            var centre = transform.position;
            var runner = new GameObject("Purple_Hole (eating)");
            runner.transform.position = centre + Vector3.up * height;
            runner.AddComponent<Eater>().Run(this, centre);
            StartCoroutine(ShieldBack());
            StartCoroutine(HideSmokes());
        }

        // The clean-up switches its smoke blasts on in the same breath as the shield: off again a frame later.
        private IEnumerator HideSmokes()
        {
            yield return null;
            foreach (var smoke in hideWhileEating)
            {
                if (smoke != null && smoke.activeSelf) smoke.SetActive(false);
            }
        }

        private void OnDisable()
        {
            if (shieldTouch != null) shieldTouch.enabled = true;
        }

        private IEnumerator ShieldBack()
        {
            yield return new WaitForSeconds(shieldAfter);
            if (shieldTouch != null) shieldTouch.enabled = true;
        }

        /// <summary>Whether the hole eats this stage-made thing: something loose or breakable, not candy, not the
        /// player's or in their hand, not a keeper, not a big structure.</summary>
        public bool Eats(GameObject thing)
        {
            for (var t = thing.transform; t != null; t = t.parent)
            {
                string n = t.name;
                if (IsCandy(n) || n.StartsWith("XR Origin")) return false;
                foreach (var keep in keepNames)
                {
                    if (!string.IsNullOrEmpty(keep) && n.StartsWith(keep)) return false;
                }
                foreach (var keep in keepNamesContaining)
                {
                    if (!string.IsNullOrEmpty(keep) && n.Contains(keep)) return false;
                }
            }
            var grab = thing.GetComponentInChildren<XRGrabInteractable>();
            if (grab != null && grab.isSelected) return false;

            // Loose (a moving body), breakable (takes hits) or a bug: furniture, props, pieces, weapons, enemies.
            bool loose = thing.CompareTag("Enemy") || thing.GetComponentInChildren<Game.Combat.EnemyHitDamage>() != null;
            if (!loose)
            {
                foreach (var body in thing.GetComponentsInChildren<Rigidbody>())
                {
                    if (!body.isKinematic) { loose = true; break; }
                }
            }
            if (!loose) return false;

            // Never a room, a valley or an island.
            bool any = false;
            var box = new Bounds();
            foreach (var r in thing.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer) continue;
                if (!any) { box = r.bounds; any = true; }
                else box.Encapsulate(r.bounds);
            }
            if (!any) return false;
            return box.size.x <= biggest && box.size.y <= biggest && box.size.z <= biggest;
        }

        // A candy or a piece of one: "Candy_<number>..." or its model "Candy_Drop...", never the Candy Box or a basket.
        private static bool IsCandy(string name) =>
            name.StartsWith("Candy_") && name.Length > 6 && (char.IsDigit(name[6]) || name.StartsWith("Candy_Drop"));

        // What the stage made that this body belongs to: the child of the stage's holder, or a loose copy at the scene's
        // top. Null for what's built into the scene (rooms, set pieces, the player).
        private static GameObject StageThing(Transform t)
        {
            while (t.parent != null)
            {
                if (t.parent.name.StartsWith(Holder)) return t.gameObject;
                t = t.parent;
            }
            return t.name.EndsWith("(Clone)") ? t.gameObject : null;
        }

        /// <summary>Runs one hole: open, eat, close, then removes itself.</summary>
        private class Eater : MonoBehaviour
        {
            private PurpleHole cfg;
            private Vector3 centre;
            private Transform look;
            private Vector3 lookScale = Vector3.one;
            private float pulse;
            private readonly List<Flight> flights = new();

            private class Flight
            {
                public Transform ghost;
                public Vector3 from, fromScale, spin;
                public Quaternion fromRot;
                public float startAt, liftAt, endAt, angle0, radius0;
                public bool done;
            }

            public void Run(PurpleHole config, Vector3 roomCentre)
            {
                cfg = config;
                centre = roomCentre;
                if (cfg.holePrefab != null)
                {
                    look = Instantiate(cfg.holePrefab, transform.position, Quaternion.identity, transform).transform;
                    lookScale = look.localScale;
                    look.localScale = lookScale * 0.001f;
                }
                if (cfg.openSound != null) AudioSource.PlayClipAtPoint(cfg.openSound, transform.position, cfg.openVolume);
                StartCoroutine(Go());
            }

            private IEnumerator Go()
            {
                // What goes: what the stage left within reach, nearest first.
                var things = new List<GameObject>();
                var seen = new HashSet<GameObject>();
                foreach (var body in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
                {
                    if (body == null || !body.gameObject.activeInHierarchy) continue;
                    var thing = StageThing(body.transform);
                    if (thing == null || !seen.Add(thing)) continue;
                    var flat = thing.transform.position - centre;
                    flat.y = 0f;
                    if (flat.magnitude > cfg.reach || !cfg.Eats(thing)) continue;
                    things.Add(thing);
                }
                things.Sort((a, b) => Flat(a).CompareTo(Flat(b)));

                // Open, while their looks are copied a few a frame (each thing itself goes as it's copied).
                float openStart = Time.time;
                float eatFrom = openStart + cfg.openSeconds;
                for (int i = 0; i < things.Count; i++)
                {
                    var thing = things[i];
                    if (thing == null) continue;
                    float startAt = eatFrom + (things.Count > 1 ? cfg.startSpread * i / (things.Count - 1) : 0f);
                    var ghost = Ghost(thing);
                    if (ghost != null)
                    {
                        float lift = startAt + Random.Range(cfg.shakeSeconds.x, cfg.shakeSeconds.y);
                        var rel = ghost.position - transform.position;
                        flights.Add(new Flight
                        {
                            ghost = ghost,
                            from = ghost.position,
                            fromScale = ghost.localScale,
                            fromRot = ghost.rotation,
                            spin = Random.onUnitSphere * Random.Range(180f, 540f),
                            startAt = startAt,
                            liftAt = lift,
                            endAt = lift + Random.Range(cfg.flySeconds.x, cfg.flySeconds.y),
                            angle0 = Mathf.Atan2(rel.z, rel.x),
                            radius0 = new Vector2(rel.x, rel.z).magnitude,
                        });
                    }
                    Destroy(thing);
                    if ((i + 1) % cfg.perFrame == 0)
                    {
                        Tick(openStart);
                        yield return null;
                    }
                }

                // Eat.
                while (Time.time < eatFrom || flights.Exists(f => !f.done))
                {
                    Tick(openStart);
                    yield return null;
                }

                // Close with a gulp.
                if (cfg.closeSound != null) AudioSource.PlayClipAtPoint(cfg.closeSound, transform.position, cfg.closeVolume);
                for (float t = 0f; t < cfg.closeSeconds; t += Time.deltaTime)
                {
                    if (look != null) look.localScale = lookScale * Mathf.Max(0.001f, 1f - Ease(t / cfg.closeSeconds));
                    yield return null;
                }
                Destroy(gameObject);
            }

            private void Tick(float openStart)
            {
                float now = Time.time;
                var hole = transform.position;
                foreach (var f in flights)
                {
                    if (f.done) continue;
                    if (f.ghost == null) { f.done = true; continue; }
                    if (now < f.startAt) continue;
                    if (now < f.liftAt)
                    {
                        // Shake where it stands, harder towards lift-off.
                        float k = Mathf.InverseLerp(f.startAt, f.liftAt, now);
                        f.ghost.position = f.from + Random.insideUnitSphere * (cfg.shake * (0.4f + k));
                        f.ghost.rotation = f.fromRot * Quaternion.Euler(Random.Range(-4f, 4f) * k, 0f, Random.Range(-4f, 4f) * k);
                        continue;
                    }
                    // Spiral in: round the hole, closer and higher, smaller and tumbling.
                    float p = Mathf.Clamp01(Mathf.InverseLerp(f.liftAt, f.endAt, now));
                    float e = p * p * (3f - 2f * p);
                    float angle = f.angle0 + p * cfg.spiralTurns * Mathf.PI * 2f;
                    float radius = f.radius0 * Mathf.Pow(1f - e, 1.5f);
                    float y = Mathf.Lerp(f.from.y, hole.y, e * e);
                    f.ghost.position = new Vector3(hole.x + Mathf.Cos(angle) * radius, y, hole.z + Mathf.Sin(angle) * radius);
                    f.ghost.rotation = f.fromRot * Quaternion.Euler(f.spin * p);
                    f.ghost.localScale = f.fromScale * Mathf.Max(0.001f, 1f - p * p);
                    if (p >= 1f)
                    {
                        f.done = true;
                        Destroy(f.ghost.gameObject);
                        pulse = 1f;
                    }
                }
                // Open, then bulge a little with each gulp.
                pulse = Mathf.MoveTowards(pulse, 0f, Time.deltaTime * 4f);
                float open = Ease((now - openStart) / cfg.openSeconds);
                if (look != null) look.localScale = lookScale * Mathf.Max(0.001f, open * (1f + 0.12f * pulse));
            }

            private float Flat(GameObject go)
            {
                if (go == null) return float.MaxValue;
                var d = go.transform.position - centre;
                d.y = 0f;
                return d.sqrMagnitude;
            }

            private static float Ease(float t)
            {
                t = Mathf.Clamp01(t);
                return 1f - (1f - t) * (1f - t);
            }

            // A copy of a thing's looks: its mesh renderers (skinned ones baked as they stand), no colliders, no scripts.
            private static Transform Ghost(GameObject thing)
            {
                var ghost = new GameObject(thing.name + " (into the hole)").transform;
                ghost.SetPositionAndRotation(thing.transform.position, thing.transform.rotation);
                int parts = 0;
                foreach (var r in thing.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                    Mesh mesh = null;
                    bool baked = false;
                    if (r is SkinnedMeshRenderer skin)
                    {
                        mesh = new Mesh();
                        skin.BakeMesh(mesh, true);
                        baked = true;
                    }
                    else if (r is MeshRenderer && r.TryGetComponent(out MeshFilter filter))
                    {
                        mesh = filter.sharedMesh;
                    }
                    if (mesh == null) continue;
                    var part = new GameObject(r.name);
                    part.transform.SetParent(ghost, false);
                    part.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
                    part.transform.localScale = baked ? Vector3.one : r.transform.lossyScale;
                    part.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var copy = part.AddComponent<MeshRenderer>();
                    copy.sharedMaterials = r.sharedMaterials;
                    copy.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    if (baked) part.AddComponent<DropBakedMesh>().mesh = mesh;
                    parts++;
                }
                if (parts > 0) return ghost;
                Destroy(ghost.gameObject);
                return null;
            }
        }

        // A baked copy's mesh goes with it (it was made for the flight).
        private class DropBakedMesh : MonoBehaviour
        {
            public Mesh mesh;
            private void OnDestroy()
            {
                if (mesh != null) Destroy(mesh);
            }
        }
    }
}
