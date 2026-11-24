using System.Collections.Generic;
using UnityEngine;

namespace Game.Story
{
    /// <summary>
    /// The girl's green shower (user's story, 2026-11-23: "the tiny green girl comes and showers me in the green light
    /// particles, thanking me for not giving up"). The Fairy's reward (her Win state's Green Shower action) rains green
    /// sparkles round the player for Story Seed Settings' Shower Seconds. The sparkles are a seed's own 'stars' and
    /// 'circles' particles (Shower Look), tinted the glow colour and spread over a ring above the player's head.
    /// </summary>
    public class StoryShower : MonoBehaviour
    {
        private static readonly string[] Parts = { "stars", "circles" };

        private readonly List<ParticleSystem> sparkles = new List<ParticleSystem>();
        private readonly List<float> seedScales = new List<float>();
        private float stopAt = -1f;

        /// <summary>How many showers are falling now (for tests).</summary>
        public static int Playing { get; private set; }

        /// <summary>How many sparkles are in the air now (for tests).</summary>
        public static int Sparkles { get; private set; }

        public static void Play(StorySeedSettings settings)
        {
            var head = Camera.main != null ? Camera.main.transform : null;
            if (head == null || settings.ShowerLook == null) return;
            var go = new GameObject("Story_Green_Shower");
            go.transform.position = head.position + Vector3.up * settings.ShowerHeight;
            go.AddComponent<StoryShower>().Begin(settings);
        }

        private void Begin(StorySeedSettings settings)
        {
            Playing++;
            // Borrow the sparkles off a copy of the seed that never wakes (no FSM, nothing to take).
            var look = Instantiate(settings.ShowerLook);
            foreach (var fsm in look.GetComponentsInChildren<PlayMakerFSM>(true)) fsm.enabled = false;
            foreach (var hit in look.GetComponentsInChildren<Collider>(true)) hit.enabled = false;
            foreach (var particles in look.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (System.Array.IndexOf(Parts, particles.name) < 0) continue;
                // On the seed they sit under a small scale (about 0.06) that also sizes them: keep their real size.
                seedScales.Add(particles.transform.lossyScale.x);
                particles.transform.SetParent(transform, false);
                particles.transform.localPosition = Vector3.zero;
                particles.transform.localRotation = Quaternion.identity;
                particles.transform.localScale = Vector3.one;
                sparkles.Add(particles);
            }
            Destroy(look);
            StorySeed.TintGlow(gameObject, settings.Glow, Parts);

            for (int i = 0; i < sparkles.Count; i++)
            {
                var particles = sparkles[i];
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles.main;
                main.scalingMode = ParticleSystemScalingMode.Shape;
                float size = seedScales[i] * (particles.name == "circles" ? settings.ShowerGlowScale : settings.ShowerStarScale);
                if (main.startSize3D)
                {
                    main.startSizeX = Scaled(main.startSizeX, size);
                    main.startSizeY = Scaled(main.startSizeY, size);
                    main.startSizeZ = Scaled(main.startSizeZ, size);
                }
                else main.startSize = Scaled(main.startSize, size);
                main.loop = true;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.gravityModifier = 0.15f;
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.4f);
                main.maxParticles = Mathf.Max(main.maxParticles, 400);
                // A ring round the player: the middle (the head) stays clear.
                var shape = particles.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = settings.ShowerRadius;
                shape.radiusThickness = settings.ShowerRing;
                shape.rotation = new Vector3(90f, 0f, 0f);
                var emission = particles.emission;
                emission.enabled = true;
                emission.rateOverTime = settings.ShowerRate;
                emission.SetBursts(new ParticleSystem.Burst[0]);
                particles.Play(true);
            }
            stopAt = Time.time + settings.ShowerSeconds;
        }

        private void Update()
        {
            int count = 0;
            foreach (var particles in sparkles)
                if (particles != null) count += particles.particleCount;
            Sparkles = count;
            if (stopAt < 0f || Time.time < stopAt) return;
            stopAt = -1f;
            foreach (var particles in sparkles)
                if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(gameObject, 3f);
        }

        private void OnDestroy()
        {
            Playing = Mathf.Max(0, Playing - 1);
            if (Playing == 0) Sparkles = 0;
        }

        // The whole size range times K (the start-size multiplier alone only moves the top of a two-constant range).
        private static ParticleSystem.MinMaxCurve Scaled(ParticleSystem.MinMaxCurve curve, float k)
        {
            switch (curve.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    curve.constant *= k;
                    break;
                case ParticleSystemCurveMode.TwoConstants:
                    curve.constantMin *= k;
                    curve.constantMax *= k;
                    break;
                default:
                    curve.curveMultiplier *= k;
                    break;
            }
            return curve;
        }
    }
}
