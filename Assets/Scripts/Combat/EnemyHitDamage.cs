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
    /// Turns hits into damage for an enemy made in PlayMaker (or, with Furniture ticked, a breakable thing: furniture and
    /// props). Hit power = hit speed x the weapon's Power / Toughness: too weak does nothing (the enemy just flinches), a
    /// normal hit does the weapon's Damage (a punch 1, a hammer 2), a hard hit twice that and knocks it back (Hit
    /// Settings), always whole numbers. It lowers the "health" variable of the enemy's Health FSM, which
    /// already watches it, sends "Damage" (or "ShakeSmall" for weak hits) so the enemy's hit reactions play, and
    /// switches off the enemy's old "Damage" FSM so hits aren't counted twice. Where a hit of the player's hurts an enemy,
    /// a small pop shows (set in the Hit Settings asset; a big hit gets the bigger one).
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

        [Header("Mini boss")]
        [Tooltip("Enemies only: extra Toughness against knocks the player didn't make (a chair tipping over, a book " +
                 "falling, something it shoved itself, a thing a blast threw). 1 = they count like any hit. 4 = they count " +
                 "4 times weaker: one too weak to hurt doesn't even make it shake, and none knocks it back. The player's own " +
                 "hits (a fist, a thing they hold or just threw, a shot) count as before. 0 counts as 1.")]
        [Min(0f)]
        [SerializeField] private float strayToughness = 1f;

        [Header("Hit effect")]
        [Tooltip("Enemies only: how big the pop is where a hit hurts it (the pops are set in the Hit Settings asset). " +
                 "1 = normal, 1.5 = half as big again (a big enemy). 0 counts as 1.")]
        [Min(0f)]
        [SerializeField] private float hitEffectSize = 1f;
        [Tooltip("Tick for an enemy that shows its hits in its own way, or for a thing that isn't an enemy (a room door, " +
                 "the start pumpkin): no pop, and a good punch shows the fist's own puff as on any breakable.")]
        [SerializeField] private bool hideHitEffect;

        [Header("Level")]
        [Tooltip("EXP the player gets when it dies: 1 for a small enemy, 2 for a normal one, 5 for a mini boss, 50 for " +
                 "pumpkin_boss_Z. 0 = none. Tools > 429 Game > Player > Set Up Level System fills it in where it's 0.")]
        [Min(0)]
        [SerializeField] private int exp;
        [Tooltip("Little gold EXP orbs fly from it into the player's chest when it dies, one per EXP point (EXP Orbs on " +
                 "'Player Level'). For the bugs; untick it for an enemy that isn't a bug (the pumpkin boss, the big pumpkin).")]
        [SerializeField] private bool expOrbs = true;

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
        [Tooltip("A piece of furniture or a prop (a book, a pumpkin, a weapon), not an enemy: only the player's hits count in full " +
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
        // A throw counts as in the face (a headshot) this far to the front of its body (share of half its depth) and
        // this high up it (share of its height).
        private const float FaceFront = 0.35f;
        private const float FaceUp = 0.4f;
        // A bug's own knock-back (Knock Back Distance), what a combo punch's distance is measured against.
        private const float BugKnockBack = 1.5f;

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

        /// <summary>
        /// It shows its own pop where a hit hurts it (an enemy without Hide Hit Effect), so a fist shows no puff there.
        /// Room doors and the start pumpkin have Hide Hit Effect ticked: they keep the fist's puff.
        /// </summary>
        public bool ShowsOwnHitPop => !furniture && !hideHitEffect;

        /// <summary>
        /// Its death is a kill the player should feel (the kill thump): an enemy that gives EXP. Room doors and the start
        /// pumpkin give none, so breaking them doesn't thump.
        /// </summary>
        public bool CountsAsKill => !furniture && exp > 0;

        /// <summary>The EXP the player gets when it dies (set while the game runs, e.g. by the bonus round; not saved).</summary>
        public int Exp
        {
            get => exp;
            set => exp = Mathf.Max(0, value);
        }

        /// <summary>
        /// Set while the game runs (not saved): it dies without the player earning it (a Fall Edge), so no EXP and no
        /// orbs. See PlayerLevel.NoRewardFor.
        /// </summary>
        [NonSerialized] public bool noReward;

        /// <summary>
        /// Multiplies the damage it takes from hits, knock-outs and blasts (set while the game runs, not saved), e.g. 2
        /// while a bee boss's bomb ticks. 1 = as usual.
        /// </summary>
        [NonSerialized] public float damageTaken = 1f;

        /// <summary>Its Toughness (set while the game runs, e.g. for the white bug; not saved).</summary>
        public float Toughness
        {
            get => toughness;
            set => toughness = Mathf.Max(0.1f, value);
        }

        /// <summary>Gold EXP orbs fly from it when it dies: an enemy (not furniture) that gives EXP, with EXP Orbs ticked.</summary>
        public bool ShowsExpOrbs => expOrbs && !furniture && exp > 0 && !noReward;

        /// <summary>The middle of its body (its solid colliders, or its renderers), e.g. where its EXP orbs start.</summary>
        public Vector3 BodyCenter => TryGetBodyBounds(out var body) ? body.center : transform.position;

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

        private HitSettings Settings => settings != null ? settings : HitSettings.Defaults;

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
            // The hit pops are made ready once, by the first enemy, so the first hit doesn't stutter.
            if (!furniture && !hideHitEffect) Settings.WarmEffects();
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
            bool stray = false;
            // The player's hit: a fist, a thing they hold or just threw, or a shot.
            bool byPlayer = HeldThings.ByPlayer(collision);
            // Furniture takes its knocks from the player: a fist, or a thing they hold or just threw. Tumbling over,
            // landing, or a thing lying around falling or sliding into it hurts it only a little, and a chair the player
            // swings or throws wears down slowly.
            if (furniture)
            {
                if (InPlayersHands) power *= wieldedShare;
                else if (!byPlayer) power *= bumpShare;
            }
            // A mini boss shrugs off knocks the player didn't make (Stray Toughness): they hurt only when very hard, one
            // too weak to hurt doesn't even make it shake, and none knocks it back.
            else if (strayToughness > 1f && !byPlayer)
            {
                power /= strayToughness;
                stray = true;
            }
            // A thrown thing in the hit (what hit it, or this thing itself flying from a throw): a hard one counts the most.
            bool thrown = IsThrown(collision) || (furniture && WeaponPower.IsThrown(this));
            // Throw Power makes a throw count as hard sooner (a tree pumpkin bursts, a hit counts three times).
            if (thrown) power /= Game.Leveling.PlayerLevel.HardThrowShare;
            // A flying jab: the skill move, its hard hit one more (not twice).
            bool flyingFist = Fist.IsFlyingFist(collision.rigidbody);
            var hit = Taken(Settings.Evaluate(power, WeaponPower.DamageOf(collision), thrown, flyingFist), byPlayer);
            LastHit = hit;
            // Punches are the last resort (user, 2026-10-27): little damage, but every one shoves the enemy back and stuns
            // it; a flying jab shoves it much farther.
            bool punch = byPlayer && !furniture && knockBack && Fist.IsFistBall(collision.rigidbody);

            if (hit.Damage <= 0f)
            {
                if (!stray && power >= MinFlinchPower && Time.time >= nextFlinch)
                {
                    nextFlinch = Time.time + FlinchInterval;
                    Send(weakHitEvent);
                    WeakHit?.Invoke(this);
                    if (punch) PunchBack(Settings.PunchKnock(false, false), true);
                }
                return;
            }

            bool wasAlive = health.Value > 0f;
            health.Value -= hit.Damage;
            nextHit = Time.time + hitCooldown;
            // The hand behind it (a fist, or a hand holding what hit) buzzes as hard as it hurt.
            HitBuzz.Dealt(collision, hit.Damage);
            // The killing blow on an enemy: that hand gets one more, strong thump just after.
            if (wasAlive && health.Value <= 0f && CountsAsKill) HitBuzz.Killed(collision, hit.Damage);
            // Its damage number (Damage Numbers watches the health) shows big.
            if (hit.IsBig) DamageNumbers.NoteBigHit(gameObject);
            // The killing blow still plays the hit reaction (sound and sparks); the Health FSM's death state switches
            // that FSM off later this frame.
            Send(hitEvent);
            // A small pop where it hurt (enemies only; by default only the player's hits, see the Hit Settings).
            if (!furniture)
                ShowHitEffect(collision.contactCount > 0 ? collision.GetContact(0).point : collision.transform.position, hit, byPlayer);
            if (health.Value <= 0f)
            {
                Die();
                HoldStill();
            }
            // A thrown thing that hurt it on the head: a headshot (it's knocked out, instead of the big hit's knock-back).
            var at = collision.contactCount > 0 ? collision.GetContact(0).point : collision.transform.position;
            bool headshot = !furniture && !stray && Settings.Headshots && IsThrown(collision) && (IsHead(at) || IsFace(at));
            if (headshot) Headshot();
            // Any punch that hurt knocks it back (a hard one farther), for longer.
            else if (punch) PunchBack(Settings.PunchKnock(true, hit.IsBig, flyingFist), !flyingFist);
            // A big killing blow still knocks the body back: Knockback moves the agent itself, which works while it's held.
            else if (hit.IsBig && knockBack && !stray) KnockBackFromPlayer();
            AnyHit?.Invoke(this, hit);
        }

        // A punch's knock-back (Hit Settings' Punch Knock: the distance and hop as shares, the stun in seconds). Combo: the
        // fist's own punch, its distance a share of how far a flying jab reaches now (Fist.JabReach), so the jab that
        // follows still lands; an enemy heavier than a bug (a smaller Knock Back Distance, e.g. a boss) goes that much less.
        private void PunchBack(Vector3 knock, bool combo)
        {
            if (knock.x <= 0f) return;
            scaledUntil = Time.time + 0.1f;
            scaledDistance = combo ? knock.x * Fist.JabReach / BugKnockBack : knock.x;
            scaledHop = knock.y;
            scaledStun = Mathf.Max(knockBackStun, knock.z);
            KnockBackFromPlayer();
        }

        // The hit's damage times Damage Taken, and for the player's own hits plus Hot Hands' bonus (overheated hands hit
        // harder). Whole numbers.
        private HitResult Taken(HitResult hit, bool byPlayer)
        {
            if (hit.Damage <= 0f) return hit;
            float damage = Mathf.Max(1f, Mathf.Round(hit.Damage * damageTaken) + (byPlayer ? HotHands.DamageBonus : 0));
            return damage == hit.Damage ? hit : new HitResult(hit.Power, damage, hit.IsBig);
        }

        // In its face: the front third of its body, from its middle up (small bugs' head bones sit mid-body, and a throw
        // at the face lands on their front legs).
        private bool IsFace(Vector3 point)
        {
            if (!TryGetBodyBounds(out var body)) return false;
            var forward = transform.forward;
            float frontExtent = Vector3.Dot(body.extents, new Vector3(Mathf.Abs(forward.x), Mathf.Abs(forward.y), Mathf.Abs(forward.z)));
            float front = Vector3.Dot(point - body.center, forward) / Mathf.Max(0.01f, frontExtent);
            float up = (point.y - body.min.y) / Mathf.Max(0.01f, body.size.y);
            return front >= FaceFront && up >= FaceUp;
        }

        // Let go a moment ago and still flying: a throw (not a fist, not a weapon in the hand, not a shot).
        private static bool IsThrown(Collision collision)
        {
            var source = collision.rigidbody;
            return source != null && source.TryGetComponent<XRGrabInteractable>(out var thrown) && !thrown.isSelected &&
                   HeldThings.InUse(thrown);
        }

        // Its words and bonk, the bonus EXP, and the knock-out (no extra damage; a dead body is just thrown back).
        private void Headshot()
        {
            var head = FindHeadPoint();
            Settings.PlayHeadshot(head);
            if (Settings.HeadshotExp > 0f) Game.Leveling.PlayerLevel.AddExp(Settings.HeadshotExp);
            var push = Settings.HeadshotPush;
            if (dead) KnockBack(push.x, push.y);
            else KnockOut(0f, push.x, push.y, Settings.HeadshotStunSeconds);
        }

        // Where its head is (its "Head" child), or the top of its body.
        private Vector3 FindHeadPoint()
        {
            if (!headSearched)
            {
                headSearched = true;
                head = FindHead();
            }
            if (head != null) return head.position;
            return TryGetBodyBounds(out var body) ? new Vector3(body.center.x, body.max.y, body.center.z) : transform.position;
        }

        // Its health has run out, however that happened: the player gets its EXP, once (not when No Reward is set).
        private void Die()
        {
            if (dead) return;
            dead = true;
            if (exp > 0 && !noReward) Game.Leveling.PlayerLevel.AddExp(exp);
            Game.VFX.StunStars.Hide(transform);
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
        /// high, and lies stunned for this many seconds (enemies with Knock Back off only take the damage). Damage Scale
        /// multiplies its damage like a weapon's Damage (WeaponPower.DamageOf the punch, e.g. 2 under the Elon Boong).
        /// At: where the blow landed, for the big hit pop (none if not given). True if the blow did damage.
        /// </summary>
        public bool KnockOut(float power, float distanceScale, float hopScale, float stunSeconds, float damageScale = 1f,
            Vector3? at = null)
        {
            if (health == null || dead || furniture) return false;

            var hit = Taken(Settings.Evaluate(power / toughness, damageScale), true);
            bool hurt = hit.Damage > 0f;
            if (hurt)
            {
                health.Value -= hit.Damage;
                // A knock-out blow's damage number shows big, and so does its pop.
                DamageNumbers.NoteBigHit(gameObject);
                Send(hitEvent);
                if (at.HasValue) ShowHitEffect(at.Value, new HitResult(hit.Power, hit.Damage, true), true);
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

        /// <summary>
        /// Shows the hit pop at a point, e.g. where a bee's wings come off: the big one if Big. Not on furniture, nor on
        /// an enemy with Hide Hit Effect ticked.
        /// </summary>
        public void PlayHitEffect(Vector3 at, bool big)
        {
            ShowHitEffect(at, new HitResult(0f, big ? 5f : 1f, big), true);
        }

        private void ShowHitEffect(Vector3 at, HitResult hit, bool byPlayer)
        {
            if (furniture || hideHitEffect || !Settings.ShowsEffect(hit, byPlayer)) return;
            Settings.PlayEffect(at, hit, hitEffectSize > 0f ? hitEffectSize : 1f);
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
            // A mini boss knocked back and stunned sees stars until it's back on its feet.
            if (!dead && brain != null && brain.IsBoss) ShowStunned(knockBackTime + stun);
        }

        /// <summary>
        /// Stars circling over its head for this many seconds (Hit Settings' Stun Stars), e.g. a mini boss knocked back and
        /// stunned, or the pumpkin boss dizzy. Head: the bone to follow (left empty: its "Head" child, or itself). Lift and
        /// Radius, in metres: how far above the head and how wide (below 0: worked out from its body).
        /// </summary>
        public void ShowStunned(float seconds, Transform head = null, float lift = -1f, float radius = -1f)
        {
            if (lift >= 0f && radius > 0f && head != null)
            {
                if (Settings.StunStars != null && seconds > 0f && !dead)
                    Game.VFX.StunStars.Show(Settings.StunStars, transform, head, lift, radius * Settings.StunStarsSize, seconds);
                return;
            }
            var prefab = Settings.StunStars;
            if (prefab == null || seconds <= 0f || dead) return;
            if (head == null)
            {
                if (!headSearched)
                {
                    headSearched = true;
                    this.head = FindHead();
                }
                head = this.head != null ? this.head : transform;
            }
            // On its head (user, 2026-10-27): centred over the head bone, a ring as wide as the head (from its body's width
            // side to side), floating just over it. Without a head bone: over the top of its body.
            float width = 0.3f, top = transform.position.y;
            if (TryGetBodyBounds(out var body))
            {
                width = body.extents.x;
                top = body.max.y;
            }
            float ring = Mathf.Clamp(width * 0.45f, 0.14f, 0.45f) * Settings.StunStarsSize;
            float over = head != transform ? ring * 0.9f : Mathf.Max(0f, top - head.position.y) + ring * 0.35f;
            Game.VFX.StunStars.Show(prefab, transform, head, over, ring, seconds);
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
