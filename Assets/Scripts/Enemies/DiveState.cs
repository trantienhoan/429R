using UnityEngine;

namespace Enemies
{
    public class DiveState : IState
    {
        private ShadowMonster monster;
        private float diveTimer;

        public DiveState(ShadowMonster monster) { this.monster = monster; }

        public void OnEnter()
        {
            diveTimer = 0f;
            if (monster.animator != null)
            {
                monster.animator.SetTrigger("Dive");
                monster.animator.Update(0f);
            }
            if (monster.agent != null) monster.agent.enabled = false;
            if (monster.rb != null)
            {
                monster.rb.isKinematic = false;
                monster.rb.linearVelocity = monster.transform.forward * monster.diveSpeed;
            }
        }

        public void Tick()
        {
            diveTimer += Time.deltaTime;
            if (Physics.Raycast(monster.transform.position, monster.transform.forward, out _, 1f))
            {
                if (monster.rb != null) monster.rb.linearVelocity = Vector3.zero;
                monster.stateMachine.ChangeState(new IdleState(monster));
                return;
            }
            if (diveTimer > monster.diveTimeout)
            {
                if (monster.rb != null) monster.rb.linearVelocity = Vector3.zero;
                monster.stateMachine.ChangeState(new IdleState(monster));
            }
        }

        public void OnExit()
        {
            if (monster.rb != null) monster.rb.linearVelocity = Vector3.zero;
            monster.EnsureAgentOnNavMesh();
        }
    }
}