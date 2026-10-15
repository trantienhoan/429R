using System;
using System.Collections.Generic;
using Game.Enemies;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using FsmFloat = HutongGames.PlayMaker.FsmFloat;

namespace Game.Combat
{
    /// <summary>
    /// Turns hits into damage for an enemy made in PlayMaker. Hit power = hit speed x the weapon's Power / Toughness:
    /// too weak does nothing (the enemy just flinches), a normal hit does 1-2 damage, a big hit 3-5 and knocks it back
    /// (the numbers are in the Hit Settings asset), times the weapon's Damage (a hammer more, a thrown book less). It lowers the "health" variable of the enemy's Health FSM, which
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

        [Header("Level")]
        [Tooltip("EXP the player gets when it dies: 1 for a small enemy, 2 for a normal one, 5 for a mini boss, 50 for " +
                 "pumpkin_boss_Z. 0 = none. Tools > 429 Game > Player > Set Up Level System fills it in where it's 0.")]
        [Min(0)]
        [SerializeField] private int exp;

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

        [Header("Furniture")]
        [Tooltip("A piece of furniture (or another breakable thing), not an enemy: only the player's hits count in full " +
                 "(a fist, or a thing they hold or just threw); falls, tumbles and things lying around that slide or drop " +
                 "into it count only Bump Share as hard. A launched player can land on it, the level-up burst pushes it like " +
                 "any loose thing, and it never gives EXP. Tools > 429 Game > Rooms > Set Up Breakable Furniture sets it.")]
        [SerializeField] private bool furniture;
        [Tooltip("Furniture only: how hard a bump from something the player isn't using counts (0.35 = a fall or a " +
                 "tumble hurts a little, only when it's hard).")]
        [Range(0f, 1f)]
        [SerializeField] private float bumpShare = 0.35f;
        [Tooltip("Furniture the player picks up and uses as a weapon (a chair): how hard its own knocks count while it's " +
                 "held or flying from a throw, so it lasts as a weapon (0.15 = only the hardest throws wear it down).")]
        [Range(0f, 1f)]
        [SerializeField] private float wieldedShare = 0.15f;
        [Tooltip("Furniture only: for this many seconds after it appears nothing hurts it, while the room is set up " +
                 "(it lands, and the things spawned on it drop into place).")]
        [Min(0f)]
        [SerializeField] private float settleSeconds = 2f;

        private readonly List<PlayMakerFSM> fsms = new();
        private FsmFloat health;
        private Knockback knockback;
        private NavMeshAgent agent;
        private SpiderBrain brain;
        private float nextHit;
        private float nextFlinch;
        private bool dead;
        private float scaledUntil = -1f;
        private float scaledDistance = 1f;
        private float scaledHop = 1f;
        private float scaledStun;
        private Transform head;
        private bool headSearched;
        private float settledAt;
        // Furniture the player can pick up (chairs, boxes).
        private XRGrabInteractable grab;

        // A hit this close to the head counts as on it: this share of the enemy's size, at least Min Head Reach metres.
        private const float HeadShare = 0.3f;
        private const float MinHeadReach = 0.12f;

        /// <summary>Raised after every hit that hurts an enemy: the enemy, and what the hit did.</summary>
        public static event Action<EnemyHitDamage, HitResult> AnyHit;

        /// <summary>Raised for a hit too weak to hurt (but more than a touch), at most every Flinch Interval: the enemy.</summary>
        public static event Action<EnemyHitDamage> WeakHit;
        /// <summary>An enemy's health has run out (once each), however that happened.</summary>
        public static event Action<EnemyHitDamage> Died;

        /// <summary>The most recent hit on this enemy, whether it hurt or not.</summary>
        public HitResult LastHit { get; private set; }

        /// <summary>True once its health has run out (its body may still be playing the death).</summary>
        public bool IsDead => dead || (health != null && health.Value <= 0f);

