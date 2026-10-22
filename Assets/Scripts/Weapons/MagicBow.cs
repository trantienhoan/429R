using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game.Weapons
{
    /// <summary>
    /// A magical bow with endless arrows of light. Hold the bow in one hand; with the other, grab the glowing string
    /// (grip) and pull it back: a light arrow appears on the string, brighter the further it's drawn. Aim along the arrow
    /// and let go of the string: the arrow flies, faster (and so harder-hitting) the further it was drawn. A pull shorter
    /// than Min Draw just lets the string go. The arrow points from the hand on the string through the bow, like a real
    /// arrow, so where it points is where it goes.
    /// Only the other hand can take the string, only from close by (String Reach), and only while the bow is held; that
    /// hand can't take the bow away from the first one. Let go of the bow and the string goes slack.
    /// Goes on the bow's root, next to its XR Grab Interactable; the string is a child XR Simple Interactable. Tools >
    /// 429 Game > Weapons > Build Magic Bow makes the whole prefab.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class MagicBow : MonoBehaviour, IXRSelectFilter
    {
        [Header("Parts")]
        [Tooltip("What the drawing hand grabs: an XR Simple Interactable with a small solid collider on the string's middle.")]
        [SerializeField] private XRSimpleInteractable stringGrab;
        [Tooltip("The string's two ends, at the limb tips.")]
        [SerializeField] private Transform stringTop;
        [SerializeField] private Transform stringBottom;
        [Tooltip("Where the arrow sits on the string when it isn't drawn (the middle of the slack string).")]
        [SerializeField] private Transform nockRest;
        [Tooltip("Where the arrow passes the bow, just above the hand. The arrow points from the string through here.")]
        [SerializeField] private Transform arrowRest;
        [Tooltip("Draws the string: three points, top - nock - bottom.")]
        [SerializeField] private LineRenderer stringLine;
        [Tooltip("The arrow shown on the string while drawing (looks only). Its tail is its pivot, pointing along its Z.")]
        [SerializeField] private Transform nockedArrow;
        [Tooltip("Sparkles at the nocked arrow's tip that show at full draw.")]
        [SerializeField] private ParticleSystem fullDrawSparkles;

        [Header("Draw")]
        [Tooltip("How far back the string pulls, in metres past where it rests.")]
        [Min(0.05f)]
        [SerializeField] private float maxDraw = 0.55f;
        [Tooltip("Shorter pulls than this share of Max Draw don't shoot: the string just goes back.")]
        [Range(0f, 1f)]
        [SerializeField] private float minDraw = 0.15f;
        [Tooltip("The drawing hand can be at most this many degrees off the bow's back for the string to follow it.")]
        [Range(0f, 90f)]
        [SerializeField] private float maxAimAngle = 60f;
        [Tooltip("The other hand takes the string only when it's this close to it, in metres.")]
        [Min(0.02f)]
        [SerializeField] private float stringReach = 0.22f;

        [Header("Shoot")]
        [SerializeField] private LightArrow arrowPrefab;
        [Tooltip("Arrow speed in metres per second: x at Min Draw, y at full draw. Faster arrows hit harder.")]
        [SerializeField] private Vector2 arrowSpeed = new(14f, 48f);
        [Tooltip("At least this many seconds between two shots.")]
        [Min(0f)]
        [SerializeField] private float shotInterval = 0.15f;

        [Header("Looks")]
        [Tooltip("The nocked arrow's size: x when the string is just taken, y at full draw.")]
        [SerializeField] private Vector2 arrowGrow = new(0.55f, 1f);
        [Tooltip("How long the string shakes after it's let go, in seconds, and how far, in metres.")]
        [Min(0.01f)]
        [SerializeField] private float twangSeconds = 0.3f;
        [Min(0f)]
        [SerializeField] private float twangSize = 0.025f;

        [Header("Sounds")]
        [Tooltip("Loops while the string is held, louder and higher the further it's drawn.")]
        [SerializeField] private AudioSource drawSound;
        [SerializeField] private AudioSource shootSound;
        [SerializeField] private AudioClip[] shootClips = System.Array.Empty<AudioClip>();

        private XRGrabInteractable grab;
        private IXRSelectInteractor drawHand;
        // How far the string is drawn (metres) and where the arrow points, as of the last frame.
        private float draw;
        private Vector3 aim;
        private Vector3 nock;
        private float nockedSince;
        private float lastShot = -10f;
        private float twangStart = -10f;
        private float twangAmount;

        /// <summary>How far the string is drawn now, 0 (slack) to 1 (full draw).</summary>
        public float Draw01 => drawHand != null ? draw / maxDraw : 0f;

        /// <summary>The hand holding the bow (not the string), or none.</summary>
        public XRBaseInputInteractor BowHand
        {
            get
            {
                foreach (var interactor in grab.interactorsSelecting)
                    if (interactor is XRBaseInputInteractor hand) return hand;
                return null;
            }
        }

        public bool canProcess => isActiveAndEnabled;

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            grab.selectFilters.Add(this);
            if (stringGrab != null) stringGrab.selectFilters.Add(this);
            ShowArrow(false);
        }

        private void OnEnable()
        {
            grab.selectExited.AddListener(OnBowLetGo);
            if (stringGrab == null) return;
            stringGrab.selectEntered.AddListener(OnStringTaken);
            stringGrab.selectExited.AddListener(OnStringLetGo);
        }

        private void OnDisable()
        {
            grab.selectExited.RemoveListener(OnBowLetGo);
            if (stringGrab != null)
            {
                stringGrab.selectEntered.RemoveListener(OnStringTaken);
                stringGrab.selectExited.RemoveListener(OnStringLetGo);
            }
            drawHand = null;
            ShowArrow(false);
            if (drawSound != null) drawSound.Stop();
        }

        // The bow: only one hand at a time (the drawing hand can't take it over). The string: only another hand than
        // the one holding the bow, close to it, while the bow is held.
        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            if (ReferenceEquals(interactable, grab))
                return !grab.isSelected || HeldBy(grab, interactor);

            if (stringGrab == null || !ReferenceEquals(interactable, stringGrab)) return true;
            if (!grab.isSelected || HeldBy(grab, interactor)) return false;
            // Holding on is always fine; taking it needs the hand right at the string.
            if (HeldBy(stringGrab, interactor)) return true;
            return (interactor.transform.position - nockRest.position).sqrMagnitude <= stringReach * stringReach;
        }

        private static bool HeldBy(IXRSelectInteractable thing, IXRSelectInteractor hand)
        {
            var hands = thing.interactorsSelecting;
            for (int i = 0; i < hands.Count; i++)
                if (ReferenceEquals(hands[i], hand)) return true;
            return false;
        }

        private void OnStringTaken(SelectEnterEventArgs args)
        {
            drawHand = args.interactorObject;
            nockedSince = Time.time;
            draw = 0f;
            ShowArrow(true);
            if (drawSound != null)
            {
                drawSound.volume = 0f;
                drawSound.Play();
            }
        }

        private void OnStringLetGo(SelectExitEventArgs args)
        {
            if (!ReferenceEquals(args.interactorObject, drawHand)) return;
            float drawn = draw / maxDraw;
            drawHand = null;
            ShowArrow(false);
            if (drawSound != null) drawSound.Stop();
            // The string snaps back and shakes, more the further it was drawn.
            twangStart = Time.time;
            twangAmount = drawn;
            // Let go because the bow was let go (or the hand slipped off): no shot.
            if (args.isCanceled || !grab.isSelected) return;
            if (drawn >= minDraw && Time.time - lastShot >= shotInterval) Shoot(drawn);
        }

        // The bow was let go: the string can't stay drawn.
        private void OnBowLetGo(SelectExitEventArgs args)
        {
            if (grab.isSelected || stringGrab == null || !stringGrab.isSelected) return;
            var manager = stringGrab.interactionManager;
            if (manager == null) return;
            for (int i = stringGrab.interactorsSelecting.Count - 1; i >= 0; i--)
                manager.SelectCancel(stringGrab.interactorsSelecting[i], stringGrab);
        }

        private void Shoot(float drawn)
        {
            lastShot = Time.time;
            if (arrowPrefab != null)
            {
                float speed = Mathf.Lerp(arrowSpeed.x, arrowSpeed.y, Mathf.InverseLerp(minDraw, 1f, drawn));
                LightArrow.Shoot(arrowPrefab, nock, aim, speed, this, BowHand);
            }
            if (shootSound != null && shootClips.Length > 0)
            {
                shootSound.pitch = Random.Range(0.94f, 1.06f);
                shootSound.PlayOneShot(shootClips[Random.Range(0, shootClips.Length)], Mathf.Lerp(0.55f, 1f, drawn));
            }
        }

        // After the hands (and the bow with them) have moved this frame.
        private void LateUpdate()
        {
            Vector3 rest = arrowRest.position;
            float restLength = Vector3.Distance(nockRest.position, rest);
            var back = -transform.forward;

            if (drawHand != null)
            {
                var pull = drawHand.transform.position - rest;
                float length = pull.magnitude;
                // The hand's direction from the arrow rest, kept within Max Aim Angle of the bow's back.
                var direction = length > 0.001f
                    ? Vector3.RotateTowards(back, pull / length, maxAimAngle * Mathf.Deg2Rad, 0f)
                    : back;
                draw = Mathf.Clamp(length - restLength, 0f, maxDraw);
                nock = rest + direction * (restLength + draw);
                aim = -direction;
            }
            else
            {
                draw = 0f;
                aim = transform.forward;
                // The slack string shakes for a moment after a shot.
                float t = (Time.time - twangStart) / twangSeconds;
                float shake = t < 1f ? Mathf.Sin(t * 28f) * (1f - t) * (1f - t) * twangSize * twangAmount : 0f;
                nock = nockRest.position + transform.forward * shake;
            }

            if (stringLine != null)
            {
                stringLine.SetPosition(0, stringTop.position);
                stringLine.SetPosition(1, nock);
                stringLine.SetPosition(2, stringBottom.position);
            }

            if (drawHand == null) return;
            float drawn = draw / maxDraw;
            if (nockedArrow != null)
            {
                nockedArrow.SetPositionAndRotation(nock, Quaternion.LookRotation(aim, transform.up));
                // It pops in over a tenth of a second, then grows with the draw.
                float pop = Mathf.Clamp01((Time.time - nockedSince) / 0.1f);
                nockedArrow.localScale = Vector3.one * (Mathf.Lerp(arrowGrow.x, arrowGrow.y, drawn) * pop);
            }
            if (fullDrawSparkles != null)
            {
                var emission = fullDrawSparkles.emission;
                emission.enabled = drawn >= 0.95f;
            }
            if (drawSound != null)
            {
                drawSound.volume = Mathf.Lerp(0.15f, 0.8f, drawn);
                drawSound.pitch = Mathf.Lerp(0.8f, 1.35f, drawn);
            }
        }

        private void ShowArrow(bool show)
        {
            if (nockedArrow != null) nockedArrow.gameObject.SetActive(show);
            if (fullDrawSparkles == null) return;
            var emission = fullDrawSparkles.emission;
            emission.enabled = false;
        }
    }
}
