using UnityEngine;

namespace Game.Locomotion
{
    /// <summary>
    /// Put on a held thing by Held Pass Through while it's held: hands its touches to it (how hard it's driven into
    /// something heavier, for the weapon's strain).
    /// </summary>
    [DisallowMultipleComponent]
    public class StrainSensor : MonoBehaviour
    {
        private HeldPassThrough owner;
        private Rigidbody body;
        // The fastest it was driven into something heavier during the last physics step, in metres per second.
        internal float step;

        internal void Begin(HeldPassThrough by)
        {
            owner = by;
            body = GetComponent<Rigidbody>();
            step = 0f;
        }

        internal void End()
        {
            owner = null;
            step = 0f;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (owner != null) owner.Touched(body, collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            if (owner != null) owner.Touched(body, collision);
        }
    }
}
