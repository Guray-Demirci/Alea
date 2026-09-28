using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Interfaces;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Parasite Checker. "Can sneak onto a point the opponent has made (a point with at least 2 of their checkers)."
    /// No capture happens during infiltration; it shares the point with the opponent.
    /// This checker is exactly why the Node class supports multiple owners.
    /// </summary>
    public class ParasiteChecker : BaseChecker, IInfiltrator, IPermanent
    {
        public const string Id = "checker_parasite";

        public override string TypeId => Id;

        /// <summary>Infiltrates only a real MADE POINT (2+ enemy checkers). A single blot is captured by the normal rules.</summary>
        public bool CanInfiltrate(Node target) => target.CountEnemiesOf(OwnerId) >= 2;

        public string ItemId => Id;
        public string DisplayName => "Parasite Checker";
        public int Price => 420;

        /// <summary>More than one copy of this checker can be put in the deck.</summary>
        public bool AllowsDuplicates => true;

        public ParasiteChecker(int ownerId) : base(ownerId) { }
    }
}
