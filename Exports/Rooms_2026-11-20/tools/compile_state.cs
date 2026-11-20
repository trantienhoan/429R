// C# 6: compile state and the newest compile errors from the Console log file.
var sb = new System.Text.StringBuilder();
sb.AppendLine("compiling " + UnityEditor.EditorApplication.isCompiling + " updating " + UnityEditor.EditorApplication.isUpdating + " failed " + UnityEditor.EditorUtility.scriptCompilationFailed);
sb.AppendLine("SecretRoom type: " + (System.Type.GetType("Game.Rooms.SecretRoom, Assembly-CSharp") != null) + ", RandomPumpkinSize: " + (System.Type.GetType("Game.Items.RandomPumpkinSize, Assembly-CSharp") != null) + ", CandiesOnDeath: " + (System.Type.GetType("Game.Enemies.CandiesOnDeath, Assembly-CSharp") != null));
var logPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData) + @"\Unity\Editor\Editor.log";
if (System.IO.File.Exists(logPath))
{
    string text;
    using (var fs = new System.IO.FileStream(logPath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
    using (var sr = new System.IO.StreamReader(fs)) { if (fs.Length > 400000) fs.Seek(-400000, System.IO.SeekOrigin.End); text = sr.ReadToEnd(); }
    var lines = text.Split('\n');
    int shown = 0;
    for (int i = lines.Length - 1; i >= 0 && shown < 12; i--)
        if (lines[i].Contains("error CS")) { sb.AppendLine(lines[i].Trim()); shown++; }
}
return sb.ToString();
