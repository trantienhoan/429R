using UnityEngine;

namespace Game.Bonus
{
    /// <summary>
    /// Paint that reaches the floor leaves a splat there (the Paint Bees round, user 2026-11-03: "paint splash on the
    /// floor"): a paint particle system's drops hit a flat plane at the floor (no real collision checks, so it's cheap)
    /// and each one that does leaves a flat round blob of its own colour, which spreads out fast and fades after a few
    /// seconds. Used on the buckets' drips, the hands' drips and the bursts of swatted bees.
    /// </summary>
    public static class PaintSplats
    {
        /// <summary>
        /// Drops of 'system' hitting the plane at Floor leave splats (made of Material: a soft round blob, alpha
        /// blended): Chance of them do, each Size across (metres, random between) for Seconds. Returns the splats' system.
        /// </summary>
        public static ParticleSystem Add(ParticleSystem system, Transform floor, Material material, float chance = 1f,
            Vector2 size = default, float seconds = 4f)
        {
            if (size == default) size = new Vector2(0.06f, 0.14f);
            var collision = system.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.Planes;
            collision.SetPlane(0, floor);
            collision.dampen = 1f;
            collision.bounce = 0f;
            collision.lifetimeLoss = 1f;

            var go = new GameObject("Paint Splats");
            go.transform.SetParent(system.transform, false);
            var splats = go.AddComponent<ParticleSystem>();
            splats.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = splats.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = seconds;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 160;
            // One splat for each drop that lands (a sub-emitter makes as many as its own emission says).
            var emission = splats.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shape = splats.shape;
            shape.enabled = false;
            // A splash: out to its size in a flash, then it lies there and fades at the end.
            var grow = splats.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(0.03f, 1.1f), new Keyframe(0.06f, 1f), new Keyframe(1f, 1f)));
            var fade = splats.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.9f, 0.75f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            // Flat on the floor, facing up.
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var sub = system.subEmitters;
            sub.enabled = true;
            sub.AddSubEmitter(splats, ParticleSystemSubEmitterType.Collision, ParticleSystemSubEmitterProperties.InheritColor, Mathf.Clamp01(chance));
            return splats;
        }
    }
}
