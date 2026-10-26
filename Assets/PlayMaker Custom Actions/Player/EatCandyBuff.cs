using Game.Leveling;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Player")]
    [Tooltip("A candy was eaten: the player gets a little stronger for this run (Player Level's candy points). Each stat " +
             "takes at most Candy Max Points from candies (10); they come on top of the level-up points, aren't saved and " +
             "go when a new run starts. Put it in the candy's eat state (Tools > 429 Game > Player > Give Candies Their " +
             "Buffs does it for Candy_1..8).")]
    public class EatCandyBuff : FsmStateAction
    {
        public enum Boost
        {
            /// <summary>Toughness points: less damage and knockback.</summary>
            Toughness,
            /// <summary>Move Speed points: faster walking.</summary>
            MoveSpeed,
            /// <summary>Throw Power points: faster throws.</summary>
            ThrowPower,
            /// <summary>Fist Size's punch damage only: harder punches, no bigger fist and no new punches.</summary>
            PunchDamage,
        }

        [Tooltip("What the candy makes better: Toughness (less damage and knockback), Move Speed (faster walking), Throw " +
                 "Power (faster throws) or Punch Damage (harder punches; the fist doesn't grow).")]
        public Boost boost;

        [RequiredField]
        [Tooltip("Level-up points it adds: 0.5 = half a level-up point.")]
        public FsmFloat points;

        public override void Reset()
        {
            boost = Boost.Toughness;
            points = 0.5f;
        }

        public override void OnEnter()
        {
            PlayerLevel.AddCandyPoints(StatOf(boost), points.Value);
            Finish();
        }

        private static Stat StatOf(Boost b)
        {
            switch (b)
            {
                case Boost.MoveSpeed: return Stat.MoveSpeed;
                case Boost.ThrowPower: return Stat.ThrowPower;
                case Boost.PunchDamage: return Stat.FistSize;
                default: return Stat.Toughness;
            }
        }
    }
}
