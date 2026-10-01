using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game.Lighting
{
    /// <summary>
    /// Puts a candle out when the player picks it up: its lights switch off and its flame objects are hidden, and,
    /// unless On Again When Dropped is ticked, they stay that way. Other effects (like the smoke when it breaks) are
    /// left alone. Goes next to the XR Grab Interactable; Tools > 429 Game > Lighting > Lights Off When Grabbed adds it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class LightOffWhenGrabbed : MonoBehaviour
    {
        // With no flames set, children with this in their name are the flames ("CandleFlame").
        private const string FlameName = "Flame";

        [Tooltip("Hidden along with the lights. Left empty, every child with \"Flame\" in its name is used.")]
        [SerializeField] private GameObject[] flames = System.Array.Empty<GameObject>();
        [Tooltip("On: the lights and flames come back when the player lets go. Off: once picked up, they stay out.")]
        [SerializeField] private bool onAgainWhenDropped;

        private XRGrabInteractable grab;
        private Light[] lights;

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            lights = GetComponentsInChildren<Light>(true);
            if (flames == null || flames.Length == 0) flames = FindFlames();
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnLetGo);
        }

        private void OnDestroy()
        {
            if (grab == null) return;
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnLetGo);
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            SetLit(false);
        }

        private void OnLetGo(SelectExitEventArgs args)
        {
            // Passed to the other hand: still held.
            if (onAgainWhenDropped && !grab.isSelected) SetLit(true);
        }

        private void SetLit(bool lit)
        {
            foreach (var light in lights)
            {
                if (light != null) light.enabled = lit;
            }
            foreach (var flame in flames)
            {
                // Never hide the grabbed object itself (or what it hangs from).
                if (flame != null && !transform.IsChildOf(flame.transform)) flame.SetActive(lit);
            }
        }

        private GameObject[] FindFlames()
        {
            var found = new List<GameObject>();
            foreach (var child in GetComponentsInChildren<Transform>(true))
            {
                if (child != transform && child.name.Contains(FlameName)) found.Add(child.gameObject);
            }
            return found.ToArray();
        }
    }
}
