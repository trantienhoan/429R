using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game.Locomotion
{
    /// <summary>
    /// Keeps what the player holds moving with them: when the player turns or moves by the controls (snap or smooth
    /// turning, walking with the stick or by swinging the arms, a teleport, being thrown by a big hit, the camera
    /// shaking), whatever is in their hands is carried along at once, so its speed is only what the hands themselves
    /// do. Without it a held weapon has to whip round to catch up with every turn (a snap turn swings it across 45
    /// degrees in one physics step), and hits as hard as a swing - enemies hurt, things broken, the weapon's own hit
    /// puffs - though the player never swung it. Goes on the XR Origin.
    /// </summary>
    // Before the XR Interaction Manager (-105) moves held things towards the hands in FixedUpdate.
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public class CarryHeldThings : MonoBehaviour
    {
        private readonly List<XRBaseInteractor> hands = new();
        private readonly HashSet<Rigidbody> carried = new();
        private Transform space;
        private Vector3 lastPosition;
        private Quaternion lastRotation;
        private bool hasLast;

        private void OnEnable()
        {
            var origin = GetComponent<XROrigin>();
            if (origin == null) origin = FindAnyObjectByType<XROrigin>();
            // The play area the hands are tracked in: it moves and turns with the player, and shakes with the camera.
            space = origin != null && origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.transform : transform;
            (origin != null ? origin.transform : transform).GetComponentsInChildren(true, hands);
            hasLast = false;
        }

        private void FixedUpdate()
        {
            space.GetPositionAndRotation(out var position, out var rotation);
            if (hasLast && (position != lastPosition || rotation != lastRotation)) Carry(position, rotation);
            lastPosition = position;
            lastRotation = rotation;
            hasLast = true;
        }

        // Each held thing goes where it was in the play area, now that the play area has moved or turned.
        private void Carry(Vector3 position, Quaternion rotation)
        {
            var turn = rotation * Quaternion.Inverse(lastRotation);
            carried.Clear();
            foreach (var hand in hands)
            {
                if (hand == null || !hand.isActiveAndEnabled || !hand.hasSelection) continue;
                foreach (var held in hand.interactablesSelected)
                {
                    if (held == null || !held.transform.TryGetComponent<Rigidbody>(out var body) || !carried.Add(body)) continue;
                    body.position = position + turn * (body.position - lastPosition);
                    body.rotation = turn * body.rotation;
                    if (body.isKinematic) continue;
                    // The hands' own swing keeps its direction in the play area.
                    body.linearVelocity = turn * body.linearVelocity;
                    body.angularVelocity = turn * body.angularVelocity;
                }
            }
        }
    }
}
