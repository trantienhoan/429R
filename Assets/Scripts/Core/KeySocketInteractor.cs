using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class KeySocketInteractor : XRSocketInteractor
{
    [SerializeField] private bool allowGrabWhileInSocket = true;
    
    protected override void Awake()
    {
        base.Awake();
        // Configure socket to allow grabbing while in socket
        if (allowGrabWhileInSocket)
        {
            socketActive = true;
            interactionLayers = InteractionLayerMask.GetMask("Default");
        }
    }

    public override bool CanSelect(IXRSelectInteractable interactable)
    {
        // Always allow selection if the interactable is already in the socket
        if (hasSelection && interactable == interactablesSelected[0])
            return true;

        return base.CanSelect(interactable);
    }

    public override bool CanHover(IXRHoverInteractable interactable)
    {
        // Always allow hovering if the interactable is already in the socket
        if (hasSelection && interactable == interactablesSelected[0])
            return true;

        return base.CanHover(interactable);
    }
} 