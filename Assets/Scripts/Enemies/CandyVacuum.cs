using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.Enemies
{
    /// <summary>
    /// Lets an enemy heal by eating candy: candy lying within Radius of it lifts off and flies into its mouth, and each
    /// one eaten heals the health in its Health FSM (Heal Like Player: the player's candy heal, which may go past the
    /// health it started with, ever more slowly; else Health Per Candy, up to the start health). Candy in the
    /// player's hands is safe. On the pumpkin boss it doesn't eat while the boss is dizzy (stunned): candy on its way
    /// drops, and nothing is pulled in until it comes round. Goes on the enemy's root, next to its "Health" FSM
    /// (pumpkin_boss_Z has it).
    /// On the pumpkin boss (user, 2026-11-11) candy flies to its real mouth (its jaw, wherever its head is: as a head in
    /// the floor, its upper body, its true form on the giant pumpkin), the eat distances grow with the boss's model (the
    /// root stays size 1), and it reaches Radius past its giant pumpkin's edge. Candy on its way ignores the other candy
    /// on its way: solid balls 0.6 m wide all flying at one point used to jam a few tenths of a metre short of it (a play
    /// test dropped 8, it ate 1); and one pulled for Stuck Eat Seconds that's near the mouth is eaten anyway.
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
        [Tooltip("Where candy goes. Empty: on the pumpkin boss its real mouth (Pumpkin Boss Brain's, at its jaw), on " +
                 "anything else Mouth Height above its feet.")]
        [SerializeField] private Transform mouth;
        [Tooltip("With no Mouth set and no boss brain: how high its mouth is above its feet, in metres at its normal size " +
                 "(it grows with it). On the boss: the lowest the candy is aimed (its head sits in the floor at first).")]
        [SerializeField] private float mouthHeight = 0.35f;
        [Tooltip("Candy this close to its mouth is eaten, in metres at its normal size (on the pumpkin boss times its " +
                 "model's size: about 0.47 m as a head or upper body, 0.58 m in its true form).")]
        [Min(0.01f)]
        [SerializeField] private float eatDistance = 0.25f;
        [Tooltip("Candy pulled this many seconds that has got within Stuck Eat Distance of the mouth is eaten anyway (held up " +
                 "by something on the way).")]
        [Min(0.5f)]
        [SerializeField] private float stuckEatSeconds = 3f;
        [Tooltip("See Stuck Eat Seconds: in metres at its normal size (times the boss's model size, like Eat Distance).")]
        [Min(0.01f)]
        [SerializeField] private float stuckEatDistance = 0.6f;

        [Header("Heal")]
        [Tooltip("The FSM holding its health, and the float variable in it.")]
        [SerializeField] private string healthFsm = "Health";
        [SerializeField] private string healthVariable = "health";
        [Tooltip("On (user, 2026-11-11: the boss heals from candy the way the player does): each candy heals a roll of " +
                 "Heal Roll, in full up to the health it started with, Above Share of it from there to Player Level's " +
                 "Candy Slow Heal From (1.5x), and only Candy Slow Heal Share (10%) above that. No limit. Off: Health Per " +
                 "Candy each, capped as below.")]
        [SerializeField] private bool healLikePlayer = true;
        [Tooltip("With Heal Like Player: the candy's heal roll, whole numbers, both ends included (the player's candy: 1 to 7).")]
        [SerializeField] private Vector2Int healRoll = new(1, 7);
        [Tooltip("With Heal Like Player: the share of the roll that counts above the start health (0.5 = half, as the player).")]
        [Range(0f, 1f)]
        [SerializeField] private float aboveShare = 0.5f;
        [Tooltip("Without Heal Like Player: health each candy gives back.")]
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
        // Each pulled candy's solid colliders (they ignore the other pulled candy's) and when its pull started.
        private readonly Dictionary<Rigidbody, Collider[]> pulledParts = new();
        private readonly Dictionary<Rigidbody, float> pulledSince = new();
        // Whether each loose object seen so far is candy, so names are only checked once.
        private readonly Dictionary<Rigidbody, bool> isCandy = new();
        private float nextSearch;
        private Collider[] ownColliders;
        private HutongGames.PlayMaker.FsmFloat health;
        private float maxHealth = float.MaxValue;
        private float startHealth;
        private AudioSource voice;
        // The pumpkin boss laughs non-stop: its munching goes through it, so the two never play at once.
        private PumpkinBossBrain boss;

        private void Awake()
        {
            foreach (var candy in candies)
            {
                if (candy != null) candyNames.Add(candy.name);
            }
            ownColliders = GetComponentsInChildren<Collider>(true);
            voice = GetComponent<AudioSource>();
            boss = GetComponent<PumpkinBossBrain>();
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
            else
            {
                // Read after the boss's own set-up (the chaos roll's extra health included).
                startHealth = health.Value;
                if (capAtStartHealth) maxHealth = health.Value;
            }
        }

        // Everything on its way drops where it is (and bumps into the other candy again).
        private void OnDisable()
        {
            for (int i = pulling.Count - 1; i >= 0; i--) LetGo(i);
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
            // Dying, or stunned (the pumpkin boss dizzy): no snacks. Candy on its way drops where it is.
            if ((health != null && health.Value <= 0f) || (boss != null && boss.IsDizzy))
            {
                OnDisable();
                return;
            }

            if (Time.time >= nextSearch)
            {
                nextSearch = Time.time + SearchInterval;
                FindCandy();
            }

            float size = Size;
            var target = MouthTarget();
            for (int i = pulling.Count - 1; i >= 0; i--)
            {
                var body = pulling[i];
                if (body == null) { Forget(i); continue; }

                var toMouth = target - body.worldCenterOfMass;
                float distance = toMouth.magnitude;
                // At its mouth; or held up a while just short of it.
                bool stuck = pulledSince.TryGetValue(body, out float since) && Time.time - since >= stuckEatSeconds &&
                             distance <= stuckEatDistance * size;
                if (distance <= eatDistance * size || stuck) { Eat(body); Forget(i); continue; }
                // Grabbed back by the player, or left far behind (measured from where it looks for candy: its mouth may
                // be high up).
                if (!IsCandy(body) || (body.worldCenterOfMass - transform.position).magnitude > Reach * 1.5f)
                {
                    LetGo(i);
                    continue;
                }

                var wanted = toMouth / distance * pullSpeed;
                body.linearVelocity = Vector3.MoveTowards(body.linearVelocity, wanted, pullSpeed * 4f * Time.fixedDeltaTime);
            }
        }

        // Its size for Mouth Height and the eat distances: the pumpkin boss's model (the root stays 1 while the model
        // grows), else its own scale.
        private float Size => boss != null ? boss.ModelSize : transform.lossyScale.x;

        // How far round it candy is pulled in: Radius, past the boss's giant pumpkin while that's out.
        private float Reach => radius + (boss != null ? boss.BodyRadius : 0f);

        // Where candy flies: the Mouth, else the boss's real mouth (never under the floor: its head sits in it at first),
        // else Mouth Height above its feet.
        private Vector3 MouthTarget()
        {
            if (mouth != null) return mouth.position;
            float lowest = mouthHeight * transform.lossyScale.x;
            if (boss == null) return transform.position + Vector3.up * lowest;
            var at = boss.MouthPosition;
            at.y = Mathf.Max(at.y, transform.position.y + lowest);
            return at;
        }

        // Starts pulling every candy in range that isn't already on its way.
        private void FindCandy()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, Reach, hits, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var body = hits[i].attachedRigidbody;
                if (!IsCandy(body) || pulling.Contains(body)) continue;

                // Candy on its way doesn't bump into other candy on its way (they all fly at one point).
                var parts = SolidParts(body);
                foreach (var other in pulling)
                {
                    if (other != null && pulledParts.TryGetValue(other, out var theirs)) IgnorePairs(parts, theirs, true);
                }
                pulling.Add(body);
                pulledParts[body] = parts;
                pulledSince[body] = Time.time;
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

        // A candy let go without being eaten (grabbed back, left behind, the boss dizzy or dying): it falls again and bumps
        // into the other candy again.
        private void LetGo(int i)
        {
            var body = pulling[i];
            if (body != null)
            {
                body.useGravity = true;
                if (pulledParts.TryGetValue(body, out var mine))
                {
                    foreach (var other in pulling)
                    {
                        if (other != null && other != body && pulledParts.TryGetValue(other, out var theirs)) IgnorePairs(mine, theirs, false);
                    }
                }
            }
            Forget(i);
        }

        // Off the list (eaten, gone or let go).
        private void Forget(int i)
        {
            var body = pulling[i];
            pulling.RemoveAt(i);
            if (ReferenceEquals(body, null)) return;
            pulledParts.Remove(body);
            pulledSince.Remove(body);
        }

        private static Collider[] SolidParts(Rigidbody body)
        {
            var all = body.GetComponentsInChildren<Collider>();
            var solid = new List<Collider>(all.Length);
            foreach (var part in all)
            {
                if (part != null && !part.isTrigger) solid.Add(part);
            }
            return solid.ToArray();
        }

        private static void IgnorePairs(Collider[] a, Collider[] b, bool ignore)
        {
            foreach (var x in a)
            {
                if (x == null) continue;
                foreach (var y in b)
                {
                    if (y != null) Physics.IgnoreCollision(x, y, ignore);
                }
            }
        }

        private void Eat(Rigidbody body)
        {
            Destroy(body.gameObject);
            if (health != null)
            {
                if (healLikePlayer && startHealth > 0f)
                {
                    float roll = Random.Range(Mathf.Min(healRoll.x, healRoll.y), Mathf.Max(healRoll.x, healRoll.y) + 1);
                    health.Value += HutongGames.PlayMaker.Actions.CandyHeal.AboveCap(health.Value, roll, startHealth, aboveShare, true);
                }
                else health.Value = Mathf.Min(health.Value + healthPerCandy, Mathf.Max(maxHealth, health.Value));
            }
            if (eatSound == null) return;
            // One mouth: on the pumpkin boss the munching stops its laugh for a moment (and doesn't stack per candy).
            if (boss != null && boss.SayInsteadOfLaugh(eatSound, eatVolume)) return;
            if (voice != null) voice.PlayOneShot(eatSound, eatVolume);
            else AudioSource.PlayClipAtPoint(eatSound, transform.position, eatVolume);
        }

        // "Candy_4(Clone)" and "Candy_4 (2)" are both Candy_4.
        private static string BaseName(string objectName)
        {
            int bracket = objectName.IndexOf('(');
            return (bracket >= 0 ? objectName.Substring(0, bracket) : objectName).TrimEnd();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.6f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, Application.isPlaying ? Reach : radius);
        }
#endif
    }
}
