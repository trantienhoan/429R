using System.Collections;
using Game.Combat;
using UnityEngine;

namespace Game.Bonus
{
    /// <summary>
    /// The Paint Bees round's giant slime (user, 2026-11-05: "a giant slime that resemble Fat Pumpkin"; the user's
    /// Giant_Slime model, no rig: a head with the eyes and stem, Giant_Slime_Below, hinged at the back on its jaw,
    /// Giant_Slime_Upper). The round drives it: it drops onto the player's head (Drop On), sits there a moment, hops off
    /// and runs away (Run To), turns to face them, then opens its mouth for each lot of flying slimes (Open For, Spit);
    /// at the end it jumps off out of sight (Leave). By itself it breathes, wobbles like jelly after every landing,
    /// throbs to the song's beat and keeps its face on the player.
    /// Standing at its spot it can be hit (user, 2026-11-06): health like the special stage's pumpkins (Enemy Hit Damage
    /// on its health FSM), super tanky; every hit squashes its jelly and leans it away from the player; punched to death
    /// it bursts in a strong explosion (smoke, a big bang, its paint everywhere, the player near it hurt, blown back and
    /// painted: user, 2026-11-06), no EXP, no reward. The round then sends the flying slimes out of the fires.
    /// Tools > 429 Game > Bonus > Set Up Giant Slime builds the prefab (Prefabs/Bonus/Paint_Giant_Slime).
    /// </summary>
    [DisallowMultipleComponent]
    public class GiantSlime : MonoBehaviour
    {
        [Header("Model")]
        [Tooltip("What squashes and stretches (its pivot at the bottom middle).")]
        [SerializeField] private Transform body;
        [Tooltip("The head (Giant_Slime_Below): it turns up round its own X axis, hinged at the back, to open the mouth.")]
        [SerializeField] private Transform head;
        [Tooltip("Where the flying slimes come out (inside the mouth).")]
        [SerializeField] private Transform mouth;
        [Tooltip("How far the head tips back with the mouth open, in degrees.")]
        [Range(5f, 80f)]
        [SerializeField] private float mouthOpenAngle = 40f;
        [Tooltip("Seconds the mouth takes to open, and to close.")]
        [Min(0.02f)]
        [SerializeField] private float openSeconds = 0.15f;
        [Min(0.02f)]
        [SerializeField] private float closeSeconds = 0.3f;

        [Header("Dropping on the player")]
        [Tooltip("It falls from this high over the head, in metres...")]
        [Min(0.5f)]
        [SerializeField] private float dropHeight = 4.5f;
        [Tooltip("...taking this long, in seconds...")]
        [Min(0.1f)]
        [SerializeField] private float fallSeconds = 0.9f;
        [Tooltip("...and sits on the head this long before it hops off.")]
        [Min(0f)]
        [SerializeField] private float sitSeconds = 1.2f;
        [Tooltip("Its bottom sits this far over the eyes, in metres.")]
        [SerializeField] private float overEyes = 0.15f;

        [Header("Hopping")]
        [Tooltip("One hop's length and height, in metres, and how long it takes, in seconds.")]
        [Min(0.2f)]
        [SerializeField] private float hopLength = 1.5f;
        [Min(0f)]
        [SerializeField] private float hopHeight = 0.6f;
        [Min(0.1f)]
        [SerializeField] private float hopSeconds = 0.42f;
        [Tooltip("How fast it turns, in degrees a second.")]
        [Min(10f)]
        [SerializeField] private float turnSpeed = 300f;

        [Header("Wobble")]
        [Tooltip("How much it swells with each beat of the song.")]
        [Range(0f, 0.3f)]
        [SerializeField] private float beatThrob = 0.06f;
        [Tooltip("How much it breathes when still.")]
        [Range(0f, 0.2f)]
        [SerializeField] private float breathe = 0.03f;
        [Tooltip("How stiff and how damped its jelly wobble is.")]
        [Min(1f)]
        [SerializeField] private float stiffness = 160f;
        [Min(0f)]
        [SerializeField] private float damping = 7f;

