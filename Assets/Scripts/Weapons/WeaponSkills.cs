using System.Collections.Generic;
using Game.Combat;
using Game.Leveling;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game.Weapons
{
    /// <summary>
    /// What a thing the player has picked up does with the Throw Power and Weapon Efficiency unlocks (user, 2026-11-12;
    /// numbers and looks: Blast Skills; unlocks: Player Level):
    /// - thrown (let go by a hand), its first hit within a moment explodes when the throw was hard enough (Explosive
    ///   Objects), or any toss with Bomber Man (then twice). Books, bottles and flip-flops (Grenade) do their own;
    /// - held in a hand, a hit that knocks a loose thing or an enemy marks it: if it meets another thing or enemy within
    ///   Knock Seconds, an impact explodes there, hurting both (Impact);
    /// - held in a hand, a hit on something that can be hurt explodes there (Weapon Master).
    /// Player Level puts it on everything the player picks up (Watch); it costs nothing while the thing lies about.
    /// </summary>
    [DisallowMultipleComponent]
    public class WeaponSkills : MonoBehaviour
    {
        // XR Toolkit gives a thrown thing its speed at the end of the frame it's let go: the throw is judged over this.
        private const float ThrowWindow = 0.2f;
        // A thrown thing's first hit counts this long after it left the hand (Held Things counts it the player's as long).
        private const float FlightSeconds = 2.5f;

        private XRGrabInteractable grab;
        private Rigidbody body;
        private bool flying;
        private float releasedAt;
        private float throwSpeed;
        private float nextMaster;
        private bool isGrenade;

        /// <summary>Puts it on a thing the player just picked up (once).</summary>
        public static void Watch(GameObject thing)
        {
            if (thing == null || thing.TryGetComponent<WeaponSkills>(out _) || !thing.TryGetComponent<XRGrabInteractable>(out _)) return;
            thing.AddComponent<WeaponSkills>();
        }

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
            isGrenade = GetComponent<Grenade>() != null;
            if (grab != null) grab.selectExited.AddListener(OnLetGo);
            var skills = BlastSkills.Asset;
            if (skills != null) skills.Warm();
        }

        private void OnDestroy()
        {
            if (grab != null) grab.selectExited.RemoveListener(OnLetGo);
        }

        // Let go by a hand (not passed to the other hand): it's flying now.
        private void OnLetGo(SelectExitEventArgs args)
        {
            if (grab.isSelected || args.interactorObject is not XRBaseInputInteractor) return;
            flying = true;
            releasedAt = Time.time;
            throwSpeed = 0f;
        }

        private void FixedUpdate()
        {
            if (grab != null && grab.isSelected) TrackHeldSpeed();
            else heldSamples = 0;
            if (!flying) return;
            if (Time.time - releasedAt > FlightSeconds || grab.isSelected)
            {
                flying = false;
                return;
            }
            if (body != null && Time.time - releasedAt <= ThrowWindow) throwSpeed = Mathf.Max(throwSpeed, ThrownSpeedNow());
        }

        // Its speed from the throw itself: the downward speed gravity has added since it left the hand is taken off, so
        // letting go of a thing above the floor isn't a toss (play test 2026-11-12: a crate let go 28 cm up reached
        // 1.5 m/s by falling and blew up twice with Bomber Man). Only falling speed is taken off, never more than it has:
        // a thing lying still, or going up, keeps its own speed.
        private float ThrownSpeedNow()
        {
            var velocity = body.linearVelocity;
            float g = Physics.gravity.magnitude;
            if (body.useGravity && g > 0f)
            {
                var down = Physics.gravity / g;
                float falling = Vector3.Dot(velocity, down);
                if (falling > 0f)
                {
                    float fromThrow = Mathf.Max(0f, falling - g * Mathf.Max(0f, Time.time - releasedAt));
                    velocity -= down * (falling - fromThrow);
                }
            }
            return velocity.magnitude;
        }

        // A held weapon follows the hand by physics, and its speed dips to almost nothing on some physics steps (play
        // test 2026-11-12: 5.3 5.0 3.9 5.1 4.0 4.5 then 0.4 m/s on the step it hit), so a hit is judged by its fastest
        // speed over the last few steps, not by that one step alone.
        private const int HeldSampleCount = 6;
        private readonly float[] heldSpeeds = new float[HeldSampleCount];
        private int heldSamples;
        private int heldNext;
        private Vector3 heldLastAt;

        private void TrackHeldSpeed()
        {
            if (body == null) return;
            var at = body.worldCenterOfMass;
            if (heldSamples > 0)
            {
                heldSpeeds[heldNext] = (at - heldLastAt).magnitude / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
                heldNext = (heldNext + 1) % HeldSampleCount;
            }
            heldSamples = Mathf.Min(heldSamples + 1, HeldSampleCount + 1);
            heldLastAt = at;
        }

        private float RecentHeldSpeed()
        {
            float most = 0f;
            int filled = Mathf.Min(heldSamples - 1, HeldSampleCount);
            for (int i = 0; i < filled; i++) most = Mathf.Max(most, heldSpeeds[i]);
            return most;
        }

        private void OnCollisionEnter(Collision collision)
        {
            var skills = BlastSkills.Asset;
            if (skills == null || collision.collider == null || Blast.IsPlayer(collision.collider)) return;
            if (grab != null && grab.isSelected) Held(collision, skills);
            else if (flying) Thrown(collision, skills);
        }

        // Its first hit after a throw: hard enough (Explosive Objects), or any toss (Bomber Man), explodes.
        private void Thrown(Collision collision, BlastSkills skills)
        {
            flying = false;
            if (isGrenade || body == null || !BlastSkills.Explodes(gameObject)) return;
            int level = PlayerLevel.ExplosiveLevel;
            bool bomberMan = PlayerLevel.BomberMan;
            if (level <= 0 && !bomberMan) return;
            // Hit before a physics step could measure the throw (point blank): the hit's own speed, less what gravity added.
            if (throwSpeed <= 0f && Time.time - releasedAt <= ThrowWindow)
                throwSpeed = Mathf.Max(0f, collision.relativeVelocity.magnitude - Physics.gravity.magnitude * (Time.time - releasedAt));
            float scale = grab != null ? grab.throwVelocityScale : 1f;
            float needed = bomberMan ? skills.TossHandSpeed * scale : skills.HardThrowHandSpeed * scale * PlayerLevel.HardThrowShare;
            if (throwSpeed < needed) return;
            var point = collision.contactCount > 0 ? collision.GetContact(0).point : body.worldCenterOfMass;
            skills.ThrownBlast(point, body, level, bomberMan, PlayerLevel.CandyBlastReach, PlayerLevel.MaxBlastReach);
        }

        // A hit while held: Weapon Master's blast on something that can be hurt, and a knock for an impact.
        private void Held(Collision collision, BlastSkills skills)
        {
            if (Fist.IsFistBall(collision.rigidbody)) return;
            float speed = Mathf.Max(collision.relativeVelocity.magnitude, RecentHeldSpeed());
            var point = collision.contactCount > 0 ? collision.GetContact(0).point : collision.transform.position;
            if (PlayerLevel.WeaponMaster && speed >= skills.MasterHitSpeed && Time.time >= nextMaster && BlastSkills.CanBeHurt(collision.collider))
            {
                nextMaster = Time.time + skills.MasterEvery;
                skills.MasterBlast(point, body);
            }
            int impact = PlayerLevel.ImpactLevel;
            if (impact > 0 && speed >= skills.KnockHitSpeed) Knocked.Mark(collision, impact, body, skills.KnockSeconds);
        }

        /// <summary>
        /// A thing or an enemy just knocked by a held weapon (Impact): meeting another thing or enemy within Knock Seconds
        /// (after it has moved a little: not one it was touching already) makes an impact there, once.
        /// </summary>
        private class Knocked : MonoBehaviour
        {
            // It must have moved this far from where it was hit, in metres, before a meeting counts...
            private const float MinMove = 0.15f;
            // ...and be moving at least this fast (m/s), so a thing drifting against another doesn't go off.
            private const float MinSpeed = 1f;
            // How close counts as meeting, in metres (round its box).
            private const float Touch = 0.04f;

            private static readonly Collider[] near = new Collider[32];

            private readonly List<Collider> own = new();
            private readonly HashSet<Collider> touchingAtStart = new();
            private Rigidbody weapon;
            private int level;
            private float until;
            private Vector3 start;
            private Vector3 last;

            /// <summary>Marks the thing the weapon hit, if it's a loose thing or an enemy (not the world, candy or a keep).</summary>
            public static void Mark(Collision collision, int level, Rigidbody weapon, float seconds)
            {
                var target = Target(collision);
                if (target == null || !BlastSkills.Explodes(target)) return;
                if (!target.TryGetComponent(out Knocked knocked)) knocked = target.AddComponent<Knocked>();
                knocked.Begin(level, weapon, seconds);
            }

            // A loose thing (a body physics moves, not too heavy, not in a hand) or an enemy (Enemy Hit Damage, not furniture).
            private static GameObject Target(Collision collision)
            {
                var hitBody = collision.rigidbody;
                var enemy = collision.collider.GetComponentInParent<EnemyHitDamage>();
                if (enemy != null && !enemy.IsFurniture && !enemy.IsDead) return hitBody != null ? hitBody.gameObject : enemy.gameObject;
                if (hitBody == null || hitBody.isKinematic || PushWeight.IsImmovable(hitBody) || Fist.IsFistBall(hitBody)) return null;
                if (hitBody.TryGetComponent(out XRGrabInteractable held) && held.isSelected) return null;
                return hitBody.gameObject;
            }

            private void Begin(int newLevel, Rigidbody by, float seconds)
            {
                level = newLevel;
                weapon = by;
                until = Time.time + seconds;
                start = last = transform.position;
                own.Clear();
                GetComponentsInChildren(own);
                touchingAtStart.Clear();
                int count = Overlap();
                for (int i = 0; i < count; i++) touchingAtStart.Add(near[i]);
                enabled = true;
            }

            private void FixedUpdate()
            {
                if (Time.time > until)
                {
                    enabled = false;
                    return;
                }
                var now = transform.position;
                float speed = (now - last).magnitude / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
                last = now;
                if ((now - start).sqrMagnitude < MinMove * MinMove || speed < MinSpeed) return;
                int count = Overlap();
                for (int i = 0; i < count; i++)
                {
                    var other = near[i];
                    if (other == null || touchingAtStart.Contains(other) || own.Contains(other) || Blast.IsPlayer(other)) continue;
                    var otherBody = other.attachedRigidbody;
                    if (otherBody != null && (otherBody == weapon || Fist.IsFistBall(otherBody))) continue;
                    if (otherBody != null && otherBody.TryGetComponent(out XRGrabInteractable held) && held.isSelected) continue;
                    // Another thing or enemy: one that can be hurt, or a loose thing.
                    bool loose = otherBody != null && !otherBody.isKinematic && !PushWeight.IsImmovable(otherBody);
                    if (!loose && !BlastSkills.CanBeHurt(other)) continue;
                    var middle = Bounds().center;
                    var point = (other.ClosestPoint(middle) + middle) * 0.5f;
                    enabled = false;
                    var skills = BlastSkills.Asset;
                    if (skills != null) skills.ImpactBlast(point, level, weapon);
                    return;
                }
            }

            private Bounds Bounds()
            {
                var box = new Bounds(transform.position, Vector3.zero);
                bool any = false;
                foreach (var part in own)
                {
                    if (part == null || !part.enabled || part.isTrigger) continue;
                    if (!any)
                    {
                        box = part.bounds;
                        any = true;
                    }
                    else
                    {
                        box.Encapsulate(part.bounds);
                    }
                }
                return box;
            }

            private int Overlap()
            {
                var box = Bounds();
                return Physics.OverlapBoxNonAlloc(box.center, box.extents + Vector3.one * Touch, near, Quaternion.identity,
                    Physics.AllLayers, QueryTriggerInteraction.Ignore);
            }
        }
    }
}
