using Game.Menus;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>Menu command that creates the health and safety notice shown at game start (Tools > 429 Game > Menu).</summary>
    public static class HealthWarningSetup
    {
        private static readonly Color PanelColor = new(0.08f, 0.06f, 0.12f, 0.96f);
        private static readonly Color TitleColor = new(1f, 0.8f, 0.3f, 1f);

        // Plain ASCII on purpose: the project's font atlas may not have bullets or dashes.
        private const string Body =
            "- Some people feel motion sickness, dizziness, nausea or eye strain in virtual reality. If you feel any " +
            "discomfort, stop playing and rest until you feel completely fine.\n\n" +
            "- A very small number of people may have seizures or blackouts triggered by flashing lights or patterns, " +
            "even with no history of them. If you or anyone in your family has had epilepsy, talk to a doctor before " +
            "playing. Stop right away if you notice twitching, blurred vision or confusion.\n\n" +
            "- Clear your play area of furniture, people and pets, and stay inside your boundary.\n\n" +
            "- Take a 10 to 15 minute break at least every 30 minutes. Don't play when you are tired or unwell.\n\n" +
            "- Children should play with an adult nearby.";

        /// <summary>
        /// Creates the Health Warning in the open scene, facing the player at start. If one exists, only fixes its
        /// fonts. Edit the text on its panel, then save the scene.
        /// </summary>
        [MenuItem("Tools/429 Game/Menu/Set Up Health Warning")]
        public static void SetUpHealthWarning()
        {
            var font = SetupUtility.ResolveFont();
            var existing = Object.FindAnyObjectByType<HealthWarning>(FindObjectsInactive.Include);
            if (existing != null)
            {
                int fixedFonts = SetupUtility.AssignMissingFonts(existing, font);
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("[Health Warning Setup] The scene already has a Health Warning" +
                          (fixedFonts > 0 ? $"; fixed {fixedFonts} fonts." : ". Nothing to change."));
                return;
            }

            var go = new GameObject("Health Warning");
            Undo.RegisterCreatedObjectUndo(go, "Create Health Warning");
            var warning = go.AddComponent<HealthWarning>();

            var rect = SetupUtility.CreateWorldCanvas("Health Warning Panel", go.transform, new Vector2(900f, 680f), PanelColor);
            var group = rect.gameObject.AddComponent<CanvasGroup>();

            var title = SetupUtility.CreateText("Title", rect, "HEALTH & SAFETY WARNING", 44f, TextAlignmentOptions.Center, FontStyles.Bold, font);
            title.color = TitleColor;
            SetupUtility.TopBand(title.rectTransform, 30f, 60f, 40f, 40f);

            var body = SetupUtility.CreateText("Body", rect, Body, 26f, TextAlignmentOptions.TopLeft, FontStyles.Normal, font);
            SetupUtility.TopBand(body.rectTransform, 112f, 470f, 54f, 54f);
            SetupUtility.ShrinkToFit(body, 18f);

            var footer = SetupUtility.CreateText("Continue", rect, "Pull either trigger to continue", 28f, TextAlignmentOptions.Center, FontStyles.Bold, font);
            footer.color = TitleColor;
            SetupUtility.BottomBand(footer.rectTransform, 28f, 48f, 40f, 40f);

            SetupUtility.SetReference(warning, "panel", group);
            SetupUtility.SetReference(warning, "continueText", footer);
            var origin = Object.FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
            if (origin != null && origin.Camera != null) SetupUtility.SetReferenceIfEmpty(warning, "head", origin.Camera.transform);
            // Hidden in the editor; the Health Warning shows it when the game starts.
            rect.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = go;
            Debug.Log("[Health Warning Setup] Created 'Health Warning' in the scene. It shows once per launch, " +
                      "in front of the player; edit the text on its panel, then save the scene.");
        }
    }
}
