using System.Collections.Generic;
using UnityEngine;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;

namespace Game.Enemies
{
    /// <summary>
    /// A portal left behind by a broken wall (the Living Room's Brick_Wall_Breakable): a summoning circle that doesn't take
    /// the player anywhere, it lets enemies in. It waits switched off under the wall's drop point; when the wall breaks and
    /// that point is switched on, it moves onto the wall's own bone (so it moves and scales with the room and goes with
    /// it), settles on the dark ground Behind Wall behind where the wall stood (out there the floor ends: stepping on it,
    /// the player falls), and rolls what it is:
    /// - a loot portal (Legendary Chance): it glows Loot Colour and gives all of Legendary at the wall's line; once all of
    ///   it has been picked up it loses its colour (fades to Spent Colour, its light goes out, its sparks stop);
    /// - otherwise an enemy portal of a random kind (Kinds: which enemies, and its colour), which:
    /// - lets its enemies into the room at the wall's line (Enter Ahead in front of it), in waves that speed up: the
    ///   first after First Spawn seconds, then every Start Interval seconds, each a bit sooner (Speed Up) down to Min
    ///   Interval; at most Start Alive of its enemies alive at once, one more every More Every spawns up to Max Alive, and
    ///   never more than Total Cap from all portals together;
    /// Either kind:
    /// - the Fairy Shield (Clear Tag) clears it like most things: touched, it stops, collapses and is gone;
    /// - it spawns nothing while a bonus round is on, and its enemies don't count against earning one.
    /// Spawned things go under the stage's CurrentlySpawnedStuffs, so the stage clean-up removes them like other enemies.
    /// </summary>
    [DisallowMultipleComponent]
    public class WallPortal : MonoBehaviour
    {
        [System.Serializable]
        public class Kind
        {
            public string name = "Mixed";
            [Tooltip("The enemies this kind lets in, one picked at random each time.")]
            public GameObject[] enemies = System.Array.Empty<GameObject>();
            [Tooltip("Its colour: the runes, the inner ring, the haze, the sparks, the wisps and the light.")]
            public Color colour = Color.white;
        }

        [Header("Kinds (one rolled per portal)")]
        [SerializeField] private Kind[] kinds = System.Array.Empty<Kind>();
        [Tooltip("Coloured with the kind's colour (their own alpha kept).")]
        [SerializeField] private ParticleSystem[] tinted = System.Array.Empty<ParticleSystem>();
        [SerializeField] private Light portalLight;

        [Header("Enemies")]
        [Min(0f)]
        [SerializeField] private float firstSpawn = 2f;
        [Min(0.5f)]
        [SerializeField] private float startInterval = 7f;
        [Tooltip("Each spawn, the wait until the next one is multiplied by this (0.92 = 8% sooner each time).")]
        [Range(0.5f, 1f)]
        [SerializeField] private float speedUp = 0.92f;
        [Min(0.5f)]
        [SerializeField] private float minInterval = 2.5f;
        [Min(1)]
        [SerializeField] private int startAlive = 3;
        [Min(1)]
        [SerializeField] private int maxAlive = 6;
        [Tooltip("One more of its enemies may be alive at once every this many spawns.")]
        [Min(1)]
        [SerializeField] private int moreEvery = 5;
        [Tooltip("The most enemies alive from all wall portals together (Quest).")]
        [Min(1)]
        [SerializeField] private int totalCap = 12;

        [Header("Where")]
        [Tooltip("How far behind where the wall stood the circle opens, on the dark ground, in metres (room units).")]
        [Min(0f)]
        [SerializeField] private float behindWall = 1.4f;
        [Tooltip("How far in front of the wall's line (into the room) its enemies come in, in metres (room units).")]
        [Min(0f)]
        [SerializeField] private float enterAhead = 0.5f;

        [Header("Loot portal")]
        [Tooltip("The chance this gate's portal is a loot portal instead: no enemies, it gives all of Legendary.")]
        [Range(0f, 1f)]
        [SerializeField] private float legendaryChance = 0.25f;
        [SerializeField] private GameObject[] legendary = System.Array.Empty<GameObject>();
        [SerializeField] private Color lootColour = new(1f, 0.9f, 0.55f);
        [Tooltip("Once all its loot has been picked up, a loot portal fades to this (loses its colour) and its light goes out.")]
        [SerializeField] private Color spentColour = new(0.3f, 0.3f, 0.32f);
        [Min(0.1f)]
        [SerializeField] private float fadeSeconds = 1.5f;
        [Tooltip("These stop (no more sparks or wisps) once a loot portal is spent.")]
        [SerializeField] private ParticleSystem[] stopWhenSpent = System.Array.Empty<ParticleSystem>();

