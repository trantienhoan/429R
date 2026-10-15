using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Audio > Make Pumpkin Laughs 3D. Every laughing pumpkin's speaker (the AudioSource its laughs
    /// and hit sounds play on) becomes 3D, so the player hears where a pumpkin is: quieter further away, and from its side.
    /// The Fairy's too (her calls for help, "oh no", the reward and flying away), and her cage breaking.
    /// Logarithmic rolloff (half as loud at twice Min Distance; it never goes silent), no doppler (launched and thrown
    /// pumpkins would bend the laugh) and a higher priority, so laughs aren't the first sounds dropped when many play.
    /// Pumpkin_Big follows Pumpkin_10. The pumpkin boss's laugh speaker copies its main speaker (PumpkinBossBrain).
    /// Running it again changes nothing.
    /// </summary>
    public static class PumpkinLaughSoundMenu
    {
        private readonly struct Speaker
        {
            public readonly string Prefab;
            // The object with the AudioSource, under the prefab's root ("" = the root).
            public readonly string Part;
            public readonly float MinDistance;
            public readonly float MaxDistance;

            public Speaker(string prefab, string part, float minDistance, float maxDistance)
            {
                Prefab = prefab;
                Part = part;
                MinDistance = minDistance;
                MaxDistance = maxDistance;
            }
        }

        private const string Breakable = "Assets/Prefabs/Items/Breakable/";
        private const string Enemies = "Assets/Prefabs/Enemies/";

        private static readonly Speaker[] Speakers =
        {
            // The start of the game.
            new(Breakable + "StartGamePumpkin.prefab", "pumpkin_Start", 3f, 25f),
            // In the rooms (bedroom, livingroom, the Jenga tower's).
            new(Breakable + "Pumpkin_0.prefab", "", 3f, 25f),
            new(Breakable + "Pumpkin_00.prefab", "", 3f, 25f),
            new(Breakable + "Pumpkin_1.prefab", "", 3f, 25f),
            new(Breakable + "Pumpkin_2.prefab", "", 3f, 25f),
            new(Breakable + "Pumpkin_4.prefab", "", 3f, 25f),
            new(Breakable + "Pumpkin_6.prefab", "", 3f, 25f),
            new(Breakable + "Pumpkin_6 1.prefab", "", 3f, 25f),
            new(Breakable + "Pumpkin_6 2.prefab", "", 3f, 25f),
            // The chase pumpkins.
            new(Breakable + "Pumpkin_3.prefab", "", 3f, 25f),
            new(Breakable + "Pumpkin_5.prefab", "", 3f, 25f),
            new(Breakable + "Pumpkin_7.prefab", "", 3f, 25f),
            // The catch-me pumpkins in the big PlayGround (Pumpkin_Big is a variant of Pumpkin_10).
            new(Breakable + "Pumpkin_8.prefab", "", 4f, 35f),
            new(Breakable + "Pumpkin_9.prefab", "", 4f, 35f),
            new(Breakable + "Pumpkin_10.prefab", "", 4f, 35f),
            // Whack a pumpkin: which barrel it popped out of.
            new(Enemies + "Pumpkin_Mob_Pumpkin_6.prefab", "", 3f, 25f),
            new(Enemies + "Pumpkin_Mob_Pumpkin_7.prefab", "", 3f, 25f),
            // The boss: heard across room 2.
            new(Enemies + "pumpkin_boss_Z.prefab", "", 5f, 40f),
            // The Fairy: the player hears her from 5-11 m away, so she's at full volume a little further out.
            new("Assets/Prefabs/Player/Fairy.prefab", "", 5f, 30f),
            new("Assets/Prefabs/Items/Broken_Stuffs/The_Cage_Drop.prefab", "", 3f, 25f),
        };

        [MenuItem("Tools/429 Game/Audio/Make Pumpkin Laughs 3D")]
        public static void MakeLaughs3D()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            var changed = new List<string>();
            var notes = new List<string>();
            foreach (var speaker in Speakers)
            {
                if (stage != null && stage.assetPath == speaker.Prefab)
                {
                    notes.Add(speaker.Prefab + " is open in Prefab Mode, skipped (close it and run this again)");
                    continue;
                }
                if (AssetDatabase.LoadAssetAtPath<GameObject>(speaker.Prefab) == null)
                {
                    notes.Add("no " + speaker.Prefab);
                    continue;
                }
                var root = PrefabUtility.LoadPrefabContents(speaker.Prefab);
                try
                {
                    var part = string.IsNullOrEmpty(speaker.Part) ? root.transform : root.transform.Find(speaker.Part);
                    var source = part != null ? part.GetComponent<AudioSource>() : null;
                    if (source == null)
                    {
                        notes.Add(speaker.Prefab + ": no AudioSource on " + (string.IsNullOrEmpty(speaker.Part) ? "its root" : speaker.Part));
                        continue;
                    }
                    if (Is3D(source, speaker)) continue;

                    source.spatialBlend = 1f;
                    source.rolloffMode = AudioRolloffMode.Logarithmic;
                    source.minDistance = speaker.MinDistance;
                    source.maxDistance = speaker.MaxDistance;
                    source.dopplerLevel = 0f;
                    source.spread = 0f;
                    source.priority = 100;
                    PrefabUtility.SaveAsPrefabAsset(root, speaker.Prefab);
                    changed.Add(System.IO.Path.GetFileNameWithoutExtension(speaker.Prefab));
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            Debug.Log("[Pumpkin Laughs] " + (changed.Count > 0 ? "3D now: " + string.Join(", ", changed) : "already 3D") +
                      (notes.Count > 0 ? ". NOTES: " + string.Join("; ", notes) : "") + ".");
        }

        [MenuItem("Tools/429 Game/Audio/Make Pumpkin Laughs 3D", true)]
        private static bool NotPlaying() => !EditorApplication.isPlaying;

        private static bool Is3D(AudioSource source, Speaker speaker)
        {
            return Mathf.Approximately(source.spatialBlend, 1f) && source.rolloffMode == AudioRolloffMode.Logarithmic &&
                   Mathf.Approximately(source.minDistance, speaker.MinDistance) &&
                   Mathf.Approximately(source.maxDistance, speaker.MaxDistance) &&
                   source.dopplerLevel == 0f && source.spread == 0f && source.priority == 100;
        }
    }
}
