using Game.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > VFX > Add Leaf Burst To Pumpkin Tree. The rooftop's Pumpkin_Tree drops its own leaves when it
    /// gets hurt: a "Leaves" particle system in its crown that uses the tree's leaf picture (pumpkin_leaves.png, four
    /// maple leaves; each falling leaf is one of them at random), and Leaf Burst on the tree, which drops a few on every
    /// hit and a big burst when the tree dies. The leaves glow like the ones on the tree (unlit), flutter down, land on
    /// the roof and shrink away. Running it again sets everything up anew (the numbers below).
    /// </summary>
    public static class LeafBurstSetupMenu
    {
        private const string TreePath = "Assets/Prefabs/Items/Breakable/Pumpkin_Tree.prefab";
        private const string LeafTexturePath = "Assets/Images/pumpkin_leaves.png";
        private const string MaterialPath = "Assets/Materials/Pumpkin_Leaf_Particle_Mat.mat";
        private const string LeavesName = "Leaves";

        // The crown, in the tree's own space: the leaves sit 1-2.7 m from the trunk, 3.5-4.7 m up.
        private static readonly Vector3 CrownCentre = new(0f, 4.1f, 0f);
        private const float CrownRadius = 2.5f;
        private const float CrownFlatness = 0.4f;
        // Each falling leaf, in metres (the leaves on the tree are about 0.65 m across).
        private const float MinLeafSize = 0.3f, MaxLeafSize = 0.55f;
        private const int MaxLeaves = 60;

        [MenuItem("Tools/429 Game/VFX/Add Leaf Burst To Pumpkin Tree")]
        public static void AddLeafBurst()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == TreePath)
            {
                Debug.LogError($"[Leaf Burst] {TreePath} is open in Prefab Mode: close it first (it would save its old copy over the change). Nothing changed.");
                return;
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(LeafTexturePath);
            if (texture == null)
            {
                Debug.LogError($"[Leaf Burst] No leaf picture at {LeafTexturePath}. Nothing changed.");
                return;
            }
            var material = BuildMaterial(texture);

            var root = PrefabUtility.LoadPrefabContents(TreePath);
            try
            {
                var child = root.transform.Find(LeavesName);
                if (child == null)
                {
                    child = new GameObject(LeavesName).transform;
                    child.SetParent(root.transform, false);
                }
                child.gameObject.layer = root.layer;
                child.localPosition = Vector3.zero;
                child.localRotation = Quaternion.identity;
                child.localScale = Vector3.one;
                if (!child.TryGetComponent(out ParticleSystem particles)) particles = child.gameObject.AddComponent<ParticleSystem>();
                SetUpLeaves(particles, material);

                if (!root.TryGetComponent(out LeafBurst burst)) burst = root.AddComponent<LeafBurst>();
                var so = new SerializedObject(burst);
                so.FindProperty("leaves").objectReferenceValue = particles;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, TreePath);
                Debug.Log($"[Leaf Burst] Pumpkin_Tree now drops its leaves when hurt (a few a hit, a burst when it dies), up to {MaxLeaves} at once, with {MaterialPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Tools/429 Game/VFX/Add Leaf Burst To Pumpkin Tree", true)]
        private static bool NotPlaying() => !EditorApplication.isPlaying;

        private static void SetUpLeaves(ParticleSystem particles, Material material)
        {
            var main = particles.main;
            main.duration = 5f;
            main.loop = true;
            main.playOnAwake = true;                  // playing, but drops nothing until Leaf Burst says so
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(MinLeafSize, MaxLeafSize);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(1f, 0.85f, 0.7f));
            main.gravityModifier = 0.08f;            // drifts down slowly, like a leaf
            main.simulationSpace = ParticleSystemSimulationSpace.World;   // falling leaves don't follow the tree's jiggle
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;       // the crown grows with the tree
            main.maxParticles = MaxLeaves;
            main.stopAction = ParticleSystemStopAction.None;

            var emission = particles.emission;
            emission.enabled = false;

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = CrownRadius;
            shape.radiusThickness = 1f;               // anywhere in the crown, not only its outside
            shape.position = CrownCentre;
            shape.rotation = Vector3.zero;
            shape.scale = new Vector3(1f, CrownFlatness, 1f);

            // Flutters sideways and spins as it falls.
            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.8f;
            noise.frequency = 0.5f;
            noise.scrollSpeed = 0.4f;
            noise.damping = true;
            noise.octaveCount = 1;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            noise.rotationAmount = 0.4f;

            var drag = particles.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = 2.5f;
            drag.dampen = 0.2f;
            drag.drag = 1f;

            var spin = particles.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-120f * Mathf.Deg2Rad, 120f * Mathf.Deg2Rad);

            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.6f), new Keyframe(0.08f, 1f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0f)));

            // Lands on the roof (and the furniture) and stays there until it shrinks away.
            var collision = particles.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.dampen = 0.7f;
            collision.bounce = 0.05f;
            collision.lifetimeLoss = 0f;
            collision.collidesWith = LayerMask.GetMask("Default");
            collision.quality = ParticleSystemCollisionQuality.Medium;
            collision.enableDynamicColliders = true;
            collision.sendCollisionMessages = false;

            // The picture holds four leaves (2 x 2): each falling leaf is one of them, picked at random.
            var sheet = particles.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = 2;
            sheet.numTilesY = 2;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 3.999f);
            sheet.cycleCount = 1;

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sharedMaterial = material;
            renderer.sortMode = ParticleSystemSortMode.None;
            renderer.maxParticleSize = 0.6f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        // URP's unlit particle shader, cut out by the picture's alpha (cheap, sorts right, no see-through halo), both sides.
        private static Material BuildMaterial(Texture2D texture)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            bool created = material == null;
            if (created) material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "Pumpkin_Leaf_Particle_Mat" };
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.5f);
            material.SetFloat("_Cull", 0f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.renderQueue = (int)RenderQueue.AlphaTest;
            if (created) AssetDatabase.CreateAsset(material, MaterialPath);
            else EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }
    }
}
