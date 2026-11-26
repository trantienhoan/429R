using System.Collections.Generic;
using System.Linq;
using Game.Story;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Story.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Story > Set Up Fairy Jar At The Boss (user 2026-11-26, picked "Fairy caged at the boss": show
    /// where the kidnapped Fairy is, shut in a jar beside the final boss, her green light flowing into it; beating the
    /// boss frees her). Re-runnable; Undo Fairy Jar At The Boss takes it all off.
    /// 1. Models/Story/Story_Fairy_Jar.asset: the lair jar's mesh (Lair_Glow_Jar) split in two by its own texture: the
    ///    glass (painted green) and the cork, so the glass can be see-through (Materials/Story/Story_Fairy_Jar_Glass) and
    ///    the cork keeps the user's texture (Lair_Glow_Jar_Mat).
    /// 2. Prefabs/Story/Story_Fairy_Jar.prefab: the jar (3.6x the lair jar, about 1 m), her look-only copy inside
    ///    (Fairy_Girl with Fairy_Controller, playing "call", a little smaller), the purple lock round it (Fairy_Lock_Tower,
    ///    watching "Lock"), a green light, the stream of sparks and the glowing arc to the boss, her calls (the user's
    ///    FairyHelp / FairyCallHelp), the lair jar's bottle pieces + glass_break_3 for the shatter. Story Fairy Jar runs it.
    /// 3. Boss_Area.prefab gets "Fairy Jar Spot" (it undoes the root's uneven scale) with the jar 1.5 m past the arena's
    ///    north edge (its purple bars just clear of the glowing line; the side across from where the player comes in),
    ///    floating 0.8 m up.
    /// 4. GAMESTAGES "wait" (the boss is beaten) gets Story Fairy Jar Break first. THE SCENE MUST BE SAVED.
    /// </summary>
    public static class StoryFairyJarMenu
    {
        private const string Tag = "[Fairy Jar]";
        private const string SourceJarPrefab = "Assets/Prefabs/Lair/Lair_Glow_Jar.prefab";
        private const string SourceJarTexture = "Assets/M_Textures/Lair/Lair_Glow_Jar.png";
        private const string FairyPrefab = "Assets/Prefabs/Player/Fairy.prefab";
        private const string FairyModel = "Assets/Models/Fairy_Girl.fbx";
        private const string LockTowerPrefab = "Assets/Prefabs/Effects/Fairy_Lock_Tower.prefab";
        private const string GlowMaterial = "Assets/Data/Effects/Danger Glow.mat";
        private const string BeamMaterial = "Assets/Data/Effects/Boss Arena Line.mat";
        private const string ShardsPrefab = "Assets/Prefabs/Items/Broken_Stuffs/Empty_Bottle_4_Drop.prefab";
        private const string BreakSound = "Assets/Audio/SFX/glass_break_3.wav";
        private static readonly string[] CallSounds = { "Assets/Audio/SFX/FairyHelp.wav", "Assets/Audio/SFX/FairyCallHelp.wav" };
        private const string MeshPath = "Assets/Models/Story/Story_Fairy_Jar.asset";
        private const string GlassPath = "Assets/Materials/Story/Story_Fairy_Jar_Glass.mat";
        private const string JarPrefabPath = "Assets/Prefabs/Story/Story_Fairy_Jar.prefab";
        private const string ArenaPath = "Assets/Prefabs/Bases/Boss_Area.prefab";
        private const string SpotName = "Fairy Jar Spot";
        private const string WaitState = "wait";
        private const float JarScale = 3.6f;
        // Past the arena's north edge (z 7.67), floating.
        private static readonly Vector3 SpotPosition = new Vector3(0f, 0.8f, 9.2f);

        [MenuItem("Tools/429 Game/Story/Set Up Fairy Jar At The Boss")]
        public static void SetUp()
        {
            var done = new List<string>();
            var sourceJar = AssetDatabase.LoadAssetAtPath<GameObject>(SourceJarPrefab);
            var fairyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FairyPrefab);
            var fairyModel = AssetDatabase.LoadAssetAtPath<GameObject>(FairyModel);
            var lockTower = AssetDatabase.LoadAssetAtPath<GameObject>(LockTowerPrefab);
            var glow = AssetDatabase.LoadAssetAtPath<Material>(GlowMaterial);
            if (sourceJar == null || fairyPrefab == null || fairyModel == null || lockTower == null || glow == null)
            {
                Debug.LogError($"{Tag} Missing {SourceJarPrefab}, {FairyPrefab}, {FairyModel}, {LockTowerPrefab} or {GlowMaterial}; nothing changed.");
                return;
            }
            var stages = FindStages();
            if (stages == null) return;
            var wait = Loaded(stages.Fsm, WaitState);
            if (wait == null)
            {
                Debug.LogError($"{Tag} GAMESTAGES has no '{WaitState}' (or an action there can't be read); nothing changed.");
                return;
            }

            var mesh = SplitJarMesh(sourceJar);
            var glass = GlassMaterial();
            BuildJarPrefab(mesh, glass, sourceJar, fairyPrefab, fairyModel, lockTower, glow);
            done.Add("jar prefab built");
            PlaceInArena();
            done.Add($"jar placed on Boss_Area at {SpotPosition}");

            if (!wait.Actions.OfType<StoryFairyJarBreak>().Any())
            {
                var a = new StoryFairyJarBreak();
                a.Reset();
                wait.Actions = new FsmStateAction[] { a }.Concat(wait.Actions).ToArray();
                wait.SaveActions();
                EditorUtility.SetDirty(stages);
                EditorSceneManager.MarkSceneDirty(stages.gameObject.scene);
                done.Add($"Story Fairy Jar Break first in GAMESTAGES '{WaitState}' (SAVE 2 Game Scene)");
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} {string.Join("; ", done)}.");
        }

        [MenuItem("Tools/429 Game/Story/Undo Fairy Jar At The Boss")]
        public static void Undo()
        {
            var done = new List<string>();
            var root = PrefabUtility.LoadPrefabContents(ArenaPath);
            try
            {
                var spot = root.transform.Find(SpotName);
                if (spot != null)
                {
                    Object.DestroyImmediate(spot.gameObject);
                    PrefabUtility.SaveAsPrefabAsset(root, ArenaPath);
                    done.Add("jar taken off Boss_Area");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            var stages = FindStages();
            var wait = stages != null ? Loaded(stages.Fsm, WaitState) : null;
            if (wait != null && wait.Actions.OfType<StoryFairyJarBreak>().Any())
            {
                wait.Actions = wait.Actions.Where(a => !(a is StoryFairyJarBreak)).ToArray();
                wait.SaveActions();
                EditorUtility.SetDirty(stages);
                EditorSceneManager.MarkSceneDirty(stages.gameObject.scene);
                done.Add($"Story Fairy Jar Break out of '{WaitState}' (SAVE 2 Game Scene)");
            }
            Debug.Log(done.Count > 0 ? $"{Tag} {string.Join("; ", done)}. The jar prefab and its mesh/material are kept." : $"{Tag} nothing to undo.");
        }

        [MenuItem("Tools/429 Game/Story/Set Up Fairy Jar At The Boss", true)]
        [MenuItem("Tools/429 Game/Story/Undo Fairy Jar At The Boss", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // The lair jar's mesh with two parts: the cork is the light-brown cap on top (the top eighth of the jar where its
        // texture is light), everything else is glass.
        private static Mesh SplitJarMesh(GameObject sourceJar)
        {
            var source = sourceJar.GetComponent<MeshFilter>().sharedMesh;
            var picture = new Texture2D(2, 2);
            picture.LoadImage(System.IO.File.ReadAllBytes(SourceJarTexture));
            var uv = source.uv;
            var vertices = source.vertices;
            var tris = source.triangles;
            float bottom = source.bounds.min.y, height = Mathf.Max(0.001f, source.bounds.size.y);
            var glassTris = new List<int>();
            var corkTris = new List<int>();
            for (int i = 0; i < tris.Length; i += 3)
            {
                var c = (uv[tris[i]] + uv[tris[i + 1]] + uv[tris[i + 2]]) / 3f;
                float y = ((vertices[tris[i]].y + vertices[tris[i + 1]].y + vertices[tris[i + 2]].y) / 3f - bottom) / height;
                var colour = picture.GetPixelBilinear(c.x, c.y);
                bool isCork = y > 0.87f && colour.r > 0.5f;
                (isCork ? corkTris : glassTris).AddRange(new[] { tris[i], tris[i + 1], tris[i + 2] });
            }
            Object.DestroyImmediate(picture);

            var mesh = Object.Instantiate(source);
            mesh.name = "Story_Fairy_Jar";
            mesh.subMeshCount = 2;
            mesh.SetTriangles(glassTris, 0);
            mesh.SetTriangles(corkTris, 1);
            mesh.RecalculateBounds();
            EnsureFolder("Assets/Models/Story");
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                mesh = existing;
            }
            else AssetDatabase.CreateAsset(mesh, MeshPath);
            Debug.Log($"{Tag} jar mesh: {glassTris.Count / 3} glass triangles, {corkTris.Count / 3} cork triangles.");
            return mesh;
        }

        // Clear, faintly green glass that glows a little, so the jar shows in the dark and she shows inside.
        private static Material GlassMaterial()
        {
            EnsureFolder("Assets/Materials/Story");
            var glass = AssetDatabase.LoadAssetAtPath<Material>(GlassPath);
            if (glass == null)
            {
                glass = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Story_Fairy_Jar_Glass" };
                AssetDatabase.CreateAsset(glass, GlassPath);
            }
            glass.SetFloat("_Surface", 1f);
            glass.SetFloat("_Blend", 0f);
            glass.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            glass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            glass.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            glass.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            glass.SetFloat("_ZWrite", 0f);
            glass.SetFloat("_AlphaClip", 0f);
            glass.SetFloat("_Cull", (float)CullMode.Back);
            glass.SetFloat("_Smoothness", 0.9f);
            glass.SetFloat("_Metallic", 0f);
            glass.SetColor("_BaseColor", new Color(0.6f, 1f, 0.65f, 0.25f));
            glass.SetColor("_EmissionColor", new Color(0.25f, 1.1f, 0.35f));
            glass.EnableKeyword("_EMISSION");
            glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            glass.DisableKeyword("_ALPHATEST_ON");
            glass.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            glass.renderQueue = (int)RenderQueue.Transparent;
            glass.SetOverrideTag("RenderType", "Transparent");
            glass.SetShaderPassEnabled("DepthOnly", false);
            glass.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(glass);
            return glass;
        }

        private static void BuildJarPrefab(Mesh mesh, Material glass, GameObject sourceJar, GameObject fairyPrefab, GameObject fairyModel, GameObject lockTower, Material glow)
        {
            EnsureFolder("Assets/Prefabs/Story");
            var root = new GameObject("Story_Fairy_Jar");
            try
            {
                var jarScript = root.AddComponent<StoryFairyJar>();
                var voice = root.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 1f;
                voice.minDistance = 3f;
                voice.maxDistance = 35f;
                voice.rolloffMode = AudioRolloffMode.Logarithmic;
                voice.dopplerLevel = 0f;

                var lockGo = new GameObject("Lock");
                lockGo.transform.SetParent(root.transform, false);

                var tower = (GameObject)PrefabUtility.InstantiatePrefab(lockTower, root.transform);
                tower.name = "Fairy_Lock_Tower";
                tower.transform.localPosition = Vector3.zero;
                var towerScript = tower.GetComponent<Game.VFX.FairyLockTower>();
                var so = new SerializedObject(towerScript);
                so.FindProperty("cage").objectReferenceValue = lockGo;
                so.ApplyModifiedPropertiesWithoutUndo();

                var jarGo = new GameObject("Jar");
                jarGo.transform.SetParent(root.transform, false);
                jarGo.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                jarGo.transform.localScale = Vector3.one * JarScale;
                jarGo.AddComponent<MeshFilter>().sharedMesh = mesh;
                var jarRenderer = jarGo.AddComponent<MeshRenderer>();
                jarRenderer.sharedMaterials = new[] { glass, sourceJar.GetComponent<MeshRenderer>().sharedMaterial };
                jarRenderer.shadowCastingMode = ShadowCastingMode.Off;

                // Her look-only copy: the model, her material and her Animator (no FSMs, no colliders).
                var fairy = (GameObject)PrefabUtility.InstantiatePrefab(fairyModel, root.transform);
                fairy.name = "Fairy";
                fairy.transform.localPosition = new Vector3(0f, -0.25f, 0f);   // her call clip hovers her ~0.4 m up: this puts her in the jar's belly
                fairy.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // facing the arena (south)
                fairy.transform.localScale = Vector3.one * 0.85f;
                var fairyAnimator = fairyPrefab.GetComponent<Animator>();
                var animator = fairy.GetComponent<Animator>();
                if (animator == null) animator = fairy.AddComponent<Animator>();
                animator.runtimeAnimatorController = fairyAnimator.runtimeAnimatorController;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var skinSource = fairyPrefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
                foreach (var skin in fairy.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    skin.sharedMaterials = skinSource.sharedMaterials;
                    skin.updateWhenOffscreen = true;
                    skin.shadowCastingMode = ShadowCastingMode.Off;
                }

                var lightGo = new GameObject("Jar Light");
                lightGo.transform.SetParent(root.transform, false);
                lightGo.transform.localPosition = new Vector3(0f, 0.55f, 0f);
                var lamp = lightGo.AddComponent<Light>();
                lamp.type = LightType.Point;
                lamp.color = new Color(0.45f, 1f, 0.5f);
                lamp.range = 4.5f;
                lamp.intensity = 2.2f;
                lamp.shadows = LightShadows.None;

                var drain = MakeDrain(root.transform, glow);
                var beam = MakeBeam(root.transform, AssetDatabase.LoadAssetAtPath<Material>(BeamMaterial) ?? glow);

                jarScript.lockObject = lockGo;
                jarScript.jar = jarRenderer;
                jarScript.fairy = fairy;
                jarScript.jarLight = lamp;
                jarScript.drain = drain;
                jarScript.beam = beam;
                jarScript.calls = CallSounds.Select(AssetDatabase.LoadAssetAtPath<AudioClip>).Where(c => c != null).ToArray();
                jarScript.shardsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShardsPrefab);
                jarScript.shardsScale = JarScale;
                jarScript.breakSound = AssetDatabase.LoadAssetAtPath<AudioClip>(BreakSound);

                PrefabUtility.SaveAsPrefabAsset(root, JarPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static ParticleSystem MakeDrain(Transform parent, Material glow)
        {
            var go = new GameObject("Drain");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startSpeed = 0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 1.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.26f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 1f, 0.5f, 0.95f), new Color(0.75f, 1f, 0.6f, 0.75f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            color.color = g;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = glow;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        private static LineRenderer MakeBeam(Transform parent, Material material)
        {
            var go = new GameObject("Beam");
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = 0.2f;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 2;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(0.5f, 1f, 0.55f), 0f), new GradientColorKey(new Color(0.35f, 1f, 0.4f), 1f) },
                new[] { new GradientAlphaKey(0.25f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0.45f, 1f) });
            line.colorGradient = g;
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        // Boss_Area's root is scaled unevenly: the spot undoes that, so the jar keeps its own size.
        private static void PlaceInArena()
        {
            var jarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(JarPrefabPath);
            var root = PrefabUtility.LoadPrefabContents(ArenaPath);
            try
            {
                var old = root.transform.Find(SpotName);
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var scale = root.transform.localScale;
                var spot = new GameObject(SpotName).transform;
                spot.SetParent(root.transform, false);
                spot.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
                spot.position = root.transform.position;
                var jar = (GameObject)PrefabUtility.InstantiatePrefab(jarPrefab, spot);
                jar.transform.localPosition = SpotPosition;
                jar.transform.localRotation = Quaternion.identity;
                PrefabUtility.SaveAsPrefabAsset(root, ArenaPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static PlayMakerFSM FindStages()
        {
            foreach (var fsm in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
            {
                if (!fsm.gameObject.scene.IsValid() || fsm.FsmName != "GAMESTAGES") continue;
                if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
                return fsm;
            }
            Debug.LogError(Tag + " No GAMESTAGES in the open scene: open 2 Game Scene and run it again.");
            return null;
        }

        private static FsmState Loaded(Fsm f, string name)
        {
            var state = f.GetState(name);
            if (state == null) return null;
            state.LoadActions();
            return state.ActionData.ActionCount == state.Actions.Length ? state : null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
