using System.Collections.Generic;
using System.IO;
using Game.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Enemies > Set Up Kamikaze Bees (user, 2026-11-11: "Make Bee rarely Dive, but if they do, that'd
    /// be how they die: Bee Dive Sting would kill the Bee, but do poison and explode damage"). The normal bees (Bee_Normal
    /// and Bee_Small: every bee that isn't a boss, wherever it comes from: the rooms, the wall portals, EnemyManager 4's
    /// rooftop swarm, the beehive, the bee bosses' minions) stay up and shoot a poison sting from range, like the bee
    /// bosses, and about 1 attack in 6 is a suicide dive that blows up on the player (Spider Brain > Kamikaze dives). It
    /// sets on the prefabs (absolute values, so running it again changes nothing):
    /// - Bee_Poison_Sting_Small (made once, a variant of the bosses' Bee_Poison_Sting, so it looks the same and its later
    ///   fixes carry over): a bug bite (Damage) plus a smaller poison dose than the bosses' (1 a second for 4 s; theirs 2
    ///   for 5). A normal bee's shot is also smaller than a boss's (the ball grows with the bee: about 10 cm, 7-8 for a small one);
    /// - Bee_Normal: Kamikaze Chance 0.17, the shot (every 3.5-6 s, 2.5-9 m, 5.5 m/s; at most 2 shooting at once), circling
    ///   3 m out so it's in range to shoot; the blast (box bomb's DamageHeavy x0.5 = 20-25, poison 2 a second for 5 s,
    ///   0.15/0.6 m shove, 0.9 m reach, the box bomb's puff and flash at 0.5, its bang and beep);
    /// - Bee_Small: the same, shooting less often (every 4.5-7.5 s, 5 m/s).
    /// Wingless menace (user, 2026-11-11: "when they lose their wings, their toughness would increase x2 or maybe even x3
    /// with PumpkinButt Boss"): Bee_Normal and Bee_Small get Wingless Toughness 2 (they lose their wings at 40% health);
    /// Bee_Boss_PumpkinButt now loses its wings too (Ground Below Health 0.4, its own value: Bee_Boss keeps 0) and gets
    /// Wingless Toughness 3: from then on it hunts the player on foot. All three walk at 80% once wingless (Ground Speed
    /// 0.8, was 0.5).
    /// The code never makes a boss a kamikaze; Bee_Boss is left as it is (the menu only checks it has no Kamikaze Chance
    /// and keeps its wings). The Paint Bees stage (Special Stage Type 2) has its
    /// own flyers without a Spider Brain, so it's not touched. Every prefab is copied to
    /// Documents/429R_claude_backups/2026-11-11/suicide_bees/ before it's changed (a later run keeps the first copies).
    /// No scene change. The "(test)" item works only in Play mode and saves nothing.
    /// </summary>
    public static class KamikazeBeeSetupMenu
    {
        private const string SetUpPath = "Tools/429 Game/Enemies/Set Up Kamikaze Bees";
        private const string ForcePath = "Tools/429 Game/Enemies/Kamikaze Bees: Every Attack Is A Dive (test)";
        private const string LogTitle = "[Kamikaze Bees]";
        private const string BackupDay = "2026-11-11";
        private const string BackupArea = "suicide_bees";

        private const string Smarter = "Assets/Prefabs/Enemies/Enemies_Smarter/";
        private const string BossStingPath = Smarter + "Bee_Poison_Sting.prefab";
        private const string StingPath = Smarter + "Bee_Poison_Sting_Small.prefab";
        private const string NormalPath = Smarter + "Bee_Normal.prefab";
        private const string SmallPath = Smarter + "Bee_Small.prefab";
        private static readonly string[] BossPaths = { Smarter + "Bee_Boss.prefab", Smarter + "Bee_Boss_PumpkinButt.prefab" };

        private const string StingSoundPath = "Assets/Audio/SFX/bug_Attack.wav";
        private const string BangSoundPath = "Assets/Audio/SFX/explosion-312361.wav";
        private const string BeepPath = "Assets/Audio/SFX/elon_boong_beep_beep.wav";
        // The box bomb's bang pair (never Smoke_Blast).
        private static readonly string[] EffectPaths =
        {
            "Assets/Prefabs/Effects/Smoke/Smoke_Flash_Small_Puff.prefab",
            "Assets/Lana Studio/Hyper Casual FX/Prefabs/Flash/Flash_round_yellow.prefab",
        };

        // The normal bees' sting shot: a bug bite plus a smaller poison dose than the bosses' (2 a second for 5 s).
        private const float StingPoisonPerSecond = 1f;
        private const float StingPoisonSeconds = 4f;

        // About 1 attack in 6 is the suicide dive.
        private const float KamikazeChance = 0.17f;
        private const string KamikazeEvent = "DamageHeavy";
        private const float KamikazeShare = 0.5f;
        private const string KamikazeNearEvent = "Damage";
        private const float KamikazeRadius = 0.9f;
        private const float KamikazePoisonPerSecond = 2f;
        private const float KamikazePoisonSeconds = 5f;
        private static readonly Vector2 KamikazeShove = new(0.15f, 0.6f);
        private const float KamikazeEffectSize = 0.5f;

        // Shooting like the bosses, smaller and slower: from 2.5 m (never point-blank) to 9 m, circling 3 m out.
        private const string WebAnimation = "attack";
        private const float WebReleaseTime = 0.4f;
        private const float WebDuration = 0.9f;
        private static readonly Vector2 WebRange = new(2.5f, 9f);
        private const int MaxWebShooters = 2;
        private const float CircleDistance = 3f;

        // Wingless menace: the normal bees x2 toughness once their wings are off; the Pumpkin Butt boss loses its wings at
        // 40% (like the normal bees) and gets x3.
        private const float WinglessToughness = 2f;
        private const string PumpkinButtPath = Smarter + "Bee_Boss_PumpkinButt.prefab";
        private const float PumpkinButtGroundBelowHealth = 0.4f;
        private const float PumpkinButtWinglessToughness = 3f;
        // Wingless bees walk at this share of their speed (user, 2026-11-11: 80%, "make them like a menace"; was 0.5).
        private const float WinglessGroundSpeed = 0.8f;

        private sealed class BeeNumbers
        {
            public string path;
            public string sizeWebs;
            public Vector2 webCooldown;
            public float webSpeed;
        }

        private static readonly BeeNumbers[] Bees =
        {
            new() { path = NormalPath, sizeWebs = "normal.webs", webCooldown = new Vector2(3.5f, 6f), webSpeed = 5.5f },
            new() { path = SmallPath, sizeWebs = "small.webs", webCooldown = new Vector2(4.5f, 7.5f), webSpeed = 5f },
        };

        [MenuItem(SetUpPath)]
        public static void SetUp()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning(LogTitle + " Stop Play mode first: it changes prefabs.");
                return;
            }
            var log = new List<string>();
            var stingSound = Load<AudioClip>(StingSoundPath, log);
            var bang = Load<AudioClip>(BangSoundPath, log);
            var beep = Load<AudioClip>(BeepPath, log);
            var effects = new List<Object>();
            foreach (var path in EffectPaths)
            {
                var effect = Load<GameObject>(path, log);
                if (effect != null && effect.name != "Smoke_Blast") effects.Add(effect);
            }

            // The shot first, so the bees can point at it.
            bool made = MakeSting(log);
            log.Add(Change(StingPath, !made, (root, notes) =>
            {
                var web = Of<WebBall>(root, notes);
                if (web == null) return false;
                bool changed = SetFloat(web, "poisonPerSecond", StingPoisonPerSecond, notes);
                changed |= SetFloat(web, "poisonSeconds", StingPoisonSeconds, notes);
                web.ApplyModifiedPropertiesWithoutUndo();
                return changed;
            }));
            var sting = AssetDatabase.LoadAssetAtPath<GameObject>(StingPath);

            foreach (var bee in Bees)
            {
                log.Add(Change(bee.path, true, (root, notes) =>
                {
                    var brain = Of<SpiderBrain>(root, notes);
                    if (brain == null) return false;
                    bool changed = SetFloat(brain, "kamikazeChance", KamikazeChance, notes);
                    changed |= SetString(brain, "kamikazeEvent", KamikazeEvent, notes);
                    changed |= SetFloat(brain, "kamikazeShare", KamikazeShare, notes);
                    changed |= SetString(brain, "kamikazeNearEvent", KamikazeNearEvent, notes);
                    changed |= SetFloat(brain, "kamikazeRadius", KamikazeRadius, notes);
                    changed |= SetFloat(brain, "kamikazePoisonPerSecond", KamikazePoisonPerSecond, notes);
                    changed |= SetFloat(brain, "kamikazePoisonSeconds", KamikazePoisonSeconds, notes);
                    changed |= SetVector2(brain, "kamikazeShove", KamikazeShove, notes);
                    changed |= SetObjects(brain, "kamikazeEffects", effects, notes);
                    changed |= SetFloat(brain, "kamikazeEffectSize", KamikazeEffectSize, notes);
                    changed |= SetObject(brain, "kamikazeSound", bang, notes);
                    changed |= SetObject(brain, "kamikazeWarning", beep, notes);
                    changed |= SetBool(brain, "kamikazeFlash", true, notes);

                    changed |= SetObject(brain, "webBall", sting, notes);
                    changed |= SetBool(brain, bee.sizeWebs, true, notes);
                    changed |= SetString(brain, "webAnimation", WebAnimation, notes);
                    // No bone to grow the shot on (that's the spider's web): it appears at its mouth as it's shot.
                    changed |= SetString(brain, "webHoldBone", "", notes);
                    changed |= SetFloat(brain, "webReleaseTime", WebReleaseTime, notes);
                    changed |= SetFloat(brain, "webDuration", WebDuration, notes);
                    changed |= SetVector2(brain, "webRange", WebRange, notes);
                    changed |= SetVector2(brain, "webCooldown", bee.webCooldown, notes);
                    changed |= SetFloat(brain, "webSpeed", bee.webSpeed, notes);
                    changed |= SetInt(brain, "maxWebShooters", MaxWebShooters, notes);
                    changed |= SetObject(brain, "webSound", stingSound, notes);
                    changed |= SetFloat(brain, "circleDistance", CircleDistance, notes);
                    changed |= SetFloat(brain, "winglessToughness", WinglessToughness, notes);
                    changed |= SetFloat(brain, "groundSpeed", WinglessGroundSpeed, notes);
                    brain.ApplyModifiedPropertiesWithoutUndo();
                    return changed;
                }));
            }

            // The Pumpkin Butt boss: loses its wings at 40% now, and is three times as tough from then on.
            log.Add(Change(PumpkinButtPath, true, (root, notes) =>
            {
                var brain = Of<SpiderBrain>(root, notes);
                if (brain == null) return false;
                bool changed = SetFloat(brain, "groundBelowHealth", PumpkinButtGroundBelowHealth, notes);
                changed |= SetFloat(brain, "winglessToughness", PumpkinButtWinglessToughness, notes);
                changed |= SetFloat(brain, "groundSpeed", WinglessGroundSpeed, notes);
                brain.ApplyModifiedPropertiesWithoutUndo();
                return changed;
            }));

            foreach (var boss in BossPaths) log.Add(CheckBoss(boss));

            AssetDatabase.SaveAssets();
            Debug.Log(LogTitle + " Set up:\n" + string.Join("\n", log));
        }

        [MenuItem(SetUpPath, true)]
        private static bool CanSetUp() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // Play mode only: every kamikaze bee's next attack is its dive (until Play stops, or this is clicked again).
        [MenuItem(ForcePath)]
        private static void ForceDives()
        {
            SpiderBrain.ForceKamikaze = !SpiderBrain.ForceKamikaze;
            Debug.Log(SpiderBrain.ForceKamikaze
                ? LogTitle + " Every kamikaze bee's next attack is its dive (test)."
                : LogTitle + " Back to the normal chance (about 1 attack in 6).");
        }

        [MenuItem(ForcePath, true)]
        private static bool CanForceDives()
        {
            Menu.SetChecked(ForcePath, EditorApplication.isPlaying && SpiderBrain.ForceKamikaze);
            return EditorApplication.isPlaying;
        }

        // ---- the sting shot ----

        // Bee_Poison_Sting_Small, once: a variant of Bee_Poison_Sting. True when made now.
        private static bool MakeSting(List<string> log)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(StingPath);
            if (existing != null)
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(existing);
                if (PrefabUtility.GetPrefabAssetType(existing) != PrefabAssetType.Variant || source == null ||
                    AssetDatabase.GetAssetPath(source) != BossStingPath)
                    log.Add("Bee_Poison_Sting_Small: not a variant of Bee_Poison_Sting any more (unpacked?); its poison is set anyway");
                return false;
            }
            var bossSting = AssetDatabase.LoadAssetAtPath<GameObject>(BossStingPath);
            if (bossSting == null)
            {
                log.Add("Bee_Poison_Sting_Small: Bee_Poison_Sting is missing (run Tools > 429 Game > Balance 2026-11-10 > Bee " +
                        "Bosses Stay High first), so it can't be made");
                return false;
            }
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(bossSting, scene);
                instance.name = "Bee_Poison_Sting_Small";
                PrefabUtility.SaveAsPrefabAsset(instance, StingPath);
                log.Add("Bee_Poison_Sting_Small: made (a variant of Bee_Poison_Sting)");
                return true;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        // What a bee boss does now: its Kamikaze Chance (never used for a boss) and its wings (Wingless Toughness).
        private static string CheckBoss(string path)
        {
            string label = Path.GetFileNameWithoutExtension(path);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) return label + ": missing";
            var brain = go.GetComponent<SpiderBrain>();
            if (brain == null) return label + ": no Spider Brain";
            var so = new SerializedObject(brain);
            var chance = so.FindProperty("kamikazeChance");
            var wingsOff = so.FindProperty("groundBelowHealth");
            var wingless = so.FindProperty("winglessToughness");
            if (chance == null || wingless == null) return label + ": kamikazeChance/winglessToughness not found (old Spider Brain?)";
            string wings = wingsOff != null && wingsOff.floatValue > 0f
                ? $"loses its wings at {wingsOff.floatValue * 100f:0}% health, then Toughness x{wingless.floatValue}"
                : "keeps its wings all fight";
            return chance.floatValue > 0f
                ? $"{label}: has Kamikaze Chance {chance.floatValue} (not used: a boss never dives to blow up); {wings}"
                : $"{label}: no Kamikaze Chance (keeps Stays High, its shots and its rare dive); {wings}";
        }

        // ---- shared bits ----

        private static T Load<T>(string path, List<string> log) where T : Object
        {
            var asset = File.Exists(path) ? AssetDatabase.LoadAssetAtPath<T>(path) : null;
            if (asset == null) log.Add(path + ": missing, left out");
            return asset;
        }

        // Loads the prefab, lets Edit change it and saves it only if something changed (a backup copy first, unless this
        // run just made it).
        private static string Change(string path, bool backUp, System.Func<GameObject, List<string>, bool> edit)
        {
            string label = Path.GetFileNameWithoutExtension(path);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return label + ": missing, skipped";
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == path) return label + ": open in Prefab Mode, skipped (close it and run again)";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var notes = new List<string>();
                bool changed = edit(root, notes);
                if (!changed) return $"{label}: {string.Join(", ", notes)}, nothing to change";
                string backup = backUp ? Backup(path) : "made this run, no backup needed";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return $"{label}: {string.Join(", ", notes)}, saved ({backup})";
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

        private static SerializedProperty Find(SerializedObject so, string property, List<string> notes)
        {
            var p = so.FindProperty(property);
            if (p == null) notes.Add(property + " not found (is the new Spider Brain compiled?)");
            return p;
        }

        private static bool SetFloat(SerializedObject so, string property, float value, List<string> notes)
        {
            var p = Find(so, property, notes);
            if (p == null) return false;
            if (Mathf.Approximately(p.floatValue, value))
            {
                notes.Add($"{property} {value} (already)");
                return false;
            }
            notes.Add($"{property} {p.floatValue} -> {value}");
            p.floatValue = value;
            return true;
        }

        private static bool SetInt(SerializedObject so, string property, int value, List<string> notes)
        {
            var p = Find(so, property, notes);
            if (p == null) return false;
            if (p.intValue == value)
            {
                notes.Add($"{property} {value} (already)");
                return false;
            }
            notes.Add($"{property} {p.intValue} -> {value}");
            p.intValue = value;
            return true;
        }

        private static bool SetBool(SerializedObject so, string property, bool value, List<string> notes)
        {
            var p = Find(so, property, notes);
            if (p == null) return false;
            if (p.boolValue == value)
            {
                notes.Add($"{property} {value} (already)");
                return false;
            }
            notes.Add($"{property} {p.boolValue} -> {value}");
            p.boolValue = value;
            return true;
        }

        private static bool SetString(SerializedObject so, string property, string value, List<string> notes)
        {
            var p = Find(so, property, notes);
            if (p == null) return false;
            if (p.stringValue == value)
            {
                notes.Add($"{property} '{value}' (already)");
                return false;
            }
            notes.Add($"{property} '{p.stringValue}' -> '{value}'");
            p.stringValue = value;
            return true;
        }

        private static bool SetVector2(SerializedObject so, string property, Vector2 value, List<string> notes)
        {
            var p = Find(so, property, notes);
            if (p == null) return false;
            if (Mathf.Approximately(p.vector2Value.x, value.x) && Mathf.Approximately(p.vector2Value.y, value.y))
            {
                notes.Add($"{property} {value.x}-{value.y} (already)");
                return false;
            }
            notes.Add($"{property} {p.vector2Value.x}-{p.vector2Value.y} -> {value.x}-{value.y}");
            p.vector2Value = value;
            return true;
        }

        private static bool SetObject(SerializedObject so, string property, Object value, List<string> notes)
        {
            var p = Find(so, property, notes);
            if (p == null) return false;
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

        // A whole list of objects, in this order.
        private static bool SetObjects(SerializedObject so, string property, List<Object> values, List<string> notes)
        {
            var p = Find(so, property, notes);
            if (p == null || !p.isArray) return false;
            if (values.Count == 0)
            {
                notes.Add(property + ": its assets are missing, left as it is");
                return false;
            }
            var names = new List<string>();
            foreach (var value in values) names.Add(value.name);
            bool same = p.arraySize == values.Count;
            for (int i = 0; same && i < values.Count; i++) same = p.GetArrayElementAtIndex(i).objectReferenceValue == values[i];
            if (same)
            {
                notes.Add($"{property} {string.Join(" + ", names)} (already)");
                return false;
            }
            p.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            notes.Add($"{property} -> {string.Join(" + ", names)}");
            return true;
        }

        // A copy of the asset as it is now, once (a later run keeps the first copy: the one from before the change).
        private static string Backup(string assetPath)
        {
            string folder = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                "429R_claude_backups", BackupDay, BackupArea);
            Directory.CreateDirectory(folder);
            string to = Path.Combine(folder, Path.GetFileName(assetPath));
            if (File.Exists(to)) return "backup from an earlier run kept";
            File.Copy(assetPath, to);
            return "backed up to " + to;
        }
    }
}
