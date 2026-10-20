using Game.Combat;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Leveling
{
    /// <summary>
    /// Little gold EXP orbs. When a bug dies, one orb per EXP point (a small bug 1, a normal one 2, a mini boss 5, at
    /// most Max Orbs) pops up out of its body, waits a moment, then flies into the player's chest, where each one
    /// plucks a little higher than the one before. No numbers, no words: the player sees that kills feed them, so a
    /// level up feels earned. Only enemies with EXP Orbs ticked on their Enemy Hit Damage (the bugs), never furniture.
    /// It runs on game time, so the orbs wait in the air while the game is paused. The orbs are made when the game
    /// starts (particle systems under this object). Tools > 429 Game > Player > Add EXP Orbs puts it on 'Player Level'.
    /// </summary>
    [DisallowMultipleComponent]
    public class ExpOrbs : MonoBehaviour
    {
        [Header("Look")]
        [Tooltip("The orbs' material: a soft round dot that glows (additive), e.g. Level Up Aura. Empty: the orbs " +
                 "aren't shown (their sound still plays).")]
        [SerializeField] private Material orbMaterial;
        [Tooltip("The orbs' colour.")]
        [SerializeField] private Color orbColor = new(1f, 0.847f, 0.29f, 1f);
        [Tooltip("How big an orb is, in metres.")]
        [Min(0.005f)]
        [SerializeField] private float orbSize = 0.04f;
        [Tooltip("How long the glittering tail behind a flying orb lasts, in seconds. 0 = no tail.")]
        [Min(0f)]
        [SerializeField] private float tailSeconds = 0.18f;
        [Tooltip("At most this many orbs from one kill (one per EXP point). 0 = no orbs at all.")]
        [Min(0)]
        [SerializeField] private int maxOrbs = 8;

        [Header("Flight")]
        [Tooltip("How high the orbs pop up out of the body first, in metres.")]
        [Min(0f)]
        [SerializeField] private float riseHeight = 0.25f;
        [Tooltip("How long they take to pop up, in seconds.")]
        [Min(0.01f)]
        [SerializeField] private float riseSeconds = 0.3f;
        [Tooltip("Each orb of a kill sets off this many seconds after the one before, so they arrive one by one.")]
        [Min(0f)]
        [SerializeField] private float staggerSeconds = 0.05f;
        [Tooltip("How long an orb flies to the player, in seconds (plus Seconds Per Metre for each metre away).")]
        [Min(0.05f)]
        [SerializeField] private float flySeconds = 0.45f;
        [Tooltip("Extra flying time for each metre between the kill and the player, so far kills don't zip in.")]
        [Min(0f)]
        [SerializeField] private float secondsPerMetre = 0.03f;
        [Tooltip("Where they fly to: this far below the eyes, in metres (the chest), so they never fly into the eyes.")]
        [Min(0f)]
        [SerializeField] private float chestBelowEyes = 0.3f;

        [Header("Sound")]
        [Tooltip("Played as each orb reaches the player, e.g. pluck_001. Empty: silent.")]
        [SerializeField] private AudioClip arriveSound;
        [Range(0f, 1f)]
        [SerializeField] private float arriveVolume = 0.25f;
        [Tooltip("Each orb of a kill plucks this much higher than the one before (pitch 1, 1.08, 1.16, ...).")]
        [Min(0f)]
        [SerializeField] private float pitchStep = 0.08f;

        // How many orbs can be in the air at once: more are skipped (ten bugs dying together still show plenty).
        private const int PoolSize = 24;
        private const int MaxTailBits = 512;
        // Arrival sounds that can ring at once, each at its own pitch.
        private const int Voices = 4;
        // The chest point is this far in front of the eyes (metres), so the orbs stay in sight until they arrive.
        private const float ChestForward = 0.12f;

        private struct Orb
        {
            public bool on;
            public Vector3 from;
            public Vector3 top;
            public Vector3 position;
            public Vector3 lastTail;
            public float age;
            public float flyAt;
            public float flyTime;
            public float size;
            public float twinkle;
            public int step;
        }

        private readonly Orb[] orbs = new Orb[PoolSize];
        private ParticleSystem.Particle[] shown;
        private ParticleSystem dots;
        private ParticleSystem tail;
        private AudioSource[] voices;
        private int nextVoice;
        private Transform head;

        /// <summary>How many orbs are in the air now (for checking it works).</summary>
        public int Flying
        {
            get
            {
                int count = 0;
                foreach (var orb in orbs)
                {
                    if (orb.on) count++;
                }
                return count;
            }
        }

        /// <summary>How many orbs have reached the player since the game started (for checking it works).</summary>
        public int Arrived { get; private set; }

        private void Awake()
        {
            shown = new ParticleSystem.Particle[PoolSize];
            dots = NewParticles("EXP Orbs", PoolSize);
            tail = NewParticles("EXP Orb Tails", MaxTailBits);
            // A tail bit shrinks and fades away.
            var shrink = tail.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) });
            var color = tail.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(fade);

            voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 0f;
                voices[i] = voice;
            }
        }

        private void OnEnable()
        {
            EnemyHitDamage.Died += OnDied;
        }

        private void OnDisable()
        {
            EnemyHitDamage.Died -= OnDied;
            for (int i = 0; i < orbs.Length; i++) orbs[i].on = false;
            if (dots != null) dots.Clear();
            if (tail != null) tail.Clear();
        }

        private void OnDied(EnemyHitDamage enemy)
        {
            if (enemy == null || !enemy.ShowsExpOrbs || maxOrbs <= 0 || !FindHead()) return;

            int count = Mathf.Clamp(enemy.Exp, 1, maxOrbs);
            var from = enemy.BodyCenter;
            float flyTime = flySeconds + secondsPerMetre * Vector3.Distance(from, Chest());
            int slot = 0;
            for (int i = 0; i < count; i++)
            {
                while (slot < PoolSize && orbs[slot].on) slot++;
                // All in the air: the rest of this kill's orbs are skipped.
                if (slot >= PoolSize) return;

                var spread = Random.insideUnitCircle * 0.12f;
                var start = from + Random.insideUnitSphere * 0.04f;
                orbs[slot] = new Orb
                {
                    on = true,
                    from = start,
                    top = start + new Vector3(spread.x, riseHeight * Random.Range(0.8f, 1.2f), spread.y),
                    position = start,
                    lastTail = start,
                    flyAt = riseSeconds + i * staggerSeconds,
                    flyTime = flyTime,
                    size = orbSize * 0.5f,
                    twinkle = Random.value * 10f,
                    step = i,
                };
            }
        }

        // After the camera has moved, so the orbs fly to where the player is this frame.
        private void LateUpdate()
        {
            float step = Time.deltaTime;
            int count = 0;
            bool moving = step > 0f && FindHead();
            var chest = moving ? Chest() : Vector3.zero;

            for (int i = 0; i < orbs.Length; i++)
            {
                ref var orb = ref orbs[i];
                if (!orb.on) continue;

                if (moving && !Fly(ref orb, step, chest)) continue;
                if (count < shown.Length)
                {
                    shown[count] = new ParticleSystem.Particle
                    {
                        position = orb.position,
                        startSize = orb.size * (1f + 0.15f * Mathf.Sin(orb.age * 25f + orb.twinkle)),
                        startColor = orbColor,
                        startLifetime = 1f,
                        remainingLifetime = 1f,
                    };
                    count++;
                }
            }
            if (dots != null) dots.SetParticles(shown, count);
        }

        // Moves one orb on: up out of the body, a short wait, then a curve into the chest. False once it has arrived.
        private bool Fly(ref Orb orb, float step, Vector3 chest)
        {
            orb.age += step;
            if (orb.age < riseSeconds)
            {
                float t = orb.age / riseSeconds;
                float eased = 1f - (1f - t) * (1f - t);
                orb.position = Vector3.LerpUnclamped(orb.from, orb.top, eased);
                orb.size = orbSize * Mathf.Lerp(0.5f, 1f, eased);
            }
            else if (orb.age < orb.flyAt)
            {
                orb.position = orb.top;
                orb.size = orbSize;
            }
            else
            {
                float t = (orb.age - orb.flyAt) / orb.flyTime;
                if (t >= 1f)
                {
                    orb.on = false;
                    Arrive(orb.step);
                    return false;
                }
                float s = Mathf.SmoothStep(0f, 1f, t);
                // A gentle arc over the way, higher for a far kill.
                float distance = Vector3.Distance(orb.top, chest);
                var bend = Vector3.Lerp(orb.top, chest, 0.35f) + Vector3.up * Mathf.Min(1f, 0.15f + 0.04f * distance);
                var a = Vector3.Lerp(orb.top, bend, s);
                var b = Vector3.Lerp(bend, chest, s);
                orb.position = Vector3.Lerp(a, b, s);
                // It shrinks a little as it sinks in.
                orb.size = orbSize * (t > 0.8f ? Mathf.Lerp(1f, 0.6f, (t - 0.8f) / 0.2f) : 1f);
            }

            // Its tail: a bit left behind every half orb of the way it moves.
            if (tail != null && tailSeconds > 0f && (orb.position - orb.lastTail).sqrMagnitude >= orbSize * orbSize * 0.25f)
            {
                orb.lastTail = orb.position;
                var bit = new ParticleSystem.EmitParams
                {
                    position = orb.position,
                    velocity = Vector3.zero,
                    startSize = orb.size * 0.7f,
                    startLifetime = tailSeconds,
                    startColor = orbColor,
                    applyShapeToPosition = false,
                };
                tail.Emit(bit, 1);
            }
            return true;
        }

        private void Arrive(int step)
        {
            Arrived++;
            if (arriveSound == null || voices == null) return;
            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            if (voice == null) return;
            voice.pitch = 1f + pitchStep * step;
            voice.PlayOneShot(arriveSound, arriveVolume);
        }

        private Vector3 Chest()
        {
            var forward = head.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.zero;
            return head.position + Vector3.down * chestBelowEyes + forward * ChestForward;
        }

        private bool FindHead()
        {
            if (head != null) return true;
            var origin = FindAnyObjectByType<XROrigin>();
            if (origin != null && origin.Camera != null) head = origin.Camera.transform;
            else if (Camera.main != null) head = Camera.main.transform;
            return head != null;
        }

        // A particle system that only shows what it's given (no emitting of its own), in world space, not scaled by
        // this object.
        private ParticleSystem NewParticles(string objectName, int max)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.maxParticles = max;
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;

            var look = go.GetComponent<ParticleSystemRenderer>();
            look.renderMode = ParticleSystemRenderMode.Billboard;
            look.sharedMaterial = orbMaterial;
            look.shadowCastingMode = ShadowCastingMode.Off;
            look.receiveShadows = false;
            look.enabled = orbMaterial != null;
            particles.Play();
            return particles;
        }
    }
}
