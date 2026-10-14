// Renders a prefab with its real Unity materials into a PNG strip (one tile per view), in a preview scene, so the
// open scene isn't touched. Run with the Unity MCP's eval_file (or `unity command eval_file --file <this>`).
// Edit the settings below first. Writes OUT (keep it outside AppData if Blender/other apps must see it; the
// session scratchpad is fine for reading it back yourself).
string PREFAB = "Assets/Prefabs/Enemies/Cockroach.prefab";
string OUT = "K:/WORK_2026/08_04/_claude/unity_render.png";
string[] ONLY_RENDERERS = { "Cockroach_Smol" };          // renderer objects to show (empty = all); others hidden
var BONE_TURNS = new System.Collections.Generic.Dictionary<string, Vector3> // local euler degrees added to bones
{
    // { "wing_lid.L", new Vector3(0, 0, 60) }, { "wing_lid.R", new Vector3(0, 0, -60) },   // open the cockroach lids
};
Vector3[] VIEWS = { new Vector3(0f, -0.4f, 1f), new Vector3(1f, 0.3f, 0.6f), new Vector3(0f, 1f, -0.3f) }; // from target to eye
float ZOOM = 1.05f;                                       // < 1 = closer
int SIZE = 512;

var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB);
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
try
{
    var go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, scene);
    go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
    Bounds b = new Bounds(); bool any = false;
    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
    {
        bool keep = ONLY_RENDERERS.Length == 0 ? !(r is ParticleSystemRenderer) : System.Array.IndexOf(ONLY_RENDERERS, r.name) >= 0;
        r.gameObject.SetActive(keep || r.gameObject.activeSelf && ONLY_RENDERERS.Length == 0);
        if (!keep) { r.enabled = false; continue; }
        for (var p = r.transform; p != null; p = p.parent) p.gameObject.SetActive(true);
        if (r is SkinnedMeshRenderer s) s.updateWhenOffscreen = true;
    }
    foreach (var a in go.GetComponentsInChildren<Animator>(true)) a.enabled = false;   // hold the bind pose
    foreach (var t in go.GetComponentsInChildren<Transform>(true))
        if (BONE_TURNS.TryGetValue(t.name, out var turn)) t.localRotation *= Quaternion.Euler(turn);
    foreach (var r in go.GetComponentsInChildren<Renderer>(false))
    {
        if (!r.enabled || r is ParticleSystemRenderer) continue;
        if (any) b.Encapsulate(r.bounds); else { b = r.bounds; any = true; }
    }
    if (!any) return "nothing to render";

    void Add(string name, LightType type, Quaternion rot, float intensity)
    {
        var lg = new GameObject(name); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lg, scene);
        var l = lg.AddComponent<Light>(); l.type = type; l.intensity = intensity; lg.transform.rotation = rot;
    }
    Add("Key", LightType.Directional, Quaternion.Euler(50, -30, 0), 1.2f);
    Add("Fill", LightType.Directional, Quaternion.Euler(-40, 150, 0), 0.6f);
    var camGo = new GameObject("Cam"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camGo, scene);
    var cam = camGo.AddComponent<Camera>(); cam.scene = scene; cam.clearFlags = CameraClearFlags.SolidColor;
    cam.backgroundColor = new Color(0.25f, 0.25f, 0.27f); cam.fieldOfView = 30; cam.nearClipPlane = 0.01f; cam.farClipPlane = 50f;
    var rt = new RenderTexture(SIZE, SIZE, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
    var strip = new Texture2D(SIZE * VIEWS.Length, SIZE, TextureFormat.RGB24, false);
    var tile = new Texture2D(SIZE, SIZE, TextureFormat.RGB24, false);
    for (int i = 0; i < VIEWS.Length; i++)
    {
        float dist = b.extents.magnitude / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * ZOOM;
        camGo.transform.position = b.center + VIEWS[i].normalized * dist;
        camGo.transform.LookAt(b.center, Mathf.Abs(VIEWS[i].normalized.y) > 0.95f ? Vector3.forward : Vector3.up);
        cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt; tile.ReadPixels(new Rect(0, 0, SIZE, SIZE), 0, 0); tile.Apply();
        strip.SetPixels(i * SIZE, 0, SIZE, SIZE, tile.GetPixels());
    }
    RenderTexture.active = null; cam.targetTexture = null; strip.Apply();
    System.IO.File.WriteAllBytes(OUT, strip.EncodeToPNG());
    UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(strip); UnityEngine.Object.DestroyImmediate(tile);
    return "wrote " + OUT + " (bounds " + b.size + ")";
}
finally
{
    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
}
