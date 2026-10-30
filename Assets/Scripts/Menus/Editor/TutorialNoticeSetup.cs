using Game.Menus;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>Menu command that creates the tutorial-in-pages notice (Tools > 429 Game > Menu).</summary>
    public static class TutorialNoticeSetup
    {
        private static readonly Color PanelColor = new(0.08f, 0.06f, 0.12f, 0.96f);
        private static readonly Color TitleColor = new(1f, 0.8f, 0.3f, 1f);
        private static readonly Color PageColor = new(1f, 0.8f, 0.3f, 0.55f);
        // The Knight's tip board: a wooden frame round dark planks.
        private static readonly Color FrameColor = new(0.45f, 0.30f, 0.16f, 1f);
        private static readonly Color WoodColor = new(0.17f, 0.10f, 0.05f, 0.95f);
        private const string PageSoundPath = "Assets/Audio/Kenney Audio/kenney_interface-sounds/Audio/click_002.ogg";
        private const string ArrowPath = "Assets/M_Textures/Tutorial/Tutorial_Arrow_Down.png";

        /// <summary>
        /// Creates the Tutorial Notice in the open scene (a panel with a title, a 16:9 picture, text, "Pull a trigger..."
        /// and the page number). It stays hidden until the Show Notice action shows it. Running it again builds it anew
        /// (its settings are kept); save the scene afterwards.
        /// </summary>
        [MenuItem("Tools/429 Game/Menu/Set Up Tutorial Notice")]
        public static void SetUpTutorialNotice()
        {
            var font = SetupUtility.ResolveFont();
            var existing = Object.FindAnyObjectByType<TutorialNotice>(FindObjectsInactive.Include);
            string kept = existing != null ? EditorJsonUtility.ToJson(existing) : null;
            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

            var go = new GameObject("Tutorial Notice");
            Undo.RegisterCreatedObjectUndo(go, "Create Tutorial Notice");
            var notice = go.AddComponent<TutorialNotice>();
            if (kept != null) EditorJsonUtility.FromJsonOverwrite(kept, notice);

            var rect = SetupUtility.CreateWorldCanvas("Tutorial Notice Panel", go.transform, new Vector2(880f, 800f), PanelColor);
            var group = rect.gameObject.AddComponent<CanvasGroup>();
            // For pages floating above something: a wooden sign (behind the text, in front of the dark background).
            var frame = SetupUtility.CreateImage("Board Frame", rect, FrameColor);
            SetupUtility.Stretch(frame.rectTransform, Vector2.zero, Vector2.zero);
            var wood = SetupUtility.CreateImage("Board Wood", rect, WoodColor);
            SetupUtility.Stretch(wood.rectTransform, new Vector2(14f, 14f), new Vector2(-14f, -14f));
            frame.enabled = false;
            wood.enabled = false;

            var title = SetupUtility.CreateText("Title", rect, "THIS IS YOUR ENEMY!", 50f, TextAlignmentOptions.Center, FontStyles.Bold, font);
            title.color = TitleColor;
            SetupUtility.TopBand(title.rectTransform, 24f, 64f, 30f, 30f);
            SetupUtility.ShrinkToFit(title, 34f);

            // 16:9, under the title.
            var pictureRect = SetupUtility.CreateRect("Picture", rect);
            pictureRect.anchorMin = pictureRect.anchorMax = new Vector2(0.5f, 1f);
            pictureRect.pivot = new Vector2(0.5f, 1f);
            pictureRect.sizeDelta = new Vector2(768f, 432f);
            pictureRect.anchoredPosition = new Vector2(0f, -96f);
            var picture = pictureRect.gameObject.AddComponent<RawImage>();
            picture.raycastTarget = false;

            // Plain ASCII on purpose: the project's font atlas may not have bullets or dashes.
            var body = SetupUtility.CreateText("Body", rect, "", 30f, TextAlignmentOptions.Top, FontStyles.Normal, font);
            SetupUtility.TopBand(body.rectTransform, 542f, 160f, 50f, 50f);
            SetupUtility.ShrinkToFit(body, 20f);

            var footer = SetupUtility.CreateText("Continue", rect, "Pull a trigger for the next page", 26f, TextAlignmentOptions.Center, FontStyles.Bold, font);
            footer.color = TitleColor;
            SetupUtility.BottomBand(footer.rectTransform, 22f, 44f, 120f, 120f);

            var pageText = SetupUtility.CreateText("Page", rect, "1 / 3", 24f, TextAlignmentOptions.Right, FontStyles.Bold, font);
            pageText.color = PageColor;
            SetupUtility.BottomBand(pageText.rectTransform, 24f, 40f, 700f, 30f);

            SetupUtility.SetReference(notice, "panel", group);
            SetupUtility.SetReference(notice, "title", title);
            SetupUtility.SetReference(notice, "picture", picture);
            SetupUtility.SetReference(notice, "body", body);
            SetupUtility.SetReference(notice, "continueText", footer);
            SetupUtility.SetReference(notice, "pageText", pageText);

            // Under the panel, pointing down at what a page floats above (the notice bobs it).
            var arrowRect = SetupUtility.CreateRect("Arrow", rect);
            arrowRect.anchorMin = arrowRect.anchorMax = new Vector2(0.5f, 0f);
            arrowRect.pivot = new Vector2(0.5f, 1f);
            arrowRect.sizeDelta = new Vector2(130f, 130f);
            arrowRect.anchoredPosition = new Vector2(0f, -12f);
            var arrowImage = arrowRect.gameObject.AddComponent<RawImage>();
            arrowImage.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ArrowPath);
            arrowImage.raycastTarget = false;
            arrowRect.gameObject.SetActive(false);
            SetupUtility.SetReference(notice, "arrow", arrowRect);
            var background = rect.Find("Background");
            if (background != null) SetupUtility.SetReference(notice, "background", background.GetComponent<Image>());
            SetupUtility.SetReference(notice, "boardFrame", frame);
            SetupUtility.SetReference(notice, "boardWood", wood);
            var origin = Object.FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
            if (origin != null && origin.Camera != null) SetupUtility.SetReference(notice, "head", origin.Camera.transform);
            SetupUtility.SetReferenceIfEmpty(notice, "pageSound", AssetDatabase.LoadAssetAtPath<AudioClip>(PageSoundPath));
            // Hidden in the editor; Show Notice shows it.
            rect.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = go;
            Debug.Log("[Tutorial Notice Setup] " + (kept != null ? "Rebuilt" : "Created") + " 'Tutorial Notice' in the scene. " +
                      "The Show Notice action shows it; save the scene.");
        }

        [MenuItem("Tools/429 Game/Menu/Set Up Tutorial Notice", true)]
        private static bool NotPlaying() => !EditorApplication.isPlaying;

        /// <summary>
        /// Forgets which first-time tutorials were seen (the enemy tutorial and the walk tutorial signs), so they show again,
        /// e.g. to test them: on this computer, and in Play mode also in the save slot being played (each save slot keeps
        /// its own; a new save slot always shows them).
        /// </summary>
        [MenuItem("Tools/429 Game/Menu/Show Tutorials Again")]
        public static void ShowTutorialsAgain()
        {
            ShowOnce.ForgetAll();
            Debug.Log("[Tutorial Notice] The first-time tutorials (enemy tutorial, walk signs) will show again" +
                      (EditorApplication.isPlaying ? " (also in the save slot being played)." : " (on this computer; each save slot keeps its own, so in Play mode use this menu again or start a new slot)."));
        }
    }
}
