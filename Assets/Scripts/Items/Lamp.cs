using UnityEngine;

public class Lamp : MonoBehaviour
{
    public bool isBroken = false;
    public static event System.Action<Lamp> OnLampBroken;

    public void Break()
    {
        if (!isBroken)
        {
            isBroken = true;
            OnLampBroken?.Invoke(this);
            // Add breaking visual/audio effects here
            Debug.Log("Lamp Broken");
            Destroy(gameObject); // Or disable the lamp
        }
    }

    // Example interaction (e.g., when the player attacks the lamp)
    private void OnCollisionEnter(Collision collision)
    {
        //Basic example, implement proper tag and attack detection
        Break();
    }
}