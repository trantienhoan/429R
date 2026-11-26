using UnityEngine;

namespace Game.Menus
{
    /// <summary>
    /// The game over black (user, 2026-11-20: "black out too soon, Player have to watch the lone sky for so long for the
    /// game to restart"). PlayerDeath's Dying... shows GAME_OVER (the HEE HEE black on the camera) and takes it off when
    /// it's done, but GAMESTAGES only then starts over (Game Over, Recreate, Checkpoint?, Walk Tuttorial or a CP state),
    /// and the first room takes a few seconds more to come: about 9 s of empty sky. Now:
    /// - the black stays on until GAMESTAGES has left those states and After Restart seconds more (the room has come),
    ///   then fades out over Fade Seconds; never longer than Most Seconds in all;
    /// - GAMESTAGES' Game Over wait (4.29 s, the black already hides it) is cut to Game Over Seconds;
    /// - the HEE HEE words go after Words Seconds; only the black stays.
    /// Made by itself in any scene with GAMESTAGES; nothing to set up.
    /// </summary>
    public class GameOverHold : MonoBehaviour
    {
        // User 2026-11-20: "I want black out longer".
        [SerializeField] private float afterRestart = 1f;   // 2026-11-22 user: "now the heehee stay for too long" (was 3.5)
        [SerializeField] private float fadeSeconds = 0.8f;
        [SerializeField] private float mostSeconds = 25f;
        [SerializeField] private float gameOverSeconds = 0.5f;
        // 2026-11-23 user: "I feel annoyed by the HeeHee, make it better". The 20-13-49 record: the shaking HEE HEE
        // stayed about 9.5 s per death (grey while dying, then red on the black). Now the words fade out after
        // Words Seconds (0 = keep them on) and the black alone stays until the room has come.
        [SerializeField] private float wordsSeconds = 2.5f;
        [SerializeField] private float wordsFadeSeconds = 0.5f;

        private GameObject over;
        private CanvasGroup fade;
        private PlayMakerFSM stages;
        private bool shown, holding, restarted;
        private float shownAt, leftRestartAt = -1f, gameOverAt = -1f, fadeFrom = -1f;
        private string lastState = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Make()
        {
            if (FindAnyObjectByType<GameOverHold>() != null) return;
            new GameObject("Game_Over_Hold").AddComponent<GameOverHold>();
        }

        private static bool Restarting(string state) =>
            state == "Game Over" || state == "Recreate" || state == "Checkpoint?" || state == "Walk Tuttorial" ||
            state.StartsWith("CP ");

