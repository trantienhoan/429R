using System;
using Game.Leveling;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using FsmFloat = HutongGames.PlayMaker.FsmFloat;

namespace Game.Weapons
{
    /// <summary>
    /// Smoking a boong. Hold it the right way up (its bowl facing up: Up Axis within Max Tilt of straight up), with its
    /// mouthpiece (Inhale) at your mouth and a lit candle's flame at its bowl (Burn), all at the same moment: the candle
    /// goes out and the boong catches fire. It plays its Fire animation slowly (the hammer boongs grow their head), or swells and shakes
    /// (Elon, Bus), keeps its flame on, bubbles and puffs smoke for Fire Seconds, then explodes: a bang that hurts bugs
    /// close by (never the player), and the player gets the boong's bonus (Buff) for Buff Seconds. The boong is let go
    /// and breaks into its pieces through its Health FSM (Break Apart), or just vanishes. Once lit, it burns on even if
    /// it's moved away or dropped; the trigger can't stop it. Almost there, it hints without words: held the right way
    /// up at the mouth with no fire, it bubbles dry now and then; the right way up with fire at the bowl but not at the
    /// mouth, a tiny wisp of smoke rises. Inhale and Burn are child points (moving with the bone
    /// that carries that part) with a reach each, drawn as spheres in the Scene view: cyan the mouthpiece, orange the
    /// bowl (green while that part is right, in Play mode). They are only points, not colliders: nothing else touches
    /// them. Goes on the boong's root, next to its XR Grab Interactable; Tools > 429 Game > Weapons > Set Up Boong
    /// Smoking adds it to the five boongs.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class BoongSmoke : MonoBehaviour
    {
        [Header("Smoking")]
        [Tooltip("The bonus the player gets when it explodes.")]
        [SerializeField] private Buff buff = Buff.GreenBoong;
        [Tooltip("How long the bonus lasts, in seconds.")]
        [Min(1f)]
        [SerializeField] private float buffSeconds = PlayerBuffs.DefaultSeconds;
        [Tooltip("The mouthpiece opening: it has to touch the player's mouth (the Mouth ball under the Main Camera).")]
        [SerializeField] private Transform inhale;
        [Tooltip("How far around Inhale counts as the mouthpiece, in metres at the boong's normal size.")]
        [Min(0.005f)]
        [SerializeField] private float inhaleRadius = 0.05f;
        [Tooltip("The bowl, where the candle's flame has to be.")]
        [SerializeField] private Transform burn;
        [Tooltip("How far around Burn counts as the bowl, in metres at the boong's normal size (it grows with the bowl). " +
                 "The candle's flame reaches a little further on its own.")]
        [Min(0.005f)]
        [SerializeField] private float burnRadius = 0.04f;
        [Tooltip("Which way the bowl's opening faces, in the boong's own space: held the right way, the bowl faces up. " +
                 "The hammer boongs (0, 0.75, 0.67), the bone boong (0, 0.93, 0.37), Elon (0.33, 0.94, 0), the bus -Z.")]
        [SerializeField] private Vector3 upAxis = Vector3.up;
        [Tooltip("How far the bowl may lean from facing straight up and still count as held the right way, in degrees.")]
        [Range(0f, 180f)]
        [SerializeField] private float maxTilt = 75f;

        [Header("Fire")]
        [Tooltip("Seconds from catching fire to exploding.")]
        [Min(0.1f)]
        [SerializeField] private float fireSeconds = 2.5f;
        [Tooltip("Its Animator, if it has a fire animation (the hammer boongs, the bone boong). Empty: no animation.")]
        [SerializeField] private Animator animator;
        [Tooltip("The Animator's fire state: played slowly, from start to end, over Fire Seconds.")]
        [SerializeField] private string fireState = "Fire";
        [Tooltip("Its flame (CandleFlame): on from catching fire to exploding, whatever the trigger does.")]
        [SerializeField] private GameObject flame;
        [Tooltip("Shakes harder and harder while it burns, e.g. its model or armature. Empty: no shaking.")]
        [SerializeField] private Transform shakePart;
        [Tooltip("How far it shakes at the end, in metres.")]
        [Min(0f)]
        [SerializeField] private float shake = 0.005f;
        [Tooltip("Swells up while it burns, for boongs with no fire animation (Elon, Bus): their model. Empty: nothing swells.")]
        [SerializeField] private Transform swellPart;
        [Tooltip("How much bigger Swell Part gets by the end: 0.25 = a quarter bigger.")]
        [Min(0f)]
        [SerializeField] private float swell = 0.25f;
        [Tooltip("How much Swell Part throbs on top of that by the end, as a share of its size.")]
        [Min(0f)]
        [SerializeField] private float throb = 0.05f;
        [Tooltip("Puffed out of the bowl a few times a second while it burns, e.g. smoke.")]
        [SerializeField] private GameObject smoke;
        [Tooltip("Size of each puff at the start (x) and the end (y). The Hyper Casual FX smoke is about 13 m wide, so 0.01 " +
                 "makes it about 13 cm.")]
        [SerializeField] private Vector2 smokeSize = new(0.008f, 0.018f);
        [Tooltip("Played once when it catches fire.")]
        [SerializeField] private AudioClip igniteSound;
        [Tooltip("Played over and over while it burns (higher and higher), e.g. bubbling.")]
        [SerializeField] private AudioClip bubbleSound;
        [Range(0f, 1f)]
        [SerializeField] private float bubbleVolume = 0.9f;

        [Header("Almost there")]
        [Tooltip("Held the right way up at the mouth but with no flame at the bowl: a short, low, dry bubble this often, in " +
                 "seconds (it needs fire). 0 = never.")]
        [Min(0f)]
        [SerializeField] private float dryPullEvery = 1.5f;
        [Tooltip("Held the right way up with a flame at the bowl but not at the mouth: a tiny wisp of smoke from the bowl " +
                 "this often, in seconds (it's catching: now to the mouth). 0 = never. Upside down, nothing gives a hint.")]
        [Min(0f)]
        [SerializeField] private float wispEvery = 0.6f;
        [Tooltip("How loud the dry bubble is.")]
        [Range(0f, 1f)]
        [SerializeField] private float cueVolume = 0.25f;

        [Header("Explosion")]
        [Tooltip("How far the blast reaches, in metres. It never hurts the player.")]
        [Min(0.1f)]
        [SerializeField] private float radius = 1f;
        [Tooltip("Health everything else in the blast loses (bugs close by; a flip-flop grenade takes 3).")]
        [Min(0f)]
        [SerializeField] private float damage = 2f;
        [Tooltip("How fast loose objects in the middle of the blast are pushed away, in metres per second. 0 = no push.")]
        [Min(0f)]
        [SerializeField] private float push = 3f;
        [Tooltip("On: it breaks the way it normally breaks (its Health FSM drops its pieces). Off: it just vanishes " +
                 "(Elon: its own 'pieces' are a bullet).")]
        [SerializeField] private bool breakApart = true;
        [Tooltip("Particle effects that appear where it explodes.")]
        [SerializeField] private GameObject[] effects = Array.Empty<GameObject>();
        [Tooltip("Size of the effects: 1 = a grenade's.")]
        [Min(0.05f)]
        [SerializeField] private float effectSize = 0.6f;
        [SerializeField] private AudioClip sound;
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.9f;

        private const string MouthTag = "Mouth";
        // Where the mouth is when there's no Mouth ball: a little below and in front of the eyes (as the Mouth ball).
        private static readonly Vector3 MouthOffset = new(0f, -0.09f, 0.07f);
        private const float MouthRadius = 0.09f;
        private const float MouthSearchEvery = 1f;
        private const string FireTrigger = "Fire";
        private const string ReleaseTrigger = "Release";
        private const float PuffEvery = 0.35f;
        private const float ThrobPerSecond = 7f;
        // The dry bubble: this low, this long; the wisp: this share of the first puff's size.
        private const float DryPullPitch = 0.55f;
        private const float DryPullSeconds = 0.35f;
        private const float WispShare = 0.5f;

        private static Transform mouth;
        private static SphereCollider mouthBall;
        private static float nextMouthSearch;

        private XRGrabInteractable grab;
        private Rigidbody body;
        private FsmFloat health;
        private AudioSource bubbles;
        private bool burning;
        private bool exploded;
        private float burned;
        private float nextPuff;
        // The 'almost there' hints: when the next may come, and when the dry bubble playing now stops (0 = none playing).
        private float nextDryPull;
        private float nextWisp;
        private float dryPullUntil;

        // The fire animation, driven by hand (Animator speed 0) so the trigger can't change it.
        private bool driving;
        private int fireHash;
        private float animStart;
        private float animSpeed = 1f;
        private bool hasFireTrigger;
        private bool hasReleaseTrigger;

        private Vector3 shakeOffset;
        private Vector3 swellBase;

        // The grab's Predicted Visuals Transform when it's the model that swells or shakes (the bus): while it's held, XRI
        // poses and sizes it every frame, which would undo both, so it's switched off while it burns. Its pose at rest.
        private Transform visuals;
        private Vector3 visualsPosition;
        private Quaternion visualsRotation;
        private Vector3 visualsScale;
        // The Rigidbody's smoothing (interpolation) before the grab, and whether it's switched on here while the fire has
        // the model (see SmoothModel).
        private RigidbodyInterpolation restInterpolation;
        private bool smoothing;

        // What was right last time it was checked (shown by the gizmos).
        private bool uprightNow;
        private bool inhaleNow;
        private bool burnNow;

        /// <summary>Whether it's on fire (lit, not exploded yet).</summary>
        public bool IsBurning => burning;

        /// <summary>How far the fire has got: 0 not lit, 1 the moment it explodes.</summary>
        public float FireProgress => exploded ? 1f : burning ? Mathf.Clamp01(burned / fireSeconds) : 0f;

        /// <summary>The bonus it gives.</summary>
        public Buff Gives => buff;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            mouth = null;
            mouthBall = null;
            nextMouthSearch = 0f;
        }