        [Header("Closing")]
        [Tooltip("Touched by this (the Fairy Shield), it closes: no more enemies, it collapses and is gone.")]
        [SerializeField] private string clearTag = "Fairy_Shield";
        [Min(0.05f)]
        [SerializeField] private float closeSeconds = 0.6f;

        [Header("Show")]
        [Tooltip("Played at each spawn.")]
        [SerializeField] private ParticleSystem spawnBurst;
        [SerializeField] private AudioClip spawnSound;
        [Range(0f, 1f)]
        [SerializeField] private float spawnVolume = 0.8f;

        private const string SpawnedStuffVariable = "CurrentlySpawnedStuffs";
        private static readonly List<GameObject> fromAllPortals = new();
        private readonly List<GameObject> mine = new();
        private bool open;
        private bool closing;
        private float next;
        private float interval;
        private int spawned;
        private AudioSource voice;
        private Kind kind;
        // Where its enemies (or loot) come in (the wall's line), in the wall bone's space so it follows the room.
        private Vector3 entryLocal;
        private bool isLoot;
        private bool spent;
        private readonly List<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable> loot = new();
        private readonly HashSet<Object> taken = new();

        /// <summary>The kind it rolled when it opened ("Loot" for a loot portal; null before).</summary>
        public string KindName => isLoot ? "Loot" : kind != null ? kind.name : null;

        /// <summary>A loot portal whose loot has all been picked up (it lost its colour).</summary>
        public bool Spent => spent;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => fromAllPortals.Clear();

        private void OnEnable()
        {
            if (open) return;
            open = true;
            // The room's middle (its Animator's object), to face into it.
            var room = GetComponentInParent<Animator>();
            var centre = room != null ? room.transform.position : Vector3.zero;
            // Off the wall that's about to be destroyed, onto the wall's bone (its parent).
            var wall = transform;
            while (wall != null && !wall.name.StartsWith("Brick_Wall_Breakable")) wall = wall.parent;
            if (wall != null && wall.parent != null) transform.SetParent(wall.parent, true);
            var inward = centre - transform.position;
            inward.y = 0f;
            if (inward.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(inward.normalized, Vector3.up);

            // Enemies come in at the wall's line; the circle opens behind it, on the dark ground.
            float scale = transform.lossyScale.z;
            var entry = transform.position + transform.forward * enterAhead * scale;
            entryLocal = transform.parent != null ? transform.parent.InverseTransformPoint(entry) : entry;
            var spot = transform.position - transform.forward * behindWall * scale;
            if (Physics.Raycast(spot, Vector3.down, out var floor, 20f, ~0, QueryTriggerInteraction.Ignore)) spot.y = floor.point.y;
            transform.position = spot;

            interval = startInterval;
            next = Time.time + firstSpawn;
            voice = GetComponent<AudioSource>();
            // One in four (Legendary Chance): a loot portal; otherwise an enemy portal of a random kind.
            isLoot = legendary.Length > 0 && Random.value < legendaryChance;
            if (isLoot)
            {
                Tint(lootColour);
                GiveLegendary();
            }
            else if (kinds.Length > 0)
            {
                kind = kinds[Random.Range(0, kinds.Length)];
                Tint(kind.colour);
            }
        }

        private void Tint(Color colour)
        {
            foreach (var ps in tinted)
            {
                if (ps == null) continue;
                var main = ps.main;
                float alpha = main.startColor.color.a;
                main.startColor = new Color(colour.r, colour.g, colour.b, alpha);
                // The ones already out (the rings and haze are single long-lived particles) take it too.
                var particles = new ParticleSystem.Particle[ps.main.maxParticles];
                int count = ps.GetParticles(particles);
                for (int i = 0; i < count; i++)
                {
                    var c = particles[i].startColor;
                    particles[i].startColor = new Color32((byte)(colour.r * 255), (byte)(colour.g * 255), (byte)(colour.b * 255), c.a);
                }
                ps.SetParticles(particles, count);
            }
            if (portalLight != null) portalLight.color = colour;
        }

        // A loot portal whose loot has all been picked up (or is gone) loses its colour, its light and its sparks.
        private void WatchLoot()
        {
            if (spent || loot.Count == 0) return;
            foreach (var item in loot)
            {
                if (item == null || item.isSelected) taken.Add(item);
            }
            if (taken.Count >= loot.Count) StartCoroutine(LoseColour());
        }

        private System.Collections.IEnumerator LoseColour()
        {
            spent = true;
            foreach (var ps in stopWhenSpent) if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            var flicker = portalLight != null ? portalLight.GetComponent<Game.VFX.FlameFlicker>() : null;
            if (flicker != null) flicker.enabled = false;
            float startLight = portalLight != null ? portalLight.intensity : 0f;
            for (float t = 0f; t <= fadeSeconds; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / fadeSeconds);
                Tint(Color.Lerp(lootColour, spentColour, k));
                if (portalLight != null) portalLight.intensity = startLight * (1f - k);
                yield return null;
            }
            Tint(spentColour);
            if (portalLight != null) portalLight.enabled = false;
        }

