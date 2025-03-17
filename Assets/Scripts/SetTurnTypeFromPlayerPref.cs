using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

public class SetTurnTypeFromPlayerPref : MonoBehaviour
{
    [SerializeField] private ActionBasedSnapTurnProvider snapTurn;
    [SerializeField] private ActionBasedContinuousTurnProvider continuousTurn;
    
    private void Start()
    {
        UpdateTurnType();
    }

    public void UpdateTurnType()
    {
        bool isSnapTurn = PlayerPrefs.GetInt("UseSnapTurn", 1) == 1;
        
        if (snapTurn != null)
            snapTurn.enabled = isSnapTurn;
            
        if (continuousTurn != null)
            continuousTurn.enabled = !isSnapTurn;
    }
}
