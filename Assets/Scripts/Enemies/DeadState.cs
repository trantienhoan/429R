using Enemies;

public class DeadState : IState
{
    private readonly ShadowMonster monster;

    public DeadState(ShadowMonster monster)
    {
        this.monster = monster;
    }

    public void OnEnter()
    {
        monster.animator.Play("Spider_Die");
        monster.agent.enabled = false;
        monster.StartCoroutine(monster.ScaleDownAndDisable());
    }

    public void Tick() {}
    public void OnExit() {}
}