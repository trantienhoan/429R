using Game.Enemies;
using UnityEngine;
using UnityEngine.AI;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Enemies")]
    [Tooltip("Spawns a random number (Min to Max) of copies of a prefab at once around a spawn point, e.g. 1-3 Spider_Small " +
             "hatching out of a web ball. Each one lands on the NavMesh within Spread metres of the point, facing a random " +
             "way. With Respect Enemy Limit on, copies that would go over the global enemy limit are skipped.")]
    public class SpawnRandomCount : FsmStateAction
    {
        [RequiredField]
        [Tooltip("What comes out.")]
        public FsmGameObject prefab;

        [Tooltip("Where they come out (usually Owner).")]
        public FsmOwnerDefault spawnPoint;

        [UIHint(UIHint.Variable)]
        [Tooltip("Optional: what they are put under, e.g. CurrentlySpawnedStuffs.")]
        public FsmGameObject parent;

        [Tooltip("Fewest that come out.")]
        public FsmInt min;

        [Tooltip("Most that come out (included).")]
        public FsmInt max;

        [Tooltip("How far from the spawn point they can land, in metres.")]
        public FsmFloat spread;

        [Tooltip("Skip copies while the enemy limit (12 on Quest, 20 on PC) is full.")]
        public FsmBool respectEnemyLimit;

        [UIHint(UIHint.Variable)]
        [Tooltip("Optional: store how many came out.")]
        public FsmInt storeCount;

        public override void Reset()
        {
            prefab = null;
            spawnPoint = null;
            parent = new FsmGameObject { UseVariable = true };
            min = 1;
            max = 3;
            spread = 0.3f;
            respectEnemyLimit = true;
            storeCount = new FsmInt { UseVariable = true };
        }

        public override void OnEnter()
        {
            int spawned = 0;
            var point = Fsm.GetOwnerDefaultTarget(spawnPoint);
            if (prefab.Value != null && point != null)
            {
                int count = Random.Range(Mathf.Max(0, min.Value), Mathf.Max(min.Value, max.Value) + 1);
                for (int i = 0; i < count; i++)
                {
                    if (respectEnemyLimit.Value && !EnemyLimit.TryTakeSlot()) break;
                    var offset = Random.insideUnitCircle * spread.Value;
                    var at = point.transform.position + new Vector3(offset.x, 0f, offset.y);
                    if (NavMesh.SamplePosition(at, out var hit, 1.5f, NavMesh.AllAreas)) at = hit.position;
                    var copy = Object.Instantiate(prefab.Value, at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                    if (!parent.IsNone && parent.Value != null) copy.transform.SetParent(parent.Value.transform, true);
                    spawned++;
                }
            }
            if (!storeCount.IsNone) storeCount.Value = spawned;
            Finish();
        }
    }
}
