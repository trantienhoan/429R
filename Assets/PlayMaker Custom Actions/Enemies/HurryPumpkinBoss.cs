using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Enemies")]
    [Tooltip("Ends a pumpkin's countdown at once, so it turns into its boss now: every pumpkin with the Pumpkin Tag whose " +
             "Countdown FSM is in the Countdown State gets FINISHED (Pumpkin_1's Wait and Transform: Wait 360s, then " +
             "Create BOSS = pumpkin_boss_Z where it stands). Nothing happens if none is counting down.")]
    public class HurryPumpkinBoss : FsmStateAction
    {
        [Tooltip("The tag of the pumpkin that turns into the boss.")]
        public FsmString pumpkinTag;

        [Tooltip("The FSM on it that counts down to the boss, and the state that waits.")]
        public FsmString countdownFsm;
        public FsmString countdownState;

        public override void Reset()
        {
            pumpkinTag = "Pumpkin_1";
            countdownFsm = "Wait and Transform";
            countdownState = "Wait 360s";
        }

        public override void OnEnter()
        {
            GameObject[] pumpkins;
            try
            {
                pumpkins = GameObject.FindGameObjectsWithTag(pumpkinTag.Value);
            }
            catch (UnityException)
            {
                // The tag doesn't exist.
                pumpkins = new GameObject[0];
            }
            foreach (var pumpkin in pumpkins)
            {
                foreach (var fsm in pumpkin.GetComponents<PlayMakerFSM>())
                {
                    if (fsm.FsmName == countdownFsm.Value && fsm.enabled && fsm.ActiveStateName == countdownState.Value)
                        fsm.Fsm.Event(FsmEvent.Finished);
                }
            }
            Finish();
        }
    }
}
