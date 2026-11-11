using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Leveling;
using HutongGames.PlayMaker;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Inventory.Editor
{
    /// <summary>
    /// Tools > 429 Game > Balance 2026-11-10 > Six Candies And Stats (user, 2026-11-10): six candies, one per level-up
    /// stat (Candy Kinds), level-up points a little weaker and candies stronger. In order:
    /// 1. The open scene's Player Level: Toughness push 4% a point (was 8%), Fist Size 5% bigger a point (was 10%), the
    ///    10th point counts as 1 (Last Point Worth, was 7). Weapon Efficiency's +1 damage a point and the candies' boost
    ///    from the player's own points are in the code. The scene is only marked changed: SAVE IT.
    /// 2. Candy_3 and Candy_6 come out of every candy list in the prefabs: component lists (spawners, chests, slimes,
    ///    pictures, the cauldron...), PlayMaker array variables (Random_* spawners) and actions' lists (Spill Candies in
    ///    the PlayGround chests). A list keeps the other candies, so the 6 come evenly. Their prefabs stay (nothing spawns
    ///    them); a single field naming one is only reported.
    /// 3. Candy Colours (the 6 colours, the slimes' candies and skins, the cauldron's link), then Give Candies Their
    ///    Buffs (each candy its stat), which reloads the scripts at the end.
    /// Every prefab it changes is first copied to Documents/429R_claude_backups/2026-11-10/six_candies. Running it again
    /// changes nothing more. Never in play mode; a prefab open in Prefab Mode is skipped.
    /// </summary>
    public static class SixCandiesMenu
    {
        private const string MenuPath = "Tools/429 Game/Balance 2026-11-10/Six Candies And Stats";
        private const string Tag = "[Six Candies] ";

        // The scene's Player Level (user, 2026-11-10).
        private const float PushCutPerPoint = 0.04f;
        private const float PunchSizePerPoint = 0.05f;
        private const int LastPointWorth = 1;

        [MenuItem(MenuPath)]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError(Tag + "Stop play mode first. Nothing changed.");
                return;
            }
            var notes = new List<string>();
            var stats = SetStats(notes);
            var removed = RemoveRetired(notes, out int files);
            AssetDatabase.SaveAssets();
            Debug.Log(Tag + "Player Level: " + stats + ". Candy_3/Candy_6 taken out of " + files + " prefabs" +
                      (removed.Count > 0 ? ": " + string.Join("; ", removed) : " (none left)") +
                      (notes.Count > 0 ? ". NOTES: " + string.Join("; ", notes) : "") + ".");
            CandyColourSetupMenu.Run();
            CandyBuffSetupMenu.Run();
        }

        [MenuItem(MenuPath, true)]
        private static bool NotPlaying() => !EditorApplication.isPlaying;

        // 1. The open scene's Player Level (marked changed, not saved).
        private static string SetStats(List<string> notes)
        {
            var level = Object.FindAnyObjectByType<PlayerLevel>(FindObjectsInactive.Include);
            if (level == null || !level.gameObject.scene.IsValid())
            {
                notes.Add("no Player Level in the open scene: its numbers weren't changed (open 2 Game Scene and run this again)");
                return "not found";
            }
            var so = new SerializedObject(level);
            var said = new List<string>();
            Set(so, "pushCutPerPoint", PushCutPerPoint, said);
            Set(so, "punchSizePerPoint", PunchSizePerPoint, said);
            var last = so.FindProperty("lastPointWorth");
            if (last != null)
            {
                said.Add(last.intValue == LastPointWorth ? $"lastPointWorth {LastPointWorth} (already)" : $"lastPointWorth {last.intValue} -> {LastPointWorth}");
                last.intValue = LastPointWorth;
            }
            if (so.ApplyModifiedProperties())
            {
                EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
                said.Add("SAVE THE SCENE");
            }
            return string.Join(", ", said);
        }

        private static void Set(SerializedObject so, string field, float value, List<string> said)
        {
            var p = so.FindProperty(field);
            if (p == null) return;
            said.Add(Mathf.Approximately(p.floatValue, value) ? $"{field} {value} (already)" : $"{field} {p.floatValue} -> {value}");
            p.floatValue = value;
        }

        // 2. Candy_3 and Candy_6 out of every list in the prefabs that name them.
        private static List<string> RemoveRetired(List<string> notes, out int files)
        {
            files = 0;
            var retired = new HashSet<GameObject>();
            var guids = new List<string>();
            foreach (int n in CandyKinds.Retired)
            {
                string path = CandyKinds.PathOf(n);
                var candy = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (candy == null) continue;
                retired.Add(candy);
                guids.Add(AssetDatabase.AssetPathToGUID(path));
            }
            var done = new List<string>();
            if (retired.Count == 0) return done;

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            string backup = BackupFolder();
            foreach (var path in AssetDatabase.GetAllAssetPaths())
            {
                if (!path.StartsWith("Assets/") || !path.EndsWith(".prefab")) continue;
                if (retired.Any(r => AssetDatabase.GetAssetPath(r) == path)) continue;
                string text;
                try { text = File.ReadAllText(path); }
                catch { continue; }
                if (!guids.Any(text.Contains)) continue;
                if (stage != null && stage.assetPath == path)
                {
                    notes.Add(path + " is open in Prefab Mode: skipped (close it, then run this again)");
                    continue;
                }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var what = new List<string>();
                    Clean(root, retired, what, notes, path);
                    if (what.Count == 0) continue;
                    Backup(backup, path);
                    PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                    if (!saved)
                    {
                        notes.Add("couldn't save " + path);
                        continue;
                    }
                    files++;
                    done.Add(Path.GetFileNameWithoutExtension(path) + " (" + string.Join(", ", what) + ")");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            return done;
        }

        private static void Clean(GameObject root, HashSet<GameObject> retired, List<string> what, List<string> notes, string path)
        {
            foreach (var comp in root.GetComponentsInChildren<Component>(true))
            {
                if (comp == null) continue;
                if (comp is PlayMakerFSM fsm)
                {
                    CleanFsm(fsm, retired, what, notes, path);
                    continue;
                }
                var so = new SerializedObject(comp);
                var lists = new List<string>();
                var it = so.GetIterator();
                while (it.Next(true))
                {
                    if (!it.isArray || it.propertyType != SerializedPropertyType.Generic) continue;
                    lists.Add(it.propertyPath);
                }
                bool any = false;
                foreach (var list in lists)
                {
                    var arr = so.FindProperty(list);
                    if (arr == null || !arr.isArray) continue;
                    for (int i = arr.arraySize - 1; i >= 0; i--)
                    {
                        var e = arr.GetArrayElementAtIndex(i);
                        if (e.propertyType != SerializedPropertyType.ObjectReference || !IsRetired(e.objectReferenceValue, retired)) continue;
                        int size = arr.arraySize;
                        arr.DeleteArrayElementAtIndex(i);
                        // An object slot may only be emptied by the first delete.
                        if (arr.arraySize == size) arr.DeleteArrayElementAtIndex(i);
                        what.Add($"{comp.GetType().Name}.{list.Replace(".Array.data", "")}[{i}]");
                        any = true;
                    }
                }
                // A single field naming one: reported, never guessed.
                var single = so.GetIterator();
                while (single.Next(true))
                {
                    if (single.propertyType != SerializedPropertyType.ObjectReference || single.propertyPath.Contains(".Array.data[")) continue;
                    if (IsRetired(single.objectReferenceValue, retired))
                        notes.Add($"{path}: {comp.GetType().Name}.{single.propertyPath} is {single.objectReferenceValue.name} (a single field, left as it is)");
                }
                if (any) so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // PlayMaker: array variables, and the lists in actions (FsmGameObject[], GameObject[], an inline Fsm Array).
        private static void CleanFsm(PlayMakerFSM fsm, HashSet<GameObject> retired, List<string> what, List<string> notes, string path)
        {
            if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
            bool any = false;
            foreach (var variable in fsm.FsmVariables.ArrayVariables)
            {
                if (CleanArray(variable, retired))
                {
                    what.Add($"FSM {fsm.FsmName} array {variable.Name}");
                    any = true;
                }
            }
            foreach (var state in fsm.FsmStates)
            {
                state.LoadActions();
                if (state.ActionData.ActionCount != state.Actions.Length ||
                    state.Actions.Any(a => a == null || a.GetType().Name == "MissingAction"))
                {
                    notes.Add($"{path}: FSM {fsm.FsmName} / {state.Name} has an action that couldn't be read: its lists were left as they are");
                    continue;
                }
                bool changed = false;
                foreach (var action in state.Actions)
                {
                    foreach (var field in action.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                    {
                        var value = field.GetValue(action);
                        if (value is FsmArray inline && !inline.UseVariable && CleanArray(inline, retired))
                        {
                            changed = true;
                        }
                        else if (value is FsmGameObject[] fsmList && fsmList.Any(g => g != null && !g.UseVariable && IsRetired(g.Value, retired)))
                        {
                            field.SetValue(action, fsmList.Where(g => g == null || g.UseVariable || !IsRetired(g.Value, retired)).ToArray());
                            changed = true;
                        }
                        else if (value is GameObject[] plainList && plainList.Any(g => IsRetired(g, retired)))
                        {
                            field.SetValue(action, plainList.Where(g => !IsRetired(g, retired)).ToArray());
                            changed = true;
                        }
                        else continue;
                        what.Add($"FSM {fsm.FsmName} / {state.Name} / {action.GetType().Name}.{field.Name}");
                    }
                }
                if (!changed) continue;
                state.SaveActions();
                any = true;
            }
            if (any) EditorUtility.SetDirty(fsm);
        }

        private static bool CleanArray(FsmArray array, HashSet<GameObject> retired)
        {
            var values = array.Values;
            if (values == null || !values.Any(v => IsRetired(v as Object, retired))) return false;
            array.Values = values.Where(v => !IsRetired(v as Object, retired)).ToArray();
            array.SaveChanges();
            return true;
        }

        private static bool IsRetired(Object thing, HashSet<GameObject> retired)
        {
            if (thing == null) return false;
            if (thing is GameObject go) return retired.Contains(go);
            return thing is Component c && retired.Contains(c.gameObject);
        }

        // Documents/429R_claude_backups/2026-11-10/six_candies (a later run: its own time-stamped folder inside).
        private static string BackupFolder()
        {
            string folder = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                "429R_claude_backups", "2026-11-10", "six_candies");
            if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any())
                folder = Path.Combine(folder, "run_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            return folder;
        }

        private static void Backup(string folder, string assetPath)
        {
            string project = Path.GetDirectoryName(Application.dataPath);
            foreach (var file in new[] { assetPath, assetPath + ".meta" })
            {
                string from = Path.Combine(project, file);
                if (!File.Exists(from)) continue;
                string to = Path.Combine(folder, file);
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                File.Copy(from, to, true);
            }
        }
    }
}
