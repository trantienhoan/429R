using System.Collections.Generic;
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

            var origin = Object.FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
            if (origin != null && origin.Camera != null) SetupUtility.SetReferenceIfEmpty(menu, "head", origin.Camera.transform);

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
    }
}
