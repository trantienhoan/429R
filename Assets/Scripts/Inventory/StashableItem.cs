using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game.Inventory
{
    /// <summary>
    /// Makes a grabbable object collectable: hold it against your chest (the StashZone)
    /// and it goes into the PlayerInventory.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class StashableItem : MonoBehaviour
    {
        [SerializeField] private ItemDefinition item;
        [Min(1)]
        [SerializeField] private int amount = 1;
        [Tooltip("Only stash while a hand holds it, so items lying around never get pulled in.")]
        [SerializeField] private bool onlyWhileHeld = true;
        [Tooltip("Seconds it must be held first, so grabbing something right next to your body doesn't stash it instantly.")]
        [Min(0f)]
        [SerializeField] private float minHoldTime = 0.2f;

        [Header("Feedback")]
        [SerializeField] private AudioClip stashSound;
        [Range(0f, 1f)]
        [SerializeField] private float stashVolume = 1f;
        [SerializeField] private GameObject stashEffectPrefab;
        [Range(0f, 1f)]
        [SerializeField] private float hapticAmplitude = 0.5f;
        [SerializeField] private float hapticDuration = 0.1f;

        [Header("After stashing")]
        [Tooltip("Destroy the object; otherwise it is only deactivated.")]
        [SerializeField] private bool destroyOnStash = true;
        [SerializeField] private UnityEvent onStashed;

        private XRGrabInteractable grab;
        private float heldSince = -1f;
        private bool stashed;

        public ItemDefinition Item => item;
        public int Amount => amount;
        public bool IsHeld => grab != null && grab.isSelected;

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
        }

        private void OnEnable()
        {
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnReleased);
        }

        private void OnDisable()
        {
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnReleased);
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            if (heldSince < 0f) heldSince = Time.time;
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            if (!grab.isSelected) heldSince = -1f;
        }

        /// <summary>Puts the item into the inventory if the rules allow it. Returns true if it was stashed.</summary>
        public bool TryStash(PlayerInventory inventory)
        {
            if (stashed || item == null || inventory == null) return false;
            if (onlyWhileHeld && (!IsHeld || heldSince < 0f || Time.time - heldSince < minHoldTime)) return false;
            if (!inventory.CanAdd(item, amount)) return false;

            stashed = true;
            inventory.Add(item, amount);

            // Buzz the hand(s) holding it, then let go.
            if (grab.isSelected)
            {
                foreach (var interactor in grab.interactorsSelecting)
                {
                    if (interactor is XRBaseInputInteractor inputInteractor)
                        inputInteractor.SendHapticImpulse(hapticAmplitude, hapticDuration);
                }
                if (grab.interactionManager != null)
                    grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
            }

            if (stashSound != null) AudioSource.PlayClipAtPoint(stashSound, transform.position, stashVolume);
            if (stashEffectPrefab != null) Instantiate(stashEffectPrefab, transform.position, Quaternion.identity);
            onStashed?.Invoke();

            if (destroyOnStash) Destroy(gameObject);
            else gameObject.SetActive(false);
            return true;
        }
    }
}
