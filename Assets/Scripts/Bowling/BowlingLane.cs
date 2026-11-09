using System.Collections;
using TMPro;
using UnityEngine;

namespace Game.Bowling
{
    /// <summary>
    /// One arm of the bowling X (Gate 2, "Strike the Slimes"): its floor, rails, the 6 pin slots a slime poops a rack
    /// into, the hover point over the deck, the aim point the roll assist steers to (the head pin), the tip, the counter,
    /// and the lit part (deck spot, deck ring, 2 fire columns, sparkles). The floor and rails always show (dark until lit).
    /// Lighting one plays a short reveal: the deck spot comes up, the fires ignite with a puff at each base, a whoosh at the
    /// mouth and a fire roar a few metres out. The transform sits at the arena's centre with +Z along the lane.
    /// Built by Tools > 429 Game > Bowling > Set Up Bowling Arena.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BowlingLane : MonoBehaviour
    {
        [Header("Places")]
        [Tooltip("Which lane this is (0..3; 0 is in front of the player at the start).")]
        [SerializeField] private int index;
        [Tooltip("Where the roll assist steers a bowl (over the head pin).")]
        [SerializeField] private Transform aim;
        [Tooltip("The lane's far end, on the floor.")]
        [SerializeField] private Transform tip;
        [Tooltip("Over the pin deck: a pooping slime hovers here (the stage sets the height it uses).")]
        [SerializeField] private Transform hover;
        [Tooltip("The 6 pin slots (1-2-3 rack, head pin first), on the floor.")]
        [SerializeField] private Transform[] slots = System.Array.Empty<Transform>();
        [Tooltip("How far from the centre the lane starts (the dirt's edge, metres): the reveal whoosh plays there.")]
        [SerializeField] private float mouthDistance = 2.5f;

        [Header("Looks")]
        [Tooltip("On only while lit: the deck light, the deck ring, the fire columns and the sparkles.")]
        [SerializeField] private GameObject litRoot;
        [Tooltip("Shown only while lit (their colliders always stay). Empty: the floor and rails always show.")]
        [SerializeField] private Renderer[] reveal = System.Array.Empty<Renderer>();
        [Tooltip("Pivots that grow along Z when the lane lights (none now: the glow strips are gone).")]
        [SerializeField] private Transform[] strips = System.Array.Empty<Transform>();
        [Tooltip("The spot light over the pin deck (no shadows).")]
        [SerializeField] private Light deckLight;
        [Tooltip("The streak counter at the lane's mouth (only while lit).")]
        [SerializeField] private TMP_Text counter;
        [Tooltip("The fire columns' bases: a puff at each when the lane lights.")]
        [SerializeField] private Transform[] fires = System.Array.Empty<Transform>();

        [Header("Reveal")]
        [Tooltip("Seconds the deck light comes up.")]
        [Min(0.05f)]
        [SerializeField] private float revealSeconds = 0.8f;
        [Tooltip("Played at the lane's mouth when it lights (whoosh).")]
        [SerializeField] private AudioClip revealSound;
        [Range(0f, 1f)]
        [SerializeField] private float revealVolume = 0.7f;
        [Tooltip("The fires catching (thrusterFire_000), played Ignite Distance metres out along the lane so it's heard.")]
        [SerializeField] private AudioClip igniteSound;
        [Range(0f, 1f)]
        [SerializeField] private float igniteVolume = 0.7f;
        [Min(0f)]
        [SerializeField] private float igniteDistance = 4f;
        [Tooltip("The puff at each fire base when it lights (Smoke_Flash_Small_Puff), and its size.")]
        [SerializeField] private GameObject puff;
        [SerializeField] private float puffScale = 0.6f;

        private float lightIntensity = -1f;
        private Coroutine revealing;

        /// <summary>Which lane this is (0..3).</summary>
        public int Index => index;

        /// <summary>Lit (deck light, fire and sparkles on). A lane stays lit once a slime picks it.</summary>
        public bool Lit { get; private set; } = true;

        /// <summary>The flat unit direction from the arena's centre to the tip.</summary>
        public Vector3 Direction
        {
            get
            {
                var d = tip != null ? tip.position - transform.position : transform.forward;
                d.y = 0f;
                if (d.sqrMagnitude < 0.0001f) d = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward;
            }
        }

        /// <summary>Where the roll assist steers a bowl on this lane.</summary>
        public Transform Aim => aim;

        /// <summary>The lane's far end, on the floor.</summary>
        public Transform Tip => tip;

        /// <summary>Over the pin deck (the stage sets the height a slime uses).</summary>
        public Transform Hover => hover;

        /// <summary>How many pin slots it has (6).</summary>
        public int SlotCount => slots != null ? slots.Length : 0;

        /// <summary>A pin slot's point on the floor (0 = the head pin).</summary>
        public Vector3 SlotPosition(int slot)
        {
            if (slots == null || slot < 0 || slot >= slots.Length || slots[slot] == null) return tip != null ? tip.position : transform.position;
            return slots[slot].position;
        }

        /// <summary>All the pin slots' floor points (a new array).</summary>
        public Vector3[] SlotPositions()
        {
            var points = new Vector3[SlotCount];
            for (int i = 0; i < points.Length; i++) points[i] = SlotPosition(i);
            return points;
        }

        private void Awake()
        {
            if (deckLight != null) lightIntensity = deckLight.intensity;
        }

        /// <summary>Lights the lane or puts it out (its floor, rails and colliders stay). Animate: the reveal (light comes
        /// up, fires ignite with puffs, whoosh and roar).</summary>
        public void SetLit(bool on, bool animate)
        {
            if (lightIntensity < 0f) lightIntensity = deckLight != null ? deckLight.intensity : 6f;
            if (revealing != null)
            {
                StopCoroutine(revealing);
                revealing = null;
            }
            Lit = on;
            foreach (var r in reveal) if (r != null) r.enabled = on;
            if (litRoot != null) litRoot.SetActive(on);
            if (counter != null && !on) counter.gameObject.SetActive(false);
            if (!on) return;
            if (animate && isActiveAndEnabled) revealing = StartCoroutine(Reveal());
            else Grown(1f);
        }

        /// <summary>The counter's text in white; empty or null hides it. Never shown on an unlit lane.</summary>
        public void SetCounter(string text) => SetCounter(text, Color.white);

        /// <summary>The counter's text in this colour; empty or null hides it. Never shown on an unlit lane.</summary>
        public void SetCounter(string text, Color colour)
        {
            if (counter == null) return;
            bool show = Lit && !string.IsNullOrEmpty(text);
            if (counter.gameObject.activeSelf != show) counter.gameObject.SetActive(show);
            if (!show) return;
            if (counter.text != text) counter.text = text;
            if (counter.color != colour) counter.color = colour;
        }

        private IEnumerator Reveal()
        {
            Grown(0f);
            RevealIgnite();
            for (float t = 0f; t < revealSeconds; t += Time.deltaTime)
            {
                Grown(Mathf.SmoothStep(0f, 1f, t / revealSeconds));
                yield return null;
            }
            Grown(1f);
            revealing = null;
        }

        // The reveal's puffs and sounds: a puff at each fire base (or over the deck with no fires), the whoosh at the mouth,
        // the fire roar a few metres out (heard from the centre).
        private void RevealIgnite()
        {
            if (puff != null)
            {
                bool any = false;
                foreach (var fire in fires)
                {
                    if (fire == null) continue;
                    EffectPool.Play(puff, fire.position + Vector3.up * 0.2f, puffScale);
                    any = true;
                }
                if (!any && deckLight != null)
                    EffectPool.Play(puff, new Vector3(deckLight.transform.position.x, transform.position.y + 0.3f, deckLight.transform.position.z), puffScale);
            }
            if (revealSound != null) AudioSource.PlayClipAtPoint(revealSound, transform.position + Direction * mouthDistance + Vector3.up * 0.5f, revealVolume);
            if (igniteSound != null) AudioSource.PlayClipAtPoint(igniteSound, transform.position + Direction * igniteDistance + Vector3.up * 0.5f, igniteVolume);
        }

        // How far the strips have grown and the deck light come up (0..1).
        private void Grown(float k)
        {
            foreach (var s in strips)
            {
                if (s == null) continue;
                var scale = s.localScale;
                scale.z = Mathf.Max(0.001f, k);
                s.localScale = scale;
            }
            if (deckLight != null)
            {
                deckLight.intensity = lightIntensity * k;
                deckLight.enabled = k > 0f;
            }
        }
    }
}
