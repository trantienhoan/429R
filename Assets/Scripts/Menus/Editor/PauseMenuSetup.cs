using System.Collections.Generic;
using Game.Locomotion;
using Game.Menus;
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
    /// <summary>Menu command that creates the pause menu (Tools > 429 Game > Menu).</summary>
    public static class PauseMenuSetup
    {
        // The options go between the tip and the Resume/Quit buttons: two rows, one under the other.
        private const float OptionsTop = 182f;
        private const float OptionRowHeight = 52f;
        private const float OptionSpacing = 10f;
        // Tall enough for the title, tip, options and buttons.
        private const float PanelHeightWithOptions = 390f;
        private static readonly Color OptionColor = new(0.25f, 0.3f, 0.52f, 1f);
        private const string BackArrowSprite = "Assets/Images/icon_back_arrow.png";
        private const string QuitMessage =
            "Re-Dream: start over from the very beginning. Your candies, items and level are kept.\n\n" +
            "Get a Rest: close the game. Next time you play, you go on from your last checkpoint.";

        /// <summary>
        /// Creates the pause menu, or adds what an existing one is missing (the Arm swing, Height, Comfort vignette,
        /// Damage numbers and Health options), puts Player Height on the XR Origin and makes the XR Origin track from the real floor.
        /// </summary>
        [MenuItem("Tools/429 Game/Menu/Set Up Pause Menu")]
        public static void SetUpPauseMenu()
        {
            var notes = new List<string>();
            var font = SetupUtility.ResolveFont();

            var menu = Object.FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include);
            if (menu == null)
            {
                var go = new GameObject("Pause Menu");
                Undo.RegisterCreatedObjectUndo(go, "Create Pause Menu");
                menu = go.AddComponent<PauseMenu>();
                BuildPanel(menu, font);
                notes.Add("created 'Pause Menu' in the scene");
            }
            else if (SetupUtility.AssignMissingFonts(menu, font) > 0)
            {
                notes.Add("fixed the pause menu's fonts");
            }

            if (AddOptions(menu, font)) notes.Add("added the Arm swing and Height options");
            if (AddVignetteOption(menu, font)) notes.Add("added the Comfort vignette option");
            if (AddDamageNumbersOption(menu, font)) notes.Add("added the Damage numbers option");
            if (AddHealthOption(menu, font)) notes.Add("added the Health (show/hide) option");
            if (AddResetProgress(menu, font)) notes.Add("added Reset Progress (hold the title for 7 s)");
            if (AddQuitChoice(menu, font)) notes.Add("Quit now asks: Re-Dream or Get a Rest");
            if (TidyQuitChoice(menu)) notes.Add("the Quit box now has Re-Dream and Get a Rest, and Back is an arrow in its top left");

            var origin = Object.FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
            if (origin != null)
            {
                if (origin.Camera != null) SetupUtility.SetReferenceIfEmpty(menu, "head", origin.Camera.transform);

                if (!origin.TryGetComponent<PlayerHeight>(out var height))
                {
                    height = Undo.AddComponent<PlayerHeight>(origin.gameObject);
                    notes.Add($"added Player Height to '{origin.name}'");
                }
                SetupUtility.SetReferenceIfEmpty(menu, "playerHeight", height);

                if (TrackFromFloor(origin))
                    notes.Add($"'{origin.name}' now tracks from the real floor (Tracking Origin Mode Floor; it was a fixed {origin.CameraYOffset:0.##} m eye height)");
            }
            else
            {
                notes.Add("no XR Origin in the scene, so the Height option has nothing to move");
            }

            var armSwing = Object.FindAnyObjectByType<ArmSwingMoveProvider>(FindObjectsInactive.Include);
            if (armSwing != null) SetupUtility.SetReferenceIfEmpty(menu, "armSwing", armSwing);
            else notes.Add("no Arm Swing Move in the scene (Tools > 429 Game > Locomotion > Set Up Arm Swing), so that option is greyed out");

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = menu.gameObject;
            Debug.Log("[Pause Menu Setup] Done: " + (notes.Count > 0 ? string.Join("; ", notes) : "everything was already set up") +
                      ". Edit the tips on the Pause Menu object, then save the scene.");
        }

        private static void BuildPanel(PauseMenu menu, TMP_FontAsset font)
        {
            var rect = SetupUtility.CreateWorldCanvas("Pause Menu Panel", menu.transform, new Vector2(380f, 270f), new Color(0.1f, 0.07f, 0.16f, 0.95f));

            var title = SetupUtility.CreateText("Title", rect, "Paused", 36f, TextAlignmentOptions.Center, FontStyles.Bold, font);
            SetupUtility.TopBand(title.rectTransform, 14f, 50f, 16f, 16f);

            var tip = SetupUtility.CreateText("Tip", rect, "Tip:", 21f, TextAlignmentOptions.Center, FontStyles.Italic, font);
            SetupUtility.TopBand(tip.rectTransform, 74f, 100f, 24f, 24f);

            var buttons = SetupUtility.CreateRect("Buttons", rect);
            SetupUtility.BottomBand(buttons, 20f, 60f, 24f, 24f);
            var row = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 20f;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;

            var resume = SetupUtility.CreateButton("Resume", buttons, "Resume", 26f, font, new Color(0.3f, 0.58f, 0.32f, 1f));
            var quit = SetupUtility.CreateButton("Quit", buttons, "Quit", 26f, font, new Color(0.6f, 0.22f, 0.28f, 1f));

            SetupUtility.SetReference(menu, "panel", rect.gameObject);
            SetupUtility.SetReference(menu, "tipText", tip);
            SetupUtility.SetReference(menu, "resumeButton", resume);
            SetupUtility.SetReference(menu, "quitButton", quit);
            rect.gameObject.SetActive(false);
        }

        // Adds the Arm swing and Height rows, for menus made before they existed too. The panel grows taller and the
        // rows go under the tip; the title and tip stay anchored to the top edge, Resume and Quit to the bottom.
        private static bool AddOptions(PauseMenu menu, TMP_FontAsset font)
        {
            var serialized = new SerializedObject(menu);
            if (serialized.FindProperty("armSwingButton").objectReferenceValue != null) return false;
            if (serialized.FindProperty("panel").objectReferenceValue is not GameObject panelObject ||
                panelObject.transform is not RectTransform panel)
            {
                Debug.LogWarning("[Pause Menu Setup] The Pause Menu has no Panel, so the options weren't added.", menu);
                return false;
            }

            Undo.RecordObject(panel, "Add Pause Menu Options");
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, Mathf.Max(panel.sizeDelta.y, PanelHeightWithOptions));

            var options = SetupUtility.CreateRect("Options", panel);
            Undo.RegisterCreatedObjectUndo(options.gameObject, "Add Pause Menu Options");
            SetupUtility.TopBand(options, OptionsTop, OptionRowHeight * 2f + OptionSpacing, 24f, 24f);
            var column = options.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = OptionSpacing;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = true;

            var armSwing = SetupUtility.CreateButton("Arm Swing", options, "Arm swing: hold X", 24f, font, OptionColor);

            var heightRow = SetupUtility.CreateRect("Height", options);
            var row = heightRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = OptionSpacing;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = true;

            var down = SetupUtility.CreateButton("Lower", heightRow, "-", 32f, font, OptionColor);
            SetupUtility.SetLayout(down.gameObject, preferredWidth: 64f);
            var label = SetupUtility.CreateText("Height Label", heightRow, "Height: normal", 24f, TextAlignmentOptions.Center, FontStyles.Bold, font);
            SetupUtility.SetLayout(label.gameObject, flexibleWidth: 1f);
            var up = SetupUtility.CreateButton("Raise", heightRow, "+", 32f, font, OptionColor);
            SetupUtility.SetLayout(up.gameObject, preferredWidth: 64f);

            SetupUtility.SetReference(menu, "armSwingButton", armSwing);
            SetupUtility.SetReference(menu, "armSwingText", armSwing.GetComponentInChildren<TMP_Text>(true));
            SetupUtility.SetReference(menu, "heightDownButton", down);
            SetupUtility.SetReference(menu, "heightUpButton", up);
            SetupUtility.SetReference(menu, "heightText", label);
            return true;
        }

        // Adds the Comfort vignette row under the other options, for menus made before it existed too: the options
        // band and the panel grow by one row, so Resume and Quit (anchored to the bottom) move down to make room.
        private static bool AddVignetteOption(PauseMenu menu, TMP_FontAsset font)
        {
            var serialized = new SerializedObject(menu);
            if (serialized.FindProperty("vignetteButton").objectReferenceValue != null) return false;
            if (serialized.FindProperty("panel").objectReferenceValue is not GameObject panelObject ||
                panelObject.transform is not RectTransform panel) return false;
            var options = panel.Find("Options") as RectTransform;
            if (options == null)
            {
                Debug.LogWarning("[Pause Menu Setup] The Pause Menu panel has no Options group, so the Comfort vignette option wasn't added.", menu);
                return false;
            }

            const float grow = OptionRowHeight + OptionSpacing;
            Undo.RecordObject(panel, "Add Comfort Vignette Option");
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, panel.sizeDelta.y + grow);
            Undo.RecordObject(options, "Add Comfort Vignette Option");
            options.offsetMin = new Vector2(options.offsetMin.x, options.offsetMin.y - grow);

            var vignette = SetupUtility.CreateButton("Comfort Vignette", options, "Comfort vignette: low", 24f, font, OptionColor);
            Undo.RegisterCreatedObjectUndo(vignette.gameObject, "Add Comfort Vignette Option");
            SetupUtility.SetReference(menu, "vignetteButton", vignette);
            SetupUtility.SetReference(menu, "vignetteText", vignette.GetComponentInChildren<TMP_Text>(true));
            return true;
        }

        // Adds the Damage numbers on/off row under the other options, the same way as the Comfort vignette row.
        private static bool AddDamageNumbersOption(PauseMenu menu, TMP_FontAsset font)
        {
            var serialized = new SerializedObject(menu);
            if (serialized.FindProperty("damageNumbersButton").objectReferenceValue != null) return false;
            if (serialized.FindProperty("panel").objectReferenceValue is not GameObject panelObject ||
                panelObject.transform is not RectTransform panel) return false;
            var options = panel.Find("Options") as RectTransform;
            if (options == null)
            {
                Debug.LogWarning("[Pause Menu Setup] The Pause Menu panel has no Options group, so the Damage numbers option wasn't added.", menu);
                return false;
            }

            const float grow = OptionRowHeight + OptionSpacing;
            Undo.RecordObject(panel, "Add Damage Numbers Option");
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, panel.sizeDelta.y + grow);
            Undo.RecordObject(options, "Add Damage Numbers Option");
            options.offsetMin = new Vector2(options.offsetMin.x, options.offsetMin.y - grow);

            var toggle = SetupUtility.CreateButton("Damage Numbers", options, "Damage numbers: on", 24f, font, OptionColor);
            Undo.RegisterCreatedObjectUndo(toggle.gameObject, "Add Damage Numbers Option");
            SetupUtility.SetReference(menu, "damageNumbersButton", toggle);
            SetupUtility.SetReference(menu, "damageNumbersText", toggle.GetComponentInChildren<TMP_Text>(true));
            return true;
        }

        // Adds the Health shown/hidden row under the other options, the same way as the Comfort vignette row.
        private static bool AddHealthOption(PauseMenu menu, TMP_FontAsset font)
        {
            var serialized = new SerializedObject(menu);
            if (serialized.FindProperty("healthButton").objectReferenceValue != null) return false;
            if (serialized.FindProperty("panel").objectReferenceValue is not GameObject panelObject ||
                panelObject.transform is not RectTransform panel) return false;
            var options = panel.Find("Options") as RectTransform;
            if (options == null)
            {
                Debug.LogWarning("[Pause Menu Setup] The Pause Menu panel has no Options group, so the Health option wasn't added.", menu);
                return false;
            }

            const float grow = OptionRowHeight + OptionSpacing;
            Undo.RecordObject(panel, "Add Health Option");
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, panel.sizeDelta.y + grow);
            Undo.RecordObject(options, "Add Health Option");
            options.offsetMin = new Vector2(options.offsetMin.x, options.offsetMin.y - grow);

            var toggle = SetupUtility.CreateButton("Health", options, "Health: shown", 24f, font, OptionColor);
            Undo.RegisterCreatedObjectUndo(toggle.gameObject, "Add Health Option");
            SetupUtility.SetReference(menu, "healthButton", toggle);
            SetupUtility.SetReference(menu, "healthText", toggle.GetComponentInChildren<TMP_Text>(true));
            return true;
        }

        // Hidden on purpose, so nobody resets by accident: holding the title for 7 s (it slowly turns red) opens a box
        // over the whole menu that asks whether to reset all progress and start over. Cancel is on the left.
        private static bool AddResetProgress(PauseMenu menu, TMP_FontAsset font)
        {
            var serialized = new SerializedObject(menu);
            if (serialized.FindProperty("resetHold").objectReferenceValue != null) return false;
            if (serialized.FindProperty("panel").objectReferenceValue is not GameObject panelObject ||
                panelObject.transform is not RectTransform panel) return false;
            var title = panel.Find("Title") != null ? panel.Find("Title").GetComponent<TMP_Text>() : null;
            if (title == null)
            {
                Debug.LogWarning("[Pause Menu Setup] The Pause Menu panel has no Title text, so Reset Progress wasn't added.", menu);
                return false;
            }

            Undo.RecordObject(title, "Add Reset Progress");
            title.raycastTarget = true;
            var hold = Undo.AddComponent<PressAndHold>(title.gameObject);

            var box = SetupUtility.CreateRect("Reset Progress", panel);
            Undo.RegisterCreatedObjectUndo(box.gameObject, "Add Reset Progress");
            SetupUtility.Stretch(box, Vector2.zero, Vector2.zero);
            var background = box.gameObject.AddComponent<Image>();
            background.color = new Color(0.17f, 0.05f, 0.09f, 1f);
            // Catches the pointer, so nothing on the menu behind it can be pressed while it asks.
            background.raycastTarget = true;

            var heading = SetupUtility.CreateText("Heading", box, "Reset progress?", 34f, TextAlignmentOptions.Center, FontStyles.Bold, font);
            SetupUtility.TopBand(heading.rectTransform, 24f, 50f, 20f, 20f);
            var message = SetupUtility.CreateText("Message", box,
                "This deletes all your saved progress: your candies, everything you bought and every path you opened, " +
                "including your cloud save. Then the game starts over from the beginning.\n\nThis can't be undone.",
                23f, TextAlignmentOptions.Center, FontStyles.Normal, font);
            message.textWrappingMode = TextWrappingModes.Normal;
            SetupUtility.TopBand(message.rectTransform, 86f, 250f, 26f, 26f);
            SetupUtility.ShrinkToFit(message, 16f);

            var buttons = SetupUtility.CreateRect("Buttons", box);
            SetupUtility.BottomBand(buttons, 24f, 64f, 24f, 24f);
            var row = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 20f;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;
            var cancel = SetupUtility.CreateButton("Cancel", buttons, "Cancel", 26f, font, OptionColor);
            var confirm = SetupUtility.CreateButton("Reset", buttons, "Reset & restart", 24f, font, new Color(0.7f, 0.16f, 0.2f, 1f));
            box.gameObject.SetActive(false);

            SetupUtility.SetReference(menu, "resetHold", hold);
            SetupUtility.SetReference(menu, "resetConfirmPanel", box.gameObject);
            SetupUtility.SetReference(menu, "resetConfirmButton", confirm);
            SetupUtility.SetReference(menu, "resetCancelButton", cancel);
            return true;
        }

        /// <summary>
        /// Brings a Quit box made before up to date: two buttons, Re-Dream (was "To the Start") and Get a Rest, with Back
        /// as an arrow in the box's top left corner. Running it again changes nothing.
        /// </summary>
        [MenuItem("Tools/429 Game/Menu/Update Quit Choice")]
        public static void UpdateQuitChoice()
        {
            var menu = Object.FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include);
            if (menu == null)
            {
                Debug.LogError("[Pause Menu Setup] No Pause Menu in the open scene: run Set Up Pause Menu.");
                return;
            }
            bool changed = TidyQuitChoice(menu);
            if (changed) EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
            Debug.Log(changed
                ? "[Pause Menu Setup] The Quit box now has Re-Dream and Get a Rest, and Back is an arrow in its top left. Save the scene."
                : "[Pause Menu Setup] The Quit box is already up to date.", menu);
        }

        [MenuItem("Tools/429 Game/Menu/Update Quit Choice", true)]
        private static bool CanUpdateQuitChoice() => !EditorApplication.isPlaying;

        // Renames To the Start to Re-Dream (button and message), and moves Back out of the button row to the top left
        // corner as an arrow.
        private static bool TidyQuitChoice(PauseMenu menu)
        {
            var serialized = new SerializedObject(menu);
            if (serialized.FindProperty("quitPanel").objectReferenceValue is not GameObject boxObject ||
                boxObject.transform is not RectTransform box) return false;
            bool changed = false;

            var message = box.Find("Message") != null ? box.Find("Message").GetComponent<TMP_Text>() : null;
            if (message != null && message.text != QuitMessage)
            {
                Undo.RecordObject(message, "Update Quit Choice");
                message.text = QuitMessage;
                changed = true;
            }

            if (serialized.FindProperty("toStartButton").objectReferenceValue is Button toStart)
            {
                var label = toStart.GetComponentInChildren<TMP_Text>(true);
                if (label != null && label.text != "Re-Dream")
                {
                    Undo.RecordObject(label, "Update Quit Choice");
                    label.text = "Re-Dream";
                    Undo.RecordObject(toStart.gameObject, "Update Quit Choice");
                    toStart.gameObject.name = "Re-Dream";
                    changed = true;
                }
            }

            if (serialized.FindProperty("quitBackButton").objectReferenceValue is Button back && back.transform.parent != box)
            {
                MakeBackArrow(back, box);
                changed = true;
            }
            return changed;
        }

        // The Back button as an arrow in the box's top left corner.
        private static void MakeBackArrow(Button back, RectTransform box)
        {
            var rect = (RectTransform)back.transform;
            Undo.SetTransformParent(rect, box, "Back Arrow");
            Undo.RecordObject(rect, "Back Arrow");
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(14f, -14f);
            rect.sizeDelta = new Vector2(60f, 60f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;

            var label = back.GetComponentInChildren<TMP_Text>(true);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackArrowSprite);
            if (sprite == null)
            {
                // No icon: an arrow drawn with the font.
                if (label != null)
                {
                    Undo.RecordObject(label, "Back Arrow");
                    label.text = "<";
                }
                return;
            }
            if (label != null) Undo.DestroyObjectImmediate(label.gameObject);
            var icon = SetupUtility.CreateImage("Arrow", rect, Color.white);
            Undo.RegisterCreatedObjectUndo(icon.gameObject, "Back Arrow");
            icon.sprite = sprite;
            icon.preserveAspect = true;
            SetupUtility.Stretch(icon.rectTransform, new Vector2(8f, 8f), new Vector2(-8f, -8f));
        }

        // The box Quit opens, over the whole menu: Re-Dream (a new run from the very beginning) or Get a Rest (close
        // the game; it continues from the last checkpoint), and Back (an arrow in its top left corner).
        private static bool AddQuitChoice(PauseMenu menu, TMP_FontAsset font)
        {
            var serialized = new SerializedObject(menu);
            if (serialized.FindProperty("quitPanel").objectReferenceValue != null) return false;
            if (serialized.FindProperty("panel").objectReferenceValue is not GameObject panelObject ||
                panelObject.transform is not RectTransform panel) return false;

            var box = SetupUtility.CreateRect("Quit Choice", panel);
            Undo.RegisterCreatedObjectUndo(box.gameObject, "Add Quit Choice");
            SetupUtility.Stretch(box, Vector2.zero, Vector2.zero);
            var background = box.gameObject.AddComponent<Image>();
            background.color = new Color(0.08f, 0.06f, 0.14f, 1f);
            // Catches the pointer, so nothing on the menu behind it can be pressed while it asks.
            background.raycastTarget = true;

            var heading = SetupUtility.CreateText("Heading", box, "Leaving?", 34f, TextAlignmentOptions.Center, FontStyles.Bold, font);
            SetupUtility.TopBand(heading.rectTransform, 22f, 50f, 20f, 20f);
            var message = SetupUtility.CreateText("Message", box, QuitMessage, 22f, TextAlignmentOptions.Center, FontStyles.Normal, font);
            message.textWrappingMode = TextWrappingModes.Normal;
            SetupUtility.TopBand(message.rectTransform, 80f, 170f, 26f, 26f);
            SetupUtility.ShrinkToFit(message, 15f);

            var buttons = SetupUtility.CreateRect("Buttons", box);
            SetupUtility.BottomBand(buttons, 24f, 64f, 20f, 20f);
            var row = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 14f;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;
            var toStart = SetupUtility.CreateButton("Re-Dream", buttons, "Re-Dream", 24f, font, new Color(0.6f, 0.35f, 0.15f, 1f));
            var rest = SetupUtility.CreateButton("Get a Rest", buttons, "Get a Rest", 24f, font, new Color(0.6f, 0.22f, 0.28f, 1f));
            foreach (var text in buttons.GetComponentsInChildren<TMP_Text>(true)) SetupUtility.ShrinkToFit(text, 14f);
            var back = SetupUtility.CreateButton("Back", box, "<", 30f, font, OptionColor);
            MakeBackArrow(back, box);
            box.gameObject.SetActive(false);

            SetupUtility.SetReference(menu, "quitPanel", box.gameObject);
            SetupUtility.SetReference(menu, "toStartButton", toStart);
            SetupUtility.SetReference(menu, "restButton", rest);
            SetupUtility.SetReference(menu, "quitBackButton", back);
            return true;
        }

        // From the floor, the headset measures the player's real height, so the game's floor is the real floor. In
        // 'Device' mode the view sits at the XR Origin's fixed Camera Y Offset, however tall the player is.
        private static bool TrackFromFloor(XROrigin origin)
        {
            var serialized = new SerializedObject(origin);
            var mode = serialized.FindProperty("m_RequestedTrackingOriginMode");
            if (mode == null || mode.intValue == (int)XROrigin.TrackingOriginMode.Floor) return false;

            mode.intValue = (int)XROrigin.TrackingOriginMode.Floor;
            serialized.ApplyModifiedProperties();
            return true;
        }
    }
}
