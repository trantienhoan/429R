using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ValleyGame = Balls.BallValleyStage;   // 429R: the gate stage took BALLS' ValleyGame's place

namespace Balls
{
    /// <summary>
    /// The win fireworks from 429R (Firework_Win + FireworkShow + FireworkSounds), built in code at valley scale: rockets
    /// with glowing trails go up round the winner, each a random colour (gold, pink, cyan, green, orange, violet), and burst
    /// high up into a flash and a ball of streaking, drooping sparks that twinkle out. A whistle as each goes up, a bang as
    /// it bursts. Volleys take turns: both sides, behind, one side, the other.
    /// Every computer runs its own show while the round is over (nothing goes over the network).
    /// </summary>
    public class Fireworks : MonoBehaviour
    {
        const float Size = 3f;                 // 429R's firework is sized for VR; the valley camera hangs 30-90 m up (429R: 3, BALLS 8: seen from the valley's edge)
        const float Interval = 1.3f;           // seconds between volleys
        const int RocketsPerSpot = 2;

        static readonly Vector3[] Spots = { new Vector3(-7f, 0f, 2f), new Vector3(7f, 0f, 2f), new Vector3(0f, 0f, 7f) };   // 429R: closer (BALLS +-16)
        static readonly int[][] Volleys = { new[] { 0, 1 }, new[] { 2 }, new[] { 1 }, new[] { 0 } };
        static readonly Color[] Colours =
        {
            new Color(1f, 0.8f, 0.3f), new Color(1f, 0.35f, 0.75f), new Color(0.35f, 0.9f, 1f),
            new Color(0.5f, 1f, 0.4f), new Color(1f, 0.5f, 0.15f), new Color(0.7f, 0.45f, 1f),
        };

        ParticleSystem rockets;
        float nextVolley;
        int volley;
        bool wasOver;
        readonly List<KeyValuePair<float, Vector3>> bangs = new List<KeyValuePair<float, Vector3>>();

        Material additive;   // 429R: destroyed with the show

        void OnDestroy()
        {
            if (additive) Destroy(additive);
        }

        void Start()
        {
            var add = additive = AdditiveMaterial();
            rockets = Rockets(Child(transform, "Firework Rockets"), add);
            var burst = Burst(Child(rockets.transform, "Burst"), add);
            var flash = Flash(Child(rockets.transform, "Flash"), add);
            var twinkle = Twinkle(Child(burst.transform, "Twinkle"), add);
            var subs = rockets.subEmitters;
            subs.enabled = true;
            subs.AddSubEmitter(burst, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritColor);
            subs.AddSubEmitter(flash, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritColor);
            var crackle = burst.subEmitters;
            crackle.enabled = true;
            crackle.AddSubEmitter(twinkle, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritColor, 0.35f);
        }

        void Update()
        {
            var g = ValleyGame.I;
            bool over = g && g.world && g.roundOver;
            if (over && !wasOver)
            {
                nextVolley = Time.time + 0.3f;
                volley = 0;
            }
            wasOver = over;
            if (over && Time.time >= nextVolley)
            {
                nextVolley = Time.time + Interval;
                Vector3 centre = g.roundWinner && g.roundWinner.alive ? g.roundWinner.Position : Vector3.zero;
                centre.y = 0f;
                foreach (int i in Volleys[volley % Volleys.Length]) Launch(centre + Spots[i]);
                volley++;
            }
            for (int i = bangs.Count - 1; i >= 0; i--)
            {
                if (Time.time < bangs[i].Key) continue;
                Sfx.PlayHere(Sfx.Sound.FireworkBang, bangs[i].Value, Random.Range(0.7f, 1f));
                bangs.RemoveAt(i);
            }
        }

