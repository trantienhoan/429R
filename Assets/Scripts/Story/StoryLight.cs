using Game.Locomotion;
using Game.Saving;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Story
{
    /// <summary>
    /// The story's bright light (a seed taken on the main game): fills the headset view with the light colour, plays the
    /// girl's voice, and stays until the next room has started, then fades. Every main room starts with a GAMESTAGES state
    /// that saves its checkpoint (Set Checkpoint: CP Bedroom, CP 2.5 - BR, CP LivingRoom, CP Scale LR, CP Rooftop), so
    /// entering such a state is the cue (even when the save already had that checkpoint);
    /// Story Seed Settings' Most Seconds is the longest it stays (paused time not counted). While a notice or the pause
    /// menu has the game paused it steps aside so they can be read, and comes back after. It lets go at once if the
    /// player is dying.
    /// It has its own sphere around the eyes, a copy of the Headset Fade's, so the death fall's and the gates' fades never
    /// meet it, and Headset Fade's Find never finds it.
    /// The whole seed (the ending, PlayEnd) instead holds for End Hold Seconds with the story's last words written in the
    /// light (End Title and End Line) and the end voice.
    /// </summary>
    public class StoryLight : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static StoryLight instance;

        private MeshRenderer sphere;
        private MaterialPropertyBlock block;
        private AudioSource speaker;
        private Color color = Color.white;
        private float alpha, from, target, seconds, elapsed;
        private float held, inSeconds, outSeconds, mostSeconds;
        private bool fading, holding;
        private PlayMakerFSM stages;
        private string lastState;
        private bool reached;
        // The ending: a set hold instead of waiting for the next room, and its words in the light.
        private float endHold = -1f;
        private TextMeshPro words;
        private Material wordsLook;

        /// <summary>How much of the view the light fills right now, 0 to 1.</summary>
        public static float Amount => instance != null ? instance.alpha : 0f;

        /// <summary>Whether the light is waiting for the next room.</summary>
        public static bool IsHolding => instance != null && instance.holding;

        public static void Play(StorySeedSettings settings)
        {
            var light = Get();
            if (light == null) return;
            light.endHold = -1f;
            light.Begin(settings, settings.Voice);
        }

        /// <summary>The whole seed: the light holds a set time with the story's last words in it.</summary>
        public static void PlayEnd(StorySeedSettings settings)
        {
            var light = Get();
            if (light == null) return;
            light.endHold = settings.EndHoldSeconds;
            light.Begin(settings, settings.EndVoice);
            light.Write(settings);
        }

        /// <summary>The words shown in the light now ("" when none), for tests.</summary>
        public static string Words => instance != null && instance.words != null && instance.words.gameObject.activeSelf
            ? instance.words.text : "";

        private static StoryLight Get()
        {
            if (instance != null) return instance;
            var fade = HeadsetFade.Find();
            var shape = fade != null ? fade.GetComponent<MeshFilter>() : null;
            var look = fade != null ? fade.GetComponent<MeshRenderer>() : null;
            if (shape == null || look == null || look.sharedMaterial == null) return null;

            var go = new GameObject("Story_Light") { layer = fade.gameObject.layer };
            go.transform.SetParent(fade.transform.parent, false);
            go.transform.localPosition = fade.transform.localPosition;
            go.transform.localRotation = fade.transform.localRotation;
            go.transform.localScale = fade.transform.localScale;
            go.AddComponent<MeshFilter>().sharedMesh = shape.sharedMesh;
            var sphere = go.AddComponent<MeshRenderer>();
            sphere.sharedMaterial = new Material(look.sharedMaterial) { renderQueue = (int)RenderQueue.Overlay };
            sphere.shadowCastingMode = ShadowCastingMode.Off;
            sphere.receiveShadows = false;
            sphere.enabled = false;
            instance = go.AddComponent<StoryLight>();
            return instance;
        }

        private void Awake()
        {
            sphere = GetComponent<MeshRenderer>();
            block = new MaterialPropertyBlock();
        }

        private void Begin(StorySeedSettings settings, AudioClip voice)
        {
            if (words != null) words.gameObject.SetActive(false);
            color = settings.LightColor;
            inSeconds = settings.LightIn;
            outSeconds = settings.LightOut;
            mostSeconds = settings.MostSeconds;
            held = 0f;
            holding = true;
            stages = GameStages.Fsm();
            lastState = stages != null ? stages.ActiveStateName : null;
            // Also the save itself, for a state that is left in the same frame it was entered.
            reached = false;
            Checkpoints.Reached -= OnCheckpoint;
            Checkpoints.Reached += OnCheckpoint;
            FadeTo(1f, settings.LightIn);

            if (voice == null) return;
            if (speaker == null)
            {
                speaker = gameObject.AddComponent<AudioSource>();
                speaker.playOnAwake = false;
                speaker.spatialBlend = 0f;
            }
            speaker.PlayOneShot(voice, settings.VoiceVolume);
        }

        // The story's last words, in the light: a text a little in front of the eyes, drawn over the light (its own
        // copy of the font's material, after the light's queue and through it), fading with it.
        private void Write(StorySeedSettings settings)
        {
            var font = settings.TitleFont != null ? settings.TitleFont : TMP_Settings.defaultFontAsset;
            if (font == null || string.IsNullOrEmpty(settings.EndTitle)) return;
            if (words == null)
            {
                var go = new GameObject("Story_Light_Words") { layer = gameObject.layer };
                go.transform.SetParent(transform.parent, false);
                go.transform.localPosition = new Vector3(0f, 0f, 1.2f);
                go.transform.localRotation = Quaternion.identity;
                words = go.AddComponent<TextMeshPro>();
                words.alignment = TextAlignmentOptions.Center;
                words.textWrappingMode = TextWrappingModes.NoWrap;
                words.rectTransform.sizeDelta = new Vector2(2f, 0.6f);
            }
            if (words.font != font || wordsLook == null)
            {
                words.font = font;
                wordsLook = new Material(font.material) { renderQueue = (int)RenderQueue.Overlay + 1 };
                wordsLook.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
                words.fontSharedMaterial = wordsLook;
            }
            words.fontSize = 1.1f;
            words.text = settings.EndTitle + (string.IsNullOrEmpty(settings.EndLine) ? "" : "\n<size=60%>" + settings.EndLine + "</size>");
            words.color = settings.TitleColor;
            words.alpha = 0f;
            words.gameObject.SetActive(true);
        }

        private void OnCheckpoint(string checkpoint)
        {
            reached = true;
        }

        // The next room has started: GAMESTAGES went into a state that saves a checkpoint.
        private bool NextRoom()
        {
            if (reached) return true;
            var state = stages != null ? stages.Fsm.ActiveState : null;
            if (state == null || state.Name == lastState) return false;
            lastState = state.Name;
            foreach (var action in state.Actions)
                if (action is HutongGames.PlayMaker.Actions.SetCheckpoint) return true;
            return false;
        }

        private void Release(float fadeSeconds)
        {
            if (!holding) return;
            holding = false;
            Checkpoints.Reached -= OnCheckpoint;
            FadeTo(0f, fadeSeconds);
        }

        private void FadeTo(float amount, float overSeconds)
        {
            from = alpha;
            target = amount;
            seconds = Mathf.Max(0f, overSeconds);
            elapsed = 0f;
            fading = true;
        }

        // Real time, like Headset Fade, so it still ends while the game is paused.
        private void Update()
        {
            if (holding)
            {
                // A notice or the pause menu stops the game (time scale 0): out of their way, back when it goes on.
                bool paused = Time.timeScale <= 0f;
                if (!paused) held += Time.unscaledDeltaTime;
                bool over = endHold > 0f ? held > endHold : NextRoom() || held > mostSeconds;
                if (Checkpoints.IsDying) Release(0.2f);
                else if (over) Release(outSeconds);
                else if (paused && target > 0f) FadeTo(0f, 0.25f);
                else if (!paused && target < 1f) FadeTo(1f, inSeconds);
            }
            if (fading)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = seconds > 0f ? Mathf.Clamp01(elapsed / seconds) : 1f;
                Apply(Mathf.Lerp(from, target, t * t * (3f - 2f * t)));
                if (t >= 1f) fading = false;
            }
            ShowWords();
        }

        // The words come in once the light is full, and go with it.
        private void ShowWords()
        {
            if (words == null || !words.gameObject.activeSelf) return;
            float shown = holding ? Mathf.MoveTowards(words.alpha, alpha >= 0.95f ? 1f : 0f, Time.unscaledDeltaTime / 0.6f)
                                  : Mathf.Min(words.alpha, alpha);
            words.alpha = shown;
            if (!holding && alpha <= 0.001f) words.gameObject.SetActive(false);
        }

        private void Apply(float amount)
        {
            alpha = amount;
            sphere.enabled = alpha > 0.001f;
            if (!sphere.enabled) return;
            var shown = color;
            shown.a *= alpha;
            block.SetColor(BaseColor, shown);
            sphere.SetPropertyBlock(block);
        }

        private void OnDestroy()
        {
            Checkpoints.Reached -= OnCheckpoint;
            if (words != null) Destroy(words.gameObject);
            if (instance == this) instance = null;
        }
    }
}
