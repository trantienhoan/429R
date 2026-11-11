#if UNITY_EDITOR_WIN
using System;
using System.Runtime.InteropServices;

namespace Game.Cam
{
    /// <summary>
    /// The photo and video keys (Photo Capture's Right Shift, Video Capture's Right Ctrl), read straight from Windows, but
    /// only while this Unity Editor is the focused window (user, 2026-11-11: typing in another app, e.g. the my-town Editor,
    /// kept starting recordings and taking photos in 429R's Play mode). Any window of this Editor counts (a floating Game
    /// view too). Editor only.
    /// </summary>
    internal static class CaptureKeys
    {
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int key);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        private static readonly uint OwnProcess = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;

        /// <summary>Whether this key (a Windows virtual-key code) is held down while this Editor is the focused window.</summary>
        public static bool Down(int key) => (GetAsyncKeyState(key) & 0x8000) != 0 && Focused();

        private static bool Focused()
        {
            var window = GetForegroundWindow();
            if (window == IntPtr.Zero) return false;
            GetWindowThreadProcessId(window, out uint process);
            return process == OwnProcess;
        }
    }
}
#endif
