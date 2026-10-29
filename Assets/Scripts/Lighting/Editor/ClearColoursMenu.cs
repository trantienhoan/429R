using System.Linq;
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
            SetAmbient(BrightAmbient);
        }

        [MenuItem("Tools/429 Game/Look/Colours: Original")]
        public static void UseOriginal()
        {
            Use(AssetDatabase.LoadAssetAtPath<VolumeProfile>(OriginalPath), "original");
            SetAmbient(BrightAmbient);
        }

        // ---------- horror (user, 2026-10-29: "too bright ... we are in a horror fighting bugs game, it's too happy") ----------

        private const string HorrorPath = "Assets/Settings/SampleSceneProfile_Horror.asset";
        // The scene's flat ambient light: the bright grey it had, and a dimmer, colder moonlight for the horror look.
        private static readonly Color BrightAmbient = new(0.5f, 0.5f, 0.55f);
        private static readonly Color HorrorAmbient = new(0.3f, 0.29f, 0.36f);

        /// <summary>
        /// The horror look: SampleSceneProfile_Horror, made from the clear one the first time (its numbers can be tuned
        /// afterwards; running this again keeps them), with the user's own Neutral tonemapping kept. Darker (exposure
        /// +0.25 instead of +0.7), deeper blacks (contrast +12), less happy colours (saturation -18, a cold filter, cold
        /// shadows, warm highlights so pumpkins and candles still glow) and darker edges (vignette 0.32). The flat
        /// ambient light goes from bright grey to a dim cold moonlight. Save the scene to keep it.
        /// </summary>
        [MenuItem("Tools/429 Game/Look/Colours: Horror")]
        public static void UseHorror()
        {
            var horror = AssetDatabase.LoadAssetAtPath<VolumeProfile>(HorrorPath);
            if (horror == null)
            {
                if (!AssetDatabase.CopyAsset(ClearPath, HorrorPath))
                {
                    Debug.LogError($"[Look] Couldn't copy {ClearPath} to {HorrorPath}. Nothing changed.");
                    return;
                }
                horror = AssetDatabase.LoadAssetAtPath<VolumeProfile>(HorrorPath);
                MakeHorror(horror);
            }
            Use(horror, "horror");
            SetAmbient(HorrorAmbient);
        }

        private static void MakeHorror(VolumeProfile profile)
        {
            if (profile.TryGet(out ColorAdjustments colour))
            {
                colour.postExposure.Override(0.25f);
                colour.contrast.Override(12f);
                colour.saturation.Override(-18f);
                colour.colorFilter.Override(new Color(0.88f, 0.93f, 1f));
            }
            if (profile.TryGet(out ShadowsMidtonesHighlights tones))
            {
                tones.shadows.Override(new Vector4(0.92f, 0.97f, 1.1f, -0.04f));
                tones.midtones.Override(new Vector4(1f, 1f, 1f, 0f));
                tones.highlights.Override(new Vector4(1.05f, 1f, 0.94f, 0f));
            }
            if (profile.TryGet(out Vignette vignette))
            {
                vignette.intensity.Override(0.32f);
                vignette.smoothness.Override(0.4f);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
        }

        private static void SetAmbient(Color colour)
        {
            if (RenderSettings.ambientLight == colour) return;
            RenderSettings.ambientLight = colour;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        // ---------- chaos darkness (user, 2026-10-29: "make chaos stage darker than other stages") ----------

        private const string ChaosPath = "Assets/Settings/SampleSceneProfile_Chaos.asset";
        private const string ChaosName = "Chaos Darkness";
        // GAMESTAGES switches it on as a chaos room comes, and off when that room's fight is cleaned up or the run resets.
        private static readonly string[] ChaosOn = { "Mini Stuffs Chaos", "BR_Chaos", "LR - Chaos", "Jenga Tower 2" };
        private static readonly string[] ChaosOff =
        {
            "Clean Mini", "Clean Enemy 1", "Clean Enemy 2", "Clean Enemy 3", "Game Over", "Restart Loop", "Door Shortcut",
        };

        /// <summary>
        /// Tools > 429 Game > Look > Set Up Chaos Darkness: a global volume "Chaos Darkness" (priority 1, over the scene's
        /// look) with SampleSceneProfile_Chaos: half a stop darker than the rest (exposure -0.3), harsher (contrast +18), a
        /// blood-red tint and dark red edges. It's off until a chaos room comes (GAMESTAGES Chaos On states) and off
        /// again when that fight is cleaned up (Chaos Off states). The profile's numbers can be tuned; running this again
        /// keeps them and rewires GAMESTAGES. Save the scene.
        /// </summary>
        [MenuItem("Tools/429 Game/Look/Set Up Chaos Darkness")]
        public static void SetUpChaosDarkness()
        {
            var stages = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(f => f.FsmName == "GAMESTAGES");
            if (stages == null)
            {
                Debug.LogError("[Look] No GAMESTAGES FSM in the open scene. Nothing changed.");
                return;
            }
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ChaosPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ChaosPath);
                var colour = profile.Add<ColorAdjustments>();
                colour.postExposure.Override(-0.3f);
                colour.contrast.Override(18f);
                colour.colorFilter.Override(new Color(1f, 0.82f, 0.86f));
                var vignette = profile.Add<Vignette>();
                vignette.color.Override(new Color(0.1f, 0f, 0.02f));
                vignette.intensity.Override(0.42f);
                vignette.smoothness.Override(0.45f);
                foreach (var part in profile.components) AssetDatabase.AddObjectToAsset(part, profile);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
            }

            var dark = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(v => v.name == ChaosName && v.gameObject.scene == stages.gameObject.scene);
            if (dark == null)
            {
                var go = new GameObject(ChaosName);
                SceneManager.MoveGameObjectToScene(go, stages.gameObject.scene);
                Undo.RegisterCreatedObjectUndo(go, "Create " + ChaosName);
                dark = go.AddComponent<Volume>();
            }
            dark.isGlobal = true;
            dark.priority = 1f;
            dark.weight = 1f;
            dark.sharedProfile = profile;
            dark.gameObject.SetActive(false);

            var fsm = stages.Fsm;
            if (!fsm.Initialized) fsm.Init(stages);
            foreach (var s in fsm.States) s.LoadActions();
            int on = 0, off = 0;
            foreach (var state in fsm.States)
            {
                // Old switches for it go first, so running this again doesn't double them.
                state.Actions = state.Actions.Where(a => !(a is HutongGames.PlayMaker.Actions.ActivateGameObject act &&
                    act.gameObject != null && act.gameObject.GameObject != null && act.gameObject.GameObject.Value == dark.gameObject)).ToArray();
                bool turnOn = ChaosOn.Contains(state.Name), turnOff = ChaosOff.Contains(state.Name);
                if (!turnOn && !turnOff) continue;
                var activate = new HutongGames.PlayMaker.Actions.ActivateGameObject();
                activate.Reset();
                activate.gameObject = new HutongGames.PlayMaker.FsmOwnerDefault
                {
                    OwnerOption = HutongGames.PlayMaker.OwnerDefaultOption.SpecifyGameObject,
                    GameObject = new HutongGames.PlayMaker.FsmGameObject { Value = dark.gameObject },
                };
                activate.activate = turnOn;
                activate.recursive = false;
                var list = state.Actions.ToList();
                list.Insert(0, activate);
                state.Actions = list.ToArray();
                if (turnOn) on++; else off++;
            }
            foreach (var s in fsm.States) s.SaveActions();
            EditorUtility.SetDirty(stages);
            EditorSceneManager.MarkSceneDirty(stages.gameObject.scene);
            Debug.Log($"[Look] Chaos Darkness: on in {on} chaos states, off in {off} states ({ChaosPath}). Save the scene.", dark);
        }

        [MenuItem("Tools/429 Game/Look/Colours: Clear", true)]
        [MenuItem("Tools/429 Game/Look/Colours: Original", true)]
        [MenuItem("Tools/429 Game/Look/Colours: Horror", true)]
        [MenuItem("Tools/429 Game/Look/Set Up Chaos Darkness", true)]
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
