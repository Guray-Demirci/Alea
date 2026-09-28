using Tavla.Core.Runtime;

namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Greedy Checker: "Produces extra gold for EVERY TURN it stays alone (as a blot)."
    /// Called at the end of each turn for every checker on the board; the checker queries its own state through the context.
    /// </summary>
    public interface ITurnEndReactor : ICheckerAbility
    {
        void OnTurnEnded(IMatchContext context, int nodeIndex);
    }
}
