using System;
using System.Collections;
using System.Collections.Generic;
using Game.Enemies;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;
using Random = UnityEngine.Random;

namespace Game.PlayGround
{
    /// <summary>
    /// Lays the PlayGround out anew each time a round's seed is taken from its chest (endgame+ cooking game):
    /// 1. Clean: everything on the field pops away (trees, bushes, the skull, barrels, boxes, pipes, broken pieces, the
    ///    opened chest...: what Clean Names matches, and what this laid out before). The cauldron stays, and so do recipe
    ///    ingredients (candies, Pumpkin_On_Tree), anything the player holds and the magic hat (it's moved, see 4).
    /// 2. The PlayGround's own FSM plays its grow animation meanwhile (PlayGround_Manager, on the seed bools); after Grow
    ///    Seconds the field is measured again from the fence, and the ghost/bug area (Ghost Roam Area) and the teleport
    ///    floor are fitted to it. (This is also done once when the PlayGround has come up, before round 1.)
    /// 3. Scatter: each Kind is put down at random spots inside the fence, Count of them plus More Per Seed for every seed
    ///    after the first, kept clear of the cauldron, the middle (where the pumpkin and the chests land), the player and
    ///    each other. They grow in with a puff. Trees, bushes, the skull and tall grass (Hideout) are where laughing
    ///    pumpkins hide.
    /// 4. The magic hat hides next to one of the things marked Hat Cover.
    /// Goes on the PlayGround's root (Play_Ground_Z). Tools > 429 Game > PlayGround > Set Up PlayGround Field adds it.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayGroundField : MonoBehaviour
    {
        [Serializable]
        public class Kind
        {
            public string label;
            [Tooltip("One of these at random each time.")]
            public GameObject[] prefabs = Array.Empty<GameObject>();
            [Tooltip("How many after the first seed.")]
            [Min(0)] public int count = 2;
            [Tooltip("How many more after each seed after that.")]
            [Min(0)] public int morePerSeed = 1;
            [Tooltip("Size: a random multiplier from x to y.")]
            public Vector2 scale = Vector2.one;
            [Tooltip("Space kept free around it, in metres.")]
            [Min(0f)] public float gap = 0.5f;
            [Tooltip("Bugs walk round it (a carving NavMesh Obstacle the size of its own collider is added).")]
            public bool blocksPaths;
            [Tooltip("The magic hat may hide next to it.")]
            public bool hatCover;
        }

        [Header("Field")]
        [SerializeField] private Kind[] kinds = Array.Empty<Kind>();

        [Header("Clean-up")]
        [Tooltip("What's cleaned off the field when a seed is taken: anything whose name contains one of these.")]
        [SerializeField] private string[] cleanNames =
        {
            "Big_Tree", "bushes_", "Giant_Skull", "Skull_Mini", "Cement_Pipe", "Wood_Barrel", "Wood_Box", "Table_2_small",
            "PG_Bench", "PG_pumpkin_music_player", "Paper_Box", "Treasure_Chest", "Ghost_Balloon", "Dropped_", "_Drop",
            "PG_Grass", "PG_Concrete", "Random_Breakable_Box", "beehive",
        };
        [Tooltip("How far past the fence things still count as on the field, in metres.")]
        [Min(0f)]
        [SerializeField] private float cleanMargin = 1.5f;

        [Header("Arena")]
        [Tooltip("The fence parts (their colliders): the field is the space inside them, measured after each grow.")]
        [SerializeField] private Transform[] fence = Array.Empty<Transform>();
        [Tooltip("How far in from the fence the field starts, in metres.")]
        [Min(0f)]
        [SerializeField] private float fenceInset = 1.2f;
        [Tooltip("Fitted to the field: where ghosts roam, bugs come in, and the pumpkin and chests land.")]
        [SerializeField] private GhostRoamArea roamArea;
        [Tooltip("Fitted to the fence: the floor the player can teleport onto (a cube with a Box Collider).")]
        [SerializeField] private Transform teleportFloor;
        [Tooltip("Seconds from taking the seed to laying out the new field (the grow animation plays meanwhile).")]
        [Min(0f)]
        [SerializeField] private float growSeconds = 4.5f;
        [Tooltip("Seconds after the PlayGround appears before the arena is first fitted to the fence.")]
        [Min(0f)]
        [SerializeField] private float firstFitAfter = 5.5f;

        [Tooltip("Bushes put down here stay bushes (a chopped tree's bush still grows back into its tree).")]
        [SerializeField] private bool bushesStayBushes = true;

        [Header("Kept clear")]
        [Min(0f)] [SerializeField] private float keepFromCauldron = 3f;
        [Min(0f)] [SerializeField] private float keepFromMiddle = 2.2f;
        [Min(0f)] [SerializeField] private float keepFromPlayer = 2.5f;

        [Header("Look and sound")]
        [SerializeField] private GameObject puff;
        [Min(0f)] [SerializeField] private float puffScale = 0.6f;
        [SerializeField] private AudioClip popSound;
        [SerializeField] private AudioClip growSound;
        [Range(0f, 1f)] [SerializeField] private float volume = 0.7f;
        [Min(0.01f)] [SerializeField] private float popSeconds = 0.35f;
        [Min(0.01f)] [SerializeField] private float growInSeconds = 0.6f;
        [Tooltip("Seconds between one thing growing in and the next.")]
        [Min(0f)] [SerializeField] private float growInterval = 0.06f;

        private const string SpawnedStuffVariable = "CurrentlySpawnedStuffs";
        // Kinds measured once: their footprint radius and how far their bottom is below their pivot (at scale 1).
        private readonly Dictionary<GameObject, (float radius, float bottom)> sizes = new();
        private readonly List<GameObject> laidOut = new();
        private Rect field;
        private bool measured;
        private Coroutine running;
        private float lastSound;

        /// <summary>A new field has been laid out (after a seed).</summary>
        public static event Action<PlayGroundField> LaidOut;

        /// <summary>The field's floor plan (world X and Z), inside the fence.</summary>
        public Rect Field => field;

#if UNITY_EDITOR
        // What's put on the PlayGround comes from its own folder.
        private const string PlayGroundFolder = "Assets/Prefabs/PlayGround/";

        private void OnValidate()
        {
            foreach (var kind in kinds)
            {
                if (kind?.prefabs == null) continue;
                foreach (var prefab in kind.prefabs)
                {
                    if (prefab == null) continue;
                    string path = UnityEditor.AssetDatabase.GetAssetPath(prefab);
                    if (!string.IsNullOrEmpty(path) && !path.StartsWith(PlayGroundFolder))
                        Debug.LogWarning($"[PlayGround Field] '{prefab.name}' in '{kind.label}' isn't in {PlayGroundFolder}: PlayGround things come from that folder.", this);
                }
            }
        }
#endif

        private void OnEnable()
        {
            PlayGroundRounds.SeedTaken += OnSeedTaken;
            StartCoroutine(FirstFit());
        }

        private void OnDisable()
        {
            PlayGroundRounds.SeedTaken -= OnSeedTaken;
        }

        private IEnumerator FirstFit()
        {
            yield return new WaitForSeconds(firstFitAfter);
            if (!measured) FitArena();
        }

        private void OnSeedTaken(PlayGroundRounds rounds, int round)
        {
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(Rebuild(round + 1));
        }

        private IEnumerator Rebuild(int seeds)
        {
            if (!measured) FitArena();
            Clean();
            yield return new WaitForSeconds(growSeconds);
            FitArena();
            yield return LayOut(seeds);
            running = null;
            LaidOut?.Invoke(this);
        }

        // ------------------------------------------------------------------ the arena

        /// <summary>Measures the fence and fits the roam area and the teleport floor to it.</summary>
        public void FitArena()
        {
            if (!MeasureFence(out var outer)) return;
            field = Rect.MinMaxRect(outer.xMin + fenceInset, outer.yMin + fenceInset, outer.xMax - fenceInset, outer.yMax - fenceInset);
            measured = true;
            if (roamArea != null) roamArea.FitFlat(new Vector3(field.center.x, transform.position.y, field.center.y), field.size);
            if (teleportFloor != null)
            {
                var parentScale = teleportFloor.parent != null ? teleportFloor.parent.lossyScale : Vector3.one;
                var local = teleportFloor.localScale;
                teleportFloor.localScale = new Vector3(outer.width / Mathf.Max(0.0001f, parentScale.x), local.y,
                    outer.height / Mathf.Max(0.0001f, parentScale.z));
                var at = teleportFloor.position;
                teleportFloor.position = new Vector3(outer.center.x, at.y, outer.center.y);
            }
        }

        private bool MeasureFence(out Rect outer)
        {
            var box = new Bounds();
            bool any = false;
            foreach (var part in fence)
            {
                if (part == null) continue;
                foreach (var wall in part.GetComponents<Collider>())
                {
                    if (!wall.enabled || wall.isTrigger) continue;
                    if (any) box.Encapsulate(wall.bounds);
                    else box = wall.bounds;
                    any = true;
                }
            }
            outer = any ? Rect.MinMaxRect(box.min.x, box.min.z, box.max.x, box.max.z) : default;
            return any && outer.width > fenceInset * 2f + 1f && outer.height > fenceInset * 2f + 1f;
        }

        private bool OnField(Vector3 point, float margin)
        {
            return point.x >= field.xMin - margin && point.x <= field.xMax + margin &&
                   point.z >= field.yMin - margin && point.z <= field.yMax + margin;
        }

        // ------------------------------------------------------------------ 1. clean

        private void Clean()
        {
            var found = new HashSet<GameObject>();
            var spawned = SpawnedStuff();
            if (spawned != null)
            {
                foreach (Transform child in spawned) found.Add(child.gameObject);
            }
            foreach (var tree in FindObjectsByType<GhostTree>(FindObjectsSortMode.None)) found.Add(tree.gameObject);
            foreach (var bush in FindObjectsByType<GhostBush>(FindObjectsSortMode.None)) found.Add(bush.gameObject);
            foreach (var hideout in Hideout.All) if (hideout != null) found.Add(hideout.gameObject);
            foreach (var thing in laidOut) if (thing != null) found.Add(thing);
            laidOut.Clear();

            float margin = fenceInset + cleanMargin;
            foreach (var thing in found)
            {
                if (thing == null || !OnField(thing.transform.position, margin) || Kept(thing) || !Cleanable(thing)) continue;
                StartCoroutine(Pop(thing));
            }
        }

        private bool Cleanable(GameObject thing)
        {
            if (thing.GetComponent<GhostTree>() != null || thing.GetComponent<GhostBush>() != null || thing.GetComponent<Hideout>() != null) return true;
            foreach (var name in cleanNames)
            {
                if (!string.IsNullOrEmpty(name) && thing.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        // Never cleaned: the PlayGround itself, the cauldron, recipe ingredients, the hat, what's held, a pumpkin still running.
        private bool Kept(GameObject thing)
        {
            if (thing == gameObject || thing.transform.IsChildOf(transform) || transform.IsChildOf(thing.transform)) return true;
            if (thing.GetComponentInChildren<Cauldron>(true) != null || thing.GetComponentInParent<Cauldron>(true) != null) return true;
            if (thing.GetComponentInChildren<CookingIngredient>(true) != null) return true;
            if (thing.GetComponentInChildren<MagicHat>(true) != null) return true;
            var pumpkin = thing.GetComponentInChildren<LaughingPumpkin>(true);
            if (pumpkin != null && !pumpkin.IsBroken) return true;
            foreach (var grab in thing.GetComponentsInChildren<XRGrabInteractable>())
            {
                if (grab.isSelected) return true;
            }
            return false;
        }

        // Shrinks away with a puff (nothing bumps into it meanwhile), then it's gone.
        private IEnumerator Pop(GameObject thing)
        {
            foreach (var part in thing.GetComponentsInChildren<Collider>()) part.enabled = false;
            foreach (var body in thing.GetComponentsInChildren<Rigidbody>())
            {
                if (!body.isKinematic) body.linearVelocity = Vector3.zero;
                body.isKinematic = true;
            }
            var at = Middle(thing);
            Puff(at, Size(thing));
            Sound(popSound, at);

            var from = thing.transform.localScale;
            for (float t = 0f; t < popSeconds && thing != null; t += Time.deltaTime)
            {
                thing.transform.localScale = from * (1f - Mathf.SmoothStep(0f, 1f, t / popSeconds));
                yield return null;
            }
            if (thing != null) Destroy(thing);
        }

        // ------------------------------------------------------------------ 3. scatter

        private IEnumerator LayOut(int seeds)
        {
            if (!measured) yield break;
            var parent = SpawnedStuff();
            var taken = new List<(Vector2 at, float radius)>();
            var cauldron = FindAnyObjectByType<Cauldron>();
            if (cauldron != null) taken.Add((Flat(cauldron.transform.position), keepFromCauldron));
            taken.Add((field.center, keepFromMiddle));
            var eyes = Camera.main != null ? Camera.main.transform : null;
            if (eyes != null) taken.Add((Flat(eyes.position), keepFromPlayer));

            var covers = new List<(Vector3 at, float radius)>();
            foreach (var kind in kinds)
            {
                if (kind == null || kind.prefabs == null || kind.prefabs.Length == 0) continue;
                int count = kind.count + kind.morePerSeed * Mathf.Max(0, seeds - 1);
                for (int i = 0; i < count; i++)
                {
                    var prefab = kind.prefabs[Random.Range(0, kind.prefabs.Length)];
                    if (prefab == null) continue;
                    float scale = Random.Range(Mathf.Min(kind.scale.x, kind.scale.y), Mathf.Max(kind.scale.x, kind.scale.y));
                    var (radius, bottom) = SizeOf(prefab);
                    radius *= scale;
                    if (!FindSpot(radius + kind.gap, taken, out var floor)) continue;

                    var made = Instantiate(prefab, floor - Vector3.up * (bottom * scale), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), parent);
                    made.transform.localScale = prefab.transform.localScale * scale;
                    if (kind.blocksPaths) BlockPaths(made);
                    if (bushesStayBushes && made.TryGetComponent<GhostBush>(out var bush)) bush.GrowsBack = false;
                    laidOut.Add(made);
                    taken.Add((Flat(floor), radius + kind.gap));
                    if (kind.hatCover) covers.Add((floor, radius));
                    StartCoroutine(GrowIn(made));
                    if (growInterval > 0f) yield return new WaitForSeconds(growInterval);
                }
            }
            HideHat(covers, taken);
        }

        // A random free spot on the field's floor (on the NavMesh), this far from everything taken.
        private bool FindSpot(float radius, List<(Vector2 at, float radius)> taken, out Vector3 floor)
        {
            floor = default;
            var inside = Rect.MinMaxRect(field.xMin + radius, field.yMin + radius, field.xMax - radius, field.yMax - radius);
            if (inside.width <= 0f || inside.height <= 0f) return false;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                var at = new Vector2(Random.Range(inside.xMin, inside.xMax), Random.Range(inside.yMin, inside.yMax));
                bool free = true;
                foreach (var (other, otherRadius) in taken)
                {
                    if ((at - other).sqrMagnitude < (radius + otherRadius) * (radius + otherRadius)) { free = false; break; }
                }
                if (!free) continue;
                float y = FloorY(new Vector3(at.x, transform.position.y + 8f, at.y));
                if (float.IsNaN(y)) continue;
                floor = new Vector3(at.x, y, at.y);
                return true;
            }
            return false;
        }

        // The floor's height here, or NaN when what's below isn't walkable floor (a prop, a hole).
        private float FloorY(Vector3 above)
        {
            if (!Physics.Raycast(above, Vector3.down, out var hit, 30f, ~0, QueryTriggerInteraction.Ignore)) return float.NaN;
            if (hit.normal.y < 0.8f) return float.NaN;
            if (!NavMesh.SamplePosition(hit.point, out var walk, 0.6f, NavMesh.AllAreas) || Mathf.Abs(walk.position.y - hit.point.y) > 0.3f) return float.NaN;
            return hit.point.y;
        }

        // Footprint radius and bottom (below the pivot) of a prefab at scale 1. The bottom is its look's (meshes, not
        // particles); the footprint is what stands on the floor (its colliders, e.g. a tree's trunk), but at least half
        // its look, so a tree's crown can reach over grass but trees don't crowd each other.
        private (float radius, float bottom) SizeOf(GameObject prefab)
        {
            if (sizes.TryGetValue(prefab, out var known)) return known;
            var root = prefab.transform;
            var toRoot = root.worldToLocalMatrix;
            var look = new Bounds();
            bool anyLook = false;
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.GetComponent<MeshRenderer>() == null) continue;
                Encapsulate(ref look, ref anyLook, toRoot * filter.transform.localToWorldMatrix, filter.sharedMesh.bounds);
            }
            var solid = new Bounds();
            bool anySolid = false;
            foreach (var part in prefab.GetComponentsInChildren<Collider>(true))
            {
                if (part.isTrigger) continue;
                Bounds local;
                switch (part)
                {
                    case BoxCollider box: local = new Bounds(box.center, box.size); break;
                    case SphereCollider ball: local = new Bounds(ball.center, Vector3.one * ball.radius * 2f); break;
                    case CapsuleCollider capsule: local = new Bounds(capsule.center, new Vector3(capsule.radius * 2f, capsule.height, capsule.radius * 2f)); break;
                    case MeshCollider mesh when mesh.sharedMesh != null: local = mesh.sharedMesh.bounds; break;
                    default: continue;
                }
                Encapsulate(ref solid, ref anySolid, toRoot * part.transform.localToWorldMatrix, local);
            }
            var s = root.localScale;
            float lookRadius = anyLook ? Mathf.Max(look.extents.x * s.x, look.extents.z * s.z) : 0.5f;
            float solidRadius = anySolid ? Mathf.Max(solid.extents.x * s.x, solid.extents.z * s.z) : lookRadius;
            var result = (Mathf.Max(solidRadius, lookRadius * 0.5f), anyLook ? look.min.y * s.y : 0f);
            sizes[prefab] = result;
            return result;
        }

