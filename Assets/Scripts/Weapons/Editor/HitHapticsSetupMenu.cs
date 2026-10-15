using System.Collections.Generic;
using Game.Weapons;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Player > Set Up Hit Haptics, for the open scene's XR Origin:
    /// - the controllers buzz at full strength again: their Haptic Impulse Player's Amplitude Multiplier goes from
    ///   0.01 (every buzz at 1%, so punches and weapon hits were never felt) to 1;
    /// - so that doesn't make grabbing or pointing at things buzz, the hands' interactors (Simple Haptic Feedback) don't
    ///   buzz on hover or grab any more: the controllers buzz only when the player deals damage (Hit Buzz);
    /// - Hit Rumble On Grab on the XR Origin: whatever the player picks up buzzes their hand when it hurts something.
    /// Punches that hurt something buzz the punching hand (Fist). The scene is left unsaved for you to save. Running it
    /// again changes nothing.
    /// </summary>
    public static class HitHapticsSetupMenu
    {
        [MenuItem("Tools/429 Game/Player/Set Up Hit Haptics")]
        public static void SetUp()
        {
            var origins = Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (origins.Length == 0)
            {
                Debug.LogError("[Hit Haptics] No XR Origin in the open scene. Nothing changed.");
                return;
            }

            var notes = new List<string>();
            foreach (var origin in origins)
            {
                int players = 0, feedbacks = 0;
                foreach (var part in origin.GetComponentsInChildren<Component>(true))
                {
                    if (part == null) continue;
                    string type = part.GetType().Name;
                    if (type == "HapticImpulsePlayer" && Set(part, so =>
                            SetFloat(so, "m_AmplitudeMultiplier", 1f)))
                        players++;
                    else if (type == "SimpleHapticFeedback" && Set(part, so =>
                                 SetBool(so, "m_PlayHoverEntered", false) | SetBool(so, "m_PlayHoverExited", false) |
                                 SetBool(so, "m_PlayHoverCanceled", false) | SetBool(so, "m_PlaySelectEntered", false) |
                                 SetBool(so, "m_PlaySelectExited", false) | SetBool(so, "m_PlaySelectCanceled", false)))
                        feedbacks++;
                }
                bool added = false;
                if (!origin.TryGetComponent<HitRumbleOnGrab>(out _))
                {
                    Undo.AddComponent<HitRumbleOnGrab>(origin.gameObject);
                    added = true;
                }
                if (players + feedbacks == 0 && !added) continue;
                EditorSceneManager.MarkSceneDirty(origin.gameObject.scene);
                notes.Add($"{origin.name}: {players} controllers at full strength, {feedbacks} hover and grab buzzes off" +
                          (added ? ", Hit Rumble On Grab added" : ""));
            }
            Debug.Log("[Hit Haptics] " + (notes.Count > 0 ? string.Join("; ", notes) + ". Save the scene." : "already set up."));
        }

        [MenuItem("Tools/429 Game/Player/Set Up Hit Haptics", true)]
        private static bool NotPlaying() => !EditorApplication.isPlaying;

        private static bool Set(Component part, System.Func<SerializedObject, bool> change)
        {
            var so = new SerializedObject(part);
            if (!change(so)) return false;
            so.ApplyModifiedProperties();
            return true;
        }

        private static bool SetFloat(SerializedObject so, string path, float value)
        {
            var property = so.FindProperty(path);
            if (property == null || Mathf.Approximately(property.floatValue, value)) return false;
            property.floatValue = value;
            return true;
        }

        private static bool SetBool(SerializedObject so, string path, bool value)
        {
            var property = so.FindProperty(path);
            if (property == null || property.boolValue == value) return false;
            property.boolValue = value;
            return true;
        }
    }
}