        private void Find()
        {
            if (stages == null)
                foreach (var f in FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (f.FsmName == "GAMESTAGES") { stages = f; break; }
            if (over == null && Camera.main != null)
            {
                var t = Camera.main.transform.Find("GAME_OVER");
                if (t != null)
                {
                    over = t.gameObject;
                    if (!over.TryGetComponent(out fade)) fade = over.AddComponent<CanvasGroup>();
                }
            }
        }

        private void LateUpdate()
        {
            Find();
            if (over == null || stages == null) return;
            string state = stages.ActiveStateName ?? "";
            float now = Time.unscaledTime;

            // GAMESTAGES' Game Over wait, cut short (the black hides it anyway).
            if (state != lastState && state == "Game Over") gameOverAt = now;
            if (state == "Game Over" && gameOverAt >= 0f && now - gameOverAt >= gameOverSeconds)
            {
                gameOverAt = -1f;
                stages.Fsm.Event(HutongGames.PlayMaker.FsmEvent.Finished);
            }
            lastState = state;

            if (!holding)
            {
                if (over.activeSelf && !shown)
                {
                    shown = true;
                    shownAt = now;
                    fade.alpha = 1f;
                    Laugh();
                }
                if (shown && over.activeSelf) fade.alpha = WordsAlpha(now);
                // Dying... took it off: keep it on through the restart.
                if (shown && !over.activeSelf)
                {
                    shown = false;
                    holding = true;
                    restarted = false;
                    leftRestartAt = -1f;
                    over.SetActive(true);
                }
                return;
            }

            if (!over.activeSelf) over.SetActive(true);
            bool restartingNow = Restarting(state);
            if (restartingNow) restarted = true;
            if (restarted && !restartingNow && leftRestartAt < 0f) leftRestartAt = now;
            // Not a game over after all (nothing restarted in a few seconds) or it's taking too long: let go.
            bool stray = !restarted && now - shownAt > 12f;
            if (fadeFrom < 0f && leftRestartAt >= 0f && now >= leftRestartAt + afterRestart) fadeFrom = now;
            if (fadeFrom < 0f && (stray || now - shownAt > mostSeconds)) fadeFrom = now;
            // The black (and any words still up) fades out at the end.
            float endAlpha = fadeFrom < 0f ? 1f
                : fadeSeconds > 0f ? Mathf.Clamp01(1f - (now - fadeFrom) / fadeSeconds) : 0f;
            SetBlack(endAlpha);
            fade.alpha = Mathf.Min(endAlpha, WordsAlpha(now));
            if (endAlpha > 0f) return;
            holding = false;
            fadeFrom = -1f;
            over.SetActive(false);
            fade.alpha = 1f;
            SetBlack(0f);
        }

        // The laughing pumpkin's HEE HEE as the picture comes up (Story Laugh Settings' Death Laughs, user 2026-11-26):
        // one at random, not the same as last time, heard in the player's head.
        private AudioSource laughVoice;
        private int lastLaugh = -1;

        /// <summary>Death laughs played so far this session (for tests).</summary>
        public static int Laughs { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Laughs = 0;

        private void Laugh()
        {
            var settings = Game.Story.StoryLaughSettings.Instance;
            var clips = settings != null ? settings.DeathLaughs : null;
            if (clips == null || clips.Length == 0) return;
            int i = Random.Range(0, clips.Length);
            if (clips.Length > 1 && i == lastLaugh) i = (i + 1) % clips.Length;
            lastLaugh = i;
            if (clips[i] == null) return;
            if (laughVoice == null)
            {
                laughVoice = gameObject.AddComponent<AudioSource>();
                laughVoice.playOnAwake = false;
                laughVoice.spatialBlend = 0f;
                laughVoice.ignoreListenerPause = true;
            }
            laughVoice.PlayOneShot(clips[i], settings.DeathLaughVolume);
            Laughs++;
        }

        // The HEE HEE words: on for Words Seconds from the death, then faded out over Words Fade Seconds.
        private float WordsAlpha(float now)
        {
            if (wordsSeconds <= 0f) return 1f;
            float past = now - shownAt - wordsSeconds;
            return wordsFadeSeconds > 0f ? Mathf.Clamp01(1f - past / wordsFadeSeconds) : (past < 0f ? 1f : 0f);
        }

        // Full black behind the HEE HEE (user, 2026-11-20: "the color still comes too soon after dying"): the
        // GAME_OVER canvas is only the words, so once Dying... ends (its grey post-processing goes off) the coloured
        // world showed round them. A quad on the camera, drawn over everything (ZTest Always) but before the
        // see-through queue, so the HEE HEE canvas still shows on it.
        private Renderer black;
        private Material blackMat;

        private void SetBlack(float alpha)
        {
            var cam = Camera.main;
            if (black == null)
            {
                if (alpha <= 0f || cam == null) return;
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Game_Over_Black";
                Destroy(quad.GetComponent<Collider>());
                quad.transform.SetParent(cam.transform, false);
                quad.transform.localPosition = new Vector3(0f, 0f, Mathf.Max(0.1f, cam.nearClipPlane * 4f));
                quad.transform.localScale = new Vector3(4f, 4f, 1f);
                blackMat = new Material(Canvas.GetDefaultCanvasMaterial()); // UI/Default, always in builds
                blackMat.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
                blackMat.renderQueue = 2999;
                black = quad.GetComponent<Renderer>();
                black.sharedMaterial = blackMat;
                black.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                black.receiveShadows = false;
            }
            black.enabled = alpha > 0f;
            blackMat.color = new Color(0f, 0f, 0f, alpha);
        }
    }
}
