using System.Collections;
using Game.Bonus;
using UnityEngine;

namespace Game.PlayGround
{
    /// <summary>
    /// Earns Special Stage Type 3 (user, 2026-11-06): when the PlayGround's 2nd laughing pumpkin is caught with its whole
    /// wave beaten first (every bug and boss sent, none alive: Play Ground Rounds' Last Wave Cleared), the stage (the
    /// Giant Slime's bridge, Darume Stage) is made far away and the player is taken there a moment later. The round's
    /// chest waits in the PlayGround for when they come back. Goes on the cauldron (Fire_Woods_n_Cauldron).
    /// </summary>
    public class SpecialStage3Trigger : MonoBehaviour
    {
        [Tooltip("The stage prefab (Prefabs/Bonus/Special_Stage_3).")]
        [SerializeField] private DarumaStage stage;
        [Tooltip("Which round's pumpkin earns it (0 = the 1st: 1 = the 2nd pumpkin).")]
        [Min(0)]
        [SerializeField] private int round = 1;
        [Tooltip("Where the stage is made (far from everything else).")]
        [SerializeField] private Vector3 stageAt = new(0f, 0f, 400f);
        [Tooltip("Seconds after the pumpkin is caught before the player is taken there.")]
        [Min(0f)]
        [SerializeField] private float delay = 3f;
        [Tooltip("Earned even with bugs left (for testing).")]
        [SerializeField] private bool always;

        private void OnEnable() => PlayGroundRounds.RoundWon += OnRoundWon;
        private void OnDisable() => PlayGroundRounds.RoundWon -= OnRoundWon;

        private void OnRoundWon(PlayGroundRounds rounds, int won)
        {
            if (won != round || stage == null) return;
            if (!always && (rounds == null || !rounds.LastWaveCleared)) return;
            StartCoroutine(Go());
        }

        private IEnumerator Go()
        {
            yield return new WaitForSeconds(delay);
            StartStage(stage, stageAt);
        }

        /// <summary>Makes the stage at a spot and takes the player there (also for Play tests).</summary>
        public static DarumaStage StartStage(DarumaStage prefab, Vector3 at)
        {
            if (prefab == null || DarumaStage.Running != null) return null;
            var holder = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmGameObject("CurrentlySpawnedStuffs");
            var parent = holder != null && holder.Value != null ? holder.Value.transform : null;
            var made = Instantiate(prefab, at, Quaternion.identity, parent);
            made.Begin();
            return made;
        }
    }
}
