using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.Combat;
using Game.Enemies;
using Game.Leveling;
using Game.Locomotion;
using Game.Menus;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using FsmVariables = HutongGames.PlayMaker.FsmVariables;

namespace Game.Rooms
{
    /// <summary>
    /// A broken jigsaw picture on the wall that opens a mini game (Picture_Hang_4_Broken). As it appears, Start Placed
    /// of its pieces stay in the frame, picked at random, and the others (and the whole painting, Complete) are hidden;
    /// its glow says something's up. Hit it, or touch it and pull a trigger, while it hangs on the wall: the game stops
    /// (Pause Menu hold), the room goes dark with stars all round (Space Prefab), the picture comes to float in front of
    /// the player, and the missing pieces fly out and buzz round them like bugs: each darts to a spot round the player,
    /// hovers there a moment and darts off again (never through the player, and round behind them rather than across
    /// the picture), and now and then one zips away from a hand reaching for it (Dodge Chance). A hand gripping catches
    /// any piece it touches; from further off, point at one (a gold beam shows which) and grip, or keep gripping and
    /// sweep the pointing over one: it flies into the hand,
    /// as an XR Grab Interactable would (the game is stopped, so XR grabbing can't run: this does it itself). A held
    /// piece stays facing the picture, its back to it; the trigger turns it a quarter; let go by its own spot the right
    /// way round and it clicks in. The left stick walks the player about the space (Move Range from where they were),
    /// the right stick snap-turns; they're put back where they were when it's over. Time Limit seconds to do it.
    /// - Done: the pieces give way to the whole painting, fireworks go up behind it (Win Fireworks), the picture goes
    ///   back on the wall, Reward Candies burst out of it and the Candy Helmet pulls them in (Helmet Seconds).
    /// - Time's up: the picture goes back, breaks (its pieces fly, the frame drops) and Bug Count small cockroaches
    ///   pour out of it.
    /// Once the walls go and it drops (Wall Hung Prop) it's only a picture. Tools > 429 Game > Rooms > Set Up Picture
    /// Puzzle puts it on the prefab and fills it in.
    /// </summary>
    [DisallowMultipleComponent]
    public class PicturePuzzle : MonoBehaviour
    {
        // Each piece: "..._Puzzle_N" (N = 1-9 in reading order); the whole painting: "..._Complete".
        private static readonly Regex PieceName = new(@"_Puzzle_(\d+)$");
        // The space's dark backdrop (429/Space Backdrop) darkens the room and clears its depth; the picture, the pieces
        // and the hands are drawn after it, then the particles, then the clock.
        private const int BackdropQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay + 1;
        private const int PictureQueue = BackdropQueue + 1;
        private const int ParticleQueue = BackdropQueue + 2;
        private const int TextQueue = BackdropQueue + 3;
        private const string TextMaterial = "Damage Number";

        [Header("Picture")]
        [Tooltip("The pieces in reading order (top-left first). Left empty, the children named ..._Puzzle_N.")]
        [SerializeField] private Transform[] pieces = System.Array.Empty<Transform>();
        [Tooltip("The whole painting, shown when the puzzle is done. Left empty, the child named ..._Complete.")]
        [SerializeField] private GameObject complete;
        [Tooltip("The glow that shows the picture can be played (Picture_Puzzle_Glow); off once it's done or broken.")]
        [SerializeField] private GameObject glow;
        [Tooltip("Pieces left in the frame at the start, picked at random; the rest are the ones to put back.")]
        [Range(0, 8)]
        [SerializeField] private int startPlaced = 3;

        [Header("Mini game")]
        [Min(5f)]
        [SerializeField] private float timeLimit = 60f;
        [Tooltip("How close a hand must be to the picture, in metres, to start it with a trigger.")]
        [Min(0.05f)]
        [SerializeField] private float touchReach = 0.25f;
        [Tooltip("The stars and dust round the player while it's played (Picture_Puzzle_Space). Its look must be drawn " +
                 "after the backdrop (render queue 4003); the Set Up menu makes it so.")]
        [SerializeField] private GameObject spacePrefab;
        [Tooltip("The dark round the player (a material with the 429/Space Backdrop shader): it hides the room and lets " +
                 "the picture and pieces show in front of any wall. Empty: the headset fade darkens the room instead " +
                 "(then a wall between can hide them).")]
        [SerializeField] private Material backdropMaterial;
        [Tooltip("How dark the room goes behind (0 = not at all, 1 = black).")]
        [Range(0f, 1f)]
        [SerializeField] private float spaceDim = 0.97f;
        [Tooltip("How far in front of the player's eyes the picture floats, in metres.")]
        [Min(0.3f)]
        [SerializeField] private float showDistance = 1.2f;
        [Tooltip("A wider picture is shrunk to this width while it floats in front of the player, in metres (its pieces " +
                 "with it); a smaller one stays its own size.")]
        [Min(0.2f)]
        [SerializeField] private float showWidth = 1.1f;
        [Tooltip("How close a gripping hand must come to a piece to catch it, in metres.")]
        [Min(0.01f)]
        [SerializeField] private float grabReach = 0.12f;
        [Tooltip("How far off a piece can be pointed at and pulled in with grip, in metres (0 = no grabbing from afar).")]
        [Min(0f)]
        [SerializeField] private float farReach = 4f;
        [Tooltip("How forgiving the pointing is: metres off the line a piece can be, per metre away (plus 12 cm).")]
        [Range(0f, 0.5f)]
        [SerializeField] private float farAim = 0.12f;
        [Tooltip("The beam from the hand to the piece it points at (an additive particle material).")]
        [SerializeField] private Material rayMaterial;
        [Tooltip("Walking speed with the left stick, in metres a second (0 = no walking).")]
        [Min(0f)]
        [SerializeField] private float moveSpeed = 1.5f;
        [Tooltip("How far the player can walk from where they were, in metres.")]
        [Min(0f)]
        [SerializeField] private float moveRange = 2.5f;
        [Tooltip("Snap turn with the right stick, in degrees (0 = no turning).")]
        [Min(0f)]
        [SerializeField] private float turnAngle = 45f;
        [Tooltip("How close to its own spot a piece must be let go to click in, in metres.")]
        [Min(0.02f)]
        [SerializeField] private float snapDistance = 0.11f;
        [Tooltip("How fast the pieces dart about, in degrees round the player a second (random between).")]
        [SerializeField] private Vector2 flySpeed = new(70f, 120f);
        [Tooltip("How long a piece hovers before it darts off again, in seconds (random between).")]
        [SerializeField] private Vector2 hoverSeconds = new(0.4f, 1.2f);
        [Tooltip("Chance a piece zips away from a hand that comes close. It can't again for Dodge Rest seconds, so it " +
                 "can always be caught.")]
        [Range(0f, 1f)]
        [SerializeField] private float dodgeChance = 0.35f;
        [Min(0f)]
        [SerializeField] private float dodgeRest = 3f;

        [Header("Done")]
        [Tooltip("Fireworks going up behind the picture as it's done (Firework_Win), while the room is still dark.")]
        [SerializeField] private GameObject winFireworks;
        [Tooltip("How long the finished picture stays in front of the player (the fireworks), in seconds.")]
        [Min(0.5f)]
        [SerializeField] private float celebrateSeconds = 2.8f;
        [SerializeField] private GameObject[] candyPrefabs = System.Array.Empty<GameObject>();
        [Min(0)]
        [SerializeField] private int rewardCandies = 12;
        [Min(0f)]
        [SerializeField] private float helmetSeconds = 5f;

        [Header("Time's up")]
        [SerializeField] private GameObject bugPrefab;
        [Min(0)]
        [SerializeField] private int bugCount = 12;
        [Tooltip("Played where the picture breaks (Effect Pool); the first is also the puff each bug comes out of.")]
        [SerializeField] private GameObject[] breakEffects = System.Array.Empty<GameObject>();
        [Tooltip("Seconds the broken pieces lie about before they go.")]
        [Min(1f)]
        [SerializeField] private float debrisSeconds = 7f;

