namespace Game.Enemies
{
    /// <summary>
    /// Something a monster can be sent to bite instead of the player (Spider Brain's Go For), with its health kept in C#:
    /// each bite that lands calls Bitten (the PlayGround's cauldron, user 2026-11-16). On the object a Spider Brain goes
    /// for, or on one of its parents. Without one, a bite goes to the object's FSM as before (the Pumpkin Tree).
    /// </summary>
    public interface IBiteTarget
    {
        /// <summary>A bite from this monster has landed.</summary>
        void Bitten(SpiderBrain by);
    }
}
