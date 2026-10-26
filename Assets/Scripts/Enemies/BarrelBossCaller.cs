using System.Collections;
using System.Collections.Generic;
using Game.Combat;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// The boss of a barrel stage, held back until the swarm is nearly cleared (Spawn From Barrels with Boss Last): once
    /// the regular enemies are out, it waits until no more than When Left of them are alive (but at least Min Wait, at
    /// most Max Wait seconds), then warns the player (a puff at its barrel, a sound, a camera shake) and comes out there.
    /// It runs on its own, so the stage's FSM can move on meanwhile (e.g. to wait for the boss to die). Spawn From
    /// Barrels adds it to its GameObject when needed; switched off (the stage reset), the boss is called off.
    /// </summary>
    public class BarrelBossCaller : MonoBehaviour
    {
        /// <summary>What the caller needs to know.</summary>
        public struct Call
        {
            public GameObject boss;
            public Vector3 at;
            public Quaternion rotation;
            public Transform parent;
            public int whenLeft;
            public float minWait;
            public float maxWait;
            public float warningSeconds;
            public GameObject warningEffect;
            public AudioClip warningSound;
            public string warningShake;
            public System.Action<GameObject> spawned;
        }

        // The puff's size at the barrel.
        private const float WarningEffectSize = 1.6f;

        private Coroutine calling;

        /// <summary>Starts waiting for the swarm (these are the regular enemies that came out), then calls the boss.</summary>
        public void Begin(Call call, IReadOnlyList<GameObject> regulars)
        {
            if (calling != null) StopCoroutine(calling);
            calling = StartCoroutine(Calling(call, new List<GameObject>(regulars)));
        }

        private void OnDisable()
        {
            if (calling != null) StopCoroutine(calling);
            calling = null;
        }

        private IEnumerator Calling(Call call, List<GameObject> regulars)
        {
            for (float waited = 0f; waited < call.maxWait; waited += Time.deltaTime)
            {
                if (waited >= call.minWait && Alive(regulars) <= call.whenLeft) break;
                yield return null;
            }

            // The warning: it's coming, from this barrel.
            if (call.warningEffect != null) EffectPool.Play(call.warningEffect, call.at + Vector3.up * 0.5f, WarningEffectSize);
            if (call.warningSound != null) AudioSource.PlayClipAtPoint(call.warningSound, call.at);
            if (!string.IsNullOrEmpty(call.warningShake)) ViewShake.Send(call.warningShake);
            if (call.warningSeconds > 0f) yield return new WaitForSeconds(call.warningSeconds);

            if (call.boss != null)
            {
                var boss = Instantiate(call.boss, call.at, call.rotation);
                if (call.parent != null) boss.transform.SetParent(call.parent, true);
                call.spawned?.Invoke(boss);
            }
            calling = null;
        }

        // Regular enemies still fighting: there, and not dead.
        private static int Alive(List<GameObject> regulars)
        {
            int alive = 0;
            foreach (var enemy in regulars)
            {
                if (enemy == null || !enemy.activeInHierarchy) continue;
                var hits = enemy.GetComponentInChildren<EnemyHitDamage>();
                if (hits == null || !hits.IsDead) alive++;
            }
            return alive;
        }
    }
}
