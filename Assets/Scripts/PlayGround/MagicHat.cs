using System.Collections.Generic;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.PlayGround
{
    /// <summary>
    /// The magician's hat that knows the cauldron's recipe (user, 2026-11-07). It lies any way up, its hole pointing
    /// anywhere. Hit it (with something you hold or throw, or slap it) and it shakes, hops and turns its hole to the
    /// sky, landing on its crown, and spits out its recipe card (Recipe_Card): the card shoots up out of the hole,
    /// growing from a fifth of its size to full size, and hangs above it with the recipe (each ingredient's picture and
    /// how many) on its blank side, turned to the player. After a few seconds, or once the player walks off, it flies
    /// back into the hat; another hit brings it out again. Once the card has been out, the cauldron shows the recipe
    /// above its pot too, as ingredients go in. When a wrong recipe finishes cooking, the hat shakes and giggles, a hint
    /// to go and read it. Goes on the hat's root, which needs a collider.
    /// </summary>
    [DisallowMultipleComponent]
    public class MagicHat : MonoBehaviour
    {
        private enum CardState
        {
            Inside,
            Spitting,
            FlyingOut,
            Out,
            FlyingBack,
        }

        [Tooltip("The cauldron whose recipe it shows. Left empty, the one in the scene.")]
        [SerializeField] private Cauldron cauldron;
        [Tooltip("Hits slower than this, in metres per second, don't count.")]
        [Min(0f)]
        [SerializeField] private float hitSpeed = 0.5f;

        [Header("Recipe card")]
        [Tooltip("The card it spits out (Recipe_Card). Its size in its prefab is its full size.")]
        [SerializeField] private GameObject card;
        [SerializeField] private TMP_FontAsset font;
        [Tooltip("Colour of the recipe's writing. The card is lit by the scene and looks dark, so a light colour reads " +
                 "best. Used each time the card comes out, so it can be changed while playing.")]
        [SerializeField] private Color textColor = new(1f, 0.93f, 0.78f, 1f);
        [Tooltip("How big the card is as it leaves the hat, compared to its full size (0.2 = a fifth).")]
        [Range(0.01f, 1f)]
        [SerializeField] private float startSize = 0.2f;
        [Tooltip("Seconds it takes to fly out and grow to full size.")]
        [Min(0.1f)]
        [SerializeField] private float flySeconds = 0.9f;
        [Tooltip("How far above the hat the card's bottom edge hangs, in metres.")]
        [SerializeField] private float floatHeight = 0.3f;
        [Tooltip("Seconds it stays out, turned to the player.")]
        [Min(1f)]
        [SerializeField] private float showSeconds = 8f;
        [Tooltip("It goes back in sooner once the player is this far from the hat, in metres along the floor.")]
        [Min(1f)]
        [SerializeField] private float farDistance = 6f;
        [Tooltip("Tick if the recipe shows up on the card's pumpkin side instead of its blank side.")]
        [SerializeField] private bool otherSide;
        [Tooltip("Played as the card flies out.")]
        [SerializeField] private AudioClip spitSound;
        [Tooltip("Played as the card goes back in.")]
        [SerializeField] private AudioClip backSound;

        [Header("Shake")]
        [Tooltip("Seconds it shakes when hit; the card comes out after.")]
        [Min(0f)]
        [SerializeField] private float hitShakeSeconds = 0.45f;
        [Tooltip("Seconds it shakes after a wrong recipe.")]
        [Min(0.1f)]
        [SerializeField] private float shakeSeconds = 1.6f;
        [Tooltip("How far it rocks, in degrees.")]
        [SerializeField] private float shakeAngle = 14f;
        [Tooltip("How high it hops, in metres.")]
        [SerializeField] private float shakeHop = 0.06f;
        [Tooltip("Played when it shakes after a wrong recipe.")]
        [SerializeField] private AudioClip shakeSound;

        [Header("Magician's hat")]
        [Tooltip("It starts (and lands after being hidden) lying any way up, its hole pointing anywhere.")]
        [SerializeField] private bool lieAnyWay = true;
        [Tooltip("Seconds it takes, after the shake, to hop and turn its hole to the sky.")]
        [Min(0.05f)]
        [SerializeField] private float flipSeconds = 0.4f;
        [Tooltip("How high it hops as it turns over, in metres.")]
        [Min(0f)]
        [SerializeField] private float flipHop = 0.3f;
        [Tooltip("Its hole counts as already pointing up within this many degrees (then it only shakes).")]
        [Range(0f, 60f)]
        [SerializeField] private float uprightWithin = 20f;
        [Tooltip("How far into the hole the card starts, in metres below the brim.")]
        [Min(0f)]
        [SerializeField] private float holeDepth = 0.25f;
        [Tooltip("How high the card shoots above where it then hangs, in metres (it falls back a little to hang).")]
        [Min(0f)]
        [SerializeField] private float launchHeight = 0.8f;

        // The recipe is written this many pixels wide, over this much of the card's side (its border stays clear).
        private const float CanvasWidth = 300f;
        private const float FaceUse = 0.74f;
        private const float RowHeight = 72f;
        private const float IconSize = 60f;
        private const float CountWidth = 110f;
        // Going back in takes this share of Fly Seconds.
        private const float BackShare = 0.7f;

        private readonly List<Collider> colliders = new();
        private Transform[] hands = System.Array.Empty<Transform>();
        private Vector3[] lastHand = System.Array.Empty<Vector3>();
        private Rigidbody body;
        private Transform head;
        private CardState state;
        private float stateSince;
        private Transform cardShown;
        private Vector3 cardScale;
        private Bounds cardShape;
        private RectTransform recipe;
        private Vector3 hatTop;
        private Vector3 flyFrom;
        private float flyFromSize;
        private bool warned;
        private float shakeStart = -1000f;
        private float shakeLength;
        private bool shaking;
        private Vector3 restPosition;
        private Quaternion restRotation;
        // Shaking and turning its hole up after a hit: the card waits for it.
        private bool tricking;
        private bool trickWasKinematic;
        // Turned hole up by a hit and held there (kinematic) until it's hidden again.
        private bool standing;

        private void Awake()
        {
            GetComponentsInChildren(colliders);
            colliders.RemoveAll(c => c.isTrigger);
            // Hits on colliders of its parts are gathered here by a body; one that doesn't move, if it has none.
            if (!TryGetComponent(out body))
            {
                body = gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true;
            }
        }

        private void Start()
        {
            if (cauldron == null) cauldron = FindAnyObjectByType<Cauldron>();
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;

            var left = XRRig.FindController(left: true);
            var right = XRRig.FindController(left: false);
            hands = new[] { left, right };
            lastHand = new Vector3[hands.Length];
            for (int i = 0; i < hands.Length; i++) lastHand[i] = hands[i] != null ? hands[i].position : Vector3.zero;

            LieAnyWay();
        }

        /// <summary>
        /// Drops it a little, lying any way up (its hole pointing anywhere), where it stands: its start, and wherever it
        /// was hidden. Only a hat that falls by itself (not kinematic); off with Lie Any Way.
        /// </summary>
        public void LieAnyWay()
        {
            if (!lieAnyWay || body == null || tricking) return;
            if (standing)
            {
                body.isKinematic = false;
                standing = false;
            }
            if (body.isKinematic) return;
            // The hole sideways most of the time, sometimes face down or up.
            float tilt = Random.value < 0.7f ? Random.Range(70f, 110f) : (Random.value < 0.5f ? 180f : 0f);
            var turn = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Quaternion.Euler(tilt, 0f, 0f);
            var at = transform.position + Vector3.up * 0.4f;
            transform.SetPositionAndRotation(at, turn);
            body.position = at;
            body.rotation = turn;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        private void OnEnable()
        {
            Cauldron.CookFailed += OnCookFailed;
            Cauldron.RecipeChanged += OnRecipeChanged;
        }

        private void OnDisable()
        {
            Cauldron.CookFailed -= OnCookFailed;
            Cauldron.RecipeChanged -= OnRecipeChanged;
            EndTrick();
            if (cardShown != null) cardShown.gameObject.SetActive(false);
            state = CardState.Inside;
        }

        private void OnDestroy()
        {
            if (cardShown != null) Destroy(cardShown.gameObject);
        }

        /// <summary>Shakes and spits out the recipe card, or keeps it out longer if it's out already.</summary>
        public void ShowRecipe()
        {
            switch (state)
            {
                case CardState.Inside:
                    // Shakes, turns its hole to the sky, then the card comes out.
                    SetState(CardState.Spitting);
                    StartCoroutine(HatTrick());
                    break;
                case CardState.Out:
                    // Stays out longer.
                    stateSince = Time.time;
                    break;
                case CardState.FlyingBack:
                    // Comes back out from where it is.
                    FlyFromHere(CardState.FlyingOut);
                    break;
            }
            // Every hit shakes it, the card out or not.
            if (state != CardState.Spitting && !tricking) StartCoroutine(HatTrick());
        }

        /// <summary>Rocks and hops the hat with a giggle, so the player notices it.</summary>
        public void Shake()
        {
            Shake(shakeSeconds, shakeSound);
        }

        // Hit by something the player holds or threw, or by the player.
        private void OnCollisionEnter(Collision collision)
        {
            if (collision.relativeVelocity.magnitude < hitSpeed) return;
            bool byPlayer = collision.collider.GetComponentInParent<XROrigin>() != null;
            bool byItem = collision.rigidbody != null && collision.rigidbody.GetComponentInParent<XRGrabInteractable>() != null;
            if (byPlayer || byItem) ShowRecipe();
        }

        private void Update()
        {
            CheckSlaps();
            AnimateShake();

            float since = Time.time - stateSince;
            switch (state)
            {
                case CardState.Spitting:
                    if (!tricking) SpitCard();
                    break;
                case CardState.FlyingOut:
                    if (since >= flySeconds) SetState(CardState.Out);
                    break;
                case CardState.Out:
                    if (since >= showSeconds || PlayerFar()) FlyFromHere(CardState.FlyingBack);
                    break;
                case CardState.FlyingBack:
                    if (since >= flySeconds * BackShare) PutCardAway();
                    break;
            }
        }

        private void LateUpdate()
        {
            if (cardShown == null || !cardShown.gameObject.activeSelf) return;

            float since = Time.time - stateSince;
            var hover = hatTop + Vector3.up * (floatHeight + cardShape.extents.y * cardScale.y);
            switch (state)
            {
                case CardState.FlyingOut:
                {
                    // Shoots up past its spot and settles back into it, growing, and spins round to face the player.
                    float u = Mathf.Clamp01(since / flySeconds);
                    float eased = 1f - (1f - u) * (1f - u) * (1f - u);
                    var over = hover + Vector3.up * Mathf.Max(launchHeight, 0.25f * Vector3.Distance(flyFrom, hover));
                    var at = Bezier(flyFrom, over, hover, eased);
                    Pose(at, Quaternion.AngleAxis(360f * (1f - eased), Vector3.up) * Facing(at), Mathf.LerpUnclamped(flyFromSize, 1f, Overshoot(u)));
                    break;
                }
                case CardState.Out:
                {
                    // Hangs there bobbing gently, keeping its recipe turned to the player.
                    var at = hover + Vector3.up * (Mathf.Sin(since * 1.6f) * 0.03f);
                    var turn = Quaternion.Slerp(cardShown.rotation, Facing(at), 1f - Mathf.Exp(-6f * Time.deltaTime));
                    Pose(at, turn, 1f);
                    break;
                }
                case CardState.FlyingBack:
                {
                    float u = Mathf.Clamp01(since / (flySeconds * BackShare));
                    float eased = u * u;
                    var at = Vector3.Lerp(flyFrom, Top(), eased);
                    Pose(at, Quaternion.AngleAxis(-270f * eased, Vector3.up) * Facing(at), Mathf.Lerp(flyFromSize, startSize, eased));
                    break;
                }
            }
        }

        private void SetState(CardState next)
        {
            state = next;
            stateSince = Time.time;
        }

        // Out of the hat it comes, small, and flies up to hang above it.
        private void SpitCard()
        {
            if (!MakeCard())
            {
                SetState(CardState.Inside);
                return;
            }

            BuildRecipe();
            // The player has seen it now, so the cauldron shows it too.
            if (cauldron != null) cauldron.RevealRecipe();
            hatTop = Top();
            // From down in the hole, straight up out of it.
            flyFrom = hatTop - Vector3.up * holeDepth;
            flyFromSize = startSize;
            Pose(flyFrom, Facing(flyFrom), startSize);
            cardShown.gameObject.SetActive(true);
            if (spitSound != null) AudioSource.PlayClipAtPoint(spitSound, hatTop);
            SetState(CardState.FlyingOut);
        }

        // Starts a flight (out or back in) from wherever the card is now.
        private void FlyFromHere(CardState flight)
        {
            flyFrom = cardShown.TransformPoint(cardShape.center);
            flyFromSize = cardScale.x != 0f ? cardShown.localScale.x / cardScale.x : 1f;
            SetState(flight);
        }

        private void PutCardAway()
        {
            cardShown.gameObject.SetActive(false);
            if (backSound != null) AudioSource.PlayClipAtPoint(backSound, Top());
            SetState(CardState.Inside);
        }

        // The card is made once, the first time it's needed, and kept (hidden while it's in the hat).
        private bool MakeCard()
        {
            if (cardShown != null) return true;
            if (card == null)
            {
                if (!warned) Debug.LogWarning($"[MagicHat] '{name}' has no Card, so it has no recipe to show.", this);
                warned = true;
                return false;
            }

            var made = Instantiate(card);
            made.name = card.name;
            made.SetActive(false);
            // Just for show: it doesn't bump into things.
            foreach (var part in made.GetComponentsInChildren<Collider>(true)) part.enabled = false;
            cardShown = made.transform;
            cardScale = card.transform.localScale;
            cardShape = LocalShape(cardShown);
            return true;
        }

        // The recipe, written on the card's blank side: each ingredient's picture and how many. Written fresh each
        // time, so it's always the current recipe (a new PlayGround thinks up a new one).
        private void BuildRecipe()
        {
            if (cauldron == null) cauldron = FindAnyObjectByType<Cauldron>();
            if (recipe != null) Destroy(recipe.gameObject);

            float scale = cardShape.size.x * FaceUse / CanvasWidth;
            float height = cardShape.size.y * FaceUse / scale;
            recipe = WorldUI.CreatePanel("Recipe", CanvasWidth, height, scale, Color.clear);
            recipe.SetParent(cardShown, false);
            float gap = Mathf.Max(0.002f, cardShape.size.z * 0.2f);
            float side = otherSide ? cardShape.max.z + gap : cardShape.min.z - gap;
            recipe.localPosition = new Vector3(cardShape.center.x, cardShape.center.y - cardShape.size.y * FaceUse * 0.5f, side);
            recipe.localRotation = otherSide ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;

            // Just the ingredients, each picture with how many: no title or words. All cooked, the card is blank.
            var lines = cauldron != null ? cauldron.Recipe : null;
            int rows = lines != null ? lines.Count : 0;
            if (rows == 0) return;
            // More rows than fit are made smaller, so they all stay on the card.
            float fit = Mathf.Min(1f, height / (rows * RowHeight));
            float rowHeight = RowHeight * fit;
            float top = Mathf.Max(0f, (height - rows * rowHeight) * 0.5f);

            float iconSize = IconSize * fit;
            float left = (CanvasWidth - iconSize - 16f - CountWidth) * 0.5f;
            for (int i = 0; i < rows; i++, top += rowHeight)
            {
                var line = lines[i];
                if (line.icon == null)
                {
                    var words = WorldUI.CreateText($"Line {i + 1}", recipe, $"{line.count} x {line.id.Replace('_', ' ')}", 34f * fit, FontStyles.Bold, TextAlignmentOptions.Center, textColor, font);
                    WorldUI.Place((Graphic)words, 0f, top, CanvasWidth, rowHeight);
                    continue;
                }

                var icon = WorldUI.CreateImage($"Icon {i + 1}", recipe, Color.white);
                icon.sprite = line.icon;
                icon.preserveAspect = true;
                WorldUI.Place(icon, left, top + (rowHeight - iconSize) * 0.5f, iconSize, iconSize);
                var count = WorldUI.CreateText($"Count {i + 1}", recipe, $"x {line.count}", 44f * fit, FontStyles.Bold, TextAlignmentOptions.Left, textColor, font);
                WorldUI.Place((Graphic)count, left + iconSize + 16f, top, CountWidth, rowHeight);
            }
        }

        // Puts the card's middle at 'at', turned 'turn', at 'size' of its full size.
        private void Pose(Vector3 at, Quaternion turn, float size)
        {
            cardShown.localScale = cardScale * size;
            cardShown.rotation = turn;
            cardShown.position = at - turn * Vector3.Scale(cardShape.center, cardShown.localScale);
        }

        // Turned so its recipe side looks at the player, staying upright.
        private Quaternion Facing(Vector3 at)
        {
            if (FindHead() == null) return cardShown.rotation;
            var away = at - head.position;
            if (otherSide) away = -away;
            return away.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(away, Vector3.up) : cardShown.rotation;
        }

        private bool PlayerFar()
        {
            if (FindHead() == null) return false;
            var away = head.position - transform.position;
            away.y = 0f;
            return away.sqrMagnitude > farDistance * farDistance;
        }

        private Transform FindHead()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            return head;
        }

        private void Shake(float seconds, AudioClip sound)
        {
            if (seconds <= 0f) return;
            // A longer shake that's still going isn't cut short.
            if (shaking && shakeLength - (Time.time - shakeStart) > seconds) return;

            shakeStart = Time.time;
            shakeLength = seconds;
            shaking = true;
            if (sound != null) AudioSource.PlayClipAtPoint(sound, Top());
            // A hat that falls around by itself gets a kick instead.
            if (body != null && !body.isKinematic)
            {
                body.AddForce(Vector3.up * 2.5f, ForceMode.VelocityChange);
                body.AddTorque(Random.onUnitSphere * 4f, ForceMode.VelocityChange);
            }
        }

        // Hit: it shakes where it lies, then hops and turns over so its hole points at the sky, landing on its crown.
        // It's held by this (kinematic) meanwhile and falls by itself again after.
        private System.Collections.IEnumerator HatTrick()
        {
            tricking = true;
            trickWasKinematic = body != null && body.isKinematic;
            if (body != null && !trickWasKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }
            var startAt = transform.position;
            var startTurn = transform.rotation;

            // A short shake.
            for (float t = 0f; t < hitShakeSeconds; t += Time.deltaTime)
            {
                float fade = 1f - t / hitShakeSeconds;
                float rock = Mathf.Sin(t * Mathf.PI * 2f * 4f) * shakeAngle * fade;
                float hop = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f * 2f)) * shakeHop * fade;
                transform.SetPositionAndRotation(startAt + Vector3.up * hop, startTurn * Quaternion.Euler(rock, 0f, rock * 0.6f));
                yield return null;
            }
            transform.SetPositionAndRotation(startAt, startTurn);

            // The hole (its up) to the sky, keeping which way it faces; it lands on its crown (its pivot) on the floor.
            if (Vector3.Angle(transform.up, Vector3.up) > uprightWithin)
            {
                var facing = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                if (facing.sqrMagnitude < 0.01f) facing = Vector3.ProjectOnPlane(-transform.up, Vector3.up);
                if (facing.sqrMagnitude < 0.01f) facing = Vector3.forward;
                var endTurn = Quaternion.LookRotation(facing.normalized, Vector3.up);
                // It stands on what it lay on: its crown (pivot) goes where its lowest point is now, a hair above.
                var middle = Middle();
                var endAt = new Vector3(middle.x, Bottom() + 0.01f, middle.z);
                for (float t = 0f; t < flipSeconds; t += Time.deltaTime)
                {
                    float u = t / flipSeconds;
                    float eased = u * u * (3f - 2f * u);
                    var at = Vector3.Lerp(startAt, endAt, eased) + Vector3.up * (Mathf.Sin(u * Mathf.PI) * flipHop);
                    transform.SetPositionAndRotation(at, Quaternion.Slerp(startTurn, endTurn, eased));
                    yield return null;
                }
                transform.SetPositionAndRotation(endAt, endTurn);
            }
            EndTrick();
        }

        // It stays standing there, hole up, held still (so nothing it stands against can knock it flying), until it's
        // hidden again and lies any way up.
        private void EndTrick()
        {
            if (!tricking) return;
            tricking = false;
            if (body != null)
            {
                body.position = transform.position;
                body.rotation = transform.rotation;
                body.isKinematic = true;
                if (!trickWasKinematic) standing = true;
            }
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
        }

        // The middle of its colliders.
        private Vector3 Middle()
        {
            if (colliders.Count == 0) return transform.position;
            var bounds = colliders[0].bounds;
            foreach (var c in colliders)
            {
                if (c != null) bounds.Encapsulate(c.bounds);
            }
            return bounds.center;
        }

        // Its lowest point now (what it rests on).
        private float Bottom()
        {
            float bottom = transform.position.y;
            foreach (var c in colliders)
            {
                if (c != null) bottom = Mathf.Min(bottom, c.bounds.min.y);
            }
            return bottom;
        }

        // A slapping hand counts as a hit too (hands don't bump into things the way held items do).
        private void CheckSlaps()
        {
            if (colliders.Count == 0) return;
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            for (int i = 0; i < hands.Length; i++)
            {
                if (hands[i] == null) continue;
                var now = hands[i].position;
                float speed = (now - lastHand[i]).magnitude / dt;
                lastHand[i] = now;
                if (speed < hitSpeed) continue;

                foreach (var c in colliders)
                {
                    if (c == null || !c.enabled) continue;
                    var touching = c.bounds;
                    touching.Expand(0.1f);
                    if (!touching.Contains(now)) continue;
                    ShowRecipe();
                    return;
                }
            }
        }

        private void AnimateShake()
        {
            if (!shaking || (body != null && !body.isKinematic)) return;

            float t = Time.time - shakeStart;
            if (t > shakeLength)
            {
                shaking = false;
                transform.localPosition = restPosition;
                transform.localRotation = restRotation;
                return;
            }

            float fade = 1f - t / shakeLength;
            float rock = Mathf.Sin(t * Mathf.PI * 2f * 4f) * shakeAngle * fade;
            float hop = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f * 2f)) * shakeHop * fade;
            transform.localRotation = restRotation * Quaternion.Euler(rock, 0f, rock * 0.6f);
            transform.localPosition = restPosition + Vector3.up * hop;
        }

        private void OnCookFailed(Cauldron from)
        {
            if (cauldron == null || from == cauldron) Shake();
        }

        // A new round: a card that's out shows the new recipe at once (and the player sees it there).
        private void OnRecipeChanged(Cauldron from)
        {
            if (cauldron != null && from != cauldron) return;
            if (cardShown == null || !cardShown.gameObject.activeSelf) return;
            if (state != CardState.FlyingOut && state != CardState.Out) return;

            cauldron = from;
            BuildRecipe();
            cauldron.RevealRecipe();
        }

        // The top of the hat, where the card comes out.
        private Vector3 Top()
        {
            if (colliders.Count == 0) return transform.position;
            var bounds = colliders[0].bounds;
            foreach (var c in colliders)
            {
                if (c != null) bounds.Encapsulate(c.bounds);
            }
            return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        }

        // The box around everything a thing shows, in its own space.
        private static Bounds LocalShape(Transform root)
        {
            var toRoot = root.worldToLocalMatrix;
            var shape = new Bounds(Vector3.zero, new Vector3(1f, 1f, 0.02f));
            bool any = false;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var toShape = toRoot * filter.transform.localToWorldMatrix;
                var b = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    corner = toShape.MultiplyPoint3x4(corner);
                    if (any) shape.Encapsulate(corner);
                    else shape = new Bounds(corner, Vector3.zero);
                    any = true;
                }
            }
            return shape;
        }

        private static Vector3 Bezier(Vector3 from, Vector3 via, Vector3 to, float t)
        {
            float u = 1f - t;
            return u * u * from + 2f * u * t * via + t * t * to;
        }

        private static float Overshoot(float t)
        {
            const float back = 1.7f;
            float u = t - 1f;
            return 1f + (back + 1f) * u * u * u + back * u * u;
        }
    }
}
