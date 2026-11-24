using UnityEngine;

namespace Game.Story
{
    /// <summary>
    /// The tiny green girl at the start (user's story, 2026-11-23: "a tiny girl with a green light, so weak ... her voices
    /// is getting weaker and weaker"). A Fairy made at the start of a run (GAMESTAGES in Move to Fairy) is weak: her green
    /// glow (her body's own light) is dim, breathes slowly and now and then nearly goes out, and each of her calls is
    /// quieter than the one before. Her cry when you reach her plays as before.
    /// The calls fade by how far her voice carries (her speaker's Min Distance): her call actions (Audio Play) set the
    /// speaker's volume themselves every frame. The player hears her from beyond her reach trigger (5.55 m), where the
    /// voice falls off with the distance, so a shorter reach is a quieter call.
    /// The Fairy made at the end (the reward) is as before: made in any other state, this removes itself.
    /// On the Fairy prefab's root (Tools > 429 Game > Story > Set Up Story Small Touches).
    /// </summary>
    [DisallowMultipleComponent]
    public class StoryWeakFairy : MonoBehaviour
    {
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        [Tooltip("GAMESTAGES states in which a new Fairy is weak: she is made in Move to Fairy at the start of a run.")]
        [SerializeField] private string[] weakStates = { "Move to Fairy" };

        [Header("Her weak green glow")]
        [Tooltip("Her glow against normal (1 = as before).")]
        [Range(0f, 1f)]
        [SerializeField] private float glow = 0.35f;
        [Tooltip("Seconds of one slow breath of her glow; 0 = steady.")]
        [Min(0f)]
        [SerializeField] private float breathSeconds = 2.4f;
        [Tooltip("How much her glow swells and sinks with each breath (0.4 = 40% up and down).")]
        [Range(0f, 1f)]
        [SerializeField] private float breath = 0.4f;
        [Tooltip("Seconds between the moments her glow nearly goes out (random in this range).")]
        [SerializeField] private Vector2 stutterEvery = new Vector2(0.8f, 2.6f);
        [Tooltip("How long her glow stays nearly out, in seconds.")]
        [Min(0f)]
        [SerializeField] private float stutterSeconds = 0.12f;
        [Tooltip("Her glow while it nearly goes out, against normal.")]
        [Range(0f, 1f)]
        [SerializeField] private float stutterGlow = 0.05f;

        [Header("Her fading calls")]
        [Tooltip("Each call is this much of the one before.")]
        [Range(0.3f, 1f)]
        [SerializeField] private float fadePerCall = 0.85f;
        [Tooltip("Her calls never get quieter than this, against normal.")]
        [Range(0f, 1f)]
        [SerializeField] private float quietest = 0.35f;
        [Tooltip("Her FairyHealth FSM's states that are her calls: each one entered is one call.")]
        [SerializeField] private string[] callStates = { "State 1", "State 2" };

        private PlayMakerFSM health;
        private AudioSource voice;
        private float fullReach;
        private Renderer body;
        private Color bodyGlow;
        private MaterialPropertyBlock block;
        private string lastState;
        private float nextStutter;
        private float stutterUntil = -1f;
        private bool weak;

        /// <summary>Her calls so far (for tests).</summary>
        public int Calls { get; private set; }

        /// <summary>Her glow now, against normal (for tests).</summary>
        public float GlowNow { get; private set; } = 1f;

        private void Start()
        {
            var stages = GameStages.Fsm();
            string state = stages != null ? stages.ActiveStateName : null;
            if (state == null || System.Array.IndexOf(weakStates, state) < 0)
            {
                Destroy(this);
                return;
            }
            weak = true;
            foreach (var fsm in GetComponents<PlayMakerFSM>())
                if (fsm.FsmName == "FairyHealth") health = fsm;
            voice = GetComponent<AudioSource>();
            if (voice != null) fullReach = voice.minDistance;
            foreach (var skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var material = skin.sharedMaterial;
                if (material == null || !material.HasProperty(EmissionId) || !material.IsKeywordEnabled("_EMISSION")) continue;
                body = skin;
                bodyGlow = material.GetColor(EmissionId);
                break;
            }
            block = new MaterialPropertyBlock();
            nextStutter = Time.time + Random.Range(stutterEvery.x, stutterEvery.y);
        }

        private void Update()
        {
            if (!weak) return;
            HearCalls();
            float now = Time.time;
            float light = glow;
            if (breathSeconds > 0f) light *= 1f + breath * Mathf.Sin(now * 2f * Mathf.PI / breathSeconds);
            if (now >= nextStutter)
            {
                stutterUntil = now + stutterSeconds;
                nextStutter = stutterUntil + Random.Range(stutterEvery.x, stutterEvery.y);
            }
            if (now < stutterUntil) light = stutterGlow;
            Shine(light);
        }

        private void OnDisable()
        {
            if (!weak) return;
            if (voice != null) voice.minDistance = fullReach;
            Shine(1f);
        }

        // Each call state entered is one call, a little quieter than the last; her cry (any other state) is as before.
        private void HearCalls()
        {
            if (health == null || voice == null) return;
            string state = health.ActiveStateName;
            if (state == lastState) return;
            lastState = state;
            if (System.Array.IndexOf(callStates, state) < 0)
            {
                voice.minDistance = fullReach;
                return;
            }
            Calls++;
            voice.minDistance = fullReach * Mathf.Max(quietest, Mathf.Pow(fadePerCall, Calls - 1));
        }

        private void Shine(float light)
        {
            GlowNow = light;
            if (body == null) return;
            body.GetPropertyBlock(block);
            block.SetColor(EmissionId, bodyGlow * light);
            body.SetPropertyBlock(block);
        }
    }
}
