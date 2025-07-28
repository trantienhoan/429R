using Enemies;
using UnityEngine;

public class IdleState : IState
{
    private readonly ShadowMonster monster;
    private float idleTime;

    public IdleState(ShadowMonster monster)
    {
        this.monster = monster;
    }

    public void OnEnter()
    {
        monster.animator.Play("Idle");
        idleTime = Random.Range(1f, 3f);
    }

    public void Tick()
    {
        idleTime -= Time.deltaTime;
        if (idleTime <= 0f)
        {
            monster.SetState(new WanderState(monster));
        }
    }

    public void OnExit() {}
}