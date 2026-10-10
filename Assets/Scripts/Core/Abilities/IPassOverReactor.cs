using Tavla.Core.Checkers;
using Tavla.Core.Runtime;

namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Bandit Checker: "Earns +1 gold for every enemy checker that passes over it (even when jumping over)."
    /// Classic backgammon never inspects the points in between; this ability adds a
    /// pass-over scan step to GameState.
    /// </summary>
    public interface IPassOverReactor : ICheckerAbility
    {
        void OnEnemyPassedOver(IMatchContext context, BaseChecker passingChecker, int nodeIndex);
    }
}
