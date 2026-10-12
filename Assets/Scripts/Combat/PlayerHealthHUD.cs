using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FsmFloat = HutongGames.PlayMaker.FsmFloat;

namespace Game.Combat
{
    /// <summary>
    /// The player's health on the head-locked canvas: a heart and the health as a number. The heart drains from the top
    /// as health drops below the starting health (the PlayerHealth FSM's DefautPlayerHealth, 120), and turns green,
    /// with little green sparkles flying up around it, when candy has pushed health above it. Health has no cap, so the number tells the rest. Each change pops a
    /// "-8" / "+5" next to the number. The HUD shows for a few seconds after any change, then fades, and stays up
    /// while health is low. It only reads the FSM, so damage and heals still all happen there.
    /// Goes on the HUD canvas under the Main Camera. Tools > 429 Game > Player > Set Up Health HUD sets it up.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public class PlayerHealthHUD : MonoBehaviour
    {
        [Header("Health")]
        [Tooltip("The PlayerHealth FSM (on the XR Origin). Left empty, the FSM called PlayerHealth is found in the scene.")]
        [SerializeField] private PlayMakerFSM healthFsm;
        [SerializeField] private string healthVariable = "PlayerHealth";
        [Tooltip("The starting health, which a full red heart stands for.")]
        [SerializeField] private string startHealthVariable = "DefautPlayerHealth";

        [Header("Parts")]
        [SerializeField] private Image heartFill;
        [SerializeField] private Image heartEmpty;
        [Tooltip("The heart's transform, for the heartbeat at low health.")]
        [SerializeField] private RectTransform heart;
        [SerializeField] private TMP_Text number;
        [SerializeField] private TMP_Text change;
        [Tooltip("Sparkles around the heart while health is above the starting health (optional). They fade with the HUD.")]
        [SerializeField] private ParticleSystem bonusSparkles;

        [Header("Colours")]
        [SerializeField] private Color healthColor = new(0.93f, 0.16f, 0.2f);
        [Tooltip("The heart and number when health is above the starting health.")]
        [SerializeField] private Color bonusColor = new(0.3f, 1f, 0.42f);
        [SerializeField] private Color emptyColor = new(0.22f, 0.04f, 0.06f, 0.92f);
        [SerializeField] private Color numberColor = Color.white;
        [SerializeField] private Color lowNumberColor = new(1f, 0.36f, 0.36f);
        [SerializeField] private Color damageColor = new(1f, 0.32f, 0.3f);
        [SerializeField] private Color healColor = new(0.45f, 1f, 0.45f);

        [Header("Low health")]
        [Tooltip("At or below this share of the starting health the number turns red, the heart beats and the HUD stays up.")]
        [Range(0f, 1f)]
        [SerializeField] private float lowShare = 0.25f;
        [Tooltip("Heartbeats per second at low health.")]
        [SerializeField] private float beatsPerSecond = 1.6f;

        [Header("Showing")]
        [Tooltip("Never fade out.")]
        [SerializeField] private bool alwaysShow;
        [Tooltip("Seconds the HUD stays after health changes, then how long it takes to fade.")]
        [Min(0f)]
        [SerializeField] private float showSeconds = 3f;
        [Min(0.01f)]
        [SerializeField] private float fadeSeconds = 0.5f;
        [Tooltip("Seconds a -8 / +5 stays, and how far (canvas units) it floats up meanwhile.")]
        [Min(0.1f)]
        [SerializeField] private float changeSeconds = 1.1f;
        [SerializeField] private float changeRise = 40f;

        [Header("Placement")]
        [Tooltip("Place the HUD in front of the eyes from the values below (on start, and live while you change them).")]
        [SerializeField] private bool place = true;
        [Tooltip("Metres from the eyes. Closer than about half a metre is tiring to focus on in VR.")]
        [Range(0.3f, 2f)]
        [SerializeField] private float distance = 0.5f;
        [Tooltip("Degrees right (+) / left (-) and up (+) / down (-) from straight ahead.")]
        [SerializeField] private Vector2 angles = new(-20f, -10f);
        [Tooltip("Canvas scale at 1 m. The canvas scales with the distance, so the HUD looks the same size.")]
        [SerializeField] private float scaleAtOneMetre = 0.0009f;

        private CanvasGroup group;
        private FsmFloat health;
        private FsmFloat startHealth;
        private float lastHealth;
        private float changedAt = -100f;
        private float changeAt = -100f;
        private float changeAmount;
        private Vector2 changeHome;
        private bool ready;
        private ParticleSystemRenderer sparkleRenderer;
        private MaterialPropertyBlock sparkleBlock;
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            if (change != null) changeHome = change.rectTransform.anchoredPosition;
            Place();
        }

        private void OnEnable()
        {
            // The FSM switches this canvas on as the first thing it does on a hit or a heal, before it changes the
            // health, so the value read here is the one from before the change.
            ready = Find();
            if (!ready) return;
            lastHealth = health.Value;
            Refresh(true);
        }

        private void Start()
        {
            // Look the variables up again once every FSM has started, in case this woke up before the FSM did.
            health = null;
            startHealth = null;
            ready = Find();
            if (!ready) return;
            lastHealth = health.Value;
            Refresh(true);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Moving a transform inside OnValidate makes Unity complain, so it waits a moment.
            if (place) UnityEditor.EditorApplication.delayCall += () => { if (this != null) Place(); };
        }
#endif

