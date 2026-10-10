using Tavla.Core.Board;
using Tavla.Core.Runtime;

namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Checkers that react when THEY are borne off. Golden Checker: "bearing it off gives the player 2 gold".
    ///
    /// Bearing off is not a move onto a point, so <see cref="IMoveReactor"/> is not raised for it: point-based
    /// reactions such as the Clover's strike would mean nothing off the board.
    /// Checkers that care about leaving the board implement this hook instead.
    /// </summary>
    public interface IBearOffReactor : ICheckerAbility
    {
        /// <param name="result">The bear-off; <see cref="MoveResult.FromIndex"/> is the point it left from.</param>
        void OnBorneOff(IMatchContext context, MoveResult result);
    }
}
