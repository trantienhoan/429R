using System.Linq;
using Game.Menus;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Menu > Set Up Walk Tutorial Signs. On the walk from where the player starts to the Fairy, two
    /// big wooden signs (like the Knight's tip board, 6 m wide): to the player's left how to move (the left stick), to their
    /// right how to teleport (push the right stick forward, aim, let go). Each stands on the ground just past the edge
    /// of the stone path (6.2 m out), 8.2 m to the side of where the player starts, facing them: turning the head left or
    /// right shows them.
    /// GAMESTAGES shows them in "Walk Tuttorial" and hides them in "Send death" (when the player reaches the Fairy). Like
    /// the enemy tutorial they're shown only the first time the game is played on this install (Once Per Install).
    /// Running it again builds them anew. Save the scene afterwards.
    /// </summary>
    public static class WalkTutorialSignsSetup
    {
        private const string RootName = "Walk Tutorial Signs";
        private const string Pictures = "Assets/M_Textures/Tutorial/";
        private static readonly Color FrameColor = new(0.45f, 0.30f, 0.16f, 1f);
        private static readonly Color WoodColor = new(0.17f, 0.10f, 0.05f, 0.95f);
        private static readonly Color TitleColor = new(1f, 0.8f, 0.3f, 1f);
        // Where the player starts (LevelREStartPos, facing the Fairy), and the signs: to the player's left and right.
        private static readonly Vector3 Start = new(-0.01f, 0f, -10.9f);
        // Just past the stone path's edge (6.2 m out on both sides), the sign's foot just above the ground: its middle is
        // half its height (800 units x Sign Scale) up.
        private static readonly Vector3 LeftSign = new(-8.2f, 2.42f, -10.9f);
        private static readonly Vector3 RightSign = new(8.2f, 2.42f, -10.9f);
        // Canvas units to metres: 1000 units = 6 m wide (4.8 m tall).
        private const float SignScale = 0.006f;

        [MenuItem("Tools/429 Game/Menu/Set Up Walk Tutorial Signs")]
        public static void SetUp()
        {
            var stages = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(f => f.FsmName == "GAMESTAGES");
            if (stages == null)
            {
                Debug.LogError("[Walk Tutorial Signs] No GAMESTAGES FSM in the open scene: open 2 Game Scene first. Nothing changed.");
                return;
            }

            var fsm = stages.Fsm;
            if (!fsm.Initialized) fsm.Init(stages);
            foreach (var s in fsm.States) s.LoadActions();
            // An earlier build: GAMESTAGES forgets it first (while it still exists), then it goes.
            foreach (var old in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                         .Where(t => t.parent == null && t.name == RootName && t.gameObject.scene == stages.gameObject.scene).ToArray())
            {
                foreach (var s in fsm.States)
                    s.Actions = s.Actions.Where(a => !(a is ActivateGameObject act && act.gameObject != null &&
                                                       act.gameObject.OwnerOption == OwnerDefaultOption.SpecifyGameObject &&
                                                       act.gameObject.GameObject != null && act.gameObject.GameObject.Value == old.gameObject)).ToArray();
                Undo.DestroyObjectImmediate(old.gameObject);
            }

            var font = SetupUtility.ResolveFont();
            var root = new GameObject(RootName);
            // In GAMESTAGES' scene (not a prefab open in Prefab Mode).
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, stages.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(root, "Create " + RootName);
            MakeSign(root.transform, "Move Sign", LeftSign, "MOVE", "Push the LEFT stick to walk", "Tutorial_4_Move.png", font);
            // Only the first time the game is played (remembered once the player reaches the Fairy).
            var once = root.AddComponent<OncePerInstall>();
            var onceSo = new SerializedObject(once);
            onceSo.FindProperty("key").stringValue = "WalkTutorialSigns";
            onceSo.ApplyModifiedPropertiesWithoutUndo();
            MakeSign(root.transform, "Teleport Sign", RightSign, "TELEPORT", "Push RIGHT stick up, aim, let go", "Tutorial_5_Teleport.png", font);
            // Hidden until the walk to the Fairy starts.
            root.SetActive(false);

            bool wired = ShowDuringWalk(stages, root);
            if (!wired) foreach (var s in fsm.States) s.SaveActions();
            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;
            Debug.Log("[Walk Tutorial Signs] Built the Move (left) and Teleport (right) signs" +
                      (wired ? "; GAMESTAGES shows them in Walk Tuttorial and hides them in Send death" : "") + ". Save the scene.");
        }

        [MenuItem("Tools/429 Game/Menu/Set Up Walk Tutorial Signs", true)]
        private static bool NotPlaying() => !EditorApplication.isPlaying;

        // A world-space sign 1 m wide: wooden frame, gold title, the picture, one line of text. Readable from the start.
        private static void MakeSign(Transform parent, string name, Vector3 position, string title, string line, string picture, TMP_FontAsset font)
        {
            var rect = SetupUtility.CreateWorldCanvas(name, parent, new Vector2(1000f, 800f), new Color(0f, 0f, 0f, 0f));
            var away = Vector3.ProjectOnPlane(position - Start, Vector3.up).normalized;
            rect.SetPositionAndRotation(position, Quaternion.LookRotation(away, Vector3.up));
            // Big: 6 m wide, read from 8 m away.
            rect.localScale = Vector3.one * SignScale;

            var frame = SetupUtility.CreateImage("Frame", rect, FrameColor);
            SetupUtility.Stretch(frame.rectTransform, Vector2.zero, Vector2.zero);
            var wood = SetupUtility.CreateImage("Wood", rect, WoodColor);
            SetupUtility.Stretch(wood.rectTransform, new Vector2(16f, 16f), new Vector2(-16f, -16f));

            var heading = SetupUtility.CreateText("Title", rect, title, 72f, TextAlignmentOptions.Center, FontStyles.Bold, font);
            heading.color = TitleColor;
            SetupUtility.TopBand(heading.rectTransform, 26f, 86f, 40f, 40f);

            var pictureRect = SetupUtility.CreateRect("Picture", rect);
            pictureRect.anchorMin = pictureRect.anchorMax = new Vector2(0.5f, 1f);
            pictureRect.pivot = new Vector2(0.5f, 1f);
            pictureRect.sizeDelta = new Vector2(880f, 495f);
            pictureRect.anchoredPosition = new Vector2(0f, -118f);
            var image = pictureRect.gameObject.AddComponent<RawImage>();
            image.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Pictures + picture);
            image.raycastTarget = false;
            if (image.texture == null) Debug.LogWarning("[Walk Tutorial Signs] No picture at " + Pictures + picture);

            var body = SetupUtility.CreateText("Line", rect, line, 44f, TextAlignmentOptions.Center, FontStyles.Bold, font);
            SetupUtility.BottomBand(body.rectTransform, 40f, 120f, 40f, 40f);
            SetupUtility.ShrinkToFit(body, 28f);
        }

        // GAMESTAGES: "Walk Tuttorial" switches the signs on, "Send death" (the Fairy is reached) switches them off.
        private static bool ShowDuringWalk(PlayMakerFSM stages, GameObject signs)
        {
            var fsm = stages.Fsm;
            var walk = fsm.States.FirstOrDefault(s => s.Name == "Walk Tuttorial");
            var reached = fsm.States.FirstOrDefault(s => s.Name == "Send death");
            if (walk == null || reached == null)
            {
                Debug.LogWarning("[Walk Tutorial Signs] GAMESTAGES has no 'Walk Tuttorial' or 'Send death' state, so the signs aren't switched on and off. Do it in PlayMaker.");
                return false;
            }
            SetSwitch(walk, signs, true);
            SetSwitch(reached, signs, false);
            foreach (var s in fsm.States) s.SaveActions();
            EditorUtility.SetDirty(stages);
            return true;
        }

        // An Activate Game Object for the signs, first in this state (before its waits and transitions).
        private static void SetSwitch(FsmState state, GameObject signs, bool on)
        {
            var kept = state.Actions.ToList();
            var activate = new ActivateGameObject();
            activate.Reset();
            activate.gameObject = new FsmOwnerDefault
            {
                OwnerOption = OwnerDefaultOption.SpecifyGameObject,
                GameObject = new FsmGameObject { Value = signs },
            };
            activate.activate = on;
            activate.recursive = false;
            kept.Insert(0, activate);
            state.Actions = kept.ToArray();
        }
    }
}
