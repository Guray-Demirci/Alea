using Tavla.Core.Board;

namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Parasite Checker: "Can sneak onto a point the opponent has made."
    /// The Parasite SETTLES on the point and shares it with the opponent. This is why a Node can have two owners.
    /// </summary>
    public interface IInfiltrator : ICheckerAbility
    {
        /// <summary>Can it infiltrate the enemy point? No capture happens during infiltration.</summary>
        bool CanInfiltrate(Node target);
    }
}
