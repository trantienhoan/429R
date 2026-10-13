using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Plays one-shot effects (explosions, puffs of smoke) from copies kept ready, instead of making a new copy and
    /// destroying it every time. Each effect is also warmed up once, as soon as something that uses it is in the scene
    /// (Warm): a tiny copy is shown in front of the camera for a moment, so the headset gets its shaders ready at load
    /// and the first explosion doesn't stutter. And no particle of these effects is drawn bigger than half the view
    /// (Max Screen Size), so an explosion in the player's face doesn't cover both eyes with layer after layer of
    /// see-through smoke, which is what slows the headset down most.
    /// </summary>
    public static class EffectPool
    {
        /// <summary>The biggest a particle of a pooled effect is drawn, as a share of the view's height.</summary>
        public const float MaxScreenSize = 0.5f;

        // A copy is handed out again this many seconds after it started (the effects last 1-2 s).
        private const float Lifetime = 3f;
        // Warming up: the copy is this small, this far in front of the camera, for this many frames.
        private const float WarmScale = 0.002f;
        private const float WarmDistance = 1.5f;
        private const int WarmFrames = 3;

        private class Copy
        {
            public GameObject instance;
            public Vector3 scale;
            public float freeAt;
            public int warmFramesLeft;
        }

        private static readonly Dictionary<GameObject, List<Copy>> pools = new();
        private static readonly List<Copy> warming = new();
        private static Runner runner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            pools.Clear();
            warming.Clear();
            runner = null;
        }

        /// <summary>Gets an effect ready (once per effect), so its first use doesn't stutter.</summary>
        public static void Warm(GameObject prefab)
        {
            if (prefab == null || !EnsureRunner()) return;
            if (pools.TryGetValue(prefab, out var list) && list.Count > 0) return;

            var copy = Make(prefab);
            copy.warmFramesLeft = WarmFrames;
            copy.freeAt = float.MaxValue;
            warming.Add(copy);
        }

        /// <summary>Plays the effect at a point, this many times its prefab's size. Returns the copy playing it.</summary>
        public static GameObject Play(GameObject prefab, Vector3 point, float scale = 1f)
        {
            if (prefab == null || !EnsureRunner()) return null;

            var copy = Free(prefab) ?? Make(prefab);
            copy.warmFramesLeft = 0;
            warming.Remove(copy);
            copy.freeAt = Time.time + Lifetime;

            var go = copy.instance;
            go.SetActive(false);
            go.transform.SetPositionAndRotation(point, Quaternion.identity);
            go.transform.localScale = copy.scale * scale;
            // Switched on again: its particles and its FSMs (e.g. one that hides it again) start over.
            go.SetActive(true);
            foreach (var particles in go.GetComponentsInChildren<ParticleSystem>())
            {
                if (particles.transform.parent != null && particles.transform.parent.GetComponentInParent<ParticleSystem>() != null) continue;
                particles.Clear(true);
                particles.Play(true);
            }
            return go;
        }

        private static Copy Free(GameObject prefab)
        {
            if (!pools.TryGetValue(prefab, out var list)) return null;
            list.RemoveAll(c => c.instance == null);
            foreach (var copy in list)
            {
                if (copy.warmFramesLeft > 0) continue;
                if (!copy.instance.activeSelf || Time.time >= copy.freeAt) return copy;
            }
            return null;
        }

        private static Copy Make(GameObject prefab)
        {
            var go = Object.Instantiate(prefab, runner.transform);
            go.name = prefab.name;
            go.SetActive(false);
            foreach (var renderer in go.GetComponentsInChildren<ParticleSystemRenderer>(true))
                renderer.maxParticleSize = Mathf.Min(renderer.maxParticleSize, MaxScreenSize);

            var copy = new Copy { instance = go, scale = prefab.transform.localScale };
            if (!pools.TryGetValue(prefab, out var list)) pools[prefab] = list = new List<Copy>();
            list.Add(copy);
            return copy;
        }

        // The copies live under one object in the scene, which also hides finished copies and runs the warm-ups.
        private static bool EnsureRunner()
        {
            if (runner != null) return true;
            // A new scene: the copies of the old one went with it.
            pools.Clear();
            warming.Clear();
            runner = new GameObject("Effect Pool").AddComponent<Runner>();
            return runner != null;
        }

        private class Runner : MonoBehaviour
        {
            private void LateUpdate()
            {
                for (int i = warming.Count - 1; i >= 0; i--)
                {
                    var copy = warming[i];
                    if (copy.instance == null)
                    {
                        warming.RemoveAt(i);
                        continue;
                    }
                    var eyes = Camera.main;
                    if (eyes == null) continue;

                    if (!copy.instance.activeSelf)
                    {
                        // Tiny and right in front of the camera, so it's drawn (and its shaders made ready) but not seen.
                        copy.instance.transform.SetPositionAndRotation(eyes.transform.position + eyes.transform.forward * WarmDistance, Quaternion.identity);
                        copy.instance.transform.localScale = copy.scale * WarmScale;
                        copy.instance.SetActive(true);
                    }
                    else if (--copy.warmFramesLeft <= 0)
                    {
                        copy.instance.SetActive(false);
                        copy.instance.transform.localScale = copy.scale;
                        copy.freeAt = 0f;
                        warming.RemoveAt(i);
                    }
                }

                foreach (var list in pools.Values)
                {
                    foreach (var copy in list)
                    {
                        if (copy.instance != null && copy.warmFramesLeft <= 0 && copy.instance.activeSelf && Time.time >= copy.freeAt)
                            copy.instance.SetActive(false);
                    }
                }
            }

            private void OnDestroy()
            {
                if (runner == this) runner = null;
            }
        }
    }
}
