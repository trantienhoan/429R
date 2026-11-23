using UnityEngine;

namespace Game.Bonus
{
    /// <summary>
    /// Put by the whack-a-pumpkin round (WhackRound) on the bonus chest's Seed_Pos in place of its FSM (which would make
    /// the nesting giant): the chest switches Seed_Pos on as it breaks open, and this tells the round where.
    /// </summary>
    public class WhackChestSeed : MonoBehaviour
    {
        public WhackRound Round { get; set; }

        private void OnEnable()
        {
            if (Round != null) Round.ChestOpened(transform.position);
        }
    }
}
