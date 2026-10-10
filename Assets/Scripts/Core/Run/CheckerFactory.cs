using Tavla.Core.Checkers;

namespace Tavla.Core.Run
{
    /// <summary>
    /// One entry of the player's deck: creates that checker for the given owner. Called once per match and must
    /// return a NEW checker each time, because checkers keep per-match state (the Phoenix's rebirth count,
    /// shield charges).
    /// </summary>
    public delegate BaseChecker CheckerFactory(int ownerId);
}
