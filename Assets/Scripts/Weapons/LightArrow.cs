using System.Collections.Generic;
using Game.Combat;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game.Weapons
{
    /// <summary>
    /// An arrow of light shot by the Magic Bow. It flies nearly straight (a little Gravity), points where it flies, and
    /// bursts into sparks on the first thing it hits. It hurts what it hits like any thrown weapon (its Weapon Power
    /// times how fast it was going), so a fully drawn shot hits hardest: enemies take damage and get knocked back,
    /// pumpkins and furniture break by their own FSMs, and the hand holding the bow buzzes when it hurts something. It
    /// never hits the bow or the player. Arrows are kept and used again (there are endless arrows), and one that hits
    /// nothing goes out after Life Seconds.
    /// Tools > 429 Game > Weapons > Build Magic Bow makes the prefab (Light_Arrow).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class LightArrow : MonoBehaviour
    {
        [Tooltip("Share of normal gravity pulling it down: 0 = dead straight, 1 = like a thrown thing.")]
        [Min(0f)]
        [SerializeField] private float gravity = 0.15f;
        [Tooltip("Seconds it flies before it goes out, if it hits nothing.")]
        [Min(0.1f)]
        [SerializeField] private float lifeSeconds = 3f;
        [Tooltip("Shown flying (its glowing body); hidden as it hits. The trail fades out by itself.")]
        [SerializeField] private GameObject body;
        [SerializeField] private TrailRenderer trail;
        [Tooltip("Sparkles along its flight; they stop as it hits.")]
        [SerializeField] private ParticleSystem sparkles;

        [Header("Hit")]
        [Tooltip("The burst where it hits (played from the Effect Pool).")]
        [SerializeField] private GameObject hitEffect;
        [Min(0f)]
        [SerializeField] private float hitEffectSize = 1f;
        [SerializeField] private AudioSource hitSound;
        [SerializeField] private AudioClip[] hitClips = System.Array.Empty<AudioClip>();

        // Kept this long after it hits, so its hit sound and trail play out before it's used again.
        private const float AfterHitSeconds = 0.6f;
        // Ignored from the start: the bow and the player (looked for around the player's head).
        private const float PlayerSearchRadius = 2.5f;

        private static readonly Dictionary<LightArrow, Queue<LightArrow>> pools = new();
        private static readonly Collider[] nearby = new Collider[64];

        private LightArrow prefab;
        private Rigidbody body3d;
        private Collider[] colliders;
        private MagicBow ignoredBow;
        private readonly HashSet<Collider> ignored = new();
        private float goneAt;
        private bool flying;
        // It hit something this physics step: it stops at the next one (stopping it inside the hit could keep the
        // thing it hit from hearing about the hit).
        private bool stopPending;

        /// <summary>The hand holding the bow that shot it (it buzzes when the arrow hurts something), or none.</summary>
        public XRBaseInputInteractor Hand { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => pools.Clear();

        /// <summary>The hand behind an arrow (for its hit buzz), or none if this isn't a flying arrow.</summary>
        public static XRBaseInputInteractor HandOf(Component thing)
        {
            return thing != null && thing.TryGetComponent(out LightArrow arrow) ? arrow.Hand : null;
        }

        /// <summary>Shoots an arrow from 'from' along 'direction' at this speed (metres per second).</summary>
        public static LightArrow Shoot(LightArrow prefab, Vector3 from, Vector3 direction, float speed, MagicBow bow,
            XRBaseInputInteractor hand)
        {
            if (!pools.TryGetValue(prefab, out var pool)) pools[prefab] = pool = new Queue<LightArrow>();
            LightArrow arrow = null;
            while (arrow == null && pool.Count > 0) arrow = pool.Dequeue();
            if (arrow == null)
            {
                arrow = Instantiate(prefab);
                arrow.prefab = prefab;
            }
            arrow.Launch(from, direction, speed, bow, hand);
            return arrow;
        }

        private void Awake()
        {
            body3d = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>(true);
        }

        private void Launch(Vector3 from, Vector3 direction, float speed, MagicBow bow, XRBaseInputInteractor hand)
        {
            Hand = hand;
            IgnoreShooter(bow);

            var rotation = Quaternion.LookRotation(direction);
            transform.SetPositionAndRotation(from, rotation);
            gameObject.SetActive(true);
            body3d.isKinematic = false;
            body3d.position = from;
            body3d.rotation = rotation;
            body3d.linearVelocity = direction * speed;
            body3d.angularVelocity = Vector3.zero;
            foreach (var c in colliders) c.enabled = true;
            if (body != null) body.SetActive(true);
            if (trail != null)
            {
                trail.Clear();
                trail.emitting = true;
            }
            if (sparkles != null)
            {
                sparkles.Clear();
                sparkles.Play();
            }
            flying = true;
            stopPending = false;
            goneAt = Time.time + lifeSeconds;
        }

        // The bow and every part of the player around it (their body, hands, fists) don't stop the arrow.
        private void IgnoreShooter(MagicBow bow)
        {
            if (bow != null && bow != ignoredBow)
            {
                ignoredBow = bow;
                foreach (var c in bow.GetComponentsInChildren<Collider>(true)) Ignore(c);
            }
            var eyes = Camera.main;
            if (eyes == null) return;
            int count = Physics.OverlapSphereNonAlloc(eyes.transform.position, PlayerSearchRadius, nearby, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var other = nearby[i];
                if (other == null || ignored.Contains(other)) continue;
                var otherBody = other.attachedRigidbody;
                if (other.GetComponentInParent<XROrigin>() != null || (otherBody != null && Fist.IsFistBall(otherBody))) Ignore(other);
            }
        }

        private void Ignore(Collider other)
        {
            if (!ignored.Add(other)) return;
            foreach (var mine in colliders) Physics.IgnoreCollision(mine, other);
        }

        private void FixedUpdate()
        {
            if (stopPending) Stop();
            if (!flying) return;
            var velocity = body3d.linearVelocity;
            if (gravity > 0f) body3d.AddForce(Physics.gravity * gravity, ForceMode.Acceleration);
            if (velocity.sqrMagnitude > 0.01f) body3d.MoveRotation(Quaternion.LookRotation(velocity));
        }

        private void Update()
        {
            if (Time.time < goneAt) return;
            if (flying || stopPending) Stop();
            else PutAway();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!flying) return;
            var point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            // A PlayMaker breakable (a pumpkin, a box) that loses health from it buzzes the bow hand; enemies with
            // Enemy Hit Damage report their own hits.
            HitBuzz.Watch(collision, body3d);
            if (hitEffect != null) EffectPool.Play(hitEffect, point, hitEffectSize);
            if (hitSound != null && hitClips.Length > 0)
            {
                hitSound.transform.position = point;
                hitSound.pitch = Random.Range(0.9f, 1.1f);
                hitSound.PlayOneShot(hitClips[Random.Range(0, hitClips.Length)]);
            }
            flying = false;
            stopPending = true;
            if (body != null) body.SetActive(false);
            if (trail != null) trail.emitting = false;
        }

        // Out: no more hits; it stays where it is, unseen, while its sound and trail play out.
        private void Stop()
        {
            flying = false;
            stopPending = false;
            foreach (var c in colliders) c.enabled = false;
            body3d.linearVelocity = Vector3.zero;
            body3d.isKinematic = true;
            if (body != null) body.SetActive(false);
            if (trail != null) trail.emitting = false;
            if (sparkles != null) sparkles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            goneAt = Time.time + AfterHitSeconds;
        }

        private void PutAway()
        {
            gameObject.SetActive(false);
            Hand = null;
            if (prefab == null) Destroy(gameObject);
            else if (pools.TryGetValue(prefab, out var pool)) pool.Enqueue(this);
        }
    }
}
