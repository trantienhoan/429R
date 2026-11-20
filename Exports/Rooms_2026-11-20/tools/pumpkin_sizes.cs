// C# 6, Play mode (bot profile): makes 40 Pumpkin_On_Tree far off the stage (held still), waits 2 s (their grow-in),
// and writes each one's size, scale, mass and health to pumpkin_sizes.txt; then removes them. Also one forced 0.75 and
// one forced 1.5 for the ends.
if (!UnityEditor.EditorApplication.isPlaying) return "not playing";
if (Game.Saving.SaveManager.Profile != "_claude_bot") return "not the bot profile";
var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Prefabs/Items/Breakable/Pumpkin_On_Tree.prefab");
var outFile = @"K:\WORK_2026\08_04\_claude\rooms_2026-11-20\pumpkin_sizes.txt";
System.IO.File.WriteAllText(outFile, "");
var made = new System.Collections.Generic.List<UnityEngine.GameObject>();
var holder = new UnityEngine.GameObject("__PUMPKIN_SIZE_TEST__");
holder.transform.position = new UnityEngine.Vector3(80f, 60f, 80f);
for (int i = 0; i < 42; i++)
{
    if (i == 40) Game.Items.RandomPumpkinSize.ForceNextSize = 0.75f;
    if (i == 41) Game.Items.RandomPumpkinSize.ForceNextSize = 1.5f;
    var at = holder.transform.position + new UnityEngine.Vector3((i % 7) * 3f, 0f, (i / 7) * 3f);
    var go = (UnityEngine.GameObject)UnityEngine.Object.Instantiate(prefab, at, UnityEngine.Quaternion.identity, holder.transform);
    var rb = go.GetComponent<UnityEngine.Rigidbody>();
    if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
    made.Add(go);
}
float t0 = UnityEngine.Time.realtimeSinceStartup;
UnityEditor.EditorApplication.CallbackFunction cb = null;
cb = () =>
{
    if (!UnityEditor.EditorApplication.isPlaying) { UnityEditor.EditorApplication.update -= cb; return; }
    if (UnityEngine.Time.realtimeSinceStartup - t0 < 2.5f) return;
    UnityEditor.EditorApplication.update -= cb;
    var sb = new System.Text.StringBuilder("size\tscale\tmass\thealth\n");
    var buckets = new System.Collections.Generic.SortedDictionary<float, int>();
    foreach (var go in made)
    {
        if (go == null) { sb.AppendLine("gone"); continue; }
        var r = go.GetComponent<Game.Items.RandomPumpkinSize>();
        float h = -1f;
        foreach (var f in go.GetComponents<PlayMakerFSM>()) if (f.FsmName == "Health") h = f.FsmVariables.FindFsmFloat("health").Value;
        var rb = go.GetComponent<UnityEngine.Rigidbody>();
        float size = r != null ? r.Size : -1f;
        sb.AppendLine(size.ToString("F2") + "\t" + go.transform.localScale.x.ToString("F3") + "\t" + (rb != null ? rb.mass.ToString("F0") : "?") + "\t" + h);
        float b = UnityEngine.Mathf.Floor(size * 10f) / 10f; int c; buckets.TryGetValue(b, out c); buckets[b] = c + 1;
    }
    sb.AppendLine("sizes by 0.1: ");
    foreach (var kv in buckets) sb.AppendLine("  " + kv.Key.ToString("F1") + ": " + new string('#', kv.Value));
    System.IO.File.WriteAllText(outFile, sb.ToString());
    UnityEngine.Object.Destroy(holder);
};
UnityEditor.EditorApplication.update += cb;
return "made 42 pumpkins; results in 2.5 s";
