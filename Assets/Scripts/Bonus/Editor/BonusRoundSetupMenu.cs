using System.Collections.Generic;
using System.Linq;
using Game.Bonus;
using Game.Combat;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Bonus > Set Up Nesting Pumpkin Bonus: builds the nesting pumpkin bonus round and hooks it into
    /// GAMESTAGES after the Bedroom and Playroom stages. Safe to run again: it only adds what's missing.
    /// - Nesting_Pumpkin_1 (Items/Breakable, the giant): a copy of StartGamePumpkin that doesn't turn into the boss, spawns
    ///   no extra pumpkins, shows no tutorial notice and doesn't throw the player; it jiggles at every hit, bursts into
    ///   pieces and gives its EXP (gold orbs). Nesting_Pumpkin_2 ... _5: copies of it (prefab variants), each smaller.
    ///   All have 120 health; the first four drop 5 candies and give 5 EXP, the last 10 candies and 30 EXP.
    /// - Bonus_Chest_Pumpkin (BedRoom): a copy of Treasure_Chest_Pumpkin without the sword, facing the player (no spin).
    /// - Bonus_Area (Bases, a copy of Boss_Area with the Sky Light and Chest_Pos): gets the Bonus Round.
    /// - GAMESTAGES: Watch Bonus Round in "Enemy1 - Wait" and "Enemy 3 Stuffs"; new states "Bonus Round BR" (after BR_GO,
    ///   before 3 - LivingRoom) and "Bonus Round LR" (after LR GO 4, before Rooftop_ON) with Play Bonus Round.
    /// </summary>
    public static class BonusRoundSetupMenu
    {
        private const string StartPumpkinPath = "Assets/Prefabs/Items/Breakable/StartGamePumpkin.prefab";
        private const string NestingPumpkinPath = "Assets/Prefabs/Items/Breakable/Nesting_Pumpkin_1.prefab";
        private const string SmallerPumpkinPath = "Assets/Prefabs/Items/Breakable/Nesting_Pumpkin_{0}.prefab";
        private const string AreaPath = "Assets/Prefabs/Bases/Bonus_Area.prefab";
        private const string ChestSourcePath = "Assets/Prefabs/BedRoom/Treasure_Chest_Pumpkin.prefab";
        private const string ChestPath = "Assets/Prefabs/BedRoom/Bonus_Chest_Pumpkin.prefab";

        // The five, giant first: size (scale), health, candies, EXP, laugh pitch, drops from the sky.
        private static readonly (float size, float health, int candies, int exp, float pitch, bool fromSky)[] Pumpkins =
        {
            (2f, 120f, 5, 5, 1f, true),
            (1.44f, 120f, 5, 5, 1.12f, false),
            (1.04f, 120f, 5, 5, 1.24f, false),
            (0.75f, 120f, 5, 5, 1.36f, false),
            (0.54f, 120f, 10, 30, 1.48f, false),
        };
        private const string CandyPath = "Assets/Prefabs/Items/Breakable/Candy_{0}.prefab";
        private const string TickPath = "Assets/Audio/Kenney Audio/kenney_interface-sounds/Audio/tick_002.ogg";
        private const string WinPath = "Assets/Audio/SFX/shop_buy_success.wav";
        private const string FontPath = "Assets/Fonts/roboto/Roboto-Bold SDF.asset";
        private const string PuffPath = "Assets/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Smoke_Blast.prefab";
        private const float SkyLightHeight = 5f;
        private const float SkyLightIntensity = 12f;
        private const float SkyLightRange = 14f;

        [MenuItem("Tools/429 Game/Bonus/Set Up Nesting Pumpkin Bonus")]
        private static void SetUpMenu()
        {
            var notes = SetUp();
            EditorUtility.DisplayDialog("Nesting Pumpkin Bonus", string.Join("\n", notes), "OK");
        }

        [MenuItem("Tools/429 Game/Bonus/Set Up Nesting Pumpkin Bonus", true)]
        private static bool CanSetUp() => !EditorApplication.isPlaying;

        /// <summary>Does the whole set-up; returns what it did.</summary>
        public static List<string> SetUp()
        {
            var notes = new List<string>();
            // The prefabs it changes must not be open in Prefab Mode (others may be).
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && (stage.assetPath.Contains("Nesting_Pumpkin") || stage.assetPath == AreaPath || stage.assetPath == ChestPath))
            {
                notes.Add($"Close {stage.assetPath} in Prefab Mode first.");
                return notes;
            }
            notes.Add(MakeNestingPumpkin());
            notes.Add(MakeSmallerPumpkins());
            notes.Add(MakeChest());
            notes.Add(SetUpArea());
            notes.AddRange(HookGameStages());
            return notes;
        }

        // ---------- Nesting_Pumpkin ----------

        private static string MakeNestingPumpkin()
        {
            bool made = false;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(NestingPumpkinPath) == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(StartPumpkinPath);
                if (source == null) return "MISSING " + StartPumpkinPath;
                var copy = (GameObject)PrefabUtility.InstantiatePrefab(source, UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                copy.name = "Nesting_Pumpkin_1";
                PrefabUtility.SaveAsPrefabAsset(copy, NestingPumpkinPath);
                Object.DestroyImmediate(copy);
                made = true;
            }

            var root = PrefabUtility.LoadPrefabContents(NestingPumpkinPath);
            var changes = new List<string>();
            try
            {
                if (root.GetComponent<NestingPumpkin>() == null)
                {
                    ApplySettings(root.AddComponent<NestingPumpkin>(), 0);
                    changes.Add("Nesting Pumpkin added (the giant)");
                }
                // It jiggles at every hit that hurts (its Jiggle watches the health).
                var jiggle = root.GetComponentInChildren<Game.VFX.Jiggle>(true);
                if (jiggle != null)
                {
                    var js = new SerializedObject(jiggle);
                    if (Set(js, "jiggleWhenHurt", true)) { js.ApplyModifiedPropertiesWithoutUndo(); changes.Add("jiggles when hurt"); }
                }
                if (!root.CompareTag("Untagged")) { root.tag = "Untagged"; changes.Add("tag Untagged (not a DoorKey)"); }
                var body = root.transform.Find("pumpkin_Start");
                if (body == null) return "Nesting_Pumpkin has no pumpkin_Start";

                foreach (var fsm in body.GetComponents<PlayMakerFSM>())
                {
                    // It never turns into the pumpkin boss.
                    if (fsm.FsmName == "Wait and Transform" && fsm.enabled) { fsm.enabled = false; changes.Add("no boss after 300 s"); }
                    // No start-game tutorial notice. The notice is what moved End 1 on to the laughing (FINISHED after
                    // 1.7 s), so a Wait does that instead.
                    if (fsm.FsmName == "Health")
                    {
                        if (DisableActions(fsm, "End 1", a => a.GetType().Name == "ShowNotice")) changes.Add("no tutorial notice");
                        if (AddFinishWait(fsm, "End 1", 1.7f)) changes.Add("End 1 goes on to the laughing after 1.7 s");
                    }
                }
                // It doesn't throw the player when touched (the start pumpkin's Monster collider).
                var monster = body.Find("Jiggle/pumpkin_0/monster_collider");
                if (monster != null && monster.gameObject.activeSelf) { monster.gameObject.SetActive(false); changes.Add("no Monster collider"); }
                // Its break drops only its pieces: no extra stage pumpkins or tree pumpkins (the round drops the candies).
                var extras = body.Find("pumpkin_0_drop/pumpkin_0_extras_drop");
                if (extras != null)
                {
                    int off = 0;
                    foreach (Transform spot in extras)
                    {
                        foreach (var fsm in spot.GetComponents<PlayMakerFSM>())
                        {
                            if (fsm.enabled && SpawnsSomething(fsm)) { fsm.enabled = false; off++; }
                        }
                    }
                    if (off > 0) changes.Add($"{off} extra pumpkin spawns off");
                }
                // 5 EXP with gold orbs, the enemy hit pop, and only the player's hits count (not the chest's flying
                // pieces or the candies).
                var hits = body.GetComponent<EnemyHitDamage>();
                if (hits != null)
                {
                    var so = new SerializedObject(hits);
                    bool any = false;
                    any |= Set(so, "expOrbs", true);
                    any |= Set(so, "hideHitEffect", false);
                    any |= Set(so, "strayToughness", 8f);
                    if (any) { so.ApplyModifiedPropertiesWithoutUndo(); changes.Add("EXP orbs, hit pop on, stray bumps barely count"); }
                }
                PrefabUtility.SaveAsPrefabAsset(root, NestingPumpkinPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return (made ? "Made Nesting_Pumpkin_1" : "Nesting_Pumpkin_1") + (changes.Count > 0 ? ": " + string.Join(", ", changes) : ": already set up");
        }

        // Nesting_Pumpkin_2 ... _5: prefab variants of the giant, each with its own size, health, candies, EXP and laugh.
        private static string MakeSmallerPumpkins()
        {
            var giant = AssetDatabase.LoadAssetAtPath<GameObject>(NestingPumpkinPath);
            if (giant == null) return "MISSING " + NestingPumpkinPath;
            var made = new List<string>();
            for (int i = 1; i < Pumpkins.Length; i++)
            {
                string path = string.Format(SmallerPumpkinPath, i + 1);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) continue;
                var copy = (GameObject)PrefabUtility.InstantiatePrefab(giant, UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                copy.name = $"Nesting_Pumpkin_{i + 1}";
                ApplySettings(copy.GetComponent<NestingPumpkin>(), i);
                PrefabUtility.SaveAsPrefabAsset(copy, path);
                Object.DestroyImmediate(copy);
                made.Add($"_{i + 1}");
            }
            return made.Count > 0 ? "Made Nesting_Pumpkin" + string.Join(", ", made) : "Nesting_Pumpkin_2 ... _5 already there";
        }

        // The table's values for pumpkin number 'index' (0 = the giant): its Nesting Pumpkin and its Enemy Hit Damage's EXP.
        private static void ApplySettings(NestingPumpkin pumpkin, int index)
        {
            var row = Pumpkins[index];
            var so = new SerializedObject(pumpkin);
            so.FindProperty("size").floatValue = row.size;
            so.FindProperty("health").floatValue = row.health;
            so.FindProperty("candies").intValue = row.candies;
            so.FindProperty("voicePitch").floatValue = row.pitch;
            so.FindProperty("dropFromTheSky").boolValue = row.fromSky;
            so.ApplyModifiedPropertiesWithoutUndo();
            var hits = pumpkin.GetComponentInChildren<EnemyHitDamage>(true);
            if (hits != null)
            {
                var hs = new SerializedObject(hits);
                Set(hs, "exp", row.exp);
                hs.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ---------- Bonus_Chest_Pumpkin ----------

        private static string MakeChest()
        {
            bool made = false;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ChestPath) == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(ChestSourcePath);
                if (source == null) return "MISSING " + ChestSourcePath;
                var copy = (GameObject)PrefabUtility.InstantiatePrefab(source, UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                copy.name = "Bonus_Chest_Pumpkin";
                PrefabUtility.SaveAsPrefabAsset(copy, ChestPath);
                Object.DestroyImmediate(copy);
                made = true;
            }
            var root = PrefabUtility.LoadPrefabContents(ChestPath);
            var changes = new List<string>();
            try
            {
                // No sword: the bonus round brings the giant pumpkin instead.
                var sword = root.transform.Find("Treasure_Chest_Drop_Pos/Giant_Pumpkin_Pos");
                var swordFsm = sword != null ? sword.GetComponent<PlayMakerFSM>() : null;
                if (swordFsm != null && swordFsm.enabled) { swordFsm.enabled = false; changes.Add("no sword"); }
                // It faces the player instead of spinning (Face The Player follows the headset; a Look At the $Player or
                // $MainCam globals would face the middle of the play area).
                foreach (var fsm in root.GetComponents<PlayMakerFSM>())
                {
                    if (fsm.FsmName != "Health") continue;
                    if (DisableActions(fsm, "Tween Position", a => a is Rotate || a is LookAt)) changes.Add("no spin");
                }
                if (root.GetComponent<Game.VFX.FaceThePlayer>() == null) { root.AddComponent<Game.VFX.FaceThePlayer>(); changes.Add("faces the player"); }
                PrefabUtility.SaveAsPrefabAsset(root, ChestPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return (made ? "Made Bonus_Chest_Pumpkin" : "Bonus_Chest_Pumpkin") + (changes.Count > 0 ? ": " + string.Join(", ", changes) : ": already set up");
        }


        // A spot that creates something (a CreateObject), like the start pumpkin's extra pumpkins.
        private static bool SpawnsSomething(PlayMakerFSM fsm)
        {
            if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
            foreach (var state in fsm.FsmStates)
            {
                state.LoadActions();
                if (state.Actions.Any(a => a is CreateObject)) return true;
            }
            return false;
        }

        // Switches actions off in one state (only that state is saved again); false if nothing changed.
        private static bool DisableActions(PlayMakerFSM fsm, string stateName, System.Func<FsmStateAction, bool> which)
        {
            if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
            var state = fsm.Fsm.GetState(stateName);
            if (state == null) return false;
            state.LoadActions();
            if (state.ActionData.ActionCount != state.Actions.Length) return false;
            bool changed = false;
            foreach (var action in state.Actions)
            {
                if (action.Enabled && which(action)) { action.Enabled = false; changed = true; }
            }
            if (!changed) return false;
            state.SaveActions();
            EditorUtility.SetDirty(fsm);
            return true;
        }

        // A Wait that sends FINISHED at the end of a state that has nothing else to finish it; false if there is one.
        private static bool AddFinishWait(PlayMakerFSM fsm, string stateName, float seconds)
        {
            if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
            var state = fsm.Fsm.GetState(stateName);
            if (state == null) return false;
            state.LoadActions();
            if (state.ActionData.ActionCount != state.Actions.Length) return false;
            if (state.Actions.Any(a => a.Enabled && a is Wait)) return false;
            var wait = new Wait();
            wait.Reset();
            wait.time = seconds;
            wait.finishEvent = FsmEvent.Finished;
            state.Actions = state.Actions.Append(wait).ToArray();
            state.SaveActions();
            EditorUtility.SetDirty(fsm);
            return true;
        }

        private static bool Set(SerializedObject so, string field, float value)
        {
            var p = so.FindProperty(field);
            if (p == null || Mathf.Approximately(p.floatValue, value)) return false;
            p.floatValue = value;
            return true;
        }

        private static bool Set(SerializedObject so, string field, int value)
        {
            var p = so.FindProperty(field);
            if (p == null || p.intValue == value) return false;
            p.intValue = value;
            return true;
        }

        private static bool Set(SerializedObject so, string field, bool value)
        {
            var p = so.FindProperty(field);
            if (p == null || p.boolValue == value) return false;
            p.boolValue = value;
            return true;
        }

        // ---------- Bonus_Area ----------

        private static string SetUpArea()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(AreaPath) == null) return "MISSING " + AreaPath + " (make it from Boss_Area first)";
            var root = PrefabUtility.LoadPrefabContents(AreaPath);
            var filled = new List<string>();
            try
            {
                // The light over the stage (like the Bedroom's room light, lower and brighter: there are no walls here).
                if (root.transform.Find("Sky Light") == null)
                {
                    var go = new GameObject("Sky Light");
                    go.transform.SetParent(root.transform, false);
                    go.transform.position = root.transform.position + Vector3.up * SkyLightHeight;
                    var light = go.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = Color.white;
                    light.intensity = SkyLightIntensity;
                    light.range = SkyLightRange;
                    light.shadows = LightShadows.Soft;
                    filled.Add("Sky Light");
                }
                if (root.transform.Find("Chest_Pos") == null)
                {
                    var spot = new GameObject("Chest_Pos");
                    spot.transform.SetParent(root.transform, false);
                    filled.Add("Chest_Pos");
                }
                var round = root.GetComponent<BonusRound>();
                if (round == null) { round = root.AddComponent<BonusRound>(); filled.Add("Bonus Round added"); }
                var so = new SerializedObject(round);
                Fill(so, "chestPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(ChestPath), filled);
                Fill(so, "chestPoint", root.transform.Find("Chest_Pos"), filled);
                var line = so.FindProperty("pumpkins");
                if (line.arraySize == 0)
                {
                    for (int i = 0; i < Pumpkins.Length; i++)
                    {
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(string.Format(SmallerPumpkinPath, i + 1));
                        var pumpkin = prefab != null ? prefab.GetComponent<NestingPumpkin>() : null;
                        if (pumpkin == null) continue;
                        line.arraySize++;
                        line.GetArrayElementAtIndex(line.arraySize - 1).objectReferenceValue = pumpkin;
                    }
                    filled.Add($"{line.arraySize} pumpkins");
                }
                Fill(so, "tickSound", AssetDatabase.LoadAssetAtPath<AudioClip>(TickPath), filled);
                Fill(so, "winSound", AssetDatabase.LoadAssetAtPath<AudioClip>(WinPath), filled);
                Fill(so, "font", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath), filled);
                Fill(so, "puffEffect", AssetDatabase.LoadAssetAtPath<GameObject>(PuffPath), filled);
                var list = so.FindProperty("candies");
                if (list.arraySize == 0)
                {
                    for (int i = 1; i <= 8; i++)
                    {
                        var candy = AssetDatabase.LoadAssetAtPath<GameObject>(string.Format(CandyPath, i));
                        if (candy == null) continue;
                        list.arraySize++;
                        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = candy;
                    }
                    filled.Add($"{list.arraySize} candies");
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, AreaPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return "Bonus_Area" + (filled.Count > 0 ? ": " + string.Join(", ", filled) : ": already set up");
        }

        private static void Fill(SerializedObject so, string field, Object value, List<string> filled)
        {
            var p = so.FindProperty(field);
            if (p == null || p.objectReferenceValue != null || value == null) return;
            p.objectReferenceValue = value;
            filled.Add(field);
        }

        // ---------- GAMESTAGES ----------

        private static IEnumerable<string> HookGameStages()
        {
            var gs = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(f => f.FsmName == "GAMESTAGES" && f.gameObject.scene.IsValid());
            if (gs == null) { yield return "No GAMESTAGES in the open scene: open 2 Game Scene and run it again."; yield break; }
            var f = gs.Fsm;
            if (!f.Initialized) f.Init(gs);
            var area = AssetDatabase.LoadAssetAtPath<GameObject>(AreaPath);
            Undo.RecordObject(gs, "Nesting Pumpkin Bonus");

            yield return Watch(gs, "Enemy1 - Wait");
            yield return Watch(gs, "Enemy 3 Stuffs");
            // The room stays: its floor (the walls are already down) is the stage's floor; Boss_Area's own is invisible.
            yield return BonusState(gs, "Bonus Round BR", "BR_GO", "3 - LivingRoom", area);
            yield return BonusState(gs, "Bonus Round LR", "LR GO 4", "Rooftop_ON", area);
            EditorUtility.SetDirty(gs);
            EditorSceneManager.MarkSceneDirty(gs.gameObject.scene);
            yield return "Save the scene to keep the GAMESTAGES changes.";
        }

        // Watch Bonus Round at the end of a stage's enemy-wave state.
        private static string Watch(PlayMakerFSM gs, string stateName)
        {
            var state = gs.Fsm.GetState(stateName);
            if (state == null) return $"GAMESTAGES has no '{stateName}'";
            state.LoadActions();
            if (state.ActionData.ActionCount != state.Actions.Length) return $"'{stateName}' didn't load all its actions; left alone";
            if (state.Actions.Any(a => a is WatchBonusRound)) return $"'{stateName}' already has Watch Bonus Round";
            var watch = new WatchBonusRound();
            watch.Reset();
            state.Actions = state.Actions.Append(watch).ToArray();
            state.SaveActions();
            return $"'{stateName}': Watch Bonus Round added";
        }

        // A new state between 'after' and 'next' that plays the bonus round (or goes straight on if it wasn't earned).
        private static string BonusState(PlayMakerFSM gs, string name, string after, string next, GameObject area)
        {
            var f = gs.Fsm;
            var from = f.GetState(after);
            if (from == null || f.GetState(next) == null) return $"GAMESTAGES has no '{after}' or '{next}'";
            if (f.GetState(name) == null)
            {
                var near = from.Position;
                var state = new FsmState(f) { Name = name, Position = new Rect(near.x + 170f, near.y + 40f, near.width + 20f, near.height) };
                var play = new PlayBonusRound();
                play.Reset();
                play.area = new FsmGameObject { Value = area };
                state.Actions = new FsmStateAction[] { play };
                state.Transitions = new[] { new FsmTransition { FsmEvent = FsmEvent.Finished, ToState = next } };
                f.States = f.States.Append(state).ToArray();
                state.SaveActions();
            }
            // 'after' goes to the bonus state instead of straight to 'next'.
            int rerouted = 0;
            foreach (var t in from.Transitions)
            {
                if (t.ToState == next) { t.ToState = name; rerouted++; }
            }
            return rerouted > 0 ? $"'{name}' between '{after}' and '{next}'" : $"'{name}' already hooked in";
        }
    }
}
