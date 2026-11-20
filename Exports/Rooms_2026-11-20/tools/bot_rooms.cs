// 2026-11-10 play-test copy of unity_mcp/bot_run_v4.cs (logs in playtest_2026-11-10/bot-playthrough/). Additions: closes
// Tutorial Notices like a player would (logged), logs a real-time pause stall (timeScale 0 > 20 s), sets BOSSPUNCH itself
// 15 s into 'Wait For the Punch' (the lid punch), and watches box bombs (state changes, bangs), bee poison, player health FSM
// state, shop restocks, special-stage stat drops and the XR Origin falling below the world. Start the run first with pt_start.cs.
// v2 (2026-11-06): C# 6, own save folder + start menu, Paint Bees ring, Console log. Playthrough bot: plays GAMESTAGES from wherever it is, doing what a player does at each stage, and logs every stage
// change and any stall. God mode (no damage).
if (!UnityEditor.EditorApplication.isPlaying) return "not playing";
if (Game.Saving.SaveManager.Profile != "_claude_bot") return "NOT the bot profile (" + Game.Saving.SaveManager.Profile + "): run pt_start.cs first";
if (GameObject.Find("__BOT__") != null) return "bot already running";
var logPath = @"K:\WORK_2026\08_04\_claude\rooms_2026-11-20\bot_" + UnityEditor.SessionState.GetString("RT_VARIANT", "stuffs") + "_log.txt";
System.IO.File.AppendAllText(logPath, "==== bot (re)started " + System.DateTime.Now.ToString("HH:mm:ss") + " ====\n");
System.Action<string> Log = s => System.IO.File.AppendAllText(logPath, $"[{Time.time,7:F1}|{Time.realtimeSinceStartup,7:F1}] {s}\n");
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;

