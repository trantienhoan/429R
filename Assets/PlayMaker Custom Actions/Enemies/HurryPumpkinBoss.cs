using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Enemies")]
    [Tooltip("Ends a pumpkin's countdown at once, so it turns into its boss now: every pumpkin with the Pumpkin Tag whose " +
             "Countdown FSM is in the Countdown State gets FINISHED (Pumpkin_1's Wait and Transform: Wait 360s, then " +
             "Create BOSS = pumpkin_boss_Z where it stands). If none is counting down yet (the door was broken before the " +
             "room's furniture and pumpkin came), the skull is saved: the pumpkin turns as soon as its countdown starts, " +
             "while this room (the FSM's root object) is still there.")]
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
            if (Game.Enemies.SavedPumpkinHurry.HurryNow(pumpkinTag.Value, countdownFsm.Value, countdownState.Value) == 0)
            {
                // Saved for later, on the room (the door itself goes away when it breaks).
                var room = Owner != null ? Owner.transform.root.gameObject : null;
                if (room != null)
                    room.AddComponent<Game.Enemies.SavedPumpkinHurry>().Set(pumpkinTag.Value, countdownFsm.Value, countdownState.Value);
            }
            Finish();
        }
    }
}