        /// <summary>A piece of furniture rather than an enemy (see Furniture in the Inspector).</summary>
        public bool IsFurniture => furniture;

        /// <summary>How far a big hit knocks it back, in metres (e.g. less for a big spider).</summary>
        public float KnockBackDistance
        {
            get => knockBackDistance;
            set => knockBackDistance = Mathf.Max(0f, value);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            AnyHit = null;
            WeakHit = null;
            Died = null;
        }

        // Its Spider Brain, if it has one: with a Stun Guard the brain decides whether a knock-back may stop it. In
        // Awake, so a knock-back in the frame it spawns (a fist, a level-up burst) already asks it.
        private void Awake()
        {
            TryGetComponent(out brain);
        }

        private void OnEnable()
        {
            settledAt = furniture ? Time.time + settleSeconds : 0f;
            if (furniture && grab == null) TryGetComponent(out grab);
        }

        /// <summary>Furniture the player is holding, or threw a moment ago: it's being used as a weapon.</summary>
        public bool InPlayersHands => HeldThings.InUse(grab);

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
                Die();
            }
            HoldStill();
        }

        // The enemy's Rigidbody gets the collisions of all its colliders, so any part of it can be hit.
        private void OnCollisionEnter(Collision collision)
        {
            if (health == null || dead || Time.time < nextHit || Time.time < settledAt) return;

            float power = collision.relativeVelocity.magnitude * WeaponPower.Of(collision) / toughness;
            // Furniture takes its knocks from the player: a fist, or a thing they hold or just threw. Tumbling over,
            // landing, or a thing lying around falling or sliding into it hurts it only a little, and a chair the player
            // swings or throws wears down slowly.
            if (furniture)
            {
                if (InPlayersHands) power *= wieldedShare;
                else if (!HeldThings.ByPlayer(collision)) power *= bumpShare;
            }
            var hit = (settings != null ? settings : HitSettings.Defaults).Evaluate(power, WeaponPower.DamageOf(collision));
            LastHit = hit;

            if (hit.Damage <= 0f)
            {
                if (power >= MinFlinchPower && Time.time >= nextFlinch)
                {
                    nextFlinch = Time.time + FlinchInterval;
                    Send(weakHitEvent);
                    WeakHit?.Invoke(this);
                }
                return;
            }

            health.Value -= hit.Damage;
            nextHit = Time.time + hitCooldown;
            // The hand behind it (a fist, or a hand holding what hit) buzzes as hard as it hurt.
            HitBuzz.Dealt(collision, hit.Damage);
            // Its damage number (Damage Numbers watches the health) shows big.
            if (hit.IsBig) DamageNumbers.NoteBigHit(gameObject);
            // The killing blow still plays the hit reaction (sound and sparks); the Health FSM's death state switches
            // that FSM off later this frame.
            Send(hitEvent);
            if (health.Value <= 0f)
            {
                Die();
                HoldStill();
            }
            // A big killing blow still knocks the body back: Knockback moves the agent itself, which works while it's held.
            if (hit.IsBig && knockBack) KnockBackFromPlayer();
            AnyHit?.Invoke(this, hit);
        }

        // Its health has run out, however that happened: the player gets its EXP, once.
        private void Die()
        {
            if (dead) return;
            dead = true;
            if (exp > 0) Game.Leveling.PlayerLevel.AddExp(exp);
            Died?.Invoke(this);
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

        /// <summary>
        /// Knocks it back away from the player as a big hit does, whatever the hit did, e.g. a fist's uppercut: this
        /// many times as far, hopping this many times as high. Not if Knock Back is off (bosses that stand their ground).
        /// </summary>
        public void KnockBack(float distanceScale = 1f, float hopScale = 1f)
        {
            if (!knockBack) return;
            // A big hit landing in the same moment keeps these, whichever of the two comes first.
            scaledUntil = Time.time + 0.1f;
            scaledDistance = distanceScale;
            scaledHop = hopScale;
            scaledStun = knockBackStun;
            KnockBackFromPlayer();
        }

        /// <summary>
        /// A knock-out blow, e.g. a fist's uppercut to the head: an extra hit of this power on top of the one that just
        /// landed (Toughness still counts), then it's knocked back this many times as far and hopping this many times as
        /// high, and lies stunned for this many seconds (enemies with Knock Back off only take the damage). True if the
        /// blow did damage.
        /// </summary>
        public bool KnockOut(float power, float distanceScale, float hopScale, float stunSeconds)
        {
            if (health == null || dead || furniture) return false;

            var hit = (settings != null ? settings : HitSettings.Defaults).Evaluate(power / toughness);
            bool hurt = hit.Damage > 0f;
            if (hurt)
            {
                health.Value -= hit.Damage;
                // A knock-out blow's damage number shows big.
                DamageNumbers.NoteBigHit(gameObject);
                Send(hitEvent);
                if (health.Value <= 0f)
                {
                    Die();
                    HoldStill();
                }
                AnyHit?.Invoke(this, hit);
            }

            if (!knockBack) return hurt;
            scaledUntil = Time.time + 0.1f;
            scaledDistance = distanceScale;
            scaledHop = hopScale;
            scaledStun = Mathf.Max(knockBackStun, stunSeconds);
            KnockBackFromPlayer();
            return hurt;
        }

        /// <summary>
        /// Whether a point on it is on its head: near its head (the child named "Head"), or, for an enemy without one,
        /// in the top or the front part of its body.
        /// </summary>
        public bool IsHead(Vector3 point)
        {
            if (!headSearched)
            {
                headSearched = true;
                head = FindHead();
            }
            if (!TryGetBodyBounds(out var body)) return head != null;

            float size = Mathf.Max(body.size.x, Mathf.Max(body.size.y, body.size.z));
            if (head != null)
            {
                float reach = Mathf.Max(MinHeadReach, size * HeadShare);
                return (point - head.position).sqrMagnitude <= reach * reach;
            }

            var forward = transform.forward;
            float frontExtent = Vector3.Dot(body.extents, new Vector3(Mathf.Abs(forward.x), Mathf.Abs(forward.y), Mathf.Abs(forward.z)));
            float front = Vector3.Dot(point - body.center, forward) / Mathf.Max(0.01f, frontExtent);
            float up = (point.y - body.min.y) / Mathf.Max(0.01f, body.size.y);
            return front > 0.4f || up > 0.7f;
        }

        private Transform FindHead()
        {
            Transform contains = null;
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t == transform) continue;
                if (string.Equals(t.name, "Head", StringComparison.OrdinalIgnoreCase)) return t;
                if (contains == null && t.name.IndexOf("head", StringComparison.OrdinalIgnoreCase) >= 0) contains = t;
            }
            return contains;
        }

        // Its solid colliders together (its renderers if it has none).
        private bool TryGetBodyBounds(out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (var c in GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger || !c.enabled) continue;
                if (any) bounds.Encapsulate(c.bounds);
                else bounds = c.bounds;
                any = true;
            }
            if (any) return true;
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (any) bounds.Encapsulate(r.bounds);
                else bounds = r.bounds;
                any = true;
            }
            return any;
        }

        private void KnockBackFromPlayer()
        {
            var eyes = Camera.main;
            if (eyes == null) return;

            bool scaled = Time.time < scaledUntil;
            float stun = scaled ? scaledStun : knockBackStun;
            // A boss with a Stun Guard shrugs it off while its guard is up (the hit's damage already counted). A killing
            // blow still throws the body.
            if (!dead && brain != null && !brain.TryStun(knockBackTime + stun)) return;
            if (knockback == null && !TryGetComponent(out knockback)) knockback = gameObject.AddComponent<Knockback>();
            knockback.Push(eyes.transform.position, knockBackDistance * (scaled ? scaledDistance : 1f), knockBackTime,
                knockBackHop * (scaled ? scaledHop : 1f), stun);
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
