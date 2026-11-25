using System;
using System.Collections.Generic;
using System.Linq;
using Game.Enemies;
using Game.VFX;
using Game.Weapons;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Audio > Fill Silent Sounds (Clips We Have) (user 2026-11-25, Exports/Audio/AUDIO_PLAN.md,
    /// "Fix now with clips you already have"). Only the user's own clips; no new sounds. Re-runnable: each fix checks
    /// first and changes nothing that is already right. Prefabs only (no scene change).
    /// 1. Fairy_Lock_Tower: its release (jail breaking) sound had no clip: glass_break_2, as its setup menu meant.
    /// 2. Bedroom_Book_5: its Health FSM "Compare Health" played an empty clip: bubble_pop_1, like the other books.
    /// 3. Stone_Cube2_Drop and Rooftop_Pole_Light_Drop: their Init picked a random break sound but never stored it, so
    ///    the Audio Play played an empty variable: the pick now goes into the variable the Audio Play plays.
    /// 4. pumpkin_boss_Z: its hurt yelp was always ouch_pumpkin_2: now ouch_pumpkin_1-4 at random (Hurt Sounds).
    /// 5. Bee_Smarter and Cockroach_Smarter (and their variants): a bite blocked by a fist or weapon was silent:
    ///    bone_bonk_1, the spiders' blocked sound.
    /// 6. Boong_Gun: no shot sound (only the bullet's laugh): elon_boong_shoot (in the project, never used) on every
    ///    trigger pull (Overheat's Shot Sound).
    /// </summary>
    public static class SilentSoundsMenu
    {
        private const string Tag = "[Silent Sounds]";
        private const string Sfx = "Assets/Audio/SFX/";

        [MenuItem("Tools/429 Game/Audio/Fill Silent Sounds (Clips We Have)")]
        public static void Run()
        {
            var done = new List<string>();

            // 1. The Fairy's jail breaking.
            var glass = Clip("glass_break_2");
            EditPrefab("Assets/Prefabs/Effects/Fairy_Lock_Tower.prefab", done, root =>
            {
                var tower = root.GetComponent<FairyLockTower>();
                if (tower == null) return "no FairyLockTower";
                var so = new SerializedObject(tower);
                var source = so.FindProperty("releaseSound").objectReferenceValue as AudioSource;
                if (source == null) source = root.GetComponent<AudioSource>();
                if (source == null) return "no release AudioSource";
                if (source.clip == glass) return null;
                source.clip = glass;
                EditorUtility.SetDirty(source);
                return "jail release sound = glass_break_2";
            });

            // 2. Book 5's hit.
            var pop = Clip("bubble_pop_1");
            EditPrefab("Assets/Prefabs/Items/Breakable/Bedroom_Book_5.prefab", done, root =>
            {
                string what = null;
                ForState(root, "Health", "Compare Health", (fsm, st) =>
                {
                    foreach (var ap in st.Actions.OfType<AudioPlay>())
                    {
                        if (ap.oneShotClip == null || ap.oneShotClip.UseVariable || ap.oneShotClip.Value != null) continue;
                        ap.oneShotClip.ObjectType = typeof(AudioClip);
                        ap.oneShotClip.Value = pop;
                        what = "Book 5 hit = bubble_pop_1";
                    }
                    return what != null;
                });
                return what;
            });

            // 3. The two break pieces whose random pick was never stored.
            foreach (var (path, fsmName) in new[]
                     {
                         ("Assets/Prefabs/Items/Broken_Stuffs/Stone_Cube2_Drop.prefab", "Break"),
                         ("Assets/Prefabs/Items/Broken_Stuffs/Rooftop_Pole_Light_Drop.prefab", "FSM"),
                     })
            {
                EditPrefab(path, done, root =>
                {
                    string what = null;
                    ForState(root, fsmName, "Init", (fsm, st) =>
                    {
                        var play = st.Actions.OfType<AudioPlay>().FirstOrDefault(a => a.oneShotClip != null && a.oneShotClip.UseVariable);
                        if (play == null) return false;
                        var variable = fsm.Fsm.Variables.GetVariable(play.oneShotClip.Name);
                        if (variable == null) return false;
                        bool changed = false;
                        foreach (var pick in st.Actions.OfType<ArrayGetRandom>())
                        {
                            if (pick.storeValue != null && !string.IsNullOrEmpty(pick.storeValue.variableName)) continue;
                            pick.storeValue = new FsmVar(variable);
                            what = $"{System.IO.Path.GetFileNameWithoutExtension(path)}: random break sound stored in {variable.Name}";
                            changed = true;
                        }
                        return changed;
                    });
                    return what;
                });
            }

            // 4. The pumpkin boss's hurt yelps.
            var ouches = new[] { "ouch_pumpkin_1", "ouch_pumpkin_2", "ouch_pumpkin_3", "ouch_pumpkin_4" }.Select(Clip).ToArray();
            EditPrefab("Assets/Prefabs/Enemies/pumpkin_boss_Z.prefab", done, root =>
            {
                var brain = root.GetComponentInChildren<PumpkinBossBrain>(true);
                if (brain == null) return "no PumpkinBossBrain";
                return SetClips(brain, "hurtSounds", ouches) ? "boss hurt = ouch_pumpkin_1-4" : null;
            });

            // 5. Bees' and roaches' blocked bites.
            var bonk = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath("9f33a6c57f37b904caaa68322aa2e0eb"));
            foreach (var path in new[]
                     {
                         "Assets/Prefabs/Enemies/Enemies_Smarter/Bee_Smarter.prefab",
                         "Assets/Prefabs/Enemies/Enemies_Smarter/Cockroach_Smarter.prefab",
                     })
            {
                EditPrefab(path, done, root =>
                {
                    var brain = root.GetComponentInChildren<SpiderBrain>(true);
                    if (brain == null) return "no SpiderBrain";
                    return SetClip(brain, "blockedSound", bonk)
                        ? $"{System.IO.Path.GetFileNameWithoutExtension(path)} blocked bite = {bonk.name}" : null;
                });
            }

            // 6. The Boong gun's shot.
            var shot = Clip("elon_boong_shoot");
            EditPrefab("Assets/Prefabs/Weapon/Boong_Gun.prefab", done, root =>
            {
                var heat = root.GetComponentInChildren<Overheat>(true);
                if (heat == null) return "no Overheat";
                return SetClip(heat, "shotSound", shot) ? "Boong gun shot = elon_boong_shoot" : null;
            });

            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} " + (done.Count > 0 ? string.Join("; ", done) : "everything was already filled") + ".");
        }

        [MenuItem("Tools/429 Game/Audio/Fill Silent Sounds (Clips We Have)", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static AudioClip Clip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + name + ".wav");
            if (clip == null) Debug.LogWarning($"{Tag} Missing {Sfx}{name}.wav");
            return clip;
        }

        // Opens a prefab, runs the fix (it returns what it changed, or null when nothing needed changing), saves it.
        private static void EditPrefab(string path, List<string> done, Func<GameObject, string> fix)
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

        // Runs `edit` on one FSM state (actions loaded); saves the state's actions when it says it changed them.
        private static void ForState(GameObject root, string fsmName, string stateName, Func<PlayMakerFSM, FsmState, bool> edit)
        {
            foreach (var fsm in root.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (fsm.FsmName != fsmName) continue;
                fsm.Fsm.Init(fsm);
                foreach (var st in fsm.Fsm.States)
                {
                    if (st.Name != stateName) continue;
                    st.LoadActions();
                    if (!edit(fsm, st)) continue;
                    st.SaveActions();
                    EditorUtility.SetDirty(fsm);
                }
            }
        }

        private static bool SetClip(UnityEngine.Object component, string field, AudioClip clip)
        {
            var so = new SerializedObject(component);
            var p = so.FindProperty(field);
            if (p == null || clip == null || p.objectReferenceValue == clip) return false;
            p.objectReferenceValue = clip;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool SetClips(UnityEngine.Object component, string field, AudioClip[] clips)
        {
            var so = new SerializedObject(component);
            var p = so.FindProperty(field);
            if (p == null || clips.Any(c => c == null)) return false;
            bool same = p.arraySize == clips.Length;
            for (int i = 0; same && i < clips.Length; i++) same = p.GetArrayElementAtIndex(i).objectReferenceValue == clips[i];
            if (same) return false;
            p.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
    }
}
