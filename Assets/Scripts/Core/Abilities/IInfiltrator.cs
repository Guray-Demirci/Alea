using Tavla.Core.Board;

namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Parasite Checker: "Can sneak onto a point the opponent has made."
    /// Difference from <see cref="IBlockadeBypass"/>: the Ghost passes the point by, the Parasite
    /// SETTLES on it and shares the point with the opponent. This is why a Node can have two owners.
    /// </summary>
    public interface IInfiltrator : ICheckerAbility
    {
        /// <summary>Can it infiltrate the enemy point? No capture happens during infiltration.</summary>
        bool CanInfiltrate(Node target);
    }
}
