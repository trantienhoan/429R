// C# 6 (CodeDom). Play-test starter, 2026-11-10. Call it TWICE (or more): it does one step per call.
//  1st call (edit mode): checks nothing is compiling / no Prefab Mode, then enters Play mode (domain reload follows).
//  next call (Play mode, after ~10-20 s): closes the Health Warning, sets SaveManager.Profile = "_claude_bot" (the user's
//  3 real slots in Saves/<SteamID> and Saves/Local are never read for play or written), WIPES the _claude_bot folder's
//  slot files (so the run is a FRESH level-0 new game), then starts slot 1 from the save slot menu once it is Open.
//  Later calls just report the state. When "RUN STARTED" shows and Time.time > 5, test away.
// God mode: pt_god_on.cs / pt_god_off.cs. Leave: pt_stop.cs. State: pt_state.cs. Log: pt_log.txt next to this file.
var log = @"K:\WORK_2026\08_04\_claude\playtest_2026-11-10\pt_log.txt";
System.Action<string> W = s => System.IO.File.AppendAllText(log, System.DateTime.Now.ToString("HH:mm:ss") + " [pt_start] " + s + "\n");
var BF = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
if (!UnityEditor.EditorApplication.isPlaying)
{
    if (UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating) return "WAIT: Unity is compiling/importing; call again later";
    var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
    if (stage != null) return "STOP: a prefab is open in Prefab Mode (" + stage.assetPath + "); not entering Play";
    if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return "WAIT: already entering Play mode; call again in ~15 s";
    UnityEditor.EditorApplication.isPlaying = true;
    W("entering Play mode");
    return "ENTERING PLAY: call pt_start.cs again in ~15 s";
}
if (Game.Saving.SaveManager.CurrentSlot > 0)
{
    return "RUN STARTED already: profile " + Game.Saving.SaveManager.Profile + " slot " + Game.Saving.SaveManager.CurrentSlot +
           " loaded " + Game.Saving.SaveManager.IsLoaded + " time " + UnityEngine.Time.time.ToString("F1") +
           (Game.Saving.SaveManager.Profile != "_claude_bot" ? "  !!! NOT THE BOT PROFILE: stop Play now (pt_stop.cs) !!!" : "");
}
var menu = UnityEngine.Object.FindAnyObjectByType<Game.Menus.SaveSlotMenu>();
if (menu == null) return "WAIT: no Save Slot Menu found yet (scene still loading?); call again";
var stepF = typeof(Game.Menus.SaveSlotMenu).GetField("step", BF);
string stepNow = stepF.GetValue(menu).ToString();
if (stepNow == "Starting") return "STARTING: the slot is loading; call again in a few s (profile " + Game.Saving.SaveManager.Profile + ")";
// The health warning holds the menu back: close it.
foreach (var hw in UnityEngine.Object.FindObjectsByType<Game.Menus.HealthWarning>(UnityEngine.FindObjectsSortMode.None))
{
    var panel = (UnityEngine.CanvasGroup)typeof(Game.Menus.HealthWarning).GetField("panel", BF).GetValue(hw);
    if (panel != null) panel.gameObject.SetActive(false);
    hw.enabled = false;
}
Game.Saving.SaveManager.Profile = "_claude_bot";
// A fresh new game: only the bot's own folder is emptied (guarded by its name).
var folder = Game.Saving.SaveManager.SaveFolder;
if (folder.Replace('\\', '/').EndsWith("/Saves/_claude_bot") && System.IO.Directory.Exists(folder))
{
    foreach (var f in System.IO.Directory.GetFiles(folder)) System.IO.File.Delete(f);
    W("emptied " + folder);
}
float t0 = UnityEngine.Time.realtimeSinceStartup;
UnityEditor.EditorApplication.CallbackFunction cb = null;
cb = () =>
{
    if (!UnityEditor.EditorApplication.isPlaying || menu == null) { UnityEditor.EditorApplication.update -= cb; return; }
    if (Game.Saving.SaveManager.CurrentSlot > 0) { UnityEditor.EditorApplication.update -= cb; return; }
    var step = stepF.GetValue(menu).ToString();
    if (step == "Open")
    {
        if (Game.Saving.SaveManager.Profile != "_claude_bot") { W("profile changed to " + Game.Saving.SaveManager.Profile + " - NOT starting"); UnityEditor.EditorApplication.update -= cb; return; }
        menu.StartSlot(0);
        W("menu open: started slot 1 (profile " + Game.Saving.SaveManager.Profile + "), step now " + stepF.GetValue(menu));
        UnityEditor.EditorApplication.update -= cb;
    }
    else if (UnityEngine.Time.realtimeSinceStartup - t0 > 30f) { W("menu never opened (step " + step + ")"); UnityEditor.EditorApplication.update -= cb; }
};
UnityEditor.EditorApplication.update += cb;
W("profile set to " + Game.Saving.SaveManager.Profile + ", menu step " + stepNow + ", waiting to start slot 1");
return "STARTING RUN: profile " + Game.Saving.SaveManager.Profile + ", menu step " + stepNow + "; call again in ~8 s until it says RUN STARTED";
