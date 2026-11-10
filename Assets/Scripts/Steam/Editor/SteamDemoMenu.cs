using System.IO;
using System.Linq;
using Game.Steam;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Steam Demo (user, 2026-11-10): the free Steam demo, which ends after the bedroom bonus round
    /// (see DemoBuild).
    /// - Set Up Demo Ending: GAMESTAGES (open 2 Game Scene) gets a check after each way out of the bedroom part: "Bonus
    ///   Round BR" (the normal end), "Door Shortcut" (the breakable bedroom door), "Clean for Path 1" and "Clean for Path
    ///   2" (the seed paths at the start). Each goes on as before in the full game; in the demo it goes to "Demo End" (the
    ///   thank-you board), then "Demo Store" (429's store page in the Steam overlay, then the demo starts over). SAVE the
    ///   scene afterwards. Running it again changes nothing.
    /// - Demo Settings: the demo's Steam App ID and build folder (Assets/Resources/Demo Settings.asset).
    /// - Play As Demo: play mode acts like the demo while ticked.
    /// - Build Steam Demo (Windows): builds the demo (the DEMO_BUILD define) into Demo Settings' build folder.
    /// </summary>
    public static class SteamDemoMenu
    {
        private const string Root = "Tools/429 Game/Steam Demo/";
        private const string PlayAsDemoItem = Root + "Play As Demo";
        private const string SettingsPath = "Assets/Resources/" + DemoSettings.ResourceName + ".asset";

        private const string EndState = "Demo End";
        private const string StoreState = "Demo Store";
        private const string DemoEventName = "demo";
        // The thank-you board (user's words, 2026-11-10).
        private const string Title = "Thanks for playing the 429 demo!";
        private const string Body = "Wishlist 429 on Steam";
        private const string CloseLine = "Punch it to open the store page";

        // Each way out of the demo's part: (the state, the check that goes after it, where the full game goes on).
        private static readonly (string from, string check, string fullGame)[] Exits =
        {
            ("Bonus Round BR", "Demo?", "3 - LivingRoom"),
            ("Door Shortcut", "Demo? Door", "Rooftop_ON"),
            ("Clean for Path 1", "Demo? Path 1", "Re-Create Stuffs Hodler"),
            ("Clean for Path 2", "Demo? Path 2", "Path 2 Holder"),
        };

        // ------------------------------------------------------------------ Set Up Demo Ending
        [MenuItem(Root + "Set Up Demo Ending", false, 0)]
        public static void SetUpEnding() => Say(SetUpDemoEnding());

        /// <summary>Set Up Demo Ending without the dialog (e.g. for tests); returns what it did.</summary>
        public static string SetUpDemoEnding()
        {
            if (EditorApplication.isPlaying) return "Stop play mode first. Nothing changed.";
            var gs = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(f => f.FsmName == "GAMESTAGES" && f.gameObject.scene.IsValid());
            if (gs == null) return "No GAMESTAGES in the open scene: open 2 Game Scene. Nothing changed.";
            var f = gs.Fsm;
            if (!f.Initialized) f.Init(gs);
            foreach (var (from, _, fullGame) in Exits)
            {
                if (f.GetState(from) == null || f.GetState(fullGame) == null)
                    return $"GAMESTAGES has no '{from}' or '{fullGame}'. Nothing changed.";
            }

            Undo.RecordObject(gs, "Set Up Demo Ending");
            var demo = FsmEvent.GetFsmEvent(DemoEventName);
            if (!f.Events.Contains(demo)) f.Events = f.Events.Append(demo).ToArray();

            var first = f.GetState(Exits[0].from).Position;
            // The thank-you board, then the store page and the restart.
            var end = StateNear(f, EndState, first, 400f, 70f);
            var notice = New<ShowNotice>();
            notice.titles = new FsmString[] { Title };
            notice.bodies = new FsmString[] { Body };
            notice.pictures = new FsmTexture[0];
            notice.showAfter = 1f;
            notice.closeLine = CloseLine;
            end.Actions = new FsmStateAction[] { notice };
            end.Transitions = new[] { Go(FsmEvent.Finished, StoreState) };

            var store = StateNear(f, StoreState, first, 400f, 140f);
            var restart = New<RestartDemo>();
            restart.delay = 2f;
            store.Actions = new FsmStateAction[] { New<OpenSteamStorePage>(), restart };
            store.Transitions = new FsmTransition[0];

            var notes = new System.Collections.Generic.List<string>();
            var changed = new System.Collections.Generic.List<FsmState> { end, store };
            foreach (var (from, checkName, fullGame) in Exits)
            {
                var fromState = f.GetState(from);
                var check = StateNear(f, checkName, fromState.Position, 0f, 60f);
                var isDemo = New<IsDemoBuild>();
                isDemo.demoEvent = demo;
                check.Actions = new FsmStateAction[] { isDemo };
                check.Transitions = new[] { Go(FsmEvent.Finished, fullGame), Go(demo, EndState) };
                changed.Add(check);
                // Only the FINISHED transition changes; the state's own actions are left as they are.
                var t = fromState.Transitions.FirstOrDefault(x => x.FsmEvent != null && x.FsmEvent.Name == "FINISHED");
                if (t != null) t.ToState = checkName;
                else fromState.Transitions = fromState.Transitions.Append(Go(FsmEvent.Finished, checkName)).ToArray();
                notes.Add($"'{from}' -> '{checkName}' -> full game: '{fullGame}', demo: '{EndState}'");
            }
            foreach (var s in changed) s.SaveActions();
            EditorUtility.SetDirty(gs);
            EditorSceneManager.MarkSceneDirty(gs.gameObject.scene);
            return "GAMESTAGES now ends the demo after the bedroom (the full game goes on as before):\n" +
                   string.Join("\n", notes) + $"\n'{EndState}': the thank-you board; '{StoreState}': the store page, then the demo starts over.\n\nSAVE the scene (2 Game Scene).";
        }

        // ------------------------------------------------------------------ Demo Settings
        [MenuItem(Root + "Demo Settings", false, 20)]
        public static void SelectSettings()
        {
            Selection.activeObject = Settings();
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        private static DemoSettings Settings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<DemoSettings>(SettingsPath);
            if (settings != null) return settings;
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            settings = ScriptableObject.CreateInstance<DemoSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        // ------------------------------------------------------------------ Play As Demo
        [MenuItem(PlayAsDemoItem, false, 21)]
        public static void TogglePlayAsDemo()
        {
            bool on = !EditorPrefs.GetBool(DemoBuild.PlayAsDemoKey, false);
            EditorPrefs.SetBool(DemoBuild.PlayAsDemoKey, on);
            Menu.SetChecked(PlayAsDemoItem, on);
            Debug.Log(on ? "[Steam Demo] Play As Demo ON: play mode acts like the demo (ends after the bedroom bonus round; saves go to the _Demo folder)."
                         : "[Steam Demo] Play As Demo OFF: play mode is the full game again.");
        }

        [MenuItem(PlayAsDemoItem, true)]
        private static bool TogglePlayAsDemoValidate()
        {
            Menu.SetChecked(PlayAsDemoItem, EditorPrefs.GetBool(DemoBuild.PlayAsDemoKey, false));
            return !EditorApplication.isPlaying;
        }

        // ------------------------------------------------------------------ Build Steam Demo
        [MenuItem(Root + "Build Steam Demo (Windows)", false, 40)]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Say("Stop play mode first."); return; }
            var settings = Settings();
            if (settings.demoAppId <= 0)
            {
                Selection.activeObject = settings;
                Say("Paste the demo's Steam App ID in Demo Settings first (now selected in the Inspector).\n\n" +
                    "Steamworks: your game's page > All associated packages, DLC, demos and tools > Add Demo.");
                return;
            }
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            {
                Say("Switch the build target to Windows first (File > Build Profiles), then build the demo.");
                return;
            }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.isDirty)
            {
                Say($"'{scene.name}' has unsaved changes. Save the scene first: the build uses the saved scene.");
                return;
            }
            var gs = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(fsm => fsm.FsmName == "GAMESTAGES" && fsm.gameObject.scene.IsValid());
            if (gs != null && gs.Fsm.GetState(EndState) == null &&
                !EditorUtility.DisplayDialog("Steam Demo", "GAMESTAGES has no demo ending yet (Set Up Demo Ending), so the demo " +
                                             "would play the whole game. Build anyway?", "Build anyway", "Cancel"))
            {
                return;
            }

            string folder = string.IsNullOrWhiteSpace(settings.buildFolder) ? "E:/429_Demo" : settings.buildFolder;
            Directory.CreateDirectory(folder);
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = Path.Combine(folder, PlayerSettings.productName + ".exe"),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
                extraScriptingDefines = new[] { DemoBuild.Define },
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Say($"The Steam demo is built (App ID {settings.demoAppId}):\n{options.locationPathName}\n" +
                    $"{summary.totalSize / (1024f * 1024f):0} MB in {summary.totalTime.TotalMinutes:0.0} min.\n\n" +
                    "Upload that folder to the demo's depot WITHOUT the *_BurstDebugInformation_DoNotShip folder. " +
                    "No steam_appid.txt is needed: start it from your Steam Library.");
            }
            else
            {
                Say($"The demo build didn't finish ({summary.result}, {summary.totalErrors} errors). See the Console.");
            }
        }

        // ------------------------------------------------------------------ helpers
        private static FsmState StateNear(Fsm f, string name, Rect near, float dx, float dy)
        {
            var state = f.GetState(name);
            if (state != null) return state;
            state = new FsmState(f) { Name = name, Position = new Rect(near.x + dx, near.y + dy, 130f, near.height) };
            f.States = f.States.Append(state).ToArray();
            return state;
        }

        private static T New<T>() where T : FsmStateAction, new()
        {
            var action = new T();
            action.Reset();
            return action;
        }

        private static FsmTransition Go(FsmEvent e, string toState) => new() { FsmEvent = e, ToState = toState };

        private static void Say(string message)
        {
            Debug.Log("[Steam Demo] " + message);
            EditorUtility.DisplayDialog("Steam Demo", message, "OK");
        }
    }
}
