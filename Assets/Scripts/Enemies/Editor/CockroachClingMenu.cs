using System.Collections.Generic;
using Game.Enemies;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Enemies > Cockroaches Cling To The Face: every cockroach's Face Leap holds on to the player's
    /// face until a hand slaps it off (Spider Brain > attack > Cling To Face), except the cockroach bosses', which keep
    /// leaping through the player. Through Player stays on everywhere: a roach that leaps while another clings goes
    /// through. Spiders aren't changed. The base prefab (Cockroach_Smarter) first, then its variants and the roaches inside
    /// furniture; only values that differ are written, so nothing gets an override it doesn't need. Safe to run again.
    /// </summary>
    public static class CockroachClingMenu
    {
        private const string BasePrefab = "Cockroach_Smarter";

        [MenuItem("Tools/429 Game/Enemies/Cockroaches Cling To The Face")]
        public static void Run()
        {
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset != null && asset.GetComponentInChildren<SpiderBrain>(true) != null) paths.Add(path);
            }
            // The base first, so its variants and the roaches nested in furniture read its new value.
            paths.Sort((a, b) => Rank(a).CompareTo(Rank(b)));
            var log = new List<string>();
            foreach (var path in paths)
            {
                int changed = SetCling(path);
                if (changed > 0) log.Add(NameOf(path) + " " + changed);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Cockroach Cling] " + (log.Count > 0 ? "set: " + string.Join("; ", log) : "already set everywhere"));
        }

        private static string NameOf(string path) => System.IO.Path.GetFileNameWithoutExtension(path);

        private static int Rank(string path) => NameOf(path) == BasePrefab ? 0 : path.Contains("/Enemies_Smarter/") ? 1 : 2;

        // Every face leap in the prefab: Cling To Face on for a cockroach, off in a boss prefab or for a spider. Returns how
        // many it changed.
        private static int SetCling(string path)
        {
            bool bossPrefab = NameOf(path).Contains("Boss");
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int changed = 0;
                foreach (var brain in root.GetComponentsInChildren<SpiderBrain>(true))
                {
                    var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(brain.gameObject);
                    bool roach = (source != null ? source.name : brain.name).StartsWith("Cockroach");
                    var so = new SerializedObject(brain);
                    var attacks = so.FindProperty("attacks");
                    for (int i = 0; i < attacks.arraySize; i++)
                    {
                        var move = attacks.GetArrayElementAtIndex(i);
                        if (!move.FindPropertyRelative("leapToFace").boolValue) continue;
                        var cling = move.FindPropertyRelative("clingToFace");
                        bool want = roach && !bossPrefab;
                        if (cling.boolValue == want) continue;
                        cling.boolValue = want;
                        changed++;
                    }
                    if (so.hasModifiedProperties) so.ApplyModifiedPropertiesWithoutUndo();
                }
                if (changed > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                return changed;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
