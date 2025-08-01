namespace Enemies
{
    public class HeldState : IState
    {
        private ShadowMonster monster;

        public HeldState(ShadowMonster monster)
        {
            this.monster = monster;
        }

        public void OnEnter()
        {
            monster.animator.SetBool("isRunning", false);
            monster.animator.SetBool("isCharging", false);
            monster.animator.SetBool("isGrounded", false);
        }

        public void Tick()
        {
            if (!monster.isBeingHeld && monster.isGrounded)
            {
                monster.stateMachine.ChangeState(new IdleState(monster));
            }
        }

        public void OnExit()
        {
            monster.animator.SetBool("isGrounded", monster.isGrounded);
        }
    }
}