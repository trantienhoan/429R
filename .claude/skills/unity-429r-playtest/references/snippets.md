# Test snippets (C# method bodies for MCP `execute_code`; tested on the second PC, 2026-11-11 to 11-20)

Run them in order for a run start. Each returns a short string; "OK ..." means done, anything else means wait and
call again (once a second is plenty).

## 1. Right after Play starts: background running + the throwaway profile

```csharp
UnityEngine.Application.runInBackground = true;
Game.Saving.SaveManager.Profile = "_claude_test";
return "profile=" + Game.Saving.SaveManager.Profile + " folder=" + Game.Saving.SaveManager.SaveFolder;
```

## 2. Skip the opening cutscene

```csharp
var cut = UnityEngine.Object.FindAnyObjectByType<Game.Menus.OpeningCutscene>();
if (cut == null) return "OK no cutscene";
var t = typeof(Game.Menus.OpeningCutscene);
var fl = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var sk = t.GetField("skipping", fl);
if ((bool)sk.GetValue(cut)) return "skipping already";
sk.SetValue(cut, true);
cut.StopAllCoroutines();
cut.StartCoroutine((System.Collections.IEnumerator)t.GetMethod("SkipOut", fl).Invoke(cut, null));
return "skip started";
```
Wait for: `return Game.Menus.OpeningCutscene.IsPlaying ? "playing" : "OK opening over";`

## 3. Close the Health Warning (about 1.5 s after the opening), then wait for the slot menu

```csharp
var fl = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
int n = 0;
foreach (var hw in UnityEngine.Object.FindObjectsByType<Game.Menus.HealthWarning>(UnityEngine.FindObjectsSortMode.None)) {
  if (!hw.enabled) continue;
  typeof(Game.Menus.HealthWarning).GetField("shownAt", fl).SetValue(hw, -100f);
  typeof(Game.Menus.HealthWarning).GetField("closing", fl).SetValue(hw, true);
  n++;
}
return "closing " + n + ", showing=" + Game.Menus.HealthWarning.IsShowing;
```
Wait for:
```csharp
var menu = UnityEngine.Object.FindAnyObjectByType<Game.Menus.SaveSlotMenu>(UnityEngine.FindObjectsInactive.Include);
if (menu == null) return "no menu";
var st = typeof(Game.Menus.SaveSlotMenu).GetField("step", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(menu).ToString();
return st == "Open" ? "OK open" : st + " warning=" + Game.Menus.HealthWarning.IsShowing;
```

## 4. Start slot 3 on the throwaway profile, wait until in the game

```csharp
if (Game.Saving.SaveManager.Profile != "_claude_test") return "WRONG PROFILE " + Game.Saving.SaveManager.Profile;
var menu = UnityEngine.Object.FindAnyObjectByType<Game.Menus.SaveSlotMenu>(UnityEngine.FindObjectsInactive.Include);
menu.StartSlot(2);
return "started slot 3 in " + Game.Saving.SaveManager.SaveFolder;
```
Wait for:
```csharp
var menu = UnityEngine.Object.FindAnyObjectByType<Game.Menus.SaveSlotMenu>(UnityEngine.FindObjectsInactive.Include);
var st = typeof(Game.Menus.SaveSlotMenu).GetField("step", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(menu).ToString();
return (st == "Done" && Game.Saving.Checkpoints.HasResumed) ? "OK in game, profile " + Game.Saving.SaveManager.Profile : st;
```

## 5. Jump to a gate and close its notice

```csharp
if (Game.Saving.SaveManager.Profile != "_claude_test") return "WRONG PROFILE";
var gs = Game.GameStages.Fsm();
string from = gs.ActiveStateName;
gs.Fsm.SetState("Clean for Path 3");          // 1 = PlayGround, 2 = Slime Bowling, 3 = Ball Valley
return "jumped from " + from;
```
A few seconds later:
```csharp
var tn = Game.Menus.TutorialNotice.Find();
string r = "state " + Game.GameStages.ActiveState() + " | notice " + (tn != null && tn.IsShowing) + " | timeScale " + UnityEngine.Time.timeScale;
if (tn != null && tn.IsShowing) tn.Close();
return r;
```
Read a GAMESTAGES bool: `Game.GameStages.GetBool("The_Seed")`.

## 6. A picture of what the player sees

```csharp
var cam = Camera.main; var dir = @"C:\Users\ADMIN\Documents\429R_claude_backups\2026-11-22\shots\";
System.IO.Directory.CreateDirectory(dir);
var name = UnityEditor.SessionState.GetString("SHOT_NAME", "shot");
var rt = new RenderTexture(640, 400, 24); var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render();
RenderTexture.active = rt; var tex = new Texture2D(640, 400, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 640, 400), 0, 0); tex.Apply();
RenderTexture.active = null; cam.targetTexture = prev; System.IO.File.WriteAllBytes(dir + name + ".png", tex.EncodeToPNG());
float lum = 0f; foreach (var c in tex.GetPixels()) lum += c.grayscale; lum /= tex.width * tex.height;
UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(tex);
UnityEditor.SessionState.SetString("SHOT_NAME", name + "x");
return name + " mean brightness " + lum.ToString("F3");
```
Change `dir` to your own work folder. Set `SHOT_NAME` first to name the next picture.

## 7. A timed log without polling

```csharp
var key = "T_LOG"; System.AppDomain.CurrentDomain.SetData(key, "");
float start = UnityEngine.Time.realtimeSinceStartup, next = 0f;
UnityEditor.EditorApplication.CallbackFunction tick = null;
tick = () => {
  float t = UnityEngine.Time.realtimeSinceStartup - start;
  if (!UnityEditor.EditorApplication.isPlaying || t > 120f) { UnityEditor.EditorApplication.update -= tick; return; }
  if (t < next) return; next = t + 1f;
  var line = t.ToString("F0") + "s " + Game.GameStages.ActiveState();
  System.AppDomain.CurrentDomain.SetData(key, (string)System.AppDomain.CurrentDomain.GetData(key) + line + " || ");
};
UnityEditor.EditorApplication.update += tick;
return "logging";
```
Read it once at the end: `return (string)System.AppDomain.CurrentDomain.GetData("T_LOG");`

## 8. After the test

Stop Play (`manage_editor` stop), then delete the throwaway save folder from a shell:
`rm -rf "/c/Users/ADMIN/AppData/LocalLow/MeLikePie/Four Twenty Knight - 429/Saves/_claude_test"`
