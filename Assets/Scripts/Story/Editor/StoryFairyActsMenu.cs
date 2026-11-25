using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Story;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Story > Set Up Fairy Acts (user 2026-11-25, the Fairy rework). Re-runnable; no scene change.
    /// 1. Resources/Story Fairy Settings (if missing): her teleport flash (Lana's Flash_heal_green) and sound
    ///    (fairy_fly_away); her send-off lines stay empty until the user's recordings are in.
    /// 2. Fairy_Controller follows her clips in Fairy_Girl.fbx (made in Fairy_Z.blend): wait, call (speed kept),
    ///    free_idle, hello (back to free_idle by itself), goodluck, teleport, cheer, talk, startled; the old pray and dead
    ///    states are removed (the user dropped those clips).
    /// 3. Fairy.prefab, FSM FairyHealth (the user's design: after the boss she cheers, talks a bit, then teleports away
    ///    with a green flash; at the first meeting she is startled): DEAD plays startled (was pray); Win plays cheer (was
    ///    call); Win 2 plays talk for 3 s with her spin, 26 m fly-up and fly-away sound switched off (kept, not deleted);
    ///    Destroy Light first runs Story Fairy Teleport, and its Destroy Self waits 0.5 s so the move and flash show.
    /// Tools > 429 Game > Story > Undo Fairy Acts (FSM) puts FairyHealth's flow back (call instead of the gone pray).
    /// </summary>
    public static class StoryFairyActsMenu
    {
        private const string Tag = "[Story Fairy Acts]";
        private const string SettingsPath = "Assets/Resources/Story Fairy Settings.asset";
        private const string FlashPath = "Assets/Lana Studio/Hyper Casual FX/Prefabs/Area/Flash_heal_green.prefab";
        private const string TeleportSoundPath = "Assets/Audio/SFX/fairy_fly_away.wav";
        private const string ControllerPath = "Assets/Models/Animator/Fairy_Controller.controller";
        private const string FbxPath = "Assets/Models/Fairy_Girl.fbx";
        private const string PrefabPath = "Assets/Prefabs/Player/Fairy.prefab";
        private const string TalkWaitName = "Fairy Talk Time";
        private const float TalkSeconds = 3f;

        // Animator state -> clip, and where a new state goes in the Animator window.
        private static readonly (string state, string clip, Vector2 at)[] States =
        {
            ("wait", "Fairy_Girl_WaitForHelp", Vector2.zero),
            ("call", "Fairy_Girl_CallForHelp", Vector2.zero),
            ("free_idle", "Fairy_Girl_FreeIdle", new Vector2(300, 300)),
            ("hello", "Fairy_Girl_Hello", new Vector2(550, 300)),
            ("goodluck", "Fairy_Girl_GoodLuck", new Vector2(550, 380)),
            ("teleport", "Fairy_Girl_Teleport", new Vector2(800, 340)),
            ("cheer", "Fairy_Girl_Cheer", new Vector2(300, 460)),
            ("talk", "Fairy_Girl_Talk", new Vector2(550, 460)),
            ("startled", "Fairy_Girl_Startled", new Vector2(300, 540)),
        };
        private static readonly string[] RemovedStates = { "pray", "dead" };

        [MenuItem("Tools/429 Game/Story/Set Up Fairy Acts")]
        public static void SetUp()
        {
            MakeSettings();
            SyncController();
            WireFairyHealth(false);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/429 Game/Story/Undo Fairy Acts (FSM)")]
        public static void UndoFsm()
        {
            WireFairyHealth(true);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/429 Game/Story/Set Up Fairy Acts", true)]
        [MenuItem("Tools/429 Game/Story/Undo Fairy Acts (FSM)", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static void MakeSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<StoryFairySettings>(SettingsPath);
            if (settings == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                settings = ScriptableObject.CreateInstance<StoryFairySettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
                Debug.Log($"{Tag} Made {SettingsPath}.");
            }
            var flash = AssetDatabase.LoadAssetAtPath<GameObject>(FlashPath);
            var sound = AssetDatabase.LoadAssetAtPath<AudioClip>(TeleportSoundPath);
            if (flash == null) Debug.LogWarning($"{Tag} Missing {FlashPath}: set the flash by hand.");
            if (sound == null) Debug.LogWarning($"{Tag} Missing {TeleportSoundPath}: set the teleport sound by hand.");
            settings.FillDefaults(flash, sound);
            EditorUtility.SetDirty(settings);
        }

        private static void SyncController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError($"{Tag} Missing {ControllerPath}.");
                return;
            }
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var c in AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<AnimationClip>())
                if (!c.name.StartsWith("__preview__")) clips[c.name] = c;
            var machine = controller.layers[0].stateMachine;
            var done = new List<string>();

            foreach (var gone in RemovedStates)
            {
                var st = machine.states.FirstOrDefault(s => s.state.name == gone).state;
                if (st == null) continue;
                foreach (var cs in machine.states)
                    foreach (var t in cs.state.transitions.Where(t => t.destinationState == st).ToArray())
                        cs.state.RemoveTransition(t);
                foreach (var t in machine.anyStateTransitions.Where(t => t.destinationState == st).ToArray())
                    machine.RemoveAnyStateTransition(t);
                if (machine.defaultState == st)
                    machine.defaultState = machine.states.First(s => s.state.name == "wait").state;
                machine.RemoveState(st);
                done.Add("removed " + gone);
            }

            var template = machine.states.FirstOrDefault(s => s.state.name == "call").state;
            foreach (var (stateName, clipName, at) in States)
            {
                if (!clips.TryGetValue(clipName, out var clip))
                {
                    Debug.LogWarning($"{Tag} {FbxPath} has no clip {clipName} (state {stateName} left as it is).");
                    continue;
                }
                var st = machine.states.FirstOrDefault(s => s.state.name == stateName).state;
                if (st == null)
                {
                    st = machine.AddState(stateName, new Vector3(at.x, at.y, 0f));
                    st.speed = 1f;
                    st.writeDefaultValues = template == null || template.writeDefaultValues;
                    done.Add("added " + stateName);
                }
                if (st.motion != clip)
                {
                    st.motion = clip;
                    if (stateName == "free_idle") st.speed = 1f;   // was held at 0 by the first stand-in
                    done.Add(stateName + " = " + clipName);
                }
            }

            // hello goes back to her idle by itself.
            var hello = machine.states.FirstOrDefault(s => s.state.name == "hello").state;
            var idle = machine.states.FirstOrDefault(s => s.state.name == "free_idle").state;
            if (hello != null && idle != null && !hello.transitions.Any(t => t.destinationState == idle))
            {
                var t = hello.AddTransition(idle);
                t.hasExitTime = true;
                t.exitTime = 0.92f;
                t.hasFixedDuration = true;
                t.duration = 0.15f;
                done.Add("hello -> free_idle");
            }
            EditorUtility.SetDirty(controller);
            Debug.Log($"{Tag} Fairy_Controller: " + (done.Count > 0 ? string.Join(", ", done) : "already up to date") + ".");
        }

        private static void WireFairyHealth(bool undo)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            bool reopen = false;
            if (stage != null && stage.assetPath == PrefabPath)
            {
                if (stage.scene.isDirty)
                {
                    Debug.LogError($"{Tag} Fairy.prefab is open in Prefab Mode with unsaved changes: save or close it, then run this again.");
                    return;
                }
                StageUtility.GoToMainStage();
                reopen = true;
            }
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            var done = new List<string>();
            try
            {
                var fsm = root.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "FairyHealth");
                if (fsm == null)
                {
                    Debug.LogError($"{Tag} Fairy.prefab has no FairyHealth FSM; nothing changed.");
                    return;
                }
                fsm.Fsm.Init(fsm);
                foreach (var st in fsm.Fsm.States)
                {
                    st.LoadActions();
                    bool changed = false;
                    if (st.Name == "DEAD")
                        changed |= Rename(st, undo ? new[] { "startled" } : new[] { "pray", "dead" }, undo ? "call" : "startled", done);
                    if (st.Name == "Win")
                        changed |= Rename(st, undo ? new[] { "cheer" } : new[] { "call" }, undo ? "call" : "cheer", done);
                    if (st.Name == "Win 2")
                    {
                        changed |= Rename(st, undo ? new[] { "talk" } : new[] { "pray", "call" }, undo ? "call" : "talk", done);
                        foreach (var a in st.Actions)
                        {
                            bool flyAway = a is Rotate || a is TweenPosition || a is AudioPlay;
                            if (!flyAway || a.Enabled == undo) continue;
                            a.Enabled = undo;
                            changed = true;
                            done.Add($"Win 2 {a.GetType().Name} {(undo ? "on" : "off")}");
                        }
                        var talk = st.Actions.OfType<Wait>().FirstOrDefault(w => w.Name == TalkWaitName);
                        // Her talk lines' own wait (Story Fairy Say, Wire ElevenLabs Sounds) takes the place of this one.
                        if (!undo && talk == null && !st.Actions.OfType<StoryFairySay>().Any())
                        {
                            var w = new Wait();
                            w.Reset();
                            w.Name = TalkWaitName;
                            w.time = TalkSeconds;
                            w.finishEvent = FsmEvent.Finished;
                            w.realTime = true;
                            st.Actions = st.Actions.Concat(new FsmStateAction[] { w }).ToArray();
                            changed = true;
                            done.Add($"Win 2 talks {TalkSeconds} s");
                        }
                        else if (undo && talk != null)
                        {
                            st.Actions = st.Actions.Where(a => a != talk).ToArray();
                            changed = true;
                            done.Add("Win 2 talk wait removed");
                        }
                    }
                    if (st.Name == "Destroy Light")
                    {
                        var tp = st.Actions.OfType<StoryFairyTeleport>().FirstOrDefault();
                        if (!undo && tp == null)
                        {
                            var a = new StoryFairyTeleport();
                            a.Reset();
                            st.Actions = new FsmStateAction[] { a }.Concat(st.Actions).ToArray();
                            changed = true;
                            done.Add("Destroy Light teleports first");
                        }
                        else if (undo && tp != null)
                        {
                            st.Actions = st.Actions.Where(a => a != tp).ToArray();
                            changed = true;
                            done.Add("Destroy Light teleport removed");
                        }
                        foreach (var d in st.Actions.OfType<DestroySelf>())
                        {
                            float want = undo ? 0f : 0.5f;
                            if (Mathf.Approximately(d.delay.Value, want)) continue;
                            d.delay = want;
                            changed = true;
                            done.Add($"Destroy Self delay {want}");
                        }
                    }
                    if (changed) st.SaveActions();
                }
                if (done.Count > 0)
                {
                    EditorUtility.SetDirty(fsm);
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                }
                Debug.Log($"{Tag} FairyHealth: " + (done.Count > 0 ? string.Join(", ", done) : "already as wanted") + ".");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                if (reopen) PrefabStageUtility.OpenPrefab(PrefabPath);
            }
        }

        // AnimatorPlay actions playing one of `from` now play `to`.
        private static bool Rename(FsmState st, string[] from, string to, List<string> done)
        {
            bool changed = false;
            foreach (var a in st.Actions.OfType<AnimatorPlay>())
            {
                if (a.stateName == null || !from.Contains(a.stateName.Value)) continue;
                done.Add($"{st.Name} {a.stateName.Value} -> {to}");
                a.stateName = to;
                changed = true;
            }
            return changed;
        }
    }
}
