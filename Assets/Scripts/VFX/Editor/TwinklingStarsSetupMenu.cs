using Game.VFX;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>Menu command that fills the Skydome's sky with twinkling Star_Yellow particles (Tools > 429 Game > VFX).</summary>
    public static class TwinklingStarsSetupMenu
    {
        private const string SkydomePath = "Assets/Prefabs/Bases/Skydome.prefab";
        private const string StarModelPath = "Assets/Models/PlayGround/Star_Yellow.fbx";
        private const string StarMeshPath = "Assets/Models/PlayGround/Star_Yellow_Particle.asset";
        private const string AtlasMaterialPath = "Assets/Materials/Bedroom_Set_1_Mat.mat";
        private const string StarMaterialPath = "Assets/Materials/Star_Twinkle_Mat.mat";
        // URP's particle shader without fog: the scene fog (2-22 m) would otherwise hide the stars completely.
        private const string StarShaderName = "429/Particles/Unlit No Fog";
        private const string StarsName = "Twinkling Stars";

        // Stars sit on a half sphere this many meters around the player, inside the camera's 40 m far clip.
        // (The dome itself is about 75 m away, so stars on it would never be drawn.)
        private const float StarDistance = 30f;
        private const int MaxStars = 180;
        private const float MinLifetime = 4f, MaxLifetime = 9f;
        // Star size as a fraction of StarDistance: roughly 0.2 to 0.5 degrees across (the full moon is 0.5).
        private const float MinStarSize = 0.004f, MaxStarSize = 0.009f;

        /// <summary>
        /// Adds a "Twinkling Stars" particle system to the Skydome, or refreshes it if it's already there. Stars appear
        /// at random spots above the player, blink a few times and fade out, always facing the player.
        /// Run it again after changing the numbers above.
        /// </summary>
        [MenuItem("Tools/429 Game/VFX/Add Twinkling Stars To Skydome")]
        public static void AddTwinklingStars()
        {
            var atlas = AssetDatabase.LoadAssetAtPath<Material>(AtlasMaterialPath);
            var shader = Shader.Find(StarShaderName);
            if (atlas == null || shader == null)
            {
                Debug.LogError($"[Stars] Missing {AtlasMaterialPath} or the shader {StarShaderName}. Nothing changed.");
                return;
            }
            var starMesh = BuildStarMesh();
            if (starMesh == null) return;
            var material = BuildStarMaterial(shader, atlas);

            var root = PrefabUtility.LoadPrefabContents(SkydomePath);
            try
            {
                var stars = root.transform.Find(StarsName);
                if (stars == null)
                {
                    stars = new GameObject(StarsName).transform;
                    stars.SetParent(root.transform, false);
                }
                stars.localPosition = Vector3.zero;
                stars.localRotation = Quaternion.identity;
                stars.localScale = Vector3.one;
                if (!stars.TryGetComponent(out FollowCameraPosition _))
                    stars.gameObject.AddComponent<FollowCameraPosition>();

                if (!stars.TryGetComponent(out ParticleSystem particles))
                    particles = stars.gameObject.AddComponent<ParticleSystem>();
                // Shape and sizes are in Skydome-root units; the particles follow the root's scale.
                float radius = StarDistance / root.transform.lossyScale.x;
                SetUpStars(particles, starMesh, material, radius);

                PrefabUtility.SaveAsPrefabAsset(root, SkydomePath);
                Debug.Log($"[Stars] The Skydome now twinkles: up to {MaxStars} stars on a half sphere {StarDistance} m " +
                          $"around the player, using {starMesh.vertexCount}-vertex stars and {StarMaterialPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetUpStars(ParticleSystem particles, Mesh starMesh, Material material, float radius)
        {
            var main = particles.main;
            main.duration = 5f;
            main.loop = true;
            main.prewarm = true;                       // the sky is already full when the dome appears
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(MinLifetime, MaxLifetime);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(radius * MinStarSize, radius * MaxStarSize);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(1f, 0.85f, 0.55f));
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;   // the stars move with the player
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;       // follows the Skydome's scale
            main.maxParticles = MaxStars;

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = MaxStars / ((MinLifetime + MaxLifetime) * 0.5f);

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = radius;
            shape.radiusThickness = 0f;                // on the surface only
            shape.position = Vector3.zero;
            shape.rotation = new Vector3(-90f, 0f, 0f);   // the hemisphere bulges along +Z: point it up
            shape.scale = Vector3.one;

            // Each star fades in, blinks three times and fades out; different lifetimes keep them out of step.
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.15f, 1f), new Keyframe(0.3f, 0.45f), new Keyframe(0.45f, 1f),
                new Keyframe(0.6f, 0.5f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0f)));

            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
            var color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = fade;

            var spin = particles.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-20f * Mathf.Deg2Rad, 20f * Mathf.Deg2Rad);

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = starMesh;
            renderer.alignment = ParticleSystemRenderSpace.View;   // face the player
            renderer.sharedMaterial = material;
            renderer.enableGPUInstancing = true;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        /// <summary>
        /// A copy of the Star_Yellow mesh, centered, 1 unit across, and turned so its flat face points along Z. Particles
        /// facing the camera then show the star's face and spin around it. Saved next to the model.
        /// </summary>
        private static Mesh BuildStarMesh()
        {
            Mesh source = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(StarModelPath))
            {
                if (asset is Mesh mesh)
                {
                    source = mesh;
                    break;
                }
            }
            if (source == null)
            {
                Debug.LogError($"[Stars] No mesh found in {StarModelPath}. Nothing changed.");
                return null;
            }

            var copy = Object.Instantiate(source);
            var vertices = copy.vertices;
            if (vertices.Length == 0)
            {
                Object.DestroyImmediate(copy);
                Debug.LogError($"[Stars] Couldn't read the vertices of {StarModelPath}. Tick Read/Write in its import settings and run this again.");
                return null;
            }

            var bounds = source.bounds;
            var extent = bounds.size;
            var turn = extent.z <= extent.x && extent.z <= extent.y ? Quaternion.identity
                     : extent.y <= extent.x ? Quaternion.Euler(90f, 0f, 0f)    // lying flat: stand it up
                     : Quaternion.Euler(0f, -90f, 0f);                        // facing sideways: turn it around
            float scale = 1f / Mathf.Max(extent.x, Mathf.Max(extent.y, extent.z));

            var normals = copy.normals;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = turn * ((vertices[i] - bounds.center) * scale);
            for (int i = 0; i < normals.Length; i++) normals[i] = turn * normals[i];
            copy.vertices = vertices;
            if (normals.Length == vertices.Length) copy.normals = normals;
            copy.RecalculateBounds();
            copy.name = "Star_Yellow_Particle";

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(StarMeshPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(copy, StarMeshPath);
                return copy;
            }
            EditorUtility.CopySerialized(copy, existing);
            Object.DestroyImmediate(copy);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        /// <summary>Unlit additive particle material without fog, showing the star's yellow from the furniture atlas.</summary>
        private static Material BuildStarMaterial(Shader shader, Material atlas)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(StarMaterialPath);
            bool created = material == null;
            if (created) material = new Material(shader);
            else if (material.shader != shader) material.shader = shader;

            material.SetTexture("_BaseMap", atlas.GetTexture("_BaseMap"));
            material.SetColor("_BaseColor", Color.white);
            // The transparent setup the URP material inspector writes, with additive blending so stars glow.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;

            if (created) AssetDatabase.CreateAsset(material, StarMaterialPath);
            else EditorUtility.SetDirty(material);
            return material;
        }
    }
}
