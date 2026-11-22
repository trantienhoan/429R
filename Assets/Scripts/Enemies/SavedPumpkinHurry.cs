using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// A skull from a broken door saved for later (user, 2026-11-20): the door was broken before the room's runner
    /// pumpkin came, so nothing was counting down. Added by Hurry Pumpkin Boss to the room; as soon as a pumpkin with the
    /// tag starts its countdown, it turns into its boss, then this goes. Gone with the room (a new run starts clean).
    /// </summary>
    public class SavedPumpkinHurry : MonoBehaviour
    {
        private string tagName, fsmName, stateName;
        private float next;

        public void Set(string pumpkinTag, string countdownFsm, string countdownState)
        {
            tagName = pumpkinTag;
            fsmName = countdownFsm;
            stateName = countdownState;
        }

        private void Update()
        {
            if (Time.time < next || string.IsNullOrEmpty(tagName)) return;
            next = Time.time + 0.5f;
            if (HurryNow(tagName, fsmName, stateName) > 0) Destroy(this);
        }

        /// <summary>FINISHED to every pumpkin with the tag that is counting down; how many there were.</summary>
        public static int HurryNow(string pumpkinTag, string countdownFsm, string countdownState)
        {
            GameObject[] pumpkins;
            try { pumpkins = GameObject.FindGameObjectsWithTag(pumpkinTag); }
            catch (UnityException) { return 0; } // the tag doesn't exist
            int hurried = 0;
            foreach (var pumpkin in pumpkins)
            {
                foreach (var fsm in pumpkin.GetComponents<PlayMakerFSM>())
                {
                    if (fsm.FsmName != countdownFsm || !fsm.enabled || fsm.ActiveStateName != countdownState) continue;
                    fsm.Fsm.Event(HutongGames.PlayMaker.FsmEvent.Finished);
                    hurried++;
                }
            }
            return hurried;
        }
    }
}
