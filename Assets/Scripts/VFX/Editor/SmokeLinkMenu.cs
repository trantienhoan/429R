using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>
    /// Smoke cleanup (user, 2026-10-28): the game's smoke puffs were pasted into about 290 prefabs and the scene as loose
    /// copies (364, each tuned by hand), so changing the smoke meant changing every one. This links each copy to one of
    /// a few shared smoke prefabs in Assets/Prefabs/Effects/Smoke, in place: the same object, name and place, so the FSMs
    /// that switch it on and the scripts that find it keep working, and its own tuned numbers and colour stay as
    /// overrides (it looks the same). The look itself (material, render mode, mesh) then comes from the shared prefab:
    /// change that one and every copy follows. Every copy is checked before and after (its smoke settings, and every
    /// reference to it); a prefab where anything changed isn't saved. Steps, with 2 Game Scene open:
    /// 1. Dry Run (changes nothing), 2. Build Shared Smoke Prefabs, 3. Link Smoke Copies (saves the scene if it had no
    /// other unsaved changes).
    /// </summary>
    public static class SmokeLinkMenu
    {
        private const string Folder = "Assets/Prefabs/Effects/Smoke";
        private static readonly string[] SmokeMaterials =
        {
            "Assets/Lana Studio/Hyper Casual FX/Materials/Circles_AB.mat",
            "Assets/Lana Studio/Hyper Casual FX/Materials/Circles_AB 1.mat",
        };

        // Not part of what a copy looks like or does (they change when it becomes a prefab instance).
        private static readonly HashSet<string> NotCompared = new()
        {
            "m_PrefabInstance", "m_CorrespondingSourceObject", "m_PrefabAsset", "m_GameObject", "m_ObjectHideFlags",
            "m_EditorHideFlags", "m_EditorClassIdentifier",
            // Curve key weights: only used by weighted tangents (Weighted Mode is still compared); linking resets unused ones.
            "inWeight", "outWeight",
        };

        private class Copy
        {
            public Transform root;
            public string where;
            public string kind;
        }

        [MenuItem("Tools/429 Game/Effects/Smoke/1. Dry Run (changes nothing)")]
        public static void DryRunMenu() => Debug.Log(DryRun());

        [MenuItem("Tools/429 Game/Effects/Smoke/2. Build Shared Smoke Prefabs")]
        public static void BuildMenu() => Debug.Log(BuildShared());

        [MenuItem("Tools/429 Game/Effects/Smoke/3. Link Smoke Copies")]
        public static void LinkMenu() => Debug.Log(Link(null));

        // ---------- finding copies ----------

        private static HashSet<Material> Materials()
        {
            var mats = new HashSet<Material>();
            foreach (var path in SmokeMaterials)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m != null) mats.Add(m);
            }
            return mats;
        }

        // Its own smoke copies (not the root itself, not ones from a nested prefab or its base: those are linked there):
        // the highest object named like smoke above a particle system on the smoke material.
        private static List<Copy> FindCopies(GameObject root, string where, HashSet<Material> mats)
        {
            var found = new List<Copy>();
            var seen = new HashSet<Transform>();
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var r = ps.GetComponent<ParticleSystemRenderer>();
                if (r == null || r.sharedMaterial == null || !mats.Contains(r.sharedMaterial)) continue;
                Transform copy = null;
                for (var t = ps.transform; t != null && t != root.transform.parent; t = t.parent)
                    if (t.name.ToLowerInvariant().Contains("smoke")) copy = t;
                if (copy == null || copy == root.transform || !seen.Add(copy)) continue;
                if (PrefabUtility.IsPartOfPrefabInstance(copy.gameObject)) continue;
                found.Add(new Copy { root = copy, where = where, kind = KindOf(copy) });
            }
            return found;
        }

        // Which shared smoke a copy is: by its systems (where they are, burst or loop, the flash's 4x4 sheet or the puffs'
        // 2x2), or null for a one-off that's left alone.
        private static string KindOf(Transform copy)
        {
            var systems = copy.GetComponentsInChildren<ParticleSystem>(true);
            var children = systems.Where(s => s.transform != copy).ToList();
            if (systems.Length == 0 || children.Any(s => s.transform.parent != copy)) return null;
            var own = copy.GetComponent<ParticleSystem>();
            if (own == null)
                return children.Count == 1 && children[0].name == "Smoke_Blast2" && IsPuff(children[0]) ? "Smoke_Puff_Holder" : null;

            var sheet = own.textureSheetAnimation;
            if (sheet.enabled && sheet.numTilesX == 4 && sheet.numTilesY == 4)
            {
                if (!children.All(IsPuff)) return null;
                var names = children.Select(s => s.name).OrderBy(n => n).ToList();
                if (names.SequenceEqual(new[] { "confetti_small" })) return "Smoke_Flash_Puff";
                if (names.SequenceEqual(new[] { "circle", "confetti_small" })) return "Smoke_Flash_Circle_Puff";
                if (names.SequenceEqual(new[] { "smoke_small" })) return "Smoke_Flash_Small_Puff";
                if (names.Count == 5 && names.All(n => n.StartsWith("confetti_small"))) return "Smoke_Flash_Five_Puffs";
                return null;
            }
            if (children.Count > 0 || !IsPuff(own)) return null;
            var r = own.GetComponent<ParticleSystemRenderer>();
            if (own.main.loop) return r.sharedMaterial != null && r.sharedMaterial.name == "Circles_AB 1" ? "Smoke_Spray_Loop" : "Smoke_Puff_Loop";
            return own.velocityOverLifetime.enabled ? "Smoke_Puff_Burst_Out" : "Smoke_Puff";
        }

        // A puff: the cloud sheet (2x2), or the flash's ring (circle, 4x2).
        private static bool IsPuff(ParticleSystem ps)
        {
            var sheet = ps.textureSheetAnimation;
            if (!sheet.enabled) return false;
            return (sheet.numTilesX == 2 && sheet.numTilesY == 2) || (ps.name == "circle" && sheet.numTilesX == 4 && sheet.numTilesY == 2);
        }

        private static List<string> PrefabPaths()
        {
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => !p.StartsWith(Folder + "/")).ToList();
            int Depth(string path)
            {
                int depth = 0;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                while (go != null && PrefabUtility.GetPrefabAssetType(go) == PrefabAssetType.Variant && depth < 10)
                {
                    go = PrefabUtility.GetCorrespondingObjectFromSource(go);
                    depth++;
                }
                return depth;
            }
            var depths = paths.ToDictionary(p => p, Depth);
            return paths.OrderBy(p => depths[p]).ThenBy(p => p).ToList();
        }

        // ---------- 1. dry run ----------

        public static string DryRun()
        {
            var mats = Materials();
            var all = new List<Copy>();
            foreach (var path in PrefabPaths())
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null) all.AddRange(FindCopies(go, path, mats));
            }
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) all.AddRange(FindCopies(root, "(scene) " + root.name, mats));
            var sb = new StringBuilder($"[Smoke] {all.Count} smoke copies in {all.Select(c => c.where).Distinct().Count()} files:\n");
            foreach (var g in all.GroupBy(c => c.kind ?? "(one-off, left alone)").OrderByDescending(g => g.Count()))
                sb.AppendLine($"  {g.Key}: {g.Count()}" + (g.Key.StartsWith("(") ? " - " + string.Join(", ", g.Select(c => c.where + "/" + c.root.name)) : ""));
            return sb.ToString();
        }

        // ---------- 2. shared smoke prefabs ----------

        /// <summary>
        /// Makes each shared smoke prefab (once) from its most common copy, at the origin, size 1, switched on.
        /// </summary>
        public static string BuildShared()
        {
            if (!Ready(out var why)) return why;
            var mats = Materials();
            // Kind -> (setup fingerprint -> how many, first path/copy path).
            var counts = new Dictionary<string, Dictionary<string, int>>();
            var firstOf = new Dictionary<string, (string path, string copyPath)>();
            foreach (var path in PrefabPaths())
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                foreach (var c in FindCopies(go, path, mats))
                {
                    if (c.kind == null) continue;
                    string print = c.kind + "\n" + Fingerprint(c.root, false);
                    if (!counts.TryGetValue(c.kind, out var setups)) counts[c.kind] = setups = new Dictionary<string, int>();
                    setups.TryGetValue(print, out int n);
                    setups[print] = n + 1;
                    if (!firstOf.ContainsKey(print)) firstOf[print] = (path, PathIn(go.transform, c.root));
                }
            }

            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Effects")) AssetDatabase.CreateFolder("Assets/Prefabs", "Effects");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs/Effects", "Smoke");
            var log = new StringBuilder("[Smoke] Shared smoke prefabs:\n");
            foreach (var pair in counts.OrderByDescending(p => p.Value.Values.Sum()))
            {
                string kind = pair.Key, target = $"{Folder}/{kind}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(target) != null)
                {
                    log.AppendLine($"  {kind}: already there");
                    continue;
                }
                var most = pair.Value.OrderByDescending(p => p.Value).First();
                var (path, copyPath) = firstOf[most.Key];
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var copy = contents.transform.Find(copyPath);
                    var made = Object.Instantiate(copy.gameObject, contents.transform);
                    made.transform.SetParent(null, false);
                    made.name = kind;
                    made.SetActive(true);
                    made.transform.localPosition = Vector3.zero;
                    made.transform.localRotation = Quaternion.identity;
                    made.transform.localScale = Vector3.one;
                    PrefabUtility.SaveAsPrefabAsset(made, target);
                    log.AppendLine($"  {kind}: made from {path.Replace("Assets/Prefabs/", "")} / {copyPath} (the setup {most.Value} of its {pair.Value.Values.Sum()} copies share)");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        // ---------- 3. link the copies ----------

        /// <summary>
        /// Links every smoke copy to its shared smoke prefab, in place (Only: just these prefab paths, e.g. a first few to
        /// try; null = every prefab, then the open scene). A prefab where any check fails isn't saved.
        /// </summary>
        public static string Link(IEnumerable<string> only)
        {
            if (!Ready(out var why)) return why;
            var mats = Materials();
            var shared = new Dictionary<string, GameObject>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Folder }))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                shared[go.name] = go;
            }
            if (shared.Count == 0) return "[Smoke] No shared smoke prefabs yet: run 2. Build Shared Smoke Prefabs first.";

            var log = new StringBuilder();
            var problems = new List<string>();
            int linked = 0, files = 0, leftAlone = 0;
            var paths = only != null ? only.ToList() : PrefabPaths();
            foreach (var path in paths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || FindCopies(asset, path, mats).Count == 0) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int done = LinkIn(root, path, mats, shared, problems, ref leftAlone);
                    if (done < 0) continue;
                    if (done == 0) continue;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    linked += done;
                    files++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            // The open scene's own copies (only on a full run, and only if it had no other unsaved changes).
            string sceneNote = "";
            if (only == null)
            {
                var scene = SceneManager.GetActiveScene();
                if (scene.isDirty) sceneNote = "\nThe scene had unsaved changes, so its copies were left for now: save it and run this again.";
                else
                {
                    int inScene = 0, sceneFailed = 0;
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        int done = LinkIn(root, "(scene) " + root.name, mats, shared, problems, ref leftAlone);
                        if (done > 0) inScene += done;
                        if (done < 0) sceneFailed++;
                    }
                    if (sceneFailed > 0) sceneNote = $"\nThe scene had problems: NOT saved (close it without saving to undo).";
                    else if (inScene > 0)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                        sceneNote = $"\n{inScene} copies in the scene linked; scene saved.";
                    }
                }
            }
            AssetDatabase.SaveAssets();
            log.Insert(0, $"[Smoke] {linked} smoke copies in {files} prefabs now linked to the shared smoke prefabs (they look the same: " +
                          $"their own numbers and colours are kept). {leftAlone} one-offs left alone.{sceneNote}\n");
            if (problems.Count > 0) log.AppendLine("NOT SAVED (something changed, see why):\n  " + string.Join("\n  ", problems));
            return log.ToString();
        }

        // Links the copies under this root. Returns how many, 0 if none, -1 if a check failed (then nothing should be saved).
        private static int LinkIn(GameObject root, string where, HashSet<Material> mats, Dictionary<string, GameObject> shared,
            List<string> problems, ref int leftAlone)
        {
            var copies = FindCopies(root, where, mats);
            if (copies.Count == 0) return 0;
            var refsBefore = References(root, copies);
            int done = 0;
            var settings = new ConvertToPrefabInstanceSettings
            {
                objectMatchMode = ObjectMatchMode.ByHierarchy,
                recordPropertyOverridesOfMatches = true,
                componentsNotMatchedBecomesOverride = true,
                gameObjectsNotMatchedBecomesOverride = true,
                changeRootNameToAssetName = false,
                logInfo = false,
            };
            foreach (var c in copies)
            {
                if (c.kind == null || !shared.TryGetValue(c.kind, out var master))
                {
                    leftAlone++;
                    continue;
                }
                string before = Fingerprint(c.root, true);
                var had = Parts(c.root);
                PrefabUtility.ConvertToPrefabInstance(c.root.gameObject, master, settings, InteractionMode.AutomatedAction);
                if (c.root == null || !PrefabUtility.IsAnyPrefabInstanceRoot(c.root.gameObject))
                {
                    problems.Add($"{where}: a copy wasn't linked");
                    return -1;
                }
                // A part the shared smoke has but this copy didn't (most copies carry a small FSM, a few don't): removed
                // again, as a removed-component override on this copy only.
                foreach (var t in c.root.GetComponentsInChildren<Transform>(true))
                {
                    var counts = new Dictionary<System.Type, int>();
                    foreach (var component in t.GetComponents<Component>())
                    {
                        if (component == null || component is Transform) continue;
                        counts.TryGetValue(component.GetType(), out int n);
                        counts[component.GetType()] = n + 1;
                        if (!had.Contains(PathIn(c.root, t) + "|" + component.GetType().Name + "|" + n)) Object.DestroyImmediate(component, true);
                    }
                }
                string after = Fingerprint(c.root, true);
                if (after != before)
                {
                    problems.Add($"{where} / {c.root.name}: its smoke settings changed ({FirstDifference(before, after)})");
                    return -1;
                }
                done++;
            }
            var refsAfter = References(root, copies);
            foreach (var pair in refsBefore)
            {
                if (!refsAfter.TryGetValue(pair.Key, out var now) || now != pair.Value)
                {
                    problems.Add($"{where}: a reference changed ({pair.Key}: {pair.Value} -> {now ?? "missing"})");
                    return -1;
                }
            }
            return done;
        }

        // ---------- checks ----------

        // Its parts: "path|type|index within that type" for every component but the Transforms.
        private static HashSet<string> Parts(Transform copy)
        {
            var parts = new HashSet<string>();
            foreach (var t in copy.GetComponentsInChildren<Transform>(true))
            {
                var counts = new Dictionary<System.Type, int>();
                foreach (var component in t.GetComponents<Component>())
                {
                    if (component == null || component is Transform) continue;
                    counts.TryGetValue(component.GetType(), out int n);
                    counts[component.GetType()] = n + 1;
                    parts.Add(PathIn(copy, t) + "|" + component.GetType().Name + "|" + n);
                }
            }
            return parts;
        }

        // Everything about a copy that shows or matters: every component's settings on it and under it (Own Place: its
        // own position too), object references as names.
        private static string Fingerprint(Transform copy, bool ownPlace)
        {
            var sb = new StringBuilder();
            foreach (var t in copy.GetComponentsInChildren<Transform>(true))
            {
                sb.Append('<').Append(PathIn(copy, t)).Append(' ').Append(t.gameObject.activeSelf).Append(' ').Append(t.tag).Append('>');
                // By type (in order within a type): linking moves a copy's own extra components after the shared ones.
                foreach (var component in t.GetComponents<Component>().Where(c => c != null).OrderBy(c => c.GetType().Name))
                {
                    if (component is Transform && t == copy && !ownPlace) continue;
                    sb.Append(component.GetType().Name).Append('{');
                    var p = new SerializedObject(component).GetIterator();
                    bool enter = true;
                    while (p.Next(enter))
                    {
                        // Not what's in them either (an object reference's file and path ids, too: it's compared by name).
                        enter = !NotCompared.Contains(p.name) && p.propertyType != SerializedPropertyType.ObjectReference;
                        if (NotCompared.Contains(p.name)) continue;
                        string value = Value(p, copy);
                        if (value != null) sb.Append(p.propertyPath).Append('=').Append(value).Append(';');
                    }
                    sb.Append('}');
                }
            }
            return sb.ToString();
        }

        private static string Value(SerializedProperty p, Transform copy)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer: return p.longValue.ToString();
                case SerializedPropertyType.Boolean: return p.boolValue ? "1" : "0";
                case SerializedPropertyType.Float: return p.floatValue.ToString("R");
                case SerializedPropertyType.String: return p.stringValue;
                case SerializedPropertyType.Enum: return p.enumValueIndex.ToString();
                case SerializedPropertyType.Color: return p.colorValue.ToString("F4");
                case SerializedPropertyType.Vector2: return p.vector2Value.ToString("F4");
                case SerializedPropertyType.Vector3: return p.vector3Value.ToString("F4");
                case SerializedPropertyType.Vector4: return p.vector4Value.ToString("F4");
                case SerializedPropertyType.Quaternion: return p.quaternionValue.ToString("F4");
                case SerializedPropertyType.LayerMask: return p.intValue.ToString();
                case SerializedPropertyType.ArraySize: return p.intValue.ToString();
                case SerializedPropertyType.ObjectReference: return Named(p.objectReferenceValue, copy);
                default: return null;
            }
        }

        // An object as its name, and where it is if it's in the copy (or the asset it is).
        private static string Named(Object o, Transform copy)
        {
            if (o == null) return "null";
            var t = o is GameObject go ? go.transform : o is Component c ? c.transform : null;
            if (t != null && copy != null && (t == copy || t.IsChildOf(copy))) return o.GetType().Name + "@" + PathIn(copy, t);
            if (t != null) return o.GetType().Name + "@" + t.name;
            return o.GetType().Name + ":" + o.name;
        }

        // Every reference from the rest of the prefab (FSMs, scripts) to a copy or anything in it: "component/path" ->
        // "copy-relative target".
        private static Dictionary<string, string> References(GameObject root, List<Copy> copies)
        {
            var found = new Dictionary<string, string>();
            var copyRoots = copies.Select(c => c.root).ToList();
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component is Transform || component is ParticleSystem || component is ParticleSystemRenderer) continue;
                var p = new SerializedObject(component).GetIterator();
                bool enter = true;
                while (p.Next(enter))
                {
                    enter = !NotCompared.Contains(p.name) && p.propertyType != SerializedPropertyType.ObjectReference;
                    if (p.propertyType != SerializedPropertyType.ObjectReference || NotCompared.Contains(p.name)) continue;
                    var o = p.objectReferenceValue;
                    var t = o is GameObject go ? go.transform : o is Component c ? c.transform : null;
                    if (t == null) continue;
                    var copy = copyRoots.FirstOrDefault(r => r != null && (t == r || t.IsChildOf(r)));
                    if (copy == null) continue;
                    string key = PathIn(root.transform, component.transform) + "/" + component.GetType().Name + "#" +
                                 System.Array.IndexOf(component.GetComponents(component.GetType()), component) + "." + p.propertyPath;
                    found[key] = o.GetType().Name + "@" + PathIn(root.transform, t);
                }
            }
            return found;
        }

        private static string PathIn(Transform root, Transform t)
        {
            if (t == root) return ".";
            var parts = new List<string>();
            for (var x = t; x != null && x != root; x = x.parent) parts.Add(x.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static string FirstDifference(string a, string b)
        {
            int i = 0;
            while (i < a.Length && i < b.Length && a[i] == b[i]) i++;
            int from = System.Math.Max(0, i - 60);
            return "before: ..." + a.Substring(from, System.Math.Min(140, a.Length - from)) + " | after: ..." + b.Substring(from, System.Math.Min(140, b.Length - from));
        }

        private static bool Ready(out string why)
        {
            why = null;
            if (EditorApplication.isPlaying) why = "[Smoke] Stop play mode first.";
            else if (PrefabStageUtility.GetCurrentPrefabStage() != null) why = "[Smoke] Close the prefab you're editing (Prefab Mode) first.";
            return why == null;
        }
    }
}
