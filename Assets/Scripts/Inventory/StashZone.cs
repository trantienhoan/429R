using UnityEngine;

namespace Game.Inventory
{
    /// <summary>
    /// Invisible trigger at the player's chest that follows the headset. A held StashableItem
    /// touching it goes into the PlayerInventory.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class StashZone : MonoBehaviour
    {
        [Tooltip("The headset camera. Left empty, Camera.main is used.")]
        [SerializeField] private Transform head;
        [Tooltip("Position relative to the head in metres (x right, y up, z forward). Turns with the head's left/right rotation only.")]
        [SerializeField] private Vector3 offsetFromHead = new(0f, -0.35f, 0.05f);
        [Tooltip("Left empty, PlayerInventory.Instance is used.")]
        [SerializeField] private PlayerInventory inventory;

        private void Reset()
        {
            ConfigureBody();
            if (!TryGetComponent<Collider>(out var zone))
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(0.35f, 0.4f, 0.3f);
                zone = box;
            }
            zone.isTrigger = true;
        }

        private void Awake()
        {
            ConfigureBody();
        }

        private void LateUpdate()
        {
            if (head == null)
            {
                var cam = Camera.main;
                if (cam == null) return;
                head = cam.transform;
            }

            var yaw = Quaternion.Euler(0f, head.eulerAngles.y, 0f);
            transform.SetPositionAndRotation(head.position + yaw * offsetFromHead, yaw);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryStash(other);
        }

        // Also checked while overlapping, so an item already touching the zone when grabbed still gets stashed.
        private void OnTriggerStay(Collider other)
        {
            TryStash(other);
        }

        private void TryStash(Collider other)
        {
            var body = other.attachedRigidbody;
            var stashable = body != null ? body.GetComponent<StashableItem>() : other.GetComponentInParent<StashableItem>();
            if (stashable == null) return;

            var target = inventory != null ? inventory : PlayerInventory.Instance;
            if (target != null) stashable.TryStash(target);
        }

        private void ConfigureBody()
        {
            var body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        private void OnDrawGizmosSelected()
        {
            if (!TryGetComponent<BoxCollider>(out var box)) return;
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
    }
}
