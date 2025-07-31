using UnityEngine;
using UnityEngine.XR.Content.Interaction;
using UnityEngine.XR.Interaction.Toolkit;

public class DoorLock : MonoBehaviour
{
    [Header("Door Settings")]
    [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor lockSocket; // Socket for key insert
    [SerializeField] private XRKnob doorKnob; // XRKnob on handle
    [SerializeField] private GameObject doorKeyVisual; // Visual key in lock (disabled at start)
    [SerializeField] private float turnThresholdDegrees = 45f; // Twist degrees to drop item

    [Header("Item Drop")]
    [SerializeField] private GameObject[] itemPrefabs; // List of random items (e.g., bombs)
    [SerializeField] private Transform dropPosition; // Position to drop item

    [Header("Audio")]
    [SerializeField] private AudioClip unlockSound;
    [SerializeField] private AudioSource audioSource;

    [Header("Optional Hinge (for future open)")]
    [SerializeField] private HingeJoint doorHinge; // Hinge on door body (limits locked at 0)

    private bool isUnlocked = false;

    private void Awake()
    {
        Debug.Log($"DoorLock Awake on {gameObject.name} - Active: {gameObject.activeSelf}");

        if (lockSocket != null)
        {
            lockSocket.selectEntered.AddListener(OnKeyInserted);
        }
        else
        {
            Debug.LogError("Lock Socket not assigned!");
        }

        if (doorKnob != null)
        {
            doorKnob.enabled = false; // Disabled until unlock
        }

        if (doorKeyVisual != null)
        {
            doorKeyVisual.SetActive(false); // Hidden at start
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (doorHinge != null)
        {
            JointLimits limits = doorHinge.limits;
            limits.min = 0f;
            limits.max = 0f; // Locked, no open
            doorHinge.limits = limits;
        }
    }

    private void OnEnable()
    {
        Debug.Log($"DoorLock OnEnable on {gameObject.name} - Reactivating knob if unlocked");
        if (isUnlocked && doorKnob != null)
        {
            doorKnob.enabled = true;
        }
    }

    private void Update()
    {
        if (isUnlocked && doorKnob != null)
        {
            float currentAngle = doorKnob.value * (doorKnob.maxAngle - doorKnob.minAngle);
            Debug.Log($"Current knob angle: {currentAngle} degrees (value: {doorKnob.value})");

            if (currentAngle >= turnThresholdDegrees)
            {
                Debug.Log("Knob twisted enough - dropping random item");
                DropRandomItem();
                this.enabled = false; // Disable script only
            }
        }
    }

    private void OnKeyInserted(SelectEnterEventArgs args)
    {
        Debug.Log("Key inserted");

        // Hide inserted key
        var insertedKey = args.interactableObject.transform.gameObject;
        insertedKey.SetActive(false); // Make "gone"

        // Activate visual key
        if (doorKeyVisual != null)
        {
            doorKeyVisual.SetActive(true);
        }

        // Play sound
        if (unlockSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(unlockSound);
        }

        // Disable socket
        if (lockSocket != null)
        {
            lockSocket.enabled = false;
        }

        // Enable knob
        if (doorKnob != null)
        {
            doorKnob.enabled = true;
        }

        isUnlocked = true;
    }

    private void DropRandomItem()
    {
        if (itemPrefabs.Length > 0 && dropPosition != null)
        {
            int randomIndex = Random.Range(0, itemPrefabs.Length);
            Instantiate(itemPrefabs[randomIndex], dropPosition.position, Quaternion.identity);
            Debug.Log("Random item dropped!");
        }
        else
        {
            Debug.LogError("No item prefabs or drop position assigned!");
        }
    }
}