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
    /// "-8" / "+5" next to the number, and a heal makes the heart pop. The HUD shows for a few seconds after any change, then fades, and stays up
    /// while health is low; then the heart beats, and the player hears it beat too (Heartbeat), faster as health drops.
    /// Once the player has died and got back up this run (a revive), the heart is cracked
    /// (until a new run). The player can hide it all in the pause menu (Health: hidden), e.g. to record video: the
    /// hit and heal flashes on it (particles the PlayerHealth FSM switches on) are hidden too. While a smoked boong's buff
    /// is on (Player Buffs) the heart smokes white and stays in view, with a countdown under it for each
    /// buff (its colour, no name), blinking red over the last 10 seconds. It only reads the FSM, so damage and heals still
    /// all happen there.
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
        [Tooltip("The crack over the heart, shown once the player has died this run (optional).")]
        [SerializeField] private Image crack;
        [Tooltip("White smoke round the heart while a boong buff is on (optional). It fades with the HUD. " +
                 "Tools > 429 Game > Player > Add Buff Smoke To Health HUD makes it.")]
        [SerializeField] private ParticleSystem buffSmoke;

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
        [Tooltip("Heartbeats per second near 0 health: the heart beats faster and faster as health drops, from Beats Per " +
                 "Second. Less than Beats Per Second (e.g. 0) = always Beats Per Second.")]
        [SerializeField] private float beatsPerSecondNearZero = 2.2f;
        [Tooltip("How much bigger the heart pops on a heal (0.2 = 20%), and for how long.")]
        [Min(0f)]
        [SerializeField] private float healPop = 0.2f;
        [Min(0.05f)]
        [SerializeField] private float healPopSeconds = 0.3f;

        [Header("Heartbeat")]
        [Tooltip("Heard at low health, in step with the beating heart: a thump at the big beat (lub) and a softer one at " +
                 "the small beat (dub), so the player feels the danger without looking at the HUD. Empty = a soft thump " +
                 "the game makes itself. Silent while the health is hidden (pause menu), the game is stopped, or health is 0.")]
        [SerializeField] private AudioClip heartbeatSound;
        [Tooltip("How loud the heartbeat is when health has just got low (x) and near 0 health (y). 0 and 0 = no heartbeat.")]
        [SerializeField] private Vector2 heartbeatVolume = new(0.35f, 0.8f);
        [Tooltip("How loud the softer second beat (dub) is, as a share of the first (lub).")]
        [Range(0f, 1f)]
        [SerializeField] private float dubShare = 0.6f;

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
        [Tooltip("Degrees right (+) / left (-) and up (+) / down (-) from straight ahead. (-24, 16) = the top left corner " +
                 "of the view; the Saved sign is in the top right.")]
        [SerializeField] private Vector2 angles = new(-24f, 16f);
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
        private float healedAt = -100f;
        private bool cracked;
        private ParticleSystemRenderer sparkleRenderer;
        private MaterialPropertyBlock sparkleBlock;
        private bool wasShown = true;
        // Every particle effect on the HUD (the hit and heal flashes the PlayerHealth FSM switches on, the sparkles): a
        // canvas group doesn't reach particles, so they're switched off themselves while the health is hidden.
        private ParticleSystemRenderer[] effects = System.Array.Empty<ParticleSystemRenderer>();
        private bool effectsShown = true;
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        // Boong buffs: the countdowns under the heart (made when the first buff starts) and the smoke.
        private const float TimerHeight = 46f;
        private const float TimerWidth = 150f;
        private const float TimerGap = 6f;
        private const float TimerWarnSeconds = 10f;
        private static readonly Color TimerWarnColor = new(1f, 0.35f, 0.3f);
        private RectTransform timers;
        private RectTransform timerPlate;
        private TMP_Text[] timerTexts;
        private int[] timerSeconds;
        private int timerMask = -1;
        private bool wasBuffed;
        private ParticleSystemRenderer smokeRenderer;
        private MaterialPropertyBlock smokeBlock;

        // The heartbeat: counts heartbeats (one per whole number; the big beat at .0, the small one at .25), so the
        // heart and the sound stay in step while the beat speeds up.
        private const float DubAt = 0.25f;
        private float beatClock;
        private bool beating;
        private AudioSource heartbeatSource;
        private AudioClip heartbeatClip;
        private static AudioClip madeHeartbeat;

        // Where the player's choice is kept (on for a new player).
        private const string ShownKey = "429 Game.HealthHUD";
        private static bool? playerChoice;

        /// <summary>
        /// The player's choice (pause menu): whether the health shows at all (off e.g. to record video; the health still
        /// works the same). Setting it saves it.
        /// </summary>
        public static bool Shown
        {
            get => playerChoice ??= PlayerPrefs.GetInt(ShownKey, 1) != 0;
            set
            {
                if (value == Shown) return;
                playerChoice = value;
                PlayerPrefs.SetInt(ShownKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static string Label() => Shown ? "Health: shown" : "Health: hidden";

        /// <summary>The settings were wiped (reset to a new install): the choice is read again, shown for a new player.</summary>
        public static void ForgetChoice() => playerChoice = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => playerChoice = null;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            if (change != null) changeHome = change.rectTransform.anchoredPosition;
            effects = GetComponentsInChildren<ParticleSystemRenderer>(true);
            Place();
            if (heartbeatVolume.x > 0f || heartbeatVolume.y > 0f)
            {
                // Heard in both ears the same (it's the player's own heart), and never cut off by other sounds.
                heartbeatSource = gameObject.AddComponent<AudioSource>();
                heartbeatSource.playOnAwake = false;
                heartbeatSource.loop = false;
                heartbeatSource.spatialBlend = 0f;
                heartbeatSource.priority = 0;
                heartbeatClip = heartbeatSound != null ? heartbeatSound : MakeHeartbeat();
            }
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
            // Cracked once the player has died this run: shown as it happens.
            bool crackedNow = Game.Saving.Checkpoints.DeathsThisRun > 0;
            if (crackedNow != cracked)
            {
                cracked = crackedNow;
                changedAt = Time.unscaledTime;
            }
            // Switched back on in the pause menu: shown for a moment, so the player sees it's back.
            if (Shown != wasShown)
            {
                wasShown = Shown;
                if (wasShown) changedAt = Time.unscaledTime;
            }
            Refresh(false);
        }

        private void Changed(float amount)
        {
            changedAt = Time.unscaledTime;
            if (amount > 0f) healedAt = Time.unscaledTime;
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
            bool beatingNow = low && hp > 0f;
            if (!instant) Heartbeat(beatingNow, share);
            if (heart != null)
            {
                float beat = 0f;
                if (beatingNow)
                {
                    // lub-dub: a big beat, a smaller one, then a rest
                    float phase = Mathf.Repeat(beatClock, 1f);
                    if (phase < 0.25f) beat = Mathf.Sin(phase * 4f * Mathf.PI);
                    else if (phase < 0.5f) beat = 0.6f * Mathf.Sin((phase - 0.25f) * 4f * Mathf.PI);
                }
                // A heal: one quick swell and back.
                float popT = (time - healedAt) / healPopSeconds;
                float pop = popT >= 0f && popT < 1f ? healPop * Mathf.Sin(popT * Mathf.PI) : 0f;
                heart.localScale = Vector3.one * (1f + 0.12f * beat + pop);
            }
            if (crack != null && crack.enabled != cracked) crack.enabled = cracked;
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

            // A boong buff keeps the HUD up (its countdown is under the heart); when the last one ends, it fades as usual.
            bool buffed = Game.Leveling.PlayerBuffs.AnyActive;
            if (wasBuffed && !buffed) changedAt = time;
            wasBuffed = buffed;

            float shown = time - changedAt;
            group.alpha = !Shown ? 0f : alwaysShow || low || buffed ? 1f : 1f - Mathf.Clamp01((shown - showSeconds) / fadeSeconds);
            Sparkle(bonus && group.alpha > 0.01f, group.alpha);
            Smoke(buffed && group.alpha > 0.01f, group.alpha);
            UpdateTimers(buffed);
            if (effectsShown != Shown)
            {
                effectsShown = Shown;
                foreach (var effect in effects)
                    if (effect != null) effect.enabled = effectsShown;
            }
        }

        // Low health: moves the heartbeat on (faster the lower the health) and thumps at the big and the small beat.
        // The first beat comes at once when health gets low.
        private void Heartbeat(bool on, float share)
        {
            if (!on)
            {
                beating = false;
                return;
            }
            // 0 just at the low mark, 1 at 0 health.
            float danger = lowShare > 0f ? 1f - Mathf.Clamp01(share / lowShare) : 1f;
            float rate = Mathf.Lerp(beatsPerSecond, Mathf.Max(beatsPerSecond, beatsPerSecondNearZero), danger);
            if (!beating)
            {
                beating = true;
                beatClock = -0.0001f;
            }
            float was = beatClock;
            beatClock += Time.unscaledDeltaTime * Mathf.Max(0f, rate);
            // Whole heartbeats only: keeps the number small without moving the beat.
            if (beatClock > 1000f)
            {
                beatClock -= 1000f;
                was -= 1000f;
            }

            if (Mathf.Floor(beatClock) > Mathf.Floor(was)) Thump(1f, danger);
            if (Mathf.Floor(beatClock - DubAt) > Mathf.Floor(was - DubAt)) Thump(dubShare, danger);
        }

        private void Thump(float loudness, float danger)
        {
            // Not while the player has hidden the health, or the game is stopped (pause menu, a notice), so it can't
            // pile up and play all at once afterwards.
            if (heartbeatSource == null || heartbeatClip == null || !Shown || Time.timeScale <= 0f || AudioListener.pause) return;
            float volume = Mathf.Lerp(heartbeatVolume.x, heartbeatVolume.y, danger) * loudness;
            if (volume > 0.001f) heartbeatSource.PlayOneShot(heartbeatClip, volume);
        }

        // A soft, deep thump (about 0.16 s): a low tone falling in pitch with a quick start and a fast fade, plus a
        // little of its overtones so small headset speakers can play it.
        private static AudioClip MakeHeartbeat()
        {
            if (madeHeartbeat != null) return madeHeartbeat;
            const int rate = 44100;
            int count = Mathf.RoundToInt(0.16f * rate);
            var samples = new float[count];
            float angle = 0f;
            float peak = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / rate;
                float envelope = (1f - Mathf.Exp(-t / 0.004f)) * Mathf.Exp(-t / 0.045f);
                float pitch = Mathf.Lerp(95f, 48f, Mathf.Clamp01(t / 0.12f));
                angle += 2f * Mathf.PI * pitch / rate;
                float s = envelope * (Mathf.Sin(angle) + 0.3f * Mathf.Sin(2f * angle) + 0.12f * Mathf.Sin(3f * angle));
                samples[i] = s;
                peak = Mathf.Max(peak, Mathf.Abs(s));
            }
            if (peak > 0f)
            {
                for (int i = 0; i < count; i++) samples[i] *= 0.9f / peak;
            }
            madeHeartbeat = AudioClip.Create("Heartbeat", count, 1, rate, false);
            madeHeartbeat.SetData(samples, 0);
            return madeHeartbeat;
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
            // The renderer can outlive the block (Unity keeps it through a script reload; the block it doesn't).
            sparkleBlock ??= new MaterialPropertyBlock();
            sparkleBlock.SetColor(ColorId, new Color(1f, 1f, 1f, alpha));
            sparkleRenderer.SetPropertyBlock(sparkleBlock);
        }

        // Buff smoke: white puffs round the heart while a boong buff is on, fading with the HUD like the sparkles.
        private void Smoke(bool on, float alpha)
        {
            if (buffSmoke == null) return;
            if (on && !buffSmoke.isEmitting) buffSmoke.Play(true);
            else if (!on && buffSmoke.isEmitting) buffSmoke.Stop(true, alpha > 0.01f ? ParticleSystemStopBehavior.StopEmitting : ParticleSystemStopBehavior.StopEmittingAndClear);
            if (!on && !buffSmoke.IsAlive(true)) return;
            if (smokeRenderer == null)
            {
                smokeRenderer = buffSmoke.GetComponent<ParticleSystemRenderer>();
                smokeBlock = new MaterialPropertyBlock();
            }
            if (smokeRenderer == null) return;
            smokeBlock.SetColor(ColorId, new Color(1f, 1f, 1f, alpha));
            smokeRenderer.SetPropertyBlock(smokeBlock);
        }

        // The countdowns under the heart: one per buff that's on, in its colour, "0:57", blinking red near the end.
        private void UpdateTimers(bool buffed)
        {
            if (!buffed)
            {
                if (timers != null && timers.gameObject.activeSelf) timers.gameObject.SetActive(false);
                timerMask = -1;
                return;
            }
            if (!BuildTimers()) return;
            if (!timers.gameObject.activeSelf) timers.gameObject.SetActive(true);

            int mask = 0;
            for (int i = 0; i < Game.Leveling.PlayerBuffs.Count; i++)
            {
                if (Game.Leveling.PlayerBuffs.IsActive((Game.Leveling.Buff)i)) mask |= 1 << i;
            }
            if (mask != timerMask) LayOutTimers(mask);

            bool blinkOn = Mathf.Repeat(Time.time * 2f, 1f) < 0.6f;
            for (int i = 0; i < timerTexts.Length; i++)
            {
                if ((mask & (1 << i)) == 0) continue;
                var buff = (Game.Leveling.Buff)i;
                float left = Game.Leveling.PlayerBuffs.SecondsLeft(buff);
                int seconds = Mathf.CeilToInt(left);
                if (seconds != timerSeconds[i])
                {
                    timerSeconds[i] = seconds;
                    timerTexts[i].SetText("{0}:{1:00}", seconds / 60, seconds % 60);
                }
                timerTexts[i].color = left <= TimerWarnSeconds && blinkOn ? TimerWarnColor : Game.Leveling.PlayerBuffs.ColorOf(buff);
            }
        }

        // One row per buff that's on, top down under the heart; the plate behind them as tall as they need.
        private void LayOutTimers(int mask)
        {
            timerMask = mask;
            int shown = 0;
            for (int i = 0; i < timerTexts.Length; i++)
            {
                bool on = (mask & (1 << i)) != 0;
                if (timerTexts[i].gameObject.activeSelf != on) timerTexts[i].gameObject.SetActive(on);
                if (!on) continue;
                timerTexts[i].rectTransform.anchoredPosition = new Vector2(0f, -TimerGap - TimerHeight * (shown + 0.5f));
                timerSeconds[i] = -1;
                shown++;
            }
            if (timerPlate != null) timerPlate.sizeDelta = new Vector2(TimerWidth, shown * TimerHeight + TimerGap * 2f);
        }

        // Made from the HUD's own parts (the number's font and overlay material, the plate's look), under the heart.
        private bool BuildTimers()
        {
            if (timers != null) return true;
            if (number == null) return false;
            var anchor = heart != null ? heart : (RectTransform)transform;
            var go = new GameObject("Buff Timers", typeof(RectTransform));
            go.layer = gameObject.layer;
            timers = (RectTransform)go.transform;
            timers.SetParent(transform, false);
            timers.anchorMin = timers.anchorMax = new Vector2(0.5f, 0.5f);
            timers.pivot = new Vector2(0.5f, 1f);
            timers.sizeDelta = new Vector2(TimerWidth, 0f);
            float bottom = anchor.anchoredPosition.y - anchor.rect.height * anchor.pivot.y;
            timers.anchoredPosition = new Vector2(anchor.anchoredPosition.x, bottom - 8f);

            var plate = transform.Find("Plate");
            if (plate != null)
            {
                timerPlate = (RectTransform)Instantiate(plate, timers, false);
                timerPlate.name = "Plate";
                timerPlate.anchorMin = timerPlate.anchorMax = new Vector2(0.5f, 1f);
                timerPlate.pivot = new Vector2(0.5f, 1f);
                timerPlate.anchoredPosition = Vector2.zero;
            }

            timerTexts = new TMP_Text[Game.Leveling.PlayerBuffs.Count];
            timerSeconds = new int[timerTexts.Length];
            for (int i = 0; i < timerTexts.Length; i++)
            {
                var text = Instantiate(number, timers, false);
                text.name = ((Game.Leveling.Buff)i).ToString();
                text.enableAutoSizing = false;
                text.fontSize = 40f;
                text.alignment = TextAlignmentOptions.Center;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                var rect = text.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(TimerWidth, TimerHeight);
                text.gameObject.SetActive(false);
                timerTexts[i] = text;
            }
            return true;
        }

        /// <summary>Moves the canvas to the Distance and Angles in front of the eyes (the setup menu calls it too).</summary>
        public void Place()
        {
            if (!place) return;
            var dir = Quaternion.Euler(-angles.y, angles.x, 0f) * Vector3.forward;
            // A canvas keeps its sideways and up/down place as its anchored position; set it there so the scene saves it.
            if (transform is RectTransform rect) rect.anchoredPosition3D = dir * distance;
            else transform.localPosition = dir * distance;
            transform.localRotation = Quaternion.LookRotation(dir, Vector3.up);
            transform.localScale = Vector3.one * (scaleAtOneMetre * distance);
        }
    }
}
