using System.Collections;
using System.Collections.Generic;
using Game.Combat;
using Game.Enemies;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using FsmFloat = HutongGames.PlayMaker.FsmFloat;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;

namespace Game.PlayGround
{
    /// <summary>
    /// The PlayGround's haunted tree (or anything that should act like one, e.g. the giant skull). Left alone, it lets a
    /// ghost balloon out of its top every so often (up to Max Ghosts at a time). Hit it with something (weapons, thrown
    /// things, grenades) and it jiggles; enough hits bring it down: it drops its firewood (if it has any) and leaves
    /// something behind (a bush), which pops back up into it when nobody's watching. With its own Damage and Health
    /// FSMs, those count the hits and break it (sounds, smoke, pieces); this then only jiggles it as their health drops
    /// and, when it reaches 0, leaves the bush and the firewood. Removed any other way (popped by the fairy's shield at
    /// the end of a stage, or the PlayGround cleared away), it leaves nothing. Goes on the root of Big_Tree_01_04,
    /// Giant_Skull, ...
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
        [Tooltip("Damage it takes before it falls: a hit does 1 per 4 m/s of speed (at least 1), a grenade 3. Not used " +
                 "when it has its own Damage and Health FSMs: their health counts instead.")]
        [Min(1f)] [SerializeField] private float health = 12f;
        [Tooltip("Hits slower than this, in metres per second, don't hurt it.")]
        [Min(0f)] [SerializeField] private float minHitSpeed = 3f;
        [SerializeField] private AudioClip[] hitSounds = System.Array.Empty<AudioClip>();
        [SerializeField] private AudioClip fallSound;
        [Tooltip("Burst where it falls, e.g. smoke.")]
        [SerializeField] private GameObject fallEffect;
        [Min(0.01f)] [SerializeField] private float fallEffectScale = 0.2f;

        [Header("When it falls")]
        [Tooltip("Firewood it drops (Dropped_Wood_Fuel), and how many. Empty: none.")]
        [SerializeField] private GameObject fuel;
        [Min(0)] [SerializeField] private int minFuel = 2;
        [Min(0)] [SerializeField] private int maxFuel = 4;
        [Tooltip("What's left where it stood (a bush whose Tree is this one, e.g. bushes_01 for the tree, " +
                 "bushes_01_Skull for the skull). Left only when it breaks, not when the fairy's shield pops it.")]
        [SerializeField] private GameObject leftBehind;

        private const string SpawnedStuffVariable = "CurrentlySpawnedStuffs";
        // With its own Health FSM, that removes it; if it hasn't after this many seconds, it goes anyway.
        private const float FsmRemoveSeconds = 3f;

        private readonly List<GameObject> alive = new();
        private DamageJiggle jiggle;
        private FsmFloat fsmHealth;
        private float lastFsmHealth;
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

        private void Start()
        {
            fsmHealth = FindFsmHealth();
            if (fsmHealth != null) lastFsmHealth = fsmHealth.Value;
        }

        private void Update()
        {
            if (felled || Time.time < nextGhost) return;

            nextGhost = Time.time + Random.Range(minSeconds, Mathf.Max(minSeconds, maxSeconds));
            alive.RemoveAll(ghost => ghost == null);
            if (alive.Count < maxGhosts) LetGhostOut();
        }

        // With its own Health FSM: jiggles as that health drops, and falls when it's gone. After every Update, so a
        // Health FSM that removes it this frame can't beat it to it.
        private void LateUpdate()
        {
            if (fsmHealth == null || felled) return;

            float now = fsmHealth.Value;
            if (now < lastFsmHealth && jiggle != null) jiggle.Jiggle(lastFsmHealth - now);
            lastFsmHealth = now;
            if (now <= 0f) Fall();
        }

        /// <summary>Pops it up from small, e.g. when a bush turns back into it.</summary>
        public void GrowIn(float seconds)
        {
            StartCoroutine(Grow(seconds));
        }

        // Hit by something the player holds or threw, or by the player. Its own Damage FSM, if it has one, counts hits instead.
        private void OnCollisionEnter(Collision collision)
        {
            if (fsmHealth != null || !PlayerHit.From(collision)) return;
            float speed = collision.relativeVelocity.magnitude;
            if (speed < minHitSpeed) return;
            Hit(speed / 4f, collision.contactCount > 0 ? collision.GetContact(0).point : transform.position);
        }

        public void Hit(float strength, Vector3 point)
        {
            if (felled) return;

            float damage = Mathf.Max(1f, strength);
            if (fsmHealth != null)
            {
                // Its Health FSM's health takes it; LateUpdate jiggles it and sees whether it falls.
                fsmHealth.Value -= damage;
                return;
            }
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

        // Logs tumble down around the trunk; a bush stays where it stood. Its own Health FSM, if it has one, breaks it
        // (e.g. into pieces) and removes it; otherwise it goes now.
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
            Destroy(gameObject, fsmHealth != null ? FsmRemoveSeconds : 0f);
        }

        // The health of its own Health FSM, when it also has a Damage FSM to lower it (the PlayMaker way of breaking
        // things, which grenades use too); null otherwise.
        private FsmFloat FindFsmHealth()
        {
            PlayMakerFSM damageFsm = null;
            PlayMakerFSM healthFsm = null;
            foreach (var fsm in GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == "Damage") damageFsm = fsm;
                else if (fsm.FsmName.EndsWith("Health", System.StringComparison.OrdinalIgnoreCase)) healthFsm = fsm;
            }
            return damageFsm != null && healthFsm != null ? healthFsm.FsmVariables.FindFsmFloat("health") : null;
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

        // Near its top (the tree's leaves, the skull's crown). Only its meshes count, not effects like a smoke puff.
        private Vector3 Crown()
        {
            var bounds = new Bounds();
            bool any = false;
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r is not MeshRenderer && r is not SkinnedMeshRenderer) continue;
                if (any) bounds.Encapsulate(r.bounds);
                else bounds = r.bounds;
                any = true;
            }
            if (!any) return transform.position + Vector3.up * 4f;
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
