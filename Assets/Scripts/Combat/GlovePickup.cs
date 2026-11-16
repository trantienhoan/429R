using System.Collections.Generic;
using Game.Menus;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// The pair of gloves hanging from the Knight's boong at the start (user, 2026-11-12): a kind picked at random each
    /// run (Gloves, Glove Kinds), tied by their cuffs to a short string so they hang down and swing a little whatever the
    /// Knight does. The player picks them up just by touching them, with a hand or the body, as they walk past: a puff and
    /// a sound, the pair is gone from the boong, and both hands are gloved for the run (grip + trigger pops a glove on).
    /// The first time in a save slot a short tutorial says how to punch with them (Show Tutorial). If the player already
    /// wears gloves this run (a save resumed at the very start), the Knight carries none. Walked past without a touch:
    /// no gloves this run.
    /// Goes on "Hanging Gloves" under the Knight's boong (Knight.prefab: Cay_boong); Tools > 429 Game > Combat > Set Up
    /// Gloves puts it there. Its children: Pivot (what swings, in metres; String, Left Slot and Right Slot under it: the
    /// gloves hang by their cuffs from the slots). Anything called "Preview" under it is only for seeing it in Prefab
    /// Mode: it goes when the game starts.
    /// </summary>
    [DisallowMultipleComponent]
    public class GlovePickup : MonoBehaviour
    {
        [Header("Hanging")]
        [Tooltip("What swings (it hangs straight down from here, whatever way the boong is turned). Its scale is set so " +
                 "everything under it is in metres.")]
        [SerializeField] private Transform pivot;
        [Tooltip("Where the left glove hangs by its cuff (under Pivot).")]
        [SerializeField] private Transform leftSlot;
        [Tooltip("Where the right glove hangs by its cuff (under Pivot).")]
        [SerializeField] private Transform rightSlot;
        [Tooltip("How big the hanging gloves are, compared with a glove on the player's fist (1 = the same).")]
        [Min(0.1f)]
        [SerializeField] private float hangSize = 1f;
        [Tooltip("How far the pair swings to and fro, in degrees.")]
        [Range(0f, 45f)]
        [SerializeField] private float swingDegrees = 8f;
        [Tooltip("Swings a second.")]
        [Min(0f)]
        [SerializeField] private float swingSpeed = 0.7f;

        [Header("Picking up")]
        [Tooltip("A hand this close to the pair, in metres (from its middle), picks it up.")]
        [Min(0.01f)]
        [SerializeField] private float handReach = 0.25f;
        [Tooltip("The player's body (a column from the floor up to a little over the head, this many metres round) " +
                 "touching the pair within Hand Reach picks it up too, so walking past it is enough.")]
        [Min(0f)]
        [SerializeField] private float bodyRadius = 0.22f;
        [Tooltip("Played where the pair is picked up. Empty: silent.")]
        [SerializeField] private AudioClip pickupSound;
        [Range(0f, 1f)]
        [SerializeField] private float pickupVolume = 0.8f;
        [Tooltip("A puff where the pair was (e.g. Smoke_Flash_Small_Puff). Empty: none.")]
        [SerializeField] private GameObject puff;
        [Tooltip("Its size, compared with its prefab.")]
        [Min(0.01f)]
        [SerializeField] private float puffSize = 0.6f;
        [Tooltip("How long the pair takes to shrink away once picked up, in seconds.")]
        [Min(0.01f)]
        [SerializeField] private float goneSeconds = 0.18f;

        [Header("Tutorial (once per save slot)")]
        [Tooltip("The first time in a save slot, picking a pair up shows a short tutorial (the scene's Tutorial Notice; it " +
                 "pauses the game until a trigger closes it). Untick: never.")]
        [SerializeField] private bool showTutorial = true;
        [SerializeField] private string tutorialTitle = "GLOVES!";
        [TextArea(1, 3)]
        [SerializeField] private string tutorialText = "Hold GRIP + TRIGGER to punch.";
        [Tooltip("Its picture (e.g. Tutorial_3_Fist). Empty: a short page without one.")]
        [SerializeField] private Texture tutorialPicture;

        /// <summary>The key Show Once remembers the tutorial by (Show Tutorials Again forgets it).</summary>
        public const string TutorialKey = "GloveTutorial";

        // The body's column goes this far over the eyes, in metres.
        private const float OverHead = 0.2f;

        private GloveKinds.Kind kind;
        private readonly List<Transform> looks = new();
        private readonly List<Vector3> lookSizes = new();
        // A hanging glove's length, in metres.
        private float gloveLength = 0.25f;
        private float pickedAt = -1f;
        private float phase;
        private Transform facing;
        private Transform head;
        private XROrigin rig;
        private Fist[] hands = System.Array.Empty<Fist>();
        private float nextHandSearch;

        /// <summary>The kind this pair is (null before it's set up, or with none this run).</summary>
        public GloveKinds.Kind Kind => kind;

        /// <summary>Whether it's still hanging there to be picked up.</summary>
        public bool Waiting => kind != null && pickedAt < 0f && isActiveAndEnabled;

        /// <summary>The middle of the pair (between the two gloves, half a glove under the slots), for touching it.</summary>
        public Vector3 Middle
        {
            get
            {
                if (leftSlot == null || rightSlot == null) return pivot != null ? pivot.position : transform.position;
                return (leftSlot.position + rightSlot.position) * 0.5f - Vector3.up * (gloveLength * 0.5f);
            }
        }

        private void Awake()
        {
            // The preview is only for Prefab Mode.
            foreach (var part in GetComponentsInChildren<Transform>(true))
            {
                if (part != null && part != transform && part.name == "Preview") Destroy(part.gameObject);
            }
            phase = Random.Range(0f, 10f);
            var animator = GetComponentInParent<Animator>();
            facing = animator != null ? animator.transform : transform.root;
        }

        private void Start()
        {
            // Already gloved this run (a save resumed at the very start), or no kinds: the Knight carries none.
            kind = Gloves.Has ? null : Gloves.OfferForThisRun();
            if (kind == null)
            {
                gameObject.SetActive(false);
                return;
            }
            if (pivot == null)
            {
                pivot = new GameObject("Pivot").transform;
                pivot.SetParent(transform, false);
            }
            // In metres under the pivot, whatever the boong's scale (it's under the Knight's bones).
            float scale = Mathf.Abs(transform.lossyScale.x);
            if (scale > 0.0001f) pivot.localScale = Vector3.one / scale;
            Hang(kind.For(true), leftSlot, true);
            Hang(kind.For(false), rightSlot, false);
            if (puff != null) EffectPool.Warm(puff);
            Gloves.Changed += OnGlovesChanged;
        }

        private void OnDestroy()
        {
            Gloves.Changed -= OnGlovesChanged;
        }

        // Gloved some other way meanwhile: the pair goes.
        private void OnGlovesChanged()
        {
            if (this != null && Gloves.Has && pickedAt < 0f) gameObject.SetActive(false);
        }

        // One glove, hanging by its cuff from its slot: its punching end down, its cuff's end at the slot.
        private void Hang(GameObject prefab, Transform slot, bool left)
        {
            if (prefab == null || slot == null) return;
            var look = Gloves.MakeLook(prefab, slot);
            look.localRotation = Quaternion.Euler(90f, left ? -12f : 12f, 0f);
            float slotScale = Mathf.Abs(slot.lossyScale.x);
            look.localScale = prefab.transform.localScale * (hangSize / Mathf.Max(0.0001f, slotScale));
            var bounds = Gloves.LookBounds(look);
            gloveLength = Mathf.Max(0.05f, bounds.size.z * Mathf.Abs(prefab.transform.localScale.z) * hangSize);
            // The cuff's end (the back of the glove, -Z) at the slot.
            var cuff = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z);
            look.localPosition = -(look.localRotation * Vector3.Scale(cuff, look.localScale));
            looks.Add(look);
            lookSizes.Add(look.localScale);
        }

        private void LateUpdate()
        {
            if (kind == null) return;
            if (pickedAt >= 0f)
            {
                // Picked up: the gloves shrink away, then the pair is gone.
                float left = 1f - (Time.time - pickedAt) / goneSeconds;
                if (left <= 0f)
                {
                    gameObject.SetActive(false);
                    return;
                }
                for (int i = 0; i < looks.Count; i++)
                {
                    if (looks[i] != null) looks[i].localScale = lookSizes[i] * left;
                }
                return;
            }
            Swing();
            if (Touched()) PickUp();
        }

        // Hangs straight down, turned the way the Knight faces, swinging a little to and fro and side to side.
        private void Swing()
        {
            var ahead = facing != null ? Vector3.ProjectOnPlane(facing.forward, Vector3.up) : Vector3.forward;
            if (ahead.sqrMagnitude < 0.0001f) ahead = Vector3.forward;
            float t = (Time.time + phase) * swingSpeed * 2f * Mathf.PI;
            var sway = Quaternion.Euler(swingDegrees * Mathf.Sin(t), 0f, swingDegrees * 0.5f * Mathf.Sin(t * 0.73f + 1.3f));
            pivot.rotation = Quaternion.LookRotation(ahead.normalized, Vector3.up) * sway;
        }

        // A hand within Hand Reach of the pair's middle, or the body's column within Body Radius + Hand Reach of it.
        private bool Touched()
        {
            var middle = Middle;
            if (hands.Length == 0 && Time.time >= nextHandSearch)
            {
                nextHandSearch = Time.time + 1f;
                hands = FindObjectsByType<Fist>(FindObjectsSortMode.None);
            }
            float reach = handReach * handReach;
            foreach (var hand in hands)
            {
                if (hand == null || !hand.isActiveAndEnabled) continue;
                if ((hand.Knuckles - middle).sqrMagnitude <= reach || (hand.transform.position - middle).sqrMagnitude <= reach) return true;
            }
            if (head == null)
            {
                if (rig == null) rig = FindAnyObjectByType<XROrigin>();
                if (rig != null && rig.Camera != null) head = rig.Camera.transform;
                else if (Camera.main != null) head = Camera.main.transform;
                if (head == null) return false;
            }
            // The body: a column from just over the floor up to a little over the head (the top of the head, a hat).
            var eyes = head.position;
            float floor = rig != null ? rig.transform.position.y : eyes.y - 1.6f;
            float top = eyes.y + OverHead;
            var nearest = new Vector3(eyes.x, Mathf.Clamp(middle.y, Mathf.Min(top, floor + 0.1f), top), eyes.z);
            float body = bodyRadius + handReach;
            return (nearest - middle).sqrMagnitude <= body * body;
        }

        /// <summary>Picks the pair up now, as a touch does (also for tests).</summary>
        public void PickUp()
        {
            if (!Waiting) return;
            var at = Middle;
            pickedAt = Time.time;
            Gloves.PickUp(kind);
            if (pickupSound != null) AudioSource.PlayClipAtPoint(pickupSound, at, pickupVolume);
            if (puff != null) EffectPool.Play(puff, at, puffSize);
            if (showTutorial) ShowTutorialOnce();
        }

        // The first time in this save slot: how to punch with them (pauses the game; skipped while another notice is up).
        private void ShowTutorialOnce()
        {
            if (ShowOnce.WasShown(TutorialKey)) return;
            var notice = TutorialNotice.Find();
            if (notice == null || notice.IsShowing) return;
            var levelUp = FindAnyObjectByType<Game.Leveling.LevelUpNotice>();
            if (levelUp != null && levelUp.IsShowing) return;
            ShowOnce.Remember(TutorialKey);
            notice.Show(new List<TutorialNotice.Page> { new(tutorialTitle, tutorialText, tutorialPicture) });
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.8f);
            Gizmos.DrawWireSphere(Middle, handReach);
        }
#endif
    }
}
