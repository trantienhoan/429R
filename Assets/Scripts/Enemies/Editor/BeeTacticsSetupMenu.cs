using System.Collections.Generic;
using Game.Enemies;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Enemies > Smarter Bees (the user's picks, 2026-10-27), on Spider Brain > Bee tactics and the
    /// stings:
    /// - Swarm dives: when a bee dives, up to 2 circling bees within 7 m, 50 degrees or more round the player from it,
    ///   dive 0.1-0.45 s later (base prefab, so every bee has it).
    /// - Feint dives: 3 in 10 dives are feints: swung at on the way in, the bee always pulls up, then strikes straight
    ///   back with a wind-up 0.4 as long.
    /// - Poison sting: Sting and Ground Sting slow the player to 60% for 1.5 s (stings in a row keep them slow).
    /// - Boss rush: the boss bees, every 9-12 s, order every bee within 15 m to dive at once after a deep buzz (bee_fly
    ///   on the boss) and a camera shake: 1.2 s of hanging up high, then they all come down.
    /// Base prefab first, so the variants inherit; each one's stings are set too (a variant can have its own attack
    /// list). A prefab open in Prefab Mode with unsaved changes is left alone. Safe to run again.
    /// </summary>
    public static class BeeTacticsSetupMenu
    {
        private const string Folder = "Assets/Prefabs/Enemies/Enemies_Smarter/";
        private const string RushSoundPath = "Assets/Audio/SFX/bee_fly.wav";
        private static readonly string[] Bees = { "Bee_Smarter", "Bee_Small", "Bee_Normal", "Bee_Boss", "Bee_Boss_PumpkinButt" };
        private static readonly string[] Bosses = { "Bee_Boss", "Bee_Boss_PumpkinButt" };
        private static readonly string[] Stings = { "Sting", "Ground Sting" };
        private const float PoisonSlowTo = 0.6f;
        private const float PoisonSeconds = 1.5f;

        [MenuItem("Tools/429 Game/Enemies/Smarter Bees")]
        public static void Run()
        {
            var log = new List<string>();
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            var rushSound = AssetDatabase.LoadAssetAtPath<AudioClip>(RushSoundPath);
            foreach (var name in Bees)
            {
                string path = Folder + name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { log.Add(name + " missing"); continue; }
                if (stage != null && stage.assetPath == path && stage.scene.isDirty) { log.Add(name + " has unsaved changes in Prefab Mode: left alone"); continue; }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var so = new SerializedObject(root.GetComponent<SpiderBrain>());
                    var done = new List<string>();
                    if (name == "Bee_Smarter")
                    {
                        so.FindProperty("swarmDives").boolValue = true;
                        so.FindProperty("swarmJoin").intValue = 2;
                        so.FindProperty("swarmRange").floatValue = 7f;
                        so.FindProperty("swarmDelay").vector2Value = new Vector2(0.1f, 0.45f);
                        so.FindProperty("swarmSpread").floatValue = 50f;
                        so.FindProperty("feintChance").floatValue = 0.3f;
                        so.FindProperty("feintWindUp").floatValue = 0.4f;
                        done.Add("swarm dives + feints");
                    }
                    if (System.Array.IndexOf(Bosses, name) >= 0)
                    {
                        so.FindProperty("bossRush").boolValue = true;
                        so.FindProperty("rushEvery").vector2Value = new Vector2(9f, 12f);
                        so.FindProperty("rushRange").floatValue = 15f;
                        so.FindProperty("rushWarning").floatValue = 1.2f;
                        so.FindProperty("rushSound").objectReferenceValue = rushSound;
                        so.FindProperty("rushShake").stringValue = "ShakeMed";
                        done.Add("boss rush" + (rushSound == null ? " (NO bee_fly sound found)" : ""));
                    }
                    var attacks = so.FindProperty("attacks");
                    int poisoned = 0;
                    for (int i = 0; i < attacks.arraySize; i++)
                    {
                        var move = attacks.GetArrayElementAtIndex(i);
                        if (System.Array.IndexOf(Stings, move.FindPropertyRelative("name").stringValue) < 0) continue;
                        move.FindPropertyRelative("slowPlayerTo").floatValue = PoisonSlowTo;
                        move.FindPropertyRelative("slowSeconds").floatValue = PoisonSeconds;
                        poisoned++;
                    }
                    done.Add(poisoned + " stings poisoned");
                    if (so.ApplyModifiedPropertiesWithoutUndo())
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        log.Add(name + ": " + string.Join(", ", done));
                    }
                    else log.Add(name + ": already set");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Smarter Bees] " + string.Join("; ", log));
        }
    }
}
