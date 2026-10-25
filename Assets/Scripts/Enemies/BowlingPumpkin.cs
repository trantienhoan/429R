using Game.Combat;
using Game.Locomotion;
using Game.Weapons;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Enemies
{
    /// <summary>
    /// A tree pumpkin turned bowling ball (Turns Into Bowler makes it): it bursts out where a Pumpkin_On_Tree was, turns
    /// to the player and shakes for Wind Up Seconds (the warning: a laugh, then the Roll Warning's choo-choo, which ends
    /// as it sets off), then rolls at where they stand in a straight line,
    /// trailing smoke. It explodes when it reaches the player, when it runs into a wall or reaches a Fall Edge (both stop
    /// it on its NavMesh), after Max Roll metres, or when the player hits it first (a punch, a weapon, something thrown):
    /// the game's explosion (Blast), and the player within Blast Radius is hurt (Player Event) and thrown. A step aside
    /// or a teleport dodges it. Goes on the prefab's root, next to its NavMesh Agent; Model is the ball that rolls.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public class BowlingPumpkin : MonoBehaviour
    {
        [Header("Look")]
        [Tooltip("The ball that rolls (its pivot in the middle of the ball).")]
        [SerializeField] private Transform model;
        [Tooltip("The ball's radius, in metres at size 1: how far one turn rolls it.")]
        [Min(0.01f)]
        [SerializeField] private float radius = 0.23f;

        [Header("Waking up")]
        [Tooltip("Played where it bursts out of the tree pumpkin (Effect Pool), and their size.")]
        [SerializeField] private GameObject[] breakOutEffects = System.Array.Empty<GameObject>();
        [Min(0f)]
        [SerializeField] private float breakOutEffectSize = 1f;
        [SerializeField] private AudioClip breakOutSound;
        [Tooltip("How long it turns to the player and shakes before it rolls, in seconds: the player's warning (its laugh, " +
                 "then Roll Warning). It aims at where they stand when this ends.")]
        [Min(0f)]
        [SerializeField] private float windUpSeconds = 2f;
        [Tooltip("How far it shakes while it winds up, in metres.")]
        [Min(0f)]
        [SerializeField] private float shake = 0.03f;
        [Tooltip("Its laugh as it wakes up (one at random).")]
        [SerializeField] private AudioClip[] wakeSounds = System.Array.Empty<AudioClip>();
        [Tooltip("Played just before it rolls, timed to end the moment it sets off (choo_choo); the laugh fades out under it.")]
        [SerializeField] private AudioClip rollWarning;
        [Range(0f, 1f)]
        [SerializeField] private float rollWarningVolume = 1f;

        [Header("Rolling")]
        [Tooltip("How fast it rolls, in metres per second.")]
        [Min(0.5f)]
        [SerializeField] private float rollSpeed = 4.55f;
        [Tooltip("It explodes after rolling this far, in metres, if nothing stopped it before.")]
        [Min(1f)]
        [SerializeField] private float maxRoll = 15f;
        [Tooltip("How close its path has to pass the player to hit them, in metres at size 1, plus their body.")]
        [Min(0.05f)]
        [SerializeField] private float hitRadius = 0.3f;
        [Tooltip("Played, looping, while it rolls (its own Audio Source).")]
        [SerializeField] private AudioClip rollSound;
        [Tooltip("Puffs of smoke it leaves along its way (Effect Pool), one every Trail Every metres, this big.")]
        [SerializeField] private GameObject trailPuff;
        [Min(0.1f)]
        [SerializeField] private float trailEvery = 0.6f;
        [Min(0f)]
        [SerializeField] private float trailSize = 0.3f;

        [Header("Exploding")]
        [Tooltip("How far the blast reaches, in metres: the player within it is hurt and thrown; enemies and breakables " +
                 "around take Blast Damage and loose things are blown away.")]
        [Min(0.5f)]
        [SerializeField] private float blastRadius = 2f;
        [Min(0f)]
        [SerializeField] private float blastDamage = 3f;
        [Min(0f)]
        [SerializeField] private float blastPush = 6f;
        [SerializeField] private GameObject[] blastEffects = System.Array.Empty<GameObject>();
        [Min(0f)]
        [SerializeField] private float blastEffectSize = 1.5f;
        [SerializeField] private AudioClip blastSound;
        [Tooltip("Sent to the player's Damage FSM when the blast reaches them: Damage (6-9), DamageHeavy (40-50).")]
        [SerializeField] private string playerEvent = "DamageHeavy";
        [Tooltip("The blast throws the player this high (x) and this far (y), in metres (Toughness cuts both).")]
        [SerializeField] private Vector2 playerLaunch = new(1.5f, 3f);
        [Tooltip("A hit of the player's at least this fast (metres per second: a punch, a weapon, something thrown) sets it " +
                 "off where it is.")]
        [Min(0f)]
        [SerializeField] private float setOffByHit = 3f;

        private enum Phase { WindUp, Roll, Gone }

        // Seconds it may wait for a player at all, and how long its pop out of the pumpkin takes.
        private const float GiveUpSeconds = 12f;
        private const float PopSeconds = 0.25f;
        // How quickly the laugh fades out under the roll warning, in seconds.
        private const float LaughFadeSeconds = 0.25f;

        /// <summary>How many are out right now.</summary>
        public static int Alive { get; private set; }

        private NavMeshAgent agent;
        private Rigidbody body;
        private AudioSource speaker;
        private AudioSource warner;
        private float speakerVolume = 1f;
        private bool warned;
        private XROrigin player;
        private Phase phase;
        private float phaseUntil;
        private float bornAt;
        private Vector3 modelHome;
        private Vector3 fullScale;
        private Vector3 direction;
        private float rolled;
        private float sinceTrail;
        private float stuck;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Alive = 0;

        private void OnEnable() => Alive++;
        private void OnDisable() => Alive--;

        private void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.updateRotation = false;
            if (agent.isOnNavMesh) agent.isStopped = true;
            TryGetComponent(out body);
            TryGetComponent(out speaker);
            if (speaker != null)
            {
                speakerVolume = speaker.volume;
                // The roll warning has its own speaker, heard from the ball like the laugh, so the laugh can fade under it.
                if (rollWarning != null)
                {
                    warner = gameObject.AddComponent<AudioSource>();
                    warner.playOnAwake = false;
                    warner.spatialBlend = speaker.spatialBlend;
                    warner.rolloffMode = speaker.rolloffMode;
                    warner.minDistance = speaker.minDistance;
                    warner.maxDistance = speaker.maxDistance;
                    warner.outputAudioMixerGroup = speaker.outputAudioMixerGroup;
                }
            }
            player = FindAnyObjectByType<XROrigin>();
            if (model != null) modelHome = model.localPosition;
            fullScale = transform.localScale;
            bornAt = Time.time;

            foreach (var effect in breakOutEffects)
            {
                if (effect != null) EffectPool.Play(effect, Middle(), breakOutEffectSize);
            }
            if (breakOutSound != null) AudioSource.PlayClipAtPoint(breakOutSound, Middle());
            if (wakeSounds.Length > 0 && speaker != null) speaker.PlayOneShot(wakeSounds[Random.Range(0, wakeSounds.Length)]);
            phase = Phase.WindUp;
            phaseUntil = Time.time + windUpSeconds;
        }

        private void Update()
        {
            if (phase == Phase.Gone) return;
            if (player == null || player.Camera == null || !agent.isOnNavMesh || Time.time - bornAt > GiveUpSeconds + windUpSeconds)
            {
                Explode();
                return;
            }
            if (phase == Phase.WindUp) WindUp();
            else Roll();
        }

        // Pops out of the pumpkin, turns to the player and shakes; then sets off at where they stand.
        private void WindUp()
        {
            float popped = Mathf.Clamp01((Time.time - bornAt) / PopSeconds);
            transform.localScale = fullScale * Mathf.Lerp(0.6f, 1f, 1f - (1f - popped) * (1f - popped));
            var to = PlayerFloor() - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 360f * Time.deltaTime);
            if (model != null) model.localPosition = modelHome + Random.insideUnitSphere * (shake / Mathf.Max(0.01f, transform.lossyScale.y));
            WarnBeforeRolling();
            if (Time.time < phaseUntil) return;

            if (model != null) model.localPosition = modelHome;
            transform.localScale = fullScale;
            direction = to.sqrMagnitude > 0.0001f ? to.normalized : transform.forward;
            transform.rotation = Quaternion.LookRotation(direction);
            phase = Phase.Roll;
            rolled = 0f;
            stuck = 0f;
            if (speaker != null && rollSound != null)
            {
                // What's left of the laugh goes; the rolling sound at full volume.
                speaker.Stop();
                speaker.volume = speakerVolume;
                speaker.clip = rollSound;
                speaker.loop = true;
                speaker.Play();
            }
        }

        // The roll warning starts so it ends the moment the ball sets off, and the laugh fades out under it.
        private void WarnBeforeRolling()
        {
            if (warner == null) return;
            if (!warned && Time.time >= phaseUntil - rollWarning.length)
            {
                warned = true;
                warner.PlayOneShot(rollWarning, rollWarningVolume);
            }
            if (warned && speaker != null)
                speaker.volume = Mathf.MoveTowards(speaker.volume, 0f, speakerVolume * Time.deltaTime / LaughFadeSeconds);
        }

        private void Roll()
        {
            float step = rollSpeed * Time.deltaTime;
            var before = transform.position;
            agent.Move(direction * step);
            var moved = transform.position - before;
            moved.y = 0f;
            float progress = Vector3.Dot(moved, direction);
            rolled += Mathf.Max(0f, progress);

            // The ball turns as it rolls.
            if (model != null && progress > 0f)
            {
                float size = Mathf.Max(0.01f, transform.lossyScale.y);
                model.Rotate(Vector3.Cross(Vector3.up, direction), progress / (radius * size) * Mathf.Rad2Deg, Space.World);
            }
            sinceTrail += Mathf.Max(0f, progress);
            if (trailPuff != null && sinceTrail >= trailEvery)
            {
                sinceTrail = 0f;
                EffectPool.Play(trailPuff, transform.position - direction * (radius * transform.lossyScale.y), trailSize);
            }

            if (ReachesPlayer(before, transform.position)) { Explode(); return; }
            // A wall, furniture or a Fall Edge (holes in its NavMesh) stops it: it explodes there.
            if (progress < step * 0.3f) stuck += Time.deltaTime;
            else stuck = 0f;
            if (stuck > 0.1f || FallEdge.Covers(transform.position + direction * 0.3f) || rolled >= maxRoll) Explode();
        }

        // Its path this frame passing within Hit Radius of the player's body.
        private bool ReachesPlayer(Vector3 from, Vector3 to)
        {
            float size = Mathf.Max(0.01f, transform.lossyScale.y);
            var path = to + direction * (radius * size) - from;
            path.y = 0f;
            var toPlayer = PlayerFloor() - from;
            toPlayer.y = 0f;
            float t = path.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector3.Dot(toPlayer, path) / path.sqrMagnitude) : 0f;
            return (toPlayer - path * t).magnitude <= (hitRadius + radius) * size;
        }

        // The player's hits set it off where it is: a fist, a weapon, a thrown thing.
        private void OnCollisionEnter(Collision collision)
        {
            if (phase == Phase.Gone) return;
            var other = collision.rigidbody;
            if (other == null || collision.relativeVelocity.magnitude < setOffByHit) return;
            if (Fist.IsFistBall(other) || other.TryGetComponent<WeaponPower>(out _) || other.CompareTag("Weapon")) Explode();
        }

        private void Explode()
        {
            if (phase == Phase.Gone) return;
            phase = Phase.Gone;
            var at = Middle();
            if (speaker != null) speaker.Stop();
            if (warner != null) warner.Stop();
            bool hurt = Blast.Explode(at, blastRadius, blastDamage, blastPush, body, blastEffects, blastSound, 1f, playerEvent,
                false, blastEffectSize);
            if (hurt) PlayerLaunch.Launch(at, playerLaunch.x, playerLaunch.y);
            Destroy(gameObject);
        }

        private Vector3 Middle() => transform.position + Vector3.up * (radius * Mathf.Max(0.01f, transform.lossyScale.y));

        private Vector3 PlayerFloor()
        {
            var head = player.Camera.transform.position;
            return new Vector3(head.x, transform.position.y, head.z);
        }
    }
}
