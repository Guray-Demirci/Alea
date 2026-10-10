namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Teleporter Checker: "It may be played to any point regardless of the dice, but you lose your other moves."
    /// When neither a single die nor a combination of dice reaches the point, the move is paid with EVERY die
    /// left. It still moves forward and still obeys the destination rules (blockades, tiles, capacity).
    /// </summary>
    public interface IFreeMover : ICheckerAbility
    {
    }
}
