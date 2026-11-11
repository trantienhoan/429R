using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.Combat
{
    /// <summary>
    /// Held, it stays as the hand holds it (user, 2026-10-30: "check why Pumpkin_00 keep moving around when being
    /// grabbed"): its FSMs' Look At and Smooth Look At (the pumpkins' Health FSM turns them to face the player every frame,
    /// fighting the hand) are paused while the player holds it, and go on once it's let go. Runs before the FSMs, so not
    /// a single frame turns it. Goes on the runner pumpkins and Pumpkin_00 (Tools > 429 Game > Combat > Held Pumpkins
    /// Explode adds it with Held Break Blast).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public class HeldStill : MonoBehaviour
    {
        private XRGrabInteractable grab;
        private PlayMakerFSM[] fsms;
        private readonly List<HutongGames.PlayMaker.FsmStateAction> paused = new();

        private void Awake()
        {
            TryGetComponent(out grab);
            fsms = GetComponents<PlayMakerFSM>();
        }

        private void Update()
        {
            if (grab != null && grab.isSelected) Pause();
            else Resume();
        }

        private void OnDisable() => Resume();

        // The turning (and punch-shaking) actions of every state of each FSM, not only the one it's in: a state entered while it's held runs
        // its actions' first step at once (play test 2026-11-11: Pumpkin_00's Health FSM turned it 18 degrees in the hand
        // 9 s after it appeared, and the hand dragged it back). Switched off (Enabled), so entering a state skips them.
        private void Pause()
        {
            foreach (var fsm in fsms)
            {
                if (fsm == null || !fsm.enabled) continue;
                foreach (var state in fsm.FsmStates)
                {
                    foreach (var action in state.Actions)
                    {
                        if (!action.Enabled || !(action is HutongGames.PlayMaker.Actions.LookAt || action is HutongGames.PlayMaker.Actions.SmoothLookAt ||
                              action is HutongGames.PlayMaker.Actions.TweenPunch)) continue;
                        action.Enabled = false;
                        action.Active = false;
                        paused.Add(action);
                    }
                }
            }
        }

        private void Resume()
        {
            if (paused.Count == 0) return;
            foreach (var action in paused)
            {
                action.Enabled = true;
                action.Active = true;
            }
            paused.Clear();
        }
    }
}
