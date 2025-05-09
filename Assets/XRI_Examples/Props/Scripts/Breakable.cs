using System;
using UnityEngine.Events;

namespace UnityEngine.XR.Content.Interaction
{
    /// <summary>
    /// Detects a collision with a tagged collider, replacing this object with a 'broken' version
    /// </summary>
    public class Breakable : MonoBehaviour
    {
        [Serializable] public class BreakEvent : UnityEvent<GameObject, GameObject> { }

        [SerializeField]
        [Tooltip("The 'broken' version of this object.")]
        GameObject m_BrokenVersion;

        [SerializeField]
        [Tooltip("The tag a collider must have to cause this object to break.")]
        string m_ColliderTag = "Destroyer";

        [SerializeField]
        [Tooltip("Events to fire when a matching object collides and break this object. " +
            "The first parameter is the colliding object, the second parameter is the 'broken' version.")]
        BreakEvent m_OnBreak = new BreakEvent();

        [SerializeField]
        [Tooltip("Particle system to play when the object breaks.")]
        ParticleSystem m_BreakParticleSystem;


        bool m_Destroyed = false;

        /// <summary>
        /// Events to fire when a atmching object collides and break this object.
        /// The first parameter is the colliding object, the second parameter is the 'broken' version.
        /// </summary>
        public BreakEvent onBreak => m_OnBreak;

        void OnCollisionEnter(Collision collision)
        {
            if (m_Destroyed)
                return;

            //if (collision.gameObject.tag.Equals(m_ColliderTag, System.StringComparison.InvariantCultureIgnoreCase))
            if (collision.gameObject.CompareTag(m_ColliderTag))
            {
                m_Destroyed = true;
                //var brokenVersion = Instantiate(m_BrokenVersion, transform.position, transform.rotation);
                GameObject brokenVersion = null; // Default to null if no broken version is assigned

                if (m_BrokenVersion != null)
                {
                    brokenVersion = Instantiate(m_BrokenVersion, transform.position, transform.rotation);
                }

                if (m_BreakParticleSystem != null)
                {
                    ParticleSystem psInstance = Instantiate(m_BreakParticleSystem, transform.position, transform.rotation);
                    psInstance.Play();
                    Destroy(psInstance.gameObject, psInstance.main.duration);
                }
                m_OnBreak.Invoke(collision.gameObject, brokenVersion);
                Destroy(gameObject);
            }
        }
    }
}
