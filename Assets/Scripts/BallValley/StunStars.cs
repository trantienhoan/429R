using UnityEngine;

namespace Balls
{
    /// <summary>
    /// The dizzy stars over a knocked ball (hit by a special move: no control until it rolls out), after 429R's Stun_Stars:
    /// a faint gold ring lying level over the ball, five chubby gold stars circling on it, spinning and twinkling, and
    /// little sparkles trailing them. Built in code, the star and ring pictures too (no art files), one per ball and
    /// reused. BallPlayer shows it while the ball is knocked, on every computer (the knock is in the snapshots).
    /// </summary>
    public class StunStars : MonoBehaviour
    {
        const float Orbit = 4f;   // round the head in about 1.6 s (radians a second)

        static Texture2D starPicture;
        static Material ringMat, starMat;

        ParticleSystem ring, stars, sparkles;
        bool showing;

        public static StunStars Create(Transform parent)
        {
            var go = new GameObject("Stun Stars");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<StunStars>();
            s.Build();
            go.SetActive(false);
            return s;
        }

        /// <summary>Over a ball of radius <paramref name="radius"/> centred at <paramref name="center"/> (call every frame while it's dizzy).</summary>
        public void Show(Vector3 center, float radius)
        {
            float ringR = Mathf.Max(1.3f, radius * 1.05f);
            transform.SetPositionAndRotation(center + Vector3.up * (radius + 0.5f + ringR * 0.2f), Quaternion.identity);
            transform.localScale = Vector3.one * ringR;
            if (showing) return;
            showing = true;
            gameObject.SetActive(true);
            foreach (var ps in new[] { ring, stars, sparkles })
            {
                ps.Clear();
                ps.Play();
            }
        }

        public void Hide()
        {
            if (!showing) return;
            showing = false;
            gameObject.SetActive(false);
        }

        // ---------- building ----------

        void Build()
        {
            if (!starPicture) starPicture = StarPicture(64);
            if (!ringMat) ringMat = Gfx.ParticleMaterialWith(Circle(128, true));   // 429R: TouchControls.Circle, copied below
            if (!starMat) starMat = Gfx.ParticleMaterialWith(starPicture);

            // The ring: one particle lying flat, turning slowly (the picture's ring is at 0.93 of its half: radius 1).
            ring = Part("Ring", ringMat, ParticleSystemRenderMode.HorizontalBillboard, 1);
            Main(ring, 1000f, 1000f, 2.15f, 2.15f, new Color(1f, 0.9f, 0.55f, 0.45f), new Color(1f, 0.9f, 0.55f, 0.45f), false);
            Burst(ring, 1);
            var turn = ring.rotationOverLifetime;
            turn.enabled = true;
            turn.z = new ParticleSystem.MinMaxCurve(1.2f);

            // The stars: five, evenly round the ring, circling on it, spinning, their size breathing (a twinkle).
            stars = Part("Stars", starMat, ParticleSystemRenderMode.Billboard, 5);
            Main(stars, 1000f, 1000f, 0.5f, 0.6f, new Color(1f, 0.82f, 0.25f), new Color(1f, 0.93f, 0.5f), false);
            Burst(stars, 5);
            OnTheRing(stars, true);
            Circle(stars);
            var spin = stars.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);
            var twinkle = stars.noise;
            twinkle.enabled = true;
            twinkle.strength = 0f;   // no drift: only the size
            twinkle.frequency = 1.5f;
            twinkle.scrollSpeed = 1.2f;
            twinkle.sizeAmount = new ParticleSystem.MinMaxCurve(0.35f);

