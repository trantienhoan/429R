using System;
using System.Collections.Generic;
using Game.Bonus;
using Game.Combat;
using Game.Enemies;
using HutongGames.PlayMaker.Actions;
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
    /// of 1.5 seconds). Out of stamina it's stunned (Tired) for Tired Seconds, then gets its full stamina back at once and
    /// can run again (user, 2026-10-29). Short of that, resting still for Rest Before Regen seconds gets breath back slowly
    /// (Regen per second). Hits take stamina too (Stamina Per Damage), so hitting it tires it out.
    /// Everything else is left to its own FSMs: the Health FSM still watches the player, cracks, breaks and drops its loot
    /// (its laugh is taken over: see Laugh); the Damage FSM still takes health on hard hits and knocks it away. It doesn't run while it grows in (Start
    /// Delay), while it's held, or once its health is gone. While it runs it can't tip over (its X/Z rotation is frozen
    /// until it has landed and settled). With Turn Away After Run (Pumpkin_1, 2, 4, 6) it turns its back to the player
    /// once a run is over and keeps it turned until it's hit or runs again.
    /// Hide and seek: it keeps its back to the player, so its face doesn't give it away, until they come within Face
    /// Range (caught) or hit it; then it turns round to face them (its Health FSM's Look At). A hit shows its face until
    /// it runs again.
    /// Running, it spins round and round (Spin While Running), like it's gone mad.
    /// Hiding (user, 2026-10-29: "they keep running and get stuck at corners ... hide among them Pumpkin_On_Tree"): it
    /// likes best to settle beside the throwing pumpkins lying about (Blend With), more so where several lie together,
    /// and there it keeps still, one pumpkin among many, until the player comes within Blend Flee Range. It keeps out of
    /// corners and won't pick a spot behind a wall it can't get round; it stops running once it's at a good spot; and if
    /// a hop gets it nowhere (stuck against something) it gives up on that spot and slips off sideways, if need be past
    /// the player.
    /// Laughing (user, 2026-10-29): only from hiding, once it has kept still and out of the player's sight for Laugh After
    /// Hidden, and never while they can see it.
    /// Out of breath (Tired) it shows Tired Stars over its top (the mini bosses' stun stars), until it has rested enough
    /// to run.
    /// Hurt (user, 2026-11-05: "more squishy", "something visually show that they are hurt"): every hit that takes health
    /// flashes it white then red, bursts orange pulp and pale seeds off it (they splat on the floor), cuts its laugh short
    /// with a yelp (Ouch Sounds), and a running one stumbles (no hop for Stumble Seconds); the more it's hurt, the more
    /// bruised its body looks. Tired, it takes Tired Damage Times the damage: the reward for catching it. Broken, it
    /// bursts into pulp and Extra Candies more (Caught Candies Times over if it was tired: caught after a chase); its
    /// EXP is its Enemy Hit Damage's.
    /// Tools > 429 Game > Items > Make Stage Pumpkins Run Away adds it with each pumpkin's stamina; Tools > 429 Game >
    /// Items > Add Tired Stars To Runner Pumpkins puts the Tired Stars on every runner (the Chaos ones too).
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
        [Tooltip("Out of stamina it's stunned (stars over it) for this many seconds, then gets its full stamina back at " +
                 "once (user, 2026-10-29). 0: it gets its breath back slowly instead, up to Rested Share.")]
        [Min(0f)]
        [SerializeField] private float tiredSeconds = 4f;
        [Tooltip("With Tired Seconds 0: once tired, it only runs again when its stamina is back up to this share of full.")]
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
        [Tooltip("While it runs it spins round on the spot this fast, in degrees per second, like it's gone mad (user, " +
                 "2026-10-29): each hop a little faster or slower, now and then the other way round. 0: no spin.")]
        [Min(0f)]
        [SerializeField] private float spinWhileRunning = 720f;

        [Header("Player")]
        [Tooltip("When the player comes this close (metres, along the floor) it starts running.")]
        [Min(0.1f)]
        [SerializeField] private float fleeRange = 4f;
        [Tooltip("Once at its hiding spot with the player at least this far, it stops running to save its breath.")]
        [Min(0.1f)]
        [SerializeField] private float calmRange = 7f;
        [Tooltip("Hide and seek: with the player farther than this (metres, along the floor) it keeps its back to them, " +
                 "so its face doesn't give it away, unless they've hit it (then it looks at them until it runs again). " +
                 "Closer, it turns round to face them (its Health FSM's Look At). 0: it always faces the player, as before.")]
        [Min(0f)]
        [SerializeField] private float faceRange = 1f;

        [Header("After a run")]
        [Tooltip("After running away it turns its back to the player and keeps it turned, sulking, until it's hit or " +
                 "runs again (its Health FSM's Look At is overruled meanwhile). Off: it faces the player as before.")]
        [SerializeField] private bool turnAwayAfterRun;
        [Tooltip("How fast it turns round (its back to the player, or back to face them), in degrees per second.")]
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
        [Tooltip("Stamina lost per point of damage a hit does (a punch does 1, a hard one 2).")]
        [Min(0f)]
        [SerializeField] private float staminaPerDamage = 6f;

        [Header("Hurt (user, 2026-11-05)")]
        [Tooltip("While it's tired (stars up), hits do this many times the damage: the reward for catching it.")]
        [Min(1f)]
        [SerializeField] private float tiredDamageTimes = 2f;
        [Tooltip("A hit that hurts it while it runs makes it stumble: no hop for this long, seconds.")]
        [Min(0f)]
        [SerializeField] private float stumbleSeconds = 0.4f;
        [Tooltip("Every hit that hurts it flashes it white, then red, for this long (seconds)...")]
        [Min(0f)]
        [SerializeField] private float flashSeconds = 0.2f;
        [Tooltip("...and the more it's hurt, the more bruised its body looks: this tint with no health left.")]
        [SerializeField] private Color bruised = new(0.6f, 0.42f, 0.38f, 1f);
        [Tooltip("Pumpkin guts flying off at a hit (orange pulp and pale seeds, splatting on the floor): a round drop " +
                 "material (e.g. Paint_Drop_Mat) and the floor splat's (e.g. Paint_Floor_Splat_Mat). Empty: none.")]
        [SerializeField] private Material pulpMaterial;
        [SerializeField] private Material pulpSplatMaterial;
        [Range(0, 30)]
        [SerializeField] private int pulpPerHit = 10;
        [Range(0, 60)]
        [SerializeField] private int pulpWhenBroken = 26;
        [SerializeField] private Color pulpColour = new(1f, 0.5f, 0.08f, 1f);
        [SerializeField] private Color seedColour = new(0.96f, 0.9f, 0.7f, 1f);
        [Tooltip("A yelp at each hit that hurts it (one at random; its laugh stops).")]
        [SerializeField] private AudioClip[] ouchSounds = Array.Empty<AudioClip>();
        [Range(0f, 1f)]
        [SerializeField] private float ouchVolume = 0.8f;

        [Header("Reward (user, 2026-11-05)")]
        [Tooltip("What makes one more candy (Random_Breakable_Candy), for Extra Candies on top of its drop's own two.")]
        [SerializeField] private GameObject extraCandy;
        [Min(0)]
        [SerializeField] private int extraCandies;
        [Tooltip("Broken while it's tired (caught after a chase): all its candies come this many times over.")]
        [Min(1)]
        [SerializeField] private int caughtCandiesTimes = 2;

        [Header("Tired")]
        [Tooltip("Circle over its top while it's out of breath (its stamina ran out), until it has rested enough to run " +
                 "again: the mini bosses' stun stars (Stun_Stars; user, 2026-10-29). Tools > 429 Game > Items > Add Tired " +
                 "Stars To Runner Pumpkins puts them on. Empty: none.")]
        [SerializeField] private GameObject tiredStars;
        [Tooltip("How wide the ring of stars is, compared with the pumpkin (1 = as wide as the pumpkin).")]
        [Range(0.1f, 1.5f)]
        [SerializeField] private float tiredStarsSize = 1f;
        [Tooltip("The stars' dizzy sound too. Off: just the stars (a pumpkin can be out of breath for 10 seconds and more).")]
        [SerializeField] private bool tiredStarsSound;

        [Header("Hiding")]
        [Tooltip("Pumpkins it hides among (objects whose name starts with one of these, e.g. Pumpkin_On_Tree): it settles " +
                 "beside them, one pumpkin among many. Empty: none.")]
        [SerializeField] private string[] blendWith = { "Pumpkin_On_Tree" };
        [Tooltip("How much it likes a spot among them over an open one (a spot far from the player scores its distance " +
                 "in metres; each pumpkin close by adds a little more).")]
        [Min(0f)]
        [SerializeField] private float blendBonus = 9f;
        [Tooltip("Settled among them, it keeps still until the player comes this close (metres, along the floor; never " +
                 "farther than Flee Range): a pumpkin among pumpkins, found only by reaching for it.")]
        [Min(0.1f)]
        [SerializeField] private float blendFleeRange = 1.4f;
        [Tooltip("How much a spot boxed in by walls or furniture (a corner) counts against it, per side that's shut.")]
        [Min(0f)]
        [SerializeField] private float cornerPenalty = 3f;

        [Header("Laugh")]
        [Tooltip("It laughs only once it has kept still and out of the player's sight this long (seconds, a random time " +
                 "between the two; user, 2026-10-29), and never while they can see it: a laugh they come upon stops at " +
                 "once. Hidden on, it laughs again as often as its Health FSM's laugh used to (that state's Wait).")]
        [SerializeField] private Vector2 laughAfterHidden = new(3f, 4f);

        [Header("PlayMaker")]
        [Tooltip("The FSM whose float \"health\" breaks it at 0: it stops running for good then.")]
        [SerializeField] private string healthFsm = "Health";
        [SerializeField] private string healthVariable = "health";
        [Tooltip("The Health FSM's laugh: the clip variable its Audio Play plays (switched off here) and the array of " +
                 "laughs it picks from.")]
        [SerializeField] private string laughClip = "randomLaughSFX";
        [SerializeField] private string laughClips = "randomLaugh";

        private static readonly RaycastHit[] Hits = new RaycastHit[24];

        private Rigidbody body;
        private XRGrabInteractable grab;
        private Collider[] ownColliders;
        // Its meshes (the pumpkin, a hat): the stars go over the top of them.
        private Renderer[] ownRenderers;
        private PlayMakerFSM health;
        private GhostRoamArea arena;
        private NavMeshPath path;

        private Mood mood = Mood.Waiting;
        private float bornAt;
        private float staminaLeft;
        private float stillFor;
        private float tiredSince;
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
        // Turned away after a run (Turn Away After Run).
        private bool turnedAway;
        // Hit by the player: it shows its face (looks at them) wherever they are, until it runs again.
        private bool revealed;
        // While it turns its back (sulking, or the player beyond Face Range) and while it turns round to face them again:
        // the way it faces, eased round.
        private bool easing;
        private float shownYaw;
        private float lastHealth = float.MaxValue;
        // Hurt: its health at the start (for the bruising), the hit flash, the Enemy Hit Damage it takes damage through
        // (and its own Damage Taken), its body (bruised) and all its meshes (flashed) with their own colours.
        private float startHealth = -1f;
        private float flashStarted = -10f;
        private float shownBruise = -1f;
        private EnemyHitDamage hitDamage;
        private float ownDamageTaken = 1f;
        private Renderer[] bodyParts = Array.Empty<Renderer>();
        private Color[] ownColours = Array.Empty<Color>();
        private MaterialPropertyBlock look;
        private float drainedAt = -10f;
        private ParticleSystem pulp;
        private Transform pulpFloor;
        private const float SplatSeconds = 6f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private const float CandyRoom = 0.09f;
        // Running: the way it faces as it spins, and how fast (degrees per second, below 0 the other way round).
        private float spinYaw;
        private float spin;
        // Hiding: the spot it's going to is among other pumpkins; it has settled there (still, among them).
        private bool targetBlends;
        private bool blending;
        // Getting nowhere: where it was a moment ago, and how many times in a row it hasn't moved on from there.
        private Vector3 progressFrom;
        private float progressAt;
        private int stuck;
        // Spots it got stuck going to: left alone for a few seconds.
        private readonly List<(Vector3 point, float until)> badSpots = new();
        private const float StuckCheck = 0.9f;
        private const float StuckMove = 0.45f;
        // The pumpkins it hides among, shared by every runner and looked up again now and then.
        private static readonly List<Transform> Crowd = new();
        // Laughing from hiding: the Health FSM's laughs (its own Audio Play switched off), how long between them, and
        // how long it has kept hidden, needs to, and is laughing for.
        private bool laughTaken;
        private AudioSource voice;
        private readonly List<AudioClip> laughs = new();
        private AudioClip lastLaugh;
        private float laughGap = 8f;
        private float hiddenFor;
        private float laughAfter = 3.5f;
        private float nextLaugh;
        private float laughingUntil;
        private static float crowdAt = -10f;
        private static readonly Vector3[] Around =
        {
            Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
            new Vector3(1f, 0f, 1f).normalized, new Vector3(-1f, 0f, 1f).normalized,
            new Vector3(1f, 0f, -1f).normalized, new Vector3(-1f, 0f, -1f).normalized,
        };

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
            var parts = new List<Renderer>();
            foreach (var part in GetComponentsInChildren<Renderer>(true))
            {
                if (part is MeshRenderer || part is SkinnedMeshRenderer) parts.Add(part);
            }
            ownRenderers = parts.ToArray();
            var body_ = new List<Renderer>();
            foreach (var part in ownRenderers)
            {
                if (part.gameObject.name.StartsWith("pumpkin", StringComparison.OrdinalIgnoreCase)) body_.Add(part);
            }
            bodyParts = body_.ToArray();
            ownColours = new Color[ownRenderers.Length];
            for (int i = 0; i < ownRenderers.Length; i++)
            {
                var material = ownRenderers[i].sharedMaterial;
                ownColours[i] = material != null && material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : Color.white;
            }
            hitDamage = GetComponent<EnemyHitDamage>();
            if (hitDamage != null) ownDamageTaken = hitDamage.damageTaken;
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

        // Its guts on the floor stay a while after it's gone.
        private void OnDestroy()
        {
            if (pulp != null) Destroy(pulp.gameObject, SplatSeconds + 1.5f);
        }

        // Its Health FSM's health (looked up each time: the FSM may only be switched on after this starts).
        private float Health()
        {
            var value = health != null ? health.FsmVariables.FindFsmFloat(healthVariable) : null;
            return value != null ? value.Value : float.MaxValue;
        }

        private void Update()
        {
            if (mood == Mood.Done) return;
            // Before its FSM's first laugh (straight after it grows in).
            TakeOverLaugh();
            float healthNow = Health();
            if (startHealth < 0f && healthNow < float.MaxValue) startHealth = healthNow;
            if (healthNow <= 0f)
            {
                // Broken: its FSM plays the crack and the drop; it bursts into guts and its extra candies.
                bool caught = mood == Mood.Tired;
                Hurt(lastHealth != float.MaxValue ? lastHealth - healthNow : 1f, true);
                Burst(caught);
                mood = Mood.Done;
                hasTarget = false;
                KeepUpright(false);
                TurnBack();
                ShowTired(false);
                return;
            }
            // Hurt: it looks at the player again, and shows it hurts. The first reading is only where it starts.
            if (lastHealth != float.MaxValue && healthNow < lastHealth - 0.01f)
            {
                Reveal();
                Hurt(lastHealth - healthNow, false);
            }
            lastHealth = healthNow;
            // Tired: it takes more damage (caught).
            if (hitDamage != null) hitDamage.damageTaken = ownDamageTaken * (mood == Mood.Tired ? tiredDamageTimes : 1f);
            ShowHurt(healthNow);

            if (grab != null && grab.isSelected)
            {
                if (mood != Mood.Held)
                {
                    mood = Mood.Held;
                    hasTarget = false;
                    hiddenFor = 0f;
                    KeepUpright(false);
                    TurnBack();
                    ShowTired(false);
                }
                return;
            }
            // Let go.
            if (mood == Mood.Held)
            {
                if (staminaLeft >= stamina * restedShare) mood = Mood.Watching;
                else GetTired();
            }

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
                    if (distance <= (blending ? BlendFleeRange : fleeRange))
                    {
                        if (staminaLeft >= Mathf.Max(1f, stamina * 0.1f)) StartFleeing(head);
                        else GetTired();
                    }
                    break;

                case Mood.Fleeing:
                    staminaLeft -= staminaUsePerSecond * Time.deltaTime;
                    if (staminaLeft <= 0f)
                    {
                        staminaLeft = 0f;
                        GetTired();
                        hasTarget = false;
                        TurnAway();
                        break;
                    }
                    // Among the pumpkins it has to be right beside them to pass for one.
                    bool arrived = hasTarget && Flat(target - body.position).magnitude < (targetBlends ? 0.55f : 0.9f);
                    bool hidden = head != null && Hidden(head.position, body.position);
                    // Very far, far and out of sight, or at its hiding spot out of sight with the player not right on it:
                    // stop and hide there, saving its breath.
                    bool safe = distance > calmRange + 3f || (distance > calmRange && hidden) || (arrived && hidden && distance > fleeRange);
                    // At its spot among the pumpkins, or anywhere it picked with the player not on top of it: it settles.
                    bool settled = arrived && distance > (targetBlends ? BlendFleeRange + 0.5f : fleeRange + 1.5f);
                    // Heading in among the pumpkins it keeps going till it's there: halfway, it would stand out.
                    if (targetBlends && !arrived) safe = false;
                    if (safe || settled || (arrived && distance > calmRange))
                    {
                        mood = Mood.Watching;
                        blending = arrived && targetBlends;
                        hasTarget = false;
                        TurnAway();
                        break;
                    }
                    if (GettingNowhere())
                    {
                        // Stuck against a wall or in a corner: that spot's no good; off another way, past the player if
                        // that's the only way out.
                        badSpots.Add((target, Time.time + 5f));
                        PickHidingSpot(head, stuck >= 2);
                        break;
                    }
                    if (!hasTarget || arrived || Time.time >= nextPick) PickHidingSpot(head);
                    break;

                case Mood.Tired:
                    // Stunned a few seconds, then fresh: full stamina at once.
                    if (tiredSeconds > 0f)
                    {
                        if (Time.time - tiredSince >= tiredSeconds)
                        {
                            staminaLeft = stamina;
                            mood = Mood.Watching;
                        }
                    }
                    else if (staminaLeft >= stamina * restedShare) mood = Mood.Watching;
                    break;
            }
            ShowTired(mood == Mood.Tired);
            Laugh(head);
        }

        // Still and out of the player's sight for Laugh After Hidden, it laughs (one of its Health FSM's laughs, on its
        // own Audio Source), then again after its FSM's wait while it keeps hidden. Seen while laughing, it stops at once.
        private void Laugh(Transform head)
        {
            if (voice == null || laughs.Count == 0) return;
            bool hidden = head != null && Hidden(head.position, body.position);
            if (!hidden && Time.time < laughingUntil)
            {
                voice.Stop();
                laughingUntil = 0f;
            }
            if (!hidden || mood != Mood.Watching || stillFor < 0.5f)
            {
                hiddenFor = 0f;
                return;
            }
            hiddenFor += Time.deltaTime;
            if (hiddenFor < laughAfter || Time.time < nextLaugh) return;
            // A different one from last time, as its FSM did.
            int pick = Random.Range(0, laughs.Count);
            if (laughs.Count > 1 && laughs[pick] == lastLaugh) pick = (pick + 1) % laughs.Count;
            lastLaugh = laughs[pick];
            voice.PlayOneShot(lastLaugh);
            laughingUntil = Time.time + lastLaugh.length;
            nextLaugh = Time.time + Mathf.Max(laughGap, lastLaugh.length);
            laughAfter = Random.Range(laughAfterHidden.x, laughAfterHidden.y);
        }

        // Its Health FSM laughed on a timer wherever the player was: those laugh sounds are switched off (on this pumpkin
        // only) and it laughs from hiding instead, with the same clips. Done once its FSM is ready (it may be switched on
        // after this starts); a pumpkin whose FSM doesn't laugh is left alone.
        private void TakeOverLaugh()
        {
            if (laughTaken || health == null || !health.Fsm.Initialized) return;
            laughTaken = true;
            bool silenced = false;
            foreach (var state in health.FsmStates)
            {
                bool laughsHere = false;
                foreach (var action in state.Actions)
                {
                    if (action is AudioPlay play && play.oneShotClip != null && play.oneShotClip.Name == laughClip)
                    {
                        play.Enabled = false;
                        laughsHere = true;
                    }
                }
                if (!laughsHere) continue;
                silenced = true;
                foreach (var action in state.Actions)
                {
                    if (action is Wait wait) laughGap = wait.time.Value;
                }
            }
            var clips = health.FsmVariables.FindFsmArray(laughClips);
            if (!silenced || clips == null) return;
            foreach (var value in clips.Values)
            {
                if (value is AudioClip clip) laughs.Add(clip);
            }
            voice = GetComponent<AudioSource>();
            laughAfter = Random.Range(laughAfterHidden.x, laughAfterHidden.y);
        }

        // The stars over its top while it's out of breath (kept up a moment at a time), off as soon as it has rested, is
        // picked up or breaks.
        private void ShowTired(bool on)
        {
            if (tiredStars == null) return;
            if (!on)
            {
                Game.VFX.StunStars.Hide(transform);
                return;
            }
            // As wide as Tired Stars Size says (from its body), over the top of all of it (a hat too).
            var shape = ownColliders.Length > 0 && ownColliders[0] != null ? ownColliders[0].bounds : new Bounds(body.position, Vector3.one * 0.5f);
            float top = shape.max.y;
            foreach (var part in ownRenderers)
            {
                if (part != null && part.enabled && part.gameObject.activeInHierarchy) top = Mathf.Max(top, part.bounds.max.y);
            }
            float ring = Mathf.Max(shape.extents.x, shape.extents.z) * tiredStarsSize;
            float lift = top - transform.position.y + ring * 0.35f;
            Game.VFX.StunStars.Show(tiredStars, transform, transform, lift, ring, 0.25f, tiredStarsSound);
        }

        private void FixedUpdate()
        {
            if (mood == Mood.Fleeing && !body.isKinematic && Time.time >= nextHop && Grounded())
            {
                KeepUpright(true);
                // The last hops in among the pumpkins are short, so it lands right beside them.
                float speed = hopSpeed;
                if (hasTarget && targetBlends) speed = Mathf.Clamp(Flat(target - body.position).magnitude * 2f, 0.6f, hopSpeed);
                body.linearVelocity = NextStep() * speed + Vector3.up * hopUp;
                nextHop = Time.time + 0.2f;
                // Each hop a little faster or slower, now and then the other way round.
                float turn = Mathf.Sign(spin) * spinWhileRunning * Random.Range(0.75f, 1.25f);
                spin = Random.value < 0.3f ? -turn : turn;
            }
            speedBefore = body.isKinematic ? 0f : body.linearVelocity.magnitude;
        }

        private void StartFleeing(Transform head)
        {
            mood = Mood.Fleeing;
            KeepUpright(true);
            TurnBack();
            // Running off to hide again: the face goes back into hiding.
            revealed = false;
            // Spinning from where it faces now, either way round.
            spinYaw = transform.eulerAngles.y;
            spin = (Random.value < 0.5f ? -1f : 1f) * spinWhileRunning;
            blending = false;
            stuck = 0;
            progressFrom = body.position;
            progressAt = Time.time;
            PickHidingSpot(head);
        }

        private float BlendFleeRange => Mathf.Min(blendFleeRange, fleeRange);

        // Running, but it hasn't got anywhere since the last look (hopping against a wall or into a corner).
        private bool GettingNowhere()
        {
            if (Time.time - progressAt < StuckCheck) return false;
            bool nowhere = Flat(body.position - progressFrom).magnitude < StuckMove;
            stuck = nowhere ? stuck + 1 : 0;
            progressFrom = body.position;
            progressAt = Time.time;
            return nowhere;
        }

        // A run is over: it turns its back to the player (Turn Away After Run).
        private void TurnAway()
        {
            if (turnAwayAfterRun) turnedAway = true;
        }

        // Hit, running again, held or broken: its Health FSM's Look At turns it to the player again (if they're close).
        private void TurnBack() => turnedAway = false;

        // Hit by the player: it looks at them, wherever they are, until it runs again.
        private void Reveal()
        {
            TurnBack();
            revealed = true;
        }

        // After its FSMs' Look At (Update). Running, it spins (Spin While Running). Sulking or with the player beyond Face
        // Range (and not hit since its last run), its back stays to the player: upright, eased round, following them.
        // Close or hit, it eases round to face them, then the Look At takes over.
        private void LateUpdate()
        {
            var head = Camera.main != null ? Camera.main.transform : null;
            if (head == null || mood == Mood.Held || mood == Mood.Done)
            {
                easing = false;
                return;
            }
            if (mood == Mood.Fleeing && spinWhileRunning > 0f)
            {
                spinYaw = Mathf.Repeat(spinYaw + spin * Time.deltaTime, 360f);
                transform.rotation = Quaternion.Euler(0f, spinYaw, 0f);
                if (!body.isKinematic) body.angularVelocity = Vector3.zero;
                // Run over: it eases round from wherever the spin left it.
                easing = false;
                return;
            }
            var away = Flat(transform.position - head.position);
            bool hideFace = !revealed && (turnedAway || (faceRange > 0f && away.magnitude > faceRange));
            // Facing the player with nothing left to ease: the Look At turns it, as before.
            if (!hideFace && !easing) return;
            if (!easing)
            {
                easing = true;
                shownYaw = transform.eulerAngles.y;
            }
            float wanted = transform.eulerAngles.y;
            if (hideFace && away.sqrMagnitude > 0.0001f) wanted = Quaternion.LookRotation(away, Vector3.up).eulerAngles.y;
            shownYaw = Mathf.MoveTowardsAngle(shownYaw, wanted, turnAwaySpeed * Time.deltaTime);
            // Round to face the player (or to where its FSM leaves it): done easing.
            if (!hideFace && Mathf.Abs(Mathf.DeltaAngle(shownYaw, wanted)) < 1f) easing = false;
            transform.rotation = Quaternion.Euler(0f, shownYaw, 0f);
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
            // The one rule, as for an enemy: only the player's hits tire it.
            var hit = EnemyHitDamage.Judge(collision, toughness, hitSettings, out bool byPlayer);
            if (!byPlayer || hit.Damage <= 0f) return;
            nextHurt = Time.time + HurtCooldown;
            LoseStamina(hit.Damage * staminaPerDamage);
            drainedAt = Time.time;
            // Hit: it looks at the player again.
            Reveal();
        }

        // ------------------------------------------------------------------ hurt

        // A hit took health: it flashes, its guts fly, it yelps, a running one stumbles and loses breath (unless the hit
        // already took its breath: a thrown thing, see On Collision Enter).
        private void Hurt(float damage, bool broken)
        {
            flashStarted = Time.time;
            var head = Camera.main != null ? Camera.main.transform : null;
            var middle = Middle();
            var toPlayer = head != null ? Flat(head.position - middle) : transform.forward;
            var at = middle + (toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized * Radius() * 0.8f : Vector3.zero);
            Pulp(broken ? pulpWhenBroken : pulpPerHit, broken ? middle : at, -toPlayer);
            if (ouchSounds.Length > 0)
            {
                var clip = ouchSounds[Random.Range(0, ouchSounds.Length)];
                if (clip != null)
                {
                    if (voice != null)
                    {
                        voice.Stop();
                        laughingUntil = 0f;
                        voice.PlayOneShot(clip, ouchVolume);
                    }
                    else AudioSource.PlayClipAtPoint(clip, middle, ouchVolume);
                }
            }
            if (broken) return;
            if (mood == Mood.Fleeing) nextHop = Mathf.Max(nextHop, Time.time + stumbleSeconds);
            if (Time.time - drainedAt > 0.2f) LoseStamina(damage * staminaPerDamage);
        }

        // The flash after a hit (white, then red, back to its own colour), and its body bruised by how hurt it is.
        private void ShowHurt(float healthNow)
        {
            if (ownRenderers.Length == 0) return;
            float hurt = startHealth > 0f && healthNow < float.MaxValue ? Mathf.Clamp01(1f - healthNow / startHealth) : 0f;
            float k = flashSeconds > 0f ? (Time.time - flashStarted) / flashSeconds : 1f;
            bool flashing = k < 1f;
            if (!flashing && Mathf.Abs(hurt - shownBruise) < 0.001f) return;
            shownBruise = flashing ? -1f : hurt;
            look ??= new MaterialPropertyBlock();
            var flash = k < 0.35f ? new Color(3f, 3f, 3f, 1f) : Color.Lerp(new Color(2.4f, 0.55f, 0.45f, 1f), Color.white, (k - 0.35f) / 0.65f);
            for (int i = 0; i < ownRenderers.Length; i++)
            {
                var part = ownRenderers[i];
                if (part == null) continue;
                var colour = ownColours[i];
                if (Array.IndexOf(bodyParts, part) >= 0) colour *= Color.Lerp(Color.white, bruised, hurt);
                if (flashing) colour *= flash;
                part.GetPropertyBlock(look);
                look.SetColor(BaseColorId, colour);
                part.SetPropertyBlock(look);
            }
        }

        // Orange pulp and pale seeds off it, flying away from the player and splatting on the floor.
        private void Pulp(int count, Vector3 at, Vector3 away)
        {
            if (count <= 0 || pulpMaterial == null) return;
            if (pulp == null) MakePulp();
            pulpFloor.position = new Vector3(at.x, Floor(), at.z);
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            var out_ = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                bool seed = Random.value < 0.3f;
                var dir = Vector3.Slerp(Random.onUnitSphere, (out_ + Vector3.up * 0.6f).normalized, 0.45f).normalized;
                emit.position = at + Random.insideUnitSphere * 0.03f;
                emit.velocity = dir * Random.Range(0.8f, 2.4f) + Vector3.up * 0.6f;
                emit.startSize = seed ? Random.Range(0.014f, 0.024f) : Random.Range(0.03f, 0.07f);
                emit.startColor = seed ? seedColour : Color.Lerp(pulpColour, pulpColour * 0.8f, Random.value);
                pulp.Emit(emit, 1);
            }
        }

        private void MakePulp()
        {
            // Not under the pumpkin: it spins and goes; its guts don't.
            var go = new GameObject(name + " Pulp");
            pulp = go.AddComponent<ParticleSystem>();
            pulp.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = pulp.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.4f);
            main.gravityModifier = 1.3f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 150;
            var emission = pulp.emission;
            emission.enabled = false;
            var shape = pulp.shape;
            shape.enabled = false;
            var size = pulp.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.8f, 0.85f), new Keyframe(1f, 0f)));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = pulpMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            pulpFloor = new GameObject("Floor").transform;
            pulpFloor.SetParent(go.transform, false);
            pulpFloor.position = new Vector3(transform.position.x, Floor(), transform.position.z);
            if (pulpSplatMaterial != null) PaintSplats.Add(pulp, pulpFloor, pulpSplatMaterial, 0.6f, new Vector2(0.05f, 0.12f), SplatSeconds);
        }

        // Broken: a big burst of guts, and its extra candies (twice over and more if it was caught tired).
        private void Burst(bool caught)
        {
            if (extraCandy == null) return;
            // Its drop brings two of its own: caught, those come again as well.
            int count = extraCandies + (caught ? (2 + extraCandies) * (caughtCandiesTimes - 1) : 0);
            var middle = Middle();
            var parent = transform.parent;
            for (int i = 0; i < count; i++)
            {
                // Round it, a spot with nothing in it (candies started inside each other break, and smoke).
                Vector3 spot = middle;
                for (int tries = 0; tries < 8; tries++)
                {
                    var way = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                    spot = middle + way * Random.Range(0.3f, 0.6f) + Vector3.up * Random.Range(0.05f, 0.3f);
                    if (!Physics.CheckSphere(spot, CandyRoom, ~0, QueryTriggerInteraction.Ignore)) break;
                }
                Instantiate(extraCandy, spot, Quaternion.identity, parent);
            }
        }

        // Its middle and size (its first collider), and the floor under it.
        private Vector3 Middle() => ownColliders.Length > 0 && ownColliders[0] != null ? ownColliders[0].bounds.center : transform.position + Vector3.up * 0.25f;
        private float Radius() => ownColliders.Length > 0 && ownColliders[0] != null ? Mathf.Max(ownColliders[0].bounds.extents.x, ownColliders[0].bounds.extents.z) : 0.25f;
        private float Floor() => ownColliders.Length > 0 && ownColliders[0] != null ? ownColliders[0].bounds.min.y : transform.position.y;

        // Hurt: out of breath sooner. At 0 it's tired.
        // Out of breath: stunned from now (the Tired Seconds count from the moment it gets tired, not each frame).
        private void GetTired()
        {
            if (mood != Mood.Tired) tiredSince = Time.time;
            mood = Mood.Tired;
        }

        private void LoseStamina(float amount)
        {
            if (amount <= 0f) return;
            staminaLeft = Mathf.Max(0f, staminaLeft - amount);
            stillFor = 0f;
            if (staminaLeft <= 0f && (mood == Mood.Fleeing || mood == Mood.Watching))
            {
                GetTired();
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

        private void PickHidingSpot(Transform head, bool escape = false)
        {
            nextPick = Time.time + 1.6f;
            badSpots.RemoveAll(s => s.until < Time.time);
            bool targetWasBlend = false;
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
            bool onNavMesh = NavMesh.SamplePosition(here, out _, 1.5f, NavMesh.AllAreas);
            foreach (var (point, passThrough, bonus) in HideSpots(player))
            {
                float far = Flat(point - here).magnitude;
                if (far > 12f || IsBad(point)) continue;
                if (!onNavMesh && passThrough == null && Blocked(here, point)) continue;
                if (arena != null && !arena.ContainsFlat(point)) continue;
                float fromPlayer = Flat(point - player).magnitude;
                if (fromPlayer < fleeRange) continue;                                   // not right next to the player
                float score = fromPlayer + bonus;
                if (PastPlayer(here, point, player, toPlayerNow, escape) && far > 1f) score -= 8f;   // don't run past the player
                score += Random.Range(0f, 1.5f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = point;
                    bestPassThrough = passThrough;
                    targetWasBlend = false;
                    found = true;
                }
            }

            // Among the pumpkins lying about: one pumpkin among many.
            foreach (var (point, crowd) in BlendSpots(here, player))
            {
                if (IsBad(point) || (!onNavMesh && Blocked(here, point))) continue;
                float fromPlayer = Flat(point - player).magnitude;
                float score = fromPlayer + blendBonus + 1.5f * Mathf.Min(crowd - 1, 3);
                if (PastPlayer(here, point, player, toPlayerNow, escape)) score -= 8f;
                score -= cornerPenalty * 0.5f * ShutSides(point);
                score += Random.Range(0f, 1.5f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = point;
                    bestPassThrough = null;
                    targetWasBlend = true;
                    found = true;
                }
            }

            for (int i = 0; i < 20; i++)
            {
                // Mostly away from the player, sometimes off to the side; getting out of a corner, any way at all.
                float angle = escape ? Random.Range(-180f, 180f) : Random.Range(-120f, 120f);
                var direction = Quaternion.Euler(0f, angle, 0f) * awayFromPlayer;
                var spot = here + direction * Random.Range(3f, 9f);
                if (arena != null && !arena.ContainsFlat(spot)) continue;
                if (NavMesh.SamplePosition(spot, out var onMesh, 1.5f, NavMesh.AllAreas)) spot = onMesh.position;
                else if (onNavMesh) continue;                                     // there is a NavMesh, just not there
                // No NavMesh to go round things: it hops straight there, so nothing solid may stand in the way.
                if (!onNavMesh && Blocked(here, spot)) continue;
                if (IsBad(spot)) continue;

                float score = Flat(spot - player).magnitude;                       // far from the player
                if (Hidden(player, spot)) score += 5f;                            // out of sight (behind furniture)
                if (PastPlayer(here, spot, player, toPlayerNow, escape)) score -= 8f;   // don't run past the player
                score -= cornerPenalty * ShutSides(spot);                         // not into a corner
                score += Random.Range(0f, 1.5f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = spot;
                    bestPassThrough = null;
                    targetWasBlend = false;
                    found = true;
                }
            }
            // Boxed in: nothing away from the player is open, so any way out at all, right now (not after getting stuck).
            if (!found && !escape)
            {
                PickHidingSpot(head, true);
                return;
            }
            target = found ? best : here + OpenestWay(here) * 3f;
            targetBlends = found && targetWasBlend;
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

        // Spots beside the pumpkins lying about (Blend With), each with how many lie within 1.5 m there (itself too): on
        // the side away from the player, or else round its sides.
        private List<(Vector3 point, int crowd)> BlendSpots(Vector3 here, Vector3 player)
        {
            var spots = new List<(Vector3, int)>();
            if (blendWith == null || blendWith.Length == 0) return spots;
            RefreshCrowd(blendWith);
            var mine = ownColliders.Length > 0 && ownColliders[0] != null ? ownColliders[0].bounds : new Bounds(body.position, Vector3.one * 0.6f);
            float myRadius = Mathf.Max(mine.extents.x, mine.extents.z);
            foreach (var other in Crowd)
            {
                if (other == null || !other.gameObject.activeInHierarchy || other == transform) continue;
                var at = other.position;
                if (Flat(at - here).magnitude > 12f || Flat(at - player).magnitude < fleeRange) continue;
                if (arena != null && !arena.ContainsFlat(at)) continue;
                int crowd = 0;
                foreach (var near in Crowd)
                {
                    if (near != null && near.gameObject.activeInHierarchy && Flat(near.position - at).magnitude < 1.5f) crowd++;
                }
                float otherRadius = OtherRadius(other);
                var away = Flat(at - player);
                away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
                // Behind it from the player first, then round its sides.
                foreach (float turn in new[] { 0f, 50f, -50f, 100f, -100f })
                {
                    var side = Quaternion.Euler(0f, turn, 0f) * away;
                    var spot = at + side * (otherRadius + myRadius + 0.08f);
                    if (NavMesh.SamplePosition(spot, out var floor, 0.6f, NavMesh.AllAreas)) spot = floor.position;
                    else spot.y = at.y - otherRadius;
                    if (!OnlyOwnOrLoose(spot + Vector3.up * (myRadius + 0.05f), myRadius * 0.85f)) continue;
                    spots.Add((spot, crowd));
                    break;
                }
            }
            return spots;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCrowd()
        {
            Crowd.Clear();
            crowdAt = -10f;
        }

        private static void RefreshCrowd(string[] names)
        {
            if (Time.time - crowdAt < 1f) return;
            crowdAt = Time.time;
            Crowd.Clear();
            foreach (var other in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
            {
                foreach (var name in names)
                {
                    if (string.IsNullOrEmpty(name) || !other.name.StartsWith(name)) continue;
                    Crowd.Add(other.transform);
                    break;
                }
            }
        }

        private static float OtherRadius(Transform other)
        {
            var parts = other.GetComponentsInChildren<Collider>();
            if (parts.Length == 0) return 0.3f;
            var bounds = parts[0].bounds;
            foreach (var part in parts) bounds.Encapsulate(part.bounds);
            return Mathf.Max(bounds.extents.x, bounds.extents.z);
        }

        // Room for it there: everything in this ball is its own colliders or small loose things (a candy, a fallen book).
        private bool OnlyOwnOrLoose(Vector3 centre, float radius)
        {
            foreach (var hit in Physics.OverlapSphere(centre, radius, ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                if (Array.IndexOf(ownColliders, hit) >= 0) continue;
                if (hit.attachedRigidbody != null && !hit.attachedRigidbody.isKinematic && hit.bounds.size.magnitude < 1f) continue;
                return false;
            }
            return true;
        }

        // How many of 8 sides of a spot are shut by a wall or furniture within a metre: a corner shuts 3 or more.
        private int ShutSides(Vector3 spot)
        {
            int shut = 0;
            var from = spot + Vector3.up * 0.35f;
            foreach (var side in Around)
            {
                int count = Physics.RaycastNonAlloc(from, side, Hits, 1f, ~(1 << 2), QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    var hit = Hits[i];
                    if (IsOwn(hit)) continue;
                    if (hit.rigidbody != null && !hit.rigidbody.isKinematic && hit.collider.bounds.size.magnitude < 1.5f) continue;
                    shut++;
                    break;
                }
            }
            return shut;
        }

        // Something solid (not small loose things) between here and the spot, at hopping height: a line (a ball cast
        // would miss a wall it's already pressed against, in a corner).
        private bool Blocked(Vector3 here, Vector3 spot)
        {
            var line = Flat(spot - here);
            float length = line.magnitude;
            if (length < 0.1f) return false;
            foreach (float height in new[] { 0.2f, 0.45f })
            {
                int count = Physics.RaycastNonAlloc(here + Vector3.up * height, line / length, Hits, length, ~(1 << 2), QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    var hit = Hits[i];
                    if (IsOwn(hit) || hit.collider.GetComponentInParent<XROrigin>() != null) continue;
                    if (hit.rigidbody != null && !hit.rigidbody.isKinematic && hit.collider.bounds.size.magnitude < 1.5f) continue;
                    return true;
                }
            }
            return false;
        }

        // Running there means running at the player: towards them at all, or (getting out of a corner) right past them.
        private static bool PastPlayer(Vector3 here, Vector3 spot, Vector3 player, Vector3 toPlayerNow, bool escape)
        {
            var toSpot = Flat(spot - here);
            if (!escape) return Vector3.Dot(toSpot.normalized, toPlayerNow) > 0.3f;
            // Escaping: only a line passing within 1.2 m of them counts.
            float length = toSpot.magnitude;
            if (length < 0.01f) return false;
            float along = Mathf.Clamp(Vector3.Dot(Flat(player - here), toSpot / length), 0f, length);
            return Flat(here + toSpot / length * along - player).magnitude < 1.2f;
        }

        // The way with the most room before a wall or furniture (of 8), for when nothing else will do.
        private Vector3 OpenestWay(Vector3 here)
        {
            var best = Vector3.forward;
            float most = -1f;
            foreach (var side in Around)
            {
                float room = 3f;
                int count = Physics.RaycastNonAlloc(here + Vector3.up * 0.3f, side, Hits, 3f, ~(1 << 2), QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    if (IsOwn(Hits[i])) continue;
                    room = Mathf.Min(room, Hits[i].distance);
                }
                if (room > most) { most = room; best = side; }
            }
            return best;
        }

        private bool IsBad(Vector3 spot)
        {
            foreach (var (point, until) in badSpots)
            {
                if (until >= Time.time && Flat(point - spot).magnitude < 1.2f) return true;
            }
            return false;
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
            DrawRing(fleeRange);
            Gizmos.color = Color.cyan;
            if (faceRange > 0f) DrawRing(faceRange);
            if (hasTarget)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, target);
                Gizmos.DrawWireSphere(target, 0.3f);
            }
        }

        private void DrawRing(float radius)
        {
            var last = transform.position + Vector3.forward * radius;
            for (int i = 1; i <= 32; i++)
            {
                float angle = i / 32f * Mathf.PI * 2f;
                var next = transform.position + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
                Gizmos.DrawLine(last, next);
                last = next;
            }
        }
#endif
    }
}