        [Header("Getting hit (user, 2026-11-06)")]
        [Tooltip("The PlayMaker FSM holding its \"health\", which Enemy Hit Damage takes the hits off (like the special " +
                 "stage's pumpkins; Set Up Giant Slime makes it, super tanky, no EXP). It can be hit only standing at its " +
                 "spot: not on the player's head, not while it hops.")]
        [SerializeField] private string healthFsm = "GiantHealth";
        [Tooltip("How much a hit squashes its jelly, per point of the hit's damage...")]
        [Min(0f)]
        [SerializeField] private float hitSquash = 0.12f;
        [Tooltip("...and how far it leans away from the player, in degrees per point of damage...")]
        [Min(0f)]
        [SerializeField] private float hitLean = 5f;
        [Tooltip("...counting at most this much damage (a weak hit counts 0.4).")]
        [Min(0.5f)]
        [SerializeField] private float hitWobbleCap = 4f;
        [Tooltip("A hit's squelch (one at random).")]
        [SerializeField] private AudioClip[] hitSounds = System.Array.Empty<AudioClip>();

        [Header("Bursting (punched to death)")]
        [Tooltip("Played where it bursts (Effect Pool), this big times its size.")]
        [SerializeField] private GameObject[] burstEffects = System.Array.Empty<GameObject>();
        [Min(0f)]
        [SerializeField] private float burstEffectSize = 1f;
        [Tooltip("All played together as it bursts (a bang and a wet pop).")]
        [SerializeField] private AudioClip[] burstSounds = System.Array.Empty<AudioClip>();
        [Tooltip("The player this close (metres times its size, flat) is hurt (Burst Event), blown back (x = how high, " +
                 "y = how far, in metres) and gets its paint on the view.")]
        [Min(0f)]
        [SerializeField] private float burstReach = 1.6f;
        [SerializeField] private Vector2 burstPush = new(0.5f, 2.2f);
        [Tooltip("Sent to the player's Damage FSM when the burst catches them (user, 2026-11-06: it hurts too): Damage " +
                 "(6-9 health), DamageHeavy (40-50), before their Toughness. Empty: it doesn't hurt.")]
        [SerializeField] private string burstEvent = "DamageHeavy";

        [Header("Sounds")]
        [Tooltip("While it falls (a falling whistle).")]
        [SerializeField] private AudioClip fallSound;
        [Tooltip("Landing on the player's head.")]
        [SerializeField] private AudioClip landSound;
        [Tooltip("Each hop's landing (one at random).")]
        [SerializeField] private AudioClip[] hopSounds = System.Array.Empty<AudioClip>();
        [Tooltip("A flying slime coming out of its mouth.")]
        [SerializeField] private AudioClip spitSound;
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.9f;

        /// <summary>The round, for its beat (Beat Pulse).</summary>
        public PaintBeesRound Round { get; set; }

        /// <summary>What it keeps its face on (the player's head). Null: it faces where it goes.</summary>
        public Transform FaceTarget { get; set; }

        /// <summary>Degrees a second it turns (to face its Face Target); e.g. Special Stage 3 whips it round to look.</summary>
        public float TurnSpeed
        {
            get => turnSpeed;
            set => turnSpeed = Mathf.Max(1f, value);
        }

        /// <summary>Where the flying slimes come out.</summary>
        public Vector3 Mouth => mouth != null ? mouth.position : transform.position + Vector3.up * Size * 0.4f;

        /// <summary>How big it is (its scale).</summary>
        public float Size => Mathf.Max(0.01f, transform.lossyScale.y);

