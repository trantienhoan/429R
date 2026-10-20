using UnityEngine;
using FsmFloat = HutongGames.PlayMaker.FsmFloat;

namespace Game.Leveling
{
    /// <summary>
    /// Gives the player EXP when this breakable breaks: when it's gone and its Health FSM's "health" had reached 0 (a hit,
    /// a throw, a blast, ...). Only what the player earns counts: rotting away, a pop (the Fairy Shield) or going into the
    /// cauldron don't (the health is still up then), nor do the giant's hands, a Fall Edge or Live Limit breaking the
    /// oldest one to make room (they set No Reward). Parts of a point add up (see Player Level), e.g. 0.25 on
    /// Pumpkin_On_Tree: four of them make 1 EXP.
    /// </summary>
    [DisallowMultipleComponent]
    public class ExpWhenBroken : MonoBehaviour
    {
        [Tooltip("EXP the player gets when it breaks. 0.25 = a quarter: four of them make 1 EXP.")]
        [Min(0f)]
        [SerializeField] private float exp = 0.25f;

        [Header("PlayMaker")]
        [Tooltip("The FSM that breaks it when its float \"health\" reaches 0.")]
        [SerializeField] private string healthFsm = "Health";
        [SerializeField] private string healthVariable = "health";

        /// <summary>
        /// Set while the game runs (not saved): it breaks without the player, so no EXP, e.g. Live Limit breaking the
        /// oldest tree pumpkin to make room for a new one, the giant's hands, a Fall Edge. See PlayerLevel.NoRewardFor.
        /// </summary>
        [System.NonSerialized] public bool noReward;

        private FsmFloat health;
        private static bool quitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            quitting = false;
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        private static void OnQuitting() => quitting = true;

        private void Start()
        {
            foreach (var fsm in GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != healthFsm) continue;
                health = fsm.FsmVariables.FindFsmFloat(healthVariable);
                break;
            }
        }

        // Its FSM breaks it and destroys it in one go, so this looks at the health as it goes.
        private void OnDestroy()
        {
            // Gone with the scene (a reload, Re-Dream) or the game closing: nothing broke.
            if (quitting || noReward || health == null || exp <= 0f || !gameObject.scene.isLoaded) return;
            if (health.Value <= 0f) PlayerLevel.AddExp(exp);
        }
    }
}
