using System.Collections.Generic;
using Game.EditorTools;
using Game.Enemies;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEngine;

namespace Game.PlayGround.Editor
{
    /// <summary>
    /// Tools > 429 Game > PlayGround > Set Up PlayGround Rounds. The endgame+ PlayGround loop:
    /// - Candy_1 ... 8 become the cauldron's ingredients (Cooking Ingredient + a recipe picture in
    ///   Assets/Data/Cooking/Icons) and last Candy Lifetime seconds on the floor instead of 20.
    /// - The cauldron (Fire_Woods_n_Cauldron): recipes of 3 / 4 / 5 / 5 candies (+ Pumpkin_On_Tree), making Pumpkin_8,
    ///   Pumpkin_9, Pumpkin_10 and Pumpkin_Big, then the mystery pot (5 x ???) that bursts into candy and The_Seed (the
    ///   Green Seed). It gets a PlayGround Rounds director with one wave per round: EnemyManager 0-3's bugs, twice as
    ///   many, two bosses each, and a Yellow Seed chest (read from the open scene's EnemyManager FSMs).
    /// - The laughing pumpkins no longer drop a seed themselves (the director's chest does).
    /// - The ghost balloons drop candy when popped, and no spiders when they pop in the player's face.
    /// Run Set Up Laughing Pumpkins first. Safe to run again; save and close those prefabs first.
    /// </summary>
    public static class PlayGroundRoundsSetupMenu
    {
        private const string Breakable = "Assets/Prefabs/Items/Breakable/";
        private const string CauldronPath = "Assets/Prefabs/PlayGround/Fire_Woods_n_Cauldron.prefab";
        private const string GhostPath = "Assets/Prefabs/Enemies/PLAYGROUND/Ghost_Balloon_{0}.prefab";
        private const string IconPath = "Assets/Data/Cooking/Icons/{0}.png";
        private const string ChestPath = "Assets/Prefabs/Items/Chest/Treasure_Chest_and_Seed_{0}.prefab";
        private const string GreenSeedPath = "Assets/Prefabs/Items/Chest/The_Seed.prefab";
        private const string SmokePath = "Assets/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Smoke_Blast.prefab";
        private const string Sfx = "Assets/Audio/SFX/";
        private const float CandyLifetime = 60f;

        private static readonly string[] Results = { "Pumpkin_8", "Pumpkin_9", "Pumpkin_10", "Pumpkin_Big" };
        private static readonly int[] RecipeCandies = { 3, 4, 5, 5 };
        private static readonly int[] RecipePumpkins = { 1, 1, 1, 2 };
        // EnemyManager 0-3 x2: bugs in all, most alive at once.
        private static readonly int[] WaveSizes = { 12, 24, 20, 24 };
        private static readonly int[] MaxAlive = { 5, 8, 6, 8 };
        private static readonly string[] Chests = { "Chest", "Head", "Shield", "Boot" };

