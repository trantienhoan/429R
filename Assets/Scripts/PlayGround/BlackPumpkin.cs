using Game.Enemies;
using UnityEngine;
using UnityEngine.AI;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;

namespace Game.PlayGround
{
    /// <summary>
    /// The black pumpkin a wrong recipe spits out of the cauldron. Where it lands it hops, cackles, wobbles and swells
    /// up, then bursts in a puff of smoke into a mini boss picked at random (within the game's enemy limit; with no room
    /// left it just bursts). Goes on pumpkin_black's root, which needs a Rigidbody and a convex collider.
    /// </summary>
    [DisallowMultipleComponent]
    public class BlackPumpkin : MonoBehaviour
    {
        [Tooltip("Mini bosses it can burst into; one at random.")]
        [SerializeField] private GameObject[] bosses = System.Array.Empty<GameObject>();
        [Tooltip("Seconds it wobbles after landing before it bursts.")]
        [Min(0f)] [SerializeField] private float fuseSeconds = 2f;
        [Tooltip("It starts wobbling anyway this many seconds after it was spat out, landed or not.")]
        [Min(0.5f)] [SerializeField] private float landSeconds = 4f;
        [Tooltip("How much it swells before it bursts (0.3 = a third bigger).")]
        [Min(0f)] [SerializeField] private float swell = 0.3f;
        [Tooltip("Played as it starts to wobble; one at random.")]
        [SerializeField] private AudioClip[] cackles = System.Array.Empty<AudioClip>();
        [Tooltip("Played as it bursts; one at random.")]
        [SerializeField] private AudioClip[] burstSounds = System.Array.Empty<AudioClip>();
        [Tooltip("Burst where it pops, e.g. smoke.")]
        [SerializeField] private GameObject burstEffect;
        [Min(0.01f)] [SerializeField] private float burstEffectScale = 0.15f;

        private const string SpawnedStuffVariable = "CurrentlySpawnedStuffs";
        // Bumps in its first moments (e.g. the cauldron's rim on the way out) aren't landing.
        private const float MinFlight = 0.3f;

        private Rigidbody body;
        private Vector3 size;
        private float madeAt;
        private float landedAt = -1f;
        private bool burst;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            size = transform.localScale;
            madeAt = Time.time;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (landedAt < 0f && Time.time - madeAt >= MinFlight) Land();
        }

        private void Update()
        {
            if (burst) return;
            if (landedAt < 0f)
            {
                if (Time.time - madeAt >= landSeconds) Land();
                return;
            }

            float since = Time.time - landedAt;
            float t = fuseSeconds > 0f ? since / fuseSeconds : 1f;
            if (t >= 1f)
            {
                Burst();
                return;
            }
            // Swells up, throbbing faster and harder, like something inside wants out.
            float throb = Mathf.Sin(since * (12f + 30f * t)) * 0.06f * t;
            transform.localScale = size * (1f + swell * t * t + throb);
        }

        private void Land()
        {
            landedAt = Time.time;
            if (cackles.Length > 0) Play(cackles[Random.Range(0, cackles.Length)], transform.position);
            if (body != null && !body.isKinematic)
            {
                body.AddForce(Vector3.up * 1.2f, ForceMode.VelocityChange);
                body.AddTorque(Random.onUnitSphere * 2f, ForceMode.VelocityChange);
            }
        }

        private void Burst()
        {
            burst = true;
            var at = Middle();
            if (burstEffect != null)
            {
                var effect = Instantiate(burstEffect, at, Quaternion.identity);
                effect.transform.localScale = Vector3.one * burstEffectScale;
                Destroy(effect, 4f);
            }
            if (burstSounds.Length > 0) Play(burstSounds[Random.Range(0, burstSounds.Length)], at);

            var prefab = bosses.Length > 0 ? bosses[Random.Range(0, bosses.Length)] : null;
            if (prefab != null && EnemyLimit.TryTakeSlot())
            {
                var spot = transform.position;
                if (NavMesh.SamplePosition(spot, out var onMesh, 3f, NavMesh.AllAreas)) spot = onMesh.position;
                Instantiate(prefab, spot, FacingPlayer(spot), SpawnedStuff());
            }
            Destroy(gameObject);
        }

        private Vector3 Middle()
        {
            var shown = GetComponentInChildren<Renderer>();
            return shown != null ? shown.bounds.center : transform.position;
        }

        // Turned to the player, so the boss comes out looking at them.
        private static Quaternion FacingPlayer(Vector3 from)
        {
            var camera = Camera.main;
            if (camera != null)
            {
                var toPlayer = camera.transform.position - from;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.0001f) return Quaternion.LookRotation(toPlayer);
            }
            return Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }

        private static void Play(AudioClip clip, Vector3 at)
        {
            if (clip != null) AudioSource.PlayClipAtPoint(clip, at);
        }

        // Where spawned things go, so the stage clean-up removes them.
        private static Transform SpawnedStuff()
        {
            var holder = FsmVariables.GlobalVariables.FindFsmGameObject(SpawnedStuffVariable);
            return holder != null && holder.Value != null ? holder.Value.transform : null;
        }
    }
}
