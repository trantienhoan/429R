using System.Collections.Generic;
using UnityEngine;

namespace Game.Story
{
    /// <summary>
    /// Where the kidnapped Fairy is (user 2026-11-26, "Fairy caged at the boss"; the lore: the evil pumpkin keeps her to
    /// take her Green Power and grow on it). During the final boss fight she is shut in a glass jar (the lair jar, see
    /// through, the user's cork kept) floating just past the north edge of the arena, inside the purple lock she was in at
    /// the start (Fairy Lock Tower). She calls for help now and then (the user's FairyHelp / FairyCallHelp recordings),
    /// and her green light flows out of the jar into the boss as a stream of sparks along a glowing arc: that is why it is
    /// so big. When the boss is beaten (GAMESTAGES "wait": the Story Fairy Jar Break action) the lock breaks, the jar
    /// shatters and she flashes away (her teleport flash); she is the Fairy who then waits in the middle of the arena.
    /// Built by Tools > 429 Game > Story > Set Up Fairy Jar At The Boss, which puts it on the Boss_Area prefab.
    /// </summary>
    public class StoryFairyJar : MonoBehaviour
    {
        [Header("Parts")]
        [Tooltip("What the purple lock (Fairy Lock Tower) watches: switching it off breaks the lock.")]
        public GameObject lockObject;
        [Tooltip("The jar (hidden when it shatters).")]
        public Renderer jar;
        [Tooltip("Her look-only copy inside the jar (an Animator playing Call State).")]
        public GameObject fairy;
        [Tooltip("The green light in the jar.")]
        public Light jarLight;
        [Tooltip("The sparks of her light flowing to the boss (world space, its own emission off).")]
        public ParticleSystem drain;
        [Tooltip("The glowing arc from the jar to the boss.")]
        public LineRenderer beam;

        [Header("Her light flowing into the boss")]
        [Tooltip("Where the stream leaves the jar (from this object).")]
        public Vector3 mouth = new Vector3(0f, 1.3f, 0f);
        [Tooltip("How high above the boss's feet the stream goes in (metres).")]
        public float targetHeight = 1.8f;
        [Tooltip("How high the arc bends up over the middle (metres).")]
        public float arcHeight = 2.2f;
        [Tooltip("Sparks a second.")]
        public float sparksPerSecond = 30f;
        [Tooltip("How far a spark wanders off the arc (metres).")]
        public float wander = 0.25f;
        [Tooltip("Points along the glowing arc.")]
        [Min(2)] public int beamPoints = 24;
        [Tooltip("The arc's brightness goes up and down this many times a second.")]
        public float beamPulse = 1.3f;

        [Header("Her calls")]
        public AudioClip[] calls = new AudioClip[0];
        [Tooltip("Seconds between her calls (from, to).")]
        public Vector2 callEvery = new Vector2(6f, 10f);
        [Range(0f, 1f)] public float callVolume = 0.8f;
        [Tooltip("Her Animator state in the jar.")]
        public string callState = "call";

        [Header("When the boss is beaten")]
        [Tooltip("The jar's broken pieces (the lair jar's bottle pieces), made as big as the jar.")]
        public GameObject shardsPrefab;
        public float shardsScale = 3.6f;
        public AudioClip breakSound;
        [Range(0f, 1f)] public float breakVolume = 1f;
        [Tooltip("Seconds after breaking before it removes itself (the sparks fade out).")]
        public float goneAfter = 4f;

        private static readonly List<StoryFairyJar> all = new List<StoryFairyJar>();

        private Transform boss;
        private float nextLook;
        private float owed;
        private float nextCall;
        private bool broken;
        private AudioSource voice;
        private Gradient beamColours;
        private ParticleSystem.Particle[] buffer;

        /// <summary>The jars in the game now.</summary>
        public static IReadOnlyList<StoryFairyJar> All => all;

        /// <summary>Breaks every jar (the boss is beaten). How many there were.</summary>
        public static int BreakAll()
        {
            int count = 0;
            foreach (var jar in all.ToArray())
                if (jar != null && !jar.broken) { jar.Break(); count++; }
            return count;
        }

        public bool Broken => broken;

        private void OnEnable() => all.Add(this);

        private void OnDisable() => all.Remove(this);

