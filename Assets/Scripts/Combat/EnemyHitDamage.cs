using System;
using System.Collections.Generic;
using Game.Enemies;
using UnityEngine;
using UnityEngine.AI;
using FsmFloat = HutongGames.PlayMaker.FsmFloat;

namespace Game.Combat
{
    /// <summary>
    /// Turns hits into damage for an enemy made in PlayMaker. Hit power = hit speed x the weapon's Power / Toughness:
    /// too weak does nothing (the enemy just flinches), a normal hit does 1-2 damage, a big hit 3-5 and knocks it back
    /// (the numbers are in the Hit Settings asset). It lowers the "health" variable of the enemy's Health FSM, which
    /// already watches it, sends "Damage" (or "ShakeSmall" for weak hits) so the enemy's hit reactions play, and
    /// switches off the enemy's old "Damage" FSM so hits aren't counted twice.
    /// Once the health runs out, however it happened, more hits are ignored and the enemy's NavMesh agent is held
    /// still: the death states switch off the FSM that walks the enemy, but not the agent, which would carry the body
    /// on along its last path.
    /// Tools > 429 Game > Combat > Set Up Hit Damage adds it.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyHitDamage : MonoBehaviour
    {
        // Weak hits make the enemy flinch at most this often, so a weapon resting against it doesn't shake it nonstop.
        private const float FlinchInterval = 0.25f;
        // Slower touches than this don't even make it flinch.
        private const float MinFlinchPower = 1f;

        [Tooltip("How hits turn into damage, shared by all enemies. Left empty, the default numbers are used.")]
        [SerializeField] private HitSettings settings;
        [Tooltip("Divides the hit power: 1 = a normal bug, 1.5 = needs harder hits (the mini-bosses).")]
        [Min(0.1f)]
        [SerializeField] private float toughness = 1f;
        [Tooltip("After a hit that hurts, more hits are ignored this long, in seconds, so one swing counts once.")]
        [Min(0f)]
        [SerializeField] private float hitCooldown = 0.3f;

        [Header("Big hits")]
        [Tooltip("Knock the enemy back, away from the player, on a big hit.")]
        [SerializeField] private bool knockBack = true;
        [Tooltip("How far a big hit knocks it back, in metres.")]
        [Min(0f)]
        [SerializeField] private float knockBackDistance = 2f;
        [Tooltip("How long the slide back takes, in seconds.")]
        [Min(0.01f)]
        [SerializeField] private float knockBackTime = 0.45f;
        [Tooltip("How high it hops during the slide, in metres.")]
        [Min(0f)]
        [SerializeField] private float knockBackHop = 0.25f;
        [Tooltip("How long it stays still after landing, in seconds.")]
        [Min(0f)]
        [SerializeField] private float knockBackStun = 0.3f;

        [Header("PlayMaker")]
        [Tooltip("The FSM holding the enemy's health. Left empty, the FSM whose name ends in \"Health\".")]
        [SerializeField] private string healthFsm = "";
        [Tooltip("The float variable in that FSM that holds the health.")]
        [SerializeField] private string healthVariable = "health";
        [Tooltip("Sent to the enemy's FSMs on a hit that hurts, to play its hit reaction.")]
        [SerializeField] private string hitEvent = "Damage";
        [Tooltip("Sent to the enemy's FSMs on a hit too weak to hurt, to play its small shake.")]
        [SerializeField] private string weakHitEvent = "ShakeSmall";
        [Tooltip("The enemy's old collision-damage FSM, switched off so hits aren't counted twice. Empty = leave every FSM on.")]
        [SerializeField] private string oldDamageFsm = "Damage";

        private readonly List<PlayMakerFSM> fsms = new();
        private FsmFloat health;
        private Knockback knockback;
        private NavMeshAgent agent;
        private float nextHit;
        private float nextFlinch;
        private bool dead;

        /// <summary>Raised after every hit that hurts an enemy: the enemy, and what the hit did.</summary>
        public static event Action<EnemyHitDamage, HitResult> AnyHit;

        /// <summary>The most recent hit on this enemy, whether it hurt or not.</summary>
        public HitResult LastHit { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            AnyHit = null;
        }

        // In Start, so every PlayMakerFSM has set itself up in Awake first.
        private void Start()
        {
            GetComponents(fsms);
            foreach (var fsm in fsms)
            {
                if (!string.IsNullOrEmpty(oldDamageFsm) && fsm.FsmName == oldDamageFsm) fsm.enabled = false;

                bool holdsHealth = string.IsNullOrEmpty(healthFsm) ? fsm.FsmName.EndsWith("Health") : fsm.FsmName == healthFsm;
                if (health == null && holdsHealth) health = fsm.FsmVariables.FindFsmFloat(healthVariable);
            }

            if (health == null)
                Debug.LogWarning($"[Hit Damage] '{name}' has no Health FSM with a \"{healthVariable}\" variable, so hits can't hurt it.", this);

            TryGetComponent(out agent);
        }

        // The Health FSM plays the death itself (it watches the health every frame); this only keeps the body still.
        private void Update()
        {
            if (!dead)
            {
                if (health == null || health.Value > 0f) return;
                dead = true;
            }
            HoldStill();
        }

        // The enemy's Rigidbody gets the collisions of all its colliders, so any part of it can be hit.
        private void OnCollisionEnter(Collision collision)
        {
            if (health == null || dead || Time.time < nextHit) return;

            float power = collision.relativeVelocity.magnitude * WeaponPower.Of(collision) / toughness;
            var hit = (settings != null ? settings : HitSettings.Defaults).Evaluate(power);
            LastHit = hit;

            if (hit.Damage <= 0f)
            {
                if (power >= MinFlinchPower && Time.time >= nextFlinch)
                {
                    nextFlinch = Time.time + FlinchInterval;
                    Send(weakHitEvent);
                }
                return;
            }

            health.Value -= hit.Damage;
            nextHit = Time.time + hitCooldown;
            // The killing blow still plays the hit reaction (sound and sparks); the Health FSM's death state switches
            // that FSM off later this frame.
            Send(hitEvent);
            if (health.Value <= 0f)
            {
                dead = true;
                HoldStill();
            }
            // A big killing blow still knocks the body back: Knockback moves the agent itself, which works while it's held.
            if (hit.IsBig && knockBack) KnockBackFromPlayer();
            AnyHit?.Invoke(this, hit);
        }

        // Every frame once dead, in case an FSM that's still running sets the agent walking again.
        private void HoldStill()
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

            if (!agent.isStopped)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            agent.velocity = Vector3.zero;
        }

        private void KnockBackFromPlayer()
        {
            var eyes = Camera.main;
            if (eyes == null) return;

            if (knockback == null && !TryGetComponent(out knockback)) knockback = gameObject.AddComponent<Knockback>();
            knockback.Push(eyes.transform.position, knockBackDistance, knockBackTime, knockBackHop, knockBackStun);
        }

        private void Send(string eventName)
        {
            if (string.IsNullOrEmpty(eventName)) return;

            foreach (var fsm in fsms)
            {
                if (fsm != null && fsm.enabled) fsm.SendEvent(eventName);
            }
        }
    }
}
