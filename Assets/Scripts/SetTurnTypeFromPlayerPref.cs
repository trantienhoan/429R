using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

public class SetTurnTypeFromPlayerPref : MonoBehaviour
{
    //[SerializeField] private ActionBasedSnapTurnProvider snapTurn;
    //[SerializeField] private ActionBasedContinuousTurnProvider continuousTurn;
    [SerializeField] private SnapTurnProvider snapTurn;
    [SerializeField] private ContinuousTurnProvider continuousTurn;
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
