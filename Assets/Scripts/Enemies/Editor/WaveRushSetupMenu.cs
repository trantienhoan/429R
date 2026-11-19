using System.Collections.Generic;
using System.Linq;
using Game.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Enemies > Set Up Rush Moments (user, 2026-10-22: "more enemies = more chaos" in the Bedroom and
    /// Playroom fights; built 2026-11-19). Puts a Wave Rush on -EnemyManager 1 (the Bedroom: Spider_Small pour in from
    /// its SpiderIdleSpawns) and -EnemyManager 3 (the Playroom: Cockroach_Small from its BeeSpawns), joined to the
    /// manager's EnemyManager FSM. A Wave Rush already there keeps its numbers; only empty slots are filled. The open
    /// scene is marked changed, not saved: save it to keep it.
    /// </summary>
    public static class WaveRushSetupMenu
    {
        private const string MenuPath = "Tools/429 Game/Enemies/Set Up Rush Moments";
        private const string Enemies = "Assets/Prefabs/Enemies/Enemies_Smarter/";
        private const string Sfx = "Assets/Audio/SFX/";

        private static readonly (string manager, string spawns, string enemy, string startSound)[] Rooms =
        {
            ("-EnemyManager 1", "SpiderIdleSpawns", "Spider_Small", "creepy_angry_ghost_laugh.wav"),
            ("-EnemyManager 3", "BeeSpawns", "Cockroach_Small", "cockroach_screech.wav"),
        };
        private const string ClearSound = "Clear,_resonant_bell_1.wav";

        [MenuItem(MenuPath)]
        public static void SetUp()
        {
            var log = new List<string>();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var candies = Game.Inventory.CandyKinds.Numbers
                .Select(i => AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Items/Breakable/Candy_{i}.prefab"))
                .Where(c => c != null).ToArray();
            foreach (var room in Rooms)
            {
                // Managers are often switched off until their stage (GAMESTAGES turns them on): look among those too.
                var manager = Resources.FindObjectsOfTypeAll<GameObject>()
                    .FirstOrDefault(g => g.name == room.manager && g.scene == scene && g.hideFlags == HideFlags.None);
                if (manager == null)
                {
                    log.Add($"{room.manager}: NOT FOUND in {scene.name}");
                    continue;
                }
                var fsm = manager.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "EnemyManager");
                if (fsm == null)
                {
                    log.Add($"{room.manager}: no EnemyManager FSM, skipped");
                    continue;
                }
                bool made = !manager.TryGetComponent(out WaveRush rush);
                if (made) rush = Undo.AddComponent<WaveRush>(manager);
                var so = new SerializedObject(rush);
                var notes = new List<string>();
                Fill(so, "manager", fsm, notes);
                Fill(so, "spawnPoints", manager.transform.Find(room.spawns), notes);
                Fill(so, "rushEnemy", AssetDatabase.LoadAssetAtPath<GameObject>(Enemies + room.enemy + ".prefab"), notes);
                Fill(so, "startSound", AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + room.startSound), notes);
                Fill(so, "clearSound", AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + ClearSound), notes);
                var list = so.FindProperty("candies");
                if (list.arraySize == 0 && candies.Length > 0)
                {
                    list.arraySize = candies.Length;
                    for (int i = 0; i < candies.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = candies[i];
                    notes.Add($"candies: {candies.Length} kinds");
                }
                so.ApplyModifiedProperties();
                int points = rush != null && so.FindProperty("spawnPoints").objectReferenceValue is Transform t ? t.childCount : 0;
                log.Add($"{room.manager}: Wave Rush {(made ? "added" : "kept")} ({room.enemy} x{so.FindProperty("rushCount").intValue}, " +
                        $"after {so.FindProperty("startAfterKills").intValue} down, {points} spawn points" +
                        (notes.Count > 0 ? "; filled " + string.Join(", ", notes) : "") + ")");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Rush Moments] " + string.Join("\n", log) + "\nSave the scene to keep it.");
        }

        [MenuItem(MenuPath, true)]
        private static bool CanSetUp() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static void Fill(SerializedObject so, string property, Object value, List<string> notes)
        {
            var p = so.FindProperty(property);
            if (p == null || p.objectReferenceValue != null || value == null) return;
            p.objectReferenceValue = value;
            notes.Add($"{property}: {value.name}");
        }
    }
}