        [Header("Sounds")]
        [SerializeField] private AudioClip startSound;
        [SerializeField] private AudioClip snapSound;
        [SerializeField] private AudioClip turnSound;
        [SerializeField] private AudioClip wrongSound;
        [SerializeField] private AudioClip tickSound;
        [SerializeField] private AudioClip winSound;
        [SerializeField] private AudioClip breakSound;
        [Tooltip("The buzz each flying piece makes (a loop; its pitch differs piece to piece).")]
        [SerializeField] private AudioClip buzzSound;
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.8f;

        [Header("Music")]
        [Tooltip("Songs for the mini game, chill at first and tense as time runs out (user, 2026-10-28). Each time it " +
                 "plays one whose length is nearest the Time Limit (one of them at random), started so it ends just as " +
                 "the time runs out. It stops while the pause menu is open and when the puzzle is done or broken. Empty: none.")]
        [SerializeField] private AudioClip[] music = System.Array.Empty<AudioClip>();
        [Range(0f, 1f)]
        [SerializeField] private float musicVolume = 0.6f;

        private enum Phase { Waiting, Playing, Done, Broken }
        private enum PieceState { Placed, Hidden, Flying, Floating, Held, Snapping }

        private class Piece
        {
            public Transform t;
            public Vector3 slot;
            public Bounds bounds;
            public PieceState state;
            public float turn;
            public float turnShown;
            public int hand = -1;
            public Vector3 grabOffset;
            // Where it is round the player: degrees from straight ahead (kept unwrapped while it darts round behind),
            // metres out, metres up from the eyes; how fast each changes; and where it's darting to.
            public float rel;
            public float radius;
            public float height;
            public float relV;
            public float radiusV;
            public float heightV;
            public float toRel;
            public float toRadius;
            public float toHeight;
            public float topSpeed;
            public bool dodging;
            public float hoverUntil;
            public float nextDodge;
            public float buzzPhase;
            public AudioSource buzz;
            public Vector3 fromPos;
            public Quaternion fromRot;
            public float tweenStart;
            public float tweenSeconds;
            public float nopeUntil;
            // Grabbed from afar: it flies to the hand first.
            public Vector3 pullFrom;
            public float pullStart;
            public float pullSeconds;
            public Vector3 baseScale = Vector3.one;
        }

        private readonly List<Piece> all = new();
        private Transform board;
        private Phase phase;
        private WallHungProp hung;
        private AudioSource voice;
        private AudioSource musicSpeaker;
        private XROrigin origin;
        private readonly Transform[] hands = new Transform[2];
        private readonly Piece[] held = new Piece[2];
        private InputAction[] grips;
        private InputAction[] gripAxes;
        private InputAction[] triggers;
        private InputAction moveStick;
        private InputAction turnStick;
        private readonly Piece[] aimed = new Piece[2];
        private readonly bool[] wasTrigger = new bool[2];
        private readonly LineRenderer[] rays = new LineRenderer[2];
        private bool turnHeld;
        // Where the player was when it began (they're put back there).
        private bool playerSaved;
        private Vector3 playerPos;
        private Quaternion playerRot;
        private Vector3 centre;
        private float baseYaw;
        // The picture's size on the wall, and the part of it the frame covers (in the board's space).
        private Vector3 wallScale = Vector3.one;
        private Bounds boardBounds;
        private float timeLeft;
        private int lastTick;
        private TextMeshPro text;
        private HeadsetFade fade;
        private float fadeBefore;
        private Material backdrop;
        private GameObject backdropObject;
        private float backdropFrom;
        private float backdropTo;
        private float backdropStart;
        private float backdropSeconds;
        private GameObject space;
        private GameObject lamp;
        private bool holding;
        private Coroutine running;
        // While it's played: the materials swapped for ones drawn over the fade, and the player's things switched off.
        private readonly List<(Renderer renderer, Material[] materials)> swapped = new();
        private readonly List<Material> copies = new();
        private readonly List<(XRBaseInteractor interactor, bool select, bool hover)> interactors = new();
        private readonly List<(Behaviour behaviour, bool enabled)> stopped = new();
        private readonly List<(Animator animator, AnimatorUpdateMode mode)> handAnimators = new();
        private readonly List<(LineRenderer line, bool enabled)> hiddenLines = new();

        /// <summary>The puzzle is being played right now (the game is stopped for it).</summary>
        public bool IsPlaying => phase == Phase.Playing;

        private void Awake()
        {
            TryGetComponent(out hung);
            FindParts();
            ChooseStart();
            grips = new[]
            {
                Action(InputActionType.Button, "<XRController>{LeftHand}/gripPressed", "<XRController>{LeftHand}/{GripButton}"),
                Action(InputActionType.Button, "<XRController>{RightHand}/gripPressed", "<XRController>{RightHand}/{GripButton}"),
            };
            // The grip's own value too, in case a controller's grip button reads differently.
            gripAxes = new[]
            {
                Action(InputActionType.Value, "<XRController>{LeftHand}/grip", "<XRController>{LeftHand}/{Grip}"),
                Action(InputActionType.Value, "<XRController>{RightHand}/grip", "<XRController>{RightHand}/{Grip}"),
            };
            triggers = new[]
            {
                Action(InputActionType.Button, "<XRController>{LeftHand}/triggerPressed", "<XRController>{LeftHand}/{TriggerButton}"),
                Action(InputActionType.Button, "<XRController>{RightHand}/triggerPressed", "<XRController>{RightHand}/{TriggerButton}"),
            };
            moveStick = Action(InputActionType.Value, "<XRController>{LeftHand}/{Primary2DAxis}", "<XRController>{LeftHand}/thumbstick");
            turnStick = Action(InputActionType.Value, "<XRController>{RightHand}/{Primary2DAxis}", "<XRController>{RightHand}/thumbstick");
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            // Its sounds play while the game (and its sound) is stopped for the puzzle.
            voice.ignoreListenerPause = true;
            musicSpeaker = gameObject.AddComponent<AudioSource>();
            musicSpeaker.playOnAwake = false;
            musicSpeaker.spatialBlend = 0f;
            musicSpeaker.priority = 32;
            musicSpeaker.ignoreListenerPause = true;
        }

        private static InputAction Action(InputActionType type, params string[] bindings)
        {
            var action = new InputAction(type: type);
            foreach (var binding in bindings) action.AddBinding(binding);
            return action;
        }

        private IEnumerable<InputAction> Actions()
        {
            foreach (var a in grips) yield return a;
            foreach (var a in gripAxes) yield return a;
            foreach (var a in triggers) yield return a;
            yield return moveStick;
            yield return turnStick;
        }

        private void OnEnable()
        {
            foreach (var a in Actions()) a.Enable();
        }

        private void OnDisable()
        {
            if (phase == Phase.Playing) CleanUp(true);
            foreach (var a in Actions()) a.Disable();
        }

        private void OnDestroy()
        {
            foreach (var a in Actions()) a.Dispose();
        }

        private void FindParts()
        {
            var found = new SortedDictionary<int, Transform>();
            if (pieces.Length > 0)
            {
                for (int i = 0; i < pieces.Length; i++) if (pieces[i] != null) found[i] = pieces[i];
            }
            else
            {
                foreach (var t in GetComponentsInChildren<Transform>(true))
                {
                    var m = PieceName.Match(t.name);
                    if (m.Success) found[int.Parse(m.Groups[1].Value)] = t;
                }
            }
            if (complete == null)
            {
                foreach (var t in GetComponentsInChildren<Transform>(true))
                    if (t.name.EndsWith("_Complete")) complete = t.gameObject;
            }
            foreach (var t in found.Values)
            {
                var mf = t.GetComponent<MeshFilter>();
                all.Add(new Piece
                {
                    t = t,
                    slot = t.localPosition,
                    baseScale = t.localScale,
                    bounds = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one * 0.3f),
                });
            }
            board = all.Count > 0 ? all[0].t.parent : transform;
        }

