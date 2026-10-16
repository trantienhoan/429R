using UnityEngine;

namespace Game.Menus
{
    /// <summary>
    /// Shows this object's signs (its canvases and meshes) only the first time the game is played on this install (Show
    /// Once): after that, switching it on shows nothing. Once it has been shown and is switched off again (e.g. GAMESTAGES
    /// hides the walk tutorial signs when the player reaches the Fairy), it's remembered; quitting while it's up shows it
    /// again next time. Goes on the root of the signs, e.g. "Walk Tutorial Signs".
    /// </summary>
    public class OncePerInstall : MonoBehaviour
    {
        [Tooltip("The name it's remembered by (one of Show Once's keys).")]
        [SerializeField] private string key = "WalkTutorialSigns";

        private static bool quitting;
        private bool showing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            quitting = false;
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        private static void OnQuitting() => quitting = true;

        /// <summary>
        /// The game is being reset to a new install: signs showing now aren't remembered as seen when the scene goes, so
        /// they show again.
        /// </summary>
        public static void ForgetShowing()
        {
            foreach (var signs in FindObjectsByType<OncePerInstall>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                signs.showing = false;
        }

        private void OnEnable()
        {
            showing = !ShowOnce.WasShown(key);
            // Hidden by switching its parts off (it can't switch itself off while it's being switched on).
            foreach (var canvas in GetComponentsInChildren<Canvas>(true)) canvas.enabled = showing;
            foreach (var look in GetComponentsInChildren<Renderer>(true)) look.enabled = showing;
        }

        private void OnDisable()
        {
            if (showing && !quitting) ShowOnce.Remember(key);
            showing = false;
        }
    }
}
