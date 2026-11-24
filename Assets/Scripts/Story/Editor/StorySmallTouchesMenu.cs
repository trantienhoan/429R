using System.Collections.Generic;
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
    /// Tools > 429 Game > Story > Set Up Story Small Touches (user's story, 2026-11-23, part 5).
    /// - The Fairy prefab's root gets a Story Weak Fairy: the Fairy made at the start of a run has a weak, stuttering
    ///   green light and her calls fade ("a tiny girl with a green light, so weak ... her voices is getting weaker and
    ///   weaker"). The end-of-game Fairy is as before.
    /// - The 4 rooms' wall drops (GAMESTAGES Mini Enemy, Enemy1 - Wait, Enemy2 - Wait, Enemy 3 Stuffs: the walls sink,
    ///   the bugs come) each get a Story Fight Voice action: "Fight" once the walls are down (user, 2026-11-24: "All 4
    ///   rooms"). The voice is Fight Voice in Resources/Story Seed Settings, empty until the user records it.
    /// Running it again changes nothing more. A scene change: save 2 Game Scene afterwards.
    /// </summary>
    public static class StorySmallTouchesMenu
    {
        private const string Tag = "[Story Small Touches]";
        private const string FairyPath = "Assets/Prefabs/Player/Fairy.prefab";

        // The wall-drop states and when their walls are down: the rooms' GO takes 3.33 s, the Jenga room's GO4 1.67 s.
        private static readonly (string state, float delay)[] FightStates =
        {
            ("Mini Enemy", 3.3f), ("Enemy1 - Wait", 3.3f), ("Enemy2 - Wait", 3.3f), ("Enemy 3 Stuffs", 1.7f),
        };

        [MenuItem("Tools/429 Game/Story/Set Up Story Small Touches")]
        public static void Run()
        {
            var done = new List<string>();
            WeakFairy(done);
            bool sceneChanged = Fight(done);
            Debug.Log(Tag + (done.Count == 0 ? " Already done." : " Done: " + string.Join(", ", done) + ".") +
                      (sceneChanged ? " Save 2 Game Scene." : ""));
        }

        private static void WeakFairy(List<string> done)
        {
            var root = PrefabUtility.LoadPrefabContents(FairyPath);
            try
            {
                if (root.GetComponent<StoryWeakFairy>() != null) return;
                root.AddComponent<StoryWeakFairy>();
                PrefabUtility.SaveAsPrefabAsset(root, FairyPath);
                done.Add("the Fairy (Story Weak Fairy)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool Fight(List<string> done)
        {
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
                return false;
            }
            if (!stages.Fsm.Initialized) stages.Fsm.Init(stages);
            var added = new List<string>();
            foreach (var (name, delay) in FightStates)
            {
                var state = stages.Fsm.GetState(name);
                if (state == null)
                {
                    Debug.LogError(Tag + " GAMESTAGES has no '" + name + "' state.");
                    continue;
                }
                state.LoadActions();
                if (state.Actions.OfType<StoryFightVoice>().Any()) continue;
                var fight = new StoryFightVoice();
                fight.Reset();
                fight.delay = delay;
                state.Actions = state.Actions.Concat(new FsmStateAction[] { fight }).ToArray();
                state.SaveActions();
                added.Add(name);
            }
            if (added.Count == 0) return false;
            EditorUtility.SetDirty(stages);
            EditorSceneManager.MarkSceneDirty(stages.gameObject.scene);
            done.Add("Story Fight Voice in GAMESTAGES '" + string.Join("', '", added) + "'");
            return true;
        }

        [MenuItem("Tools/429 Game/Story/Set Up Story Small Touches", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