        // Start Placed pieces stay, at random; the rest and the whole painting are hidden.
        private void ChooseStart()
        {
            var order = new List<Piece>(all);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            for (int i = 0; i < order.Count; i++)
            {
                var p = order[i];
                p.t.localPosition = p.slot;
                p.t.localRotation = Quaternion.identity;
                p.state = i < startPlaced ? PieceState.Placed : PieceState.Hidden;
                p.t.gameObject.SetActive(p.state == PieceState.Placed);
            }
            if (complete != null) complete.SetActive(false);
            if (glow != null) glow.SetActive(true);
            phase = Phase.Waiting;
        }

        // ---------- Starting it ----------

        // A hit from the player (a fist, a thing they hold or threw, a shot).
        private void OnCollisionEnter(Collision collision)
        {
            if (phase == Phase.Waiting && HeldThings.ByPlayer(collision)) TryBegin();
        }

        private void Update()
        {
            if (phase == Phase.Waiting)
            {
                // Fallen off the wall with the walls: only a picture now.
                if (hung != null && hung.Dropped)
                {
                    phase = Phase.Done;
                    if (glow != null) glow.SetActive(false);
                    return;
                }
                // Touch it and pull a trigger.
                for (int h = 0; h < 2; h++)
                {
                    if (!triggers[h].WasPressedThisFrame()) continue;
                    var hand = Hand(h);
                    if (hand != null && Touching(HandPoint(hand))) TryBegin();
                }
                return;
            }
            if (phase == Phase.Playing && holding) Play(Time.unscaledDeltaTime, Time.unscaledTime);
            if (backdrop != null)
            {
                float t = backdropSeconds > 0f ? Mathf.Clamp01((Time.unscaledTime - backdropStart) / backdropSeconds) : 1f;
                var c = backdrop.color;
                c.a = Mathf.Lerp(backdropFrom, backdropTo, t * t * (3f - 2f * t));
                backdrop.color = c;
            }
        }

        private bool Touching(Vector3 point)
        {
            var local = board.InverseTransformPoint(point);
            var size = transform.TryGetComponent(out BoxCollider box) ? box.size : new Vector3(1.05f, 1.05f, 0.05f);
            return Mathf.Abs(local.x) <= size.x * 0.5f + touchReach && Mathf.Abs(local.y) <= size.y * 0.5f + touchReach &&
                   local.z >= -0.05f && local.z <= size.z + touchReach;
        }

        private void TryBegin()
        {
            if (phase != Phase.Waiting || running != null || (hung != null && hung.Dropped)) return;
            if (PauseMenu.IsPaused || PauseMenu.IsHeld || Camera.main == null) return;
            if (ReviveCoins.Instance != null && ReviveCoins.Instance.IsFlipping) return;
            running = StartCoroutine(Run());
        }

        // ---------- The mini game ----------

        private IEnumerator Run()
        {
            phase = Phase.Playing;
            var eyes = Camera.main.transform;
            PauseMenu.Hold(this);
            PauseMenu.BlockOpening(this);
            holding = true;
            var player = Origin();
            if (player != null)
            {
                playerSaved = true;
                playerPos = player.transform.position;
                playerRot = player.transform.rotation;
            }
            FreezePlayer(true);
            Say(startSound);

            if (backdropMaterial != null) MakeBackdrop(eyes);
            else
            {
                fade = HeadsetFade.Find();
                if (fade != null)
                {
                    fadeBefore = fade.Alpha;
                    fade.FadeTo(spaceDim, 0.5f);
                }
            }
            centre = eyes.position;
            var flat = Vector3.ProjectOnPlane(eyes.forward, Vector3.up);
            if (flat.sqrMagnitude < 0.001f) flat = Vector3.ProjectOnPlane(eyes.up, Vector3.up);
            flat.Normalize();
            baseYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            if (spacePrefab != null) space = Instantiate(spacePrefab, centre, Quaternion.LookRotation(flat));
            lamp = MakeLamp(centre);
            OverFade(true);
            MakeRays();
            // A big picture is shrunk to Show Width while it's played.
            boardBounds = BoardBounds();
            float shrink = Mathf.Min(1f, showWidth / Mathf.Max(0.01f, boardBounds.size.x * board.lossyScale.x));
            text = MakeText(shrink);

            // The picture comes off the wall to float in front of the player's eyes, its front to them, its middle
            // (not where it hangs from) there.
            var wallPos = transform.position;
            var wallRot = transform.rotation;
            wallScale = transform.localScale;
            var showScale = wallScale * shrink;
            var middle = transform.InverseTransformPoint(board.TransformPoint(new Vector3(boardBounds.center.x, boardBounds.center.y, 0f)));
            var showRot = Quaternion.LookRotation(-flat, Vector3.up) * Quaternion.Inverse(Quaternion.Inverse(transform.rotation) * board.rotation);
            var showPos = centre + flat * showDistance + Vector3.down * 0.1f - showRot * Vector3.Scale(middle, showScale);
            yield return Move(wallPos, wallRot, wallScale, showPos, showRot, showScale, 0.7f);

            // The missing pieces fly out of it and off round the player, buzzing, half to each side.
            float now = Time.unscaledTime;
            int k = 0;
            foreach (var p in all)
            {
                if (p.state != PieceState.Hidden) continue;
                p.t.gameObject.SetActive(true);
                p.t.localPosition = p.slot;
                p.t.localRotation = Quaternion.identity;
                p.turn = 90f * Random.Range(0, 4);
                p.turnShown = p.turn;
                p.buzzPhase = Random.Range(0f, 50f);
                p.nextDodge = now + 2f;
                FromHere(p);
                p.state = PieceState.Flying;
                Spot(p, k % 2 == 0 ? -1f : 1f, Random.Range(50f, 160f), false);
                k++;
                StartBuzz(p);
            }

            timeLeft = timeLimit;
            lastTick = Mathf.CeilToInt(timeLeft);
            StartMusic();
            while (phase == Phase.Playing)
            {
                if (AllPlaced())
                {
                    musicSpeaker.Stop();
                    yield return Won(wallPos, wallRot);
                    break;
                }
                if (timeLeft <= 0f)
                {
                    musicSpeaker.Stop();
                    yield return Lost(wallPos, wallRot);
                    break;
                }
                yield return null;
            }
            running = null;
        }

