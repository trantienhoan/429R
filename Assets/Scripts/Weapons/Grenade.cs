using System;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game.Weapons
{
    /// <summary>
    /// Turns a weapon into a grenade: thrown hard enough, it explodes on the first thing it hits. Everything in the
    /// blast that can be hit loses health, loose objects are pushed away, the player is hurt like a bug bite if they're
    /// in it too (Player Event), the hand that threw it buzzes, and the weapon is gone (with Break Apart, it shatters
    /// into its pieces, like a bottle). Dropped or tossed gently, it stays a normal weapon. Goes on the weapon's root,
    /// next to its XR Grab Interactable; Tools > 429 Game > Weapons > Make Flip-Flops Explode adds it.
    /// With Shake To Charge (the bottles), shaking it in the hand charges it: shaken long enough (the first step of
    /// Charge), it explodes on whatever it hits first once it leaves the hand, however it left it, and the longer it
    /// was shaken the bigger and harder the blast; shaken to the last step it goes off in the hand (In Hand Event).
    /// The hand buzzes while it charges, harder at each step, a puff of smoke marks each step, the bottle rattles
    /// (Shake Sound) while it's shaken, and just before it would go off in the hand it sounds its warning (Warning Sound).
    /// "Can be hit" means what the game's PlayMaker damage uses: a "Health" FSM (or "SpiderHealth", "BeeHealth", ...)
    /// with a 'health' float, next to a "Damage" FSM. Objects without a Damage FSM (story objects, minigame
    /// counters) are left alone. Things with a Damage FSM but no health of their own (the whack-a-mole
    /// pumpkins, whose barrels count the hits) are sent the event "Blast"; a Damage FSM with a Blast transition takes
    /// it as a hard hit. Scripts that handle hits themselves (IHittable, e.g. the PlayGround's ghost tree, bush and balloons)
    /// are hit with the damage first, even when they also have a Health FSM (e.g. for the fairy's shield pop); the ghost
    /// tree passes it on to that FSM's health.
    /// The player's Throw Power (a level-up stat) decides it all: at 0 it never explodes (nor charges), it's just a
    /// weapon that hits and breaks; from 1 point it explodes as set here, and every point after that makes the blast
    /// reach farther and do more damage (Player Level: Exploding throws). The extra reach never hurts the player or what
    /// they hold: only the blast's own reach (or going off in the hand) does.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class Grenade : MonoBehaviour
    {
        [Header("Throw")]
        [Tooltip("It only explodes if it leaves your hand at least this fast, in metres per second. Your hand's speed is " +
                 "multiplied by the grab's Throw Velocity Scale (6 on the flip-flops), so 8 takes a quick flick, not a drop.")]
        [Min(0f)]
        [SerializeField] private float minThrowSpeed = 8f;

        [Header("Blast")]
        [Tooltip("How far the blast reaches, in metres.")]
        [Min(0.1f)]
        [SerializeField] private float radius = 1.5f;
        [Tooltip("Health everything in the blast loses.")]
        [Min(0f)]
        [SerializeField] private float damage = 3f;
        [Tooltip("How fast loose objects in the middle of the blast are pushed away, in metres per second; the same for " +
                 "light and heavy things, less further out. 0 = no push.")]
        [Min(0f)]
        [SerializeField] private float push = 4f;
        [Tooltip("Event sent to the player's Damage FSM if they're in the blast: Damage (a bug bite), DamageHeavy, ... " +
                 "Empty: it never hurts the player.")]
        [SerializeField] private string playerEvent = "Damage";
        [Tooltip("On: exploding breaks it the way it normally breaks (its Health FSM drops its pieces, like a bottle's " +
                 "glass). Off: it just vanishes.")]
        [SerializeField] private bool breakApart;

        [Header("Shake to charge")]
        [Tooltip("Shaking it in the hand charges it (the bottles): see Charge. Off: a plain grenade.")]
        [SerializeField] private bool shakeToCharge;
        [Tooltip("Seconds shaken (x) and the blast's reach in metres (y) that gives; in between, the reach is in between. " +
                 "Shaken less than the first x, it's the plain grenade above. Shaken to the last x, it goes off in the hand.")]
        [SerializeField] private Vector2[] charge = { new(1f, 0.5f), new(2f, 1f), new(2.5f, 1.25f), new(3f, 1.5f) };
        [Tooltip("Health everything in a charged blast loses: x at the first step of Charge, y at the last.")]
        [SerializeField] private Vector2 chargeDamage = new(3f, 8f);
        [Tooltip("How fast a charged blast pushes loose things away: x at the first step of Charge, y at the last.")]
        [SerializeField] private Vector2 chargePush = new(4f, 10f);
        [Tooltip("How hard the hand must shake for it to count, in metres per second squared: a real shake is 30-60, " +
                 "just moving the hand about stays under 10.")]
        [Min(1f)]
        [SerializeField] private float shakeStrength = 20f;
        [Tooltip("Event sent to the player's Damage FSM when it goes off in their hand.")]
        [SerializeField] private string inHandEvent = "DamageHeavy";
        [Tooltip("Played from it while it's being shaken (looping, paused when the shaking stops), e.g. a bottle rattling.")]
        [SerializeField] private AudioClip shakeSound;
        [Range(0f, 1f)]
        [SerializeField] private float shakeVolume = 0.8f;
        [Tooltip("Played once from it just before it goes off in the hand (Warn Before), e.g. a beep.")]
        [FormerlySerializedAs("chargeSound")]
        [SerializeField] private AudioClip warningSound;
        [Tooltip("The warning sounds when this many seconds of shaking are left before it goes off in the hand.")]
        [Min(0f)]
        [SerializeField] private float warnBefore = 0.7f;

        [Header("Effects")]
        [Tooltip("Particle effects that appear where it explodes.")]
        [SerializeField] private GameObject[] effects = Array.Empty<GameObject>();
        [Tooltip("Size of the effects for a plain throw, e.g. 0.7 for a smaller bang. A blast that reaches farther (Throw " +
                 "Power, a charged bottle) makes them bigger, up to 1.3.")]
        [Min(0.05f)]
        [SerializeField] private float effectSize = 1f;
        [SerializeField] private AudioClip sound;
        [Range(0f, 1f)]
        [SerializeField] private float volume = 1f;
        [Tooltip("How hard the controller that threw it buzzes (0-1).")]
        [Range(0f, 1f)]
        [SerializeField] private float rumbleStrength = 1f;
        [Tooltip("How long the controller that threw it buzzes, in seconds.")]
        [Min(0f)]
        [SerializeField] private float rumbleDuration = 0.3f;

        // XR Toolkit gives a thrown object its speed at the end of the frame it's let go, so the throw is judged over
        // a short moment after that.
        private const float ThrowWindow = 0.2f;
        // The effects are made for a blast about this wide (in metres of reach); charged blasts scale them to theirs.
        private const float EffectReach = 2f;
        // How quickly the shake reading follows the hand, in seconds.
        private const float ShakeSmoothing = 0.12f;
        private const float BuzzEvery = 0.1f;

        private XRGrabInteractable grab;
        private Rigidbody body;
        private XRBaseInputInteractor thrower;
        private bool inFlight;
        private float releasedAt;
        private float throwSpeed;
        private bool exploded;

        // Shaking: seconds shaken so far, how hard the hand shakes now, and the hand's last place and speed (in the play
        // area's space, so walking and turning don't count).
        private float shaken;
        private float shakeLevel;
        private bool hasLastHand;
        private Vector3 lastHand;
        private Vector3 lastHandVelocity;
        private int stepReached = -1;
        private float nextBuzz;
        private Transform smokePuff;
        private XROrigin origin;
        // Its own speaker in the world, for the rattle (looping) and the warning.
        private AudioSource rattle;
        private bool rattling;
        private bool warned;
        // A shake this much weaker than Shake Strength still keeps the rattle going, so it doesn't stutter at the edge.
        private const float KeepRattling = 0.7f;

        /// <summary>Seconds it has been shaken (Shake To Charge).</summary>
        public float Shaken => shaken;

        /// <summary>
        /// How far the shaking has got (Shake To Charge): 0 not shaken, 1 the moment it goes off in the hand. 0 for a
        /// plain grenade.
        /// </summary>
        public float ChargeProgress => shakeToCharge && charge != null && charge.Length > 0
            ? Mathf.Clamp01(shaken / Mathf.Max(charge[charge.Length - 1].x, 0.01f))
            : 0f;

        private bool Charged => shakeToCharge && charge != null && charge.Length > 0 && shaken >= charge[0].x;

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnLetGo);
            // Ready at load, so the first explosion doesn't stutter.
            Blast.Warm(effects);
            // The bottles' own puff of smoke (shown when they take a hit) marks each step of the charge.
            foreach (var child in GetComponentsInChildren<Transform>(true))
            {
                if (child != transform && child.name == "Smoke_Blast") smokePuff = child;
            }

            if (shakeToCharge && (shakeSound != null || warningSound != null))
            {
                var go = new GameObject("Shake Sound");
                go.transform.SetParent(transform, false);
                rattle = go.AddComponent<AudioSource>();
                rattle.playOnAwake = false;
                rattle.loop = true;
                rattle.clip = shakeSound;
                rattle.volume = shakeVolume;
                // Heard from the bottle, where it is.
                rattle.spatialBlend = 1f;
                rattle.minDistance = 0.5f;
                rattle.maxDistance = 15f;
            }
        }

        private void OnDestroy()
        {
            if (grab == null) return;
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnLetGo);
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            inFlight = false;
            hasLastHand = false;
        }

        private void OnLetGo(SelectExitEventArgs args)
        {
            // Passed to the other hand, not thrown.
            if (grab.isSelected) return;

            thrower = args.interactorObject as XRBaseInputInteractor;
            inFlight = true;
            releasedAt = Time.time;
            throwSpeed = 0f;
            hasLastHand = false;
            Rattle(false);
        }

        // The bottle's rattle on or off (paused, so it goes on where it left off).
        private void Rattle(bool on)
        {
            if (rattle == null || rattle.clip == null || on == rattling) return;
            rattling = on;
            if (!on) rattle.Pause();
            else if (rattle.time > 0f) rattle.UnPause();
            else rattle.Play();
        }

        private void Update()
        {
            // Without Throw Power it can't be charged either.
            if (shakeToCharge && !exploded && grab.isSelected && Game.Leveling.PlayerLevel.ExplodingThrows) WatchShaking();
        }

        private void FixedUpdate()
        {
            if (inFlight && body != null && Time.time - releasedAt <= ThrowWindow)
                throwSpeed = Mathf.Max(throwSpeed, body.linearVelocity.magnitude);
        }

        // The first thing it hits after leaving the hand decides: charged, or thrown hard, it explodes; otherwise it's
        // a weapon again.
        private void OnCollisionEnter(Collision collision)
        {
            if (!inFlight || exploded || Blast.IsPlayer(collision.collider)) return;

            if (Time.time - releasedAt <= ThrowWindow) throwSpeed = Mathf.Max(throwSpeed, collision.relativeVelocity.magnitude);
            inFlight = false;
            // No Throw Power yet: it just hits (its Damage FSM breaks it as usual).
            if (!Game.Leveling.PlayerLevel.ExplodingThrows) return;
            var point = collision.contactCount > 0 ? collision.GetContact(0).point : body.position;
            if (Charged) ExplodeCharged(point, false);
            else if (throwSpeed >= minThrowSpeed)
            {
                float reach = Game.Leveling.PlayerLevel.ThrowBlastRadius(radius);
                Explode(point, reach, Game.Leveling.PlayerLevel.ThrowBlastDamage(damage), push, playerEvent, false, BangSize(reach), radius);
            }
        }

        // Measures how hard the hand holding it shakes: the change in its speed, smoothed. Shaking hard enough adds to
        // the charge; the hand buzzes as it charges, harder at each step, and it beeps once charged.
        private void WatchShaking()
        {
            float dt = Time.deltaTime;
            var hand = grab.firstInteractorSelecting as Component;
            if (dt <= 0f || hand == null) return;
            if (origin == null) origin = FindAnyObjectByType<XROrigin>();
            var space = origin != null && origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.transform : null;
            var position = space != null ? space.InverseTransformPoint(hand.transform.position) : hand.transform.position;
            if (!hasLastHand)
            {
                lastHand = position;
                lastHandVelocity = Vector3.zero;
                hasLastHand = true;
                return;
            }

            var velocity = (position - lastHand) / dt;
            var acceleration = (velocity - lastHandVelocity) / dt;
            lastHand = position;
            lastHandVelocity = velocity;
            shakeLevel = Mathf.Lerp(shakeLevel, acceleration.magnitude, 1f - Mathf.Exp(-dt / ShakeSmoothing));
            bool shaking = shakeLevel >= shakeStrength;
            if (shaking) shaken += dt;
            Rattle(shaking || (rattling && shakeLevel >= shakeStrength * KeepRattling));

            float blowUp = charge[charge.Length - 1].x;
            if (shaken >= blowUp)
            {
                ExplodeCharged(body.worldCenterOfMass, true);
                return;
            }
            // Nearly there: the warning, once, from the bottle.
            if (!warned && warningSound != null && rattle != null && shaken >= blowUp - warnBefore)
            {
                warned = true;
                rattle.PlayOneShot(warningSound, volume);
            }

            // Each step reached: a strong buzz and a puff of smoke.
            int step = -1;
            for (int i = 0; i < charge.Length; i++)
            {
                if (shaken >= charge[i].x) step = i;
            }
            if (step > stepReached)
            {
                stepReached = step;
                Buzz(0.9f, 0.12f);
                if (smokePuff != null)
                {
                    smokePuff.gameObject.SetActive(false);
                    smokePuff.gameObject.SetActive(true);
                }
            }

            float progress = Mathf.Clamp01(shaken / Mathf.Max(blowUp, 0.01f));
            if (shaking && Time.time >= nextBuzz)
            {
                Buzz(Mathf.Lerp(0.15f, 0.6f, progress), BuzzEvery);
                nextBuzz = Time.time + BuzzEvery;
            }
        }

        // The blast as big as the shaking made it: in between two steps of Charge, in between their reach.
        private void ExplodeCharged(Vector3 point, bool inHand)
        {
            float reach = charge[charge.Length - 1].y;
            for (int i = 0; i < charge.Length - 1; i++)
            {
                if (shaken > charge[i + 1].x) continue;
                reach = Mathf.Lerp(charge[i].y, charge[i + 1].y, Mathf.InverseLerp(charge[i].x, charge[i + 1].x, shaken));
                break;
            }
            float strength = Mathf.InverseLerp(charge[0].x, charge[charge.Length - 1].x, shaken);
            // Throw Power makes it bigger and harder, but only the charge's own reach can hurt the player.
            float bigger = Game.Leveling.PlayerLevel.ThrowBlastRadius(reach);
            Explode(point, bigger, Game.Leveling.PlayerLevel.ThrowBlastDamage(Mathf.Lerp(chargeDamage.x, chargeDamage.y, strength)),
                Mathf.Lerp(chargePush.x, chargePush.y, strength), inHand ? inHandEvent : playerEvent, inHand, BangSize(bigger), reach);
        }

        private const float MaxEffectScale = 1.3f;

        // The effects' size: Effect Size for a plain throw's own blast, bigger as a blast reaches farther (Throw Power, a
        // charged bottle), at most Max Effect Scale.
        private float BangSize(float reach) =>
            Mathf.Min(effectSize + Mathf.Max(0f, EffectScale(reach) - EffectScale(radius)), MaxEffectScale);

        private static float EffectScale(float reach)
        {
            return Mathf.Clamp(reach / EffectReach, 0.3f, MaxEffectScale);
        }

        // Player Reach: how far from the blast the player is hurt (its reach before Throw Power made it bigger).
        private void Explode(Vector3 point, float reach, float hurt, float force, string eventForPlayer, bool inHand, float effectScale,
            float playerReach)
        {
            exploded = true;

            var hand = inHand && grab.firstInteractorSelecting is XRBaseInputInteractor holder ? holder : thrower;
            if (rumbleStrength > 0f) Game.Combat.HitBuzz.Other(hand, rumbleStrength, rumbleDuration);
            Blast.Explode(point, reach, hurt, force, body, effects, sound, volume, eventForPlayer, inHand, effectScale, playerReach);

            // Breaking the usual way lets its own Health FSM drop the pieces and remove it; otherwise it just vanishes.
            // Weapon Wear is switched off first: it gives back part of a small loss (Weapon Efficiency), so a worn weapon
            // would never reach 0 and stay whole.
            if (breakApart && Blast.HasHealth(gameObject, out var ownHealth, out _) && ownHealth != null)
            {
                if (TryGetComponent(out Game.Leveling.WeaponWear wear)) wear.enabled = false;
                ownHealth.Value = 0f;
            }
            else Destroy(gameObject);
        }

        // The hand holding it.
        private void Buzz(float strength, float duration)
        {
            if (grab.firstInteractorSelecting is XRBaseInputInteractor hand) Game.Combat.HitBuzz.Other(hand, strength, duration);
        }
    }
}
