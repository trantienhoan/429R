using UnityEngine;

public class ResetPosition : MonoBehaviour
{
    public Vector3 originalPos;

    void Start()
    {
        originalPos = transform.position; // Store initial position
    }
}