using System.Collections;
using System.Collections.Generic;
using Game.Combat;
using Game.Enemies;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;

namespace Game.PlayGround
{
    /// <summary>
    /// The PlayGround's haunted tree. Left alone, it lets a ghost balloon out of its crown every so often (up to Max
    /// Ghosts at a time). Hit it with something (weapons, thrown things, grenades) and it jiggles; enough hits chop it
    /// down: it drops a few logs of firewood for the cauldron and leaves a bush behind, which pops back up into a tree
    /// when nobody's watching. Goes on Big_Tree_01_04's root.
    /// </summary>
    [DisallowMultipleComponent]
    public class GhostTree : MonoBehaviour, IHittable
    {
        [Header("Ghosts")]
        [Tooltip("Ghost balloons it lets out; one at random each time.")]
        [SerializeField] private GameObject[] ghosts = System.Array.Empty<GameObject>();
        [Tooltip("Seconds between ghosts, picked at random between these.")]
        [Min(1f)] [SerializeField] private float minSeconds = 20f;
        [Min(1f)] [SerializeField] private float maxSeconds = 40f;
        [Tooltip("Most of its ghosts floating about at once.")]
        [Min(0)] [SerializeField] private int maxGhosts = 3;
        [Tooltip("How high above the crown its ghosts float, in metres.")]
        [SerializeField] private float ghostHeight = 2f;
        [Tooltip("How far around the crown they spread, in metres.")]
        [Min(0f)] [SerializeField] private float ghostSpread = 2.5f;

        [Header("Chopping down")]
        [Tooltip("Damage it takes before it falls: a hit does 1 per 4 m/s of speed (at least 1), a grenade 3.")]
        [Min(1f)] [SerializeField] private float health = 12f;
        [Tooltip("Hits slower than this, in metres per second, don't hurt it.")]
        [Min(0f)] [SerializeField] private float minHitSpeed = 3f;
        [SerializeField] private AudioClip[] hitSounds = System.Array.Empty<AudioClip>();
        [SerializeField] private AudioClip fallSound;
        [Tooltip("Burst where it falls, e.g. smoke.")]
        [SerializeField] private GameObject fallEffect;
        [Min(0.01f)] [SerializeField] private float fallEffectScale = 0.2f;

        [Header("When it falls")]
        [Tooltip("Firewood it drops (Dropped_Wood_Fuel), and how many.")]
        [SerializeField] private GameObject fuel;
        [Min(0)] [SerializeField] private int minFuel = 2;
        [Min(0)] [SerializeField] private int maxFuel = 4;
        [Tooltip("What's left where it stood (bushes_01).")]
        [SerializeField] private GameObject leftBehind;

        private const string SpawnedStuffVariable = "CurrentlySpawnedStuffs";

        private readonly List<GameObject> alive = new();
        private DamageJiggle jiggle;
        private float damageLeft;
        private float nextGhost;
        private bool felled;

        private void Awake()
        {
            // Its colliders are on its parts; a body that doesn't move gathers their hits here.
            if (!TryGetComponent<Rigidbody>(out var body))
            {
                body = gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true;
            }
            damageLeft = health;
            jiggle = GetComponent<DamageJiggle>();
            nextGhost = Time.time + Random.Range(minSeconds, Mathf.Max(minSeconds, maxSeconds));
        }

        private void Update()
        {
            if (felled || Time.time < nextGhost) return;

            nextGhost = Time.time + Random.Range(minSeconds, Mathf.Max(minSeconds, maxSeconds));
            alive.RemoveAll(ghost => ghost == null);
            if (alive.Count < maxGhosts) LetGhostOut();
        }

        /// <summary>Pops it up from small, e.g. when a bush turns back into it.</summary>
        public void GrowIn(float seconds)
        {
            StartCoroutine(Grow(seconds));
        }