        private bool Find()
        {
            if (health != null && startHealth != null) return true;
            if (healthFsm == null)
            {
                foreach (var fsm in FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (fsm.FsmName == "PlayerHealth") { healthFsm = fsm; break; }
                }
            }
            if (healthFsm != null)
            {
                health = healthFsm.FsmVariables.FindFsmFloat(healthVariable);
                startHealth = healthFsm.FsmVariables.FindFsmFloat(startHealthVariable);
            }
            if (health == null || startHealth == null)
            {
                Debug.LogWarning($"[Health HUD] Needs the PlayerHealth FSM with the floats '{healthVariable}' and " +
                                 $"'{startHealthVariable}'. Run Tools > 429 Game > Player > Set Up Health HUD.", this);
                return false;
            }
            return true;
        }

        private void Update()
        {
            if (!ready) return;
            float now = health.Value;
            if (Mathf.Abs(now - lastHealth) > 0.01f)
            {
                Changed(now - lastHealth);
                lastHealth = now;
            }
            Refresh(false);
        }

        private void Changed(float amount)
        {
            changedAt = Time.unscaledTime;
            if (change == null) return;
            // Quick hits in a row add up in one pop instead of flickering.
            bool sameWay = Mathf.Sign(amount) == Mathf.Sign(changeAmount);
            if (Time.unscaledTime - changeAt < changeSeconds * 0.5f && sameWay) changeAmount += amount;
            else changeAmount = amount;
            changeAt = Time.unscaledTime;
            int shown = Mathf.RoundToInt(changeAmount);
            change.SetText(shown > 0 ? "+{0}" : "{0}", shown);
            change.color = shown > 0 ? healColor : damageColor;
        }

        private void Refresh(bool instant)
        {
            float hp = health.Value;
            float full = Mathf.Max(1f, startHealth.Value);
            float share = hp / full;
            bool bonus = hp > full + 0.01f;
            bool low = hp <= full * lowShare;
            float time = Time.unscaledTime;

            if (heartFill != null)
            {
                heartFill.fillAmount = Mathf.Clamp01(share);
                var target = bonus ? bonusColor * (1f + 0.08f * Mathf.Sin(time * 3f)) : healthColor;
                target.a = 1f;
                heartFill.color = instant ? target : Color.Lerp(heartFill.color, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 8f));
            }
            if (heartEmpty != null) heartEmpty.color = emptyColor;
            if (heart != null)
            {
                float beat = 0f;
                if (low && hp > 0f)
                {
                    // lub-dub: a big beat, a smaller one, then a rest
                    float phase = Mathf.Repeat(time * beatsPerSecond, 1f);
                    if (phase < 0.25f) beat = Mathf.Sin(phase * 4f * Mathf.PI);
                    else if (phase < 0.5f) beat = 0.6f * Mathf.Sin((phase - 0.25f) * 4f * Mathf.PI);
                }
                heart.localScale = Vector3.one * (1f + 0.12f * beat);
            }
            if (number != null)
            {
                number.SetText("{0}", Mathf.Max(0, Mathf.CeilToInt(hp - 0.001f)));
                number.color = low ? lowNumberColor : (bonus ? bonusColor : numberColor);
            }
            if (change != null)
            {
                float t = (time - changeAt) / changeSeconds;
                bool on = t >= 0f && t < 1f;
                if (change.enabled != on) change.enabled = on;
                if (on)
                {
                    change.rectTransform.anchoredPosition = changeHome + Vector2.up * (changeRise * t);
                    var c = change.color;
                    c.a = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
                    change.color = c;
                }
            }

            float shown = time - changedAt;
            group.alpha = alwaysShow || low ? 1f : 1f - Mathf.Clamp01((shown - showSeconds) / fadeSeconds);
            Sparkle(bonus && group.alpha > 0.01f, group.alpha);
        }

        // Bonus sparkles: emit while health is above the start and the HUD shows; they fade with the HUD (a CanvasGroup
        // doesn't reach particles, so their material colour follows its alpha).
        private void Sparkle(bool on, float alpha)
        {
            if (bonusSparkles == null) return;
            if (on && !bonusSparkles.isEmitting) bonusSparkles.Play(true);
            else if (!on && bonusSparkles.isEmitting) bonusSparkles.Stop(true, alpha > 0.01f ? ParticleSystemStopBehavior.StopEmitting : ParticleSystemStopBehavior.StopEmittingAndClear);
            if (sparkleRenderer == null)
            {
                sparkleRenderer = bonusSparkles.GetComponent<ParticleSystemRenderer>();
                sparkleBlock = new MaterialPropertyBlock();
            }
            if (sparkleRenderer == null) return;
            sparkleBlock.SetColor(ColorId, new Color(1f, 1f, 1f, alpha));
            sparkleRenderer.SetPropertyBlock(sparkleBlock);
        }

        /// <summary>Moves the canvas to the Distance and Angles in front of the eyes (the setup menu calls it too).</summary>
        public void Place()
        {
            if (!place) return;
            var dir = Quaternion.Euler(-angles.y, angles.x, 0f) * Vector3.forward;
            transform.localPosition = dir * distance;
            transform.localRotation = Quaternion.LookRotation(dir, Vector3.up);
            transform.localScale = Vector3.one * (scaleAtOneMetre * distance);
        }
    }
}
