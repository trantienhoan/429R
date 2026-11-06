using System.Collections;
using UnityEngine;

namespace Game.Paths
{
    /// <summary>
    /// A paid seed lock's block leaving like a rocket (user, 2026-11-06: "when the dice is paid, shoot the dice up the
    /// sky like a rocket then destroy it after it leaves the skydome"): it shakes and rumbles a moment with smoke at its
    /// foot, then ignites (a whoosh and a roaring thruster that flies with it) and speeds up into the sky with a slight
    /// spin, a flame jet and a growing smoke trail under it; once it's above the skydome (or after Most Seconds) it's
    /// gone, its trail drifting and fading after it. Seed Lock adds it when paid (Rocket Away on).
    /// </summary>
    public class RocketAway : MonoBehaviour
    {
        private AudioClip rumble, launch, thrust;
        private Material flame, smoke;
        private GameObject[] dust, cloud;
        private float cloudSize = 1f;
        private ParticleSystemRenderer smokeLook;
        private ParticleSystem flameJet, smokeTrail;
        private AudioSource rumbleVoice, roarVoice;

        private const float ShakeSeconds = 0.7f;
        private const float MostSeconds = 9f;

        /// <summary>Sends this block off into the sky.</summary>
        public static void Launch(GameObject block, AudioClip rumble, AudioClip launch, AudioClip thrust, Material flame, Material smoke,
            GameObject[] rumbleEffects, GameObject[] launchEffects, float launchEffectSize = 1f, GameObject smokeLook = null)
        {
            var rocket = block.AddComponent<RocketAway>();
            rocket.rumble = rumble;
            rocket.launch = launch;
            rocket.thrust = thrust;
            rocket.flame = flame;
            rocket.smoke = smoke;
            rocket.dust = rumbleEffects ?? System.Array.Empty<GameObject>();
            rocket.cloud = launchEffects ?? System.Array.Empty<GameObject>();
            rocket.cloudSize = launchEffectSize;
            rocket.smokeLook = smokeLook != null ? smokeLook.GetComponentInChildren<ParticleSystemRenderer>() : null;
        }

        private IEnumerator Start()
        {
            // Nothing pushes it or is pushed by it now; it's moved by hand. On its own (it sits in the rooftop, which
            // is cleared when the player steps through the gate), so it always finishes its flight.
            transform.SetParent(null, true);
            if (TryGetComponent(out Rigidbody body)) { body.isKinematic = true; body.constraints = RigidbodyConstraints.None; }
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            var bounds = BoundsOf();
            float half = bounds.extents.y;
            var foot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            var start = transform.position;
            var startTurn = transform.rotation;

            // The rumble: shaking on the spot, smoke at its foot, the view shaking.
            rumbleVoice = Play(rumble, foot, 0.9f);
            foreach (var effect in dust) if (effect != null) EffectPool.Play(effect, foot + Vector3.up * 0.05f);
            ShakeView("ShakeMed");
            for (float t = 0f; t < ShakeSeconds; t += Time.deltaTime)
            {
                float k = t / ShakeSeconds;
                transform.position = start + Random.insideUnitSphere * (0.02f + 0.04f * k);
                transform.rotation = startTurn * Quaternion.Euler(Random.Range(-1.5f, 1.5f) * k, 0f, Random.Range(-1.5f, 1.5f) * k);
                yield return null;
            }
            transform.SetPositionAndRotation(start, startTurn);

            // Ignition.
            Play(launch, foot, 1f);
            roarVoice = Play(thrust, transform.position, 0.85f);
            MakeExhaust(half);
            foreach (var effect in cloud) if (effect != null) EffectPool.Play(effect, foot + Vector3.up * 0.1f, cloudSize);
            ShakeView("ShakeBigger");

            float top = SkyTop();
            float speed = 0f, spin = 0f;
            for (float t = 0f; t < MostSeconds; t += Time.deltaTime)
            {
                // Slow off the pad, then faster and faster.
                float accel = Mathf.Lerp(6f, 38f, Mathf.Clamp01(t / 2.5f));
                speed += accel * Time.deltaTime;
                spin = Mathf.MoveTowards(spin, 140f, 60f * Time.deltaTime);
                transform.position += Vector3.up * speed * Time.deltaTime;
                transform.Rotate(0f, spin * Time.deltaTime, 0f, Space.World);
                if (roarVoice != null) roarVoice.transform.position = transform.position;
                // The rumble left on the pad dies away as it climbs.
                if (rumbleVoice != null)
                {
                    rumbleVoice.volume = Mathf.Lerp(0.9f, 0f, t / 1.2f);
                    if (t >= 1.2f) Destroy(rumbleVoice.gameObject);
                }
                if (transform.position.y - half > top) break;
                yield return null;
            }

            // Gone: the trail stays behind to drift and fade.
            foreach (var ps in new[] { flameJet, smokeTrail })
            {
                if (ps == null) continue;
                ps.transform.SetParent(null, true);
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Destroy(ps.gameObject, 6f);
            }
            Destroy(gameObject);
        }

