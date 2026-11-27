using UnityEngine;

namespace Game.Rooms
{
    /// <summary>
    /// The last fight under the opening's sky (user 2026-11-27: "I want to use the sky, the light of that to the boss area,
    /// so the last fight look just like the intro scene, still keep the current setup floor like we are having right now,
    /// just add the sky").
    /// On Boss_Area (Tools > 429 Game > Story > Boss Fight Under The Opening Sky). While GAMESTAGES is in one of the Fight
    /// States (the boss fight and the ending after it), it puts up a copy of the opening's sky (Opening_Battle's Sky: its
    /// stars, bright stars, clouds and dust) round the player, the opening's moon light and its light all round, and the
    /// fog goes, as in the opening. The game's own skybox (moon, clouds) shows behind it as it does now. The floor stays
    /// as it is: only the opening's ground nobody sees goes under it, so no stars show through it.
    /// When the state leaves them (a new run, a death), it all fades back as it was. Nowhere else Boss_Area is used (the
    /// bonus area, the opening's copy of its flames) does anything happen.
    /// How far the eyes see is left alone (40 m): further, the rooftop's dark Skydome (75 m) would hide the sky. So the
    /// sky is made smaller (Sky Size) to sit inside that, and it moves with the player (it's far off, like a real sky).
    /// </summary>
    [DisallowMultipleComponent]
    public class BossFightSky : MonoBehaviour
    {
        [Tooltip("GAMESTAGES states that show the sky: the boss fight and the ending after it.")]
        [SerializeField] private string[] fightStates = { "BOSS", "wait", "fairy reward", "Thank For Playing" };

        [Header("Looks (Opening_Battle's own)")]
        [Tooltip("The opening's sky (Resources/Opening_Battle > Sky): copied round the player while the fight is on.")]
        [SerializeField] private GameObject sky;
        [Tooltip("Its size against the opening's (its stars are 34-52 m off there; the eyes here see 40 m).")]
        [Min(0.1f)]
        [SerializeField] private float skySize = 0.75f;
        [Tooltip("Its parts left out here (the opening's Floor Ring and Rising Sparkles sit on its dream floor).")]
        [SerializeField] private string[] skipParts = { "Floor Ring", "Rising Sparkles" };
        [Tooltip("The opening's moon (a soft directional light, no shadows).")]
        [SerializeField] private Light moon;
        [Tooltip("The opening's ground nobody sees (429/Depth Only): it hides the stars under the floor. Empty = they show " +
                 "under the floor too.")]
        [SerializeField] private GameObject groundMask;

        [Header("Light")]
        [Tooltip("The light all round while it's on (the opening's).")]
        [SerializeField] private Color ambient = new(0.42f, 0.4f, 0.5f);
        [Tooltip("The fog goes (the opening has none; nothing stands beyond the arena). Off: the scene's fog stays (the sky " +
                 "shows through it either way).")]
        [SerializeField] private bool noFog = true;
        [Tooltip("Seconds it takes to come in and to go.")]
        [Min(0.05f)]
        [SerializeField] private float fadeSeconds = 2.5f;

        /// <summary>Whether a boss fight sky is up now (for tests).</summary>
        public static bool Showing { get; private set; }

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static BossFightSky owner;
        private GameObject skyMade;
        private GameObject maskMade;
        private Light moonMade;
        private float moonFull;
        private float amount;
        private bool on;
        private bool saved;
        private Color ambientBefore;
        private bool fogBefore;
        private float fogStartBefore, fogEndBefore;
        private Transform head;
        private Transform player;
        private MaterialPropertyBlock block;
        private float nextCheck;

        private void Update()
        {
            if (Time.unscaledTime >= nextCheck)
            {
                nextCheck = Time.unscaledTime + 0.25f;
                bool want = InFight() && (owner == null || owner == this);
                if (want && !saved) Begin();
                on = want;
            }
            if (!saved) return;
            amount = Mathf.MoveTowards(amount, on ? 1f : 0f, Time.unscaledDeltaTime / fadeSeconds);
            Apply(amount);
            if (!on && amount <= 0f) End();
        }

