using System.Collections.Generic;
using Game.Combat;
using Game.Enemies;
using Game.Leveling;
using HutongGames.PlayMaker.Actions;
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
    /// <summary>Menu command that sets up the player's level system (Tools > 429 Game > Player).</summary>
    public static class LevelSetupMenu
    {
        private static readonly Color PanelColor = new(0.08f, 0.06f, 0.12f, 0.96f);
        private static readonly Color TitleColor = new(1f, 0.8f, 0.3f, 1f);
        private static readonly Color GoodColor = new(0.55f, 1f, 0.55f, 1f);
        private static readonly Color FaintColor = new(0.78f, 0.8f, 0.9f, 1f);
        private static readonly Color PlusColor = new(0.22f, 0.62f, 0.32f, 1f);

        // The menu's layout: the level, points and EXP bar above the six stat rows, the hint below them.
        private const float MenuWidth = 640f;
        private const float RowHeight = 84f;
        private const float RowSpacing = 8f;
        private const float RowsTop = 132f;
        private const float RowsBottom = 62f;

        // EXP by the kind of enemy.
        private const int SmallExp = 1;
        private const int NormalExp = 2;
        private const int MiniBossExp = 5;
        private const int BossExp = 50;

        /// <summary>
        /// Sets up the level system in the open scene: the Player Level, the Level Up menu and notice, Fist Bump on the
        /// XR Origin (two fists bumped twice opens the menu), the EXP each enemy prefab gives (where it's still 0), and
        /// Player Toughness on the PlayerHealth FSM's damage. Running it again only adds what's missing.
        /// </summary>
        [MenuItem("Tools/429 Game/Player/Set Up Level System")]
        public static void SetUpLevelSystem()
        {
            var notes = new List<string>();
            var font = SetupUtility.ResolveFont();
            var origin = Object.FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
            var head = origin != null && origin.Camera != null ? origin.Camera.transform : null;

            var level = Object.FindAnyObjectByType<PlayerLevel>(FindObjectsInactive.Include);
            if (level == null)
            {
                var go = new GameObject("Player Level");
                Undo.RegisterCreatedObjectUndo(go, "Create Player Level");
                level = go.AddComponent<PlayerLevel>();
                notes.Add("created 'Player Level' in the scene");
            }

            var menu = Object.FindAnyObjectByType<LevelUpMenu>(FindObjectsInactive.Include);
            if (menu == null)
            {
                menu = CreateMenu(level.transform, font);
                notes.Add("created the Level Up menu");
            }
            else if (SetupUtility.AssignMissingFonts(menu, font) > 0)
            {
                notes.Add("fixed the Level Up menu's fonts");
            }

            var notice = Object.FindAnyObjectByType<LevelUpNotice>(FindObjectsInactive.Include);
            if (notice == null)
            {
                notice = CreateNotice(level.transform, font);
                notes.Add("created the Level Up notice");
            }
            else if (SetupUtility.AssignMissingFonts(notice, font) > 0)
            {
                notes.Add("fixed the Level Up notice's fonts");
            }

            if (head != null)
            {
                SetupUtility.SetReferenceIfEmpty(menu, "head", head);
                SetupUtility.SetReferenceIfEmpty(notice, "head", head);
            }

            if (origin == null)
            {
                notes.Add("WARNING: no XR Origin in the scene, so there's nothing to put Fist Bump on");
            }
            else if (!origin.TryGetComponent<FistBump>(out _))
            {
                Undo.AddComponent<FistBump>(origin.gameObject);
                notes.Add($"added Fist Bump to '{origin.name}'");
            }
            if (Object.FindObjectsByType<Fist>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length < 2)
                notes.Add("WARNING: the scene needs a Fist on each controller for the fist bump that opens the menu");

            int toughness = TickPlayerToughness();
            if (toughness > 0) notes.Add($"ticked Player Toughness on {toughness} damage action(s) of the PlayerHealth FSM");

            int exp = SetEnemyExp();
            if (exp > 0) notes.Add($"set the EXP of {exp} enemy prefab(s) (small {SmallExp}, normal {NormalExp}, mini boss {MiniBossExp}, pumpkin_boss_Z {BossExp})");

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = level.gameObject;
            Debug.Log("[Level Setup] Done: " + (notes.Count > 0 ? string.Join("; ", notes) : "everything was already set up") +
                      ". Levels and what a point gives are on 'Player Level'; each enemy's EXP is on its Enemy Hit Damage. Save the scene.",
                level);
        }

        [MenuItem("Tools/429 Game/Player/Set Up Level System", true)]
        private static bool CanSetUp()
        {
            // Changes made while playing are thrown away.
            return !EditorApplication.isPlaying;
        }

        private static LevelUpMenu CreateMenu(Transform parent, TMP_FontAsset font)
        {
            var go = new GameObject("Level Up Menu");
            Undo.RegisterCreatedObjectUndo(go, "Create Level Up Menu");
            go.transform.SetParent(parent, false);
            var menu = go.AddComponent<LevelUpMenu>();

            float height = RowsTop + RowsBottom + PlayerLevel.StatCount * RowHeight + (PlayerLevel.StatCount - 1) * RowSpacing;
            var rect = SetupUtility.CreateWorldCanvas("Level Up Panel", go.transform, new Vector2(MenuWidth, height), PanelColor);
            // The whole panel counts as "on the menu" for a click, not only its buttons.
            foreach (var image in rect.GetComponentsInChildren<Image>(true)) image.raycastTarget = true;

            var levelText = SetupUtility.CreateText("Level", rect, "LEVEL 0", 40f, TextAlignmentOptions.Left, FontStyles.Bold, font);
            levelText.color = TitleColor;
            SetupUtility.TopBand(levelText.rectTransform, 18f, 52f, 28f, 300f);

            var pointsText = SetupUtility.CreateText("Points", rect, "0 points to spend", 24f, TextAlignmentOptions.Right, FontStyles.Bold, font);
            pointsText.color = GoodColor;
            SetupUtility.TopBand(pointsText.rectTransform, 24f, 44f, 330f, 28f);

            var bar = SetupUtility.CreateImage("EXP Bar", rect, new Color(1f, 1f, 1f, 0.12f));
            SetupUtility.TopBand(bar.rectTransform, 80f, 34f, 28f, 28f);
            var fill = SetupUtility.CreateImage("Fill", bar.transform, new Color(0.35f, 0.62f, 1f, 0.9f));
            SetupUtility.Stretch(fill.rectTransform, Vector2.zero, Vector2.zero);
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            var expText = SetupUtility.CreateText("EXP", bar.transform, "EXP 0 / 100", 20f, TextAlignmentOptions.Center, FontStyles.Bold, font);
            SetupUtility.Stretch(expText.rectTransform, Vector2.zero, Vector2.zero);

            var rows = SetupUtility.CreateRect("Rows", rect);
            SetupUtility.Stretch(rows, new Vector2(20f, RowsBottom), new Vector2(-20f, -RowsTop));
            var list = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            list.spacing = RowSpacing;
            list.childAlignment = TextAnchor.UpperLeft;
            list.childControlWidth = true;
            list.childControlHeight = true;
            list.childForceExpandWidth = true;
            list.childForceExpandHeight = false;

            var row = SetupUtility.CreateImage("Row Template", rows, new Color(1f, 1f, 1f, 0.08f));
            SetupUtility.SetLayout(row.gameObject, preferredHeight: RowHeight).minHeight = RowHeight;
            var line = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            line.padding = new RectOffset(16, 10, 8, 8);
            line.spacing = 12f;
            line.childAlignment = TextAnchor.MiddleLeft;
            line.childControlWidth = true;
            line.childControlHeight = true;
            line.childForceExpandWidth = false;
            line.childForceExpandHeight = true;

            var texts = SetupUtility.CreateRect("Texts", row.transform);
            SetupUtility.SetLayout(texts.gameObject, flexibleWidth: 1f);
            var stack = texts.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.spacing = 0f;
            stack.childAlignment = TextAnchor.MiddleLeft;
            stack.childControlWidth = true;
            stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = true;
            var label = SetupUtility.CreateText("Name", texts, "Stat", 26f, TextAlignmentOptions.Left, FontStyles.Bold, font);
            var effect = SetupUtility.CreateText("Effect", texts, "+0", 19f, TextAlignmentOptions.Left, FontStyles.Normal, font);
            effect.color = FaintColor;

            var points = SetupUtility.CreateText("Points", row.transform, "0 / 10", 24f, TextAlignmentOptions.Center, FontStyles.Bold, font);
            SetupUtility.SetLayout(points.gameObject, preferredWidth: 96f);

            var plus = SetupUtility.CreateButton("Plus", row.transform, "+", 44f, font, PlusColor);
            SetupUtility.SetLayout(plus.gameObject, preferredWidth: 72f);
            // Brighter while it's pointed at, darker while it's pressed, grey when there's nothing to spend.
            var colors = plus.colors;
            colors.normalColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = colors.normalColor;
            colors.pressedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.45f);
            plus.colors = colors;

            var rowView = row.gameObject.AddComponent<LevelUpStatRow>();
            SetupUtility.SetReference(rowView, "label", label);
            SetupUtility.SetReference(rowView, "effect", effect);
            SetupUtility.SetReference(rowView, "points", points);
            SetupUtility.SetReference(rowView, "plusButton", plus);
            foreach (var text in row.GetComponentsInChildren<TMP_Text>(true))
                SetupUtility.ShrinkToFit(text, 14f);
            row.gameObject.SetActive(false);

            var hint = SetupUtility.CreateText("Hint", rect, "Point at + and pull the trigger. Click anywhere else to close.", 19f,
                TextAlignmentOptions.Center, FontStyles.Italic, font);
            hint.color = FaintColor;
            SetupUtility.BottomBand(hint.rectTransform, 12f, 40f, 20f, 20f);

            SetupUtility.SetReference(menu, "panel", rect);
            SetupUtility.SetReference(menu, "levelText", levelText);
            SetupUtility.SetReference(menu, "pointsText", pointsText);
            SetupUtility.SetReference(menu, "expText", expText);
            SetupUtility.SetReference(menu, "expFill", fill.rectTransform);
            SetupUtility.SetReference(menu, "rowTemplate", rowView);
            SetupUtility.SetReference(menu, "rowParent", rows);
            // Hidden in the editor; two fists bumped twice open it.
            rect.gameObject.SetActive(false);
            return menu;
        }

        private static LevelUpNotice CreateNotice(Transform parent, TMP_FontAsset font)
        {
            var go = new GameObject("Level Up Notice");
            Undo.RegisterCreatedObjectUndo(go, "Create Level Up Notice");
            go.transform.SetParent(parent, false);
            var notice = go.AddComponent<LevelUpNotice>();

            var rect = SetupUtility.CreateWorldCanvas("Level Up Notice Panel", go.transform, new Vector2(820f, 400f), PanelColor);
            var group = rect.gameObject.AddComponent<CanvasGroup>();

            var title = SetupUtility.CreateText("Title", rect, "LEVEL UP!", 52f, TextAlignmentOptions.Center, FontStyles.Bold, font);
            title.color = TitleColor;
            SetupUtility.TopBand(title.rectTransform, 26f, 68f, 40f, 40f);

            // Plain ASCII on purpose: the project's font atlas may not have bullets or dashes.
            var body = SetupUtility.CreateText("Body", rect, "", 28f, TextAlignmentOptions.Top, FontStyles.Normal, font);
            SetupUtility.TopBand(body.rectTransform, 110f, 210f, 50f, 50f);
            SetupUtility.ShrinkToFit(body, 18f);

            var footer = SetupUtility.CreateText("Continue", rect, "Pull either trigger to continue", 26f, TextAlignmentOptions.Center, FontStyles.Bold, font);
            footer.color = TitleColor;
            SetupUtility.BottomBand(footer.rectTransform, 22f, 46f, 40f, 40f);

            SetupUtility.SetReference(notice, "panel", group);
            SetupUtility.SetReference(notice, "title", title);
            SetupUtility.SetReference(notice, "body", body);
            SetupUtility.SetReference(notice, "continueText", footer);
            // Hidden in the editor; the notice shows it at a level up.
            rect.gameObject.SetActive(false);
            return notice;
        }

        // Damage to the player (the PlayerHealth FSM's Float Subtract Random actions on PlayerHealth) is cut by Toughness.
        private static int TickPlayerToughness()
        {
            int ticked = 0;
            foreach (var fsm in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (fsm.FsmName != "PlayerHealth") continue;
                if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);

                int inFsm = 0;
                foreach (var state in fsm.FsmStates)
                {
                    state.LoadActions();
                    bool changed = false;
                    foreach (var action in state.Actions)
                    {
                        if (action is not FloatSubtractRandom damage) continue;
                        if (damage.floatVariable == null || damage.floatVariable.Name != "PlayerHealth") continue;
                        if (damage.playerToughness != null && damage.playerToughness.Value) continue;
                        damage.playerToughness = true;
                        changed = true;
                        inFsm++;
                    }
                    if (changed) state.SaveActions();
                }
                if (inFsm == 0) continue;
                EditorUtility.SetDirty(fsm);
                ticked += inFsm;
            }
            return ticked;
        }

        // Fills in the EXP of every enemy prefab that still gives none: by its name, small / normal / mini boss / the boss.
        private static int SetEnemyExp()
        {
            int set = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab == null) continue;

                Component enemy = prefab.GetComponent<EnemyHitDamage>();
                int exp = EnemyExp(prefab.name);
                if (enemy == null)
                {
                    enemy = prefab.GetComponent<GhostBalloon>();
                    exp = GhostExp(prefab.name);
                }
                if (enemy == null) continue;

                var serialized = new SerializedObject(enemy);
                var property = serialized.FindProperty("exp");
                if (property == null || property.intValue == exp) continue;
                // A number set by hand is kept. One a prefab variant only has from the prefab it's made from isn't its own.
                bool inherited = PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.Variant && !property.prefabOverride;
                if (property.intValue != 0 && !inherited) continue;
                property.intValue = exp;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(prefab);
                set++;
            }
            return set;
        }

        private static int EnemyExp(string prefabName)
        {
            string lower = prefabName.ToLowerInvariant();
            if (lower.Contains("pumpkin_boss_z")) return BossExp;
            // TheSpider, TheBee, TheCockroach, TheFlea and the ..._Boss ones.
            if (lower.StartsWith("the") || lower.Contains("boss")) return MiniBossExp;
            if (lower.Contains("small") || lower == "flea") return SmallExp;
            return NormalExp;
        }

        // The two big balloons that carry a Big Bee count as mini bosses.
        private static int GhostExp(string prefabName)
        {
            return prefabName.EndsWith("3 1") || prefabName.EndsWith("3 2") ? MiniBossExp : NormalExp;
        }
    }
}
