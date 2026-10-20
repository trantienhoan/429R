using Game.Saving;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Menus
{
    /// <summary>
    /// The save notices, both fixed to the view (children of the headset camera) and drawn over everything, the headset
    /// fade too:
    /// - the loading screen, shown over the black while a save slot starts: the save icon (a cauldron) slowly showing up
    ///   and disappearing, "Loading..." and a short reminder that the game saves by itself at every checkpoint (don't
    ///   quit while the cauldron shows). The Save Slot Menu shows and hides it.
    /// - the small "Saved" sign in the top right corner of the view (Saved sign placement), for a couple of seconds each
    ///   time the game saves at a checkpoint, its cauldron showing up and disappearing once. It doesn't pause anything.
    /// Tools > 429 Game > Menu > Set Up Save Slot Menu builds it.
    /// </summary>
    public class SaveNotice : MonoBehaviour
    {
        [Header("Loading screen")]
        [SerializeField] private CanvasGroup loading;
        [Tooltip("The save icon: slowly shows up and disappears, again and again, while the loading screen shows.")]
        [SerializeField] private Graphic loadingIcon;

        [Header("Saved sign")]
        [SerializeField] private CanvasGroup saved;
        [Tooltip("The save icon on the Saved sign: shows up and disappears like the loading screen's.")]
        [SerializeField] private Graphic savedIcon;
        [Tooltip("Seconds the Saved sign shows after a checkpoint save.")]
        [Min(0.2f)]
        [SerializeField] private float savedSeconds = 2f;

        [Header("Saved sign placement")]
        [Tooltip("Place the Saved sign in front of the eyes from the values below (on start, and live while you change " +
                 "them).")]
        [SerializeField] private bool placeSaved = true;
        [Tooltip("Metres from the eyes. Closer than about half a metre is tiring to focus on in VR.")]
        [Range(0.3f, 2f)]
        [SerializeField] private float savedDistance = 0.5f;
        [Tooltip("Degrees right (+) / left (-) and up (+) / down (-) from straight ahead. (24, 16) = the top right corner " +
                 "of the view; the health heart is in the top left.")]
        [SerializeField] private Vector2 savedAngles = new(24f, 16f);
        [Tooltip("Canvas scale at 1 m. The canvas scales with the distance, so the sign looks the same size.")]
        [SerializeField] private float savedScaleAtOneMetre = 0.0009f;

        [Header("Motion")]
        [Tooltip("Seconds the save icon takes to show up and disappear once.")]
        [Min(0.2f)]
        [SerializeField] private float pulseSeconds = 2f;
        [Tooltip("Seconds to fade in and out.")]
        [Min(0f)]
        [SerializeField] private float fadeTime = 0.25f;

        private bool loadingShown;
        private float loadingSince;
        private float savedUntil = -1f;
        private float savedSince;

        /// <summary>Whether the loading screen is up (or still fading out).</summary>
        public bool IsLoadingShown => loadingShown || (loading != null && loading.gameObject.activeSelf && loading.alpha > 0f);

        /// <summary>The scene's save notice, or null.</summary>
        public static SaveNotice Find() => FindAnyObjectByType<SaveNotice>(FindObjectsInactive.Include);

        private void Awake()
        {
            PlaceSavedSign();
            Hide(loading);
            Hide(saved);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Moving a transform inside OnValidate makes Unity complain, so it waits a moment.
            if (placeSaved) UnityEditor.EditorApplication.delayCall += () => { if (this != null) PlaceSavedSign(); };
        }
#endif

        /// <summary>Moves the Saved sign to its Distance and Angles in front of the eyes (the setup menu calls it too).</summary>
        public void PlaceSavedSign()
        {
            if (!placeSaved || saved == null) return;
            var sign = saved.transform;
            var dir = Quaternion.Euler(-savedAngles.y, savedAngles.x, 0f) * Vector3.forward;
            // A canvas keeps its sideways and up/down place as its anchored position; set it there so the scene saves it.
            if (sign is RectTransform rect) rect.anchoredPosition3D = dir * savedDistance;
            else sign.localPosition = dir * savedDistance;
            sign.localRotation = Quaternion.LookRotation(dir, Vector3.up);
            sign.localScale = Vector3.one * (savedScaleAtOneMetre * savedDistance);
        }

        private void OnEnable()
        {
            SaveManager.CheckpointSaved += ShowSaved;
        }

        private void OnDisable()
        {
            SaveManager.CheckpointSaved -= ShowSaved;
        }

        /// <summary>Fades the loading screen in (true) or out (false).</summary>
        public void ShowLoading(bool show)
        {
            if (show && !loadingShown) StartPulse(loadingIcon, out loadingSince);
            loadingShown = show;
            if (show && loading != null) loading.gameObject.SetActive(true);
        }

        /// <summary>Shows the small "Saved" sign for Saved Seconds (the Save Manager's checkpoint saves do it).</summary>
        public void ShowSaved()
        {
            float now = Time.unscaledTime;
            // Already up: it just stays longer, the cauldron carrying on.
            if (now >= savedUntil) StartPulse(savedIcon, out savedSince);
            savedUntil = now + savedSeconds;
            if (saved != null) saved.gameObject.SetActive(true);
        }

        // Unscaled time: the loading screen shows while the game is held.
        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float step = fadeTime > 0f ? dt / fadeTime : 1f;
            Fade(loading, loadingIcon, loadingShown, loadingSince, step);
            // The Saved sign waits behind the loading screen.
            Fade(saved, savedIcon, Time.unscaledTime < savedUntil && !loadingShown, savedSince, step);
        }

        private void Fade(CanvasGroup group, Graphic icon, bool show, float since, float step)
        {
            if (group == null || !group.gameObject.activeSelf) return;
            group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, step);
            // The cauldron: from nothing to full and back to nothing, smoothly, every Pulse Seconds.
            float phase = (Time.unscaledTime - since) / pulseSeconds;
            if (icon != null) icon.canvasRenderer.SetAlpha(0.5f - 0.5f * Mathf.Cos(phase * 2f * Mathf.PI));
            // Gone: not drawn at all.
            if (!show && group.alpha <= 0f) group.gameObject.SetActive(false);
        }

        // The cauldron starts from nothing each time its notice comes up.
        private static void StartPulse(Graphic icon, out float since)
        {
            since = Time.unscaledTime;
            if (icon != null) icon.canvasRenderer.SetAlpha(0f);
        }

        private static void Hide(CanvasGroup group)
        {
            if (group == null) return;
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            group.gameObject.SetActive(false);
        }
    }
}
