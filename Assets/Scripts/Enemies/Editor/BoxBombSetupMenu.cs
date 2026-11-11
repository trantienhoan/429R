using System.Collections.Generic;
using System.IO;
using Game.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Enemies > Set Up Box Bombs (user, 2026-11-10: "random paper boxes evolve into bombs"). Puts a
    /// Box Bomb on Paper_Box_1-5 (Assets/Prefabs/Items/Breakable) and fills its empty sound and effect slots. Every box
    /// made from them anywhere can then be a bomb (30%): the rooms' boxes, the ones that fall out of Paper_Box_0 and
    /// Paper_Box_5, and boxes put into any other stage later. Paper_Box_0 (the shop's paid box of boxes) and the shop's
    /// Candy_Box are left out on purpose: to make one of them a bomb too, add its name to Boxes below. It only changes
    /// those prefab files (each copied to Documents/429R_claude_backups/2026-11-10/box_bombs/ first; a later run keeps
    /// the first copies), never the scene, and skips a prefab open in Prefab Mode. Running it again changes nothing.
    /// Remove Box Bombs takes it off again. The two "(test)" items work only in Play mode and save nothing.
    /// </summary>
    public static class BoxBombSetupMenu
    {
        private const string SetUpPath = "Tools/429 Game/Enemies/Set Up Box Bombs";
        private const string RemovePath = "Tools/429 Game/Enemies/Remove Box Bombs";
        private const string ArmAllPath = "Tools/429 Game/Enemies/Box Bombs: Arm Every New Box (test)";
        private const string WakePath = "Tools/429 Game/Enemies/Box Bombs: Wake Nearest (test)";

        private const string Breakable = "Assets/Prefabs/Items/Breakable/";
        // The boxes that can become bombs (not Paper_Box_0 or the shop's Candy_Box: paid items).
        private static readonly string[] Boxes = { "Paper_Box_1", "Paper_Box_2", "Paper_Box_3", "Paper_Box_4", "Paper_Box_5" };
        private const string BackupArea = "box_bombs";

        // Its sounds and effects (filled only where a slot is empty).
        private static readonly (string property, string path)[] Sounds =
        {
            ("rattle", "Assets/Audio/SFX/poision_bottle_shake.wav"),
            ("beep", "Assets/Audio/SFX/elon_boong_beep_beep.wav"),
            ("launchSound", "Assets/Audio/SFX/whoosh.wav"),
            ("bangSound", "Assets/Audio/SFX/explosion-312361.wav"),
        };
        // The books' bang pair, smaller (never Smoke_Blast).
        private static readonly string[] EffectPaths =
        {
            "Assets/Prefabs/Effects/Smoke/Smoke_Flash_Small_Puff.prefab",
            "Assets/Lana Studio/Hyper Casual FX/Prefabs/Flash/Flash_round_yellow.prefab",
        };

        [MenuItem(SetUpPath)]
        public static void SetUp()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Box Bombs] Stop Play mode first: it changes prefabs.");
                return;
            }
            var log = new List<string>();
            foreach (var box in Boxes) log.Add(Change(Breakable + box + ".prefab", AddBomb));
            Debug.Log("[Box Bombs] Set up:\n" + string.Join("\n", log));
        }

        [MenuItem(SetUpPath, true)]
        private static bool CanSetUp() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem(RemovePath)]
        public static void Remove()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Box Bombs] Stop Play mode first: it changes prefabs.");
                return;
            }
            var log = new List<string>();
            foreach (var box in Boxes) log.Add(Change(Breakable + box + ".prefab", RemoveBomb));
            Debug.Log("[Box Bombs] Removed:\n" + string.Join("\n", log));
        }

        [MenuItem(RemovePath, true)]
        private static bool CanRemove() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // Play mode only: every box that appears from now on is a bomb (until Play stops, or this is clicked again).
        [MenuItem(ArmAllPath)]
        private static void ArmAll()
        {
            BoxBomb.ForceArm = !BoxBomb.ForceArm;
            Debug.Log(BoxBomb.ForceArm
                ? $"[Box Bombs] Every box that appears from now on is a bomb (test; {BoxBomb.ArmedCount} armed now). " +
                  "Start a room from its checkpoint so its boxes appear again."
                : $"[Box Bombs] Back to the normal chance for new boxes ({BoxBomb.ArmedCount} armed now).");
        }

        [MenuItem(ArmAllPath, true)]
        private static bool CanArmAll()
        {
            Menu.SetChecked(ArmAllPath, EditorApplication.isPlaying && BoxBomb.ForceArm);
            return EditorApplication.isPlaying;
        }

        // Play mode only: the armed box nearest the head wakes now (any distance, in view or not, cap and rest skipped).
        [MenuItem(WakePath)]
        private static void WakeNearest()
        {
            Debug.Log("[Box Bombs] Wake Nearest (test): " + BoxBomb.WakeNearestForTest());
        }

        [MenuItem(WakePath, true)]
        private static bool CanWakeNearest() => EditorApplication.isPlaying;

        // Adds Box Bomb (if it isn't there) and fills its empty slots.
        private static bool AddBomb(GameObject root, List<string> notes)
        {
            bool changed = false;
            var bomb = root.GetComponent<BoxBomb>();
            if (bomb == null)
            {
                bomb = root.AddComponent<BoxBomb>();
                notes.Add("Box Bomb: none -> added");
                changed = true;
            }
            var so = new SerializedObject(bomb);
            foreach (var (property, path) in Sounds) changed |= FillClip(so, property, path, notes);
            changed |= FillEffects(so, notes);
            so.ApplyModifiedPropertiesWithoutUndo();
            if (!changed) notes.Add("already set up");
            return changed;
        }

        private static bool RemoveBomb(GameObject root, List<string> notes)
        {
            var bomb = root.GetComponent<BoxBomb>();
            if (bomb == null)
            {
                notes.Add("no Box Bomb (nothing to remove)");
                return false;
            }
            Object.DestroyImmediate(bomb);
            notes.Add("Box Bomb: removed");
            return true;
        }

        private static bool FillClip(SerializedObject so, string property, string path, List<string> notes)
        {
            var p = so.FindProperty(property);
            if (p == null)
            {
                notes.Add(property + ": not found on Box Bomb");
                return false;
            }
            if (p.objectReferenceValue != null) return false;
            var clip = File.Exists(path) ? AssetDatabase.LoadAssetAtPath<AudioClip>(path) : null;
            if (clip == null)
            {
                notes.Add($"{property}: {path} is missing, left empty");
                return false;
            }
            p.objectReferenceValue = clip;
            notes.Add($"{property}: none -> {clip.name}");
            return true;
        }

        // Only an empty list is filled (one already chosen by hand is kept).
        private static bool FillEffects(SerializedObject so, List<string> notes)
        {
            var p = so.FindProperty("effects");
            if (p == null || !p.isArray)
            {
                notes.Add("effects: not found on Box Bomb");
                return false;
            }
            for (int i = 0; i < p.arraySize; i++)
            {
                if (p.GetArrayElementAtIndex(i).objectReferenceValue != null) return false;
            }
            var found = new List<GameObject>();
            foreach (var path in EffectPaths)
            {
                var effect = File.Exists(path) ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
                if (effect == null)
                {
                    notes.Add($"effects: {path} is missing, left out");
                    continue;
                }
                if (effect.name == "Smoke_Blast") continue;
                found.Add(effect);
            }
            if (found.Count == 0) return false;
            p.arraySize = found.Count;
            var names = new List<string>();
            for (int i = 0; i < found.Count; i++)
            {
                p.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
                names.Add(found[i].name);
            }
            notes.Add("effects: none -> " + string.Join(" + ", names));
            return true;
        }

        // Loads the prefab, lets Edit change it and saves it only if something changed (a backup copy first).
        private static string Change(string path, System.Func<GameObject, List<string>, bool> edit)
        {
            string label = Path.GetFileNameWithoutExtension(path);
            if (!File.Exists(path) || AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return label + ": missing, skipped";
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == path) return label + ": open in Prefab Mode, skipped (close it and run again)";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var notes = new List<string>();
                if (!edit(root, notes)) return $"{label}: {string.Join(", ", notes)}";
                string backup = Backup(path);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return $"{label}: {string.Join(", ", notes)}, saved ({backup})";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // A copy of the prefab file as it is now, once (a later run keeps the first copy: the one from before the change).
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
