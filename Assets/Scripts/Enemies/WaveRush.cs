using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Enemies
{
    /// <summary>
    /// A rush moment in an enemy manager's wave (user, 2026-10-22: the Bedroom and Playroom fights need a rush like the
    /// rooftop's whack-a-pumpkin and bee swarm, "more enemies = more chaos"; built 2026-11-19). Once Start After Kills of
    /// the manager's wave are down, a swarm of small bugs pours in from the manager's own spawn points, many at once,
    /// with a laugh and a big shake. They join the manager's wave, so its own spawns wait while they're about and its
    /// boss comes only when the rush is (nearly) beaten. Beat the whole swarm: a burst of candy where the last one fell,
    /// and a bell. Once per wave (a new wave, after a game over or a new loop, has its own).
    /// Made by Tools > 429 Game > Enemies > Set Up Rush Moments (EnemyManager 1 and 3).
    /// </summary>
    public class WaveRush : MonoBehaviour
    {
        [Tooltip("The manager's wave FSM (EnemyManager): the swarm joins its wave.")]
        [SerializeField] private PlayMakerFSM manager;
        [Tooltip("That FSM's count of the enemies it has spawned this wave.")]
        [SerializeField] private string spawnedVariable = "numberOfEnemies";
        [Tooltip("The rush starts when this many of the wave are down (12 in the wave: 6 = halfway).")]
        [Min(1)]
        [SerializeField] private int startAfterKills = 6;

        [Header("The swarm")]
        [Tooltip("What pours in (the small bugs).")]
        [SerializeField] private GameObject rushEnemy;
        [Tooltip("How many come in all.")]
        [Min(1)]
        [SerializeField] private int rushCount = 12;
        [Tooltip("Most of the swarm alive at once. The game's Enemy Limit still caps every enemy (12 on Quest, 20 on PC).")]
        [Min(1)]
        [SerializeField] private int rushAlive = 8;
        [Tooltip("Seconds between two of them, shortest and longest.")]
        [SerializeField] private Vector2 spawnEvery = new Vector2(0.15f, 0.35f);
        [Tooltip("Where they pour in from: the children of this (the manager's own spawn points).")]
        [SerializeField] private Transform spawnPoints;
        [Tooltip("How far from a spawn point they appear (m), so they don't stack.")]
        [Min(0f)]
        [SerializeField] private float spread = 0.7f;

        [Header("Cues")]
        [Tooltip("Played in the room as the rush starts. Empty: silent.")]
        [SerializeField] private AudioClip startSound;
        [Range(0f, 1f)]
        [SerializeField] private float startVolume = 0.9f;
        [Tooltip("A camera shake event for the main camera's Info FSM as the rush starts (ShakeSmall, ShakeMed, ShakeBigger). Empty: none.")]
        [SerializeField] private string startShake = "ShakeBigger";
        [Tooltip("Seconds between the cue and the first bug.")]
        [Min(0f)]
        [SerializeField] private float leadIn = 0.6f;

        [Header("Reward")]
        [Tooltip("Candies that burst out where the last of the swarm fell. 0 = none.")]
        [Min(0)]
        [SerializeField] private int rewardCandies = 8;
        [Tooltip("The candies it picks from at random.")]
        [SerializeField] private GameObject[] candies = System.Array.Empty<GameObject>();
        [Tooltip("Played as the swarm is beaten. Empty: silent.")]
        [SerializeField] private AudioClip clearSound;
        [Range(0f, 1f)]
        [SerializeField] private float clearVolume = 0.8f;
        [Tooltip("Seconds after the last of the swarm came that the rush stops waiting for a straggler stuck out of reach: " +
                 "the reward comes anyway, by the player.")]
        [Min(5f)]
        [SerializeField] private float giveUpAfter = 45f;

        private enum Phase { Waiting, Rushing, Done }

        private readonly List<GameObject> swarm = new();
        private Phase phase;
        private HutongGames.PlayMaker.FsmInt count;
        private int spawned;
        private int lastCount;
        private float nextSpawn;
        private float lastSpawnAt;
        private Vector3 lastSeen;

        /// <summary>The swarm is on its way or still about.</summary>
        public bool IsRushing => phase == Phase.Rushing;

        private void OnEnable()
        {
            ResetRush();
        }

        private void ResetRush()
        {
            phase = Phase.Waiting;
            swarm.Clear();
            spawned = 0;
            lastCount = 0;
        }

        private void Update()
        {
            if (manager == null || rushEnemy == null || Time.timeScale == 0f) return;
            count ??= manager.FsmVariables.FindFsmInt(spawnedVariable);
            if (count == null) return;
            // A new wave (its count started over) has its own rush.
            if (count.Value < lastCount && phase == Phase.Done) ResetRush();
            lastCount = count.Value;

            if (phase == Phase.Waiting)
            {
                // Before the rush every one of the wave is the manager's own: down = spawned - alive.
                if (count.Value - EnemyWave.Alive(manager.Fsm) >= startAfterKills) Begin();
            }
            else if (phase == Phase.Rushing)
            {
                Rush();
            }
        }

        private void Begin()
        {
            phase = Phase.Rushing;
            spawned = 0;
            swarm.Clear();
            nextSpawn = Time.time + leadIn;
            lastSpawnAt = Time.time;
            lastSeen = RoomMiddle();
            if (startSound != null) AudioSource.PlayClipAtPoint(startSound, lastSeen, startVolume);
            Shake(startShake);
        }

        private void Rush()
        {
            // Who's still about (and where the last one was, for the candy).
            for (int i = swarm.Count - 1; i >= 0; i--)
            {
                var bug = swarm[i];
                if (bug != null && EnemyWave.IsAlive(bug)) lastSeen = bug.transform.position;
                else swarm.RemoveAt(i);
            }

            if (spawned < rushCount)
            {
                if (Time.time >= nextSpawn && swarm.Count < rushAlive && EnemyLimit.TryTakeSlot())
                {
                    SpawnOne();
                    nextSpawn = Time.time + Random.Range(spawnEvery.x, Mathf.Max(spawnEvery.x, spawnEvery.y));
                }
                return;
            }
            if (swarm.Count == 0) Finish(lastSeen);
            else if (Time.time - lastSpawnAt > giveUpAfter) Finish(ByThePlayer());
        }

        private void SpawnOne()
        {
            // In the holder the manager's own spawns go in (GAMESTAGES' Game Over clears it), so a game over takes them too.
            var holder = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmGameObject("CurrentlySpawnedStuffs");
            var parent = holder != null && holder.Value != null ? holder.Value.transform : null;
            var bug = Instantiate(rushEnemy, PickSpot(), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), parent);
            spawned++;
            lastSpawnAt = Time.time;
            // The rare white bug only runs away: it doesn't hold the rush (nor the wave) up.
            if (SpiderBrain.IsAnomalyBug(bug)) return;
            EnemyWave.Add(manager.Fsm, bug);
            swarm.Add(bug);
        }

        // A spawn point of the manager's at random, a little to one side, on the NavMesh (as the manager's own spawns).
        private Vector3 PickSpot()
        {
            var at = transform.position;
            if (spawnPoints != null && spawnPoints.childCount > 0) at = spawnPoints.GetChild(Random.Range(0, spawnPoints.childCount)).position;
            var side = Random.insideUnitCircle * spread;
            at += new Vector3(side.x, 0f, side.y);
            if (NavMesh.SamplePosition(at, out var hit, 10f, NavMesh.AllAreas)) at = hit.position;
            return at;
        }

        private void Finish(Vector3 at)
        {
            phase = Phase.Done;
            if (rewardCandies > 0 && candies != null && candies.Length > 0)
                Game.Bonus.CandyFountain.Play(at + Vector3.up * 0.5f, rewardCandies, candies, 1f, new Vector2(2.5f, 4.5f), new Vector2(1f, 2.5f));
            if (clearSound != null) AudioSource.PlayClipAtPoint(clearSound, at, clearVolume);
        }

        // The middle of the manager's spawn points (the room), or the manager itself.
        private Vector3 RoomMiddle()
        {
            if (spawnPoints == null || spawnPoints.childCount == 0) return transform.position;
            var sum = Vector3.zero;
            foreach (Transform point in spawnPoints) sum += point.position;
            return sum / spawnPoints.childCount;
        }

        // A metre in front of the player, on the floor (for a reward when the last bug is out of reach).
        private Vector3 ByThePlayer()
        {
            var head = Camera.main;
            if (head == null) return lastSeen;
            var ahead = Vector3.ProjectOnPlane(head.transform.forward, Vector3.up);
            var at = head.transform.position + (ahead.sqrMagnitude > 0.0001f ? ahead.normalized : Vector3.forward);
            if (NavMesh.SamplePosition(at, out var hit, 3f, NavMesh.AllAreas)) at = hit.position;
            return at;
        }

        // A camera shake event sent to the main camera's Info FSM (ShakeSmall, ShakeMed, ShakeBigger).
        private static void Shake(string eventName)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            var cam = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmGameObject("MainCam");
            if (cam == null || cam.Value == null) return;
            foreach (var fsm in cam.Value.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == "Info") fsm.SendEvent(eventName);
            }
        }
    }
}
