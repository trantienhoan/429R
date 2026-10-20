using System;
using System.Collections.Generic;
using Game.Enemies;
using UnityEngine;
using UnityEngine.AI;
using FsmFloat = HutongGames.PlayMaker.FsmFloat;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;
using Random = UnityEngine.Random;

namespace Game.Rooms
{
    /// <summary>
    /// Bugs hiding in a big piece of furniture (a shelf, a bed, a table, a sofa, a coffer): when it BREAKS, sometimes
    /// (Chance, 12%) a little horde of small bugs (2-7, each picked at random from Bugs) bursts out of it, one after
    /// another over half a second, each with a puff of dust and a pop, and they come for the player at once.
    /// - Only a real break counts: its Health FSM's "health" ran out (a hit, a throw, a blast, a fall). The stage
    ///   clean-ups (the Fairy Shield pop, a room or CurrentlySpawnedStuffs being destroyed) leave the health up, so no
    ///   bugs then.
    /// - The bugs go under CurrentlySpawnedStuffs (so the stage clean-ups remove them like any other enemy), on the
    ///   NavMesh under and around where the furniture stood.
    /// - They aren't part of any wave, so a wave's count and its boss call aren't changed. They do count for the Enemy
    ///   Limit like every enemy; a horde leaves Keep Free For Waves places of it free, so the waves can still spawn.
    /// Tools > 429 Game > Rooms > Add Bug Hordes To Furniture puts it on the furniture.
    /// </summary>
    [DisallowMultipleComponent]
    public class BugHorde : MonoBehaviour
    {
        // How far from the furniture's footprint a bug may land, looking for the NavMesh, in metres.
        private const float NavMeshReach = 3f;
        // The bugs land in the middle part of the footprint (this share of it), not on its very edge.
        private const float FootprintShare = 0.7f;

        [Tooltip("The chance that bugs hide in it, each time it breaks: 0.12 = 12%. 0 = never, 1 = always.")]
        [Range(0f, 1f)]
        [SerializeField] private float chance = 0.12f;
        [Tooltip("How many bugs come out: a random number from X to Y (both included).")]
        [SerializeField] private Vector2Int count = new Vector2Int(2, 7);
        [Tooltip("The bugs, one picked at random for each (put one in twice to make it come more often). Small, weak " +
                 "bugs: Enemies_Smarter/Spider_Small, Cockroach_Small, Bee_Small.")]
        [SerializeField] private GameObject[] bugs = Array.Empty<GameObject>();
        [Tooltip("They come out one after another over this many seconds.")]
        [Min(0f)]
        [SerializeField] private float burstSeconds = 0.5f;
        [Tooltip("Tick: they come for the player as soon as they're out, like a kicked nest. Untick: they look around first.")]
        [SerializeField] private bool comeAtOnce = true;
        [Tooltip("Places of the Enemy Limit (12 on Quest, 20 on PC) a horde leaves free, so the waves can still spawn: " +
                 "with this many places or fewer left, fewer bugs (or none) come out.")]
        [Min(0)]
        [SerializeField] private int keepFreeForWaves = 3;

        [Header("Puff and sound")]
        [Tooltip("The puff where each bug comes out (played from the Effect Pool). Empty = none.")]
        [SerializeField] private GameObject puff;
        [Tooltip("How big the puff is: 1 = the puff's own size.")]
        [Min(0f)]
        [SerializeField] private float puffSize = 0.35f;
        [Tooltip("The pop as each bug comes out (from where the furniture stood). Empty = none.")]
        [SerializeField] private AudioClip popSound;
        [Range(0f, 1f)]
        [SerializeField] private float popVolume = 0.5f;

        [Header("PlayMaker")]
        [Tooltip("The FSM that breaks it when its float health reaches 0. Empty = the FSM whose name ends in \"Health\".")]
        [SerializeField] private string healthFsm = "";
        [SerializeField] private string healthVariable = "health";

        private FsmFloat health;
        private bool standing;
        private bool done;
        private static bool quitting;

        /// <summary>Raised for every bug that comes out of a piece of furniture.</summary>
        public static event Action<GameObject> BugCameOut;

        /// <summary>The chance that bugs hide in it each time it breaks (0-1).</summary>
        public float Chance
        {
            get => chance;
            set => chance = Mathf.Clamp01(value);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            BugCameOut = null;
            quitting = false;
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        private static void OnQuitting() => quitting = true;

        // In Start, so its PlayMakerFSMs have set themselves up in Awake first.
        private void Start()
        {
            foreach (var fsm in GetComponents<PlayMakerFSM>())
            {
                bool holdsHealth = string.IsNullOrEmpty(healthFsm) ? fsm.FsmName.EndsWith("Health") : fsm.FsmName == healthFsm;
                if (!holdsHealth) continue;
                health = fsm.FsmVariables.FindFsmFloat(healthVariable);
                if (health != null) break;
            }
            // Only a piece that stood whole first can break.
            standing = health != null && health.Value > 0f;
            if (puff != null && chance > 0f) EffectPool.Warm(puff, 2);
        }

        // Its Health FSM breaks it as soon as the health runs out, and destroys it a moment later.
        private void Update()
        {
            if (done || !standing) return;
            if (health.Value <= 0f) Broke();
        }

        // In case the health ran out and it was destroyed in the same frame, before Update looked.
        private void OnDestroy()
        {
            // Gone with the scene (a reload, Re-Dream) or the game closing: nothing broke.
            if (done || !standing || quitting || !gameObject.scene.isLoaded) return;
            if (health.Value <= 0f) Broke();
        }

        private void Broke()
        {
            done = true;
            if (chance <= 0f || Random.value > chance || bugs == null || bugs.Length == 0) return;

            // No CurrentlySpawnedStuffs: the stage is being set up again (game over), so no bugs.
            var stuff = FsmVariables.GlobalVariables.FindFsmGameObject("CurrentlySpawnedStuffs");
            if (stuff == null || stuff.Value == null) return;

            int bugCount = Random.Range(Mathf.Max(0, count.x), Mathf.Max(count.x, count.y) + 1);
            if (bugCount <= 0) return;

            var area = Footprint();
            var horde = new GameObject("Bug Horde (" + name + ")");
            horde.transform.SetParent(stuff.Value.transform, false);
            horde.transform.position = area.center;
            horde.AddComponent<Burst>().Begin(this, bugCount, area);
        }

        // Where it stood: its solid colliders together (its renderers if it has none).
        private Bounds Footprint()
        {
            var bounds = new Bounds(transform.position, Vector3.zero);
            bool any = false;
            foreach (var c in GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger || !c.enabled) continue;
                if (any) bounds.Encapsulate(c.bounds);
                else bounds = c.bounds;
                any = true;
            }
            if (any) return bounds;
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (any) bounds.Encapsulate(r.bounds);
                else bounds = r.bounds;
                any = true;
            }
            return bounds;
        }

