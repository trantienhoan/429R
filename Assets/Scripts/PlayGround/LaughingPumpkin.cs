using System;
using System.Collections.Generic;
using Game.Combat;
using Game.Enemies;
using Game.Weapons;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using FsmFloat = HutongGames.PlayMaker.FsmFloat;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;
using Random = UnityEngine.Random;

namespace Game.PlayGround
{
    /// <summary>
    /// The laughing pumpkin the cauldron spits out: a catch-me game.
    /// - Fresh out of the pot it just sits there laughing. Get close (Catch Range) and it loses 1 health and hops away.
    /// - From then on it runs and hides once the player comes within Flee Range: inside a bush (its own, or the bushes
    ///   round a tree's trunk), behind a tree or the giant skull, or anywhere out of the player's sight, dropping candy
    ///   (its Candy Trail) as it goes. Running uses Stamina (Stamina Use per second, e.g. 60 = 6 s).
    /// - Out of stamina it stops and laughs weakly (tired): get close again and it breaks. It gets its breath back
    ///   (Regen per second) only after staying still for Rest Before Regen seconds, and only runs again once it's
    ///   Rested Share full.
    /// - Cornering it within Catch Range for a while breaks it. Holding it (close range only, see Near Grab Only) for a
    ///   while makes it go off in the hand: an explosion that costs the player a lot of health.
    /// - Every break is a small explosion, just big enough to reach the hand holding it: only a player holding it is hurt.
    /// - Hitting it or throwing things at it never breaks it: it hops away, and the hit's damage (worked out as for an
    ///   enemy, see Hit Settings) comes off its stamina instead, so beating on it tires it out. Blasts too.
    /// With Can Run off it's the big tanky one: it stays put, laughs, takes damage from hits (Enemy Hit Damage lowers its
    /// Health FSM) and throws out a candy for every Candy Every Health it loses.
    /// Breaking is left to its Health FSM (health 0 -> crack, drop, destroy); Caught / On Caught fire when it breaks.
    /// Tools > 429 Game > PlayGround > Set Up Laughing Pumpkins adds it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class LaughingPumpkin : MonoBehaviour, IHittable
    {
        public enum Mood { Fresh, Watching, Fleeing, Tired, Held, Broken }

        private const string SpawnedStuffVariable = "CurrentlySpawnedStuffs";

        [Header("Running")]
        [Tooltip("Off = the big tanky pumpkin: it never runs, it takes damage from hits instead.")]
        [SerializeField] private bool canRun = true;
        [Tooltip("Full stamina. Running uses Stamina Use per second, so 60 = 6 seconds of running.")]
        [Min(0f)]
        [SerializeField] private float stamina = 60f;
        [Min(0.1f)]
        [SerializeField] private float staminaUsePerSecond = 10f;
        [Tooltip("Stamina back per second while resting.")]
        [Min(0f)]
        [SerializeField] private float regenPerSecond = 1f;
        [Tooltip("It has to stay still this long, in seconds, before it gets any stamina back.")]
        [Min(0f)]
        [SerializeField] private float restBeforeRegen = 3f;
        [Tooltip("Once tired, it only runs again when its stamina is back up to this share of full.")]
        [Range(0f, 1f)]
        [SerializeField] private float restedShare = 0.5f;
        [Tooltip("Sideways speed of each hop, in metres per second.")]
        [Min(0.1f)]
        [SerializeField] private float hopSpeed = 3f;
        [Tooltip("Upward speed of each hop, in metres per second (2.4 = about half a second in the air).")]
        [Min(0.1f)]
        [SerializeField] private float hopUp = 2.4f;

        [Header("Player")]
        [Tooltip("This close (metres, along the floor) counts as catching it.")]
        [Min(0.1f)]
        [SerializeField] private float catchRange = 1.3f;
        [Tooltip("When the player comes this close it starts running (more than Catch Range).")]
        [Min(0.1f)]
        [SerializeField] private float fleeRange = 4f;
        [Tooltip("Once at its hiding spot with the player at least this far, it stops running to save its breath.")]
        [Min(0.1f)]
        [SerializeField] private float calmRange = 7f;
        [Tooltip("Cornered within Catch Range this long (seconds) while it can still run: it breaks anyway.")]
        [Min(0f)]
        [SerializeField] private float cornerSeconds = 1.5f;
        [Tooltip("Held in a hand this long (seconds): it breaks.")]
        [Min(0f)]
        [SerializeField] private float holdSeconds = 1.5f;

        [Header("Hits")]
        [Tooltip("Something hitting it at least this fast (metres per second) makes it hop away.")]
        [Min(0f)]
        [SerializeField] private float hitSpeed = 1.5f;
        [Tooltip("How hard a hit knocks it away: sideways and upward speed, in metres per second.")]
        [SerializeField] private Vector2 knock = new(3.5f, 3f);
        [Tooltip("How hits turn into damage (the enemies' shared Hit Settings asset); the damage comes off its stamina. " +
                 "Empty: the default numbers.")]
        [SerializeField] private HitSettings hitSettings;
        [Tooltip("Divides the hit power, like an enemy's toughness.")]
        [Min(0.1f)]
        [SerializeField] private float toughness = 1f;
        [Tooltip("Stamina lost per point of damage a hit (or a blast: grenade, bottle) does. Enemy damage is about 1-8 a " +
                 "hit, so 6 makes a punch take about 12-18 stamina and a big hammer hit about 48.")]
        [Min(0f)]
        [SerializeField] private float staminaPerDamage = 6f;

        [Header("Exploding")]
        [Tooltip("It explodes whenever it breaks (caught, cornered, tired out, its health gone, or held too long). Only a " +
                 "player holding it is hurt (Hand Blast Event to their Damage FSM); things close by are hurt and pushed.")]
        [SerializeField] private GameObject[] handBlastEffects = Array.Empty<GameObject>();
        [SerializeField] private AudioClip handBlastSound;
        [Tooltip("Size of the effects (the bottle grenade's are 1).")]
        [Min(0.05f)] [SerializeField] private float handBlastEffectScale = 0.5f;
        [Tooltip("Metres: just big enough to reach the hand holding it.")]
        [Min(0.1f)] [SerializeField] private float handBlastRadius = 0.6f;
        [Tooltip("Health it takes from things in reach (bugs, breakables), like a grenade's damage.")]
        [Min(0f)] [SerializeField] private float handBlastDamage = 1f;
        [Min(0f)] [SerializeField] private float handBlastPush = 3f;
        [Tooltip("Sent to the player's Damage FSM when it explodes in their hand: DamageHeavy (40-50 health) or Damage (a bite).")]
        [SerializeField] private string handBlastEvent = "DamageHeavy";

        [Header("Candy (the big one)")]
        [Tooltip("Throws out one candy for every this much health it loses. 0 = none.")]
        [Min(0f)]
        [SerializeField] private float candyEveryHealth = 5f;
        [Tooltip("Candies to throw out; one picked at random each time.")]
        [SerializeField] private GameObject[] candies = Array.Empty<GameObject>();
        [Tooltip("How fast a thrown-out candy flies up, in metres per second.")]
        [Min(0f)]
        [SerializeField] private float candyLaunchSpeed = 3.5f;

        [Header("Reward")]
        [Tooltip("EXP the player gets when it breaks: caught, cornered, tired out, its health gone, or going off in their hand.")]
        [Min(0)]
        [SerializeField] private int exp = 5;

        [Header("Sounds")]
        [SerializeField] private AudioClip[] laughs = Array.Empty<AudioClip>();
        [Tooltip("Played when it's caught the first time, hit, or grabbed.")]
        [SerializeField] private AudioClip[] hitSounds = Array.Empty<AudioClip>();
        [Tooltip("Seconds between laughs (min, max).")]
        [SerializeField] private Vector2 laughEvery = new(1.4f, 2.8f);
        [Tooltip("Seconds between laughs when it's tired (min, max): slower, lower and quieter.")]
        [SerializeField] private Vector2 tiredLaughEvery = new(2.8f, 4.5f);
        [Range(0.3f, 2f)]
        [SerializeField] private float tiredPitch = 0.78f;
        [Range(0f, 1f)]
        [SerializeField] private float tiredVolume = 0.5f;
        [Range(0.3f, 2f)]
        [SerializeField] private float runPitch = 1.15f;

        [Header("PlayMaker")]
        [Tooltip("The FSM that breaks it when its float \"health\" reaches 0.")]
        [SerializeField] private string healthFsm = "Health";
        [SerializeField] private string healthVariable = "health";
        [Tooltip("The old FSM that took health when the player touched it; switched off (this script decides now).")]
        [SerializeField] private string oldDamageFsm = "Damage";

        [Header("Events")]
        public UnityEvent onCaught = new();

        /// <summary>Fired when any laughing pumpkin breaks (caught, held, or its health ran out).</summary>
        public static event Action<LaughingPumpkin> Caught;

        private static readonly List<LaughingPumpkin> all = new();
        public static IReadOnlyList<LaughingPumpkin> All => all;

        private Rigidbody body;
        private XRGrabInteractable grab;
        private AudioSource voice;
        private Collider[] ownColliders;
        private FsmFloat health;
        private GhostRoamArea arena;
        private NavMeshPath path;

        private Mood mood = Mood.Fresh;
        private float staminaLeft;
        private float stillFor;
        private float cornered;
        private float held;
        private float nextLaugh;
        private float nextHop;
        private float nextPick;
        private float nextPath;
        private float lastHealth;
        private float lostForCandy;
        private float nextHurt;
        private const float HurtCooldown = 0.3f;
        private readonly List<(Vector3 point, Collider[] passThrough, float bonus)> hideSpots = new();
        private bool hasTarget;
        private Vector3 target;
        private Vector3 faceDirection;
        private readonly List<Vector3> corners = new();

        public Mood Current => mood;
        public float StaminaLeft => staminaLeft;
        public float MaxStamina => stamina;
        public bool CanRun => canRun;
        public bool IsBroken => mood == Mood.Broken;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            grab = GetComponent<XRGrabInteractable>();
            voice = GetComponent<AudioSource>();
            ownColliders = GetComponentsInChildren<Collider>(true);
            path = new NavMeshPath();
            staminaLeft = stamina;

            foreach (var fsm in GetComponents<PlayMakerFSM>())
            {
                if (!string.IsNullOrEmpty(oldDamageFsm) && fsm.FsmName == oldDamageFsm) fsm.enabled = false;
                if (fsm.FsmName == healthFsm) health = fsm.FsmVariables.FindFsmFloat(healthVariable);
            }

            if (grab != null)
            {
                grab.selectEntered.AddListener(OnGrabbed);
                grab.selectExited.AddListener(OnReleased);
            }
        }

