using Game.Menus;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>Menu command that creates the end-of-game congratulations notice (Tools > 429 Game > Menu).</summary>
    public static class CongratulationWarningSetup
    {
        private static readonly Color PanelColor = new(0.08f, 0.06f, 0.12f, 0.96f);
        private static readonly Color TitleColor = new(1f, 0.8f, 0.3f, 1f);

        // Plain ASCII on purpose: the project's font atlas may not have bullets or dashes.
        private const string Body =
            "You have achieved something truly special. I don't even know how you did it, but here you are. " +
            "Good job!\n\n" +
            "Thank you for playing Four Twenty Knight.\n\n" +
            "New game content is coming soon. When it arrives, you can find it by breaking the cube in the " +
            "first stage.\n\n" +
            "See you soon!";

        /// <summary>
        /// Creates the Congratulation Warning in the open scene. It stays hidden until the "Show Congratulation
        /// Warning" action shows it (the Thank_You_GAME_OVER prefab does, as its song starts). If one exists, only
        /// fixes its fonts. Edit the text on its panel, then save the scene.
        /// </summary>
        [MenuItem("Tools/429 Game/Menu/Set Up Congratulation Warning")]
        public static void SetUpCongratulationWarning()
        {
            var font = SetupUtility.ResolveFont();
            var existing = Object.FindAnyObjectByType<CongratulationWarning>(FindObjectsInactive.Include);
            if (existing != null)
            {
                int fixedFonts = SetupUtility.AssignMissingFonts(existing, font);
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("[Congratulation Warning Setup] The scene already has a Congratulation Warning" +
                          (fixedFonts > 0 ? $"; fixed {fixedFonts} fonts." : ". Nothing to change."));
                return;
            }

            var go = new GameObject("Congratulation Warning");
            Undo.RegisterCreatedObjectUndo(go, "Create Congratulation Warning");
            var warning = go.AddComponent<CongratulationWarning>();

            var rect = SetupUtility.CreateWorldCanvas("Congratulation Warning Panel", go.transform, new Vector2(900f, 680f), PanelColor);
            var group = rect.gameObject.AddComponent<CanvasGroup>();

            var title = SetupUtility.CreateText("Title", rect, "CONGRATULATIONS!", 52f, TextAlignmentOptions.Center, FontStyles.Bold, font);
            title.color = TitleColor;
            SetupUtility.TopBand(title.rectTransform, 30f, 70f, 40f, 40f);

            var body = SetupUtility.CreateText("Body", rect, Body, 30f, TextAlignmentOptions.Center, FontStyles.Normal, font);
            SetupUtility.TopBand(body.rectTransform, 125f, 455f, 60f, 60f);
            SetupUtility.ShrinkToFit(body, 20f);

            var footer = SetupUtility.CreateText("Continue", rect, "Pull either trigger to continue", 28f, TextAlignmentOptions.Center, FontStyles.Bold, font);
            footer.color = TitleColor;
            SetupUtility.BottomBand(footer.rectTransform, 28f, 48f, 40f, 40f);

            SetupUtility.SetReference(warning, "panel", group);
            SetupUtility.SetReference(warning, "continueText", footer);
            var origin = Object.FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
            if (origin != null && origin.Camera != null) SetupUtility.SetReferenceIfEmpty(warning, "head", origin.Camera.transform);
            // Hidden in the editor; the Congratulation Warning shows it when it's asked to.
            rect.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = go;
            Debug.Log("[Congratulation Warning Setup] Created 'Congratulation Warning' in the scene. The \"Show " +
                      "Congratulation Warning\" action shows it; edit the text on its panel, then save the scene.");
        }
    }
}