        [MenuItem("Tools/429 Game/PlayGround/Set Up PlayGround Rounds")]
        public static void Run()
        {
            var notes = new List<string>();
            var smoke = Load<GameObject>(SmokePath);

            // 1. Candies: ingredients with a picture, and they stay a while.
            var candies = new List<GameObject>();
            for (int n = 1; n <= 8; n++)
            {
                string path = Breakable + "Candy_" + n + ".prefab";
                var asset = Load<GameObject>(path);
                if (asset == null) continue;
                candies.Add(asset);
                string iconPath = string.Format(IconPath, "Candy_" + n);
                var icon = Load<Sprite>(iconPath) ?? PrefabIconMaker.MakeSprite(asset, iconPath);
                string id = "Candy_" + n;
                Edit(path, notes, root =>
                {
                    if (!root.TryGetComponent<CookingIngredient>(out var ingredient)) ingredient = root.AddComponent<CookingIngredient>();
                    var so = new SerializedObject(ingredient);
                    so.FindProperty("ingredientId").stringValue = id;
                    so.FindProperty("icon").objectReferenceValue = icon;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    SetCandyLifetime(root);
                });
            }

            // 2. Laughing pumpkins: the director's chest gives the seed now.
            foreach (var name in new[] { "Pumpkin_8", "Pumpkin_9", "Pumpkin_9 1", "Pumpkin_10", "Pumpkin_Big" })
                Edit(Breakable + name + ".prefab", notes, StopSeedDrop);

            // 3. Ghost balloons: candy prizes, no spiders.
            for (int n = 1; n <= 3; n++)
            {
                Edit(string.Format(GhostPath, n), notes, root =>
                {
                    if (!root.TryGetComponent<GhostBalloon>(out var balloon)) return;
                    var so = new SerializedObject(balloon);
                    SetArray(so.FindProperty("lollipops"), candies.ToArray());
                    so.FindProperty("minLollipops").intValue = 1;
                    so.FindProperty("maxLollipops").intValue = 2;
                    so.FindProperty("minSpiders").intValue = 0;
                    so.FindProperty("maxSpiders").intValue = 0;
                    so.ApplyModifiedPropertiesWithoutUndo();
                });
            }

            // 4. The cauldron and its rounds.
            var waves = ReadEnemyManagers(notes);
            Edit(CauldronPath, notes, root =>
            {
                // The Fairy Shield sweeps the PlayGround after each round; the cauldron has to stay (its Damage FSM
                // used to pop it on the shield).
                IgnoreFairyShield(root);
                var cauldron = root.GetComponent<Cauldron>();
                if (cauldron == null) { notes.Add("no Cauldron on the cauldron prefab"); return; }
                var so = new SerializedObject(cauldron);
                SetArray(so.FindProperty("candies"), candies.ToArray());
                var rounds = so.FindProperty("rounds");
                rounds.arraySize = Results.Length;
                for (int i = 0; i < Results.Length; i++)
                {
                    var round = rounds.GetArrayElementAtIndex(i);
                    round.FindPropertyRelative("pumpkins").intValue = RecipePumpkins[i];
                    round.FindPropertyRelative("candies").intValue = RecipeCandies[i];
                    round.FindPropertyRelative("result").objectReferenceValue = Load<GameObject>(Breakable + Results[i] + ".prefab");
                    round.FindPropertyRelative("results").intValue = 1;
                }
                so.FindProperty("maxKinds").intValue = 3;
                so.FindProperty("spitToMiddle").boolValue = true;
                so.FindProperty("mysteryCount").intValue = 5;
                so.FindProperty("finalSeed").objectReferenceValue = Load<GameObject>(GreenSeedPath);
                so.FindProperty("finalCandies").intValue = 24;
                so.FindProperty("breakEffect").objectReferenceValue = smoke;
                so.FindProperty("swellSound").objectReferenceValue = Load<AudioClip>(Sfx + "earth_rumble.wav");
                so.FindProperty("breakSound").objectReferenceValue = Load<AudioClip>(Sfx + "pumpkin_explode_2.wav");
                so.ApplyModifiedPropertiesWithoutUndo();

                if (!root.TryGetComponent<PlayGroundRounds>(out var director)) director = root.AddComponent<PlayGroundRounds>();
                var d = new SerializedObject(director);
                var list = d.FindProperty("waves");
                list.arraySize = waves.Count;
                for (int i = 0; i < waves.Count; i++)
                {
                    var (enemy, boss, label) = waves[i];
                    var wave = list.GetArrayElementAtIndex(i);
                    wave.FindPropertyRelative("label").stringValue = label;
                    var enemies = wave.FindPropertyRelative("enemies");
                    enemies.arraySize = 1;
                    enemies.GetArrayElementAtIndex(0).objectReferenceValue = enemy;
                    wave.FindPropertyRelative("waveSize").intValue = WaveSizes[i];
                    wave.FindPropertyRelative("maxAlive").intValue = MaxAlive[i];
                    wave.FindPropertyRelative("spawnWait").vector2Value = new Vector2(1.2f, 3f);
                    wave.FindPropertyRelative("boss").objectReferenceValue = boss;
                    wave.FindPropertyRelative("bosses").intValue = 2;
                    wave.FindPropertyRelative("bossWhenLeft").intValue = 2;
                    wave.FindPropertyRelative("chest").objectReferenceValue = Load<GameObject>(string.Format(ChestPath, Chests[i]));
                }
                d.FindProperty("spawnEffect").objectReferenceValue = smoke;
                d.FindProperty("chestEffect").objectReferenceValue = smoke;
                d.ApplyModifiedPropertiesWithoutUndo();
            });

            AssetDatabase.SaveAssets();
            Debug.Log("[PlayGround Rounds] " + string.Join("; ", notes));
        }

