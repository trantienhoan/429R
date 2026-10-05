using System.Collections.Generic;
using Game.Combat;
using Game.Enemies;
using UnityEngine;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;

namespace Game.PlayGround
{
    /// <summary>
    /// The bush a chopped-down ghost tree leaves. Hit it and ghost balloons fly out of it: the harder the hit, the more
    /// (1 per 4 m/s of speed, a grenade 3). It grows back into the tree when nobody's watching: once it's been left
    /// alone a while (each hit starts that over), and the player is away from it and can't see it or where the tree
    /// would stand (it's outside their view, or behind something), the tree pops up in its place. Goes on bushes_01's
    /// root.
    /// </summary>
    [DisallowMultipleComponent]
    public class GhostBush : MonoBehaviour, IHittable
    {
        [Header("Ghosts")]
        [Tooltip("Ghost balloons that fly out; one at random each.")]
        [SerializeField] private GameObject[] ghosts = System.Array.Empty<GameObject>();
        [Tooltip("Hits slower than this, in metres per second, don't let any out.")]
        [Min(0f)] [SerializeField] private float minHitSpeed = 1f;
        [Tooltip("Most ghosts one hit lets out.")]
        [Min(1)] [SerializeField] private int maxGhostsPerHit = 3;
        [Tooltip("Most of its ghosts floating about at once.")]
        [Min(1)] [SerializeField] private int maxGhosts = 6;
        [Tooltip("How high above the bush they float, in metres.")]
        [SerializeField] private float ghostHeight = 2.5f;
        [Tooltip("How far around the bush they spread, in metres.")]
        [Min(0f)] [SerializeField] private float ghostSpread = 2f;
        [Tooltip("Seconds after a hit before another hit lets more out.")]
        [Min(0f)] [SerializeField] private float cooldown = 0.5f;
        [SerializeField] private AudioClip hitSound;

        [Header("Growing back (when nobody's watching)")]
        [Tooltip("The tree it grows back into (Big_Tree_01_04).")]
        [SerializeField] private GameObject tree;
        [Tooltip("Seconds it stays a bush at least; each hit starts the wait over.")]
        [Min(0f)] [SerializeField] private float minSeconds = 15f;
        [Tooltip("It only grows back while the player is farther from it than this, in metres along the floor...")]
        [Min(0f)] [SerializeField] private float nearDistance = 4f;
        [Tooltip("...and can't see it: it's more than this many degrees from where they look, or behind something.")]
        [Range(10f, 180f)] [SerializeField] private float viewAngle = 60f;
        [Tooltip("Seconds the player has to be away and not looking before it grows back.")]
        [Min(0f)] [SerializeField] private float unseenSeconds = 1f;
        [Tooltip("Seconds the tree takes to pop up to full size.")]
        [Min(0f)] [SerializeField] private float growSeconds = 1.5f;
        [SerializeField] private AudioClip growSound;
        [SerializeField] private GameObject growEffect;
        [Min(0.01f)] [SerializeField] private float growEffectScale = 0.2f;

        private const string SpawnedStuffVariable = "CurrentlySpawnedStuffs";
        private const float CheckEvery = 0.25f;
        // Spots looked at up where the tree would stand, from the bush to its crown.
        private const int Spots = 4;
        private static readonly RaycastHit[] Hits = new RaycastHit[8];

        private readonly List<GameObject> alive = new();
        private DamageJiggle jiggle;
        private Transform head;
        private float leftAlone;
        private float nextRelease;
        private float nextCheck;
        private float unseenSince = -1f;
        private float treeHeight = 4f;
        private float treeRadius = 1.5f;

        private void Awake()
        {
            jiggle = GetComponent<DamageJiggle>();
            MeasureTree();
        }