        private void OnEnable() => all.Add(this);
        private void OnDisable() => all.Remove(this);

        private void Start()
        {
            // The small ones: 2 health = caught once (hops away), then caught again (breaks).
            if (canRun && health != null) health.Value = 2f;
            lastHealth = health != null ? health.Value : 0f;
            arena = GhostRoamArea.Around(body.position);
            if (!canRun) mood = Mood.Watching;
            nextLaugh = Time.time + Random.Range(0.2f, 0.8f);
            faceDirection = transform.forward;
            // So the first blast doesn't stutter.
            Blast.Warm(handBlastEffects);
        }

        private void Update()
        {
            if (mood == Mood.Broken) return;
            WatchHealth();
            if (mood == Mood.Broken) return;

            var head = Camera.main != null ? Camera.main.transform : null;
            float distance = head != null ? Flat(head.position - body.position).magnitude : float.MaxValue;
            if (head != null && mood != Mood.Fleeing) faceDirection = Flat(head.position - body.position);

            bool moving = Flat(body.linearVelocity).sqrMagnitude > 0.09f || !Grounded();
            stillFor = moving || mood == Mood.Fleeing ? 0f : stillFor + Time.deltaTime;
            if (canRun && mood != Mood.Fleeing && stillFor >= restBeforeRegen)
                staminaLeft = Mathf.Min(stamina, staminaLeft + regenPerSecond * Time.deltaTime);

            switch (mood)
            {
                case Mood.Fresh:
                    if (distance <= catchRange) FirstCatch(head);
                    break;

                case Mood.Watching:
                    if (!canRun) break;
                    if (CorneredTooLong(distance)) { Break(); return; }
                    if (distance <= fleeRange)
                    {
                        if (staminaLeft >= Mathf.Max(1f, stamina * 0.1f)) StartFleeing(head);
                        else mood = Mood.Tired;
                    }
                    break;

                case Mood.Fleeing:
                    staminaLeft -= staminaUsePerSecond * Time.deltaTime;
                    if (staminaLeft <= 0f)
                    {
                        staminaLeft = 0f;
                        mood = Mood.Tired;
                        hasTarget = false;
                        Laugh();
                        break;
                    }
                    if (CorneredTooLong(distance)) { Break(); return; }
                    bool arrived = hasTarget && Flat(target - body.position).magnitude < 0.9f;
                    bool hidden = head != null && Hidden(head.position, body.position);
                    // Very far, far and out of sight, or at its hiding spot (in a bush, behind a tree) out of sight with
                    // the player not right on it: stop and hide there, saving its breath.
                    bool safe = distance > calmRange + 3f || (distance > calmRange && hidden) || (arrived && hidden && distance > fleeRange);
                    if (safe || (arrived && distance > calmRange)) { mood = Mood.Watching; hasTarget = false; break; }
                    if (!hasTarget || arrived || Time.time >= nextPick) PickHidingSpot(head);
                    break;

                case Mood.Tired:
                    if (distance <= catchRange) { Break(); return; }
                    if (staminaLeft >= stamina * restedShare) mood = Mood.Watching;
                    break;

                case Mood.Held:
                    held += Time.deltaTime;
                    if (held >= holdSeconds) { ExplodeInHand(); return; }
                    break;
            }

            if (Time.time >= nextLaugh) Laugh();
        }

