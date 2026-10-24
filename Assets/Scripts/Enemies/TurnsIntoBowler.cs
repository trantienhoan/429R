using System.Collections.Generic;
using Game.Combat;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;

namespace Game.Enemies
{
    /// <summary>
    /// Now and then a tree pumpkin turns into a Bowling Pumpkin: with more than More Than tree pumpkins out (Live Limit's
    /// count for Group), every Check Every seconds there's Chance that a random one bursts open and a Bowler comes out of
    /// it. Only one lying still on the floor, Distance from the player, with a clear straight run at them; never one in
    /// a hand, just thrown, or with a roach hiding in it. At most Max At Once bowlers at a time. One check runs for all of
    /// them. Goes on Pumpkin_On_Tree's root.
    /// </summary>
    [DisallowMultipleComponent]
    public class TurnsIntoBowler : MonoBehaviour
    {
        [Tooltip("What comes out (Bowling_Pumpkin_Enemy).")]
        [SerializeField] private GameObject bowler;
        [Tooltip("Only with more than this many of the Group out at once.")]
        [Min(0)]
        [SerializeField] private int moreThan = 12;
        [Tooltip("Live Limit's group that's counted.")]
        [SerializeField] private string group = "Pumpkin_On_Tree";
        [Tooltip("Seconds between two checks.")]
        [Min(0.5f)]
        [SerializeField] private float checkEvery = 8f;
        [Tooltip("Chance (0 to 1) at each check that one turns.")]
        [Range(0f, 1f)]
        [SerializeField] private float chance = 0.35f;
        [Tooltip("At most this many bowlers out at once.")]
        [Min(1)]
        [SerializeField] private int maxAtOnce = 1;
        [Tooltip("Only one this far from the player, in metres (between).")]
        [SerializeField] private Vector2 distance = new(3f, 12f);

        private const string SpawnedStuffVariable = "CurrentlySpawnedStuffs";
        private static readonly List<TurnsIntoBowler> all = new();
        private static float nextCheck;

        private Rigidbody body;
        private XRGrabInteractable grab;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            all.Clear();
            nextCheck = 0f;
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
            nextCheck = Time.time + checkEvery;
            if (bowler == null || LiveLimit.Count(group) <= moreThan || BowlingPumpkin.Alive >= maxAtOnce) return;
            if (Random.value > chance) return;
            TurnOne();
        }

        /// <summary>Turns a random tree pumpkin that's ready into a bowler now (also for testing). False if none is.</summary>
        public static bool TurnOne()
        {
            var player = FindAnyObjectByType<XROrigin>();
            if (player == null || player.Camera == null) return false;
            var head = player.Camera.transform.position;
            int start = Random.Range(0, Mathf.Max(1, all.Count));
            for (int i = 0; i < all.Count; i++)
            {
                var one = all[(start + i) % all.Count];
                if (one != null && one.Ready(head, out var spot))
                {
                    one.Become(spot, head);
                    return true;
                }
            }
            return false;
        }

        // Lying still on the floor, nobody's using it, no roach in it, the player at the right distance with a clear run.
        private bool Ready(Vector3 head, out Vector3 spot)
        {
            spot = transform.position;
            if (bowler == null || GetComponent<RottingPumpkin>() != null) return false;
            if (grab != null && (grab.isSelected || HeldThings.InUse(grab))) return false;
            if (body != null && !body.isKinematic && body.linearVelocity.sqrMagnitude > 0.04f) return false;
            var flat = head - transform.position;
            flat.y = 0f;
            if (flat.magnitude < distance.x || flat.magnitude > distance.y) return false;
            if (!NavMesh.SamplePosition(transform.position, out var hit, 0.8f, NavMesh.AllAreas)) return false;
            spot = hit.position;
            var playerFloor = new Vector3(head.x, spot.y, head.z);
            if (!NavMesh.SamplePosition(playerFloor, out var near, 1.5f, NavMesh.AllAreas)) return false;
            return !NavMesh.Raycast(spot, near.position, out _, NavMesh.AllAreas);
        }

        // The pumpkin is gone (no pieces, no candy: the bowler bursts out of it), and the bowler is there.
        private void Become(Vector3 spot, Vector3 head)
        {
            var to = head - spot;
            to.y = 0f;
            var holder = FsmVariables.GlobalVariables.FindFsmGameObject(SpawnedStuffVariable);
            var parent = holder != null && holder.Value != null ? holder.Value.transform : transform.parent;
            Instantiate(bowler, spot, to.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(to) : transform.rotation, parent);
            all.Remove(this);
            Destroy(gameObject);
        }
    }
}
