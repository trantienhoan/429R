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
            Debug.Log("Lamp Broken");
        }
    }
}