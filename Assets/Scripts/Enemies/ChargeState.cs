using UnityEngine;
using System.Collections;namespace Enemies
{
    public class ChargeState : IState
    {
        private ShadowMonster monster;
        private float lockedDistance;    public ChargeState(ShadowMonster monster) { this.monster = monster; }

    public void OnEnter()
    {
        monster.StartCharge();
        lockedDistance = monster.GetDistanceToTarget();
        if (monster.agent != null && monster.agent.isActiveAndEnabled && monster.agent.isOnNavMesh)
        {
            monster.agent.isStopped = true;
        }
        if (monster.animator != null)
        {
            monster.animator.SetBool("isCharging", true);
            monster.animator.Update(0f);
        }
    }

    public void Tick()
    {
        if (monster.IsChargeComplete())
        {
            if (monster.animator != null)
            {
                monster.animator.SetBool("isCharging", false);
                monster.animator.Update(0f);  // Force process
                Debug.Log("Charge complete: isCharging reset, current animator state: " + monster.animator.GetCurrentAnimatorStateInfo(0).shortNameHash);  // Debug
            }

            // Kamikaze check
            if (monster.isInKamikazeMode || monster.healthComponent.GetHealthPercentage() <= monster.kamikazeHealthThreshold)
            {
                monster.StartCoroutine(TransitionToKamikaze());  // Fixed: Removed extra 'monster.'
                return;
            }

            // Attack check
            if (monster.currentTarget != null && lockedDistance <= monster.attackRange)
            {
                monster.StartCoroutine(TransitionToAttack());  // Fixed if similar typo here
            }
            else
            {
                monster.stateMachine.ChangeState(new IdleState(monster));
            }
        }
    }

    private IEnumerator TransitionToAttack()
    {
        ResetAllTriggers();  // Clear before set
        if (monster.animator != null)
        {
            monster.animator.SetTrigger("Attack");
            monster.animator.Update(0f);
            yield return null;  // Delay 1 frame for animator to process
        }
        monster.stateMachine.ChangeState(new AttackState(monster));
        Debug.Log("Transitioning to Attack: Trigger set, state changed");
    }

    private IEnumerator TransitionToKamikaze()
    {
        ResetAllTriggers();
        if (monster.animator != null)
        {
            monster.animator.SetTrigger("KamikazeAttack");
            monster.animator.Update(0f);
            yield return null;
        }
        monster.stateMachine.ChangeState(new KamikazeState(monster));
    }

    private void ResetAllTriggers()
    {
        if (monster.animator != null)
        {
            foreach (var param in monster.animator.parameters)
            {
                if (param.type == AnimatorControllerParameterType.Trigger)
                {
                    monster.animator.ResetTrigger(param.name);
                }
            }
        }
    }

    public void OnExit()
    {
        monster.ResetChargeTimer();
        if (monster.animator != null)
        {
            monster.animator.SetBool("isCharging", false);
        }
    }
}}