        // Hit by something the player holds or threw, or by the player.
        private void OnCollisionEnter(Collision collision)
        {
            if (!PlayerHit.From(collision)) return;
            float speed = collision.relativeVelocity.magnitude;
            if (speed < minHitSpeed) return;
            Hit(speed / 4f, collision.contactCount > 0 ? collision.GetContact(0).point : transform.position);
        }

        public void Hit(float strength, Vector3 point)
        {
            if (felled) return;

            float damage = Mathf.Max(1f, strength);
            damageLeft -= damage;
            if (jiggle != null) jiggle.Jiggle(damage);
            if (hitSounds.Length > 0) Play(hitSounds[Random.Range(0, hitSounds.Length)], point);
            if (damageLeft <= 0f) Fall();
        }

        private void LetGhostOut()
        {
            if (ghosts.Length == 0) return;
            var prefab = ghosts[Random.Range(0, ghosts.Length)];
            if (prefab == null) return;

            var crown = Crown();
            var spread = Random.insideUnitCircle * ghostSpread;
            var home = crown + new Vector3(spread.x, ghostHeight + Random.Range(0f, 1f), spread.y);
            var ghost = GhostBalloon.Spawn(prefab, crown, home, SpawnedStuff());
            if (ghost != null) alive.Add(ghost.gameObject);
        }

        // Logs tumble down around the trunk; a bush stays where it stood.
        private void Fall()
        {
            felled = true;
            var foot = transform.position;
            Play(fallSound, foot + Vector3.up);
            if (fallEffect != null)
            {
                var effect = Instantiate(fallEffect, foot + Vector3.up, Quaternion.identity);
                effect.transform.localScale = Vector3.one * fallEffectScale;
                Destroy(effect, 4f);
            }

            if (fuel != null)
            {
                var parent = SpawnedStuff();
                int logs = Random.Range(Mathf.Min(minFuel, maxFuel), maxFuel + 1);
                for (int i = 0; i < logs; i++)
                {
                    var around = Random.insideUnitCircle.normalized;
                    var spot = foot + new Vector3(around.x * 0.7f, Random.Range(0.6f, 1.6f), around.y * 0.7f);
                    var log = Instantiate(fuel, spot, Random.rotation, parent);
                    if (log.TryGetComponent<Rigidbody>(out var body) && !body.isKinematic)
                        body.linearVelocity = new Vector3(around.x * 1.5f, 1.5f, around.y * 1.5f);
                }
            }

            if (leftBehind != null) Instantiate(leftBehind, foot, transform.rotation, transform.parent);
            Destroy(gameObject);
        }

        private IEnumerator Grow(float seconds)
        {
            var full = transform.localScale;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                // Springs up a little past full size and settles back.
                transform.localScale = full * Mathf.LerpUnclamped(0.15f, 1f, Overshoot(t / seconds));
                yield return null;
            }
            transform.localScale = full;
        }

        private static float Overshoot(float t)
        {
            const float back = 1.7f;
            float u = t - 1f;
            return 1f + (back + 1f) * u * u * u + back * u * u;
        }

        // Near the top of the tree, where its leaves are.
        private Vector3 Crown()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return transform.position + Vector3.up * 4f;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return new Vector3(bounds.center.x, bounds.max.y - bounds.extents.y * 0.3f, bounds.center.z);
        }

        private static void Play(AudioClip clip, Vector3 at)
        {
            if (clip != null) AudioSource.PlayClipAtPoint(clip, at);
        }

        private static Transform SpawnedStuff()
        {
            var holder = FsmVariables.GlobalVariables.FindFsmGameObject(SpawnedStuffVariable);
            return holder != null && holder.Value != null ? holder.Value.transform : null;
        }
    }

    /// <summary>Whether a collision is the player hitting something: their body or hands, or something they hold or threw.</summary>
    public static class PlayerHit
    {
        public static bool From(Collision collision)
        {
            if (collision.collider.GetComponentInParent<XROrigin>() != null) return true;
            return collision.rigidbody != null && collision.rigidbody.GetComponentInParent<XRGrabInteractable>() != null;
        }
    }
}
