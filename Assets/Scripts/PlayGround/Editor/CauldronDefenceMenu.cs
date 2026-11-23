using System.Collections.Generic;
using System.IO;
using System.Linq;
using HutongGames.PlayMaker;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.PlayGround.Editor
{
    /// <summary>
    /// Tools > 429 Game > PlayGround > Use Defend The Cauldron (user, 2026-11-16): gate 1 is "defend the cauldron" (see
    /// Cauldron Defence). Since 2026-11-18 every gate ends the same way (its giant pumpkin, The_Seed, the rooftop's tree
    /// defence: GAMESTAGES "Path 1 ON" next1 -> "Tween PlayGroundZ" -> ... -> "Rooftop_ON PG").
    /// Fire_Woods_n_Cauldron.prefab only (copied to Documents/429R_claude_backups/2026-11-16/gate1_defend/ first; a later
    /// run keeps that first copy): PlayGround Rounds is switched off (kept, with its waves), a Cauldron Health and a
    /// Cauldron Defence are added (or switched on) and filled in: the monster phases, the mini bosses with their PG_
    /// chests, the chaos boss, the smoke, sounds and numbers below. A component that's already there keeps its numbers
    /// (tune them in the Inspector); only its empty references are filled. The Cauldron gets its fire's whoosh if it has
    /// none. The chaos boss is a prefab variant, Prefabs/Enemies/pumpkin_boss_Z_Doom (made once: pumpkin_boss_Z with Wake
    /// Up Seconds 4 instead of 33 and Chaos Chance 1), so it rises out of the broken pot at once; its Game Stages Event and
    /// Bool are empty (user, 2026-11-23): killing it only ends the gate, never the whole game (no Win, no BOSS).
    /// GAMESTAGES is NOT touched any more (2026-11-23): until then this menu re-pointed "Path 1 ON" next1 to "Tween
    /// PlayGroundZ To Boss" (straight to the boss, the 11-16 design), which silently dropped the rooftop's tree defence
    /// on every re-run. The gate flow belongs to the gates' shared set-up (Scripts/Gates/Editor, the coordinator's).
    /// "Use Cooking Rounds (Old Gate 1)" switches the prefab back (Cauldron Defence off, PlayGround Rounds on); it no
    /// longer touches GAMESTAGES either. The old "To Boss" / "Boss PG" states it once made are left as they are (unused).
    /// </summary>
    public static class CauldronDefenceMenu
    {
        private const string Log = "[Cauldron Defence]";
        private const string CauldronPath = "Assets/Prefabs/PlayGround/Fire_Woods_n_Cauldron.prefab";
        private const string BackupArea = "gate1_defend";
        private const string BugFolder = "Assets/Prefabs/Enemies/Enemies_Smarter/";
        private const string SlimeFolder = "Assets/Prefabs/Enemies/PLAYGROUND/";
        private const string ChestFolder = "Assets/Prefabs/PlayGround/PG_Wood/";
        private const string SmokeFolder = "Assets/Prefabs/Effects/Smoke/";
        private const string Sfx = "Assets/Audio/SFX/";
        private const string ChaosBossPath = "Assets/Prefabs/Enemies/pumpkin_boss_Z.prefab";
        // The doom boss: a variant of the boss that rises out of the floor after Doom Wake Up seconds (the boss's own
        // 33 s is for the rooftop's entrance) and is a chaos boss every time.
        private const string DoomBossPath = "Assets/Prefabs/Enemies/pumpkin_boss_Z_Doom.prefab";
        private const float DoomWakeUp = 4f;
        private const string LogPath = "Assets/Prefabs/PlayGround/Dropped_Wood_Fuel.prefab";
        private const string HatPath = "Assets/Prefabs/PlayGround/Recipe_Hat.prefab";
        private const string FontPath = "Assets/Fonts/socake/Socake SDF.asset";

        // GAMESTAGES (only read: where the gate 1 seed leads).
        private const string StagesFsm = "GAMESTAGES";
        private const string On1 = "Path 1 ON";
        private const string RooftopRoute = "Tween PlayGroundZ";

        // The monsters by share of the cook: label, from, the bugs (Enemies_Smarter), most alive, seconds between.
        private static readonly (string label, float from, string[] bugs, int maxAlive, Vector2 wait)[] Phases =
        {
            ("0:00-2:30 small spiders", 0f, new[] { "Spider_Small" }, 4, new Vector2(2.5f, 4f)),
            ("2:30-5:00 spiders", 0.25f, new[] { "Spider_Small", "Spider_Normal" }, 5, new Vector2(2f, 3.5f)),
            ("5:00-7:30 spiders and small roaches", 0.5f, new[] { "Spider_Normal", "Cockroach_Small" }, 6, new Vector2(1.8f, 3f)),
            ("7:30-10:00 roaches", 0.75f, new[] { "Cockroach_Small", "Cockroach_Normal" }, 7, new Vector2(1.5f, 2.5f)),
        };

        // The mini bosses: label, share of the cook, the slime (PLAYGROUND), its chest (PG_Wood), the gear bool.
        private static readonly (string label, float at, string boss, string chest, string gear)[] Bosses =
        {
            ("2:30 Slime_Big_Blue (Sticky): Chest gear", 0.25f, "Slime_Big_Blue", "PG_Treasure_Chest_and_Seed_Chest", "Chest_Gear"),
            ("5:00 Slime_Big_Red (Bull): Head gear", 0.5f, "Slime_Big_Red", "PG_Treasure_Chest_and_Seed_Head", "Head_Gear"),
            ("7:30 Slime_Big_Yellow (Bomb): Shield gear", 0.75f, "Slime_Big_Yellow", "PG_Treasure_Chest_and_Seed_Shield", "Shield_Gear"),
            // User 2026-11-16: "even 4th big slime can come, more = fun": the last rush, a plain reward chest (no gear).
            ("9:00 Slime_Big: reward chest", 0.9f, "Slime_Big", "PG_Treasure_Chest_Drop", ""),
        };

        // Effects: smoke prefab, size (times its own), height over the cauldron's foot.
        private static readonly (string smoke, float scale, float height)[] PopEffects =
        {
            ("Smoke_Flash_Circle_Puff", 0.6f, 0.05f),
            ("Smoke_Puff_Burst_Out", 0.8f, 0.6f),
            ("Smoke_Flash_Five_Puffs", 0.35f, 0.3f),
        };
        private static readonly (string smoke, float scale, float height)[] BreakEffects =
        {
            ("Smoke_Flash_Circle_Puff", 1f, 0.05f),
            ("Smoke_Flash_Five_Puffs", 0.6f, 0.3f),
            ("Smoke_Puff_Burst_Out", 1.2f, 0.6f),
        };

        [MenuItem("Tools/429 Game/PlayGround/Use Defend The Cauldron")]
        public static void UseDefence()
        {
            if (EditorApplication.isPlaying) { Debug.LogError($"{Log} Stop play mode first; nothing changed."); return; }
            if (OpenInPrefabMode()) return;
            var notes = new List<string>();
            if (!SetUpCauldron(notes)) return;
            CheckStages(notes);
            AssetDatabase.SaveAssets();
            Debug.Log($"{Log} Gate 1 = defend the cauldron (the prefab only; GAMESTAGES untouched): {string.Join("; ", notes)}.");
        }

        [MenuItem("Tools/429 Game/PlayGround/Use Defend The Cauldron", true)]
        private static bool CanUse() => !EditorApplication.isPlaying;

        [MenuItem("Tools/429 Game/PlayGround/Use Cooking Rounds (Old Gate 1)")]
        public static void UseRounds()
        {
            if (EditorApplication.isPlaying) { Debug.LogError($"{Log} Stop play mode first; nothing changed."); return; }
            if (OpenInPrefabMode()) return;
            var notes = new List<string>();
            Backup(CauldronPath, notes);
            var root = PrefabUtility.LoadPrefabContents(CauldronPath);
            try
            {
                if (root.TryGetComponent<CauldronDefence>(out var defence) && defence.enabled)
                {
                    defence.enabled = false;
                    notes.Add("Cauldron Defence off");
                }
                if (root.TryGetComponent<PlayGroundRounds>(out var rounds) && !rounds.enabled)
                {
                    rounds.enabled = true;
                    notes.Add("PlayGround Rounds on");
                }
                PrefabUtility.SaveAsPrefabAsset(root, CauldronPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            CheckStages(notes);
            AssetDatabase.SaveAssets();
            Debug.Log($"{Log} Gate 1 = the cooking rounds again (the prefab only; GAMESTAGES untouched): {string.Join("; ", notes)}.");
        }

        [MenuItem("Tools/429 Game/PlayGround/Use Cooking Rounds (Old Gate 1)", true)]
        private static bool CanGoBack() => !EditorApplication.isPlaying;

        // ------------------------------------------------------------------ 1. the cauldron prefab

        private static bool SetUpCauldron(List<string> notes)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CauldronPath) == null)
            {
                Debug.LogError($"{Log} No {CauldronPath}; nothing changed.");
                return false;
            }
            Backup(CauldronPath, notes);
            doomBoss = MakeDoomBoss(notes);
            var root = PrefabUtility.LoadPrefabContents(CauldronPath);
            try
            {
                if (!root.TryGetComponent<Cauldron>(out var cauldron))
                {
                    Debug.LogError($"{Log} {CauldronPath} has no Cauldron; nothing changed.");
                    return false;
                }
                if (root.TryGetComponent<PlayGroundRounds>(out var rounds) && rounds.enabled)
                {
                    rounds.enabled = false;
                    notes.Add("PlayGround Rounds off (kept)");
                }

                bool newHealth = !root.TryGetComponent<CauldronHealth>(out var health);
                if (newHealth) health = root.AddComponent<CauldronHealth>();
                FillHealth(new SerializedObject(health), newHealth, notes);
                notes.Add(newHealth ? "Cauldron Health added (150)" : "Cauldron Health kept");

                bool newDefence = !root.TryGetComponent<CauldronDefence>(out var defence);
                if (newDefence) defence = root.AddComponent<CauldronDefence>();
                if (!defence.enabled)
                {
                    defence.enabled = true;
                    notes.Add("Cauldron Defence switched on");
                }
                FillDefence(new SerializedObject(defence), newDefence, notes);
                notes.Add(newDefence ? "Cauldron Defence added" : "Cauldron Defence kept (its numbers untouched)");

                var c = new SerializedObject(cauldron);
                if (SetIfEmpty(c.FindProperty("lightSound"), Load<AudioClip>(Sfx + "whoosh.wav", notes))) notes.Add("fire whoosh set");
                // A wrong recipe costs less (2026-11-23, the gate 1 brief): one small slime in the colour of a candy the
                // recipe still needs (it drops that candy), not a big one that splits. Only replaces the old big slime.
                var wrong = c.FindProperty("wrongResult");
                var bigSlime = Load<GameObject>(SlimeFolder + "Slime_Big.prefab", notes);
                var smallSlime = Load<GameObject>(SlimeFolder + "Slime_Small.prefab", notes);
                if (wrong != null && smallSlime != null && (wrong.objectReferenceValue == null || wrong.objectReferenceValue == bigSlime))
                {
                    wrong.objectReferenceValue = smallSlime;
                    notes.Add("a wrong recipe spits a small slime (was the big one)");
                }
                c.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, CauldronPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return true;
        }

        private static void FillHealth(SerializedObject h, bool fresh, List<string> notes)
        {
            if (fresh)
            {
                h.FindProperty("maxHealth").floatValue = 150f;
                h.FindProperty("smallBite").floatValue = 1f;
                h.FindProperty("normalBite").floatValue = 2f;
                h.FindProperty("bossBite").floatValue = 4f;
                h.FindProperty("hitVolume").floatValue = 0.8f;
                h.FindProperty("hitPuffScale").floatValue = 0.15f;
                h.FindProperty("squash").floatValue = 0.06f;
                h.FindProperty("alarmEvery").floatValue = 3f;
            }
            var hits = h.FindProperty("hitSounds");
            if (hits.arraySize == 0)
            {
                var clips = new List<Object>();
                for (int i = 1; i <= 4; i++) clips.Add(Load<AudioClip>(Sfx + "rock_hit_" + i + ".wav", notes));
                SetArray(hits, clips.Where(x => x != null).ToArray());
            }
            SetIfEmpty(h.FindProperty("hitPuff"), Load<GameObject>(SmokeFolder + "Smoke_Flash_Small_Puff.prefab", notes));
            h.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void FillDefence(SerializedObject d, bool fresh, List<string> notes)
        {
            if (fresh)
            {
                d.FindProperty("recipeCandies").intValue = 4;
                d.FindProperty("recipePumpkins").intValue = 1;
                d.FindProperty("cookSeconds").floatValue = 300f;   // user 2026-11-18: 5 minutes (was 10)
                d.FindProperty("fuelSpeedsCountdown").boolValue = true;
                d.FindProperty("spareLogAfter").floatValue = 45f;
                d.FindProperty("countdownTitle").stringValue = "DEFEND THE CAULDRON!";
                d.FindProperty("popVolume").floatValue = 1f;
                d.FindProperty("popGrowSeconds").floatValue = 0.4f;
                d.FindProperty("popPushRadius").floatValue = 3f;
                d.FindProperty("popClear").floatValue = 2.8f;
                d.FindProperty("popMinPush").floatValue = 1f;
                d.FindProperty("popPushHeight").floatValue = 0.5f;
                d.FindProperty("popShake").boolValue = false;
                d.FindProperty("firstSpawnAfter").floatValue = 2f;
                d.FindProperty("spawnEffectScale").floatValue = 0.5f;
                d.FindProperty("minPlayerDistance").floatValue = 5f;
                d.FindProperty("edgeInset").floatValue = 1f;
                d.FindProperty("nearRadius").floatValue = 7f;
                d.FindProperty("farRadius").floatValue = 8.5f;
                d.FindProperty("standOff").floatValue = 1.05f;
                d.FindProperty("bossNote").stringValue = "MINI BOSS!";
                d.FindProperty("chestEffectScale").floatValue = 0.6f;
                d.FindProperty("cleanWithFairyShield").boolValue = true;
                d.FindProperty("shieldName").stringValue = "Shield_Sphere";
                d.FindProperty("shieldSmokeName").stringValue = "Smoke_Blast_Green";
                d.FindProperty("startGearBool").stringValue = "Bracer_Gear";
                d.FindProperty("burstDelay").floatValue = 2.5f;
                d.FindProperty("bossDelay").floatValue = 1.5f;
                d.FindProperty("breakShake").stringValue = "ShakeBig";
                d.FindProperty("doomExtraAlive").intValue = 2;
                d.FindProperty("doomSpawnWaitShare").floatValue = 0.7f;
                d.FindProperty("groundBreakScale").floatValue = 0.35f;
                d.FindProperty("groundBreakText").stringValue = "NO ESCAPE!";
            }

            var phases = d.FindProperty("phases");
            if (fresh || phases.arraySize == 0)
            {
                phases.arraySize = Phases.Length;
                for (int i = 0; i < Phases.Length; i++)
                {
                    var (label, from, bugs, maxAlive, wait) = Phases[i];
                    var p = phases.GetArrayElementAtIndex(i);
                    p.FindPropertyRelative("label").stringValue = label;
                    p.FindPropertyRelative("from").floatValue = from;
                    SetArray(p.FindPropertyRelative("enemies"), bugs.Select(b => (Object)Load<GameObject>(BugFolder + b + ".prefab", notes)).Where(x => x != null).ToArray());
                    p.FindPropertyRelative("maxAlive").intValue = maxAlive;
                    p.FindPropertyRelative("spawnWait").vector2Value = wait;
                }
            }

            var bosses = d.FindProperty("miniBosses");
            if (fresh || bosses.arraySize == 0)
            {
                bosses.arraySize = Bosses.Length;
                for (int i = 0; i < Bosses.Length; i++)
                {
                    var (label, at, boss, chest, gear) = Bosses[i];
                    var b = bosses.GetArrayElementAtIndex(i);
                    b.FindPropertyRelative("label").stringValue = label;
                    b.FindPropertyRelative("at").floatValue = at;
                    b.FindPropertyRelative("boss").objectReferenceValue = Load<GameObject>(SlimeFolder + boss + ".prefab", notes);
                    b.FindPropertyRelative("chest").objectReferenceValue = Load<GameObject>(ChestFolder + chest + ".prefab", notes);
                    b.FindPropertyRelative("gearBool").stringValue = gear;
                }
            }

            FillBursts(d.FindProperty("popEffects"), PopEffects, fresh, notes);
            FillBursts(d.FindProperty("breakEffects"), BreakEffects, fresh, notes);

            var smallPuff = Load<GameObject>(SmokeFolder + "Smoke_Flash_Small_Puff.prefab", notes);
            SetIfEmpty(d.FindProperty("spareLog"), Load<GameObject>(LogPath, notes));
            if (SetIfEmpty(d.FindProperty("hatPrefab"), Load<GameObject>(HatPath, notes))) notes.Add("spare magic hat set");
            SetIfEmpty(d.FindProperty("popSound"), Load<AudioClip>(Sfx + "pumpkin_explode_2.wav", notes));
            SetIfEmpty(d.FindProperty("spawnEffect"), smallPuff);
            SetIfEmpty(d.FindProperty("bossSound"), Load<AudioClip>(Sfx + "creepy_laugh_1.wav", notes));
            SetIfEmpty(d.FindProperty("chestEffect"), smallPuff);
            SetIfEmpty(d.FindProperty("chaosBoss"), doomBoss != null ? doomBoss : Load<GameObject>(ChaosBossPath, notes));
            SetIfEmpty(d.FindProperty("breakSound"), Load<AudioClip>(Sfx + "rock_pumpkin_break.wav", notes));
            SetIfEmpty(d.FindProperty("rumbleSound"), Load<AudioClip>(Sfx + "earth_rumble.wav", notes));
            SetIfEmpty(d.FindProperty("groundBreakEffect"), Load<GameObject>(SmokeFolder + "Smoke_Flash_Circle_Puff.prefab", notes));
            SetIfEmpty(d.FindProperty("groundBreakSound"), Load<AudioClip>(Sfx + "glass_break_2.wav", notes));
            SetIfEmpty(d.FindProperty("font"), Load<Object>(FontPath, notes));
            d.ApplyModifiedPropertiesWithoutUndo();
        }

        // Set by Set Up Cauldron for Fill Defence: the doom boss variant (null if it couldn't be made).
        private static GameObject doomBoss;

        // pumpkin_boss_Z_Doom: a prefab variant of pumpkin_boss_Z (so it follows every change to the boss) with only these
        // overrides: it wakes Doom Wake Up seconds after it comes up (not 33), is a chaos boss every time, and sends
        // GAMESTAGES no Win and sets no BOSS (killing it only ends the gate). Made once; an existing one is kept as it is
        // (tune it in the Inspector), apart from the Win and BOSS.
        private static GameObject MakeDoomBoss(List<string> notes)
        {
            var made = AssetDatabase.LoadAssetAtPath<GameObject>(DoomBossPath);
            if (made != null)
            {
                DoomBossEndsGateOnly(notes);
                return made;
            }
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ChaosBossPath);
            if (source == null || source.GetComponent<Game.Enemies.PumpkinBossBrain>() == null)
            {
                notes.Add("no doom boss variant (no " + ChaosBossPath + " with a Pumpkin Boss Brain): the plain boss is used");
                return null;
            }
            // Built in a preview scene, so the open scene isn't touched.
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
                var brain = new SerializedObject(instance.GetComponent<Game.Enemies.PumpkinBossBrain>());
                brain.FindProperty("wakeUpSeconds").floatValue = DoomWakeUp;
                brain.FindProperty("chaosChance").floatValue = 1f;
                brain.FindProperty("gameStagesEvent").stringValue = "";
                brain.FindProperty("gameStagesBool").stringValue = "";
                brain.ApplyModifiedPropertiesWithoutUndo();
                made = PrefabUtility.SaveAsPrefabAsset(instance, DoomBossPath);
                notes.Add(made != null ? "made " + DoomBossPath + " (variant: wakes after " + DoomWakeUp + " s, always chaos, only ends the gate)" : "doom boss variant NOT made");
            }
            catch (System.Exception e)
            {
                // Not worth stopping for: the plain boss comes out instead (it wakes after its own 33 s).
                Debug.LogWarning($"{Log} Couldn't make {DoomBossPath} ({e.Message}): the plain pumpkin_boss_Z is used.");
                notes.Add("doom boss variant NOT made: the plain boss is used");
                made = null;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
            return made;
        }

        /// <summary>
        /// pumpkin_boss_Z_Doom sends GAMESTAGES no Win and sets no BOSS (user, 2026-11-23: killing the chaos boss of a broken
        /// pot only ends the gate; Cauldron Defence then drops The_Seed and Gate Exit takes the player to the rooftop). A
        /// BOSS left true would also skip the rooftop's real boss later in the run. Re-runnable.
        /// </summary>
        internal static void DoomBossEndsGateOnly(List<string> notes)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(DoomBossPath) == null)
            {
                notes.Add("no " + DoomBossPath);
                return;
            }
            var root = PrefabUtility.LoadPrefabContents(DoomBossPath);
            try
            {
                var brain = root.GetComponent<Game.Enemies.PumpkinBossBrain>();
                if (brain == null)
                {
                    notes.Add(DoomBossPath + " has no Pumpkin Boss Brain");
                    return;
                }
                var so = new SerializedObject(brain);
                var winEvent = so.FindProperty("gameStagesEvent");
                var bossBool = so.FindProperty("gameStagesBool");
                if (string.IsNullOrEmpty(winEvent.stringValue) && string.IsNullOrEmpty(bossBool.stringValue))
                {
                    notes.Add("doom boss already only ends the gate");
                    return;
                }
                winEvent.stringValue = "";
                bossBool.stringValue = "";
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, DoomBossPath);
                notes.Add("doom boss now only ends the gate (no Win, no BOSS)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void FillBursts(SerializedProperty list, (string smoke, float scale, float height)[] bursts, bool fresh, List<string> notes)
        {
            if (!fresh && list.arraySize > 0) return;
            list.arraySize = bursts.Length;
            for (int i = 0; i < bursts.Length; i++)
            {
                var (smoke, scale, height) = bursts[i];
                var b = list.GetArrayElementAtIndex(i);
                b.FindPropertyRelative("effect").objectReferenceValue = Load<GameObject>(SmokeFolder + smoke + ".prefab", notes);
                b.FindPropertyRelative("scale").floatValue = scale;
                b.FindPropertyRelative("height").floatValue = height;
            }
        }

        // ------------------------------------------------------------------ 2. GAMESTAGES (read only)

        // Says where the gate 1 seed leads now; changes nothing (the flow is the gates' shared set-up's).
        private static void CheckStages(List<string> notes)
        {
            var gs = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(f => f.FsmName == StagesFsm && f.gameObject.scene.IsValid());
            if (gs == null)
            {
                notes.Add("no GAMESTAGES in the open scene to check");
                return;
            }
            if (!gs.Fsm.Initialized) gs.Fsm.Init(gs);
            var next1 = gs.Fsm.GetState(On1)?.Transitions.FirstOrDefault(t => t.EventName == "next1");
            string to = next1 != null ? next1.ToState : "(none)";
            notes.Add($"GAMESTAGES untouched: '{On1}' next1 -> '{to}'");
            if (to != RooftopRoute)
                Debug.LogWarning($"{Log} GAMESTAGES '{On1}' next1 goes to '{to}', not '{RooftopRoute}' (the rooftop's tree " +
                                 "defence, like every gate). This menu no longer changes it: ask for the gates' flow set-up.", gs);
        }

        // ------------------------------------------------------------------ helpers

        private static bool OpenInPrefabMode()
        {
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null || stage.assetPath != CauldronPath) return false;
            Debug.LogError($"{Log} {CauldronPath} is open in Prefab Mode: save and close it, then run this again; nothing changed.");
            return true;
        }

        // A copy of the file as it is now, once (a later run keeps the first copy: the one from before the change).
        private static void Backup(string assetPath, List<string> notes)
        {
            string folder = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                "429R_claude_backups", "2026-11-16", BackupArea);
            Directory.CreateDirectory(folder);
            string to = Path.Combine(folder, Path.GetFileName(assetPath));
            if (File.Exists(to)) return;
            File.Copy(assetPath, to);
            notes.Add("backup " + to);
        }

        private static T Load<T>(string path, List<string> notes) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                Debug.LogWarning($"{Log} Missing '{path}' (left empty).");
                notes.Add("MISSING " + path);
            }
            return asset;
        }

        // Sets a reference only if it's empty; true if it did.
        private static bool SetIfEmpty(SerializedProperty property, Object value)
        {
            if (property == null || value == null || property.objectReferenceValue != null) return false;
            property.objectReferenceValue = value;
            return true;
        }

        private static void SetArray(SerializedProperty array, Object[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
