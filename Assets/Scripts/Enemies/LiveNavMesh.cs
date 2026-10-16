using System.Collections.Generic;
using Unity.AI.Navigation;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Enemies
{
    /// <summary>
    /// Keeps the enemies' NavMesh matching the base that's there now: the floor (Floor And Walls: the Teleport layer)
    /// with that base's walls (the Wall layer) cut out, so enemies walk round walls, through doorways, instead of
    /// straight through them. Walls only block (Block Only): their tops, like the rooms' ceilings, are never walkable,
    /// so enemies don't end up running on the roof. It rebuilds itself (a few milliseconds, in the background) whenever a base appears, goes,
    /// grows or moves and then holds still: nothing to bake. Loose things (a Rigidbody that isn't kinematic), enemies
    /// themselves and the player are never cut out. Goes on the Ground; while playing it replaces the Ground's NavMesh
    /// Surface (its baked NavMesh has no walls).
    /// </summary>
    [DisallowMultipleComponent]
    public class LiveNavMesh : MonoBehaviour
    {
        [Tooltip("The area kept walkable (world space, metres).")]
        [SerializeField] private Vector3 center = new(0f, 3f, 0f);
        [SerializeField] private Vector3 size = new(40f, 8f, 40f);
        [Tooltip("What's built from: the floors (Teleport) and the walls (Wall).")]
        [SerializeField] private LayerMask floorAndWalls;
        [Tooltip("Of those, the ones that only block (Wall): enemies go round them but never walk on top of them, so the " +
                 "rooms' ceilings and roofs, shelves and wall tops never become somewhere to run.")]
        [SerializeField] private LayerMask blockOnly;
        [Tooltip("Seconds between looks for a change.")]
        [Min(0.1f)]
        [SerializeField] private float lookEvery = 0.5f;

        // The NavMesh's built-in "Not Walkable" area.
        private const int NotWalkable = 1;
        private readonly List<NavMeshBuildSource> sources = new();
        private readonly List<NavMeshBuildMarkup> noMarkups = new();
        private readonly Collider[] found = new Collider[1024];
        private NavMeshBuildSettings settings;
        private NavMeshData data;
        private NavMeshDataInstance instance;
        private NavMeshSurface surface;
        private AsyncOperation building;
        private int builtSignature;
        private int lastSignature;
        private float nextLook;

        /// <summary>How many times it has been built since the scene started (for tests).</summary>
        public int Builds { get; private set; }

        private Bounds Area => new(center, size);

        private void Reset()
        {
            floorAndWalls = LayerMask.GetMask("Teleport", "Wall");
            blockOnly = LayerMask.GetMask("Wall");
        }

        private void Awake()
        {
            surface = GetComponent<NavMeshSurface>();
            // Set up before Block Only existed: walls only block.
            if (blockOnly.value == 0) blockOnly = LayerMask.GetMask("Wall");
            int agentType = surface != null ? surface.agentTypeID : 0;
            settings = NavMesh.GetSettingsByID(agentType);
            // The NavMesh itself sits a few centimetres above the floor (it's built from blocks); the height mesh keeps
            // the real floor height, so enemies stand on the floor instead of hovering over it.
            settings.buildHeightMesh = true;
            data = new NavMeshData(agentType) { name = "Live NavMesh" };
            instance = NavMesh.AddNavMeshData(data);
            // Built now, before anything walks; the flat baked one goes.
            builtSignature = lastSignature = Signature();
            Build(false);
            if (surface != null) surface.enabled = false;
        }

        private void OnDestroy()
        {
            if (instance.valid) instance.Remove();
            if (data != null) Destroy(data);
        }

        private void Update()
        {
            if (building != null && !building.isDone) return;
            if (Time.time < nextLook) return;
            nextLook = Time.time + lookEvery;

            int now = Signature();
            if (now == builtSignature)
            {
                lastSignature = now;
                return;
            }
            // Still changing (a base growing, sinking): wait until it holds still.
            if (now != lastSignature)
            {
                lastSignature = now;
                return;
            }
            builtSignature = now;
            Build(true);
        }

        private void Build(bool inBackground)
        {
            NavMeshBuilder.CollectSources(Area, floorAndWalls, NavMeshCollectGeometry.PhysicsColliders, 0, noMarkups, sources);
            sources.RemoveAll(source => source.component is Collider part && (!Solid(part) || Unreadable(part)));
            // Walls and furniture only block: nothing on top of them is walkable.
            for (int i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                if (source.component == null || (blockOnly.value & (1 << source.component.gameObject.layer)) == 0) continue;
                source.area = NotWalkable;
                sources[i] = source;
            }
            Builds++;
            if (inBackground) building = NavMeshBuilder.UpdateNavMeshDataAsync(data, settings, sources, Area);
            else NavMeshBuilder.UpdateNavMeshData(data, settings, sources, Area);
        }

        // What's there now, as one number: each fixed floor and wall, and where it is (to 10 cm), in any order.
        private int Signature()
        {
            int count = Physics.OverlapBoxNonAlloc(center, size * 0.5f, found, Quaternion.identity, floorAndWalls, QueryTriggerInteraction.Ignore);
            int signature = count;
            for (int i = 0; i < count; i++)
            {
                var part = found[i];
                if (!Solid(part)) continue;
                var box = part.bounds;
                var min = Vector3Int.RoundToInt(box.min * 10f);
                var max = Vector3Int.RoundToInt(box.max * 10f);
                unchecked
                {
                    int one = part.GetInstanceID();
                    one = one * 31 + min.GetHashCode();
                    one = one * 31 + max.GetHashCode();
                    signature += one * 16777619;
                }
            }
            return signature;
        }

        // A mesh collider whose mesh has Read/Write off (its model's import settings) can't be built from in the game
        // (only in the editor): left out, so it doesn't block enemies (e.g. the rooftop's pebbles and lid).
        private static bool Unreadable(Collider part) =>
            part is MeshCollider mesh && (mesh.sharedMesh == null || !mesh.sharedMesh.isReadable);

        // Part of the level: not a trigger, not loose, not an enemy, not the player.
        private static bool Solid(Collider part)
        {
            if (part == null || !part.enabled || part.isTrigger) return false;
            var body = part.attachedRigidbody;
            if (body != null && !body.isKinematic) return false;
            if (part.GetComponentInParent<NavMeshAgent>() != null) return false;
            return part.GetComponentInParent<XROrigin>() == null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireCube(center, size);
        }
#endif
    }
}
