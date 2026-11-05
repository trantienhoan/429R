using System.Collections.Generic;
using Game.Combat;
using UnityEngine;

namespace Game.Bonus
{
    /// <summary>
    /// The player's two hands in the Paint Bees round: each takes the colour of the paint bucket it was last dipped in,
    /// and drips paint for a moment after the dip, then now and then while it's painted. The round makes it and takes it
    /// away with it, and the hands are clean again.
    /// With a Coat Material (user, 2026-11-05: "make part of my palm that put in the bucket that colour") only the part
    /// of the hand that went into the paint takes its colour: a copy of the hand's skinned renderer (same mesh, same bones)
    /// draws the paint below the paint line, kept in the hand's own space (the deepest dip of that colour; at least Coat
    /// Above Knuckles over the knuckles, so a shallow dip shows too). Without one, the whole hand is tinted (after Hot
    /// Hands, which tints it too, so both show).
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
        private static readonly int CoatToLocal = Shader.PropertyToID("_CoatToLocal");
        private static readonly int CoatPlane = Shader.PropertyToID("_CoatPlane");
        // A dip paints at least this far over the knuckles (metres), even when they only got near the paint.
        private const float CoatAboveKnuckles = 0.03f;

        /// <summary>One of the player's hands.</summary>
        public class Hand
        {
            public Fist fist;
            public int colour = None;
            public float dippedAt = -10f;
            public readonly List<Renderer> renderers = new();
            public ParticleSystem drips;
            // The paint coat: its renderer, the hand renderer it copies, the paint line (hand space: into the paint xyz,
            // its distance w), how deep the deepest dip of its colour went, and that colour.
            public SkinnedMeshRenderer coat;
            public SkinnedMeshRenderer coatOf;
            public Vector4 line = new(0f, -1f, 0f, 1000f);
            public float coatDepth = float.NegativeInfinity;
            public int coatColour = None;
        }

        private readonly List<Hand> hands = new();
        private Color[] colours = System.Array.Empty<Color>();
        private Material dropMaterial;
        private Transform floor;
        private Material splatMaterial;
        private MaterialPropertyBlock block;
        private MaterialPropertyBlock coatBlock;
        private Material coatMaterial;

        /// <summary>The hands found (two, unless one controller is missing).</summary>
        public IReadOnlyList<Hand> Hands => hands;

        /// <summary>
        /// Finds the player's hands (their Fists); the paint colours, the drops' material, and the floor their drops splat
        /// on (none: they don't).
        /// </summary>
        public void Setup(Color[] paintColours, Material drops, Transform floor = null, Material splats = null, Material coat = null)
        {
            colours = paintColours;
            coatMaterial = coat;
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

        /// <summary>
        /// The hand is in this bucket's paint (call it every frame it is): the part of it below the paint line takes the
        /// colour. The deepest dip of a colour is kept; a new colour starts again.
        /// </summary>
        public void Dip(Hand hand, PaintBucket bucket)
        {
            if (coatMaterial == null || hand.fist == null || bucket == null || hand.colour == None) return;
            var skin = SkinOf(hand);
            if (skin == null) return;
            var knuckles = PointOf(hand);
            float line = Mathf.Max(bucket.PaintMiddle.y, knuckles.y + CoatAboveKnuckles);
            float depth = line - knuckles.y;
            if (hand.coatColour != hand.colour)
            {
                hand.coatColour = hand.colour;
                hand.coatDepth = float.NegativeInfinity;
            }
            if (depth <= hand.coatDepth) return;
            hand.coatDepth = depth;
            var toHand = HandFrame(skin).inverse;
            var into = toHand.MultiplyVector(Vector3.down).normalized;
            var onLine = toHand.MultiplyPoint3x4(new Vector3(knuckles.x, line, knuckles.z));
            hand.line = new Vector4(into.x, into.y, into.z, Vector3.Dot(into, onLine));
        }

        // The hand's model (its skinned renderer), and its own space: where its root bone is and how it's turned, no scale.
        private static SkinnedMeshRenderer SkinOf(Hand hand)
        {
            hand.renderers.Clear();
            hand.fist.CollectHandRenderers(hand.renderers);
            foreach (var part in hand.renderers)
            {
                if (part is SkinnedMeshRenderer skin && skin.sharedMesh != null) return skin;
            }
            return null;
        }

        private static Matrix4x4 HandFrame(SkinnedMeshRenderer skin)
        {
            var root = skin.rootBone != null ? skin.rootBone : skin.transform;
            return Matrix4x4.TRS(root.position, root.rotation, Vector3.one);
        }

        // The coat: the hand's skinned renderer copied (same mesh and bones, the coat material on every part of it).
        private SkinnedMeshRenderer MakeCoat(SkinnedMeshRenderer skin)
        {
            var go = new GameObject("Paint Coat");
            go.layer = skin.gameObject.layer;
            go.transform.SetParent(skin.transform, false);
            var coat = go.AddComponent<SkinnedMeshRenderer>();
            coat.sharedMesh = skin.sharedMesh;
            coat.bones = skin.bones;
            coat.rootBone = skin.rootBone;
            coat.localBounds = skin.localBounds;
            coat.updateWhenOffscreen = skin.updateWhenOffscreen;
            coat.quality = skin.quality;
            coat.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            coat.receiveShadows = false;
            var materials = new Material[Mathf.Max(1, skin.sharedMesh.subMeshCount)];
            for (int i = 0; i < materials.Length; i++) materials[i] = coatMaterial;
            coat.sharedMaterials = materials;
            return coat;
        }

        private void UpdateCoat(Hand hand, bool painted)
        {
            var skin = painted ? SkinOf(hand) : null;
            bool show = skin != null && hand.coatColour == hand.colour && !float.IsNegativeInfinity(hand.coatDepth) &&
                        skin.enabled && skin.gameObject.activeInHierarchy;
            if (!show)
            {
                if (hand.coat != null) hand.coat.enabled = false;
                return;
            }
            if (hand.coat == null || hand.coatOf != skin)
            {
                if (hand.coat != null) Destroy(hand.coat.gameObject);
                hand.coat = MakeCoat(skin);
                hand.coatOf = skin;
            }
            hand.coat.enabled = true;
            // A hand that bends by blend shapes bends the coat the same way.
            for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++) hand.coat.SetBlendShapeWeight(i, skin.GetBlendShapeWeight(i));
            coatBlock ??= new MaterialPropertyBlock();
            hand.coat.GetPropertyBlock(coatBlock);
            coatBlock.SetColor(BaseColor, colours[hand.colour]);
            coatBlock.SetVector(CoatPlane, hand.line);
            coatBlock.SetMatrix(CoatToLocal, HandFrame(skin).inverse);
            hand.coat.SetPropertyBlock(coatBlock);
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
                // With a coat only the dipped part shows the paint: the hand itself isn't tinted.
                if (coatMaterial != null)
                {
                    UpdateCoat(hand, painted);
                    continue;
                }
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
                if (hand.coat != null) Destroy(hand.coat.gameObject);
                if (hand.colour == None || coatMaterial != null) continue;
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