        private void FixedUpdate()
        {
            if (mood == Mood.Broken || mood == Mood.Held || body.isKinematic) return;

            if (mood == Mood.Fleeing && Time.time >= nextHop && Grounded())
            {
                var step = NextStep();
                faceDirection = step;
                body.linearVelocity = step * hopSpeed + Vector3.up * hopUp;
                nextHop = Time.time + 0.2f;
            }

            // Stay upright, turned to face the player (or where it's running).
            if (faceDirection.sqrMagnitude > 0.001f)
            {
                var want = Quaternion.LookRotation(faceDirection.normalized, Vector3.up);
                body.MoveRotation(Quaternion.RotateTowards(body.rotation, want, 540f * Time.fixedDeltaTime));
            }
        }

        // ------------------------------------------------------------------ moods

        private void FirstCatch(Transform head)
        {
            if (health != null) health.Value = Mathf.Max(1f, health.Value - 1f);
            lastHealth = health != null ? health.Value : 0f;
            Knock(head != null ? Flat(body.position - head.position) : -transform.forward);
            Play(hitSounds, 1f, 1f);
            mood = Mood.Watching;
        }

        private void StartFleeing(Transform head)
        {
            mood = Mood.Fleeing;
            cornered = 0f;
            PickHidingSpot(head);
            Laugh();
        }

