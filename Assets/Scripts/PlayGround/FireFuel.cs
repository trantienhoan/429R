using UnityEngine;

namespace Game.PlayGround
{
    /// <summary>
    /// Firewood for the cauldron: thrown into the fire under it, it burns up, the fire roars and the cooking goes
    /// faster for a while. Goes on Dropped_Wood_Fuel.
    /// </summary>
    [DisallowMultipleComponent]
    public class FireFuel : MonoBehaviour
    {
        [Tooltip("How much heat it adds. Each point of heat makes cooking go one time faster again (2 = three times as fast).")]
        [Min(0f)]
        [SerializeField] private float heat = 1f;

        public float Heat => heat;
    }
}