        private static void Encapsulate(ref Bounds into, ref bool any, Matrix4x4 toRoot, Bounds local)
        {
            for (int c = 0; c < 8; c++)
            {
                var corner = toRoot.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents,
                    new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)));
                if (any) into.Encapsulate(corner);
                else into = new Bounds(corner, Vector3.zero);
                any = true;
            }
        }

        // A carving NavMesh Obstacle the size of its own collider, so the bugs walk round it.
        private static void BlockPaths(GameObject made)
        {
            if (made.GetComponent<NavMeshObstacle>() != null) return;
            Bounds local;
            if (made.TryGetComponent<BoxCollider>(out var box)) local = new Bounds(box.center, box.size);
            else if (made.TryGetComponent<MeshCollider>(out var mesh) && mesh.sharedMesh != null) local = mesh.sharedMesh.bounds;
            else if (made.TryGetComponent<CapsuleCollider>(out var capsule)) local = new Bounds(capsule.center, new Vector3(capsule.radius * 2f, capsule.height, capsule.radius * 2f));
            else return;
            var obstacle = made.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = local.center;
            obstacle.size = local.size;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
        }

        // Grows from nothing to its size with a puff; it stays put (kinematic) while it grows.
        private IEnumerator GrowIn(GameObject made)
        {
            var bodies = made.GetComponentsInChildren<Rigidbody>();
            var wasKinematic = new bool[bodies.Length];
            for (int i = 0; i < bodies.Length; i++)
            {
                wasKinematic[i] = bodies[i].isKinematic;
                bodies[i].isKinematic = true;
            }
            var to = made.transform.localScale;
            made.transform.localScale = to * 0.05f;
            Puff(made.transform.position + Vector3.up * 0.3f, 1f);
            Sound(growSound, made.transform.position);
            for (float t = 0f; t < growInSeconds && made != null; t += Time.deltaTime)
            {
                float k = t / growInSeconds;
                // A little overshoot, then settle.
                float s = Mathf.SmoothStep(0.05f, 1f, k) + Mathf.Sin(k * Mathf.PI) * 0.08f;
                made.transform.localScale = to * s;
                yield return null;
            }
            if (made == null) yield break;
            made.transform.localScale = to;
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] != null) bodies[i].isKinematic = wasKinematic[i];
            }
        }

        // ------------------------------------------------------------------ 4. the hat

        // The magic hat moves to a spot just beside one of the covers (a tree, a bush, the grass...), on the far side
        // from the middle when it can.
        private void HideHat(List<(Vector3 at, float radius)> covers, List<(Vector2 at, float radius)> taken)
        {
            var hat = FindAnyObjectByType<MagicHat>();
            if (hat == null) return;
            foreach (var grab in hat.GetComponentsInChildren<XRGrabInteractable>())
            {
                if (grab.isSelected) return;
            }

            Vector3 spot = default;
            bool found = false;
            for (int attempt = 0; attempt < 30 && covers.Count > 0 && !found; attempt++)
            {
                var (at, radius) = covers[Random.Range(0, covers.Count)];
                var away = Flat(at) - field.center;
                var dir = away.sqrMagnitude > 0.01f && Random.value < 0.7f ? away.normalized : Random.insideUnitCircle.normalized;
                dir = (Vector2)(Quaternion.Euler(0f, 0f, Random.Range(-50f, 50f)) * dir);
                var flat = Flat(at) + dir * (radius + 0.6f);
                if (!field.Contains(flat)) continue;
                float y = FloorY(new Vector3(flat.x, transform.position.y + 8f, flat.y));
                if (float.IsNaN(y)) continue;
                spot = new Vector3(flat.x, y, flat.y);
                found = true;
            }
            if (!found && !FindSpot(0.6f, taken, out spot)) return;

            Puff(hat.transform.position + Vector3.up * 0.3f, 1f);
            var body = hat.GetComponent<Rigidbody>();
            var turn = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            hat.transform.SetPositionAndRotation(spot + Vector3.up * 0.05f, turn);
            if (body != null)
            {
                body.position = hat.transform.position;
                body.rotation = turn;
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }
            Puff(spot + Vector3.up * 0.3f, 1f);
            Sound(growSound, spot);
        }

        // ------------------------------------------------------------------ helpers

        private static Vector2 Flat(Vector3 point) => new(point.x, point.z);

        private static Vector3 Middle(GameObject thing)
        {
            var shown = thing.GetComponentInChildren<Renderer>();
            return shown != null ? shown.bounds.center : thing.transform.position;
        }

        private static float Size(GameObject thing)
        {
            var shown = thing.GetComponentInChildren<MeshRenderer>();
            return shown != null ? Mathf.Clamp(shown.bounds.size.magnitude * 0.5f, 0.5f, 2.5f) : 1f;
        }

        private void Puff(Vector3 at, float size)
        {
            if (puff != null) EffectPool.Play(puff, at, puffScale * size);
        }

        // Many things pop at once: a sound at most every 60 ms, so it doesn't get loud.
        private void Sound(AudioClip clip, Vector3 at)
        {
            if (clip == null || Time.time - lastSound < 0.06f) return;
            lastSound = Time.time;
            AudioSource.PlayClipAtPoint(clip, at, volume);
        }

        private static Transform SpawnedStuff()
        {
            var variable = FsmVariables.GlobalVariables.FindFsmGameObject(SpawnedStuffVariable);
            return variable != null && variable.Value != null ? variable.Value.transform : null;
        }
    }
}
