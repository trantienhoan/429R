using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Story;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Story > Set Up Dark Walk (user 2026-11-25: "re-do the 1st tutorial stage a little bit, make
    /// everything dark except the fairy, so player have to walk in the dark mostly with only fairy is the light source").
    /// Makes Resources/Story Dark Walk Settings (if missing) and puts a Story Darkness action FIRST in two GAMESTAGES
    /// states: "Walk Tuttorial" (Begin Dark, 1.2 s: the run starts in the dark) and "Send death" (End Dark, 2.5 s: the
    /// Fairy is reached and the floor shows up). Re-runnable; "Undo Dark Walk" takes both actions out. Changes the scene
    /// (GAMESTAGES): save it after.
    /// </summary>
    public static class StoryDarkWalkMenu
    {
        private const string Tag = "[Story Dark Walk]";
        private const string SettingsPath = "Assets/Resources/Story Dark Walk Settings.asset";
        private static readonly (string state, StoryDarkness.Mode mode, float seconds)[] Places =
        {
            ("Walk Tuttorial", StoryDarkness.Mode.BeginDark, 1.2f),
            ("Send death", StoryDarkness.Mode.EndDark, 2.5f),
        };

        [MenuItem("Tools/429 Game/Story/Set Up Dark Walk")]
        public static void SetUp() => Run(false);

        [MenuItem("Tools/429 Game/Story/Undo Dark Walk")]
        public static void UndoDarkWalk() => Run(true);

        [MenuItem("Tools/429 Game/Story/Set Up Dark Walk", true)]
        [MenuItem("Tools/429 Game/Story/Undo Dark Walk", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static void Run(bool undo)
        {
            var done = new List<string>();
            if (!undo && AssetDatabase.LoadAssetAtPath<StoryDarkWalkSettings>(SettingsPath) == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<StoryDarkWalkSettings>(), SettingsPath);
                AssetDatabase.SaveAssets();
                done.Add("made " + SettingsPath);
            }
            PlayMakerFSM stages = null;
            foreach (var f in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
            {
                if (!f.gameObject.scene.IsValid() || f.FsmName != "GAMESTAGES") continue;
                stages = f;
                break;
            }
            if (stages == null)
            {
                Debug.LogError(Tag + " No GAMESTAGES in the open scene: open 2 Game Scene and run it again.");
                return;
            }
            if (!stages.Fsm.Initialized) stages.Fsm.Init(stages);
            foreach (var (name, mode, seconds) in Places)
            {
                var state = stages.Fsm.GetState(name);
                if (state == null)
                {
                    Debug.LogError($"{Tag} GAMESTAGES has no '{name}' state.");
                    continue;
                }
                state.LoadActions();
                if (state.ActionData.ActionCount != state.Actions.Length)
                {
                    Debug.LogError($"{Tag} '{name}' has an action that could not be read; left alone (saving would drop it).");
                    continue;
                }
                bool has = state.Actions.OfType<StoryDarkness>().Any();
                if (undo)
                {
                    if (!has) continue;
                    state.Actions = state.Actions.Where(a => !(a is StoryDarkness)).ToArray();
                    state.SaveActions();
                    done.Add($"Story Darkness out of '{name}'");
                    continue;
                }
                if (has) continue;
                var dark = new StoryDarkness();
                dark.Reset();
                dark.mode = mode;
                dark.fadeSeconds = seconds;
                state.Actions = new FsmStateAction[] { dark }.Concat(state.Actions).ToArray();
                state.SaveActions();
                done.Add($"Story Darkness ({mode}, {seconds} s) first in '{name}'");
            }
            if (done.Count == 0)
            {
                Debug.Log($"{Tag} Nothing to change (already {(undo ? "undone" : "set up")}).");
                return;
            }
            EditorUtility.SetDirty(stages);
            EditorSceneManager.MarkSceneDirty(stages.gameObject.scene);
            Debug.Log($"{Tag} {string.Join("; ", done)}. SAVE 2 Game Scene.");
        }
    }
}