PlayMakerFSM gs = null, playerHealth = null, playerDamage = null;
foreach (var x in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
{
    if (!x.gameObject.scene.IsValid()) continue;
    if (x.gameObject.name == "-GAMESTAGES" && x.FsmName == "GAMESTAGES") gs = x;
    if (x.gameObject.name == "XR Origin" && x.FsmName == "PlayerHealth") playerHealth = x;
    if (x.gameObject.name == "XR Origin" && x.FsmName == "Damage") playerDamage = x;
}
var origin = playerHealth.gameObject;
Collider body = null;
foreach (var c in origin.GetComponentsInChildren<Collider>()) if (c.CompareTag("Player") && !c.isTrigger) body = c;
if (body == null) foreach (var c in origin.GetComponentsInChildren<Collider>()) if (c.CompareTag("Player")) body = c;
Log($"start: GAMESTAGES={gs.ActiveStateName} body={(body ? body.name : "none")}");

// Which pumpkin each stage waits for the player to break.
var breakFor = new System.Collections.Generic.Dictionary<string, string[]> {
    { "Stage 1 - The Start", new[] { "pumpkin_Start", "Pumpkin_0" } },
    { "Mini Stuffs", new[] { "Pumpkin_1" } }, { "Mini Pumpkins", new[] { "Pumpkin_1" } }, { "Mini Stuffs Chaos", new[] { "Pumpkin_1" } }, { "BR_Pumpkins", new[] { "Pumpkin_2" } },
    { "Pumpkin_5", new[] { "Pumpkin_5" } },
    { "Pumpkin_7", new[] { "Pumpkin_7" } },
    { "BR_Chaos", new[] { "Pumpkin_2" } }, { "BR_Stuffs", new[] { "Pumpkin_2" } },
    { "3 - LivingRoom", new[] { "Pumpkin_3" } },
    { "LR - Chaos", new[] { "Pumpkin_4" } }, { "LR - Stuffs", new[] { "Pumpkin_4" } },
    { "Jenga Tower", new[] { "Pumpkin_6" } }, { "Jenga Tower 2", new[] { "Pumpkin_6" } },
    { "Whack 6", new[] { "-Wood_Barrels_Whack_Pumpkin_6-" } },
    { "Whack 7", new[] { "-Wood_Barrels_Whack_Pumpkin_7-" } },
};
// Skull choice at each magic table, so both kinds get played: Chaos, Stuffs, Chaos.
var skullFor = new System.Collections.Generic.Dictionary<string, string> { { "BR_Magic_Table", "Skull_Chaos" }, { "Magic_Table", "Skull_Stuffs" }, { "Pumpkin_7", "Skull_Chaos" }, { "MiniBR_MagicTable", "Skull_Stuffs" } };
var hatFor = new System.Collections.Generic.Dictionary<string, string> { { "BR_Magic_Table", "Hat_Green" }, { "Magic_Table", "Hat_Black" }, { "Pumpkin_7", "Hat_Blue" }, { "MiniBR_MagicTable", "Hat_Green" } };
bool hatPulled = false; bool skullSwapped = false;
// Each pumpkin is broken once (a whack pumpkin's Health resets itself after dying).
var broken = new System.Collections.Generic.HashSet<int>();
var noAutoKill =new[] { "Door", "pumpkin_Start", "Pumpkin_0", "Pumpkin_1", "Pumpkin_2", "Pumpkin_3", "Pumpkin_4", "Pumpkin_5", "Pumpkin_6", "Pumpkin_7", "Pumpkin_On_Tree", "Pumpkin_Mob", "Pumpkin_Boss", "pumpkin_boss_Z", "Paint_Giant_Slime" };

string last = null; float since = 0f; float nextTick = 0f; bool stallLogged = false; int loops = 0; float startReal = Time.realtimeSinceStartup;
var visited = new System.Collections.Generic.List<string>();
System.Func<string, string> Base = n => n.Replace("(Clone)", "").Trim();
System.Action<Vector3> MovePlayerTo = target =>
{
    var cc = origin.GetComponent<CharacterController>(); if (cc) cc.enabled = false;
    var offset = target - (body != null ? body.bounds.center : origin.transform.position);
    origin.transform.position += offset;
    Physics.SyncTransforms();
    if (cc) cc.enabled = true;
};
// Touching something counts only on entering it: step 1.5 m back first, then walk in on the next tick.
var stepped = new System.Collections.Generic.Dictionary<int, bool>();
System.Action<GameObject> Touch = thing =>
{
    int id = thing.GetInstanceID();
    bool back; stepped.TryGetValue(id, out back);
    if (!back) MovePlayerTo(thing.transform.position + new Vector3(1.5f, 0f, 0f));
    else MovePlayerTo(thing.transform.position);
    stepped[id] = !back;
};
System.Func<string, GameObject> Global =n => { var v = PlayMakerGlobals.Instance.Variables.FindFsmGameObject(n); return v != null ? v.Value : null; };
System.Action<string> Diagnose = why =>
{
    var sb = new System.Text.StringBuilder($"STALL ({why}) in '{last}' for {Time.time - since:F0}s. ");
    var trues = new System.Collections.Generic.List<string>(); foreach (var b in gs.FsmVariables.BoolVariables) if (b.Value) trues.Add(b.Name);
    sb.Append("true flags: " + string.Join(",", trues) + ". enemies: ");
    foreach (var hd in UnityEngine.Object.FindObjectsByType<Game.Combat.EnemyHitDamage>(FindObjectsSortMode.None)) sb.Append(Base(hd.name) + (hd.IsDead ? "(dead) " : " "));
    sb.Append("| EMs: ");
    foreach (var f in Resources.FindObjectsOfTypeAll<PlayMakerFSM>()) if (f.gameObject.scene.IsValid() && f.gameObject.name.StartsWith("-EnemyManager") && f.FsmName == "EnemyManager" && f.gameObject.activeInHierarchy) sb.Append(f.gameObject.name + ":" + f.ActiveStateName + " ");
    sb.Append("| timeScale=" + Time.timeScale);
    Log(sb.ToString());
};

// Every Console warning/error/exception: the first of each (with its stack's first lines), then counts at the end.
var consolePath = "K:/WORK_2026/08_04/_claude/rooms_2026-11-20/bot_console.txt";
System.IO.File.AppendAllText(consolePath, "==== bot (re)started " + System.DateTime.Now.ToString("HH:mm:ss") + " ====\n");
var counts = new System.Collections.Generic.Dictionary<string, int>();
UnityEngine.Application.LogCallback onLog = (msg, stack, type) =>
{
    if (type == LogType.Log) return;
    var key = type + ": " + (msg.Length > 160 ? msg.Substring(0, 160) : msg);
    int n; counts.TryGetValue(key, out n); counts[key] = n + 1;
    if (n == 0)
    {
        var lines = stack.Split('\n'); var top = new System.Collections.Generic.List<string>();
        for (int i = 0; i < lines.Length && top.Count < 4; i++) if (lines[i].Trim().Length > 0) top.Add("    " + lines[i].Trim());
        System.IO.File.AppendAllText(consolePath, "[" + Time.time.ToString("F1") + " " + (gs != null ? gs.ActiveStateName : "?") + "] " + key + "\n" + string.Join("\n", top.ToArray()) + "\n");
    }
};
Application.logMessageReceived += onLog;
System.Action WriteCounts = () =>
{
    var sbc = new System.Text.StringBuilder("\n== counts ==\n");
    foreach (var kv in counts) sbc.AppendLine(kv.Value + "x " + kv.Key);
    System.IO.File.AppendAllText(consolePath, sbc.ToString());
};
// A new game in its own save folder (the user's three slots are never read or written).
Game.Saving.SaveManager.Profile = "_claude_bot";
var menu = UnityEngine.Object.FindAnyObjectByType<Game.Menus.SaveSlotMenu>();
if (menu != null && Game.Saving.SaveManager.CurrentSlot <= 0) { menu.StartSlot(0); Log("started slot 1 in Saves/_claude_bot from the start menu"); }
else Log("no save slot menu (already started?)");

// Off switch: the bot stops once this marker is destroyed (destroy "__BOT__" to stop it).
var marker = new GameObject("__BOT__");

// ---- 2026-11-10 watchers ----
var SF = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
var bbStateF = typeof(Game.Enemies.BoxBomb).GetField("state", flags);
var bbStates = new System.Collections.Generic.Dictionary<int, string>();
var bbCount = new System.Collections.Generic.Dictionary<string, int>();
var tutTitleF = typeof(Game.Menus.TutorialNotice).GetField("title", flags);
var holdsF = typeof(Game.Menus.PauseMenu).GetField("holds", SF);
float pausedSinceReal = -1f; bool pauseLogged = false;
bool lastPoison = false; int poisonStarts = 0;
string lastPhState = null; bool lastSpecial = false; int lastLevel = -1;
float lastRestockLeft = -1f; int restocks = 0;
float lastHealthSeen = 99999f; int bossPunchSet = 0;
float lowestY = 999f; float healthBeforeGod = 99999f; int healthLosses = 0;
// Run 2 option (SessionState BOT_SPARE_FURNITURE, default true here): paper boxes and other furniture are left alone (so box
// bombs get to wake and attack), and bugs are killed only after they have been around 8 s (so bees get to sting).
bool spareFurniture = UnityEditor.SessionState.GetBool("BOT_SPARE_FURNITURE", true);
float killAfter = UnityEditor.SessionState.GetFloat("BOT_KILL_AFTER", 8f);
var firstSeen = new System.Collections.Generic.Dictionary<int, float>();
System.Action<string> CountBB = k => { int c; bbCount.TryGetValue(k, out c); bbCount[k] = c + 1; };
System.Func<string> BBSummary = () => { var sb2 = new System.Text.StringBuilder(); foreach (var kv in bbCount) sb2.Append(kv.Key + "=" + kv.Value + " "); return sb2.ToString(); };
System.Action Watch = () =>
{
    // Tutorial notices: a player reads and closes them (they hold the game at timeScale 0).
    var tn = Game.Menus.TutorialNotice.Find();
    if (tn != null && tn.IsShowing && Time.unscaledTime - (float)typeof(Game.Menus.TutorialNotice).GetField("shownAt", flags).GetValue(tn) > 2.5f)
    {
        var tt = tutTitleF != null ? tutTitleF.GetValue(tn) as TMPro.TMP_Text : null;
        Log("closed a tutorial notice '" + (tt != null ? tt.text : "?") + "'");
        tn.Close();
    }
    // A pause that lasts (real time): who holds it.
    if (Time.timeScale == 0f)
    {
        if (pausedSinceReal < 0f) { pausedSinceReal = Time.realtimeSinceStartup; pauseLogged = false; }
        if (!pauseLogged && Time.realtimeSinceStartup - pausedSinceReal > 20f)
        {
            pauseLogged = true;
            var hs = "";
            var holds = holdsF != null ? holdsF.GetValue(null) as System.Collections.IEnumerable : null;
            if (holds != null) foreach (var h in holds) hs += (h == null ? "null" : h.GetType().Name + (h is UnityEngine.Object ? "(" + ((UnityEngine.Object)h).name + ")" : "")) + "; ";
            Log("PAUSE STALL: timeScale 0 for 20 s real. paused " + Game.Menus.PauseMenu.IsPaused + " held " + Game.Menus.PauseMenu.IsHeld + " holds: " + hs);
        }
    }
    else pausedSinceReal = -1f;
    // Box bombs: every state change.
    foreach (var bb in UnityEngine.Object.FindObjectsByType<Game.Enemies.BoxBomb>(FindObjectsSortMode.None))
    {
        string s = bbStateF.GetValue(bb).ToString();
        string old; int id = bb.GetInstanceID();
        if (!bbStates.TryGetValue(id, out old)) { bbStates[id] = s; continue; }
        if (old != s)
        {
            bbStates[id] = s; CountBB(old + ">" + s);
            if (s == "Shaking" || s == "Done" || old == "Flying")
                Log("box bomb " + Base(bb.name) + " " + old + " -> " + s + " (" + (Camera.main != null ? Vector3.Distance(bb.transform.position, Camera.main.transform.position).ToString("F1") : "?") + " m from head, armed " + Game.Enemies.BoxBomb.ArmedCount + ")");
        }
    }
    // Bee poison.
    bool pz = Game.Combat.PlayerPoison.Active;
    if (pz != lastPoison) { if (pz) poisonStarts++; Log("bee poison " + (pz ? "ON (" + Game.Combat.PlayerPoison.SecondsLeft.ToString("F1") + " s)" : "off")); lastPoison = pz; }
    // Player health FSM state (god mode: it should never reach a death state).
    var phs = playerHealth.ActiveStateName;
    if (phs != lastPhState) { if (lastPhState != null) Log("PlayerHealth FSM: " + lastPhState + " -> " + phs); lastPhState = phs; }
    if (healthBeforeGod < 99998.5f) { healthLosses++; Log("health -" + (99999f - healthBeforeGod).ToString("F1") + " (PlayerHealth state " + phs + ", poison " + Game.Combat.PlayerPoison.Active + ")"); }
    // Special stage stats (level 0 inside a special stage).
    bool sp = Game.Leveling.PlayerLevel.InSpecialStage;
    if (sp != lastSpecial) { Log("special stage stats " + (sp ? "ON (stats at level 0)" : "OFF (stats back)") + ", level " + (Game.Leveling.PlayerLevel.Instance != null ? Game.Leveling.PlayerLevel.Instance.Level : -1)); lastSpecial = sp; }
    var pl = Game.Leveling.PlayerLevel.Instance;
    if (pl != null && pl.Level != lastLevel) { if (lastLevel >= 0) Log("level " + lastLevel + " -> " + pl.Level + " (exp " + pl.Exp + ")"); lastLevel = pl.Level; }
    // Shop restocks.
    var shop = Game.Shopping.Shop.Main;
    if (shop != null)
    {
        float left = shop.SecondsToRestock;
        if (lastRestockLeft >= 0f && left > lastRestockLeft + 5f)
        {
            restocks++;
            var cs = ""; foreach (var i in shop.CandiesOnSale) cs += shop.Catalog.Candies[i].item.Id + " ";
            Log("shop restocked #" + restocks + " candies: " + cs);
        }
        lastRestockLeft = left;
    }
    // Falling out of the world.
    float y = origin.transform.position.y;
    if (y < lowestY) lowestY = y;
    if (y < -20f) Log("XR Origin BELOW THE WORLD at " + origin.transform.position);
};
System.Action Summary = () => Log("SUMMARY: spareFurniture " + spareFurniture + " killAfter " + killAfter + " | box bomb changes: " + BBSummary() + "| health losses " + healthLosses + " | poison starts " + poisonStarts + " | shop restocks " + restocks + " | lowest origin y " + lowestY.ToString("F2") + " | level " + (Game.Leveling.PlayerLevel.Instance != null ? Game.Leveling.PlayerLevel.Instance.Level : -1) + " | visited " + string.Join(" > ", visited.ToArray()));
// ---- 2026-11-20 room photos: each room's 4 walls + a plan, once its furniture is in (the bot holds still meanwhile) ----
string VARIANT = UnityEditor.SessionState.GetString("RT_VARIANT", "stuffs");
string wantSkull = VARIANT == "chaos" ? "Skull_Chaos" : VARIANT == "pumpkin" ? "Skull_Pumpkin" : "Skull_Stuffs";
var photoDir = @"K:\WORK_2026\08_04\_claude\rooms_2026-11-20\walls\";
System.IO.Directory.CreateDirectory(photoDir);
// furnish state -> room, kit set, half size of the room's square (m), variant label
var photoStates = new System.Collections.Generic.Dictionary<string, string[]> {
    { "Mini Stuffs", new[] { "1_storage", "Mini", "3.75", "stuffs" } }, { "Mini Stuffs Chaos", new[] { "1_storage", "Mini", "3.75", "chaos" } }, { "Mini Pumpkins", new[] { "1_storage", "Mini", "3.75", "pumpkin" } },
    { "BR_Stuffs", new[] { "2_bedroom", "Big", "3.75", "stuffs" } }, { "BR_Chaos", new[] { "2_bedroom", "Big", "3.75", "chaos" } }, { "BR_Pumpkins", new[] { "2_bedroom", "Big", "3.75", "pumpkin" } },
    { "LR - Stuffs", new[] { "3_living", "Living", "4.5", "stuffs" } }, { "LR - Chaos", new[] { "3_living", "Living", "4.5", "chaos" } },
    { "Jenga Tower", new[] { "4_playroom", "Play", "5.625", "stuffs" } }, { "Jenga Tower 2", new[] { "4_playroom", "Play", "5.625", "chaos" } },
};
string lastPhotoRoom = VARIANT == "pumpkin" ? "2_bedroom" : "4_playroom";
string[] photoJob = null; float photoAt = -1f; bool photosDone = false;
var takenRooms = new System.Collections.Generic.HashSet<string>();
System.Func<string, Transform> FindSet = setName =>
{
    Transform best = null;
    foreach (var kit in UnityEngine.Object.FindObjectsByType<Game.Rooms.RoomKit>(FindObjectsSortMode.None))
        foreach (var s in (System.Array)typeof(Game.Rooms.RoomKit).GetField("sets", flags).GetValue(kit))
        {
            var root = (Transform)s.GetType().GetField("root").GetValue(s);
            if ((string)s.GetType().GetField("name").GetValue(s) != setName || root == null) continue;
            float big = 0f; foreach (Transform ch in root) big = Mathf.Max(big, ch.lossyScale.y);
            if (big > 0.5f) best = root;
        }
    return best;
};
System.Action<Camera, int, int, string> Shot = (cam, w, h, file) =>
{
    var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32); rt.antiAliasing = 4;
    cam.targetTexture = rt; cam.Render();
    var prev = RenderTexture.active; RenderTexture.active = rt;
    var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
    tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
    RenderTexture.active = prev; cam.targetTexture = null;
    System.IO.File.WriteAllBytes(photoDir + file + ".png", tex.EncodeToPNG());
    UnityEngine.Object.Destroy(tex); rt.Release(); UnityEngine.Object.Destroy(rt);
};
System.Action<string[]> TakeRoom = job =>
{
    var set = FindSet(job[1]);
    if (set == null) { Log("PHOTO: no visible set " + job[1]); return; }
    float half = float.Parse(job[2], System.Globalization.CultureInfo.InvariantCulture) * (job[1] == "Play" ? 1f : 1f);
    Vector3 c = set.position; float floorY = c.y;
    float roofMin = 99f;
    foreach (var r in set.GetComponentsInChildren<Renderer>(false))
    {
        bool isRoof = false; var p = r.transform;
        while (p != null && p != set) { if (p.name == "Roof" || p.name.Contains("Roof_Sub")) isRoof = true; p = p.parent; }
        if (isRoof && r.enabled) roofMin = Mathf.Min(roofMin, r.bounds.min.y);
    }
    float ceiling = roofMin < 90f ? roofMin - floorY : 3f;
    // the lamps at full (no flicker dip in a picture); the player's own body out of sight
    foreach (var mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        if (mb.GetType().Name == "LampFlicker")
        {
            var lamp = mb.GetType().GetField("lamp", flags).GetValue(mb) as Light;
            if (lamp != null) lamp.intensity = (float)mb.GetType().GetField("baseIntensity", flags).GetValue(mb);
        }
    var hidden = new System.Collections.Generic.List<Renderer>();
    foreach (var r in origin.GetComponentsInChildren<Renderer>(false)) if (r.enabled) { r.enabled = false; hidden.Add(r); }
    var camGo = new GameObject("__ROOM_CAM__");
    var cam = camGo.AddComponent<Camera>();
    if (Camera.main != null) cam.CopyFrom(Camera.main);
    camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;
    cam.enabled = false; cam.orthographic = false; cam.fieldOfView = 46f; cam.nearClipPlane = 0.3f; cam.farClipPlane = 80f;
    string tag = job[0] + "_" + job[3];
    var dirs = new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
    var names = new[] { "N", "E", "S", "W" };
    float eyeH = Mathf.Min(2.45f, ceiling - 0.45f);
    for (int i = 0; i < 4; i++)
    {
        camGo.transform.position = new Vector3(c.x, floorY + eyeH, c.z) - dirs[i] * half * 0.6f;
        camGo.transform.LookAt(new Vector3(c.x, floorY + 1.25f, c.z) + dirs[i] * half);
        Shot(cam, 1280, 720, tag + "_" + i + names[i]);
    }
    // plan: straight down from just under the roof, the lamp clipped away
    cam.orthographic = true;
    cam.orthographicSize = job[1] == "Play" ? 11.6f : half * 1.13f;
    camGo.transform.position = new Vector3(c.x, floorY + ceiling - 0.1f, c.z);
    camGo.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
    cam.nearClipPlane = 0.7f;
    Shot(cam, 1100, 1100, tag + "_plan");
    UnityEngine.Object.Destroy(camGo);
    foreach (var r in hidden) if (r != null) r.enabled = true;
    Log("PHOTOS " + tag + ": 4 walls + plan (centre " + c.ToString("F2") + ", half " + half + ", ceiling " + ceiling.ToString("F2") + ")");
};

System.Action tick = null;
tick = () =>
{
    if (!UnityEditor.EditorApplication.isPlaying || gs == null || marker == null) { UnityEditor.EditorApplication.update -= new UnityEditor.EditorApplication.CallbackFunction(tick); Application.logMessageReceived -= onLog; WriteCounts(); Summary(); Log("bot stopped"); return; }
    if (Time.realtimeSinceStartup < nextTick) return;
    nextTick = Time.realtimeSinceStartup + 0.4f;
    try
    {
        // God mode, and no notice left waiting for a trigger.
        healthBeforeGod = playerHealth.FsmVariables.GetFsmFloat("PlayerHealth").Value;
        playerHealth.FsmVariables.GetFsmFloat("PlayerHealth").Value = 99999f;
        if (playerDamage.enabled) playerDamage.enabled = false;
        var notice = UnityEngine.Object.FindFirstObjectByType<Game.Leveling.LevelUpNotice>();
        var nt = typeof(Game.Leveling.LevelUpNotice);
        if (notice != null && (bool)nt.GetField("showing", flags).GetValue(notice) && !(bool)nt.GetField("closing", flags).GetValue(notice)
            && Time.unscaledTime - (float)nt.GetField("shownAt", flags).GetValue(notice) > 2.5f) { notice.Close(); Log("closed a level-up notice"); }

        string state = gs.ActiveStateName;
        if (state != last)
        {
            Log($"-> {state}   (after {Time.time - since:F1}s in '{last}')");
            if (state == "Stage 1 - The Start" && visited.Contains("Thank For Playing")) { loops++; if (loops < 2) { Log("LOOP " + loops + " done: real minutes=" + ((Time.realtimeSinceStartup - startReal) / 60f).ToString("F1")); visited.Remove("Thank For Playing"); } }
            visited.Add(state); last = state; since = Time.time; stallLogged = false; hatPulled = false; skullSwapped = false;
            string[] pj;
            if (photoStates.TryGetValue(state, out pj) && !takenRooms.Contains(pj[0])) { photoJob = pj; photoAt = Time.realtimeSinceStartup + 5f; Log("photos of " + pj[0] + " " + pj[3] + " in 5 s"); }
            if (loops >= 2) { Log($"DONE: two full runs, back at the start. real minutes={(Time.realtimeSinceStartup - startReal) / 60f:F1}"); UnityEditor.EditorApplication.update -= new UnityEditor.EditorApplication.CallbackFunction(tick); Application.logMessageReceived -= onLog; WriteCounts(); Summary(); return; }
        }
        float inState = Time.time - since;
        if (!stallLogged && inState > 150f) { stallLogged = true; Diagnose("150 s"); }
        try { Watch(); } catch (System.Exception we) { Log("WATCH ERROR " + we.GetType().Name + ": " + we.Message); }
        if (Time.timeScale == 0f) return;
        if (photoJob != null)
        {
            if (Time.realtimeSinceStartup < photoAt) return;
            try { TakeRoom(photoJob); } catch (System.Exception pe) { Log("PHOTO ERROR " + pe); }
            takenRooms.Add(photoJob[0]);
            if (photoJob[0] == lastPhotoRoom) { Log("PHOTOS DONE " + VARIANT); photoJob = null; UnityEngine.Object.Destroy(marker); return; }
            photoJob = null;
        }
        // The lid punch (a player punches the lid; the bot sets BOSSPUNCH like boss_punch.cs).
        if (state == "Wait For the Punch" && inState > 15f && bossPunchSet == 0)
        {
            var bp = gs.FsmVariables.FindFsmBool("BOSSPUNCH");
            if (bp == null) bp = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmBool("BOSSPUNCH");
            if (bp != null) { bp.Value = true; bossPunchSet = 1; Log("BOSSPUNCH set true (the lid punch)"); }
        }
        if (state != "Wait For the Punch") bossPunchSet = 0;

        // Touch what the stage waits for the player to walk into.
        if ((state == "Move to Fairy" || state == "wait") && inState > 2f) { var fairy = Global("Fairy"); if (fairy) Touch(fairy); }
        // Magic table: pull a hat off the coffer (the skulls come out of it), then touch a skull.
        string skullName;
        if (skullFor.TryGetValue(state, out skullName) && inState > 9f)
        {
            string want = (state == "Magic_Table" || state == "Pumpkin_7") && wantSkull == "Skull_Pumpkin" ? "Skull_Stuffs" : wantSkull;
            var skull = Global(want);
            GameObject wrong = null;
            foreach (var sn in new[] { "Skull_Chaos", "Skull_Stuffs", "Skull_Pumpkin" }) { var g = Global(sn); if (sn != want && g && g.activeInHierarchy) wrong = g; }
            if (skull && skull.activeInHierarchy && skullSwapped) Touch(skull);
            else if (skull && skull.activeInHierarchy && wrong == null) Touch(skull);
            else if (wrong != null && !skullSwapped)
            {
                var pf = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Items/Breakable/" + want + ".prefab");
                var at = wrong.transform.position; var par = wrong.transform.parent;
                UnityEngine.Object.Destroy(wrong);
                UnityEngine.Object.Instantiate(pf, at, Quaternion.identity, par);
                skullSwapped = true; Log("swapped " + Base(wrong.name) + " for " + want + " (variant " + VARIANT + ")");
            }
            else if (!hatPulled)
            {
                var hat = Global(hatFor[state]);
                if (hat == null) foreach (var h in new[] { "Hat_Green", "Hat_Black", "Hat_Blue" }) if (hat == null) hat = Global(h);
                if (hat != null) { hat.transform.position += Vector3.up * 1.5f; Physics.SyncTransforms(); hatPulled = true; Log("pulled " + Base(hat.name)); }
            }
        }
        // Break the stage's pumpkin (after a few seconds, like a player getting to it).
        string[] targets;
        if (breakFor.TryGetValue(state, out targets) && inState > 4f)
            foreach (var go in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (System.Array.IndexOf(targets, Base(go.name)) >= 0 || System.Array.IndexOf(targets, Base(go.name).Replace("_Chaos", "")) >= 0)
                    foreach (var f in go.GetComponents<PlayMakerFSM>())
                    {
                        var h = f.FsmVariables.FindFsmFloat("health");
                        if (f.FsmName.EndsWith("Health") && h != null && h.Value > 0f && broken.Add(f.GetInstanceID())) { h.Value = 0f; Log("broke " + Base(go.name) + " (" + f.FsmName + (f.enabled ? "" : ", FSM off") + ")"); }
                    }
        // The final boss.
        if (state == "BOSS" && inState > 8f)
            foreach (var hd in UnityEngine.Object.FindObjectsByType<Game.Combat.EnemyHitDamage>(FindObjectsSortMode.None))
                if (Base(hd.name).StartsWith("pumpkin_boss_Z") && !hd.IsDead) { Game.Enemies.EnemyWave.Kill(hd.gameObject); Log("killed " + Base(hd.name)); }
        // Bugs: a strong player kills what comes (not pumpkins, giants or doors).
        if (inState > 3f)
            foreach (var hd in UnityEngine.Object.FindObjectsByType<Game.Combat.EnemyHitDamage>(FindObjectsSortMode.None))
            {
                if (hd.IsDead) continue;
                string n = Base(hd.name); bool skip = false;
                foreach (var ex in noAutoKill) if (n.StartsWith(ex) || n.Contains("Door")) skip = true;
                if (spareFurniture && hd.IsFurniture) skip = true;
                float seenAt; int hid = hd.GetInstanceID();
                if (!firstSeen.TryGetValue(hid, out seenAt)) { firstSeen[hid] = Time.time; seenAt = Time.time; }
                if (Time.time - seenAt < killAfter) skip = true;
                if (!skip) Game.Enemies.EnemyWave.Kill(hd.gameObject);
            }
        // The Paint Bees round: stand on its ring until it starts.
        foreach (var pb in UnityEngine.Object.FindObjectsByType<Game.Bonus.PaintBeesRound>(FindObjectsSortMode.None))
        {
            if (typeof(Game.Bonus.PaintBeesRound).GetField("hands", flags).GetValue(pb) != null) continue;
            var cam = Camera.main.transform;
            var flat = new Vector3(pb.transform.position.x - cam.position.x, 0f, pb.transform.position.z - cam.position.z);
            if (flat.magnitude > 0.3f) { origin.transform.position += flat; Physics.SyncTransforms(); Log("stepped onto the Paint Bees ring"); }
        }
        // Ghost balloons: swatted.
        foreach (var gb in UnityEngine.Object.FindObjectsByType<Game.Enemies.GhostBalloon>(FindObjectsSortMode.None))
        {
            var pop = typeof(Game.Enemies.GhostBalloon).GetMethod("Pop", flags);
            var reason = System.Enum.ToObject(pop.GetParameters()[0].ParameterType, 1);
            pop.Invoke(gb, new[] { reason }); Log("popped " + Base(gb.name));
        }
        // Chests: opened. Seeds: taken.
        foreach (var f in UnityEngine.Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
        {
            if (f.FsmName == "Chest_Control" && f.ActiveStateName == "Play_Smoke_n_Wait") { f.SendEvent("FINISHED"); Log("opened " + Base(f.gameObject.name)); }
            else if (f.FsmName == "Rotate and Wait" && f.ActiveStateName == "Rotate and Wait" && body != null)
            {
                // The seed bobs on a tween (moving it gets undone), so the player walks into it.
                Touch(f.gameObject);
                Log("walked into " + Base(f.gameObject.name));
            }
        }
    }
    catch (System.Exception e) { Log("BOT ERROR " + e.GetType().Name + ": " + e.Message); }
};
UnityEditor.EditorApplication.update += new UnityEditor.EditorApplication.CallbackFunction(tick);
return "bot running from " + gs.ActiveStateName;
