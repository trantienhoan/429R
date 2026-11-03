using System.Collections.Generic;
using System.Linq;
using Game.Bonus;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// The Paint Bees bonus round, the Playroom's bonus in place of the nesting pumpkins (user, 2026-11-03: "this is bonus
    /// round replaces the Punching Pumpkin special round after the Playroom"; the bees fly to Pumpkin_Pillow_Party's beat):
    /// - Tools > 429 Game > Bonus > Set Up Paint Bees Bonus builds its parts and never touches the scene:
    ///   Paint_Bee (Bee's model and Animator, no Spider Brain, a grey copy of its texture so any colour shows, its buzz,
    ///   a light trail), Paint_Bucket (pumpkin_bucket with paint in it, a glow under it, a ripple, paint dripping down its
    ///   side and splatting on the floor, a growing puddle), Paint_Bees_Start (a ring on the floor, a bobbing arrow,
    ///   sparkles), Paint_Bees_Road (the lit road the bees fly in on: runway lights, lights marching in on the beat,
    ///   embers, a danger ring), their materials (Assets/Data/Effects/Paint Bees *.mat) and Paint_Bees_Round
    ///   (Prefabs/Bases): the song at 100 BPM, the user's sounds, the bonus round's candies, sky, puff and fireworks.
    ///   Safe to run again: the round's settings are only filled where empty, so changes by hand stay.
    /// - Tools > 429 Game > Bonus > Use Paint Bees In Playroom (2 Game Scene open): GAMESTAGES' "Bonus Round PR" plays
    ///   Paint_Bees_Round, and the Playroom's bonus stage no longer makes the pumpkin chest (Playroom_Minigame_Treasure_Pos
    ///   off). Save the scene after.
    /// </summary>
    public static class PaintBeesSetupMenu
    {
        public const string RoundPath = "Assets/Prefabs/Bases/Paint_Bees_Round.prefab";
        private const string PartsFolder = "Assets/Prefabs/Bases/Paint_Bees";
        private const string BeePath = PartsFolder + "/Paint_Bee.prefab";
        private const string BucketPath = PartsFolder + "/Paint_Bucket.prefab";
        private const string StartPath = PartsFolder + "/Paint_Bees_Start.prefab";
        private const string RoadPath = PartsFolder + "/Paint_Bees_Road.prefab";
        private const string NoFogShader = "429/Particles/Unlit No Fog";
        private const string ArrowMeshPath = PartsFolder + "/Paint_Bees_Arrow.asset";
        private const string EffectsFolder = "Assets/Data/Effects/";
        private const string SourceBeePath = "Assets/Prefabs/Enemies/Bee.prefab";
        private const string BeeGreyPath = "Assets/M_Textures/Bee_Smol/Paint_Bee_Grey.png";
        private const string BucketModelPath = "Assets/Models/Bedroom_Stuffs/pumpkin_bucket.fbx";
        private const string Textures = "Assets/Lana Studio/Hyper Casual FX/Textures/";
        // Made for the round: a crisp round blob (drops) and a splat with droplets (floor splats, puddles, the view).
        private const string BlobTexture = "Assets/M_Textures/Paint_Bees/Paint_Blob.png";
        private const string SplatTexture = "Assets/M_Textures/Paint_Bees/Paint_Splat.png";
        private const string SongPath = "Assets/Audio/SFX/Pumpkin_Pillow_Party.wav";
        private const string Sfx = "Assets/Audio/SFX/";
        private const string TickPath = "Assets/Audio/Kenney Audio/kenney_interface-sounds/Audio/tick_002.ogg";
        private const string CandyPath = "Assets/Prefabs/Items/Breakable/Candy_{0}.prefab";
        private const string SkyPath = "Assets/Prefabs/Bases/Bonus_Stage_Sky.prefab";
        private const string PuffPath = "Assets/Prefabs/Effects/Smoke/Smoke_Flash_Small_Puff.prefab";
        private const string FireworkPath = "Assets/Prefabs/Light/Firework_Big.prefab";
        private const string FlashPath = "Assets/Lana Studio/Hyper Casual FX/Prefabs/Flash/Flash_round_yellow.prefab";
        private const string FontPath = "Assets/Fonts/roboto/Roboto-Bold SDF.asset";
        private const string PlayroomStageName = "--Playroom--MiniGame--";
        private const string ChestSpotName = "Playroom_Minigame_Treasure_Pos";
        private const string BonusState = "Bonus Round PR";

        // Sizes, in metres: the bee across, the bucket across, how deep the paint sits under its rim.
        private const float BeeSize = 0.3f;
        private const float BucketWidth = 0.55f;
        private const float PaintInset = 0.06f;
        // How much a bee glows its colour (it reads in a dark room).
        private const float BeeGlow = 0.65f;
        // Where the round floats the paint (Paint Height): the glow under the bucket goes this far down, on the floor.
        private const float PaintHeight = 0.85f;
        private static readonly Color Gold = new(1f, 0.78f, 0.2f, 1f);

        [MenuItem("Tools/429 Game/Bonus/Set Up Paint Bees Bonus")]
        public static void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(PartsFolder)) AssetDatabase.CreateFolder("Assets/Prefabs/Bases", "Paint_Bees");
            var drop = DangerFlameSetupMenu.MakeMaterial("Paint Bees Drop", BlobTexture, false);
            var splat = DangerFlameSetupMenu.MakeMaterial("Paint Bees Splat", SplatTexture, false);
            var bee = BuildBee();
            var bucket = BuildBucket(drop, splat);
            var start = BuildStart();
            var road = BuildRoad();
            var notes = BuildRound(bee, bucket, start, road, drop, splat);
            AssetDatabase.SaveAssets();
            Debug.Log("[Paint Bees] Built " + string.Join(", ", new[] { BeePath, BucketPath, StartPath, RoadPath, RoundPath }) + "; " + notes +
                      ". Next: Tools > 429 Game > Bonus > Use Paint Bees In Playroom, then save the scene.");
        }

        // ---------- The bee ----------

        private static PaintBee BuildBee()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceBeePath);
            var copy = Object.Instantiate(source);
            var root = new GameObject("Paint_Bee");
            try
            {
                var model = copy.GetComponentsInChildren<Transform>(true).First(t => t.name == "Bee_Models");
                model.SetParent(root.transform, false);
                model.localPosition = Vector3.zero;
                model.localRotation = Quaternion.identity;
                // Only its looks and its Animator: no brain, FSMs, colliders, effects or sounds of the enemy bee (two
                // passes: a part another one needs goes once that one has gone).
                foreach (var t in model.GetComponentsInChildren<Transform>(true).Where(t => t != model &&
                             (t.name.StartsWith("ProximityBubble") || t.GetComponent<ParticleSystem>() != null)).ToArray())
                {
                    if (t != null) Object.DestroyImmediate(t.gameObject);
                }
                for (int pass = 0; pass < 2; pass++)
                {
                    foreach (var c in model.GetComponentsInChildren<Component>(true))
                    {
                        if (c == null || c is Transform || c is Animator || c is Renderer || c is MeshFilter) continue;
                        Object.DestroyImmediate(c);
                    }
                }
                var animator = model.GetComponent<Animator>();
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var looks = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r => r.gameObject).Distinct().ToArray();
                var material = BeeMaterial(looks.SelectMany(l => l.GetComponent<Renderer>().sharedMaterials).FirstOrDefault(m => m != null));
                foreach (var look in looks)
                {
                    var r = look.GetComponent<Renderer>();
                    r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    look.SetActive(true);
                }
                // As big as Bee Size across.
                var seen = looks.Length > 0 ? Bounds(looks[0], null).size : Vector3.one;
                model.localScale *= BeeSize / Mathf.Max(0.001f, Mathf.Max(seen.x, seen.y, seen.z));

                var buzz = root.AddComponent<AudioSource>();
                buzz.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + "bee_fly.wav");
                buzz.loop = true;
                buzz.playOnAwake = false;
                buzz.spatialBlend = 1f;
                buzz.minDistance = 0.4f;
                buzz.maxDistance = 8f;
                buzz.rolloffMode = AudioRolloffMode.Logarithmic;
                buzz.dopplerLevel = 0f;
                buzz.volume = 0.3f;

                // Its light trail (it takes the bee's colour; red hot while it dives).
                var trail = new GameObject("Trail").AddComponent<TrailRenderer>();
                trail.transform.SetParent(root.transform, false);
                trail.time = 0.35f;
                trail.minVertexDistance = 0.02f;
                trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.07f), new Keyframe(1f, 0f));
                trail.numCapVertices = 2;
                trail.sharedMaterial = NoFog(DangerFlameSetupMenu.MakeMaterial("Paint Bees Trail", "", true));
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;

                var bee = root.AddComponent<PaintBee>();
                var so = new SerializedObject(bee);
                so.FindProperty("trail").objectReferenceValue = trail;
                so.FindProperty("animator").objectReferenceValue = animator;
                so.FindProperty("buzz").objectReferenceValue = buzz;
                so.FindProperty("glow").floatValue = BeeGlow;
                var list = so.FindProperty("looks");
                list.arraySize = looks.Length;
                for (int i = 0; i < looks.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = looks[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, BeePath).GetComponent<PaintBee>();
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(copy);
            }
        }

        // The bee's own material with a grey copy of its texture (so the round's colour shows, stripes and all), glowing
        // its colour all over.
        private static Material BeeMaterial(Material own)
        {
            string path = EffectsFolder + "Paint Bees Bee.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(own);
                AssetDatabase.CreateAsset(material, path);
            }
            var grey = AssetDatabase.LoadAssetAtPath<Texture2D>(BeeGreyPath);
            material.SetTexture("_BaseMap", grey);
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_EmissionMap", grey);
            material.SetColor("_EmissionColor", Color.black);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- The bucket ----------

        private static PaintBucket BuildBucket(Material drop, Material splat)
        {
            var root = new GameObject("Paint_Bucket");
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BucketModelPath));
                model.name = "Bucket";
                model.transform.SetParent(root.transform, false);
                foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                // Bucket Width across, its paint's surface (Paint Inset under the rim) at the root.
                var b = Bounds(model, root.transform);
                float scale = BucketWidth / Mathf.Max(0.001f, Mathf.Max(b.size.x, b.size.z));
                model.transform.localScale *= scale;
                b = Bounds(model, root.transform);
                // Moved from where the model file puts it (its own offset counts), not from zero.
                model.transform.localPosition -= new Vector3(b.center.x, b.max.y - PaintInset, b.center.z);
                float inner = 0.42f * BucketWidth;

                var paint = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                paint.name = "Paint";
                Object.DestroyImmediate(paint.GetComponent<Collider>());
                paint.transform.SetParent(root.transform, false);
                paint.transform.localScale = new Vector3(inner * 2f, 0.004f, inner * 2f);
                var paintRenderer = paint.GetComponent<Renderer>();
                paintRenderer.sharedMaterial = PaintMaterial();
                paintRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                var glow = Flat(root, "Floor Glow", DangerFlameSetupMenu.MakeMaterial("Paint Bees Glow", Textures + "Circle03.png", true), 1.1f);
                glow.transform.localPosition = new Vector3(0f, -PaintHeight + 0.02f, 0f);
                // The puddle under it (it grows as the round goes on) and the floor its drips splat on.
                var puddle = Flat(root, "Puddle", splat, 0.3f);
                puddle.transform.localPosition = new Vector3(0f, -PaintHeight + 0.012f, 0f);
                var floor = new GameObject("Floor").transform;
                floor.SetParent(root.transform, false);
                floor.localPosition = new Vector3(0f, -PaintHeight, 0f);
                var drips = Drips(root, drop, splat, floor);

                var ripple = new GameObject("Ripple").AddComponent<LineRenderer>();
                ripple.transform.SetParent(root.transform, false);
                ripple.transform.localPosition = new Vector3(0f, 0.004f, 0f);
                ripple.useWorldSpace = false;
                ripple.loop = true;
                ripple.positionCount = 40;
                for (int i = 0; i < 40; i++)
                {
                    float a = i / 40f * Mathf.PI * 2f;
                    ripple.SetPosition(i, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (inner * 0.9f));
                }
                ripple.widthMultiplier = 0.012f;
                ripple.sharedMaterial = DangerFlameSetupMenu.MakeMaterial("Paint Bees Ripple", "", true);
                ripple.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ripple.enabled = false;

                var bucket = root.AddComponent<PaintBucket>();
                var so = new SerializedObject(bucket);
                so.FindProperty("paint").objectReferenceValue = paintRenderer;
                so.FindProperty("floorGlow").objectReferenceValue = glow.GetComponent<Renderer>();
                so.FindProperty("ripple").objectReferenceValue = ripple;
                so.FindProperty("reach").floatValue = inner + 0.06f;
                so.FindProperty("drips").objectReferenceValue = drips;
                so.FindProperty("puddle").objectReferenceValue = puddle.GetComponent<Renderer>();
                var onFloor = so.FindProperty("onFloor");
                var floorParts = new Object[] { glow.transform, puddle.transform, floor };
                onFloor.arraySize = floorParts.Length;
                for (int i = 0; i < floorParts.Length; i++) onFloor.GetArrayElementAtIndex(i).objectReferenceValue = floorParts[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, BucketPath).GetComponent<PaintBucket>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // The paint: glossy, its colour (and a little glow) set by the bucket.
        private static Material PaintMaterial()
        {
            string path = EffectsFolder + "Paint Bees Paint.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.9f);
            material.SetColor("_EmissionColor", Color.black);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- The start ring ----------

        private static GameObject BuildStart()
        {
            var root = new GameObject("Paint_Bees_Start");
            try
            {
                var glowMat = DangerFlameSetupMenu.MakeMaterial("Paint Bees Start Glow", Textures + "Circle03.png", true);
                glowMat.SetColor("_BaseColor", new Color(Gold.r, Gold.g, Gold.b, 0.45f));
                var ringMat = DangerFlameSetupMenu.MakeMaterial("Paint Bees Start Ring", Textures + "Circle06.png", true);
                ringMat.SetColor("_BaseColor", Gold);
                Flat(root, "Glow", glowMat, 1.7f).transform.localPosition = new Vector3(0f, 0.01f, 0f);
                Flat(root, "Ring", ringMat, 1.1f).transform.localPosition = new Vector3(0f, 0.015f, 0f);

                var arrow = new GameObject("Arrow");
                arrow.transform.SetParent(root.transform, false);
                arrow.transform.localPosition = new Vector3(0f, 1.7f, 0f);
                arrow.AddComponent<MeshFilter>().sharedMesh = ArrowMesh();
                var arrowRenderer = arrow.AddComponent<MeshRenderer>();
                arrowRenderer.sharedMaterial = ArrowMaterial();
                arrowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                var sparkles = new GameObject("Sparkles").AddComponent<ParticleSystem>();
                sparkles.transform.SetParent(root.transform, false);
                sparkles.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                sparkles.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                var main = sparkles.main;
                main.loop = true;
                main.playOnAwake = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
                main.startColor = Gold;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 60;
                var emission = sparkles.emission;
                emission.rateOverTime = 14f;
                var shape = sparkles.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.5f;
                shape.radiusThickness = 0.2f;
                var fade = sparkles.colorOverLifetime;
                fade.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
                fade.color = gradient;
                var sparkleRenderer = sparkles.GetComponent<ParticleSystemRenderer>();
                sparkleRenderer.sharedMaterial = DangerFlameSetupMenu.MakeMaterial("Paint Bees Sparkle", Textures + "Flare03.png", true);
                return PrefabUtility.SaveAsPrefabAsset(root, StartPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // A gold arrow pointing down at the ring: a cone with its tip at the bottom, on a short shaft.
        private static Mesh ArrowMesh()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ArrowMeshPath);
            bool fresh = mesh == null;
            if (fresh) mesh = new Mesh { name = "Paint_Bees_Arrow" };
            const int sides = 12;
            const float headR = 0.13f, headH = 0.2f, shaftR = 0.05f, shaftTop = 0.42f;
            var v = new List<Vector3>();
            var t = new List<int>();
            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                t.Add(v.Count); v.Add(a);
                t.Add(v.Count); v.Add(b);
                t.Add(v.Count); v.Add(c);
            }
            Vector3 Ring(float r, float y, int i)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
            }
            for (int i = 0; i < sides; i++)
            {
                // The head: the cone's sides down to the tip, and its flat top.
                Tri(Vector3.zero, Ring(headR, headH, i + 1), Ring(headR, headH, i));
                Tri(new Vector3(0f, headH, 0f), Ring(headR, headH, i), Ring(headR, headH, i + 1));
                // The shaft's sides and its top.
                var a0 = Ring(shaftR, headH, i);
                var a1 = Ring(shaftR, headH, i + 1);
                var b0 = Ring(shaftR, shaftTop, i);
                var b1 = Ring(shaftR, shaftTop, i + 1);
                Tri(a0, a1, b1);
                Tri(a0, b1, b0);
                Tri(new Vector3(0f, shaftTop, 0f), b1, b0);
            }
            mesh.Clear();
            mesh.SetVertices(v);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (fresh) AssetDatabase.CreateAsset(mesh, ArrowMeshPath);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Material ArrowMaterial()
        {
            string path = EffectsFolder + "Paint Bees Arrow.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", Gold);
            material.SetFloat("_Smoothness", 0.6f);
            material.SetColor("_EmissionColor", new Color(1f, 0.6f, 0.1f) * 1.3f);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
            return material;
        }

        // Paint running down the bucket's side from its rim: slow at first (along the side), then falling off; each drop
        // splats on the floor (Floor) where it lands.
        private static ParticleSystem Drips(GameObject bucket, Material drop, Material splat, Transform floor)
        {
            var drips = new GameObject("Drips").AddComponent<ParticleSystem>();
            drips.transform.SetParent(bucket.transform, false);
            drips.transform.localPosition = new Vector3(0f, PaintInset * 0.5f, 0f);
            drips.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = drips.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 1.8f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.05f);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.05f), new Keyframe(0.32f, 0.07f), new Keyframe(0.42f, 1f), new Keyframe(1f, 1f)));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            var emission = drips.emission;
            emission.rateOverTime = 5f;
            var shape = drips.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = BucketWidth * 0.5f;
            shape.radiusThickness = 0f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            var renderer = drips.GetComponent<ParticleSystemRenderer>();
            // Long as they fall, like drips.
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f;
            renderer.lengthScale = 1.3f;
            renderer.sharedMaterial = drop;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            PaintSplats.Add(drips, floor, splat, 1f, new Vector2(0.05f, 0.11f));
            return drips;
        }

        // ---------- The bees' road ----------

        private static PaintBeesRoad BuildRoad()
        {
            var light = NoFog(DangerFlameSetupMenu.MakeMaterial("Paint Bees Light", Textures + "Circle01.png", true));
            var lane = NoFog(DangerFlameSetupMenu.MakeMaterial("Paint Bees Lane", "", true));
            var orb = NoFog(DangerFlameSetupMenu.MakeMaterial("Paint Bees Orb", Textures + "Circle03.png", true));
            var ring = NoFog(DangerFlameSetupMenu.MakeMaterial("Paint Bees Danger Ring", Textures + "Circle06.png", true));
            var root = new GameObject("Paint_Bees_Road");
            try
            {
                // Lights marching in along the lanes: gold far off, hot red close by, sparkling as they come.
                var marchers = Lights(root, "Marching Lights", orb, 160);
                var march = marchers.colorOverLifetime;
                march.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(new Color(1f, 0.85f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.45f, 0.1f), 0.6f), new GradientColorKey(new Color(1f, 0.12f, 0.05f), 0.95f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(1f, 0.88f), new GradientAlphaKey(0f, 1f) });
                march.color = gradient;
                var grow = marchers.sizeOverLifetime;
                grow.enabled = true;
                grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.4f));
                var spin = marchers.rotationOverLifetime;
                spin.enabled = true;
                spin.z = new ParticleSystem.MinMaxCurve(Mathf.PI);

                // The still runway lights along the lanes, and a glow at the far end of each.
                var runway = Lights(root, "Runway Lights", light, 160);
                var ends = Lights(root, "Lane Ends", orb, 16);

                // Embers rising off the floor all over the road.
                var embers = new GameObject("Embers").AddComponent<ParticleSystem>();
                embers.transform.SetParent(root.transform, false);
                embers.transform.localPosition = new Vector3(0f, 0.02f, 3.8f);
                embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = embers.main;
                main.loop = true;
                main.playOnAwake = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3.2f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
                main.gravityModifier = -0.04f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 100;
                var emission = embers.emission;
                emission.rateOverTime = 24f;
                var shape = embers.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(8.5f, 6.5f, 0.05f);
                shape.rotation = new Vector3(-90f, 0f, 0f);
                var noise = embers.noise;
                noise.enabled = true;
                noise.strength = 0.25f;
                noise.frequency = 0.6f;
                var burn = embers.colorOverLifetime;
                burn.enabled = true;
                var fire = new Gradient();
                fire.SetKeys(
                    new[] { new GradientColorKey(new Color(1f, 0.75f, 0.3f), 0f), new GradientColorKey(new Color(1f, 0.25f, 0.05f), 0.5f), new GradientColorKey(new Color(0.6f, 0.05f, 0.02f), 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
                burn.color = fire;
                var emberRenderer = embers.GetComponent<ParticleSystemRenderer>();
                emberRenderer.sharedMaterial = light;
                emberRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                // The danger ring round the player's feet.
                var danger = Flat(root, "Danger Ring", ring, 1.7f);
                danger.transform.localPosition = new Vector3(0f, 0.02f, 0f);

                var road = root.AddComponent<PaintBeesRoad>();
                var so = new SerializedObject(road);
                so.FindProperty("marchers").objectReferenceValue = marchers;
                so.FindProperty("runway").objectReferenceValue = runway;
                so.FindProperty("laneEnds").objectReferenceValue = ends;
                so.FindProperty("embers").objectReferenceValue = embers;
                so.FindProperty("dangerRing").objectReferenceValue = danger.GetComponent<Renderer>();
                so.FindProperty("laneMaterial").objectReferenceValue = lane;
                so.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, RoadPath).GetComponent<PaintBeesRoad>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // A particle system the road emits into itself (playing, with no emission of its own), in world space.
        private static ParticleSystem Lights(GameObject parent, string name, Material material, int most)
        {
            var system = new GameObject(name).AddComponent<ParticleSystem>();
            system.transform.SetParent(parent.transform, false);
            var main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = 2f;
            main.startSpeed = 0f;
            main.startSize = 0.1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = most;
            var emission = system.emission;
            emission.rateOverTime = 0f;
            var shape = system.shape;
            shape.enabled = false;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return system;
        }

        // The particle shader without fog: the road reaches past where the fog starts.
        private static Material NoFog(Material material)
        {
            var shader = Shader.Find(NoFogShader);
            if (shader != null && material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        // ---------- The round ----------

        private static string BuildRound(PaintBee bee, PaintBucket bucket, GameObject start, PaintBeesRoad road, Material drop, Material splat)
        {
            var filled = new List<string>();
            bool made = AssetDatabase.LoadAssetAtPath<GameObject>(RoundPath) == null;
            var root = made ? new GameObject("Paint_Bees_Round") : PrefabUtility.LoadPrefabContents(RoundPath);
            try
            {
                if (!root.TryGetComponent(out PaintBeesRound round)) round = root.AddComponent<PaintBeesRound>();
                var so = new SerializedObject(round);
                Fill(so, "stageSky", AssetDatabase.LoadAssetAtPath<GameObject>(SkyPath), filled);
                Fill(so, "marker", start, filled);
                Fill(so, "bucket", bucket, filled);
                Fill(so, "bee", bee, filled);
                Fill(so, "road", road, filled);
                Fill(so, "song", AssetDatabase.LoadAssetAtPath<AudioClip>(SongPath), filled);
                Fill(so, "puffEffect", AssetDatabase.LoadAssetAtPath<GameObject>(PuffPath), filled);
                Fill(so, "burstMaterial", drop, filled);
                // The splat with droplets (it was the round drop before there was one).
                var splatField = so.FindProperty("splatMaterial");
                if (splatField.objectReferenceValue == null || splatField.objectReferenceValue == drop)
                {
                    splatField.objectReferenceValue = splat;
                    filled.Add("splatMaterial");
                }
                Fill(so, "winFireworks", AssetDatabase.LoadAssetAtPath<GameObject>(FireworkPath), filled);
                Fill(so, "onBeatFlash", AssetDatabase.LoadAssetAtPath<GameObject>(FlashPath), filled);
                Fill(so, "font", AssetDatabase.LoadAssetAtPath<Object>(FontPath), filled);
                Fill(so, "bucketSound", Clip("magic_hat_spit_card"), filled);
                Fill(so, "boingSound", Clip("bone_bonk_1"), filled);
                Fill(so, "stingSound", Clip("get_biten_1"), filled);
                Fill(so, "tickSound", AssetDatabase.LoadAssetAtPath<AudioClip>(TickPath), filled);
                Fill(so, "goSound", Clip("whoosh"), filled);
                Fill(so, "rainbowSound", Clip("A_magical_glowing"), filled);
                Fill(so, "perfectSound", Clip("fairy_yippy"), filled);
                FillList(so, "dipSounds", new Object[] { Clip("bubble_pop_1"), Clip("pumpkin_face_splat") }, filled);
                FillList(so, "swatSounds", new Object[] { Clip("Cartoon_bug_squash_1"), Clip("Cartoon_bug_squash_2"), Clip("Cartoon_bug_squash_3") }, filled);
                FillList(so, "candies", Enumerable.Range(1, 8).Select(i => (Object)AssetDatabase.LoadAssetAtPath<GameObject>(string.Format(CandyPath, i))).ToArray(), filled);
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, RoundPath);
            }
            finally
            {
                if (made) Object.DestroyImmediate(root);
                else PrefabUtility.UnloadPrefabContents(root);
            }
            return (made ? "made the round" : "the round was there") + (filled.Count > 0 ? ", filled " + string.Join(", ", filled) : ", nothing to fill");
        }

        // ---------- The scene ----------

        [MenuItem("Tools/429 Game/Bonus/Use Paint Bees In Playroom")]
        public static void UseInPlayroom()
        {
            var round = AssetDatabase.LoadAssetAtPath<GameObject>(RoundPath);
            if (round == null)
            {
                SetUp();
                round = AssetDatabase.LoadAssetAtPath<GameObject>(RoundPath);
            }
            Debug.Log("[Paint Bees] " + string.Join("; ", Hook(round)));
        }

        /// <summary>"Bonus Round PR" plays the round; the Playroom stage makes no pumpkin chest. Returns what it did.</summary>
        public static IEnumerable<string> Hook(GameObject round)
        {
            var gs = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(f => f.FsmName == "GAMESTAGES" && f.gameObject.scene.IsValid());
            if (gs == null) { yield return "No GAMESTAGES in the open scene: open 2 Game Scene and run it again."; yield break; }
            if (!gs.Fsm.Initialized) gs.Fsm.Init(gs);
            var state = gs.Fsm.GetState(BonusState);
            if (state == null) { yield return $"GAMESTAGES has no '{BonusState}': run Set Up Nesting Pumpkin Bonus first."; yield break; }
            state.LoadActions();
            if (state.ActionData.ActionCount != state.Actions.Length) { yield return $"'{BonusState}' didn't load all its actions; left alone."; yield break; }
            var play = state.Actions.OfType<PlayBonusRound>().FirstOrDefault();
            if (play == null) { yield return $"'{BonusState}' has no Play Bonus Round."; yield break; }
            Undo.RecordObject(gs, "Paint Bees Bonus");
            if (play.round == null || play.round.Value != round)
            {
                play.round = new FsmGameObject { Value = round };
                state.SaveActions();
                yield return $"'{BonusState}' plays {round.name}";
            }
            else yield return $"'{BonusState}' already plays {round.name}";

            var stage = gs.transform.Find(PlayroomStageName);
            var chest = stage != null ? stage.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == ChestSpotName) : null;
            if (chest == null) yield return $"No {ChestSpotName} under {PlayroomStageName}";
            else if (chest.gameObject.activeSelf)
            {
                Undo.RecordObject(chest.gameObject, "Paint Bees Bonus");
                chest.gameObject.SetActive(false);
                yield return $"{ChestSpotName} off (no pumpkin chest in the Playroom's bonus)";
            }
            EditorUtility.SetDirty(gs);
            EditorSceneManager.MarkSceneDirty(gs.gameObject.scene);
            yield return "Save the scene to keep it.";
        }

        // ---------- Helpers ----------

        private static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + name + ".wav");

        // A flat quad lying on the floor (facing up), this many metres across, with no collider.
        private static GameObject Flat(GameObject parent, string name, Material material, float size)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent.transform, false);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = Vector3.one * size;
            var r = quad.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return quad;
        }

        // Its meshes' bounds in Space (null: the world). From the meshes themselves: a renderer's bounds lag behind a move
        // or a resize made just before (the first bucket came out 30 cm off its paint).
        private static Bounds Bounds(GameObject go, Transform space)
        {
            var b = new Bounds();
            bool first = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mesh = r is SkinnedMeshRenderer skinned ? skinned.sharedMesh : r.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
                if (mesh == null) continue;
                var mb = mesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var world = r.transform.TransformPoint(corner);
                    var local = space != null ? space.InverseTransformPoint(world) : world;
                    if (first) { b = new Bounds(local, Vector3.zero); first = false; }
                    else b.Encapsulate(local);
                }
            }
            return b;
        }

        private static void Fill(SerializedObject so, string field, Object value, List<string> filled)
        {
            var p = so.FindProperty(field);
            if (p == null || p.objectReferenceValue != null || value == null) return;
            p.objectReferenceValue = value;
            filled.Add(field);
        }

        private static void FillList(SerializedObject so, string field, Object[] values, List<string> filled)
        {
            var p = so.FindProperty(field);
            if (p == null || p.arraySize > 0) return;
            var real = values.Where(v => v != null).ToArray();
            p.arraySize = real.Length;
            for (int i = 0; i < real.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = real[i];
            if (real.Length > 0) filled.Add($"{field} ({real.Length})");
        }
    }
}
