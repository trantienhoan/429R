using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Gates.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Gates > Gate Finales: Giant Pumpkin In Gates 1 And 2 (user, 2026-11-18: "gate 2 and 1 learn from
    /// gate 3 ... make it fun, make it simple"): every gate ends with the giant pumpkin (Prefabs/Gates/Pumpkin_Ball_Boss,
    /// made if it's missing), whose seed leads to the rooftop's tree defence.
    /// - Gate 1 (Prefabs/PlayGround/Fire_Woods_n_Cauldron, Cauldron Defence): Pumpkin Finale on, Finale Boss and Finale
    ///   Seed (The_Seed) set: after the pot bursts the giant pumpkin drops where it stood, instead of the Daruma game.
    /// - Gate 2 (Prefabs/Bowling/Bowling_Arena, Bowling Stage): Pumpkin Finale on (after phase 2's chest), Finale Boss
    ///   set, strikes add up (3 strikes hurt a slime; a miss no longer resets them), the rules sign says so.
    /// Only those fields change; the stages' other numbers stay. Never touches the scene. "Gate Finales: As Before" turns
    /// both finales off again (and strikes back to 3 in a row).
    /// Re-runs keep each gate's own tuning (gates plan 2026-11-22): a Finale Boss that is a prefab variant of the giant
    /// pumpkin (the gate's own look) stays; the cook time lives on the cauldron prefab (5 minutes, user 2026-11-18) and
    /// is no longer written here; the bowling rules sign is only rewritten while it still shows one of the two texts.
    /// </summary>
    public static class GateFinalesMenu
    {
        private const string Log = "[Gates]";
        private const string CauldronPath = "Assets/Prefabs/PlayGround/Fire_Woods_n_Cauldron.prefab";
        private const string ArenaPath = "Assets/Prefabs/Bowling/Bowling_Arena.prefab";
        // Gate 1 carries its Gate Run on the PlayGround itself (2026-11-23).
        private const string PlayGroundPath = "Assets/Prefabs/Bases/Play_Ground_Z.prefab";
        private const string SeedPath = "Assets/Prefabs/Items/Chest/The_Seed.prefab";
        private const string RulesSimple = "STRIKE THE SLIMES\n<size=80%>3 strikes hurt one</size>";
        private const string RulesBefore = "STRIKE THE SLIMES\n<size=80%>3 strikes in a row hurts one</size>";

        [MenuItem("Tools/429 Game/Gates/Gate Finales: Giant Pumpkin In Gates 1 And 2")]
        public static void Use() => Set(true);

        [MenuItem("Tools/429 Game/Gates/Gate Finales: As Before")]
        public static void AsBefore() => Set(false);

        [MenuItem("Tools/429 Game/Gates/Gate Finales: Giant Pumpkin In Gates 1 And 2", true)]
        [MenuItem("Tools/429 Game/Gates/Gate Finales: As Before", true)]
        private static bool CanRun() => !EditorApplication.isPlaying;

        private static void Set(bool on)
        {
            if (EditorApplication.isPlaying) { Debug.LogError($"{Log} Stop play mode first; nothing changed."); return; }
            var boss = AssetDatabase.LoadAssetAtPath<GameObject>(PumpkinBallBossMenu.PrefabPath);
            if (on && boss == null) boss = PumpkinBallBossMenu.Run();
            if (on && boss == null) { Debug.LogError($"{Log} No giant pumpkin prefab; nothing changed."); return; }
            var seed = AssetDatabase.LoadAssetAtPath<GameObject>(SeedPath);
            var notes = new List<string>();

            // A gate on Gate Run (2026-11-23) always ends with its giant pumpkin: only Gate Run's boss and seed are set
            // (a gate's own variant is kept), its old stage fields are left alone, and "As Before" doesn't touch it.
            foreach (var path in new[] { CauldronPath, PlayGroundPath, ArenaPath })
            {
                if (!Has(path, RunType)) continue;
                if (on)
                    Edit(path, RunType, so =>
                    {
                        SetBoss(so, boss);
                        var s = so.FindProperty("finaleSeed");
                        if (s != null) s.objectReferenceValue = seed;
                    }, notes);
                notes.Add(System.IO.Path.GetFileNameWithoutExtension(path) + ": on Gate Run, its giant pumpkin " + (on ? "kept or set" : "unchanged"));
            }

            if (!Has(CauldronPath, RunType) && !Has(PlayGroundPath, RunType))
                Edit(CauldronPath, "Game.PlayGround.CauldronDefence", so =>
                {
                    SetBool(so, "pumpkinFinale", on);
                    if (on)
                    {
                        SetBoss(so, boss);
                        var s = so.FindProperty("finaleSeed");
                        if (s != null) s.objectReferenceValue = seed;
                    }
                    notes.Add($"gate 1 (cauldron): giant pumpkin {(on ? "ON" : "off (the Daruma game)")}");
                }, notes);

            if (!Has(ArenaPath, RunType))
                Edit(ArenaPath, "Game.Bowling.BowlingStage", so =>
                {
                    SetBool(so, "pumpkinFinale", on);
                    if (on) SetBoss(so, boss);
                    SetBool(so, "strikesAddUp", on);
                    var rules = so.FindProperty("rulesText");
                    if (rules != null && (string.IsNullOrEmpty(rules.stringValue) || rules.stringValue == RulesSimple || rules.stringValue == RulesBefore))
                        rules.stringValue = on ? RulesSimple : RulesBefore;
                    notes.Add($"gate 2 (bowling): giant pumpkin {(on ? "ON after phase 2, strikes add up" : "off (phase 3, the showdown, 3 in a row)")}");
                }, notes);

            AssetDatabase.SaveAssets();
            Debug.Log($"{Log} Gate finales: {string.Join("; ", notes)}.");
        }

        private const string RunType = "Game.Gates.GateRun";

        // Whether the prefab has a component of that type (anywhere in it).
        private static bool Has(string path, string typeName)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) return false;
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                if (c != null && c.GetType().FullName == typeName) return true;
            }
            return false;
        }

        private static void SetBool(SerializedObject so, string field, bool value)
        {
            var prop = so.FindProperty(field);
            if (prop != null) prop.boolValue = value;
        }

        // Finale Boss = the giant pumpkin, unless it already is a prefab variant of it (the gate's own look): kept.
        private static void SetBoss(SerializedObject so, GameObject boss)
        {
            var prop = so.FindProperty("finaleBoss");
            if (prop == null) return;
            var now = prop.objectReferenceValue as GameObject;
            for (var src = now != null ? PrefabUtility.GetCorrespondingObjectFromSource(now) : null; src != null;
                 src = PrefabUtility.GetCorrespondingObjectFromSource(src))
            {
                if (src == boss) return;
            }
            prop.objectReferenceValue = boss;
        }

        // Loads the prefab, edits its component of that type through a Serialized Object, saves it.
        private static void Edit(string path, string typeName, System.Action<SerializedObject> change, List<string> notes)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                notes.Add("NO " + path);
                return;
            }
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Component target = null;
                foreach (var c in root.GetComponentsInChildren<Component>(true))
                {
                    if (c != null && c.GetType().FullName == typeName) { target = c; break; }
                }
                if (target == null)
                {
                    notes.Add($"NO {typeName} in {path}");
                    return;
                }
                var so = new SerializedObject(target);
                change(so);
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                if (!saved) notes.Add("COULD NOT SAVE " + path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
