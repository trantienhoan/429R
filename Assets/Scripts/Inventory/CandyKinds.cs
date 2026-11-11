using System.Collections.Generic;
using Game.Leveling;

namespace Game.Inventory
{
    /// <summary>
    /// The candy kinds in the game (user, 2026-11-10: six, one for each level-up stat; Candy_3 and Candy_6 are out). The
    /// setup menus build their candy lists from here, so a menu run again never brings a retired candy back.
    /// - Candy_1 "Pumpkin Candy": Max Health
    /// - Candy_2: Weapon Efficiency
    /// - Candy_4: Move Speed
    /// - Candy_5 "Marshmallow": Toughness
    /// - Candy_7: Throw Power
    /// - Candy_8: Fist Size
    /// Their colours (and the slimes that drop them) are in Candy Colours.
    /// </summary>
    public static class CandyKinds
    {
        public const string Folder = "Assets/Prefabs/Items/Breakable/";

        /// <summary>The candies in the game, by number (Candy_N), in order.</summary>
        public static readonly int[] Numbers = { 1, 2, 4, 5, 7, 8 };

        /// <summary>Candies taken out of the game (their prefabs stay, nothing spawns them).</summary>
        public static readonly int[] Retired = { 3, 6 };

        /// <summary>The asset path of Candy_N.</summary>
        public static string PathOf(int number) => Folder + "Candy_" + number + ".prefab";

        /// <summary>The asset paths of the candies in the game, in order.</summary>
        public static IEnumerable<string> Paths
        {
            get
            {
                foreach (int n in Numbers) yield return PathOf(n);
            }
        }

        /// <summary>Whether Candy_N is in the game.</summary>
        public static bool InGame(int number) => System.Array.IndexOf(Numbers, number) >= 0;

        /// <summary>The stat Candy_N gives points to (Toughness for a retired one).</summary>
        public static Stat StatOf(int number)
        {
            switch (number)
            {
                case 1: return Stat.MaxHealth;
                case 2: return Stat.WeaponEfficiency;
                case 4: return Stat.MoveSpeed;
                case 7: return Stat.ThrowPower;
                case 8: return Stat.FistSize;
                default: return Stat.Toughness;
            }
        }
    }
}
