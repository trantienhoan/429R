using UnityEngine;

namespace Game.PlayGround
{
    /// <summary>
    /// A stage pumpkin (Pumpkin_0, 1, 2, 4, 6) goes off with a big bang when it's broken: a flash, a burst and a cloud of
    /// smoke (Effects, Size times their own size, and bigger for a bigger pumpkin) and a pumpkin explosion sound. Only
    /// the look and the sound: it hurts and pushes nothing (its pieces still fly apart by its drop's own FSM). It goes off
    /// when its Health FSM's health runs out; not when the fairy's shield pops it or the stage clears it away.
    /// Tools > 429 Game > Items > Make Stage Pumpkins Run Away adds it.
    /// </summary>
    [DisallowMultipleComponent]
    public class PumpkinBoom : MonoBehaviour
    {
        [Tooltip("Played where it breaks (from the Effect Pool), e.g. Flash_round_yellow, Hit_Blast and Smoke_Blast.")]
        [SerializeField] private GameObject[] effects = System.Array.Empty<GameObject>();
        [Tooltip("Size of the effects for a pumpkin 30 cm across, as the stage pumpkins are (1.3 = nearly twice a flip-flop's " +
                 "bang, 0.7); a bigger pumpkin gets a bigger bang.")]
        [Min(0.05f)]
        [SerializeField] private float size = 1.3f;
        [Tooltip("The biggest the bang gets, however big the pumpkin.")]
        [Min(0.05f)]
        [SerializeField] private float maxSize = 2.5f;
        [Tooltip("One of these plays where it breaks.")]
        [SerializeField] private AudioClip[] sounds = System.Array.Empty<AudioClip>();
        [Range(0f, 1f)]
        [SerializeField] private float volume = 1f;

        [Header("PlayMaker")]
        [Tooltip("The FSM whose float \"health\" breaks it at 0.")]
        [SerializeField] private string healthFsm = "Health";
        [SerializeField] private string healthVariable = "health";

        // A pumpkin this wide (metres) gets the effects at Size.
        private const float ReferenceWidth = 0.3f;

        private HutongGames.PlayMaker.FsmFloat health;
        private bool armed;
        private bool done;

        private void Start()
        {
            foreach (var fsm in GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != healthFsm) continue;
                health = fsm.FsmVariables.FindFsmFloat(healthVariable);
                break;
            }
            Game.Weapons.Blast.Warm(effects);
        }

        private void Update()
        {
            if (done || health == null) return;
            // Only once it has had some health (its FSM sets it as it starts).
            if (health.Value > 0f)
            {
                armed = true;
                return;
            }
            if (!armed) return;
            done = true;
            Boom();
        }

        private void Boom()
        {
            var at = transform.position;
            // How wide it is now (its Health FSM grows it in, and some grow bigger than others).
            float width = ReferenceWidth * Mathf.Abs(transform.lossyScale.x);
            if (TryGetComponent(out Collider shape) && shape.enabled)
            {
                at = shape.bounds.center;
                width = Mathf.Max(shape.bounds.size.x, shape.bounds.size.z);
            }
            float scale = Mathf.Min(size * width / ReferenceWidth, maxSize);
            foreach (var effect in effects)
            {
                if (effect != null) EffectPool.Play(effect, at, scale);
            }
            if (sounds.Length > 0)
            {
                var clip = sounds[Random.Range(0, sounds.Length)];
                if (clip != null) AudioSource.PlayClipAtPoint(clip, at, volume);
            }
        }
    }
}
