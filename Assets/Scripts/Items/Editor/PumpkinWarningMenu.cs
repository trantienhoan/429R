using System.Collections.Generic;
using System.Linq;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Items > Warn Before Pumpkins Transform: the stage pumpkins (Pumpkin_0, 1, 2, 4, 6) get angry in
    /// the last minute before they turn into the pumpkin boss. A Warn Before Transform action goes last in the state of
    /// their Wait and Transform FSM that waits (WAIT, Wait 360s, Wait 300s, wait 300s), with the pumpkin laughs and the
    /// low rumble. Only that state is saved again. Running it again changes nothing.
    /// </summary>
    public static class PumpkinWarningMenu
    {
        private const string Folder = "Assets/Prefabs/Items/Breakable/";
        private static readonly string[] Pumpkins = { "Pumpkin_0", "Pumpkin_1", "Pumpkin_2", "Pumpkin_4", "Pumpkin_6" };
        private const string FsmName = "Wait and Transform";
        private static readonly string[] LaughPaths =
        {
            "Assets/Audio/SFX/pumpkin_laugh_1.wav", "Assets/Audio/SFX/pumpkin_laugh_2.wav",
            "Assets/Audio/SFX/pumpkin_laugh_3.wav", "Assets/Audio/SFX/pumpkin_laugh_4.wav",
        };
        private const string RumblePath = "Assets/Audio/SFX/Low,_rumbling_hmmmm.wav";
        // Only a wait this long is the countdown to the boss.
        private const float LongWait = 60f;

        [MenuItem("Tools/429 Game/Items/Warn Before Pumpkins Transform")]
        public static void AddWarnings()
        {
            var laughs = LaughPaths.Select(AssetDatabase.LoadAssetAtPath<AudioClip>).Where(c => c != null).ToArray();
            var rumble = AssetDatabase.LoadAssetAtPath<AudioClip>(RumblePath);
            if (laughs.Length == 0 || rumble == null)
            {
                Debug.LogError("[Pumpkins] The pumpkin laughs or the rumble sound (Assets/Audio/SFX) are missing; nothing changed.");
                return;
            }
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();

            var notes = new List<string>();
            foreach (var name in Pumpkins)
            {
                string path = Folder + name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { notes.Add(name + ": missing"); continue; }
                if (stage != null && stage.assetPath == path) { notes.Add(name + ": open in Prefab Mode, skipped (close it and run again)"); continue; }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    notes.Add(name + ": " + AddTo(root, laughs, rumble, path));
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            Debug.Log("[Pumpkins] Warn before transform: " + string.Join("; ", notes) + ".");
        }

        [MenuItem("Tools/429 Game/Items/Warn Before Pumpkins Transform", true)]
        private static bool NotPlaying() => !EditorApplication.isPlaying;

        private static string AddTo(GameObject root, AudioClip[] laughs, AudioClip rumble, string path)
        {
            var fsm = root.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == FsmName);
            if (fsm == null) return "no " + FsmName + " FSM, skipped";
            if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
            foreach (var s in fsm.FsmStates) s.LoadActions();

            var waiting = fsm.FsmStates.FirstOrDefault(s => s.Actions.OfType<Wait>().Any(w => w.time.Value >= LongWait));
            if (waiting == null) return "no state with a long Wait, skipped";
            if (waiting.Actions.OfType<WarnBeforeTransform>().Any()) return $"'{waiting.Name}' already warns";
            // A state whose actions didn't all load would lose the missing ones when saved: stop instead.
            if (waiting.ActionData.ActionCount != waiting.Actions.Length)
                return $"'{waiting.Name}' has {waiting.ActionData.ActionCount} actions saved but {waiting.Actions.Length} loaded, skipped";

            var warn = new WarnBeforeTransform();
            warn.Reset();
            warn.gameObject = new FsmOwnerDefault();
            warn.laughSounds = laughs;
            warn.rumbleSound = new FsmObject { Value = rumble, ObjectType = typeof(AudioClip) };
            int before = waiting.Actions.Length;
            waiting.Actions = waiting.Actions.Append(warn).ToArray();
            waiting.SaveActions();
            EditorUtility.SetDirty(fsm);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return $"'{waiting.Name}' {before} -> {waiting.Actions.Length} actions";
        }
    }
}
