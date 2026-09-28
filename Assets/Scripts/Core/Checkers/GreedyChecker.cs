using Tavla.Core.Abilities;
using Tavla.Core.Board;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Checkers
{
    /// <summary>
    /// Greedy Checker. "Produces extra gold for EVERY TURN it stays alone (as a blot) on the board.
    /// Risk: the opponent can hit it at any moment."
    /// It does not know its own position; GameState tells it which point it is on at the end of the turn.
    /// </summary>
    public class GreedyChecker : BaseChecker, ITurnEndReactor, IPermanent
    {
        public const string Id = "checker_greedy";
        private const int GoldPerTurn = 3;

        public override string TypeId => Id;

        public void OnTurnEnded(IMatchContext context, int nodeIndex)
        {
            Node node = context.Board.Nodes[nodeIndex];

            // Being a "blot" = standing completely alone on the point. Production stops once someone joins it.
            if (node.CheckerCount != 1)
                return;

            context.AddGold(OwnerId, GoldPerTurn);
            context.Board.ReportAbility(AbilityIds.GreedyIncome, this, nodeIndex);
        }

        public string ItemId => Id;
        public string DisplayName => "Greedy Checker";
        public int Price => 260;

        /// <summary>More than one copy of this checker can be put in the deck.</summary>
        public bool AllowsDuplicates => true;

        public GreedyChecker(int ownerId) : base(ownerId) { }
    }
}
