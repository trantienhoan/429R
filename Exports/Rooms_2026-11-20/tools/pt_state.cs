// C# 6: Editor state for play testers: play/compile/prefab stage/scene dirty, Time, GAMESTAGES state, save profile + slot.
var sb = new System.Text.StringBuilder();
sb.Append("playing " + UnityEditor.EditorApplication.isPlaying + " paused " + UnityEditor.EditorApplication.isPaused + " compiling " + UnityEditor.EditorApplication.isCompiling + " updating " + UnityEditor.EditorApplication.isUpdating + "\n");
var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
sb.Append("prefab stage " + (stage != null ? stage.assetPath : "none") + "\n");
var scn = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
sb.Append("scene " + scn.name + " dirty " + scn.isDirty + "\n");
sb.Append("time " + UnityEngine.Time.time.ToString("F1") + " real " + UnityEngine.Time.realtimeSinceStartup.ToString("F1") + " timeScale " + UnityEngine.Time.timeScale + "\n");
sb.Append("profile " + Game.Saving.SaveManager.Profile + " slot " + Game.Saving.SaveManager.CurrentSlot + " loaded " + Game.Saving.SaveManager.IsLoaded + " folder " + Game.Saving.SaveManager.SaveFolder + "\n");
if (UnityEditor.EditorApplication.isPlaying)
{
    foreach (var x in UnityEngine.Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
    {
        if (!x.gameObject.scene.IsValid()) continue;
        if (x.gameObject.name == "-GAMESTAGES" && x.FsmName == "GAMESTAGES") sb.Append("GAMESTAGES " + x.ActiveStateName + "\n");
        if (x.gameObject.name == "XR Origin" && x.FsmName == "PlayerHealth") sb.Append("PlayerHealth " + x.FsmVariables.GetFsmFloat("PlayerHealth").Value + " state " + x.ActiveStateName + "\n");
    }
    var bot = UnityEngine.GameObject.Find("__PT_GOD__");
    sb.Append("god mode " + (bot != null) + "\n");
}
return sb.ToString();
