using System.Collections.Generic;
using System.IO;
using Game.Inventory;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>
    /// Makes item icons from the 3D preview Unity draws for a prefab. Previews load in the background,
    /// so icons are saved a moment after they're requested.
    /// </summary>
    internal static class PrefabIconMaker
    {
        private const string IconFolder = "Assets/Data/Inventory/Icons";
        private const double TimeoutSeconds = 10.0;

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

        private static void Tick()
        {
            if (pending.Count == 0)
            {
                EditorApplication.update -= Tick;
                running = false;
                return;
            }

            var (prefab, item) = pending.Peek();
            if (prefab != null && item != null)
            {
                var preview = AssetPreview.GetAssetPreview(prefab);
                if (preview == null)
                {
                    if (EditorApplication.timeSinceStartup - waitingSince < TimeoutSeconds) return;
                    Debug.LogWarning($"[Item Icons] Couldn't make an icon for '{prefab.name}'. Give '{item.name}' an icon by hand.", item);
                }
                else
                {
                    SaveIcon(preview, item);
                }
            }

            pending.Dequeue();
            waitingSince = EditorApplication.timeSinceStartup;
        }

        private static void SaveIcon(Texture2D preview, ItemDefinition item)
        {
            SetupUtility.EnsureFolder(IconFolder);
            var path = $"{IconFolder}/{item.name}.png";

            var copy = ReadableCopy(preview);
            File.WriteAllBytes(path, copy.EncodeToPNG());
            Object.DestroyImmediate(copy);

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
