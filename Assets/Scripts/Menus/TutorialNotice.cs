using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Menus
{
    /// <summary>
    /// A tutorial in pages: each page has a title, a picture (or none) and a few lines. A page can float above something
    /// in the world (Above, e.g. the enemy it's about), facing the player, with an arrow bobbing under it that points down
    /// at it; the other pages show in front of the player (like the Level Up notice), gliding back if they look away. The
    /// game pauses while it's up (time and sound). A trigger turns the page once it has been read for a moment; on the
    /// last page it closes the tutorial. While the pause menu is open it hides and waits. The Show Notice action
    /// (PlayMaker, Menus) shows it, e.g. when StartGamePumpkin lands. Tools > 429 Game > Menu > Set Up Tutorial Notice
    /// builds it.
    /// </summary>
    public class TutorialNotice : MonoBehaviour
    {
        /// <summary>
        /// One page: a title, a picture (none = a short page without one), the text under it, and something to float above
        /// (none = in front of the player).
        /// </summary>
        public struct Page
        {
            public string Title;
            public string Body;
            public Texture Picture;
            public Transform Above;

            public Page(string title, string body, Texture picture, Transform above = null)
            {
                Title = title;
                Body = body;
                Picture = picture;
                Above = above;
            }
        }

        [SerializeField] private CanvasGroup panel;
        [SerializeField] private TMP_Text title;
        [SerializeField] private RawImage picture;
        [SerializeField] private TMP_Text body;
        [Tooltip("Shown once the page has been read for Reading Time.")]
        [SerializeField] private TMP_Text continueText;
        [Tooltip("\"1 / 3\".")]
        [SerializeField] private TMP_Text pageText;
        [Tooltip("Under the panel, pointing down at what a page floats above.")]
        [SerializeField] private RectTransform arrow;
        [Tooltip("The panel's dark background, for pages in front of the player.")]
        [SerializeField] private Graphic background;
        [Tooltip("A wooden board's frame and planks (like the Knight's tip board), for pages floating above something.")]
        [SerializeField] private Graphic boardFrame;
        [SerializeField] private Graphic boardWood;
        [Tooltip("The headset camera. Left empty, Camera.main is used.")]
        [SerializeField] private Transform head;

        [Header("Text")]
        [SerializeField] private string nextPageLine = "Pull a trigger for the next page";
        [SerializeField] private string lastPageLine = "Pull a trigger to FIGHT!";

        [Header("Layout (canvas units)")]
        [Tooltip("The panel's height with a picture, and without one.")]
        [SerializeField] private float tallHeight = 800f;
        [SerializeField] private float shortHeight = 240f;
        [Tooltip("The panel's height with only a title (no picture, no text).")]
        [SerializeField] private float titleOnlyHeight = 175f;
        [Tooltip("Where the text starts, down from the top, with a picture and without one.")]
        [SerializeField] private float bodyTopTall = 542f;
        [SerializeField] private float bodyTopShort = 104f;

        [Header("Timing")]
        [Tooltip("Seconds a page shows before a trigger can turn it, so it gets read.")]
        [Min(0f)]
        [SerializeField] private float readingTime = 1.5f;
        [Tooltip("The tutorial goes away by itself after this many seconds on one page.")]
        [Min(1f)]
        [SerializeField] private float pageSeconds = 90f;
        [Tooltip("Defaults to either trigger, and Enter or Space on the keyboard.")]
        [SerializeField] private InputActionProperty continueButton = new(CreateDefaultButton());
        [Tooltip("Where a page in front of the player sits, in metres from the head (x right, y up, z forward).")]
        [SerializeField] private Vector3 panelOffset = new(0f, 0f, 1.4f);
        [Tooltip("Looking further away than this (degrees), a page in front of the player glides back in front of them.")]
        [SerializeField] private float followAngle = 35f;
        [Tooltip("Seconds to fade in and out.")]
        [SerializeField] private float fadeTime = 0.35f;

        [Header("Floating above something")]
        [Tooltip("Metres between the top of what a page floats above and the tip of the arrow.")]
        [SerializeField] private float aboveGap = 0.15f;
        [Tooltip("A page further away is made bigger, so it reads as if it were this far away (metres). Up to Most Growth times.")]
        [SerializeField] private float readsLikeAt = 2f;
        [SerializeField] private float mostGrowth = 3f;

        [Header("Sound (heard while the game's sound is paused)")]
        [SerializeField] private AudioClip pageSound;
        [Range(0f, 1f)]
        [SerializeField] private float pageVolume = 0.6f;

        private readonly List<Page> pages = new();
        private int page;
        private float shownAt;
        private float buttonFrom;
        private bool showing;
        private bool closing;
        // Fading out to show the next page somewhere else (above something / in front of the player).
        private bool moving;
        private bool gliding;
        private bool holding;
        private AudioSource sound;
        private Vector3 baseScale;
        private Vector2 arrowHome;

        /// <summary>Whether the tutorial is up (or still fading out).</summary>
        public bool IsShowing => showing;

        /// <summary>The scene's tutorial notice, or null.</summary>
        public static TutorialNotice Find() => FindAnyObjectByType<TutorialNotice>(FindObjectsInactive.Include);

        private static InputAction CreateDefaultButton()
        {
            var action = new InputAction("Continue", InputActionType.Button);
            action.AddBinding("<XRController>{LeftHand}/triggerPressed");
            action.AddBinding("<XRController>{RightHand}/triggerPressed");
            action.AddBinding("<Keyboard>/enter");
            action.AddBinding("<Keyboard>/space");
            return action;
        }

        private void Awake()
        {
            if (panel != null)
            {
                baseScale = panel.transform.localScale;
                panel.gameObject.SetActive(false);
                // A punch shatters it (whatever page it's on): gone at once, the shards take its place.
                PunchToShatter.Add((RectTransform)panel.transform, () => { panel.alpha = 0f; Close(); });
            }
            if (arrow != null) arrowHome = arrow.anchoredPosition;
            sound = gameObject.AddComponent<AudioSource>();
            sound.playOnAwake = false;
            sound.spatialBlend = 0f;
            sound.ignoreListenerPause = true;
        }

        private void OnEnable()
        {
            continueButton.action?.Enable();
        }

        private void OnDisable()
        {
            // Only switch off our own button; a referenced action may be used elsewhere.
            if (continueButton.reference == null) continueButton.action?.Disable();
            // Never leave the game paused (e.g. the scene closing while the tutorial is up).
            Unpause();
            showing = false;
            if (panel != null) panel.gameObject.SetActive(false);
        }

        /// <summary>Fades the tutorial in at its first page and pauses the game until its last page is closed.</summary>
        public void Show(IList<Page> newPages)
        {
            if (panel == null || newPages == null || newPages.Count == 0) return;
            pages.Clear();
            pages.AddRange(newPages);
            page = 0;
            ShowPage();
            showing = true;
            closing = false;
            moving = false;
            panel.alpha = 0f;
            panel.interactable = false;
            panel.blocksRaycasts = false;
            panel.gameObject.SetActive(true);
            Place(true);
            if (!holding)
            {
                holding = true;
                PauseMenu.Hold(this);
            }
        }

        /// <summary>Fades it out (whatever page it's on).</summary>
        public void Close()
        {
            if (showing) closing = true;
        }

        /// <summary>As a trigger does: the next page, or on the last page, closes it.</summary>
        public void Next()
        {
            if (!showing || closing || moving) return;
            if (sound != null && pageSound != null) sound.PlayOneShot(pageSound, pageVolume);
            if (page >= pages.Count - 1)
            {
                closing = true;
                return;
            }
            bool elsewhere = pages[page + 1].Above != pages[page].Above;
            page++;
            // Somewhere else: it fades out where it is first, then comes back there.
            if (elsewhere) moving = true;
            else ShowPage();
        }

        private void ShowPage()
        {
            var p = pages[page];
            if (title != null) title.text = p.Title;
            if (body != null) body.text = p.Body;
            bool hasPicture = p.Picture != null;
            if (picture != null)
            {
                picture.texture = p.Picture;
                picture.enabled = hasPicture;
            }
            // Without a picture the panel is short and the text moves up.
            var rect = (RectTransform)panel.transform;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, hasPicture ? tallHeight : string.IsNullOrEmpty(p.Body) ? titleOnlyHeight : shortHeight);
            if (body != null)
            {
                var b = body.rectTransform;
                float height = b.offsetMax.y - b.offsetMin.y;
                float top = hasPicture ? bodyTopTall : bodyTopShort;
                b.offsetMax = new Vector2(b.offsetMax.x, -top);
                b.offsetMin = new Vector2(b.offsetMin.x, -top - height);
            }
            // Above something it's a wooden sign with an arrow; in front of the player, the dark panel.
            bool board = p.Above != null;
            if (arrow != null) arrow.gameObject.SetActive(board);
            if (background != null) background.enabled = !board || boardWood == null;
            if (boardFrame != null) boardFrame.enabled = board;
            if (boardWood != null) boardWood.enabled = board;
            if (pageText != null) pageText.text = pages.Count > 1 ? $"{page + 1} / {pages.Count}" : "";
            if (continueText != null)
            {
                continueText.text = page < pages.Count - 1 ? nextPageLine : lastPageLine;
                continueText.enabled = false;
            }
            shownAt = Time.unscaledTime;
            gliding = false;
        }

        private void Unpause()
        {
            if (!holding) return;
            holding = false;
            PauseMenu.Release(this);
        }

        private void Update()
        {
            if (!showing) return;

            // The pause menu sits in front: the tutorial hides and its clock stops (and the trigger that picks a menu
            // button doesn't turn the page too) until the menu is closed.
            if (PauseMenu.IsPaused)
            {
                shownAt += Time.unscaledDeltaTime;
                buttonFrom = Time.unscaledTime + 0.3f;
                panel.alpha = 0f;
                return;
            }

            // Unscaled time: the game is paused underneath it.
            float shown = Time.unscaledTime - shownAt;
            bool ready = shown >= readingTime && !moving;
            if (continueText != null) continueText.enabled = ready && !closing;
            bool pressed = Time.unscaledTime >= buttonFrom && continueButton.action != null && continueButton.action.WasPressedThisFrame();
            if (!closing && ready && pressed) Next();
            // Left alone too long on one page: it goes away.
            else if (!closing && shown >= pageSeconds) closing = true;

            float step = fadeTime > 0f ? Time.unscaledDeltaTime / fadeTime : 1f;
            panel.alpha = Mathf.MoveTowards(panel.alpha, closing || moving ? 0f : 1f, step);
            if (closing && panel.alpha <= 0f)
            {
                panel.gameObject.SetActive(false);
                showing = false;
                Unpause();
                return;
            }
            if (moving && panel.alpha <= 0f)
            {
                moving = false;
                ShowPage();
                Place(true);
            }

            // Fading out to show the next page elsewhere: it stays where it is meanwhile.
            if (!moving) Place(false);
            if (arrow != null && arrow.gameObject.activeSelf)
                arrow.anchoredPosition = arrowHome + Vector2.down * (18f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3.5f)));
        }

        // A page above something stays above it, facing the player. A page in front of the player stays put while it's
        // read and glides back (no snapping, which is uncomfortable in VR) once they look away. 'now' puts it there at once.
        private void Place(bool now)
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (head == null) return;
            var above = pages.Count > 0 ? pages[page].Above : null;
            if (above != null)
            {
                PlaceAbove(above);
                return;
            }

            panel.transform.localScale = baseScale;
            if (!TryGetPlacement(out var position, out var rotation)) return;
            if (now)
            {
                panel.transform.SetPositionAndRotation(position, rotation);
                return;
            }
            var toPanel = panel.transform.position - head.position;
            var wanted = position - head.position;
            if (!gliding && (Vector3.Angle(toPanel, wanted) > followAngle || Mathf.Abs(toPanel.magnitude - wanted.magnitude) > 0.5f))
                gliding = true;
            if (!gliding) return;

            float blend = 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime);
            panel.transform.SetPositionAndRotation(
                Vector3.Lerp(panel.transform.position, position, blend),
                Quaternion.Slerp(panel.transform.rotation, rotation, blend));
            if (Vector3.Distance(panel.transform.position, position) < 0.02f) gliding = false;
        }

        // Over the top of its meshes, turned to the player, as big as if it were Reads Like At metres away.
        private void PlaceAbove(Transform target)
        {
            float top = target.position.y;
            var middle = target.position;
            bool any = false;
            foreach (var r in target.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer) continue;
                var b = r.bounds;
                if (!any)
                {
                    top = b.max.y;
                    middle = b.center;
                    any = true;
                }
                else top = Mathf.Max(top, b.max.y);
            }

            var flat = Vector3.ProjectOnPlane(middle - head.position, Vector3.up);
            float distance = Mathf.Max(0.5f, flat.magnitude);
            float grow = Mathf.Clamp(distance / Mathf.Max(0.1f, readsLikeAt), 1f, Mathf.Max(1f, mostGrowth));
            panel.transform.localScale = baseScale * grow;

            // The panel's pivot is its middle; the arrow hangs under its bottom edge.
            var rect = (RectTransform)panel.transform;
            float unit = panel.transform.lossyScale.y;
            float arrowLength = arrow != null && arrow.gameObject.activeSelf ? -arrowHome.y + arrow.rect.height : 0f;
            float lift = aboveGap + (rect.rect.height * 0.5f + arrowLength) * unit;
            var position = new Vector3(middle.x, top + lift, middle.z);
            // Readable from where the player is: its forward points away from them.
            var away = Vector3.ProjectOnPlane(position - head.position, Vector3.up);
            if (away.sqrMagnitude < 0.0001f) away = Vector3.forward;
            panel.transform.SetPositionAndRotation(position, Quaternion.LookRotation(away.normalized, Vector3.up));
        }

        private bool TryGetPlacement(out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = default;
            if (head == null) return false;

            // Face the way the head looks, ignoring up/down tilt (looking down, the head's up points forward).
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;

            var yaw = Quaternion.LookRotation(forward.normalized, Vector3.up);
            position = head.position + yaw * panelOffset;
            // A world-space canvas is readable when its forward points away from the viewer.
            rotation = Quaternion.LookRotation(position - head.position, Vector3.up);
            return true;
        }
    }
}
