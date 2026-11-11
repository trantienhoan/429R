using System.Collections.Generic;
using System.Linq;
using Game.Bonus;
using Game.Combat;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Bonus > Set Up Nesting Pumpkin Bonus: builds the nesting pumpkin bonus round and hooks it into
    /// GAMESTAGES after each enemy stage (EnemyManager 0 to 3). Safe to run again: it only adds what's missing.
    /// - Nesting_Pumpkin_1 (Items/Breakable, the giant): a copy of StartGamePumpkin that doesn't turn into the boss, spawns
    ///   no extra pumpkins, shows no tutorial notice and doesn't throw the player; it jiggles at every hit, bursts into
    ///   pieces and gives its EXP (gold orbs). Nesting_Pumpkin_2: a copy of it (prefab variant) that pops out, used for
    ///   the 5 pumpkins inside the giant. The Bonus Round sets each one's size (the giant is 1.5 times the start pumpkin,
    ///   each next one 7% of that smaller), health (120, then tougher: +20% of it each), jiggle (less each), candies, EXP
    ///   and laugh. (Nesting_Pumpkin_3 ... _5, Bonus_Area and Bonus_Chest_Pumpkin from the first versions aren't used.)
    /// - Bonus_Round (Bases): the Bonus Round (30 s, +3 s per pumpkin, the Candy Helmet when the giant breaks), with
    ///   Bonus_Stage_Sky (Build Stage Sky).
    /// - The bonus stages: --Bedroom--MiniGame-- (made by hand: Boss_Area and Treasure_Chest_PUMPKIN_EXTRA, whose opening
    ///   makes the giant) and --Playroom--MiniGame-- (a copy of it, made here). Tagged Untagged (EditorOnly isn't in builds).
    /// - GAMESTAGES (rooms: Storage room = EnemyManager 0, Bedroom = 1, Livingroom = 2, Playroom = 3): the bonus is only
    ///   for the Bedroom and the Playroom. Watch Bonus Round in "Enemy1 - Wait" and "Enemy 3 Stuffs"; "Bonus Round BR"
    ///   (BR_GO, then 3 - LivingRoom) and "Bonus Round PR" (LR GO 4, then Rooftop_ON) with Play Bonus Round, which switches
    ///   the stage on only when the round was earned. Each bonus state first sets the next room's checkpoint (CP LivingRoom,
    ///   CP Rooftop), so the room just cleared isn't played again from a checkpoint; the next state takes the field away
    ///   (Destroy Object $BOSS_AREA). Leftovers of the 2026-10-22 version that had a bonus after every room (Bonus Round
    ///   Mini, Bonus Round LR2 and their watches) are taken out.
    /// </summary>
    public static class BonusRoundSetupMenu
    {
        private const string StartPumpkinPath = "Assets/Prefabs/Items/Breakable/StartGamePumpkin.prefab";
        // With every hit counting, how long one fist (or thrown thing) waits before it can hurt the pumpkin again: one
        // punch counts once, and quick jabs from the same hand still all land.
        private const float EveryHitCooldown = 0.15f;
        private const string NestingPumpkinPath = "Assets/Prefabs/Items/Breakable/Nesting_Pumpkin_1.prefab";
        private const string SmallerPumpkinPath = "Assets/Prefabs/Items/Breakable/Nesting_Pumpkin_{0}.prefab";
        private const string RoundPath = "Assets/Prefabs/Bases/Bonus_Round.prefab";
        private const string SkyPath = "Assets/Prefabs/Bases/Bonus_Stage_Sky.prefab";
        // The stars and dust it's made from (see Picture Puzzle Setup Menu).
        private const string SpacePath = "Assets/Prefabs/Items/Deco/Picture_Puzzle_Space.prefab";
        private const string NoFogShader = "429/Particles/Unlit No Fog";
        private const string SoftTexture = "Assets/Lana Studio/Hyper Casual FX/Textures/Circle03.png";
        private const string StarTexture = "Assets/Lana Studio/Hyper Casual FX/Textures/Flare03.png";
        private const string RingTexture = "Assets/Lana Studio/Hyper Casual FX/Textures/Circle06.png";
        // The share of its picture's width that Ring Texture's ring takes.
        private const float RingTextureShare = 0.74f;
        // How far from the field's middle the sparkles rise and the ring lies, in metres (round its corner flames).
        private const float SparkleRing = 6.5f;
        private const float CloudStrength = 0.3f;
        private const float RingStrength = 0.55f;
        private const string BedroomStageName = "--Bedroom--MiniGame--";
        private const string PlayroomStageName = "--Playroom--MiniGame--";
        // Its name in the first version (the Playroom is the Livingroom base's second room).
        private const string OldPlayroomStageName = "--Livingroom--MiniGame--";
        private const string BlastSoundPath = "Assets/Audio/SFX/explosion-312361.wav";
        private static readonly string[] BlastEffectPaths =
        {
            "Assets/Lana Studio/Hyper Casual FX/Prefabs/Flash/Flash_round_yellow.prefab",
            "Assets/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Hit_Blast.prefab",
            "Assets/Prefabs/Effects/Smoke/Smoke_Flash_Small_Puff.prefab",
        };

        // The giant and the one every smaller pumpkin is made from: size (scale), health, candies, EXP, laugh pitch, drops
        // from the sky. Only what they have on their own: the Bonus Round sets these for each pumpkin it takes.
        private static readonly (float size, float health, int candies, int exp, float pitch, bool fromSky)[] Pumpkins =
        {
            // Health halved for whole-number damage (user, 2026-10-27).
            (2.282f, 60f, 3, 3, 1f, true),
            (2.123f, 72f, 3, 3, 1.12f, false),
        };
        private const string BonusSoundPath = "Assets/Audio/Kenney Audio/kenney_interface-sounds/Audio/confirmation_002.ogg";
        private const string HelmetSoundPath = "Assets/Audio/Kenney Audio/kenney_digital-audio/Audio/powerUp2.ogg";
        private const string CandyPath = "Assets/Prefabs/Items/Breakable/Candy_{0}.prefab";
        private const string TickPath = "Assets/Audio/Kenney Audio/kenney_interface-sounds/Audio/tick_002.ogg";
        private const string FontPath = "Assets/Fonts/socake/Socake SDF.asset";   // user 2026-11-05: Socake everywhere
        private const string PuffPath = "Assets/Prefabs/Effects/Smoke/Smoke_Flash_Small_Puff.prefab";

        [MenuItem("Tools/429 Game/Bonus/Set Up Nesting Pumpkin Bonus")]
        private static void SetUpMenu()
        {
            var notes = SetUp();
            EditorUtility.DisplayDialog("Nesting Pumpkin Bonus", string.Join("\n", notes), "OK");
        }

        [MenuItem("Tools/429 Game/Bonus/Set Up Nesting Pumpkin Bonus", true)]
        private static bool CanSetUp() => !EditorApplication.isPlaying;

        /// <summary>
        /// Tools > 429 Game > Bonus > Build Stage Sky: makes Bonus_Stage_Sky anew and updates Bonus_Round (its slot, and the
        /// tougher numbers), without touching the scene.
        /// </summary>
        [MenuItem("Tools/429 Game/Bonus/Build Stage Sky")]
        public static void BuildStageSky()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == RoundPath)
            {
                Debug.LogError($"[Bonus] Close {RoundPath} in Prefab Mode first. Nothing changed.");
                return;
            }
            MakeStageSky();
            Debug.Log("[Bonus] " + MakeRound());
        }

        /// <summary>Does the whole set-up; returns what it did.</summary>
        public static List<string> SetUp()
        {
            var notes = new List<string>();
            // The prefabs it changes must not be open in Prefab Mode (others may be).
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && (stage.assetPath.Contains("Nesting_Pumpkin") || stage.assetPath == RoundPath))
            {
                notes.Add($"Close {stage.assetPath} in Prefab Mode first.");
                return notes;
            }
            notes.Add(MakeNestingPumpkin());
            notes.Add(MakeSmallerPumpkins());
            notes.Add(MakeRound());
            notes.AddRange(HookGameStages());
            return notes;
        }

        // ---------- Nesting_Pumpkin ----------

        private static string MakeNestingPumpkin()
        {
            bool made = false;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(NestingPumpkinPath) == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(StartPumpkinPath);
                if (source == null) return "MISSING " + StartPumpkinPath;
                var copy = (GameObject)PrefabUtility.InstantiatePrefab(source, UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                copy.name = "Nesting_Pumpkin_1";
                PrefabUtility.SaveAsPrefabAsset(copy, NestingPumpkinPath);
                Object.DestroyImmediate(copy);
                made = true;
            }

            var root = PrefabUtility.LoadPrefabContents(NestingPumpkinPath);
            var changes = new List<string>();
            try
            {
                if (root.GetComponent<NestingPumpkin>() == null)
                {
                    ApplySettings(root.AddComponent<NestingPumpkin>(), 0);
                    changes.Add("Nesting Pumpkin added (the giant)");
                }
                // It jiggles at every hit that hurts (its Jiggle watches the health).
                var jiggle = root.GetComponentInChildren<Game.VFX.Jiggle>(true);
                if (jiggle != null)
                {
                    var js = new SerializedObject(jiggle);
                    if (Set(js, "jiggleWhenHurt", true)) { js.ApplyModifiedPropertiesWithoutUndo(); changes.Add("jiggles when hurt"); }
                }
                if (!root.CompareTag("Untagged")) { root.tag = "Untagged"; changes.Add("tag Untagged (not a DoorKey)"); }
                var body = root.transform.Find("pumpkin_Start");
                if (body == null) return "Nesting_Pumpkin has no pumpkin_Start";

                foreach (var fsm in body.GetComponents<PlayMakerFSM>())
                {
                    // It never turns into the pumpkin boss.
                    if (fsm.FsmName == "Wait and Transform" && fsm.enabled) { fsm.enabled = false; changes.Add("no boss after 300 s"); }
                    // No start-game tutorial notice. The notice is what moved End 1 on to the laughing (FINISHED after
                    // 1.7 s), so a Wait does that instead.
                    if (fsm.FsmName == "Health")
                    {
                        if (DisableActions(fsm, "End 1", a => a.GetType().Name == "ShowNotice")) changes.Add("no tutorial notice");
                        if (AddFinishWait(fsm, "End 1", 1.7f)) changes.Add("End 1 goes on to the laughing after 1.7 s");
                    }
                }
                // It doesn't throw the player when touched (the start pumpkin's Monster collider).
                var monster = body.Find("Jiggle/pumpkin_0/monster_collider");
                if (monster != null && monster.gameObject.activeSelf) { monster.gameObject.SetActive(false); changes.Add("no Monster collider"); }
                // Its break drops only its pieces: no extra stage pumpkins or tree pumpkins (the round drops the candies).
                var extras = body.Find("pumpkin_0_drop/pumpkin_0_extras_drop");
                if (extras != null)
                {
                    int off = 0;
                    foreach (Transform spot in extras)
                    {
                        foreach (var fsm in spot.GetComponents<PlayMakerFSM>())
                        {
                            if (fsm.enabled && SpawnsSomething(fsm)) { fsm.enabled = false; off++; }
                        }
                    }
                    if (off > 0) changes.Add($"{off} extra pumpkin spawns off");
                }
                // 5 EXP with gold orbs, the enemy hit pop, and only the player's hits count (not the chest's flying
                // pieces or the candies).
                var hits = body.GetComponent<EnemyHitDamage>();
                if (hits != null)
                {
                    var so = new SerializedObject(hits);
                    bool any = false;
                    any |= Set(so, "expOrbs", true);
                    any |= Set(so, "hideHitEffect", false);
                    any |= Set(so, "strayToughness", 8f);
                    if (any) { so.ApplyModifiedPropertiesWithoutUndo(); changes.Add("EXP orbs, hit pop on, stray bumps barely count"); }
                    // Every hit counts (user, 2026-10-29): each fist waits out its own short pause, soft punches take 1.
                    bool every = Set(so, "everyHitCounts", true);
                    every |= Set(so, "hitCooldown", EveryHitCooldown);
                    if (every) { so.ApplyModifiedPropertiesWithoutUndo(); changes.Add("every hit counts"); }
                }
                PrefabUtility.SaveAsPrefabAsset(root, NestingPumpkinPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return (made ? "Made Nesting_Pumpkin_1" : "Nesting_Pumpkin_1") + (changes.Count > 0 ? ": " + string.Join(", ", changes) : ": already set up");
        }

        // Nesting_Pumpkin_2: a prefab variant of the giant, with its own size, health, candies, EXP and laugh.
        private static string MakeSmallerPumpkins()
        {
            var giant = AssetDatabase.LoadAssetAtPath<GameObject>(NestingPumpkinPath);
            if (giant == null) return "MISSING " + NestingPumpkinPath;
            var made = new List<string>();
            for (int i = 1; i < Pumpkins.Length; i++)
            {
                string path = string.Format(SmallerPumpkinPath, i + 1);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) continue;
                var copy = (GameObject)PrefabUtility.InstantiatePrefab(giant, UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                copy.name = $"Nesting_Pumpkin_{i + 1}";
                ApplySettings(copy.GetComponent<NestingPumpkin>(), i);
                PrefabUtility.SaveAsPrefabAsset(copy, path);
                Object.DestroyImmediate(copy);
                made.Add($"_{i + 1}");
            }
            return made.Count > 0 ? "Made Nesting_Pumpkin" + string.Join(", ", made) : "Nesting_Pumpkin_2 already there";
        }

        // The table's values for pumpkin number 'index' (0 = the giant): its Nesting Pumpkin and its Enemy Hit Damage's EXP.
        private static void ApplySettings(NestingPumpkin pumpkin, int index)
        {
            var row = Pumpkins[index];
            var so = new SerializedObject(pumpkin);
            so.FindProperty("size").floatValue = row.size;
            so.FindProperty("health").floatValue = row.health;
            so.FindProperty("candies").intValue = row.candies;
            so.FindProperty("voicePitch").floatValue = row.pitch;
            so.FindProperty("dropFromTheSky").boolValue = row.fromSky;
            so.ApplyModifiedPropertiesWithoutUndo();
            var hits = pumpkin.GetComponentInChildren<EnemyHitDamage>(true);
            if (hits != null)
            {
                var hs = new SerializedObject(hits);
                Set(hs, "exp", row.exp);
                hs.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // A spot that creates something (a CreateObject), like the start pumpkin's extra pumpkins.
        private static bool SpawnsSomething(PlayMakerFSM fsm)
        {
            if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
            foreach (var state in fsm.FsmStates)
            {
                state.LoadActions();
                if (state.Actions.Any(a => a is CreateObject)) return true;
            }
            return false;
        }

        // Switches actions off in one state (only that state is saved again); false if nothing changed.
        private static bool DisableActions(PlayMakerFSM fsm, string stateName, System.Func<FsmStateAction, bool> which)
        {
            if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
            var state = fsm.Fsm.GetState(stateName);
            if (state == null) return false;
            state.LoadActions();
            if (state.ActionData.ActionCount != state.Actions.Length) return false;
            bool changed = false;
            foreach (var action in state.Actions)
            {
                if (action.Enabled && which(action)) { action.Enabled = false; changed = true; }
            }
            if (!changed) return false;
            state.SaveActions();
            EditorUtility.SetDirty(fsm);
            return true;
        }

        // A Wait that sends FINISHED at the end of a state that has nothing else to finish it; false if there is one.
        private static bool AddFinishWait(PlayMakerFSM fsm, string stateName, float seconds)
        {
            if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
            var state = fsm.Fsm.GetState(stateName);
            if (state == null) return false;
            state.LoadActions();
            if (state.ActionData.ActionCount != state.Actions.Length) return false;
            if (state.Actions.Any(a => a.Enabled && a is Wait)) return false;
            var wait = new Wait();
            wait.Reset();
            wait.time = seconds;
            wait.finishEvent = FsmEvent.Finished;
            state.Actions = state.Actions.Append(wait).ToArray();
            state.SaveActions();
            EditorUtility.SetDirty(fsm);
            return true;
        }

        private static bool Set(SerializedObject so, string field, float value)
        {
            var p = so.FindProperty(field);
            if (p == null || Mathf.Approximately(p.floatValue, value)) return false;
            p.floatValue = value;
            return true;
        }

        private static bool Set(SerializedObject so, string field, int value)
        {
            var p = so.FindProperty(field);
            if (p == null || p.intValue == value) return false;
            p.intValue = value;
            return true;
        }

        private static bool Set(SerializedObject so, string field, bool value)
        {
            var p = so.FindProperty(field);
            if (p == null || p.boolValue == value) return false;
            p.boolValue = value;
            return true;
        }

        // ---------- Bonus_Round ----------

        private static string MakeRound()
        {
            bool made = false;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(RoundPath) == null)
            {
                var temp = new GameObject("Bonus_Round");
                temp.AddComponent<BonusRound>();
                PrefabUtility.SaveAsPrefabAsset(temp, RoundPath);
                Object.DestroyImmediate(temp);
                made = true;
            }
            var root = PrefabUtility.LoadPrefabContents(RoundPath);
            var filled = new List<string>();
            try
            {
                var round = root.GetComponent<BonusRound>();
                if (round == null) { round = root.AddComponent<BonusRound>(); filled.Add("Bonus Round added"); }
                var so = new SerializedObject(round);
                var giant = AssetDatabase.LoadAssetAtPath<GameObject>(NestingPumpkinPath);
                var smaller = AssetDatabase.LoadAssetAtPath<GameObject>(string.Format(SmallerPumpkinPath, 2));
                Fill(so, "giant", giant != null ? giant.GetComponent<NestingPumpkin>() : null, filled);
                Fill(so, "smaller", smaller != null ? smaller.GetComponent<NestingPumpkin>() : null, filled);
                Fill(so, "startPumpkin", AssetDatabase.LoadAssetAtPath<GameObject>(StartPumpkinPath), filled);
                Fill(so, "bonusSound", AssetDatabase.LoadAssetAtPath<AudioClip>(BonusSoundPath), filled);
                Fill(so, "helmetSound", AssetDatabase.LoadAssetAtPath<AudioClip>(HelmetSoundPath), filled);
                Fill(so, "tickSound", AssetDatabase.LoadAssetAtPath<AudioClip>(TickPath), filled);
                Fill(so, "font", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath), filled);
                Fill(so, "puffEffect", AssetDatabase.LoadAssetAtPath<GameObject>(PuffPath), filled);
                // The first rounds' numbers (a 4x giant, 14 pumpkins, 60 health, +5 s): the new ones, once.
                if (Changed(so, "giantTimes", 4f, 1.3f) || Changed(so, "giantTimes", 1.5f, 1.3f)) filled.Add("giant 1.3x the start pumpkin");
                Fill(so, "blastSound", AssetDatabase.LoadAssetAtPath<AudioClip>(BlastSoundPath), filled);
                var blasts = so.FindProperty("blastEffects");
                if (blasts != null && blasts.arraySize == 0)
                {
                    foreach (var path in BlastEffectPaths)
                    {
                        var fx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        if (fx == null) continue;
                        blasts.arraySize++;
                        blasts.GetArrayElementAtIndex(blasts.arraySize - 1).objectReferenceValue = fx;
                    }
                    filled.Add($"{blasts.arraySize} blast effects");
                }
                if (Changed(so, "count", 14, 6)) filled.Add("6 pumpkins");
                // The time-up blast throws the player half as far and high (user, 2026-10-29: "50% force").
                if (Changed(so, "blastHeight", 1.6f, 0.8f) | Changed(so, "blastDistance", 4f, 2f)) filled.Add("time-up blast throws half as hard");
                // 9 pumpkins (user, 2026-10-25), from the giant down to the same smallest, toughest one as the 6 had.
                if (Changed(so, "count", 6, 9))
                {
                    Changed(so, "shrinkEach", 0.15f, 0.1f);
                    Changed(so, "tougherPerPumpkin", 0.2f, 0.125f);
                    Changed(so, "stifferPerPumpkin", 0.15f, 0.09f);
                    filled.Add("9 pumpkins");
                }
                // The giant bursts out (user, 2026-10-25): much quicker than the old slow grow.
                if (Changed(so, "growSeconds", 1.2f, 0.45f)) filled.Add("giant bursts out in 0.45 s");
                // The candy vacuum (user, 2026-10-25): 5 s for every pumpkin, stacking, instead of 60 s from the giant.
                if (Changed(so, "helmetSeconds", 60f, 5f)) filled.Add("candy vacuum 5 s per pumpkin");
                // Whole-number damage (user, 2026-10-27): a punch does 1-2 instead of 1-5, so half the health.
                if (Changed(so, "giantHealth", 120f, 60f)) filled.Add("giant health 60");
                if (Changed(so, "bonusSeconds", 5f, 6f)) filled.Add("+6 s per pumpkin");
                // Too easy (user, 2026-11-02; 9/9 in 47-57 s of 78 s on 2026-11-01): less time, more health.
                if (Changed(so, "bonusSeconds", 6f, 3f)) filled.Add("+3 s per pumpkin");
                if (Changed(so, "giantHealth", 60f, 70f)) filled.Add("giant health 70");
                Fill(so, "stageSky", AssetDatabase.LoadAssetAtPath<GameObject>(SkyPath), filled);
                var list = so.FindProperty("candies");
                if (list.arraySize == 0)
                {
                    foreach (int i in Game.Inventory.CandyKinds.Numbers)
                    {
                        var candy = AssetDatabase.LoadAssetAtPath<GameObject>(string.Format(CandyPath, i));
                        if (candy == null) continue;
                        list.arraySize++;
                        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = candy;
                    }
                    filled.Add($"{list.arraySize} candies");
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, RoundPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return (made ? "Made Bonus_Round" : "Bonus_Round") + (filled.Count > 0 ? ": " + string.Join(", ", filled) : ": already set up");
        }

        // ---------- Bonus_Stage_Sky ----------

        // The picture puzzle's space (its stars, clouds and dust, round the player's head in the dark) made for out in the
        // open: a dome over the field instead of a ball, seen from far off (so wider and bigger), there from the first
        // moment, stopping with the game, free of the fog and drawn in the usual order (the puzzle's are drawn after
        // everything, over its dark backdrop). Plus the sparkles off the puzzle's frame, rising round the field.
        private static GameObject MakeStageSky()
        {
            var glow = SkyMaterial("Bonus Sky Glow", SoftTexture);
            var star = SkyMaterial("Bonus Sky Star", StarTexture);
            var ring = SkyMaterial("Bonus Sky Ring", RingTexture);
            var root = PrefabUtility.LoadPrefabContents(SpacePath);
            try
            {
                root.name = "Bonus_Stage_Sky";
                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    main.useUnscaledTime = false;
                    main.prewarm = true;
                    var emission = ps.emission;
                    var renderer = ps.GetComponent<ParticleSystemRenderer>();
                    renderer.sharedMaterial = ps.name == "Bright Stars" ? star : glow;
                    switch (ps.name)
                    {
                        case "Stars":
                            main.maxParticles = 220;
                            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.26f);
                            emission.rateOverTime = 32f;
                            Dome(ps, 10f);
                            break;
                        case "Bright Stars":
                            main.maxParticles = 26;
                            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
                            emission.rateOverTime = 12f;
                            Dome(ps, 8.5f);
                            break;
                        case "Clouds":
                            main.maxParticles = 10;
                            main.startSize = new ParticleSystem.MinMaxCurve(9f, 15f);
                            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.2f, 0.85f), new Color(0.15f, 0.5f, 0.75f));
                            emission.rateOverTime = 0.9f;
                            Dome(ps, 13f);
                            FadeInOut(ps, Color.white, Color.white, CloudStrength);
                            break;
                        case "Dust":
                            main.maxParticles = 100;
                            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.075f);
                            emission.rateOverTime = 18f;
                            var shape = ps.shape;
                            shape.radius = 6f;
                            ps.transform.localPosition = Vector3.up * 1.5f;
                            break;
                    }
                }
                AddRisingSparkles(root, glow);
                // No purple ring on the floor (user, 2026-11-04: "the special stage have an purple ring on the floor, help me
                // remove it"). AddFloorRing is kept, unused, in case it's wanted back.
                return PrefabUtility.SaveAsPrefabAsset(root, SkyPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // Particles on a dome over the floor (the half of a ball under it would never show).
        private static void Dome(ParticleSystem ps, float radius)
        {
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = radius;
            shape.radiusThickness = 0f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
        }

        // Gold sparks rising off a ring round the field (the puzzle frame's Sparkles, as big as the field).
        private static void AddRisingSparkles(GameObject root, Material material)
        {
            var go = new GameObject("Rising Sparkles");
            go.transform.SetParent(root.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.maxParticles = 170;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.2f);
            var emission = ps.emission;
            emission.rateOverTime = 46f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = SparkleRing;
            shape.radiusThickness = 0.08f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            FadeInOut(ps, new Color(1f, 0.92f, 0.6f), new Color(1f, 0.55f, 0.15f), 1f);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        // A glowing purple ring on the floor where the sparks rise, like the start arena's: the field's edge.
        private static void AddFloorRing(GameObject root, Material material)
        {
            var go = new GameObject("Floor Ring");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = Vector3.up * 0.04f;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.maxParticles = 3;
            main.startLifetime = 8f;
            main.startSpeed = 0f;
            main.startSize = SparkleRing * 2f / RingTextureShare;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.62f, 0.2f, 1f), new Color(0.38f, 0.22f, 0.95f));
            var emission = ps.emission;
            emission.rateOverTime = 0.3f;
            var shape = ps.shape;
            shape.enabled = false;
            FadeInOut(ps, Color.white, Color.white, RingStrength);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            // It's far wider than the view when the player stands on it: it mustn't shrink to fit the screen.
            renderer.maxParticleSize = 100f;
        }

        private static void FadeInOut(ParticleSystem ps, Color from, Color to, float strength)
        {
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(strength, 0.3f), new GradientAlphaKey(strength * 0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
            colour.color = fade;
        }

        // An additive particle material without fog: the sky lies past where the fog starts.
        private static Material SkyMaterial(string name, string texture)
        {
            var material = DangerFlameSetupMenu.MakeMaterial(name, texture, true);
            var noFog = Shader.Find(NoFogShader);
            if (noFog != null) material.shader = noFog;
            EditorUtility.SetDirty(material);
            return material;
        }

        // A number still at the old value gets the new one; one changed by hand is kept.
        private static bool Changed(SerializedObject so, string field, float was, float now)
        {
            var p = so.FindProperty(field);
            if (p == null || !Mathf.Approximately(p.floatValue, was)) return false;
            p.floatValue = now;
            return true;
        }

        private static bool Changed(SerializedObject so, string field, int was, int now)
        {
            var p = so.FindProperty(field);
            if (p == null || p.intValue != was) return false;
            p.intValue = now;
            return true;
        }

        private static void Fill(SerializedObject so, string field, Object value, List<string> filled)
        {
            var p = so.FindProperty(field);
            if (p == null || p.objectReferenceValue != null || value == null) return;
            p.objectReferenceValue = value;
            filled.Add(field);
        }

        // ---------- GAMESTAGES ----------

        private static IEnumerable<string> HookGameStages()
        {
            var gs = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(f => f.FsmName == "GAMESTAGES" && f.gameObject.scene.IsValid());
            if (gs == null) { yield return "No GAMESTAGES in the open scene: open 2 Game Scene and run it again."; yield break; }
            var f = gs.Fsm;
            if (!f.Initialized) f.Init(gs);
            var round = AssetDatabase.LoadAssetAtPath<GameObject>(RoundPath);
            var bedroom = gs.transform.Find(BedroomStageName);
            if (bedroom == null) { yield return $"No {BedroomStageName} under {gs.name}: make it first."; yield break; }
            Undo.RecordObject(gs, "Nesting Pumpkin Bonus");

            // Only the Bedroom (EnemyManager 1) and the Playroom (EnemyManager 3) have a bonus.
            yield return Watch(gs, "Enemy1 - Wait");
            yield return Watch(gs, "Enemy 3 Stuffs");
            yield return Unwatch(gs, "Mini Enemy");
            yield return Unwatch(gs, "Enemy2 - Wait");
            yield return RemoveBonusState(gs, "Bonus Round Mini");
            yield return RemoveBonusState(gs, "Bonus Round LR2");
            foreach (var next in new[] { "2.5 - BR", "Scale LR" })
                yield return UnclearField(gs, next);
            yield return Untag(bedroom.gameObject);
            var playroom = gs.transform.Find(PlayroomStageName);
            var old = gs.transform.Find(OldPlayroomStageName);
            if (playroom == null && old != null)
            {
                Undo.RecordObject(old.gameObject, "Nesting Pumpkin Bonus");
                old.name = PlayroomStageName;
                foreach (var t in old.GetComponentsInChildren<Transform>(true))
                {
                    if (t != old) { Undo.RecordObject(t.gameObject, "Nesting Pumpkin Bonus"); t.name = t.name.Replace("Livingroom_", "Playroom_"); }
                }
                playroom = old;
                yield return $"{OldPlayroomStageName} renamed {PlayroomStageName} (it's the Playroom's)";
            }
            if (playroom == null)
            {
                playroom = CopyStage(bedroom.gameObject, PlayroomStageName).transform;
                yield return $"Made {PlayroomStageName} (a copy of {BedroomStageName})";
            }
            yield return Untag(playroom.gameObject);
            yield return RenameState(gs, "Bonus Round LR", "Bonus Round PR");
            yield return BonusState(gs, "Bonus Round BR", "BR_GO", "3 - LivingRoom", round, bedroom.gameObject, "CP LivingRoom");
            yield return BonusState(gs, "Bonus Round PR", "LR GO 4", "Rooftop_ON", round, playroom.gameObject, "CP Rooftop");
            foreach (var next in new[] { "3 - LivingRoom", "Rooftop_ON" })
                yield return ClearField(gs, next);
            EditorUtility.SetDirty(gs);
            EditorSceneManager.MarkSceneDirty(gs.gameObject.scene);
            yield return "Save the scene to keep the GAMESTAGES changes.";
        }

        // EditorOnly objects are left out of builds.
        private static string Untag(GameObject stage)
        {
            if (!stage.CompareTag("EditorOnly")) return $"{stage.name}: tag {stage.tag}";
            Undo.RecordObject(stage, "Nesting Pumpkin Bonus");
            stage.tag = "Untagged";
            return $"{stage.name}: EditorOnly -> Untagged (it's in builds now)";
        }

        // A copy of a bonus stage next to it, its spots renamed (Bedroom_ -> Playroom_). Its Create Objects make the
        // same things at the same places; move its spots to change them.
        private static GameObject CopyStage(GameObject source, string name)
        {
            var copy = Object.Instantiate(source, source.transform.parent);
            copy.name = name;
            copy.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
            foreach (var t in copy.GetComponentsInChildren<Transform>(true))
            {
                if (t != copy.transform) t.name = t.name.Replace("Bedroom_", "Playroom_");
            }
            Undo.RegisterCreatedObjectUndo(copy, "Nesting Pumpkin Bonus");
            return copy;
        }

        // Watch Bonus Round at the end of a stage's enemy-wave state.
        private static string Watch(PlayMakerFSM gs, string stateName)
        {
            var state = gs.Fsm.GetState(stateName);
            if (state == null) return $"GAMESTAGES has no '{stateName}'";
            state.LoadActions();
            if (state.ActionData.ActionCount != state.Actions.Length) return $"'{stateName}' didn't load all its actions; left alone";
            if (state.Actions.Any(a => a is WatchBonusRound)) return $"'{stateName}' already has Watch Bonus Round";
            var watch = new WatchBonusRound();
            watch.Reset();
            state.Actions = state.Actions.Append(watch).ToArray();
            state.SaveActions();
            return $"'{stateName}': Watch Bonus Round added";
        }

        // The state between 'after' and 'next' that plays the bonus round in 'stage' (or goes straight on if it wasn't
        // earned). The stage is switched on by Play Bonus Round, so an Activate Game Object of it there goes.
        private static string BonusState(PlayMakerFSM gs, string name, string after, string next, GameObject round, GameObject stage, string checkpoint)
        {
            var f = gs.Fsm;
            var from = f.GetState(after);
            if (from == null || f.GetState(next) == null) return $"GAMESTAGES has no '{after}' or '{next}'";
            var changes = new List<string>();
            var state = f.GetState(name);
            if (state == null)
            {
                var near = from.Position;
                state = new FsmState(f) { Name = name, Position = new Rect(near.x + 170f, near.y + 40f, near.width + 20f, near.height) };
                var fresh = new PlayBonusRound();
                fresh.Reset();
                state.Actions = new FsmStateAction[] { fresh };
                state.Transitions = new[] { new FsmTransition { FsmEvent = FsmEvent.Finished, ToState = next } };
                f.States = f.States.Append(state).ToArray();
                changes.Add("made");
            }
            else
            {
                state.LoadActions();
                if (state.ActionData.ActionCount != state.Actions.Length) return $"'{name}' didn't load all its actions; left alone";
            }
            var play = state.Actions.OfType<PlayBonusRound>().FirstOrDefault();
            if (play == null)
            {
                play = new PlayBonusRound();
                play.Reset();
                state.Actions = new FsmStateAction[] { play }.Concat(state.Actions).ToArray();
                changes.Add("Play Bonus Round added");
            }
            // Another bonus game put here (the Playroom's Paint Bees, Set Up Paint Bees Bonus) is kept.
            bool otherGame = play.round != null && play.round.Value != null && play.round.Value != round && play.round.Value.GetComponentInChildren<IBonusGame>(true) != null;
            if (!otherGame && (play.round == null || play.round.Value != round)) { play.round = new FsmGameObject { Value = round }; changes.Add("round Bonus_Round"); }
            if (play.stage == null || play.stage.Value != stage) { play.stage = new FsmGameObject { Value = stage }; changes.Add("stage " + stage.name); }
            // The room is cleared: a checkpoint from here on is the next room's, so it isn't played again (nor the bonus earned twice).
            if (!state.Actions.OfType<SetCheckpoint>().Any(c => c.checkpoint != null && c.checkpoint.Value == checkpoint))
            {
                var cp = new SetCheckpoint();
                cp.Reset();
                cp.checkpoint = checkpoint;
                state.Actions = new FsmStateAction[] { cp }.Concat(state.Actions).ToArray();
                changes.Add("checkpoint " + checkpoint);
            }
            int before = state.Actions.Length;
            state.Actions = state.Actions.Where(a => !(a is ActivateGameObject activate && Switches(activate, stage))).ToArray();
            if (state.Actions.Length < before) changes.Add($"its own Activate {stage.name} removed (Play Bonus Round does it when earned)");
            if (changes.Count > 0) state.SaveActions();

            // 'after' goes to the bonus state instead of straight to 'next'.
            foreach (var t in from.Transitions)
            {
                if (t.ToState == next) { t.ToState = name; changes.Add($"after '{after}'"); }
            }
            return $"'{name}': " + (changes.Count > 0 ? string.Join(", ", changes) : "already set up");
        }

        // No Watch Bonus Round in a room without a bonus.
        private static string Unwatch(PlayMakerFSM gs, string stateName)
        {
            var state = gs.Fsm.GetState(stateName);
            if (state == null) return $"GAMESTAGES has no '{stateName}'";
            state.LoadActions();
            if (!state.Actions.Any(a => a is WatchBonusRound)) return $"'{stateName}': no bonus (as it should be)";
            state.Actions = state.Actions.Where(a => !(a is WatchBonusRound)).ToArray();
            state.SaveActions();
            return $"'{stateName}': Watch Bonus Round taken out (no bonus after this room)";
        }

        // A bonus state that shouldn't be there: whatever went to it goes straight to where it went, and it's deleted.
        private static string RemoveBonusState(PlayMakerFSM gs, string name)
        {
            var f = gs.Fsm;
            var state = f.GetState(name);
            if (state == null) return $"'{name}': not there (as it should be)";
            var next = state.Transitions.FirstOrDefault()?.ToState;
            int rerouted = 0;
            foreach (var other in f.States)
            {
                foreach (var t in other.Transitions)
                {
                    if (t.ToState == name) { t.ToState = next; rerouted++; }
                }
            }
            f.States = f.States.Where(st => st != state).ToArray();
            return $"'{name}' deleted ({rerouted} transitions now go straight to '{next}')";
        }

        // Takes out a Destroy Object $BOSS_AREA from a state after a room that has no bonus any more.
        private static string UnclearField(PlayMakerFSM gs, string stateName)
        {
            var state = gs.Fsm.GetState(stateName);
            if (state == null) return $"GAMESTAGES has no '{stateName}'";
            state.LoadActions();
            if (state.ActionData.ActionCount != state.Actions.Length) return $"'{stateName}' didn't load all its actions; left alone";
            if (!state.Actions.OfType<DestroyObject>().Any(IsFieldDestroy)) return $"'{stateName}': no field to take away (as it should be)";
            state.Actions = state.Actions.Where(a => !(a is DestroyObject d && IsFieldDestroy(d))).ToArray();
            state.SaveActions();
            return $"'{stateName}': its Destroy Object $BOSS_AREA taken out";
        }

        // Renames a state and every transition to it.
        private static string RenameState(PlayMakerFSM gs, string from, string to)
        {
            var f = gs.Fsm;
            var state = f.GetState(from);
            if (state == null) return f.GetState(to) != null ? $"'{to}' already named" : $"GAMESTAGES has no '{from}'";
            state.Name = to;
            foreach (var other in f.States)
            {
                foreach (var t in other.Transitions)
                {
                    if (t.ToState == from) t.ToState = to;
                }
            }
            foreach (var t in f.GlobalTransitions)
            {
                if (t.ToState == from) t.ToState = to;
            }
            return $"'{from}' renamed '{to}' (Playroom)";
        }

        // The state after a bonus state takes the field away: a Destroy Object $BOSS_AREA (the one already in another
        // state is copied, so it points at the same global) first thing, unless it has one.
        private static string ClearField(PlayMakerFSM gs, string stateName)
        {
            var state = gs.Fsm.GetState(stateName);
            if (state == null) return $"GAMESTAGES has no '{stateName}'";
            DestroyObject model = null;
            foreach (var other in gs.Fsm.States)
            {
                if (model != null) break;
                other.LoadActions();
                model = other.Actions.OfType<DestroyObject>().FirstOrDefault(IsFieldDestroy);
            }
            if (model == null) return $"'{stateName}': no Destroy Object $BOSS_AREA anywhere to copy; add it by hand";
            state.LoadActions();
            if (state.ActionData.ActionCount != state.Actions.Length) return $"'{stateName}' didn't load all its actions; left alone";
            if (state.Actions.OfType<DestroyObject>().Any(IsFieldDestroy)) return $"'{stateName}' already destroys $BOSS_AREA";
            var destroy = new DestroyObject();
            destroy.Reset();
            destroy.gameObject = model.gameObject;
            state.Actions = new FsmStateAction[] { destroy }.Concat(state.Actions).ToArray();
            state.SaveActions();
            return $"'{stateName}': Destroy Object $BOSS_AREA added";
        }

        private static bool IsFieldDestroy(DestroyObject action) =>
            action.gameObject != null && action.gameObject.UseVariable && action.gameObject.Name == "BOSS_AREA";

        // An Activate Game Object that switches 'stage' on.
        private static bool Switches(ActivateGameObject action, GameObject stage)
        {
            var target = action.gameObject;
            return target != null && target.OwnerOption == OwnerDefaultOption.SpecifyGameObject &&
                   target.GameObject != null && target.GameObject.Value == stage && action.activate != null && action.activate.Value;
        }
    }
}
