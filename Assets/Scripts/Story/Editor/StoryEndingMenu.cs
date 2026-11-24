using System.Collections.Generic;
using System.Linq;
using Game.Story;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Story > Set Up Story Ending (user's story, 2026-11-23, part 4: "a whole green glowing seed, touch
    /// it ... a bright light ... a happy voice ... the tiny green girl comes and showers me in the green light particles
    /// ... I am the Green Protector"). Run Set Up Story Seeds first.
    /// - The_Boss_Seed (the boss's chest) gets a Story Seed marked Whole Seed and a first Story Seed Touched action: it
    ///   glows green, and taking it fills the view with the light, the story's last words in it (End Title, End Line)
    ///   and the end voice. "Thank For Playing" joins Story Seed Settings' main states (the chest comes in it).
    /// - The Fairy's FairyHealth "Win" state (her reward after the boss) gets a first Green Shower action.
    /// - Story Seed Settings gets its title font (Socake SDF) and the shower's look (The_Boss_Seed's sparkles).
    /// Running it again changes nothing more. Prefabs and the settings asset only: no scene change.
    /// </summary>
    public static class StoryEndingMenu
    {
        private const string Tag = "[Story Ending]";
        private const string SettingsPath = "Assets/Resources/Story Seed Settings.asset";
        private const string BossSeedPath = "Assets/Prefabs/Items/Chest/The_Boss_Seed.prefab";
        private const string FairyPath = "Assets/Prefabs/Player/Fairy.prefab";
        private const string FontPath = "Assets/Fonts/socake/Socake SDF.asset";
        private const string EndState = "Thank For Playing";

        [MenuItem("Tools/429 Game/Story/Set Up Story Ending")]
        public static void Run()
        {
            var settings = AssetDatabase.LoadAssetAtPath<StorySeedSettings>(SettingsPath);
            if (settings == null)
            {
                Debug.LogError(Tag + " No " + SettingsPath + ": run Tools > 429 Game > Story > Set Up Story Seeds first.");
                return;
            }
            var done = new List<string>();
            Settings(settings, done);
            WholeSeed(done);
            Shower(done);
            AssetDatabase.SaveAssets();
            Debug.Log(Tag + (done.Count == 0 ? " Already done." : " Done: " + string.Join(", ", done) + ".") +
                      " No scene change.");
        }

        private static void Settings(StorySeedSettings settings, List<string> done)
        {
            var serialized = new SerializedObject(settings);
            var states = serialized.FindProperty("mainStates");
            bool hasEnd = false;
            for (int i = 0; i < states.arraySize; i++)
                if (states.GetArrayElementAtIndex(i).stringValue == EndState) hasEnd = true;
            if (!hasEnd)
            {
                states.InsertArrayElementAtIndex(states.arraySize);
                states.GetArrayElementAtIndex(states.arraySize - 1).stringValue = EndState;
                done.Add("main state '" + EndState + "'");
            }
            var font = serialized.FindProperty("titleFont");
            if (font.objectReferenceValue == null)
            {
                var socake = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                font.objectReferenceValue = socake != null ? socake : SetupUtility.ResolveFont();
                done.Add("title font " + (font.objectReferenceValue != null ? font.objectReferenceValue.name : "none"));
            }
            var look = serialized.FindProperty("showerLook");
            if (look.objectReferenceValue == null)
            {
                look.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(BossSeedPath);
                done.Add("shower look The_Boss_Seed");
            }
            if (!serialized.hasModifiedProperties) return;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        private static void WholeSeed(List<string> done)
        {
            var root = PrefabUtility.LoadPrefabContents(BossSeedPath);
            try
            {
                bool changed = false;
                var seed = root.GetComponent<StorySeed>();
                if (seed == null)
                {
                    seed = root.AddComponent<StorySeed>();
                    changed = true;
                }
                var serialized = new SerializedObject(seed);
                var whole = serialized.FindProperty("wholeSeed");
                if (!whole.boolValue)
                {
                    whole.boolValue = true;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }

                var fsm = root.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Rotate and Wait");
                if (fsm == null)
                {
                    Debug.LogError(Tag + " The_Boss_Seed has no 'Rotate and Wait' FSM.");
                    return;
                }
                if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
                var taken = fsm.Fsm.GetState("Activate Stuffs");
                if (taken == null)
                {
                    Debug.LogError(Tag + " The_Boss_Seed has no 'Activate Stuffs' state.");
                    return;
                }
                taken.LoadActions();
                if (!taken.Actions.OfType<StorySeedTouched>().Any())
                {
                    var light = new StorySeedTouched();
                    light.Reset();
                    light.gameObject = new FsmOwnerDefault { OwnerOption = OwnerDefaultOption.UseOwner };
                    taken.Actions = new FsmStateAction[] { light }.Concat(taken.Actions).ToArray();
                    taken.SaveActions();
                    EditorUtility.SetDirty(fsm);
                    changed = true;
                }
                if (!changed) return;
                PrefabUtility.SaveAsPrefabAsset(root, BossSeedPath);
                done.Add("The_Boss_Seed (whole seed)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Shower(List<string> done)
        {
            var root = PrefabUtility.LoadPrefabContents(FairyPath);
            try
            {
                var fsm = root.GetComponentsInChildren<PlayMakerFSM>(true).FirstOrDefault(f => f.FsmName == "FairyHealth");
                if (fsm == null)
                {
                    Debug.LogError(Tag + " The Fairy has no FairyHealth FSM.");
                    return;
                }
                if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
                var win = fsm.Fsm.GetState("Win");
                if (win == null)
                {
                    Debug.LogError(Tag + " The Fairy's FairyHealth has no 'Win' state.");
                    return;
                }
                win.LoadActions();
                if (win.Actions.OfType<GreenShower>().Any()) return;
                var shower = new GreenShower();
                shower.Reset();
                win.Actions = new FsmStateAction[] { shower }.Concat(win.Actions).ToArray();
                win.SaveActions();
                EditorUtility.SetDirty(fsm);
                PrefabUtility.SaveAsPrefabAsset(root, FairyPath);
                done.Add("the Fairy's reward (Green Shower)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Tools/429 Game/Story/Set Up Story Ending", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
