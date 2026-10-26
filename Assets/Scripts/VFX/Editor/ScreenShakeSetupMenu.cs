using System.Collections.Generic;
using System.Linq;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.VFX.Editor
{
    /// <summary>
    /// Tools > 429 Game > VFX > Set Up Screen Shakes. Safe to run again.
    /// - Pumpkin bursts shake the view a little when the player is near (Burst Shake on the pumpkins' breaks). Thrown
    ///   bottles, flip-flops and books (Grenade) and the bowling pumpkin's blast do it in code (View Shake).
    /// - The rooftop breaking (Rooftop_Base_Z's Info FSM, state End) throws the player straight up (Launch Player, their
    ///   Toughness doesn't cut it), before its big shake.
    /// - ShakeBigger (the camera rig's Info FSM, state Shake 3: treasure chests coming down, the bonus round's blast) is
    ///   cut from 3 s to Bigger Seconds, as strong as before. In the open scene: save it afterwards.
    /// </summary>
    public static class ScreenShakeSetupMenu
    {
        private static readonly string[] BurstPaths =
        {
            "Assets/Prefabs/Items/Broken_Stuffs/pumpkin_0_drop.prefab",
            "Assets/Prefabs/Items/Broken_Stuffs/pumpkin_2_Drop.prefab",
            "Assets/Prefabs/Items/Broken_Stuffs/pumpkin_evil_drop.prefab",
            "Assets/Prefabs/Items/Broken_Stuffs/Pumpkin_BOSS_Drop.prefab",
        };
        private const string RooftopPath = "Assets/Prefabs/Bases/Rooftop_Base_Z.prefab";
        private const string RooftopFsm = "Info";
        private const string RooftopBreaks = "End";
        // How high the rooftop breaking throws the player, in metres (straight up).
        private const float RooftopLaunchHeight = 3f;
        private const float BiggerSeconds = 0.8f;

        [MenuItem("Tools/429 Game/VFX/Set Up Screen Shakes")]
        public static void Run()
        {
            var notes = new List<string>();
            foreach (var path in BurstPaths) notes.Add(AddBurstShake(path));
            notes.Add(LaunchOnRooftopBreak());
            notes.Add(ShortenBigger());
            AssetDatabase.SaveAssets();
            Debug.Log("[Screen Shakes]\n- " + string.Join("\n- ", notes) +
                      "\nReloading scripts so PlayMaker reads the saved rooftop FSM again.");
            // PlayMaker keeps the actions it has already read from a prefab: only a script reload renews them.
            EditorUtility.RequestScriptReload();
        }

        private static string AddBurstShake(string path)
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return $"{name}: not found";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<BurstShake>() != null) return $"{name}: already shakes";
                root.AddComponent<BurstShake>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return $"{name}: Burst Shake added";
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static string LaunchOnRooftopBreak()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(RooftopPath) == null) return "Rooftop_Base_Z: not found";
            var root = PrefabUtility.LoadPrefabContents(RooftopPath);
            try
            {
                var fsm = root.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == RooftopFsm);
                if (fsm == null) return $"Rooftop_Base_Z: no '{RooftopFsm}' FSM";
                if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
                var state = fsm.Fsm.GetState(RooftopBreaks);
                if (state == null) return $"Rooftop_Base_Z: no '{RooftopBreaks}' state";
                state.LoadActions();
                // Saving writes back only the actions that could be read: if one couldn't, leave it alone.
                if (state.ActionData.ActionCount != state.Actions.Length ||
                    state.Actions.Any(a => a == null || a.GetType().Name == "MissingAction"))
                    return $"Rooftop_Base_Z: an action in '{RooftopBreaks}' couldn't be read, so it was left as it is";
                if (state.Actions.OfType<LaunchPlayer>().Any()) return "Rooftop_Base_Z: already throws the player up";

                var launch = new LaunchPlayer();
                launch.Reset();
                launch.height = RooftopLaunchHeight;
                launch.distance = 0f;
                launch.ignoreToughness = true;
                // Before its big shake, so the throw's own (smaller) shake doesn't cut that one short.
                var actions = state.Actions.ToList();
                int shake = actions.FindIndex(a => a is SendEvent send && send.sendEvent != null && send.sendEvent.Name.StartsWith("Shake"));
                actions.Insert(shake >= 0 ? shake : actions.Count, launch);
                state.Actions = actions.ToArray();
                state.SaveActions();
                EditorUtility.SetDirty(fsm);
                PrefabUtility.SaveAsPrefabAsset(root, RooftopPath);
                return $"Rooftop_Base_Z: '{RooftopBreaks}' throws the player {RooftopLaunchHeight} m up";
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // The camera rig's Info FSM in the open scene(s): its Shake 3 tween (ShakeBigger) lasts Bigger Seconds.
        private static string ShortenBigger()
        {
            int changed = 0, found = 0;
            foreach (var fsm in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
            {
                if (fsm.FsmName != "Info" || !fsm.gameObject.scene.IsValid() || EditorUtility.IsPersistent(fsm)) continue;
                if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
                var state = fsm.Fsm.GetState("Shake 3");
                if (state == null || !fsm.Fsm.GlobalTransitions.Any(t => t.EventName == "ShakeBigger")) continue;
                found++;
                state.LoadActions();
                foreach (var action in state.Actions)
                {
                    if (action == null || action.GetType().Name != "TweenPunch") continue;
                    if (!(action.GetType().GetField("time")?.GetValue(action) is FsmFloat time) || Mathf.Approximately(time.Value, BiggerSeconds)) continue;
                    Undo.RecordObject(fsm, "Shorten ShakeBigger");
                    time.Value = BiggerSeconds;
                    state.SaveActions();
                    EditorUtility.SetDirty(fsm);
                    EditorSceneManager.MarkSceneDirty(fsm.gameObject.scene);
                    changed++;
                }
            }
            if (found == 0) return "ShakeBigger: no camera Info FSM in the open scene (open 2 Game Scene and run this again)";
            return changed > 0 ? $"ShakeBigger: now {BiggerSeconds} s (was 3 s) - save the scene" : $"ShakeBigger: already {BiggerSeconds} s";
        }
    }
}
