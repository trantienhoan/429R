using System;
using System.Collections.Generic;
using System.IO;
using Game.Menus;
using Game.Saving;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>
    /// Menu command that builds the start menu (three save slots, Start Game, Delete Save) and the save notices (the
    /// loading screen and the small "Saved" sign) in the open scene (Tools > 429 Game > Menu).
    /// </summary>
    public static class SaveSlotMenuSetup
    {
        private const string MaterialFolder = "Assets/Data/UI";
        private const string ImageMaterialPath = MaterialFolder + "/Save Notice Image.mat";
        private const string TextMaterialPath = MaterialFolder + "/Save Notice Text.mat";
        private const string ImageShader = "429/UI Overlay";
        private const string TextShader = "TextMeshPro/Mobile/Distance Field Overlay";
        // Drawn after the headset fade (its material: 3002 now, 4000 when Set Up Death Fall makes it) and after the
        // GAME_OVER picture (UI Over Fade, 4001), so the loading screen shows over the black.
        private const int OverFadeQueue = 4002;
        // The save icon: a cauldron (white on clear, made from the user's picture). If it's missing, a floppy disk is drawn
        // in its place.
        private const string SaveIconPath = "Assets/Images/icon_save_cauldron.png";
        // The cauldron's size on the loading screen and on the Saved sign (it shows up and disappears; no spinner).
        private const float LoadingIconSize = 170f;
        private const float SavedIconSize = 86f;
        private const string ReminderText = "Saves at every checkpoint.\nDon't quit while the cauldron shows.";
        private const string SoundFolder = "Assets/Audio/Kenney Audio/kenney_interface-sounds/Audio/";
        private const int IconSize = 256;

        // The menu panel, in canvas units (1 = 1 mm).
        private const float PanelWidth = 640f;
        private const float PanelHeight = 620f;
        private const float SlotsTop = 128f;
        private const float SlotHeight = 108f;
        private const float SlotSpacing = 14f;
        private const float SlotInset = 40f;

        private static readonly Color PanelColor = new(0.08f, 0.06f, 0.12f, 0.96f);
        private static readonly Color TitleColor = new(1f, 0.8f, 0.3f, 1f);
        private static readonly Color SlotColor = new(0.2f, 0.17f, 0.32f, 1f);
        private static readonly Color HoldColor = new(0.8f, 0.12f, 0.12f, 0.85f);
        private static readonly Color HoldFullColor = new(1f, 0.16f, 0.1f, 0.95f);
        private static readonly Color StartColor = new(0.3f, 0.58f, 0.32f, 1f);
        private static readonly Color DeleteColor = new(0.6f, 0.22f, 0.28f, 1f);
        private static readonly Color CancelColor = new(0.25f, 0.3f, 0.52f, 1f);
        private static readonly Color DateColor = new(1f, 1f, 1f, 0.6f);

        /// <summary>
        /// Creates the Save Slot Menu (in front of the player at start) and the Save Notice (under the headset camera), with
        /// the save icons and the over-the-fade materials. Run again, it only adds what's missing and fixes fonts; what
        /// was tuned by hand is kept. Also brings the pause menu's Quit and Reset Progress texts up to date.
        /// </summary>
        [MenuItem("Tools/429 Game/Menu/Set Up Save Slot Menu")]
        public static void SetUpSaveSlotMenu()
        {
            var notes = new List<string>();
            var font = SetupUtility.ResolveFont();
            var origin = Object.FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
            var camera = origin != null && origin.Camera != null ? origin.Camera : Camera.main;
            var head = camera != null ? camera.transform : null;

            var icon = PrepareIcon(SaveIconPath, DrawDisk, notes);

            var menu = Object.FindAnyObjectByType<SaveSlotMenu>(FindObjectsInactive.Include);
            if (menu == null)
            {
                menu = BuildMenu(font, camera);
                notes.Add("created 'Save Slot Menu' in the scene");
            }
            else if (SetupUtility.AssignMissingFonts(menu, font) > 0)
            {
                notes.Add("fixed the start menu's fonts");
            }
            if (head != null) SetupUtility.SetReferenceIfEmpty(menu, "head", head);
            SetSoundIfEmpty(menu, "clickSound", "select_002.ogg");
            SetSoundIfEmpty(menu, "startSound", "confirmation_002.ogg");
            SetSoundIfEmpty(menu, "deleteSound", "close_002.ogg");

            var notice = Object.FindAnyObjectByType<SaveNotice>(FindObjectsInactive.Include);
            if (notice == null)
            {
                if (head == null)
                {
                    notes.Add("WARNING: no XR Origin camera in the scene, so the loading screen and Saved sign weren't made");
                }
                else if (!TryGetMaterials(font, out var imageMaterial, out var textMaterial))
                {
                    notes.Add($"WARNING: shader '{ImageShader}' (Assets/Shader/UIOverlay.shader) or '{TextShader}' is missing, so the loading screen and Saved sign weren't made");
                }
                else
                {
                    BuildNotice(head, font, imageMaterial, textMaterial, icon);
                    notes.Add($"created 'Save Notice' (loading screen + Saved sign) under '{head.name}'");
                }
            }
            else if (SetupUtility.AssignMissingFonts(notice, font) > 0)
            {
                notes.Add("fixed the save notice's fonts");
            }

            var pause = Object.FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include);
            if (pause != null && PauseMenuSetup.UpdateSaveSlotTexts(pause))
                notes.Add("the pause menu's Quit and Reset Progress texts now talk about checkpoints and save slots");

            if (Object.FindAnyObjectByType<SaveManager>(FindObjectsInactive.Include) == null)
                notes.Add("WARNING: there's no Save Manager in the scene: run Tools > 429 Game > Save > Set Up Save Manager");

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = menu.gameObject;
            Debug.Log("[Save Slot Menu Setup] Done: " + (notes.Count > 0 ? string.Join("; ", notes) : "everything was already set up") +
                      ". Save the scene to keep it.", menu);
        }

        [MenuItem("Tools/429 Game/Menu/Set Up Save Slot Menu", true)]
        private static bool CanSetUpSaveSlotMenu() => !EditorApplication.isPlaying;

        // ---------- The start menu ----------

        private static SaveSlotMenu BuildMenu(TMP_FontAsset font, Camera camera)
        {
            var go = new GameObject("Save Slot Menu");
            Undo.RegisterCreatedObjectUndo(go, "Create Save Slot Menu");
            var menu = go.AddComponent<SaveSlotMenu>();

            var rect = SetupUtility.CreateWorldCanvas("Save Slot Panel", go.transform, new Vector2(PanelWidth, PanelHeight), PanelColor);
            var group = rect.gameObject.AddComponent<CanvasGroup>();
            // The mouse (Editor tests): it needs a plain raycaster and a camera; the headset's rays use the tracked one.
            var mouse = rect.gameObject.AddComponent<GraphicRaycaster>();
            if (camera != null) rect.GetComponent<Canvas>().worldCamera = camera;

            var title = SetupUtility.CreateText("Title", rect, "Choose a Save", 50f, TextAlignmentOptions.Center, FontStyles.Bold, font);
            title.color = TitleColor;
            SetupUtility.TopBand(title.rectTransform, 22f, 64f, 30f, 30f);

            var hint = SetupUtility.CreateText("Hint", rect, "Hold a slot to start", 24f, TextAlignmentOptions.Center, FontStyles.Italic, font);
            hint.color = DateColor;
            SetupUtility.TopBand(hint.rectTransform, 86f, 32f, 30f, 30f);

            var serialized = new SerializedObject(menu);
            var slots = serialized.FindProperty("slots");
            slots.arraySize = SaveManager.SlotCount;
            for (int i = 0; i < SaveManager.SlotCount; i++)
            {
                BuildSlot(rect, i, font, slots.GetArrayElementAtIndex(i));
            }

            // Start Game big on the right, Delete Save clearly smaller on the left.
            var delete = SetupUtility.CreateButton("Delete Save", rect, "Delete Save", 24f, font, DeleteColor);
            PlaceBottom((RectTransform)delete.transform, -170f, 70f, new Vector2(180f, 56f));
            FitLabel(delete, 14f, 10f);
            var start = SetupUtility.CreateButton("Start Game", rect, "Start Game", 40f, font, StartColor);
            PlaceBottom((RectTransform)start.transform, 110f, 70f, new Vector2(300f, 84f));
            FitLabel(start, 22f, 12f);

            var confirm = BuildConfirm(rect, font, out var confirmText, out var confirmDelete, out var confirmCancel);

            serialized.FindProperty("panel").objectReferenceValue = group;
            serialized.FindProperty("startButton").objectReferenceValue = start;
            serialized.FindProperty("deleteButton").objectReferenceValue = delete;
            serialized.FindProperty("confirmPanel").objectReferenceValue = confirm;
            serialized.FindProperty("confirmText").objectReferenceValue = confirmText;
            serialized.FindProperty("confirmDeleteButton").objectReferenceValue = confirmDelete;
            serialized.FindProperty("confirmCancelButton").objectReferenceValue = confirmCancel;
            serialized.FindProperty("mouseRaycaster").objectReferenceValue = mouse;
            if (camera != null) serialized.FindProperty("head").objectReferenceValue = camera.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // Hidden in the editor; the menu shows it when the game starts.
            rect.gameObject.SetActive(false);
            return menu;
        }

        // One slot: "Slot 1" on the left, the level and room and the date on the right, a red bar that fills while it's
        // held, and an outline when selected.
        private static void BuildSlot(RectTransform panel, int index, TMP_FontAsset font, SerializedProperty slot)
        {
            int number = index + 1;
            var button = SetupUtility.CreateButton($"Slot {number}", panel, "", 30f, font, SlotColor);
            var label = button.transform.Find("Label");
            if (label != null) Object.DestroyImmediate(label.gameObject);
            var rect = (RectTransform)button.transform;
            SetupUtility.TopBand(rect, SlotsTop + index * (SlotHeight + SlotSpacing), SlotHeight, SlotInset, SlotInset);
            var background = button.GetComponent<Image>();
            // The menu colours the selected slot itself; the button only darkens a little when pointed at or pressed.
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.9f, 0.9f, 1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.85f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;

            var outline = background.gameObject.AddComponent<Outline>();
            outline.effectColor = TitleColor;
            outline.effectDistance = new Vector2(5f, -5f);
            outline.enabled = false;

            var fill = SetupUtility.CreateImage("Hold Fill", rect, HoldColor);
            var fillRect = fill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fill.gameObject.SetActive(false);

            var title = SetupUtility.CreateText("Title", rect, $"Slot {number}", 40f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, font);
            SetupUtility.Stretch(title.rectTransform, new Vector2(24f, 0f), new Vector2(-(PanelWidth - 2f * SlotInset - 190f), 0f));
            SetupUtility.ShrinkToFit(title, 24f);

            var info = SetupUtility.CreateText("Info", rect, "Empty", 30f, TextAlignmentOptions.BottomLeft, FontStyles.Bold, font);
            SetupUtility.TopBand(info.rectTransform, 12f, 46f, 190f, 20f);
            SetupUtility.ShrinkToFit(info, 18f);
            var date = SetupUtility.CreateText("Date", rect, "", 22f, TextAlignmentOptions.TopLeft, FontStyles.Normal, font);
            date.color = DateColor;
            SetupUtility.TopBand(date.rectTransform, 62f, 34f, 190f, 20f);
            SetupUtility.ShrinkToFit(date, 14f);

            var hold = button.gameObject.AddComponent<PressAndHold>();
            var holdSettings = new SerializedObject(hold);
            holdSettings.FindProperty("holdSeconds").floatValue = 2f;
            holdSettings.FindProperty("tint").objectReferenceValue = fill;
            holdSettings.FindProperty("warningColor").colorValue = HoldFullColor;
            holdSettings.FindProperty("fill").objectReferenceValue = fillRect;
            holdSettings.FindProperty("drainSeconds").floatValue = 0.35f;
            holdSettings.FindProperty("fasterWithBothTriggers").boolValue = true;
            holdSettings.ApplyModifiedPropertiesWithoutUndo();

            slot.FindPropertyRelative("button").objectReferenceValue = button;
            slot.FindPropertyRelative("background").objectReferenceValue = background;
            slot.FindPropertyRelative("selectedMark").objectReferenceValue = outline;
            slot.FindPropertyRelative("title").objectReferenceValue = title;
            slot.FindPropertyRelative("info").objectReferenceValue = info;
            slot.FindPropertyRelative("date").objectReferenceValue = date;
            slot.FindPropertyRelative("hold").objectReferenceValue = hold;
        }

        // The box over the menu that asks before deleting: "Delete Slot 2?", Cancel on the left, Delete on the right.
        private static GameObject BuildConfirm(RectTransform panel, TMP_FontAsset font, out TMP_Text heading, out Button delete, out Button cancel)
        {
            var box = SetupUtility.CreateRect("Confirm Delete", panel);
            SetupUtility.Stretch(box, Vector2.zero, Vector2.zero);
            var background = box.gameObject.AddComponent<Image>();
            background.color = new Color(0.17f, 0.05f, 0.09f, 1f);
            // Catches the pointer, so nothing on the menu behind it can be pressed while it asks.
            background.raycastTarget = true;

            heading = SetupUtility.CreateText("Heading", box, "Delete Slot 1?", 48f, TextAlignmentOptions.Center, FontStyles.Bold, font);
            SetupUtility.TopBand(heading.rectTransform, 170f, 70f, 30f, 30f);
            SetupUtility.ShrinkToFit(heading, 28f);
            var message = SetupUtility.CreateText("Message", box, "Can't be undone.", 28f, TextAlignmentOptions.Center, FontStyles.Normal, font);
            message.color = DateColor;
            SetupUtility.TopBand(message.rectTransform, 250f, 44f, 30f, 30f);

            cancel = SetupUtility.CreateButton("Cancel", box, "Cancel", 32f, font, CancelColor);
            PlaceBottom((RectTransform)cancel.transform, -125f, 170f, new Vector2(220f, 76f));
            FitLabel(cancel, 16f, 10f);
            delete = SetupUtility.CreateButton("Delete", box, "Delete", 32f, font, DeleteColor);
            PlaceBottom((RectTransform)delete.transform, 125f, 170f, new Vector2(220f, 76f));
            FitLabel(delete, 16f, 10f);

            box.gameObject.SetActive(false);
            return box.gameObject;
        }

        // A button anchored to the bottom middle: x from the middle, y up from the bottom edge (to its centre).
        private static void PlaceBottom(RectTransform rect, float x, float y, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(x, y);
        }

        // The label stays on one line inside its button, shrinking down to minSize if needed, with room at the sides.
        private static void FitLabel(Button button, float minSize, float sideRoom)
        {
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null) return;
            SetupUtility.ShrinkToFit(label, minSize);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.margin = new Vector4(sideRoom, 0f, sideRoom, 0f);
        }

        private static void SetSoundIfEmpty(SaveSlotMenu menu, string field, string file)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SoundFolder + file);
            if (clip != null) SetupUtility.SetReferenceIfEmpty(menu, field, clip);
        }

        // ---------- The save notices ----------

        // Under the headset camera, so both stay fixed to the view: the loading screen 0.6 m ahead, the Saved sign low on
        // the right (where the health HUD is on the left).
        private static void BuildNotice(Transform head, TMP_FontAsset font, Material imageMaterial, Material textMaterial, Sprite icon)
        {
            var go = new GameObject("Save Notice");
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(head, false);
            Undo.RegisterCreatedObjectUndo(go, "Create Save Notice");
            var notice = go.AddComponent<SaveNotice>();

            // The loading screen, over the black.
            var loading = CreateViewCanvas("Loading Screen", go.transform, new Vector2(640f, 360f), 0.0005f, new Vector3(0f, -0.03f, 0.6f), Quaternion.identity);
            var loadingGroup = loading.gameObject.AddComponent<CanvasGroup>();
            var loadingSaveIcon = Icon("Save Icon", loading, icon, LoadingIconSize, imageMaterial);
            Centre(loadingSaveIcon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(LoadingIconSize, LoadingIconSize));
            var loadingText = OverText("Loading", loading, "Loading...", 60f, FontStyles.Bold, font, textMaterial);
            SetupUtility.TopBand(loadingText.rectTransform, 196f, 70f, 20f, 20f);
            var reminder = OverText("Reminder", loading, ReminderText, 30f, FontStyles.Normal, font, textMaterial);
            reminder.color = new Color(1f, 1f, 1f, 0.8f);
            reminder.textWrappingMode = TextWrappingModes.Normal;
            SetupUtility.TopBand(reminder.rectTransform, 270f, 84f, 20f, 20f);
            SetupUtility.ShrinkToFit(reminder, 18f);

            // The small Saved sign: a dark plate, the save icon and "Saved".
            var saved = CreateViewCanvas("Saved Sign", go.transform, new Vector2(300f, 110f), 0.00045f, new Vector3(0.17f, -0.09f, 0.46f), Quaternion.Euler(10f, 20f, 0f));
            var savedGroup = saved.gameObject.AddComponent<CanvasGroup>();
            var plate = SetupUtility.CreateImage("Plate", saved, new Color(0f, 0f, 0f, 0.45f));
            plate.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            plate.type = Image.Type.Sliced;
            plate.pixelsPerUnitMultiplier = 0.4f;
            plate.material = imageMaterial;
            SetupUtility.Stretch(plate.rectTransform, Vector2.zero, Vector2.zero);
            var savedSaveIcon = Icon("Save Icon", saved, icon, SavedIconSize, imageMaterial);
            Centre(savedSaveIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(62f, 0f), new Vector2(SavedIconSize, SavedIconSize));
            var savedText = OverText("Saved", saved, "Saved", 54f, FontStyles.Bold, font, textMaterial);
            savedText.alignment = TextAlignmentOptions.MidlineLeft;
            SetupUtility.Stretch(savedText.rectTransform, new Vector2(118f, 0f), new Vector2(-12f, 0f));

            var serialized = new SerializedObject(notice);
            serialized.FindProperty("loading").objectReferenceValue = loadingGroup;
            serialized.FindProperty("loadingIcon").objectReferenceValue = loadingSaveIcon;
            serialized.FindProperty("saved").objectReferenceValue = savedGroup;
            serialized.FindProperty("savedIcon").objectReferenceValue = savedSaveIcon;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // Hidden in the editor; the notice shows them.
            loading.gameObject.SetActive(false);
            saved.gameObject.SetActive(false);
        }

        // A world-space canvas fixed to the view (a child of the camera), nothing to click on it.
        private static RectTransform CreateViewCanvas(string name, Transform parent, Vector2 size, float scale, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localPosition = position;
            rect.localRotation = rotation;
            rect.localScale = Vector3.one * scale;
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            return rect;
        }

        private static Image Icon(string name, RectTransform parent, Sprite sprite, float size, Material material)
        {
            var image = SetupUtility.CreateImage(name, parent, Color.white);
            image.sprite = sprite;
            image.preserveAspect = true;
            image.material = material;
            image.rectTransform.sizeDelta = new Vector2(size, size);
            return image;
        }

        private static void Centre(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static TextMeshProUGUI OverText(string name, RectTransform parent, string text, float size, FontStyles style, TMP_FontAsset font, Material material)
        {
            var label = SetupUtility.CreateText(name, parent, text, size, TextAlignmentOptions.Center, style, font);
            label.fontSharedMaterial = material;
            return label;
        }

        // The images use "429/UI Overlay" and the text a copy of the font's material with TextMeshPro's overlay shader:
        // both skip the depth test and draw after the headset fade, so the loading screen shows over the black.
        private static bool TryGetMaterials(TMP_FontAsset font, out Material imageMaterial, out Material textMaterial)
        {
            imageMaterial = null;
            textMaterial = null;
            var imageShader = Shader.Find(ImageShader);
            var textShader = Shader.Find(TextShader);
            if (imageShader == null || textShader == null || font == null) return false;

            SetupUtility.EnsureFolder(MaterialFolder);
            imageMaterial = GetOrCreateMaterial(ImageMaterialPath, () => new Material(imageShader) { name = "Save Notice Image" });
            textMaterial = GetOrCreateMaterial(TextMaterialPath, () =>
            {
                var m = new Material(font.material) { name = "Save Notice Text" };
                m.shader = textShader;
                return m;
            });
            if (imageMaterial.renderQueue != OverFadeQueue)
            {
                imageMaterial.renderQueue = OverFadeQueue;
                EditorUtility.SetDirty(imageMaterial);
            }
            if (textMaterial.renderQueue != OverFadeQueue)
            {
                textMaterial.renderQueue = OverFadeQueue;
                EditorUtility.SetDirty(textMaterial);
            }
            return true;
        }

        private static Material GetOrCreateMaterial(string path, Func<Material> create)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = create();
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // ---------- The save icons (drawn here once; edit or replace the PNGs freely) ----------

        // Makes the icon's PNG if it's missing, then imports it as a smooth (mipmapped) sprite.
        private static Sprite PrepareIcon(string path, Func<float, float, float> draw, List<string> notes)
        {
            if (!File.Exists(path))
            {
                var texture = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
                var pixels = new Color32[IconSize * IconSize];
                for (int y = 0; y < IconSize; y++)
                {
                    for (int x = 0; x < IconSize; x++)
                    {
                        // Centred, y up, 1 unit = 1 pixel.
                        float alpha = Mathf.Clamp01(draw(x + 0.5f - IconSize * 0.5f, y + 0.5f - IconSize * 0.5f));
                        pixels[y * IconSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                    }
                }
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
                notes.Add($"drew '{path}'");
            }

            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return null;
            if (importer.textureType != TextureImporterType.Sprite || !importer.mipmapEnabled || importer.maxTextureSize != IconSize)
            {
                // A small sprite seen from about half a metre: mipmaps keep it smooth instead of shimmering.
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Trilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = IconSize;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // Coverage of a shape from its signed distance (negative inside), smoothed over one pixel.
        private static float Cover(float distance) => Mathf.Clamp01(0.5f - distance);

        private static float Box(float x, float y, float cx, float cy, float halfWidth, float halfHeight, float radius)
        {
            float qx = Mathf.Abs(x - cx) - halfWidth + radius;
            float qy = Mathf.Abs(y - cy) - halfHeight + radius;
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        // A floppy disk: the body with its top right corner cut, the metal shutter at the top (a hole with a slot in it)
        // and the label at the bottom (a hole).
        private static float DrawDisk(float x, float y)
        {
            float body = Box(x, y, 0f, 0f, 96f, 96f, 16f);
            float corner = (x + y - 150f) * 0.70710678f;
            float shape = Mathf.Max(body, corner);
            float shutter = Box(x, y, -8f, 62f, 54f, 30f, 4f);
            float label = Box(x, y, 0f, -44f, 66f, 42f, 6f);
            shape = Mathf.Max(shape, Mathf.Max(-shutter, -label));
            float slot = Box(x, y, 20f, 62f, 13f, 22f, 2f);
            return Cover(Mathf.Min(shape, slot));
        }
    }
}
