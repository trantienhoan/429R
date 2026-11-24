using HutongGames.PlayMaker.Actions;
using HutongGames.PlayMaker.TweenEnums;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Menu > HEE HEE Punch Once (user, 2026-11-23: the game-over HEE HEE shook non-stop, "Punch
    /// once"). The HeeHee picture under the Main Camera's GAME_OVER has a "Tween" FSM whose three TweenPunch actions
    /// (move, roll, grow; 1 s each) looped PingPong. This sets their Loop Type to None: each death punches it once, then
    /// it holds still. The FSM restarts whenever GAME_OVER turns on, so every death gets its punch.
    /// Running it again changes nothing more. A scene change: save 2 Game Scene afterwards.
    /// To get the old shake back, set Loop Type to PingPong on the three actions (Tween FSM, state Shake).
    /// </summary>
    public static class HeeHeePunchOnceMenu
    {
        private const string Tag = "[HEE HEE Punch Once]";

        [MenuItem("Tools/429 Game/Menu/HEE HEE Punch Once")]
        public static void Run()
        {
            PlayMakerFSM tween = null;
            foreach (var f in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
            {
                if (!f.gameObject.scene.IsValid() || f.gameObject.name != "HeeHee" || f.FsmName != "Tween") continue;
                if (f.transform.parent == null || f.transform.parent.name != "GAME_OVER") continue;
                tween = f;
                break;
            }
            if (tween == null)
            {
                Debug.LogError(Tag + " No HeeHee 'Tween' FSM under GAME_OVER in the open scene.");
                return;
            }

            tween.Fsm.Init(tween);
            int punches = 0, changed = 0;
            foreach (var state in tween.Fsm.States)
            {
                state.LoadActions();
                bool stateChanged = false;
                foreach (var action in state.Actions)
                {
                    var punch = action as TweenPunch;
                    if (punch == null) continue;
                    punches++;
                    if (punch.loopType == LoopType.None) continue;
                    punch.loopType = LoopType.None;
                    stateChanged = true;
                    changed++;
                }
                if (stateChanged) state.SaveActions();
            }

            if (changed == 0)
            {
                Debug.Log(Tag + " Already done: " + punches + " TweenPunch actions play once.");
                return;
            }
            EditorUtility.SetDirty(tween);
            EditorSceneManager.MarkSceneDirty(tween.gameObject.scene);
            Debug.Log(Tag + " " + changed + " of " + punches + " TweenPunch actions on HeeHee now play once (Loop Type None). Save 2 Game Scene.");
        }

        [MenuItem("Tools/429 Game/Menu/HEE HEE Punch Once", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
