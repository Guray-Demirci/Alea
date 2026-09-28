using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Interfaces;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Nomad Checker. "If the points to the left and right of its current point are completely empty,
    /// it may move 3 extra points."
    /// The bonus is added to the DIE VALUE and grants no extra move: with a 3 it travels 6 points.
    /// </summary>
    public class NomadChecker : BaseChecker, IExtraDistanceProvider, IPermanent
    {
        public const string Id = "checker_nomad";
        private const int BonusDistance = 3;

        public override string TypeId => Id;

        public int GetBonusDistance(GameState state, int fromIndex)
        {
            // Entering from the bar has no "neighbouring point" -> no bonus.
            if (!GameState.IsOnBoard(fromIndex))
                return 0;

            return IsSideEmpty(state, fromIndex - 1) && IsSideEmpty(state, fromIndex + 1)
                ? BonusDistance
                : 0;
        }

        /// <summary>The board edge also counts as "empty" — no checker can block from there.</summary>
        private static bool IsSideEmpty(GameState state, int index)
        {
            return !GameState.IsOnBoard(index) || state.Nodes[index].IsEmpty;
        }

        public string ItemId => Id;
        public string DisplayName => "Nomad Checker";
        public int Price => 300;

        /// <summary>More than one copy of this checker can be put in the deck.</summary>
        public bool AllowsDuplicates => true;

        public NomadChecker(int ownerId) : base(ownerId) { }
    }
}
