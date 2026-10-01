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

        /// <summary>
        /// Creates the pause menu, or adds what an existing one is missing (the Arm swing and Height options), puts
        /// Player Height on the XR Origin and makes the XR Origin track from the real floor.
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