        private AudioSource speaker;
        private Vector3 bodyScale = Vector3.one;
        private Quaternion headRest;
        private float mouthNow;
        private float openUntil;
        // The jelly wobble: + squashed, - stretched.
        private float squash;
        private float squashSpeed;
        // Its own stretch while it flies (on top of the wobble).
        private float stretch;
        // While it moves, it turns towards where it goes, not the player.
        private Vector3? heading;
        // Getting hit: what takes the hits, its hit box, its health, and whether it burst.
        private EnemyHitDamage hits;
        private Collider hitBox;
        private HutongGames.PlayMaker.FsmFloat health;
        private bool burst;
        // The lean away from a hit: degrees, how fast, round which axis (the root's space).
        private float lean;
        private float leanSpeed;
        private Vector3 leanAxis = Vector3.right;
        private Quaternion bodyRest = Quaternion.identity;

        private void Awake()
        {
            speaker = gameObject.AddComponent<AudioSource>();
            speaker.playOnAwake = false;
            speaker.spatialBlend = 1f;
            speaker.rolloffMode = AudioRolloffMode.Linear;
            speaker.minDistance = 2f;
            speaker.maxDistance = 30f;
            if (body != null)
            {
                bodyScale = body.localScale;
                bodyRest = body.localRotation;
            }
            if (head != null) headRest = head.localRotation;
            // No reward for it, ever (user, 2026-11-06): no EXP, no orbs.
            hits = GetComponent<EnemyHitDamage>();
            if (hits != null) hits.noReward = true;
            hitBox = GetComponent<Collider>();
            foreach (var fsm in GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == healthFsm) health = fsm.FsmVariables.FindFsmFloat("health");
            }
            Hittable(false);
        }

        private void OnEnable()
        {
            EnemyHitDamage.AnyHit += OnAnyHit;
            EnemyHitDamage.WeakHit += OnWeakHit;
        }

        private void OnDisable()
        {
            EnemyHitDamage.AnyHit -= OnAnyHit;
            EnemyHitDamage.WeakHit -= OnWeakHit;
        }

        private void OnAnyHit(EnemyHitDamage enemy, HitResult hit)
        {
            if (enemy == null || enemy != hits || burst) return;
            Jiggle(Mathf.Clamp(hit.Damage, 0.5f, hitWobbleCap));
        }

        private void OnWeakHit(EnemyHitDamage enemy)
        {
            if (enemy != null && enemy == hits && !burst) Jiggle(0.4f);
        }

        // A hit: the jelly squashes and it leans away from the player, with a squelch.
        private void Jiggle(float strength)
        {
            Kick(hitSquash * strength);
            var from = FaceTarget != null ? FaceTarget.position : Camera.main != null ? Camera.main.transform.position : transform.position - transform.forward;
            var away = Flat(transform.position - from);
            if (away.sqrMagnitude < 0.0001f) away = -transform.forward;
            leanAxis = Quaternion.Inverse(transform.rotation) * Vector3.Cross(Vector3.up, away.normalized);
            leanSpeed += hitLean * strength * Mathf.Sqrt(stiffness);
            PlayAt(Pick(hitSounds), Mouth, Random.Range(0.95f, 1.2f));
        }

        // It can be hit only standing at its spot (its hit box on), and never once it burst.
        private void Hittable(bool on)
        {
            if (hitBox != null) hitBox.enabled = on && !burst;
        }

        // Punched to death: a strong explosion, its paint everywhere, the player near it blown back; then it's gone.
        private void Burst()
        {
            burst = true;
            var at = hitBox != null && hitBox.enabled ? hitBox.bounds.center : transform.position + Vector3.up * (0.35f * Size);
            Hittable(false);
            foreach (var effect in burstEffects)
            {
                if (effect != null) EffectPool.Play(effect, at, burstEffectSize * Size);
            }
            foreach (var clip in burstSounds) PlayAt(clip, at, Random.Range(0.92f, 1.05f));
            ShakeView("ShakeBigger");
            var player = FaceTarget != null ? FaceTarget : Camera.main != null ? Camera.main.transform : null;
            bool close = player != null && Flat(player.position - at).magnitude <= burstReach * Size;
            if (close)
            {
                if (!string.IsNullOrEmpty(burstEvent)) Game.Weapons.Blast.SendToPlayer(burstEvent);
                PlayerLaunch.Launch(at, burstPush.x, burstPush.y);
            }
            if (Round != null) Round.GiantBurst(at, Size, close);
            Destroy(gameObject);
        }

