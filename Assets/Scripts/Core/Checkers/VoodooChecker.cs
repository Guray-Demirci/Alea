using Tavla.Core.Abilities;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Voodoo Checker. "When playing a die it may ignore the classic 'move forward' rule: it swaps places with
    /// another of your checkers that stands exactly the die's distance away."
    /// Forward it moves like any checker; backward, onto a friendly checker, it swaps (see GameState.Swaps.cs).
    /// </summary>
    public class VoodooChecker : SpecialChecker, ISwapMover
    {
        public const string Id = "checker_voodoo";

        public VoodooChecker(int ownerId) : base(ownerId, Id, "Voodoo Checker") { }
    }
}
