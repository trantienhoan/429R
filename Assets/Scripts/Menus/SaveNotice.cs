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
    /// - the small "Saved" sign near the edge of the view, for a couple of seconds each time the game saves at a
    ///   checkpoint, its cauldron showing up and disappearing once. It doesn't pause anything.
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
            Hide(loading);
            Hide(saved);
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
