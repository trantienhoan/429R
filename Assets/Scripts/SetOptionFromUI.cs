using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

public class SetOptionFromUI : MonoBehaviour
{
    public Scrollbar volumeSlider;
    public TMPro.TMP_Dropdown turnDropdown;
    public SetTurnTypeFromPlayerPref turnTypeFromPlayerPref;

    private void Start()
    {
        volumeSlider.onValueChanged.AddListener(SetGlobalVolume);
        turnDropdown.onValueChanged.AddListener(SetTurnPlayerPref);

        // Initialize dropdown with current turn type
        bool isSnapTurn = PlayerPrefs.GetInt("UseSnapTurn", 1) == 1;
        turnDropdown.SetValueWithoutNotify(isSnapTurn ? 0 : 1);
    }

    public void SetGlobalVolume(float value)
    {
        AudioListener.volume = value;
    }

    public void SetTurnPlayerPref(int value)
    {
        // Convert dropdown value to UseSnapTurn format (0 = snap turn, 1 = continuous turn)
        PlayerPrefs.SetInt("UseSnapTurn", value == 0 ? 1 : 0);
        turnTypeFromPlayerPref.UpdateTurnType();
    }
}
