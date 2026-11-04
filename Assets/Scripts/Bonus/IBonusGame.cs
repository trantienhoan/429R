namespace Game.Bonus
{
    /// <summary>A bonus round the Play Bonus Round action can run (Bonus Round, Paint Bees Round): it says when it's over.</summary>
    public interface IBonusGame
    {
        /// <summary>The round is over and its candies are eaten: Play Bonus Round goes on.</summary>
        bool Finished { get; }
    }
}