        private bool CorneredTooLong(float distance)
        {
            cornered = distance <= catchRange ? cornered + Time.deltaTime : 0f;
            return cornered >= cornerSeconds;
        }

        /// <summary>Breaks it: it explodes, and its Health FSM plays the crack and the drop.</summary>
        public void Break()
        {
            if (mood == Mood.Broken) return;
            bool inHand = LetGo();
            if (health != null) health.Value = 0f;
            Broke(inHand);
        }

        /// <summary>
        /// Held too long: it goes off in the player's hand. The explosion hurts them a lot and anything close, and the
        /// pumpkin is gone (that still counts as caught).
        /// </summary>
        public void ExplodeInHand()
        {
            if (mood == Mood.Broken) return;
            LetGo();
            Broke(true);

            // Gone in the blast: no crack, no leftovers.
            foreach (var shown in GetComponentsInChildren<Renderer>()) shown.enabled = false;
            foreach (var part in ownColliders) if (part != null) part.enabled = false;
            foreach (var fsm in GetComponents<PlayMakerFSM>()) fsm.enabled = false;
            if (TryGetComponent<Game.Inventory.CandyTrail>(out var trail)) trail.enabled = false;
            Destroy(gameObject, 0.1f);
        }

        // Out of the player's hand for good (no grabbing it again); returns whether a hand was holding it.
        private bool LetGo()
        {
            bool inHand = mood == Mood.Held || (grab != null && grab.isSelected);
            if (grab != null && grab.isSelected && grab.interactionManager != null)
                grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
            if (grab != null) grab.enabled = false;
            return inHand;
        }

