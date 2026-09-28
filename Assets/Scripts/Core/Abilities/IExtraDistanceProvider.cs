using Tavla.Core.Board;

namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Nomad Checker: "If the points to the left and right of its current point are completely empty,
    /// it may move 3 extra points."
    /// The bonus is ADDED to the die value during distance validation; it does not grant an extra move.
    /// </summary>
    public interface IExtraDistanceProvider : ICheckerAbility
    {
        /// <summary>Extra points this checker earns from its current position (may be 0).</summary>
        int GetBonusDistance(GameState state, int fromIndex);
    }
}
