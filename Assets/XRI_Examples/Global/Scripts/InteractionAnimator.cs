using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.XR.Interaction.Toolkit;

namespace UnityEngine.XR.Content.Interaction
{
    /// <summary>
    /// Component that when paired with an interactable will drive an associated timeline with the activate button
    /// Must be used with an action-based controller
    /// </summary>
    public class InteractionAnimator : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The timeline to drive with the activation button.")]
        PlayableDirector m_ToAnimate;

        bool m_Animating;
        InputAction m_ActivateAction;

        void Start()
        {
            // We want to hook up to the Select events so we can read data about the interacting controller
            var interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable>();
            if (interactable == null || interactable as UnityEngine.Object == null)
            {
                Debug.LogWarning($"No interactable on {name} - no animation will be played.", this);
                enabled = false;
                return;
            }

            if (m_ToAnimate == null)
            {
                Debug.LogWarning($"No timeline configured on {name} - no animation will be played.", this);
                enabled = false;
                return;
            }

            interactable.selectEntered.AddListener(OnSelect);
            interactable.selectExited.AddListener(OnDeselect);
        }

        void Update()
        {
            if (m_Animating && m_ActivateAction != null)
            {
                // Read value from input action - this works with all controller types in XRI 3.x
                float floatValue = m_ActivateAction.ReadValue<float>();
                m_ToAnimate.time = floatValue;
            }
        }

        void OnSelect(SelectEnterEventArgs args)
        {
            // Get the interactor
            var interactor = args.interactorObject;
            if (interactor == null)
            {
                Debug.LogWarning($"Selected by a null interactor", this);
                return;
            }

            // Find the activate action from the interactor
            bool foundAction = FindActivateActionFromInteractor(interactor, out m_ActivateAction);

            if (!foundAction || m_ActivateAction == null)
            {
                Debug.LogWarning($"Selected by {interactor.transform.name}, which does not have a valid activate action", this);
                return;
            }

            // Ready to animate
            m_ToAnimate.Play();
            m_Animating = true;
        }

        private bool FindActivateActionFromInteractor(UnityEngine.XR.Interaction.Toolkit.Interactors.IXRInteractor interactor, out InputAction activateAction)
        {
            activateAction = null;
            
            // Get the GameObject of the interactor
            var interactorObj = interactor as MonoBehaviour;
            if (interactorObj == null) return false;
            
            // Option 1: Find the action directly from an InputActionProperty component
            var properties = interactorObj.GetComponentsInParent<Component>(true);
            foreach (var property in properties)
            {
                // Search properties for fields that contain InputActions
                var fields = property.GetType().GetFields(System.Reflection.BindingFlags.Public | 
                                                        System.Reflection.BindingFlags.Instance | 
                                                        System.Reflection.BindingFlags.NonPublic);
                
                foreach (var field in fields)
                {
                    // Look for InputActionProperty or InputActionReference fields that might be the activate action
                    if (field.FieldType == typeof(InputActionProperty) || field.FieldType == typeof(InputActionReference))
                    {
                        // Check if the field name suggests it's an activate action
                        string fieldName = field.Name.ToLower();
                        if (fieldName.Contains("activate") || fieldName.Contains("trigger") || 
                            fieldName.Contains("select") || fieldName.Contains("use"))
                        {
                            try
                            {
                                var value = field.GetValue(property);
                                
                                // Handle InputActionProperty
                                if (value is InputActionProperty actionProperty && actionProperty.action != null)
                                {
                                    activateAction = actionProperty.action;
                                    return true;
                                }
                                
                                // Handle InputActionReference
                                if (value is InputActionReference actionReference && actionReference.action != null)
                                {
                                    activateAction = actionReference.action;
                                    return true;
                                }
                            }
                            catch (Exception)
                            {
                                // Ignore exceptions from reflection
                            }
                        }
                    }
                    
                    // Direct InputAction field
                    if (field.FieldType == typeof(InputAction))
                    {
                        string fieldName = field.Name.ToLower();
                        if (fieldName.Contains("activate") || fieldName.Contains("trigger") || 
                            fieldName.Contains("select") || fieldName.Contains("use"))
                        {
                            try
                            {
                                activateAction = field.GetValue(property) as InputAction;
                                if (activateAction != null)
                                    return true;
                            }
                            catch (Exception)
                            {
                                // Ignore exceptions from reflection
                            }
                        }
                    }
                }
            }
            
            // Option 2: Look for commonly named actions in action assets
            var playerInputs = new List<PlayerInput>();
            
            // Find all PlayerInput components in the hierarchy
            foreach (var component in properties)
            {
                var playerInput = component.GetComponent<PlayerInput>();
                if (playerInput != null)
                {
                    playerInputs.Add(playerInput);
                }
            }
            
            // Look through each PlayerInput's action maps
            foreach (var playerInput in playerInputs)
            {
                if (playerInput.actions == null)
                    continue;
                    
                foreach (var map in playerInput.actions.actionMaps)
                {
                    // Look for actions that are likely to be the activate action
                    var action = map.FindAction("activate", false) ?? 
                                map.FindAction("trigger", false) ?? 
                                map.FindAction("select", false);
                                
                    if (action != null)
                    {
                        activateAction = action;
                        return true;
                    }
                }
            }

            // Couldn't find an activate action
            return false;
        }

        void OnDeselect(SelectExitEventArgs args)
        {
            m_Animating = false;
            m_ToAnimate.Stop();
            m_ActivateAction = null;
        }
    }
}