        private void Start()
        {
            voice = GetComponent<AudioSource>();
            nextCall = Time.time + Random.Range(1.5f, 3f);
            if (fairy != null)
            {
                var animator = fairy.GetComponentInChildren<Animator>();
                if (animator != null && !string.IsNullOrEmpty(callState)) animator.Play(callState, 0, Random.value);
                // Her stored bounds are off (see Story Fairy Board): keep them true so she is never culled in the jar.
                foreach (var skin in fairy.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.updateWhenOffscreen = true;
            }
            if (beam != null) beamColours = beam.colorGradient;
        }

        private void Update()
        {
            if (!broken)
            {
                FindBoss();
                Call();
            }
            bool flowing = !broken && boss != null && boss.gameObject.activeInHierarchy;
            Vector3 from = transform.TransformPoint(mouth);
            Vector3 to = flowing ? boss.position + Vector3.up * targetHeight : from;
            Vector3 bend = (from + to) * 0.5f + Vector3.up * arcHeight;
            if (flowing && drain != null)
            {
                owed += sparksPerSecond * Time.deltaTime;
                int count = Mathf.Min(16, Mathf.FloorToInt(owed));
                owed -= count;
                if (count > 0) drain.Emit(new ParticleSystem.EmitParams { position = from, applyShapeToPosition = false }, count);
            }
            MoveSparks(from, bend, to, flowing);
            DrawBeam(from, bend, to, flowing);
        }

        private void FindBoss()
        {
            if (boss != null && boss.gameObject.activeInHierarchy) return;
            if (Time.time < nextLook) return;
            nextLook = Time.time + 0.5f;
            var brain = FindAnyObjectByType<Game.Enemies.PumpkinBossBrain>();
            boss = brain != null ? brain.transform : null;
        }

        private void Call()
        {
            if (voice == null || calls == null || calls.Length == 0 || Time.time < nextCall) return;
            nextCall = Time.time + Random.Range(callEvery.x, Mathf.Max(callEvery.x, callEvery.y));
            var clip = calls[Random.Range(0, calls.Length)];
            if (clip != null) voice.PlayOneShot(clip, callVolume);
        }

        // Each spark rides the arc by its age (with its own wobble off it); once she is free they just fade where they are.
        private void MoveSparks(Vector3 from, Vector3 bend, Vector3 to, bool flowing)
        {
            if (drain == null || !flowing) return;
            int most = drain.main.maxParticles;
            if (buffer == null || buffer.Length < most) buffer = new ParticleSystem.Particle[most];
            int count = drain.GetParticles(buffer);
            for (int i = 0; i < count; i++)
            {
                var p = buffer[i];
                float t = 1f - p.remainingLifetime / Mathf.Max(0.01f, p.startLifetime);
                var at = Bezier(from, bend, to, t);
                uint seed = p.randomSeed;
                float a = (seed % 360u) * Mathf.Deg2Rad;
                float r = wander * ((seed / 360u) % 100u) / 100f;
                var side = new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.6f, Mathf.Sin(a)) * r * Mathf.Sin(t * Mathf.PI);
                p.position = at + side;
                p.velocity = Vector3.zero;
                buffer[i] = p;
            }
            drain.SetParticles(buffer, count);
        }

        private void DrawBeam(Vector3 from, Vector3 bend, Vector3 to, bool flowing)
        {
            if (beam == null) return;
            beam.enabled = flowing;
            if (!flowing) return;
            if (beam.positionCount != beamPoints) beam.positionCount = beamPoints;
            for (int i = 0; i < beamPoints; i++)
                beam.SetPosition(i, Bezier(from, bend, to, i / (beamPoints - 1f)));
            if (beamColours != null)
            {
                float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * beamPulse * 2f * Mathf.PI);
                var keys = beamColours.alphaKeys;
                for (int i = 0; i < keys.Length; i++) keys[i].alpha = beamColours.alphaKeys[i].alpha * pulse;
                var g = new Gradient();
                g.SetKeys(beamColours.colorKeys, keys);
                beam.colorGradient = g;
            }
        }

        private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        /// <summary>The boss is beaten: the lock breaks, the jar shatters, she flashes away, her light stops flowing.</summary>
        public void Break()
        {
            if (broken) return;
            broken = true;
            if (lockObject != null) lockObject.SetActive(false);
            Vector3 middle = jar != null ? jar.bounds.center : transform.TransformPoint(mouth);
            if (jar != null)
            {
                jar.enabled = false;
                if (shardsPrefab != null)
                {
                    var shards = Instantiate(shardsPrefab, jar.transform.position, jar.transform.rotation);
                    shards.transform.localScale *= shardsScale;
                }
            }
            if (breakSound != null) AudioSource.PlayClipAtPoint(breakSound, middle, breakVolume);
            if (fairy != null)
            {
                Flash(BodyMiddle(fairy, middle));
                fairy.SetActive(false);
            }
            if (jarLight != null) jarLight.gameObject.AddComponent<StoryFlashFade>().Begin(0.6f);
            if (drain != null) drain.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            if (beam != null) beam.enabled = false;
            if (voice != null) voice.Stop();
            Destroy(gameObject, goneAfter);
        }

        private static Vector3 BodyMiddle(GameObject body, Vector3 fallback)
        {
            bool any = false;
            var bounds = new Bounds(fallback, Vector3.zero);
            foreach (var look in body.GetComponentsInChildren<Renderer>())
            {
                if (!any) { bounds = look.bounds; any = true; }
                else bounds.Encapsulate(look.bounds);
            }
            return any ? bounds.center : fallback;
        }

        // Her teleport flash (the same as Story Fairy Acts makes: Story Fairy Settings' flash, light and sound).
        private static void Flash(Vector3 at)
        {
            var settings = StoryFairySettings.Instance;
            if (settings == null) return;
            if (settings.FlashPrefab != null)
            {
                var flash = Instantiate(settings.FlashPrefab, at, Quaternion.identity);
                flash.transform.localScale *= settings.FlashScale;
                Destroy(flash, 5f);
            }
            var lightGo = new GameObject("Fairy_Teleport_Flash");
            lightGo.transform.position = at;
            var flashLight = lightGo.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.color = settings.FlashColour;
            flashLight.range = settings.FlashRange;
            flashLight.intensity = settings.FlashIntensity;
            flashLight.shadows = LightShadows.None;
            lightGo.AddComponent<StoryFlashFade>().Begin(settings.FlashSeconds);
            var sound = settings.RandomTeleportSound();
            if (sound != null) AudioSource.PlayClipAtPoint(sound, at, settings.TeleportVolume);
        }
    }
}
