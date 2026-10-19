using System.Collections.Generic;
using Game.Combat;
using Game.Leveling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Player > Set Up Boong Buffs, for the open scene: gives both hands' Fist the Elon Boong's
    /// explosion (the bottles' effects, Flash_round_yellow, Hit_Blast and Smoke_Blast, and their explosion sound), where
    /// they don't have one yet. Everything else about the boong buffs (Player Buffs; the health HUD shows them) is made when the game
    /// runs. The scene is left unsaved for you to save. Running it again changes nothing, and effects or a sound you
    /// picked yourself are kept.
    /// Tools > 429 Game > Player > Give Boong Buff (Play Mode) gives a buff while playing, to try them out.
    /// </summary>
    public static class BoongBuffSetupMenu
    {
        private static readonly string[] EffectPaths =
        {
            "Assets/Lana Studio/Hyper Casual FX/Prefabs/Flash/Flash_round_yellow.prefab",
            "Assets/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Hit_Blast.prefab",
            "Assets/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Smoke_Blast.prefab",
        };
        private const string SoundPath = "Assets/Audio/SFX/explosion-312361.wav";

        [MenuItem("Tools/429 Game/Player/Set Up Boong Buffs")]
        public static void SetUp()
        {
            var fists = Object.FindObjectsByType<Fist>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (fists.Length == 0)
            {
                Debug.LogError("[Boong Buffs] No Fist in the open scene (Left Controller, Right Controller). Nothing changed.");
                return;
            }

            var effects = new List<GameObject>();
            foreach (var path in EffectPaths)
            {
                var effect = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (effect != null) effects.Add(effect);
                else Debug.LogWarning($"[Boong Buffs] Effect not found: {path}");
            }
            var sound = AssetDatabase.LoadAssetAtPath<AudioClip>(SoundPath);
            if (sound == null) Debug.LogWarning($"[Boong Buffs] Sound not found: {SoundPath}");

            var notes = new List<string>();
            foreach (var fist in fists)
            {
                var so = new SerializedObject(fist);
                var changes = new List<string>();
                var effectList = so.FindProperty("boongBlastEffects");
                if (effectList != null && effects.Count > 0 && !HasAny(effectList))
                {
                    effectList.arraySize = effects.Count;
                    for (int i = 0; i < effects.Count; i++) effectList.GetArrayElementAtIndex(i).objectReferenceValue = effects[i];
                    changes.Add($"{effects.Count} explosion effects");
                }
                var soundProperty = so.FindProperty("boongBlastSound");
                if (soundProperty != null && sound != null && soundProperty.objectReferenceValue == null)
                {
                    soundProperty.objectReferenceValue = sound;
                    changes.Add("explosion sound");
                }
                if (changes.Count == 0) continue;
                so.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(fist.gameObject.scene);
                notes.Add($"{fist.name}: {string.Join(", ", changes)}");
            }
            Debug.Log("[Boong Buffs] " + (notes.Count > 0
                ? "Elon Boong punch explosions set up. " + string.Join("; ", notes) + ". Save the scene."
                : "already set up."));
        }

        [MenuItem("Tools/429 Game/Player/Set Up Boong Buffs", true)]
        private static bool NotPlaying() => !EditorApplication.isPlaying;

        private static bool HasAny(SerializedProperty list)
        {
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue != null) return true;
            }
            return false;
        }

        // Play Mode only: gives a buff for its full 60 seconds, as smoking that boong does.

        [MenuItem("Tools/429 Game/Player/Give Boong Buff (Play Mode)/Elon Boong")]
        private static void GiveElon() => PlayerBuffs.Give(Buff.ElonBoong);

        [MenuItem("Tools/429 Game/Player/Give Boong Buff (Play Mode)/Bone Boong")]
        private static void GiveBone() => PlayerBuffs.Give(Buff.BoneBoong);

        [MenuItem("Tools/429 Game/Player/Give Boong Buff (Play Mode)/Green Boong")]
        private static void GiveGreen() => PlayerBuffs.Give(Buff.GreenBoong);

        [MenuItem("Tools/429 Game/Player/Give Boong Buff (Play Mode)/Bus Boong")]
        private static void GiveBus() => PlayerBuffs.Give(Buff.BusBoong);

        [MenuItem("Tools/429 Game/Player/Give Boong Buff (Play Mode)/All Four")]
        private static void GiveAll()
        {
            for (int i = 0; i < PlayerBuffs.Count; i++) PlayerBuffs.Give((Buff)i);
        }

        [MenuItem("Tools/429 Game/Player/Give Boong Buff (Play Mode)/End All Buffs")]
        private static void EndAll() => PlayerBuffs.EndAll();

        [MenuItem("Tools/429 Game/Player/Give Boong Buff (Play Mode)/Elon Boong", true)]
        [MenuItem("Tools/429 Game/Player/Give Boong Buff (Play Mode)/Bone Boong", true)]
        [MenuItem("Tools/429 Game/Player/Give Boong Buff (Play Mode)/Green Boong", true)]
        [MenuItem("Tools/429 Game/Player/Give Boong Buff (Play Mode)/Bus Boong", true)]
        [MenuItem("Tools/429 Game/Player/Give Boong Buff (Play Mode)/All Four", true)]
        [MenuItem("Tools/429 Game/Player/Give Boong Buff (Play Mode)/End All Buffs", true)]
        private static bool Playing() => EditorApplication.isPlaying;
    }
}
