using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Content.Interaction;

public class DoorManager : MonoBehaviour
{
    [Header("Door Settings")]
    [SerializeField] private string nextSceneName = "MiniGameScene"; // Set this in inspector
    [SerializeField] private float doorOpenAngle = 90f;
    [SerializeField] private float transitionDelay = 1f;
    
    [Header("References")]
    [SerializeField] private Door door;
    [SerializeField] private KeySocketInteractor keySocket;
    
    private bool isDoorUnlocked = false;
    private bool isTransitioning = false;

    private void Start()
    {
        // Subscribe to door events
        if (door != null)
        {
            door.onUnlock.AddListener(OnDoorUnlocked);
        }

        // Subscribe to key socket events
        if (keySocket != null)
        {
            keySocket.selectEntered.AddListener(OnKeyInserted);
            keySocket.selectExited.AddListener(OnKeyRemoved);
        }
    }

    private void OnDoorUnlocked()
    {
        isDoorUnlocked = true;
        Debug.Log("Door unlocked!");
    }

    private void OnKeyInserted(SelectEnterEventArgs args)
    {
        // When key is inserted, allow door to be unlocked
        if (door != null)
        {
            door.enabled = true;
        }
    }

    private void OnKeyRemoved(SelectExitEventArgs args)
    {
        // When key is removed, lock the door
        if (door != null)
        {
            door.enabled = false;
        }
    }

    private void Update()
    {
        // Check if door is open enough to trigger transition
        if (isDoorUnlocked && !isTransitioning)
        {
            float currentAngle = Mathf.Abs(door.transform.localEulerAngles.y);
            if (currentAngle >= doorOpenAngle)
            {
                StartCoroutine(TransitionToNextScene());
            }
        }
    }

    private System.Collections.IEnumerator TransitionToNextScene()
    {
        isTransitioning = true;
        
        // Wait for transition delay
        yield return new WaitForSeconds(transitionDelay);
        
        // Load the next scene
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogWarning("Next scene name not set in DoorManager!");
        }
    }
} 