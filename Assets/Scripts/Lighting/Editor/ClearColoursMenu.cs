using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Look: switches the scene's "Post Processing" volume between the original colours
    /// (SampleSceneProfile) and the clear ones (SampleSceneProfile_Clear, made from the original the first time). Clear:
    /// the same look with ACES kept, but no lift on the darks, midtones and highlights (they turned black into 9% grey),
    /// contrast +10 instead of -10.8, saturation +10 instead of -5, and exposure +0.2. Its numbers can be tuned on the
    /// profile afterwards; running Clear again keeps them. The death look (Post Processing_Dead) isn't touched. Save the
    /// scene to keep the choice.
    /// </summary>
    public static class ClearColoursMenu
    {
        private const string OriginalPath = "Assets/Settings/SampleSceneProfile.asset";
        private const string ClearPath = "Assets/Settings/SampleSceneProfile_Clear.asset";
        private const string VolumeName = "Post Processing";

        [MenuItem("Tools/429 Game/Look/Colours: Clear")]
        public static void UseClear()
        {
            var clear = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ClearPath);
            if (clear == null)
            {
                if (!AssetDatabase.CopyAsset(OriginalPath, ClearPath))
                {
                    Debug.LogError($"[Look] Couldn't copy {OriginalPath} to {ClearPath}. Nothing changed.");
                    return;
                }
                clear = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ClearPath);
                MakeClear(clear);
            }
            Use(clear, "clear");
        }

        [MenuItem("Tools/429 Game/Look/Colours: Original")]
        public static void UseOriginal()
        {
            Use(AssetDatabase.LoadAssetAtPath<VolumeProfile>(OriginalPath), "original");
        }

        [MenuItem("Tools/429 Game/Look/Colours: Clear", true)]
        [MenuItem("Tools/429 Game/Look/Colours: Original", true)]
        private static bool NotPlaying() => !EditorApplication.isPlaying;

        // Real blacks, more contrast and colour, a little brighter. ACES (Tonemapping) and the rest stay as they are.
        private static void MakeClear(VolumeProfile profile)
        {
            if (profile.TryGet(out ShadowsMidtonesHighlights tones))
            {
                tones.shadows.Override(new Vector4(1f, 1f, 1f, 0f));
                tones.midtones.Override(new Vector4(1f, 1f, 1f, 0f));
                tones.highlights.Override(new Vector4(1f, 1f, 1f, 0f));
            }
            if (profile.TryGet(out ColorAdjustments colour))
            {
                colour.contrast.Override(10f);
                colour.saturation.Override(10f);
                colour.postExposure.Override(0.2f);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
        }

        private static void Use(VolumeProfile profile, string which)
        {
            if (profile == null)
            {
                Debug.LogError($"[Look] The {which} profile is missing. Nothing changed.");
                return;
            }
            Volume volume = null;
            foreach (var found in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (found.name == VolumeName) volume = found;
            }
            if (volume == null)
            {
                Debug.LogError($"[Look] No '{VolumeName}' volume in the open scene. Nothing changed.");
                return;
            }
            if (volume.sharedProfile == profile)
            {
                Debug.Log($"[Look] The {which} colours are already on.");
                return;
            }
            Undo.RecordObject(volume, $"Use {which} colours");
            volume.sharedProfile = profile;
            EditorUtility.SetDirty(volume);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[Look] The {which} colours are on ({AssetDatabase.GetAssetPath(profile)}). Save the scene to keep them.", volume);
        }
    }
}