        // A camera shake event sent to the main camera's Info FSM (ShakeSmall, ShakeMed, ShakeBigger).
        private static void ShakeView(string eventName)
        {
            var cam = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmGameObject("MainCam");
            if (cam == null || cam.Value == null) return;
            foreach (var fsm in cam.Value.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == "Info") fsm.SendEvent(eventName);
            }
        }

        /// <summary>Gives its jelly a shove: + squashes it, - stretches it.</summary>
        public void Kick(float amount) => squashSpeed += amount * 12f;

        /// <summary>Opens its mouth (or keeps it open) for this many seconds more.</summary>
        public void OpenFor(float seconds) => openUntil = Mathf.Max(openUntil, Time.time + seconds);

        /// <summary>A flying slime comes out of its mouth now: the mouth opens if it isn't, a little lurch and the sound.</summary>
        public void Spit()
        {
            OpenFor(0.3f);
            Kick(-0.1f);
            PlayAt(spitSound, Mouth, Random.Range(0.9f, 1.1f));
        }

        /// <summary>
        /// Falls onto Target (the player's head) from Drop Height, stretched as it falls, lands squashed on top and sits
        /// there Sit Seconds, wobbling with the head. On Land is called the moment it lands.
        /// </summary>
        public IEnumerator DropOn(Transform target, System.Action onLand)
        {
            if (target == null) yield break;
            Hittable(false);
            var look = Flat(target.forward);
            transform.rotation = Quaternion.LookRotation(look.sqrMagnitude > 0.001f ? look : Vector3.forward);
            transform.position = OnHead(target) + Vector3.up * dropHeight;
            PlayAt(fallSound, transform.position, 1f, true);
            for (float t = 0f; t < fallSeconds; t += Time.deltaTime)
            {
                float k = t / fallSeconds;
                // Faster and faster, kept over the head as it moves.
                transform.position = OnHead(target) + Vector3.up * (dropHeight * (1f - k * k));
                stretch = -0.25f * k;
                yield return null;
            }
            stretch = 0f;
            transform.position = OnHead(target);
            speaker.Stop();
            PlayAt(landSound, transform.position, 1f);
            Kick(0.9f);
            onLand?.Invoke();
            for (float t = 0f; t < sitSeconds; t += Time.deltaTime)
            {
                transform.position = Vector3.Lerp(transform.position, OnHead(target), 1f - Mathf.Exp(-12f * Time.deltaTime));
                yield return null;
            }
        }

        /// <summary>Hops from where it is to Spot (on the floor), a hop at a time, facing the way; then turns to its Face Target.</summary>
        public IEnumerator RunTo(Vector3 spot)
        {
            float size = Size;
            Hittable(false);
            int guard = 0;
            while (guard++ < 40)
            {
                var from = transform.position;
                var way = Flat(spot - from);
                float left = way.magnitude;
                if (left < 0.05f && Mathf.Abs(from.y - spot.y) < 0.05f) break;
                float length = Mathf.Min(left, hopLength * size);
                var to = left > 0.001f ? from + way / left * length : from;
                to.y = spot.y;
                heading = left > 0.001f ? way / left : (Vector3?)null;
                yield return Hop(from, to, hopHeight * size + Mathf.Max(0f, from.y - to.y) * 0.3f);
            }
            heading = null;
            // At its spot: it can be hit now.
            Hittable(true);
        }

        /// <summary>Jumps up and away out of sight, then it's gone.</summary>
        public IEnumerator Leave()
        {
            heading = null;
            Hittable(false);
            Kick(0.6f);
            yield return new WaitForSeconds(0.25f);
            var from = transform.position;
            PlayAt(Pick(hopSounds), from, 0.8f);
            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                float k = t / 0.8f;
                transform.position = from + Vector3.up * (12f * k * k + 2f * k);
                stretch = -0.3f;
                yield return null;
            }
            Destroy(gameObject);
        }

        // One hop: a quick crouch, up stretched along an arc, down onto To with a squash.
        private IEnumerator Hop(Vector3 from, Vector3 to, float height)
        {
            Kick(0.35f);
            yield return new WaitForSeconds(0.08f);
            for (float t = 0f; t < hopSeconds; t += Time.deltaTime)
            {
                float k = t / hopSeconds;
                transform.position = Vector3.Lerp(from, to, k) + Vector3.up * (4f * height * k * (1f - k));
                stretch = -0.22f * (1f - Mathf.Abs(2f * k - 1f));
                yield return null;
            }
            stretch = 0f;
            transform.position = to;
            Kick(0.5f);
            PlayAt(Pick(hopSounds), to, Random.Range(0.85f, 1.05f));
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            // The jelly: a spring back to its shape.
            squashSpeed += (-stiffness * squash - damping * squashSpeed) * dt;
            squash = Mathf.Clamp(squash + squashSpeed * dt, -0.6f, 0.6f);
            float pulse = Round != null ? Round.BeatPulse * beatThrob : 0f;
            float breath = Mathf.Sin(Time.time * 2.1f) * breathe;
            float s = squash + stretch;
            if (body != null)
            {
                float wide = 1f + s * 0.5f + pulse + breath * 0.5f;
                float tall = Mathf.Max(0.2f, 1f - s + pulse * 0.5f - breath * 0.3f);
                body.localScale = new Vector3(bodyScale.x * wide, bodyScale.y * tall, bodyScale.z * wide);
                // The lean away from a hit, wobbling back like jelly.
                leanSpeed += (-stiffness * lean - damping * leanSpeed) * dt;
                lean = Mathf.Clamp(lean + leanSpeed * dt, -35f, 35f);
                body.localRotation = Quaternion.AngleAxis(lean, leanAxis) * bodyRest;
            }
            if (!burst && health != null && health.Value <= 0f)
            {
                Burst();
                return;
            }

            // The mouth: the head tips back round its hinge.
            bool open = Time.time < openUntil;
            mouthNow = Mathf.MoveTowards(mouthNow, open ? 1f : 0f, dt / (open ? openSeconds : closeSeconds));
            if (head != null)
            {
                float ease = mouthNow * mouthNow * (3f - 2f * mouthNow);
                head.localRotation = headRest * Quaternion.Euler(-mouthOpenAngle * ease, 0f, 0f);
            }

            // Its face: where it goes while it hops, otherwise the player.
            var want = heading ?? (FaceTarget != null ? Flat(FaceTarget.position - transform.position) : Vector3.zero);
            if (want.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(want.normalized), turnSpeed * dt);
            }
        }

        // Where it sits on the player's head: its bottom just over their eyes.
        private Vector3 OnHead(Transform target) => target.position + Vector3.up * overEyes;

        private void PlayAt(AudioClip clip, Vector3 at, float pitch, bool own = false)
        {
            if (clip == null) return;
            if (own)
            {
                speaker.clip = clip;
                speaker.pitch = pitch;
                speaker.volume = volume;
                speaker.Play();
                return;
            }
            var go = new GameObject("Giant Slime Sound");
            go.transform.position = at;
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.pitch = pitch;
            source.volume = volume;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2f;
            source.maxDistance = 30f;
            source.Play();
            Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
        }

        private static AudioClip Pick(AudioClip[] clips) => clips != null && clips.Length > 0 ? clips[Random.Range(0, clips.Length)] : null;

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
