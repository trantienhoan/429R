using System.Collections.Generic;
using Game.Combat;
using Game.Enemies;
using UnityEngine;
using UnityEngine.AI;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Enemies")]
    [Tooltip("Keeps spawning enemies (picked by weight) every few seconds at a random spawn point (a child of Spawn " +
             "Points) while the state is active, at most Max Alive of them at once, within the Enemy Limit. It stops once " +
             "a boss (Until Gone) that was alive is killed or gone, or none has shown up within Boss Wait seconds, or any " +
             "Stop When bool is true. Then the ones it spawned can scatter: they pop one after another (Pop When Stopped).")]
    public class KeepSpawning : FsmStateAction
    {
        [RequiredField]
        [CompoundArray("Enemies", "Enemy", "Weight")]
        [Tooltip("What it spawns, e.g. Bee_Small.")]
        public FsmGameObject[] enemies;
        [HasFloatSlider(0, 10)]
        public FsmFloat[] weights;

        [RequiredField]
        [Tooltip("Its children are the spawn points; one is picked at random each time.")]
        public FsmGameObject spawnPoints;

        [UIHint(UIHint.Variable)]
        [Tooltip("Optional: what the enemies are put under, e.g. CurrentlySpawnedStuffs.")]
        public FsmGameObject parent;

        [Tooltip("Seconds between two spawns: a random time from Min Wait to Max Wait.")]
        public FsmFloat minWait;
        public FsmFloat maxWait;

        [Tooltip("Most of them alive at the same time.")]
        public FsmInt maxAlive;

        [Tooltip("The bosses (their prefabs, e.g. Bee_Boss, Bee_Boss_PumpkinButt). It stops once one that was alive is " +
                 "killed or gone. Empty: only Stop When stops it.")]
        public FsmGameObject[] untilGone;

        [Tooltip("With Until Gone set: stops if none of them has shown up after this many seconds.")]
        public FsmFloat bossWait;

        [UIHint(UIHint.Variable)]
        [Tooltip("It also stops once any of these is true.")]
        public FsmBool[] stopWhen;

        [Tooltip("When it stops, the ones it spawned pop one after another (and give their EXP).")]
        public FsmBool popWhenStopped;

        [Tooltip("Seconds between two pops.")]
        public FsmFloat popGap;

        [Tooltip("Event to send once it has stopped (and the rest have popped). Left empty, the state just finishes (FINISHED).")]
        public FsmEvent stoppedEvent;

        // Spawned a little apart from the point, on the NavMesh within this many metres.
        private const float Scatter = 0.3f;
        private const float NavMeshReach = 10f;
        // Counting the bosses looks at every enemy, so it's done a few times a second.
        private const float BossCheckInterval = 0.25f;

        private readonly List<GameObject> spawned = new();
        private float startedAt;
        private float nextSpawn;
        private float nextPop;
        private float nextBossCheck;
        private int bossesSeen;
        private bool stopped;

        public override void Reset()
        {
            enemies = new FsmGameObject[1];
            weights = new FsmFloat[] { 1f };
            spawnPoints = null;
            parent = new FsmGameObject { UseVariable = true };
            minWait = 2f;
            maxWait = 3.5f;
            maxAlive = 5;
            untilGone = new FsmGameObject[0];
            bossWait = 8f;
            stopWhen = new FsmBool[0];
            popWhenStopped = true;
            popGap = 0.12f;
            stoppedEvent = null;
        }

        public override void OnEnter()
        {
            spawned.Clear();
            stopped = false;
            bossesSeen = 0;
            startedAt = Time.time;
            nextSpawn = Time.time + minWait.Value;
            nextBossCheck = 0f;
            if (ShouldStop()) Stop();
        }

        public override void OnUpdate()
        {
            if (!stopped && ShouldStop()) Stop();
            if (stopped)
            {
                PopNext();
                return;
            }
            // Nothing dies while the game is paused, so nothing spawns then either.
            if (Time.timeScale == 0f || Time.time < nextSpawn) return;
            spawned.RemoveAll(one => !EnemyWave.IsAlive(one));
            // No room yet: asks again next frame.
            if (spawned.Count >= maxAlive.Value || !EnemyLimit.TryTakeSlot()) return;
            Spawn();
            nextSpawn = Time.time + Random.Range(minWait.Value, Mathf.Max(minWait.Value, maxWait.Value));
        }

        // Left early because it should stop: the rest pop together.
        public override void OnExit()
        {
            if (!popWhenStopped.Value || !stopped) return;
            foreach (var one in spawned) EnemyWave.Kill(one);
            spawned.Clear();
        }

        private bool ShouldStop()
        {
            foreach (var flag in stopWhen)
            {
                if (flag != null && flag.Value) return true;
            }
            return BossGone();
        }

        // A boss that was alive is gone (fewer alive than the most seen), or none came in time.
        private bool BossGone()
        {
            if (untilGone == null || untilGone.Length == 0 || Time.time < nextBossCheck) return false;
            nextBossCheck = Time.time + BossCheckInterval;
            int alive = AliveBosses();
            if (alive < bossesSeen) return true;
            bossesSeen = alive;
            return alive == 0 && Time.time - startedAt >= bossWait.Value;
        }

        private int AliveBosses()
        {
            int alive = 0;
            foreach (var enemy in Object.FindObjectsByType<EnemyHitDamage>(FindObjectsSortMode.None))
            {
                if (enemy.IsDead) continue;
                string name = enemy.gameObject.name.Replace("(Clone)", "").Trim();
                foreach (var boss in untilGone)
                {
                    if (boss != null && boss.Value != null && boss.Value.name == name)
                    {
                        alive++;
                        break;
                    }
                }
            }
            return alive;
        }

        private void Stop()
        {
            stopped = true;
            nextPop = Time.time;
            if (!popWhenStopped.Value) spawned.Clear();
        }

        private void PopNext()
        {
            spawned.RemoveAll(one => !EnemyWave.IsAlive(one));
            if (spawned.Count == 0)
            {
                Finish();
                if (stoppedEvent != null) Fsm.Event(stoppedEvent);
                return;
            }
            if (Time.time < nextPop) return;
            nextPop = Time.time + popGap.Value;
            EnemyWave.Kill(spawned[0]);
            spawned.RemoveAt(0);
        }

        private void Spawn()
        {
            var prefab = Pick();
            var points = spawnPoints.Value;
            if (prefab == null || points == null) return;
            var choices = new List<Transform>();
            foreach (Transform point in points.transform)
            {
                if (point.gameObject.activeInHierarchy) choices.Add(point);
            }
            var at = choices.Count > 0 ? choices[Random.Range(0, choices.Count)].position : points.transform.position;
            at += new Vector3(Random.Range(-Scatter, Scatter), 0f, Random.Range(-Scatter, Scatter));
            if (NavMesh.SamplePosition(at, out var hit, NavMeshReach, NavMesh.AllAreas)) at = hit.position;

            var one = Object.Instantiate(prefab, at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            if (!parent.IsNone && parent.Value != null) one.transform.SetParent(parent.Value.transform, true);
            spawned.Add(one);
        }

        // One of Enemies, by weight.
        private GameObject Pick()
        {
            float total = 0f;
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] != null && enemies[i].Value != null) total += Weight(i);
            }
            float roll = Random.Range(0f, total);
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] == null || enemies[i].Value == null) continue;
                roll -= Weight(i);
                if (roll <= 0f) return enemies[i].Value;
            }
            return null;
        }

        private float Weight(int i) => weights != null && i < weights.Length && weights[i] != null ? Mathf.Max(0f, weights[i].Value) : 1f;
    }
}
