using Game.Leveling;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Player")]
    [Tooltip("A candy was eaten: the player gets a little stronger for this run (Player Level's candy points). Each stat " +
             "takes the full Points up to Candy Full Rate Points (5), then only Candy Slow Points a candy (0.1), at most " +
             "Candy Max Points (10), all bigger the more of their own points the player has in that stat; they come on " +
             "top of the level-up points, aren't saved and go when a new run starts. Put it in the candy's eat state " +
             "(Tools > 429 Game > Player > Give Candies Their Buffs does it for the 6 candies, one per stat).")]
    public class EatCandyBuff : FsmStateAction
    {
        // The numbers are saved in the candies' FSMs: never change one, only add new ones.
        public enum Boost
        {
            /// <summary>Toughness points: less damage and knockback.</summary>
            Toughness = 0,
            /// <summary>Move Speed points: faster walking.</summary>
            MoveSpeed = 1,
            /// <summary>Throw Power points: faster throws, bigger blasts.</summary>
            ThrowPower = 2,
            /// <summary>Fist Size points: a bigger fist, bigger and harder flying punches (never a new punch).</summary>
            FistSize = 3,
            /// <summary>Max Health points: a higher health cap, candy heals more.</summary>
            MaxHealth = 4,
            /// <summary>Weapon Efficiency points: weapons hit harder and wear slower.</summary>
            WeaponEfficiency = 5,
        }

        [Tooltip("The stat the candy makes better (user, 2026-11-10: one candy kind per stat): Toughness, Move Speed, " +
                 "Throw Power, Fist Size (it never unlocks a punch), Max Health or Weapon Efficiency.")]
        public Boost boost;

        [RequiredField]
        [Tooltip("Level-up points it adds before the player's own points in the stat make it bigger: 0.5 = half a " +
                 "level-up point.")]
        public FsmFloat points;

        public override void Reset()
        {
            boost = Boost.Toughness;
            points = 0.1f;
        }

        public override void OnEnter()
        {
            PlayerLevel.AddCandyPoints(StatOf(boost), points.Value);
            Finish();
        }

        /// <summary>The stat a boost adds points to.</summary>
        public static Stat StatOf(Boost b)
        {
            switch (b)
            {
                case Boost.MoveSpeed: return Stat.MoveSpeed;
                case Boost.ThrowPower: return Stat.ThrowPower;
                case Boost.FistSize: return Stat.FistSize;
                case Boost.MaxHealth: return Stat.MaxHealth;
                case Boost.WeaponEfficiency: return Stat.WeaponEfficiency;
                default: return Stat.Toughness;
            }
        }

        /// <summary>The boost that adds points to a stat (Toughness for one no candy gives).</summary>
        public static Boost BoostOf(Stat stat)
        {
            switch (stat)
            {
                case Stat.MoveSpeed: return Boost.MoveSpeed;
                case Stat.ThrowPower: return Boost.ThrowPower;
                case Stat.FistSize: return Boost.FistSize;
                case Stat.MaxHealth: return Boost.MaxHealth;
                case Stat.WeaponEfficiency: return Boost.WeaponEfficiency;
                default: return Boost.Toughness;
            }
        }
    }
}
