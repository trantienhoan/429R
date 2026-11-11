using Game.Combat;
using Game.Locomotion;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;

namespace Game.Enemies
{
    /// <summary>
    /// A spider's web shot (Spider Brain launches it). It flies with gravity (straight, with its Rigidbody's Use Gravity
    /// off: the bee bosses' poison sting, Bee_Poison_Sting); reaching the player's body it hurts them (Player Event, sent
    /// to their Damage FSM), adds web to them (Web Added; a full web slows them and stops teleporting for a while, Player
    /// Web) and poisons them if it carries Poison (Player Poison). A weapon held in its way, or a fist, swats it: no web,
    /// no poison. Anything else just splats it (its splat comes from the shared Effect Pool). It has no collider of its
    /// own and finds what it hits by sweeping
    /// along its flight, so it never knocks things over, hurts other enemies, or sets off other objects' trigger FSMs.
    /// A boss's giant web (MakeGiant) goes straight through fists and held weapons, and smashes through what's in its way
    /// (user, 2026-11-05: "strong enough to break almost everything that block its path"): furniture, props and loose
    /// things break (Smash Damage, not the player's kill) and are flung ahead; walls, the floor, the Pumpkin Tree and the
    /// heaviest blocks still stop it.
    /// Goes on the web ball prefab's root, next to its Rigidbody.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class WebBall : MonoBehaviour
    {
        [Tooltip("Sent to the player's Damage FSM when it hits them: Damage (1-3 health), DamageHeavy (6-12), ...")]
        [SerializeField] private string playerEvent = "Damage";
        [Tooltip("How much web it adds to the player when it hits them (0.2 = 20% of a full web). A full web slows them " +
                 "and stops teleporting for a while (Player Web on the XR Origin).")]
        [Range(0f, 1f)]
        [SerializeField] private float webAdded = 0.2f;
        [Tooltip("Poison (the bee bosses' sting shot; user, 2026-11-10): when it hits the player they also lose this much " +
                 "health every second for Poison Seconds (Player Poison: cut by Toughness like any hit, never below 1 " +
                 "health). Only with Poison Seconds above 0.")]
        [Min(0f)]
        [SerializeField] private float poisonPerSecond;
        [Tooltip("How long its poison lasts, in seconds. 0 = no poison (the spiders' web balls).")]
        [Min(0f)]
        [SerializeField] private float poisonSeconds;
        [Tooltip("How big it is for hitting things (radius, metres at size 1).")]
        [Min(0.01f)]
        [SerializeField] private float radius = 0.1f;
        [Tooltip("It splats on its own after this many seconds.")]
        [Min(0.5f)]
        [SerializeField] private float lifetime = 4f;

        [Header("Giant web")]
        [Tooltip("A boss's giant web breaks what's in its way with this much damage (furniture, props, loose things) and " +
                 "flies on. Walls, the floor, the Pumpkin Tree and the heaviest blocks still stop it.")]
        [Min(0f)]
        [SerializeField] private float smashDamage = 1000f;
        [Tooltip("How fast what it smashes is flung along its flight, in metres per second.")]
        [Min(0f)]
        [SerializeField] private float smashShove = 4f;

        [Header("Effects")]
        [Tooltip("Appears where it splats, e.g. a puff.")]
        [SerializeField] private GameObject splatEffect;
        [Tooltip("Size of the splat effect. The Hyper Casual FX smoke is about 13 m wide, so 0.03 makes it about 40 cm.")]
        [Min(0f)]
        [SerializeField] private float splatScale = 0.03f;
        [Tooltip("Played when it sticks to the player.")]
        [SerializeField] private AudioClip hitSound;
        [Tooltip("Played when it splats anywhere else or is swatted.")]
        [SerializeField] private AudioClip splatSound;
        [Range(0f, 1f)]
        [SerializeField] private float volume = 1f;

        private const string PlayerVariable = "Player";
        private const string PlayerDamageFsm = "Damage";
        private static readonly RaycastHit[] hits = new RaycastHit[16];
        private static readonly NearestFirst nearestFirst = new();
        private static int wallLayer = -2;

        private Rigidbody body;
        // A giant web smashes through things; each is smashed once.
        private bool smashes;
        private readonly System.Collections.Generic.HashSet<Object> smashed = new();
        private Transform shooter;
        // The player's FSM its hit goes to, and whether fists and held weapons stop it (not a giant web).
        private string playerFsm = PlayerDamageFsm;
        private bool swattable = true;
        private Vector3 lastPosition;
        private float dieAt;
        private bool done;
        private bool held;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.interpolation = RigidbodyInterpolation.Interpolate;
            lastPosition = body.position;
            dieAt = Time.time + lifetime;
            // Its splat ready before the first one (a bee boss shoots every few seconds).
            if (splatEffect != null) EffectPool.Warm(splatEffect);
        }

