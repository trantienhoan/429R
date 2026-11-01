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
    /// grurantee the fun, Love u! I am going finish this soon, so soon.'"). All three gates (Cube 1-3 on Gate 1-3) lead to
    /// GAMESTAGES' "Path 1 ON", which takes the player to the PlayGround; a Show Notice there shows the note every time,
    /// once the fade is over (the game pauses while it's up; a trigger or a punch closes it). The open scene is marked
    /// changed, not saved. Running it again changes nothing more.
    /// </summary>
    public static class PlayGroundNoticeSetup
    {
        private const string StagesObject = "-GAMESTAGES";
        private const string StagesFsm = "GAMESTAGES";
        private const string PathState = "Path 1 ON";

        private const string Title = "STILL WORKING ON THIS!";
        private const string Body = "You can play around here, but I can't guarantee the fun.\nLove u! I'll finish it soon, so soon.";
        private const string CloseLine = "Pull a trigger to play around";
        // After the fade into the PlayGround (Path 1 ON fades for 1 s).
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
            var state = fsm.Fsm.GetState(PathState);
            state?.LoadActions();
            // Saving writes back only the actions that could be read: if one couldn't, leave the state alone.
            if (state == null || state.ActionData.ActionCount != state.Actions.Length)
            {
                Debug.LogWarning($"[PlayGround Notice] {StagesFsm} has no '{PathState}', or an action in it couldn't be read; nothing changed.", fsm);
                return;
            }
            if (state.Actions.OfType<ShowNotice>().Any(n => n.titles.Length > 0 && n.titles[0].Value == Title))
            {
                Debug.Log($"[PlayGround Notice] '{PathState}' already shows it.", fsm);
                return;
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

            Undo.RecordObject(fsm, "PlayGround Notice");
            state.Actions = state.Actions.Append(notice).ToArray();
            state.SaveActions();
            EditorUtility.SetDirty(fsm);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[PlayGround Notice] '{PathState}' shows \"{Title}\" every time the player gets to the PlayGround. Save the scene.", fsm);
        }

        [MenuItem("Tools/429 Game/Menu/PlayGround Work-In-Progress Notice", true)]
        private static bool NotPlaying() => !EditorApplication.isPlaying;
    }
}
