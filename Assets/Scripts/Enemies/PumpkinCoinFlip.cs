using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// On the good chase pumpkins (Pumpkin_3, 5, 7): each time one is made (GAMESTAGES, the living room's spawn
    /// points...), Bad Chance (0.5) of the time it swaps itself for its bad twin (Bad_Pumpkin_3, 5, 7: one health, no
    /// candies, goes off near the player and lets spiders out; user 2026-11-26: "50 50 chance to spawn pumpkin 3 5 7 or
    /// Bad_pumpkin_3 5 7", so the healing candies aren't free every time). The twin appears in the same place, under
    /// the same parent (CurrentlySpawnedStuffs) and does the same job for the game. It runs before the pumpkin's own
    /// FSMs start, so the good one does nothing at all when it is swapped. Tools > 429 Game > Enemies > Set Up Bad
    /// Pumpkins puts it on.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public class PumpkinCoinFlip : MonoBehaviour
    {
        [Tooltip("Its bad twin (Bad_Pumpkin_3, 5 or 7).")]
        [SerializeField] private GameObject badVersion;
        [Tooltip("The chance it comes as the bad twin: 0.5 = half the time, 0 = never, 1 = always.")]
        [Range(0f, 1f)]
        [SerializeField] private float badChance = 0.5f;

        /// <summary>Editor test runs only: a chance used instead of Bad Chance (below 0 = use Bad Chance).</summary>
        public static float TestChance = -1f;

        // Start, not Awake: PlayMaker's Create Object sets the parent only after the pumpkin is made, and the twin must
        // land under the same parent (CurrentlySpawnedStuffs) so the stage clean-up removes it.
        private void Start()
        {
            float chance = TestChance >= 0f ? TestChance : badChance;
            if (badVersion == null || Random.value >= chance) return;
            gameObject.SetActive(false);
            var bad = Instantiate(badVersion, transform.position, transform.rotation, transform.parent);
            bad.name = badVersion.name;
            Destroy(gameObject);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => TestChance = -1f;
    }
}
