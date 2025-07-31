using System.Collections;
using UnityEngine;

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
            monster.StartCoroutine(DelayedScaleDown(2f)); // Add this for delayed disappear
        }

        private IEnumerator DelayedScaleDown(float delay)
        {
            yield return new WaitForSeconds(delay);
            monster.StartCoroutine(monster.ScaleDownAndDisable());
        }

        public void Tick() { }
        public void OnExit() { }
    }
}