using UnityEngine;
using Core; // for HealthComponent

namespace Enemies
{
    /// <summary>
    /// Add this to any scene object to make it a ShadowMonster spawn point.
    /// If the object has a HealthComponent and dies (or is disabled/destroyed),
    /// the point becomes unavailable and notifies listeners.
    /// </summary>
    [DisallowMultipleComponent]
    public class SpawnPoint : MonoBehaviour
    {
        [Tooltip("Optional: override the exact spawn anchor (defaults to this transform).")]
        public Transform overrideAnchor;

        [Tooltip("Radius for NavMesh sampling around this point.")]
        public float navmeshSampleRadius = 6f;

        public event System.Action<SpawnPoint> OnUnavailable;

        private HealthComponent _health;

        private void Awake()
        {
            // Subscribe to death if a HealthComponent exists on this object or a parent.
            _health = GetComponent<HealthComponent>() ?? GetComponentInParent<HealthComponent>();
            if (_health != null)
                _health.OnDeath.AddListener(OnOwnerDeath);
        }

        private void OnDestroy()
        {
            if (_health != null)
                _health.OnDeath.RemoveListener(OnOwnerDeath);

            // If the point itself is destroyed, report unavailable.
            OnUnavailable?.Invoke(this);
        }

        private void OnDisable()
        {
            // Consider disabled spawn points as unavailable as well.
            OnUnavailable?.Invoke(this);
        }

        private void OnOwnerDeath(HealthComponent _)
        {
            OnUnavailable?.Invoke(this);
        }

        public Vector3 Position => (overrideAnchor ? overrideAnchor.position : transform.position);
    }
}