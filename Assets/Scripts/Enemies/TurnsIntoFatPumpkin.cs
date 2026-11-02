using System.Collections.Generic;
using Game.Bonus;
using Game.Combat;
using Game.Inventory;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;

namespace Game.Enemies
{
    /// <summary>
    /// A tree pumpkin left among the candies gets greedy: with more than More Than candies within Reach of it for Seconds
    /// in a row (user, 2026-11-02), it bursts open and a Fat Pumpkin comes out of it. Only one lying still on the floor; never
    /// one in a hand, just thrown, or with a roach hiding in it. At most Max At Once fat pumpkins at a time, Rest Seconds
    /// between two, none in the bonus round (those candies are the player's prize). One check runs for all of them. Goes
    /// on Pumpkin_On_Tree's root.
    /// </summary>
    [DisallowMultipleComponent]
    public class TurnsIntoFatPumpkin : MonoBehaviour
    {
        [Tooltip("What comes out (Fat_Pumpkin_Enemy).")]
        [SerializeField] private GameObject fatPumpkin;
        [Tooltip("Only with more than this many candies round it.")]
        [Min(0)]
        [SerializeField] private int moreThan = 4;
        [Tooltip("How far from it a candy counts as round it, in metres.")]
        [Min(0.1f)]
        [SerializeField] private float reach = 2f;
        [Tooltip("How long, in seconds, the candies have to stay round it.")]
        [Min(0f)]
        [SerializeField] private float seconds = 3f;
        [Tooltip("At most this many fat pumpkins out at once.")]
        [Min(1)]
        [SerializeField] private int maxAtOnce = 2;
        [Tooltip("At least this many seconds between one coming out and the next.")]
        [Min(0f)]
        [SerializeField] private float restSeconds = 15f;

        // Seconds between two checks.
        private const float CheckEvery = 0.5f;
        private const float StillSpeed = 0.2f;
        private const float NavMeshReach = 0.8f;
        private const string SpawnedStuffVariable = "CurrentlySpawnedStuffs";

        private static readonly List<TurnsIntoFatPumpkin> all = new();
        private static readonly List<GameObject> loose = new();
        private static float nextCheck;
        private static float restUntil;

        private Rigidbody body;
        private XRGrabInteractable grab;
        // Since when the candies have been round it (-1: they aren't).
        private float surroundedSince = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            all.Clear();
            loose.Clear();
            nextCheck = 0f;
            restUntil = 0f;
        }

        private void OnEnable()
        {
            all.Add(this);
            TryGetComponent(out body);
            TryGetComponent(out grab);
        }

        private void OnDisable() => all.Remove(this);

        private void Update()
        {
            // The first one checks for all of them.
            if (all.Count == 0 || all[0] != this || Time.time < nextCheck) return;
            nextCheck = Time.time + CheckEvery;
            CheckAll();
        }

        private static void CheckAll()
        {
            CandyHelmet.FindLoose(loose);
            bool none = BonusRound.Running != null || loose.Count == 0;
            TurnsIntoFatPumpkin ready = null;
            float now = Time.time;
            foreach (var one in all)
            {
                if (none || !one.Surrounded())
                {
                    one.surroundedSince = -1f;
                    continue;
                }
                if (one.surroundedSince < 0f) one.surroundedSince = now;
                // The one waiting longest goes first.
                if (now - one.surroundedSince >= one.seconds && (ready == null || one.surroundedSince < ready.surroundedSince) && one.Ready(out _))
                    ready = one;
            }
            if (ready == null || FatPumpkin.Alive >= ready.maxAtOnce || now < restUntil) return;
            restUntil = now + ready.restSeconds;
            ready.Become();
        }

        private bool Surrounded()
        {
            if (fatPumpkin == null) return false;
            var here = transform.position;
            float range = reach * reach;
            int near = 0;
            foreach (var candy in loose)
            {
                if ((candy.transform.position - here).sqrMagnitude > range) continue;
                if (++near > moreThan) return true;
            }
            return false;
        }

        // Lying still on the floor, nobody's using it, no roach in it.
        private bool Ready(out Vector3 spot)
        {
            spot = transform.position;
            if (GetComponent<RottingPumpkin>() != null) return false;
            if (grab != null && (grab.isSelected || HeldThings.InUse(grab))) return false;
            if (body != null && !body.isKinematic && body.linearVelocity.sqrMagnitude > StillSpeed * StillSpeed) return false;
            if (!NavMesh.SamplePosition(transform.position, out var hit, NavMeshReach, NavMesh.AllAreas)) return false;
            spot = hit.position;
            return true;
        }

        // The pumpkin is gone (no pieces, no candy: the fat one bursts out of it), and the fat one is there.
        private void Become()
        {
            if (!Ready(out var spot)) return;
            var holder = FsmVariables.GlobalVariables.FindFsmGameObject(SpawnedStuffVariable);
            var parent = holder != null && holder.Value != null ? holder.Value.transform : transform.parent;
            Instantiate(fatPumpkin, spot, Quaternion.Euler(0f, transform.eulerAngles.y, 0f), parent);
            all.Remove(this);
            Destroy(gameObject);
        }
    }
}
