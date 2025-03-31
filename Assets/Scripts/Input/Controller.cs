using UnityEngine;

public class Controller : MonoBehaviour
{
    public Vector2 StickL { get; private set; }
    public Vector2 StickR { get; private set; }

    void Update()
    {
        // Get left stick input (movement)
        StickL = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        
        // Get right stick input (rotation)
        StickR = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
    }
} 