        /// <summary>
        /// Keeps it still and harmless while the spider makes it (the spider moves it); Launch lets it go.
        /// </summary>
        public void Hold()
        {
            held = true;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
        }

        /// <summary>How big it is for hitting things right now (radius, metres).</summary>
        public float HitRadius => radius * Mathf.Max(0.01f, transform.lossyScale.x);

        /// <summary>Sends it flying at this velocity; the shooter's own body is never hit.</summary>
        public void Launch(Vector3 velocity, Transform from)
        {
            shooter = from;
            if (held)
            {
                held = false;
                body.position = transform.position;
                body.isKinematic = false;
                body.interpolation = RigidbodyInterpolation.Interpolate;
            }
            body.linearVelocity = velocity;
            lastPosition = body.position;
            dieAt = Time.time + lifetime;
        }

        /// <summary>
        /// Makes it a boss's giant web: on the player it sends <paramref name="eventName"/> to their FSM called
        /// <paramref name="fsmName"/> and fills their web at once (they're webbed); fists and held weapons don't stop it
        /// (a legendary shield still does). Its splat is <paramref name="size"/> times as big.
        /// </summary>
        public void MakeGiant(string eventName, string fsmName, float size)
        {
            playerEvent = eventName;
            playerFsm = fsmName;
            webAdded = 1f;
            splatScale *= size;
            swattable = false;
            smashes = smashDamage > 0f;
        }

        /// <summary>Bursts where it is, e.g. a half-made ball when its spider is interrupted.</summary>
        public void Burst()
        {
            if (!done) Splat(transform.position, splatSound);
        }

        /// <summary>
        /// The starting velocity at about <paramref name="speed"/> that lands on <paramref name="target"/>: the low arc,
        /// or the furthest-reaching one if it can't get there at that speed.
        /// </summary>
        public static Vector3 Aim(Vector3 from, Vector3 target, float speed, float gravity)
        {
            var to = target - from;
            var flat = new Vector3(to.x, 0f, to.z);
            float x = flat.magnitude, y = to.y;
            if (x < 0.05f || gravity <= 0.01f) return to.normalized * speed;
            float v2 = speed * speed;
            float root = v2 * v2 - gravity * (gravity * x * x + 2f * y * v2);
            float angle = root >= 0f ? Mathf.Atan((v2 - Mathf.Sqrt(root)) / (gravity * x)) : Mathf.PI / 4f;
            return flat / x * (speed * Mathf.Cos(angle)) + Vector3.up * (speed * Mathf.Sin(angle));
        }

        // Sweeps from where it was to where it is: the first thing in between decides.
        private void FixedUpdate()
        {
            if (done || held) return;
            var now = body.position;
            var step = now - lastPosition;
            float size = Mathf.Max(0.01f, transform.lossyScale.x);
            if (step.sqrMagnitude > 0.000001f)
            {
                int count = Physics.SphereCastNonAlloc(lastPosition, radius * size, step.normalized, hits, step.magnitude, Physics.AllLayers, QueryTriggerInteraction.Collide);
                // Nearest first: a giant web smashes through things until something stops it.
                System.Array.Sort(hits, 0, count, nearestFirst);
                for (int i = 0; i < count; i++)
                {
                    var other = hits[i].collider;
                    if (!Counts(other)) continue;
                    var point = hits[i].distance > 0f ? hits[i].point : now;
                    if (smashes && Smashable(other))
                    {
                        Smash(other, point);
                        continue;
                    }
                    // Scenery it already touches as it sets off (a big ball made near the floor) doesn't splat it.
                    if (hits[i].distance <= 0f && !IsPlayer(other) && !IsHeldOrFist(other)) continue;
                    HitSomething(other, point);
                    return;
                }
            }
            lastPosition = now;
            if (Time.time >= dieAt) Splat(now, splatSound);
        }

        // What it can hit: the player (any of their colliders), held weapons and fists, and solid scenery. Not its
        // shooter, other enemies, loose triggers, or other web balls.
        private bool Counts(Collider other)
        {
            if (other == null || (shooter != null && other.transform.IsChildOf(shooter))) return false;
            if (IsHeldOrFist(other)) return swattable;
            if (IsPlayer(other)) return true;
            if (other.isTrigger || other.GetComponentInParent<NavMeshAgent>() != null || other.GetComponentInParent<WebBall>() != null) return false;
            return true;
        }

