using UnityEngine;

namespace Balls
{
    /// <summary>A grass tuft that pops up on the valley floor. Only slimes eat it (and grow).</summary>
    public class GrassTuft : MonoBehaviour
    {
        /// <summary>The tufts from BALLS_Map.blend (shared palette texture); 429R's grass is the fallback.</summary>
        public static readonly string[] MapModels = { "Map/Grass_Small", "Map/Grass_Tall", "Map/Grass_Wide" };
        public static readonly string[] Models = { "grass_1_big", "grass_1_medium", "grass_2_big" };

        // 429R: no netId (no network)
        public byte variant;
        public Transform vis;
        float appear;
        Vector3 baseScale;

        void Start()
        {
            if (vis) baseScale = vis.localScale;
        }

        void Update()
        {
            if (!vis || appear >= 1f) return;
            appear = Mathf.Min(1f, appear + Time.deltaTime * 2.5f);
            float t = appear - 1f;
            vis.localScale = baseScale * (1f + 2.7f * t * t * t + 1.7f * t * t);   // ease-out-back pop
        }

        /// <summary>Builds the tuft's look: the imported grass model, or a few green blades if it's missing.</summary>
        public static Transform BuildVisual(Transform root, byte variant, float height)
        {
            string mapModel = MapModels[variant % MapModels.Length];
            if (ModelLib.HasPath(mapModel))
            {
                var holder = new GameObject("Vis").transform;
                holder.SetParent(root, false);
                holder.localScale = Vector3.one * (height / 1.2f);   // modelled about 1.2 m tall
                ModelLib.Place(mapModel, holder, Gfx.PaletteMaterial());
                return holder;
            }
            var green = Gfx.Lit(new Color(0.30f, 0.62f, 0.22f), 0.2f);
            var dark = Gfx.Lit(new Color(0.20f, 0.45f, 0.16f), 0.2f);
            var vis = ModelLib.Spawn(Models[variant % Models.Length], root, height, 0f, r => r.name.Contains("Ground") ? dark : green);
            if (vis) return vis;
            vis = new GameObject("Vis").transform;
            vis.SetParent(root, false);
            for (int i = 0; i < 5; i++)
            {
                float a = i * 72f;
                Gfx.Visual("Blade", Gfx.Cone, i % 2 == 0 ? green : dark, vis, Quaternion.Euler(0f, a, 0f) * new Vector3(0.18f, 0f, 0f),
                    new Vector3(0.25f, height, 0.25f), Quaternion.Euler(0f, a, 12f));
            }
            return vis;
        }
    }
}
