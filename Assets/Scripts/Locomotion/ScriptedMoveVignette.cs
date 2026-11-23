using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;

namespace Game.Locomotion
{
    /// <summary>
    /// The comfort vignette also closes in while a script moves the player fast (gate 3's request, 2026-11-23: its
    /// two-fist dash and the launches bypassed it, being no locomotion provider). It uses the player's Comfort Vignette
    /// level like the locomotion providers (the controller's default parameters; Off = nothing drawn).
    /// A script calls <see cref="Begin"/>(this) when its fast move starts and <see cref="End"/>(this) when it's over;
    /// the first Begin adds this component next to the Tunneling Vignette Controller (no scene change).
    /// Player Launch throws use it only while <see cref="Launches"/> is on: Gate Run turns it on while a gate runs, so the
    /// main game's feel is unchanged until the user says so.
    /// </summary>
    [DisallowMultipleComponent]
    public class ScriptedMoveVignette : MonoBehaviour, ITunnelingVignetteProvider
    {
        /// <summary>Player Launch throws use the vignette too (Gate Run turns this on while a gate runs).</summary>
        public static bool Launches { get; set; }

        private static ScriptedMoveVignette instance;
        private static readonly HashSet<object> movers = new HashSet<object>();
        private TunnelingVignetteController controller;
        private bool on;

        /// <summary>null = the controller's default parameters, which Comfort Vignette Level keeps at the player's level.</summary>
        public VignetteParameters vignetteParameters => null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            movers.Clear();
            Launches = false;
        }

        /// <summary>A fast scripted move starts (<paramref name="who"/> = the mover). Calling it again meanwhile is fine.</summary>
        public static void Begin(object who)
        {
            if (who == null) return;
            movers.Add(who);
            var v = Find();
            if (v != null) v.Refresh();
        }

        /// <summary>That mover's fast move is over.</summary>
        public static void End(object who)
        {
            if (who == null || !movers.Remove(who)) return;
            if (instance != null) instance.Refresh();
        }

        private static ScriptedMoveVignette Find()
        {
            if (instance != null) return instance;
            var controller = FindAnyObjectByType<TunnelingVignetteController>();
            if (controller == null) return null;
            var v = controller.GetComponent<ScriptedMoveVignette>();
            if (v == null) v = controller.gameObject.AddComponent<ScriptedMoveVignette>();
            instance = v;
            return v;
        }

        private void Awake()
        {
            controller = GetComponent<TunnelingVignetteController>();
            if (instance == null) instance = this;
        }

        private void Refresh()
        {
            // A mover destroyed mid-move (a stage cleared away) must not leave the view closed in.
            movers.RemoveWhere(m => m == null || (m is Object o && o == null));
            bool want = movers.Count > 0 && controller != null && isActiveAndEnabled;
            if (want == on) return;
            on = want;
            if (on) controller.BeginTunnelingVignette(this);
            else controller.EndTunnelingVignette(this);
        }

        private void Update()
        {
            if (on) Refresh();
        }

        private void OnDisable()
        {
            if (on && controller != null) controller.EndTunnelingVignette(this);
            on = false;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
