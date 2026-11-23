using System;
using System.Collections;
using UnityEngine;

namespace Game.Gates
{
    /// <summary>
    /// A gate stage's last fight on an object of its own (user, 2026-11-18: gates 1 and 2 end like gate 3): Delay seconds
    /// after Begin, the giant pumpkin (a Pumpkin Ball Boss prefab) drops onto the floor point; the stage sets it up (its
    /// arena, lanes). When it breaks, The_Seed drops where it broke and Gate Exit takes the player to the rooftop's tree
    /// defence. Under Currently Spawned Stuffs (a Game Over clears it) until Gate Exit makes it stand alone. Its own object,
    /// so the stage that started it may go (the cauldron is destroyed 3 s after its pot bursts).
    /// </summary>
    [DisallowMultipleComponent]
    public class GateBossFinale : MonoBehaviour
    {
        /// <summary>The boss, once it's in (null before, or if there was no prefab).</summary>
        public PumpkinBallBoss Boss { get; private set; }

        private GameObject bossPrefab;
        private GameObject seedPrefab;
        private Vector3 floorPoint;
        private Action<PumpkinBallBoss> setup;
        private Func<Vector3, Vector3> seedAt;
        private string log = "[Gate]";
        private bool exited;

        /// <summary>
        /// The finale: <paramref name="delay"/> seconds from now the boss drops onto <paramref name="landAt"/> (a floor
        /// point), <paramref name="setUp"/> sets it up; broken, The_Seed drops where it broke (moved by
        /// <paramref name="seedSpot"/> if given, e.g. kept off an edge) and Gate Exit takes over.
        /// </summary>
        public static GateBossFinale Begin(GameObject boss, GameObject seed, Vector3 landAt, float delay,
            Action<PumpkinBallBoss> setUp, Func<Vector3, Vector3> seedSpot, string logTag)
        {
            var go = new GameObject("Gate Boss Finale");
            var spawned = GateExit.Global("CurrentlySpawnedStuffs");
            if (spawned != null) go.transform.SetParent(spawned.transform, false);
            go.transform.position = landAt;
            var finale = go.AddComponent<GateBossFinale>();
            finale.bossPrefab = boss;
            finale.seedPrefab = seed;
            finale.floorPoint = landAt;
            finale.setup = setUp;
            finale.seedAt = seedSpot;
            if (!string.IsNullOrEmpty(logTag)) finale.log = logTag;
            finale.StartCoroutine(finale.Run(Mathf.Max(0f, delay)));
            return finale;
        }

        private IEnumerator Run(float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (bossPrefab == null)
            {
                Debug.LogError($"{log} No giant pumpkin prefab (Tools > 429 Game > Gates > Set Up Pumpkin Ball Boss): the seed drops now.", this);
                Exit(floorPoint);
                yield break;
            }
            Boss = PumpkinBallBoss.Spawn(bossPrefab, floorPoint, transform);
            if (Boss == null)
            {
                Exit(floorPoint);
                yield break;
            }
            setup?.Invoke(Boss);
            Boss.Broken += OnBroken;
            Debug.Log($"{log} The giant pumpkin drops in.", this);
            // Never stuck (2026-11-23): gone without breaking (destroyed by something else), the seed still drops where it
            // was last seen.
            var last = Boss.transform.position;
            while (!exited && Boss != null)
            {
                last = Boss.transform.position;
                yield return null;
            }
            if (!exited)
            {
                Debug.LogWarning($"{log} The giant pumpkin vanished without breaking: the seed drops where it was.", this);
                last.y = floorPoint.y;
                Exit(last);
            }
        }

        private void OnBroken(PumpkinBallBoss boss)
        {
            var at = boss != null ? boss.transform.position : floorPoint;
            at.y = boss != null ? boss.floorY : floorPoint.y;
            Exit(at);
        }

        private void Exit(Vector3 at)
        {
            if (exited) return;
            exited = true;
            if (seedAt != null) at = seedAt(at);
            GateExit.Begin(gameObject, seedPrefab, at, log);
        }
    }
}
