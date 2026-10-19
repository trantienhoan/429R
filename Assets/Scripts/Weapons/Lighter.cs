using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.Weapons
{
    /// <summary>
    /// A candle that lights boongs (Candle_Fat, Candle_Land, Candle_Land_2, Pumpkin_Candle). Its flame lights a boong's
    /// bowl (BoongSmoke finds it with TryFindFlame), in the hand or standing where it is. Picked up, it goes out (Out When
    /// Grabbed, as Light Off When Grabbed did); it also goes out when it has lit something (UseUp) and when it's swung too
    /// fast (blown out). Shake it a few times in the hand (3 back-and-forth shakes within 2 s: Shakes To Relight flicks
    /// within Shake Window seconds) and it lights again, a spark at each flick; put down, it keeps burning. A candle whose
    /// flame is on in the prefab or the scene starts lit. Breaking and the fairy's shield stay its Health FSM's job. Goes
    /// on the candle's root, next to its XR Grab Interactable, in place of Light Off When Grabbed;
    /// Tools > 429 Game > Items > Make Candles Lighters sets up the four candles.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class Lighter : MonoBehaviour
    {
        [Header("Flame")]
        [Tooltip("The flame (CandleFlame): its particles and its light. On while it burns.")]
        [SerializeField] private GameObject flame;
        [Tooltip("How far the flame reaches around the Flame object (the wick), in metres at the candle's normal size: a " +
                 "boong's bowl this close to it catches fire.")]
        [Min(0.005f)]
        [SerializeField] private float flameRadius = 0.05f;

        [Header("Going out")]
        [Tooltip("Picking it up puts it out; shake it to light it again.")]
        [SerializeField] private bool outWhenGrabbed = true;
        [Tooltip("Swung faster than this (the flame's speed, in metres per second; walking and turning don't count), it's " +
                 "blown out. Bringing it to a bowl is about 1, a real swing 4-8. 0 = never.")]
        [Min(0f)]
        [SerializeField] private float blowOutSpeed = 4f;

        [Header("Shake to relight")]
        [Tooltip("Flicks it takes to light it again. A flick is one hard push of the hand one way, so a back-and-forth " +
                 "shake is 2: 6 = shake it 3 times.")]
        [Range(1, 10)]
        [SerializeField] private int shakesToRelight = 6;
        [Tooltip("...all within this many seconds.")]
        [Min(0.2f)]
        [SerializeField] private float shakeWindow = 2f;
        [Tooltip("How hard a flick must be to count, in metres per second squared: a real shake is 30-60, just moving " +
                 "the hand about stays under 10.")]
        [Min(1f)]
        [SerializeField] private float shakeStrength = 15f;

        [Header("Effects")]
        [Tooltip("Sparks at the wick (their own emission off; this emits them): one per shake, a burst when it lights.")]
        [SerializeField] private ParticleSystem sparks;
        [Min(0)]
        [SerializeField] private int relightSparks = 14;
        [Tooltip("Played when shaking lights it again.")]
        [SerializeField] private AudioClip relightSound;
        [Tooltip("Played when it's blown out (not when it's picked up or lights something).")]
        [SerializeField] private AudioClip outSound;
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.8f;

        // How quickly the shake and swing readings follow the hand, in seconds (short, so each flick shows on its own).
        private const float ShakeSmoothing = 0.03f;
        private const float SpeedSmoothing = 0.05f;
        // Two flicks the other way count as two shakes: their pushes point more than ~105 degrees apart.
        private const float FlipDot = -0.25f;
        // Right after it's picked up, lights or goes out, the hand's movement isn't judged (the grab settling, the swing
        // that blew it out).
        private const float SettleSeconds = 0.3f;
        // Let go and grabbed again this quickly: passed from one hand to the other, not picked up.
        private const float PassSeconds = 0.05f;

        private static readonly List<Lighter> burning = new();
        private static XROrigin origin;

        private XRGrabInteractable grab;
        private AudioSource speaker;
        private bool lit;
        private float settleAt = -10f;
        private float letGoAt = -10f;

        // The flame's speed in the play area's space (blow-out).
        private bool hasLastFlame;
        private Vector3 lastFlame;
        private float flameSpeed;
        // The hand holding it and where the flame is in that hand's space. The swing is measured from the hand: the hand
        // moves with the play area at once, but the candle itself only catches up at the next physics step, so a snap
        // turn or a teleport would look like a fast swing and blow it out.
        private Transform flameHand;
        private Vector3 flameInHand;

        // Shaking: the hand's last places (play area space), its smoothed push, and the last few shakes' times.
        private int handSamples;
        private Vector3 lastHand;
        private Vector3 lastHandVelocity;
        private Vector3 handPush;
        private bool shakeArmed = true;
        private Vector3 lastShakeDirection;
        private float[] shakeTimes;
        private int shakeIndex;
        private int shakesSeen;

        /// <summary>A candle whose flame is lit and touches the sphere at 'point' with 'radius' (world space).</summary>
        public static bool TryFindFlame(Vector3 point, float radius, out Lighter lighter)
        {
            lighter = null;
            float best = float.MaxValue;
            foreach (var candidate in burning)
            {
                if (candidate == null || !candidate.IsLit) continue;
                float reach = Mathf.Max(0f, radius) + candidate.FlameRadius;
                float distance = (candidate.FlamePoint - point).sqrMagnitude;
                if (distance > reach * reach || distance >= best) continue;
                best = distance;
                lighter = candidate;
            }
            return lighter != null;
        }

        /// <summary>Whether the flame is burning right now.</summary>
        public bool IsLit => lit && flame != null && flame.activeInHierarchy;

        /// <summary>The flame's wick, in world space.</summary>
        public Vector3 FlamePoint => flame != null ? flame.transform.position : transform.position;

        /// <summary>How far the flame reaches around FlamePoint, in metres (world).</summary>
        public float FlameRadius => flameRadius * Mathf.Abs(transform.lossyScale.x);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            burning.Clear();
            origin = null;
        }

        /// <summary>The flame was used to light something: it goes out (quietly) until it's shaken.</summary>
        public void UseUp()
        {
            GoOut(false);
        }

        /// <summary>Lights it again, as shaking does: sparks and its flame. Can be called from PlayMaker (Call Method).</summary>
        public void Relight()
        {
            ClearShakes();
            if (lit || flame == null) return;
            Sparks(relightSparks);
            Play(relightSound);
            SetLit(true);
        }

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            shakeTimes = new float[Mathf.Clamp(shakesToRelight, 1, 10)];
            lit = flame != null && flame.activeSelf;
            if (sparks != null)
            {
                var emission = sparks.emission;
                emission.enabled = false;
            }

            // Its own small speaker in the world, for the relight and the blow-out.
            speaker = gameObject.AddComponent<AudioSource>();
            speaker.playOnAwake = false;
            speaker.spatialBlend = 1f;
            speaker.minDistance = 0.4f;
            speaker.maxDistance = 10f;
            speaker.rolloffMode = AudioRolloffMode.Linear;
        }

        private void OnEnable()
        {
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnLetGo);
            if (lit && !burning.Contains(this)) burning.Add(this);
        }

        // Put away (its stage ends, it breaks): it keeps its flame for when it's back, but doesn't count meanwhile.
        private void OnDisable()
        {
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnLetGo);
            burning.Remove(this);
            handSamples = 0;
            flameHand = null;
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            handSamples = 0;
            settleAt = Time.time;
            // Passed straight from one hand to the other: it stays as it was.
            bool passed = Time.time - letGoAt < PassSeconds;
            if (lit && outWhenGrabbed && !passed) GoOut(false);
            else if (lit) HoldFlame();
        }

        // Put down (or thrown): it keeps burning.
        private void OnLetGo(SelectExitEventArgs args)
        {
            // Passed to the other hand: still held.
            if (grab.isSelected) return;
            letGoAt = Time.time;
            handSamples = 0;
            flameHand = null;
        }

        private void Update()
        {
            if (!grab.isSelected) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            var space = PlayArea();
            if (lit) WatchFlame(dt, space);
            else WatchShaking(dt, space);
        }

        // Burning in the hand: it goes out when it's swung too fast.
        private void WatchFlame(float dt, Transform space)
        {
            if (blowOutSpeed <= 0f) return;

            // Settling (the candle may still be on its way to the hand): where the flame sits in the hand is taken
            // again each frame, and the swing isn't measured yet.
            if (Time.time - settleAt < SettleSeconds)
            {
                if (flameHand != null) flameInHand = flameHand.InverseTransformPoint(FlamePoint);
                hasLastFlame = false;
                return;
            }

            // The flame as carried by the hand, in the play area's space (see flameHand).
            var flamePoint = flameHand != null ? flameHand.TransformPoint(flameInHand) : FlamePoint;
            var point = space != null ? space.InverseTransformPoint(flamePoint) : flamePoint;
            if (hasLastFlame)
            {
                float speed = (point - lastFlame).magnitude / dt;
                flameSpeed = Mathf.Lerp(flameSpeed, speed, 1f - Mathf.Exp(-dt / SpeedSmoothing));
                if (flameSpeed > blowOutSpeed)
                {
                    GoOut(true);
                    return;
                }
            }
            lastFlame = point;
            hasLastFlame = true;
        }

        // Out and held: counts shakes. A shake is a strong push of the hand (its change of speed, smoothed a little),
        // counted when it rises above Shake Strength after dropping below half of it, or when it pushes the other way
        // than the last shake (a back-and-forth shake never calms down in between). Enough shakes soon enough light it.
        private void WatchShaking(float dt, Transform space)
        {
            var hand = grab.firstInteractorSelecting as Component;
            if (hand == null || flame == null) return;
            var position = space != null ? space.InverseTransformPoint(hand.transform.position) : hand.transform.position;
            if (handSamples < 2)
            {
                // The first two frames only give a place and then a speed.
                lastHandVelocity = handSamples == 1 ? (position - lastHand) / dt : Vector3.zero;
                lastHand = position;
                handPush = Vector3.zero;
                handSamples++;
                return;
            }

            var velocity = (position - lastHand) / dt;
            var push = (velocity - lastHandVelocity) / dt;
            lastHand = position;
            lastHandVelocity = velocity;
            handPush = Vector3.Lerp(handPush, push, 1f - Mathf.Exp(-dt / ShakeSmoothing));
            if (Time.time - settleAt < SettleSeconds) return;

            float strength = handPush.magnitude;
            if (strength < shakeStrength * 0.5f) shakeArmed = true;
            if (strength < shakeStrength) return;
            var direction = handPush / strength;
            bool otherWay = lastShakeDirection != Vector3.zero && Vector3.Dot(direction, lastShakeDirection) < FlipDot;
            if (!shakeArmed && !otherWay) return;

            shakeArmed = false;
            lastShakeDirection = direction;
            Shook();
        }

        private void Shook()
        {
            Sparks(1);
            shakeTimes[shakeIndex] = Time.time;
            shakeIndex = (shakeIndex + 1) % shakeTimes.Length;
            if (shakesSeen < shakeTimes.Length) shakesSeen++;
            // shakeTimes[shakeIndex] is now the oldest of the last few shakes.
            if (shakesSeen >= shakeTimes.Length && Time.time - shakeTimes[shakeIndex] <= shakeWindow) Relight();
        }

        private void ClearShakes()
        {
            shakesSeen = 0;
            shakeIndex = 0;
            shakeArmed = true;
            lastShakeDirection = Vector3.zero;
        }

        // Out until it's shaken: picked up or it lit something (quietly), or blown out (a puff of sound).
        private void GoOut(bool withSound)
        {
            if (!lit) return;
            settleAt = Time.time;
            ClearShakes();
            handSamples = 0;
            SetLit(false);
            if (withSound) Play(outSound);
        }

        private void SetLit(bool on)
        {
            if (flame == null) on = false;
            if (on == lit) return;
            lit = on;
            if (flame != null) flame.SetActive(on);
            if (on)
            {
                if (grab.isSelected) HoldFlame();
                if (isActiveAndEnabled && !burning.Contains(this)) burning.Add(this);
            }
            else burning.Remove(this);
        }

        // Lit in the hand: the swing is measured from this hand, after a moment to settle.
        private void HoldFlame()
        {
            var hand = grab.firstInteractorSelecting as Component;
            flameHand = hand != null ? hand.transform : null;
            if (flameHand != null) flameInHand = flameHand.InverseTransformPoint(FlamePoint);
            settleAt = Time.time;
            hasLastFlame = false;
            flameSpeed = 0f;
        }

        private void Sparks(int count)
        {
            if (sparks == null || count <= 0) return;
            if (!sparks.isPlaying) sparks.Play();
            sparks.Emit(count);
        }

        private void Play(AudioClip clip)
        {
            if (clip != null && speaker != null) speaker.PlayOneShot(clip, volume);
        }

        // The play area (the XR Origin's camera floor offset), so walking and turning don't count as moving the hand.
        private static Transform PlayArea()
        {
            if (origin == null) origin = FindAnyObjectByType<XROrigin>();
            return origin != null && origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.transform : null;
        }
    }
}
