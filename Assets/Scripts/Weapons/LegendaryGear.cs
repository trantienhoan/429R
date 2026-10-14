using System.Collections.Generic;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game.Weapons
{
    /// <summary>
    /// The knight's legendary sword and shield (rare legendary special offers in the shop). Holding it, keep the trigger
    /// pressed and it grows to twice its size (Grow Scale), its glow and light on; let go of the trigger, or of it, and
    /// it shrinks back. A shield (Blocks) held between an enemy and your head stops the enemy's bite or hit. They don't
    /// last: from the first time one is picked up it counts down Life Seconds (210) on a little label above it, then
    /// bursts into a few candies. Tools > 429 Game > Weapons > Make Legendary Knight Gear sets them up.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class LegendaryGear : MonoBehaviour
    {
        [Header("Grow")]
        [Tooltip("How big it gets while the trigger is held: 2 = twice its size.")]
        [Min(1f)]
        [SerializeField] private float growScale = 2f;
        [Tooltip("Seconds to grow or shrink. Not much faster: a held weapon may only move about 1 m/s faster than the hand (Held Swing Limit), so a quicker grow leaves the sword behind the hand for a moment.")]
        [Min(0.01f)]
        [SerializeField] private float growSeconds = 0.4f;
        [Tooltip("Shown only while it's big, e.g. its glow and light.")]
        [SerializeField] private GameObject[] showWhileBig = System.Array.Empty<GameObject>();
        [SerializeField] private AudioClip growSound;

        [Header("Shield")]
        [Tooltip("Held between an enemy and your head, it stops the enemy's bite or hit.")]
        [SerializeField] private bool blocks;
        [Tooltip("Degrees past the shield's edge, seen from your head, that still count as covered.")]
        [Range(0f, 45f)]
        [SerializeField] private float blockMargin = 12f;
        [SerializeField] private AudioClip blockSound;
        [SerializeField] private GameObject blockEffect;
        [Min(0f)]
        [SerializeField] private float blockEffectSize = 0.15f;

        [Header("Time limit")]
        [Tooltip("Seconds of play it lasts from the first time it's picked up, then it bursts into candies. 0 = forever.")]
        [Min(0f)]
        [SerializeField] private float lifeSeconds = 210f;
        [Tooltip("The countdown turns red and blinks for this many last seconds.")]
        [Min(0f)]
        [SerializeField] private float warnSeconds = 30f;
        [Tooltip("What it turns into: this many of these, picked at random.")]
        [SerializeField] private GameObject[] candies = System.Array.Empty<GameObject>();
        [SerializeField] private Vector2Int candyCount = new(3, 5);
        [SerializeField] private GameObject poofEffect;
        [Min(0f)]
        [SerializeField] private float poofEffectSize = 0.4f;
        [SerializeField] private AudioClip poofSound;

        private const string SpawnedStuffVariable = "CurrentlySpawnedStuffs";
        // The countdown label: this far above the grip, and this big (metres per canvas pixel).
        private const float LabelHeight = 0.12f;
        private const float LabelScale = 0.0006f;
        // Candies appear this far from the player, along the floor, and this far apart: a candy is eaten when its
        // 0.6 m trigger touches the player, and is 0.6 m across.
        private const float CandyDistance = 1.5f;
        private const float CandySpacing = 0.6f;

        private static readonly List<LegendaryGear> shields = new();
        private static readonly Color LabelColor = new(1f, 0.92f, 0.6f);
        private static readonly Color WarnColor = new(1f, 0.35f, 0.3f);

        private XRGrabInteractable grab;
        private Vector3 baseScale;
        private float size = 1f;
        private bool triggered;
        private bool started;
        private float endsAt;
        private RectTransform label;
        private TMP_Text labelText;
        private int shownSeconds = -1;
        private Transform head;

        /// <summary>Seconds left before it turns into candies (its full life until it's first picked up).</summary>
        public float SecondsLeft => started ? Mathf.Max(0f, endsAt - Time.time) : lifeSeconds;

        public bool IsBig => size > 1.001f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            shields.Clear();
        }

        /// <summary>
        /// Whether a shield held by the player covers their head from an attack coming from this point. If one does, it
        /// clangs and buzzes the hand holding it, and the attacker should do no damage.
        /// </summary>
        public static bool TryBlock(Vector3 from)
        {
            if (shields.Count == 0) return false;
            var eyes = Camera.main;
            if (eyes == null) return false;
            var headPosition = eyes.transform.position;
            foreach (var shield in shields)
            {
                if (shield == null || !shield.Covers(headPosition, from)) continue;
                shield.Blocked(from);
                return true;
            }
            return false;
        }

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            baseScale = transform.localScale;
            ShowBig(false);
        }

        private void OnEnable()
        {
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnLetGo);
            grab.activated.AddListener(OnTriggerPressed);
            grab.deactivated.AddListener(OnTriggerReleased);
            if (blocks) shields.Add(this);
        }

        private void OnDisable()
        {
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnLetGo);
            grab.activated.RemoveListener(OnTriggerPressed);
            grab.deactivated.RemoveListener(OnTriggerReleased);
            shields.Remove(this);
        }

        private void OnDestroy()
        {
            if (label != null) Destroy(label.gameObject);
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            if (started || lifeSeconds <= 0f) return;
            started = true;
            endsAt = Time.time + lifeSeconds;
            MakeLabel();
        }

        private void OnLetGo(SelectExitEventArgs args)
        {
            if (!grab.isSelected) triggered = false;
        }

        private void OnTriggerPressed(ActivateEventArgs args)
        {
            if (!triggered && growSound != null) AudioSource.PlayClipAtPoint(growSound, transform.position);
            triggered = true;
        }

        private void OnTriggerReleased(DeactivateEventArgs args)
        {
            triggered = false;
        }

        private void Update()
        {
            Grow();
            if (!started) return;
            if (Time.time >= endsAt)
            {
                Expire();
                return;
            }
            ShowCountdown();
        }

        private void LateUpdate()
        {
            if (label == null) return;
            var grip = grab.attachTransform;
            label.position = (grip != null ? grip.position : transform.position) + Vector3.up * LabelHeight;
            if (head == null && Camera.main != null) head = Camera.main.transform;
            WorldUI.Face(label, head);
        }

        // Smoothly to its big or normal size. Held, XRI owns its scale, so it's asked to grow around the grip.
        private void Grow()
        {
            float target = triggered && grab.isSelected ? growScale : 1f;
            if (Mathf.Approximately(size, target)) return;

            bool wasBig = IsBig;
            size = Mathf.MoveTowards(size, target, (growScale - 1f) / growSeconds * Time.deltaTime);
            var scale = baseScale * size;
            if (grab.isSelected) grab.SetTargetLocalScale(scale);
            else transform.localScale = scale;
            if (IsBig != wasBig) ShowBig(IsBig);
        }

        private void ShowBig(bool big)
        {
            foreach (var thing in showWhileBig)
            {
                if (thing != null) thing.SetActive(big);
            }
        }

        private void MakeLabel()
        {
            label = WorldUI.CreatePanel(name + " Countdown", 150f, 52f, LabelScale, new Color(0.08f, 0.05f, 0.12f, 0.75f));
            labelText = WorldUI.CreateText("Time", label, "", 34f, FontStyles.Bold, TextAlignmentOptions.Center, LabelColor, TMP_Settings.defaultFontAsset);
            WorldUI.Stretch(labelText.rectTransform);
            shownSeconds = -1;
            ShowCountdown();
        }

        private void ShowCountdown()
        {
            if (labelText == null) return;
            float left = SecondsLeft;
            int seconds = Mathf.CeilToInt(left);
            bool warn = left <= warnSeconds;
            // Blinks twice a second near the end.
            labelText.color = warn ? (Mathf.Repeat(Time.time * 2f, 1f) < 0.6f ? WarnColor : LabelColor) : LabelColor;
            if (seconds == shownSeconds) return;
            shownSeconds = seconds;
            labelText.text = $"{seconds / 60}:{seconds % 60:00}";
        }

        // Time's up: out of the hand, a puff, and a few candies where it was.
        private void Expire()
        {
            var manager = grab.interactionManager;
            if (manager != null)
            {
                foreach (var interactor in new List<IXRSelectInteractor>(grab.interactorsSelecting))
                    manager.SelectExit(interactor, (IXRSelectInteractable)grab);
            }

            var middle = Middle();
            if (poofEffect != null)
            {
                var puff = Instantiate(poofEffect, middle, Quaternion.identity);
                puff.transform.localScale = Vector3.one * poofEffectSize;
                Destroy(puff, 3f);
            }
            if (poofSound != null) AudioSource.PlayClipAtPoint(poofSound, middle);
            DropCandies(middle);
            Destroy(gameObject);
        }

        private void DropCandies(Vector3 at)
        {
            if (candies.Length == 0) return;
            var global = FsmVariables.GlobalVariables.FindFsmGameObject(SpawnedStuffVariable);
            var parent = global != null && global.Value != null ? global.Value.transform : null;

            // Candies are eaten when they touch the player, so ones that appear in the hand go a little way out in
            // front, where they can be seen (and picked up), then pop away from the player.
            // Away from the head and from the body (the rig, which in room-scale isn't always under the head).
            var away = Vector3.zero;
            if (head == null && Camera.main != null) head = Camera.main.transform;
            var rig = FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
            foreach (var body in new[] { head, rig != null ? rig.transform : null })
            {
                if (body == null) continue;
                var flat = Vector3.ProjectOnPlane(at - body.position, Vector3.up);
                if (flat.sqrMagnitude < 0.0001f) flat = head != null ? Vector3.ProjectOnPlane(head.forward, Vector3.up) : Vector3.forward;
                if (away == Vector3.zero) away = flat.normalized;
                if (flat.magnitude < CandyDistance) at += flat.normalized * (CandyDistance - flat.magnitude);
            }

            int count = Random.Range(Mathf.Min(candyCount.x, candyCount.y), Mathf.Max(candyCount.x, candyCount.y) + 1);
            float turn = Random.Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                var prefab = candies[Random.Range(0, candies.Length)];
                if (prefab == null) continue;
                // On a ring, apart: candies that appear inside each other fly apart hard enough to crack.
                var spot = at + Quaternion.Euler(0f, turn + i * 360f / count, 0f) * Vector3.forward * CandySpacing;
                var candy = Instantiate(prefab, spot, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), parent);
                // A little pop up and out, so they don't land in one pile.
                if (candy.TryGetComponent(out Rigidbody body) && !body.isKinematic)
                {
                    var sideways = Random.insideUnitCircle * 0.8f;
                    body.linearVelocity = new Vector3(sideways.x, Random.Range(1.5f, 2.5f), sideways.y) + away * 1.2f;
                }
            }
        }

        private Vector3 Middle()
        {
            return TrySolidBounds(out var bounds) ? bounds.center : transform.position;
        }

        // Its solid colliders together (not its pick-up trigger).
        private bool TrySolidBounds(out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (var c in GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger || !c.enabled) continue;
                if (any) bounds.Encapsulate(c.bounds);
                else bounds = c.bounds;
                any = true;
            }
            return any;
        }

        // Held, and seen from the head, the attack comes from behind it (within its size plus the margin).
        private bool Covers(Vector3 headPosition, Vector3 from)
        {
            if (!isActiveAndEnabled || !grab.isSelected || !TrySolidBounds(out var bounds)) return false;

            var toShield = bounds.center - headPosition;
            float distance = toShield.magnitude;
            var toAttack = from - headPosition;
            // Not if the attack comes from between the head and the shield.
            if (distance < 0.05f || toAttack.magnitude + 0.3f < distance) return false;

            float halfAngle = Mathf.Atan2(bounds.extents.magnitude * 0.8f, distance) * Mathf.Rad2Deg + blockMargin;
            return Vector3.Angle(toShield, toAttack) <= halfAngle;
        }

        private void Blocked(Vector3 from)
        {
            var point = TrySolidBounds(out var bounds) ? bounds.ClosestPoint(from) : transform.position;
            if (blockSound != null) AudioSource.PlayClipAtPoint(blockSound, point);
            if (blockEffect != null)
            {
                var effect = Instantiate(blockEffect, point, Quaternion.identity);
                effect.transform.localScale = Vector3.one * blockEffectSize;
                Destroy(effect, 2f);
            }
            foreach (var interactor in grab.interactorsSelecting)
            {
                if (interactor is XRBaseInputInteractor hand) hand.SendHapticImpulse(0.8f, 0.15f);
            }
        }
    }
}
