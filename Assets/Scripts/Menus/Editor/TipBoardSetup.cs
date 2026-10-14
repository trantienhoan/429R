using Game.Menus;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>Menu command that puts the tip board on the knight at the start (Tools > 429 Game > Menu).</summary>
    public static class TipBoardSetup
    {
        private const string KnightPath = "Assets/Prefabs/Player/Knight.prefab";
        // Where the board goes: the middle of the path, just above the weapon the knight holds across it (measured with
        // the knight standing at its spawn point, Knight_pos, under GAMESTAGES' Fairy_Pos).
        private static readonly Vector3 BoardCenter = new(0f, 1.8f, -6.95f);
        private static readonly Vector2 BoardSize = new(1000f, 460f);
        // About 1 x 0.46 m.
        private const float MetresPerUnit = 0.001f;
        private static readonly Color FrameColor = new(0.45f, 0.3f, 0.16f, 1f);
        private static readonly Color WoodColor = new(0.17f, 0.1f, 0.05f, 0.95f);
        private static readonly Color TitleColor = new(1f, 0.8f, 0.3f, 1f);

        /// <summary>
        /// Adds the tip board to the Knight prefab, facing the player as they come down the path. If it has one, only
        /// fixes its fonts. Edit the tips on the Knight prefab's Tip Board component.
        /// </summary>
        [MenuItem("Tools/429 Game/Menu/Set Up Knight Tip Board")]
        public static void SetUp()
        {
            Transform spawn = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == "Knight_pos") spawn = t;
            }
            if (spawn == null)
            {
                Debug.LogError("[Tip Board] No 'Knight_pos' in the open scene (open 2 Game Scene), so there's nothing to measure the board's place from.");
                return;
            }

            var font = SetupUtility.ResolveFont();
            var root = PrefabUtility.LoadPrefabContents(KnightPath);
            try
            {
                var existing = root.GetComponent<TipBoard>();
                if (existing != null)
                {
                    int fixedFonts = SetupUtility.AssignMissingFonts(existing, font);
                    if (fixedFonts > 0) PrefabUtility.SaveAsPrefabAsset(root, KnightPath);
                    Debug.Log("[Tip Board] The Knight already has a tip board" + (fixedFonts > 0 ? $"; fixed {fixedFonts} fonts." : ". Nothing to change."));
                    return;
                }

                var tip = root.AddComponent<TipBoard>();

                // The board's place and turn in the knight's own space, from where the knight stands when it appears.
                var knightAtSpawn = Matrix4x4.TRS(spawn.position, spawn.rotation, root.transform.localScale);
                var go = new GameObject("Tip Board", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                int uiLayer = LayerMask.NameToLayer("UI");
                if (uiLayer >= 0) go.layer = uiLayer;
                var rect = (RectTransform)go.transform;
                rect.SetParent(root.transform, false);
                rect.sizeDelta = BoardSize;
                rect.localPosition = knightAtSpawn.inverse.MultiplyPoint3x4(BoardCenter);
                // Readable from the path: a world-space canvas reads right when its forward points away from the viewer.
                rect.localRotation = Quaternion.Inverse(spawn.rotation);
                rect.localScale = Vector3.one * (MetresPerUnit / root.transform.localScale.x);
                go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;

                var frame = SetupUtility.CreateImage("Frame", rect, FrameColor);
                SetupUtility.Stretch(frame.rectTransform, Vector2.zero, Vector2.zero);
                var wood = SetupUtility.CreateImage("Wood", rect, WoodColor);
                SetupUtility.Stretch(wood.rectTransform, new Vector2(14f, 14f), new Vector2(-14f, -14f));

                var title = SetupUtility.CreateText("Title", rect, "TIP", 52f, TextAlignmentOptions.Center, FontStyles.Bold, font);
                title.color = TitleColor;
                SetupUtility.TopBand(title.rectTransform, 22f, 52f, 40f, 40f);

                var text = SetupUtility.CreateText("Tip", rect, "You should be this High", 74f, TextAlignmentOptions.Center, FontStyles.Bold, font);
                text.textWrappingMode = TextWrappingModes.Normal;
                SetupUtility.Stretch(text.rectTransform, new Vector2(40f, 30f), new Vector2(-40f, -80f));
                SetupUtility.ShrinkToFit(text, 32f);

                SetupUtility.SetReference(tip, "board", rect, false);
                SetupUtility.SetReference(tip, "text", text, false);
                PrefabUtility.SaveAsPrefabAsset(root, KnightPath);
                Debug.Log($"[Tip Board] Added a tip board to '{KnightPath}', above the knight's weapon in the middle of the path. " +
                          "Edit the tips on its Tip Board component.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Tools/429 Game/Menu/Set Up Knight Tip Board", true)]
        private static bool CanSetUp()
        {
            // Changes made while playing are thrown away.
            return !EditorApplication.isPlaying;
        }
    }
}
