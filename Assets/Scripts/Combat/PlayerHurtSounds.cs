using UnityEngine;
using FsmFloat = HutongGames.PlayMaker.FsmFloat;

namespace Game.Combat
{
    /// <summary>
    /// The player's own hurt sound (user 2026-11-25, ElevenLabs): whenever their health drops from a hit, heard in their
    /// head: a light hurt for a small hit (a bug's bite or sting) or a heavy one for a big hit (a boss's punch, a slam, a
    /// charge, a blast). Watches the PlayerHealth FSM float on this object, as Damage Vignette does; health the game sets
    /// itself (Get a Rest, poison ticks, a stage's heal) isn't a hit. On the XR Origin (Tools > 429 Game > Audio > Wire
    /// ElevenLabs Sounds (P1)).
    /// </summary>
    public class PlayerHurtSounds : MonoBehaviour
    {
        [Tooltip("The FSM on this object that holds the player's health, and its float variable.")]
        [SerializeField] private string healthFsm = "PlayerHealth";
        [SerializeField] private string healthVariable = "PlayerHealth";

        [Tooltip("A small hit: one at random, never the same twice in a row (player_hurt_light_1-3).")]
        [SerializeField] private AudioClip[] lightHurts = System.Array.Empty<AudioClip>();
        [Tooltip("A big hit: one at random, never the same twice in a row (player_hurt_heavy_1-2).")]
        [SerializeField] private AudioClip[] heavyHurts = System.Array.Empty<AudioClip>();
        [Tooltip("A drop of at least this much health is a big hit (a bite or sting, Damage, takes 6-9; DamageHeavy 40-50).")]
        [Min(0.1f)]
        [SerializeField] private float heavyFrom = 15f;
        [Range(0f, 1f)]
        [SerializeField] private float lightVolume = 0.8f;
        [Range(0f, 1f)]
        [SerializeField] private float heavyVolume = 1f;
        [Tooltip("Seconds before another hurt sound (a big hit right after a small one is still heard).")]
        [Min(0f)]
        [SerializeField] private float minGap = 0.15f;

        private FsmFloat health;
        private float lastHealth;
        private AudioSource speaker;
        private float lastAt = -100f;
        private bool lastWasHeavy;
        private int lastLight = -1;
        private int lastHeavy = -1;

        private void Start()
        {
            foreach (var fsm in GetComponents<PlayMakerFSM>())
                if (fsm.FsmName == healthFsm) health = fsm.FsmVariables.FindFsmFloat(healthVariable);
            if (health == null)
            {
                Debug.LogWarning($"[Player Hurt Sounds] Needs a '{healthFsm}' FSM with a '{healthVariable}' float on this " +
                                 "object (the XR Origin).", this);
                enabled = false;
                return;
            }
            lastHealth = health.Value;
            // Heard in both ears the same: it's the player's own body.
            speaker = gameObject.AddComponent<AudioSource>();
            speaker.playOnAwake = false;
            speaker.spatialBlend = 0f;
            speaker.priority = 16;
        }

        // Health set by the game isn't a hit (as Damage Vignette and Hurt Slow do).
        private void OnEnable() => Game.Saving.PlayerBoosts.HealthSet += TakeHealthAsIs;
        private void OnDisable() => Game.Saving.PlayerBoosts.HealthSet -= TakeHealthAsIs;

        private void TakeHealthAsIs(float change)
        {
            if (health != null) lastHealth += change;
        }

        private void Update()
        {
            if (health == null) return;
            float now = health.Value;
            if (now < lastHealth - 0.01f) Hurt(lastHealth - now);
            lastHealth = now;
        }

        private void Hurt(float damage)
        {
            bool heavy = damage >= heavyFrom;
            if (Time.unscaledTime - lastAt < minGap && (lastWasHeavy || !heavy)) return;
            var clip = heavy ? Pick(heavyHurts, ref lastHeavy) : Pick(lightHurts, ref lastLight);
            if (clip == null && heavy) clip = Pick(lightHurts, ref lastLight);
            if (clip == null || speaker == null) return;
            speaker.PlayOneShot(clip, heavy ? heavyVolume : lightVolume);
            lastAt = Time.unscaledTime;
            lastWasHeavy = heavy;
        }

        private static AudioClip Pick(AudioClip[] clips, ref int last)
        {
            if (clips == null || clips.Length == 0) return null;
            int i = Random.Range(0, clips.Length);
            if (clips.Length > 1 && i == last) i = (i + 1) % clips.Length;
            last = i;
            return clips[i];
        }
    }
}
