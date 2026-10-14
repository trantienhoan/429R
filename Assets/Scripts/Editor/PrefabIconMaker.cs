using System.Collections.Generic;
using System.IO;
using Game.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>
    /// Makes item icons: the prefab drawn on its own, from the front-right and a little above, on a see-through
    /// background. If that can't be done (no visible mesh), Unity's own 3D preview of the prefab is used, which loads
    /// in the background, so those icons are saved a moment after they're requested.
    /// </summary>
    internal static class PrefabIconMaker
    {
        private const string IconFolder = "Assets/Data/Inventory/Icons";
        private const double TimeoutSeconds = 10.0;
        private const int IconSize = 256;
        // Drawn this many times bigger, then shrunk, for smooth edges.
        private const int Supersample = 2;

        private static readonly Queue<(GameObject prefab, ItemDefinition item)> pending = new();
        private static double waitingSince;
        private static bool running;

        public static void Queue(GameObject prefab, ItemDefinition item)
        {
            if (prefab == null || item == null) return;

            pending.Enqueue((prefab, item));
            if (running) return;

            running = true;
            waitingSince = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }

        /// <summary>Draws the icon now and gives it to the item. False if the prefab has nothing to draw.</summary>
        public static bool MakeNow(GameObject prefab, ItemDefinition item)
        {
            if (prefab == null || item == null) return false;

            var icon = Render(prefab);
            if (icon == null) return false;

            SaveIcon(icon, item);
            Object.DestroyImmediate(icon);
            return true;
        }

        /// <summary>Draws the prefab's icon and saves it as a sprite at 'path' (e.g. a recipe picture). Null if it has
        /// nothing to draw.</summary>
        public static Sprite MakeSprite(GameObject prefab, string path)
        {
            if (prefab == null) return null;
            var icon = Render(prefab);
            if (icon == null) return null;
            SetupUtility.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            File.WriteAllBytes(path, icon.EncodeToPNG());
            Object.DestroyImmediate(icon);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void Tick()
        {
            if (pending.Count == 0)
            {
                EditorApplication.update -= Tick;
                running = false;
                return;
            }

            var (prefab, item) = pending.Peek();
            if (prefab != null && item != null && !MakeNow(prefab, item))
            {
                var preview = AssetPreview.GetAssetPreview(prefab);
                if (preview == null)
                {
                    if (EditorApplication.timeSinceStartup - waitingSince < TimeoutSeconds) return;
                    Debug.LogWarning($"[Item Icons] Couldn't make an icon for '{prefab.name}'. Give '{item.name}' an icon by hand.", item);
                }
                else
                {
                    var copy = ReadableCopy(preview);
                    SaveIcon(copy, item);
                    Object.DestroyImmediate(copy);
                }
            }

            pending.Dequeue();
            waitingSince = EditorApplication.timeSinceStartup;
        }

        // The prefab alone in a preview scene (the open scene isn't touched), lit by two lights, framed to fill the icon.
        private static Texture2D Render(GameObject prefab)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var cameraObject = EditorUtility.CreateGameObjectWithHideFlags("Icon Camera", HideFlags.HideAndDontSave);
            RenderTexture target = null;
            try
            {
                var copy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                copy.transform.SetPositionAndRotation(Vector3.zero, prefab.transform.rotation);

                // Only solid meshes: no particles, trails, lines or world-space menus.
                bool hasBounds = false;
                var bounds = new Bounds();
                foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true))
                {
                    bool solid = (renderer is MeshRenderer || renderer is SkinnedMeshRenderer) && renderer.enabled && renderer.gameObject.activeInHierarchy;
                    if (!solid)
                    {
                        renderer.enabled = false;
                        continue;
                    }
                    if (renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
                    if (hasBounds) bounds.Encapsulate(renderer.bounds);
                    else bounds = renderer.bounds;
                    hasBounds = true;
                }
                foreach (var canvas in copy.GetComponentsInChildren<Canvas>(true)) canvas.enabled = false;
                if (!hasBounds || bounds.size.sqrMagnitude < 1e-8f) return null;

                AddLight(scene, "Key", Quaternion.Euler(35f, 30f, 0f), 1.1f);
                AddLight(scene, "Fill", Quaternion.Euler(10f, -140f, 0f), 0.5f);

                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camera.orthographic = true;

                // From the front-right and a little above; fit the bounds' corners as seen from there.
                var view = Quaternion.LookRotation(new Vector3(-0.55f, -0.4f, 1f).normalized, Vector3.up);
                cameraObject.transform.rotation = view;
                float half = 0f;
                for (int i = 0; i < 8; i++)
                {
                    var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var offset = Quaternion.Inverse(view) * (corner - bounds.center);
                    half = Mathf.Max(half, Mathf.Abs(offset.x), Mathf.Abs(offset.y));
                }
                float depth = bounds.extents.magnitude + 1f;
                cameraObject.transform.position = bounds.center - cameraObject.transform.forward * depth;
                camera.orthographicSize = half * 1.08f;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = depth * 2f + 1f;

                int big = IconSize * Supersample;
                target = new RenderTexture(big, big, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 };
                camera.targetTexture = target;
                camera.Render();
                camera.targetTexture = null;

                var small = RenderTexture.GetTemporary(IconSize, IconSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(target, small);
                var previous = RenderTexture.active;
                RenderTexture.active = small;
                var icon = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
                icon.ReadPixels(new Rect(0, 0, IconSize, IconSize), 0, 0);
                icon.Apply();
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(small);
                return icon;
            }
            finally
            {
                if (target != null) Object.DestroyImmediate(target);
                EditorSceneManager.ClosePreviewScene(scene);
                if (cameraObject != null) Object.DestroyImmediate(cameraObject);
            }
        }

        private static void AddLight(Scene scene, string name, Quaternion rotation, float intensity)
        {
            var go = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.rotation = rotation;
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
        }

        private static void SaveIcon(Texture2D icon, ItemDefinition item)
        {
            SetupUtility.EnsureFolder(IconFolder);
            var path = $"{IconFolder}/{item.name}.png";
            File.WriteAllBytes(path, icon.EncodeToPNG());

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            var serialized = new SerializedObject(item);
            serialized.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();
        }

        // Preview textures aren't always readable, so copy them through a render texture first.
        private static Texture2D ReadableCopy(Texture2D source)
        {
            var target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, target);

            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            copy.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            return copy;
        }
    }
}
