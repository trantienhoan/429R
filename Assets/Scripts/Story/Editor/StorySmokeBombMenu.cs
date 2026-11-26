using System.Collections.Generic;
using System.Linq;
using Game.Story;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Story > Smoke Bomb Escape + Eyes-Only Watcher (user 2026-11-26: "When the miniboss spawn out of
    /// the laughing pumpkin, make them use animation Alert then pop a smoke bomb like the one ninja always use then
    /// hide"; the seed piece on its back "Remove the one on their back"; the watcher outside the room "make it rarer to
    /// spawn and show only 2 eyes"). Re-runnable, assets only (no scene change):
    /// - Story Boss Escape Settings: Escape = Smoke Bomb (Smoke_Flash_Five_Puffs, the stage swap poofs), Alert 1 s (the
    ///   four mini bosses' alert look is 0.98 s), no seed piece on the back, the real boss comes in a smoke bomb too;
    /// - Story Giant Settings: the watcher's eyes only, a 0.02 chance each half second the player looks (was 0.06), then
    ///   180-360 s quiet (was 60-120).
    /// </summary>
    public static class StorySmokeBombMenu
    {
        private const string Tag = "[Story]";
        private const string EscapePath = "Assets/Resources/Story Boss Escape Settings.asset";
        private const string GiantPath = "Assets/Resources/Story Giant Settings.asset";
        private const string Smoke = "Assets/Prefabs/Effects/Smoke/Smoke_Flash_Five_Puffs.prefab";
        private static readonly string[] Poofs = { "Assets/Audio/SFX/stage_swap_poof_1.wav", "Assets/Audio/SFX/stage_swap_poof_2.wav" };
        private const float AlertSeconds = 1f;
        private const float WatchChance = 0.02f;
        private static readonly Vector2 WatchEvery = new(180f, 360f);

        [MenuItem("Tools/429 Game/Story/Smoke Bomb Escape + Eyes-Only Watcher")]
        public static void Run()
        {
            var notes = new List<string>();
            var escape = AssetDatabase.LoadAssetAtPath<StoryBossEscapeSettings>(EscapePath);
            if (escape == null) notes.Add($"no {EscapePath} (run Set Up Boss Escape first)");
            else
            {
                var so = new SerializedObject(escape);
                so.FindProperty("escape").enumValueIndex = (int)StoryBossEscapeSettings.EscapeStyle.SmokeBomb;
                var smoke = AssetDatabase.LoadAssetAtPath<GameObject>(Smoke);
                if (smoke == null) notes.Add($"no {Smoke} (no smoke shows)");
                so.FindProperty("smokeBomb").objectReferenceValue = smoke;
                var sounds = Poofs.Select(AssetDatabase.LoadAssetAtPath<AudioClip>).Where(c => c != null).ToArray();
                var list = so.FindProperty("smokeBombSounds");
                list.arraySize = sounds.Length;
                for (int i = 0; i < sounds.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = sounds[i];
                so.FindProperty("hissSeconds").floatValue = AlertSeconds;
                so.FindProperty("seedOnBoss").boolValue = false;
                so.FindProperty("comeBackInSmoke").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(escape);
                notes.Add($"escape: Alert {AlertSeconds} s then a smoke bomb ({(smoke != null ? smoke.name : "none")}, " +
                          $"{sounds.Length} poofs), no seed on the back, back in a smoke bomb");
            }

            var giant = AssetDatabase.LoadAssetAtPath<StoryGiantSettings>(GiantPath);
            if (giant == null) notes.Add($"no {GiantPath} (run Set Up Story Hints first)");
            else
            {
                var so = new SerializedObject(giant);
                so.FindProperty("watcherBody").boolValue = false;
                so.FindProperty("watchChance").floatValue = WatchChance;
                so.FindProperty("watchEvery").vector2Value = WatchEvery;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(giant);
                notes.Add($"watcher: eyes only, chance {WatchChance}, {WatchEvery.x}-{WatchEvery.y} s quiet");
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} {string.Join("; ", notes)}. Assets only: no scene change.");
        }

        [MenuItem("Tools/429 Game/Story/Smoke Bomb Escape + Eyes-Only Watcher", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
