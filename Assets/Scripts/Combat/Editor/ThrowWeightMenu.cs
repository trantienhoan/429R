using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Combat > Balance Throw Weight (user, 2026-10-29: "they all feel like having no weight when I
    /// throw them, with a small flick they all fly to border of the game ... make stuffs harder to throw"). Most
    /// throwables left the hand at 4-6.5x its speed with no air drag, so a flick crossed the room. Every grabbable that
    /// throws is put in a size class by its drawn size and gets:
    /// - Light (up to Light Size across): throw boost Light Boost; a flat one (books, paper, flip-flops) more air drag.
    /// - Medium (up to Medium Size): Medium Boost.
    /// - Heavy (bigger: chairs, stools, tables): Heavy Boost, and it trails the hand a little while held (heft).
    /// Throw Assist's Max Throw Speed caps every throw on top. The bowling ball keeps its own tuning. Prefabs, and the
    /// open scene's own grabbables (save the scene afterwards). A prefab copy that only inherits its values is left to its
    /// source prefab. Running it again changes nothing.
    /// </summary>
    public static class ThrowWeightMenu
    {
        private const float LightSize = 0.45f;
        private const float MediumSize = 0.9f;
        private const float LightBoost = 1.8f;
        private const float MediumBoost = 1.4f;
        private const float HeavyBoost = 1.1f;
        // Air drag (Rigidbody linear damping): a flat light thing slows like a book in the air; the rest a little.
        private const float FlatDrag = 0.25f;
        private const float LightDrag = 0.1f;
        private const float MediumDrag = 0.08f;
        private const float HeavyDrag = 0.05f;
        // Heavy things follow the hand a little behind (XRI smoothing: lower = more lag; 8 is XRI's own default).
        private const float HeavySmoothing = 6f;
        // A thin thing: its thinnest side under this share of its longest.
        private const float FlatShare = 0.3f;
        private static readonly string[] Skip = { "pumpkin_bowling" };
        // Their own throw boost and drag, whatever their size, by name start (user, 2026-10-29, from the headset): the
        // rooftop's throwing pumpkin and the flip-flops 3.5; the books and bottles lighter and easier still, 4.29.
        private static readonly (string name, float boost, float drag)[] Own =
        {
            ("Pumpkin_On_Tree", 3.5f, 0.05f),
            ("Flip_Flop", 3.5f, 0.08f),
            ("Bedroom_Book_", 4.29f, 0.1f),
            ("Empty_Bottle_", 4.29f, 0.05f),
        };

        /// <summary>The thing's own throw boost and drag, if it has them (see Own).</summary>
        public static bool OwnWeight(string thing, out float boost, out float drag)
        {
            foreach (var (name, b, d) in Own)
            {
                if (!thing.StartsWith(name)) continue;
                boost = b;
                drag = d;
                return true;
            }
            boost = drag = 0f;
            return false;
        }

        [MenuItem("Tools/429 Game/Combat/Balance Throw Weight")]
        public static void Menu() => Debug.Log(Run());

        public static string Run()
        {
            if (EditorApplication.isPlaying) return "[Throw Weight] Stop play mode first.";
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) return "[Throw Weight] Close Prefab Mode first.";
            var log = new StringBuilder();
            var counts = new Dictionary<string, int> { { "light", 0 }, { "flat", 0 }, { "medium", 0 }, { "heavy", 0 } };
            int files = 0;
            foreach (var path in PrefabPaths())
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || !asset.GetComponentsInChildren<XRGrabInteractable>(true).Any(g => g.throwOnDetach)) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    string done = Balance(root, counts);
                    if (done.Length == 0) continue;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    files++;
                    log.Append($"\n  {System.IO.Path.GetFileNameWithoutExtension(path)}: {done}");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            int inScene = 0;
            foreach (var top in scene.GetRootGameObjects())
            {
                string done = Balance(top, counts);
                if (done.Length == 0) continue;
                inScene++;
                log.Append($"\n  scene {top.name}: {done}");
            }
            if (inScene > 0) EditorSceneManager.MarkSceneDirty(scene);
            return $"[Throw Weight] {files} prefabs and {inScene} scene objects changed (light {counts["light"]}, flat {counts["flat"]}, " +
                   $"medium {counts["medium"]}, heavy {counts["heavy"]}).{(inScene > 0 ? " Save the scene." : "")}" + log;
        }

        // Every throwing grabbable under Root that this file owns (its own, or an override it already made) gets its
        // class's numbers. What changed, as text.
        private static string Balance(GameObject root, Dictionary<string, int> counts)
        {
            var notes = new List<string>();
            foreach (var grab in root.GetComponentsInChildren<XRGrabInteractable>(true))
            {
                if (!grab.throwOnDetach || Skip.Any(s => grab.name.StartsWith(s))) continue;
                // A copy of another prefab (nested, a variant's base, or in the scene) gets its numbers from that prefab,
                // unless it changed them itself.
                if (PrefabUtility.IsPartOfPrefabInstance(grab) && !Overridden(grab, "m_ThrowVelocityScale")) continue;
                var (kind, size) = KindOf(grab.gameObject);
                counts[kind]++;
                var so = new SerializedObject(grab);
                float old = grab.throwVelocityScale;
                // Only ever lower it: things already gentler than their class (XRI's own 1.5) stay as they are.
                float boost = Mathf.Min(old, kind == "heavy" ? HeavyBoost : kind == "medium" ? MediumBoost : LightBoost);
                float drag = kind == "flat" ? FlatDrag : kind == "light" ? LightDrag : kind == "medium" ? MediumDrag : HeavyDrag;
                if (OwnWeight(grab.name, out float ownBoost, out float ownDrag)) (boost, drag) = (ownBoost, ownDrag);
                bool changed = Set(so, "m_ThrowVelocityScale", boost);
                // Never on a weapon: a sword or hammer that trails the hand would spoil the swing.
                if (kind == "heavy" && !IsWeapon(grab))
                {
                    changed |= SetBool(so, "m_SmoothPosition", true) | Set(so, "m_SmoothPositionAmount", HeavySmoothing);
                    changed |= SetBool(so, "m_SmoothRotation", true) | Set(so, "m_SmoothRotationAmount", HeavySmoothing);
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                if (grab.TryGetComponent(out Rigidbody body) && !Mathf.Approximately(body.linearDamping, drag))
                {
                    if (!PrefabUtility.IsPartOfPrefabInstance(body) || Overridden(body, "m_LinearDamping") || Overridden(grab, "m_ThrowVelocityScale"))
                    {
                        body.linearDamping = drag;
                        changed = true;
                    }
                }
                if (changed) notes.Add($"{grab.name} {kind} {size:0.00} m, boost {old:0.#} -> {boost:0.#}, drag {drag}");
            }
            return string.Join("; ", notes);
        }

        // Its class by the size it's drawn at: light, flat (a thin light one), medium or heavy; and that size. A long thin
        // thing (a lollipop, a sword, a broom) counts by its length and thickness together, not its length alone.
        private static (string kind, float size) KindOf(GameObject thing)
        {
            var renderers = thing.GetComponentsInChildren<Renderer>(true)
                .Where(r => !(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer)).ToArray();
            if (renderers.Length == 0) return ("medium", 0f);
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            var sides = new[] { bounds.size.x, bounds.size.y, bounds.size.z }.OrderByDescending(v => v).ToArray();
            float longest = sides[0], middle = sides[1], thinnest = sides[2];
            float size = middle < longest * 0.5f ? Mathf.Sqrt(longest * middle) : longest;
            if (size <= LightSize) return (thinnest < longest * FlatShare ? "flat" : "light", size);
            return (size <= MediumSize ? "medium" : "heavy", size);
        }

        private static readonly string[] WeaponWords = { "Sword", "Shield", "Bow", "Gun", "Hammer", "Boong", "Broom" };

        private static bool IsWeapon(Component grab) =>
            WeaponWords.Any(w => grab.name.Contains(w));

        private static bool Overridden(Object part, string property)
        {
            var p = new SerializedObject(part).FindProperty(property);
            return p != null && p.prefabOverride;
        }

        private static bool Set(SerializedObject so, string property, float value)
        {
            var p = so.FindProperty(property);
            if (p == null || Mathf.Approximately(p.floatValue, value)) return false;
            p.floatValue = value;
            return true;
        }

        private static bool SetBool(SerializedObject so, string property, bool value)
        {
            var p = so.FindProperty(property);
            if (p == null || p.boolValue == value) return false;
            p.boolValue = value;
            return true;
        }

        // Every prefab, the ones others are made from first.
        private static List<string> PrefabPaths()
        {
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }).Select(AssetDatabase.GUIDToAssetPath).ToList();
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
    }
}
