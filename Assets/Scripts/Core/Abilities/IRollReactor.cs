using Tavla.Core.Runtime;

namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Checkers that react to their owner's dice roll while they stand on the board.
    /// Kraken Checker: "If you roll a double, the Kraken wakes up and every open enemy checker is captured."
    /// Raised once per roll, after the relics and the pinned die have shaped it.
    /// </summary>
    public interface IRollReactor : ICheckerAbility
    {
        void OnDiceRolled(IMatchContext context, DiceRoll roll);
    }
}
