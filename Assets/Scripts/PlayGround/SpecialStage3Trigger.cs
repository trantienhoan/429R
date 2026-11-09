using Game.Bonus;
using UnityEngine;

namespace Game.PlayGround
{
    /// <summary>
    /// Earns Special Stage Type 3, the Daruma game (user, 2026-11-07): when this cauldron's mystery pot breaks (the
    /// finale after the 3rd round), the stage (the Giant Slime's bridge, Daruma Stage) is made far away at once and takes
    /// the player there a moment later, once the smoke billows. The pot drops no seed: the stage hands the Green Seed
    /// (The_Seed) to the player after a win. Goes on the cauldron (Fire_Woods_n_Cauldron); Tools > 429 Game > Bonus >
    /// Hook Special Stage 3 To Cauldron sets it up.
    /// </summary>
    public class SpecialStage3Trigger : MonoBehaviour
    {
        [Tooltip("The stage prefab (Prefabs/Bonus/Special_Stage_3).")]
        [SerializeField] private DarumaStage stage;
        [Tooltip("The seed won there (Items/Chest/The_Seed): eating it ends the PlayGround (GAMESTAGES The_Seed).")]
        [SerializeField] private GameObject seed;
        [Tooltip("Where the stage is made (far from everything else).")]
        [SerializeField] private Vector3 stageAt = new(0f, 0f, 400f);
        [Tooltip("Seconds after the pot breaks before the player is taken there (the smoke first).")]
        [Min(0f)]
        [SerializeField] private float delay = 1.2f;

        private void OnEnable() => Cauldron.PotBroken += OnPotBroken;
        private void OnDisable() => Cauldron.PotBroken -= OnPotBroken;

        // Only this cauldron's pot. The stage is made in the same frame (Daruma Stage's Running is set at once), and it
        // lives under Currently Spawned Stuffs: the cauldron is gone 3 s after the break.
        private void OnPotBroken(Cauldron cauldron)
        {
            if (cauldron == null || cauldron.gameObject != gameObject) return;
            if (StartStage(stage, stageAt, seed, delay) != null) return;
            // No stage (not set up): the seed is left where the pot stood, as before, so the game can go on.
            Debug.LogWarning("[Special Stage 3] No stage could be made" + (stage == null ? " (Stage is empty)" : "") +
                             ": the seed is left at the pot instead.", this);
            if (seed != null && DarumaStage.Running == null)
                Instantiate(seed, transform.position + Vector3.up * 0.5f, Quaternion.identity, SpawnedStuff());
        }

        /// <summary>
        /// Makes the stage at a spot and takes the player there after Start Delay seconds (also for Play tests). The seed
        /// is what a win hands over. Null if there's no prefab or a stage is already running.
        /// </summary>
        public static DarumaStage StartStage(DarumaStage prefab, Vector3 at, GameObject seed = null, float startDelay = 0f)
        {
            if (prefab == null || DarumaStage.Running != null) return null;
            var made = Instantiate(prefab, at, Quaternion.identity, SpawnedStuff());
            made.SeedPrefab = seed;
            made.Begin(startDelay);
            return made;
        }

        // The stages' holder (PlayMaker global CurrentlySpawnedStuffs): a Game Over clears it.
        private static Transform SpawnedStuff()
        {
            var holder = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmGameObject("CurrentlySpawnedStuffs");
            return holder != null && holder.Value != null ? holder.Value.transform : null;
        }
    }
}
