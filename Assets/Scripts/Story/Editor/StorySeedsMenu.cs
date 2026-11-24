using System.Collections.Generic;
using System.Linq;
using Game.Story;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Story > Set Up Story Seeds (user's story, 2026-11-23, part 1 "seed pieces"). The main game's 5
    /// room seeds (Seed_Bracer at the start, Seed_Shield, Seed_Head, Seed_Chest, Seed_Boot) get a Story Seed and, first in
    /// their "Activate Stuffs" state (the player took the seed), a Story Seed Touched action: on the main game their glow
    /// turns green and taking one fills the view with the bright light and the girl's voice until the next room starts.
    /// Gates keep their seeds as they are (Story Seed checks GAMESTAGES). Also makes Resources/Story Seed Settings with
    /// Fairy_Call_2 as a stand-in voice until the user records "please find me".
    /// Running it again changes nothing more. Prefabs only: no scene change.
    /// </summary>
    public static class StorySeedsMenu
    {
        private const string Tag = "[Story Seeds]";
        private const string SettingsPath = "Assets/Resources/Story Seed Settings.asset";
        private const string StandInVoice = "Assets/Audio/SFX/Fairy_Call_2.wav";
        private const string SeedFolder = "Assets/Prefabs/Items/Chest/";
        private static readonly string[] Seeds = { "Seed_Bracer", "Seed_Shield", "Seed_Head", "Seed_Chest", "Seed_Boot" };
        private static readonly string[] GlowParts = { "circles", "GrowthParticles", "stars", "shadow" };
        private const string SeedFsm = "Rotate and Wait";
        private const string TakenState = "Activate Stuffs";

        [MenuItem("Tools/429 Game/Story/Set Up Story Seeds")]
        public static void Run()
        {
            var done = new List<string>();

            var settings = AssetDatabase.LoadAssetAtPath<StorySeedSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<StorySeedSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
                done.Add("made " + SettingsPath);
            }
            var serialized = new SerializedObject(settings);
            var voice = serialized.FindProperty("voice");
            if (voice.objectReferenceValue == null)
            {
                voice.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(StandInVoice);
                done.Add("stand-in voice Fairy_Call_2");
            }
            // The seed's warm halo ('shadow') glows green too (added 2026-11-24).
            var parts = serialized.FindProperty("glowParts");
            foreach (var part in GlowParts)
            {
                bool has = false;
                for (int i = 0; i < parts.arraySize; i++)
                    if (parts.GetArrayElementAtIndex(i).stringValue == part) has = true;
                if (has) continue;
                parts.InsertArrayElementAtIndex(parts.arraySize);
                parts.GetArrayElementAtIndex(parts.arraySize - 1).stringValue = part;
                done.Add("glow part " + part);
            }
            if (serialized.hasModifiedProperties)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
            }

            foreach (var name in Seeds)
            {
                string path = SeedFolder + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool changed = false;
                    if (root.GetComponent<StorySeed>() == null)
                    {
                        root.AddComponent<StorySeed>();
                        changed = true;
                    }

                    var fsm = root.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == SeedFsm);
                    if (fsm == null)
                    {
                        Debug.LogError(Tag + " " + name + ": no '" + SeedFsm + "' FSM, skipped.");
                        continue;
                    }
                    if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
                    var taken = fsm.Fsm.GetState(TakenState);
                    if (taken == null)
                    {
                        Debug.LogError(Tag + " " + name + ": no '" + TakenState + "' state, skipped.");
                        continue;
                    }
                    taken.LoadActions();
                    if (!taken.Actions.OfType<StorySeedTouched>().Any())
                    {
                        var light = new StorySeedTouched();
                        light.Reset();
                        light.gameObject = new FsmOwnerDefault { OwnerOption = OwnerDefaultOption.UseOwner };
                        // First, before the seed sets its gear bool and GAMESTAGES leaves the room's state.
                        taken.Actions = new FsmStateAction[] { light }.Concat(taken.Actions).ToArray();
                        taken.SaveActions();
                        EditorUtility.SetDirty(fsm);
                        changed = true;
                    }

                    if (!changed) continue;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    done.Add(name);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log(Tag + (done.Count == 0 ? " Already done." : " Done: " + string.Join(", ", done) + ".") +
                      " Main game only; gates keep their seeds. No scene change.");
        }

        [MenuItem("Tools/429 Game/Story/Set Up Story Seeds", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