        /// <summary>Lets the bugs out one after another, then removes itself. It lives under CurrentlySpawnedStuffs, so
        /// a stage clean-up that clears that stops it too.</summary>
        private class Burst : MonoBehaviour
        {
            private GameObject[] bugs;
            private bool comeAtOnce;
            private int keepFree;
            private GameObject puff;
            private float puffSize;
            private AudioClip pop;
            private float popVolume;
            private UnityEngine.Audio.AudioMixerGroup popMixer;
            // One speaker per pop: a speaker's pitch changes every sound still playing on it, so each pop gets its own
            // to keep its own pitch.
            private readonly List<AudioSource> speakers = new();
            private Bounds area;
            private int left;
            private float gap;
            private float next;

            public void Begin(BugHorde from, int bugCount, Bounds where)
            {
                // Copied: the furniture is destroyed in a moment.
                bugs = (GameObject[])from.bugs.Clone();
                comeAtOnce = from.comeAtOnce;
                keepFree = from.keepFreeForWaves;
                puff = from.puff;
                puffSize = from.puffSize;
                pop = from.popSound;
                popVolume = from.popVolume;
                area = where;
                left = bugCount;
                gap = bugCount > 1 ? from.burstSeconds / (bugCount - 1) : 0f;
                next = Time.time;
                if (pop != null && popVolume > 0f && from.TryGetComponent(out AudioSource like)) popMixer = like.outputAudioMixerGroup;
            }

            // Game time: a paused or held game holds the burst too.
            private void Update()
            {
                if (Time.time < next) return;
                if (left > 0)
                {
                    left--;
                    LetOneOut();
                    next = Time.time + gap;
                    return;
                }
                // All out: gone once the last pop has played.
                foreach (var speaker in speakers)
                {
                    if (speaker != null && speaker.isPlaying) return;
                }
                Destroy(gameObject);
            }

            // A pop from where the furniture stood, on a speaker of its own, at its own pitch.
            private void Pop()
            {
                var speaker = gameObject.AddComponent<AudioSource>();
                speaker.playOnAwake = false;
                speaker.spatialBlend = 1f;
                speaker.rolloffMode = AudioRolloffMode.Logarithmic;
                speaker.minDistance = 2f;
                speaker.maxDistance = 25f;
                speaker.dopplerLevel = 0f;
                speaker.outputAudioMixerGroup = popMixer;
                speaker.pitch = Random.Range(0.9f, 1.2f);
                speaker.PlayOneShot(pop, popVolume);
                speakers.Add(speaker);
            }

            private void LetOneOut()
            {
                var prefab = bugs[Random.Range(0, bugs.Length)];
                if (prefab == null) return;
                // Within the Enemy Limit, leaving its last places to the waves.
                if (!EnemyLimit.TryTakeSlot(Mathf.Max(1, EnemyLimit.Max - keepFree))) return;

                // A random spot in the middle of where it stood, on the NavMesh at the floor.
                var spot = new Vector3(
                    area.center.x + Random.Range(-1f, 1f) * area.extents.x * FootprintShare,
                    area.min.y + 0.2f,
                    area.center.z + Random.Range(-1f, 1f) * area.extents.z * FootprintShare);
                var filter = new NavMeshQueryFilter { areaMask = NavMesh.AllAreas };
                if (prefab.TryGetComponent(out NavMeshAgent agent)) filter.agentTypeID = agent.agentTypeID;
                if (!NavMesh.SamplePosition(spot, out var hit, NavMeshReach, filter)) return;

                // Facing out of the furniture.
                var away = hit.position - area.center;
                away.y = 0f;
                if (away.sqrMagnitude < 0.0001f) away = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                var bug = Instantiate(prefab, hit.position, Quaternion.LookRotation(away), transform.parent);
                if (comeAtOnce && bug.TryGetComponent(out SpiderBrain brain)) brain.ComeAtOnce();

                if (puff != null && puffSize > 0f) EffectPool.Play(puff, hit.position + Vector3.up * 0.05f, puffSize, 1.2f);
                if (pop != null && popVolume > 0f) Pop();
                BugCameOut?.Invoke(bug);
            }
        }
    }
}
