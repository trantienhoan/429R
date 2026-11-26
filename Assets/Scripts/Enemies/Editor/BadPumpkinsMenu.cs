using System.Collections.Generic;
using Game.Enemies;
using Game.Inventory;
using Game.Rooms;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// User 2026-11-26: "I want them Pumpkins to have chance to become a bomb pumpkin but still keep the functions of the
    /// Pumpkin 3 5 7 so the game can still progress, just not dropping candies anymore ... 50 50 chance to spawn pumpkin
    /// 3 5 7 or Bad_pumpkin_3 5 7 ... they drop a bunch of spiders and Pumpkin only have 1 health".
    /// Tools > 429 Game > Enemies > Set Up Bad Pumpkins (re-runnable; prefabs only, no scene change):
    /// - Prefabs/Items/Broken_Stuffs/pumpkin_evil_drop_No_Candy: the broken pumpkin pieces without their 2 candy spots.
    /// - Prefabs/Items/Breakable/Bad_Pumpkin_3, _5, _7: made again from Pumpkin_3, 5, 7 each run (so changes to the good
    ///   ones carry over), with: Health 1, no Candy Trail, the candy-free pieces, Bomb Pumpkin (the box bombs' rattle,
    ///   beep, bang and effects) and a Bug Horde that always lets 6-8 Spider_Small out. Their FSMs are otherwise the
    ///   good ones', so they set Pumpkin_3 / Pumpkin_5 in GAMESTAGES or make Pumpkin_7's skulls like before.
    /// - Pumpkin_3, 5, 7 get Pumpkin Coin Flip (Bad Chance 0.5; a re-run keeps a chance changed by hand).
    /// Tools > 429 Game > Enemies > Remove Bad Pumpkins takes the coin flips off again (the bad prefabs stay, unused).
    /// </summary>
    public static class BadPumpkinsMenu
    {
        private const string Tag = "[Bad Pumpkins]";
        private const string Folder = "Assets/Prefabs/Items/Breakable/";
        private const string DropPath = "Assets/Prefabs/Items/Broken_Stuffs/pumpkin_evil_drop.prefab";
        private const string NoCandyDropPath = "Assets/Prefabs/Items/Broken_Stuffs/pumpkin_evil_drop_No_Candy.prefab";
        private const string SpiderPath = "Assets/Prefabs/Enemies/Enemies_Smarter/Spider_Small.prefab";
        private const string BoxBombFrom = "Assets/Prefabs/Items/Breakable/Paper_Box_1.prefab";
        private const string HordeFrom = "Assets/Prefabs/BedRoom/Bedroom_Bed.prefab";
        private static readonly int[] Numbers = { 3, 5, 7 };

        [MenuItem("Tools/429 Game/Enemies/Set Up Bad Pumpkins")]
        public static void SetUp()
        {
            var spider = AssetDatabase.LoadAssetAtPath<GameObject>(SpiderPath);
            var boxBomb = Load<BoxBomb>(BoxBombFrom);
            var horde = Load<BugHorde>(HordeFrom);
            if (spider == null || boxBomb == null || horde == null || AssetDatabase.LoadAssetAtPath<GameObject>(DropPath) == null)
            {
                Debug.LogError($"{Tag} Missing {SpiderPath}, {BoxBombFrom}'s Box Bomb, {HordeFrom}'s Bug Horde or {DropPath}; nothing changed.");
                return;
            }
            var notes = new List<string>();
            var noCandyDrop = NoCandyDrop(notes);
            foreach (int n in Numbers)
            {
                string goodPath = Folder + "Pumpkin_" + n + ".prefab";
                string badPath = Folder + "Bad_Pumpkin_" + n + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(goodPath) == null)
                {
                    notes.Add($"WARNING: {goodPath} missing");
                    continue;
                }
                MakeBad(n, goodPath, badPath, noCandyDrop, spider, boxBomb, horde, notes);
                AddCoinFlip(goodPath, AssetDatabase.LoadAssetAtPath<GameObject>(badPath), notes);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} {string.Join("; ", notes)}");
        }

        [MenuItem("Tools/429 Game/Enemies/Remove Bad Pumpkins")]
        public static void Remove()
        {
            var notes = new List<string>();
            foreach (int n in Numbers)
            {
                string goodPath = Folder + "Pumpkin_" + n + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(goodPath) == null) continue;
                var root = PrefabUtility.LoadPrefabContents(goodPath);
                try
                {
                    var flip = root.GetComponent<PumpkinCoinFlip>();
                    if (flip == null) continue;
                    Object.DestroyImmediate(flip);
                    PrefabUtility.SaveAsPrefabAsset(root, goodPath);
                    notes.Add("Pumpkin_" + n + ": coin flip removed");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            Debug.Log($"{Tag} {(notes.Count > 0 ? string.Join("; ", notes) : "no coin flips to remove")}. The Bad_Pumpkin prefabs are kept (unused).");
        }

        [MenuItem("Tools/429 Game/Enemies/Set Up Bad Pumpkins", true)]
        [MenuItem("Tools/429 Game/Enemies/Remove Bad Pumpkins", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // The broken pieces without their candy spots.
        private static GameObject NoCandyDrop(List<string> notes)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(NoCandyDropPath) == null)
            {
                AssetDatabase.CopyAsset(DropPath, NoCandyDropPath);
                notes.Add("made pumpkin_evil_drop_No_Candy");
            }
            var root = PrefabUtility.LoadPrefabContents(NoCandyDropPath);
            try
            {
                int removed = 0;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t == null || t == root.transform || !t.name.StartsWith("Candy_Pos")) continue;
                    Object.DestroyImmediate(t.gameObject);
                    removed++;
                }
                if (removed > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, NoCandyDropPath);
                    notes.Add($"pumpkin_evil_drop_No_Candy: {removed} candy spots removed");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(NoCandyDropPath);
        }

        private static void MakeBad(int n, string goodPath, string badPath, GameObject noCandyDrop, GameObject spider,
            BoxBomb boxBomb, BugHorde hordeFrom, List<string> notes)
        {
            var root = PrefabUtility.LoadPrefabContents(goodPath);
            try
            {
                root.name = "Bad_Pumpkin_" + n;
                var flip = root.GetComponent<PumpkinCoinFlip>();
                if (flip != null) Object.DestroyImmediate(flip);
                var trail = root.GetComponent<CandyTrail>();
                if (trail != null) Object.DestroyImmediate(trail);

                // One health.
                string health = "no Health FSM";
                foreach (var fsm in root.GetComponents<PlayMakerFSM>())
                {
                    if (fsm.FsmName != "Health") continue;
                    var so = new SerializedObject(fsm);
                    var floats = so.FindProperty("fsm.variables.floatVariables");
                    for (int i = 0; floats != null && i < floats.arraySize; i++)
                    {
                        var v = floats.GetArrayElementAtIndex(i);
                        if (v.FindPropertyRelative("name").stringValue != "health") continue;
                        v.FindPropertyRelative("value").floatValue = 1f;
                        health = "health 1";
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                // The pieces it breaks into: no candies.
                string pieces = "pieces NOT changed (no crack drop spot)";
                foreach (var fsm in root.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    if (!fsm.gameObject.name.EndsWith("_crack_drop_pos")) continue;
                    fsm.Fsm.Init(fsm);
                    foreach (var state in fsm.Fsm.States)
                    {
                        state.LoadActions();
                        bool changed = false;
                        foreach (var action in state.Actions)
                        {
                            if (!(action is HutongGames.PlayMaker.Actions.CreateObject create)) continue;
                            if (create.gameObject == null) create.gameObject = new HutongGames.PlayMaker.FsmGameObject();
                            create.gameObject.Value = noCandyDrop;
                            changed = true;
                        }
                        if (!changed) continue;
                        state.SaveActions();
                        EditorUtility.SetDirty(fsm);
                        pieces = "candy-free pieces";
                    }
                }

                // The bang (the box bombs' sounds and effects) and the spiders.
                var bomb = root.GetComponent<BombPumpkin>();
                if (bomb == null) bomb = root.AddComponent<BombPumpkin>();
                var from = new SerializedObject(boxBomb);
                var to = new SerializedObject(bomb);
                to.FindProperty("rattle").objectReferenceValue = from.FindProperty("rattle").objectReferenceValue;
                to.FindProperty("beep").objectReferenceValue = from.FindProperty("beep").objectReferenceValue;
                to.FindProperty("bangSound").objectReferenceValue = from.FindProperty("bangSound").objectReferenceValue;
                to.CopyFromSerializedProperty(from.FindProperty("effects"));
                to.ApplyModifiedPropertiesWithoutUndo();

                var horde = root.GetComponent<BugHorde>();
                if (horde == null) horde = root.AddComponent<BugHorde>();
                var hs = new SerializedObject(horde);
                var hf = new SerializedObject(hordeFrom);
                hs.FindProperty("chance").floatValue = 1f;
                hs.FindProperty("count").vector2IntValue = new Vector2Int(6, 8);
                var bugs = hs.FindProperty("bugs");
                bugs.arraySize = 1;
                bugs.GetArrayElementAtIndex(0).objectReferenceValue = spider;
                hs.FindProperty("burstSeconds").floatValue = 0.5f;
                hs.FindProperty("comeAtOnce").boolValue = true;
                hs.FindProperty("keepFreeForWaves").intValue = 3;
                hs.FindProperty("puff").objectReferenceValue = hf.FindProperty("puff").objectReferenceValue;
                hs.FindProperty("puffSize").floatValue = 0.35f;
                hs.FindProperty("popSound").objectReferenceValue = hf.FindProperty("popSound").objectReferenceValue;
                hs.FindProperty("popVolume").floatValue = 0.5f;
                hs.FindProperty("healthFsm").stringValue = "Health";
                hs.FindProperty("healthVariable").stringValue = "health";
                hs.ApplyModifiedPropertiesWithoutUndo();

                bool had = AssetDatabase.LoadAssetAtPath<GameObject>(badPath) != null;
                PrefabUtility.SaveAsPrefabAsset(root, badPath);
                notes.Add($"Bad_Pumpkin_{n} {(had ? "made again" : "made")}: {health}, no candy trail, {pieces}, bomb, 6-8 Spider_Small");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AddCoinFlip(string goodPath, GameObject bad, List<string> notes)
        {
            var root = PrefabUtility.LoadPrefabContents(goodPath);
            try
            {
                var flip = root.GetComponent<PumpkinCoinFlip>();
                bool fresh = flip == null;
                if (fresh) flip = root.AddComponent<PumpkinCoinFlip>();
                var so = new SerializedObject(flip);
                so.FindProperty("badVersion").objectReferenceValue = bad;
                if (fresh) so.FindProperty("badChance").floatValue = 0.5f;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, goodPath);
                notes.Add($"{root.name}: coin flip {(fresh ? "added" : "kept")} ({so.FindProperty("badChance").floatValue:0.##} bad)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static T Load<T>(string path) where T : Component
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return go != null ? go.GetComponent<T>() : null;
        }
    }
}
