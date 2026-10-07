using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// How much harder this weapon hits than a bare swing: its hit speed is multiplied by Power before the damage is
    /// worked out, so a hammer (2) turns a normal swing into a big hit. Anything without it counts as 1.
    /// Goes on the weapon's root (the object with the Rigidbody).
    /// </summary>
    [DisallowMultipleComponent]
    public class WeaponPower : MonoBehaviour
    {
        [Tooltip("Multiplies this weapon's hit speed. 1 = a bare swing, 2 = twice as hard, below 1 = softer (e.g. a pillow).")]
        [Min(0f)]
        [SerializeField] private float power = 1f;

        public float Power
        {
            get => power;
            set => power = Mathf.Max(0f, value);
        }

        /// <summary>The Power of whatever made this collision, or 1 if it has none.</summary>
        public static float Of(Collision collision)
        {
            WeaponPower weapon = null;
            if (collision.rigidbody != null) collision.rigidbody.TryGetComponent(out weapon);
            if (weapon == null && collision.collider != null) weapon = collision.collider.GetComponentInParent<WeaponPower>();
            return weapon != null ? weapon.power : 1f;
        }
    }
}
