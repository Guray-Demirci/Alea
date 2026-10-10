namespace Tavla.Core.Abilities
{
    /// <summary>
    /// Voodoo Checker: "It may ignore the 'only forward' rule: it swaps places with one of your own checkers
    /// that stands exactly a die's distance away."
    /// Forward, an ordinary move already lands on a friendly checker, so the swap is the BACKWARD move:
    /// the Voodoo goes back by the die and the partner comes forward to the Voodoo's old point.
    /// </summary>
    public interface ISwapMover : ICheckerAbility
    {
    }
}
