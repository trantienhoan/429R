using System.Collections.Generic;
using System.Linq;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Menu > PlayGround Work-In-Progress Notice (user, 2026-10-30: "Whenever some one get to the
    /// PlayGround, can you show a pop up 'This is still on working, you can still play around here, but I can not
    /// grurantee the fun, Love u! I am going finish this soon, so soon.'"). Each gate's stage now has its own GAMESTAGES
    /// state: "Path 1 ON" (gate 1, the PlayGround), "Path 2 ON" (gate 2, Slime Bowling) and "Path 3 ON" (gate 3, Ball
    /// Valley); since 2026-11-20 (user: "add to both") all three show the note, every time, once the fade is over (the
    /// game pauses while it's up; a trigger or a punch closes it). The open scene is marked changed, not saved. Running
    /// it again changes nothing more.
    /// </summary>
    public static class PlayGroundNoticeSetup
    {
        private const string StagesObject = "-GAMESTAGES";
        private const string StagesFsm = "GAMESTAGES";
        private static readonly string[] PathStates = { "Path 1 ON", "Path 2 ON", "Path 3 ON" };

        private const string Title = "WORKING SITE";   // user 2026-11-16 (was "STILL WORKING ON THIS!")
        private const string Body = "You can play around here, but I can't guarantee the fun.\nLove u! I'll finish it soon, so soon.";
        private const string CloseLine = "Pull a trigger to play around";
        // After the fade into the gate's stage (each Path N ON fades for 1 s).
        private const float ShowAfter = 1.5f;

        [MenuItem("Tools/429 Game/Menu/PlayGround Work-In-Progress Notice")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            var holder = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == StagesObject);
            var fsm = holder != null ? holder.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == StagesFsm) : null;
            if (fsm == null)
            {
                Debug.LogWarning($"[PlayGround Notice] {StagesObject} / {StagesFsm} not in the open scene; nothing changed.");
                return;
            }
            if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
            var added = new List<string>();
            var had = new List<string>();
            Undo.RecordObject(fsm, "Gate Notices");
            foreach (var pathState in PathStates)
            {
                var state = fsm.Fsm.GetState(pathState);
                state?.LoadActions();
                // Saving writes back only the actions that could be read: if one couldn't, leave the state alone.
                if (state == null || state.ActionData.ActionCount != state.Actions.Length)
                {
                    Debug.LogWarning($"[PlayGround Notice] {StagesFsm} has no '{pathState}', or an action in it couldn't be read; left alone.", fsm);
                    continue;
                }
                if (state.Actions.OfType<ShowNotice>().Any(n => n.titles.Length > 0 && n.titles[0].Value == Title))
                {
                    had.Add(pathState);
                    continue;
                }
                var notice = new ShowNotice();
                notice.Reset();
                notice.titles = new[] { new FsmString { Value = Title } };
                notice.bodies = new[] { new FsmString { Value = Body } };
                notice.pictures = new FsmTexture[0];
                notice.above = new FsmOwnerDefault();
                notice.pagesAbove = 0;
                notice.showAfter = ShowAfter;
                notice.onceKey = "";
                notice.oncePerInstall = false;
                notice.closeLine = CloseLine;
                state.Actions = state.Actions.Append(notice).ToArray();
                state.SaveActions();
                added.Add(pathState);
            }
            if (added.Count > 0)
            {
                EditorUtility.SetDirty(fsm);
                EditorSceneManager.MarkSceneDirty(scene);
            }
            Debug.Log($"[PlayGround Notice] \"{Title}\" added to: {(added.Count > 0 ? string.Join(", ", added) : "none")}; " +
                      $"already there: {(had.Count > 0 ? string.Join(", ", had) : "none")}." + (added.Count > 0 ? " Save the scene." : ""), fsm);
        }

        [MenuItem("Tools/429 Game/Menu/PlayGround Work-In-Progress Notice", true)]
        private static bool NotPlaying() => !EditorApplication.isPlaying;
    }
}
