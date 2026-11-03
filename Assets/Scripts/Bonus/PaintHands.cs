using System.Collections.Generic;
using Game.Combat;
using UnityEngine;

namespace Game.Bonus
{
    /// <summary>
    /// The player's two hands in the Paint Bees round: each takes the colour of the paint bucket it was last dipped in.
    /// The hand's model is tinted that colour (after Hot Hands, which tints it too, so both show) and drips paint for a
    /// moment after the dip, then now and then while it's painted. The round makes it and takes it away with it, and the
    /// hands are clean again.
    /// </summary>
    public class PaintHands : MonoBehaviour
    {
        /// <summary>No paint on the hand.</summary>
        public const int None = -1;

        // Strongly the paint's colour, a little of the glove still showing through.
        private const float Tint = 0.85f;
        // Drips: per second just after a dip, for how long, and now and then after that.
        private const float FreshDrips = 14f;
        private const float FreshSeconds = 2.2f;
        private const float LateDrips = 1.5f;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        /// <summary>One of the player's hands.</summary>
        public class Hand
        {
            public Fist fist;
            public int colour = None;
            public float dippedAt = -10f;
            public readonly List<Renderer> renderers = new();
            public ParticleSystem drips;
        }

        private readonly List<Hand> hands = new();
        private Color[] colours = System.Array.Empty<Color>();
        private Material dropMaterial;
        private Transform floor;
        private Material splatMaterial;
        private MaterialPropertyBlock block;

        /// <summary>The hands found (two, unless one controller is missing).</summary>
        public IReadOnlyList<Hand> Hands => hands;

        /// <summary>
        /// Finds the player's hands (their Fists); the paint colours, the drops' material, and the floor their drops splat
        /// on (none: they don't).
        /// </summary>
        public void Setup(Color[] paintColours, Material drops, Transform floor = null, Material splats = null)
        {
            colours = paintColours;
            dropMaterial = drops;
            this.floor = floor;
            splatMaterial = splats != null ? splats : drops;
            foreach (var fist in FindObjectsByType<Fist>(FindObjectsSortMode.None))
            {
                var hand = new Hand { fist = fist };
                hand.drips = MakeDrips(fist.transform);
                hands.Add(hand);
            }
        }

        /// <summary>Where the hand is (its knuckles, open or closed).</summary>
        public static Vector3 PointOf(Hand hand) => hand.fist.Knuckles;

        /// <summary>How fast the hand is moving, in metres a second.</summary>
        public static float SpeedOf(Hand hand) => hand.fist.HandVelocity.magnitude;

        /// <summary>The hand takes this colour (a bucket's index); its fresh drips start.</summary>
        public void Paint(Hand hand, int colour)
        {
            hand.colour = colour;
            hand.dippedAt = Time.time;
            var main = hand.drips.main;
            main.startColor = colours[colour];
        }

        /// <summary>The colour the hand shows (white when clean).</summary>
        public Color ColourOf(Hand hand) => hand.colour >= 0 && hand.colour < colours.Length ? colours[hand.colour] : Color.white;

        private void LateUpdate()
        {
            block ??= new MaterialPropertyBlock();
            foreach (var hand in hands)
            {
                if (hand.fist == null) continue;
                bool painted = hand.colour != None;
                var emission = hand.drips.emission;
                emission.rateOverTime = !painted ? 0f : Time.time - hand.dippedAt < FreshSeconds ? FreshDrips : LateDrips;
                if (!painted) continue;
                // Hot Hands sets its glow on the same renderers in its Update: the colour goes over it here.
                hand.renderers.Clear();
                hand.fist.CollectHandRenderers(hand.renderers);
                foreach (var part in hand.renderers)
                {
                    if (part == null) continue;
                    var material = part.sharedMaterial;
                    var own = material != null && material.HasProperty(BaseColor) ? material.GetColor(BaseColor) : Color.white;
                    part.GetPropertyBlock(block);
                    block.SetColor(BaseColor, Color.Lerp(own, colours[hand.colour], Tint));
                    part.SetPropertyBlock(block);
                }
            }
        }

        // Clean again (Hot Hands puts its own glow back by itself while a hand is hot).
        private void OnDestroy()
        {
            foreach (var hand in hands)
            {
                if (hand.drips != null) Destroy(hand.drips.gameObject);
                if (hand.colour == None) continue;
                foreach (var part in hand.renderers)
                {
                    if (part != null) part.SetPropertyBlock(null);
                }
            }
        }

        // Paint drops falling off the hand: small, round, gone on the floor.
        private ParticleSystem MakeDrips(Transform under)
        {
            var go = new GameObject("Paint Drips");
            go.transform.SetParent(under, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.022f);
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;
            var emission = system.emission;
            emission.rateOverTime = 0f;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.035f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = dropMaterial;
            // Half of them splat where they land.
            if (floor != null && splatMaterial != null) PaintSplats.Add(system, floor, splatMaterial, 0.5f, new Vector2(0.04f, 0.08f));
            system.Play();
            return system;
        }
    }
}
