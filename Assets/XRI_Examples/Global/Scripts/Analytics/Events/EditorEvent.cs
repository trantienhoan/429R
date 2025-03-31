using UnityEngine.Analytics;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if DEBUG_XRC_EDITOR_ANALYTICS
using UnityEngine;
#endif

namespace UnityEngine.XR.Content.Interaction.Analytics
{
    /// <summary>
    /// Base class for <c>XRContent</c> editor events.
    /// </summary>
    abstract class EditorEvent
    {
        protected const int k_DefaultMaxEventsPerHour = 1000;
        protected const int k_DefaultMaxElementCount = 1000;

        /// <summary>
        /// The event name determines which database table it goes into in the CDP backend.
        /// All events which we want grouped into a table must share the same event name.
        /// </summary>
        readonly string m_EventName;

        readonly int m_MaxEventsPerHour;
        readonly int m_MaxElementCount;

        internal EditorEvent(string eventName, int maxPerHour = k_DefaultMaxEventsPerHour, int maxElementCount = k_DefaultMaxElementCount)
        {
            m_EventName = eventName;
            m_MaxEventsPerHour = maxPerHour;
            m_MaxElementCount = maxElementCount;
        }

        /// <summary>
        /// Call this method in the child classes to send an event.
        /// </summary>
        /// <param name="parameter">The parameter object within the event.</param>
        /// <returns>Returns whenever the event was successfully sent.</returns>
        protected bool Send(object parameter)
        {
#if ENABLE_CLOUD_SERVICES_ANALYTICS
            // Analytics events will always refuse to send if analytics are disabled or the editor is for sure quitting
            if (XrcAnalytics.disabled || XrcAnalytics.quitting)
                return false;

#if UNITY_EDITOR
            try
            {
                SendAnalyticsEvent(m_EventName, parameter);
                
#if DEBUG_XRC_EDITOR_ANALYTICS
                Debug.Log($"Event {m_EventName} sent");
#endif
                return true;
            }
            catch
            {
#if DEBUG_XRC_EDITOR_ANALYTICS
                Debug.LogError($"Failed to send analytics event {m_EventName}");
#endif
                return false;
            }
#else
            return false;
#endif
#else // ENABLE_CLOUD_SERVICES_ANALYTICS
            return false;
#endif
        }

#if UNITY_EDITOR && ENABLE_CLOUD_SERVICES_ANALYTICS
        // Note: The following methods use deprecated Unity Editor Analytics APIs.
        // TODO: Update to new Unity Gaming Services Analytics when project is configured appropriately.
        private void SendAnalyticsEvent(string eventName, object parameters)
        {
            // Convert the parameter object to dictionary for better compatibility
            var customParams = new Dictionary<string, object>();
            
            if (parameters != null)
            {
                foreach (var property in parameters.GetType().GetProperties())
                {
                    customParams[property.Name] = property.GetValue(parameters);
                }
            }

            // Using deprecated API with a suppression pragma to silence the warning
#pragma warning disable CS0618 // Type or member is obsolete
            EditorAnalytics.SendEventWithLimit(eventName, customParams);
#pragma warning restore CS0618
        }
#endif

        internal bool Register()
        {
#if UNITY_EDITOR && ENABLE_CLOUD_SERVICES_ANALYTICS
            // Using deprecated API with a suppression pragma to silence the warning
#pragma warning disable CS0618 // Type or member is obsolete
            return EditorAnalytics.RegisterEventWithLimit(m_EventName, m_MaxEventsPerHour, m_MaxElementCount, XrcAnalytics.k_VendorKey) == AnalyticsResult.Ok;
#pragma warning restore CS0618
#else
            return false;
#endif
        }
    }
}