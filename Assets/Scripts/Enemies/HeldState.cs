using Enemies;

public class HeldState : IState
{
    private readonly ShadowMonster monster;

    public HeldState(ShadowMonster monster)
    {
        this.monster = monster;
    }

    public void OnEnter()
    {
        monster.agent.enabled = false;
        monster.animator.Play("Spider_Idle_On_Air");
    }

    public void Tick() {}

    public void OnExit()
    {
        monster.agent.enabled = true;
    }
}