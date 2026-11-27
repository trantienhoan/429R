using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Game.Combat;
using HutongGames.PlayMaker;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace Game.EditorTools.Testing
{
    /// <summary>
    /// User 2026-11-26: "I want to create a bot for testing, play through like a human, check in with all the stage in
    /// GameStages so stuffs can be clean after each room."
    /// Tools > 429 Game > Test > Play-Through Bot > Start: enters Play, starts a NEW game in its own save folder
    /// (Saves/_test_bot; the three real slots are never read or written; the folder is deleted at the end) and plays
    /// the main path like a player: it WALKS (Character Controller, walls block it; stuck = a teleport, as the VR
    /// player would), walks INTO what a player touches (the Fairy, chase pumpkins, skulls, seeds), lifts the magic hats,
    /// PUNCHES what a player punches (the player's own hit: Enemy Hit Damage's player blast, so EXP, reactions and
    /// deaths are the real ones), stands on the Paint Bees ring, closes notices. God mode is on (it can't die).
    /// What it CHECKS:
    /// - every GAMESTAGES state it passes (time in it, what moved it on) and which states it never saw;
    /// - after each room (its clean-up stage: Clean ..., clean ..., Destroy PlayGroundZ..., Game Over, with the purple
    ///   hole) what is LEFT OVER: everything that room made that is still there (enemies, props, pieces, pickups...);
    /// - stalls (no stage change for a long time; then it uses a shortcut and says so) and every Console warning/error.
    /// THE GATES (user 2026-11-26: "test all main game and 3 gates, take some pictures along the way"): after the main
    /// game's ending it jumps to each gate as the gate tests do (GAMESTAGES "Clean for Path N") and plays it until the
    /// rooftop's whack starts; a gate's own test buttons are used only after a stall, and said in the report.
    /// PICTURES: from the player's eyes (a copy of the main camera), 2.5 s into every stage and every 40 s in a long one
    /// (6 a stage at most), into Exports/Tests/PlayThroughBot_[date]/. The bot turns to face what it walks to.
    /// Report: Exports/Tests/PlayThroughBot_[date].md (+ _log.txt). Stop: Tools > 429 Game > Test > Play-Through Bot > Stop
    /// (or leave Play). Editor only; nothing of it goes in a build.
    /// </summary>
    public static class PlayThroughBot
    {
        private const string Profile = "_test_bot";
        private const string PrefRunning = "429.PlayThroughBot.Pending";
        private const string PrefRan = "429.PlayThroughBot.Ran";
        private const string PrefGatesOnly = "429.PlayThroughBot.GatesOnly";
        private const string ReportFolder = "Exports/Tests";
        private const float WalkSpeed = 1.6f;
        private const float PunchEvery = 0.35f;
        private const float PunchDamage = 5f;
        private const float StallSeconds = 150f;
        private const float MaxMinutes = 40f;

        private static Runner runner;

        [MenuItem("Tools/429 Game/Test/Play-Through Bot/Start")]
        public static void StartBot()
        {
            if (runner != null)
            {
                Debug.Log("[Bot] already running.");
                return;
            }
            if (!EditorApplication.isPlaying)
            {
                SessionState.SetBool(PrefRunning, true);
                EditorApplication.isPlaying = true;
                return;
            }
            Begin();
        }

        [MenuItem("Tools/429 Game/Test/Play-Through Bot/Start (Gates Only)")]
        public static void StartGatesOnly()
        {
            SessionState.SetBool(PrefGatesOnly, true);
            StartBot();
        }

        [MenuItem("Tools/429 Game/Test/Play-Through Bot/Stop")]
        public static void StopBot()
        {
            SessionState.SetBool(PrefRunning, false);
            if (runner != null) runner.Finish("stopped from the menu");
        }

        [MenuItem("Tools/429 Game/Test/Play-Through Bot/Stop", true)]
        private static bool CanStop() => runner != null;

        /// <summary>Is the bot playing right now.</summary>
        public static bool Running => runner != null;

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PrefRunning, false))
                {
                    SessionState.SetBool(PrefRunning, false);
                    Begin();
                }
                if (change == PlayModeStateChange.ExitingPlayMode && runner != null) runner.Finish("Play mode ended");
                // The game saves on after a finished run (back at the start): the bot's folder goes once Play is over.
                if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(PrefRan, false))
                {
                    SessionState.SetBool(PrefRan, false);
                    DeleteBotSaves();
                }
            };
        }

        private static void DeleteBotSaves()
        {
            string dir = Path.Combine(Application.persistentDataPath, "Saves", Profile);
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
            catch (Exception e) { Debug.LogWarning("[Bot] couldn't delete " + dir + ": " + e.Message); }
        }

        private static void Begin()
        {
            SessionState.SetBool(PrefRan, true);
            bool gatesOnly = SessionState.GetBool(PrefGatesOnly, false);
            SessionState.SetBool(PrefGatesOnly, false);
            runner = new Runner(gatesOnly);
            EditorApplication.update += runner.Tick;
        }

        // ------------------------------------------------------------------------------------------------------------

        private sealed class Runner
        {
            private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

            private readonly string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmm");
            private readonly StringBuilder log = new();
            private readonly float startReal = Time.realtimeSinceStartup;
            private int boot;
            private float bootAt;

            // GAMESTAGES
            private PlayMakerFSM gs;
            private string state;
            private float stateSince;
            private string movedBy;
            private readonly List<(string name, float seconds, string by)> stages = new();
            private readonly HashSet<string> seen = new();
            private string[] allStates = Array.Empty<string>();
            private bool sawEnding;
            private bool done;
            // Gates: 0 = the main game; 1-3 = gate N, played after the main game's ending.
            private int gate;
            private float gateStartReal;
            private readonly List<string> gateResults = new();
            private int stallHooks;
            private int gateShortcuts;
            // Pictures
            private string picDir;
            private int shotNo;
            private int shotsThisStage;
            private float nextShot = -1f;
            private Unity.XR.CoreUtils.XROrigin xr;

            // Player
            private GameObject origin;
            private CharacterController cc;
            private Collider body;
            private FsmFloat hp;
            private float lastTickReal;
            private float nextSave;

            // Moving
            private Vector3 goal;
            private bool hasGoal;
            private string goalWhat;
            private Vector3 lastPos;
            private float stuckSince = -1f;
            private float nextPunch;
            private float walked;
            private int teleports, punches, touches, lifts, shortcuts;
            private readonly Dictionary<int, float> touchedAt = new();
            private readonly HashSet<int> liftedHats = new();
            private readonly Dictionary<int, float> giveUpUntil = new();
            private int teleportsAtGoal;
            private string lastAction;
            private float lastActionAt = -100f;
            // What to forget when the bot can't reach its goal (set by the one asking).
            private Action giveUp;

            // Rooms and leftovers: a room ends with a clean-up stage ("Clean ...", "clean ...", "Destroy PlayGroundZ...",
            // "Game Over"). CurrentlySpawnedStuffs itself is only made again at Game Over, the ending and the gates.
            private readonly HashSet<int> staticObjects = new();
            private GameObject stuff;
            private int room = 1;
            private string cleaning;
            private readonly List<string> roomStages = new();
            private readonly HashSet<string> checkStages = new();
            private string roomStartedIn;
            private readonly Dictionary<int, (string name, string stage)> bornIn = new();
            private readonly List<string> roomReports = new();
            private readonly List<string> endLeaks = new();

            // Console
            private readonly Dictionary<string, int> console = new();
            private readonly Dictionary<string, string> consoleWhere = new();

            private readonly bool gatesOnly;
            // The next gate to jump to once a fresh run has started (after Restart Loop); 0 = none waiting.
            private int gatePending;

            public Runner(bool gatesOnly)
            {
                this.gatesOnly = gatesOnly;
                Application.logMessageReceived += OnLog;
                Note("bot started (profile " + Profile + ")");
            }

            // ---------- the loop ----------

            public void Tick()
            {
                if (done) return;
                if (!EditorApplication.isPlaying)
                {
                    Finish("Play mode ended");
                    return;
                }
                float now = Time.realtimeSinceStartup;
                float dt = Mathf.Clamp(now - lastTickReal, 0f, 0.1f) * Mathf.Max(Time.timeScale, 0f);
                lastTickReal = now;
                try
                {
                    if (boot < 4)
                    {
                        Boot(now);
                        return;
                    }
                    if (now - startReal > MaxMinutes * 60f)
                    {
                        Finish($"time limit ({MaxMinutes} min)");
                        return;
                    }
                    if (now >= nextSave)
                    {
                        nextSave = now + 30f;
                        Directory.CreateDirectory(ReportFolder);
                        File.WriteAllText(Path.Combine(ReportFolder, $"PlayThroughBot_{stamp}_log.txt"), log.ToString());
                    }
                    KeepAlive();
                    WatchStage();
                    WatchRooms();
                    if (done) return;
                    if (nextShot > 0f && Time.time >= nextShot && shotsThisStage < 6)
                    {
                        shotsThisStage++;
                        nextShot = Time.time + 40f;
                        Snap(state);
                    }
                    if (Time.timeScale == 0f) return;
                    Act(dt);
                }
                catch (Exception e)
                {
                    Note("BOT ERROR " + e.GetType().Name + ": " + e.Message + " @ " + FirstLine(e.StackTrace));
                }
            }

            // A new game in the bot's own save folder.
            private void Boot(float now)
            {
                switch (boot)
                {
                    case 0:
                        Application.runInBackground = true;
                        Game.Saving.SaveManager.Profile = Profile;
                        DeleteSaves();
                        bootAt = now;
                        boot = 1;
                        break;
                    case 1:
                    {
                        var cut = Object.FindAnyObjectByType<Game.Menus.OpeningCutscene>();
                        var skip = typeof(Game.Menus.OpeningCutscene).GetField("skipping", Any);
                        if (cut != null && skip != null && !(bool)skip.GetValue(cut))
                        {
                            skip.SetValue(cut, true);
                            cut.StopAllCoroutines();
                            var skipOut = typeof(Game.Menus.OpeningCutscene).GetMethod("SkipOut", Any);
                            if (skipOut != null) cut.StartCoroutine((System.Collections.IEnumerator)skipOut.Invoke(cut, null));
                        }
                        if (!Game.Menus.OpeningCutscene.IsPlaying && now - bootAt > 1f) { boot = 2; bootAt = now; }
                        break;
                    }
                    case 2:
                    {
                        foreach (var hw in Object.FindObjectsByType<Game.Menus.HealthWarning>(FindObjectsSortMode.None))
                        {
                            if (!hw.enabled) continue;
                            typeof(Game.Menus.HealthWarning).GetField("shownAt", Any)?.SetValue(hw, -100f);
                            typeof(Game.Menus.HealthWarning).GetField("closing", Any)?.SetValue(hw, true);
                        }
                        var menu = Object.FindAnyObjectByType<Game.Menus.SaveSlotMenu>(FindObjectsInactive.Include);
                        var step = typeof(Game.Menus.SaveSlotMenu).GetField("step", Any);
                        if (menu != null && step != null && step.GetValue(menu).ToString() == "Open")
                        {
                            if (Game.Saving.SaveManager.Profile != Profile)
                            {
                                Finish("the save profile wasn't the bot's own; stopped before starting");
                                return;
                            }
                            menu.StartSlot(0);
                            Note("new game in slot 1 of Saves/" + Profile);
                            boot = 3;
                            bootAt = now;
                        }
                        else if (now - bootAt > 40f) { Finish("the save slot menu never opened"); }
                        break;
                    }
                    case 3:
                        if (Game.Saving.Checkpoints.HasResumed && now - bootAt > 2f)
                        {
                            FindPlayer();
                            gs = Game.GameStages.Fsm();
                            if (gs == null || origin == null)
                            {
                                Finish("no GAMESTAGES or player found");
                                return;
                            }
                            allStates = gs.Fsm.States.Select(s => s.Name).ToArray();
                            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                                staticObjects.Add(go.GetInstanceID());
                            var pc = Game.Locomotion.PcControls.Active;
                            if (pc != null) pc.enabled = false;
                            xr = Object.FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
                            picDir = Path.Combine(ReportFolder, "PlayThroughBot_" + stamp);
                            Directory.CreateDirectory(picDir);
                            lastPos = Head();
                            boot = 4;
                            Note($"playing from '{gs.ActiveStateName}', {allStates.Length} GAMESTAGES states, {staticObjects.Count} objects at the start");
                            if (gatesOnly)
                            {
                                gateResults.Add("- **Main game:** skipped (Start (Gates Only)).");
                                NextGate();
                            }
                        }
                        break;
                }
            }

            private void FindPlayer()
            {
                foreach (var f in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
                {
                    if (!f.gameObject.scene.IsValid() || f.FsmName != "PlayerHealth") continue;
                    origin = f.gameObject;
                    hp = f.FsmVariables.FindFsmFloat("PlayerHealth");
                }
                if (origin == null) return;
                cc = origin.GetComponent<CharacterController>();
                foreach (var c in origin.GetComponentsInChildren<Collider>())
                    if (c.CompareTag("Player") && !c.isTrigger) body = c;
            }

            // God mode, and no notice left waiting.
            private void KeepAlive()
            {
                if (hp != null && hp.Value < 500f) hp.Value = 1000f;
                var pc = Game.Locomotion.PcControls.Active;
                if (pc != null && pc.enabled) pc.enabled = false;
                var tn = Game.Menus.TutorialNotice.Find();
                if (tn != null && tn.IsShowing) { tn.Close(); Note("closed a notice"); }
                foreach (var lv in Object.FindObjectsByType<Game.Leveling.LevelUpNotice>(FindObjectsSortMode.None))
                {
                    if (!lv.IsShowing) continue;
                    var shown = typeof(Game.Leveling.LevelUpNotice).GetField("shownAt", Any);
                    var closing = typeof(Game.Leveling.LevelUpNotice).GetField("closing", Any);
                    if (closing != null && (bool)closing.GetValue(lv)) continue;
                    if (shown != null && Time.unscaledTime - (float)shown.GetValue(lv) < 2f) continue;
                    lv.Close();
                    Note("closed a level-up card");
                }
            }

            // ---------- stages ----------

            private void WatchStage()
            {
                string now = gs.ActiveStateName;
                if (now == state)
                {
                    if (Time.time - stateSince > StallSeconds) Unstick();
                    return;
                }
                if (state != null)
                {
                    if (movedBy == null && lastAction != null && Time.time - lastActionAt < 3f) movedBy = "bot: " + lastAction;
                    stages.Add((state, Time.time - stateSince, movedBy ?? "the game"));
                    Note($"stage '{state}' -> '{now}' after {Time.time - stateSince:F1} s ({movedBy ?? "the game"})");
                }
                // A clean-up stage just finished: what its room made and still lies about is a leftover.
                if (cleaning != null && state == cleaning)
                {
                    CheckLeftovers(cleaning, now);
                    cleaning = null;
                }
                if (IsCleanUp(now))
                {
                    cleaning = now;
                    checkStages.Clear();
                    foreach (var st in roomStages) checkStages.Add(st);
                    roomStages.Clear();
                }
                roomStages.Add(now);
                if (roomStartedIn == null) roomStartedIn = now;
                state = now;
                stateSince = Time.time;
                movedBy = null;
                seen.Add(now);
                touchedAt.Clear();
                shotsThisStage = 0;
                nextShot = Time.time + 2.5f;
                stallHooks = 0;
                if (now == "Thank For Playing") sawEnding = true;
                if (gatePending > 0 && (now == "Walk Tuttorial" || now == "Move to Fairy" || now == "Stage 1 - The Start"))
                {
                    JumpToGate();
                    return;
                }
                if (gate == 0 && sawEnding && (now == "Walk Tuttorial" || now == "Stage 1 - The Start" || now == "Move to Fairy"))
                {
                    gateResults.Add($"- **Main game:** a full run, back at the start after the ending, {(Time.realtimeSinceStartup - startReal) / 60f:F1} min.");
                    NextGate();
                    return;
                }
                if (gate > 0 && gatePending == 0 && (now == "Prepare_Whack_6" || now == "Rooftop_ON PG" || now == "Whack 6"))
                {
                    float took = Time.realtimeSinceStartup - gateStartReal;
                    gateResults.Add(took < 20f
                        ? $"- **Gate {gate}: NOT PLAYED**: the path went straight to the rooftop ('{now}') after {took:F1} s (its flags were already set)."
                        : $"- **Gate {gate}:** reached the rooftop ('{now}') {took / 60f:F1} min after 'Clean for Path {gate}' ({gateShortcuts} shortcuts).");
                    gateShortcuts = 0;
                    Note($"GATE {gate} DONE: reached '{now}'");
                    Snap("gate" + gate + "_rooftop");
                    NextGate();
                }
            }

            // The next gate, as the gate tests start one (GAMESTAGES "Clean for Path N"), or the end.
            private void NextGate()
            {
                if (gate >= 3)
                {
                    Finish("the main game and gates 1-3 played");
                    return;
                }
                gate++;
                if (gate > 1 || sawEnding)
                {
                    // The last gate left its gear and seed flags set (the path would go straight to the rooftop): a fresh
                    // run first, as after the ending (Restart Loop resets them), then the jump at its start.
                    gatePending = gate;
                    Note($"GATE {gate}: a fresh run first (Restart Loop), then 'Clean for Path {gate}'");
                    gs.Fsm.SetState("Restart Loop");
                    return;
                }
                JumpToGate();
            }

            private void JumpToGate()
            {
                gatePending = 0;
                gateStartReal = Time.realtimeSinceStartup;
                Note($"GATE {gate}: jumping to 'Clean for Path {gate}'");
                gs.Fsm.SetState("Clean for Path " + gate);
            }

            // A gate stage that doesn't move on: the gate's own test buttons, one more each stall, said out loud.
            private bool GateShortcut()
            {
                stallHooks++;
                string did = null;
                var run = Game.Gates.GateRun.Current;
                var cauldron = Object.FindAnyObjectByType<Game.PlayGround.Cauldron>();
                var defence = Game.PlayGround.CauldronDefence.Current;
                var bowling = Object.FindAnyObjectByType<Game.Bowling.BowlingStage>();
                var valley = Object.FindAnyObjectByType<Balls.BallValleyGate>();
                if (stallHooks == 1)
                {
                    if (cauldron != null) { cauldron.TestPutRecipeIn(); did = "Cauldron: Test: Put The Right Recipe In"; }
                    else if (bowling != null) { bowling.DebugWinRound(); did = "Bowling: Win This Round"; }
                    else if (valley != null) { Invoke(valley, "TestPocketAll"); did = "Ball Valley: Test: Pocket All Balls"; }
                }
                else if (stallHooks == 2)
                {
                    if (defence != null) { defence.TestSkipTo(0.98f); did = "Cauldron Defence: TestSkipTo(0.98)"; }
                    else if (bowling != null) { bowling.DebugWinRound(); did = "Bowling: Win This Round (again)"; }
                    else if (valley != null) { valley.TestBreakBlack(); did = "Ball Valley: Test: Break The Black Pumpkin"; }
                }
                else if (run != null)
                {
                    Invoke(run, stallHooks == 3 ? "TestSkipChest" : "TestToFinale");
                    did = stallHooks == 3 ? "Gate Run: Test: Stop Waiting For This Chest" : "Gate Run: Test: Skip To The Finale";
                    if (stallHooks >= 5 && bowling != null) { bowling.DebugWin(); did += " + Bowling: Win Now"; }
                }
                if (did == null) return false;
                movedBy = "SHORTCUT (gate " + gate + "): " + did;
                gateShortcuts++;
                Note(movedBy);
                return true;
            }

            private void Invoke(object target, string method)
            {
                var m = target.GetType().GetMethod(method, Any);
                if (m == null) { Note("no method " + method + " on " + target.GetType().Name); return; }
                m.Invoke(target, m.GetParameters().Length == 0 ? null : new object[] { 1f });
            }

            // A picture from the player's eyes (a copy of the main camera, post-processing on).
            private void Snap(string label)
            {
                var main = Camera.main;
                if (main == null || picDir == null) return;
                var go = new GameObject("TEST_BotCamera");
                try
                {
                    var cam = go.AddComponent<Camera>();
                    cam.CopyFrom(main);
                    cam.enabled = false;
                    var urp = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
                    if (urp != null) { var data = go.AddComponent(urp); urp.GetProperty("renderPostProcessing")?.SetValue(data, true, null); }
                    var rt = new RenderTexture(960, 540, 24);
                    cam.targetTexture = rt;
                    cam.Render();
                    var was = RenderTexture.active;
                    RenderTexture.active = rt;
                    var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                    tex.Apply();
                    RenderTexture.active = was;
                    cam.targetTexture = null;
                    string safe = new string((label ?? "x").Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
                    shotNo++;
                    File.WriteAllBytes(Path.Combine(picDir, $"{shotNo:000}_{(gate > 0 ? "gate" + gate + "_" : "")}{safe}.jpg"), tex.EncodeToJPG(85));
                    Object.DestroyImmediate(tex);
                    rt.Release();
                    Object.DestroyImmediate(rt);
                }
                catch (Exception e) { Note("picture failed: " + e.Message); }
                finally { Object.DestroyImmediate(go); }
            }

            // A stage that doesn't move on: the shortcut a test needs, said out loud.
            private void Unstick()
            {
                stateSince = Time.time - StallSeconds * 0.5f;
                shortcuts++;
                var flags = string.Join(",", gs.FsmVariables.BoolVariables.Where(b => b.Value).Select(b => b.Name));
                Note($"STALL in '{state}' ({StallSeconds:F0} s): true flags {flags}; enemies alive {AliveEnemies().Count()}; goal {goalWhat ?? "none"}");
                Snap("STALL_" + state);
                if (gate > 0 && GateShortcut()) return;
                if (state == "Wait For the Punch")
                {
                    gs.FsmVariables.FindFsmBool("BOSSPUNCH").Value = true;
                    movedBy = "SHORTCUT: BOSSPUNCH set";
                    Note(movedBy);
                    return;
                }
                foreach (var target in StagePumpkins())
                {
                    var h = HealthOf(target);
                    if (h == null || h.Value <= 0f) continue;
                    h.Value = 0f;
                    movedBy = "SHORTCUT: broke " + Clean(target.name);
                    Note(movedBy);
                    return;
                }
                foreach (var e in AliveEnemies().ToList())
                {
                    Game.Enemies.EnemyWave.Kill(e.gameObject);
                    movedBy = "SHORTCUT: killed the enemies left";
                }
                if (movedBy == null && state == "BOSS")
                    foreach (var e in Object.FindObjectsByType<EnemyHitDamage>(FindObjectsSortMode.None))
                        if (!e.IsDead && Clean(e.name).StartsWith("pumpkin_boss_Z")) { Game.Enemies.EnemyWave.Kill(e.gameObject); movedBy = "SHORTCUT: killed the boss"; }
                if (movedBy != null) Note(movedBy);
            }

            // ---------- rooms and leftovers ----------

            private static bool IsCleanUp(string s) =>
                s.StartsWith("Clean ") || s.StartsWith("clean ") || s.StartsWith("Destroy PlayGroundZ") || s == "Game Over";

            private void WatchRooms()
            {
                var v = FsmVariables.GlobalVariables.FindFsmGameObject("CurrentlySpawnedStuffs");
                stuff = v != null ? v.Value : null;
                // Note where each new thing first showed up (a few times a second is plenty).
                if (Time.frameCount % 15 == 0) NoteNewThings();
            }

            private void NoteNewThings()
            {
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                {
                    if (t.parent != null && !IsTopOfThing(t)) continue;
                    int id = t.gameObject.GetInstanceID();
                    if (staticObjects.Contains(id) || bornIn.ContainsKey(id)) continue;
                    bornIn[id] = (Clean(t.name), state);
                }
            }

            // A thing worth counting: a scene root, or a child of CurrentlySpawnedStuffs / of a scene object.
            private bool IsTopOfThing(Transform t) =>
                t.parent == null || (stuff != null && t.parent == stuff.transform) || staticObjects.Contains(t.parent.gameObject.GetInstanceID());

            private void CheckLeftovers(string cleanUp, string next)
            {
                NoteNewThings();
                var left = new Dictionary<string, (int count, string stage)>();
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                {
                    var go = t.gameObject;
                    int id = go.GetInstanceID();
                    if (staticObjects.Contains(id) || !bornIn.TryGetValue(id, out var born)) continue;
                    if (!checkStages.Contains(born.stage)) continue;                   // not this room's
                    if (origin != null && t.IsChildOf(origin.transform)) continue;     // the player's own things
                    if (Ignored(born.name) || HeldByPlayer(go)) continue;
                    string where = t.parent == null ? "scene root" : stuff != null && t.parent == stuff.transform ? "CurrentlySpawnedStuffs" : Clean(t.parent.name);
                    string key = born.name + " (under " + where + ")";
                    left.TryGetValue(key, out var had);
                    left[key] = (had.count + 1, had.stage ?? born.stage);
                }
                var enemies = AliveEnemies().Where(e => bornIn.TryGetValue(e.gameObject.GetInstanceID(), out var bb) && checkStages.Contains(bb.stage)).ToList();
                var sb = new StringBuilder();
                sb.Append($"- **Room {room}** (`{roomStartedIn ?? "?"}` ... cleaned by `{cleanUp}`, next `{next}`): ");
                if (left.Count == 0 && enemies.Count == 0) sb.Append("clean.");
                else
                {
                    sb.Append($"{left.Values.Sum(x => x.count)} things it made are still here");
                    if (enemies.Count > 0) sb.Append($" ({enemies.Count} of them enemies still alive)");
                    sb.Append(":");
                    var kept = left.Where(k => KeptOnPurpose(k.Key)).ToList();
                    foreach (var kv in left.Where(k => !KeptOnPurpose(k.Key)).OrderByDescending(k => k.Value.count))
                        sb.Append($"\n  - {kv.Value.count} x {kv.Key}, first seen in `{kv.Value.stage}`");
                    if (kept.Count > 0)
                        sb.Append($"\n  - (kept on purpose: {kept.Sum(k => k.Value.count)} candies / room kits going on to the next room)");
                }
                roomReports.Add(sb.ToString());
                Note("leftover check: " + sb.ToString().Replace("\n", " "));
                room++;
                roomStartedIn = next;
            }

            // Candy stays for the player (the purple hole keeps it); a room kit goes on to the next room.
            private static bool KeptOnPurpose(string key) =>
                key.StartsWith("Candy_") || key.Contains("_Kit_Base") || key.StartsWith("Rooftop_Base");

            private static bool HeldByPlayer(GameObject go)
            {
                var grab = go.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                return grab != null && grab.isSelected;
            }

            // Systems that live on their own outside the rooms (pools, one-shot sounds, the bot's helpers).
            private static bool Ignored(string name) =>
                name.StartsWith("One shot audio") || name.Contains("Pool") || name.StartsWith("[") ||
                name.StartsWith("Damage Number") || name.StartsWith("TEST_");

            // ---------- acting ----------

            private void Act(float dt)
            {
                float inState = Time.time - stateSince;
                giveUp = null;   // each goal says what to forget if it can't be reached
                // What the stage wants, in a player's order: what's right on them first, then the stage's own thing,
                // then the rest of the fight.
                if (inState > 2f && TryFight(dt, 2.5f)) return;
                if (TryStageThing(dt, inState)) return;
                if (inState > 2f && TryFight(dt, 14f)) return;
                if (inState > 2f && TryPickups(dt)) return;
                hasGoal = false;
                goalWhat = null;
            }

            private bool TryFight(float dt, float within)
            {
                EnemyHitDamage best = null;
                float bestD = float.MaxValue;
                var head = Head();
                foreach (var e in AliveEnemies())
                {
                    float d = Flat(e.BodyCenter - head);
                    if (d < bestD) { bestD = d; best = e; }
                }
                if (best == null || bestD > within) return false;
                int id = best.GetInstanceID();
                giveUp = () => giveUpUntil[id] = Time.time + 30f;
                if (WalkTo(best.BodyCenter, ReachOf(best), dt, "fight " + Clean(best.name))) { Punch(best); Did("punched " + Clean(best.name)); }
                return true;
            }

            private bool TryStageThing(float dt, float inState)
            {
                // The green light path (2026-11-27): walk along it into its teleport zone, then wait for the jump.
                if (state.StartsWith("Green Path"))
                {
                    var run = Game.Story.GreenPathRun.Current;
                    if (run != null && run.Path != null && run.Path.Drawn && !run.Teleporting && Camera.main != null)
                    {
                        var goal = run.NextGoal(Camera.main.transform.position);
                        bool zone = (goal - run.Path.ZoneAt).sqrMagnitude < 0.01f;
                        WalkTo(goal, 0.25f, dt, zone ? "the green path's zone" : "the green path's doorway");
                    }
                    return true;
                }
                // The Fairy (the first meeting, after the boss).
                if ((state == "Move to Fairy" || state == "wait") && inState > 2f)
                {
                    var fairy = Global("Fairy");
                    if (fairy != null && fairy.activeInHierarchy) { WalkInto(fairy, dt, "the Fairy"); return true; }
                }
                // The stage pumpkin: chase pumpkins are run into, the others are punched.
                if (inState > 3f)
                {
                    foreach (var p in StagePumpkins())
                    {
                        var h = HealthOf(p);
                        if (h == null || h.Value <= 0f) continue;
                        if (p.GetComponent<TouchOnlyDamage>() != null || p.GetComponent<Game.Enemies.BombPumpkin>() != null)
                            WalkInto(p, dt, "run into " + Clean(p.name));
                        else if (WalkTo(Centre(p), ReachOfThing(p), dt, "punch " + Clean(p.name))) { PunchThing(p); Did("punched " + Clean(p.name)); }
                        return true;
                    }
                }
                // Magic tables: lift a hat, then walk into a skull.
                if (state.Contains("Magic") || state == "Pumpkin_7")
                {
                    foreach (var name in new[] { "Skull_Chaos", "Skull_Stuffs" }.OrderBy(_ => room % 2 == 0 ? 0 : 1))
                    {
                        var skull = Global(name);
                        if (skull != null && skull.activeInHierarchy && inState > 4f) { WalkInto(skull, dt, "the skull " + name); return true; }
                    }
                    foreach (var name in new[] { "Hat_Green", "Hat_Black", "Hat_Blue" })
                    {
                        var hat = Global(name);
                        if (hat == null || !hat.activeInHierarchy || liftedHats.Contains(hat.GetInstanceID()) || inState < 4f) continue;
                        if (WalkTo(hat.transform.position, 0.7f, dt, "lift " + name))
                        {
                            hat.transform.position += Vector3.up * 1.5f;
                            Physics.SyncTransforms();
                            liftedHats.Add(hat.GetInstanceID());
                            lifts++;
                            Did("lifted " + name);
                            Note("lifted " + name);
                        }
                        return true;
                    }
                }
                // A gate's giant pumpkin (its finale): punched like any player's blast.
                foreach (var giant in Object.FindObjectsByType<Game.Gates.PumpkinBallBoss>(FindObjectsSortMode.None))
                {
                    if (giant.IsBroken || !giant.gameObject.activeInHierarchy) continue;
                    float half = 1f;
                    foreach (var c in giant.GetComponentsInChildren<Collider>())
                        if (!c.isTrigger && c.enabled) half = Mathf.Max(half, Mathf.Max(c.bounds.extents.x, c.bounds.extents.z));
                    var at = giant.transform.position;
                    if (WalkTo(at, Mathf.Clamp(half + 0.7f, 1f, 4f), dt, "punch " + Clean(giant.name)) && Time.time >= nextPunch)
                    {
                        nextPunch = Time.time + PunchEvery;
                        giant.Hit(PunchDamage * 2f, at);
                        punches++;
                        Did("punched " + Clean(giant.name));
                    }
                    return true;
                }
                // The boss and the rooftop tree.
                if (state == "BOSS" || state == "Wait For the Punch")
                {
                    foreach (var e in Object.FindObjectsByType<EnemyHitDamage>(FindObjectsSortMode.None))
                    {
                        string n = Clean(e.name);
                        if (e.IsDead || !(n.StartsWith("pumpkin_boss_Z") || n.StartsWith("Pumpkin_Tree") || n.StartsWith("PumpkinTree"))) continue;
                        if (WalkTo(e.BodyCenter, ReachOf(e), dt, "punch " + n)) { Punch(e); Did("punched " + n); }
                        return true;
                    }
                }
                // The whack-a-pumpkin round: both mallets in the hands start it; let go once it's over.
                var whack = Game.Bonus.WhackRound.Running;
                if (whack == null) foreach (var w in Object.FindObjectsByType<Game.Bonus.WhackRound>(FindObjectsSortMode.None)) whack = w;
                if (whack != null && whack.Hammers != null && whack.Hammers.Count > 0 && !whack.Finished)
                {
                    if (!whack.Playing) TakeMallets(whack, dt);
                    return true;
                }
                if (holding) LetGo();
                // The Paint Bees round: stand on its ring.
                foreach (var pb in Object.FindObjectsByType<Game.Bonus.PaintBeesRound>(FindObjectsSortMode.None))
                {
                    if (typeof(Game.Bonus.PaintBeesRound).GetField("hands", Any)?.GetValue(pb) != null) continue;
                    WalkTo(pb.transform.position, 0.2f, dt, "the Paint Bees ring");
                    return true;
                }
                return false;
            }

            private bool TryPickups(float dt)
            {
                foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                {
                    int fid = f.gameObject.GetInstanceID();
                    if (giveUpUntil.TryGetValue(fid, out var until) && Time.time < until) continue;
                    if (f.FsmName == "Rotate and Wait" && f.ActiveStateName == "Rotate and Wait")
                    {
                        giveUp = () => giveUpUntil[fid] = Time.time + 30f;
                        WalkInto(f.gameObject, dt, "take " + Clean(f.gameObject.name));
                        return true;
                    }
                    if (f.FsmName == "Chest_Control" && f.ActiveStateName == "Play_Smoke_n_Wait")
                    {
                        giveUp = () => giveUpUntil[fid] = Time.time + 30f;
                        if (WalkTo(f.transform.position, 1f, dt, "open " + Clean(f.gameObject.name)))
                        {
                            f.SendEvent("FINISHED");
                            Note("opened " + Clean(f.gameObject.name));
                            Did("opened " + Clean(f.gameObject.name));
                        }
                        return true;
                    }
                }
                return false;
            }

            private bool holding;
            private int malletTakes;

            private void TakeMallets(Game.Bonus.WhackRound round, float dt)
            {
                foreach (var fist in Object.FindObjectsByType<Fist>(FindObjectsSortMode.None)) fist.enabled = false;
                var hands = Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>(FindObjectsSortMode.None);
                foreach (var h in round.Hammers)
                {
                    if (h == null || h.Held) continue;
                    if (!WalkTo(h.transform.position, 0.9f, dt, "the mallets")) return;
                    var hand = hands.FirstOrDefault(x => !x.hasSelection);
                    if (hand == null) return;
                    hand.selectInput.inputSourceMode = UnityEngine.XR.Interaction.Toolkit.Inputs.Readers.XRInputButtonReader.InputSourceMode.ManualValue;
                    hand.selectInput.QueueManualState(true, 1f, true, false);
                    hand.interactionManager.SelectEnter((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)hand,
                        (UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)h.Grab);
                    holding = true;
                    Did("took a mallet");
                    if (++malletTakes <= 6) Note("took a mallet in " + (hand.transform.parent != null ? hand.transform.parent.name : hand.name));
                    else if (malletTakes == 7) Note("the mallets keep slipping out of the hands (not logged again)");
                }
            }

            private void LetGo()
            {
                holding = false;
                foreach (var fist in Object.FindObjectsByType<Fist>(FindObjectsInactive.Include, FindObjectsSortMode.None)) fist.enabled = true;
                foreach (var hand in Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>(FindObjectsSortMode.None))
                {
                    hand.selectInput.QueueManualState(false, 0f, false, true);
                    foreach (var sel in hand.interactablesSelected.ToList())
                        hand.interactionManager.SelectExit((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)hand, sel);
                }
                Note("let go of what the hands held");
            }

            // ---------- body ----------

            private Vector3 Head() => Camera.main != null ? Camera.main.transform.position : origin.transform.position;

            private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

            // Walks (at a walking pace, walls block it) until within Stop metres; true once there. Stuck for 2 s: a
            // teleport next to it, as a VR player would.
            private bool WalkTo(Vector3 target, float stop, float dt, string what)
            {
                if (!hasGoal || goalWhat != what)
                {
                    hasGoal = true;
                    goalWhat = what;
                    stuckSince = -1f;
                    teleportsAtGoal = 0;
                }
                goal = target;
                var head = Head();
                var to = target - head;
                to.y = 0f;
                float d = to.magnitude;
                if (d <= stop) { stuckSince = -1f; return true; }
                // Turn to face where it goes (as a player looks where they walk).
                if (xr != null && Camera.main != null)
                {
                    var fwd = Camera.main.transform.forward;
                    fwd.y = 0f;
                    if (fwd.sqrMagnitude > 0.0001f)
                    {
                        float yaw = Vector3.SignedAngle(fwd, to, Vector3.up);
                        xr.RotateAroundCameraUsingOriginUp(Mathf.Clamp(yaw, -240f * dt, 240f * dt));
                    }
                }
                var move = to / d * Mathf.Min(d - stop * 0.5f, WalkSpeed * dt);
                // Walking never changes its height: the rig has no gravity, so a Character Controller that stepped up
                // onto a pumpkin or a table stayed up there (2026-11-26: head 2.28 m, so a bad pumpkin's fuse never lit).
                float footY = origin.transform.position.y;
                if (cc != null && cc.enabled) cc.Move(move);
                else origin.transform.position += move;
                if (Mathf.Abs(origin.transform.position.y - footY) > 0.001f)
                {
                    bool was = cc != null && cc.enabled;
                    if (was) cc.enabled = false;
                    var p = origin.transform.position;
                    origin.transform.position = new Vector3(p.x, footY, p.z);
                    Physics.SyncTransforms();
                    if (was) cc.enabled = true;
                }
                walked += Flat(Head() - head);
                // Stuck: it hardly moved for 2 s.
                if (Flat(Head() - lastPos) > 0.25f) { lastPos = Head(); stuckSince = -1f; }
                else if (stuckSince < 0f) stuckSince = Time.time;
                else if (Time.time - stuckSince > 2f)
                {
                    stuckSince = -1f;
                    if (++teleportsAtGoal > 3)
                    {
                        Note($"gave up on '{what}' for 30 s (can't reach it)");
                        giveUp?.Invoke();
                        teleportsAtGoal = 0;
                        hasGoal = false;
                        return false;
                    }
                    Teleport(target, stop);
                }
                return false;
            }

            // Walks into a thing's trigger (a touch only counts on the way in: back off a step first if already in).
            private void WalkInto(GameObject thing, float dt, string what)
            {
                int id = thing.GetInstanceID();
                var c = Centre(thing);
                if (touchedAt.TryGetValue(id, out var at) && Time.time - at < 1.2f)
                {
                    // Just touched: step back a little before going in again.
                    var away = Head() - c;
                    away.y = 0f;
                    if (away.sqrMagnitude < 0.01f) away = Vector3.back;
                    WalkTo(c + away.normalized * 1.4f, 0.2f, dt, what + " (back off)");
                    return;
                }
                float reach = 0.15f;
                var col = thing.GetComponentInChildren<Collider>();
                if (col != null) reach = Mathf.Max(0.15f, Mathf.Min(col.bounds.extents.x, col.bounds.extents.z) * 0.8f);
                if (WalkTo(c, reach, dt, what))
                {
                    touchedAt[id] = Time.time;
                    touches++;
                    Did(what);
                }
            }

            private void Teleport(Vector3 target, float stop)
            {
                var spot = target;
                if (NavMesh.SamplePosition(target, out var hit, 2f, NavMesh.AllAreas)) spot = hit.position;
                var head = Head();
                var off = head - target;
                off.y = 0f;
                spot += off.sqrMagnitude > 0.01f ? off.normalized * Mathf.Max(0.3f, stop * 0.8f) : Vector3.back * 0.5f;
                bool was = cc != null && cc.enabled;
                if (was) cc.enabled = false;
                origin.transform.position += new Vector3(spot.x - head.x, 0f, spot.z - head.z);
                Physics.SyncTransforms();
                if (was) cc.enabled = true;
                lastPos = Head();
                teleports++;
                Note($"teleported (stuck) towards {goalWhat}");
            }

            private void Did(string what)
            {
                lastAction = what;
                lastActionAt = Time.time;
            }

            // How close to an enemy's middle a punch lands: its body's half width plus an arm.
            private static float ReachOf(EnemyHitDamage e)
            {
                float half = 0f;
                foreach (var c in e.GetComponentsInChildren<Collider>())
                {
                    if (c.isTrigger || !c.enabled) continue;
                    half = Mathf.Max(half, Mathf.Max(c.bounds.extents.x, c.bounds.extents.z));
                }
                return Mathf.Clamp(half + 0.7f, 0.8f, 3.5f);
            }

            private static float ReachOfThing(GameObject thing)
            {
                float half = 0f;
                foreach (var c in thing.GetComponentsInChildren<Collider>())
                {
                    if (c.isTrigger || !c.enabled) continue;
                    half = Mathf.Max(half, Mathf.Max(c.bounds.extents.x, c.bounds.extents.z));
                }
                return Mathf.Clamp(half + 0.7f, 0.9f, 3.5f);
            }

            private void Punch(EnemyHitDamage e)
            {
                if (Time.time < nextPunch) return;
                nextPunch = Time.time + PunchEvery;
                // The player's own hit: EXP, reactions and deaths as from a fist.
                e.TakeBlast(PunchDamage, e.BodyCenter, 0.5f, true);
                punches++;
            }

            // A stage pumpkin without Enemy Hit Damage: its Health FSM loses 1 a punch (what its Damage FSM does).
            private void PunchThing(GameObject thing)
            {
                if (Time.time < nextPunch) return;
                nextPunch = Time.time + PunchEvery;
                punches++;
                var e = thing.GetComponentInParent<EnemyHitDamage>();
                if (e != null) { e.TakeBlast(PunchDamage, e.BodyCenter, 0.5f, true); return; }
                var h = HealthOf(thing);
                if (h != null) h.Value -= 1f;
            }

            // ---------- finding things ----------

            private static readonly string[] StagePumpkinNames =
            {
                "pumpkin_Start", "Pumpkin_0", "Pumpkin_1", "Pumpkin_2", "Pumpkin_3", "Pumpkin_4", "Pumpkin_5", "Pumpkin_6",
                "Pumpkin_7", "-Wood_Barrels_Whack_Pumpkin_6-", "-Wood_Barrels_Whack_Pumpkin_7-",
            };

            private IEnumerable<GameObject> StagePumpkins()
            {
                foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                {
                    if (!f.FsmName.EndsWith("Health")) continue;
                    string n = Clean(f.gameObject.name).Replace("Bad_", "").Replace("_Chaos", "");
                    if (Array.IndexOf(StagePumpkinNames, n) < 0) continue;
                    if ((n == "pumpkin_Start" || n == "Pumpkin_0") && state != "Stage 1 - The Start") continue;
                    if (n.StartsWith("-Wood_Barrels") && !state.StartsWith("Whack")) continue;
                    yield return f.gameObject;
                }
            }

            private static FsmFloat HealthOf(GameObject go)
            {
                foreach (var f in go.GetComponents<PlayMakerFSM>())
                {
                    if (!f.FsmName.EndsWith("Health")) continue;
                    var h = f.FsmVariables.FindFsmFloat("health");
                    if (h != null) return h;
                }
                return null;
            }

            private static readonly string[] NotEnemies =
            {
                "Door", "pumpkin_Start", "Pumpkin_0", "Pumpkin_1", "Pumpkin_2", "Pumpkin_3", "Pumpkin_4", "Pumpkin_5",
                "Pumpkin_6", "Pumpkin_7", "Bad_Pumpkin", "Pumpkin_On_Tree", "Pumpkin_Mob", "Pumpkin_Boss", "pumpkin_boss_Z",
                "Paint_Giant_Slime", "Pumpkin_Tree", "PumpkinTree",
            };

            private IEnumerable<EnemyHitDamage> AliveEnemies()
            {
                foreach (var e in Object.FindObjectsByType<EnemyHitDamage>(FindObjectsSortMode.None))
                {
                    if (e.IsDead || e.IsFurniture || !e.gameObject.activeInHierarchy || !e.CompareTag("Enemy")) continue;
                    if (giveUpUntil.TryGetValue(e.GetInstanceID(), out var until) && Time.time < until) continue;
                    string n = Clean(e.name);
                    if (NotEnemies.Any(x => n.StartsWith(x) || n.Contains("Door"))) continue;
                    yield return e;
                }
            }

            private static GameObject Global(string name)
            {
                var v = FsmVariables.GlobalVariables.FindFsmGameObject(name);
                return v != null ? v.Value : null;
            }

            private static Vector3 Centre(GameObject go)
            {
                var rb = go.GetComponent<Rigidbody>();
                if (rb != null) return rb.worldCenterOfMass;
                var r = go.GetComponentInChildren<Renderer>();
                return r != null ? r.bounds.center : go.transform.position;
            }

            private static string Clean(string n) => n.Replace("(Clone)", "").Trim();

            private static string FirstLine(string s) => string.IsNullOrEmpty(s) ? "" : s.Split('\n')[0].Trim();

            // ---------- notes and the report ----------

            private void Note(string s)
            {
                string line = $"[{Time.time,7:F1} | {(Time.realtimeSinceStartup - startReal) / 60f,5:F1} min] {s}";
                log.AppendLine(line);
                if (s.StartsWith("STALL") || s.StartsWith("BOT ERROR") || s.StartsWith("SHORTCUT")) Debug.LogWarning("[Bot] " + s);
            }

            private void OnLog(string message, string stack, LogType type)
            {
                if (type == LogType.Log || message.StartsWith("[Bot]")) return;
                string key = type + ": " + (message.Length > 180 ? message.Substring(0, 180) : message);
                console.TryGetValue(key, out int n);
                console[key] = n + 1;
                if (n == 0) consoleWhere[key] = (state ?? "boot") + " | " + FirstLine(stack);
            }

            public void Finish(string why)
            {
                if (done) return;
                done = true;
                EditorApplication.update -= Tick;
                Application.logMessageReceived -= OnLog;
                if (state != null) stages.Add((state, Time.time - stateSince, movedBy ?? "(still here)"));
                Note("finished: " + why);
                // What the run made that's still in the scene and outside CurrentlySpawnedStuffs (no clean-up reaches it).
                try
                {
                    endLeaks.Clear();
                    foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                    {
                        if (t.parent != null) continue;
                        int id = t.gameObject.GetInstanceID();
                        if (staticObjects.Contains(id) || !bornIn.TryGetValue(id, out var born) || Ignored(born.name)) continue;
                        endLeaks.Add($"{born.name} (first seen in `{born.stage}`)");
                    }
                }
                catch (Exception) { }
                try { WriteReport(why); }
                catch (Exception e) { Debug.LogError("[Bot] report failed: " + e.Message); }
                if (EditorApplication.isPlaying)
                {
                    var pc = Game.Locomotion.PcControls.Active;
                    if (pc != null) pc.enabled = true;
                }
                if (Game.Saving.SaveManager.Profile == Profile) DeleteSaves();
                runner = null;
            }

            private static void DeleteSaves()
            {
                string dir = Path.Combine(Application.persistentDataPath, "Saves", Profile);
                try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
                catch (Exception e) { Debug.LogWarning("[Bot] couldn't delete " + dir + ": " + e.Message); }
            }

            private void WriteReport(string why)
            {
                Directory.CreateDirectory(ReportFolder);
                string path = Path.Combine(ReportFolder, $"PlayThroughBot_{stamp}.md");
                var sb = new StringBuilder();
                float minutes = (Time.realtimeSinceStartup - startReal) / 60f;
                sb.AppendLine($"# Play-through bot, {DateTime.Now:yyyy-MM-dd HH:mm}");
                sb.AppendLine();
                sb.AppendLine($"**Result:** {why}. {minutes:F1} real minutes, {stages.Count} stage changes, {roomReports.Count} rooms checked.");
                sb.AppendLine();
                sb.AppendLine($"Bot: walked {walked:F0} m, {teleports} teleports (stuck), {touches} touches, {punches} punches, {lifts} hats lifted, **{shortcuts} shortcuts**. Saves/{Profile} deleted.");
                sb.AppendLine();
                sb.AppendLine("## Main game and gates");
                sb.AppendLine();
                if (gateResults.Count == 0) sb.AppendLine("- (nothing finished)");
                foreach (var g in gateResults) sb.AppendLine(g);
                if (gate > 0 && !gateResults.Any(g => g.StartsWith($"- **Gate {gate}:"))) sb.AppendLine($"- **Gate {gate}:** not finished ({why}).");
                sb.AppendLine();
                sb.AppendLine($"Pictures: {shotNo} in `{picDir}` (numbered in order, named by stage).");
                sb.AppendLine();
                sb.AppendLine("## Rooms: what each room made that was still there after its clean-up stage");
                sb.AppendLine();
                if (roomReports.Count == 0) sb.AppendLine("- (no room finished)");
                foreach (var r in roomReports) sb.AppendLine(r);
                sb.AppendLine();
                sb.AppendLine("## At the end: things the run made at the scene's top (outside CurrentlySpawnedStuffs: no clean-up removes them)");
                sb.AppendLine();
                if (endLeaks.Count == 0) sb.AppendLine("- none");
                foreach (var g in endLeaks.GroupBy(x => x)) sb.AppendLine($"- {g.Count()} x {g.Key}");
                sb.AppendLine();
                sb.AppendLine("## Stages in order");
                sb.AppendLine();
                sb.AppendLine("| # | Stage | Seconds | Moved on by |");
                sb.AppendLine("|---|---|---|---|");
                for (int i = 0; i < stages.Count; i++)
                    sb.AppendLine($"| {i + 1} | {stages[i].name} | {stages[i].seconds:F1} | {stages[i].by} |");
                sb.AppendLine();
                var missed = allStates.Where(s => !seen.Contains(s)).ToList();
                sb.AppendLine($"## GAMESTAGES coverage: {seen.Count} of {allStates.Length} states seen");
                sb.AppendLine();
                sb.AppendLine("Not seen this run: " + (missed.Count > 0 ? string.Join(", ", missed.Select(m => "`" + m + "`")) : "none") + ".");
                sb.AppendLine();
                sb.AppendLine($"## Console: {console.Count} different warnings/errors");
                sb.AppendLine();
                foreach (var kv in console.OrderByDescending(k => k.Key.StartsWith("Error") || k.Key.StartsWith("Exception")).ThenByDescending(k => k.Value))
                    sb.AppendLine($"- {kv.Value} x {kv.Key.Replace("\n", " ")} (first in {consoleWhere[kv.Key]})");
                File.WriteAllText(path, sb.ToString());
                File.WriteAllText(Path.ChangeExtension(path, null) + "_log.txt", log.ToString());
                Debug.Log($"[Bot] {why}. Report: {path}");
            }
        }
    }
}
