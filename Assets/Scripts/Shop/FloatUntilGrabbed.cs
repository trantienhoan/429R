using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.Shopping
{
    /// <summary>
    /// Keeps a freshly bought item floating and slowly spinning until the player grabs it,
    /// then gives it back its normal physics once let go.
    /// </summary>
    [DisallowMultipleComponent]
    public class FloatUntilGrabbed : MonoBehaviour
    {
        [SerializeField] private float spinSpeed = 45f;

        private Rigidbody body;
        private XRGrabInteractable grab;
        private bool wasKinematic;
        private bool usedGravity;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            grab = GetComponent<XRGrabInteractable>();

            if (body != null)
            {
                wasKinematic = body.isKinematic;
                usedGravity = body.useGravity;
                body.isKinematic = true;
                body.useGravity = false;
            }
            if (grab != null) grab.selectExited.AddListener(OnReleased);
        }

        private void OnDestroy()
        {
            if (grab != null) grab.selectExited.RemoveListener(OnReleased);
        }

        private void Update()
        {
            if (grab == null || !grab.isSelected) transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            if (grab.isSelected) return;

            // Letting go restores the floating state the grab started from, so put the item's own physics back.
            if (body != null)
            {
                body.isKinematic = wasKinematic;
                body.useGravity = usedGravity;
            }
            Destroy(this);
        }
    }
}
