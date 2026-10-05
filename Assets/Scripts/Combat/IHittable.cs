using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Something that reacts to being hit in its own script rather than through a PlayMaker Health FSM, e.g. the
    /// PlayGround's ghost tree and bush. Grenade blasts call it too.
    /// </summary>
    public interface IHittable
    {
        /// <summary>
        /// Hit with this strength at this point: a grenade passes its damage, a thrown or swung thing its speed
        /// (metres per second) divided by 4, so a hard hit is about as strong as a grenade's 3.
        /// </summary>
        void Hit(float strength, Vector3 point);
    }
}