        // Gone (or cleared away mid-flight): its sounds stop with it. Up at the skydome they're nearly silent anyway.
        private void OnDisable()
        {
            if (rumbleVoice != null) Destroy(rumbleVoice.gameObject);
            if (roarVoice != null) Destroy(roarVoice.gameObject);
        }

        // A flame jet and a smoke trail out of its foot, left behind in the world as it climbs.
        private void MakeExhaust(float half)
        {
            flameJet = Exhaust("Rocket Flame", flame, half, 0.32f, new Vector2(7f, 11f), new Vector2(0.5f, 0.95f), 90f, 12f,
                new Color(1f, 0.85f, 0.45f, 1f), new Color(1f, 0.35f, 0.05f, 0f), 1f, 1.3f);
            bool solid = smokeLook != null && smokeLook.renderMode == ParticleSystemRenderMode.Mesh;
            smokeTrail = Exhaust("Rocket Smoke", smoke, half, 3.2f, new Vector2(0.6f, 1.6f), solid ? new Vector2(0.7f, 1.2f) : new Vector2(0.8f, 1.4f),
                45f, 30f, new Color(0.78f, 0.76f, 0.74f, 0.75f), new Color(0.5f, 0.48f, 0.47f, 0f), 1f, solid ? 2.6f : 3.2f);
            if (solid) LookLike(smokeTrail, smokeLook);
        }

        // The trail's puffs drawn like the shared 3D smoke (its puff meshes and material), each turned its own way.
        private static void LookLike(ParticleSystem ps, ParticleSystemRenderer look)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            var meshes = new Mesh[look.meshCount];
            look.GetMeshes(meshes);
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.SetMeshes(meshes);
            renderer.sharedMaterial = look.sharedMaterial;
            renderer.alignment = look.alignment;
            var main = ps.main;
            main.startRotation3D = true;
            var turn = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationX = turn;
            main.startRotationY = turn;
            main.startRotationZ = turn;
        }

        private ParticleSystem Exhaust(string name, Material material, float half, float life, Vector2 speed, Vector2 size, float rate,
            float angle, Color from, Color to, float sizeFrom, float sizeTo)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.down * (half / Mathf.Max(0.0001f, transform.lossyScale.y));
            go.transform.rotation = Quaternion.LookRotation(Vector3.down);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 600;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle;
            shape.radius = 0.5f;
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
                new[] { new GradientAlphaKey(from.a, 0f), new GradientAlphaKey(from.a * 0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
            colour.color = new ParticleSystem.MinMaxGradient(gradient);
            var grow = ps.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, sizeFrom, 1f, sizeTo));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ps.Play();
            return ps;
        }

        // The top of the skydome (the global Skydome: its root is the flat floor disc, the dome is a child, so the
        // highest of all its meshes), or high up. Never less than 25 m above the pad.
        private float SkyTop()
        {
            float pad = transform.position.y, top = pad + 120f;
            var sky = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmGameObject("Skydome");
            if (sky != null && sky.Value != null)
            {
                float highest = float.MinValue;
                foreach (var r in sky.Value.GetComponentsInChildren<Renderer>())
                {
                    if (r is ParticleSystemRenderer) continue;
                    highest = Mathf.Max(highest, r.bounds.max.y);
                }
                if (highest > float.MinValue) top = highest + 2f;
            }
            return Mathf.Max(top, pad + 25f);
        }

        private Bounds BoundsOf()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(transform.position, Vector3.one);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        // A sound at a point, on its own object (returned, so it can ride along or fade); gone when the clip ends.
        private static AudioSource Play(AudioClip clip, Vector3 at, float volume)
        {
            if (clip == null) return null;
            var go = new GameObject("Rocket Sound");
            go.transform.position = at;
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 4f;
            source.maxDistance = 70f;
            source.dopplerLevel = 0f;
            source.Play();
            Destroy(go, clip.length + 0.1f);
            return source;
        }

        private static void ShakeView(string eventName)
        {
            var cam = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmGameObject("MainCam");
            if (cam == null || cam.Value == null) return;
            foreach (var fsm in cam.Value.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == "Info") fsm.SendEvent(eventName);
            }
        }
    }
}
