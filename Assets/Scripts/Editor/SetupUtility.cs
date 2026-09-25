using System;
using System.Collections.Generic;
using System.IO;
using Game.Inventory;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>Shared helpers for the Tools > 429 Game setup menus.</summary>
    internal static class SetupUtility
    {
        public const string ItemsFolder = "Assets/Data/Inventory/Items";

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) return;

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        public static List<ItemDefinition> FindAllItems()
        {
            var items = new List<ItemDefinition>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(ItemDefinition)))
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (item != null) items.Add(item);
            }
            items.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return items;
        }

        /// <summary>Returns the item whose world prefab is this prefab, creating one (with an icon) if needed.</summary>
        public static ItemDefinition GetOrCreateItemForPrefab(GameObject prefab)
        {
            foreach (var existing in FindAllItems())
            {
                if (existing.WorldPrefab == prefab) return existing;
            }

            EnsureFolder(ItemsFolder);
            var path = AssetDatabase.GenerateUniqueAssetPath($"{ItemsFolder}/{prefab.name}.asset");
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            AssetDatabase.CreateAsset(item, path);

            var serialized = new SerializedObject(item);
            serialized.FindProperty("displayName").stringValue = prefab.name.Replace('_', ' ').Trim();
            serialized.FindProperty("worldPrefab").objectReferenceValue = prefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            item.EditorFreezeId();
            EditorUtility.SetDirty(item);

            PrefabIconMaker.Queue(prefab, item);
            return item;
        }

        public static void SetReference(Object target, string field, Object value, bool recordUndo = true)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[Setup] {target.GetType().Name} has no field '{field}'.", target);
                return;
            }
            property.objectReferenceValue = value;
            if (recordUndo) serialized.ApplyModifiedProperties();
            else serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Sets the field only if it's empty, so choices made in the Inspector are kept.</summary>
        public static void SetReferenceIfEmpty(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null || property.objectReferenceValue != null) return;

            property.objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
        }

        /// <summary>A world-space canvas (1 unit = 1 mm) with a background, which XR hands can poke and point at.</summary>
        public static RectTransform CreateWorldCanvas(string name, Transform parent, Vector2 size, Color background)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(root, "Create " + name);
            root.layer = LayerMask.NameToLayer("UI");

            var rect = (RectTransform)root.transform;
            if (parent != null) rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.localScale = Vector3.one * 0.001f;
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            root.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;

            var backgroundImage = CreateImage("Background", rect, background);
            Stretch(backgroundImage.rectTransform, Vector2.zero, Vector2.zero);
            return rect;
        }

        /// <summary>Anchors rect to the top edge: 'top' down from it, 'height' tall, inset 'left' and 'right'.</summary>
        public static void TopBand(RectTransform rect, float top, float height, float left, float right)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>Anchors rect to the bottom edge: 'bottom' up from it, 'height' tall, inset 'left' and 'right'.</summary>
        public static void BottomBand(RectTransform rect, float bottom, float height, float left, float right)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, bottom + height);
        }

        /// <summary>
        /// Picks a font that can actually draw text. TextMeshPro's default font can be missing from a project,
        /// and a dynamic font without its source file has no letters, so both are skipped.
        /// Prefers Roboto-Bold, then any Roboto, then any usable font.
        /// </summary>
        public static TMP_FontAsset ResolveFont()
        {
            if (CanDrawText(TMP_Settings.defaultFontAsset)) return TMP_Settings.defaultFontAsset;

            TMP_FontAsset roboto = null;
            TMP_FontAsset any = null;
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(TMP_FontAsset)))
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (!CanDrawText(font)) continue;

                if (font.name == "Roboto-Bold SDF") return font;
                if (roboto == null && font.name.StartsWith("Roboto", StringComparison.OrdinalIgnoreCase)) roboto = font;
                if (any == null) any = font;
            }
            return roboto != null ? roboto : any;
        }

        public static bool CanDrawText(TMP_FontAsset font)
        {
            return font != null && (font.characterTable.Count > 0 || font.sourceFontFile != null);
        }

        /// <summary>Gives every text under root that can't draw a usable font. Returns how many changed.</summary>
        public static int AssignMissingFonts(Component root, TMP_FontAsset font)
        {
            if (font == null) return 0;

            int assigned = 0;
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (CanDrawText(text.font)) continue;

                Undo.RecordObject(text, "Assign Font");
                text.font = font;
                EditorUtility.SetDirty(text);
                assigned++;
            }
            return assigned;
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        public static TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, TextAlignmentOptions alignment, FontStyles style, TMP_FontAsset font)
        {
            var label = CreateRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = Color.white;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            return label;
        }

        public static Image CreateImage(string name, Transform parent, Color color)
        {
            var image = CreateRect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Button CreateButton(string name, Transform parent, string text, float fontSize, TMP_FontAsset font, Color color)
        {
            var image = CreateImage(name, parent, color);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var label = CreateText("Label", image.transform, text, fontSize, TextAlignmentOptions.Center, FontStyles.Bold, font);
            Stretch(label.rectTransform, Vector2.zero, Vector2.zero);
            return button;
        }

        public static LayoutElement SetLayout(GameObject go, float preferredWidth = -1f, float preferredHeight = -1f, float flexibleWidth = -1f)
        {
            if (!go.TryGetComponent<LayoutElement>(out var layout)) layout = go.AddComponent<LayoutElement>();
            if (preferredWidth >= 0f)
            {
                layout.minWidth = preferredWidth;
                layout.preferredWidth = preferredWidth;
            }
            if (preferredHeight >= 0f) layout.preferredHeight = preferredHeight;
            if (flexibleWidth >= 0f) layout.flexibleWidth = flexibleWidth;
            return layout;
        }
    }
}
