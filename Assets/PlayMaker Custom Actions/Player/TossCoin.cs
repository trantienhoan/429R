using Game.Combat;
using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Player")]
    [Tooltip("Tosses the lucky coin (Revive Coins on the XR Origin: the same coin, table and sounds as getting back up, " +
             "the game stops meanwhile) and waits for it to land: four-leaf clover sends Clover, skull sends Skull. " +
             "Without Revive Coins it's decided at once, half and half.")]
    public class TossCoin : FsmStateAction
    {
        [RequiredField]
        [Tooltip("Sent when the coin lands on the four-leaf clover.")]
        public FsmEvent clover;

        [RequiredField]
        [Tooltip("Sent when it lands on the skull.")]
        public FsmEvent skull;

        private bool tossing;

        public override void Reset()
        {
            clover = null;
            skull = null;
        }

        public override void OnEnter()
        {
            var coins = ReviveCoins.Instance;
            tossing = coins != null && coins.Toss(1);
            if (tossing) return;

            Finish();
            Fsm.Event(Random.value < 0.5f ? clover : skull);
        }

        // Waits for the coin (it runs in real time while the game is stopped).
        public override void OnUpdate()
        {
            var coins = ReviveCoins.Instance;
            if (coins != null && coins.IsFlipping) return;

            tossing = false;
            Finish();
            Fsm.Event(coins != null && coins.Clovers > 0 ? clover : skull);
        }

        public override void OnExit()
        {
            // Left some other way while the coin was up: put it away.
            if (tossing && ReviveCoins.Instance != null) ReviveCoins.Instance.Cancel();
            tossing = false;
        }
    }
}