        // One frame of play: the clock, the hands, the pieces.
        private void Play(float dt, float now)
        {
            // The song waits with the clock while the pause menu is open.
            if (PauseMenu.IsPaused && musicSpeaker.isPlaying) musicSpeaker.Pause();
            else if (!PauseMenu.IsPaused && !musicSpeaker.isPlaying && musicSpeaker.clip != null && musicSpeaker.time > 0f) musicSpeaker.UnPause();
            if (!PauseMenu.IsPaused && timeLeft > 0f && !AllPlaced())
            {
                timeLeft = Mathf.Max(0f, timeLeft - dt);
                int shown = Mathf.CeilToInt(timeLeft);
                if (shown != lastTick)
                {
                    lastTick = shown;
                    if (shown <= 10 && shown > 0) Say(tickSound, 0.6f);
                }
                if (text != null)
                {
                    text.text = shown.ToString();
                    text.color = shown <= 10 ? new Color(1f, 0.3f, 0.25f) : Color.white;
                }
            }

            for (int h = 0; h < 2; h++)
            {
                aimed[h] = null;
                var hand = Hand(h);
                if (hand == null)
                {
                    ShowRay(h, null);
                    continue;
                }
                var point = HandPoint(hand);
                bool gripping = Gripping(h);
                // A trigger press: caught by the action, or seen as the trigger going down since last frame.
                bool trigger = triggers[h].IsPressed();
                bool turnPressed = triggers[h].WasPressedThisFrame() || (trigger && !wasTrigger[h]);
                wasTrigger[h] = trigger;
                if (held[h] == null)
                {
                    // Close by, a gripping hand catches whatever it touches; from further off, the piece it points at
                    // comes to it: grip on it, or keep gripping and sweep the pointing over one (they fly about).
                    var near = Nearest(point);
                    var far = near == null ? Aimed(h, hand) : null;
                    aimed[h] = near ?? far;
                    if (near != null && gripping) Grab(h, near, point, false, now);
                    else if (far != null && gripping) Grab(h, far, point, true, now);
                    ShowRay(h, held[h] == null ? far : null);
                }
                else
                {
                    ShowRay(h, null);
                    if (!gripping) LetGo(held[h], now);
                    else if (turnPressed)
                    {
                        held[h].turn += 90f;
                        Say(turnSound, 0.5f, 1.5f);
                    }
                }
            }

            Walk(dt);
            TurnPlayer();
            // The pieces fly round the player wherever they go; the stars and the light go with them.
            var eyes = Camera.main != null ? Camera.main.transform.position : centre;
            centre = Vector3.Lerp(centre, eyes, 1f - Mathf.Exp(-4f * dt));
            if (space != null) space.transform.position = eyes;
            if (lamp != null) lamp.transform.position = eyes + Vector3.up * 0.35f;

            foreach (var p in all) Move(p, dt, now);
        }

        private void Move(Piece p, float dt, float now)
        {
            switch (p.state)
            {
                case PieceState.Flying:
                case PieceState.Floating:
                    Fly(p, dt, now);
                    break;
                case PieceState.Held:
                {
                    var hand = Hand(p.hand);
                    if (hand != null)
                    {
                        var to = HandPoint(hand) + p.grabOffset;
                        float t = p.pullSeconds > 0f ? Mathf.Clamp01((now - p.pullStart) / p.pullSeconds) : 1f;
                        p.t.position = t < 1f ? Vector3.Lerp(p.pullFrom, to, t * t * (3f - 2f * t)) : to;
                    }
                    p.t.localScale = Vector3.Lerp(p.t.localScale, p.baseScale, 1f - Mathf.Exp(-12f * dt));
                    p.turnShown = Mathf.MoveTowards(p.turnShown, p.turn, 900f * dt);
                    // Its back to the picture, turned by the trigger.
                    p.t.rotation = Quaternion.Slerp(p.t.rotation, board.rotation * Quaternion.Euler(0f, 0f, p.turnShown), 1f - Mathf.Exp(-22f * dt));
                    break;
                }
                case PieceState.Snapping:
                {
                    float t = Mathf.Clamp01((now - p.tweenStart) / p.tweenSeconds);
                    float e = t * t * (3f - 2f * t);
                    p.t.position = Vector3.Lerp(p.fromPos, board.TransformPoint(p.slot), e);
                    p.t.rotation = Quaternion.Slerp(p.fromRot, board.rotation, e);
                    if (t >= 1f)
                    {
                        p.t.localPosition = p.slot;
                        p.t.localRotation = Quaternion.identity;
                        p.t.localScale = p.baseScale;
                        p.state = PieceState.Placed;
                    }
                    break;
                }
            }
        }

        // Like a bug: darts to its spot (Flying), hovers there buzzing (Floating), then darts off to a new one; now and
        // then it zips away from a hand that comes close.
        private void Fly(Piece p, float dt, float now)
        {
            if (p.state == PieceState.Floating && now >= p.hoverUntil) NewSpot(p, false);
            if (now >= p.nextDodge && HandNear(p.t.position))
            {
                p.nextDodge = now + dodgeRest;
                if (Random.value < dodgeChance) NewSpot(p, true);
            }

            bool darting = p.state == PieceState.Flying;
            float top = p.topSpeed * (p.dodging ? 1.8f : 1f);
            float wantRel = darting ? Mathf.Clamp((p.toRel - p.rel) * 4f, -top, top) : 0f;
            float wantRadius = darting ? Mathf.Clamp((p.toRadius - p.radius) * 4f, -0.7f, 0.7f) : 0f;
            float wantHeight = darting ? Mathf.Clamp((p.toHeight - p.height) * 4f, -0.7f, 0.7f) : 0f;
            p.relV = Mathf.MoveTowards(p.relV, wantRel, top * 5f * dt);
            p.radiusV = Mathf.MoveTowards(p.radiusV, wantRadius, 3f * dt);
            p.heightV = Mathf.MoveTowards(p.heightV, wantHeight, 3f * dt);
            p.rel += p.relV * dt;
            p.radius += p.radiusV * dt;
            p.height += p.heightV * dt;
            KeepApart(p, dt);
            if (darting && Mathf.Abs(p.toRel - p.rel) < 3f && Mathf.Abs(p.toRadius - p.radius) < 0.03f && Mathf.Abs(p.toHeight - p.height) < 0.03f)
            {
                p.state = PieceState.Floating;
                p.dodging = false;
                p.rel = Mathf.DeltaAngle(0f, p.rel);
                p.toRel = p.rel;
                p.hoverUntil = now + Random.Range(hoverSeconds.x, Mathf.Max(hoverSeconds.x, hoverSeconds.y));
            }

            // A quick buzzing jitter, bigger while it hovers.
            float shake = darting ? 0.005f : 0.011f;
            float b = now + p.buzzPhase;
            var jitter = new Vector3(Mathf.Sin(b * 31f), Mathf.Sin(b * 23f + 1.7f), Mathf.Sin(b * 27f + 3.1f)) * shake;
            if (now < p.nopeUntil) jitter += board.right * (0.025f * Mathf.Sin((p.nopeUntil - now) * 40f));
            var at = Around(p.rel, p.radius, p.height) + jitter;
            p.t.position = at;
            // Its front to the player, banking into its turns.
            var look = centre - at;
            if (look.sqrMagnitude < 0.0001f) look = -board.forward;
            float roll = Mathf.Clamp(-p.relV * 0.22f, -32f, 32f) + 4f * Mathf.Sin(b * 19f);
            float pitch = Mathf.Clamp(p.heightV * 35f, -20f, 20f) + 3f * Mathf.Sin(b * 17f + 1f);
            var facing = Quaternion.LookRotation(look, Vector3.up) * Quaternion.Euler(pitch, 0f, roll) * Quaternion.Euler(0f, 0f, p.turn);
            p.t.rotation = Quaternion.Slerp(p.t.rotation, facing, 1f - Mathf.Exp(-12f * dt));
            if (p.buzz != null) p.buzz.pitch = Mathf.Lerp(p.buzz.pitch, (p.dodging ? 1.4f : 1f) * BuzzPitch(p), 1f - Mathf.Exp(-6f * dt));
            // A little bigger while a hand can catch it (close by, or pointing at it).
            bool hot = aimed[0] == p || aimed[1] == p;
            p.t.localScale = Vector3.Lerp(p.t.localScale, p.baseScale * (hot ? 1.12f : 1f), 1f - Mathf.Exp(-14f * dt));
        }

        // A new spot to dart to: mostly on its own side of the player, sometimes the other side (round behind them);
        // dodging, further round, away from the picture and faster.
        private void NewSpot(Piece p, bool dodge)
        {
            float from = Mathf.DeltaAngle(0f, p.rel);
            float side = from >= 0f ? 1f : -1f;
            p.state = PieceState.Flying;
            if (dodge)
            {
                p.rel = from;
                p.toRel = from + side * Random.Range(55f, 95f);
                p.toRadius = Mathf.Clamp(p.radius + Random.Range(-0.1f, 0.2f), 0.45f, 0.9f);
                p.toHeight = Mathf.Clamp(p.height + Random.Range(-0.2f, 0.2f), -0.65f, 0.15f);
                p.topSpeed = Random.Range(flySpeed.x, Mathf.Max(flySpeed.x, flySpeed.y));
                p.dodging = true;
                return;
            }
            Spot(p, Random.value < 0.25f ? -side : side, Random.Range(48f, 168f), false);
        }