        // What a giant web breaks and flies on through: furniture and props (Enemy Hit Damage in Furniture mode, or a
        // health FSM) and loose things. Not the player, walls (the Wall layer), the floor and other fixed scenery, the
        // Pumpkin Tree, the heaviest blocks, enemies, or candy (it just passes those).
        private bool Smashable(Collider other)
        {
            if (IsPlayer(other) || IsHeldOrFist(other)) return false;
            if (wallLayer == -2) wallLayer = LayerMask.NameToLayer("Wall");
            if (wallLayer >= 0 && other.gameObject.layer == wallLayer) return false;
            for (var t = other.transform; t != null; t = t.parent)
            {
                if (t.CompareTag("PumpkinTree")) return false;
            }
            var thing = other.attachedRigidbody;
            if (thing != null && Game.Combat.PushWeight.IsImmovable(thing)) return false;
            var breakable = other.GetComponentInParent<EnemyHitDamage>();
            if (breakable != null) return breakable.IsFurniture;
            if (thing != null && Game.Weapons.Blast.HasHealth(thing.gameObject, out _, out _)) return true;
            if (Game.Weapons.Blast.HasHealth(other.gameObject, out _, out _)) return true;
            return thing != null && !thing.isKinematic;
        }

        private void Smash(Collider other, Vector3 point)
        {
            var thing = other.attachedRigidbody;
            // Candy is the player's: passed, not smashed.
            if ((thing != null ? thing.name : other.name).StartsWith("Candy")) return;
            if (!smashed.Add(thing != null ? thing : other)) return;
            Game.Weapons.Blast.Damage(other, point, smashDamage, false);
            if (thing != null && !thing.isKinematic)
                thing.AddForce(body.linearVelocity.normalized * smashShove + Vector3.up * (smashShove * 0.3f), ForceMode.VelocityChange);
            PlaySplat(point, 0.5f);
            if (splatSound != null) AudioSource.PlayClipAtPoint(splatSound, point, volume);
        }

        private sealed class NearestFirst : System.Collections.Generic.IComparer<RaycastHit>
        {
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }

        private void HitSomething(Collider other, Vector3 point)
        {
            if (IsHeldOrFist(other))
            {
                Splat(point, splatSound);
                return;
            }
            if (IsPlayer(other) && Game.Weapons.LegendaryGear.TryBlock(lastPosition))
            {
                Splat(point, splatSound);
                return;
            }
            if (IsPlayer(other))
            {
                DamageVignette.HitFrom(shooter != null ? shooter.position : point);
                SendToPlayer(playerEvent, playerFsm);
                PlayerWeb.Add(webAdded);
                if (poisonSeconds > 0f && poisonPerSecond > 0f) PlayerPoison.Add(poisonPerSecond, poisonSeconds);
                Splat(point, hitSound != null ? hitSound : splatSound);
                return;
            }
            Splat(point, splatSound);
        }

        private void Splat(Vector3 point, AudioClip sound)
        {
            done = true;
            PlaySplat(point, 1f);
            if (sound != null) AudioSource.PlayClipAtPoint(sound, point, volume);
            Destroy(gameObject);
        }

        // Its splat effect at Splat Scale (times Times), from the shared pool instead of a new copy each time: the same
        // size as before, whatever the effect prefab's own root scale (Smoke_Flash_Small_Puff's is 3).
        private void PlaySplat(Vector3 point, float times)
        {
            if (splatEffect == null) return;
            float rootScale = Mathf.Max(0.0001f, splatEffect.transform.localScale.x);
            EffectPool.Play(splatEffect, point, splatScale * times / rootScale);
        }

        private static bool IsPlayer(Collider other)
        {
            return other.GetComponentInParent<XROrigin>() != null;
        }

        private static bool IsHeldOrFist(Collider other)
        {
            var held = other.attachedRigidbody;
            if (held == null) return false;
            if (Fist.IsFistBall(held)) return true;
            return held.TryGetComponent(out XRGrabInteractable grab) && grab.isSelected;
        }

        private static void SendToPlayer(string eventName, string fsmName)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            var global = FsmVariables.GlobalVariables.FindFsmGameObject(PlayerVariable);
            var origin = FindAnyObjectByType<XROrigin>();
            var target = global != null && global.Value != null ? global.Value : origin != null ? origin.gameObject : null;
            if (target == null) return;
            foreach (var fsm in target.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == fsmName) fsm.SendEvent(eventName);
            }
        }
    }
}
