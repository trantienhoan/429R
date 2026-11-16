using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.PlayGround.Editor
{
    /// <summary>
    /// Tools > 429 Game > PlayGround > Use Defend The Cauldron (user, 2026-11-16): gate 1 becomes "defend the cauldron"
    /// (see Cauldron Defence), and its win goes through the Daruma game straight to the Pumpkin Boss, with no rooftop and
    /// no tree defence.
    /// 1. Fire_Woods_n_Cauldron.prefab (copied to Documents/429R_claude_backups/2026-11-16/gate1_defend/ first; a later run
    ///    keeps that first copy): PlayGround Rounds is switched off (kept, with its waves), a Cauldron Health and a Cauldron
    ///    Defence are added (or switched on) and filled in: the 4 monster phases, the 3 mini bosses with their PG_ chests,
    ///    the chaos boss, the smoke, sounds and numbers below. A component that's already there keeps its numbers (tune
    ///    them in the Inspector); only its empty references are filled. The Cauldron gets its fire's whoosh if it has none.
    ///    The chaos boss is a new prefab variant, Prefabs/Enemies/pumpkin_boss_Z_Doom (made once: pumpkin_boss_Z with Wake Up
    ///    Seconds 4 instead of 33 and Chaos Chance 1), so it rises out of the broken pot at once.
    /// 2. The open scene's GAMESTAGES: three new states after the gate 1 seed, "Tween PlayGroundZ To Boss" and "Destroy
    ///    PlayGroundZ To Boss" (copies of Tween PlayGroundZ and Destroy PlayGroundZ) and "Boss PG" (Rooftop_ON PG without
    ///    the rooftop, plus the CP Boss checkpoint), which goes on to "clean it" and "BOSS". "Path 1 ON" next1 goes there
    ///    instead of "Enemy 4 - Get the Seed". Gate 2 (bowling) still uses the old states, untouched. The scene is marked
    ///    changed, never saved: save it yourself.
    /// The Daruma stage (Special_Stage_3) needs nothing: its new settings (Candies Per Kind 8, Feed Leftovers, Boss States)
    /// come from the code.
    /// "Use Cooking Rounds (Old Gate 1)" switches back: Cauldron Defence off, PlayGround Rounds on, Path 1 ON next1 back to
    /// "Enemy 4 - Get the Seed" (the new states stay, unused).
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
        private const string FontPath = "Assets/Fonts/socake/Socake SDF.asset";

        // GAMESTAGES.
        private const string StagesFsm = "GAMESTAGES";
        private const string On1 = "Path 1 ON";
        private const string OldSeed = "Enemy 4 - Get the Seed";
        private const string TweenOld = "Tween PlayGroundZ";
        private const string DestroyOld = "Destroy PlayGroundZ";
        private const string RoofPg = "Rooftop_ON PG";
        private const string LidState = "Damage The Lid";
        private const string CleanIt = "clean it";
        private const string TweenNew = "Tween PlayGroundZ To Boss";
        private const string DestroyNew = "Destroy PlayGroundZ To Boss";
        private const string BossPg = "Boss PG";
        private const string RoofSpawner = "Rooftop_Base_Pos";

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

        private static readonly MethodInfo CloneMethod =
            typeof(object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance);

        [MenuItem("Tools/429 Game/PlayGround/Use Defend The Cauldron")]
        public static void UseDefence()
        {
            if (EditorApplication.isPlaying) { Debug.LogError($"{Log} Stop play mode first; nothing changed."); return; }
            if (OpenInPrefabMode()) return;
            var notes = new List<string>();
            if (!SetUpCauldron(notes)) return;
            RouteStages(true, notes);
            AssetDatabase.SaveAssets();
            Debug.Log($"{Log} Gate 1 = defend the cauldron: {string.Join("; ", notes)}. Save the scene (2 Game Scene).");
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
            RouteStages(false, notes);
            AssetDatabase.SaveAssets();
            Debug.Log($"{Log} Gate 1 = the cooking rounds again: {string.Join("; ", notes)}. Save the scene (2 Game Scene).");
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
                d.FindProperty("cookSeconds").floatValue = 600f;
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

        // pumpkin_boss_Z_Doom: a prefab variant of pumpkin_boss_Z (so it follows every change to the boss) with only two
        // overrides: it wakes Doom Wake Up seconds after it comes up (not 33) and is a chaos boss every time. Made once;
        // an existing one is kept as it is (tune it in the Inspector).
        private static GameObject MakeDoomBoss(List<string> notes)
        {
            var made = AssetDatabase.LoadAssetAtPath<GameObject>(DoomBossPath);
            if (made != null) return made;
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
                brain.ApplyModifiedPropertiesWithoutUndo();
                made = PrefabUtility.SaveAsPrefabAsset(instance, DoomBossPath);
                notes.Add(made != null ? "made " + DoomBossPath + " (variant: wakes after " + DoomWakeUp + " s, always chaos)" : "doom boss variant NOT made");
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

        // ------------------------------------------------------------------ 2. GAMESTAGES

        // Defence: Path 1 ON next1 -> the new states (made if missing) -> clean it -> BOSS. Back: next1 -> Enemy 4.
        private static void RouteStages(bool defence, List<string> notes)
        {
            var gs = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(f => f.FsmName == StagesFsm && f.gameObject.scene.IsValid());
            if (gs == null)
            {
                Debug.LogError($"{Log} No GAMESTAGES FSM in the open scene (open 2 Game Scene): GAMESTAGES not changed.");
                notes.Add("GAMESTAGES NOT CHANGED (no scene)");
                return;
            }
            if (!gs.Fsm.Initialized) gs.Fsm.Init(gs);
            var f = gs.Fsm;
            var on1 = f.GetState(On1);
            var next1 = on1?.Transitions.FirstOrDefault(t => t.EventName == "next1");
            if (next1 == null)
            {
                Debug.LogError($"{Log} GAMESTAGES has no '{On1}' with a next1 transition: GAMESTAGES not changed.", gs);
                notes.Add("GAMESTAGES NOT CHANGED");
                return;
            }

            if (!defence)
            {
                if (next1.ToState == OldSeed) { notes.Add($"'{On1}' already goes to '{OldSeed}'"); return; }
                Undo.RecordObject(gs, "Use Cooking Rounds");
                next1.ToState = OldSeed;
                EditorUtility.SetDirty(gs);
                EditorSceneManager.MarkSceneDirty(gs.gameObject.scene);
                notes.Add($"'{On1}' next1 -> '{OldSeed}' (rooftop + tree again)");
                return;
            }

            // What the new states are copied from, all fully loaded before anything changes.
            var tweenOld = Loaded(f, TweenOld);
            var destroyOld = Loaded(f, DestroyOld);
            var roofPg = Loaded(f, RoofPg);
            var lid = Loaded(f, LidState);
            if (tweenOld == null || destroyOld == null || roofPg == null || lid == null || f.GetState(CleanIt) == null)
            {
                Debug.LogError($"{Log} GAMESTAGES is missing '{TweenOld}', '{DestroyOld}', '{RoofPg}', '{LidState}' or '{CleanIt}', or " +
                               "one of their actions couldn't be read: GAMESTAGES not changed.", gs);
                notes.Add("GAMESTAGES NOT CHANGED");
                return;
            }
            var checkpoint = lid.Actions.FirstOrDefault(a => a.GetType().Name == "SetCheckpoint");
            foreach (var name in new[] { TweenNew, DestroyNew, BossPg })
            {
                var s = f.GetState(name);
                if (s == null) continue;
                s.LoadActions();
                if (s.ActionData.ActionCount != s.Actions.Length)
                {
                    Debug.LogError($"{Log} GAMESTAGES '{name}' has {s.ActionData.ActionCount} actions saved but only {s.Actions.Length} loaded: GAMESTAGES not changed.", gs);
                    notes.Add("GAMESTAGES NOT CHANGED");
                    return;
                }
            }

            Undo.RecordObject(gs, "Use Defend The Cauldron");
            var changed = new List<FsmState>();
            int made = 0;
            var near = roofPg.Position;

            // The PlayGround sinks, as in Tween PlayGroundZ.
            if (f.GetState(TweenNew) == null)
            {
                changed.Add(NewState(f, TweenNew, near, 260f, DestroyNew, tweenOld.Actions.Select(Clone).ToArray()));
                made++;
            }
            // It's gone with everything it spawned, as in Destroy PlayGroundZ.
            if (f.GetState(DestroyNew) == null)
            {
                changed.Add(NewState(f, DestroyNew, near, 320f, BossPg, destroyOld.Actions.Select(Clone).ToArray()));
                made++;
            }
            // Rooftop_ON PG without the rooftop (the rooms go, the sky comes back, smoke), and the boss checkpoint as
            // Damage The Lid sets it; then clean it (the Fairy Shield) and BOSS.
            if (f.GetState(BossPg) == null)
            {
                var actions = new List<FsmStateAction>();
                if (checkpoint != null) actions.Add(Clone(checkpoint));
                foreach (var a in roofPg.Actions)
                {
                    if (a is ActivateGameObject act && act.gameObject != null && act.gameObject.GameObject != null &&
                        act.gameObject.GameObject.Value != null && act.gameObject.GameObject.Value.name == RoofSpawner) continue;
                    actions.Add(Clone(a));
                }
                changed.Add(NewState(f, BossPg, near, 380f, CleanIt, actions.ToArray()));
                made++;
            }
            bool rerouted = next1.ToState != TweenNew;
            if (rerouted) next1.ToState = TweenNew;

            if (made == 0 && !rerouted)
            {
                notes.Add("GAMESTAGES already goes from the gate 1 seed straight to the boss");
                return;
            }
            foreach (var s in changed) s.SaveActions();
            EditorUtility.SetDirty(gs);
            EditorSceneManager.MarkSceneDirty(gs.gameObject.scene);
            notes.Add($"GAMESTAGES: {made} new state(s) ('{TweenNew}' -> '{DestroyNew}' -> '{BossPg}' -> '{CleanIt}'), '{On1}' next1 -> '{TweenNew}'" +
                      (checkpoint == null ? $" (no Set Checkpoint found in '{LidState}': none in '{BossPg}')" : ""));
        }

        // The state with its actions loaded, or null if it's missing or an action couldn't be read (saving would drop it).
        private static FsmState Loaded(Fsm f, string name)
        {
            var state = f.GetState(name);
            if (state == null) return null;
            state.LoadActions();
            return state.ActionData.ActionCount == state.Actions.Length ? state : null;
        }

        private static FsmState NewState(Fsm f, string name, Rect near, float down, string finishedTo, FsmStateAction[] actions)
        {
            var state = new FsmState(f)
            {
                Name = name,
                Position = new Rect(near.x + 220f, near.y + down, near.width, near.height),
                Actions = actions,
                Transitions = new[] { new FsmTransition { FsmEvent = FsmEvent.Finished, ToState = finishedTo } },
            };
            f.States = f.States.Append(state).ToArray();
            return state;
        }

        // A copy of an action with the same settings (shallow: fields changed later get new objects).
        private static FsmStateAction Clone(FsmStateAction action) => (FsmStateAction)CloneMethod.Invoke(action, null);

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