        // The sky stays round the player wherever they go (after their head moved this frame).
        private void LateUpdate()
        {
            if (skyMade == null || head == null) return;
            float floor = player != null ? player.position.y : skyMade.transform.position.y;
            skyMade.transform.position = new Vector3(head.position.x, floor, head.position.z);
        }

        private void OnDisable() => End();

        private bool InFight()
        {
            var fsm = GameStages.Fsm();
            if (fsm == null || fsm.Fsm == null) return false;
            string state = fsm.Fsm.ActiveStateName;
            foreach (var s in fightStates) if (s == state) return true;
            return false;
        }

        private void Begin()
        {
            owner = this;
            saved = true;
            amount = 0f;
            ambientBefore = RenderSettings.ambientLight;
            fogBefore = RenderSettings.fog;
            fogStartBefore = RenderSettings.fogStartDistance;
            fogEndBefore = RenderSettings.fogEndDistance;
            var origin = FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
            player = origin != null ? origin.transform : null;
            head = origin != null && origin.Camera != null ? origin.Camera.transform : Camera.main != null ? Camera.main.transform : null;
            float floor = player != null ? player.position.y : transform.position.y;

            if (sky != null)
            {
                var at = head != null ? new Vector3(head.position.x, floor, head.position.z) : transform.position;
                skyMade = Instantiate(sky, at, Quaternion.identity);
                skyMade.name = "Boss Fight Sky";
                skyMade.transform.localScale = sky.transform.localScale * skySize;
                foreach (var part in skyMade.GetComponentsInChildren<Transform>(true))
                    foreach (var skip in skipParts)
                        if (part != skyMade.transform && !string.IsNullOrEmpty(skip) && part.name == skip) part.gameObject.SetActive(false);
            }
            if (groundMask != null)
            {
                // Under the arena's middle, lying as it lies in the opening.
                maskMade = Instantiate(groundMask, new Vector3(transform.position.x, floor - 0.02f, transform.position.z),
                    groundMask.transform.rotation);
                maskMade.name = "Boss Fight Sky Ground";
            }
            if (moon != null)
            {
                moonMade = Instantiate(moon, transform.position, moon.transform.rotation);
                moonMade.name = "Boss Fight Moon";
                moonFull = moon.intensity;
                moonMade.intensity = 0f;
            }
            Showing = true;
            Debug.Log("[Boss Fight Sky] The opening's sky comes over the boss fight.");
        }

        private void Apply(float k)
        {
            float s = Mathf.SmoothStep(0f, 1f, k);
            RenderSettings.ambientLight = Color.Lerp(ambientBefore, ambient, s);
            if (noFog && fogBefore)
            {
                // The fog draws back as it comes in, then goes.
                RenderSettings.fog = k < 1f;
                RenderSettings.fogStartDistance = Mathf.Lerp(fogStartBefore, 60f, s);
                RenderSettings.fogEndDistance = Mathf.Lerp(fogEndBefore, 120f, s);
            }
            if (moonMade != null) moonMade.intensity = moonFull * s;
            if (skyMade != null) Fade(skyMade, s);
        }

        // The sky's particles fade in and out with it (their material's colour, all four parts: additive or blended).
        private void Fade(GameObject made, float s)
        {
            block ??= new MaterialPropertyBlock();
            foreach (var look in made.GetComponentsInChildren<ParticleSystemRenderer>())
            {
                var material = look.sharedMaterial;
                if (material == null || !material.HasProperty(BaseColor)) continue;
                block.Clear();
                block.SetColor(BaseColor, material.GetColor(BaseColor) * s);
                look.SetPropertyBlock(block);
            }
        }

        // Back as it was.
        private void End()
        {
            if (!saved) return;
            saved = false;
            on = false;
            amount = 0f;
            RenderSettings.ambientLight = ambientBefore;
            RenderSettings.fog = fogBefore;
            RenderSettings.fogStartDistance = fogStartBefore;
            RenderSettings.fogEndDistance = fogEndBefore;
            if (skyMade != null) Destroy(skyMade);
            if (maskMade != null) Destroy(maskMade);
            if (moonMade != null) Destroy(moonMade.gameObject);
            skyMade = maskMade = null;
            moonMade = null;
            if (owner == this) owner = null;
            Showing = false;
            Debug.Log("[Boss Fight Sky] Gone: the scene's own light and fog are back.");
        }
    }
}