            // The sparkles: little twinkles left along the ring behind the stars, shrinking and fading fast.
            sparkles = Part("Sparkles", starMat, ParticleSystemRenderMode.Billboard, 40);
            Main(sparkles, 0.35f, 0.55f, 0.12f, 0.2f, new Color(1f, 1f, 0.9f), new Color(1f, 0.85f, 0.4f), true);
            var rate = sparkles.emission;
            rate.rateOverTime = 12f;   // 429R: Quest, as BALLS' phones (BALLS 26)
            OnTheRing(sparkles, false);
            Circle(sparkles);
            var shrink = sparkles.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.2f)));
            var fade = sparkles.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(g);
        }

        ParticleSystem Part(string name, Material mat, ParticleSystemRenderMode mode, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.maxParticles = max;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = mode;
            r.alignment = ParticleSystemRenderSpace.View;
            r.sortMode = ParticleSystemSortMode.None;
            r.maxParticleSize = 1f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        static void Main(ParticleSystem ps, float lifeMin, float lifeMax, float sizeMin, float sizeMax, Color a, Color b, bool loop)
        {
            var main = ps.main;
            main.duration = 1f;
            main.loop = loop;
            main.playOnAwake = false;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;   // the whole thing scales with the ring
            main.simulationSpace = ParticleSystemSimulationSpace.Local;   // and rides along over the ball
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startSpeed = 0f;
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0f;
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[0]);
            var shape = ps.shape;
            shape.enabled = false;
        }

        static void Burst(ParticleSystem ps, int count)
        {
            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        }

        /// <summary>Born on the level ring of radius 1: evenly spaced round it (the stars), or anywhere on it (the sparkles).</summary>
        static void OnTheRing(ParticleSystem ps, bool spread)
        {
            var s = ps.shape;
            s.enabled = true;
            s.shapeType = ParticleSystemShapeType.Circle;
            s.radius = 1f;
            s.radiusThickness = 0f;
            s.arc = 360f;
            s.arcMode = spread ? ParticleSystemShapeMultiModeValue.BurstSpread : ParticleSystemShapeMultiModeValue.Random;
            s.rotation = new Vector3(90f, 0f, 0f);   // the circle lies flat
        }

        /// <summary>Round and round the middle, staying on the ring.</summary>
        static void Circle(ParticleSystem ps)
        {
            var orbit = ps.velocityOverLifetime;
            orbit.enabled = true;
            orbit.space = ParticleSystemSimulationSpace.Local;
            orbit.x = new ParticleSystem.MinMaxCurve(0f);
            orbit.y = new ParticleSystem.MinMaxCurve(0f);
            orbit.z = new ParticleSystem.MinMaxCurve(0f);
            orbit.orbitalX = new ParticleSystem.MinMaxCurve(0f);
            orbit.orbitalY = new ParticleSystem.MinMaxCurve(Orbit);
            orbit.orbitalZ = new ParticleSystem.MinMaxCurve(0f);
            orbit.radial = new ParticleSystem.MinMaxCurve(0f);
        }

        /// <summary>429R: the shared pictures and materials are destroyed (the stage is gone; the next one makes new ones).</summary>
        public static void Release()
        {
            if (ringMat) { Destroy(ringMat.GetTexture("_BaseMap")); Destroy(ringMat); }
            if (starMat) Destroy(starMat);
            if (starPicture) Destroy(starPicture);
            ringMat = starMat = null;
            starPicture = null;
        }

        /// <summary>429R: BALLS' TouchControls.Circle (TouchControls isn't brought over): a white disc, or only its rim.</summary>
        static Texture2D Circle(int n, bool ringOnly)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;   // 0 centre .. 1 edge
                float a = ringOnly ? Mathf.Clamp01(1f - Mathf.Abs(d - 0.93f) * 25f) : Mathf.Clamp01((1f - d) * 30f);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            t.Apply();
            return t;
        }

        // ---------- the star picture ----------

        /// <summary>A chubby five-pointed star, point up: white with a slightly darker rim (the particles tint it gold).</summary>
        static Texture2D StarPicture(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            const float span = 1.08f;            // the picture covers -span..span
            float pixel = 2f * span / n;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                var p = new Vector2(((x + 0.5f) / n * 2f - 1f) * span, ((y + 0.5f) / n * 2f - 1f) * span);
                float d = Star5(p, 0.8f, 0.48f) - 0.1f;   // rounded off: chubby points
                float alpha = Mathf.Clamp01(0.5f - d / pixel);
                float shade = Mathf.Lerp(0.62f, 1f, Mathf.Clamp01((-d - 0.07f) / pixel));   // a darker rim
                byte c = (byte)(255f * shade);
                px[y * n + x] = new Color32(c, c, c, (byte)(255f * alpha));
            }
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        /// <summary>Signed distance to a five-pointed star, point up (Inigo Quilez): outer radius r, inner/outer ratio rf.</summary>
        static float Star5(Vector2 p, float r, float rf)
        {
            var k1 = new Vector2(0.809016994375f, -0.587785252292f);
            var k2 = new Vector2(-k1.x, k1.y);
            p.x = Mathf.Abs(p.x);
            p -= 2f * Mathf.Max(Vector2.Dot(k1, p), 0f) * k1;
            p -= 2f * Mathf.Max(Vector2.Dot(k2, p), 0f) * k2;
            p.x = Mathf.Abs(p.x);
            p.y -= r;
            Vector2 ba = rf * new Vector2(-k1.y, k1.x) - new Vector2(0f, 1f);
            float h = Mathf.Clamp(Vector2.Dot(p, ba) / Vector2.Dot(ba, ba), 0f, r);
            return (p - ba * h).magnitude * Mathf.Sign(p.y * ba.x - p.x * ba.y);
        }
    }
}
