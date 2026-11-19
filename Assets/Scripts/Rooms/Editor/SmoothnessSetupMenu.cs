using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Smoothness pass (2026-11-19), from an editor profile of the bedroom stages:
    /// - Tools > 429 Game > Performance > Spread Room Set Spawns: a Staggered Spawn on every room set under -GAMESTAGES
    ///   (--Bedroom--, --MiniBedroom_Chaos--, --Livingroom_Jenga-- ...) with 8 or more spawn points; its simple spawn points
    ///   are switched off in the scene and it switches them on a few a frame, so a room's furniture comes over a few
    ///   frames instead of in one 60-160 ms freeze. Restore Room Set Spawns undoes it. The open scene is marked changed,
    ///   not saved.
    /// - Tools > 429 Game > Performance > Preload Short Sounds: sounds that load the first time they play (a 2-12 ms freeze
    ///   each) load with the scene instead when under 1 MB, or in the background when bigger. Music that streams is
    ///   left alone.
    /// </summary>
    public static class SmoothnessSetupMenu
    {
        private const string Root = "Tools/429 Game/Performance/";
        private const int MinSpawnPoints = 8;
        private const long PreloadUnderBytes = 1024 * 1024;

        [MenuItem(Root + "Spread Room Set Spawns")]
        public static void SpreadRoomSetSpawns()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var stages = Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(g => g.name == "-GAMESTAGES" && g.scene == scene && g.hideFlags == HideFlags.None);
            if (stages == null)
            {
                Debug.LogError("[Smoothness] No -GAMESTAGES in " + scene.name + ". Nothing changed.");
                return;
            }
            var done = new List<string>();
            foreach (Transform set in stages.transform)
            {
                if (!set.name.StartsWith("--") || set.childCount < MinSpawnPoints) continue;
                if (!set.TryGetComponent(out StaggeredSpawn spread)) spread = Undo.AddComponent<StaggeredSpawn>(set.gameObject);
                // The simple spawn points: on in the scene, every FSM one state that ends there (make it, done).
                var points = new List<GameObject>(spread.Points.Where(p => p != null));
                int left = 0;
                foreach (Transform child in set)
                {
                    var go = child.gameObject;
                    if (points.Contains(go)) continue;
                    var fsms = go.GetComponents<PlayMakerFSM>();
                    bool simple = go.activeSelf && fsms.Length > 0 &&
                                  fsms.All(f => f.FsmStates.Length == 1 && f.FsmStates[0].Transitions.Length == 0);
                    if (!simple)
                    {
                        left++;
                        continue;
                    }
                    Undo.RecordObject(go, "Spread Room Set Spawns");
                    go.SetActive(false);
                    points.Add(go);
                }
                var so = new SerializedObject(spread);
                var list = so.FindProperty("points");
                list.arraySize = points.Count;
                for (int i = 0; i < points.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
                so.ApplyModifiedProperties();
                done.Add($"{set.name} {points.Count}/{set.childCount}" + (left > 0 ? $" ({left} left as they were)" : ""));
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Smoothness] Room sets now spawn a few points a frame ({done.Count}): {string.Join(", ", done)}" +
                      "\nTheir points are kept switched off in the scene (the set's Staggered Spawn switches them on). Save the scene to keep it.");
        }

        [MenuItem(Root + "Restore Room Set Spawns")]
        public static void RestoreRoomSetSpawns()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            int sets = 0, points = 0;
            foreach (var spread in Resources.FindObjectsOfTypeAll<StaggeredSpawn>())
            {
                if (spread.gameObject.scene != scene) continue;
                foreach (var p in spread.Points)
                {
                    if (p == null || p.activeSelf) continue;
                    Undo.RecordObject(p, "Restore Room Set Spawns");
                    p.SetActive(true);
                    points++;
                }
                Undo.DestroyObjectImmediate(spread);
                sets++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Smoothness] Restored {sets} room sets ({points} spawn points switched back on, Staggered Spawn removed). Save the scene to keep it.");
        }

        [MenuItem(Root + "Restore Room Set Spawns", true)]
        private static bool CanRestore() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem(Root + "Spread Room Set Spawns", true)]
        private static bool CanSpread() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem(Root + "Preload Short Sounds")]
        public static void PreloadShortSounds()
        {
            var preloaded = new List<string>();
            var background = new List<string>();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetImporter.GetAtPath(path) is not AudioImporter importer) continue;
                    var settings = importer.defaultSampleSettings;
                    if (settings.loadType == AudioClipLoadType.Streaming || settings.preloadAudioData) continue;
                    long bytes = new FileInfo(path).Length;
                    if (bytes < PreloadUnderBytes)
                    {
                        settings.preloadAudioData = true;
                        importer.defaultSampleSettings = settings;
                        preloaded.Add(Path.GetFileName(path));
                    }
                    else if (!importer.loadInBackground)
                    {
                        importer.loadInBackground = true;
                        background.Add(Path.GetFileName(path));
                    }
                    else continue;
                    importer.SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            Debug.Log($"[Smoothness] Sounds that loaded the first time they played: {preloaded.Count} now load with the scene " +
                      $"(under 1 MB), {background.Count} bigger ones load in the background: {string.Join(", ", background)}");
        }

        [MenuItem(Root + "Preload Short Sounds", true)]
        private static bool CanPreload() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
