using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// The Pumpkin boss's smoke and dust; Pumpkin Boss Brain says when and where:
    /// - Ground: a blast where it slams the ground (a flash, a shockwave ring along the floor, dust rolling out, a column
    ///   of smoke and flying dirt), as big as the move is hard (Small, Medium, Big, Huge) and the boss is big, with a
    ///   boom for the hard ones;
    /// - Hit: a smaller blast in the air where one of its blows lands on the player;
    /// - Scrape: a little dust kicked up round it while it spins;
    /// - its vomit: black smoke pours out of its mouth, falls and spreads over the floor, and a black cloud rolls out
    ///   over the spot where its bugs come out (the brain picks it, away from the player), thick enough to hide them
    ///   until they're up and going.
    /// The blasts are pooled (Effect Pool, a few copies warmed up at load). The vomit smoke is the boss's own, because it
    /// lasts longer than a pooled effect may. Tools > 429 Game > Enemies > Build Pumpkin Boss Smoke FX makes the effects
    /// and sets this up on the boss.
    /// </summary>
    [DisallowMultipleComponent]
    public class PumpkinBossFx : MonoBehaviour
    {
        public enum Blast { Small, Medium, Big, Huge }

        [Header("Blasts (pooled)")]
        [SerializeField] private GameObject groundBlast;
        [SerializeField] private GameObject hitBlast;
        [Tooltip("A little dust round it while it spins.")]
        [SerializeField] private GameObject scrapeDust;
        [Tooltip("Ground blast size for Small, Medium, Big and Huge, at the boss size below.")]
        [SerializeField] private float[] groundSizes = { 0.45f, 0.7f, 1f, 1.35f };
        [Tooltip("Hit blast size at the boss size below.")]
        [Min(0.1f)] [SerializeField] private float hitSize = 1f;
        [Tooltip("The boss size the sizes above are for (its upper body is 1.7, its true form 2.1).")]
        [Min(0.1f)] [SerializeField] private float sizeFor = 1.7f;
        [Tooltip("A hit blast goes off this far from the player, towards the blow (not inside their head).")]
        [Min(0f)] [SerializeField] private float hitInFront = 0.45f;
        [Tooltip("The shockwave ring along the floor may be drawn this big (share of the view's height), so it can sweep " +
                 "out past a player standing close; everything else is kept to half the view.")]
        [Min(0.5f)] [SerializeField] private float ringScreenSize = 4f;

        [Header("Vomit (its own)")]
        [Tooltip("Black smoke out of its mouth: falls and spreads over the floor (its child 'Floor' is the floor).")]
        [SerializeField] private ParticleSystem vomitStream;
        [Tooltip("The black cloud over the spot where its bugs come out.")]
        [SerializeField] private ParticleSystem vomitCloud;
        [Tooltip("Smoke out of its mouth per second while its mouth is open, and per retch.")]
        [Min(0f)] [SerializeField] private float streamRate = 16f;
        [Min(0)] [SerializeField] private int streamPerRetch = 5;
        [Tooltip("The cloud: when it starts and stops rolling out (seconds into the vomit: as the first smoke lands), how " +
                 "much comes at once, and then per second. Where: the spot the brain gives (else this far in front).")]
        [SerializeField] private float cloudFrom = 0.9f;
        [SerializeField] private float cloudUntil = 1.9f;
        [Min(0)] [SerializeField] private int cloudBurst = 8;
        [Min(0f)] [SerializeField] private float cloudRate = 12f;
        [SerializeField] private float cloudAhead = 2.5f;

        [Header("Sounds")]
        [Tooltip("For Big and Huge blasts.")]
        [SerializeField] private AudioClip bigBoom;
        [Tooltip("For Medium blasts and blows landing (Small ones are quiet).")]
        [SerializeField] private AudioClip mediumBoom;
        [Tooltip("On each retch of its vomit.")]
        [SerializeField] private AudioClip vomitSound;
        [Range(0f, 1f)] [SerializeField] private float volume = 1f;

        // When its mouth is open in the vomit clip (seconds in): smoke pours out then.
        private static readonly Vector2[] MouthOpen = { new(0.5f, 1.05f), new(1.15f, 1.48f), new(1.55f, 1.8f) };
        // The vomit clip lasts 2 s; the smoke stops pouring by then whatever happens.
        private const float VomitLasts = 2.1f;
        // Two booms closer together than this sound as one (unless the second is louder).
        private const float BoomGap = 0.12f;

        private ParticleSystem stream, cloud;
        private Transform streamFloor;
        // Two speakers for booms (a new one doesn't move or re-pitch the one still ringing), one for its retches.
        private readonly AudioSource[] boomSpeakers = new AudioSource[2];
        private AudioSource retchSpeaker;
        private int nextSpeaker;
        private bool vomiting, cloudOut, hasCloudSpot;
        private Vector3 cloudSpot;
        private float vomitStarted, lastBoom = -1f, lastLoudness;

        // Awake, so it's ready for the brain's very first blast (it bursts out of the ground in its Start).
        private void Awake()
        {
            // A few copies ready: a jump or a cyclone sets off several blasts within a couple of seconds.
            EffectPool.Warm(groundBlast, 3);
            EffectPool.Warm(hitBlast, 2);
            EffectPool.Warm(scrapeDust, 3);
            // The vomit smoke stays where it was let out (world space): it sits in the scene, not under the boss.
            stream = Own(vomitStream);
            cloud = Own(vomitCloud);
            if (stream != null) streamFloor = stream.transform.Find("Floor");
            for (int i = 0; i < boomSpeakers.Length; i++) boomSpeakers[i] = MakeSpeaker("Boss Boom " + i);
            retchSpeaker = MakeSpeaker("Boss Retch");
        }

        private void OnDisable() => EndVomit();

        private void OnDestroy()
        {
            if (stream != null) Destroy(stream.gameObject);
            if (cloud != null) Destroy(cloud.gameObject);
        }

        private ParticleSystem Own(ParticleSystem prefab)
        {
            if (prefab == null) return null;
            var copy = Instantiate(prefab, transform.parent);
            copy.name = prefab.name;
            copy.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            SetRate(copy, 0f);
            return copy;
        }

        // A speaker set up like the boss's own (3D, same distances), moved to where each sound happens.
        private AudioSource MakeSpeaker(string name)
        {
            var like = GetComponent<AudioSource>();
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            if (like == null) return s;
            s.outputAudioMixerGroup = like.outputAudioMixerGroup;
            s.spatialBlend = like.spatialBlend;
            s.rolloffMode = like.rolloffMode;
            s.minDistance = like.minDistance;
            s.maxDistance = like.maxDistance;
            s.priority = like.priority;
            s.dopplerLevel = like.dopplerLevel;
            s.spread = like.spread;
            return s;
        }

        /// <summary>A blast on the ground at this point (put it on the floor first), for a boss this big.</summary>
        public void Ground(Vector3 point, Blast strength, float bossSize)
        {
            int i = Mathf.Clamp((int)strength, 0, groundSizes.Length - 1);
            float size = groundSizes.Length > 0 ? groundSizes[i] : 1f;
            var blast = EffectPool.Play(groundBlast, point, size * bossSize / sizeFor);
            // The pool keeps its copies' particles to half the view; the flat ring along the floor may be bigger.
            var ring = blast != null ? blast.transform.Find("Shockwave") : null;
            if (ring != null && ring.TryGetComponent(out ParticleSystemRenderer flat)) flat.maxParticleSize = ringScreenSize;
            if (strength >= Blast.Big) Boom(bigBoom, point, strength == Blast.Huge ? 1f : 0.85f);
            else if (strength == Blast.Medium) Boom(mediumBoom, point, 0.7f);
        }

        /// <summary>A blast in the air where a blow lands on the player: at the blow, pulled back towards where it came from.</summary>
        public void Hit(Vector3 point, Vector3 from, float bossSize)
        {
            var back = from - point;
            if (back.sqrMagnitude > 0.0001f) point += back.normalized * Mathf.Min(hitInFront, back.magnitude);
            EffectPool.Play(hitBlast, point, hitSize * bossSize / sizeFor);
            Boom(mediumBoom, point, 0.6f);
        }

        /// <summary>A little dust kicked up on the floor here, for a boss this big.</summary>
        public void Scrape(Vector3 point, float bossSize) => EffectPool.Play(scrapeDust, point, bossSize / sizeFor);

        private void Boom(AudioClip clip, Vector3 at, float loudness)
        {
            if (clip == null) return;
            // Close together, only a louder one gets through: a slam's boom right after its blow landing still plays.
            if (Time.time - lastBoom < BoomGap && loudness <= lastLoudness) return;
            lastBoom = Time.time;
            lastLoudness = loudness;
            var speaker = boomSpeakers[nextSpeaker];
            nextSpeaker = (nextSpeaker + 1) % boomSpeakers.Length;
            Play(speaker, clip, at, loudness);
        }

        private void Play(AudioSource speaker, AudioClip clip, Vector3 at, float loudness)
        {
            if (speaker == null || clip == null) return;
            speaker.transform.position = at;
            speaker.pitch = Random.Range(0.9f, 1.05f);
            speaker.PlayOneShot(clip, volume * loudness);
        }

        // ---------- vomit ----------

        /// <summary>It starts to vomit; its bugs (and the black cloud) come out over Spot. Vomit Mouth then moves the
        /// smoke along with its mouth every frame.</summary>
        public void BeginVomit(float bossSize, Vector3 spot)
        {
            vomitStarted = Time.time;
            vomiting = true;
            cloudOut = false;
            cloudSpot = spot;
            hasCloudSpot = true;
            if (stream == null) return;
            stream.transform.localScale = Vector3.one * bossSize / 2.1f;
            stream.Play(true);
        }

        /// <summary>Its mouth this frame: where the smoke comes out, which way, and where the floor is.</summary>
        public void VomitMouth(Vector3 mouth, Vector3 direction, float floorY)
        {
            if (!vomiting || stream == null) return;
            mouth.y = Mathf.Max(mouth.y, floorY + 0.15f);
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.down;
            stream.transform.SetPositionAndRotation(mouth, Quaternion.LookRotation(direction));
            if (streamFloor != null) streamFloor.SetPositionAndRotation(new Vector3(mouth.x, floorY, mouth.z), Quaternion.identity);
        }

        /// <summary>A retch: a gush of smoke (and a retching sound).</summary>
        public void Retch(Vector3 mouth)
        {
            if (stream != null && vomiting) stream.Emit(streamPerRetch);
            Play(retchSpeaker, vomitSound, mouth, 0.8f);
        }

        /// <summary>Stops the smoke pouring out (what's out drifts and fades on its own).</summary>
        public void EndVomit()
        {
            if (!vomiting) return;
            vomiting = false;
            hasCloudSpot = false;
            if (stream != null) { SetRate(stream, 0f); stream.Stop(true, ParticleSystemStopBehavior.StopEmitting); }
            if (cloud != null) { SetRate(cloud, 0f); cloud.Stop(true, ParticleSystemStopBehavior.StopEmitting); }
        }

        private void Update()
        {
            if (!vomiting) return;
            float t = Time.time - vomitStarted;
            if (t >= VomitLasts) { EndVomit(); return; }
            if (stream != null) SetRate(stream, MouthIsOpen(t) ? streamRate : 0f);
            if (cloud == null) return;
            if (!cloudOut && t >= cloudFrom)
            {
                // Rolls out over the spot its bugs come out on, turned the way the boss looks at it.
                cloudOut = true;
                var spot = hasCloudSpot ? cloudSpot : transform.position + transform.forward * cloudAhead;
                spot.y = transform.position.y;
                var along = spot - transform.position;
                along.y = 0f;
                if (along.sqrMagnitude < 0.0001f) along = transform.forward;
                cloud.transform.SetPositionAndRotation(spot, Quaternion.LookRotation(along.normalized));
                cloud.Play(true);
                cloud.Emit(cloudBurst);
            }
            SetRate(cloud, cloudOut && t <= cloudUntil ? cloudRate : 0f);
        }

        private static bool MouthIsOpen(float t)
        {
            foreach (var w in MouthOpen)
                if (t >= w.x && t <= w.y) return true;
            return false;
        }

        private static void SetRate(ParticleSystem particles, float perSecond)
        {
            var emission = particles.emission;
            emission.rateOverTime = perSecond;
        }
    }
}
