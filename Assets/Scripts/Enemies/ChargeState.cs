using UnityEngine;

namespace Enemies
{
    public class ChargeState : IState
    {
        private readonly ShadowMonster monster;
        private readonly Transform target;
        private float chargeTimer;

        public ChargeState(ShadowMonster monster, Transform target)
        {
            this.monster = monster;
            this.target = target;
        }

        public void OnEnter()
        {
            monster.animator.Play("Charge");
            monster.agent.isStopped = true;
            chargeTimer = monster.chargeDelay;
        }

        public void Tick()
        {
            if (target == null)
            {
                monster.SetState(new IdleState(monster));
                return;
            }

            chargeTimer -= Time.deltaTime;
            
            if (monster.IsBeingHeld) return;

            if (chargeTimer <= 0f)
            {
                monster.Explode();
            }
        }

        public void OnExit() {}
    }
}