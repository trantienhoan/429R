using Game.Combat;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace Game.Menus
{
    /// <summary>
    /// A board with a tip on it, above the knight's weapon in the middle of the path at the start. The first time it
    /// shows after the game is launched it says the First Tip; every time after that (the knight is back after the
    /// player died, or the game started over after the boss) it says a tip picked at random from Tips, never the same
    /// one twice in a row.
    /// Every tip can have a hint (Hints, one for each tip; the First Tip's is First Tip Hint), in smaller letters under
    /// it. The hint stays hidden until the player touches the board or the knight; then it fades in. A hard hit from
    /// the player (a punch or a flying punch, a swung or thrown weapon, a shot, a slap of the hand) knocks the board
    /// with a wooden knock and shows the next tip, in order, with its hint. Things that only fall or slide into it, and
    /// enemies, don't count. Open hands have no colliders, so the hands are also checked against the board's face.
    /// Goes on the knight (Knight prefab). The board stays where it was when the knight appeared, even if the knight is
    /// knocked about, fades away once the player has walked past it (from then on touching it does nothing), and goes
    /// away with the knight. Tools > 429 Game > Menu > Set Up Knight Tip Board builds it, gives it its hit box and adds
    /// the tips.
    /// </summary>
    public class TipBoard : MonoBehaviour
    {
        [Tooltip("The board (a world-space canvas, under this object until the game starts).")]
        [SerializeField] private RectTransform board;
        [SerializeField] private TMP_Text text;
        [Tooltip("The board fades away once the player's head is this far past it (in metres, beyond its face), and stays " +
                 "off until the knight is back. Below 0: it never fades.")]
        [SerializeField] private float passedBy = 0.3f;
        [Tooltip("Seconds the board takes to fade away.")]
        [Min(0.01f)]
        [SerializeField] private float fadeSeconds = 0.5f;

        [Tooltip("Shown the first time, each time the game is launched.")]
        [TextArea(1, 3)]
        [SerializeField] private string firstTip = "High Recommended";
        [Tooltip("The First Tip's hint (how to fix the height: the pause menu's Height option). Shown under it, smaller, " +
                 "once the player touches the board. Empty: none.")]
        [TextArea(1, 3)]
        [SerializeField] private string firstTipHint = "Not this high? You can change your height in the menu.";
        [Tooltip("How big the hint's letters are, as a share of the tip's.")]
        [Range(0.3f, 1f)]
        [SerializeField] private float hintSize = 0.55f;
        [Tooltip("Extra space between the tip and its hint, in lines of the tip's size (0 = none, 1 = a whole empty line).")]
        [Range(0f, 2f)]
        [SerializeField] private float hintGap = 0.6f;
        [Tooltip("One is picked at random every time after the first. A hard hit on the board shows the next one.")]
        [TextArea(1, 3)]
        [SerializeField] private string[] tips =
        {
            "Eat as much candies as you want, this is a game",
            "Nothing to throw? Make a fist and start punching",
            "Clean your room and watch out for the bug bite",
            "DONT RUN\nDONT FEAR\nDONT GIVE UP",
            "Drink Water",
            "Bump your fists together twice to spend your level-up points",
            "Shake a bottle before you throw it. Careful though!",
            "Hold B to open the Candy Shop",
        };
        [Tooltip("Each tip's hint: Hint 1 goes with Tip 1, and so on. Shown under the tip, smaller, once the player " +
                 "touches the board. Empty or only spaces: none (the set-up menu only fills hints that are empty).")]
        [TextArea(1, 2)]
        [SerializeField] private string[] hints = { };

        [Header("Touch And Hit")]
        [Tooltip("A hit at least this fast (metres per second) from the player on the board or the knight shows the next " +
                 "tip. Slower touches only show the hint. A firm punch is about 3-5, a gentle touch under 1.")]
        [Min(0.1f)]
        [SerializeField] private float strongHit = 2.5f;
        [Tooltip("Seconds after a hard hit before the next one shows another tip.")]
        [Min(0f)]
        [SerializeField] private float hitCooldown = 0.5f;
        [Tooltip("Seconds the hint takes to fade in.")]
        [Min(0f)]
        [SerializeField] private float hintFadeSeconds = 0.3f;
        [Tooltip("A hand this close to the board's face (in metres, from the middle of the hand) touches it.")]
        [Range(0.02f, 0.2f)]
        [SerializeField] private float touchDistance = 0.08f;
        [Tooltip("How far a hard hit knocks the board back, in metres, before it springs back.")]
        [Min(0f)]
        [SerializeField] private float knockDistance = 0.04f;
        [Tooltip("How far a hard hit tips the board over, in degrees, before it springs back.")]
        [Min(0f)]
        [SerializeField] private float knockDegrees = 7f;
        [Tooltip("Played on a hard hit, one picked at random. Empty: no sound.")]
        [SerializeField] private AudioClip[] knockSounds = { };
        [Range(0f, 1f)]
        [SerializeField] private float knockVolume = 0.8f;

        // Which tips Tools > 429 Game > Menu > Set Up Knight Tip Board has already put on, so tips and hints deleted by
        // hand stay deleted when it runs again.
        [SerializeField, HideInInspector] private int menuVersion;

        // A light touch only wobbles the board this much (a hard hit = 1), with no sound.
        private const float SoftKnock = 0.3f;
        private const float KnockSeconds = 0.5f;
        private const float KnockWobbles = 6f;
        private const float KnockSettle = 8f;
        // Hands a little off the board's edges still touch it, in metres.
        private const float EdgeMargin = 0.03f;
        // A hand pushed this far through the board (in metres, behind its face) still touches it.
        private const float BehindFace = 0.1f;
        // A hand must be this much farther away (in metres) before touching again counts again.
        private const float LetGo = 0.05f;

        // Once per launch: the game can start over in the same scene (after dying, or after the boss).
        private static bool firstShown;
        private static int lastTip = -1;

        private Transform head;
        private CanvasGroup fade;
        private bool passed;

        // The tip showing: an index into Tips, or -1 for the First Tip.
        private int current = -1;
        private bool hintWanted;
        private float hintAlpha;
        private int writtenTip = int.MinValue;
        private int writtenAlpha = -1;
        private float nextStrong;

        // The player's hands (each controller's Fist), checked against the board's face.
        private Fist[] hands = { };
        private bool[] handTouching = { };
        private float nextHandSearch;
        // The board's hit box, and the player's own body (everything on the XR Origin), which walks through it.
        private Collider[] boardBody = { };
        private Collider[] playerBody = { };
        private float nextLetThrough;

        private Vector3 restPosition;
        private Quaternion restRotation;
        private float knockStart = -1f;
        private float knockPower;
        private float knockAcross;
        private AudioSource knockSource;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            firstShown = false;
            lastTip = -1;
        }

        /// <summary>The first tip shows again (the game was reset to a new install).</summary>
        public static void ForgetShown()
        {
            firstShown = false;
            lastTip = -1;
        }

        private void Start()
        {
            if (board != null)
            {
                // Left where it is, so a knight knocked about doesn't swing it around; next to the knight, so it's
                // cleared away with everything else the game spawned.
                board.SetParent(transform.parent, true);
                restPosition = board.position;
                restRotation = board.rotation;
                // The board's own hit box isn't on the knight any more, so it passes its hits on.
                foreach (var hitBox in board.GetComponentsInChildren<TipBoardHitBox>(true)) hitBox.Owner = this;
                boardBody = board.GetComponentsInChildren<Collider>(true);
                LetPlayerThrough();
            }
            current = PickTip();
            Write();
        }

        private void Update()
        {
            if (board == null || !board.gameObject.activeSelf) return;
            Knock();
            if (!passed && passedBy >= 0f)
            {
                // Once the player is past the board (a world-space canvas is read from behind its forward), it fades away.
                if (head == null && Camera.main != null) head = Camera.main.transform;
                if (head != null && Vector3.Dot(head.position - board.position, board.forward) >= passedBy)
                {
                    passed = true;
                    if (!board.TryGetComponent(out fade)) fade = board.gameObject.AddComponent<CanvasGroup>();
                }
            }
            if (passed)
            {
                fade.alpha = Mathf.MoveTowards(fade.alpha, 0f, Time.deltaTime / fadeSeconds);
                if (fade.alpha <= 0f) board.gameObject.SetActive(false);
                return;
            }

            if (hintWanted && hintAlpha < 1f)
            {
                hintAlpha = hintFadeSeconds > 0f ? Mathf.MoveTowards(hintAlpha, 1f, Time.deltaTime / hintFadeSeconds) : 1f;
                Write();
            }
            WatchHands();
            LetPlayerThrough();
        }

        // The hit box is at head height: the player's body (its Character Controller) walks through it, as it did before
        // the board had one, instead of bumping its head. Done again every second: a collider switched off and on again
        // forgets what it ignores.
        private void LetPlayerThrough()
        {
            if (boardBody.Length == 0 || Time.time < nextLetThrough) return;
            nextLetThrough = Time.time + 1f;
            if (playerBody.Length == 0)
            {
                var rig = FindAnyObjectByType<XROrigin>();
                if (rig == null) return;
                playerBody = rig.GetComponentsInChildren<Collider>(true);
            }
            foreach (var part in boardBody)
            {
                if (part == null) continue;
                foreach (var body in playerBody)
                {
                    if (body != null) Physics.IgnoreCollision(part, body);
                }
            }
        }

        private void OnDestroy()
        {
            if (board != null) Destroy(board.gameObject);
        }

        // Hits on the knight itself (its colliders are on this object's body).
        private void OnCollisionEnter(Collision collision)
        {
            Hit(collision);
        }

        /// <summary>
        /// Something hit the board or the knight. Only the player's hits count (a fist, a thing they hold or just threw,
        /// a shot): a light one shows the hint, a hard one the next tip.
        /// </summary>
        public void Hit(Collision collision)
        {
            if (!Listening || collision == null || !HeldThings.ByPlayer(collision)) return;
            var at = collision.contactCount > 0 ? collision.GetContact(0).point : collision.transform.position;
            Touched(HitSpeed(collision), at);
        }

        // How fast the player's hit was. A fist's own punching ball counts only as fast as the hand itself moves (as the
        // fist's own punches do): the ball is pulled along after the hand, so walking, turning or the Elon Boong's reach
        // make it hit fast though the hand never punched. A flying punch (Fist calls it "Flying Fist (hand)") only flies
        // after a real punch, so it counts at its full speed, as does everything else.
        private static float HitSpeed(Collision collision)
        {
            float speed = collision.relativeVelocity.magnitude;
            var ball = collision.rigidbody;
            if (!Fist.IsFistBall(ball) || ball.name.StartsWith("Flying Fist", System.StringComparison.Ordinal)) return speed;
            var hand = Fist.HandOf(ball);
            var fist = hand != null ? hand.GetComponentInParent<Fist>(true) : null;
            return fist != null ? Mathf.Min(speed, fist.HandVelocity.magnitude) : speed;
        }

        // Still there and not walked past yet.
        private bool Listening => board != null && board.gameObject.activeInHierarchy && !passed;

        private void Touched(float speed, Vector3 at)
        {
            // Where it was hit across the board, from -1 (the reader's left edge) to 1 (the right): it turns that way.
            var local = board.InverseTransformPoint(at);
            var area = board.rect;
            float across = area.width > 0f ? Mathf.Clamp((local.x - area.center.x) / (area.width * 0.5f), -1f, 1f) : 0f;

            if (speed >= strongHit && Time.time >= nextStrong)
            {
                nextStrong = Time.time + hitCooldown;
                NextTip();
                StartKnock(1f, across);
                PlayKnock();
                return;
            }
            ShowHint();
            // A light touch wobbles it a little, but doesn't cut a hard knock short.
            if (knockStart < 0f || Time.time - knockStart > KnockSeconds * 0.4f) StartKnock(SoftKnock, across);
        }

        private void ShowHint()
        {
            hintWanted = true;
        }

        // The tip after the one showing (after the last, the first again), with its hint already showing.
        private void NextTip()
        {
            if (tips != null && tips.Length > 0)
            {
                current = current < 0 || current >= tips.Length ? 0 : (current + 1) % tips.Length;
                // The next knight doesn't start with the same one.
                lastTip = current;
            }
            hintWanted = true;
            hintAlpha = 1f;
            Write();
        }

        // Open hands have no colliders: a hand that comes within Touch Distance of the board's face touches it (once,
        // until it moves away again); a fast one is a hard hit (a slap).
        private void WatchHands()
        {
            if (hands.Length == 0)
            {
                if (Time.time < nextHandSearch) return;
                nextHandSearch = Time.time + 1f;
                hands = FindObjectsByType<Fist>(FindObjectsSortMode.None);
                handTouching = new bool[hands.Length];
                if (hands.Length == 0) return;
            }

            var area = board.rect;
            var scale = board.lossyScale;
            for (int i = 0; i < hands.Length; i++)
            {
                var hand = hands[i];
                if (hand == null || !hand.isActiveAndEnabled)
                {
                    handTouching[i] = false;
                    continue;
                }
                // How far the hand is outside the board's edges, and in front of its face (the reader's side is -z),
                // in metres.
                var local = board.InverseTransformPoint(hand.Knuckles);
                float outX = (Mathf.Abs(local.x - area.center.x) - area.width * 0.5f) * Mathf.Abs(scale.x);
                float outY = (Mathf.Abs(local.y - area.center.y) - area.height * 0.5f) * Mathf.Abs(scale.y);
                float front = -local.z * Mathf.Abs(scale.z);

                if (handTouching[i])
                {
                    if (outX > EdgeMargin + LetGo || outY > EdgeMargin + LetGo || front > touchDistance + LetGo ||
                        front < -BehindFace - LetGo)
                        handTouching[i] = false;
                    continue;
                }
                if (outX > EdgeMargin || outY > EdgeMargin || front > touchDistance || front < -BehindFace) continue;
                handTouching[i] = true;
                // The hand's own speed, walking and turning left out.
                Touched(hand.HandVelocity.magnitude, hand.Knuckles);
            }
        }

        private void StartKnock(float power, float across)
        {
            if (power <= 0f) return;
            knockStart = Time.time;
            knockPower = power;
            knockAcross = across;
        }

        // The board knocked back and tipped over (towards the side that was hit), springing back to where it was.
        private void Knock()
        {
            if (knockStart < 0f) return;
            float t = Time.time - knockStart;
            if (t >= KnockSeconds)
            {
                knockStart = -1f;
                board.SetPositionAndRotation(restPosition, restRotation);
                return;
            }
            float wave = Mathf.Exp(-KnockSettle * t) * Mathf.Sin(t * KnockWobbles * 2f * Mathf.PI) * knockPower;
            var away = restRotation * Vector3.forward;
            var tilt = Quaternion.AngleAxis(knockDegrees * wave, restRotation * Vector3.right);
            var turn = Quaternion.AngleAxis(-knockDegrees * 0.6f * knockAcross * wave, restRotation * Vector3.up);
            board.SetPositionAndRotation(restPosition + away * (knockDistance * wave), tilt * turn * restRotation);
        }

        private void PlayKnock()
        {
            if (knockSounds == null || knockSounds.Length == 0) return;
            var clip = knockSounds[Random.Range(0, knockSounds.Length)];
            if (clip == null) return;
            if (knockSource == null)
            {
                knockSource = board.gameObject.AddComponent<AudioSource>();
                knockSource.playOnAwake = false;
                knockSource.spatialBlend = 1f;
                knockSource.dopplerLevel = 0f;
            }
            knockSource.pitch = Random.Range(0.92f, 1.08f);
            knockSource.PlayOneShot(clip, knockVolume);
        }

        private int PickTip()
        {
            if (!firstShown || tips == null || tips.Length == 0)
            {
                firstShown = true;
                return -1;
            }

            int pick = Random.Range(0, tips.Length);
            // Never the same tip twice in a row.
            if (tips.Length > 1 && pick == lastTip) pick = (pick + 1 + Random.Range(0, tips.Length - 1)) % tips.Length;
            lastTip = pick;
            return pick;
        }

        private string HintOf(int tip)
        {
            string hint = tip < 0 ? firstTipHint : hints != null && tip < hints.Length ? hints[tip] : null;
            return string.IsNullOrWhiteSpace(hint) ? null : hint;
        }

        // Puts the tip on the board, with its hint as far faded in as it is (only when something changed).
        private void Write()
        {
            if (text == null) return;
            if (tips == null || current >= tips.Length) current = -1;
            string hint = HintOf(current);
            int alpha = hint != null ? Mathf.RoundToInt(Mathf.Clamp01(hintAlpha) * 255f) : 255;
            if (current == writtenTip && alpha == writtenAlpha) return;
            writtenTip = current;
            writtenAlpha = alpha;

            string tip = current < 0 ? firstTip : tips[current];
            if (hint == null)
            {
                text.text = tip;
                return;
            }
            // The tip, then a gap (a line holding only a space, Hint Gap tall), then the hint on its own lines under it
            // in smaller letters. The First Tip is kept on one line. A hidden hint is there but see-through, so the tip
            // doesn't jump when it fades in.
            if (current < 0) tip = $"<nobr>{tip}</nobr>";
            string gap = hintGap > 0.01f ? $"\n<size={Mathf.RoundToInt(hintGap * 100f)}%> </size>" : "";
            string see = alpha < 255 ? $"<alpha=#{alpha:X2}>" : "";
            text.text = $"{tip}{gap}\n<size={Mathf.RoundToInt(hintSize * 100f)}%>{see}{hint}</size>";
        }
    }
}
