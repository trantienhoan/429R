using UnityEngine;

namespace Game.VFX
{
    /// <summary>
    /// A one-shot effect that outlives what it's on: when it's switched on (e.g. a barrel's Wood_Barrel_Drop_Pos as the
    /// barrel breaks, just before the barrel is destroyed) it leaves its parent where it is, plays (its particles and sound
    /// play on awake) and is destroyed after Lifetime. Used by Barrel_Seal_Break.
    /// </summary>
    public class DetachOnEnable : MonoBehaviour
    {
        [Tooltip("Seconds it stays before it's destroyed.")]
        [Min(0.1f)]
        [SerializeField] private float lifetime = 2.5f;

        private void OnEnable()
        {
            if (transform.parent != null) transform.SetParent(null, true);
            Destroy(gameObject, lifetime);
        }
    }
}
