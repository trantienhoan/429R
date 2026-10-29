using Game.Locomotion;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Menu command that makes held things hit only as hard as the hand swings them and stay in the hand (Tools > 429 Game > Locomotion).</summary>
    public static class CarryHeldThingsSetupMenu
    {
        /// <summary>
        /// Puts Carry Held Things, Held Swing Limit and Held Pass Through on the XR Origin: turning or moving by the
        /// controls carries whatever is held along, a held thing can't go much faster than the hand moves it, so a held
        /// weapon only hits as hard as the hand swings it, and something heavier can't knock a held thing out of the hand
        /// (driving a weapon into it strains it instead). Running it again only adds what's missing.
        /// </summary>
        [MenuItem("Tools/429 Game/Locomotion/Carry Held Things With The Player")]
        public static void SetUp()
        {
            var origin = Object.FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
            if (origin == null)
            {
                Debug.LogError("[Carry Held Things] No XR Origin in the open scene.");
                return;
            }
            bool added = false;
            if (!origin.TryGetComponent<CarryHeldThings>(out _))
            {
                Undo.AddComponent<CarryHeldThings>(origin.gameObject);
                added = true;
            }
            if (!origin.TryGetComponent<HeldSwingLimit>(out _))
            {
                Undo.AddComponent<HeldSwingLimit>(origin.gameObject);
                added = true;
            }
            if (!origin.TryGetComponent<HeldPassThrough>(out _))
            {
                Undo.AddComponent<HeldPassThrough>(origin.gameObject);
                added = true;
            }
            if (added) EditorSceneManager.MarkSceneDirty(origin.gameObject.scene);
            Debug.Log(added
                ? "[Carry Held Things] The XR Origin now carries held things along, limits them to the hands' swing and keeps them in the hand against heavier things. Save the scene."
                : "[Carry Held Things] The XR Origin already has all three.", origin);
        }

        [MenuItem("Tools/429 Game/Locomotion/Carry Held Things With The Player", true)]
        private static bool CanSetUp() => !EditorApplication.isPlaying;
    }
}