        private void Update()
        {
            leftAlone += Time.deltaTime;
            if (leftAlone < minSeconds || Time.time < nextCheck) return;
            nextCheck = Time.time + CheckEvery;

            if (Watched())
            {
                unseenSince = -1f;
                return;
            }
            if (unseenSince < 0f) unseenSince = Time.time;
            if (Time.time - unseenSince >= unseenSeconds) GrowBack();
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
            leftAlone = 0f;
            if (jiggle != null) jiggle.Jiggle(Mathf.Max(1f, strength));
            if (Time.time < nextRelease) return;
            nextRelease = Time.time + cooldown;

            if (hitSound != null) AudioSource.PlayClipAtPoint(hitSound, point);
            alive.RemoveAll(ghost => ghost == null);
            int wanted = Mathf.Clamp(Mathf.FloorToInt(strength), 1, maxGhostsPerHit);
            for (int i = 0; i < wanted && alive.Count < maxGhosts; i++) LetGhostOut();
        }

        private void LetGhostOut()
        {
            if (ghosts.Length == 0) return;
            var prefab = ghosts[Random.Range(0, ghosts.Length)];
            if (prefab == null) return;

            var top = Top();
            var spread = Random.insideUnitCircle * ghostSpread;
            var home = top + new Vector3(spread.x, ghostHeight + Random.Range(0f, 1f), spread.y);
            var ghost = GhostBalloon.Spawn(prefab, top, home, SpawnedStuff());
            if (ghost != null) alive.Add(ghost.gameObject);
        }

        // Whether the player is near, or could see the bush or the tree that would grow there.
        private bool Watched()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (head == null) return false;

            var foot = transform.position;
            var away = head.position - foot;
            away.y = 0f;
            if (away.sqrMagnitude < nearDistance * nearDistance) return true;

            for (int i = 0; i <= Spots; i++)
            {
                if (CanSee(foot + Vector3.up * (treeHeight * i / Spots))) return true;
            }
            return false;
        }

        private bool CanSee(Vector3 spot)
        {
            var eye = head.position;
            var look = spot - eye;
            float distance = look.magnitude;
            if (distance < 0.01f) return true;

            // The tree's width counts too: just outside their view, its leaves would still show.
            float edge = Mathf.Atan2(treeRadius, distance) * Mathf.Rad2Deg;
            if (Vector3.Angle(head.forward, look) - edge > viewAngle) return false;
            return !Hidden(eye, look / distance, distance);
        }

        // Something solid in the way that the player can see (a wall, a bench; not things that move about).
        private bool Hidden(Vector3 eye, Vector3 direction, float distance)
        {
            int count = Physics.RaycastNonAlloc(eye, direction, Hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var blocker = Hits[i].collider;
                if (blocker.attachedRigidbody != null || blocker.transform.IsChildOf(transform)) continue;
                if (blocker.TryGetComponent<Renderer>(out var shown) && shown.enabled) return true;
            }
            return false;
        }

        // How tall and wide the tree it grows into is, from its prefab.
        private void MeasureTree()
        {
            if (tree == null) return;
            var root = tree.transform;
            var shape = new Bounds();
            bool any = false;
            foreach (var filter in tree.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var b = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    var point = filter.transform.TransformPoint(corner) - root.position;
                    if (any) shape.Encapsulate(point);
                    else shape = new Bounds(point, Vector3.zero);
                    any = true;
                }
            }
            if (!any) return;
            treeHeight = Mathf.Max(1f, shape.max.y);
            treeRadius = Mathf.Max(0.5f, Mathf.Max(shape.extents.x, shape.extents.z));
        }

        private void GrowBack()
        {
            if (tree != null)
            {
                var grown = Instantiate(tree, transform.position, transform.rotation, transform.parent);
                if (grown.TryGetComponent<GhostTree>(out var ghostTree)) ghostTree.GrowIn(growSeconds);
            }
            if (growSound != null) AudioSource.PlayClipAtPoint(growSound, transform.position + Vector3.up);
            if (growEffect != null)
            {
                var effect = Instantiate(growEffect, transform.position + Vector3.up * 0.5f, Quaternion.identity);
                effect.transform.localScale = Vector3.one * growEffectScale;
                Destroy(effect, 4f);
            }
            Destroy(gameObject);
        }

        private Vector3 Top()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return transform.position + Vector3.up;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        }

        private static Transform SpawnedStuff()
        {
            var holder = FsmVariables.GlobalVariables.FindFsmGameObject(SpawnedStuffVariable);
            return holder != null && holder.Value != null ? holder.Value.transform : null;
        }
    }
}
