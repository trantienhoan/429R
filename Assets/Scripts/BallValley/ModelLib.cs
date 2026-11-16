using System.Collections.Generic;
using UnityEngine;

namespace Balls
{
    /// <summary>
    /// Loads imported models from Resources/BALLS/Models and places them at a known size, whatever scale and pivot they
    /// were exported with. Materials are chosen by the caller (plain colours for now; one shared texture later).
    /// </summary>
    public static class ModelLib
    {
        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, Bounds> bounds = new Dictionary<string, Bounds>();

        /// <summary>429R: forget what was loaded (the stage is gone; the next one loads afresh).</summary>
        public static void Clear()
        {
            prefabs.Clear();
            bounds.Clear();
        }

        public static bool Has(string name) => Load(name) != null;

        /// <summary>Is there a model at Resources/BALLS/<paramref name="path"/> (e.g. "Map/Rim_Block")?</summary>
        public static bool HasPath(string path) => LoadPath(path) != null;

        static GameObject Load(string name) => LoadPath("Models/" + name);

        static GameObject LoadPath(string path)
        {
            if (prefabs.TryGetValue(path, out var p)) return p;
            p = Resources.Load<GameObject>("BALLS/" + path);
            if (!p) Debug.LogWarning($"Model Resources/BALLS/{path} not found; using a stand-in shape.");
            prefabs[path] = p;
            return p;
        }

        /// <summary>
        /// Places a model exported from BALLS_Map.blend exactly as modelled (metres, origin at its base) under
        /// <paramref name="parent"/>, every part using <paramref name="material"/> (the shared palette).
        /// Its own colliders and animators are dropped. Returns null if the model is missing.
        /// </summary>
        public static Transform Place(string path, Transform parent, Material material)
        {
            var prefab = LoadPath(path);
            if (!prefab) return null;
            var inst = Object.Instantiate(prefab, parent, false);
            inst.name = prefab.name;
            foreach (var c in inst.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            foreach (var a in inst.GetComponentsInChildren<Animator>()) Object.Destroy(a);
            if (material)
            {
                foreach (var r in inst.GetComponentsInChildren<Renderer>())
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = material;
                    r.sharedMaterials = mats;
                }
            }
            return inst.transform;
        }

        /// <summary>
        /// Instantiates <paramref name="name"/> under <paramref name="parent"/>, centred on X/Z with its lowest point at the
        /// parent's origin, scaled so its height is <paramref name="height"/> (or, if 0, its widest side is <paramref name="width"/>).
        /// <paramref name="material"/> picks a material for each renderer. Returns null if the model is missing.
        /// </summary>
        public static Transform Spawn(string name, Transform parent, float height, float width, System.Func<Renderer, Material> material)
            => SpawnPath("Models/" + name, parent, height, width, material);

        /// <summary><see cref="Spawn"/> for any model under Resources/BALLS (e.g. "Map/Slime").</summary>
        public static Transform SpawnPath(string path, Transform parent, float height, float width, System.Func<Renderer, Material> material)
        {
            string name = path;
            var prefab = LoadPath(path);
            if (!prefab) return null;
            var inst = Object.Instantiate(prefab);
            inst.name = name;
            foreach (var c in inst.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            foreach (var a in inst.GetComponentsInChildren<Animator>()) Object.Destroy(a);

            if (!bounds.TryGetValue(name, out var b))
            {
                // Measure once, with the model at the origin, unrotated (how 429R places these models too: the
                // rotation stored on the imported root would lay the slime on its side).
                inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                bool first = true;
                foreach (var r in inst.GetComponentsInChildren<Renderer>())
                {
                    if (first) { b = r.bounds; first = false; }
                    else b.Encapsulate(r.bounds);
                }
                if (first) b = new Bounds(Vector3.zero, Vector3.one);
                bounds[name] = b;
            }

            float scale = height > 0f
                ? height / Mathf.Max(0.0001f, b.size.y)
                : width / Mathf.Max(0.0001f, Mathf.Max(b.size.x, b.size.z));
            var holder = new GameObject(name + " (fit)").transform;
            holder.SetParent(parent, false);
            holder.localScale = Vector3.one * scale;
            Vector3 anchor = new Vector3(b.center.x, b.min.y, b.center.z);
            inst.transform.SetParent(holder, false);
            inst.transform.localPosition = -anchor;
            inst.transform.localRotation = Quaternion.identity;

            if (material != null)
            {
                foreach (var r in inst.GetComponentsInChildren<Renderer>())
                {
                    var mats = r.sharedMaterials;
                    var m = material(r);
                    for (int i = 0; i < mats.Length; i++) mats[i] = m;
                    r.sharedMaterials = mats;
                }
            }
            return holder;
        }
    }
}