        // Each wave from the open scene's -EnemyManager 0 ... 3: what it spawns and its boss.
        private static List<(GameObject enemy, GameObject boss, string label)> ReadEnemyManagers(List<string> notes)
        {
            var found = new List<(GameObject, GameObject, string)>();
            for (int i = 0; i < 4; i++)
            {
                GameObject enemy = null, boss = null;
                var manager = FindSceneObject("-EnemyManager " + i);
                if (manager != null)
                {
                    foreach (var fsm in manager.GetComponents<PlayMakerFSM>())
                    {
                        if (fsm.FsmName != "EnemyManager") continue;
                        foreach (var state in fsm.Fsm.States)
                        {
                            foreach (var action in state.Actions)
                            {
                                if (action is not CreateObject create || create.gameObject.Value == null) continue;
                                if (state.Name.Contains("Spawn") && enemy == null) enemy = create.gameObject.Value;
                                else if ((state.Name.Contains("COMPLETE") || state.Name.Contains("Boss")) && boss == null) boss = create.gameObject.Value;
                            }
                        }
                    }
                }
                if (enemy == null) notes.Add($"EnemyManager {i}: no enemy found (open 2 Game Scene)");
                found.Add((enemy, boss, $"Round {i + 1}: {(enemy != null ? enemy.name : "?")} x{WaveSizes[i]}, 2x {(boss != null ? boss.name : "?")}"));
            }
            return found;
        }

        private static GameObject FindSceneObject(string name)
        {
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.name == name && go.scene.IsValid() && !EditorUtility.IsPersistent(go)) return go;
            }
            return null;
        }

        // The candy's Health FSM breaks it after a while on the floor (its Wait): make that longer.
        private static void SetCandyLifetime(GameObject root)
        {
            foreach (var fsm in root.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != "Health") continue;
                foreach (var state in fsm.Fsm.States)
                {
                    bool changed = false;
                    foreach (var action in state.Actions)
                    {
                        if (action is Wait wait && !wait.time.UseVariable && wait.time.Value > 5f && wait.time.Value < CandyLifetime)
                        {
                            wait.time.Value = CandyLifetime;
                            changed = true;
                        }
                    }
                    if (changed) state.SaveActions();
                }
                EditorUtility.SetDirty(fsm);
            }
        }

        // Switches off every "touched by the Fairy Shield" Trigger Event in the object's FSMs (tick them back on to undo).
        private static void IgnoreFairyShield(GameObject root)
        {
            foreach (var fsm in root.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                bool any = false;
                foreach (var state in fsm.Fsm.States)
                {
                    bool changed = false;
                    foreach (var action in state.Actions)
                    {
                        if (action is TriggerEvent trigger && trigger.Enabled && trigger.collideTag.Value == "Fairy_Shield")
                        {
                            trigger.Enabled = false;
                            changed = true;
                        }
                    }
                    if (changed)
                    {
                        state.SaveActions();
                        any = true;
                    }
                }
                if (any) EditorUtility.SetDirty(fsm);
            }
        }

        // Switches off the seed the pumpkin's Health FSM used to drop when it broke (its Drop_Crack Create Object).
        private static void StopSeedDrop(GameObject root)
        {
            foreach (var fsm in root.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName != "Health") continue;
                foreach (var state in fsm.Fsm.States)
                {
                    bool changed = false;
                    foreach (var action in state.Actions)
                    {
                        if (action is CreateObject create && create.Enabled && create.gameObject.Value != null &&
                            create.gameObject.Value.name.Contains("Seed"))
                        {
                            create.Enabled = false;
                            changed = true;
                        }
                    }
                    if (changed) state.SaveActions();
                }
                EditorUtility.SetDirty(fsm);
            }
        }

        private static void Edit(string path, List<string> notes, System.Action<GameObject> change)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                notes.Add($"no '{path}'");
                return;
            }
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                change(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                notes.Add(root.name);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

        private static void SetArray(SerializedProperty array, Object[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