        // The Fairy Shield clears it like most things (its trigger round the circle; a kinematic body so triggers fire).
        private void OnTriggerEnter(Collider other)
        {
            if (open && !closing && !string.IsNullOrEmpty(clearTag) && other.CompareTag(clearTag)) StartCoroutine(Close());
        }

        private System.Collections.IEnumerator Close()
        {
            closing = true;
            if (spawnBurst != null) spawnBurst.Play(true);
            if (spawnSound != null && voice != null) voice.PlayOneShot(spawnSound, spawnVolume);
            var from = transform.localScale;
            for (float t = 0f; t < closeSeconds; t += Time.deltaTime)
            {
                float k = 1f - t / closeSeconds;
                transform.localScale = from * (k * k);
                yield return null;
            }
            Destroy(gameObject);
        }

        /// <summary>Whether this enemy came out of a wall portal (the bonus round's "kill every enemy" rule ignores them).</summary>
        public static bool IsPortalEnemy(GameObject enemy) => enemy != null && fromAllPortals.Contains(enemy);

        private void Update()
        {
            if (!open || closing) return;
            if (isLoot) { WatchLoot(); return; }
            mine.RemoveAll(x => x == null);
            fromAllPortals.RemoveAll(x => x == null);
            // Paused while a bonus round is on: the wait to the next one starts again after it.
            if (Game.Bonus.BonusRound.Running != null) { next = Mathf.Max(next, Time.time + 1f); return; }
            if (Time.time < next || kind == null || kind.enemies.Length == 0) return;
            int alive = Mathf.Min(maxAlive, startAlive + spawned / moreEvery);
            if (mine.Count >= alive || fromAllPortals.Count >= totalCap) { next = Time.time + 0.5f; return; }
            Spawn(kind.enemies[Random.Range(0, kind.enemies.Length)]);
            spawned++;
            interval = Mathf.Max(minInterval, interval * speedUp);
            next = Time.time + interval;
        }

        private void Spawn(GameObject prefab)
        {
            if (prefab == null) return;
            var enemy = Instantiate(prefab, Entry(0f), Quaternion.LookRotation(transform.forward, Vector3.up), SpawnedStuff());
            mine.Add(enemy);
            fromAllPortals.Add(enemy);
            if (spawnBurst != null) spawnBurst.Play(true);
            if (spawnSound != null)
            {
                if (voice != null) voice.PlayOneShot(spawnSound, spawnVolume);
                else AudioSource.PlayClipAtPoint(spawnSound, transform.position, spawnVolume);
            }
        }

        private void GiveLegendary()
        {
            for (int i = 0; i < legendary.Length; i++)
            {
                if (legendary[i] == null) continue;
                float side = (i - (legendary.Length - 1) * 0.5f) * 0.6f;
                var item = Instantiate(legendary[i], Entry(0.8f) + transform.right * side, Quaternion.LookRotation(transform.forward, Vector3.up), SpawnedStuff());
                var grab = item.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                if (grab != null) loot.Add(grab);
            }
            if (spawnBurst != null) spawnBurst.Play(true);
            if (spawnSound != null && voice != null) voice.PlayOneShot(spawnSound, spawnVolume);
        }

        // On the floor at the wall's line, in the room (plus Lift metres).
        private Vector3 Entry(float lift)
        {
            var spot = transform.parent != null ? transform.parent.TransformPoint(entryLocal) : entryLocal;
            if (Physics.Raycast(spot, Vector3.down, out var hit, 10f, ~0, QueryTriggerInteraction.Ignore)) spot.y = hit.point.y;
            return spot + Vector3.up * lift;
        }

        private static Transform SpawnedStuff()
        {
            var variable = FsmVariables.GlobalVariables.FindFsmGameObject(SpawnedStuffVariable);
            return variable != null && variable.Value != null ? variable.Value.transform : null;
        }
    }
}
