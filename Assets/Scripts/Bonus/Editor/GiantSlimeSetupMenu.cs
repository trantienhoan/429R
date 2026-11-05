using System.Collections.Generic;
using Game.Bonus;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Bonus > Set Up Giant Slime (user, 2026-11-05). Builds Prefabs/Bonus/Paint_Giant_Slime from the
    /// user's Giant_Slime model (Models/Enemies/Giant_Slime.fbx: the head Giant_Slime_Below, hinged at the back on the jaw
    /// Giant_Slime_Upper), with its own material (Flying_Slime_Mat's look on the user's Flying_Slime_Base texture), and
    /// puts it in Paint_Bees_Round's Giant. The model stays a nested model, so a new export of the FBX shows up by itself.
    /// Running it again rebuilds the prefab. Never touches the scene.
    /// </summary>
    public static class GiantSlimeSetupMenu
    {
        private const string ModelPath = "Assets/Models/Enemies/Giant_Slime.fbx";
        private const string PrefabPath = "Assets/Prefabs/Bonus/Paint_Giant_Slime.prefab";
        private const string RoundPath = "Assets/Prefabs/Bases/Paint_Bees_Round.prefab";
        private const string LookSource = "Assets/Materials/Flying_Slime/Flying_Slime_Mat.mat";
        private const string MaterialPath = "Assets/Materials/Flying_Slime/Giant_Slime_Mat.mat";
        private const string BaseMap = "Assets/M_Textures/Flying_Slime/Flying_Slime_Base.png";
        private const string HeadName = "Giant_Slime_Below";
        private const string JawName = "Giant_Slime_Upper";
        private const string FallSound = "Assets/Audio/SFX/Cartoon_fall_1.wav";
        private const string LandSound = "Assets/Audio/SFX/pumpkin_face_splat.wav";
        private const string SpitSound = "Assets/Audio/SFX/magic_hat_spit_card.wav";
        private static readonly string[] HopSounds =
        {
            "Assets/Audio/Kenney Audio/kenney_sci-fi-sounds/Audio/slime_000.ogg",
            "Assets/Audio/Kenney Audio/kenney_sci-fi-sounds/Audio/slime_001.ogg",
        };

        [MenuItem("Tools/429 Game/Bonus/Set Up Giant Slime")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[Giant Slime] Stop play mode first. Nothing changed.");
                return;
            }
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && (stage.assetPath == PrefabPath || stage.assetPath == RoundPath))
            {
                Debug.LogError($"[Giant Slime] {stage.assetPath} is open in Prefab Mode: close it first. Nothing changed.");
                return;
            }
            Debug.Log("[Giant Slime] " + string.Join("; ", SetUp()));
        }

        public static List<string> SetUp()
        {
            var notes = new List<string>();
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
            {
                notes.Add($"no model at {ModelPath}: nothing changed");
                return notes;
            }
            // No animation in it (its parts are moved by Giant Slime), like the flying slime's model.
            if (importer.importAnimation || importer.animationType != ModelImporterAnimationType.None)
            {
                importer.importAnimation = false;
                importer.animationType = ModelImporterAnimationType.None;
                importer.SaveAndReimport();
                notes.Add("model: no animation, no Animator");
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var material = MakeMaterial(notes);

            // Built in a preview scene, so the open scene is never touched.
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Paint_Giant_Slime");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var body = new GameObject("Body").transform;
                body.SetParent(root.transform, false);
                var look = (GameObject)PrefabUtility.InstantiatePrefab(model, body);
                look.name = "Giant_Slime";
                foreach (var c in look.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                foreach (var r in look.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = material;
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                // Its bottom middle on the root (Body squashes from the floor up).
                var all = Bounds(look);
                look.transform.localPosition = new Vector3(-all.center.x, -all.min.y, -all.center.z);

                var head = look.transform.Find(HeadName);
                var jaw = look.transform.Find(JawName);
                if (head == null || jaw == null) notes.Add($"WARNING: no {HeadName} / {JawName} in the model: the mouth won't open");
                // The mouth: over the middle of the jaw, a little forward.
                var mouth = new GameObject("Mouth").transform;
                mouth.SetParent(look.transform, false);
                if (jaw != null && jaw.TryGetComponent(out Renderer jawLook))
                {
                    var b = jawLook.bounds;
                    mouth.position = new Vector3(b.center.x, b.max.y + 0.03f, b.center.z + b.extents.z * 0.3f);
                }
                else mouth.position = new Vector3(0f, all.size.y * 0.3f, all.extents.z * 0.3f);

                var slime = root.AddComponent<GiantSlime>();
                var so = new SerializedObject(slime);
                so.FindProperty("body").objectReferenceValue = body;
                so.FindProperty("head").objectReferenceValue = head;
                so.FindProperty("mouth").objectReferenceValue = mouth;
                so.FindProperty("fallSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(FallSound);
                so.FindProperty("landSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(LandSound);
                so.FindProperty("spitSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SpitSound);
                var hops = so.FindProperty("hopSounds");
                hops.arraySize = HopSounds.Length;
                for (int i = 0; i < HopSounds.Length; i++)
                    hops.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(HopSounds[i]);
                so.ApplyModifiedPropertiesWithoutUndo();

                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                notes.Add($"made {PrefabPath} ({all.size.x:0.00} m across at scale 1)");
                HookRound(saved.GetComponent<GiantSlime>(), notes);
            }
            finally
            {
                Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(scene);
            }
            return notes;
        }

        private static Material MakeMaterial(List<string> notes)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null) return material;
            var source = AssetDatabase.LoadAssetAtPath<Material>(LookSource);
            material = source != null ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var map = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseMap);
            if (map != null)
            {
                material.SetTexture("_BaseMap", map);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", map);
            }
            material.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(material, MaterialPath);
            notes.Add($"made {MaterialPath} (the flying slime's look, the user's Flying_Slime_Base texture)");
            return material;
        }

        private static void HookRound(GiantSlime giant, List<string> notes)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(RoundPath) == null)
            {
                notes.Add($"no {RoundPath}: Giant not set");
                return;
            }
            var round = PrefabUtility.LoadPrefabContents(RoundPath);
            try
            {
                var game = round.GetComponent<PaintBeesRound>();
                var so = new SerializedObject(game);
                so.FindProperty("giant").objectReferenceValue = giant;
                so.FindProperty("dipText").stringValue = "Dip your hands in the paint!";
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(round, RoundPath);
                notes.Add("Paint_Bees_Round: Giant set");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(round);
            }
        }

        private static Bounds Bounds(GameObject thing)
        {
            var renderers = thing.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(thing.transform.position, Vector3.one * 0.5f);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
