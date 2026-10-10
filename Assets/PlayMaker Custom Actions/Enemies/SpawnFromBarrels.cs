using System.Collections.Generic;
using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Enemies")]
    [Tooltip("Spawns an enemy out of each barrel: one random barrel gets the Boss, every other one a Regular enemy (so " +
             "5 barrels = 4 regular + 1 boss). Barrels that are gone or empty are skipped. Each one comes out First Delay + " +
             "n x Delay Between seconds after the state starts, to match the barrels breaking (Send Event Dead with delays). " +
             "Leaving the state early spawns the rest at once.")]
    public class SpawnFromBarrels : FsmStateAction
    {
        [Tooltip("The barrels, in the order they break.")]
        public FsmGameObject[] barrels;

        [RequiredField]
        [Tooltip("Comes out of every barrel but one.")]
        public FsmGameObject regular;

        [Tooltip("Comes out of one random barrel. Empty = a regular one there too.")]
        public FsmGameObject boss;

        [UIHint(UIHint.Variable)]
        [Tooltip("Optional: what the enemies are put under, e.g. CurrentlySpawnedStuffs.")]
        public FsmGameObject parent;

        [Tooltip("Seconds before the first one comes out.")]
        public FsmFloat firstDelay;

        [Tooltip("Seconds between one barrel and the next.")]
        public FsmFloat delayBetween;

        [UIHint(UIHint.Variable)]
        [Tooltip("Optional: store the boss.")]
        public FsmGameObject storeBoss;

        [Tooltip("Event to send when they're all out. Left empty, the action just finishes.")]
        public FsmEvent finishEvent;

        private readonly List<(Vector3 position, Quaternion rotation, bool isBoss)> waiting = new();
        private float elapsed;
        private int spawned;

        public override void Reset()
        {
            barrels = new FsmGameObject[0];
            regular = null;
            boss = null;
            parent = new FsmGameObject { UseVariable = true };
            firstDelay = 0.1f;
            delayBetween = 0.1f;
            storeBoss = new FsmGameObject { UseVariable = true };
            finishEvent = null;
        }

        public override void OnEnter()
        {
            // Where the barrels stand now: they're destroyed a moment after they break.
            waiting.Clear();
            foreach (var barrel in barrels)
            {
                var go = barrel != null ? barrel.Value : null;
                if (go != null && go.activeInHierarchy) waiting.Add((go.transform.position, go.transform.rotation, false));
            }
            if (waiting.Count == 0) waiting.Add((Owner.transform.position, Quaternion.identity, false));
            int bossAt = Random.Range(0, waiting.Count);
            waiting[bossAt] = (waiting[bossAt].position, waiting[bossAt].rotation, true);
            elapsed = 0f;
            spawned = 0;
            SpawnDue();
        }

        public override void OnUpdate()
        {
            elapsed += Time.deltaTime;
            SpawnDue();
        }

        public override void OnExit()
        {
            while (spawned < waiting.Count) Spawn(waiting[spawned++]);
        }

        private void SpawnDue()
        {
            while (spawned < waiting.Count && elapsed >= firstDelay.Value + spawned * delayBetween.Value) Spawn(waiting[spawned++]);
            if (spawned < waiting.Count) return;
            Finish();
            if (finishEvent != null) Fsm.Event(finishEvent);
        }

        private void Spawn((Vector3 position, Quaternion rotation, bool isBoss) at)
        {
            var prefab = at.isBoss && boss.Value != null ? boss.Value : regular.Value;
            if (prefab == null) return;
            var enemy = Object.Instantiate(prefab, at.position, Quaternion.Euler(0f, at.rotation.eulerAngles.y, 0f));
            if (!parent.IsNone && parent.Value != null) enemy.transform.SetParent(parent.Value.transform, true);
            if (at.isBoss && !storeBoss.IsNone) storeBoss.Value = enemy;
        }
    }
}
