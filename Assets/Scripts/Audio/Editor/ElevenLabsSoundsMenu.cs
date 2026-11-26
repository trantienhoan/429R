using System;
using System.Collections.Generic;
using System.Linq;
using Game.Combat;
using Game.Enemies;
using Game.Menus;
using Game.Story;
using Game.Weapons;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Audio > Wire ElevenLabs Sounds (P1) (user 2026-11-25: "Yes, fix and wire"; Exports/Audio/
    /// AUDIO_PLAN.md and P1_PROMPTS.md). Puts the user's new sounds (Assets/Audio/SFX) in their slots. Re-runnable: each
    /// step checks first and changes nothing already right; a missing file is skipped with a warning (bee_buzz_loop,
    /// round_start, round_won and time_up_buzzer are still to come: run it again once they are in).
    /// Assets and prefabs are saved here. The scene parts (the health HUD's heartbeat, the hurt sounds on the XR Origin,
    /// the notice's pop, the pause menu, the stage-change smoke in GAMESTAGES) leave the scene dirty: save the scene.
    /// </summary>
    public static class ElevenLabsSoundsMenu
    {
        private const string Tag = "[ElevenLabs Sounds]";
        private const string Sfx = "Assets/Audio/SFX/";

        // The new files: their import settings (preloaded like the game's other short sounds; 3D ones made mono).
        private static readonly string[] NewFiles =
        {
            "bat_hit_1.wav", "bat_hit_2.wav", "bat_hit_3.wav", "bomb_beep2.wav", "broom_hit_1.wav", "broom_hit_2.wav",
            "bug_skitter_loop_1.wav", "bug_skitter_loop_2.wav", "bee_buzz_loop.wav", "explosion_big_1.wav",
            "explosion_big_2.wav", "explosion_medium_1.wav", "explosion_medium_2.wav", "explosion_small_1.wav",
            "explosion_small_2.wav", "fairy_teleport_blink_1.wav", "fairy_teleport_blink_2.wav", "heartbeat_loop.wav",
            "player_hurt_heavy_1.wav", "player_hurt_heavy_2.wav", "player_hurt_light_1.wav", "player_hurt_light_2.wav",
            "player_hurt_light_3.wav", "stage_swap_poof_1.wav", "stage_swap_poof_2.wav", "ui_menu_close.wav",
            "ui_menu_open.wav", "ui_notice_pop.wav", "weapon_swing_1.wav", "weapon_swing_2.wav", "weapon_swing_3.wav",
            "round_start.wav", "round_won.wav", "time_up_buzzer.wav",
            "fairy_please_find_me.wav", "fairy_please_find_me_2(Iam here).wav", "fairy_good_luck.wav",
            "fairy_have_fun.wav", "fairy_be_careful_out_there.wav", "fairy_hello.mp3", "fairy_hello_there.wav",
            "fairy_thank_you.wav", "fairy_talk_1.mp3", "fairy_talk_2.mp3", "voice_fight.mp3",
            "voice_protected_the_green.mp3",
            // The new laughing pumpkin voices (2026-11-26).
            "pumpkin_laugh_1.mp3", "pumpkin_laugh_2.mp3", "pumpkin_laugh_3.mp3", "pumpkin_laugh_4.mp3",
            "laughing_pumpkin_1.mp3", "laughing_pumpkin_2.mp3", "laughing_pumpkin_3.mp3", "laughing_pumpkin_4.mp3",
            "laughing_pumpkin_5.mp3", "pumpkin_boss_laugh.mp3", "pumpkin_boss_laugh_2.mp3",
            "player_death_hee_hee_1.mp3", "player_death_hee_hee_2.mp3",
        };
        // Heard in the head (not placed in the world): they keep their stereo.
        private static readonly string[] KeepStereo =
            { "ui_menu_close.wav", "ui_menu_open.wav", "ui_notice_pop.wav", "heartbeat_loop.wav",
              "player_death_hee_hee_1.mp3", "player_death_hee_hee_2.mp3" };

        // The new laughing pumpkin voices (user 2026-11-26, Exports/Audio/PUMPKIN_LAUGH_PROMPTS.md), saved as .mp3 next to
        // the old .wav laughs: every prefab that played an old one plays the new one (the boss's two loud copies too).
        private static readonly (string old, string now)[] LaughSwaps =
        {
            ("pumpkin_laugh_1.wav", "pumpkin_laugh_1.mp3"), ("pumpkin_laugh_2.wav", "pumpkin_laugh_2.mp3"),
            ("pumpkin_laugh_3.wav", "pumpkin_laugh_3.mp3"), ("pumpkin_laugh_4.wav", "pumpkin_laugh_4.mp3"),
            ("laughing_pumpkin_1.wav", "laughing_pumpkin_1.mp3"), ("laughing_pumpkin_2.wav", "laughing_pumpkin_2.mp3"),
            ("laughing_pumpkin_3.wav", "laughing_pumpkin_3.mp3"), ("laughing_pumpkin_4.wav", "laughing_pumpkin_4.mp3"),
            ("laughing_pumpkin_5.wav", "laughing_pumpkin_5.mp3"),
            ("pumpkin_boss_laugh.wav", "pumpkin_boss_laugh.mp3"), ("pumpkin_boss_laugh_2.wav", "pumpkin_boss_laugh_2.mp3"),
            ("pumpkin_boss_laugh_loud.wav", "pumpkin_boss_laugh.mp3"), ("pumpkin_boss_laugh_2_loud.wav", "pumpkin_boss_laugh_2.mp3"),
        };

        private static List<string> done;
        private static bool sceneChanged;

        [MenuItem("Tools/429 Game/Audio/Wire ElevenLabs Sounds (P1)")]
        public static void Run()
        {
            done = new List<string>();
            sceneChanged = false;

            ImportSettings();
            FairyLines();
            SeedVoices();
            FairyFsm();
            BugLoops();
            GateRounds();
            Weapons();
            Explosions();
            PlayerSounds();
            StageSmoke();
            Laughs();
            DeathLaughs();

            AssetDatabase.SaveAssets();
            if (sceneChanged) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"{Tag} " + (done.Count > 0 ? string.Join("; ", done) : "everything was already wired") +
                      (sceneChanged ? ". SAVE THE SCENE." : "."));
        }

        [MenuItem("Tools/429 Game/Audio/Wire ElevenLabs Sounds (P1)", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // ---- 1. Import settings ----

        private static void ImportSettings()
        {
            int changed = 0;
            foreach (var file in NewFiles)
            {
                if (!(AssetImporter.GetAtPath(Sfx + file) is AudioImporter importer)) continue;
                bool dirty = false;
                bool mono = !KeepStereo.Contains(file);
                if (importer.forceToMono != mono)
                {
                    importer.forceToMono = mono;
                    dirty = true;
                }
                var settings = importer.defaultSampleSettings;
                if (!settings.preloadAudioData)
                {
                    settings.preloadAudioData = true;
                    importer.defaultSampleSettings = settings;
                    dirty = true;
                }
                if (!dirty) continue;
                importer.SaveAndReimport();
                changed++;
            }
            if (changed > 0) done.Add($"{changed} new clips preloaded (3D ones mono)");
        }

        // ---- 2. The Fairy's lines and teleport (Resources/Story Fairy Settings) ----

        private static void FairyLines()
        {
            var settings = AssetDatabase.LoadAssetAtPath<StoryFairySettings>("Assets/Resources/Story Fairy Settings.asset");
            if (settings == null)
            {
                Debug.LogWarning($"{Tag} No Assets/Resources/Story Fairy Settings.asset: run Tools > 429 Game > Story > Set Up Fairy Acts first.");
                return;
            }
            var so = new SerializedObject(settings);
            Fill(so, "teleportSounds", "Fairy teleport", "fairy_teleport_blink_1.wav", "fairy_teleport_blink_2.wav");
            Fill(so, "sendOffLines", "Fairy send-off", "fairy_good_luck.wav", "fairy_have_fun.wav", "fairy_be_careful_out_there.wav");
            Fill(so, "helloLines", "Fairy hello", "fairy_hello.mp3", "fairy_hello_there.wav");
            Fill(so, "thankYouLines", "Fairy thank you", "fairy_thank_you.wav");
            Fill(so, "talkLines", "Fairy talk", "fairy_talk_1.mp3", "fairy_talk_2.mp3");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- 3. The seed voices (Resources/Story Seed Settings) ----

        private static void SeedVoices()
        {
            var settings = AssetDatabase.LoadAssetAtPath<StorySeedSettings>("Assets/Resources/Story Seed Settings.asset");
            if (settings == null)
            {
                Debug.LogWarning($"{Tag} No Assets/Resources/Story Seed Settings.asset (skipped).");
                return;
            }
            var so = new SerializedObject(settings);
            One(so, "voice", "seed voice", "fairy_please_find_me.wav");
            Fill(so, "moreVoices", "seed voice 2nd take", "fairy_please_find_me_2(Iam here).wav");
            One(so, "fightVoice", "Fight voice", "voice_fight.mp3");
            One(so, "endVoice", "ending voice", "voice_protected_the_green.mp3");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- 4. The Fairy after the final boss: thank you while she cheers, her talk before she goes (FairyHealth) ----

        private static void FairyFsm()
        {
            EditPrefab("Assets/Prefabs/Player/Fairy.prefab", root =>
            {
                var fsm = root.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "FairyHealth");
                if (fsm == null) return "no FairyHealth FSM";
                fsm.Fsm.Init(fsm);
                var what = new List<string>();
                foreach (var st in fsm.Fsm.States)
                {
                    if (st.Name != "Win" && st.Name != "Win 2") continue;
                    st.LoadActions();
                    bool changed = false;
                    if (st.Name == "Win" && !st.Actions.OfType<StoryFairySay>().Any())
                    {
                        var say = NewSay(StoryFairySay.Lines.ThankYou, 0.6f, 0f, null);
                        st.Actions = st.Actions.Concat(new FsmStateAction[] { say }).ToArray();
                        what.Add("Win says thank you");
                        changed = true;
                    }
                    if (st.Name == "Win 2")
                    {
                        // Her talk lines take the 3 s talk wait's place: the state waits for them (3 s at least).
                        var wait = st.Actions.OfType<Wait>().FirstOrDefault(w => w.Name == "Fairy Talk Time");
                        if (wait != null)
                        {
                            st.Actions = st.Actions.Where(a => a != wait).ToArray();
                            changed = true;
                        }
                        if (!st.Actions.OfType<StoryFairySay>().Any())
                        {
                            var say = NewSay(StoryFairySay.Lines.Talk, 0.3f, 3f, FsmEvent.Finished);
                            st.Actions = st.Actions.Concat(new FsmStateAction[] { say }).ToArray();
                            what.Add("Win 2 talks (waits for her lines)");
                            changed = true;
                        }
                    }
                    if (!changed) continue;
                    st.SaveActions();
                    EditorUtility.SetDirty(fsm);
                }
                return what.Count > 0 ? "Fairy: " + string.Join(", ", what) : null;
            });
        }

        private static StoryFairySay NewSay(StoryFairySay.Lines lines, float delay, float atLeast, FsmEvent finish)
        {
            var say = new StoryFairySay();
            say.Reset();
            say.gameObject = new FsmOwnerDefault();
            say.lines = lines;
            say.delay = delay;
            say.atLeast = atLeast;
            say.finishEvent = finish;
            return say;
        }

        // ---- 5. Bugs: skitter while walking, buzz while flying (Resources/Bug Sounds) ----

        private static void BugLoops()
        {
            const string path = "Assets/Resources/Bug Sounds.asset";
            var settings = AssetDatabase.LoadAssetAtPath<BugSoundSettings>(path);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<BugSoundSettings>();
                AssetDatabase.CreateAsset(settings, path);
                done.Add("made " + path);
            }
            var so = new SerializedObject(settings);
            Fill(so, "walkLoops", "bug skitter", "bug_skitter_loop_1.wav", "bug_skitter_loop_2.wav");
            Fill(so, "flyLoops", "bee buzz", "bee_buzz_loop.wav");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- 5b. Gate rounds (GateRun: gate 1 PlayGround, gate 2 Slime Bowling; gate 3 has its own flow) ----

        private static void GateRounds()
        {
            foreach (var path in new[] { "Assets/Prefabs/Bases/Play_Ground_Z.prefab", "Assets/Prefabs/Bowling/Bowling_Arena.prefab" })
            {
                EditPrefab(path, root =>
                {
                    var run = root.GetComponentInChildren<Game.Gates.GateRun>(true);
                    if (run == null) return "no GateRun";
                    var so = new SerializedObject(run);
                    bool a = One(so, "roundStartSound", root.name + " round start", "round_start.wav");
                    bool b = One(so, "roundWonSound", root.name + " round won", "round_won.wav");
                    bool c = One(so, "roundTimeUpSound", root.name + " time up", "time_up_buzzer.wav");
                    so.ApplyModifiedPropertiesWithoutUndo();
                    return a || b || c ? root.name + " saved" : null;
                });
            }
        }

        // ---- 6. Weapons ----

        // Held weapons that are swung (bases first: the bat and the pan were made from the broom).
        private static readonly string[] SwungWeapons =
        {
            "Assets/Prefabs/Weapon/Hammer_Broom.prefab", "Assets/Prefabs/Weapon/Weapon_Baseball_Bat.prefab",
            "Assets/Prefabs/Weapon/Weapon_Frying_Pan.prefab", "Assets/Prefabs/Weapon/Hammer_Boong.prefab",
            "Assets/Prefabs/Weapon/Hammer_Boong 2ND.prefab", "Assets/Prefabs/Weapon/Hammer_Boong_Bone.prefab",
            "Assets/Prefabs/Weapon/Hammer_Pillow.prefab", "Assets/Prefabs/Weapon/Hammer_Tool.prefab",
        };

        private static void Weapons()
        {
            foreach (var path in SwungWeapons)
            {
                EditPrefab(path, root =>
                {
                    var grab = root.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>(true);
                    if (grab == null) return "no XR Grab Interactable";
                    bool changed = false;
                    var swing = grab.GetComponent<WeaponSwingSound>();
                    if (swing == null)
                    {
                        swing = grab.gameObject.AddComponent<WeaponSwingSound>();
                        changed = true;
                    }
                    var so = new SerializedObject(swing);
                    changed |= Fill(so, "swings", root.name + " swing", "weapon_swing_1.wav", "weapon_swing_2.wav", "weapon_swing_3.wav");
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changed |= HitSounds(root);
                    return changed ? root.name + " saved" : null;
                });
            }
        }

        // Its hits on enemies (Weapon Power's Hit Sounds): the bat's and the broom's own.
        private static bool HitSounds(GameObject root)
        {
            string[] files = root.name == "Weapon_Baseball_Bat" ? new[] { "bat_hit_1.wav", "bat_hit_2.wav", "bat_hit_3.wav" }
                : root.name == "Hammer_Broom" ? new[] { "broom_hit_1.wav", "broom_hit_2.wav" }
                : null;
            if (files == null) return false;
            var power = root.GetComponentInChildren<WeaponPower>(true);
            if (power == null)
            {
                Debug.LogWarning($"{Tag} {root.name} has no Weapon Power (hit sounds skipped).");
                return false;
            }
            var so = new SerializedObject(power);
            bool changed = Fill(so, "hitSounds", root.name + " hits", files);
            so.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        // ---- 7. Explosions and the bomb beep ----

        // Every explosion used the one old bang (explosion-312361.wav, single-clip slots): now a small, medium or big one
        // by what blows up (AUDIO_PLAN.md P1). Takes _1/_2 go to different things so they don't all sound the same.
        private const string Breakable = "Assets/Prefabs/Items/Breakable/";
        private const string Smarter = "Assets/Prefabs/Enemies/Enemies_Smarter/";

        private static void Explosions()
        {
            var old = Clip("explosion-312361.wav", false);
            if (old != null)
            {
                // Small: books, flip-flops, the Elon Boong's smoke ball / bottles, the glow jar, box bombs.
                Swap(old, "explosion_small_1.wav", Numbered(Breakable + "Bedroom_Book_", 1, 13).Concat(new[]
                {
                    "Assets/Prefabs/Weapon/Flip_Flop_L.prefab", "Assets/Prefabs/Weapon/Flip_Flop_R.prefab",
                    "Assets/Prefabs/Weapon/Flip_Flop2_L.prefab", "Assets/Prefabs/Weapon/Flip_Flop2_R.prefab",
                    "Assets/Prefabs/Weapon/Elon_Bullet.prefab",
                }));
                Swap(old, "explosion_small_2.wav", Numbered(Breakable + "Empty_Bottle_", 1, 5)
                    .Concat(new[] { "Assets/Prefabs/Lair/Lair_Glow_Jar.prefab" })
                    .Concat(Numbered(Breakable + "Paper_Box_", 1, 5)));
                // Medium: the held runner pumpkins (bases before their Chaos copies), the Boong gun's overheat / the
                // kamikaze bees, the bowling pumpkin.
                Swap(old, "explosion_medium_1.wav", new[] { "0", "00", "1", "2", "4", "6", "1_Chaos", "2_Chaos", "4_Chaos", "6_Chaos" }
                    .Select(n => Breakable + "Pumpkin_" + n + ".prefab")
                    .Concat(new[] { "Assets/Prefabs/Weapon/Boong_Gun.prefab" }));
                Swap(old, "explosion_medium_2.wav", new[]
                {
                    Smarter + "Bee_Normal.prefab", Smarter + "Bee_Small.prefab", "Assets/Prefabs/Enemies/Bowling_Pumpkin_Enemy.prefab",
                });
                // Big: the bee boss's Ticking Bomb (and the yellow bomb slime that copies it).
                Swap(old, "explosion_big_1.wav", new[]
                {
                    Smarter + "Bee_Boss_PumpkinButt.prefab", "Assets/Prefabs/Enemies/PLAYGROUND/Slime_Big_Yellow.prefab",
                });
                // The last three (user 2026-11-25 "Yes, switch them"): a smoked boong blowing up = medium (like the Boong
                // gun), the bonus round's time-up blast and the giant slime's burst = big.
                Swap(old, "explosion_medium_1.wav", new[] { "Bus_Boong", "Elon_Boong", "Hammer_Boong", "Hammer_Boong 2ND", "Hammer_Boong_Bone" }
                    .Select(n => "Assets/Prefabs/Weapon/" + n + ".prefab"));
                Swap(old, "explosion_big_2.wav", new[] { "Assets/Prefabs/Bases/Bonus_Round.prefab" });
                Swap(old, "explosion_big_1.wav", new[] { "Assets/Prefabs/Bonus/Paint_Giant_Slime.prefab" });
                BlastSkills(old);
                ExplosiveFists(old);
            }
            BombBeep();
        }

        private static IEnumerable<string> Numbered(string start, int from, int to)
        {
            for (int i = from; i <= to; i++) yield return start + i + ".prefab";
        }

        // In each prefab, the old bang -> this clip (only that clip's slots change).
        private static void Swap(Object old, string file, IEnumerable<string> paths)
        {
            var clip = Clip(file);
            if (clip == null) return;
            foreach (var path in paths)
                EditPrefab(path, root => SwapClip(root, old, clip) > 0 ? $"{root.name} bang = {clip.name}" : null);
        }

        // Blast Skills: thrown things (Explosive Objects, Bomber Man) small, Weapon Master big.
        private static void BlastSkills(Object old)
        {
            var skills = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Resources/Blast Skills.asset");
            if (skills == null)
            {
                Debug.LogWarning($"{Tag} No Assets/Resources/Blast Skills.asset (skipped).");
                return;
            }
            var so = new SerializedObject(skills);
            foreach (var (field, file) in new[] { ("thrownSound", "explosion_small_2.wav"), ("masterSound", "explosion_big_1.wav") })
            {
                var p = so.FindProperty(field);
                if (p == null || p.objectReferenceValue != old) continue;
                One(so, field, "Blast Skills " + field, file);
            }
            if (so.ApplyModifiedPropertiesWithoutUndo()) EditorUtility.SetDirty(skills);
        }

        // The Explosive Fist (and the Elon Boong buff's punch blast): Fist on both controllers in the scene.
        private static void ExplosiveFists(Object old)
        {
            var big = Clip("explosion_big_2.wav");
            if (big == null) return;
            foreach (var fist in Resources.FindObjectsOfTypeAll<Fist>())
            {
                if (fist == null || EditorUtility.IsPersistent(fist) || !fist.gameObject.scene.IsValid()) continue;
                var so = new SerializedObject(fist);
                var p = so.FindProperty("boongBlastSound");
                if (p == null || p.objectReferenceValue != old) continue;
                p.objectReferenceValue = big;
                so.ApplyModifiedPropertiesWithoutUndo();
                Scene(fist);
                done.Add($"{fist.name} Explosive Fist bang = {big.name}");
            }
        }

        // The made-up bomb_beep.wav (synthesized 10-24, against the user's rule) -> the user's bomb_beep2 wherever it is
        // used: the bee boss's Ticking Bomb, the yellow bomb slime, the hanging pictures' puzzle tick.
        private static void BombBeep()
        {
            var old = Clip("bomb_beep.wav", false);
            var beep = Clip("bomb_beep2.wav");
            if (old == null || beep == null) return;
            foreach (var path in PrefabsUsing(old))
                EditPrefab(path, root =>
                {
                    int n = SwapClip(root, old, beep);
                    return n > 0 ? $"{root.name}: bomb beep = bomb_beep2 ({n})" : null;
                });
        }

        // Every prefab whose file points at this clip.
        private static IEnumerable<string> PrefabsUsing(Object asset)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                string text = System.IO.File.ReadAllText(path);
                if (text.Contains(guid)) yield return path;
            }
        }

        // Points every reference to Old on every component of the prefab (PlayMaker actions too) at New.
        private static int SwapClip(GameObject root, Object old, Object replacement)
        {
            int count = 0;
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                var so = new SerializedObject(component);
                var p = so.GetIterator();
                bool changed = false;
                while (p.Next(true))
                {
                    if (p.propertyType != SerializedPropertyType.ObjectReference || p.objectReferenceValue != old) continue;
                    p.objectReferenceValue = replacement;
                    changed = true;
                    count++;
                }
                if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            }
            return count;
        }

        // ---- 8. The player (scene): heartbeat, hurt, notice pop, pause menu ----

        private static void PlayerSounds()
        {
            var hud = FindInScene<PlayerHealthHUD>();
            if (hud == null) Debug.LogWarning($"{Tag} No Player Health HUD in the open scene (heartbeat skipped).");
            else
            {
                var so = new SerializedObject(hud);
                bool a = One(so, "heartbeatSound", "heartbeat", "heartbeat_loop.wav");
                bool b = Float(so, "heartbeatClipBeat", 0.95f);
                so.ApplyModifiedPropertiesWithoutUndo();
                if (a || b) Scene(hud);
            }

            var vignette = FindInScene<DamageVignette>();
            if (vignette == null) Debug.LogWarning($"{Tag} No Damage Vignette (XR Origin) in the open scene (hurt sounds skipped).");
            else
            {
                var hurt = vignette.GetComponent<PlayerHurtSounds>();
                if (hurt == null)
                {
                    hurt = vignette.gameObject.AddComponent<PlayerHurtSounds>();
                    done.Add("Player Hurt Sounds added to " + vignette.name);
                    Scene(hurt);
                }
                var so = new SerializedObject(hurt);
                bool a = Fill(so, "lightHurts", "player hurt light", "player_hurt_light_1.wav", "player_hurt_light_2.wav", "player_hurt_light_3.wav");
                bool b = Fill(so, "heavyHurts", "player hurt heavy", "player_hurt_heavy_1.wav", "player_hurt_heavy_2.wav");
                // Measured 2026-11-25: Damage takes 8, DamageHeavy 42.
                bool c = Float(so, "heavyFrom", 15f);
                so.ApplyModifiedPropertiesWithoutUndo();
                if (a || b || c) Scene(hurt);
            }

            var notice = FindInScene<TutorialNotice>();
            if (notice == null) Debug.LogWarning($"{Tag} No Tutorial Notice in the open scene (pop skipped).");
            else
            {
                var so = new SerializedObject(notice);
                bool a = One(so, "showSound", "notice pop", "ui_notice_pop.wav");
                so.ApplyModifiedPropertiesWithoutUndo();
                if (a) Scene(notice);
            }

            var menu = FindInScene<PauseMenu>();
            if (menu == null) Debug.LogWarning($"{Tag} No Pause Menu in the open scene (menu sounds skipped).");
            else
            {
                var so = new SerializedObject(menu);
                bool a = One(so, "openSound", "menu open", "ui_menu_open.wav");
                bool b = One(so, "closeSound", "menu close", "ui_menu_close.wav");
                so.ApplyModifiedPropertiesWithoutUndo();
                if (a || b) Scene(menu);
            }
        }

        // ---- 9. The smoke at a stage change (GAMESTAGES, scene) ----

        // GAMESTAGES switches on 4 copies of Smoke_Flash_Five_Puffs at the stage changes (Smoke_Blast, _Brown, _Green under
        // -GAMESTAGES, one under Skydome_Base_Pos; 25 states). Each copy's FSM "Init" plays bubble_pop_1 on Room_Bases:
        // that pop -> stage_swap_poof_1/2 (taking turns). Real breaks (Wall_Shatter_1/2 in the drop prefabs) stay.
        private static void StageSmoke()
        {
            const string smokePath = "Assets/Prefabs/Effects/Smoke/Smoke_Flash_Five_Puffs.prefab";
            var pop = Clip("bubble_pop_1.wav", false);
            var poofs = new[] { Clip("stage_swap_poof_1.wav"), Clip("stage_swap_poof_2.wav") };
            if (pop == null || poofs.Any(p => p == null)) return;
            int n = 0;
            var names = new List<string>();
            foreach (var fsm in Resources.FindObjectsOfTypeAll<PlayMakerFSM>().OrderBy(f => f != null ? f.name : ""))
            {
                if (fsm == null || EditorUtility.IsPersistent(fsm) || !fsm.gameObject.scene.IsValid() || fsm.FsmName != "FSM") continue;
                var go = fsm.gameObject;
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(go) ||
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) != smokePath) continue;
                if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
                var st = fsm.Fsm.GetState("Init");
                if (st == null) continue;
                st.LoadActions();
                bool changed = false;
                foreach (var play in st.Actions.OfType<AudioPlay>())
                {
                    if (play.oneShotClip == null || play.oneShotClip.UseVariable) continue;
                    var clip = play.oneShotClip.Value as AudioClip;
                    if (poofs.Contains(clip))
                    {
                        n++;
                        continue;
                    }
                    if (clip != pop) continue;
                    play.oneShotClip.Value = poofs[n % 2];
                    n++;
                    changed = true;
                }
                if (!changed) continue;
                st.SaveActions();
                Scene(fsm);
                names.Add(go.name);
            }
            if (names.Count > 0) done.Add($"stage-change poof on {names.Count} smokes ({string.Join(", ", names)})");
        }

        // ---- 10. The new laughing pumpkin voices ----

        private static void Laughs()
        {
            var pairs = new List<(Object old, Object now)>();
            foreach (var (oldFile, newFile) in LaughSwaps)
            {
                var now = Clip(newFile, false);
                var old = Clip(oldFile, false);
                if (now == null)
                {
                    Debug.LogWarning($"{Tag} Missing {Sfx}{newFile}: {oldFile} stays.");
                    continue;
                }
                if (old != null) pairs.Add((old, now));
            }
            if (pairs.Count == 0) return;
            var guids = pairs.Select(p => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(p.old))).ToArray();
            var paths = new List<string>();
            foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                string text = System.IO.File.ReadAllText(path);
                if (guids.Any(text.Contains)) paths.Add(path);
            }
            // Bases first, so their variants take the new laugh from them (no needless overrides).
            paths = paths.OrderBy(p => PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadAssetAtPath<GameObject>(p)) == PrefabAssetType.Variant ? 1 : 0)
                .ThenBy(p => p).ToList();
            int prefabs = 0, slots = 0;
            foreach (var path in paths)
            {
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null && stage.assetPath == path)
                {
                    Debug.LogWarning($"{Tag} {path} is open in Prefab Mode: close it and run this again (skipped).");
                    continue;
                }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int n = 0;
                    foreach (var (old, now) in pairs) n += SwapClip(root, old, now);
                    if (n == 0) continue;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    prefabs++;
                    slots += n;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            if (prefabs > 0) done.Add($"new pumpkin laughs in {prefabs} prefabs ({slots} slots)");
        }

        // When the player dies, the laughing pumpkin's HEE HEE (Story Laugh Settings, Game Over Hold plays it).
        private static void DeathLaughs()
        {
            var settings = AssetDatabase.LoadAssetAtPath<StoryLaughSettings>("Assets/Resources/Story Laugh Settings.asset");
            if (settings == null)
            {
                Debug.LogWarning($"{Tag} No Assets/Resources/Story Laugh Settings.asset (death laugh skipped).");
                return;
            }
            var so = new SerializedObject(settings);
            Fill(so, "deathLaughs", "death HEE HEE", "player_death_hee_hee_1.mp3", "player_death_hee_hee_2.mp3");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- Helpers ----

        private static AudioClip Clip(string file, bool warn = true)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + file);
            if (clip == null && warn) Debug.LogWarning($"{Tag} Missing {Sfx}{file} (that slot is left as it is).");
            return clip;
        }

        // An array slot = these clips (the ones that exist). False when nothing changed or none exist.
        private static bool Fill(SerializedObject so, string field, string what, params string[] files)
        {
            var p = so.FindProperty(field);
            if (p == null || !p.isArray)
            {
                Debug.LogWarning($"{Tag} {so.targetObject.name} has no list '{field}' (skipped).");
                return false;
            }
            var clips = files.Select(f => Clip(f)).Where(c => c != null).ToArray();
            if (clips.Length == 0) return false;
            bool same = p.arraySize == clips.Length;
            for (int i = 0; same && i < clips.Length; i++) same = p.GetArrayElementAtIndex(i).objectReferenceValue == clips[i];
            if (same) return false;
            p.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            done.Add($"{what} = {string.Join(", ", clips.Select(c => c.name))}");
            return true;
        }

        // A single clip slot. False when nothing changed or the file is missing.
        private static bool One(SerializedObject so, string field, string what, string file)
        {
            var p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogWarning($"{Tag} {so.targetObject.name} has no slot '{field}' (skipped).");
                return false;
            }
            var clip = Clip(file);
            if (clip == null || p.objectReferenceValue == clip) return false;
            p.objectReferenceValue = clip;
            done.Add($"{what} = {clip.name}");
            return true;
        }

        private static bool Float(SerializedObject so, string field, float value)
        {
            var p = so.FindProperty(field);
            if (p == null || Mathf.Approximately(p.floatValue, value)) return false;
            p.floatValue = value;
            return true;
        }

        private static void Scene(Object changed)
        {
            EditorUtility.SetDirty(changed);
            sceneChanged = true;
        }

        private static T FindInScene<T>() where T : Component
        {
            foreach (var c in Resources.FindObjectsOfTypeAll<T>())
                if (c != null && c.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(c)) return c;
            return null;
        }

        // Opens a prefab, runs the fix (it returns what it changed, "no ..." when it can't, or null), saves it.
        private static void EditPrefab(string path, Func<GameObject, string> fix)
        {
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == path)
            {
                Debug.LogWarning($"{Tag} {path} is open in Prefab Mode: close it and run this again (skipped).");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Debug.LogWarning($"{Tag} Missing {path} (skipped).");
                return;
            }
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                string what = fix(root);
                if (string.IsNullOrEmpty(what)) return;
                if (what.StartsWith("no "))
                {
                    Debug.LogWarning($"{Tag} {path}: {what} (skipped).");
                    return;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                done.Add(what);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
