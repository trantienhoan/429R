using UnityEngine;

namespace Enemies
{
    public class HeldState : IState
    {
        private static readonly int IsRunning = Animator.StringToHash("isRunning");
        private static readonly int IsCharging = Animator.StringToHash("isCharging");
        private static readonly int IsGrounded = Animator.StringToHash("isGrounded");
        private readonly ShadowMonster monster;
        private float holdTimer;

        public HeldState(ShadowMonster monster)
        {
            this.monster = monster;
        }

        public void OnEnter()
        {
            holdTimer = 0f;
            monster.animator.SetBool(IsRunning, false);
            monster.animator.SetBool(IsCharging, false);
            monster.animator.SetBool(IsGrounded, false);
        }

        public void Tick()
        {
            holdTimer += Time.deltaTime;

            // Struggle feedback at 80% hold time (shake object)
            if (holdTimer > monster.maxHoldTime * 0.8f)
            {
                monster.transform.localPosition += Random.insideUnitSphere * (monster.struggleShakeIntensity * Time.deltaTime);
                // Optional: Trigger "Struggle" animation if you add one
            }

            if (holdTimer > monster.maxHoldTime)
            {
                monster.ForceBreakHold();
            }

            if (!monster.isBeingHeld && monster.isGrounded)
            {
                monster.stateMachine.ChangeState(new IdleState(monster));
            }
        }

        public void OnExit()
        {
            monster.animator.SetBool(IsGrounded, monster.isGrounded);
        }
    }
}