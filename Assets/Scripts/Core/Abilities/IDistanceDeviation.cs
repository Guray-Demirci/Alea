using Tavla.Core.Runtime;

namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Drunken Checker: "When you move it, it travels one point less or one point more than the die, at random."
    /// The die is chosen and validated as usual; only when the move is carried out does GameState ask for the
    /// stagger and try the point next to the chosen one. If the checker cannot land there, it lands where aimed.
    /// </summary>
    public interface IDistanceDeviation : ICheckerAbility
    {
        /// <summary>Points added to the chosen distance: -1 or +1 (0 = no stagger this time).</summary>
        int RollDeviation(IRandomProvider random);
    }
}