        // Darts to 'away' degrees from straight ahead on a side (-1 left, +1 right), never across the front of the
        // picture: to the other side it goes round behind the player.
        private void Spot(Piece p, float side, float away, bool dodge)
        {
            float from = Mathf.DeltaAngle(0f, p.rel);
            p.rel = from;
            // Of a few spots near the one asked for, the one furthest from the other pieces (and where they're
            // going), so they don't bunch up on top of each other.
            float bestAway = away, bestRadius = 0.65f, bestHeight = -0.25f, bestRoom = -1f;
            for (int i = 0; i < 6; i++)
            {
                float a = i == 0 ? away : Mathf.Clamp(away + Random.Range(-45f, 45f), 48f, 170f);
                float r = Random.Range(0.45f, 0.85f);
                float h = Random.Range(-0.6f, 0.1f);
                float room = Room(p, Around(side * a, r, h));
                if (room <= bestRoom) continue;
                bestRoom = room;
                bestAway = a;
                bestRadius = r;
                bestHeight = h;
                if (room > 0.4f) break;
            }
            float to = side * bestAway;
            float delta = to - from;
            if (Mathf.Abs(from) > 0.01f && Mathf.Sign(to) != Mathf.Sign(from)) delta -= 360f * Mathf.Sign(delta);
            p.toRel = from + delta;
            p.toRadius = bestRadius;
            p.toHeight = bestHeight;
            p.topSpeed = Random.Range(flySpeed.x, Mathf.Max(flySpeed.x, flySpeed.y));
            p.dodging = dodge;
        }

        // How far a spot is from the nearest other flying piece, or the spot it's heading for.
        private float Room(Piece p, Vector3 spot)
        {
            float room = 9f;
            foreach (var other in all)
            {
                if (other == p || (other.state != PieceState.Flying && other.state != PieceState.Floating)) continue;
                room = Mathf.Min(room, (other.t.position - spot).magnitude, (Around(other.toRel, other.toRadius, other.toHeight) - spot).magnitude);
            }
            return room;
        }

        // Two pieces flying close slide apart round the player, so they don't sit on top of each other.
        private void KeepApart(Piece p, float dt)
        {
            foreach (var other in all)
            {
                if (other == p || (other.state != PieceState.Flying && other.state != PieceState.Floating)) continue;
                float apart = Mathf.DeltaAngle(other.rel, p.rel);
                if (Mathf.Abs(apart) > 28f || Mathf.Abs(other.height - p.height) > 0.28f || Mathf.Abs(other.radius - p.radius) > 0.3f) continue;
                float push = (28f - Mathf.Abs(apart)) * 2.5f * dt;
                float way = Mathf.Abs(apart) < 0.5f ? (p.buzzPhase > other.buzzPhase ? 1f : -1f) : Mathf.Sign(apart);
                // Never pushed out in front of the picture (it only starts there, flying out of it).
                bool aside = Mathf.Abs(Mathf.DeltaAngle(0f, p.rel)) >= 45f;
                if (aside && Mathf.Abs(Mathf.DeltaAngle(0f, p.rel + way * push)) < 45f) continue;
                p.rel += way * push;
                if (p.state == PieceState.Floating) p.toRel = p.rel;
            }
        }

        // Where it is now, round the player (from straight ahead when the puzzle began).
        private void FromHere(Piece p)
        {
            var v = p.t.position - centre;
            p.rel = Mathf.DeltaAngle(baseYaw, Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg);
            p.radius = Mathf.Clamp(new Vector2(v.x, v.z).magnitude, 0.3f, 1.3f);
            p.height = v.y;
            p.relV = p.radiusV = p.heightV = 0f;
            p.toRel = p.rel;
            p.toRadius = p.radius;
            p.toHeight = p.height;
        }

        private Vector3 Around(float rel, float radius, float height)
        {
            return centre + Quaternion.Euler(0f, baseYaw + rel, 0f) * Vector3.forward * radius + Vector3.up * height;
        }

        private bool HandNear(Vector3 at)
        {
            for (int h = 0; h < 2; h++)
            {
                var hand = Hand(h);
                if (hand != null && held[h] == null && (HandPoint(hand) - at).sqrMagnitude < 0.22f * 0.22f) return true;
            }
            return false;
        }

        private void StartBuzz(Piece p)
        {
            if (buzzSound == null) return;
            if (p.buzz == null)
            {
                p.buzz = p.t.gameObject.AddComponent<AudioSource>();
                p.buzz.clip = buzzSound;
                p.buzz.loop = true;
                p.buzz.playOnAwake = false;
                p.buzz.spatialBlend = 1f;
                p.buzz.minDistance = 0.15f;
                p.buzz.maxDistance = 3f;
                p.buzz.rolloffMode = AudioRolloffMode.Logarithmic;
                p.buzz.dopplerLevel = 0f;
                // It buzzes while the game (and its sound) is stopped for the puzzle.
                p.buzz.ignoreListenerPause = true;
            }
            p.buzz.volume = 0.22f * volume;
            p.buzz.pitch = BuzzPitch(p);
            p.buzz.time = Random.Range(0f, Mathf.Max(0f, buzzSound.length - 0.1f));
            p.buzz.Play();
        }

        private static void StopBuzz(Piece p)
        {
            if (p.buzz != null) p.buzz.Stop();
        }

        // Each piece its own buzz: a little higher or lower.
        private static float BuzzPitch(Piece p) => 1.35f + Mathf.Repeat(p.buzzPhase, 1f) * 0.5f;

        private void StartTween(Piece p, PieceState state, float seconds, float now)
        {
            p.fromPos = p.t.position;
            p.fromRot = p.t.rotation;
            p.tweenStart = now;
            p.tweenSeconds = Mathf.Max(0.01f, seconds);
            p.state = state;
        }

        private static bool Free(Piece p) => p.state == PieceState.Floating || p.state == PieceState.Flying;

        // The flying piece a hand touches (within Grab Reach of it), the closest.
        private Piece Nearest(Vector3 point)
        {
            Piece best = null;
            float bestDistance = grabReach;
            foreach (var p in all)
            {
                if (!Free(p) || p.hand >= 0) continue;
                // In metres (a shrunk picture's pieces are shrunk too).
                float d = Vector3.Distance(point, p.t.TransformPoint(p.bounds.ClosestPoint(p.t.InverseTransformPoint(point))));
                if (d > bestDistance) continue;
                bestDistance = d;
                best = p;
            }
            return best;
        }

        // The flying piece a hand points at (the one nearest its line), within Far Reach.
        private Piece Aimed(int h, Transform hand)
        {
            if (farReach <= 0f) return null;
            var (from, dir) = Aim(hand);
            Piece best = null;
            float bestScore = float.MaxValue;
            foreach (var p in all)
            {
                if (!Free(p) || p.hand >= 0) continue;
                var v = p.t.TransformPoint(p.bounds.center) - from;
                float along = Vector3.Dot(v, dir);
                if (along < 0.15f || along > farReach) continue;
                float off = (v - dir * along).magnitude;
                if (off > 0.12f + along * farAim) continue;
                float score = off / along;
                if (score >= bestScore) continue;
                bestScore = score;
                best = p;
            }
            return best;
        }

