namespace Game.Bonus
{
    /// <summary>
    /// A bonus round Play Bonus Round can run (the nesting pumpkins' Bonus Round, the Playroom's Paint Bees Round): it
    /// starts by itself where it's made, and says when it's over.
    /// </summary>
    public interface IBonusGame
    {
        /// <summary>The round is over and its candies eaten: Play Bonus Round goes on.</summary>
        bool Finished { get; }
    }
}
