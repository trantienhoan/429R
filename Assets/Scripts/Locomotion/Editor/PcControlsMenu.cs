using Game.Locomotion;
using UnityEditor;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Player > PC Controls (Editor): switches the Editor's PC controls (Pc Controls: keyboard and mouse
    /// when Play runs without a headset) on or off for the next Play. On by default; never in a build.
    /// </summary>
    public static class PcControlsMenu
    {
        private const string MenuPath = "Tools/429 Game/Player/PC Controls (Editor)";

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            bool on = !EditorPrefs.GetBool(PcControls.PrefsKey, true);
            EditorPrefs.SetBool(PcControls.PrefsKey, on);
            UnityEngine.Debug.Log($"[PC Controls] {(on ? "On" : "Off")} for the next Play without a headset.");
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleCheck()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(PcControls.PrefsKey, true));
            return true;
        }
    }
}