        private void Grab(int h, Piece p, Vector3 point, bool fromAfar, float now)
        {
            if (held[h] != null || p == null) return;
            held[h] = p;
            p.hand = h;
            if (fromAfar)
            {
                // It flies into the hand, held just in front of it.
                var hand = Hand(h);
                p.grabOffset = hand != null ? Aim(hand).dir * 0.12f : Vector3.zero;
                p.pullFrom = p.t.position;
                p.pullStart = now;
                p.pullSeconds = 0.2f;
            }
            else
            {
                p.grabOffset = p.t.position - point;
                p.pullSeconds = 0f;
            }
            // It turns to face the way the picture does, keeping its own quarter turn; caught, it stops buzzing.
            p.turnShown = p.turn;
            p.state = PieceState.Held;
            p.dodging = false;
            StopBuzz(p);
            Say(turnSound, 0.4f, 1.1f);
        }

        private bool Gripping(int h) => grips[h].IsPressed() || gripAxes[h].ReadValue<float>() > 0.55f;

        // Where a hand points from, and which way: its far caster's origin (the controller's own ray), or the controller.
        private static (Vector3 from, Vector3 dir) Aim(Transform hand)
        {
            var caster = hand.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.Casters.CurveInteractionCaster>();
            var origin = caster != null && caster.castOrigin != null ? caster.castOrigin : hand;
            return (origin.position, origin.forward);
        }

        // The beams, from each hand to the piece it points at, drawn after the backdrop.
        private void MakeRays()
        {
            if (rayMaterial == null) return;
            var look = new Material(rayMaterial) { renderQueue = ParticleQueue };
            copies.Add(look);
            for (int h = 0; h < 2; h++)
            {
                var go = new GameObject("Grab Beam");
                go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.widthCurve = AnimationCurve.Linear(0f, 0.008f, 1f, 0.004f);
                line.startColor = new Color(1f, 0.85f, 0.45f, 0.9f);
                line.endColor = new Color(1f, 0.75f, 0.3f, 0.35f);
                line.sharedMaterial = look;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.enabled = false;
                rays[h] = line;
            }
        }

        private void ShowRay(int h, Piece p)
        {
            var line = rays[h];
            if (line == null) return;
            var hand = Hand(h);
            if (p == null || hand == null)
            {
                line.enabled = false;
                return;
            }
            line.SetPosition(0, Aim(hand).from);
            line.SetPosition(1, p.t.TransformPoint(p.bounds.center));
            line.enabled = true;
        }

