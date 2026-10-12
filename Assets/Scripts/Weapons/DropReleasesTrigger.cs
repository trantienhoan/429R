using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game.Weapons
{
    /// <summary>
    /// Lets go of a weapon's trigger when the weapon is dropped. XR only sends a weapon its Deactivated event (effects
    /// off, the hammers' Release) when the trigger is let go while it's still in the hand, so a weapon dropped with the
    /// trigger squeezed stayed fired on the floor: Bus_Boong / Elon_Boong blasting, hammers stuck in Fire, the knight's
    /// sword and shield glowing. This sends that Deactivated when the last hand lets go of a fired weapon, the same as
    /// letting go of the trigger. Works by itself for every grabbable in every scene, shop-bought ones too; nothing to
    /// set up.
    /// </summary>
    public static class DropReleasesTrigger
    {
        private static readonly HashSet<XRInteractionManager> managers = new();
        private static readonly HashSet<XRBaseInteractable> watched = new();
        // Fired (Activated) and not yet let go (Deactivated).
        private static readonly HashSet<XRBaseInteractable> fired = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            managers.Clear();
            watched.Clear();
            fired.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            WatchManagers();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            watched.RemoveWhere(grab => grab == null);
            fired.RemoveWhere(grab => grab == null);
            WatchManagers();
        }

        // Every grabbable registers with an Interaction Manager: the ones already there, and each new one (spawned,
        // bought, switched on) as it comes.
        private static void WatchManagers()
        {
            managers.RemoveWhere(manager => manager == null);
            var registered = new List<IXRInteractable>();
            foreach (var manager in Object.FindObjectsByType<XRInteractionManager>(FindObjectsSortMode.None))
            {
                if (!managers.Add(manager)) continue;
                manager.interactableRegistered += args => Watch(args.interactableObject);
                manager.GetRegisteredInteractables(registered);
                foreach (var interactable in registered) Watch(interactable);
            }
        }

        private static void Watch(IXRInteractable interactable)
        {
            if (interactable is not XRBaseInteractable grab || grab == null || !watched.Add(grab)) return;
            grab.activated.AddListener(_ => fired.Add(grab));
            grab.deactivated.AddListener(_ => fired.Remove(grab));
            grab.selectExited.AddListener(args => OnLetGo(grab, args));
        }

        private static void OnLetGo(XRBaseInteractable grab, SelectExitEventArgs args)
        {
            // Still in the other hand: that hand's trigger decides.
            if (grab == null || grab.isSelected || !fired.Contains(grab)) return;
            ((IXRActivateInteractable)grab).OnDeactivated(new DeactivateEventArgs
            {
                interactorObject = args.interactorObject as IXRActivateInteractor,
                interactableObject = grab,
            });
            fired.Remove(grab);
        }
    }
}
