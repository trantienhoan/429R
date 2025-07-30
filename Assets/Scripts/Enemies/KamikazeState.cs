using UnityEngine;
using Core;

namespace Enemies
{
    public class KamikazeState : IState
    {
        private ShadowMonster monster;
        private float kamikazeDuration = 0.5f; // Adjust to match KamikazeAttack animation duration
        private float kamikazeTimer;

        public KamikazeState(ShadowMonster monster)
        {
            this.monster = monster;
        }

        public void OnEnter()
        {
            kamikazeTimer = 0f;
            if (monster.animator != null)
            {
                monster.animator.SetBool("isCharging", false);
                monster.animator.SetBool("isRunning", false);
                monster.animator.SetBool("isAttacking", false);
                monster.animator.SetBool("isGrounded", monster.IsGrounded());
                monster.animator.SetTrigger("KamikazeAttack");
                monster.animator.Update(0f);
                Debug.Log($"[KamikazeState {monster.gameObject.name}] OnEnter: Triggered KamikazeAttack animation");
            }
            if (monster.agent != null && monster.agent.isActiveAndEnabled && monster.agent.isOnNavMesh)
            {
                monster.agent.isStopped = true;
                Debug.Log($"[KamikazeState {monster.gameObject.name}] OnEnter: NavMeshAgent stopped");
            }
            monster.PerformKamikazeAttack();
            Debug.Log($"[KamikazeState {monster.gameObject.name}] PerformKamikazeAttack called");
        }

        public void Tick()
        {
            kamikazeTimer += Time.deltaTime;
            if (monster.animator != null)
            {
                AnimatorStateInfo stateInfo = monster.animator.GetCurrentAnimatorStateInfo(0);
                bool isInKamikaze = stateInfo.IsName("KamikazeAttack") || stateInfo.fullPathHash == Animator.StringToHash("Base Layer.KamikazeAttack");
                Debug.Log($"[KamikazeState {monster.gameObject.name}] Tick: kamikazeTimer={kamikazeTimer}/{kamikazeDuration}, isInKamikaze={isInKamikaze}, normalizedTime={stateInfo.normalizedTime}");
                if (kamikazeTimer >= kamikazeDuration && isInKamikaze && stateInfo.normalizedTime >= 1f)
                {
                    Debug.Log($"[KamikazeState {monster.gameObject.name}] Kamikaze animation complete, transitioning to DeadState");
                    monster.stateMachine.ChangeState(new DeadState(monster));
                }
            }
            else
            {
                Debug.Log($"[KamikazeState {monster.gameObject.name}] No animator, transitioning to DeadState");
                monster.stateMachine.ChangeState(new DeadState(monster));
            }
        }

        public void OnExit()
        {
            if (monster.animator != null)
            {
                monster.animator.ResetTrigger("KamikazeAttack");
                monster.animator.SetBool("isCharging", false);
                monster.animator.SetBool("isRunning", false);
                monster.animator.SetBool("isAttacking", false);
                Debug.Log($"[KamikazeState {monster.gameObject.name}] OnExit: Reset KamikazeAttack trigger, cleared bools");
            }
            if (monster.agent != null && monster.agent.isActiveAndEnabled && monster.agent.isOnNavMesh)
            {
                monster.agent.isStopped = false;
                Debug.Log($"[KamikazeState {monster.gameObject.name}] OnExit: NavMeshAgent resumed");
            }
        }
    }
}