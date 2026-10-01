using UnityEngine;
using UnityEngine.AI;

namespace Game.Enemies
{
    /// <summary>
    /// Puts a freshly spawned enemy onto the NavMesh. Breakables drop their loot where they were, so a spider can appear
    /// up on a shelf or a Jenga tower, off the NavMesh, where its agent can't be stopped, moved or sent anywhere (the
    /// "can only be called on an active agent that has been placed on a NavMesh" errors, which also pause PlayMaker in
    /// the Editor). Before its FSMs start, this moves it to the nearest NavMesh point (normally the floor below); with
    /// no NavMesh near at all, the enemy is removed. Tools > 429 Game > Enemies > Keep Enemies On NavMesh adds it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    // Before PlayMakerFSM.Start, where the FSMs start and first give the agent orders.
    [DefaultExecutionOrder(-1000)]
    public class LandOnNavMesh : MonoBehaviour
    {
        [Tooltip("How far to look for the NavMesh, in metres.")]
        [Min(0.1f)]
        [SerializeField] private float searchRadius = 10f;

        private void Start()
        {
            var agent = GetComponent<NavMeshAgent>();
            if (!agent.enabled || agent.isOnNavMesh) return;

            var position = transform.position;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (IsFinite(position) && NavMesh.SamplePosition(position, out var hit, searchRadius, filter) && agent.Warp(hit.position))
                return;

            // Switched off first, so its FSMs never start and give orders the agent can't take.
            Debug.LogWarning($"[Enemies] '{name}' appeared at {position} with no NavMesh within {searchRadius} m, so it was removed.", this);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        // A failed NavMesh sample gives an endless position; spawning there leaves the enemy nowhere.
        private static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsInfinity(v.x) &&
                   !float.IsNaN(v.y) && !float.IsInfinity(v.y) &&
                   !float.IsNaN(v.z) && !float.IsInfinity(v.z);
        }
    }
}
