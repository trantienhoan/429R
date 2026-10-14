using Game.Combat;
using Game.Saving;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Player")]
    [Tooltip("The player just died. With Revive Coins on the XR Origin: the revives left this run are lucky coins; " +
             "it flips them and waits. A clover sends Revive (one coin used up, the health the clovers won); all skulls, " +
             "or no coin left, sends Game Over. Without Revive Coins: with a revive left it uses one up and sends " +
             "Revive; otherwise Game Over, and the game over goes on as before.")]
    public class TryRevive : FsmStateAction
    {
        [RequiredField]
        [Tooltip("Sent when the player gets back up (a revive was used up).")]
        public FsmEvent revive;

        [Tooltip("Sent when there's no revive left: the game is over.")]
        public FsmEvent gameOver;

        public override void Reset()
        {
            revive = null;
            gameOver = FsmEvent.Finished;
        }

        private bool flipping;

        public override void OnEnter()
        {
            flipping = false;
            var coins = ReviveCoins.Instance;
            if (PlayerRevive.Instance != null && coins != null && coins.isActiveAndEnabled)
            {
                if (coins.CoinsNow > 0)
                {
                    coins.Flip();
                    // A coin is spent as they're thrown, before anyone can see how they land (quitting during the show
                    // can't dodge the cost).
                    Checkpoints.UseRevive();
                    flipping = true;
                    return;
                }
                Finish();
                Fsm.Event(gameOver);
                return;
            }

            Finish();
            if (PlayerRevive.Instance != null && Checkpoints.UseRevive()) Fsm.Event(revive);
            else Fsm.Event(gameOver);
        }

        // Waits for the coins (they run in real time while the game is stopped).
        public override void OnUpdate()
        {
            if (!flipping) return;
            var coins = ReviveCoins.Instance;
            if (coins != null && coins.IsFlipping) return;

            flipping = false;
            Finish();
            if (coins != null && coins.Clovers > 0 && PlayerRevive.Instance != null)
            {
                PlayerRevive.Instance.SetCoinResult(coins.HealthShare);
                Fsm.Event(revive);
            }
            else
            {
                // Out of luck: the run's revives are gone, so the game over starts a new run.
                Checkpoints.UseAllRevives();
                Fsm.Event(gameOver);
            }
        }

        public override void OnExit()
        {
            // Left some other way while the coins were up: put them away.
            if (flipping && ReviveCoins.Instance != null) ReviveCoins.Instance.Cancel();
            flipping = false;
        }
    }

    [ActionCategory("Player")]
    [Tooltip("Gets the player back up where they fell: Player Revive on the XR Origin plays \"Not yet\", the death fall " +
             "backwards, full health (or what the lucky coins won) and the wake-up aura. Finishes once the player can be " +
             "hurt again.")]
    public class RevivePlayer : FsmStateAction
    {
        public override void OnEnter()
        {
            var reviver = PlayerRevive.Instance;
            if (reviver == null)
            {
                Finish();
                return;
            }
            reviver.Revive();
        }

        public override void OnUpdate()
        {
            var reviver = PlayerRevive.Instance;
            if (reviver == null || !reviver.IsReviving) Finish();
        }
    }
}
