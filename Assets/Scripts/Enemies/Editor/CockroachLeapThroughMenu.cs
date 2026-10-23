using System.Collections.Generic;
using Game.Enemies;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Enemies > Cockroaches Leap Through The Player: every cockroach's Face Leap, and the spider
    /// bosses' Pounce, goes on through the player's head and lands behind them (Spider Brain > attack > Through Player,
    /// 1.2 m), instead of dropping back off the face and running away. Base prefab first, so the variants inherit it;
    /// ones with their own attack list (the bosses) are set too. Safe to run again.
    /// </summary>
    public static class CockroachLeapThroughMenu
    {
        private const string Folder = "Assets/Prefabs/Enemies/Enemies_Smarter/";
        // Each prefab and the name of its leap at the face.
        private static readonly (string prefab, string leap)[] Leapers =
        {
            ("Cockroach_Smarter", "Face Leap"), ("Cockroach_Normal", "Face Leap"), ("Cockroach_Small", "Face Leap"),
            ("Cockroach_Boss", "Face Leap"), ("Cockroach_Boss_PumpkinButt", "Face Leap"),
            ("Spider_Boss", "Pounce"), ("Spider_Boss_PumpkinButt", "Pounce"),
        };
        private const float ThroughDistance = 1.2f;

        [MenuItem("Tools/429 Game/Enemies/Cockroaches Leap Through The Player")]
        public static void Run()
        {
            var log = new List<string>();
            foreach (var (name, leap) in Leapers)
            {
                string path = Folder + name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { log.Add(name + " missing"); continue; }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var so = new SerializedObject(root.GetComponent<SpiderBrain>());
                    var attacks = so.FindProperty("attacks");
                    bool changed = false, found = false;
                    for (int i = 0; i < attacks.arraySize; i++)
                    {
                        var move = attacks.GetArrayElementAtIndex(i);
                        if (move.FindPropertyRelative("name").stringValue != leap) continue;
                        found = true;
                        var through = move.FindPropertyRelative("throughPlayer");
                        var distance = move.FindPropertyRelative("throughDistance");
                        if (!through.boolValue || !Mathf.Approximately(distance.floatValue, ThroughDistance))
                        {
                            through.boolValue = true;
                            distance.floatValue = ThroughDistance;
                            changed = true;
                        }
                    }
                    if (changed)
                    {
                        so.ApplyModifiedPropertiesWithoutUndo();
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                    log.Add(name + (found ? (changed ? " set" : " already set") : " has no " + leap));
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Cockroach Leap] " + string.Join("; ", log));
        }
    }
}
