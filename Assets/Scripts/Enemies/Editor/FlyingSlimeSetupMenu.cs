using System.Collections.Generic;
using System.IO;
using Game.Bonus;
using Game.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Enemies > Set Up Flying Slime (user 2026-11-05: a new enemy, the flying paint slime, no rig).
    /// Builds Prefabs/Enemies/Flying_Slime from the model Models/Enemies/Flying_Slime.fbx (exported from
    /// Blender/Flying_Slime.blend): Flying_Slime (Flying Slime: all its moving) > Body Squash > Body Turn > Model, its
    /// materials (Materials/Flying_Slime, made once: change them freely), its drips (paint drops forming under it, hanging,
    /// falling, splatting on the floor), its pop (blobs and drops, the bursting skin) and the floor plane the splats land on.
    /// Run it again after a new export of the model (the parts must keep their names: Slime_Body, Slime_Wing_L/R,
    /// Slime_Stem, Slime_Eye_L/R); it remakes the prefab (changes made to the prefab itself are lost; those on its
    /// Flying Slime component are kept).
    /// </summary>
    public static class FlyingSlimeSetupMenu
    {
        private const string ModelPath = "Assets/Models/Enemies/Flying_Slime.fbx";
        private const string PrefabPath = "Assets/Prefabs/Enemies/Flying_Slime.prefab";
        // Special Stage Type 2's flyers (user 2026-11-05: "put the slimes into special stage type 2"): a variant of the
        // slime flown by the paint bee's brain, with the bee's aura and trail; the round spawns it instead of Paint_Bee.
        private const string PaintSlimePath = "Assets/Prefabs/Bonus/Paint_Slime.prefab";
        private const string PaintBeePath = "Assets/Prefabs/Bonus/Paint_Bee.prefab";
        private const string PaintRoundPath = "Assets/Prefabs/Bases/Paint_Bees_Round.prefab";
        // Its size in the stage (the bee's hit size: radius 0.16 x 0.75 = 0.12 m).
        private const float PaintSlimeScale = 0.75f;
        private const float PaintSlimeRadius = 0.16f;
        private const string MaterialFolder = "Assets/Materials/Flying_Slime/";
        private const string DropMaterialPath = "Assets/Materials/Paint_Bees/Paint_Drop_Mat.mat";
        // The user's textured remodel (2026-11-05): one skin for every part (429/Slime Lit), its paint texture's alpha
        // saying what takes the slime's colour.
        private const string TextureFolder = "Assets/M_Textures/Flying_Slime/";
        private const string PaintTexturePath = TextureFolder + "Flying_Slime_Paint.png";
        private const string GlowTexturePath = TextureFolder + "Flying_Slime_Emission.png";
        private const string SlimeShaderName = "429/Slime Lit";
        private const string SplatMaterialPath = "Assets/Materials/Paint_Bees/Paint_Floor_Splat_Mat.mat";
        private static readonly string[] PopSoundPaths = { "Assets/Audio/SFX/pumpkin_explode_1.wav", "Assets/Audio/SFX/pumpkin_explode_2.wav" };

        // Drops: seconds they live (the floor ends them sooner) and hang before they fall.
        private const float DropSeconds = 2.2f;
        private const float HangSeconds = 0.45f;

        [MenuItem("Tools/429 Game/Enemies/Set Up Flying Slime")]
        public static void SetUp()
        {
            var notes = Build();
            Debug.Log("[Flying Slime] " + string.Join("\n", notes));
            EditorUtility.DisplayDialog("Set Up Flying Slime", string.Join("\n", notes), "OK");
        }

        public static List<string> Build()
        {
            var notes = new List<string>();
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == PrefabPath)
            {
                notes.Add("Close " + PrefabPath + " in Prefab Mode first. Nothing changed.");
                return notes;
            }
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelAsset == null)
            {
                notes.Add("No model at " + ModelPath + ". Nothing changed.");
                return notes;
            }
            FixImporter(notes);
            modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);

            Directory.CreateDirectory(MaterialFolder);
            var bodyMat = LitMaterial("Flying_Slime_Body_Mat", Color.white, 0.86f, Color.black);
            var insideMat = LitMaterial("Flying_Slime_Inside_Mat", Hex("#16050C"), 0.2f, null);
            var wingMat = LitMaterial("Flying_Slime_Wing_Mat", Color.white, 0.75f, Color.black);
            var stemMat = LitMaterial("Flying_Slime_Stem_Mat", Hex("#B88A2C"), 0.5f, null);
            var eyeMat = LitMaterial("Flying_Slime_Eye_Mat", Hex("#FF8A1C"), 0.6f, new Color(2.2f, 0.92f, 0.13f));
            var rainbowMat = RainbowMaterial();
            var skinMat = SkinMaterial(notes);
            var dropMat = AssetDatabase.LoadAssetAtPath<Material>(DropMaterialPath);
            var splatMat = AssetDatabase.LoadAssetAtPath<Material>(SplatMaterialPath);

            // What the old prefab's Flying Slime was set to (its colour, speeds...), kept.
            string kept = null;
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (old != null && old.TryGetComponent(out FlyingSlime oldSlime)) kept = EditorJsonUtility.ToJson(oldSlime);

            var build = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("Flying_Slime");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, build);
                var squash = new GameObject("Body Squash").transform;
                squash.SetParent(root.transform, false);
                var turn = new GameObject("Body Turn").transform;
                turn.SetParent(squash, false);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, build);
                model.name = "Model";
                model.transform.SetParent(turn, false);

                Transform Find(string part)
                {
                    foreach (var t in model.GetComponentsInChildren<Transform>(true))
                        if (t.name == part) return t;
                    notes.Add("The model has no " + part + ".");
                    return null;
                }
                var body = Find("Slime_Body");
                var wingL = Find("Slime_Wing_L");
                var wingR = Find("Slime_Wing_R");
                var stem = Find("Slime_Stem");
                var eyeL = Find("Slime_Eye_L");
                var eyeR = Find("Slime_Eye_R");
                // The textured model (the body in one piece, no separate inside): the one skin on every part.
                var bodyFilter = body != null ? body.GetComponent<MeshFilter>() : null;
                bool textured = skinMat != null && bodyFilter != null && bodyFilter.sharedMesh != null && bodyFilter.sharedMesh.subMeshCount == 1;
                notes.Add(textured ? "Textured model: every part uses " + skinMat.name + "." : "Untextured model: the plain materials.");
                foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (textured)
                    {
                        r.sharedMaterial = skinMat;
                        if (r.name.StartsWith("Slime_Eye")) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                    else if (r.name == "Slime_Body") r.sharedMaterials = new[] { bodyMat, insideMat };
                    else if (r.name.StartsWith("Slime_Wing")) r.sharedMaterial = wingMat;
                    else if (r.name.StartsWith("Slime_Stem")) r.sharedMaterial = stemMat;
                    else if (r.name.StartsWith("Slime_Eye"))
                    {
                        r.sharedMaterial = eyeMat;
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                    r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
                }

                var floor = new GameObject("Floor").transform;
                floor.SetParent(root.transform, false);
                floor.localPosition = Vector3.down * 1.5f;

                var drips = MakeDrips(root.transform, dropMat);
                var blobs = MakeBlobs(root.transform, dropMat);
                var shell = MakeShell(root.transform, dropMat);
                if (splatMat != null)
                {
                    PaintSplats.Add(drips, floor, splatMat, 0.75f, new Vector2(0.035f, 0.07f), 3.5f);
                    PaintSplats.Add(blobs, floor, splatMat, 0.8f, new Vector2(0.05f, 0.12f), 5f);
                }

                var slime = root.AddComponent<FlyingSlime>();
                if (kept != null) EditorJsonUtility.FromJsonOverwrite(kept, slime);
                var so = new SerializedObject(slime);
                so.FindProperty("squash").objectReferenceValue = squash;
                so.FindProperty("turn").objectReferenceValue = turn;
                so.FindProperty("model").objectReferenceValue = model.transform;
                so.FindProperty("body").objectReferenceValue = body != null ? body.GetComponent<Renderer>() : null;
                so.FindProperty("wingLeft").objectReferenceValue = wingL;
                so.FindProperty("wingRight").objectReferenceValue = wingR;
                so.FindProperty("stem").objectReferenceValue = stem;
                so.FindProperty("eyeLeft").objectReferenceValue = eyeL;
                so.FindProperty("eyeRight").objectReferenceValue = eyeR;
                so.FindProperty("drips").objectReferenceValue = drips;
                so.FindProperty("popBlobs").objectReferenceValue = blobs;
                so.FindProperty("popShell").objectReferenceValue = shell;
                so.FindProperty("floor").objectReferenceValue = floor;
                so.FindProperty("rainbowMaterial").objectReferenceValue = rainbowMat;
                if (textured)
                {
                    // The glow map has the eyes' own colours: the glow colour is just how bright (and it pulses).
                    so.FindProperty("eyeGlow").colorValue = new Color(1.6f, 1.6f, 1.6f, 1f);
                    // The stem is cut out of the head (its foot capped): a smaller wobble keeps the cut hidden.
                    var stemMost = so.FindProperty("stemMost");
                    if (stemMost.floatValue > 16f) stemMost.floatValue = 16f;
                }
                var sounds = so.FindProperty("popSounds");
                if (sounds.arraySize == 0)
                {
                    foreach (var path in PopSoundPaths)
                    {
                        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                        if (clip == null) continue;
                        sounds.arraySize++;
                        sounds.GetArrayElementAtIndex(sounds.arraySize - 1).objectReferenceValue = clip;
                    }
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
                notes.Add(saved ? "Made " + PrefabPath + "." : "Couldn't save " + PrefabPath + ".");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(build);
            }
            AssetDatabase.SaveAssets();
            notes.AddRange(BuildPaintSlime());
            AssetDatabase.SaveAssets();
            return notes;
        }

        /// <summary>
        /// Paint_Slime: a variant of Flying_Slime (so it follows the slime's changes) with the paint bee's brain (Paint
        /// Bee: flies in on the beat, hovers, dives, bounces, stings, is swatted) and its aura and trail, at the stage's
        /// size; and the Paint Bees round set to spawn it.
        /// </summary>
        public static List<string> BuildPaintSlime()
        {
            var notes = new List<string>();
            var slimeAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var beeAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PaintBeePath);
            if (slimeAsset == null || beeAsset == null || beeAsset.GetComponent<PaintBee>() == null)
            {
                notes.Add("No Flying_Slime or Paint_Bee prefab: no Paint_Slime.");
                return notes;
            }
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && (stage.assetPath == PaintSlimePath || stage.assetPath == PaintRoundPath))
            {
                notes.Add("Close " + stage.assetPath + " in Prefab Mode first. Paint_Slime not made.");
                return notes;
            }
            var build = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(slimeAsset, build);
                root.name = "Paint_Slime";
                root.transform.localScale = Vector3.one * PaintSlimeScale;
                // The bee's aura and trail, moved over from a copy of the bee.
                var beeCopy = (GameObject)Object.Instantiate(beeAsset);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(beeCopy, build);
                var beeBrain = beeCopy.GetComponent<PaintBee>();
                var beeSo = new SerializedObject(beeBrain);
                var auras = new List<Object>();
                var auraList = beeSo.FindProperty("auras");
                for (int i = 0; i < auraList.arraySize; i++)
                {
                    var aura = auraList.GetArrayElementAtIndex(i).objectReferenceValue as ParticleSystem;
                    if (aura == null) continue;
                    aura.transform.SetParent(root.transform, false);
                    auras.Add(aura);
                }
                var trail = beeSo.FindProperty("trail").objectReferenceValue as TrailRenderer;
                if (trail != null) trail.transform.SetParent(root.transform, false);
                // Its brain: the bee's settings (beat throb, how long it must be seen...), no bee model, buzz or rainbow look.
                var brain = root.AddComponent<PaintBee>();
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(beeBrain), brain);
                var so = new SerializedObject(brain);
                so.FindProperty("animator").objectReferenceValue = null;
                so.FindProperty("looks").arraySize = 0;
                so.FindProperty("buzz").objectReferenceValue = null;
                so.FindProperty("rainbowLook").objectReferenceValue = null;
                so.FindProperty("radius").floatValue = PaintSlimeRadius;
                so.FindProperty("trail").objectReferenceValue = trail;
                var list = so.FindProperty("auras");
                list.arraySize = auras.Count;
                for (int i = 0; i < auras.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = auras[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                Object.DestroyImmediate(beeCopy);
                PrefabUtility.SaveAsPrefabAsset(root, PaintSlimePath, out bool saved);
                notes.Add(saved ? "Made " + PaintSlimePath + " (a variant of Flying_Slime)." : "Couldn't save " + PaintSlimePath + ".");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(build);
            }

            // The round spawns it.
            var made = AssetDatabase.LoadAssetAtPath<GameObject>(PaintSlimePath);
            if (made != null && AssetDatabase.LoadAssetAtPath<GameObject>(PaintRoundPath) != null)
            {
                var round = PrefabUtility.LoadPrefabContents(PaintRoundPath);
                try
                {
                    var paintRound = round.GetComponent<PaintBeesRound>();
                    if (paintRound != null)
                    {
                        var so = new SerializedObject(paintRound);
                        var bee = so.FindProperty("bee");
                        if (bee.objectReferenceValue != made.GetComponent<PaintBee>())
                        {
                            bee.objectReferenceValue = made.GetComponent<PaintBee>();
                            so.ApplyModifiedPropertiesWithoutUndo();
                            PrefabUtility.SaveAsPrefabAsset(round, PaintRoundPath);
                            notes.Add("Paint_Bees_Round now spawns Paint_Slime.");
                        }
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(round);
                }
            }
            return notes;
        }

        // The model: no animation, cameras or lights; its own materials aren't used (the prefab's are).
        private static void FixImporter(List<string> notes)
        {
            if (!(AssetImporter.GetAtPath(ModelPath) is ModelImporter importer)) return;
            bool changed = false;
            if (importer.importAnimation) { importer.importAnimation = false; changed = true; }
            if (importer.importCameras) { importer.importCameras = false; changed = true; }
            if (importer.importLights) { importer.importLights = false; changed = true; }
            if (importer.materialImportMode != ModelImporterMaterialImportMode.None) { importer.materialImportMode = ModelImporterMaterialImportMode.None; changed = true; }
            if (!changed) return;
            importer.SaveAndReimport();
            notes.Add("Model import: no animation, cameras, lights or materials.");
        }

        private static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        // Made once (then left as the user sets it). Emission: none (null), or on with this colour (the slime sets the
        // body's and wings' own at runtime).
        private static Material LitMaterial(string name, Color colour, float smoothness, Color? emission)
        {
            string path = MaterialFolder + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", colour);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // The textured model's skin, made once (then left as the user sets it); its textures are set if missing.
        private static Material SkinMaterial(List<string> notes)
        {
            var shader = Shader.Find(SlimeShaderName);
            var paint = AssetDatabase.LoadAssetAtPath<Texture2D>(PaintTexturePath);
            if (shader == null || paint == null) return null;
            // The paint's alpha is a mask (not see-through); both keep their mip maps.
            if (AssetImporter.GetAtPath(PaintTexturePath) is TextureImporter paintImporter &&
                (paintImporter.alphaIsTransparency || paintImporter.alphaSource != TextureImporterAlphaSource.FromInput || !paintImporter.sRGBTexture))
            {
                paintImporter.alphaIsTransparency = false;
                paintImporter.alphaSource = TextureImporterAlphaSource.FromInput;
                paintImporter.sRGBTexture = true;
                paintImporter.SaveAndReimport();
                notes.Add("Paint texture: alpha = mask.");
            }
            string path = MaterialFolder + "Flying_Slime_Mat.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                m.SetColor("_BaseColor", new Color(0.89f, 0.15f, 0.18f));
                m.SetFloat("_Smoothness", 0.86f);
                m.SetColor("_EmissionColor", new Color(1.6f, 1.6f, 1.6f));
                m.SetFloat("_PaintGlow", 0.14f);
                AssetDatabase.CreateAsset(m, path);
            }
            if (m.GetTexture("_BaseMap") == null) m.SetTexture("_BaseMap", paint);
            if (m.GetTexture("_EmissionMap") == null) m.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(GlowTexturePath));
            EditorUtility.SetDirty(m);
            return m;
        }

        // The rainbow slime's skin: wavy rainbow stripes running round the body (its UVs: round it, then up it), made once.
        private static Material RainbowMaterial()
        {
            string texPath = MaterialFolder + "Flying_Slime_Rainbow.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(texPath) == null)
            {
                const int w = 256, h = 128;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w, v = y / (float)(h - 1);
                    float hueAt = Mathf.Repeat(u + 0.1f * Mathf.Sin(2f * Mathf.PI * (v * 2f + u)), 1f);
                    float value = Mathf.Lerp(0.88f, 1f, Mathf.SmoothStep(0f, 1f, v * 1.6f));
                    tex.SetPixel(x, y, Color.HSVToRGB(hueAt, 0.8f, value));
                }
                tex.Apply();
                File.WriteAllBytes(texPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(texPath);
                if (AssetImporter.GetAtPath(texPath) is TextureImporter importer)
                {
                    importer.wrapMode = TextureWrapMode.Repeat;
                    importer.mipmapEnabled = true;
                    importer.SaveAndReimport();
                }
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            string path = MaterialFolder + "Flying_Slime_Rainbow_Mat.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BaseMap", texture);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", 0.86f);
            m.EnableKeyword("_EMISSION");
            m.SetTexture("_EmissionMap", texture);
            m.SetColor("_EmissionColor", Color.black);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // A world-space system the slime emits into by itself (no emission, no shape).
        private static ParticleSystem NewSystem(string name, Transform parent, Material material, int most)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startSpeed = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.maxParticles = most;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return system;
        }

        private static AnimationCurve Linear(params Keyframe[] keys)
        {
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            }
            return curve;
        }

        // Drops: they grow where they form, hang there (moving along with the slime) for Hang Seconds, then fall,
        // stretching as they speed up.
        private static ParticleSystem MakeDrips(Transform parent, Material material)
        {
            var system = NewSystem("Drips", parent, material, 40);
            float hang = HangSeconds / DropSeconds;
            var main = system.main;
            main.startLifetime = DropSeconds;
            main.gravityModifier = 0f;
            main.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Transform;
            var flat = Linear(new Keyframe(0f, 0f), new Keyframe(1f, 0f));
            var force = system.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.World;
            force.x = new ParticleSystem.MinMaxCurve(0f, flat);
            force.y = new ParticleSystem.MinMaxCurve(-9.81f * 1.2f, Linear(new Keyframe(0f, 0f), new Keyframe(hang, 0f), new Keyframe(hang + 0.02f, 1f), new Keyframe(1f, 1f)));
            force.z = new ParticleSystem.MinMaxCurve(0f, flat);
            var follow = system.inheritVelocity;
            follow.enabled = true;
            follow.mode = ParticleSystemInheritVelocityMode.Current;
            follow.curve = new ParticleSystem.MinMaxCurve(1f, Linear(new Keyframe(0f, 1f), new Keyframe(hang, 1f), new Keyframe(hang + 0.02f, 0f), new Keyframe(1f, 0f)));
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, Linear(new Keyframe(0f, 0.2f), new Keyframe(hang * 0.85f, 1f), new Keyframe(1f, 1f)));
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.035f;
            renderer.lengthScale = 1.1f;
            return system;
        }

        // The pop's blobs and drops (and the few a bonk shakes off): flung out, falling, splatting.
        private static ParticleSystem MakeBlobs(Transform parent, Material material)
        {
            var system = NewSystem("Pop Blobs", parent, material, 120);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.4f);
            main.gravityModifier = 1.3f;
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.8f, 0.85f), new Keyframe(1f, 0f)));
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.045f;
            renderer.lengthScale = 1f;
            return system;
        }

        // The paint skin bursting: one quick round splash that opens out and fades.
        private static ParticleSystem MakeShell(Transform parent, Material material)
        {
            var system = NewSystem("Pop Shell", parent, material, 4);
            var main = system.main;
            main.startLifetime = 0.16f;
            main.gravityModifier = 0f;
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.35f, 1.15f), new Keyframe(1f, 1.3f)));
            var fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.5f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            system.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.Billboard;
            return system;
        }
    }
}
