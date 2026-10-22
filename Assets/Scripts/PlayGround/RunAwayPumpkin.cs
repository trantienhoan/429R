using System;
using System.Collections.Generic;
using Game.Combat;
using Game.Enemies;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Random = UnityEngine.Random;

namespace Game.PlayGround
{
    /// <summary>
    /// Makes a stage pumpkin (Pumpkin_0, 1, 2, 4, 6) run away from the player like the laughing pumpkins (Pumpkin_9), but
    /// only for a short while: when the player comes within Flee Range it hops off to a hiding spot (behind furniture, into
    /// a bush, anywhere out of sight), using up its Stamina as it goes (Stamina Use per second: 10 makes Stamina 15 a run
    /// of 1.5 seconds). Out of stamina it stays put; it gets its breath back (Regen per second) once it has stayed still for
    /// Rest Before Regen seconds, and runs again when it's Rested Share full. Hits take stamina too (Stamina Per Damage),
    /// so hitting it tires it out.
    /// Everything else is left to its own FSMs: the Health FSM still laughs, watches the player, cracks, breaks and drops
    /// its loot; the Damage FSM still takes health on hard hits and knocks it away. It doesn't run while it grows in (Start
    /// Delay), while it's held, or once its health is gone. While it runs it can't tip over (its X/Z rotation is frozen
    /// until it has landed and settled). With Turn Away After Run (Pumpkin_1, 2, 4, 6) it turns its back to the player
    /// once a run is over and keeps it turned until it's hit or runs again.
    /// Tools > 429 Game > Items > Make Stage Pumpkins Run Away adds it with each pumpkin's stamina.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class RunAwayPumpkin : MonoBehaviour
    {
        public enum Mood { Waiting, Watching, Fleeing, Tired, Held, Done }

        [Header("Running")]
        [Tooltip("Full stamina. Running uses Stamina Use per second, so 15 = 1.5 seconds of running.")]
        [Min(0f)]
        [SerializeField] private float stamina = 20f;
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
        [Tooltip("Seconds after it appears before it may run: its Health FSM grows it in first.")]
        [Min(0f)]
        [SerializeField] private float startDelay = 3.2f;

        [Header("Player")]
        [Tooltip("When the player comes this close (metres, along the floor) it starts running.")]
        [Min(0.1f)]
        [SerializeField] private float fleeRange = 4f;
        [Tooltip("Once at its hiding spot with the player at least this far, it stops running to save its breath.")]
        [Min(0.1f)]
        [SerializeField] private float calmRange = 7f;

        [Header("After a run")]
        [Tooltip("After running away it turns its back to the player and keeps it turned, sulking, until it's hit or " +
                 "runs again (its Health FSM's Look At is overruled meanwhile). Off: it faces the player as before.")]
        [SerializeField] private bool turnAwayAfterRun;
        [Tooltip("How fast it turns its back, in degrees per second.")]
        [Min(1f)]
        [SerializeField] private float turnAwaySpeed = 240f;

        [Header("Hits")]
        [Tooltip("Something hitting it at least this much faster (metres per second) than it was moving itself takes " +
                 "stamina. Bumping into things as it hops doesn't.")]
        [Min(0f)]
        [SerializeField] private float hitSpeed = 1.5f;
        [Tooltip("How hits turn into damage (the enemies' shared Hit Settings asset); the damage comes off its stamina " +
                 "(its health is still up to its Damage FSM). Empty: the default numbers.")]
        [SerializeField] private HitSettings hitSettings;
        [Tooltip("Divides the hit power, like an enemy's toughness.")]
        [Min(0.1f)]
        [SerializeField] private float toughness = 1f;
        [Tooltip("Stamina lost per point of damage a hit does. A punch does about 2-3, so 6 makes it about 12-18 stamina.")]
        [Min(0f)]
        [SerializeField] private float staminaPerDamage = 6f;

        [Header("PlayMaker")]
        [Tooltip("The FSM whose float \"health\" breaks it at 0: it stops running for good then.")]
        [SerializeField] private string healthFsm = "Health";
        [SerializeField] private string healthVariable = "health";

        private static readonly RaycastHit[] Hits = new RaycastHit[24];

        private Rigidbody body;
        private XRGrabInteractable grab;
        private Collider[] ownColliders;
        private PlayMakerFSM health;
        private GhostRoamArea arena;
        private NavMeshPath path;

        private Mood mood = Mood.Waiting;
        private float bornAt;
        private float staminaLeft;
        private float stillFor;
        private float nextHop;
        private float nextPick;
        private float nextPath;
        private float nextHurt;
        private const float HurtCooldown = 0.3f;
        // How fast it was going just before the physics step (a hit is reported after it).
        private float speedBefore;
        private bool upright;
        private RigidbodyConstraints ownConstraints;
        private readonly List<(Vector3 point, Collider[] passThrough, float bonus)> hideSpots = new();
        private bool hasTarget;
        private Vector3 target;
        private readonly List<Vector3> corners = new();
        // Turned away after a run (Turn Away After Run): the way it faces, eased round to its back to the player.
        private bool turnedAway;
        private float awayYaw;
        private float lastHealth = float.MaxValue;

        public Mood Current => mood;
        public float StaminaLeft => staminaLeft;
        public float MaxStamina => stamina;
        public bool KeepingUpright => upright;
        public bool TurnedAway => turnedAway;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            grab = GetComponent<XRGrabInteractable>();
            ownColliders = GetComponentsInChildren<Collider>(true);
            path = new NavMeshPath();
            staminaLeft = stamina;
            foreach (var fsm in GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == healthFsm) health = fsm;
            }
        }

        private void Start()
        {
            bornAt = Time.time;
            arena = GhostRoamArea.Around(body.position);
            // Only the PlayGround's own pumpkins keep to its arena.
            if (arena != null && !arena.ContainsFlat(body.position)) arena = null;
        }

        private void OnDisable() => KeepUpright(false);

        // Its Health FSM's health (looked up each time: the FSM may only be switched on after this starts).
        private float Health()
        {
            var value = health != null ? health.FsmVariables.FindFsmFloat(healthVariable) : null;
            return value != null ? value.Value : float.MaxValue;
        }

        private void Update()
        {
            if (mood == Mood.Done) return;
            float healthNow = Health();
            if (healthNow <= 0f)
            {
                // Broken: its FSM plays the crack and the drop.
                mood = Mood.Done;
                hasTarget = false;
                KeepUpright(false);
                TurnBack();
                return;
            }
            // Hurt (its Damage FSM took health): it looks at the player again.
            if (healthNow < lastHealth - 0.01f) TurnBack();
            lastHealth = healthNow;

            if (grab != null && grab.isSelected)
            {
                if (mood != Mood.Held)
                {
                    mood = Mood.Held;
                    hasTarget = false;
                    KeepUpright(false);
                    TurnBack();
                }
                return;
            }
            // Let go.
            if (mood == Mood.Held) mood = staminaLeft >= stamina * restedShare ? Mood.Watching : Mood.Tired;

            if (mood == Mood.Waiting)
            {
                if (Time.time - bornAt < startDelay) return;
                mood = Mood.Watching;
            }

            var head = Camera.main != null ? Camera.main.transform : null;
            float distance = head != null ? Flat(head.position - body.position).magnitude : float.MaxValue;

            bool moving = Flat(body.linearVelocity).sqrMagnitude > 0.09f || !Grounded();
            stillFor = moving || mood == Mood.Fleeing ? 0f : stillFor + Time.deltaTime;
            if (mood != Mood.Fleeing && stillFor >= restBeforeRegen)
                staminaLeft = Mathf.Min(stamina, staminaLeft + regenPerSecond * Time.deltaTime);
            // Landed and settled after a run: it may tip over again like any other thing.
            if (upright && mood != Mood.Fleeing && stillFor >= 0.5f) KeepUpright(false);

            switch (mood)
            {
                case Mood.Watching:
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
                        TurnAway();
                        break;
                    }
                    bool arrived = hasTarget && Flat(target - body.position).magnitude < 0.9f;
                    bool hidden = head != null && Hidden(head.position, body.position);
                    // Very far, far and out of sight, or at its hiding spot out of sight with the player not right on it:
                    // stop and hide there, saving its breath.
                    bool safe = distance > calmRange + 3f || (distance > calmRange && hidden) || (arrived && hidden && distance > fleeRange);
                    if (safe || (arrived && distance > calmRange)) { mood = Mood.Watching; hasTarget = false; TurnAway(); break; }
                    if (!hasTarget || arrived || Time.time >= nextPick) PickHidingSpot(head);
                    break;

                case Mood.Tired:
                    if (staminaLeft >= stamina * restedShare) mood = Mood.Watching;
                    break;
            }
        }

        private void FixedUpdate()
        {
            if (mood == Mood.Fleeing && !body.isKinematic && Time.time >= nextHop && Grounded())
            {
                KeepUpright(true);
                body.linearVelocity = NextStep() * hopSpeed + Vector3.up * hopUp;
                nextHop = Time.time + 0.2f;
            }
            speedBefore = body.isKinematic ? 0f : body.linearVelocity.magnitude;
        }

        private void StartFleeing(Transform head)
        {
            mood = Mood.Fleeing;
            KeepUpright(true);
            TurnBack();
            PickHidingSpot(head);
        }

        // A run is over: it turns its back to the player (Turn Away After Run), from the way it faces now.
        private void TurnAway()
        {
            if (!turnAwayAfterRun || turnedAway) return;
            turnedAway = true;
            awayYaw = transform.eulerAngles.y;
        }

        // Hit, running again, held or broken: its Health FSM's Look At turns it to the player again.
        private void TurnBack() => turnedAway = false;

        // After its FSMs' Look At (Update), so its back stays to the player: upright, eased round, following them.
        private void LateUpdate()
        {
            if (!turnedAway) return;
            var head = Camera.main != null ? Camera.main.transform : null;
            if (head == null) return;
            var away = Flat(transform.position - head.position);
            if (away.sqrMagnitude > 0.0001f)
            {
                float wanted = Quaternion.LookRotation(away, Vector3.up).eulerAngles.y;
                awayYaw = Mathf.MoveTowardsAngle(awayYaw, wanted, turnAwaySpeed * Time.deltaTime);
            }
            var turned = Quaternion.Euler(0f, awayYaw, 0f);
            transform.rotation = turned;
            if (!body.isKinematic) body.angularVelocity = Vector3.zero;
        }

        // Running, it can't tip over: X/Z rotation frozen (its Health FSM turns it to face the player). Off again it gets
        // back the constraints it had.
        private void KeepUpright(bool on)
        {
            if (on == upright || body == null) return;
            upright = on;
            if (on)
            {
                ownConstraints = body.constraints;
                body.constraints = ownConstraints | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                if (!body.isKinematic) body.angularVelocity = new Vector3(0f, body.angularVelocity.y, 0f);
            }
            else
            {
                body.constraints = ownConstraints;
            }
        }

        // ------------------------------------------------------------------ hits

        private void OnCollisionEnter(Collision collision)
        {
            if (mood == Mood.Done || mood == Mood.Held) return;
            // Only what comes at it: bumping into things as it hops (its own speed) isn't a hit.
            float speed = collision.relativeVelocity.magnitude;
            if (speed < speedBefore + hitSpeed) return;
            var other = collision.collider;
            if (IsPlayerBody(other)) return;
            // The floor and big props (some have a Rigidbody) aren't hits: landing on them is just landing.
            if (other.bounds.size.magnitude > 3f) return;
            var otherBody = collision.rigidbody;
            bool thrownOrSwung = (otherBody != null && !otherBody.isKinematic) || other.CompareTag("Weapon");
            if (!thrownOrSwung) return;

            // Its Damage FSM may knock it away: let that play out before the next hop.
            nextHop = Mathf.Max(nextHop, Time.time + 0.6f);
            if (Time.time < nextHurt) return;
            float power = speed * WeaponPower.Of(collision) / toughness;
            var hit = (hitSettings != null ? hitSettings : HitSettings.Defaults).Evaluate(power, WeaponPower.DamageOf(collision));
            if (hit.Damage <= 0f) return;
            nextHurt = Time.time + HurtCooldown;
            LoseStamina(hit.Damage * staminaPerDamage);
            // Hit: it looks at the player again.
            TurnBack();
        }

        // Hurt: out of breath sooner. At 0 it's tired.
        private void LoseStamina(float amount)
        {
            if (amount <= 0f) return;
            staminaLeft = Mathf.Max(0f, staminaLeft - amount);
            stillFor = 0f;
            if (staminaLeft <= 0f && (mood == Mood.Fleeing || mood == Mood.Watching))
            {
                mood = Mood.Tired;
                hasTarget = false;
            }
        }

        private static bool IsPlayerBody(Collider other)
        {
            if (other.CompareTag("Weapon")) return false;
            return other.GetComponentInParent<XROrigin>() != null;
        }

        // ------------------------------------------------------------------ running and hiding (as Laughing Pumpkin)

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

            // Its favourite spots first: inside a bush or tall grass, behind a tree trunk or the giant skull.
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
                if (Hidden(player, spot)) score += 5f;                            // out of sight (behind furniture)
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

        // Spots it likes to hide in: inside bushes and tall grass big enough to hold it, behind a ghost tree's trunk.
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
            if (Mathf.Min(bounds.size.x, bounds.size.z) < width * 1.3f || bounds.size.y < width * 0.9f) return;
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
            if (Hideout.Covers(spot)) return true;
            var aim = spot + Vector3.up * 0.4f;
            var line = aim - eye;
            float length = line.magnitude;
            if (length < 0.01f) return false;
            int count = Physics.RaycastNonAlloc(eye, line / length, Hits, length - 0.3f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = Hits[i];
                if (IsOwn(hit)) continue;
                if (hit.collider.GetComponentInParent<XROrigin>() != null) continue;
                if (hit.rigidbody != null && !hit.rigidbody.isKinematic && hit.collider.bounds.size.magnitude < 1f) continue;
                return true;
            }
            return false;
        }

        // On something solid, not rising. Loose small things under it don't count, or it would hop off them in mid-air.
        private bool Grounded()
        {
            if (body.linearVelocity.y > 0.6f) return false;
            var bounds = ownColliders.Length > 0 && ownColliders[0] != null ? ownColliders[0].bounds : new Bounds(body.position, Vector3.one * 0.5f);
            int count = Physics.RaycastNonAlloc(bounds.center, Vector3.down, Hits, bounds.extents.y + 0.08f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = Hits[i];
                if (IsOwn(hit)) continue;
                if (hit.rigidbody != null && !hit.rigidbody.isKinematic && hit.collider.bounds.size.magnitude < 1.5f) continue;
                return true;
            }
            return false;
        }

        // Its own parts, also ones that turn up later (the crack model).
        private bool IsOwn(RaycastHit hit) => hit.rigidbody == body || Array.IndexOf(ownColliders, hit.collider) >= 0;

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            var last = transform.position + Vector3.forward * fleeRange;
            for (int i = 1; i <= 32; i++)
            {
                float angle = i / 32f * Mathf.PI * 2f;
                var next = transform.position + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * fleeRange;
                Gizmos.DrawLine(last, next);
                last = next;
            }
            if (hasTarget)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, target);
                Gizmos.DrawWireSphere(target, 0.3f);
            }
        }
#endif
    }
}