        // The left stick walks the player about the space (round where they were, Move Range at most).
        private void Walk(float dt)
        {
            var player = Origin();
            var eyes = Camera.main;
            if (player == null || eyes == null || moveSpeed <= 0f || !playerSaved) return;
            var stick = moveStick.ReadValue<Vector2>();
            if (stick.sqrMagnitude < 0.04f) return;
            var forward = Vector3.ProjectOnPlane(eyes.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f) return;
            forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward);
            var next = player.transform.position + (forward * stick.y + right * stick.x) * (moveSpeed * dt);
            var off = next - playerPos;
            off.y = 0f;
            if (off.magnitude > moveRange)
            {
                off = off.normalized * moveRange;
                next = new Vector3(playerPos.x + off.x, next.y, playerPos.z + off.z);
            }
            player.transform.position = next;
        }

        // A flick of the right stick turns the player, round their head.
        private void TurnPlayer()
        {
            var player = Origin();
            var eyes = Camera.main;
            if (player == null || eyes == null || turnAngle <= 0f) return;
            float x = turnStick.ReadValue<Vector2>().x;
            if (Mathf.Abs(x) < 0.3f)
            {
                turnHeld = false;
                return;
            }
            if (turnHeld || Mathf.Abs(x) < 0.7f) return;
            turnHeld = true;
            player.transform.RotateAround(eyes.transform.position, Vector3.up, Mathf.Sign(x) * turnAngle);
        }

        // Back where they were when it began.
        private void PutPlayerBack()
        {
            if (!playerSaved) return;
            playerSaved = false;
            var player = Origin();
            if (player != null) player.transform.SetPositionAndRotation(playerPos, playerRot);
        }

        // Let go: by its own spot the right way round it clicks in; anywhere else it floats on from there.
        private void LetGo(Piece p, float now)
        {
            if (p.hand >= 0 && p.hand < 2 && held[p.hand] == p) held[p.hand] = null;
            p.hand = -1;
            var local = board.InverseTransformPoint(p.t.position);
            // In metres, so a shrunk picture is as forgiving.
            var offSlot = (local - p.slot) * board.lossyScale.x;
            bool near = new Vector2(offSlot.x, offSlot.y).magnitude <= snapDistance && Mathf.Abs(offSlot.z) <= 0.3f;
            bool rightWay = Mathf.Abs(Mathf.DeltaAngle(p.turn, 0f)) < 1f;
            if (near && rightWay)
            {
                StartTween(p, PieceState.Snapping, 0.12f, now);
                Say(snapSound, 1f);
                return;
            }
            // Let go anywhere else: it hovers there a moment (to be caught again), then flies off.
            FromHere(p);
            p.state = PieceState.Floating;
            p.hoverUntil = now + 0.9f;
            p.nextDodge = now + 1.5f;
            StartBuzz(p);
            if (near)
            {
                // Its spot, but the wrong way round: a little shake.
                p.nopeUntil = now + 0.35f;
                Say(wrongSound, 0.7f);
            }
        }

        private bool AllPlaced()
        {
            foreach (var p in all) if (p.state != PieceState.Placed) return false;
            return all.Count > 0;
        }

        private IEnumerator Won(Vector3 wallPos, Quaternion wallRot)
        {
            phase = Phase.Done;
            // The whole painting takes the pieces' place; with none (Complete empty) the finished pieces stay as it.
            if (complete != null)
            {
                foreach (var p in all) p.t.gameObject.SetActive(false);
                complete.SetActive(true);
            }
            if (text != null)
            {
                text.text = "COMPLETE!";
                text.color = new Color(1f, 0.85f, 0.4f);
            }
            Say(winSound, 1f);
            Fireworks();
            yield return Wait(celebrateSeconds);
            PutPlayerBack();
            FadeBackdrop(0f, 0.5f);
            yield return Move(transform.position, transform.rotation, transform.localScale, wallPos, wallRot, wallScale, 0.5f);
            CleanUp(false);
            if (glow != null) glow.SetActive(false);
            StartCoroutine(Reward());
        }

        private IEnumerator Lost(Vector3 wallPos, Quaternion wallRot)
        {
            phase = Phase.Broken;
            if (text != null)
            {
                text.text = "TIME'S UP!";
                text.color = new Color(1f, 0.3f, 0.25f);
            }
            Say(wrongSound, 1f, 0.8f);
            // The loose pieces stay where they float (they fall once time runs again); the picture goes back.
            var loose = new List<Piece>();
            foreach (var p in all)
            {
                if (p.state == PieceState.Placed || p.state == PieceState.Hidden) continue;
                p.t.SetParent(null, true);
                StopBuzz(p);
                loose.Add(p);
            }
            for (int h = 0; h < 2; h++) held[h] = null;
            yield return Wait(0.8f);
            PutPlayerBack();
            FadeBackdrop(0f, 0.45f);
            yield return Move(transform.position, transform.rotation, transform.localScale, wallPos, wallRot, wallScale, 0.45f);
            CleanUp(false);
            if (glow != null) glow.SetActive(false);
            Break(loose);
        }

        private IEnumerator Move(Vector3 fromPos, Quaternion fromRot, Vector3 fromScale, Vector3 toPos, Quaternion toRot,
            Vector3 toScale, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float e = t / seconds;
                e = e * e * (3f - 2f * e);
                transform.SetPositionAndRotation(Vector3.Lerp(fromPos, toPos, e), Quaternion.Slerp(fromRot, toRot, e));
                transform.localScale = Vector3.Lerp(fromScale, toScale, e);
                yield return null;
            }
            transform.SetPositionAndRotation(toPos, toRot);
            transform.localScale = toScale;
        }

        // The picture on its board, in the board's space: the frame's own mesh, or else where the pieces sit.
        private Bounds BoardBounds()
        {
            if (board.TryGetComponent(out MeshFilter frame) && frame.sharedMesh != null) return frame.sharedMesh.bounds;
            var b = new Bounds(all.Count > 0 ? all[0].slot : Vector3.zero, Vector3.zero);
            foreach (var p in all)
            {
                b.Encapsulate(p.slot + p.bounds.min);
                b.Encapsulate(p.slot + p.bounds.max);
            }
            return b;
        }

        private static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
        }

        // ---------- Done / time's up ----------

        // Fireworks on both sides behind the picture, bursting round it (not hidden behind it), drawn after the
        // backdrop like the rest of the puzzle.
        private void Fireworks()
        {
            if (winFireworks == null) return;
            var back = Vector3.ProjectOnPlane(-board.forward, Vector3.up).normalized;
            var side = Vector3.Cross(Vector3.up, back).normalized;
            foreach (float way in new[] { -1f, 1f })
            {
                var show = Instantiate(winFireworks, board.position + back * 1.8f + side * (2.2f * way) + Vector3.down * 0.9f, Quaternion.identity);
                foreach (var r in show.GetComponentsInChildren<Renderer>(true))
                {
                    var original = r.sharedMaterials;
                    var over = new Material[original.Length];
                    for (int i = 0; i < original.Length; i++)
                    {
                        if (original[i] == null) continue;
                        over[i] = new Material(original[i]) { renderQueue = ParticleQueue };
                        copies.Add(over[i]);
                    }
                    r.sharedMaterials = over;
                }
                // Its time runs while the game's is stopped; it's gone a few seconds after the game goes on.
                Destroy(show, 5f);
            }
        }

        // Candies pop out of the picture one after another, from all over it; the Candy Helmet pulls them in.
        private IEnumerator Reward()
        {
            if (helmetSeconds > 0f) PlayerBuffs.Give(Buff.CandyHelmet, helmetSeconds);
            var stuff = SpawnedStuff();
            var front = board.forward;
            // From across the picture (at least 60 cm square), clear of the frame and of each other (a candy breaks if
            // it's knocked hard).
            float wide = Mathf.Max(0.3f, 0.6f * boardBounds.extents.x), high = Mathf.Max(0.3f, 0.6f * boardBounds.extents.y);
            for (int i = 0; i < rewardCandies && candyPrefabs.Length > 0; i++)
            {
                var prefab = candyPrefabs[Random.Range(0, candyPrefabs.Length)];
                if (prefab == null) continue;
                var at = board.TransformPoint(new Vector3(boardBounds.center.x + Random.Range(-wide, wide),
                    boardBounds.center.y + Random.Range(-high, high), 0.22f));
                var candy = Instantiate(prefab, at, Random.rotation, stuff);
                if (candy.TryGetComponent(out Rigidbody body))
                    body.linearVelocity = front * Random.Range(1.2f, 2f) + Vector3.up * Random.Range(0.6f, 1.4f) + Random.insideUnitSphere * 0.4f;
                yield return new WaitForSeconds(0.06f);
            }
        }

        // The picture breaks: its pieces fly off, the frame drops, and the bugs pour out.
        private void Break(List<Piece> loose)
        {
            var stuff = SpawnedStuff();
            var at = board.TransformPoint(new Vector3(0f, 0f, 0.1f));
            foreach (var effect in breakEffects) if (effect != null) EffectPool.Play(effect, at, 1f);
            Say(breakSound, 1f);
            foreach (var p in all)
            {
                if (!p.t.gameObject.activeSelf) continue;
                bool inFrame = p.state == PieceState.Placed || p.state == PieceState.Snapping;
                if (inFrame) p.t.SetParent(null, true);
                var body = Debris(p, stuff);
                if (inFrame)
                    body.linearVelocity = board.forward * Random.Range(1.5f, 3f) + Vector3.up * Random.Range(0.5f, 2f) + Random.insideUnitSphere;
                else
                    body.linearVelocity = Random.insideUnitSphere * 0.4f;
                body.angularVelocity = Random.insideUnitSphere * 6f;
            }
            if (hung != null) hung.Drop();
            if (bugPrefab != null && bugCount > 0) StartCoroutine(Bugs(stuff));
        }

        private Rigidbody Debris(Piece p, Transform parent)
        {
            p.t.SetParent(parent, true);
            var box = p.t.gameObject.AddComponent<BoxCollider>();
            box.center = p.bounds.center;
            box.size = Vector3.Max(p.bounds.size, new Vector3(0.02f, 0.02f, 0.02f));
            var body = p.t.gameObject.AddComponent<Rigidbody>();
            body.mass = 0.3f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            Destroy(p.t.gameObject, debrisSeconds + Random.Range(0f, 1.5f));
            return body;
        }

        private IEnumerator Bugs(Transform stuff)
        {
            var front = Vector3.ProjectOnPlane(board.forward, Vector3.up).normalized;
            var below = board.position + front * 0.9f;
            for (int i = 0; i < bugCount; i++)
            {
                var spot = below + Quaternion.Euler(0f, Random.Range(-70f, 70f), 0f) * front * Random.Range(0f, 0.8f);
                if (NavMesh.SamplePosition(spot, out var hit, 3f, NavMesh.AllAreas)) spot = hit.position;
                if (breakEffects.Length > 0 && breakEffects[0] != null) EffectPool.Play(breakEffects[0], spot + Vector3.up * 0.1f, 0.35f);
                var bug = Instantiate(bugPrefab, spot, Quaternion.LookRotation(front), stuff);
                if (bug.TryGetComponent(out SpiderBrain brain)) brain.ComeAtOnce();
                yield return new WaitForSeconds(0.07f);
            }
        }

        // ---------- The space round the player ----------

        // Back to the game: the room comes back, the player's hands work again, time runs.
        private void CleanUp(bool hurry)
        {
            foreach (var p in all) StopBuzz(p);
            PutPlayerBack();
            for (int h = 0; h < 2; h++)
            {
                if (rays[h] != null) Destroy(rays[h].gameObject);
                rays[h] = null;
                aimed[h] = null;
            }
            OverFade(false);
            if (backdropObject != null) Destroy(backdropObject);
            if (backdrop != null) Destroy(backdrop);
            backdrop = null;
            if (space != null) Destroy(space);
            if (lamp != null) Destroy(lamp);
            if (text != null) Destroy(text.gameObject);
            if (fade != null) fade.FadeTo(fadeBefore, hurry ? 0f : 0.4f);
            fade = null;
            FreezePlayer(false);
            if (holding)
            {
                holding = false;
                PauseMenu.AllowOpening(this);
                PauseMenu.Release(this);
            }
        }

        // The picture, the pieces and the player's hands are drawn after the backdrop, so no wall hides them.
        private void OverFade(bool on)
        {
            if (!on)
            {
                foreach (var (renderer, materials) in swapped)
                    if (renderer != null) renderer.sharedMaterials = materials;
                swapped.Clear();
                foreach (var m in copies) if (m != null) Destroy(m);
                copies.Clear();
                return;
            }
            var renderers = new List<Renderer>(GetComponentsInChildren<Renderer>(true));
            // The stars too: a material's render queue can be reset as the game loads, so they're set here.
            if (space != null) renderers.AddRange(space.GetComponentsInChildren<Renderer>(true));
            for (int h = 0; h < 2; h++)
            {
                var hand = Hand(h);
                if (hand == null) continue;
                foreach (var r in hand.GetComponentsInChildren<Renderer>(true))
                    if (!(r is LineRenderer) && !(r is ParticleSystemRenderer)) renderers.Add(r);
            }
            foreach (var r in renderers)
            {
                var original = r.sharedMaterials;
                var over = new Material[original.Length];
                for (int i = 0; i < original.Length; i++)
                {
                    if (original[i] == null) continue;
                    over[i] = new Material(original[i]) { renderQueue = r is ParticleSystemRenderer ? ParticleQueue : PictureQueue };
                    copies.Add(over[i]);
                }
                swapped.Add((r, original));
                r.sharedMaterials = over;
            }
        }

        // The player's hands only do the puzzle: they can't grab or use anything else, and the game's own walking,
        // turning and teleporting are off (the puzzle walks and turns them itself, Walk and Turn Player).
        private void FreezePlayer(bool freeze)
        {
            if (!freeze)
            {
                foreach (var (interactor, select, hover) in interactors)
                {
                    if (interactor == null) continue;
                    interactor.allowSelect = select;
                    interactor.allowHover = hover;
                }
                interactors.Clear();
                foreach (var (behaviour, enabled) in stopped) if (behaviour != null) behaviour.enabled = enabled;
                stopped.Clear();
                foreach (var (line, enabled) in hiddenLines) if (line != null) line.enabled = enabled;
                hiddenLines.Clear();
                foreach (var (animator, mode) in handAnimators) if (animator != null) animator.updateMode = mode;
                handAnimators.Clear();
                for (int h = 0; h < 2; h++) held[h] = null;
                return;
            }
            var player = Origin();
            if (player == null) return;
            foreach (var interactor in player.GetComponentsInChildren<XRBaseInteractor>(true))
            {
                interactors.Add((interactor, interactor.allowSelect, interactor.allowHover));
                interactor.allowSelect = false;
                interactor.allowHover = false;
            }
            foreach (var provider in player.GetComponentsInChildren<LocomotionProvider>(true))
            {
                stopped.Add((provider, provider.enabled));
                provider.enabled = false;
            }
            foreach (var bump in player.GetComponentsInChildren<FistBump>(true))
            {
                stopped.Add((bump, bump.enabled));
                bump.enabled = false;
            }
            // The hands' own rays and teleport arc (the puzzle draws its own beam).
            foreach (var visual in player.GetComponentsInChildren<Behaviour>(true))
            {
                if (visual == null) continue;
                string kind = visual.GetType().Name;
                if (!kind.Contains("LineVisual") && !kind.Contains("CurveVisual")) continue;
                stopped.Add((visual, visual.enabled));
                visual.enabled = false;
            }
            foreach (var line in player.GetComponentsInChildren<LineRenderer>(true))
            {
                hiddenLines.Add((line, line.enabled));
                line.enabled = false;
            }
            // The hands still close and open while time is stopped.
            for (int h = 0; h < 2; h++)
            {
                var hand = Hand(h);
                if (hand == null) continue;
                foreach (var animator in hand.GetComponentsInChildren<Animator>(true))
                {
                    handAnimators.Add((animator, animator.updateMode));
                    animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                }
            }
        }

        // The dark round the player's eyes (it follows the head), fading in.
        private void MakeBackdrop(Transform eyes)
        {
            backdropObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            backdropObject.name = "Picture Puzzle Backdrop";
            Destroy(backdropObject.GetComponent<Collider>());
            backdropObject.transform.SetParent(eyes, false);
            backdropObject.transform.localScale = Vector3.one * 0.6f;
            var r = backdropObject.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            backdrop = new Material(backdropMaterial) { renderQueue = BackdropQueue };
            var c = backdrop.color;
            c.a = 0f;
            backdrop.color = c;
            r.sharedMaterial = backdrop;
            FadeBackdrop(spaceDim, 0.5f);
        }

        private void FadeBackdrop(float to, float seconds)
        {
            if (backdrop == null) return;
            backdropFrom = backdrop.color.a;
            backdropTo = to;
            backdropStart = Time.unscaledTime;
            backdropSeconds = seconds;
        }

        // A soft light on the picture and pieces while the room is dark.
        private static GameObject MakeLamp(Vector3 eyes)
        {
            var go = new GameObject("Picture Puzzle Light");
            go.transform.position = eyes + Vector3.up * 0.35f;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 3f;
            light.intensity = 1.6f;
            light.color = new Color(1f, 0.93f, 0.85f);
            light.shadows = LightShadows.None;
            return go;
        }

        // The clock just above the picture; the same size however much the picture is shrunk.
        private TextMeshPro MakeText(float shrink)
        {
            var font = TMP_Settings.defaultFontAsset;
            if (font == null) return null;
            var go = new GameObject("Puzzle Clock");
            go.transform.SetParent(board, false);
            go.transform.localPosition = new Vector3(boardBounds.center.x, boardBounds.max.y + 0.075f / shrink, 0.06f / shrink);
            // TextMeshPro reads from its back: turned round to face the player in front of the picture.
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            go.transform.localScale = Vector3.one * (0.1f / shrink);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = font;
            var look = Resources.Load<Material>(TextMaterial);
            if (look != null) tmp.fontSharedMaterial = look;
            tmp.fontSize = 10f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Bottom;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(12f, 3f);
            tmp.rectTransform.pivot = new Vector2(0.5f, 0f);
            tmp.text = Mathf.CeilToInt(timeLimit).ToString();
            var over = tmp.fontMaterial;
            over.renderQueue = TextQueue;
            copies.Add(over);
            return tmp;
        }

        // One of the songs nearest the Time Limit in length (within 5 s of the nearest), at random, started so that it ends
        // as the time runs out (a shorter one just starts with the clock).
        private void StartMusic()
        {
            musicSpeaker.Stop();
            musicSpeaker.clip = null;
            if (music == null || music.Length == 0) return;
            float nearest = float.MaxValue;
            foreach (var song in music)
                if (song != null) nearest = Mathf.Min(nearest, Mathf.Abs(song.length - timeLimit));
            var fits = new List<AudioClip>();
            foreach (var song in music)
                if (song != null && Mathf.Abs(song.length - timeLimit) <= nearest + 5f) fits.Add(song);
            if (fits.Count == 0) return;
            var pick = fits[Random.Range(0, fits.Count)];
            musicSpeaker.clip = pick;
            musicSpeaker.volume = musicVolume;
            musicSpeaker.Play();
            // (Set once playing: a streamed song ignores it before.)
            if (pick.length - timeLimit > 0.05f) musicSpeaker.time = pick.length - timeLimit;
        }

        private void Say(AudioClip clip, float loudness = 1f, float pitch = 1f)
        {
            if (clip == null || voice == null) return;
            voice.pitch = pitch;
            voice.PlayOneShot(clip, loudness * volume);
        }

        private XROrigin Origin()
        {
            if (origin == null) origin = FindAnyObjectByType<XROrigin>();
            return origin;
        }

        // The left (0) or right (1) controller.
        private Transform Hand(int h)
        {
            if (hands[h] != null) return hands[h].gameObject.activeInHierarchy ? hands[h] : null;
            var player = Origin();
            if (player == null) return null;
            string want = h == 0 ? "Left Controller" : "Right Controller";
            foreach (var t in player.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != want) continue;
                hands[h] = t;
                break;
            }
            return hands[h] != null && hands[h].gameObject.activeInHierarchy ? hands[h] : null;
        }

        // Where the hand grips: its Direct Interactor's sphere, or the controller.
        private static Vector3 HandPoint(Transform hand)
        {
            var direct = hand.Find("Direct Interactor");
            if (direct != null && direct.TryGetComponent(out SphereCollider sphere)) return direct.TransformPoint(sphere.center);
            return hand.position;
        }

        private static Transform SpawnedStuff()
        {
            var holder = FsmVariables.GlobalVariables.FindFsmGameObject("CurrentlySpawnedStuffs");
            return holder != null && holder.Value != null ? holder.Value.transform : null;
        }
    }
}
