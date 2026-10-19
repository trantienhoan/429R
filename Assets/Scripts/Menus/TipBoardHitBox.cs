using UnityEngine;

namespace Game.Menus
{
    /// <summary>
    /// The tip board's own thin hit box (a box collider on a still, kinematic body over the board's face), so things
    /// that hit the board count, not only things that hit the knight. The board is moved off the knight when the game
    /// starts, so its hits don't reach the knight's Tip Board by themselves: this passes them on (the Tip Board sets
    /// Owner). Tools > 429 Game > Menu > Set Up Knight Tip Board adds it, on the "Hit Box" under the board.
    /// </summary>
    [DisallowMultipleComponent]
    public class TipBoardHitBox : MonoBehaviour
    {
        /// <summary>The Tip Board the hits go to.</summary>
        public TipBoard Owner { get; set; }

        private void OnCollisionEnter(Collision collision)
        {
            if (Owner != null) Owner.Hit(collision);
        }
    }
}