        void Launch(Vector3 at)
        {
            if (!rockets) return;
            Sfx.PlayHere(Sfx.Sound.FireworkLaunch, at, 0.8f);
            var ep = new ParticleSystem.EmitParams();
            float gravity = 9.81f * 0.6f * Size;
            for (int k = 0; k < RocketsPerSpot; k++)
            {
                Vector3 dir = Quaternion.Euler(Random.Range(-18f, 18f), 0f, Random.Range(-18f, 18f)) * Vector3.up;
                float speed = Random.Range(4.6f, 6f) * Size;
                float life = Random.Range(0.55f, 0.8f);
                ep.position = at + new Vector3(Random.Range(-1.5f, 1.5f), 0.5f, Random.Range(-1.5f, 1.5f));
                ep.velocity = dir * speed;
                ep.startLifetime = life;
                ep.startSize = Random.Range(0.06f, 0.08f) * Size;
                ep.startColor = Colours[Random.Range(0, Colours.Length)];
                rockets.Emit(ep, 1);
                // Where (and when) it bursts, for the bang.
                Vector3 burstAt = ep.position + ep.velocity * life + 0.5f * life * life * gravity * Vector3.down;
                bangs.Add(new KeyValuePair<float, Vector3>(Time.time + life, burstAt));
            }
        }

        // ---------- the particle systems (429R FireworkSetupMenu recipe, times Size) ----------

        static GameObject Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        static ParticleSystem NewSystem(GameObject go, Material mat, int max)
        {
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.duration = 1f;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        // Rockets: emitted by hand (Launch), with fading trails.
        static ParticleSystem Rockets(GameObject go, Material mat)
        {
            var ps = NewSystem(go, mat, 200);
            var main = ps.main;
            main.gravityModifier = 0.6f * Size;
            var trails = ps.trails;
            trails.enabled = true;
            trails.lifetime = new ParticleSystem.MinMaxCurve(0.3f);
            trails.minVertexDistance = 0.04f * Size;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));
            trails.inheritParticleColor = true;
            trails.dieWithParticles = false;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            trails.colorOverTrail = new ParticleSystem.MinMaxGradient(fade);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.trailMaterial = mat;
            ps.Play();
            return ps;
        }

        // The burst: a ball of sparks streaking out, slowing, drooping and going out.
        static ParticleSystem Burst(GameObject go, Material mat)
        {
            var ps = NewSystem(go, mat, 4000);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f * Size, 2.4f * Size);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f * Size * 2.5f, 0.06f * Size * 2.5f);
            main.gravityModifier = 0.35f * Size * 0.25f;
            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 90) });
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f * Size;
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = new ParticleSystem.MinMaxCurve(2.4f * Size);
            limit.dampen = 0.08f;
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
            colour.color = new ParticleSystem.MinMaxGradient(gr);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.06f;
            r.lengthScale = 1f;
            ps.Play();
            return ps;
        }

        // A soft flash of the rocket's colour as it bursts.
        static ParticleSystem Flash(GameObject go, Material mat)
        {
            var ps = NewSystem(go, mat, 100);
            var main = ps.main;
            main.startLifetime = 0.2f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(1f * Size, 1.4f * Size);
            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.75f, 0f), new GradientAlphaKey(0f, 1f) });
            colour.color = new ParticleSystem.MinMaxGradient(gr);
            ps.Play();
            return ps;
        }

        // Twinkles: some sparks wink white as they go out.
        static ParticleSystem Twinkle(GameObject go, Material mat)
        {
            var ps = NewSystem(go, mat, 2000);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f * Size * 2f, 0.09f * Size * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(1f, 0.95f, 0.8f));
            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f)));
            ps.Play();
            return ps;
        }

        /// <summary>URP particles, additive, no fog (fireworks glow, the valley's haze shouldn't dull them).</summary>
        static Material AdditiveMaterial()
        {
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var m = new Material(sh);
            m.SetTexture("_BaseMap", Gfx.SoftTexture());
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);   // additive
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_BLENDMODE_ADD");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent + 10;
            return m;
        }
    }
}
