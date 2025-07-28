namespace Enemies
{
    public class DeadState : IState
    {
        private ShadowMonster monster;

        public DeadState(ShadowMonster monster)
        {
            this.monster = monster;
        }

        public void OnEnter()
        {
            if (monster.agent != null && monster.agent.isActiveAndEnabled)
            {
                monster.agent.enabled = false;
            }
            monster.rb.isKinematic = true;
            monster.grabInteractable.enabled = false;
            monster.PlayDeathEffects();
        }

        public void Tick() { }
        public void OnExit() { }
    }
}