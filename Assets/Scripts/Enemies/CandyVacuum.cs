using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.Enemies
{
    /// <summary>
    /// Lets an enemy heal by eating candy: candy lying within Radius of it lifts off and flies into its mouth, and each
    /// one eaten adds Health Per Candy to the health in its Health FSM, up to the health it started with. Candy in the
    /// player's hands is safe. Goes on the enemy's root, next to its "Health" FSM (pumpkin_boss_Z has it).
    /// </summary>
    [DisallowMultipleComponent]
    public class CandyVacuum : MonoBehaviour
    {
        [Tooltip("The candy prefabs it eats (Candy_1 ... Candy_8). Copies in the scene and spawned ones both count.")]
        [SerializeField] private GameObject[] candies = System.Array.Empty<GameObject>();

        [Header("Suck")]
        [Tooltip("How far around it candy gets sucked in, in metres.")]
        [Min(0.1f)]
        [SerializeField] private float radius = 3f;
        [Tooltip("How fast candy flies to its mouth, in metres per second. Under 5, so candy doesn't crack on the way.")]
        [Min(0.1f)]
        [SerializeField] private float pullSpeed = 3.5f;
        [Tooltip("Where candy goes. Empty: Mouth Height above its feet.")]
        [SerializeField] private Transform mouth;
        [Tooltip("With no Mouth set: how high its mouth is above its feet, in metres at its normal size (it grows with it).")]
        [SerializeField] private float mouthHeight = 0.35f;
        [Tooltip("Candy this close to its mouth is eaten, in metres at its normal size.")]
        [Min(0.01f)]
        [SerializeField] private float eatDistance = 0.25f;

        [Header("Heal")]
        [Tooltip("The FSM holding its health, and the float variable in it.")]
        [SerializeField] private string healthFsm = "Health";
        [SerializeField] private string healthVariable = "health";
        [Tooltip("Health each candy gives back.")]
        [SerializeField] private float healthPerCandy = 1f;
        [Tooltip("On: it can't heal above the health it started with. Off: no limit.")]
        [SerializeField] private bool capAtStartHealth = true;
        [SerializeField] private AudioClip eatSound;
        [Range(0f, 1f)]
        [SerializeField] private float eatVolume = 1f;

        // How often it looks for new candy, in seconds; candy already on its way is pulled every physics step.
        private const float SearchInterval = 0.1f;

        private static readonly Collider[] hits = new Collider[128];

        private readonly HashSet<string> candyNames = new();
        private readonly List<Rigidbody> pulling = new();
        // Whether each loose object seen so far is candy, so names are only checked once.
        private readonly Dictionary<Rigidbody, bool> isCandy = new();
        private float nextSearch;
        private Collider[] ownColliders;
        private HutongGames.PlayMaker.FsmFloat health;
        private float maxHealth = float.MaxValue;
        private AudioSource voice;

        private void Awake()
        {
            foreach (var candy in candies)
            {
                if (candy != null) candyNames.Add(candy.name);
            }
            ownColliders = GetComponentsInChildren<Collider>(true);
            voice = GetComponent<AudioSource>();
        }

        private void Start()
        {
            foreach (var fsm in GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != healthFsm) continue;
                health = fsm.FsmVariables.FindFsmFloat(healthVariable);
                break;
            }
            if (health == null) Debug.LogWarning($"[CandyVacuum] '{name}' has no '{healthFsm}' FSM with a float '{healthVariable}'; candy won't heal it.", this);
            else if (capAtStartHealth) maxHealth = health.Value;
        }

        private void OnDisable()
        {
            foreach (var body in pulling)
            {
                if (body != null) body.useGravity = true;
            }
            pulling.Clear();
        }

        /// <summary>Whether this is candy it eats (lying around, not in the player's hands).</summary>
        public bool IsCandy(Rigidbody body)
        {
            if (body == null || body.isKinematic) return false;
            if (!isCandy.TryGetValue(body, out bool candy))
            {
                if (isCandy.Count > 1000) isCandy.Clear();
                candy = candyNames.Contains(BaseName(body.name));
                isCandy[body] = candy;
            }
            if (!candy) return false;
            return !body.TryGetComponent<XRGrabInteractable>(out var grab) || !grab.isSelected;
        }

        private void FixedUpdate()
        {
            // Dying: no more snacks.
            if (health != null && health.Value <= 0f)
            {
                OnDisable();
                return;
            }

            if (Time.time >= nextSearch)
            {
                nextSearch = Time.time + SearchInterval;
                FindCandy();
            }

            float size = transform.lossyScale.x;
            var target = mouth != null ? mouth.position : transform.position + Vector3.up * (mouthHeight * size);
            for (int i = pulling.Count - 1; i >= 0; i--)
            {
                var body = pulling[i];
                if (body == null) { pulling.RemoveAt(i); continue; }

                var toMouth = target - body.worldCenterOfMass;
                float distance = toMouth.magnitude;
                if (distance <= eatDistance * size) { Eat(body); pulling.RemoveAt(i); continue; }
                // Grabbed back by the player, or left far behind.
                if (!IsCandy(body) || distance > radius * 1.5f)
                {
                    body.useGravity = true;
                    pulling.RemoveAt(i);
                    continue;
                }

                var wanted = toMouth / distance * pullSpeed;
                body.linearVelocity = Vector3.MoveTowards(body.linearVelocity, wanted, pullSpeed * 4f * Time.fixedDeltaTime);
            }
        }

        // Starts pulling every candy in range that isn't already on its way.
        private void FindCandy()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, radius, hits, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var body = hits[i].attachedRigidbody;
                if (!IsCandy(body) || pulling.Contains(body)) continue;

                pulling.Add(body);
                body.useGravity = false;
                body.WakeUp();
                // Straight into the mouth, without bumping into its body on the way.
                foreach (var candyCollider in body.GetComponentsInChildren<Collider>())
                {
                    foreach (var own in ownColliders)
                    {
                        if (own != null) Physics.IgnoreCollision(candyCollider, own);
                    }
                }
            }
        }

        private void Eat(Rigidbody body)
        {
            Destroy(body.gameObject);
            if (health != null) health.Value = Mathf.Min(health.Value + healthPerCandy, Mathf.Max(maxHealth, health.Value));
            if (eatSound == null) return;
            if (voice != null) voice.PlayOneShot(eatSound, eatVolume);
            else AudioSource.PlayClipAtPoint(eatSound, transform.position, eatVolume);
        }

        // "Candy_3(Clone)" and "Candy_3 (2)" are both Candy_3.
        private static string BaseName(string objectName)
        {
            int bracket = objectName.IndexOf('(');
            return (bracket >= 0 ? objectName.Substring(0, bracket) : objectName).TrimEnd();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.6f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
#endif
    }
}
