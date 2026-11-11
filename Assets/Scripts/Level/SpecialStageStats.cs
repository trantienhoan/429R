using UnityEngine;

namespace Game.Leveling
{
    /// <summary>
    /// While this is on, the player plays a special stage with level-0 stats (user, 2026-11-10; see Player Level's Enter
    /// Special Stage): their own points and eaten candies count for nothing and the health cap drops to the level-0 one,
    /// while the health itself, boong buffs and lucky coins stay. Everything comes back when it goes (switched off, or
    /// destroyed with its stage). Added by code only, so re-running a stage's setup menu can't drop it: Play Bonus Round
    /// puts it on the round it makes (Special Stage Types 1 and 2: the nesting pumpkins, Paint Bees) and Daruma Stage on
    /// itself while it has the player (Type 3).
    /// </summary>
    public class SpecialStageStats : MonoBehaviour
    {
        private void OnEnable() => PlayerLevel.EnterSpecialStage(this);

        private void OnDisable() => PlayerLevel.ExitSpecialStage(this);
    }
}
