using System.Collections.Generic;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// The giant pumpkin (Pumpkin_Boss) that climbs up at the end of the rooftop and pounds it to pieces. Its hands break
    /// the pumpkins they touch (Breaks: Pumpkin_On_Tree burst and drop their candies, as when hit hard). Each pound of a
    /// hand shakes the camera, puffs up dust and breaks pumpkins around it; pounds are only the show, they never hurt
    /// the player (user, 2026-10-28): the hit is the rooftop End's Giant Smash, once. Pounds are found from the
    /// animation itself: a hand that was coming down fast and stops just above the ground (waving in the air doesn't
    /// count). Goes on the Pumpkin_Boss prefab;
    /// Tools > 429 Game > Enemies > Set Up Giant Pumpkins adds it.
    /// </summary>
    [DisallowMultipleComponent]
    public class GiantPumpkin : MonoBehaviour
    {
        [Tooltip("The hands: bones of its rig. Empty: the bones named palm.L and palm.R.")]
        [SerializeField] private Transform[] hands;
        [Tooltip("How far around a hand things in Breaks get broken, in metres.")]
        [Min(0.05f)]
        [SerializeField] private float handReach = 0.9f;
        [Tooltip("How far from where a hand pounds down things in Breaks get broken, in metres (along the floor).")]
        [Min(0.1f)]
        [SerializeField] private float poundReach = 2.2f;
        [Tooltip("Things whose name starts with one of these break when a hand touches them or a pound lands near them " +
                 "(their Health FSM's health goes to 0, so they burst and drop what they hold, e.g. candies).")]
        [SerializeField] private string[] breaks = { "Pumpkin_On_Tree" };
        [Tooltip("A hand coming down faster than this, in metres per second, and then stopping is a pound.")]
        [Min(0.5f)]
        [SerializeField] private float poundSpeed = 6f;
        [Tooltip("A hand only pounds when it stops at most this high above the ground, in metres (its palm bone sits high " +
                 "in the giant's big hand). Stopping higher, waving in the air, isn't a pound.")]
        [Min(0.1f)]
        [SerializeField] private float groundReach = 3.5f;
        [Tooltip("Shown where a hand pounds down, e.g. Smoke_Blast. Empty: none.")]
        [SerializeField] private GameObject poundEffect;
        [Min(0.01f)]
        [SerializeField] private float poundEffectSize = 1.5f;
        [Tooltip("Camera shake event sent to the main camera's Info FSM at each pound. Empty: none.")]
        [SerializeField] private string poundShake = "ShakeMed";

        // A pound is over once the hand slows to this share of Pound Speed.
        private const float StopShare = 0.25f;
        // Things this far above or below the pound aren't reached by it (another floor), in metres.
        private const float PoundHeight = 3f;

        private readonly Collider[] hits = new Collider[64];
        private Vector3[] lastPositions;
        private bool[] falling;
        private float nextShake;

        private void Awake()
        {
            if (hands == null || hands.Length == 0)
            {
                var found = new List<Transform>();
                foreach (var bone in GetComponentsInChildren<Transform>(true))
                {
                    if (bone.name == "palm.L" || bone.name == "palm.R") found.Add(bone);
                }
                hands = found.ToArray();
            }
            lastPositions = new Vector3[hands.Length];
            falling = new bool[hands.Length];
            for (int i = 0; i < hands.Length; i++) lastPositions[i] = hands[i] != null ? hands[i].position : Vector3.zero;
            if (poundEffect != null) EffectPool.Warm(poundEffect);
        }

        // After the Animator has posed the hands.
        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            for (int i = 0; i < hands.Length; i++)
            {
                var hand = hands[i];
                if (hand == null) continue;
                var position = hand.position;
                float down = (lastPositions[i].y - position.y) / dt;
                lastPositions[i] = position;

                BreakAround(position, handReach, false);
                if (down >= poundSpeed) falling[i] = true;
                else if (falling[i] && down <= poundSpeed * StopShare)
                {
                    falling[i] = false;
                    if (Physics.Raycast(position, Vector3.down, out var ground, groundReach, ~0, QueryTriggerInteraction.Ignore))
                        Pound(position, ground.point);
                }
            }
        }

        // A hand stopped at Hand, just above Ground: dust, a shake, pumpkins round it broken. Never hurts the player.
        private void Pound(Vector3 hand, Vector3 ground)
        {
            if (poundEffect != null) EffectPool.Play(poundEffect, ground, poundEffectSize);
            if (!string.IsNullOrEmpty(poundShake) && Time.time >= nextShake)
            {
                nextShake = Time.time + 0.3f;
                SendToPlayerFsm("MainCam", "Info", poundShake);
            }
            BreakAround(hand, poundReach, true);
        }

        // Breaks the things in Breaks within reach: a pound reaches along the floor (any height near it), a hand all round.
        private void BreakAround(Vector3 at, float reach, bool pound)
        {
            if (breaks == null || breaks.Length == 0) return;
            int count = pound
                ? Physics.OverlapCapsuleNonAlloc(at - Vector3.up * PoundHeight, at + Vector3.up * PoundHeight, reach, hits, ~0, QueryTriggerInteraction.Ignore)
                : Physics.OverlapSphereNonAlloc(at, reach, hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var body = hits[i].attachedRigidbody;
                var thing = body != null ? body.gameObject : hits[i].gameObject;
                if (Breakable(thing.name)) Break(thing);
            }
        }

        private bool Breakable(string thingName)
        {
            foreach (var prefix in breaks)
            {
                if (!string.IsNullOrEmpty(prefix) && thingName.StartsWith(prefix)) return true;
            }
            return false;
        }

        // Its health to 0: its Health FSM breaks it the usual way (pieces, candies). No EXP: the giant broke it, not the
        // player.
        private static void Break(GameObject thing)
        {
            foreach (var fsm in thing.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != "Health") continue;
                var health = fsm.FsmVariables.FindFsmFloat("health");
                if (health == null || health.Value <= 0f) continue;
                // (One the player already broke keeps its EXP.)
                Game.Leveling.PlayerLevel.NoRewardFor(thing);
                health.Value = 0f;
            }
        }

        // An event to an FSM on an object kept in a PlayMaker global (e.g. "Player", "MainCam").
        private static void SendToPlayerFsm(string globalObject, string fsmName, string eventName)
        {
            var target = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmGameObject(globalObject);
            if (target == null || target.Value == null) return;
            foreach (var fsm in target.Value.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == fsmName) fsm.SendEvent(eventName);
            }
        }
    }
}
