using Tavla.Core.Board;

namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Nomad Checker: "If the points to the left and right of the point a die brings it to are empty,
    /// it may move 3 extra points."
    /// The bonus is ADDED to the die value during distance validation — for bearing off as well — and it does not
    /// grant an extra move. When it can pay its part, it does: a smaller die plus the bonus is spent rather than
    /// a die covering the whole distance.
    /// </summary>
    public interface IExtraDistanceProvider : ICheckerAbility
    {
        /// <summary>
        /// Extra points this checker earns once a die has brought it to <paramref name="dieTargetIndex"/> (may be 0).
        /// Asked before the move: the checker still stands on its source point. Never on the bar: a captured checker
        /// enters as a plain one.
        /// </summary>
        int GetBonusDistance(GameState state, int dieTargetIndex);
    }
}
