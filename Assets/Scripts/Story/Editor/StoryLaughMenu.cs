using Game.Story;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Story > Set Up Laugh Chill (user's story, 2026-11-23, part 2): makes
    /// Assets/Resources/Story Laugh Settings, where the laugh's light dips, colour and cold are tuned. The effect itself
    /// (Laugh Chill) makes itself in every scene and listens to the runner pumpkins' laughs, so there is nothing else to
    /// set up and no scene change. Running it again changes nothing.
    /// </summary>
    public static class StoryLaughMenu
    {
        private const string SettingsPath = "Assets/Resources/Story Laugh Settings.asset";

        [MenuItem("Tools/429 Game/Story/Set Up Laugh Chill")]
        public static void Run()
        {
            if (AssetDatabase.LoadAssetAtPath<StoryLaughSettings>(SettingsPath) != null)
            {
                Debug.Log("[Laugh Chill] Already set up: " + SettingsPath + ".");
                return;
            }
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<StoryLaughSettings>(), SettingsPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Laugh Chill] Made " + SettingsPath + ". Runner laughs in the main rooms now dip the ceiling " +
                      "light and chill the view. No scene change.");
        }

        [MenuItem("Tools/429 Game/Story/Set Up Laugh Chill", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