        /// <summary>
        /// Sets it on fire now, without a candle (for tests, or from PlayMaker with Call Method): it burns for Fire
        /// Seconds, then explodes and gives its bonus.
        /// </summary>
        [ContextMenu("Light It Now")]
        public void Ignite()
        {
            CatchFire(null);
        }

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
            var predicted = grab.predictedVisualsTransform;
            if (predicted != null && (Moves(swellPart, predicted) || Moves(shakePart, predicted)))
            {
                visuals = predicted;
                visuals.GetLocalPositionAndRotation(out visualsPosition, out visualsRotation);
                visualsScale = visuals.localScale;
                if (body != null) restInterpolation = body.interpolation;
            }
            // Ready at load, so the first explosion doesn't stutter.
            Blast.Warm(effects);
            EffectPool.Warm(smoke, 3);
        }

        private void OnDisable()
        {
            if (burning) PutOut();
            StopDryPull();
            nextDryPull = nextWisp = 0f;
        }

        private void Update()
        {
            SmoothModel();
            if (dryPullUntil > 0f && Time.time >= dryPullUntil) StopDryPull();
            if (exploded) return;
            StopShaking();
            if (burning) Burn();
            else if (grab.isSelected) Watch();
            else GiveVisualsBack();
        }

        // After the Animator: the shaking and swelling go on top of its pose; and anything the trigger asked for this
        // frame is dropped again.
        private void LateUpdate()
        {
            if (!burning) return;
            DropTriggers();
            float progress = FireProgress;

            if (swellPart != null)
            {
                float size = 1f + swell * progress * progress + throb * progress * Mathf.Sin(burned * ThrobPerSecond * 2f * Mathf.PI);
                swellPart.localScale = swellBase * size;
            }

            if (shakePart != null && shake > 0f)
            {
                var parent = shakePart.parent;
                var offset = UnityEngine.Random.insideUnitSphere * (shake * progress);
                shakeOffset = parent != null ? parent.InverseTransformVector(offset) : offset;
                shakePart.localPosition += shakeOffset;
            }
        }

        // Held: the right way up, mouthpiece at the mouth, a lit flame at the bowl, all at once, sets it on fire.
        private void Watch()
        {
            var up = transform.TransformDirection(upAxis);
            uprightNow = up.sqrMagnitude < 1e-6f || Vector3.Dot(up.normalized, Vector3.up) >= Mathf.Cos(maxTilt * Mathf.Deg2Rad);
            inhaleNow = inhale != null && AtMouth(inhale.position, Reach(inhale, inhaleRadius));
            Lighter lighter = null;
            burnNow = burn != null && Lighter.TryFindFlame(burn.position, Reach(burn, burnRadius), out lighter);
            if (uprightNow && inhaleNow && burnNow) CatchFire(lighter);
            else if (uprightNow && inhaleNow) DryPull();
            else if (uprightNow && burnNow) Wisp();
        }

        // Almost there (at the mouth, no fire): a short, low, quiet bubble now and then, as if pulling on it dry.
        private void DryPull()
        {
            if (dryPullEvery <= 0f || cueVolume <= 0f || bubbleSound == null || Time.time < nextDryPull) return;
            nextDryPull = Time.time + dryPullEvery;
            var source = Bubbles();
            source.clip = bubbleSound;
            source.volume = cueVolume;
            source.pitch = DryPullPitch;
            source.Play();
            dryPullUntil = Time.time + DryPullSeconds;
        }

        // Almost there (fire at the bowl, not at the mouth yet): a tiny wisp of smoke from the bowl now and then.
        private void Wisp()
        {
            if (wispEvery <= 0f || smoke == null || Time.time < nextWisp) return;
            nextWisp = Time.time + wispEvery;
            float prefabScale = Mathf.Max(smoke.transform.localScale.x, 0.0001f);
            EffectPool.Play(smoke, burn.position, smokeSize.x * WispShare / prefabScale);
        }

        private void StopDryPull()
        {
            if (dryPullUntil <= 0f) return;
            dryPullUntil = 0f;
            // Lit meanwhile: the bubbling goes on.
            if (bubbles != null && !burning) bubbles.Stop();
        }

        // The bubbling sound, made the first time it's needed.
        private AudioSource Bubbles()
        {
            if (bubbles != null) return bubbles;
            var go = new GameObject("Bubbles");
            go.transform.SetParent(transform, false);
            bubbles = go.AddComponent<AudioSource>();
            bubbles.playOnAwake = false;
            bubbles.loop = true;
            // Heard from the boong, where it is.
            bubbles.spatialBlend = 1f;
            bubbles.minDistance = 0.5f;
            bubbles.maxDistance = 15f;
            return bubbles;
        }

        private void CatchFire(Lighter lighter)
        {
            if (burning || exploded || !isActiveAndEnabled) return;
            if (lighter != null) lighter.UseUp();
            burning = true;
            burned = 0f;
            nextPuff = 0f;
            if (health == null && Blast.HasHealth(gameObject, out var ownHealth, out _)) health = ownHealth;

            StartAnimation();
            if (flame != null) flame.SetActive(true);
            TakeVisuals();
            if (swellPart != null) swellBase = swellPart.localScale;

            var at = burn != null ? burn.position : transform.position;
            if (igniteSound != null) AudioSource.PlayClipAtPoint(igniteSound, at, bubbleVolume);
            dryPullUntil = 0f;
            if (bubbleSound != null)
            {
                Bubbles();
                bubbles.clip = bubbleSound;
                bubbles.volume = bubbleVolume;
                bubbles.pitch = 0.85f;
                bubbles.Play();
            }
        }

        private void Burn()
        {
            // Broken some other way in the meantime (a hard hit): no explosion.
            if (health != null && health.Value <= 0f)
            {
                PutOut();
                return;
            }

            burned += Time.deltaTime;
            float progress = FireProgress;
            if (driving)
            {
                DropTriggers();
                animator.Play(fireHash, 0, Mathf.Lerp(animStart, 1f, progress));
            }
            // The trigger's let-go switches the flame off on some boongs; it stays on.
            if (flame != null && !flame.activeSelf) flame.SetActive(true);
            if (bubbles != null) bubbles.pitch = Mathf.Lerp(0.85f, 1.35f, progress);
            if (smoke != null && burned >= nextPuff)
            {
                // Smoke Size is the puff's size; the pool scales the prefab's own size.
                float prefabScale = Mathf.Max(smoke.transform.localScale.x, 0.0001f);
                var at = burn != null ? burn.position : transform.position;
                EffectPool.Play(smoke, at, Mathf.Lerp(smokeSize.x, smokeSize.y, progress) / prefabScale);
                nextPuff = burned + PuffEvery;
            }

            if (progress >= 1f) Explode();
        }

        private void Explode()
        {
            exploded = true;
            burning = false;
            StopShaking();
            if (bubbles != null) bubbles.Stop();
            if (flame != null) flame.SetActive(false);

            // Out of the hand for good first (no grabbing it again while it breaks).
            if (grab.isSelected && grab.interactionManager != null)
                grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
            grab.enabled = false;

            var point = body != null ? body.worldCenterOfMass : transform.position;
            // The player's blast: overheated hands (Hot Hands) make it hurt more.
            Blast.Explode(point, radius, Game.Combat.HotHands.Boost(damage), push, body, effects, sound, volume, null, false, effectSize);
            PlayerBuffs.Give(buff, buffSeconds);

            // Breaking the usual way lets its own Health FSM drop the pieces and remove it; otherwise it just vanishes.
            // Weapon Wear is switched off first: it gives back part of a small loss (Weapon Efficiency), so a worn boong
            // would never reach 0 and stay whole.
            if (breakApart && health != null)
            {
                if (TryGetComponent(out WeaponWear wear)) wear.enabled = false;
                health.Value = 0f;
            }
            else Destroy(gameObject);
        }

        // Stopped before exploding (switched off, or broken by a hit): back to normal.
        private void PutOut()
        {
            burning = false;
            StopShaking();
            if (bubbles != null) bubbles.Stop();
            if (flame != null) flame.SetActive(false);
            if (swellPart != null) swellPart.localScale = swellBase;
            GiveVisualsBack();
            if (!driving) return;
            driving = false;
            animator.speed = animSpeed;
            if (hasReleaseTrigger) animator.SetTrigger(ReleaseTrigger);
        }

        // The fire state from where it is (already playing: from there, else from its start), stopped so only this
        // script moves it on.
        private void StartAnimation()
        {
            driving = false;
            if (animator == null || string.IsNullOrEmpty(fireState) || !animator.isActiveAndEnabled) return;
            fireHash = Animator.StringToHash(fireState);
            if (!animator.HasState(0, fireHash)) return;

            hasFireTrigger = hasReleaseTrigger = false;
            foreach (var parameter in animator.parameters)
            {
                if (parameter.type != AnimatorControllerParameterType.Trigger) continue;
                if (parameter.name == FireTrigger) hasFireTrigger = true;
                else if (parameter.name == ReleaseTrigger) hasReleaseTrigger = true;
            }

            var now = animator.GetCurrentAnimatorStateInfo(0);
            animStart = now.shortNameHash == fireHash ? Mathf.Clamp01(now.normalizedTime) : 0f;
            animSpeed = animator.speed;
            animator.speed = 0f;
            driving = true;
            DropTriggers();
            animator.Play(fireHash, 0, animStart);
        }

        // The trigger's Fire (would start the animation over) and Release (would end it) are ignored while it burns.
        private void DropTriggers()
        {
            if (!driving) return;
            if (hasFireTrigger) animator.ResetTrigger(FireTrigger);
            if (hasReleaseTrigger) animator.ResetTrigger(ReleaseTrigger);
        }

        // From now on the fire moves the model, not XRI: back to its pose at rest, under the Rigidbody.
        private void TakeVisuals()
        {
            if (visuals == null || grab.predictedVisualsTransform != visuals) return;
            grab.predictedVisualsTransform = null;
            visuals.SetLocalPositionAndRotation(visualsPosition, visualsRotation);
            visuals.localScale = visualsScale;
            SmoothModel();
        }

        // Put out: XRI may smooth the model again. Not while it's held: XRI would size it from the pose it took at the
        // grab, so it waits until it's let go (Update).
        private void GiveVisualsBack()
        {
            if (visuals == null || grab.predictedVisualsTransform != null || grab.isSelected) return;
            grab.predictedVisualsTransform = visuals;
        }

        // In the hand XRI smooths the model itself and switches the Rigidbody's own smoothing off; once the fire has the
        // model, nothing would smooth it and it would jump from one physics step to the next. So while it's held then,
        // the Rigidbody smooths it; let go (XRI leaves the setting be then), it's back to what it was before the grab.
        private void SmoothModel()
        {
            if (visuals == null || body == null) return;
            bool smooth = grab.isSelected && grab.predictedVisualsTransform == null;
            if (smooth == smoothing) return;
            smoothing = smooth;
            body.interpolation = smooth ? RigidbodyInterpolation.Interpolate : restInterpolation;
        }

        // Whether swelling or shaking 'part' would move 'model' (it is the model, or one of the model's parents).
        private static bool Moves(Transform part, Transform model)
        {
            return part != null && model.IsChildOf(part);
        }

        private void StopShaking()
        {
            if (shakeOffset == Vector3.zero) return;
            if (shakePart != null) shakePart.localPosition -= shakeOffset;
            shakeOffset = Vector3.zero;
        }

        // A reach in metres at the boong's normal size, grown or shrunk with the point (and the bone it's on).
        private static float Reach(Transform point, float reach)
        {
            return reach * Mathf.Abs(point.lossyScale.x);
        }

        // Whether a ball at 'point' touches the player's mouth: the Mouth ball under the Main Camera, or failing that,
        // a ball a little below and in front of the eyes.
        private static bool AtMouth(Vector3 point, float reach)
        {
            if (!Mouth(out var center, out float size)) return false;
            float touch = reach + size;
            return (point - center).sqrMagnitude <= touch * touch;
        }

        private static bool Mouth(out Vector3 center, out float size)
        {
            if (mouth == null && Time.unscaledTime >= nextMouthSearch)
            {
                nextMouthSearch = Time.unscaledTime + MouthSearchEvery;
                try
                {
                    var found = GameObject.FindWithTag(MouthTag);
                    if (found != null)
                    {
                        mouth = found.transform;
                        mouthBall = found.GetComponent<SphereCollider>();
                    }
                }
                catch (UnityException)
                {
                    // No Mouth tag in this project: the eyes it is.
                }
            }

            if (mouth != null && mouth.gameObject.activeInHierarchy)
            {
                if (mouthBall != null)
                {
                    var scale = mouth.lossyScale;
                    center = mouth.TransformPoint(mouthBall.center);
                    size = mouthBall.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                }
                else
                {
                    center = mouth.position;
                    size = MouthRadius;
                }
                return true;
            }

            var eyes = Camera.main;
            if (eyes == null)
            {
                center = Vector3.zero;
                size = 0f;
                return false;
            }
            center = eyes.transform.TransformPoint(MouthOffset);
            size = MouthRadius;
            return true;
        }

        private void OnDrawGizmos()
        {
            bool playing = Application.isPlaying && grab != null && grab.isSelected && !burning && !exploded;
            if (inhale != null)
            {
                Gizmos.color = playing && inhaleNow ? Color.green : Color.cyan;
                Gizmos.DrawWireSphere(inhale.position, Reach(inhale, inhaleRadius));
            }
            if (burn != null)
            {
                Gizmos.color = playing && burnNow ? Color.green : new Color(1f, 0.55f, 0.1f);
                Gizmos.DrawWireSphere(burn.position, Reach(burn, burnRadius));
            }
        }

        // The way up it should be held: a line from its middle.
        private void OnDrawGizmosSelected()
        {
            var up = transform.TransformDirection(upAxis);
            if (up.sqrMagnitude < 1e-6f) return;
            bool playing = Application.isPlaying && grab != null && grab.isSelected && !burning && !exploded;
            Gizmos.color = playing && uprightNow ? Color.green : Color.yellow;
            var from = transform.position;
            Gizmos.DrawLine(from, from + up.normalized * 0.3f);
        }
    }
}
