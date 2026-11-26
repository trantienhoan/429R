using System.Collections.Generic;
using System.Linq;
using Game.Story;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Story > Seed Pieces: Green Smoke + Whack 7 Piece (user 2026-11-26: "when player get a piece of
    /// the seed, don't blind them too much like that, just a green smoke bomb is enough"; "change the seed in the whack 7
    /// pumpkin, change it to another piece of the seed"). Re-runnable:
    /// - Story Seed Settings: Piece Smoke = Smoke_Flash_Five_Puffs (tinted the seed's green when it goes off) instead of
    ///   the light that filled the view until the next room; "Whack 7", "Damage The Lid" and "Wait For the Punch" join
    ///   the main states (the Whack 7 chest opens there), so its piece glows green and goes up in the smoke too;
    /// - Prefabs/Items/Chest/Seed_Whack + Treasure_Chest_and_Seed_Whack: copies of the boot piece and its chest; the
    ///   piece sets GAMESTAGES' The_Seed (as The_Seed did; nothing in the flow waits for it) instead of Boots_Gear;
    /// - the scene's -Wood_Barrels_Whack_Pumpkin_7- (GAME-Pumpkin7, Deactivate Self) makes that chest instead of
    ///   Treasure_Chest_and_Seed (whose The_Seed shows the WHOLE seed; the gates and the ending keep it);
    /// - Rooftop_Base_Z's Seeds Into Soil counts The_Seed too: the Whack 7 piece flies into the soil with the others.
    /// Changes the scene: save it after.
    /// </summary>
    public static class StorySeedPiecesMenu
    {
        private const string Tag = "[Story Seeds]";
        private const string SettingsPath = "Assets/Resources/Story Seed Settings.asset";
        private const string Chests = "Assets/Prefabs/Items/Chest/";
        private const string Smoke = "Assets/Prefabs/Effects/Smoke/Smoke_Flash_Five_Puffs.prefab";
        // About 0.6 m across (0.3 made a 1.2 m cloud: too much at arm's length).
        private const float SmokeSize = 0.15f;
        private static readonly string[] MoreMainStates = { "Whack 7", "Damage The Lid", "Wait For the Punch" };
        private const string PieceBool = "The_Seed";
        private const string WhackObject = "-Wood_Barrels_Whack_Pumpkin_7-";
        private const string WhackFsm = "GAME-Pumpkin7";
        private const string WhackState = "Deactivate Self";

        [MenuItem("Tools/429 Game/Story/Seed Pieces: Green Smoke + Whack 7 Piece")]
        public static void Run()
        {
            var notes = new List<string>();
            Settings(notes);
            var seed = CopyPiece(notes);
            var chest = seed != null ? CopyChest(seed, notes) : null;
            if (chest != null) PointWhack7(chest, notes);
            SoilCountsIt(notes);
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} {string.Join("; ", notes)}. Save the scene.");
        }

        [MenuItem("Tools/429 Game/Story/Seed Pieces: Green Smoke + Whack 7 Piece", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static void Settings(List<string> notes)
        {
            var settings = AssetDatabase.LoadAssetAtPath<StorySeedSettings>(SettingsPath);
            if (settings == null) { notes.Add($"no {SettingsPath} (run Set Up Story Seeds)"); return; }
            var so = new SerializedObject(settings);
            so.FindProperty("pieceSmoke").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Smoke);
            so.FindProperty("pieceSmokeSize").floatValue = SmokeSize;
            var states = so.FindProperty("mainStates");
            var have = new List<string>();
            for (int i = 0; i < states.arraySize; i++) have.Add(states.GetArrayElementAtIndex(i).stringValue);
            foreach (var state in MoreMainStates)
            {
                if (have.Contains(state)) continue;
                states.arraySize++;
                states.GetArrayElementAtIndex(states.arraySize - 1).stringValue = state;
                have.Add(state);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            notes.Add($"pieces: a green smoke bomb (Smoke_Flash_Five_Puffs x{SmokeSize}), main states now {have.Count}");
        }

        // Seed_Boot made over as Seed_Whack: the same piece, setting The_Seed instead of Boots_Gear.
        private static GameObject CopyPiece(List<string> notes)
        {
            string from = Chests + "Seed_Boot.prefab", to = Chests + "Seed_Whack.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(to) == null && !AssetDatabase.CopyAsset(from, to))
            {
                notes.Add($"could not copy {from}");
                return null;
            }
            var root = PrefabUtility.LoadPrefabContents(to);
            try
            {
                root.name = "Seed_Whack";
                int changed = 0;
                foreach (var fsm in root.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    fsm.Fsm.Init(fsm);
                    foreach (var state in fsm.Fsm.States)
                    {
                        state.LoadActions();
                        bool touched = false;
                        foreach (var action in state.Actions)
                        {
                            if (action is SetFsmVariable set && set.variableName != null && set.variableName.Value == "Boots_Gear")
                            {
                                set.variableName = new FsmString { Value = PieceBool };
                                touched = true;
                                changed++;
                            }
                        }
                        if (touched) state.SaveActions();
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root, to);
                notes.Add($"{to}: {changed} GAMESTAGES bools set to {PieceBool}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(to);
        }

        // Treasure_Chest_and_Seed_Boot made over as Treasure_Chest_and_Seed_Whack, making Seed_Whack.
        private static GameObject CopyChest(GameObject seed, List<string> notes)
        {
            string from = Chests + "Treasure_Chest_and_Seed_Boot.prefab", to = Chests + "Treasure_Chest_and_Seed_Whack.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(to) == null && !AssetDatabase.CopyAsset(from, to))
            {
                notes.Add($"could not copy {from}");
                return null;
            }
            var root = PrefabUtility.LoadPrefabContents(to);
            try
            {
                root.name = "Treasure_Chest_and_Seed_Whack";
                int changed = Repoint(root, "Seed_Boot", seed);
                PrefabUtility.SaveAsPrefabAsset(root, to);
                notes.Add($"{to}: makes Seed_Whack ({changed})");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(to);
        }

        // Every Create Object in these FSMs that makes 'was' makes 'now' instead.
        private static int Repoint(GameObject root, string was, GameObject now)
        {
            int changed = 0;
            foreach (var fsm in root.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                fsm.Fsm.Init(fsm);
                foreach (var state in fsm.Fsm.States)
                {
                    state.LoadActions();
                    bool touched = false;
                    foreach (var action in state.Actions)
                    {
                        if (action is CreateObject create && create.gameObject != null && create.gameObject.Value != null &&
                            create.gameObject.Value.name == was)
                        {
                            create.gameObject = new FsmGameObject { Value = now };
                            touched = true;
                            changed++;
                        }
                    }
                    if (touched) state.SaveActions();
                }
            }
            return changed;
        }

        private static void PointWhack7(GameObject chest, List<string> notes)
        {
            PlayMakerFSM fsm = null;
            foreach (var f in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
                if (f.gameObject.scene.IsValid() && f.gameObject.name == WhackObject && f.FsmName == WhackFsm) { fsm = f; break; }
            if (fsm == null) { notes.Add($"no {WhackObject} ({WhackFsm}) in the open scene"); return; }
            fsm.Fsm.Init(fsm);
            int changed = 0;
            foreach (var state in fsm.Fsm.States)
            {
                if (state.Name != WhackState) continue;
                state.LoadActions();
                foreach (var action in state.Actions)
                {
                    if (action is CreateObject create && create.gameObject != null && create.gameObject.Value != null &&
                        create.gameObject.Value.name.StartsWith("Treasure_Chest_and_Seed"))
                    {
                        if (create.gameObject.Value == chest) { changed = -1; continue; }
                        Undo.RecordObject(fsm, "Whack 7 Piece");
                        create.gameObject = new FsmGameObject { Value = chest };
                        changed++;
                    }
                }
                if (changed > 0) state.SaveActions();
            }
            if (changed > 0)
            {
                EditorUtility.SetDirty(fsm);
                EditorSceneManager.MarkSceneDirty(fsm.gameObject.scene);
            }
            notes.Add(changed > 0 ? $"Whack 7 now makes {chest.name}" : changed < 0 ? $"Whack 7 already makes {chest.name}" : "Whack 7's chest not found (unchanged)");
        }

        private static void SoilCountsIt(List<string> notes)
        {
            var guid = AssetDatabase.FindAssets("Rooftop_Base_Z t:Prefab")
                .FirstOrDefault(g => System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)) == "Rooftop_Base_Z");
            if (guid == null) { notes.Add("no Rooftop_Base_Z prefab"); return; }
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int changed = 0, found = 0;
                foreach (var fsm in root.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    fsm.Fsm.Init(fsm);
                    foreach (var state in fsm.Fsm.States)
                    {
                        state.LoadActions();
                        bool touched = false;
                        foreach (var action in state.Actions)
                        {
                            if (!(action is SeedsIntoSoil soil)) continue;
                            found++;
                            var names = (soil.pieces ?? new FsmString[0]).ToList();
                            if (names.Any(n => n != null && n.Value == PieceBool)) continue;
                            names.Add(new FsmString { Value = PieceBool });
                            soil.pieces = names.ToArray();
                            touched = true;
                            changed++;
                        }
                        if (touched) state.SaveActions();
                    }
                }
                if (changed > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                notes.Add(found == 0 ? "no Seeds Into Soil in Rooftop_Base_Z" : changed > 0 ? $"Seeds Into Soil counts {PieceBool} too" : $"Seeds Into Soil already counts {PieceBool}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
