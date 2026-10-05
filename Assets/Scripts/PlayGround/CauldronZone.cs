using UnityEngine;

namespace Game.PlayGround
{
    /// <summary>One of a cauldron's invisible zones (its opening, or the fire under it); tells the cauldron what enters it.</summary>
    [DisallowMultipleComponent]
    public class CauldronZone : MonoBehaviour
    {
        private Cauldron owner;
        private Cauldron.Zone kind;

        public void Setup(Cauldron cauldron, Cauldron.Zone zone)
        {
            owner = cauldron;
            kind = zone;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (owner != null) owner.OnZoneEnter(kind, other);
        }
    }
}
