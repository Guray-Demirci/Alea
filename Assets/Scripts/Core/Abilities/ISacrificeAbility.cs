using Tavla.Core.Runtime;

namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Checkers whose ability is used by removing them from the board for good.
    /// Second Chance Checker: "It does nothing on the board, but at any moment of your turn you can sacrifice it
    /// to gain an extra move."
    /// The sacrifice itself — leaving play, the events, the end of the match — is GameState's
    /// (<see cref="Board.GameState.TrySacrificeChecker"/>); the ability only says what it buys.
    /// It is for good: in a run the checker also leaves the player's deck (<see cref="Run.RunState.Deck"/>).
    /// </summary>
    public interface ISacrificeAbility : ICheckerAbility
    {
        /// <summary>Called right after the checker has left the board.</summary>
        void OnSacrificed(IMatchContext context, int nodeIndex);
    }
}
