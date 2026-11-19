using UnityEngine;

namespace Game.Rooms
{
    /// <summary>
    /// On an Animator state of a kit room: entering the state plays the Room Kit's come or go for a set, so GAMESTAGES'
    /// Animator Play actions (Bedroom_Mini_COME, Bedroom_GO ...) drive the kit room like they drove the old rigged one.
    /// Made by Tools > 429 Game > Rooms > Build Bedroom Kit Room.
    /// </summary>
    public class RoomKitState : StateMachineBehaviour
    {
        [Tooltip("The Room Kit set this state moves (Mini, Big).")]
        public string set = "Mini";
        [Tooltip("Come (walls up, roof on) or go (roof off, walls down).")]
        public bool come = true;
        [Tooltip("Seconds it takes (the old clips: mini come 2.67, big come 4.0, go 3.33).")]
        [Min(0.05f)]
        public float seconds = 2.67f;
        [Tooltip("Sets taken away at once when this state starts (a checkpoint that starts in the next room: the living " +
                 "room's COME2 hides the living room if it's still there).")]
        public string[] hideAtOnce = System.Array.Empty<string>();

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            var kit = animator.GetComponent<RoomKit>();
            if (kit == null) return;
            foreach (var other in hideAtOnce) kit.Hide(other);
            kit.Play(set, come, seconds);
        }
    }
}
