using System.Collections.Generic;
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

        // The hit box over the board's face, in the board's units (1 = 1 mm): as big as the board, this deep (behind the
        // face, where nobody looks, so fast punches don't pass through it), its front this far in front of the face.
        private const float HitBoxDepth = 80f;
        private const float HitBoxFront = 5f;
        // On no ray's way: the hands' far-grab rays hit Default, UI and Teleport, so a box on UI would block them.
        private const string HitBoxLayer = "Ignore Raycast";

        // The knock a hard hit makes: short wood hits the furniture already uses.
        private static readonly string[] KnockSoundPaths =
        {
            "Assets/Audio/SFX/wood_get_hit_1.wav",
            "Assets/Audio/SFX/wood_get_hit_2.wav",
        };

        // What this menu puts on the board, once (the board remembers it: Menu Version). Hints for the tips already
        // there (matched by their exact text; only empty hints are filled), then new tips with their hints (only ones not
        // there yet). The user's own tip texts are never changed. Add a later batch as a new version.
        private const int TipsVersion = 1;
        private static readonly (string tip, string hint)[] Tips =
        {
            // The user's own tips: hints only.
            ("Eat as much candies as you want, this is a game", "Candy heals. Its wrap pays the Candy Shop."),
            ("Nothing to throw? Make a fist and start punching", "Squeeze grip and trigger with an empty hand."),
            ("Clean your room and watch out for the bug bite", "Smash the bugs before they bite."),
            ("DONT RUN\nDONT FEAR\nDONT GIVE UP", "Okay, dodging is allowed."),
            ("Drink Water", "For real. Take a break now and then."),
            ("Bump your fists together twice to spend your level-up points", "Beat bugs to earn points."),
            ("Shake a bottle before you throw it. Careful though!", "Needs 1 Throw Power. Too long: boom in your hand."),
            ("Hold B to open the Candy Shop", "Pay with candy wraps. It restocks often."),
            // New ones.
            ("Touch me for a hint", "Hit me hard for the next tip."),
            ("Fist Size unlocks flying punches", "1 point: jab. More points hit harder. 10: triple!"),
            ("Hold Y to open your bag", "Hold a thing to your chest to put it in."),
            ("Press A to pause", "Height, volume and more are in there."),
            ("Lollipop? Hold it at your mouth", "Keep it there for 3 seconds."),
            ("Squeeze the trigger on a hammer", "Its head grows and hits harder."),
            ("Two hands on a broom hit harder", "Pillow and sword too. They wear out faster."),
            ("Smoke a boong: 60 s power-up", "Hold it upright at your mouth, lit candle on the bowl."),
            ("Elon Boong: exploding punches", "Punches reach further and blow up on hit."),
            ("Bone Boong: tough as bone", "+7 Toughness for 60 seconds."),
            ("Green Boong: grow taller", "Flying punches go 5 m further too."),
            ("Bus Boong: move faster", "+7 Move Speed for 60 seconds."),
            ("Candle out? Shake it", "3 quick shakes light it again."),
            ("The game saves at checkpoints", "Pick your save slot to go on from there."),
            ("Mind the edge", "Falling off costs health."),
            ("Revive coins can save you", "Clover: get back up. All skulls: game over."),
            ("Hit the magic hat for a recipe", "Cook it in the cauldron for a laughing pumpkin."),
            ("Stone dice block the path", "Hit one to see its price. Pay with seeds."),
            ("Hit a chest to open it", "Touch the seed inside to take it."),
            ("Swing your arms to run", "Hold X and jog with your arms."),
            ("Skull Gun: Don't do it too fast", "Too many quick shots and it blows up."),
            ("Explosions hurt you too", "Throw them far, not at your feet."),
            ("Weapons wear out", "Weapon Efficiency makes them last longer."),
            ("Don't hug the Pumpkin Boss", "Too close and he spins. Dodge his pumpkins."),
            ("Throw Power makes throws explode", "Books, bottles, flip-flops. More points, bigger boom."),
        };

        /// <summary>
        /// Adds the tip board to the Knight prefab, facing the player as they come down the path (needs 2 Game Scene
        /// open, to measure where). If it has one already: fixes its fonts, gives the board its hit box if it has none,
        /// and, the first time, the knock sounds, a hint for each of the tips already there and the new tips. Running it
        /// again changes nothing; tips and hints deleted by hand stay deleted. Edit the tips on the Knight prefab's Tip
        /// Board component.
        /// </summary>
        [MenuItem("Tools/429 Game/Menu/Set Up Knight Tip Board")]
        public static void SetUp()
        {
            var root = PrefabUtility.LoadPrefabContents(KnightPath);
            try
            {
                var done = new List<string>();
                var tip = root.GetComponent<TipBoard>();
                if (tip == null)
                {
                    tip = Build(root);
                    if (tip == null) return;
                    done.Add("built the board above the knight's weapon in the middle of the path");
                }
                else
                {
                    int fixedFonts = SetupUtility.AssignMissingFonts(tip, SetupUtility.ResolveFont());
                    if (fixedFonts > 0) done.Add($"fixed {fixedFonts} fonts");
                }

                if (AddHitBox(tip)) done.Add("gave the board a hit box");
                AddTips(tip, done);

                if (done.Count == 0)
                {
                    Debug.Log("[Tip Board] The Knight's tip board is all set up. Nothing to change.");
                    return;
                }
                PrefabUtility.SaveAsPrefabAsset(root, KnightPath);
                Debug.Log($"[Tip Board] '{KnightPath}': {string.Join("; ", done)}. Edit the tips on its Tip Board component.");
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

        // The board itself, placed from the knight's spawn point in the open scene. Null if that isn't there.
        private static TipBoard Build(GameObject root)
        {
            Transform spawn = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == "Knight_pos") spawn = t;
            }
            if (spawn == null)
            {
                Debug.LogError("[Tip Board] No 'Knight_pos' in the open scene (open 2 Game Scene), so there's nothing to measure the board's place from.");
                return null;
            }

            var font = SetupUtility.ResolveFont();
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

            var text = SetupUtility.CreateText("Tip", rect, "High Recommended", 74f, TextAlignmentOptions.Center, FontStyles.Bold, font);
            text.textWrappingMode = TextWrappingModes.Normal;
            SetupUtility.Stretch(text.rectTransform, new Vector2(40f, 30f), new Vector2(-40f, -80f));
            SetupUtility.ShrinkToFit(text, 32f);

            SetupUtility.SetReference(tip, "board", rect, false);
            SetupUtility.SetReference(tip, "text", text, false);
            return tip;
        }

        // A thin box over the board's face, on a still (kinematic) body of its own with Tip Board Hit Box, so hits on
        // the board count (the board leaves the knight when the game starts). Only if it has none. True if added.
        private static bool AddHitBox(TipBoard tip)
        {
            var board = new SerializedObject(tip).FindProperty("board").objectReferenceValue as RectTransform;
            if (board == null)
            {
                Debug.LogWarning("[Tip Board] The Tip Board has no Board set, so it got no hit box.", tip);
                return false;
            }
            if (board.GetComponentInChildren<TipBoardHitBox>(true) != null) return false;

            var go = new GameObject("Hit Box");
            int layer = LayerMask.NameToLayer(HitBoxLayer);
            if (layer >= 0) go.layer = layer;
            go.transform.SetParent(board, false);
            var area = board.rect;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(area.center.x, area.center.y, HitBoxDepth * 0.5f - HitBoxFront);
            box.size = new Vector3(area.width, area.height, HitBoxDepth);
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            go.AddComponent<TipBoardHitBox>();
            return true;
        }

        // Once (Menu Version): the knock sounds if it has none, hints for the tips already there whose hint is empty,
        // and the new tips that aren't there yet.
        private static void AddTips(TipBoard tip, List<string> done)
        {
            var serialized = new SerializedObject(tip);
            var version = serialized.FindProperty("menuVersion");
            var tips = serialized.FindProperty("tips");
            var hints = serialized.FindProperty("hints");
            var sounds = serialized.FindProperty("knockSounds");
            if (version == null || tips == null || hints == null || sounds == null)
            {
                Debug.LogError("[Tip Board] The Tip Board script is missing its tip fields (is it up to date?).", tip);
                return;
            }
            if (version.intValue >= TipsVersion) return;

            if (sounds.arraySize == 0)
            {
                foreach (var path in KnockSoundPaths)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (clip == null) continue;
                    sounds.arraySize++;
                    sounds.GetArrayElementAtIndex(sounds.arraySize - 1).objectReferenceValue = clip;
                }
                if (sounds.arraySize > 0) done.Add($"{sounds.arraySize} knock sounds");
            }

            int added = 0;
            int hinted = 0;
            foreach (var (text, hint) in Tips)
            {
                int at = Find(tips, text);
                if (at < 0)
                {
                    at = tips.arraySize;
                    tips.arraySize++;
                    tips.GetArrayElementAtIndex(at).stringValue = text;
                    added++;
                }
                // A bigger array copies its last value into the new places: they're made empty.
                while (hints.arraySize < tips.arraySize)
                {
                    hints.arraySize++;
                    hints.GetArrayElementAtIndex(hints.arraySize - 1).stringValue = "";
                }
                var hintValue = hints.GetArrayElementAtIndex(at);
                if (!string.IsNullOrEmpty(hintValue.stringValue)) continue;
                hintValue.stringValue = hint;
                hinted++;
            }
            while (hints.arraySize < tips.arraySize)
            {
                hints.arraySize++;
                hints.GetArrayElementAtIndex(hints.arraySize - 1).stringValue = "";
            }
            version.intValue = TipsVersion;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            done.Add($"added {added} tips and {hinted} hints");
        }

        // Where a tip with exactly this text is (line ends written either way), or -1.
        private static int Find(SerializedProperty tips, string text)
        {
            string wanted = text.Replace("\r", "");
            for (int i = 0; i < tips.arraySize; i++)
            {
                if (tips.GetArrayElementAtIndex(i).stringValue.Replace("\r", "") == wanted) return i;
            }
            return -1;
        }
    }
}
