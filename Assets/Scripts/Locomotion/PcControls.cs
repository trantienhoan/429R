#if UNITY_EDITOR
using System.Collections.Generic;
using Game.Combat;
using Game.Inventory;
using Game.Menus;
using Game.Shopping;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Game.Locomotion
{
    /// <summary>
    /// PC controls for testing in the Editor without a headset (user, 2026-11-20: "Build PC controller in Editor test
    /// only, don't build it in PCVR version"): the whole file is Editor-only, so no build has it. It puts itself on the
    /// XR Origin when Play starts and switches on only when no headset is active.
    /// It plays the VR hands instead of adding PC attacks: the hands are moved in front of the view and their grip pressed,
    /// so the game's own rules decide everything (a punch's damage from how fast the fist moves, normal/hard/smash, flying
    /// punches, Hot Hands, the hit window, throws from how fast the hand lets go).
    /// Keys (user, 2026-11-20): WASD walk (the thumbstick's move, so Move Speed and slows count), mouse look (at once, no
    /// click needed: the Game view takes the mouse when Play starts and when it's clicked back into), Space jump.
    /// Left/right mouse button: the left/right hand punches (a jab), or swings what it holds. With Shift: the hard punch,
    /// a hook (the "swing" punch), or a hard swing; with Ctrl: an uppercut, or an upward swing. Flying punches come as in VR
    /// once Fist Size unlocks them. F/G: the left/right hand grabs what the crosshair is on and holds it (far things are
    /// pulled in, as in VR); again: drops it. Q/E: the left/right hand throws what it holds at the crosshair (hold longer,
    /// throw harder). Esc: the pause menu; I: the inventory; B: the shop. With a menu open the mouse is free: the right
    /// hand points where it is, and a left click presses what it points at.
    /// Tools > 429 Game > Player > PC Controls (Editor) switches it off and on.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    [DisallowMultipleComponent]
    public class PcControls : MonoBehaviour
    {
        public const string PrefsKey = "429 Game.PcControls";

        [Header("Look, walk, jump")]
        [Tooltip("Degrees the view turns for each pixel the mouse moves.")]
        [SerializeField] private float lookSpeed = 0.12f;
        [SerializeField] private float maxPitch = 80f;
        [Tooltip("Eye height above the floor, in metres (the pause menu's Height and the Green Boong still add to it).")]
        [SerializeField] private float eyeHeight = 1.6f;
        [SerializeField] private float fieldOfView = 75f;
        [Tooltip("How high a jump goes, in metres.")]
        [SerializeField] private float jumpHeight = 0.6f;
        [SerializeField] private float gravity = 14f;

        [Header("Punches")]
        [Tooltip("Where the left hand rests, from the eyes, in metres (x right, y up, z ahead). The right one is its mirror.")]
        [SerializeField] private Vector3 restLeft = new(-0.2f, -0.38f, 0.3f);
        [Tooltip("How far a jab reaches at least, in metres. Aimed at something farther, it reaches out to it, up to Max " +
                 "Punch Reach (bugs on the floor: a VR player bends down, here the arm stretches).")]
        [SerializeField] private float punchReach = 0.5f;
        [SerializeField] private float maxPunchReach = 1.3f;
        [Tooltip("The fist's speed in a jab (a mouse click), in metres per second: just past the flying jab's with Boxing " +
                 "gloves (2.6), so their jabs fly once unlocked (other gloves need 3.2 for that).")]
        [SerializeField] private float jabSpeed = 2.8f;
        [Tooltip("The fist's speed in a hook (Shift + click: the hard punch, the game's \"swing\" punch).")]
        [SerializeField] private float hookSpeed = 7.5f;
        [Tooltip("The fist's speed in an uppercut (Ctrl + click).")]
        [SerializeField] private float upperSpeed = 5.5f;
        [Tooltip("How fast the hand goes back to rest, in metres per second.")]
        [SerializeField] private float returnSpeed = 1.6f;

        [Header("Held things")]
        [Tooltip("The hand's speed swinging what it holds (a click), in metres per second; with Shift; with Ctrl (upwards).")]
        [SerializeField] private float swingSpeed = 5f;
        [SerializeField] private float hardSwingSpeed = 8f;
        [SerializeField] private float upSwingSpeed = 5f;
        [Tooltip("The hand's speed letting go of a throw (Q/E), as a VR hand would: x a tap, y held for Throw Charge " +
                 "Seconds. Each thing's own Throw Velocity Scale comes on top (a book's is 4.29) and the Throw Assist caps " +
                 "every throw at 16 m/s, so light things reach the cap well before a full charge, heavy ones fly slower.")]
        [SerializeField] private Vector2 throwSpeed = new(1.5f, 6f);
        [SerializeField] private float throwChargeSeconds = 0.6f;
        [Tooltip("How long a grab may look for something before it gives up, in seconds.")]
        [SerializeField] private float grabSeconds = 0.4f;
        [Tooltip("How far away F/G grab what the crosshair is on, in metres (the hand's far grab reaches about this far).")]
        [SerializeField] private float grabRange = 4.3f;

        [Header("Menus")]
        [Tooltip("Where the hand carrying an open menu goes (the inventory rides on the left hand, the shop on the right), " +
                 "from the eyes as they look (x is mirrored for the right hand).")]
        [SerializeField] private Vector3 menuHandSpot = new(-0.1f, -0.2f, 0.5f);
        [Tooltip("Where the pointing hand goes while a menu is open, from the eyes as they look (x mirrored for the left " +
                 "hand): next to the eye, so its ray meets the menu where the mouse is, however near the menu is.")]
        [SerializeField] private Vector3 pointerHandSpot = new(0.12f, -0.1f, 0.02f);

        /// <summary>
        /// For tests: when set, the controls read this instead of the keyboard and mouse (and need no focus). Look is the
        /// mouse movement since the last frame: it's used up when read. Pointer (with Use Pointer) is the mouse's place on
        /// the screen while a menu is open; else the middle.
        /// </summary>
        public static TestInput Simulated;

        /// <summary>The controls as a test sets them (see Simulated).</summary>
        public class TestInput
        {
            public Vector2 move, look, pointer;
            public bool usePointer;
            public bool leftPunch, rightPunch, leftGrab, rightGrab, leftThrow, rightThrow;
            public bool hard, upper, jump, menu, inventory, shop;
        }

        /// <summary>The PC controls running now, or null (a headset is on, or they're switched off).</summary>
        public static PcControls Active { get; private set; }

        private enum State { Ready, Strike, Charging, Return, Grab }
        private enum Kind { Jab, Hook, Upper, Swing, HardSwing, UpSwing, Throw }
        private enum Phase { WindUp, Form, Go }

        private class Hand
        {
            public bool left;
            public Transform controller;
            public Fist fist;
            public NearFarInteractor interactor;
            public AnimateHandOnInput animate;
            public Animator animator;
            public State state;
            public Kind kind;
            public Phase phase;
            public Vector3 pos;
            public Quaternion rot = Quaternion.identity;
            public float since;
            public Vector3 start, to, direction;
            public float speed, windSpeed;
            public bool openWindUp;
            public float turnFrom, turnTo;
            public float releaseAt = -1f;
            public bool selectOn, pressOn;
            public XRGrabInteractable throwing, grabTarget;
            public float throwingSmoothing;
            public float charge;
            public bool lastPunch, lastGrab, lastThrow;
        }

        private XROrigin origin;
        private Camera eyes;
        private Transform offset;
        private PlayerHeight height;
        private readonly Hand[] hands = { new Hand { left = true }, new Hand() };
        private readonly List<ContinuousMoveProvider> movers = new();
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private readonly TestInput scratch = new();
        private readonly List<Canvas> canvases = new();
        private ShopPanelUI shopPanel;
        private WristInventoryUI inventoryPanel;
        private float pitch;
        private bool on;
        private float bornAt;
        private bool jumping;
        private float jumpSpeed;
        private float jumpStartY;
        private bool lastJump, lastMenu, lastInventory, lastShop;
        private bool inventoryShown;
        private bool wasInMenu;
        private bool wasPaused;
        private bool swallowClick;
        private bool hadFocus;
        private int quietFrames;
        private bool holding;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active = null;
            Simulated = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!UnityEditor.EditorPrefs.GetBool(PrefsKey, true)) return;
            var rig = FindAnyObjectByType<XROrigin>();
            if (rig != null && !rig.TryGetComponent<PcControls>(out _)) rig.gameObject.AddComponent<PcControls>();
        }

        private void Awake()
        {
            origin = GetComponent<XROrigin>();
            height = GetComponent<PlayerHeight>();
            bornAt = Time.unscaledTime;
        }

        // Switched on a moment after Play starts, once it's clear no headset is running (it never switches on with one).
        private void Update()
        {
            if (!on)
            {
                if (UnityEngine.XR.XRSettings.isDeviceActive) { enabled = false; return; }
                if (Time.unscaledTime - bornAt < 0.5f) return;
                SwitchOn();
                if (!on) { enabled = false; return; }
            }
            var input = ReadInput();
            Menus(input);
            bool inMenu = InMenu;
            if (inMenu != wasInMenu) MenuChanged(inMenu);
            Look(input.look);
            Walk(input.move);
            Jump(input.jump && !inMenu);
            if (inMenu)
            {
                MenuHands(input);
                return;
            }
            HandleHand(hands[0], input.leftPunch, input.leftGrab, input.leftThrow, input);
            HandleHand(hands[1], input.rightPunch, input.rightGrab, input.rightThrow, input);
        }

        private void SwitchOn()
        {
            eyes = origin.Camera;
            offset = origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.transform : null;
            if (eyes == null || offset == null) return;
            foreach (var fist in GetComponentsInChildren<Fist>(true))
            {
                var hand = fist.name.IndexOf("Left", System.StringComparison.OrdinalIgnoreCase) >= 0 ? hands[0] : hands[1];
                hand.fist = fist;
                hand.controller = fist.transform;
                hand.interactor = fist.GetComponentInChildren<NearFarInteractor>(true);
                hand.animate = fist.GetComponentInChildren<AnimateHandOnInput>(true);
                hand.animator = hand.animate != null ? hand.animate.handAnimator : fist.GetComponentInChildren<Animator>(true);
            }
            if (hands[0].controller == null || hands[1].controller == null) return;

            // The headset and the controllers no longer move the view and the hands; this does.
            foreach (var driver in origin.GetComponentsInChildren<TrackedPoseDriver>(true)) driver.enabled = false;
            foreach (var hand in hands)
            {
                if (hand.animate != null) hand.animate.enabled = false;
                if (hand.interactor != null)
                {
                    hand.interactor.selectInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
                    // Held = holding (as long as F/G's grab is on), not only grabbed on the press.
                    hand.interactor.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.State;
                    hand.interactor.uiPressInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
                    // The ray points exactly where the mouse is (no smoothing for shaky hands).
                    foreach (var caster in hand.interactor.GetComponents<InteractionCasterBase>()) caster.enableStabilization = false;
                }
                hand.pos = Rest(hand);
                hand.rot = RestTurn();
            }
            // Arm swing walking would walk the player with every punch.
            foreach (var mover in origin.GetComponentsInChildren<ContinuousMoveProvider>(true))
            {
                if (mover is ArmSwingMoveProvider) { mover.enabled = false; continue; }
                movers.Add(mover);
                mover.leftHandMoveInput.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;
            }
            // The comfort vignette darkens the edges while walking: for a headset, not a monitor.
            foreach (var t in origin.GetComponentsInChildren<Transform>(true))
                if (t.name == "TunnelingVignette") t.gameObject.SetActive(false);

            eyes.fieldOfView = fieldOfView;
            pitch = 0f;
            on = true;
            Active = this;
            Debug.Log("[PC Controls] On (no headset): WASD walk, mouse look, Space jump; left/right click punch or swing " +
                      "(Shift hard hook, Ctrl uppercut); F/G grab or drop; Q/E throw (hold = harder); Esc menu, I inventory, " +
                      "B shop. The view follows the mouse at once; Esc frees the mouse (pause menu).");
        }

        private void OnDisable()
        {
            if (Active == this) Active = null;
        }

        // ------------------------------------------------------------------ input

        private TestInput ReadInput()
        {
            var input = scratch;
            if (Simulated != null)
            {
                input.move = Simulated.move;
                input.look = Simulated.look;
                // Mouse movement is used up, as a real mouse's is.
                Simulated.look = Vector2.zero;
                input.pointer = Simulated.pointer;
                input.usePointer = Simulated.usePointer;
                input.leftPunch = Simulated.leftPunch;
                input.rightPunch = Simulated.rightPunch;
                input.leftGrab = Simulated.leftGrab;
                input.rightGrab = Simulated.rightGrab;
                input.leftThrow = Simulated.leftThrow;
                input.rightThrow = Simulated.rightThrow;
                input.hard = Simulated.hard;
                input.upper = Simulated.upper;
                input.jump = Simulated.jump;
                input.menu = Simulated.menu;
                input.inventory = Simulated.inventory;
                input.shop = Simulated.shop;
                return input;
            }
            input.move = input.look = input.pointer = Vector2.zero;
            input.usePointer = false;
            input.leftPunch = input.rightPunch = input.leftGrab = input.rightGrab = input.leftThrow = input.rightThrow = false;
            input.hard = input.upper = input.jump = input.menu = input.inventory = input.shop = false;
            var keys = Keyboard.current;
            var mouse = Mouse.current;
            if (keys == null || mouse == null) return input;

            input.menu = keys.escapeKey.isPressed;
            input.inventory = keys.iKey.isPressed;
            input.shop = keys.bKey.isPressed;
            input.move = new Vector2((keys.dKey.isPressed ? 1f : 0f) - (keys.aKey.isPressed ? 1f : 0f),
                                     (keys.wKey.isPressed ? 1f : 0f) - (keys.sKey.isPressed ? 1f : 0f));
            input.jump = keys.spaceKey.isPressed;
            if (InMenu)
            {
                // The mouse is free: it points (the right hand's ray follows it) and the left button presses.
                input.usePointer = Cursor.lockState != CursorLockMode.Locked;
                input.pointer = mouse.position.ReadValue();
                input.leftPunch = mouse.leftButton.isPressed;
                return input;
            }
            // The view follows the mouse from the start (user, 2026-11-23: "head cam view move with mouse point"): the mouse
            // is taken as soon as the Game view has it, when Play starts and each time the Game view is clicked back into,
            // with no click needed first. Esc lets it go (the pause menu); a click in the Game view takes it back.
            // Focused = the Game view has the keys AND Unity is the front app (behind another app the mouse is left alone;
            // it's taken again when Unity comes back to the front).
            bool focused = Application.isFocused && UnityEditorInternal.InternalEditorUtility.isApplicationActive;
            bool cameBack = focused && !hadFocus;
            hadFocus = focused;
            if (keys.escapeKey.wasPressedThisFrame) LetGoOfMouse();
            if (cameBack || !holding)
            {
                if (cameBack || mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame) TakeMouse();
                return input;
            }
            if (!focused) return input;
            if (swallowClick)
            {
                // The click that brought the mouse back isn't a punch.
                if (mouse.leftButton.isPressed || mouse.rightButton.isPressed) return input;
                swallowClick = false;
            }
            input.look = mouse.delta.ReadValue();
            if (Cursor.lockState != CursorLockMode.Locked) PinPointer(mouse);
            // The jump of the pointer to the middle as the mouse is taken isn't a look.
            if (quietFrames > 0)
            {
                quietFrames--;
                input.look = Vector2.zero;
            }
            input.leftPunch = mouse.leftButton.isPressed;
            input.rightPunch = mouse.rightButton.isPressed;
            input.leftGrab = keys.fKey.isPressed;
            input.rightGrab = keys.gKey.isPressed;
            input.leftThrow = keys.qKey.isPressed;
            input.rightThrow = keys.eKey.isPressed;
            input.hard = keys.leftShiftKey.isPressed || keys.rightShiftKey.isPressed;
            input.upper = keys.leftCtrlKey.isPressed || keys.rightCtrlKey.isPressed;
            return input;
        }

        // ------------------------------------------------------------------ menus

        private static bool ShopOpen => Shop.Main != null && Shop.Main.IsOpen;

        private bool InMenu => PauseMenu.IsPaused || inventoryShown || ShopOpen;

        // Esc: the pause menu; I: the inventory; B: the shop (each opens or closes on a press).
        private void Menus(TestInput input)
        {
            bool menu = input.menu && !lastMenu, inventory = input.inventory && !lastInventory, shop = input.shop && !lastShop;
            lastMenu = input.menu;
            lastInventory = input.inventory;
            lastShop = input.shop;
            // The keyboard's Esc is already the pause menu's own key (its Pause Menu action binds <Keyboard>/escape), so
            // only a test's pretend Esc opens or closes it here: both together opened and shut it in one press (found
            // 2026-11-23; the 11-20 test only used pretend keys).
            if (menu && Simulated != null)
            {
                var pause = FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include);
                if (pause != null)
                {
                    if (PauseMenu.IsPaused) pause.Resume();
                    else pause.Pause();
                }
            }
            // However it opened, the eyes turn to it.
            bool paused = PauseMenu.IsPaused;
            if (paused && !wasPaused)
            {
                var pause = FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include);
                if (pause != null) FacePauseMenu(pause);
            }
            wasPaused = paused;
            if (inventory)
            {
                inventoryShown = !inventoryShown;
                foreach (var ui in FindObjectsByType<WristInventoryUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    ui.forceShow = inventoryShown;
            }
            if (shop && Shop.Main != null)
            {
                if (Shop.Main.IsOpen) Shop.Main.Close();
                else Shop.Main.Open();
            }
        }

        // A menu opened: the hands stop what they were doing and the mouse is freed to point; closed: taken back.
        private void MenuChanged(bool inMenu)
        {
            wasInMenu = inMenu;
            foreach (var hand in hands)
            {
                if (hand.state == State.Strike || hand.state == State.Charging || hand.state == State.Grab)
                {
                    if (hand.state == State.Grab && !Holding(hand)) Select(hand, false);
                    hand.state = State.Return;
                    hand.since = Time.time;
                }
                Press(hand, false);
            }
            if (Simulated != null) return;
            if (inMenu) LetGoOfMouse();
            else TakeMouse();
        }

        // The mouse is held in the Game view and hidden: from now on it turns the view.
        private void TakeMouse()
        {
            holding = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            swallowClick = true;
            quietFrames = 2;
        }

        private void LetGoOfMouse()
        {
            holding = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // The Editor doesn't always lock the mouse when asked (play test 2026-11-23: Unity in front, the Game view focused,
        // still refused until it saw a real click in it). The look still comes from the mouse's own movement (Mouse.delta,
        // which a warp doesn't change: tested); the pointer is only put back in the middle of the Game view each frame so
        // it can't wander off it and click the Editor around it.
        private void PinPointer(Mouse mouse)
        {
            var middle = new Vector2(Mathf.Round(Screen.width * 0.5f), Mathf.Round(Screen.height * 0.5f));
            if ((mouse.position.ReadValue() - middle).sqrMagnitude > 4f) mouse.WarpCursorPosition(middle);
        }

        // The pause menu opens level in front of the head, whatever its up/down tilt (in VR you just look at it); the
        // mouse doesn't look round while a menu is open, so the eyes are tilted to it.
        private void FacePauseMenu(PauseMenu pause)
        {
            foreach (var canvas in pause.GetComponentsInChildren<Canvas>(false))
            {
                if (canvas.renderMode != RenderMode.WorldSpace || !canvas.isActiveAndEnabled) continue;
                var to = canvas.transform.position - eyes.transform.position;
                float flat = new Vector2(to.x, to.z).magnitude;
                if (flat < 0.05f) return;
                pitch = Mathf.Clamp(-Mathf.Atan2(to.y, flat) * Mathf.Rad2Deg, -maxPitch, maxPitch);
                eyes.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                return;
            }
        }

        // The hand an open menu rides on (the shop's, else the inventory's), or none (the pause menu floats by itself).
        private Hand MenuCarrier()
        {
            Transform panel = null;
            if (ShopOpen)
            {
                if (shopPanel == null) shopPanel = FindAnyObjectByType<ShopPanelUI>(FindObjectsInactive.Include);
                if (shopPanel != null) panel = shopPanel.transform;
            }
            if (panel == null && inventoryShown)
            {
                if (inventoryPanel == null) inventoryPanel = FindAnyObjectByType<WristInventoryUI>(FindObjectsInactive.Include);
                if (inventoryPanel != null) panel = inventoryPanel.transform;
            }
            if (panel == null) return null;
            foreach (var hand in hands)
                if (hand.controller != null && panel.IsChildOf(hand.controller)) return hand;
            return null;
        }

        // With a menu open: the hand carrying it (if it rides on one) comes up in front of where the eyes look, the other
        // hand's ray points where the mouse is (the crosshair while it's locked), and the left button presses what it
        // points at. With no menu on a hand (the pause menu), the left rests and the right points.
        private void MenuHands(TestInput input)
        {
            float dt = Time.unscaledDeltaTime;
            var carrier = MenuCarrier();
            var pointer = carrier == hands[1] ? hands[0] : hands[1];
            foreach (var hand in hands)
            {
                if (hand == pointer) continue;
                if (hand == carrier)
                {
                    var spot = menuHandSpot;
                    if (!hand.left) spot.x = -spot.x;
                    var look = eyes.transform.localRotation;
                    hand.pos = Vector3.MoveTowards(hand.pos, eyes.transform.localPosition + look * spot, 2f * dt);
                    hand.rot = Quaternion.RotateTowards(hand.rot, look * Quaternion.Euler(-35f, hand.left ? 15f : -15f, 0f), 360f * dt);
                }
                else
                {
                    hand.pos = Vector3.MoveTowards(hand.pos, Rest(hand), 2f * dt);
                    hand.rot = Quaternion.RotateTowards(hand.rot, RestTurn(), 360f * dt);
                }
                Pose(hand, 1f, Holding(hand));
                Press(hand, false);
            }

            var pointFrom = pointerHandSpot;
            if (pointer.left) pointFrom.x = -pointFrom.x;
            pointer.pos = Vector3.MoveTowards(pointer.pos, eyes.transform.localPosition + eyes.transform.localRotation * pointFrom, 2f * dt);
            var screen = input.usePointer ? input.pointer : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var target = PointedAt(eyes.ScreenPointToRay(screen));
            var handWorld = offset.TransformPoint(pointer.pos);
            var way = offset.InverseTransformDirection(target - handWorld);
            if (way.sqrMagnitude > 0.0001f) pointer.rot = Quaternion.LookRotation(way);
            // An open hand, pointing.
            Pose(pointer, 0f, Holding(pointer));
            Press(pointer, input.leftPunch);
        }

        // Where a ray from the eyes meets an open world-space menu (the nearest), else 2 m along it.
        private Vector3 PointedAt(Ray ray)
        {
            canvases.Clear();
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas.renderMode != RenderMode.WorldSpace || !canvas.isActiveAndEnabled) continue;
                if (!canvas.TryGetComponent<TrackedDeviceGraphicRaycaster>(out var raycaster) || !raycaster.isActiveAndEnabled) continue;
                var group = canvas.GetComponentInParent<CanvasGroup>();
                if (group != null && group.alpha < 0.05f) continue;
                canvases.Add(canvas);
            }
            float best = float.MaxValue;
            var point = ray.GetPoint(2f);
            foreach (var canvas in canvases)
            {
                var rect = (RectTransform)canvas.transform;
                var plane = new Plane(rect.forward, rect.position);
                if (!plane.Raycast(ray, out float distance) || distance >= best) continue;
                var p = ray.GetPoint(distance);
                if (!rect.rect.Contains(rect.InverseTransformPoint(p))) continue;
                best = distance;
                point = p;
            }
            return point;
        }

        private void Press(Hand hand, bool press)
        {
            if (hand.interactor == null || hand.pressOn == press) return;
            hand.pressOn = press;
            hand.interactor.uiPressInput.QueueManualState(press, press ? 1f : 0f);
        }

        // ------------------------------------------------------------------ look, walk, jump

        private void Look(Vector2 delta)
        {
            if (delta.x != 0f) origin.RotateAroundCameraUsingOriginUp(delta.x * lookSpeed);
            pitch = Mathf.Clamp(pitch - delta.y * lookSpeed, -maxPitch, maxPitch);
            // The eyes: at Eye Height over the floor (plus the Height option and the Green Boong), turned only up and down
            // (the XR Origin turns left and right).
            float extra = (height != null ? height.Offset : 0f) + PlayerHeight.Bonus;
            eyes.transform.localPosition = new Vector3(0f, eyeHeight + extra - offset.localPosition.y, 0f);
            eyes.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void Walk(Vector2 move)
        {
            if (move.sqrMagnitude > 1f) move.Normalize();
            foreach (var mover in movers)
                if (mover != null) mover.leftHandMoveInput.manualValue = move;
        }

        private void Jump(bool pressed)
        {
            bool press = pressed && !lastJump;
            lastJump = pressed;
            if (press && !jumping)
            {
                jumping = true;
                jumpSpeed = Mathf.Sqrt(2f * gravity * jumpHeight);
                jumpStartY = transform.position.y;
            }
            if (!jumping) return;
            float dt = Time.deltaTime;
            jumpSpeed -= gravity * dt;
            var at = transform.position;
            at.y += jumpSpeed * dt;
            // Coming down: lands on the floor under the player (the rig stands at floor height; the game has no gravity of
            // its own). With no floor under them it falls on (Fall Edges and falling out of the world take over), and
            // stops 3 m under where it jumped from at most.
            if (jumpSpeed < 0f)
            {
                float floor = FloorBelow(transform.position);
                float land = float.IsNaN(floor) ? jumpStartY - 3f : floor;
                if (at.y <= land)
                {
                    at.y = land;
                    jumping = false;
                }
            }
            // Moved directly (the Character Controller's capsule is only kept up to date while XRI walks the body), then
            // synced so the next walk starts from here.
            transform.position = at;
            Physics.SyncTransforms();
        }

        // The height of the floor under a point: the first solid thing below knee height that isn't the player, what they
        // hold or a loose thing lying about. NaN: none within 6 m.
        private float FloorBelow(Vector3 feet)
        {
            int n = Physics.RaycastNonAlloc(feet + Vector3.up * 0.5f, Vector3.down, hits, 6.5f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue, floor = float.NaN;
            for (int i = 0; i < n; i++)
            {
                var hit = hits[i];
                if (hit.collider == null || hit.distance >= best) continue;
                if (hit.collider.transform.IsChildOf(origin.transform.root)) continue;
                var thing = hit.collider.attachedRigidbody;
                if (thing != null && !thing.isKinematic) continue;
                best = hit.distance;
                floor = hit.point.y;
            }
            return floor;
        }

        // ------------------------------------------------------------------ hands

        // Its rest, in the Camera Offset's space: from the eyes, turned with the body (not with the look up and down, so
        // looking about never swings a fist).
        private Vector3 Rest(Hand hand)
        {
            var at = restLeft;
            if (!hand.left) at.x = -at.x;
            return eyes.transform.localPosition + at;
        }

        private static Quaternion RestTurn() => Quaternion.Euler(10f, 0f, 0f);

        // Where the crosshair points, in the Camera Offset's space: the first solid thing it meets (not the player or
        // what they hold), else 3 m ahead.
        private Vector3 AimPoint()
        {
            var ray = new Ray(eyes.transform.position, eyes.transform.forward);
            int n = Physics.RaycastNonAlloc(ray, hits, 30f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            var point = ray.GetPoint(3f);
            for (int i = 0; i < n; i++)
            {
                var hit = hits[i];
                if (hit.distance >= best || hit.collider == null) continue;
                if (hit.collider.transform.IsChildOf(origin.transform.root)) continue;
                var thing = hit.rigidbody;
                if (thing != null && thing.TryGetComponent(out XRGrabInteractable grab) && grab.isSelected) continue;
                best = hit.distance;
                point = hit.point;
            }
            return offset.InverseTransformPoint(point);
        }

        private Vector3 AimFrom(Vector3 start)
        {
            var way = AimPoint() - start;
            if (way.magnitude < 0.25f) way = offset.InverseTransformDirection(eyes.transform.forward);
            return way.normalized;
        }

        private bool Holding(Hand hand) => hand.interactor != null && hand.interactor.hasSelection;

        private void HandleHand(Hand hand, bool punch, bool grab, bool throwing, TestInput input)
        {
            bool punchDown = punch && !hand.lastPunch;
            bool grabDown = grab && !hand.lastGrab;
            bool throwDown = throwing && !hand.lastThrow;
            hand.lastPunch = punch;
            hand.lastGrab = grab;
            hand.lastThrow = throwing;
            float dt = Time.deltaTime;
            var rest = Rest(hand);
            bool holding = Holding(hand);
            bool free = hand.state == State.Ready || (hand.state == State.Return && Time.time - hand.since > 0.1f);

            // F / G: grab what the crosshair is on, or drop what the hand holds.
            if (grabDown && free)
            {
                if (holding) Select(hand, false);
                else
                {
                    // What the crosshair is on is taken at once (XRI's own far ray is stopped by the floor in front of a
                    // thing lying on it, and lags a turned hand); with nothing there, whatever is at the hand.
                    hand.state = State.Grab;
                    hand.since = Time.time;
                    hand.grabTarget = CrosshairGrab();
                    // Opened first: a fist (the trigger squeezed) only takes what's within reach (Fist's far grab rule).
                    Pose(hand, 0f, false);
                    Select(hand, true);
                    if (hand.grabTarget != null && hand.interactor != null && hand.interactor.interactionManager != null)
                        hand.interactor.interactionManager.SelectEnter((IXRSelectInteractor)hand.interactor, (IXRSelectInteractable)hand.grabTarget);
                }
            }
            // Click: a jab, a hook (Shift) or an uppercut (Ctrl); holding something, a swing, a hard swing or an upward one.
            else if (punchDown && free)
            {
                Kind kind = holding ? (input.upper ? Kind.UpSwing : input.hard ? Kind.HardSwing : Kind.Swing)
                                    : (input.upper ? Kind.Upper : input.hard ? Kind.Hook : Kind.Jab);
                StartStrike(hand, kind, rest);
            }
            // Q / E: wind up a throw while it's held, let go to throw.
            else if (throwDown && free && holding)
            {
                hand.state = State.Charging;
                hand.since = Time.time;
                hand.charge = 0f;
            }

            switch (hand.state)
            {
                case State.Ready:
                    hand.pos = Vector3.MoveTowards(hand.pos, rest, returnSpeed * dt);
                    hand.rot = Quaternion.RotateTowards(hand.rot, RestTurn(), 360f * dt);
                    break;
                case State.Return:
                    hand.pos = Vector3.MoveTowards(hand.pos, rest, returnSpeed * dt);
                    hand.rot = Quaternion.RotateTowards(hand.rot, RestTurn(), 360f * dt);
                    if ((hand.pos - rest).sqrMagnitude < 0.0001f) hand.state = State.Ready;
                    break;
                case State.Charging:
                {
                    hand.charge = Mathf.Clamp01((Time.time - hand.since) / throwChargeSeconds);
                    hand.direction = AimFrom(rest);
                    // Wound back and up slowly while it's held.
                    var back = rest - hand.direction * (0.16f * hand.charge) + Vector3.up * (0.1f * hand.charge);
                    hand.pos = Vector3.MoveTowards(hand.pos, back, 0.5f * dt);
                    hand.rot = Quaternion.RotateTowards(hand.rot, Quaternion.LookRotation(hand.direction) * Quaternion.Euler(10f, 0f, 0f), 360f * dt);
                    if (!throwing) StartThrow(hand);
                    break;
                }
                case State.Strike:
                    Strike(hand, dt);
                    break;
                case State.Grab:
                {
                    var target = GrabTarget(hand, rest);
                    hand.pos = Vector3.MoveTowards(hand.pos, target, 2f * dt);
                    hand.rot = Quaternion.LookRotation(AimFrom(hand.pos));
                    if (Holding(hand)) { hand.state = State.Return; hand.since = Time.time; }
                    else if (Time.time - hand.since > grabSeconds) { Select(hand, false); hand.state = State.Return; hand.since = Time.time; }
                    break;
                }
            }

            // A free hand is a fist, ready to punch; grabbing, or winding up a hook or an uppercut, it's open.
            bool open = hand.state == State.Grab || (hand.state == State.Strike && hand.phase == Phase.WindUp && hand.openWindUp);
            Pose(hand, open ? 0f : 1f, Holding(hand));

            // A thrown thing's smoothing is put back once it has flown.
            if (hand.throwing != null && hand.releaseAt >= 0f && Time.time > hand.releaseAt + 0.3f)
            {
                hand.throwing.throwSmoothingDuration = hand.throwingSmoothing;
                hand.throwing = null;
            }
        }

        // The fist's grip, the hand's look and its place: Closed 1 = a fist (open while it holds something).
        private void Pose(Hand hand, float closed, bool holding)
        {
            if (holding) closed = 0f;
            if (hand.fist != null)
            {
                hand.fist.manualGrip = closed;
                hand.fist.manualTrigger = closed;
            }
            if (hand.animator != null)
            {
                hand.animator.SetFloat("Grip", holding ? 1f : closed);
                hand.animator.SetFloat("Trigger", holding ? 1f : closed);
            }
            hand.controller.localPosition = hand.pos;
            hand.controller.localRotation = hand.rot;
        }

        // A punch or a swing: where it starts and ends (in the body's frame, at what the crosshair is on), how fast, and
        // whether the hand first gets into place open (a hook's and an uppercut's wind-up mustn't count as a punch).
        private void StartStrike(Hand hand, Kind kind, Vector3 rest)
        {
            var aim = AimPoint();
            var aimDir = AimFrom(rest);
            float distance = (aim - rest).magnitude;
            var strikeAt = rest + aimDir * Mathf.Clamp(distance, 0.35f, 0.9f);
            var side = hand.left ? Vector3.left : Vector3.right;
            var ahead = new Vector3(aimDir.x, 0f, aimDir.z);
            ahead = ahead.sqrMagnitude > 0.0001f ? ahead.normalized : Vector3.forward;
            hand.kind = kind;
            hand.openWindUp = false;
            hand.turnFrom = hand.turnTo = 10f;
            switch (kind)
            {
                case Kind.Jab:
                    hand.start = hand.pos;
                    hand.to = hand.pos + (aim - hand.pos).normalized * Mathf.Clamp((aim - hand.pos).magnitude + 0.1f, punchReach, maxPunchReach);
                    hand.speed = jabSpeed;
                    break;
                case Kind.Hook:
                    // From out to the side, across in front: the game's "swing" punch.
                    hand.start = strikeAt + side * 0.45f - ahead * 0.1f;
                    hand.to = strikeAt - side * 0.15f + ahead * 0.05f;
                    hand.speed = hookSpeed;
                    hand.openWindUp = true;
                    break;
                case Kind.Upper:
                    hand.start = strikeAt - Vector3.up * 0.4f - ahead * 0.05f;
                    hand.to = strikeAt + Vector3.up * 0.15f + ahead * 0.08f;
                    hand.speed = upperSpeed;
                    hand.openWindUp = true;
                    break;
                case Kind.Swing:
                case Kind.HardSwing:
                {
                    // A chop down through what the crosshair is on (as far as a jab reaches) and on past it, so the
                    // held thing (which trails the hand a little) is at full speed when it gets there; the hard one
                    // from higher and further back. Never into the floor.
                    bool big = kind == Kind.HardSwing;
                    var target = rest + aimDir * Mathf.Clamp(distance, 0.35f, maxPunchReach);
                    hand.start = target + Vector3.up * (big ? 0.7f : 0.45f) - ahead * (big ? 0.35f : 0.25f) + side * (big ? 0.15f : 0.1f);
                    hand.to = target - Vector3.up * 0.3f + ahead * 0.2f;
                    hand.to.y = Mathf.Max(hand.to.y, FloorHeight() + 0.05f);
                    hand.speed = big ? hardSwingSpeed : swingSpeed;
                    hand.turnFrom = big ? -80f : -70f;
                    hand.turnTo = 35f;
                    break;
                }
                case Kind.UpSwing:
                {
                    // From low and nearer, up through what the crosshair is on and on past it.
                    var target = rest + aimDir * Mathf.Clamp(distance, 0.35f, maxPunchReach);
                    hand.start = target - Vector3.up * 0.3f - ahead * 0.35f;
                    hand.start.y = Mathf.Max(hand.start.y, FloorHeight() + 0.1f);
                    hand.to = target + Vector3.up * 0.35f + ahead * 0.15f;
                    hand.speed = upSwingSpeed;
                    hand.turnFrom = 40f;
                    hand.turnTo = -50f;
                    break;
                }
            }
            hand.direction = (hand.to - hand.start).sqrMagnitude > 0.0001f ? (hand.to - hand.start).normalized : aimDir;
            hand.windSpeed = hand.openWindUp ? 6f : 2f;
            hand.state = State.Strike;
            hand.phase = (hand.pos - hand.start).sqrMagnitude > 0.0004f ? Phase.WindUp : Phase.Go;
            hand.releaseAt = -1f;
            hand.since = Time.time;
        }

        // The floor's height in the hands' space (the play area).
        private float FloorHeight() => offset != null ? offset.InverseTransformPoint(origin.transform.position).y : 0f;

        // The throw (Q/E let go): forward at the crosshair at a speed by how long it was held, let go 70% of the way, once
        // the hand has gone at full speed for longer than XRI's throw looks back (Throw Smoothing, 0.06 s here), so the
        // still wind-up before it doesn't count and every throw of the same charge flies the same.
        private void StartThrow(Hand hand)
        {
            hand.kind = Kind.Throw;
            hand.direction = AimFrom(hand.pos);
            hand.start = hand.pos;
            // Long enough that it's let go after more than 0.06 s at full speed, whatever the speed.
            hand.to = hand.pos + hand.direction * Mathf.Clamp(hand.speed * 0.13f, 0.35f, 1.1f);
            hand.speed = Mathf.Lerp(throwSpeed.x, throwSpeed.y, hand.charge);
            hand.turnFrom = hand.turnTo = 10f;
            hand.openWindUp = false;
            hand.phase = Phase.Go;
            hand.releaseAt = 0.7f;
            // XRI judges a throw by the last moments only while this throw lasts (put back after).
            var grab = hand.interactor != null ? hand.interactor.firstInteractableSelected as XRGrabInteractable : null;
            if (grab != null)
            {
                hand.throwing = grab;
                hand.throwingSmoothing = grab.throwSmoothingDuration;
                grab.throwSmoothingDuration = 0.06f;
            }
            hand.state = State.Strike;
            hand.since = Time.time;
        }

        private void Strike(Hand hand, float dt)
        {
            if (hand.phase == Phase.WindUp)
            {
                hand.pos = Vector3.MoveTowards(hand.pos, hand.start, hand.windSpeed * dt);
                hand.rot = Quaternion.RotateTowards(hand.rot, Quaternion.LookRotation(hand.direction) * Quaternion.Euler(hand.turnFrom, 0f, 0f), 720f * dt);
                if ((hand.pos - hand.start).sqrMagnitude > 0.0001f) return;
                // Into place: close the fist (it takes the Fist's Form Seconds to count) before going.
                hand.phase = hand.openWindUp ? Phase.Form : Phase.Go;
                hand.since = Time.time;
                return;
            }
            if (hand.phase == Phase.Form)
            {
                if (Time.time - hand.since < 0.14f) return;
                hand.phase = Phase.Go;
                hand.since = Time.time;
            }
            float total = Mathf.Max(0.001f, (hand.to - hand.start).magnitude);
            hand.pos = Vector3.MoveTowards(hand.pos, hand.to, hand.speed * dt);
            float done = 1f - (hand.to - hand.pos).magnitude / total;
            hand.rot = Quaternion.LookRotation(hand.direction) * Quaternion.Euler(Mathf.Lerp(hand.turnFrom, hand.turnTo, done), 0f, 0f);
            if (hand.kind == Kind.Throw && hand.releaseAt >= 0f && hand.releaseAt < 1f && done >= hand.releaseAt && hand.selectOn)
            {
                Select(hand, false);
                hand.releaseAt = Time.time;
            }
            if (done < 0.999f) return;
            hand.state = State.Return;
            hand.since = Time.time;
        }

        // Where a grabbing hand goes: next to what the other hand holds when the crosshair is on it (two hands on one
        // thing), to the thing when it's within arm's length, else a little toward the crosshair (far things are pulled
        // to it).
        private Vector3 GrabTarget(Hand hand, Vector3 rest)
        {
            var other = hand.left ? hands[1] : hands[0];
            if (Holding(other) && other.interactor.firstInteractableSelected is XRGrabInteractable shared)
            {
                var aimWorld = offset.TransformPoint(AimPoint());
                var item = shared.transform.position;
                if ((aimWorld - item).magnitude < 0.6f)
                    return offset.InverseTransformPoint(item) + (hand.left ? Vector3.left : Vector3.right) * 0.12f;
            }
            if (hand.grabTarget != null)
            {
                var at = offset.InverseTransformPoint(hand.grabTarget.transform.position);
                if ((at - rest).magnitude < 0.9f) return at;
            }
            return rest + AimFrom(rest) * 0.15f;
        }

        // The thing to grab under the crosshair, within the far grab's reach: a thin ray first (what's exactly there), then
        // a wider one (the nearest grabbable close to the crosshair, a little help with aiming). Not the player's own
        // things; null: none.
        private XRGrabInteractable CrosshairGrab()
        {
            var ray = new Ray(eyes.transform.position, eyes.transform.forward);
            int n = Physics.SphereCastNonAlloc(ray, 0.02f, hits, grabRange, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            XRGrabInteractable found = null;
            bool blocked = false;
            for (int i = 0; i < n; i++)
            {
                var hit = hits[i];
                if (hit.collider == null || hit.distance >= best || hit.collider.transform.IsChildOf(origin.transform.root)) continue;
                best = hit.distance;
                found = hit.collider.GetComponentInParent<XRGrabInteractable>();
                blocked = found == null;
            }
            if (found != null) return found;
            n = Physics.SphereCastNonAlloc(ray, 0.15f, hits, blocked ? Mathf.Min(grabRange, best + 0.3f) : grabRange, ~0, QueryTriggerInteraction.Ignore);
            best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var hit = hits[i];
                if (hit.collider == null || hit.distance >= best || hit.collider.transform.IsChildOf(origin.transform.root)) continue;
                var grab = hit.collider.GetComponentInParent<XRGrabInteractable>();
                if (grab == null) continue;
                best = hit.distance;
                found = grab;
            }
            return found;
        }

        private void Select(Hand hand, bool select)
        {
            if (hand.interactor == null) return;
            hand.selectOn = select;
            hand.interactor.selectInput.QueueManualState(select, select ? 1f : 0f);
        }

        // ------------------------------------------------------------------ crosshair

        private void OnGUI()
        {
            if (!on || (InMenu && Cursor.lockState != CursorLockMode.Locked)) return;
            float x = Screen.width * 0.5f, y = Screen.height * 0.5f;
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            GUI.DrawTexture(new Rect(x - 2f, y - 2f, 4f, 4f), Texture2D.whiteTexture);
            // A charge bar under the crosshair for each hand winding up a throw (left on the left, right on the right).
            foreach (var hand in hands)
            {
                if (hand.state != State.Charging) continue;
                float c = hand.charge;
                GUI.color = Color.Lerp(new Color(1f, 1f, 1f, 0.7f), new Color(1f, 0.3f, 0.1f, 0.9f), c);
                float w = 30f * c;
                GUI.DrawTexture(new Rect(hand.left ? x - 4f - w : x + 4f, y + 12f, w, 3f), Texture2D.whiteTexture);
            }
            GUI.color = old;
        }
    }
}
#endif
