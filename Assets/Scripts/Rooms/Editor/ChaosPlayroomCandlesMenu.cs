using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Rooms.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Rooms > Fix Chaos Playroom Candles (user 2026-11-26: "Yes, fix it"). In the chaos playroom
    /// (--Livingroom_Jenga Chaos--) the 4 wall candle points (Candle_Hand_Pos*) made their Candle_Hang with no parent, so
    /// the candles were left behind when the stage cleans up (it clears the global CurrentlySpawnedStuffs). Their twins in
    /// the normal playroom put them under CurrentlySpawnedStuffs; this does the same for every point of the chaos set whose
    /// Create Object has no parent. Re-runnable (a second run finds nothing to do). Changes the scene: SAVE it.
    /// </summary>
    public static class ChaosPlayroomCandlesMenu
    {
        private const string Tag = "[Chaos Playroom Candles]";
        private const string SetName = "--Livingroom_Jenga Chaos--";
        private const string StuffVariable = "CurrentlySpawnedStuffs";

        [MenuItem("Tools/429 Game/Rooms/Fix Chaos Playroom Candles")]
        public static void Fix()
        {
            GameObject set = null;
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                if (go.name == SetName && go.scene.IsValid() && go.scene.isLoaded) set = go;
            var stuff = FsmVariables.GlobalVariables.FindFsmGameObject(StuffVariable);
            if (set == null || stuff == null)
            {
                Debug.LogError($"{Tag} No '{SetName}' in the open scene or no global '{StuffVariable}'; nothing changed.");
                return;
            }
            var fixedPoints = new List<string>();
            foreach (var fsm in set.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (fsm.gameObject == set) continue;
                if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
                foreach (var state in fsm.Fsm.States)
                {
                    state.LoadActions();
                    if (state.ActionData.ActionCount != state.Actions.Length) continue;   // an action it can't read: leave it
                    bool changed = false;
                    foreach (var action in state.Actions)
                    {
                        if (!(action is CreateObject create)) continue;
                        bool hasParent = create.parent != null && (create.parent.Value != null || (create.parent.UseVariable && !string.IsNullOrEmpty(create.parent.Name)));
                        if (hasParent) continue;
                        Undo.RecordObject(fsm, "Fix Chaos Playroom Candles");
                        create.parent = stuff;
                        changed = true;
                    }
                    if (!changed) continue;
                    state.SaveActions();
                    EditorUtility.SetDirty(fsm);
                    fixedPoints.Add(fsm.gameObject.name);
                }
            }
            if (fixedPoints.Count == 0)
            {
                Debug.Log($"{Tag} Nothing to fix: every point in {SetName} already puts what it makes under {StuffVariable}.");
                return;
            }
            EditorSceneManager.MarkSceneDirty(set.scene);
            Debug.Log($"{Tag} Now under {StuffVariable}: {string.Join(", ", fixedPoints)}. SAVE 2 Game Scene.");
        }

        [MenuItem("Tools/429 Game/Rooms/Fix Chaos Playroom Candles", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
