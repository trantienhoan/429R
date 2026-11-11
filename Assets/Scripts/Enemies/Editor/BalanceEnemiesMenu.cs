using System.Collections.Generic;
using System.IO;
using Game.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Balance 2026-11-10 > Enemies (user, 2026-11-10: the game is too easy at level 10). Sets these
    /// numbers on the prefabs; they're absolute, so running it again gives the same result:
    /// - the rare Rainbow Slime: Spider Brain > White anomaly > Anomaly Chance 0.003 (about 1 in 333, was 0.01) on
    ///   Spider_, Cockroach_ and Bee_Smarter; every bug made from them inherits it;
    /// - Fat_Pumpkin_Enemy: Health 20 (was 5), Toughness 2 (was 1), Giant Health 300;
    /// - Spider_Boss and Cockroach_Boss: Size > Boss > Speed 1.17 and 1.235 (+30%, was 0.9 and 0.95), set on those
    ///   variants so their Pumpkin Butt variants inherit it;
    /// - pumpkin_boss_Z: Push Power 7.5 and Cyclone Push Power 3.75 (x2.5, was 3 and 1.5), and its Chaos Aura (Chaos_Aura).
    /// The rest of the pass is code: the low-health stun chance (Hit Settings), the chaos boss roll (Pumpkin Boss Brain)
    /// and the giant Fat Pumpkin shrugging hits off. Every prefab is copied to Documents/429R_claude_backups/2026-11-10/
    /// enemies/ before it's changed (a later run keeps the first run's copies, the ones from before the pass).
    /// </summary>
    public static class BalanceEnemiesMenu
    {
        private const string Smarter = "Assets/Prefabs/Enemies/Enemies_Smarter/";
        private const string FatPumpkinPath = "Assets/Prefabs/Enemies/Fat_Pumpkin_Enemy.prefab";
        private const string PumpkinBossPath = "Assets/Prefabs/Enemies/pumpkin_boss_Z.prefab";
        private const string ChaosAuraPath = "Assets/Prefabs/Effects/Chaos_Aura.prefab";
        private const string BackupArea = "enemies";

        // The Rainbow Slime's chance, on the three base bugs (no variant has its own).
        private const float AnomalyChance = 0.003f;
        private static readonly string[] Bugs = { "Spider_Smarter", "Cockroach_Smarter", "Bee_Smarter" };

        // Fat Pumpkin: tankier, and a giant has a fixed 300.
        private const float FatHealth = 20f;
        private const float FatToughness = 2f;
        private const float FatGiantHealth = 300f;

        // The white spider and cockroach bosses +30% (their base prefabs' 0.9 and 0.95 x 1.3); the Pumpkin Butt ones
        // are variants of these and must not have their own.
        private static readonly (string prefab, float speed)[] BossSpeeds = { ("Spider_Boss", 1.17f), ("Cockroach_Boss", 1.235f) };
        private static readonly string[] Inheriting = { "Spider_Boss_PumpkinButt", "Cockroach_Boss_PumpkinButt" };

        // The pumpkin boss throws the player 2.5 times as far.
        private const float PushPower = 7.5f;
        private const float CyclonePushPower = 3.75f;

        [MenuItem("Tools/429 Game/Balance 2026-11-10/Enemies")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Balance 2026-11-10 Enemies] Stop Play mode first: it changes prefabs.");
                return;
            }
            var log = new List<string>();

            // Base prefabs first, so the variants below load with the new base numbers.
            foreach (var bug in Bugs)
            {
                log.Add(Change(Smarter + bug + ".prefab", (root, notes) =>
                {
                    var brain = Of<SpiderBrain>(root, notes);
                    if (brain == null) return false;
                    bool changed = SetFloat(brain, "anomalyChance", AnomalyChance, notes);
                    brain.ApplyModifiedPropertiesWithoutUndo();
                    return changed;
                }));
            }
            log.Add(AnomalyOverrides());

            log.Add(Change(FatPumpkinPath, (root, notes) =>
            {
                var fat = Of<FatPumpkin>(root, notes);
                if (fat == null) return false;
                bool changed = SetFloat(fat, "health", FatHealth, notes);
                changed |= SetFloat(fat, "toughness", FatToughness, notes);
                changed |= SetFloat(fat, "giantHealth", FatGiantHealth, notes);
                fat.ApplyModifiedPropertiesWithoutUndo();
                return changed;
            }));

            foreach (var (prefab, speed) in BossSpeeds)
            {
                log.Add(Change(Smarter + prefab + ".prefab", (root, notes) =>
                {
                    var brain = Of<SpiderBrain>(root, notes);
                    if (brain == null) return false;
                    // Saved as an override on this variant (its base prefab keeps its own).
                    bool changed = SetFloat(brain, "boss.speed", speed, notes);
                    brain.ApplyModifiedPropertiesWithoutUndo();
                    return changed;
                }));
            }
            foreach (var prefab in Inheriting) log.Add(CheckInherits(Smarter + prefab + ".prefab", "boss.speed"));

            log.Add(Change(PumpkinBossPath, (root, notes) =>
            {
                var boss = Of<PumpkinBossBrain>(root, notes);
                if (boss == null) return false;
                bool changed = SetFloat(boss, "pushPower", PushPower, notes);
                changed |= SetFloat(boss, "cyclonePushPower", CyclonePushPower, notes);
                changed |= SetObject(boss, "chaosAura", AssetDatabase.LoadAssetAtPath<GameObject>(ChaosAuraPath), notes);
                boss.ApplyModifiedPropertiesWithoutUndo();
                return changed;
            }));

            AssetDatabase.SaveAssets();
            Debug.Log("[Balance 2026-11-10 Enemies]\n" + string.Join("\n", log));
        }

        // Loads the prefab, lets Edit change it and saves it only if something changed (a backup copy first).
        private static string Change(string path, System.Func<GameObject, List<string>, bool> edit)
        {
            string label = Path.GetFileNameWithoutExtension(path);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return label + ": missing, skipped";
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == path) return label + ": open in Prefab Mode, skipped (close it and run again)";
            string backup = Backup(path);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var notes = new List<string>();
                bool changed = edit(root, notes);
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                return $"{label}: {string.Join(", ", notes)}{(changed ? ", saved" : ", nothing to change")} ({backup})";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static SerializedObject Of<T>(GameObject root, List<string> notes) where T : Component
        {
            var component = root.GetComponent<T>();
            if (component != null) return new SerializedObject(component);
            notes.Add("no " + typeof(T).Name + " on its root");
            return null;
        }

        private static bool SetFloat(SerializedObject so, string property, float value, List<string> notes)
        {
            var p = so.FindProperty(property);
            if (p == null)
            {
                notes.Add(property + " not found");
                return false;
            }
            if (Mathf.Approximately(p.floatValue, value))
            {
                notes.Add($"{property} {value} (already)");
                return false;
            }
            notes.Add($"{property} {p.floatValue} -> {value}");
            p.floatValue = value;
            return true;
        }

        private static bool SetObject(SerializedObject so, string property, Object value, List<string> notes)
        {
            var p = so.FindProperty(property);
            if (p == null)
            {
                notes.Add(property + " not found");
                return false;
            }
            if (value == null)
            {
                notes.Add(property + ": its asset is missing, left as it is");
                return false;
            }
            if (p.objectReferenceValue == value)
            {
                notes.Add($"{property} {value.name} (already)");
                return false;
            }
            notes.Add($"{property} -> {value.name}");
            p.objectReferenceValue = value;
            return true;
        }

        // A variant that should inherit this value: says so, or warns that it has its own.
        private static string CheckInherits(string path, string property)
        {
            string label = Path.GetFileNameWithoutExtension(path);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) return label + ": missing";
            foreach (var change in PrefabUtility.GetPropertyModifications(go) ?? System.Array.Empty<PropertyModification>())
            {
                if (change.propertyPath == property && change.target is SpiderBrain)
                    return $"{label}: has its own {property} {change.value} (not inherited; left as it is)";
            }
            return $"{label}: {property} inherited";
        }

        // Bug variants with their own Anomaly Chance keep it (e.g. the PlayGround slimes' 0): listed, not changed.
        private static string AnomalyOverrides()
        {
            var own = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (go == null || go.GetComponent<SpiderBrain>() == null) continue;
                if (PrefabUtility.GetPrefabAssetType(go) != PrefabAssetType.Variant) continue;
                foreach (var change in PrefabUtility.GetPropertyModifications(go) ?? System.Array.Empty<PropertyModification>())
                {
                    if (change.propertyPath != "anomalyChance" || !(change.target is SpiderBrain)) continue;
                    own.Add($"{go.name} {change.value}");
                    break;
                }
            }
            return own.Count == 0 ? "No bug variant has its own Anomaly Chance: all inherit " + AnomalyChance
                : "Own Anomaly Chance kept on: " + string.Join(", ", own);
        }

        // A copy of the prefab as it is now, once (a later run keeps the first copy: the one from before the pass).
        private static string Backup(string assetPath)
        {
            string folder = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                "429R_claude_backups", "2026-11-10", BackupArea);
            Directory.CreateDirectory(folder);
            string to = Path.Combine(folder, Path.GetFileName(assetPath));
            if (File.Exists(to)) return "backup from an earlier run kept";
            File.Copy(assetPath, to);
            return "backed up to " + to;
        }
    }
}
