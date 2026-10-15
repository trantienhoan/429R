using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game.Weapons
{
    /// <summary>
    /// Whatever the player picks up buzzes the hand holding it when it hurts something (melee): the first time a thing is
    /// grabbed it gets a Hit Rumble, unless it already has one (the weapons set up with Add Hit Rumble To Weapons keep
    /// theirs). Punches buzz by themselves (Fist). It also tells Held Things when a hand lets go, so a thrown thing still
    /// counts as the player's for a moment. Goes on the XR Origin; it finds the hands under it, also the ones switched on
    /// later. Tools > 429 Game > Player > Set Up Hit Haptics adds it.
    /// Things that are kinematic while held (e.g. Boong_Gun) don't collide while held, so they can't buzz.
    /// </summary>
    [DisallowMultipleComponent]
    public class HitRumbleOnGrab : MonoBehaviour
    {
        private readonly List<XRBaseInputInteractor> hands = new();

        private void OnEnable()
        {
            // Inactive too: the controllers are switched on by the XR Origin's Info FSM after this.
            GetComponentsInChildren(true, hands);
            foreach (var hand in hands)
            {
                hand.selectEntered.AddListener(OnGrabbed);
                hand.selectExited.AddListener(OnLetGo);
            }
        }

        private void OnDisable()
        {
            foreach (var hand in hands)
            {
                if (hand == null) continue;
                hand.selectEntered.RemoveListener(OnGrabbed);
                hand.selectExited.RemoveListener(OnLetGo);
            }
            hands.Clear();
        }

        private static void OnLetGo(SelectExitEventArgs args)
        {
            if (args.interactableObject is XRGrabInteractable grab) Game.Combat.HeldThings.Released(grab);
        }

        private static void OnGrabbed(SelectEnterEventArgs args)
        {
            if (args.interactableObject is not XRGrabInteractable grab || grab == null) return;
            if (grab.TryGetComponent<HitRumble>(out _)) return;
            // Its Rigidbody (an XR Grab Interactable always has one) gets the collisions of all its colliders.
            var rumble = grab.gameObject.AddComponent<HitRumble>();
            // Added after the grab began: it must still count this one.
            rumble.Grabbed();
        }
    }
}