        // It breaks, exploding: only a player holding it ('inHand') is hurt; things close by are hurt and pushed.
        private void Broke(bool inHand)
        {
            if (mood == Mood.Broken) return;
            var center = ownColliders.Length > 0 && ownColliders[0] != null && ownColliders[0].enabled ? ownColliders[0].bounds.center : body.position;
            mood = Mood.Broken;
            hasTarget = false;
            if (!body.isKinematic) body.linearVelocity = new Vector3(0f, Mathf.Min(0f, body.linearVelocity.y), 0f);
            Blast.Explode(center, handBlastRadius, handBlastDamage, handBlastPush, body, handBlastEffects, handBlastSound,
                1f, inHand ? handBlastEvent : null, held: inHand, effectScale: handBlastEffectScale);
            if (exp > 0) Game.Leveling.PlayerLevel.AddExp(exp);
            onCaught.Invoke();
            Caught?.Invoke(this);
        }

        private void WatchHealth()
        {
            if (health == null) return;
            float now = health.Value;
            if (now < lastHealth)
            {
                float lost = lastHealth - now;
                if (!canRun)
                {
                    Hop(Vector3.zero, 1.6f);
                    Play(hitSounds, 1f, 1f);
                    if (candyEveryHealth > 0f)
                    {
                        lostForCandy += lost;
                        while (lostForCandy >= candyEveryHealth)
                        {
                            lostForCandy -= candyEveryHealth;
                            LaunchCandy();
                        }
                    }
                }
            }
            lastHealth = now;
            if (now <= 0f) Broke(LetGo());
        }

