using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Items
{
    public class DoorLock : MonoBehaviour
    {
        [Header("Door Settings")]
        [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor doorSocket;
        [SerializeField] private Animator doorAnimator; // If using animation
        [SerializeField] private bool isLocked = true;

        [Header("Audio")]
        [SerializeField] private AudioClip unlockSound;
        [SerializeField] private AudioSource audioSource;

        private void Awake()
        {
            // Set up socket listener
            if (doorSocket != null)
            {
                doorSocket.selectEntered.AddListener(OnKeySocketed);
                Debug.Log("Door socket listener set up");
            }
            else
            {
                Debug.LogError("Door socket not assigned!");
            }

            // Get audio source if not assigned
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }
        }

        private void OnKeySocketed(SelectEnterEventArgs args)
        {
            if (!isLocked)
            {
                Debug.Log("Door is already unlocked");
                return;
            }

            // Check if the socketed object is the key
            if (args.interactableObject.transform.CompareTag("DoorKey"))
            {
                Debug.Log("Key detected in door socket - unlocking door");
                UnlockDoor();
            }
        }

        private void UnlockDoor()
        {
            isLocked = false;

            // Play unlock sound if available
            if (unlockSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(unlockSound);
            }

            // If using animation, trigger the unlock animation
            if (doorAnimator != null)
            {
                doorAnimator.SetTrigger("Unlock");
            }

            // Disable the socket to prevent removing the key
            if (doorSocket != null)
            {
                doorSocket.enabled = false;
            }

            Debug.Log("Door unlocked!");
        }
    }
} 