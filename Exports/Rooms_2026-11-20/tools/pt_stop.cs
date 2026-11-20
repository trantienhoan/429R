// C# 6: leaves Play mode (ends god mode and the playthrough bot first). Call pt_state.cs after ~5 s to confirm
// "playing False". Play-mode changes vanish on exit; the edit-mode scene is untouched.
if (!UnityEditor.EditorApplication.isPlaying) return "not playing (already in edit mode)";
var god = UnityEngine.GameObject.Find("__PT_GOD__");
if (god != null) UnityEngine.Object.DestroyImmediate(god);
var bot = UnityEngine.GameObject.Find("__BOT__");
if (bot != null) UnityEngine.Object.DestroyImmediate(bot);
System.IO.File.AppendAllText(@"K:\WORK_2026\08_04\_claude\playtest_2026-11-10\pt_log.txt", System.DateTime.Now.ToString("HH:mm:ss") + " [pt_stop] leaving Play (profile " + Game.Saving.SaveManager.Profile + ")\n");
UnityEditor.EditorApplication.isPlaying = false;
return "LEAVING PLAY: confirm with pt_state.cs in ~5 s";