        // ------------------------------------------------------------------ grabbing

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            if (mood == Mood.Broken) return;
            if (mood == Mood.Fresh && health != null) health.Value = Mathf.Max(1f, health.Value - 1f);
            mood = Mood.Held;
            held = 0f;
            hasTarget = false;
            Play(hitSounds, 1f, runPitch);
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            if (mood != Mood.Held) return;
            mood = staminaLeft >= stamina * restedShare ? Mood.Watching : Mood.Tired;
        }

        // ------------------------------------------------------------------ hits

        private void OnCollisionEnter(Collision collision)
        {
            if (mood == Mood.Broken || mood == Mood.Held || !canRun) return;
            if (collision.relativeVelocity.magnitude < hitSpeed) return;
            var other = collision.collider;
            if (IsPlayerBody(other)) return;
            // The floor and big props (some have a Rigidbody) aren't hits: landing on them is just landing.
            if (other.bounds.size.magnitude > 3f) return;
            var otherBody = collision.rigidbody;
            bool thrownOrSwung = (otherBody != null && !otherBody.isKinematic) || other.CompareTag("Weapon");
            if (!thrownOrSwung) return;

            // The hit's damage, worked out as for an enemy, comes off its stamina.
            if (Time.time >= nextHurt)
            {
                float power = collision.relativeVelocity.magnitude * WeaponPower.Of(collision) / toughness;
                var hit = (hitSettings != null ? hitSettings : HitSettings.Defaults).Evaluate(power, WeaponPower.DamageOf(collision));
                if (hit.Damage > 0f)
                {
                    LoseStamina(hit.Damage * staminaPerDamage);
                    nextHurt = Time.time + HurtCooldown;
                    // The hand holding what hit it buzzes as hard as it hurt.
                    HitBuzz.Dealt(collision, hit.Damage);
                }
            }

            var away = Flat(body.position - collision.GetContact(0).point);
            if (away.sqrMagnitude < 0.0001f) away = Flat(collision.relativeVelocity);
            Knock(away);
            Play(hitSounds, 1f, 1f);
            if (mood == Mood.Fresh) mood = Mood.Watching;
        }

        /// <summary>A blast (grenade, bottle): its damage times Stamina Per Damage comes off its stamina, and it's knocked away.</summary>
        public void Hit(float strength, Vector3 point)
        {
            if (mood == Mood.Broken || mood == Mood.Held || !canRun) return;
            LoseStamina(strength * staminaPerDamage);
            Knock(Flat(body.position - point));
            Play(hitSounds, 1f, 1f);
            if (mood == Mood.Fresh) mood = Mood.Watching;
        }

        // Hurt: out of breath sooner. At 0 it's tired (never broken by this).
        private void LoseStamina(float amount)
        {
            if (!canRun || amount <= 0f) return;
            staminaLeft = Mathf.Max(0f, staminaLeft - amount);
            stillFor = 0f;
            if (staminaLeft <= 0f && (mood == Mood.Fleeing || mood == Mood.Watching))
            {
                mood = Mood.Tired;
                hasTarget = false;
                nextLaugh = Time.time + 0.6f;
            }
        }

        private static bool IsPlayerBody(Collider other)
        {
            if (other.CompareTag("Weapon")) return false;
            return other.GetComponentInParent<XROrigin>() != null;
        }

        private void Knock(Vector3 away)
        {
            away = away.sqrMagnitude > 0.0001f ? away.normalized : Random.insideUnitCircle.normalized.X0Y();
            if (!body.isKinematic) body.linearVelocity = away * knock.x + Vector3.up * knock.y;
            nextHop = Time.time + 0.35f;
        }

        private void Hop(Vector3 sideways, float up)
        {
            if (body.isKinematic || !Grounded()) return;
            body.linearVelocity = sideways + Vector3.up * up;
        }

        // ------------------------------------------------------------------ running and hiding

        private Vector3 NextStep()
        {
            var here = body.position;
            if (!hasTarget) return Random.insideUnitCircle.normalized.X0Y();

            if (Time.time >= nextPath)
            {
                nextPath = Time.time + 0.4f;
                corners.Clear();
                if (NavMesh.SamplePosition(here, out var from, 1.5f, NavMesh.AllAreas) &&
                    NavMesh.CalculatePath(from.position, target, NavMesh.AllAreas, path) &&
                    path.status != NavMeshPathStatus.PathInvalid)
                {
                    corners.AddRange(path.corners);
                }
            }

            var toward = target;
            foreach (var corner in corners)
            {
                if (Flat(corner - here).magnitude > 0.5f) { toward = corner; break; }
            }
            var step = Flat(toward - here);
            if (step.sqrMagnitude < 0.0001f) step = Random.insideUnitCircle.X0Y();
            // A little wobble so it doesn't move like a robot.
            return Quaternion.Euler(0f, Random.Range(-12f, 12f), 0f) * step.normalized;
        }

        private void PickHidingSpot(Transform head)
        {
            nextPick = Time.time + 1.6f;
            var here = body.position;
            var player = head != null ? head.position : here - transform.forward * 3f;
            var awayFromPlayer = Flat(here - player);
            if (awayFromPlayer.sqrMagnitude < 0.01f) awayFromPlayer = Random.insideUnitCircle.X0Y();
            awayFromPlayer.Normalize();

            float bestScore = float.MinValue;
            Vector3 best = here + awayFromPlayer * 4f;
            Collider[] bestPassThrough = null;
            bool found = false;

            // Its favourite spots first: inside a bush, behind a tree trunk or the giant skull (the things that grow back).
            var toPlayerNow = Flat(player - here).normalized;
            foreach (var (point, passThrough, bonus) in HideSpots(player))
            {
                float far = Flat(point - here).magnitude;
                if (far > 12f) continue;
                if (arena != null && !arena.ContainsFlat(point)) continue;
                float fromPlayer = Flat(point - player).magnitude;
                if (fromPlayer < fleeRange) continue;                                   // not right next to the player
                float score = fromPlayer + bonus;
                if (Vector3.Dot(Flat(point - here).normalized, toPlayerNow) > 0.3f && far > 1f) score -= 8f;   // don't run past the player
                score += Random.Range(0f, 1.5f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = point;
                    bestPassThrough = passThrough;
                    found = true;
                }
            }

            for (int i = 0; i < 20; i++)
            {
                // Mostly away from the player, sometimes off to the side.
                float angle = Random.Range(-120f, 120f);
                var direction = Quaternion.Euler(0f, angle, 0f) * awayFromPlayer;
                var spot = here + direction * Random.Range(3f, 9f);
                if (arena != null && !arena.ContainsFlat(spot)) continue;
                if (NavMesh.SamplePosition(spot, out var onMesh, 1.5f, NavMesh.AllAreas)) spot = onMesh.position;
                else if (NavMesh.SamplePosition(here, out _, 1.5f, NavMesh.AllAreas)) continue;   // there is a NavMesh, just not there

                float score = Flat(spot - player).magnitude;                       // far from the player
                if (Hidden(player, spot)) score += 5f;                            // out of sight
                var toSpot = Flat(spot - here).normalized;
                var toPlayer = Flat(player - here).normalized;
                if (Vector3.Dot(toSpot, toPlayer) > 0.3f) score -= 8f;            // don't run past the player
                score += Random.Range(0f, 1.5f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = spot;
                    bestPassThrough = null;
                    found = true;
                }
            }
            target = found ? best : here + awayFromPlayer * 3f;
            hasTarget = true;
            nextPath = 0f;

            // Going into a bush: it can pass through the bush's leaves.
            if (bestPassThrough != null)
            {
                foreach (var leaves in bestPassThrough)
                {
                    foreach (var own in ownColliders)
                    {
                        if (leaves != null && own != null) Physics.IgnoreCollision(own, leaves);
                    }
                }
            }
        }

        // Spots it likes to hide in: inside bushes (ghost bushes, and the bushes round a ghost tree's trunk) big enough to
        // hold it, and behind a ghost tree's trunk or the giant skull, on the side away from the player.
        private List<(Vector3 point, Collider[] passThrough, float bonus)> HideSpots(Vector3 player)
        {
            hideSpots.Clear();
            var mine = ownColliders.Length > 0 && ownColliders[0] != null ? ownColliders[0].bounds : new Bounds(body.position, Vector3.one * 0.7f);
            float width = Mathf.Max(mine.size.x, mine.size.z);

            foreach (var bush in FindObjectsByType<GhostBush>(FindObjectsSortMode.None))
            {
                var leaves = Solid(bush.GetComponentsInChildren<Collider>());
                AddBush(leaves, width);
            }
            // Tall grass (hideouts): it walks right in; inside, it can't be seen.
            foreach (var hideout in Hideout.All)
            {
                if (hideout == null || !hideout.Covering || hideout.Width < width * 1.3f || hideout.Height < width * 0.9f) continue;
                if (!NavMesh.SamplePosition(hideout.Spot, out var floor, 0.8f, NavMesh.AllAreas)) continue;
                hideSpots.Add((floor.position, null, 8f));
            }
            foreach (var tree in FindObjectsByType<GhostTree>(FindObjectsSortMode.None))
            {
                var trunk = Solid(tree.GetComponents<Collider>());
                if (trunk.Length > 0)
                {
                    var bounds = Encapsulate(trunk);
                    var away = Flat(bounds.center - player);
                    if (away.sqrMagnitude > 0.01f)
                    {
                        float radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
                        var behind = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) + away.normalized * (radius + width * 0.6f + 0.2f);
                        if (NavMesh.SamplePosition(behind, out var onMesh, 1f, NavMesh.AllAreas)) behind = onMesh.position;
                        hideSpots.Add((behind, null, 6f));
                    }
                }
                // The bushes round its trunk.
                foreach (var part in tree.GetComponentsInChildren<Collider>())
                {
                    if (part.transform == tree.transform || part.isTrigger) continue;
                    AddBush(new[] { part }, width);
                }
            }
            return hideSpots;
        }

        private void AddBush(Collider[] leaves, float width)
        {
            if (leaves.Length == 0) return;
            var bounds = Encapsulate(leaves);
            // Too small to hide in (e.g. the mini skull).
            if (Mathf.Min(bounds.size.x, bounds.size.z) < width * 1.3f || bounds.size.y < width * 0.9f) return;
            // Only bushes standing on the floor (not leaves up in a tree's crown).
            var inside = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            if (!NavMesh.SamplePosition(inside, out var floor, 0.8f, NavMesh.AllAreas) || Mathf.Abs(floor.position.y - bounds.min.y) > 0.5f) return;
            hideSpots.Add((floor.position, leaves, 8f));
        }

        private static Collider[] Solid(Collider[] colliders) => Array.FindAll(colliders, c => c != null && c.enabled && !c.isTrigger);

        private static Bounds Encapsulate(Collider[] colliders)
        {
            var bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++) bounds.Encapsulate(colliders[i].bounds);
            return bounds;
        }

        // Is 'spot' out of the player's line of sight (something solid in between)?
        private bool Hidden(Vector3 eye, Vector3 spot)
        {
            // In tall grass nothing solid stands in the way, but it can't be seen there.
            if (Hideout.Covers(spot)) return true;
            var aim = spot + Vector3.up * 0.4f;
            var line = aim - eye;
            float length = line.magnitude;
            if (length < 0.01f) return false;
            foreach (var hit in Physics.RaycastAll(eye, line / length, length - 0.3f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (Array.IndexOf(ownColliders, hit.collider) >= 0) continue;
                if (hit.collider.GetComponentInParent<XROrigin>() != null) continue;
                if (hit.rigidbody != null && !hit.rigidbody.isKinematic && hit.collider.bounds.size.magnitude < 1f) continue;
                return true;
            }
            return false;
        }

        // On something solid, not rising. Loose things under it (its own candy trail, a ball) don't count, or it would
        // hop off them in mid-air and climb.
        private bool Grounded()
        {
            if (body.linearVelocity.y > 0.6f) return false;
            var bounds = ownColliders.Length > 0 && ownColliders[0] != null ? ownColliders[0].bounds : new Bounds(body.position, Vector3.one * 0.5f);
            var from = bounds.center;
            float reach = bounds.extents.y + 0.08f;
            foreach (var hit in Physics.RaycastAll(from, Vector3.down, reach, ~0, QueryTriggerInteraction.Ignore))
            {
                if (Array.IndexOf(ownColliders, hit.collider) >= 0) continue;
                if (hit.rigidbody != null && !hit.rigidbody.isKinematic && hit.collider.bounds.size.magnitude < 1.5f) continue;
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ candy and sound

        private void LaunchCandy()
        {
            if (candies.Length == 0) return;
            var prefab = candies[Random.Range(0, candies.Length)];
            if (prefab == null) return;
            var bounds = ownColliders.Length > 0 && ownColliders[0] != null ? ownColliders[0].bounds : new Bounds(body.position, Vector3.one);
            var at = new Vector3(bounds.center.x, bounds.max.y + 0.3f, bounds.center.z);
            var candy = Instantiate(prefab, at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), SpawnedStuff());
            foreach (var candyCollider in candy.GetComponentsInChildren<Collider>())
            {
                foreach (var own in ownColliders)
                {
                    if (own != null) Physics.IgnoreCollision(candyCollider, own);
                }
            }
            if (candy.TryGetComponent<Rigidbody>(out var candyBody))
                candyBody.linearVelocity = Random.insideUnitCircle.normalized.X0Y() * Random.Range(0.8f, 1.6f) + Vector3.up * candyLaunchSpeed;
        }

        private void Laugh()
        {
            bool tired = mood == Mood.Tired;
            var every = tired ? tiredLaughEvery : laughEvery;
            nextLaugh = Time.time + Random.Range(every.x, every.y);
            float pitch = tired ? tiredPitch : mood == Mood.Fleeing || mood == Mood.Held ? runPitch : 1f;
            Play(laughs, tired ? tiredVolume : 1f, pitch * Random.Range(0.95f, 1.05f));
        }

        private void Play(AudioClip[] clips, float volume, float pitch)
        {
            if (voice == null || clips.Length == 0) return;
            var clip = clips[Random.Range(0, clips.Length)];
            if (clip == null) return;
            voice.pitch = pitch;
            voice.PlayOneShot(clip, volume);
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        private static Transform SpawnedStuff()
        {
            var variable = FsmVariables.GlobalVariables.FindFsmGameObject(SpawnedStuffVariable);
            return variable != null && variable.Value != null ? variable.Value.transform : null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            DrawCircle(transform.position, catchRange);
            Gizmos.color = Color.yellow;
            DrawCircle(transform.position, fleeRange);
            if (hasTarget)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, target);
                Gizmos.DrawWireSphere(target, 0.3f);
            }
        }

        private static void DrawCircle(Vector3 center, float radius)
        {
            var last = center + Vector3.forward * radius;
            for (int i = 1; i <= 32; i++)
            {
                float angle = i / 32f * Mathf.PI * 2f;
                var next = center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
                Gizmos.DrawLine(last, next);
                last = next;
            }
        }
#endif
    }

    internal static class LaughingPumpkinVectors
    {
        public static Vector3 X0Y(this Vector2 v) => new(v.x, 0f, v.y);
    }
}
