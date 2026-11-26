using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Story.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Story > Set Up Story Hints (Watcher, Shadow, Her Flowers) (user 2026-11-26: "I want 4, 6, 7, 8";
    /// the story told with visuals, hints and easter eggs). Re-runnable: it makes only what's missing.
    /// - M_Textures/Story/Story_Giant_Silhouette.png: pumpkin_boss_Z mid-laugh, black, cut out (where its eyes are goes in
    ///   the settings); Materials/Story/Story_Giant_Shadow.mat (Sprites/Default);
    /// - Resources/Story Giant Settings: the watcher (6) and the laugh's shadow (7), read by Story Giant Hints, which makes
    ///   itself in Play;
    /// - her lost things (8): Prefabs/Items/Knight_Armor/Fairy_Flower_1-4 (the Foliage_Set_1 flowers, a soft green glow,
    ///   a Knight Armor Pickup with id Keepsake:0-3), put into Resources/Knight Armor Settings with her sounds
    ///   (fairy_yippy for one, A_gentle_miracle_sound_2 for her thanks). Run Set Up Knight Armor Hunt first.
    /// Prefabs and assets only: nothing in the scene.
    /// </summary>
    public static class StoryHintsMenu
    {
        private const string Tag = "[Story Hints]";
        private const string SilhouettePath = "Assets/M_Textures/Story/Story_Giant_Silhouette.png";
        private const string ShadowMaterialPath = "Assets/Materials/Story/Story_Giant_Shadow.mat";
        private const string GiantSettingsPath = "Assets/Resources/Story Giant Settings.asset";
        private const string ArmorSettingsPath = "Assets/Resources/Knight Armor Settings.asset";
        private const string BossPath = "Assets/Prefabs/Enemies/pumpkin_boss_Z.prefab";
        // Its pose: both pumpkin fists raised in a V (the "laugh" clip hides its arms behind it, seen from the front).
        private const string LaughClip = "attack";
        private const float LaughAt = 0.4f;
        // A bright dot with a soft glow round it (Circle02 is a sheet of four rings).
        private const string EyeTexturePath = "Assets/Lana Studio/Hyper Casual FX/Textures/Circle01.png";
        private const string EyeMaterialPath = "Assets/Lana Studio/Hyper Casual FX/Materials/Circles_Additive.mat";
        private const string FlowerFolder = "Assets/Prefabs/Items/Knight_Armor";
        private const float FlowerSize = 0.25f;

        [MenuItem("Tools/429 Game/Story/Set Up Story Hints (Watcher, Shadow, Her Flowers)")]
        public static void SetUp() => Run(false);

        /// <summary>Paints the giant's picture again (after changing its pose here), and where its eyes are.</summary>
        [MenuItem("Tools/429 Game/Story/Re-render The Giant's Silhouette")]
        public static void Rerender() => Run(true);

        private static void Run(bool rerender)
        {
            var done = new List<string>();
            bool rendered = RenderSilhouette(rerender, out var leftEye, out var rightEye, done);
            var material = ShadowMaterial(done);
            Settings(rendered, leftEye, rightEye, material, done);
            Flowers(done);
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} " + (done.Count > 0 ? string.Join("; ", done) : "everything was already set up") +
                      ". Prefabs and assets only: no scene change.");
        }

        [MenuItem("Tools/429 Game/Story/Set Up Story Hints (Watcher, Shadow, Her Flowers)", true)]
        [MenuItem("Tools/429 Game/Story/Re-render The Giant's Silhouette", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // ---- The giant's silhouette: pumpkin_boss_Z mid-laugh, from the front, black on clear ----

        private static bool RenderSilhouette(bool rerender, out Vector2 leftEye, out Vector2 rightEye, List<string> done)
        {
            leftEye = rightEye = default;
            if (File.Exists(SilhouettePath) && !rerender) return false;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPath);
            if (prefab == null)
            {
                Debug.LogWarning($"{Tag} Missing {BossPath}: no silhouette.");
                return false;
            }
            var scene = EditorSceneManager.NewPreviewScene();
            var black = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            black.SetColor("_BaseColor", Color.black);
            RenderTexture rt = null;
            Texture2D shot = null;
            try
            {
                var boss = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                boss.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var animator = boss.GetComponentInChildren<Animator>(true);
                var clip = animator != null && animator.runtimeAnimatorController != null
                    ? animator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name == LaughClip) : null;
                if (clip != null) clip.SampleAnimation(animator.gameObject, clip.length * LaughAt);

                var body = boss.GetComponentsInChildren<Renderer>(true);
                foreach (var look in body)
                {
                    bool keep = look is SkinnedMeshRenderer || look is MeshRenderer;
                    look.enabled = keep;
                    if (keep) look.sharedMaterials = Enumerable.Repeat(black, look.sharedMaterials.Length).ToArray();
                }
                var bounds = new Bounds();
                bool any = false;
                foreach (var look in body.Where(l => l.enabled))
                {
                    if (look is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
                    if (!any) { bounds = look.bounds; any = true; }
                    else bounds.Encapsulate(look.bounds);
                }
                var bones = boss.GetComponentsInChildren<Transform>(true);
                var eyeL = bones.FirstOrDefault(t => t.name == "eye.L");
                var eyeR = bones.FirstOrDefault(t => t.name == "eye.R");
                var head = bones.FirstOrDefault(t => t.name == "Head");
                // Its face side: where its eyes are, seen from its head (else its forward).
                var face = boss.transform.forward;
                if (eyeL != null && eyeR != null && head != null)
                {
                    var toEyes = Vector3.ProjectOnPlane((eyeL.position + eyeR.position) * 0.5f - head.position, Vector3.up);
                    if (toEyes.sqrMagnitude > 0.0001f) face = toEyes.normalized;
                }

                var camGo = new GameObject("Silhouette Camera");
                SceneManager_Move(camGo, scene);
                var cam = camGo.AddComponent<Camera>();
                cam.scene = scene;
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                float biggest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                cam.orthographicSize = biggest * 0.55f;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = biggest * 6f;
                camGo.transform.position = bounds.center + face * biggest * 2f;
                camGo.transform.rotation = Quaternion.LookRotation(-face, Vector3.up);
                const int size = 1024;
                rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                shot = new Texture2D(size, size, TextureFormat.RGBA32, false);
                shot.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                shot.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;

                // Cut to what's drawn, black with its coverage as alpha.
                var pixels = shot.GetPixels32();
                int minX = size, minY = size, maxX = -1, maxY = -1;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    if (pixels[y * size + x].a < 25) continue;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
                if (maxX < 0)
                {
                    Debug.LogWarning($"{Tag} The silhouette render came out empty (nothing drawn).");
                    return false;
                }
                const int pad = 4;
                minX = Mathf.Max(0, minX - pad); minY = Mathf.Max(0, minY - pad);
                maxX = Mathf.Min(size - 1, maxX + pad); maxY = Mathf.Min(size - 1, maxY + pad);
                int w = maxX - minX + 1, h = maxY - minY + 1;
                var cut = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var outPixels = new Color32[w * h];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    outPixels[y * w + x] = new Color32(0, 0, 0, pixels[(y + minY) * size + x + minX].a);
                cut.SetPixels32(outPixels);
                cut.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(SilhouettePath));
                File.WriteAllBytes(SilhouettePath, cut.EncodeToPNG());
                Object.DestroyImmediate(cut);

                Vector2 ToCut(Transform t)
                {
                    if (t == null) return new Vector2(0.5f, 0.8f);
                    var v = cam.WorldToViewportPoint(t.position);
                    return new Vector2((v.x * size - minX) / w, (v.y * size - minY) / h);
                }
                leftEye = ToCut(eyeL);
                rightEye = ToCut(eyeR);
            }
            finally
            {
                if (rt != null) Object.DestroyImmediate(rt);
                if (shot != null) Object.DestroyImmediate(shot);
                Object.DestroyImmediate(black);
                EditorSceneManager.ClosePreviewScene(scene);
            }

            AssetDatabase.ImportAsset(SilhouettePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(SilhouettePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            var spriteSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(spriteSettings);
            spriteSettings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            importer.SetTextureSettings(spriteSettings);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
            done.Add($"rendered {SilhouettePath} (eyes at {leftEye:F2} / {rightEye:F2})");
            return true;
        }

        private static void SceneManager_Move(GameObject go, UnityEngine.SceneManagement.Scene scene) =>
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);

        private static Material ShadowMaterial(List<string> done)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(ShadowMaterialPath);
            if (material != null) return material;
            Directory.CreateDirectory(Path.GetDirectoryName(ShadowMaterialPath));
            material = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(material, ShadowMaterialPath);
            done.Add("made " + ShadowMaterialPath);
            return material;
        }

        private static void Settings(bool rendered, Vector2 leftEye, Vector2 rightEye, Material material, List<string> done)
        {
            var settings = AssetDatabase.LoadAssetAtPath<StoryGiantSettings>(GiantSettingsPath);
            if (settings == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(GiantSettingsPath));
                settings = ScriptableObject.CreateInstance<StoryGiantSettings>();
                AssetDatabase.CreateAsset(settings, GiantSettingsPath);
                done.Add("made " + GiantSettingsPath);
            }
            var so = new SerializedObject(settings);
            var sprite = AssetDatabase.LoadAllAssetsAtPath(SilhouettePath).OfType<Sprite>().FirstOrDefault();
            Fill(so, "silhouette", sprite, done);
            Fill(so, "shadowMaterial", material, done);
            Fill(so, "eyeTexture", AssetDatabase.LoadAssetAtPath<Texture2D>(EyeTexturePath), done);
            Fill(so, "eyeMaterial", AssetDatabase.LoadAssetAtPath<Material>(EyeMaterialPath), done);
            // A new picture: where its eyes are on it.
            if (rendered)
            {
                so.FindProperty("leftEye").vector2Value = leftEye;
                so.FindProperty("rightEye").vector2Value = rightEye;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        // ---- Her lost things: her 4 flowers ----

        private static void Flowers(List<string> done)
        {
            var armor = AssetDatabase.LoadAssetAtPath<KnightArmorSettings>(ArmorSettingsPath);
            if (armor == null)
            {
                Debug.LogWarning($"{Tag} No {ArmorSettingsPath}: run Tools > 429 Game > Items > Set Up Knight Armor Hunt first (no flowers).");
                return;
            }
            var made = new List<GameObject>();
            for (int i = 0; i < 4; i++)
            {
                var flower = Flower(i, done);
                if (flower != null) made.Add(flower);
            }
            var so = new SerializedObject(armor);
            var list = so.FindProperty("keepsakes");
            if (list.arraySize == 0 && made.Count > 0)
            {
                list.arraySize = made.Count;
                for (int i = 0; i < made.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = made[i];
                done.Add($"her lost things = {made.Count} flowers");
            }
            Fill(so, "keepsakeSound", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/fairy_yippy.wav"), done);
            Fill(so, "allKeepsakesSound", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/A_gentle_miracle_sound_2.wav"), done);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(armor);
        }

        private static GameObject Flower(int index, List<string> done)
        {
            string path = $"{FlowerFolder}/Fairy_Flower_{index + 1}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            string source = $"Assets/Models/Foliage_Set_1/Foliage_Set_1_Flower_{index + 1}.fbx";
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(source);
            if (fbx == null)
            {
                Debug.LogWarning($"{Tag} Missing {source}.");
                return null;
            }
            Directory.CreateDirectory(FlowerFolder);
            var root = new GameObject($"Fairy_Flower_{index + 1}");
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            foreach (var look in fbx.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = look is SkinnedMeshRenderer skin ? skin.sharedMesh
                    : look.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
                if (mesh == null) continue;
                var part = new GameObject(look.name);
                part.transform.SetParent(model, false);
                var m = fbx.transform.worldToLocalMatrix * look.transform.localToWorldMatrix;
                part.transform.localPosition = m.GetColumn(3);
                part.transform.localRotation = m.rotation;
                part.transform.localScale = m.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = part.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = look.sharedMaterials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var bounds = new Bounds();
            bool any = false;
            foreach (var look in model.GetComponentsInChildren<MeshRenderer>())
            {
                if (!any) { bounds = look.bounds; any = true; }
                else bounds.Encapsulate(look.bounds);
            }
            if (any)
            {
                float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                float scale = longest > 0.0001f ? FlowerSize / longest : 1f;
                model.localScale = Vector3.one * scale;
                model.localPosition = -bounds.center * scale;
            }
            var glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(root.transform, false);
            glow.type = LightType.Point;
            glow.color = new Color(0.55f, 1f, 0.5f);
            glow.intensity = 1.2f;
            glow.range = 1.2f;
            glow.shadows = LightShadows.None;
            var touch = root.AddComponent<SphereCollider>();
            touch.isTrigger = true;
            touch.radius = 0.25f;
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var pickup = root.AddComponent<KnightArmorPickup>();
            var so = new SerializedObject(pickup);
            so.FindProperty("pieceId").stringValue = KnightArmorSettings.KeepsakePrefix + index;
            so.ApplyModifiedPropertiesWithoutUndo();
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            done.Add("made " + path);
            return saved;
        }

        private static void Fill(SerializedObject so, string field, Object value, List<string> done)
        {
            var p = so.FindProperty(field);
            if (p == null || p.objectReferenceValue != null || value == null) return;
            p.objectReferenceValue = value;
            done.Add($"{field} = {value.name}");
        }
    }
}
