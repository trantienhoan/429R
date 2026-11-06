using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.Rooms
{
    /// <summary>
    /// A magic hat's aura (its GrowthParticles) goes once the hat is picked: grabbed, or moved off its spot on the magic
    /// table (user, 2026-11-06: "When the hat is picked, or moved out of the table, lose its aura particle"). The
    /// particles already out fade away; it never comes back. Goes on Hat_Green, Hat_Black and Hat_Blue (Tools > 429 Game
    /// > Rooms > Set Up Magic Hats + Pumpkin Rooms puts it there).
    /// </summary>
    [DisallowMultipleComponent]
    public class MagicHatAura : MonoBehaviour
    {
        [Tooltip("The aura (empty: the child called GrowthParticles).")]
        [SerializeField] private ParticleSystem aura;
        [Tooltip("Moved this far from its spot on the table, in metres, it's been picked.")]
        [Min(0.02f)]
        [SerializeField] private float moveAway = 0.12f;
        [Tooltip("Its spot is where it is this long after it appears (it may drop a little onto the table first), in seconds.")]
        [Min(0f)]
        [SerializeField] private float settleSeconds = 0.5f;

        private XRBaseInteractable grab;
        private Vector3 spot;
        private bool spotSet;
        private bool gone;
        private float bornAt;

        /// <summary>Whether its aura is gone (it's been picked).</summary>
        public bool Picked => gone;

        private void Awake()
        {
            if (aura == null)
            {
                var child = transform.Find("GrowthParticles");
                if (child != null) aura = child.GetComponent<ParticleSystem>();
            }
            bornAt = Time.time;
            if (TryGetComponent(out grab)) grab.selectEntered.AddListener(OnGrabbed);
        }

        private void OnDestroy()
        {
            if (grab != null) grab.selectEntered.RemoveListener(OnGrabbed);
        }

        private void OnGrabbed(SelectEnterEventArgs args) => Lose();

        private void Update()
        {
            if (gone) return;
            if (!spotSet)
            {
                if (Time.time - bornAt < settleSeconds) return;
                spot = transform.position;
                spotSet = true;
                return;
            }
            if ((transform.position - spot).sqrMagnitude > moveAway * moveAway) Lose();
        }

        /// <summary>The aura goes (the particles out fade away).</summary>
        public void Lose()
        {
            if (gone) return;
            gone = true;
            if (aura != null) aura.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            enabled = false;
        }
    }
}
