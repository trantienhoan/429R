using System.Collections.Generic;
using Game.EditorTools;
using Game.Enemies;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEngine;

namespace Game.PlayGround.Editor
{
    /// <summary>
    /// Tools > 429 Game > PlayGround > Set Up PlayGround Rounds. The endgame+ PlayGround's three cooking rounds (user,
    /// 2026-11-07):
    /// - Candy_1 ... 8 become cooking ingredients (Cooking Ingredient + a recipe picture in Assets/Data/Cooking/Icons) and
    ///   last Candy Lifetime seconds on the floor instead of 20.
    /// - The laughing pumpkins (Pumpkin_8, 9, 10) no longer drop a seed themselves (the round's chest does).
    /// - The ghost balloons drop candy when popped, and no spiders.
    /// - The cauldron (Fire_Woods_n_Cauldron) gets a PlayGround Rounds director with the three Waves below: the bugs that
    ///   come non-stop from the start of cooking until the round's pumpkin is caught, the round's coloured slime mini boss,
    ///   the PG_ chest with the Yellow Seed, and the GAMESTAGES gear bool set if that seed is never taken. Its Fairy Shield
    ///   trigger events are switched off (the pot stays when the shield sweeps).
    /// The cauldron's own settings (recipes, cook times, the finale) are its own menu's, never set here.
    /// Run the slimes menu first (it makes the mini bosses). Safe to run again; save and close those prefabs first.
    /// </summary>
    public static class PlayGroundRoundsSetupMenu
    {
        private const string Breakable = "Assets/Prefabs/Items/Breakable/";
        private const string CauldronPath = "Assets/Prefabs/PlayGround/Fire_Woods_n_Cauldron.prefab";
        private const string GhostPath = "Assets/Prefabs/Enemies/PLAYGROUND/Ghost_Balloon_{0}.prefab";
        private const string IconPath = "Assets/Data/Cooking/Icons/{0}.png";
        private const string BugFolder = "Assets/Prefabs/Enemies/Enemies_Smarter/";
        private const string SlimeFolder = "Assets/Prefabs/Enemies/PLAYGROUND/";
        private const string ChestFolder = "Assets/Prefabs/PlayGround/PG_Wood/";
        private const string SmokePath = "Assets/Prefabs/Effects/Smoke/Smoke_Flash_Small_Puff.prefab";
        private const float CandyLifetime = 60f;

        // The laughing pumpkins the cauldron makes (rounds 1-3).
        private static readonly string[] Pumpkins = { "Pumpkin_8", "Pumpkin_9", "Pumpkin_9 1", "Pumpkin_10" };

        // One wave per round: label, the bug (Enemies_Smarter), most alive at once, the mini boss (PLAYGROUND), the
        // chest (PG_Wood) and the GAMESTAGES bool of its gear.
        private static readonly (string label, string bug, int maxAlive, string boss, string chest, string gearBool)[] Waves =
        {
            ("Round 1: Spider_Small, Slime_Big_Blue (Sticky)", "Spider_Small", 5, "Slime_Big_Blue", "PG_Treasure_Chest_and_Seed_Chest", "Chest_Gear"),
            ("Round 2: Spider_Normal, Slime_Big_Red (Bull)", "Spider_Normal", 8, "Slime_Big_Red", "PG_Treasure_Chest_and_Seed_Head", "Head_Gear"),
            ("Round 3: Cockroach_Small, Slime_Big_Yellow (Bomb)", "Cockroach_Small", 6, "Slime_Big_Yellow", "PG_Treasure_Chest_and_Seed_Shield", "Shield_Gear"),
        };
        private static readonly Vector2 SpawnWait = new(1.2f, 3f);

        [MenuItem("Tools/429 Game/PlayGround/Set Up PlayGround Rounds")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[PlayGround Rounds] Stop play mode first."); return; }
            var notes = new List<string>();
            var smoke = Load<GameObject>(SmokePath, notes);

            // 1. Candies: ingredients with a picture, and they stay a while.
            var candies = new List<GameObject>();
            for (int n = 1; n <= 8; n++)
            {
                string path = Breakable + "Candy_" + n + ".prefab";
                var asset = Load<GameObject>(path, notes);
                if (asset == null) continue;
                candies.Add(asset);
                string iconPath = string.Format(IconPath, "Candy_" + n);
                var icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath) ?? PrefabIconMaker.MakeSprite(asset, iconPath);
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

            // 2. Laughing pumpkins: the round's chest gives the seed now.
            foreach (var name in Pumpkins) Edit(Breakable + name + ".prefab", notes, StopSeedDrop);

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

            // 4. The cauldron's rounds director (not the Cauldron itself: its own menu sets that).
            Edit(CauldronPath, notes, root =>
            {
                // The Fairy Shield sweeps the PlayGround after each round; the cauldron has to stay.
                IgnoreFairyShield(root);
                if (!root.TryGetComponent<PlayGroundRounds>(out var director)) director = root.AddComponent<PlayGroundRounds>();
                var d = new SerializedObject(director);
                var list = d.FindProperty("waves");
                list.arraySize = Waves.Length;
                for (int i = 0; i < Waves.Length; i++)
                {
                    var (label, bug, maxAlive, boss, chest, gearBool) = Waves[i];
                    var wave = list.GetArrayElementAtIndex(i);
                    wave.FindPropertyRelative("label").stringValue = label;
                    var enemies = wave.FindPropertyRelative("enemies");
                    enemies.arraySize = 1;
                    enemies.GetArrayElementAtIndex(0).objectReferenceValue = Load<GameObject>(BugFolder + bug + ".prefab", notes);
                    wave.FindPropertyRelative("maxAlive").intValue = maxAlive;
                    wave.FindPropertyRelative("spawnWait").vector2Value = SpawnWait;
                    wave.FindPropertyRelative("boss").objectReferenceValue = Load<GameObject>(SlimeFolder + boss + ".prefab", notes);
                    wave.FindPropertyRelative("chest").objectReferenceValue = Load<GameObject>(ChestFolder + chest + ".prefab", notes);
                    wave.FindPropertyRelative("gearBool").stringValue = gearBool;
                }
                d.FindProperty("spawnEffect").objectReferenceValue = smoke;
                d.FindProperty("chestEffect").objectReferenceValue = smoke;
                d.ApplyModifiedPropertiesWithoutUndo();
            });

            AssetDatabase.SaveAssets();
            Debug.Log("[PlayGround Rounds] " + string.Join("; ", notes));
        }

        [MenuItem("Tools/429 Game/PlayGround/Set Up PlayGround Rounds", true)]
        private static bool NotPlaying() => !EditorApplication.isPlaying;

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
                Debug.LogWarning($"[PlayGround Rounds] No '{path}': skipped.");
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

        // Loads an asset; a missing one is warned about (and noted) and comes back null.
        private static T Load<T>(string path, List<string> notes) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                Debug.LogWarning($"[PlayGround Rounds] Missing '{path}' (left empty; make it, then run this again).");
                notes.Add($"MISSING {path}");
            }
            return asset;
        }

        private static void SetArray(SerializedProperty array, Object[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
