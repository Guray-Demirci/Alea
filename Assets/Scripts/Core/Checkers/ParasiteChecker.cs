using Tavla.Core.Abilities;
using Tavla.Core.Board;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Parasite Checker. "Can sneak onto a point the opponent has made (a point with at least 2 of their checkers)."
    /// No capture happens during infiltration; it shares the point with the opponent.
    /// This checker is exactly why the Node class supports multiple owners.
    /// It still obeys <see cref="GameState.StackLimit"/>: a point already holding 5 checkers has no room for it.
    /// </summary>
    public class ParasiteChecker : SpecialChecker, IInfiltrator
    {
        public const string Id = "checker_parasite";

        /// <summary>Infiltrates only a real MADE POINT (2+ enemy checkers). A single blot is captured by the normal rules.</summary>
        public bool CanInfiltrate(Node target) => target.CountEnemiesOf(OwnerId) >= 2;

        public ParasiteChecker(int ownerId) : base(ownerId, Id, "Parasite Checker") { }
    }
}
